using System.ComponentModel.DataAnnotations;
using Bingo.Application.Evidence;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Submissions;

[Authorize]
[RequestSizeLimit(11 * 1024 * 1024)]
public sealed class SubmissionModel(
    ApplicationDbContext db,
    ISubmissionService service,
    IEvidenceAuthority evidenceAuthority,
    TimeProvider time,
    IStringLocalizer<SharedResource> text,
    ILogger<SubmissionModel> logger) : PageModel
{
    public string DetailPage => "/Submissions/Submission";
    public string LedgerPage => "/Submissions/Index";
    public DetailsView Details { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public string EventSlug { get; private set; } = string.Empty;
    public string TeamSlug { get; private set; } = string.Empty;
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public bool CanOpenTeamLedger { get; private set; }
    public bool IsArchivedFormerOwner { get; private set; }
    public IReadOnlyList<RequirementView> Requirements { get; private set; } = [];
    public IReadOnlyList<DropView> Drops { get; private set; } = [];
    public IReadOnlyList<TargetView> TargetOptions { get; private set; } = [];
    public bool CanResubmit { get; private set; }
    [BindProperty] public EditInput Input { get; set; } = new();
    [BindProperty] public ResubmitInput Resubmission { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await Load(id, ct)) return NotFound();
        Input = new() { BoardTileId = Details.TileId, RequirementId = Details.RequirementId, DropSnapshotId = Details.DropId, CreditedParticipantId = Details.PlayerId, ClaimedWeight = Details.ClaimedWeight, Note = Details.CaptainNote, ExpectedVersion = Details.Version };
        Resubmission = new() { BoardTileId = Details.TileId, RequirementId = Details.RequirementId, DropSnapshotId = Details.DropId, Note = Details.CaptainNote, ExpectedVersion = Details.Version };
        return Page();
    }

    public RedirectToPageResult OnGetCorrect(Guid id) => RedirectToCanonicalGet(id);
    public RedirectToPageResult OnGetResubmit(Guid id) => RedirectToCanonicalGet(id);
    public RedirectToPageResult OnGetWithdraw(Guid id) => RedirectToCanonicalGet(id);

    public async Task<IActionResult> OnPostCorrectAsync(Guid id, CancellationToken ct)
    {
        if (!await Load(id, ct)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await using var stream = Input.Evidence?.OpenReadStream();
            await service.CorrectAsync(new(id, User.GetAccountId()!.Value, Input.BoardTileId, Input.RequirementId, Input.DropSnapshotId, Details.PlayerId, Details.ClaimedWeight, Input.Note, Input.ExpectedVersion, Input.Evidence?.FileName, stream, false), ct);
            TempData["StatusMessage"] = text["Submission updated and returned to review."].Value;
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            return RedirectToPage(new { id, eventId = EventId, teamId = TeamId, handler = (string?)null });
        }
        catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, ex)); return Page(); }
    }

    public async Task<IActionResult> OnPostResubmitAsync(Guid id, CancellationToken ct)
    {
        if (!await Load(id, ct)) return NotFound();
        if (Resubmission.Evidence is null) ModelState.AddModelError("Resubmission.Evidence", text["Paste, drag, or choose one screenshot."]);
        if (!ModelState.IsValid) return Page();
        try
        {
            await using var stream = Resubmission.Evidence!.OpenReadStream();
            var result = await service.ResubmitAsync(new(id, User.GetAccountId()!.Value, Resubmission.BoardTileId, Resubmission.RequirementId, Resubmission.DropSnapshotId, Resubmission.Note, Resubmission.Evidence.FileName, stream, Resubmission.ExpectedVersion, false), ct);
            TempData["StatusMessage"] = text["Linked resubmission created and returned to review."].Value;
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            return RedirectToPage(new { id = result.SubmissionId, eventId = EventId, teamId = TeamId, handler = (string?)null });
        }
        catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, ex)); return Page(); }
    }

    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, CancellationToken ct)
    {
        if (!await Load(id, ct)) return NotFound();
        try
        {
            await service.WithdrawAsync(id, User.GetAccountId()!.Value, ct, Input.ExpectedVersion, false);
            TempData["StatusMessage"] = text["Submission withdrawn."].Value;
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
        }
        catch (InvalidOperationException ex) { TempData["StatusMessage"] = SafeUserFailure.Message(text, logger, ex); }
        return RedirectToPage(LedgerPage, new { eventId = EventId, teamId = TeamId, handler = (string?)null });
    }

    private RedirectToPageResult RedirectToCanonicalGet(Guid id)
    {
        Guid? eventId = Guid.TryParse(Request.Query["eventId"].ToString(), out var parsedEventId) ? parsedEventId : null;
        Guid? teamId = Guid.TryParse(Request.Query["teamId"].ToString(), out var parsedTeamId) ? parsedTeamId : null;
        return RedirectToPage(DetailPage, new { id, eventId, teamId, handler = (string?)null });
    }

    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        var accountId = User.GetAccountId();
        if (accountId is null) return false;
        var submission = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (submission is null) return false;
        EvidenceActorScope? scope = null;
        try { scope = await evidenceAuthority.ResolveActorAsync(accountId.Value, submission.EventId, submission.TeamId, time.GetUtcNow(), ct); }
        catch (InvalidOperationException) { }
        var currentTeamActor = scope is { Kind: EvidenceActorKind.Participant or EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain };
        if (!currentTeamActor && !await IsArchivedFormerOwnerAsync(accountId.Value, submission, ct)) return false;
        IsArchivedFormerOwner = !currentTeamActor;

        EventId = submission.EventId;
        TeamId = submission.TeamId;
        CanOpenTeamLedger = currentTeamActor;
        var context = await (from eventRow in db.Events.AsNoTracking()
                             join team in db.Teams.AsNoTracking() on eventRow.Id equals team.EventId
                             where eventRow.Id == submission.EventId && eventRow.HiddenAt == null && team.Id == submission.TeamId
                             select new { EventSlug = eventRow.Slug, eventRow.Timezone, TeamSlug = team.Slug }).SingleOrDefaultAsync(ct);
        if (context is null) return false;
        EventSlug = context.EventSlug;
        EventTimezone = context.Timezone;
        TeamSlug = context.TeamSlug;
        var tile = await db.BoardTiles.AsNoTracking().SingleAsync(x => x.Id == submission.BoardTileId, ct);
        var requirement = await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.Id == submission.RequirementId, ct);
        var drop = submission.DropSnapshotId is Guid dropId
            ? await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => x.Id == dropId).Select(x => new { x.ItemName, x.DisplayRate }).SingleOrDefaultAsync(ct)
            : null;
        var asset = await db.EvidenceAssets.AsNoTracking().Where(x => x.SubmissionId == id && x.Active).OrderByDescending(x => x.UploadedAt).FirstOrDefaultAsync(ct);
        var replacementId = await db.Submissions.AsNoTracking()
            .Where(x => x.EventId == submission.EventId && x.TeamId == submission.TeamId && x.ResubmissionOfSubmissionId == id)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct);
        var eventItem = await db.Events.AsNoTracking().SingleAsync(x => x.Id == submission.EventId && x.HiddenAt == null, ct);
        var windowOpen = currentTeamActor && scope!.Kind == EvidenceActorKind.EmergencyCaptain
            ? eventItem.AcceptsEmergencySubmissions(time.GetUtcNow())
            : currentTeamActor && eventItem.AcceptsNewSubmissions(time.GetUtcNow());
        var canMutate = currentTeamActor && (scope!.Kind is EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain ||
                        scope.Kind == EvidenceActorKind.Participant && scope.CreditedParticipantId == submission.CreditedParticipantId);
        Details = new(submission.Id, submission.BoardTileId, submission.RequirementId, submission.DropSnapshotId, submission.CreditedParticipantId,
            tile.NameSnapshot, requirement.Description, drop is null ? null : $"{drop.ItemName} ({drop.DisplayRate})", submission.CreditedCharacterName,
            submission.Status, submission.ClaimedWeight, submission.ApprovedContribution, submission.SubmittedAt, submission.CaptainNote,
            submission.CurrentReviewerNote, submission.ExpectedEvidenceCode, asset?.Id,
            canMutate && windowOpen && submission.Status == SubmissionStatus.Pending,
            canMutate && windowOpen && submission.Status is (SubmissionStatus.Rejected or SubmissionStatus.Reversed) && replacementId is null,
            submission.ResubmissionOfSubmissionId, replacementId, submission.Version);
        CanResubmit = Details.Resubmittable;

        var board = await db.Boards.AsNoTracking().SingleAsync(x => x.EventId == submission.EventId, ct);
        var tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).ToListAsync(ct);
        var tileMap = tiles.ToDictionary(x => x.Id);
        var tileIds = tiles.Select(x => x.Id).ToList();
        var requirementEntities = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).OrderBy(x => x.Position).ToListAsync(ct);
        Requirements = requirementEntities.Select(x => new RequirementView(x.Id, x.BoardTileId, tileMap[x.BoardTileId].NameSnapshot, x.Description, x.AllowHigherWeightings, x.ManualObjective)).ToList();
        var requirementIds = Requirements.Select(x => x.Id).ToList();
        Drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => requirementIds.Contains(x.RequirementId)).OrderBy(x => x.BossName).ThenBy(x => x.ItemName).Select(x => new DropView(x.Id, x.RequirementId, x.BossName, x.ItemName, x.DisplayRate)).ToListAsync(ct);
        TargetOptions = Requirements.SelectMany(item => Drops.Where(dropItem => dropItem.RequirementId == item.Id).Select(dropItem => new TargetView(item.TileId, item.Id, dropItem.Id, $"drop:{dropItem.Id}", $"{dropItem.Item} ({dropItem.Rate})", item.Description)))
            .Concat(Requirements.Where(item => item.Manual).Select(item => new TargetView(item.TileId, item.Id, null, $"manual:{item.Id}", item.Description, item.Description)))
            .ToList();
        return true;
    }

    private async Task<bool> IsArchivedFormerOwnerAsync(Guid accountId, Bingo.Domain.Evidence.Submission submission, CancellationToken ct)
    {
        return submission.Status is SubmissionStatus.Rejected or SubmissionStatus.Withdrawn &&
               await (from eventRow in db.Events.AsNoTracking()
                      join participant in db.EventParticipants.AsNoTracking() on eventRow.Id equals participant.EventId
                      join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                      where eventRow.Id == submission.EventId && eventRow.HiddenAt == null && eventRow.State == EventState.Archived &&
                            participant.Id == submission.CreditedParticipantId && participant.AccountId == accountId && membership.TeamId == submission.TeamId
                      select participant.Id).AnyAsync(ct);
    }

    public sealed class EditInput
    {
        [Required] public Guid BoardTileId { get; set; }
        [Required] public Guid RequirementId { get; set; }
        public Guid? DropSnapshotId { get; set; }
        [Required] public Guid CreditedParticipantId { get; set; }
        [Range(1, 10000)] public int ClaimedWeight { get; set; } = 1;
        [StringLength(4000)] public string? Note { get; set; }
        public IFormFile? Evidence { get; set; }
        public int? ExpectedVersion { get; set; }
    }

    public sealed class ResubmitInput
    {
        [Required] public Guid BoardTileId { get; set; }
        [Required] public Guid RequirementId { get; set; }
        public Guid? DropSnapshotId { get; set; }
        [StringLength(4000)] public string? Note { get; set; }
        public IFormFile? Evidence { get; set; }
        public int? ExpectedVersion { get; set; }
    }

    public sealed record DetailsView(Guid Id, Guid TileId, Guid RequirementId, Guid? DropId, Guid PlayerId, string Tile, string Requirement, string? Drop, string Player, SubmissionStatus Status, int ClaimedWeight, int Approved, DateTimeOffset SubmittedAt, string? CaptainNote, string? Feedback, string? ExpectedCode, Guid? AssetId, bool Editable, bool Resubmittable, Guid? PriorSubmissionId, Guid? ReplacementSubmissionId, int Version = 1)
    {
        public string DisplayStatus => ReplacementSubmissionId is not null ? "Replaced" : Status.ToString();
    }

    public sealed record RequirementView(Guid Id, Guid TileId, string Tile, string Description, bool Higher, bool Manual);
    public sealed record DropView(Guid Id, Guid RequirementId, string Boss, string Item, string Rate);
    public sealed record TargetView(Guid TileId, Guid RequirementId, Guid? DropId, string Value, string Label, string Group);
}
