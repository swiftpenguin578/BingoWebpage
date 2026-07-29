using System.ComponentModel.DataAnnotations;
using Bingo.Application.Evidence;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Captain;

[RequestSizeLimit(11 * 1024 * 1024)]
public sealed class SubmissionModel(ApplicationDbContext db, ISubmissionService service, IStringLocalizer<SharedResource> text, ILogger<SubmissionModel> logger) : PageModel
{
    public DetailsView Details { get; private set; } = null!; public IReadOnlyList<PlayerView> Players { get; private set; } = []; public IReadOnlyList<RequirementView> Requirements { get; private set; } = []; public IReadOnlyList<DropView> Drops { get; private set; } = [];
    [BindProperty] public EditInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) { if (!await Load(id, ct)) return NotFound(); Input = new() { BoardTileId = Details.TileId, RequirementId = Details.RequirementId, DropSnapshotId = Details.DropId, CreditedParticipantId = Details.PlayerId, ClaimedWeight = Details.ClaimedWeight, Note = Details.CaptainNote, RequestPublicPrivacy = Details.PublicPrivacyRequested }; return Page(); }
    public async Task<IActionResult> OnPostCorrectAsync(Guid id, CancellationToken ct)
    {
        if (!await Load(id, ct)) return NotFound(); if (!ModelState.IsValid) return Page(); try { Stream? stream = null; if (Input.Replacement is not null) stream = Input.Replacement.OpenReadStream(); try { await service.CorrectAsync(new(id, User.GetAccountId()!.Value, Input.BoardTileId, Input.RequirementId, Input.DropSnapshotId, Input.CreditedParticipantId, Input.ClaimedWeight, Input.Note, Input.Replacement?.FileName, stream, Input.RequestPublicPrivacy), ct); } finally { if (stream is not null) await stream.DisposeAsync(); } TempData["StatusMessage"] = text["Submission updated and returned to review."]; return RedirectToPage(new { id }); } catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, ex)); return Page(); }
    }
    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, CancellationToken ct) { try { await service.WithdrawAsync(id, User.GetAccountId()!.Value, ct); TempData["StatusMessage"] = text["Submission withdrawn."]; } catch (InvalidOperationException ex) { TempData["StatusMessage"] = SafeUserFailure.Message(text, logger, ex); } return RedirectToPage("Index"); }
    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        var teamId = User.GetTeamId()!.Value; var s = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TeamId == teamId, ct); if (s is null) return false; var tile = await db.BoardTiles.AsNoTracking().SingleAsync(x => x.Id == s.BoardTileId, ct); var req = await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.Id == s.RequirementId, ct); var player = await db.PrimaryCharacters().AsNoTracking().SingleAsync(x => x.ParticipantId == s.CreditedParticipantId, ct); var asset = await db.EvidenceAssets.AsNoTracking().Where(x => x.SubmissionId == id && x.Active).OrderByDescending(x => x.UploadedAt).FirstOrDefaultAsync(ct); Details = new(s.Id, s.BoardTileId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, tile.NameSnapshot, req.Description, player.Name, s.Status, s.ClaimedWeight, s.ApprovedContribution, s.SubmittedAt, s.CaptainNote, s.CurrentReviewerNote, s.ExpectedEvidenceCode, asset?.Id, s.Status is SubmissionStatus.Pending or SubmissionStatus.ChangesRequested);
        Players = await (from m in db.TeamMemberships.AsNoTracking() join p in db.PrimaryCharacters() on m.EventParticipantId equals p.ParticipantId where m.TeamId == teamId && m.LeftAt == null orderby p.Name select new PlayerView(p.ParticipantId, p.Name)).ToListAsync(ct); Details = Details with { PublicPrivacyRequested = s.PublicPrivacyRequested }; var board = await db.Boards.AsNoTracking().SingleAsync(x => x.EventId == s.EventId, ct); var tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).ToListAsync(ct); var tileMap = tiles.ToDictionary(x => x.Id); var tileIds = tiles.Select(x => x.Id).ToList(); var requirementEntities = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).OrderBy(x => x.Position).ToListAsync(ct); Requirements = requirementEntities.Select(x => new RequirementView(x.Id, x.BoardTileId, tileMap[x.BoardTileId].NameSnapshot, x.Description, x.AllowHigherWeightings, x.ManualObjective)).ToList(); var reqIds = Requirements.Select(x => x.Id).ToList(); Drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => reqIds.Contains(x.RequirementId)).OrderBy(x => x.BossName).ThenBy(x => x.ItemName).Select(x => new DropView(x.Id, x.RequirementId, x.BossName, x.ItemName, x.DisplayRate)).ToListAsync(ct); return true;
    }
    public sealed class EditInput { [Required] public Guid BoardTileId { get; set; } [Required] public Guid RequirementId { get; set; } public Guid? DropSnapshotId { get; set; } [Required] public Guid CreditedParticipantId { get; set; } [Range(1, 10000)] public int ClaimedWeight { get; set; } = 1; [StringLength(4000)] public string? Note { get; set; } public bool RequestPublicPrivacy { get; set; } public IFormFile? Replacement { get; set; } }
    public sealed record DetailsView(Guid Id, Guid TileId, Guid RequirementId, Guid? DropId, Guid PlayerId, string Tile, string Requirement, string Player, SubmissionStatus Status, int ClaimedWeight, int Approved, DateTimeOffset SubmittedAt, string? CaptainNote, string? Feedback, string? ExpectedCode, Guid? AssetId, bool Editable, bool PublicPrivacyRequested = false); public sealed record PlayerView(Guid Id, string Name); public sealed record RequirementView(Guid Id, Guid TileId, string Tile, string Description, bool Higher, bool Manual); public sealed record DropView(Guid Id, Guid RequirementId, string Boss, string Item, string Rate);
}
