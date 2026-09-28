using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Teams;
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
public sealed class DraftModel(ApplicationDbContext db, TimeProvider time, IAuditWriter audit, IAdminCollaborationNotifier collaboration, ISignupService signupService, EventParticipantCharacterService characterService, IEvidenceStorage? storage = null, ITeamCaptainAuthorityService? captainAuthority = null, IStringLocalizer<SharedResource>? text = null, IWiseOldManAccountValidation? accountValidation = null) : PageModel
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
    public Guid CurrentAccountId { get; private set; }
    public bool CanControlDraft { get; private set; }
    public bool CanChangeCaptainRoles { get; private set; }
    public bool CanCorrectPreLiveRoster { get; private set; }
    public Guid? DraftControllerAccountId { get; private set; }
    public Guid? RosterTeamId { get; private set; }
    public string? DraftControllerName { get; private set; }
    public DateTimeOffset? DraftControllerLeaseExpiresAt { get; private set; }
    public bool DraftOrderReady { get; private set; }
    public bool CanDirectFinalize { get; private set; }
    public string? WomValidationConfirmationToken { get; private set; }
    public IReadOnlyList<RosterAccountOption> RosterWebsiteAccounts { get; private set; } = [];
    private static readonly HashSet<string> RetiredLegacyHandlers = new(StringComparer.OrdinalIgnoreCase)
    {
        "RemoveExternalTeam", "AddExternalMember", "RosterCsvTemplate", "PreviewRosterCsv", "ApplyRosterCsv",
        "Pause", "Resume", "Reopen"
    };

    public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
    {
        if (context.HttpContext.Request.Query.TryGetValue("handler", out var requestedHandler) && RetiredLegacyHandlers.Contains(requestedHandler.ToString()))
            context.Result = NotFound();
    }

    public async Task<IActionResult> OnGetAsync(Guid id, string? sort, CancellationToken ct, Guid? rosterTeamId = null)
    {
        _ = characterService;
        _ = accountValidation;
        CurrentAccountId = AdminId;
        WomValidationConfirmationToken = TempData.Peek("WomValidationConfirmationToken") as string;
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

    public async Task<IActionResult> OnPostAddTeamAsync(Guid id, string name, TeamFormationType? formationType, string? affiliation, CancellationToken ct, bool confirmed = false, bool includedInDraft = true)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ev is null) return NotFound();
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) { draft = new DraftSession(Guid.NewGuid(), id, 1); db.DraftSessions.Add(draft); }
        if (draft.State is DraftState.Running or DraftState.Paused) { SetStatus(Localize("Team structure is locked while a private draft is active; cancel the private draft to return to setup."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (string.IsNullOrWhiteSpace(name)) { SetStatus(Localize("A team name is required."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (WiseOldManCompetitionRules.ProviderCharacterCount(name.Trim()) > WiseOldManCompetitionRules.MaximumTeamNameLength) { SetStatus(Localize("Team names must be 30 characters or fewer."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (!CanDirectPreEventRosterMutation(ev) && !CanCreateInitialPrivateTeam(ev, draft)) { SetStatus(Localize("Direct roster changes are available only before the configured event start."), UiMessageType.Error); return RedirectToPage(new { id }); }
        // Keep the retired enum parameter only for old direct callers; new team
        // participation is controlled exclusively by IncludedInDraft.
        var includeInDraft = includedInDraft;
        if (draft.State == DraftState.Finalized && includeInDraft) { SetStatus(Localize("Teams included in the website draft cannot be added after the draft has been completed."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (draft.State == DraftState.Finalized && !confirmed) { SetStatus(Localize("Confirm this published roster correction."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (await db.Teams.AnyAsync(team => team.EventId == id && team.Active && team.Name == name.Trim(), ct)) { SetStatus(Localize("A team with that name already exists for this event."), UiMessageType.Error); return RedirectToPage(new { id }); }
        var team = new Team(Guid.NewGuid(), id, name.Trim(), await UniqueTeamSlug(id, name, ct), Clean(affiliation), includeInDraft, time.GetUtcNow());
        db.Teams.Add(team);
        try
        {
            if (draft.State == DraftState.Finalized)
                await RepublishPreformedCorrectionAsync(ev, draft, "team.preformed_corrected", team.Id, ct);
            else
                await audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", "team.created", "team", team.Id.ToString(), JsonSerializer.Serialize(new { team.IncludedInDraft, team.AffiliationName }), ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); SetStatus(Localize("A team with that name already exists for this event."), UiMessageType.Error); return RedirectToPage(new { id }); }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        SetStatus(Localize("{0} created.", team.Name), UiMessageType.Success); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostRemoveDraftTeamAsync(Guid id, Guid teamId, CancellationToken ct)
    { var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft is null) return NotFound(); if (draft.State != DraftState.Setup) { SetStatus(Localize("Included-team structure is locked while a private draft is active or published."), UiMessageType.Error); return RedirectToPage(new { id }); } var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id && x.IncludedInDraft && x.Active, ct); if (team is null) return NotFound(); if (await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.LeftAt == null, ct)) { SetStatus(Localize("Remove the players from {0} before removing the team.", team.Name), UiMessageType.Error); return RedirectToPage(new { id }); } var before = TeamAuditState(team); team.SetActive(false); team.SetDraftPosition(null); await AuditMutation(id, "draft.team_removed", "team", team.Id, before, TeamAuditState(team), ct); SetStatus(Localize("{0} removed from the website draft.", team.Name), UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostWithdrawParticipantAsync(Guid id, Guid participantId, CancellationToken ct)
    { var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft?.State != DraftState.Setup) { SetStatus(Localize("Participants cannot be withdrawn after the draft starts."), UiMessageType.Error); return RedirectToPage(new { id }); } var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.Id == participantId && x.EventId == id, ct); if (participant is null) return NotFound(); if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct)) { SetStatus(Localize("Remove this player from their roster before withdrawing them from the event."), UiMessageType.Error); return RedirectToPage(new { id }); } var result = await signupService.WithdrawAsync(id, participantId, AdminId, User.Identity?.Name ?? "Admin", true, cancellationToken: ct); SetStatus(result.Succeeded ? Localize("Participant withdrawn. The waiting list was promoted where a place became available.") : result.Error ?? Localize("The participant could not be withdrawn."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostUpdateTeamAsync(Guid id, Guid teamId, string name, string? affiliation, IFormFile? image, bool removeImage, long version, CancellationToken ct, Guid? rosterTeamId = null, bool? includedInDraft = null)
    {
        var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id, ct); if (team is null) return NotFound();
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var currentDraft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (currentDraft?.State == DraftState.Paused) { SetStatus(Localize("This historical paused draft is read-only; no roster changes are available."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (ev.ActualStartedAt is not null || team.Version != version || string.IsNullOrWhiteSpace(name)) { SetStatus(ev.ActualStartedAt is not null ? Localize("Team metadata is locked after event start.") : Localize("This team changed; reload and try again."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (WiseOldManCompetitionRules.ProviderCharacterCount(name.Trim()) > WiseOldManCompetitionRules.MaximumTeamNameLength && !string.Equals(team.Name, name.Trim(), StringComparison.Ordinal)) { SetStatus(Localize("Team names must be 30 characters or fewer."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var requestedInclusion = includedInDraft ?? team.IncludedInDraft;
        var inclusionChanged = requestedInclusion != team.IncludedInDraft;
        var draft = inclusionChanged ? await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct) : null;
        if (inclusionChanged && draft is { State: not DraftState.Setup }) { SetStatus(Localize("Website-draft inclusion can only change during team setup."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (inclusionChanged && !CanDirectPreEventRosterMutation(ev)) { SetStatus(Localize("Website-draft inclusion can only change before the configured event start."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var affectedParticipantIds = inclusionChanged
            ? await db.TeamMemberships.AsNoTracking().Where(membership => membership.TeamId == team.Id && membership.LeftAt == null).Select(membership => membership.EventParticipantId).ToListAsync(ct)
            : [];
        var before = TeamAuditState(team);
        StoredEvidence? uploaded = null; await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = time.GetUtcNow(); if (removeImage && team.ActiveImageAssetId is { } old) { (await db.TeamImageAssets.SingleOrDefaultAsync(x => x.Id == old, ct))?.Replace(now); team.SetActiveImage(null); }
            if (image is { Length: > 0 }) { if (storage is null) throw new InvalidOperationException("Image storage is unavailable."); var assetId = Guid.NewGuid(); await using var content = image.OpenReadStream(); uploaded = await storage.StoreAsync(id, assetId, image.FileName, content, ct); if (team.ActiveImageAssetId is { } previous) (await db.TeamImageAssets.SingleOrDefaultAsync(x => x.Id == previous, ct))?.Replace(now); db.TeamImageAssets.Add(new TeamImageAsset(assetId, id, team.Id, uploaded.StorageKey, uploaded.OriginalFilename, uploaded.MediaType, uploaded.ByteSize, uploaded.Width, uploaded.Height, uploaded.Checksum, AdminId, now)); team.SetActiveImage(assetId); }
            team.Update(name.Trim(), team.Slug, Clean(affiliation), null);
            if (inclusionChanged) team.SetIncludedInDraft(requestedInclusion);
            team.AdvanceVersion();
            var after = TeamAuditState(team);
            var action = inclusionChanged ? "team.inclusion_changed" : "team.updated";
            var beforeAudit = inclusionChanged ? new { Team = before, AffectedParticipantIds = affectedParticipantIds } : before;
            var afterAudit = inclusionChanged ? new { Team = after, AffectedParticipantIds = affectedParticipantIds } : after;
            await AuditMutation(id, action, "team", team.Id, beforeAudit, afterAudit, ct); await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { await tx.RollbackAsync(ct); if (uploaded is not null && storage is not null) await storage.DeleteAsync(uploaded.StorageKey, ct); SetStatus(Localize("The team update could not be saved."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        SetStatus(inclusionChanged
            ? requestedInclusion ? Localize("{0} now participates in the website draft.", team.Name) : Localize("{0} is now a manual roster team.", team.Name)
            : Localize("{0} updated.", team.Name), UiMessageType.Success); return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostAddMemberAsync(Guid id, Guid teamId, Guid? participantId, string? reason, CancellationToken ct, bool confirmed = false, Guid? rosterTeamId = null, TeamMembershipRole role = TeamMembershipRole.Participant, Guid? accountId = null, long? expectedTeamVersion = null, Guid? playingCharacterId = null, decimal? playingEhb = null)
    {
        if (HasInvalidRoleBinding() || !IsRosterRole(role)) { SetStatus(Localize("Choose Participant, Captain, or Co-captain."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (role != TeamMembershipRole.Participant && captainAuthority is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var finalizedEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        var finalizedDraft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (finalizedEvent?.CanCorrectFinalizedRoster(finalizedDraft?.State, time.GetUtcNow()) == true)
        {
            if (!confirmed)
            {
                SetStatus(Localize("Confirm this published roster addition before continuing."), UiMessageType.Error);
                return RedirectToPage(new { id, rosterTeamId });
            }
            var result = await signupService.AddFinalizedRosterParticipantAsync(new FinalizedRosterAddRequest(
                id, teamId, AdminId, User.Identity?.Name ?? "Admin", accountId, participantId, role, expectedTeamVersion, playingCharacterId, playingEhb), ct);
            SetStatus(FinalizedRosterMutationMessage(result, "added"), result.Succeeded
                ? FinalizedRosterWomMessageType(result.WomSyncStatus)
                : UiMessageType.Error);
            return RedirectToPage(new { id, rosterTeamId });
        }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var team = await db.Teams.SingleOrDefaultAsync(value => value.Id == teamId && value.EventId == id, ct); var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); var draft = await db.DraftSessions.SingleOrDefaultAsync(value => value.EventId == id, ct);
        if (team is null || ev is null) return NotFound();
        if (draft?.State == DraftState.Paused) { SetStatus(Localize("This historical paused draft is read-only; no roster changes are available."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var draftedSetupAssignment = team.IncludedInDraft && CanDirectDraftedSetupAssignment(ev, draft);
        if (!CanDirectPreEventRosterMutation(ev) || (!draftedSetupAssignment && team.IncludedInDraft)) { SetStatus(Localize("Direct roster additions are available only before event start, or for an included team before the first pick."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (draft?.State == DraftState.Finalized && !team.IncludedInDraft && !confirmed) { SetStatus(Localize("Confirm this published roster correction."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (participantId is null) { SetStatus(Localize("Choose a participant."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (team.IncludedInDraft && !await db.EventParticipants.AnyAsync(participant => participant.Id == participantId.Value && participant.EventId == id && participant.SignupStatus == SignupStatus.Confirmed, ct)) { SetStatus(Localize("Only confirmed participants can be preassigned to an included team."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var participantName = await db.PrimaryCharacters().Where(x => x.ParticipantId == participantId.Value && x.EventId == id).Select(x => x.Name).SingleOrDefaultAsync(ct); if (participantName is null) return NotFound();
        if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId.Value && x.LeftAt == null, ct)) { SetStatus(Localize("Participant is already assigned to a team."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var assignmentReason = reason?.Trim() ?? "Manual roster assignment";
        var membership = new TeamMembership(Guid.NewGuid(), teamId, participantId.Value, TeamMembershipRole.Participant, time.GetUtcNow(), null, assignmentReason); membership.SetSource(TeamMembershipSource.RetainedConversion); db.TeamMemberships.Add(membership);
        await db.SaveChangesAsync(ct);
        if (role != TeamMembershipRole.Participant)
        {
            var roleResult = await ApplySelectedRoleAsync(id, membership, role, ct);
            if (!roleResult.Succeeded)
            {
                await tx.RollbackAsync(ct);
                SetStatus(roleResult.Error ?? Localize("The role could not be assigned."), UiMessageType.Error);
                return RedirectToPage(new { id, rosterTeamId });
            }
        }
        if (draft?.State == DraftState.Finalized) { await db.SaveChangesAsync(ct); await RepublishPreformedCorrectionAsync(ev, draft, "team.member_added", membership.Id, ct); }
        else await audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", "team.member_added", "team", teamId.ToString(), $"{participantId}: {assignmentReason}", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); SetStatus(Localize("{0} added to {1}.", participantName, team.Name), UiMessageType.Success); return RedirectToPage(new { id, rosterTeamId });
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
    public async Task<IActionResult> OnPostRemoveMemberAsync(Guid id, Guid membershipId, string? reason, CancellationToken ct, bool confirmed = false, Guid? rosterTeamId = null, long? expectedMembershipVersion = null)
    {
        var finalizedEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        var finalizedDraft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        var finalizedParticipantId = await db.TeamMemberships.AsNoTracking()
            .Where(x => x.Id == membershipId && x.LeftAt == null)
            .Select(x => (Guid?)x.EventParticipantId).SingleOrDefaultAsync(ct);
        if (finalizedEvent?.CanCorrectFinalizedRoster(finalizedDraft?.State, time.GetUtcNow()) == true)
        {
            var result = finalizedParticipantId is null
                ? new FinalizedRosterMutationResult(false, "The current roster membership could not be found.")
                : await signupService.RemoveFinalizedRosterParticipantAsync(new FinalizedRosterRemoveRequest(
                    id, finalizedParticipantId.Value, AdminId, User.Identity?.Name ?? "Admin", confirmed, expectedMembershipVersion), ct);
            SetStatus(FinalizedRosterMutationMessage(result, "removed"), result.Succeeded
                ? FinalizedRosterWomMessageType(result.WomSyncStatus)
                : UiMessageType.Error);
            return RedirectToPage(new { id, rosterTeamId });
        }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        var membership = await db.TeamMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.LeftAt == null, ct);
        if (ev is null || membership is null) return NotFound();
        var team = await db.Teams.SingleAsync(x => x.Id == membership.TeamId, ct);
        if (team.EventId != id || !CanDirectPreEventRosterMutation(ev)) { SetStatus(Localize("Direct roster removal is available only before the configured event start."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft?.State == DraftState.Paused) { SetStatus(Localize("This historical paused draft is read-only; no roster changes are available."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if ((team.IncludedInDraft && ev.DraftLocked) || (!team.IncludedInDraft && draft?.State == DraftState.Finalized && !confirmed)) { SetStatus(Localize("Included rosters lock when the draft starts; published roster corrections require confirmation."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var correctionReason = reason?.Trim() ?? "Roster removal";
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == membership.EventParticipantId, ct); var participantName = await PrimaryName(participant.Id, ct); var now = time.GetUtcNow(); var previous = membership.Role;
        membership.Leave(now, correctionReason); if (previous is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain) db.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(Guid.NewGuid(), membership.Id, previous, TeamMembershipRole.Participant, AdminId, now));
        if (draft?.State == DraftState.Finalized) { await db.SaveChangesAsync(ct); await RepublishPreformedCorrectionAsync(ev, draft, "team.member_removed", membership.Id, ct); }
        else await audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", "team.member_removed", "membership", membership.Id.ToString(), correctionReason, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); SetStatus(Localize("{0} removed from {1}.", participantName, team.Name), UiMessageType.Success); return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostChangeRoleAsync(Guid id, Guid membershipId, TeamMembershipRole role, CancellationToken ct, long? membershipVersion = null, Guid? rosterTeamId = null)
    {
        if (captainAuthority is null) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (membershipVersion is null)
        {
            SetStatus(Localize("This membership changed or the role form is stale. Reload before changing its role."), UiMessageType.Error);
            return RedirectToPage(new { id, rosterTeamId });
        }
        TeamCaptainRoleChangeResult result;
        var observedDraft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        var observedEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (observedDraft?.State == DraftState.Paused) { SetStatus(Localize("This historical paused draft is read-only; no roster changes are available."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        if (observedDraft?.State == DraftState.Finalized && observedEvent?.State == EventState.SignupClosed)
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct);
                if (item is not null) await db.Entry(item).ReloadAsync(ct);
                var currentDraft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
                if (currentDraft is not null) await db.Entry(currentDraft).ReloadAsync(ct);
                var now = time.GetUtcNow();
                if (item is null || !item.CanCorrectFinalizedRoster(currentDraft?.State, now) ||
                    !await db.DraftPublicationCycles.AnyAsync(x => x.DraftSessionId == currentDraft!.Id && x.SupersededAt == null, ct))
                    throw new InvalidOperationException("Roster corrections require a finalized draft before the event starts and a future event end.");
                if (!await db.Accounts.AnyAsync(x => x.Id == AdminId && x.Active && x.AccountType == Bingo.Domain.Access.AccountType.WebsiteAccount &&
                    (x.GlobalRole == Bingo.Domain.Access.GlobalRole.Admin || x.GlobalRole == Bingo.Domain.Access.GlobalRole.SuperAdmin), ct)) return Forbid();
                result = await captainAuthority.ChangeRoleAsync(new(id, membershipId, role, AdminId, User.Identity?.Name ?? "Admin", membershipVersion), ct);
                if (result.Succeeded)
                {
                    await RepublishPreformedCorrectionAsync(item, currentDraft!, "team.role_roster_published", membershipId, ct, "Pre-Live Captain role correction", item.Id);
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                else await tx.RollbackAsync(ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                result = new(false, Localize("The role correction could not be saved. No changes were applied. Reload and try again."));
            }
        }
        else result = await captainAuthority.ChangeRoleAsync(new(id, membershipId, role, AdminId, User.Identity?.Name ?? "Admin", membershipVersion), ct);
        SetStatus(result.Succeeded ? Localize("{0} is now {1}.", result.ParticipantName ?? string.Empty, RoleLabel(role)) : result.Error ?? Localize("The role could not be changed."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostMoveMemberAsync(Guid id, Guid membershipId, Guid targetTeamId, CancellationToken ct, bool confirmed = false, Guid? rosterTeamId = null)
    {
        var observedEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        var observedDraft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (observedEvent?.CanCorrectFinalizedRoster(observedDraft?.State, time.GetUtcNow()) == true)
        {
            SetStatus(Localize("Finalized roster movement is retired. Remove the participant, then add them to the other team."), UiMessageType.Error);
            return RedirectToPage(new { id, rosterTeamId });
        }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); var membership = await db.TeamMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.LeftAt == null, ct);
        if (ev is null || membership is null) return NotFound();
        var source = await db.Teams.SingleOrDefaultAsync(x => x.Id == membership.TeamId && x.EventId == id && x.Active, ct);
        var target = await db.Teams.SingleOrDefaultAsync(x => x.Id == targetTeamId && x.EventId == id && x.Active, ct);
        if (source is null || target is null || !await db.EventParticipants.AnyAsync(x => x.Id == membership.EventParticipantId && x.EventId == id, ct)) return NotFound();
        if (!CanDirectPreEventRosterMutation(ev) || source.IncludedInDraft || target.IncludedInDraft) { SetStatus(Localize("Direct roster movement is available only between manually assembled teams before the configured event start."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft?.State == DraftState.Finalized)
        {
            SetStatus(Localize("Finalized roster movement is retired. Remove the participant, then add them to the other team."), UiMessageType.Error);
            return RedirectToPage(new { id, rosterTeamId });
        }
        if (draft?.State == DraftState.Paused) { SetStatus(Localize("This historical paused draft is read-only; no roster changes are available."), UiMessageType.Error); return RedirectToPage(new { id, rosterTeamId }); }
        var participantName = await PrimaryName(membership.EventParticipantId, ct); var now = time.GetUtcNow(); var previous = membership.Role; const string correctionReason = "Roster correction"; membership.Leave(now, correctionReason);
        if (previous is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain) db.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(Guid.NewGuid(), membership.Id, previous, TeamMembershipRole.Participant, AdminId, now));
        var replacement = new TeamMembership(Guid.NewGuid(), target.Id, membership.EventParticipantId, previous, now, null, correctionReason); replacement.SetSource(TeamMembershipSource.Replacement, membership.Id); db.TeamMemberships.Add(replacement);
        await audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", "team.member_moved", "membership", membership.Id.ToString(), $"{source.Name} → {target.Name}: {correctionReason}", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); SetStatus(Localize("{0} moved from {1} to {2}.", participantName, source.Name, target.Name), UiMessageType.Success); return RedirectToPage(new { id, rosterTeamId });
    }
    public async Task<IActionResult> OnPostScrambleAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct);
        if (draft.State != DraftState.Running) { SetStatus(Localize("The draft is not ready to scramble."), UiMessageType.Error); return RedirectToPage(new { id }); }
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        if (draft.FirstPickRecordedAt is not null)
        {
            SetStatus(Localize("The team order cannot be changed after the first pick."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var teams = await db.Teams.Where(x => x.EventId == id && x.Active && x.IncludedInDraft).ToListAsync(ct);
        if (teams.Count < 2) { SetStatus(Localize("Add at least two drafted teams before scrambling the order."), UiMessageType.Error); return RedirectToPage(new { id }); }
        var before = new { draft = DraftAuditState(draft), teams = TeamOrderAuditState(teams) };
        for (var i = teams.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (teams[i], teams[j]) = (teams[j], teams[i]);
        }
        for (var i = 0; i < teams.Count; i++) teams[i].SetDraftPosition(i + 1);
        draft.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration);
        await AuditMutation(id, "draft.order_scrambled", "draft", draft.Id, before, new { draft = DraftAuditState(draft), teams = TeamOrderAuditState(teams) }, ct);
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
        var captainTeamIds = await (from membership in db.TeamMemberships.AsNoTracking()
                                    join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                    join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
                                    where membership.LeftAt == null && membership.Role == TeamMembershipRole.Captain &&
                                          teams.Select(team => team.Id).Contains(membership.TeamId) && participant.EventId == id &&
                                          participant.SignupStatus == SignupStatus.Confirmed &&
                                          account.Active && account.AccountType == AccountType.WebsiteAccount
                                    select membership.TeamId).Distinct().ToListAsync(ct);
        var missingCaptains = teams.Where(team => !captainTeamIds.Contains(team.Id)).Select(team => team.Name).ToList();
        if (missingCaptains.Count > 0) { SetStatus(Localize("Assign a current Captain to every drafted team before starting: {0}.", string.Join(", ", missingCaptains)), UiMessageType.Error); return RedirectToPage(new { id }); }
        var derived = await DeriveDraftState(id, teams, null, ct);
        if (derived.Blockers.Count != 0) { SetStatus(string.Join(" ", derived.Blockers), UiMessageType.Error); return RedirectToPage(new { id }); }
        try
        {
            var before = new { draft = DraftAuditState(draft), bingoEvent.DraftLocked, teams = TeamOrderAuditState(teams) };
            if (draft.FirstPickRecordedAt is null)
                foreach (var team in teams) team.SetDraftPosition(null);
            if (draft.HasActiveController(now)) draft.RenewControl(AdminId, now, DraftControlLease.Duration);
            else draft.AcquireControl(AdminId, now, DraftControlLease.Duration);
            draft.Start(now); bingoEvent.SetDraftLocked(true, now);
            await AuditMutation(id, "draft.started", "draft", draft.Id, before, new { draft = DraftAuditState(draft), bingoEvent.DraftLocked, teams = TeamOrderAuditState(teams), derived.Distribution.IncludedParticipants }, ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception exception) when (IsDraftConflict(exception)) { return DraftConflict(id, exception); }
        SetStatus(Localize("Draft started. Scramble the teams to draw the order."), UiMessageType.Success); await NotifyDraft(id, ct); return RedirectToPage(new { id });
    }
    public Task<IActionResult> OnPostConfigureAsync(Guid id, int teamCount, int targetSize, CancellationToken ct) => Task.FromResult<IActionResult>(BadRequest());
    public async Task<IActionResult> OnPostPickAsync(Guid id, Guid participantId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) return NotFound();
        if (draft.State != DraftState.Running) return BadRequest();
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null &&
            db.Teams.Any(team => team.Id == x.TeamId && team.EventId == id && team.Active), ct))
        {
            SetStatus(Localize("That participant is already assigned to a team."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.Id == participantId && x.EventId == id && x.SignupStatus == SignupStatus.Confirmed, ct);
        if (participant is null)
        {
            SetStatus(Localize("That participant is not available for this pick."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var participantName = await db.PrimaryCharacters().Where(x => x.ParticipantId == participantId && x.EventId == id).Select(x => x.Name).SingleAsync(ct);
        var teams = await OrderedDraftTeams(id, ct);
        if (teams.Count < 2 || teams.Any(x => x.DraftPosition is null))
        {
            SetStatus(Localize("Scramble the teams before making the first pick."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var activePickTeams = await db.DraftPicks
            .Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null)
            .OrderBy(x => x.PickNumber)
            .Select(x => x.TeamId)
            .ToListAsync(ct);
        var derived = await DeriveDraftState(id, teams, activePickTeams, ct);
        if (derived.Blockers.Count != 0)
        {
            SetStatus(string.Join(" ", derived.Blockers), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var teamIds = teams.Select(x => x.Id).ToList();
        var turn = SnakeDraftOrder.GetNextEligibleTurn(activePickTeams, teamIds, derived.RosterSizes, derived.Distribution);
        if (turn is null)
        {
            SetStatus(Localize("Every derived drafted-team place is filled."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var now = time.GetUtcNow();
        var before = new { draft = DraftAuditState(draft), pick = (object?)null, membership = (object?)null };
        var pick = new DraftPick(Guid.NewGuid(), draft.Id, turn.TeamId, participantId, turn.PickNumber, turn.RoundNumber, now);
        draft.RecordFirstPick(now);
        db.DraftPicks.Add(pick);
        var membership = new TeamMembership(Guid.NewGuid(), turn.TeamId, participantId, TeamMembershipRole.Participant, now, pick.Id, "Snake draft pick");
        membership.SetSource(TeamMembershipSource.DraftPick);
        db.TeamMemberships.Add(membership);
        try
        {
            draft.RenewControl(AdminId, now, DraftControlLease.Duration);
            await AuditMutation(id, "draft.pick_recorded", "pick", pick.Id, before, PickAuditState(draft, pick, membership), ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }

        SetStatus(Localize("{0} picked for {1}.", participantName, teams.Single(x => x.Id == turn.TeamId).Name), UiMessageType.Success);
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostUndoAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) return NotFound();
        if (draft.State != DraftState.Running) return BadRequest();
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });

        var pick = await db.DraftPicks
            .Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null)
            .OrderByDescending(x => x.PickNumber)
            .ThenByDescending(x => x.PickedAt)
            .FirstOrDefaultAsync(ct);
        if (pick is null)
        {
            SetStatus(Localize("There is no active pick to undo."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var membership = await db.TeamMemberships.SingleOrDefaultAsync(x => x.AssignedByDraftPickId == pick.Id && x.LeftAt == null, ct);
        if (membership is null)
        {
            SetStatus(Localize("The latest pick has no active membership to undo. Reload and inspect the draft history."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }

        var now = time.GetUtcNow();
        var before = PickAuditState(draft, pick, membership);
        pick.Undo(now);
        membership.Leave(now, "Draft pick undone");
        try
        {
            draft.RenewControl(AdminId, now, DraftControlLease.Duration);
            await AuditMutation(id, "draft.pick_undone", "pick", pick.Id, before, PickAuditState(draft, pick, membership), ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }

        SetStatus(Localize("Pick #{0} undone.", pick.PickNumber), UiMessageType.Success);
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (draft is null || bingoEvent is null) return NotFound();
        if (draft.State != DraftState.Running || bingoEvent.TeamRostersPublished || bingoEvent.DraftResultsPublished) return BadRequest();
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        try
        {
            var now = time.GetUtcNow();
            var activePickCount = await db.DraftPicks.CountAsync(x => x.DraftSessionId == draft.Id && x.UndoneAt == null, ct);
            if (activePickCount != 0)
            {
                SetStatus(Localize("Undo the latest active pick before cancelling the draft. {0} active pick(s) remain; no membership or pick history was changed.", activePickCount), UiMessageType.Error);
                return RedirectToPage(new { id });
            }

            var draftPickIds = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id).Select(x => x.Id).ToListAsync(ct);
            if (await db.TeamMemberships.AnyAsync(x => x.AssignedByDraftPickId != null && draftPickIds.Contains(x.AssignedByDraftPickId.Value) && x.LeftAt == null, ct))
            {
                SetStatus(Localize("The draft has an active pick membership even though its pick is not active. No changes were applied; inspect the draft history."), UiMessageType.Error);
                return RedirectToPage(new { id });
            }

            var hadFirstPick = draft.FirstPickRecordedAt is not null;
            draft.ReturnToSetup();
            // A cancelled draft may be started again, but once a pick has existed
            // the historical team order remains authoritative. Only an untouched
            // setup draft can have its order cleared.
            if (!hadFirstPick)
                foreach (var team in await db.Teams.Where(x => x.EventId == id && x.Active && x.IncludedInDraft).ToListAsync(ct)) team.SetDraftPosition(null);
            bingoEvent.SetDraftLocked(false);
            await audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", "draft.cancelled", "draft", draft.Id.ToString(), "Returned to setup with zero active picks; no picks or memberships were undone.", ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            SetStatus(Localize("Draft returned to setup. No picks were undone."), UiMessageType.Success);
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
            var draftedTeams = await OrderedDraftTeams(id, ct);

            // A zero/one-team event is a manually assembled roster. It is
            // finalized directly from setup and never receives a synthetic
            // running state, pick, turn, or team-balance calculation.
            if (draftedTeams.Count <= 1)
            {
                if (draft.State != DraftState.Setup)
                    throw new InvalidOperationException("A manually assembled roster can only be finalized from setup.");
                RequireOrAcquireControl(draft, now);
                var directMembers = await ActiveEventMembersAsync(id, ct);
                var directBlockers = await ValidateFinalRosterAsync(id, directMembers, ct);
                if (directBlockers.Count > 0) throw new InvalidOperationException(string.Join(" ", directBlockers));

                var nextCycle = (await db.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).Select(x => (int?)x.CycleNumber).MaxAsync(ct) ?? 0) + 1;
                var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, nextCycle, now, AdminId, DraftPublicationMethod.DirectRoster);
                db.DraftPublicationCycles.Add(cycle);
                var publicNames = await FrozenPublicNamesAsync(id, directMembers.Select(x => x.EventParticipantId), now, ct);
                foreach (var member in directMembers)
                    db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, member.TeamId, member.EventParticipantId, member.Role, null, publicNames[member.EventParticipantId]));
                foreach (var team in await db.Teams.Where(x => x.EventId == id && x.Active).ToListAsync(ct)) team.Finalize(now);
                draft.FinalizeDirect(now);
                bingoEvent.SetDraftLocked(true, now);
                bingoEvent.SetDraftRosterPublication(true);
                await audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", "draft.finalized", "draft", draft.Id.ToString(), $"DirectRoster publication cycle {nextCycle}; {directMembers.Count} frozen roster entries; no draft picks.", ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                published = true;
                offerBoardPublication = await db.Boards.AsNoTracking().AnyAsync(x => x.EventId == id && x.State == BoardState.Validated && x.ActiveApprovalSnapshotId != null, ct);
                SetStatus(offerBoardPublication
                    ? Localize("Manual roster finalized and published. The board is approved and ready to publish separately.")
                    : Localize("Manual roster finalized and published."), UiMessageType.Success);
            }
            else
            {
                if (draft.State != DraftState.Running)
                    throw new InvalidOperationException("A website draft must be running before it can be finalized.");
                draft.RequireControl(AdminId, now);

                var captainTeamIds = await (from membership in db.TeamMemberships.AsNoTracking()
                                            join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                            join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
                                            where membership.LeftAt == null && membership.Role == TeamMembershipRole.Captain &&
                                                  draftedTeams.Select(team => team.Id).Contains(membership.TeamId) && participant.EventId == id &&
                                                  participant.SignupStatus == SignupStatus.Confirmed &&
                                                  account.Active && account.AccountType == AccountType.WebsiteAccount
                                            select membership.TeamId).Distinct().ToListAsync(ct);
                var missingCaptains = draftedTeams.Where(team => !captainTeamIds.Contains(team.Id)).ToList();
                if (missingCaptains.Count > 0)
                {
                    SetStatus(Localize("Assign a current Captain to every drafted team before finalizing: {0}.", string.Join(", ", missingCaptains.Select(team => team.Name))), UiMessageType.Error);
                    return RedirectToPage(new { id, rosterTeamId = missingCaptains[0].Id });
                }
                if (draftedTeams.Any(x => x.DraftPosition is null))
                    throw new InvalidOperationException("Scramble the drafted teams before finalizing.");

                var activePickTeams = await db.DraftPicks
                    .Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null)
                    .OrderBy(x => x.PickNumber)
                    .Select(x => x.TeamId)
                    .ToListAsync(ct);
                var derived = await DeriveDraftState(id, draftedTeams, activePickTeams, ct);
                if (derived.Blockers.Count != 0) throw new InvalidOperationException(string.Join(" ", derived.Blockers));
                var includedParticipantIds = derived.IncludedParticipantIds;
                var draftedMembershipIds = await db.TeamMemberships
                    .Where(x => x.LeftAt == null && includedParticipantIds.Contains(x.EventParticipantId) && draftedTeams.Select(t => t.Id).Contains(x.TeamId))
                    .Select(x => x.EventParticipantId)
                    .ToListAsync(ct);
                if (draftedMembershipIds.Count != includedParticipantIds.Count || draftedMembershipIds.Distinct().Count() != includedParticipantIds.Count)
                    throw new InvalidOperationException("Every included participant must have exactly one active drafted-team membership before finalization.");

                var activeMembers = await ActiveEventMembersAsync(id, ct);
                var blockers = await ValidateFinalRosterAsync(id, activeMembers, ct);
                if (blockers.Count > 0) throw new InvalidOperationException(string.Join(" ", blockers));
                var nextCycle = (await db.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).Select(x => (int?)x.CycleNumber).MaxAsync(ct) ?? 0) + 1;
                var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, nextCycle, now, AdminId, DraftPublicationMethod.WebsiteDraft);
                db.DraftPublicationCycles.Add(cycle);
                var pickNumbers = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).ToDictionaryAsync(x => x.Id, x => x.PickNumber, ct);
                var publicNames = await FrozenPublicNamesAsync(id, activeMembers.Select(x => x.EventParticipantId), now, ct);
                foreach (var member in activeMembers)
                    db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, member.TeamId, member.EventParticipantId, member.Role,
                        member.AssignedByDraftPickId is { } pickId && pickNumbers.TryGetValue(pickId, out var pickNumber) ? pickNumber : null,
                        publicNames[member.EventParticipantId]));
                foreach (var team in await db.Teams.Where(x => x.EventId == id && x.Active).ToListAsync(ct)) team.Finalize(now);
                draft.FinalizeWebsiteDraft(now);
                bingoEvent.SetDraftRosterPublication(true);
                await audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", "draft.finalized", "draft", draft.Id.ToString(), $"WebsiteDraft publication cycle {nextCycle}; {activeMembers.Count} frozen roster entries.", ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                published = true;
                offerBoardPublication = await db.Boards.AsNoTracking().AnyAsync(x => x.EventId == id && x.State == BoardState.Validated && x.ActiveApprovalSnapshotId != null, ct);
                SetStatus(offerBoardPublication
                    ? Localize("Draft finalized and team rosters published. The board is approved and ready to publish separately.")
                    : Localize("Draft finalized and team rosters published."), UiMessageType.Success);
            }
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

    public async Task<IActionResult> OnPostAcquireControlAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) return NotFound();
        if (draft.State == DraftState.Paused) return NotFound();
        try
        {
            var before = DraftAuditState(draft);
            draft.AcquireControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration);
            await AuditMutation(id, "draft.control_acquired", "draft", draft.Id, before, DraftAuditState(draft), ct);
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
        if (draft.State == DraftState.Paused) return NotFound();
        try
        {
            var before = DraftAuditState(draft);
            draft.AcquireControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration, force: true);
            await AuditMutation(id, "draft.control_taken_over", "draft", draft.Id, before, DraftAuditState(draft), ct);
            await tx.CommitAsync(ct);
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
        if (draft.State == DraftState.Paused) return NotFound();
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        try { var before = DraftAuditState(draft); draft.ReleaseControl(AdminId, time.GetUtcNow()); await AuditMutation(id, "draft.control_released", "draft", draft.Id, before, DraftAuditState(draft), ct); }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
        SetStatus(Localize("Draft control released."), UiMessageType.Success);
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }

    private async Task<string> AddMembership(Guid eventId, Guid teamId, Guid participantId, TeamMembershipRole role, string reason, Guid? pickId, CancellationToken ct) { var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == eventId, ct); if (team is null) throw new InvalidOperationException("The selected team no longer exists."); if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct)) throw new InvalidOperationException("Participant is already assigned to a team."); var membership = new TeamMembership(Guid.NewGuid(), teamId, participantId, role, time.GetUtcNow(), pickId, reason); membership.SetSource(pickId is null ? TeamMembershipSource.RetainedConversion : TeamMembershipSource.DraftPick); db.TeamMemberships.Add(membership); await db.SaveChangesAsync(ct); await Audit("team.member_added", "team", teamId, $"{participantId}: {reason}", ct); return team.Name; }
    private void RequireOrAcquireControl(DraftSession draft, DateTimeOffset now)
    {
        if (draft.HasActiveController(now)) draft.RequireControl(AdminId, now);
        else draft.AcquireControl(AdminId, now, DraftControlLease.Duration);
    }
    private Task<List<TeamMembership>> ActiveEventMembersAsync(Guid eventId, CancellationToken ct) =>
        db.TeamMemberships
            .Where(membership => membership.LeftAt == null && db.Teams.Any(team => team.Id == membership.TeamId && team.EventId == eventId && team.Active))
            .ToListAsync(ct);
    private async Task<List<string>> ValidateFinalRosterAsync(Guid eventId, List<TeamMembership> activeMembers, CancellationToken ct)
    {
        var blockers = new List<string>();
        if (activeMembers.Count == 0) return ["A final roster is required before finalization."];
        var participantIds = activeMembers.Select(membership => membership.EventParticipantId).ToList();
        if (activeMembers.Any(membership => !IsRosterRole(membership.Role)))
            blockers.Add("Every active roster membership must use a supported team role.");
        if (activeMembers.GroupBy(membership => membership.EventParticipantId).Any(group => group.Count() != 1))
            blockers.Add("Every participant must have exactly one active team membership.");
        var activeTeamIds = await db.Teams.AsNoTracking()
            .Where(team => team.EventId == eventId && team.Active)
            .Select(team => team.Id)
            .ToListAsync(ct);
        var populatedTeamIds = activeMembers.Select(membership => membership.TeamId).ToHashSet();
        if (activeTeamIds.Any(teamId => !populatedTeamIds.Contains(teamId)))
            blockers.Add("Every active team must have at least one roster member before finalization.");

        var participants = await db.EventParticipants
            .Where(participant => participant.EventId == eventId && participantIds.Contains(participant.Id))
            .ToListAsync(ct);
        if (participants.Count != participantIds.Distinct().Count() || participants.Any(participant => participant.SignupStatus != SignupStatus.Confirmed))
            blockers.Add("Every published roster participant must be currently confirmed and eligible.");
        if (participants.Where(participant => participant.AccountId is not null).GroupBy(participant => participant.AccountId).Any(group => group.Count() > 1))
            blockers.Add("A website account cannot own more than one participant in the event roster.");

        var primary = await db.PrimaryCharacters().AsNoTracking()
            .Where(character => character.EventId == eventId && participantIds.Contains(character.ParticipantId))
            .Select(character => new { character.ParticipantId, character.OsrsCharacterId })
            .ToListAsync(ct);
        var primaryCounts = primary.GroupBy(character => character.ParticipantId).ToDictionary(group => group.Key, group => group.Count());
        if (participantIds.Distinct().Any(participantId => primaryCounts.GetValueOrDefault(participantId) != 1))
            blockers.Add("Every published roster participant must retain exactly one current Playing character.");
        if (primary.GroupBy(character => character.OsrsCharacterId).Any(group => group.Count() > 1))
            blockers.Add("A Playing character cannot be assigned to more than one roster participant in the event.");
        return blockers;
    }
    private bool CanDirectPreEventRosterMutation(Bingo.Domain.Events.BingoEvent bingoEvent) =>
        (bingoEvent.State is Bingo.Domain.Events.EventState.Draft or Bingo.Domain.Events.EventState.SignupOpen or Bingo.Domain.Events.EventState.SignupClosed)
        && bingoEvent.ActualStartedAt is null && bingoEvent.EventEndsAt is { } ends && time.GetUtcNow() < ends;

    private static bool CanCreateInitialPrivateTeam(Bingo.Domain.Events.BingoEvent bingoEvent, DraftSession draft) =>
        draft.State == DraftState.Setup
        && bingoEvent.State == Bingo.Domain.Events.EventState.Draft
        && bingoEvent.HiddenAt is null
        && bingoEvent.ActualStartedAt is null
        && bingoEvent.ActualSignupOpenedAt is null
        && bingoEvent.ActualSignupClosedAt is null
        && bingoEvent.FirstPublicAt is null
        && bingoEvent.EventStartsAt is null
        && bingoEvent.EventEndsAt is null;

    private string FinalizedRosterMutationMessage(FinalizedRosterMutationResult result, string action)
    {
        if (!result.Succeeded) return Localize(result.Error ?? "The finalized roster could not be changed.");
        var team = string.IsNullOrWhiteSpace(result.TeamName) ? "the selected team" : result.TeamName;
        var count = result.CurrentTeamMemberCount is { } memberCount ? $" ({memberCount} current member{(memberCount == 1 ? "" : "s")})" : string.Empty;
        var shortage = result.TeamIsShort && result.TargetTeamSize is { } target
            ? $" The team remains short ({result.CurrentTeamMemberCount}/{target})."
            : string.Empty;
        var provider = result.WomSyncStatus switch
        {
            "NotManaged" or "Unchanged" => " WOM does not require an update.",
            "Failed" or "Conflict" or "Unknown" => $" WOM synchronization failed ({result.WomSyncStatus}): {result.WomSyncError ?? "the provider is unavailable"}. Retry the synchronization after resolving the reported issue.",
            "Pending" or "Sending" or "Retry" => $" WOM synchronization is {result.WomSyncStatus.ToLowerInvariant()}; the local roster is saved and the worker will retry.{(string.IsNullOrWhiteSpace(result.WomSyncError) ? string.Empty : $" Reason: {result.WomSyncError}")}",
            null => string.Empty,
            "Queued" or "Succeeded" or "Success" => " WOM synchronization is queued.",
            _ => $" WOM synchronization is {result.WomSyncStatus.ToLowerInvariant()} and still needs attention.{(string.IsNullOrWhiteSpace(result.WomSyncError) ? string.Empty : $" Reason: {result.WomSyncError}")}"
        };
        return Localize($"Participant {action} locally in {team}{count}.{shortage}{provider}");
    }
    private static UiMessageType FinalizedRosterWomMessageType(string? status) => status is null or "NotManaged" or "Unchanged" or "Queued" or "Succeeded" or "Success"
        ? UiMessageType.Success
        : status is "Pending" or "Sending" or "Retry" ? UiMessageType.Warning : UiMessageType.Error;
    private bool CanDirectDraftedSetupAssignment(Bingo.Domain.Events.BingoEvent bingoEvent, DraftSession? draft) =>
        CanDirectPreEventRosterMutation(bingoEvent)
        && draft is { FirstPickRecordedAt: null, State: DraftState.Setup or DraftState.Running };
    private async Task<Dictionary<Guid, string>> FrozenPublicNamesAsync(Guid eventId, IEnumerable<Guid> participantIds, DateTimeOffset publishedAt, CancellationToken ct)
    {
        var ids = participantIds.Distinct().ToList();
        var names = await db.PrimaryCharacters().AsNoTracking()
            .Where(character => character.EventId == eventId && ids.Contains(character.ParticipantId))
            .ToDictionaryAsync(character => character.ParticipantId, character => character.Name, ct);
        var missing = ids.Where(id => !names.ContainsKey(id)).ToList();
        if (missing.Count > 0 || names.Values.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Every published roster entry requires exactly one retained playing-character identity.");
        return names;
    }
    private async Task RepublishPreformedCorrectionAsync(Bingo.Domain.Events.BingoEvent bingoEvent, DraftSession draft, string action, Guid targetId, CancellationToken ct, string correctionReason = "Pre-formed roster correction", Guid? publicationAuditEventId = null)
    {
        if (draft.State != DraftState.Finalized || !CanDirectPreEventRosterMutation(bingoEvent)) throw new InvalidOperationException("Published pre-formed corrections are unavailable after event start.");
        var now = time.GetUtcNow();
        var activeCycle = await db.DraftPublicationCycles.SingleOrDefaultAsync(x => x.DraftSessionId == draft.Id && x.SupersededAt == null, ct) ?? throw new InvalidOperationException("The active roster publication no longer exists.");
        var activeMembers = await db.TeamMemberships.Where(x => x.LeftAt == null && db.Teams.Any(t => t.Id == x.TeamId && t.EventId == bingoEvent.Id && t.Active)).ToListAsync(ct);
        var names = await FrozenPublicNamesAsync(bingoEvent.Id, activeMembers.Select(x => x.EventParticipantId), now, ct);
        var picks = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).ToDictionaryAsync(x => x.Id, x => x.PickNumber, ct);
        activeCycle.Supersede(now, AdminId, correctionReason);
        await db.SaveChangesAsync(ct);
        var nextCycle = (await db.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).Select(x => (int?)x.CycleNumber).MaxAsync(ct) ?? 0) + 1;
        var replacement = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, nextCycle, now, AdminId, activeCycle.PublicationMethod); db.DraftPublicationCycles.Add(replacement);
        foreach (var member in activeMembers) db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), replacement.Id, member.TeamId, member.EventParticipantId, member.Role, member.AssignedByDraftPickId is { } pick && picks.TryGetValue(pick, out var number) ? number : null, names[member.EventParticipantId]));
        bingoEvent.SetDraftRosterPublication(true);
        await audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", action, "draft_publication", targetId.ToString(), $"Superseded publication cycle {activeCycle.CycleNumber}: {correctionReason}.", publicationAuditEventId, ct);
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
        var now = time.GetUtcNow();
        CanChangeCaptainRoles = ev.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live
            || ev.State == EventState.AwaitingFinalReview && ev.AcceptsNewSubmissions(now);
        Sort = sort is "name" or "signup" or "status" ? sort : "ehb";
        var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        var persistedDraft = draft is not null;
        CanCorrectPreLiveRoster = ev.CanCorrectFinalizedRoster(draft?.State, now) &&
            await db.ActiveRosterPublications(id).AnyAsync(ct);
        if (CanCorrectPreLiveRoster)
        {
            var rosterAccounts = await db.Accounts.AsNoTracking()
                .Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount)
                .OrderBy(x => x.LoginName)
                .ToListAsync(ct);
            var rosterAccountIds = rosterAccounts.Select(account => account.Id).ToList();
            var rosterCharacters = rosterAccountIds.Count == 0
                ? []
                : await (from link in db.AccountOsrsCharacters.AsNoTracking()
                         join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
                         where rosterAccountIds.Contains(link.AccountId) && link.Active
                         orderby link.Position, link.Id
                         select new { link.AccountId, CharacterId = link.OsrsCharacterId, character.DisplayName, link.SavedEhb })
                    .ToListAsync(ct);
            RosterWebsiteAccounts = rosterAccounts
                .Select(account => new RosterAccountOption(
                    account.Id,
                    account.LoginName,
                    rosterCharacters
                        .Where(character => character.AccountId == account.Id)
                        .Select(character => new RosterCharacterOption(character.CharacterId, character.DisplayName, character.SavedEhb))
                        .ToList()))
                .ToList();
        }
        if (draft?.State == DraftState.Finalized && ev.State == EventState.SignupClosed) CanChangeCaptainRoles = CanCorrectPreLiveRoster;
        // Keep the normal setup workspace available before the first team is added.
        // This is a render-only draft; the first POST creates the persisted session.
        draft ??= new DraftSession(Guid.NewGuid(), id, 1);

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
        var frozenPublicationNames = draft.State == DraftState.Finalized
            ? await (from entry in db.DraftPublicationRosters.AsNoTracking()
                     join publication in db.DraftPublicationCycles.AsNoTracking() on entry.DraftPublicationCycleId equals publication.Id
                     where publication.DraftSessionId == draft.Id
                     orderby publication.CycleNumber descending
                     select new { entry.EventParticipantId, entry.PublicCharacterName })
                .ToListAsync(ct)
            : [];
        var frozenNameByParticipant = frozenPublicationNames
            .GroupBy(entry => entry.EventParticipantId)
            .ToDictionary(group => group.Key, group => group.First().PublicCharacterName);
        string DisplayName(Guid participantId) => frozenNameByParticipant.GetValueOrDefault(participantId) ?? DisplayAuthority(participantId).Name;
        var captainTeamIds = await db.TeamMemberships.AsNoTracking()
            .Where(membership => membership.LeftAt == null && membership.Role == TeamMembershipRole.Captain && teams.Select(team => team.Id).Contains(membership.TeamId))
            .Select(membership => membership.TeamId)
            .Distinct()
            .ToListAsync(ct);
        var usableCaptainTeamIds = await (from membership in db.TeamMemberships.AsNoTracking()
                                          join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                          join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
                                          where membership.LeftAt == null && membership.Role == TeamMembershipRole.Captain && teams.Select(team => team.Id).Contains(membership.TeamId) && account.Active && account.AccountType == Bingo.Domain.Access.AccountType.WebsiteAccount && participant.EventId == id
                                          select membership.TeamId).Distinct().ToListAsync(ct);
        var manuallyAssembledParticipantIds = memberships.Where(m => teams.Any(t => t.Id == m.TeamId && !t.IncludedInDraft)).Select(m => m.EventParticipantId).ToHashSet();
        var eligibleParticipants = participants.Where(x => !manuallyAssembledParticipantIds.Contains(x.Id) && x.SignupStatus == SignupStatus.Confirmed).ToList();
        var teamMap = teams.ToDictionary(x => x.Id);
        var membershipByParticipant = memberships.ToDictionary(x => x.EventParticipantId);
        var internalParticipants = participants.Where(x => !manuallyAssembledParticipantIds.Contains(x.Id));
        IEnumerable<EventParticipant> ordered = Sort switch
        {
            "name" => internalParticipants.OrderBy(x => DisplayName(x.Id)).ThenBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id),
            "signup" => internalParticipants.OrderBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id),
            "status" => internalParticipants.OrderBy(x => membershipByParticipant.ContainsKey(x.Id)).ThenByDescending(x => DisplayAuthority(x.Id).Ehb).ThenBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id),
            _ => internalParticipants.OrderByDescending(x => DisplayAuthority(x.Id).Ehb).ThenBy(x => DisplayName(x.Id)).ThenBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id)
        };
        Participants = ordered.Select(x =>
        {
            membershipByParticipant.TryGetValue(x.Id, out var membership);
            var authority = DisplayAuthority(x.Id);
            return new ParticipantView(x.Id, DisplayName(x.Id), authority.Ehb, x.SignedUpAt, x.CaptainVolunteer, membership?.TeamId, membership is null ? null : teamMap[membership.TeamId].Name, x.SignupStatus);
        }).ToList();

        var activePicks = await db.DraftPicks.AsNoTracking().Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).OrderBy(x => x.PickNumber).ToListAsync(ct);
        var pickNumberByParticipant = activePicks.ToDictionary(x => x.EventParticipantId, x => x.PickNumber);
        var teamPickNumberByParticipant = activePicks.GroupBy(x => x.TeamId).SelectMany(group => group.OrderBy(x => x.PickNumber).Select((pick, index) => new { pick.EventParticipantId, TeamPickNumber = index + 1 })).ToDictionary(x => x.EventParticipantId, x => x.TeamPickNumber);
        var draftedOrder = teams.Where(x => x.IncludedInDraft).ToList();
        CanDirectFinalize = persistedDraft && draft.State == DraftState.Setup && draftedOrder.Count <= 1;
        DraftOrderReady = draftedOrder.Count >= 2 && draftedOrder.All(x => x.DraftPosition is not null);
        var derived = await DeriveDraftState(id, draftedOrder, activePicks.Select(pick => pick.TeamId).ToList(), ct);
        if (draft.State == DraftState.Running && DraftOrderReady && derived.Blockers.Count == 0)
        {
            var orderedTeams = draftedOrder.OrderBy(x => x.DraftPosition).ToList();
            var turn = SnakeDraftOrder.GetNextEligibleTurn(activePicks.Select(x => x.TeamId).ToList(), orderedTeams.Select(x => x.Id).ToList(), derived.RosterSizes, derived.Distribution);
            if (turn is not null) CurrentTurn = new(turn.PickNumber, turn.RoundNumber, turn.TeamId, teamMap[turn.TeamId].Name);
        }
        var latest = activePicks.LastOrDefault();
        if (latest is not null)
        {
            var publishedName = await (from entry in db.DraftPublicationRosters.AsNoTracking()
                                       join publication in db.DraftPublicationCycles.AsNoTracking() on entry.DraftPublicationCycleId equals publication.Id
                                       where publication.DraftSessionId == draft.Id && publication.PublishedAt >= latest.PickedAt && entry.EffectivePickNumber == latest.PickNumber &&
                                           entry.TeamId == latest.TeamId && entry.EventParticipantId == latest.EventParticipantId
                                       orderby publication.CycleNumber
                                       select entry.PublicCharacterName).FirstOrDefaultAsync(ct);
            var wasPublished = publishedName is not null || await db.DraftPublicationCycles.AnyAsync(x => x.DraftSessionId == draft.Id, ct);
            LatestPick = new(latest.PickNumber, wasPublished ? publishedName ?? Localize("Historical player unavailable") : DisplayAuthority(latest.EventParticipantId).Name, teamMap[latest.TeamId].Name);
        }
        Teams = teams.Select(team => new TeamView(
            team.Id, team.Name, team.IncludedInDraft, team.AffiliationName, team.ActiveImageAssetId is null ? null : $"/Admin/Events/Draft/{id}?handler=TeamImage&teamId={team.Id}", team.DraftPosition, team.Version,
            CurrentTurn?.TeamId == team.Id, derived.ProjectedFinalSizes.GetValueOrDefault(team.Id, memberships.Count(membership => membership.TeamId == team.Id)),
            memberships.Where(m => m.TeamId == team.Id)
                .OrderBy(m => m.Role == TeamMembershipRole.Captain ? 0 : m.Role == TeamMembershipRole.CoCaptain ? 1 : 2)
                .ThenBy(m => pickNumberByParticipant.GetValueOrDefault(m.EventParticipantId, int.MaxValue))
                .Select(m =>
                {
                    var participant = participants.Single(p => p.Id == m.EventParticipantId);
                    var authority = DisplayAuthority(m.EventParticipantId);
                    teamPickNumberByParticipant.TryGetValue(m.EventParticipantId, out var pickNumber);
                    return new MemberView(m.Id, DisplayName(m.EventParticipantId), authority.Ehb, m.Role, m.Version, participant.AccountId is null, pickNumber == 0 ? null : pickNumber, m.EventParticipantId);
                }).ToList(), captainTeamIds.Contains(team.Id), usableCaptainTeamIds.Contains(team.Id),
            team.IncludedInDraft
                ? memberships.Where(m => m.TeamId == team.Id).Sum(m => DisplayAuthority(m.EventParticipantId).Ehb)
                : 0m)).ToList();
        ConfirmedCount = eligibleParticipants.Count;
        AssignedCount = eligibleParticipants.Count(x => membershipByParticipant.ContainsKey(x.Id));
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
        var includedByTeam = await db.Teams.AsNoTracking().Where(team => team.EventId == eventId && team.Active)
            .ToDictionaryAsync(team => team.Id, team => team.IncludedInDraft, ct);
        var includedParticipants = await db.EventParticipants.AsNoTracking()
            .Where(participant => participant.EventId == eventId && participant.SignupStatus == SignupStatus.Confirmed)
            .ToListAsync(ct);
        var included = includedParticipants.Where(participant => !allActiveMemberships.Any(membership =>
            membership.EventParticipantId == participant.Id &&
            !includedByTeam.GetValueOrDefault(membership.TeamId))).ToList();
        var rosterSizes = teamIds.ToDictionary(teamId => teamId, teamId => allActiveMemberships.Count(membership => membership.TeamId == teamId));
        var distribution = DraftRosterDistribution.Derive(included.Count, teamIds.Count);
        var blockers = distribution.ValidateCurrentRosters(rosterSizes).ToList();
        if (included.Where(participant => participant.AccountId is not null).GroupBy(participant => participant.AccountId).Any(group => group.Count() > 1))
            blockers.Add("A website account cannot own more than one included participant.");
        var primary = await db.PrimaryCharacters().AsNoTracking()
            .Where(character => included.Select(participant => participant.Id).Contains(character.ParticipantId))
            .Select(character => new { character.ParticipantId, character.OsrsCharacterId })
            .ToListAsync(ct);
        var primaryByParticipant = primary.GroupBy(character => character.ParticipantId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var includedParticipantIds = included.Select(participant => participant.Id).ToHashSet();
        if (includedParticipantIds.Any(participantId => !primaryByParticipant.TryGetValue(participantId, out var rows) || rows.Count != 1))
            blockers.Add("Every included confirmed participant must retain one valid primary-account reservation.");
        if (primary.GroupBy(character => character.OsrsCharacterId).Any(group => group.Count() > 1))
            blockers.Add("A Playing character cannot be assigned to more than one included participant.");
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
    private Task<TeamCaptainRoleChangeResult> ApplySelectedRoleAsync(Guid eventId, TeamMembership membership, TeamMembershipRole role, CancellationToken ct) =>
        captainAuthority!.ChangeRoleAsync(new(eventId, membership.Id, role, AdminId, User.Identity?.Name ?? "Admin", membership.Version), ct);
    private bool HasInvalidRoleBinding() => ModelState.TryGetValue("role", out var entry) && entry.Errors.Count > 0;
    private static bool IsRosterRole(TeamMembershipRole role) => role is TeamMembershipRole.Participant or TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain;
    private Task NotifyDraft(Guid eventId, CancellationToken ct) => collaboration.NotifyDraftChangedAsync(eventId, ct);
    private Task AuditMutation(Guid eventId, string action, string target, Guid targetId, object? before, object? after, CancellationToken ct) =>
        audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", action, target, targetId.ToString(), JsonSerializer.Serialize(new { before, after }), eventId, ct);

    private static object TeamAuditState(Team team) => new { team.Id, Name = AuditText(team.Name), AffiliationName = team.AffiliationName is null ? (JsonElement?)null : AuditText(team.AffiliationName), team.IncludedInDraft, team.ActiveImageAssetId, team.Active, team.DraftPosition };
    private static JsonElement TeamOrderAuditState(IEnumerable<Team> teams)
    {
        var positions = teams.OrderBy(team => team.Id).Select(team => new { team.Id, team.DraftPosition }).ToArray();
        var json = JsonSerializer.Serialize(positions);
        return json.Length <= 900 ? JsonSerializer.SerializeToElement(positions) : JsonSerializer.SerializeToElement(new { Count = positions.Length, ValuesOmitted = true, Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))) });
    }
    private static JsonElement AuditText(string value) => JsonSerializer.Serialize(value).Length <= 200 ? JsonSerializer.SerializeToElement(value)
        : JsonSerializer.SerializeToElement(new { Preview = value[..Math.Min(value.Length, 40)], value.Length, Truncated = true, Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))) });
    private static object DraftAuditState(DraftSession draft) => new { State = draft.State.ToString(), draft.LockedAt, draft.FirstPickRecordedAt, draft.ControllerAccountId, draft.ControllerLeaseExpiresAt, draft.ControlVersion };
    private static object PickAuditState(DraftSession draft, DraftPick pick, TeamMembership membership) => new
    {
        draft = DraftAuditState(draft),
        pick = new { pick.Id, pick.TeamId, pick.EventParticipantId, pick.PickNumber, pick.RoundNumber, pick.PickedAt, pick.UndoneAt },
        membership = new { membership.Id, membership.TeamId, membership.EventParticipantId, membership.AssignedByDraftPickId, membership.JoinedAt, membership.LeftAt }
    };

    private Task Audit(string action, string target, Guid targetId, string details, CancellationToken ct) => audit.WriteAndSaveAsync(User.GetAccountId(), User.Identity!.Name!, action, target, targetId.ToString(), details, ct); private static string RoleLabel(TeamMembershipRole role) => role == TeamMembershipRole.CoCaptain ? "co-captain" : role.ToString().ToLowerInvariant(); private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    private void StoreCredentials(IReadOnlyList<GeneratedCaptainCredential> credentials) { if (credentials.Count > 0) TempData["GeneratedCaptainCredentials"] = JsonSerializer.Serialize(credentials); }
    private sealed record DerivedDraftState(IReadOnlyList<Guid> IncludedParticipantIds, DraftRosterDistribution Distribution, IReadOnlyDictionary<Guid, int> RosterSizes, IReadOnlyDictionary<Guid, int> ProjectedFinalSizes, IReadOnlyList<string> Blockers);
    public sealed record DraftView(Guid Id, DraftState State, bool FirstPickRecorded); public sealed record ParticipantView(Guid Id, string Name, decimal Ehb, DateTimeOffset SignedUpAt, bool CaptainVolunteer, Guid? TeamId, string? TeamName, SignupStatus SignupStatus); public sealed record RosterAccountOption(Guid Id, string LoginName, IReadOnlyList<RosterCharacterOption> Characters); public sealed record RosterCharacterOption(Guid Id, string DisplayName, decimal? SavedEhb); public sealed record TeamView(Guid Id, string Name, bool IncludedInDraft, string? Affiliation, string? ImageUrl, int? DraftPosition, long Version, bool IsCurrent, int ProjectedFinalSize, IReadOnlyList<MemberView> Members, bool HasCurrentCaptain, bool HasUsableCaptain, decimal TotalEhb); public sealed record MemberView(Guid MembershipId, string Name, decimal Ehb, TeamMembershipRole Role, long Version, bool External, int? PickNumber, Guid ParticipantId = default); public sealed record TurnView(int PickNumber, int RoundNumber, Guid TeamId, string TeamName); public sealed record PickView(int PickNumber, string PlayerName, string TeamName);
}
