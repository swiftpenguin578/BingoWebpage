using System.ComponentModel.DataAnnotations;
using Bingo.Application.Evidence;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Captain;

[RequestSizeLimit(11 * 1024 * 1024)]
public sealed class SubmissionModel(
    ApplicationDbContext db,
    ISubmissionService service,
    IEvidenceAuthority evidenceAuthority,
    TimeProvider time,
    IStringLocalizer<SharedResource> text,
    ILogger<SubmissionModel> logger) : PageModel
{
    public DetailsView Details { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public IReadOnlyList<RequirementView> Requirements { get; private set; } = [];
    public IReadOnlyList<DropView> Drops { get; private set; } = [];
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

    public async Task<IActionResult> OnPostCorrectAsync(Guid id, CancellationToken ct)
    {
        if (!await Load(id, ct)) return NotFound();
        if (!ModelState.IsValid) return Page();
        try
        {
            await service.CorrectAsync(new(id, User.GetAccountId()!.Value, Input.BoardTileId, Input.RequirementId, Input.DropSnapshotId, Details.PlayerId, Details.ClaimedWeight, Input.Note, Input.ExpectedVersion), ct);
            TempData["StatusMessage"] = text["Submission updated and returned to review."].Value; TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString(); return RedirectToPage(new { id, eventId = EventId, teamId = TeamId });
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
            var result = await service.ResubmitAsync(new(id, User.GetAccountId()!.Value, Resubmission.BoardTileId, Resubmission.RequirementId, Resubmission.DropSnapshotId, Resubmission.Note, Resubmission.Evidence.FileName, stream, Resubmission.ExpectedVersion), ct);
            TempData["StatusMessage"] = text["Linked resubmission created and returned to review."].Value; TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString(); return RedirectToPage(new { id = result.SubmissionId, eventId = EventId, teamId = TeamId });
        }
        catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, ex)); return Page(); }
    }

    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, CancellationToken ct)
    {
        if (!await Load(id, ct)) return NotFound();
        try { await service.WithdrawAsync(id, User.GetAccountId()!.Value, ct, Input.ExpectedVersion); TempData["StatusMessage"] = text["Submission withdrawn."].Value; TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString(); }
        catch (InvalidOperationException ex) { TempData["StatusMessage"] = SafeUserFailure.Message(text, logger, ex); }
        return RedirectToPage("Index", new { eventId = EventId, teamId = TeamId });
    }

    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        var accountId = User.GetAccountId(); if (accountId is null) return false;
        var s = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (s is null) return false;
        EvidenceActorScope scope; try { scope = await evidenceAuthority.ResolveActorAsync(accountId.Value, s.EventId, s.TeamId, time.GetUtcNow(), ct); } catch (InvalidOperationException) { return false; }
        EventId = scope.EventId; TeamId = scope.TeamId;
        if (scope.Kind == EvidenceActorKind.Participant && scope.CreditedParticipantId != s.CreditedParticipantId) return false;
        var tile = await db.BoardTiles.AsNoTracking().SingleAsync(x => x.Id == s.BoardTileId, ct); var req = await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.Id == s.RequirementId, ct); var asset = await db.EvidenceAssets.AsNoTracking().Where(x => x.SubmissionId == id && x.Active).OrderByDescending(x => x.UploadedAt).FirstOrDefaultAsync(ct);
        var eventItem = await db.Events.AsNoTracking().SingleAsync(x => x.Id == s.EventId, ct); var canMutate = scope.Kind is EvidenceActorKind.Participant or EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain;
        var windowOpen = scope.Kind == EvidenceActorKind.EmergencyCaptain ? eventItem.AcceptsEmergencySubmissions(time.GetUtcNow()) : eventItem.AcceptsNewSubmissions(time.GetUtcNow());
        Details = new(s.Id, s.BoardTileId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, tile.NameSnapshot, req.Description, s.CreditedCharacterName, s.Status, s.ClaimedWeight, s.ApprovedContribution, s.SubmittedAt, s.CaptainNote, s.CurrentReviewerNote, s.ExpectedEvidenceCode, asset?.Id, canMutate && s.Status == SubmissionStatus.Pending, canMutate && windowOpen && s.Status == SubmissionStatus.Rejected, s.Version);
        CanResubmit = Details.Resubmittable;
        var board = await db.Boards.AsNoTracking().SingleAsync(x => x.EventId == s.EventId, ct); var tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).ToListAsync(ct); var tileMap = tiles.ToDictionary(x => x.Id); var tileIds = tiles.Select(x => x.Id).ToList(); var requirementEntities = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).OrderBy(x => x.Position).ToListAsync(ct); Requirements = requirementEntities.Select(x => new RequirementView(x.Id, x.BoardTileId, tileMap[x.BoardTileId].NameSnapshot, x.Description, x.AllowHigherWeightings, x.ManualObjective)).ToList(); var reqIds = Requirements.Select(x => x.Id).ToList(); Drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => reqIds.Contains(x.RequirementId)).OrderBy(x => x.BossName).ThenBy(x => x.ItemName).Select(x => new DropView(x.Id, x.RequirementId, x.BossName, x.ItemName, x.DisplayRate)).ToListAsync(ct); return true;
    }

    public sealed class EditInput { [Required] public Guid BoardTileId { get; set; } [Required] public Guid RequirementId { get; set; } public Guid? DropSnapshotId { get; set; } [Required] public Guid CreditedParticipantId { get; set; } [Range(1, 10000)] public int ClaimedWeight { get; set; } = 1; [StringLength(4000)] public string? Note { get; set; } public int? ExpectedVersion { get; set; } }
    public sealed class ResubmitInput { [Required] public Guid BoardTileId { get; set; } [Required] public Guid RequirementId { get; set; } public Guid? DropSnapshotId { get; set; } [StringLength(4000)] public string? Note { get; set; } [Required] public IFormFile? Evidence { get; set; } public int? ExpectedVersion { get; set; } }
    public sealed record DetailsView(Guid Id, Guid TileId, Guid RequirementId, Guid? DropId, Guid PlayerId, string Tile, string Requirement, string Player, SubmissionStatus Status, int ClaimedWeight, int Approved, DateTimeOffset SubmittedAt, string? CaptainNote, string? Feedback, string? ExpectedCode, Guid? AssetId, bool Editable, bool Resubmittable, int Version = 1);
    public sealed record RequirementView(Guid Id, Guid TileId, string Tile, string Description, bool Higher, bool Manual); public sealed record DropView(Guid Id, Guid RequirementId, string Boss, string Item, string Rate);
}
