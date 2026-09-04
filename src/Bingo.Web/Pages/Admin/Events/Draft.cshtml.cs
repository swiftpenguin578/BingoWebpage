using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Evidence;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Boards;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Bingo.Web.Teams;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class DraftModel(ApplicationDbContext db, TimeProvider time, IAuditWriter audit, IAdminCollaborationNotifier collaboration, ISignupService signupService, EventParticipantCharacterService characterService, IEvidenceStorage? storage = null, PreformedRosterCsvImportService? csvImport = null, ITeamCaptainAuthorityService? captainAuthority = null, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    public string EventName { get; private set; } = string.Empty; public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId; public string Sort { get; private set; } = "ehb"; public DraftView? Draft { get; private set; }
    public Guid EventId { get; private set; }
    public IReadOnlyList<TeamView> Teams { get; private set; } = []; public IReadOnlyList<ParticipantView> Participants { get; private set; } = [];
    public IReadOnlyDictionary<Guid, PaymentStatus> ParticipantPayments { get; private set; } = new Dictionary<Guid, PaymentStatus>();
    public TurnView? CurrentTurn { get; private set; }
    public PickView? LatestPick { get; private set; }
    public int ConfirmedCount { get; private set; }
    public int AvailableCount { get; private set; }
    public int AssignedCount { get; private set; }
    public int Remainder { get; private set; }
    public DraftRosterDistribution? Distribution { get; private set; }
    public IReadOnlyList<GeneratedCaptainCredential> GeneratedCredentials { get; private set; } = [];
    public Guid CurrentAccountId { get; private set; }
    public bool CanControlDraft { get; private set; }
    public Guid? DraftControllerAccountId { get; private set; }
    public PreformedRosterCsvImportService.Preview? CsvPreview { get; private set; }
    public Guid? RosterTeamId { get; private set; }
    public bool CsvFileError { get; private set; }
    public string? DraftControllerName { get; private set; }
    public DateTimeOffset? DraftControllerLeaseExpiresAt { get; private set; }
    public bool DraftOrderReady { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid id, string? sort, CancellationToken ct, Guid? rosterTeamId = null)
    {
        if (TempData["GeneratedCaptainCredentials"] is string json) GeneratedCredentials = JsonSerializer.Deserialize<List<GeneratedCaptainCredential>>(json) ?? [];
        CurrentAccountId = AdminId;
        if (!await Load(id, sort, ct)) return NotFound();
        RosterTeamId = rosterTeamId is { } requested && Teams.Any(team => team.Id == requested) ? requested : null;
        if (Participants.Count > 0)
        {
            var participantIds = Participants.Select(participant => participant.Id).ToList();
            ParticipantPayments = await db.EventParticipants
                .AsNoTracking()
                .Where(participant => participantIds.Contains(participant.Id))
                .ToDictionaryAsync(participant => participant.Id, participant => participant.PaymentStatus, ct);
        }
        await LoadControllerState(id, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAddTeamAsync(Guid id, string name, TeamFormationType formationType, string? affiliation, CancellationToken ct, bool confirmed = false)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ev is null) return NotFound();
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) { draft = new DraftSession(Guid.NewGuid(), id, 1); db.DraftSessions.Add(draft); }
        if (draft.State is DraftState.Running or DraftState.Paused || string.IsNullOrWhiteSpace(name)) { SetStatus(Localize("Team structure is locked while a private draft is active; cancel the private draft to return to setup."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (!CanDirectPreEventRosterMutation(ev)) { SetStatus(Localize("Direct roster changes are available only before the configured event start."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (draft.State == DraftState.Finalized && formationType == TeamFormationType.Drafted) { SetStatus(Localize("Drafted teams cannot be added after the draft has been completed."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (draft.State == DraftState.Finalized && !confirmed) { SetStatus(Localize("Confirm this published pre-formed correction."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (await db.Teams.AnyAsync(team => team.EventId == id && team.Active && team.Name == name.Trim(), ct)) { SetStatus(Localize("A team with that name already exists for this event."), UiMessageType.Error); return RedirectToPage(new { id }); }
        var team = new Team(Guid.NewGuid(), id, name.Trim(), await UniqueTeamSlug(id, name, ct), formationType, Clean(affiliation), formationType == TeamFormationType.Drafted, time.GetUtcNow());
        db.Teams.Add(team);
        try
        {
            if (draft.State == DraftState.Finalized)
                await RepublishPreformedCorrectionAsync(ev, draft, "team.preformed_corrected", team.Id, ct);
            else
                await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "team.created", "team", team.Id.ToString(), formationType.ToString(), ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); SetStatus(Localize("A team with that name already exists for this event."), UiMessageType.Error); return RedirectToPage(new { id }); }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        SetStatus(Localize("{0} created.", team.Name), UiMessageType.Success); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostRemoveDraftTeamAsync(Guid id, Guid teamId, CancellationToken ct)
    { var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft is null) return NotFound(); if (draft.State != DraftState.Setup) { SetStatus(Localize("Drafted-team structure is locked while a private draft is active or published."), UiMessageType.Error); return RedirectToPage(new { id }); } var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id && x.FormationType == TeamFormationType.Drafted && x.Active, ct); if (team is null) return NotFound(); if (await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.LeftAt == null, ct)) { SetStatus(Localize("Remove the players from {0} before removing the team.", team.Name), UiMessageType.Error); return RedirectToPage(new { id }); } team.SetActive(false); team.SetDraftPosition(null); await db.SaveChangesAsync(ct); await Audit("draft.team_removed", "team", team.Id, "Removed from website draft setup", ct); SetStatus(Localize("{0} removed from the website draft.", team.Name), UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostRemoveExternalTeamAsync(Guid id, Guid teamId, CancellationToken ct, bool confirmed = false)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id && x.FormationType == TeamFormationType.Preformed && x.Active, ct);
        if (ev is null || team is null) return NotFound();
        if (!CanDirectPreEventRosterMutation(ev) || draft?.State is DraftState.Running or DraftState.Paused) { SetStatus(Localize("Pre-formed teams can only be removed before the configured event start and outside the running draft."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (draft?.State == DraftState.Finalized && !confirmed) { SetStatus(Localize("Confirm this published pre-formed correction."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.LeftAt == null, ct)) { SetStatus(Localize("Remove the players from {0} before removing the external team.", team.Name), UiMessageType.Error); return RedirectToPage(new { id }); }
        team.SetActive(false); team.SetDraftPosition(null);
        if (draft?.State == DraftState.Finalized) { await db.SaveChangesAsync(ct); await RepublishPreformedCorrectionAsync(ev, draft, "team.preformed_removed", team.Id, ct); }
        else await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "external_team.removed", "team", team.Id.ToString(), "External team removed", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); SetStatus(Localize("External team {0} removed.", team.Name), UiMessageType.Success); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostWithdrawParticipantAsync(Guid id, Guid participantId, CancellationToken ct)
    { var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft?.State != DraftState.Setup) { SetStatus(Localize("Participants cannot be withdrawn after the draft starts."), UiMessageType.Error); return RedirectToPage(new { id }); } var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.Id == participantId && x.EventId == id && x.Source != SignupSource.AdminCreated, ct); if (participant is null) return NotFound(); if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct)) { SetStatus(Localize("Remove this player from their roster before withdrawing them from the event."), UiMessageType.Error); return RedirectToPage(new { id }); } var result = await signupService.WithdrawAsync(id, participantId, AdminId, User.Identity?.Name ?? "Admin", true, cancellationToken: ct); SetStatus(result.Succeeded ? Localize("Participant withdrawn. The waiting list was promoted where a place became available.") : result.Error ?? Localize("The participant could not be withdrawn."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostUpdateTeamAsync(Guid id, Guid teamId, string name, string? affiliation, IFormFile? image, bool removeImage, long version, CancellationToken ct, Guid? rosterTeamId = null)
    {
        var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id, ct); if (team is null) return NotFound();
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        if (ev.ActualStartedAt is not null || team.Version != version || string.IsNullOrWhiteSpace(name)) { SetStatus(ev.ActualStartedAt is not null ? Localize("Team metadata is locked after event start.") : Localize("This team changed; reload and try again."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        StoredEvidence? uploaded = null; await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = time.GetUtcNow(); if (removeImage && team.ActiveImageAssetId is { } old) { (await db.TeamImageAssets.SingleOrDefaultAsync(x => x.Id == old, ct))?.Replace(now); team.SetActiveImage(null); }
            if (image is { Length: > 0 }) { if (storage is null) throw new InvalidOperationException("Image storage is unavailable."); var assetId = Guid.NewGuid(); await using var content = image.OpenReadStream(); uploaded = await storage.StoreAsync(id, assetId, image.FileName, content, ct); if (team.ActiveImageAssetId is { } previous) (await db.TeamImageAssets.SingleOrDefaultAsync(x => x.Id == previous, ct))?.Replace(now); db.TeamImageAssets.Add(new TeamImageAsset(assetId, id, team.Id, uploaded.StorageKey, uploaded.OriginalFilename, uploaded.MediaType, uploaded.ByteSize, uploaded.Width, uploaded.Height, uploaded.Checksum, AdminId, now)); team.SetActiveImage(assetId); }
            team.Update(name.Trim(), team.Slug, Clean(affiliation), null); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { await tx.RollbackAsync(ct); if (uploaded is not null && storage is not null) await storage.DeleteAsync(uploaded.StorageKey, ct); SetStatus(Localize("The team update could not be saved."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        await Audit("team.updated", "team", team.Id, name, ct); SetStatus(Localize("{0} updated.", team.Name), UiMessageType.Success); return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostAddMemberAsync(Guid id, Guid teamId, Guid? participantId, string? reason, CancellationToken ct, bool confirmed = false, Guid? rosterTeamId = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var team = await db.Teams.SingleOrDefaultAsync(value => value.Id == teamId && value.EventId == id, ct); var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); var draft = await db.DraftSessions.SingleOrDefaultAsync(value => value.EventId == id, ct);
        if (team is null || ev is null) return NotFound();
        var draftedSetupAssignment = team.FormationType == TeamFormationType.Drafted && CanDirectDraftedSetupAssignment(ev, draft);
        if (!CanDirectPreEventRosterMutation(ev) || (team.FormationType != TeamFormationType.Preformed && !draftedSetupAssignment)) { SetStatus(Localize("Direct roster additions are available only for pre-formed teams before event start, or drafted teams before the first pick."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (draft?.State == DraftState.Finalized && team.FormationType == TeamFormationType.Preformed && !confirmed) { SetStatus(Localize("Confirm this published pre-formed correction."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (participantId is null) { SetStatus(Localize("Choose a participant."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (team.FormationType == TeamFormationType.Drafted && !await db.EventParticipants.AnyAsync(participant => participant.Id == participantId.Value && participant.EventId == id && participant.Source == SignupSource.Website && participant.SignupStatus == SignupStatus.Confirmed, ct)) { SetStatus(Localize("Only confirmed website-draft participants can be preassigned to a drafted team."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var participantName = await db.PrimaryCharacters().Where(x => x.ParticipantId == participantId.Value && x.EventId == id).Select(x => x.Name).SingleOrDefaultAsync(ct); if (participantName is null) return NotFound();
        if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId.Value && x.LeftAt == null, ct)) { SetStatus(Localize("Participant is already assigned to a team."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var assignmentReason = team.FormationType == TeamFormationType.Preformed ? "Pre-formed roster assignment" : reason?.Trim() ?? "Manual roster assignment";
        var membership = new TeamMembership(Guid.NewGuid(), teamId, participantId.Value, TeamMembershipRole.Participant, time.GetUtcNow(), null, assignmentReason); membership.SetSource(TeamMembershipSource.PreformedManual); db.TeamMemberships.Add(membership);
        if (draft?.State == DraftState.Finalized) { await db.SaveChangesAsync(ct); await RepublishPreformedCorrectionAsync(ev, draft, "team.member_added", membership.Id, ct); }
        else await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "team.member_added", "team", teamId.ToString(), $"{participantId}: {assignmentReason}", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); SetStatus(Localize("{0} added to {1}.", participantName, team.Name), UiMessageType.Success); return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostAddExternalMemberAsync(Guid id, Guid teamId, string name, decimal ehb, string? additionalAccounts, CancellationToken ct, bool confirmed = false, Guid? rosterTeamId = null)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == teamId && x.EventId == id && x.FormationType == TeamFormationType.Preformed, ct);
        if (team is null) return BadRequest();
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (ev is null || !CanDirectPreEventRosterMutation(ev)) { SetStatus(Localize("Direct roster additions are available only before the configured event start."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (draft?.State == DraftState.Finalized && !confirmed) { SetStatus(Localize("Confirm this published pre-formed correction."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var sequence = (await db.EventParticipants.Where(x => x.EventId == id).MaxAsync(x => (long?)x.SignupSequence, ct) ?? 0) + 1;
            var participant = new EventParticipant(Guid.NewGuid(), id, SignupStatus.Confirmed, sequence, time.GetUtcNow(), SignupSource.AdminCreated);
            db.EventParticipants.Add(participant);
            await characterService.AssignExternalRosterCharactersAsync(participant, name, ehb, (additionalAccounts ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), EhbSource.AdminCorrection, AdminId, ct);
            await db.SaveChangesAsync(ct);
            var membership = new TeamMembership(Guid.NewGuid(), teamId, participant.Id, TeamMembershipRole.Participant, time.GetUtcNow(), null, "Pre-formed roster assignment"); membership.SetSource(TeamMembershipSource.PreformedManual); db.TeamMemberships.Add(membership);
            if (draft?.State == DraftState.Finalized) { await db.SaveChangesAsync(ct); await RepublishPreformedCorrectionAsync(ev, draft, "team.member_added", membership.Id, ct); }
            else await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "team.member_added", "team", teamId.ToString(), $"{participant.Id}: Manual pre-formed roster", ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            SetStatus(Localize("{0} added to {1}.", name.Trim(), team.Name), UiMessageType.Success);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            await transaction.RollbackAsync(ct);
            SetStatus(Localize("That external roster member could not be added because an account is already reserved or assigned."), UiMessageType.Error);
        }
        return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnGetTeamImageAsync(Guid id, Guid teamId, CancellationToken ct)
    {
        if (storage is null) return NotFound();
        var asset = await (from team in db.Teams.AsNoTracking()
                           join image in db.TeamImageAssets.AsNoTracking() on team.ActiveImageAssetId equals image.Id
                           where team.Id == teamId && team.EventId == id && team.Active && image.ReplacedAt == null
                           select image).SingleOrDefaultAsync(ct);
        if (asset is null) return NotFound();
        try { return new FileStreamResult(await storage.OpenReadAsync(asset.StorageKey, ct), asset.MediaType) { EnableRangeProcessing = true }; }
        catch (FileNotFoundException) { return NotFound(); }
    }
    public async Task<IActionResult> OnGetRosterCsvTemplateAsync(Guid id, Guid teamId, CancellationToken ct)
    {
        var allowed = await db.Teams.AsNoTracking().AnyAsync(team => team.Id == teamId && team.EventId == id && team.Active && team.FormationType == TeamFormationType.Preformed, ct);
        return allowed ? File(PreformedRosterCsvImportService.Template(), "text/csv; charset=utf-8", "preformed-roster-template.csv") : NotFound();
    }
    public async Task<IActionResult> OnPostPreviewRosterCsvAsync(Guid id, Guid teamId, IFormFile? csv, CancellationToken ct)
    {
        RosterTeamId = teamId;
        if (csv is null || csv.Length == 0) { CsvFileError = true; CsvPreview = PreformedRosterCsvImportService.Preview.Invalid("Choose a CSV file to preview."); }
        else if (csv.Length > PreformedRosterCsvImportService.MaxBytes) CsvPreview = PreformedRosterCsvImportService.Preview.Invalid("The CSV file is larger than 1 MB.");
        else if (csvImport is null) CsvPreview = PreformedRosterCsvImportService.Preview.Invalid("Roster CSV import is unavailable.");
        else { await using var stream = csv.OpenReadStream(); CsvPreview = await csvImport.PreviewAsync(AdminId, id, teamId, stream, ct); }
        if (!await Load(id, null, ct)) return NotFound();
        RosterTeamId = teamId;
        return Page();
    }
    public async Task<IActionResult> OnPostApplyRosterCsvAsync(Guid id, Guid teamId, string? previewToken, CancellationToken ct)
    {
        if (await db.DraftSessions.AsNoTracking().AnyAsync(x => x.EventId == id && x.State == DraftState.Finalized, ct)) { SetStatus(Localize("Use the confirmed manual correction path after roster publication."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId = teamId }); }
        if (string.IsNullOrWhiteSpace(previewToken)) { SetStatus(Localize("Preview the CSV again before applying it."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId = teamId }); }
        if (csvImport is null) { SetStatus(Localize("Roster CSV import is unavailable."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId = teamId }); }
        var result = await csvImport.ApplyAsync(AdminId, User.Identity?.Name ?? "Admin", id, teamId, previewToken, ct);
        SetStatus(result.Message, result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id, rosterTeamId = teamId });
    }
    public async Task<IActionResult> OnPostRemoveMemberAsync(Guid id, Guid membershipId, string? reason, CancellationToken ct, bool confirmed = false, Guid? rosterTeamId = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        var membership = await db.TeamMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.LeftAt == null, ct);
        if (ev is null || membership is null) return NotFound();
        var team = await db.Teams.SingleAsync(x => x.Id == membership.TeamId, ct);
        if (team.EventId != id || !CanDirectPreEventRosterMutation(ev)) { SetStatus(Localize("Direct roster removal is available only before the configured event start."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if ((team.FormationType == TeamFormationType.Drafted && ev.DraftLocked) || (team.FormationType == TeamFormationType.Preformed && draft?.State == DraftState.Finalized && !confirmed)) { SetStatus(Localize("Drafted rosters lock when the draft starts; published pre-formed corrections require confirmation."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var correctionReason = team.FormationType == TeamFormationType.Preformed ? "Pre-formed roster correction" : reason?.Trim() ?? "Roster removal";
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == membership.EventParticipantId, ct); var participantName = await PrimaryName(participant.Id, ct); var now = time.GetUtcNow(); var previous = membership.Role;
        membership.Leave(now, correctionReason); if (previous is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain) db.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(Guid.NewGuid(), membership.Id, previous, TeamMembershipRole.Participant, AdminId, now));
        if (participant.Source == SignupSource.AdminCreated) { participant.Withdraw(now, correctionReason, AdminId); await characterService.ReleaseAllAsync(participant.Id, AdminId, ct); }
        if (draft?.State == DraftState.Finalized) { await db.SaveChangesAsync(ct); await RepublishPreformedCorrectionAsync(ev, draft, "team.member_removed", membership.Id, ct); }
        else await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "team.member_removed", "membership", membership.Id.ToString(), correctionReason, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); SetStatus(Localize("{0} removed from {1}.", participantName, team.Name), UiMessageType.Success); return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostChangeRoleAsync(Guid id, Guid membershipId, TeamMembershipRole role, CancellationToken ct, Guid? rosterTeamId = null)
    {
        if (captainAuthority is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var result = await captainAuthority.ChangeRoleAsync(new(id, membershipId, role, AdminId, User.Identity?.Name ?? "Admin"), ct);
        SetStatus(result.Succeeded ? Localize("{0} is now {1}.", result.ParticipantName ?? string.Empty, RoleLabel(role)) : result.Error ?? Localize("The role could not be changed."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostMoveMemberAsync(Guid id, Guid membershipId, Guid targetTeamId, CancellationToken ct, bool confirmed = false, Guid? rosterTeamId = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); var membership = await db.TeamMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.LeftAt == null, ct);
        if (ev is null || membership is null) return NotFound();
        var source = await db.Teams.SingleAsync(x => x.Id == membership.TeamId, ct); var target = await db.Teams.SingleOrDefaultAsync(x => x.Id == targetTeamId && x.EventId == id, ct);
        if (!CanDirectPreEventRosterMutation(ev) || source.FormationType != TeamFormationType.Preformed || target?.FormationType != TeamFormationType.Preformed) { SetStatus(Localize("Direct roster movement is available only for pre-formed teams before the configured event start."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft?.State == DraftState.Finalized && !confirmed) { SetStatus(Localize("Confirm this published pre-formed correction."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var participantName = await PrimaryName(membership.EventParticipantId, ct); var now = time.GetUtcNow(); var previous = membership.Role; const string correctionReason = "Pre-formed roster correction"; membership.Leave(now, correctionReason);
        if (previous is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain) db.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(Guid.NewGuid(), membership.Id, previous, TeamMembershipRole.Participant, AdminId, now));
        foreach (var access in await db.AccountEventAccesses.Where(x => x.EventId == id && x.TeamId == source.Id && x.ParticipantId == membership.EventParticipantId && x.Enabled).ToListAsync(ct)) access.Disable();
        var replacement = new TeamMembership(Guid.NewGuid(), target.Id, membership.EventParticipantId, previous, now, null, correctionReason); replacement.SetSource(TeamMembershipSource.Replacement, membership.Id); db.TeamMemberships.Add(replacement);
        if (draft?.State == DraftState.Finalized) { await db.SaveChangesAsync(ct); await RepublishPreformedCorrectionAsync(ev, draft, "team.member_moved", membership.Id, ct); }
        else await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "team.member_moved", "membership", membership.Id.ToString(), $"{source.Name} → {target.Name}: {correctionReason}", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); SetStatus(Localize("{0} moved from {1} to {2}.", participantName, source.Name, target.Name), UiMessageType.Success); return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostScrambleAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct);
        if (draft.State is not (DraftState.Running or DraftState.Paused)) { SetStatus(Localize("The draft is not ready to scramble."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        if (draft.FirstPickRecordedAt is not null)
        {
            SetStatus(Localize("The team order cannot be changed after the first pick."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var teams = await db.Teams.Where(x => x.EventId == id && x.Active && x.IncludedInDraft).ToListAsync(ct);
        if (teams.Count < 2) { SetStatus(Localize("Add at least two drafted teams before scrambling the order."), UiMessageType.Error); return RedirectToPage(new { id }); }
        for (var i = teams.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (teams[i], teams[j]) = (teams[j], teams[i]);
        }
        for (var i = 0; i < teams.Count; i++) teams[i].SetDraftPosition(i + 1);
        draft.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration);
        await db.SaveChangesAsync(ct);
        await Audit("draft.order_scrambled", "draft", draft.Id, string.Join(", ", teams.Select(x => x.Name)), ct);
        SetStatus(Localize("Team order scrambled."), UiMessageType.Success);
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostStartAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (bingoEvent is null) return NotFound();
        if (bingoEvent.State != Bingo.Domain.Events.EventState.SignupClosed)
        {
            SetStatus(
                bingoEvent.State is Bingo.Domain.Events.EventState.Draft or Bingo.Domain.Events.EventState.SignupOpen
                    ? Localize("Close signup before starting the draft.")
                    : Localize("The draft can only start while the event is in Signup Closed."),
                UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct);
        if (draft.State != DraftState.Setup)
        {
            SetStatus(Localize("The draft has already started or finished."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }
        var now = time.GetUtcNow();
        if (draft.HasActiveController(now) && draft.ControllerAccountId != AdminId)
        {
            if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        }
        var teams = await OrderedDraftTeams(id, ct);
        var captainTeamIds = await db.TeamMemberships.AsNoTracking().Where(x => x.LeftAt == null && x.Role == TeamMembershipRole.Captain && teams.Select(team => team.Id).Contains(x.TeamId)).Select(x => x.TeamId).Distinct().ToListAsync(ct);
        var missingCaptains = teams.Where(team => !captainTeamIds.Contains(team.Id)).Select(team => team.Name).ToList();
        if (missingCaptains.Count > 0) { SetStatus(Localize("Assign a current Captain to every drafted team before starting: {0}.", string.Join(", ", missingCaptains)), UiMessageType.Error); return RedirectToPage(new { id }); }
        var derived = await DeriveDraftState(id, teams, null, ct);
        if (derived.Blockers.Count != 0) { SetStatus(string.Join(" ", derived.Blockers), UiMessageType.Error); return RedirectToPage(new { id }); }
        try
        {
            foreach (var team in teams) team.SetDraftPosition(null);
            if (draft.HasActiveController(now)) draft.RenewControl(AdminId, now, DraftControlLease.Duration);
            else draft.AcquireControl(AdminId, now, DraftControlLease.Duration);
            draft.Start(now); bingoEvent.SetDraftLocked(true); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch (Exception exception) when (IsDraftConflict(exception)) { return DraftConflict(id, exception); }
        await Audit("draft.started", "draft", draft.Id, $"{teams.Count} teams; awaiting team-order draw; {derived.Distribution.IncludedParticipants} included participants", ct); SetStatus(Localize("Draft started. Scramble the teams to draw the order."), UiMessageType.Success); await NotifyDraft(id, ct); return RedirectToPage(new { id });
    }
    public Task<IActionResult> OnPostConfigureAsync(Guid id, int teamCount, int targetSize, CancellationToken ct) => Task.FromResult<IActionResult>(BadRequest());
    public async Task<IActionResult> OnPostPauseAsync(Guid id, CancellationToken ct) { var d = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (!RequireControl(d, id)) return RedirectToPage(new { id }); try { d.Pause(); d.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration); await db.SaveChangesAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.paused", "draft", d.Id, "Paused by admin", ct); SetStatus(Localize("Draft paused."), UiMessageType.Success); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostResumeAsync(Guid id, CancellationToken ct) { var d = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (!RequireControl(d, id)) return RedirectToPage(new { id }); try { d.Resume(); d.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration); await db.SaveChangesAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.resumed", "draft", d.Id, "Resumed by admin", ct); SetStatus(Localize("Draft resumed."), UiMessageType.Success); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostPickAsync(Guid id, Guid participantId, CancellationToken ct)
    { await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct); var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (draft.State != DraftState.Running) return BadRequest(); if (!RequireControl(draft, id)) return RedirectToPage(new { id }); if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct)) { SetStatus(Localize("That participant is already assigned to a team."), UiMessageType.Error); return RedirectToPage(new { id }); } var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.Id == participantId && x.EventId == id && x.Source != SignupSource.AdminCreated && x.SignupStatus == SignupStatus.Confirmed, ct); if (participant is null) { SetStatus(Localize("That participant is not available for this pick."), UiMessageType.Error); return RedirectToPage(new { id }); } var participantName = await PrimaryName(participantId, ct); var teams = await OrderedDraftTeams(id, ct); if (teams.Count < 2 || teams.Any(x => x.DraftPosition is null)) { SetStatus(Localize("Scramble the teams before making the first pick."), UiMessageType.Error); return RedirectToPage(new { id }); } var derived = await DeriveDraftState(id, teams, null, ct); if (derived.Blockers.Count != 0) { SetStatus(string.Join(" ", derived.Blockers), UiMessageType.Error); return RedirectToPage(new { id }); } var teamIds = teams.Select(x => x.Id).ToList(); var activePickTeams = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).OrderBy(x => x.PickNumber).Select(x => x.TeamId).ToListAsync(ct); var turn = SnakeDraftOrder.GetNextEligibleTurn(activePickTeams, teamIds, derived.RosterSizes, derived.Distribution); if (turn is null) { SetStatus(Localize("Every derived drafted-team place is filled."), UiMessageType.Error); return RedirectToPage(new { id }); } var pick = new DraftPick(Guid.NewGuid(), draft.Id, turn.TeamId, participantId, turn.PickNumber, turn.RoundNumber, time.GetUtcNow()); draft.RecordFirstPick(time.GetUtcNow()); db.DraftPicks.Add(pick); var membership = new TeamMembership(Guid.NewGuid(), turn.TeamId, participantId, TeamMembershipRole.Participant, time.GetUtcNow(), pick.Id, "Snake draft pick"); membership.SetSource(TeamMembershipSource.DraftPick); db.TeamMemberships.Add(membership); try { draft.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.pick_recorded", "pick", pick.Id, $"#{turn.PickNumber} {participantName}", ct); SetStatus(Localize("{0} picked for {1}.", participantName, turn.TeamId), UiMessageType.Success); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostUndoAsync(Guid id, CancellationToken ct)
    { await using var tx = await db.Database.BeginTransactionAsync(ct); var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (draft.State is not (DraftState.Running or DraftState.Paused)) return BadRequest(); if (!RequireControl(draft, id)) return RedirectToPage(new { id }); var pick = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).OrderByDescending(x => x.PickNumber).FirstOrDefaultAsync(ct); if (pick is null) { SetStatus(Localize("There is no active pick to undo."), UiMessageType.Error); return RedirectToPage(new { id }); } var membership = await db.TeamMemberships.SingleAsync(x => x.AssignedByDraftPickId == pick.Id && x.LeftAt == null, ct); pick.Undo(time.GetUtcNow()); membership.Leave(time.GetUtcNow(), "Draft pick undone"); try { draft.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.pick_undone", "pick", pick.Id, $"#{pick.PickNumber}", ct); SetStatus(Localize("Pick #{0} undone.", pick.PickNumber), UiMessageType.Success); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (draft is null || bingoEvent is null) return NotFound();
        if (draft.State is not (DraftState.Running or DraftState.Paused) || bingoEvent.TeamRostersPublished || bingoEvent.DraftResultsPublished) return BadRequest();
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        try
        {
            var now = time.GetUtcNow();
            var activePicks = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).ToListAsync(ct);
            var pickIds = activePicks.Select(x => x.Id).ToList();
            var memberships = await db.TeamMemberships.Where(x => pickIds.Contains(x.AssignedByDraftPickId!.Value) && x.LeftAt == null).ToListAsync(ct);
            foreach (var pick in activePicks) pick.Undo(now);
            foreach (var membership in memberships) membership.Leave(now, "Draft cancelled and returned to setup");
            foreach (var team in await db.Teams.Where(x => x.EventId == id && x.Active && x.IncludedInDraft).ToListAsync(ct)) team.SetDraftPosition(null);
            draft.ReturnToSetup();
            bingoEvent.SetDraftLocked(false);
            await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "draft.cancelled", "draft", draft.Id.ToString(), $"Returned to setup; undone active picks: {activePicks.Count}", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            SetStatus(Localize("Draft returned to setup. Active picks were undone and remain in the draft history."), UiMessageType.Success);
        }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostFinalizeAsync(Guid id, CancellationToken ct, bool confirmed = false)
    {
        if (!confirmed) { SetStatus(Localize("Confirm finalization before publishing the roster."), UiMessageType.Error); return RedirectToPage(new { id }); }
        var published = false;
        var offerBoardPublication = false;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var now = time.GetUtcNow();
            var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
            var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
            if (bingoEvent is null || draft is null) return NotFound();
            if (bingoEvent.State != Bingo.Domain.Events.EventState.SignupClosed || bingoEvent.ActualStartedAt is not null || bingoEvent.EventEndsAt is not { } ends || ends <= now)
                throw new InvalidOperationException("The draft can only be finalized before the event has started and while its configured end remains in the future.");
            if (draft.State is not (DraftState.Running or DraftState.Paused)) throw new InvalidOperationException("This draft is already finalized or is not running.");
            draft.RequireControl(AdminId, now);
            var draftedTeams = await OrderedDraftTeams(id, ct);
            var derived = await DeriveDraftState(id, draftedTeams, null, ct);
            if (derived.Blockers.Count != 0) throw new InvalidOperationException(string.Join(" ", derived.Blockers));
            if (draftedTeams.Count < 2 || draftedTeams.Any(x => x.DraftPosition is null)) throw new InvalidOperationException("Scramble the drafted teams before finalizing.");
            var includedParticipantIds = derived.IncludedParticipantIds;
            var draftedMembershipIds = await db.TeamMemberships.Where(x => x.LeftAt == null && includedParticipantIds.Contains(x.EventParticipantId) && draftedTeams.Select(t => t.Id).Contains(x.TeamId)).Select(x => x.EventParticipantId).ToListAsync(ct);
            if (draftedMembershipIds.Count != includedParticipantIds.Count || draftedMembershipIds.Distinct().Count() != includedParticipantIds.Count)
                throw new InvalidOperationException("Every included participant must have exactly one active drafted-team membership before finalization.");
            var activeMembers = await db.TeamMemberships.Where(x => x.LeftAt == null && db.Teams.Any(t => t.Id == x.TeamId && t.EventId == id && t.Active)).ToListAsync(ct);
            if (activeMembers.Count == 0) throw new InvalidOperationException("A final roster is required before finalization.");
            var nextCycle = (await db.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).Select(x => (int?)x.CycleNumber).MaxAsync(ct) ?? 0) + 1;
            var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, nextCycle, now, AdminId);
            db.DraftPublicationCycles.Add(cycle);
            var pickNumbers = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).ToDictionaryAsync(x => x.Id, x => x.PickNumber, ct);
            var publicNames = await FrozenPublicNamesAsync(id, activeMembers.Select(x => x.EventParticipantId), now, ct);
            foreach (var member in activeMembers)
                db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, member.TeamId, member.EventParticipantId, member.Role,
                    member.AssignedByDraftPickId is { } pickId && pickNumbers.TryGetValue(pickId, out var pickNumber) ? pickNumber : null,
                    publicNames[member.EventParticipantId]));
            foreach (var team in await db.Teams.Where(x => x.EventId == id && x.Active).ToListAsync(ct)) team.Finalize(now);
            draft.Finalize(now);
            bingoEvent.SetDraftRosterPublication(true);
            await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "draft.finalized", "draft", draft.Id.ToString(), $"Publication cycle {nextCycle}; {activeMembers.Count} frozen roster entries", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            published = true;
            offerBoardPublication = await db.Boards.AsNoTracking().AnyAsync(x => x.EventId == id && x.State == BoardState.Validated && x.ActiveApprovalSnapshotId != null, ct);
            SetStatus(offerBoardPublication
                ? Localize("Draft finalized and team rosters published. The board is approved and ready to publish separately.")
                : Localize("Draft finalized and team rosters published."), UiMessageType.Success);
        }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        catch (InvalidOperationException ex) { SetStatus(ex.Message, UiMessageType.Error); }
        catch (Exception) { SetStatus(Localize("The draft could not be finalized. No roster was published."), UiMessageType.Error); }
        if (published) await NotifyDraft(id, ct);
        if (published && offerBoardPublication)
        {
            SetStatus(Localize("Publish board? The approved board is ready. Publishing it is a separate action."), UiMessageType.Information);
            return RedirectToPage("Board", new { id });
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReopenAsync(Guid id, bool confirmed, string? reason, CancellationToken ct)
    {
        if (!confirmed || string.IsNullOrWhiteSpace(reason)) { SetStatus(Localize("Confirm reopening and provide an Admin reason."), UiMessageType.Error); return RedirectToPage(new { id }); }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var now = time.GetUtcNow();
            var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
            var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
            if (bingoEvent is null || draft is null) return NotFound();
            if (bingoEvent.State != Bingo.Domain.Events.EventState.SignupClosed || bingoEvent.ActualStartedAt is not null || bingoEvent.EventEndsAt is not { } ends || ends <= now)
                throw new InvalidOperationException("A published draft can only be reopened before the event has started and while its configured end remains in the future.");
            if (draft.State != DraftState.Finalized) throw new InvalidOperationException("This draft is not finalized.");
            var activeCycle = await db.DraftPublicationCycles.SingleOrDefaultAsync(x => x.DraftSessionId == draft.Id && x.SupersededAt == null, ct);
            if (activeCycle is null) throw new InvalidOperationException("The active roster publication no longer exists.");
            activeCycle.Supersede(now, AdminId, reason.Trim());
            draft.Reopen(now);
            bingoEvent.SetDraftRosterPublication(false);
            await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", "draft.reopened", "draft", draft.Id.ToString(), $"Publication cycle {activeCycle.CycleNumber}; {reason.Trim()}", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            SetStatus(Localize("Draft reopened. The public roster is withdrawn while corrections are made."), UiMessageType.Success);
        }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        catch (InvalidOperationException ex) { SetStatus(ex.Message, UiMessageType.Error); }
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAcquireControlAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) return NotFound();
        try
        {
            var previous = draft.AcquireControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration);
            await db.SaveChangesAsync(ct);
            await Audit("draft.control_acquired", "draft", draft.Id, $"Controller: {User.Identity!.Name}", ct);
        }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        SetStatus(Localize("Draft control acquired."), UiMessageType.Success);
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTakeControlAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        if (!confirmed) { SetStatus(Localize("Confirm takeover to replace the current draft controller."), UiMessageType.Error); return RedirectToPage(new { id }); }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) return NotFound();
        try
        {
            var previous = draft.AcquireControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration, force: true);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await Audit("draft.control_taken_over", "draft", draft.Id, $"New controller: {User.Identity!.Name}; previous account: {previous}", ct);
        }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        SetStatus(Localize("Draft control taken over."), UiMessageType.Success);
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReleaseControlAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) return NotFound();
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        try { draft.ReleaseControl(AdminId, time.GetUtcNow()); await db.SaveChangesAsync(ct); }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        await Audit("draft.control_released", "draft", draft.Id, $"Released by {User.Identity!.Name}", ct);
        SetStatus(Localize("Draft control released."), UiMessageType.Success);
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }

    private async Task<string> AddMembership(Guid eventId, Guid teamId, Guid participantId, TeamMembershipRole role, string reason, Guid? pickId, CancellationToken ct) { var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == eventId, ct); if (team is null) throw new InvalidOperationException("The selected team no longer exists."); if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct)) throw new InvalidOperationException("Participant is already assigned to a team."); var membership = new TeamMembership(Guid.NewGuid(), teamId, participantId, role, time.GetUtcNow(), pickId, reason); membership.SetSource(team.FormationType == TeamFormationType.Preformed ? TeamMembershipSource.PreformedManual : TeamMembershipSource.RetainedConversion); db.TeamMemberships.Add(membership); await db.SaveChangesAsync(ct); await Audit("team.member_added", "team", teamId, $"{participantId}: {reason}", ct); return team.Name; }
    private bool CanDirectPreEventRosterMutation(Bingo.Domain.Events.BingoEvent bingoEvent) =>
        (bingoEvent.State is Bingo.Domain.Events.EventState.Draft or Bingo.Domain.Events.EventState.SignupOpen or Bingo.Domain.Events.EventState.SignupClosed)
        && bingoEvent.ActualStartedAt is null && bingoEvent.EventEndsAt is { } ends && time.GetUtcNow() < ends;
    private bool CanDirectDraftedSetupAssignment(Bingo.Domain.Events.BingoEvent bingoEvent, DraftSession? draft) =>
        CanDirectPreEventRosterMutation(bingoEvent)
        && draft is { FirstPickRecordedAt: null, State: DraftState.Setup or DraftState.Running };
    private async Task<Dictionary<Guid, string>> FrozenPublicNamesAsync(Guid eventId, IEnumerable<Guid> participantIds, DateTimeOffset publishedAt, CancellationToken ct)
    {
        var ids = participantIds.Distinct().ToList();
        var rows = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                          join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                          where assignment.EventId == eventId && ids.Contains(assignment.EventParticipantId) && assignment.EventRole == EventCharacterRole.Playing && assignment.RegisteredAt <= publishedAt && (assignment.ReleasedAt == null || assignment.ReleasedAt > publishedAt)
                          select new { assignment.EventParticipantId, character.DisplayName }).ToListAsync(ct);
        var names = rows.GroupBy(x => x.EventParticipantId).ToDictionary(x => x.Key, x => x.Select(y => y.DisplayName).Distinct().ToList());
        if (names.Count != ids.Count || names.Values.Any(x => x.Count != 1 || string.IsNullOrWhiteSpace(x[0]))) throw new InvalidOperationException("Every published roster entry requires exactly one retained playing-character identity.");
        return names.ToDictionary(x => x.Key, x => x.Value[0]);
    }
    private async Task RepublishPreformedCorrectionAsync(Bingo.Domain.Events.BingoEvent bingoEvent, DraftSession draft, string action, Guid targetId, CancellationToken ct)
    {
        if (draft.State != DraftState.Finalized || !CanDirectPreEventRosterMutation(bingoEvent)) throw new InvalidOperationException("Published pre-formed corrections are unavailable after event start.");
        var now = time.GetUtcNow();
        var activeCycle = await db.DraftPublicationCycles.SingleOrDefaultAsync(x => x.DraftSessionId == draft.Id && x.SupersededAt == null, ct) ?? throw new InvalidOperationException("The active roster publication no longer exists.");
        var activeMembers = await db.TeamMemberships.Where(x => x.LeftAt == null && db.Teams.Any(t => t.Id == x.TeamId && t.EventId == bingoEvent.Id && t.Active)).ToListAsync(ct);
        var names = await FrozenPublicNamesAsync(bingoEvent.Id, activeMembers.Select(x => x.EventParticipantId), now, ct);
        var picks = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).ToDictionaryAsync(x => x.Id, x => x.PickNumber, ct);
        const string correctionReason = "Pre-formed roster correction";
        activeCycle.Supersede(now, AdminId, correctionReason);
        var nextCycle = (await db.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).Select(x => (int?)x.CycleNumber).MaxAsync(ct) ?? 0) + 1;
        var replacement = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, nextCycle, now, AdminId); db.DraftPublicationCycles.Add(replacement);
        foreach (var member in activeMembers) db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), replacement.Id, member.TeamId, member.EventParticipantId, member.Role, member.AssignedByDraftPickId is { } pick && picks.TryGetValue(pick, out var number) ? number : null, names[member.EventParticipantId]));
        bingoEvent.SetDraftRosterPublication(true);
        await audit.WriteAsync(AdminId, User.Identity?.Name ?? "Admin", action, "draft_publication", targetId.ToString(), $"Superseded publication cycle {activeCycle.CycleNumber} for pre-formed correction.", ct);
    }
    private async Task<List<Team>> OrderedDraftTeams(Guid id, CancellationToken ct) => await db.Teams.Where(x => x.EventId == id && x.Active && x.IncludedInDraft).OrderBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
    private async Task<string> UniqueTeamSlug(Guid eventId, string name, CancellationToken ct) { var root = EventSlugGenerator.Generate(name); var slug = root; for (var n = 2; await db.Teams.AnyAsync(x => x.EventId == eventId && x.Slug == slug, ct); n++) slug = $"{root}-{n}"; return slug; }
    private async Task<bool> Load(Guid id, string? sort, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ev is null) return false;
        EventId = id;
        EventName = ev.Name;
        EventTimezone = ev.Timezone;
        Sort = sort is "name" or "signup" or "status" ? sort : "ehb";
        var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) { ConfirmedCount = await db.EventParticipants.CountAsync(x => x.EventId == id && x.Source != SignupSource.AdminCreated && x.SignupStatus == SignupStatus.Confirmed, ct); Distribution = DraftRosterDistribution.Derive(ConfirmedCount, 0); return true; }

        Draft = new(draft.Id, draft.State, draft.FirstPickRecordedAt is not null);
        var teams = await db.Teams.AsNoTracking().Where(x => x.EventId == id && x.Active).OrderBy(x => x.IncludedInDraft ? 0 : 1).ThenBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
        var memberships = await db.TeamMemberships.AsNoTracking().Where(x => x.LeftAt == null && teams.Select(t => t.Id).Contains(x.TeamId)).ToListAsync(ct);
        var assignedParticipantIds = memberships.Select(x => x.EventParticipantId).ToList();
        var participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == id && (x.SignupStatus == SignupStatus.Confirmed || assignedParticipantIds.Contains(x.Id))).ToListAsync(ct);
        var authorities = await db.AdminPrimaryCharacters().AsNoTracking().Where(x => x.EventId == id).ToDictionaryAsync(x => x.ParticipantId, ct);
        EventParticipantAuthority DisplayAuthority(Guid participantId) => authorities.GetValueOrDefault(participantId) ?? new EventParticipantAuthority
        {
            ParticipantId = participantId,
            EventId = id,
            Name = "External roster member",
            Ehb = 0m
        };
        var usableCaptainTeamIds = await (from membership in db.TeamMemberships.AsNoTracking()
                                          join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                          join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
                                          where membership.LeftAt == null && membership.Role == TeamMembershipRole.Captain && account.Active && account.AccountType == Bingo.Domain.Access.AccountType.WebsiteAccount && participant.EventId == id
                                          select membership.TeamId).Distinct().ToListAsync(ct);
        var emergencyTeamIds = await (from access in db.AccountEventAccesses.AsNoTracking()
                                      join account in db.Accounts.AsNoTracking() on access.AccountId equals account.Id
                                      where access.EventId == id && access.Enabled && account.Active && account.AccountType == Bingo.Domain.Access.AccountType.EmergencyCaptain
                                      select access.TeamId).Distinct().ToListAsync(ct);
        var eligibleParticipants = participants.Where(x => x.Source != SignupSource.AdminCreated && x.SignupStatus == SignupStatus.Confirmed).ToList();
        var teamMap = teams.ToDictionary(x => x.Id);
        var membershipByParticipant = memberships.ToDictionary(x => x.EventParticipantId);
        var internalParticipants = participants.Where(x => x.Source != SignupSource.AdminCreated);
        IEnumerable<EventParticipant> ordered = Sort switch
        {
            "name" => internalParticipants.OrderBy(x => DisplayAuthority(x.Id).Name).ThenBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id),
            "signup" => internalParticipants.OrderBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id),
            "status" => internalParticipants.OrderBy(x => membershipByParticipant.ContainsKey(x.Id)).ThenByDescending(x => DisplayAuthority(x.Id).Ehb).ThenBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id),
            _ => internalParticipants.OrderByDescending(x => DisplayAuthority(x.Id).Ehb).ThenBy(x => DisplayAuthority(x.Id).Name).ThenBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id)
        };
        Participants = ordered.Select(x =>
        {
            membershipByParticipant.TryGetValue(x.Id, out var membership);
            var authority = DisplayAuthority(x.Id);
            return new ParticipantView(x.Id, authority.Name, authority.Ehb, x.SignedUpAt, x.CaptainVolunteer, membership?.TeamId, membership is null ? null : teamMap[membership.TeamId].Name, x.SignupStatus);
        }).ToList();

        var activePicks = await db.DraftPicks.AsNoTracking().Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).OrderBy(x => x.PickNumber).ToListAsync(ct);
        var pickNumberByParticipant = activePicks.ToDictionary(x => x.EventParticipantId, x => x.PickNumber);
        var teamPickNumberByParticipant = activePicks.GroupBy(x => x.TeamId).SelectMany(group => group.OrderBy(x => x.PickNumber).Select((pick, index) => new { pick.EventParticipantId, TeamPickNumber = index + 1 })).ToDictionary(x => x.EventParticipantId, x => x.TeamPickNumber);
        var draftedOrder = teams.Where(x => x.IncludedInDraft).ToList();
        DraftOrderReady = draftedOrder.Count >= 2 && draftedOrder.All(x => x.DraftPosition is not null);
        var derived = await DeriveDraftState(id, draftedOrder, activePicks.Select(pick => pick.TeamId).ToList(), ct);
        if (draft.State == DraftState.Running && DraftOrderReady && derived.Blockers.Count == 0)
        {
            var orderedTeams = draftedOrder.OrderBy(x => x.DraftPosition).ToList();
            var turn = SnakeDraftOrder.GetNextEligibleTurn(activePicks.Select(x => x.TeamId).ToList(), orderedTeams.Select(x => x.Id).ToList(), derived.RosterSizes, derived.Distribution);
            if (turn is not null) CurrentTurn = new(turn.PickNumber, turn.RoundNumber, turn.TeamId, teamMap[turn.TeamId].Name);
        }
        var latest = activePicks.LastOrDefault();
        if (latest is not null) LatestPick = new(latest.PickNumber, DisplayAuthority(latest.EventParticipantId).Name, teamMap[latest.TeamId].Name);
        Teams = teams.Select(team => new TeamView(
            team.Id, team.Name, team.FormationType, team.AffiliationName, team.ActiveImageAssetId is null ? null : $"/Admin/Events/Draft/{id}?handler=TeamImage&teamId={team.Id}", team.DraftPosition, team.Version,
            CurrentTurn?.TeamId == team.Id, derived.ProjectedFinalSizes.GetValueOrDefault(team.Id, memberships.Count(membership => membership.TeamId == team.Id)),
            memberships.Where(m => m.TeamId == team.Id)
                .OrderBy(m => m.Role == TeamMembershipRole.Captain ? 0 : m.Role == TeamMembershipRole.CoCaptain ? 1 : 2)
                .ThenBy(m => pickNumberByParticipant.GetValueOrDefault(m.EventParticipantId, int.MaxValue))
                .Select(m =>
                {
                    var participant = participants.Single(p => p.Id == m.EventParticipantId);
                    var authority = DisplayAuthority(m.EventParticipantId);
                    teamPickNumberByParticipant.TryGetValue(m.EventParticipantId, out var pickNumber);
                    return new MemberView(m.Id, authority.Name, authority.Ehb, m.Role, participant.Source == SignupSource.AdminCreated, pickNumber == 0 ? null : pickNumber);
                }).ToList(), usableCaptainTeamIds.Contains(team.Id), emergencyTeamIds.Contains(team.Id),
            team.FormationType == TeamFormationType.Drafted
                ? memberships.Where(m => m.TeamId == team.Id).Sum(m => DisplayAuthority(m.EventParticipantId).Ehb)
                : 0m)).ToList();
        ConfirmedCount = eligibleParticipants.Count;
        AssignedCount = memberships.Count(m => participants.Single(p => p.Id == m.EventParticipantId).Source != SignupSource.AdminCreated);
        AvailableCount = eligibleParticipants.Count(x => !membershipByParticipant.ContainsKey(x.Id));
        Distribution = derived.Distribution;
        Remainder = Distribution.LargerTeamCount;
        return true;
    }
    private async Task<DerivedDraftState> DeriveDraftState(Guid eventId, IReadOnlyList<Team> draftedTeams, List<Guid>? activePickTeamIds, CancellationToken ct)
    {
        var teamIds = draftedTeams.Select(team => team.Id).ToList();
        var allActiveMemberships = await db.TeamMemberships.AsNoTracking()
            .Where(membership => membership.LeftAt == null && db.Teams.Any(team => team.Id == membership.TeamId && team.EventId == eventId && team.Active))
            .ToListAsync(ct);
        var formationByTeam = await db.Teams.AsNoTracking().Where(team => team.EventId == eventId && team.Active)
            .ToDictionaryAsync(team => team.Id, team => team.FormationType, ct);
        var includedParticipants = await db.EventParticipants.AsNoTracking()
            .Where(participant => participant.EventId == eventId && participant.Source != SignupSource.AdminCreated && participant.SignupStatus == SignupStatus.Confirmed)
            .ToListAsync(ct);
        var included = includedParticipants.Where(participant => !allActiveMemberships.Any(membership =>
            membership.EventParticipantId == participant.Id &&
            formationByTeam.GetValueOrDefault(membership.TeamId) == TeamFormationType.Preformed)).ToList();
        var rosterSizes = teamIds.ToDictionary(teamId => teamId, teamId => allActiveMemberships.Count(membership => membership.TeamId == teamId));
        var distribution = DraftRosterDistribution.Derive(included.Count, teamIds.Count);
        var blockers = distribution.ValidateCurrentRosters(rosterSizes).ToList();
        var primaryCount = await db.PrimaryCharacters().AsNoTracking().Where(character => included.Select(participant => participant.Id).Contains(character.ParticipantId)).CountAsync(ct);
        if (primaryCount != included.Count)
            blockers.Add("Every included confirmed participant must retain one valid primary-account reservation.");
        var orderedIds = draftedTeams.OrderBy(team => team.DraftPosition ?? int.MaxValue).ThenBy(team => team.Name).Select(team => team.Id).ToList();
        var projected = blockers.Count == 0
            ? activePickTeamIds is not null && activePickTeamIds.Count != 0
                ? SnakeDraftOrder.ProjectFinalRosterSizes(activePickTeamIds, orderedIds, rosterSizes, distribution)
                : distribution.AllocateNamedFinalSizes(orderedIds, rosterSizes)
            : rosterSizes;
        if (blockers.Count == 0 && projected.Values.Sum() != distribution.IncludedParticipants)
            blockers.Add("The included participants cannot be exhausted into balanced drafted-team rosters.");
        return new(included.Select(participant => participant.Id).ToList(), distribution, rosterSizes, projected, blockers);
    }
    private Task<string> PrimaryName(Guid participantId, CancellationToken ct) =>
        db.PrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => x.Name).SingleAsync(ct);
    private Guid AdminId => User.GetAccountId()!.Value;
    private static bool IsDraftConflict(Exception exception) => exception is InvalidOperationException or DbUpdateConcurrencyException
        or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation }
        or DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation } };
    private bool RequireControl(DraftSession draft, Guid eventId)
    {
        try { draft.RequireControl(AdminId, time.GetUtcNow()); return true; }
        catch (InvalidOperationException ex) { SetStatus(ex.Message, UiMessageType.Error); return false; }
    }
    private RedirectToPageResult DraftConflict(Guid id, Exception exception)
    {
        db.ChangeTracker.Clear();
        SetStatus(exception is DbUpdateConcurrencyException
            ? Localize("Another administrator changed the draft first. Nothing from your stale action was saved; the latest draft has been loaded.")
            : exception.Message, UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    private async Task LoadControllerState(Guid eventId, CancellationToken ct)
    {
        var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (draft is null || !draft.HasActiveController(time.GetUtcNow())) return;
        DraftControllerAccountId = draft.ControllerAccountId;
        DraftControllerLeaseExpiresAt = draft.ControllerLeaseExpiresAt;
        DraftControllerName = await db.Accounts.AsNoTracking().Where(x => x.Id == draft.ControllerAccountId).Select(x => x.LoginName).SingleOrDefaultAsync(ct);
        CanControlDraft = draft.ControllerAccountId == AdminId;
    }
    private Task NotifyDraft(Guid eventId, CancellationToken ct) => collaboration.NotifyDraftChangedAsync(eventId, ct);
    private Task Audit(string action, string target, Guid targetId, string details, CancellationToken ct) => audit.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, target, targetId.ToString(), details, ct); private static string RoleLabel(TeamMembershipRole role) => role == TeamMembershipRole.CoCaptain ? "co-captain" : role.ToString().ToLowerInvariant(); private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    private void StoreCredentials(IReadOnlyList<GeneratedCaptainCredential> credentials) { if (credentials.Count > 0) TempData["GeneratedCaptainCredentials"] = JsonSerializer.Serialize(credentials); }
    private sealed record DerivedDraftState(IReadOnlyList<Guid> IncludedParticipantIds, DraftRosterDistribution Distribution, IReadOnlyDictionary<Guid, int> RosterSizes, IReadOnlyDictionary<Guid, int> ProjectedFinalSizes, IReadOnlyList<string> Blockers);
    public sealed record DraftView(Guid Id, DraftState State, bool FirstPickRecorded); public sealed record ParticipantView(Guid Id, string Name, decimal Ehb, DateTimeOffset SignedUpAt, bool CaptainVolunteer, Guid? TeamId, string? TeamName, SignupStatus SignupStatus); public sealed record TeamView(Guid Id, string Name, TeamFormationType FormationType, string? Affiliation, string? ImageUrl, int? DraftPosition, long Version, bool IsCurrent, int ProjectedFinalSize, IReadOnlyList<MemberView> Members, bool HasUsableCaptain, bool HasEnabledEmergencyAccess, decimal TotalEhb); public sealed record MemberView(Guid MembershipId, string Name, decimal Ehb, TeamMembershipRole Role, bool External, int? PickNumber); public sealed record TurnView(int PickNumber, int RoundNumber, Guid TeamId, string TeamName); public sealed record PickView(int PickNumber, string PlayerName, string TeamName);
}
