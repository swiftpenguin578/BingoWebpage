using System.ComponentModel.DataAnnotations;
using Bingo.Application.Evidence;
using Bingo.Domain.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Captain;

[Authorize]
[RequestSizeLimit(11 * 1024 * 1024)]
public sealed class SubmitModel(ApplicationDbContext db, ISubmissionService submissions, IEvidenceAuthority evidenceAuthority, TimeProvider time, IStringLocalizer<SharedResource> text, ILogger<SubmitModel> logger) : PageModel
{
    public TileDetails? Tile { get; private set; }
    public IReadOnlyList<TileOption> Tiles { get; private set; } = []; public IReadOnlyList<PlayerView> Players { get; private set; } = []; public IReadOnlyList<RequirementView> Requirements { get; private set; } = [];
    public Guid? DefaultParticipantId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public EvidenceActorKind ActorKind { get; private set; }
    public bool CanChooseCreditedParticipant => ActorKind is EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain;
    private EvidenceActorScope Scope { get; set; } = null!;
    [BindProperty] public SubmissionInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid? tileId, Guid? eventId, Guid? teamId, CancellationToken ct)
    {
        if (!await Load(tileId, eventId, teamId, ct)) return NotFound();

        var destination = await (from team in db.Teams.AsNoTracking()
                                 join eventItem in db.Events.AsNoTracking() on team.EventId equals eventItem.Id
                                 where team.Id == TeamId && eventItem.Id == EventId && eventItem.HiddenAt == null
                                 select new { EventSlug = eventItem.Slug, TeamSlug = team.Slug }).SingleOrDefaultAsync(ct);
        if (destination is null) return NotFound();
        return tileId is Guid selectedTileId
            ? RedirectToPage("/Events/Tile", new { slug = destination.EventSlug, teamSlug = destination.TeamSlug, tileId = selectedTileId })
            : RedirectToPage("/Events/TeamBoard", new { slug = destination.EventSlug, teamSlug = destination.TeamSlug });
    }
    public async Task<IActionResult> OnGetDrawerAsync(Guid? tileId, Guid? eventId, Guid? teamId, CancellationToken ct) => await Load(tileId, eventId, teamId, ct) ? Partial("_SubmissionDrawer", this) : NotFound();
    public async Task<IActionResult> OnPostDrawerAsync(Guid? tileId, Guid? eventId, Guid? teamId, CancellationToken ct)
    {
        tileId ??= Input.TileId;
        if (!await Load(tileId, eventId, teamId, ct)) return NotFound();
        if (Input.Evidence is null) ModelState.AddModelError("Input.Evidence", text["Paste, drag, or choose one screenshot."]);
        if (!ModelState.IsValid) return Partial("_SubmissionDrawer", this);
        try
        {
            await using var stream = Input.Evidence!.OpenReadStream();
            var result = await submissions.CreateAsync(new(User.GetAccountId()!.Value, Scope.EventId, Scope.TeamId, tileId!.Value, Input.RequirementId, Input.DropSnapshotId, Input.CreditedParticipantId, Input.ClaimedWeight, Input.Note, Input.Evidence.FileName, stream), ct);
            return new JsonResult(new { success = true, submissionId = result.SubmissionId, message = text["Evidence submitted for review."].Value });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, exception));
            return Partial("_SubmissionDrawer", this);
        }
    }
    private async Task<bool> Load(Guid? tileId, Guid? requestedEventId, Guid? requestedTeamId, CancellationToken ct)
    {
        var accountId = User.GetAccountId(); if (accountId is null) return false;
        try { Scope = await evidenceAuthority.ResolveActorAsync(accountId.Value, requestedEventId ?? User.GetEventId(), requestedTeamId ?? User.GetTeamId(), time.GetUtcNow(), ct); } catch (InvalidOperationException) { return false; }
        if (Scope.Kind == EvidenceActorKind.Administrator) return false;
        EventId = Scope.EventId; TeamId = Scope.TeamId; ActorKind = Scope.Kind;
        var boardTiles = await (from t in db.BoardTiles.AsNoTracking() join b in db.Boards on t.BoardId equals b.Id where b.EventId == Scope.EventId && b.State == BoardState.Published orderby t.RowIndex, t.ColumnIndex select new TileOption(t.Id, t.NameSnapshot)).ToListAsync(ct);
        var tileIds = boardTiles.Select(value => value.Id).ToList();
        var tileRequirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(value => tileIds.Contains(value.BoardTileId)).ToListAsync(ct);
        var requirementIds = tileRequirements.Select(value => value.Id).ToList();
        var tileContributions = await db.SubmissionContributions.AsNoTracking().Where(value => value.TeamId == Scope.TeamId && requirementIds.Contains(value.RequirementId) && value.ReversedAt == null).GroupBy(value => value.RequirementId).Select(value => new { value.Key, Total = value.Sum(item => item.Amount) }).ToDictionaryAsync(value => value.Key, value => value.Total, ct);
        var tileContributionRows = await db.SubmissionContributions.AsNoTracking().Where(value => value.TeamId == Scope.TeamId && requirementIds.Contains(value.RequirementId) && value.ReversedAt == null && value.DropSnapshotId != null).GroupBy(value => new { value.RequirementId, value.DropSnapshotId }).Select(value => new { value.Key.RequirementId, DropSnapshotId = value.Key.DropSnapshotId!.Value, Total = value.Sum(item => item.Amount) }).ToDictionaryAsync(value => (value.RequirementId, value.DropSnapshotId), value => value.Total, ct);
        var tileDrops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(value => requirementIds.Contains(value.RequirementId)).ToListAsync(ct);
        var availableTileIds = tileRequirements.GroupBy(value => value.BoardTileId).Where(group => group.Any(requirement =>
            tileContributions.GetValueOrDefault(requirement.Id) < requirement.TargetContribution &&
            (requirement.ManualObjective || tileDrops.Any(drop => drop.RequirementId == requirement.Id && (drop.MaximumContribution ?? (requirement.DuplicatesAllowed ? int.MaxValue : 1)) > tileContributionRows.GetValueOrDefault((requirement.Id, drop.Id)))))).Select(group => group.Key).ToHashSet();
        Tiles = boardTiles.Where(value => availableTileIds.Contains(value.Id) || value.Id == tileId).ToList();
        if (tileId is null) { Tile = null; Requirements = []; Players = []; return true; }
        if (!boardTiles.Any(value => value.Id == tileId)) return false;
        var tile = await (from t in db.BoardTiles.AsNoTracking() join b in db.Boards on t.BoardId equals b.Id where t.Id == tileId && b.EventId == Scope.EventId && b.State == BoardState.Published select t).SingleOrDefaultAsync(ct); if (tile is null) return false; Tile = new(tile.Id, tile.NameSnapshot, tile.DescriptionSnapshot, tile.EvidenceInstructionsSnapshot); Input.TileId = tile.Id;
        Players = (await evidenceAuthority.GetCurrentTeamCandidatesAsync(Scope, ct)).Select(player => new PlayerView(player.ParticipantId, player.CharacterName)).ToList();
        DefaultParticipantId = Scope.Kind == EvidenceActorKind.Participant ? Scope.CreditedParticipantId : null;
        if (Input.CreditedParticipantId == Guid.Empty &&
            DefaultParticipantId is Guid participantId &&
            Players.Any(player => player.Id == participantId))
        {
            Input.CreditedParticipantId = participantId;
        }
        var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => x.BoardTileId == tileId).OrderBy(x => x.Position).ToListAsync(ct); var ids = requirements.Select(x => x.Id).ToList(); var drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => ids.Contains(x.RequirementId)).OrderBy(x => x.BossName).ThenBy(x => x.ItemName).ToListAsync(ct); var contributions = await db.SubmissionContributions.AsNoTracking().Where(x => x.TeamId == Scope.TeamId && ids.Contains(x.RequirementId) && x.ReversedAt == null).ToListAsync(ct); var contributed = contributions.GroupBy(x => x.RequirementId).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount)); var contributedByDrop = contributions.Where(x => x.DropSnapshotId is not null).GroupBy(x => x.DropSnapshotId!.Value).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount)); Requirements = requirements.Select(x => new RequirementView(x.Id, x.Description, x.TargetContribution, Math.Max(0, x.TargetContribution - contributed.GetValueOrDefault(x.Id)), x.ManualObjective, x.DuplicatesAllowed, drops.Where(d => d.RequirementId == x.Id && (d.MaximumContribution ?? (x.DuplicatesAllowed ? int.MaxValue : 1)) > contributedByDrop.GetValueOrDefault(d.Id)).Select(d => new DropView(x.Id, d.Id, d.BossName, d.ItemName, d.DisplayRate, d.CreditedWeight)).ToList())).ToList(); return true;
    }
    public sealed class SubmissionInput { public Guid? TileId { get; set; } [Required] public Guid RequirementId { get; set; } public Guid? DropSnapshotId { get; set; } [Required] public Guid CreditedParticipantId { get; set; } [Range(1, 10000)] public int ClaimedWeight { get; set; } = 1; [StringLength(4000)] public string? Note { get; set; } public IFormFile? Evidence { get; set; } }
    public sealed record TileDetails(Guid Id, string Name, string Description, string EvidenceInstructions); public sealed record TileOption(Guid Id, string Name); public sealed record PlayerView(Guid Id, string Name); public sealed record RequirementView(Guid Id, string Description, int Target, int Remaining, bool Manual, bool DuplicatesAllowed, IReadOnlyList<DropView> Drops); public sealed record DropView(Guid RequirementId, Guid Id, string Boss, string Item, string Rate, int CreditedWeight);
}
