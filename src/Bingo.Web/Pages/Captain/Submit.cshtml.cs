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
    public TileDetails Tile { get; private set; } = null!; public IReadOnlyList<PlayerView> Players { get; private set; } = []; public IReadOnlyList<RequirementView> Requirements { get; private set; } = [];
    public Guid? DefaultParticipantId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public EvidenceActorKind ActorKind { get; private set; }
    public bool CanChooseCreditedParticipant => ActorKind is EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain;
    private EvidenceActorScope Scope { get; set; } = null!;
    [BindProperty] public SubmissionInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid tileId, Guid? eventId, Guid? teamId, CancellationToken ct) => await Load(tileId, eventId, teamId, ct) ? Page() : NotFound();
    public async Task<IActionResult> OnGetDrawerAsync(Guid tileId, Guid? eventId, Guid? teamId, CancellationToken ct) => await Load(tileId, eventId, teamId, ct) ? Partial("_SubmissionDrawer", this) : NotFound();
    public async Task<IActionResult> OnPostAsync(Guid tileId, Guid? eventId, Guid? teamId, CancellationToken ct)
    {
        if (!await Load(tileId, eventId, teamId, ct)) return NotFound(); if (Input.Evidence is null) ModelState.AddModelError("Input.Evidence", text["Paste, drag, or choose one screenshot."]); if (!ModelState.IsValid) return Page();
        try { await using var stream = Input.Evidence!.OpenReadStream(); var result = await submissions.CreateAsync(new(User.GetAccountId()!.Value, Scope.EventId, Scope.TeamId, tileId, Input.RequirementId, Input.DropSnapshotId, Input.CreditedParticipantId, Input.ClaimedWeight, Input.Note, Input.Evidence.FileName, stream), ct); TempData["StatusMessage"] = text["Evidence submitted for review."].Value; TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString(); return RedirectToPage("Submission", new { id = result.SubmissionId, eventId = Scope.EventId, teamId = Scope.TeamId }); }
        catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, ex)); return Page(); }
    }
    public async Task<IActionResult> OnPostDrawerAsync(Guid tileId, Guid? eventId, Guid? teamId, CancellationToken ct)
    {
        if (!await Load(tileId, eventId, teamId, ct)) return NotFound();
        if (Input.Evidence is null) ModelState.AddModelError("Input.Evidence", text["Paste, drag, or choose one screenshot."]);
        if (!ModelState.IsValid) return Partial("_SubmissionDrawer", this);
        try
        {
            await using var stream = Input.Evidence!.OpenReadStream();
            var result = await submissions.CreateAsync(new(User.GetAccountId()!.Value, Scope.EventId, Scope.TeamId, tileId, Input.RequirementId, Input.DropSnapshotId, Input.CreditedParticipantId, Input.ClaimedWeight, Input.Note, Input.Evidence.FileName, stream), ct);
            return new JsonResult(new { success = true, submissionId = result.SubmissionId, message = text["Evidence submitted for review."].Value });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, exception));
            return Partial("_SubmissionDrawer", this);
        }
    }
    private async Task<bool> Load(Guid tileId, Guid? requestedEventId, Guid? requestedTeamId, CancellationToken ct)
    {
        var accountId = User.GetAccountId(); if (accountId is null) return false;
        try { Scope = await evidenceAuthority.ResolveActorAsync(accountId.Value, requestedEventId ?? User.GetEventId(), requestedTeamId ?? User.GetTeamId(), time.GetUtcNow(), ct); } catch (InvalidOperationException) { return false; }
        if (Scope.Kind == EvidenceActorKind.Administrator) return false;
        EventId = Scope.EventId; TeamId = Scope.TeamId; ActorKind = Scope.Kind;
        var tile = await (from t in db.BoardTiles.AsNoTracking() join b in db.Boards on t.BoardId equals b.Id where t.Id == tileId && b.EventId == Scope.EventId && b.State == BoardState.Published select t).SingleOrDefaultAsync(ct); if (tile is null) return false; Tile = new(tile.Id, tile.NameSnapshot, tile.DescriptionSnapshot, tile.EvidenceInstructionsSnapshot);
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
    public sealed class SubmissionInput { [Required] public Guid RequirementId { get; set; } public Guid? DropSnapshotId { get; set; } [Required] public Guid CreditedParticipantId { get; set; } [Range(1, 10000)] public int ClaimedWeight { get; set; } = 1; [StringLength(4000)] public string? Note { get; set; } public IFormFile? Evidence { get; set; } }
    public sealed record TileDetails(Guid Id, string Name, string Description, string EvidenceInstructions); public sealed record PlayerView(Guid Id, string Name); public sealed record RequirementView(Guid Id, string Description, int Target, int Remaining, bool Manual, bool DuplicatesAllowed, IReadOnlyList<DropView> Drops); public sealed record DropView(Guid RequirementId, Guid Id, string Boss, string Item, string Rate, int CreditedWeight);
}
