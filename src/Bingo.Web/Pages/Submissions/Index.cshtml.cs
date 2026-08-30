using Bingo.Application.Evidence;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Captain;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Submissions;

[Authorize]
public sealed class IndexModel(ApplicationDbContext db, IEvidenceAuthority evidenceAuthority, TimeProvider time) : PageModel
{
    public string EventName { get; private set; } = string.Empty;
    public string TeamName { get; private set; } = string.Empty;
    public string EventSlug { get; private set; } = string.Empty;
    public string TeamSlug { get; private set; } = string.Empty;
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public IReadOnlyList<Captain.IndexModel.PlayerOption> PlayerOptions { get; private set; } = [];
    public IReadOnlyList<Captain.IndexModel.SubmissionView> Submissions { get; private set; } = [];
    public int PageNumber { get; private set; }
    public int TotalPages { get; private set; }
    public int TotalSubmissionCount { get; private set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "player")]
    public Guid? PlayerFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "ledgerPage")]
    public int RequestedLedgerPage { get; set; } = 1;

    public SubmissionLedgerViewModel Ledger => new(EventId, TeamId, "/Submissions/Index", "/Submissions/Submission", Submissions, PageNumber, TotalPages, TotalSubmissionCount, Search, PlayerFilter, EventTimezone);

    public async Task<IActionResult> OnGetAsync(Guid? eventId, Guid? teamId, CancellationToken cancellationToken)
    {
        var scope = await ResolveMemberAsync(eventId, teamId, cancellationToken);
        if (scope is null || !await LoadAsync(scope, cancellationToken)) return NotFound();
        ViewData["BodyClass"] = "public-ui-pass2";
        ViewData["CompactNavigation"] = true;
        return Page();
    }

    public async Task<IActionResult> OnGetLedgerAsync(Guid? eventId, Guid? teamId, string? search, Guid? player, int ledgerPage = 1, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveMemberAsync(eventId, teamId, cancellationToken);
        if (scope is null) return NotFound();
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(value => value.EventId == scope.EventId && value.State == BoardState.Published, cancellationToken);
        if (board is null) return NotFound();
        EventId = scope.EventId;
        TeamId = scope.TeamId;
        Search = search;
        PlayerFilter = player;
        RequestedLedgerPage = ledgerPage;
        await LoadLedgerAsync(scope, board, true, cancellationToken);
        return Partial("/Pages/Captain/_SubmissionLedger.cshtml", Ledger);
    }

    private async Task<EvidenceActorScope?> ResolveMemberAsync(Guid? requestedEventId, Guid? requestedTeamId, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is not { } value) return null;
        try
        {
            var scope = await evidenceAuthority.ResolveActorAsync(value, requestedEventId, requestedTeamId, time.GetUtcNow(), cancellationToken);
            return scope.Kind is EvidenceActorKind.Participant or EvidenceActorKind.Captain ? scope : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<bool> LoadAsync(EvidenceActorScope scope, CancellationToken cancellationToken)
    {
        EventId = scope.EventId;
        TeamId = scope.TeamId;
        var context = await (from eventItem in db.Events.AsNoTracking()
                             join team in db.Teams.AsNoTracking() on eventItem.Id equals team.EventId
                             where eventItem.Id == scope.EventId && team.Id == scope.TeamId && team.Active
                             select new { eventItem.Name, eventItem.Slug, eventItem.Timezone, TeamName = team.Name, TeamSlug = team.Slug }).SingleOrDefaultAsync(cancellationToken);
        if (context is null) return false;
        EventName = context.Name;
        EventTimezone = context.Timezone;
        EventSlug = context.Slug;
        TeamName = context.TeamName;
        TeamSlug = context.TeamSlug;
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(value => value.EventId == scope.EventId && value.State == BoardState.Published, cancellationToken);
        if (board is null) return false;
        await LoadLedgerAsync(scope, board, true, cancellationToken);
        return true;
    }

    private async Task LoadLedgerAsync(EvidenceActorScope scope, Board board, bool includePlayerOptions, CancellationToken cancellationToken)
    {
        var replacedIds = db.Submissions.AsNoTracking()
            .Where(value => value.EventId == scope.EventId && value.TeamId == scope.TeamId && value.ResubmissionOfSubmissionId != null)
            .Select(value => value.ResubmissionOfSubmissionId!.Value);
        var submissions = db.Submissions.AsNoTracking().Where(value => value.EventId == scope.EventId && value.TeamId == scope.TeamId);
        if (PlayerFilter is { } playerId) submissions = submissions.Where(value => value.CreditedParticipantId == playerId);
        var ledger = from submission in submissions
                     join tile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals tile.Id
                     join drop in db.BoardRequirementDropSnapshots.AsNoTracking() on submission.DropSnapshotId equals drop.Id into dropGroup
                     from drop in dropGroup.DefaultIfEmpty()
                     where tile.BoardId == board.Id
                     select new { Submission = submission, Tile = tile.NameSnapshot, Drop = drop == null ? null : drop.ItemName, IsReplaced = replacedIds.Contains(submission.Id) };
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var pattern = $"%{Search.Trim()}%";
            ledger = ledger.Where(value => (value.Drop != null && EF.Functions.ILike(value.Drop, pattern)) || EF.Functions.ILike(value.Tile, pattern) || EF.Functions.ILike(value.Submission.CreditedCharacterName, pattern));
        }
        TotalSubmissionCount = await ledger.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalSubmissionCount / (double)Captain.IndexModel.PageSize));
        PageNumber = Math.Clamp(RequestedLedgerPage, 1, TotalPages);
        Submissions = await ledger.OrderByDescending(value => value.Submission.SubmittedAt).ThenByDescending(value => value.Submission.Id)
            .Skip((PageNumber - 1) * Captain.IndexModel.PageSize).Take(Captain.IndexModel.PageSize)
            .Select(value => new Captain.IndexModel.SubmissionView(value.Submission.Id, value.Drop, value.Tile, value.Submission.CreditedCharacterName,
                value.IsReplaced ? "Replaced" : value.Submission.Status.ToString(), value.Submission.ClaimedWeight, value.Submission.ApprovedContribution,
                value.Submission.SubmittedAt, value.Submission.CurrentReviewerNote != null)).ToListAsync(cancellationToken);
        if (includePlayerOptions)
            PlayerOptions = await db.Submissions.AsNoTracking().Where(value => value.EventId == scope.EventId && value.TeamId == scope.TeamId)
                .Select(value => new { Id = value.CreditedParticipantId, Name = value.CreditedCharacterName }).Distinct().OrderBy(value => value.Name).ThenBy(value => value.Id)
                .Select(value => new Captain.IndexModel.PlayerOption(value.Id, value.Name)).ToListAsync(cancellationToken);
    }
}
