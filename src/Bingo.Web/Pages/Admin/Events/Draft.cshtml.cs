using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class DraftModel(ApplicationDbContext db, TimeProvider time, IAuditWriter audit, IAdminCollaborationNotifier collaboration, ISignupService signupService, EventParticipantCharacterService characterService) : PageModel
{
    public string EventName { get; private set; } = string.Empty; public string Sort { get; private set; } = "ehb"; public DraftView? Draft { get; private set; }
    public IReadOnlyList<TeamView> Teams { get; private set; } = []; public IReadOnlyList<ParticipantView> Participants { get; private set; } = [];
    public IReadOnlyDictionary<Guid, PaymentStatus> ParticipantPayments { get; private set; } = new Dictionary<Guid, PaymentStatus>();
    public TurnView? CurrentTurn { get; private set; }
    public PickView? LatestPick { get; private set; }
    public int ConfirmedCount { get; private set; }
    public int AvailableCount { get; private set; }
    public int AssignedCount { get; private set; }
    public int Remainder { get; private set; }
    public IReadOnlyList<GeneratedCaptainCredential> GeneratedCredentials { get; private set; } = [];
    public Guid CurrentAccountId { get; private set; }
    public bool CanControlDraft { get; private set; }
    public Guid? DraftControllerAccountId { get; private set; }
    public string? DraftControllerName { get; private set; }
    public DateTimeOffset? DraftControllerLeaseExpiresAt { get; private set; }
    public bool DraftOrderReady { get; private set; }
    [BindProperty] public SetupInput Setup { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid id, string? sort, CancellationToken ct)
    {
        if (TempData["GeneratedCaptainCredentials"] is string json) GeneratedCredentials = JsonSerializer.Deserialize<List<GeneratedCaptainCredential>>(json) ?? [];
        CurrentAccountId = AdminId;
        if (!await Load(id, sort, ct)) return NotFound();
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

    public async Task<IActionResult> OnPostSetupAsync(Guid id, CancellationToken ct)
    {
        if (!ModelState.IsValid) return RedirectToPage(new { id }); if (await db.DraftSessions.AnyAsync(x => x.EventId == id, ct)) return RedirectToPage(new { id });
        var draft = new DraftSession(Guid.NewGuid(), id, Setup.TargetSize); db.DraftSessions.Add(draft);
        for (var n = 1; n <= Setup.TeamCount; n++) db.Teams.Add(new Team(Guid.NewGuid(), id, $"Team {n}", await UniqueTeamSlug(id, $"Team {n}", ct), TeamFormationType.Drafted, null, true));
        await db.SaveChangesAsync(ct); await Audit("draft.setup_created", "draft", draft.Id, $"{Setup.TeamCount} teams; target {Setup.TargetSize}", ct); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostAddTeamAsync(Guid id, string name, string? affiliation, string? imageUrl, CancellationToken ct)
    { var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft?.State is DraftState.Running or DraftState.Paused) { SetStatus("External teams can be added before the draft starts or after it is finished.", UiMessageType.Error); return RedirectToPage(new { id }); } var team = new Team(Guid.NewGuid(), id, name.Trim(), await UniqueTeamSlug(id, name, ct), TeamFormationType.Preformed, Clean(affiliation), false); team.Update(name.Trim(), team.Slug, Clean(affiliation), Clean(imageUrl)); if (draft?.State == DraftState.Finalized) team.Finalize(time.GetUtcNow()); db.Teams.Add(team); await db.SaveChangesAsync(ct); await Audit("team.preformed_added", "team", team.Id, name, ct); SetStatus($"External team {team.Name} added.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostAddDraftTeamAsync(Guid id, CancellationToken ct)
    { var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft is null) return NotFound(); if (draft.State != DraftState.Setup) { TempData["StatusMessage"] = "Draft teams cannot be added after the draft starts."; return RedirectToPage(new { id }); } var teams = await db.Teams.Where(x => x.EventId == id && x.FormationType == TeamFormationType.Drafted).OrderBy(x => x.Name).ToListAsync(ct); var activeCount = teams.Count(x => x.Active); if (activeCount >= 20) { TempData["StatusMessage"] = "A draft can have at most 20 website-drafted teams."; return RedirectToPage(new { id }); } var team = teams.FirstOrDefault(x => !x.Active); if (team is not null) team.SetActive(true); else { var number = activeCount + 1; var name = $"Team {number}"; team = new Team(Guid.NewGuid(), id, name, await UniqueTeamSlug(id, name, ct), TeamFormationType.Drafted, null, true); db.Teams.Add(team); } await db.SaveChangesAsync(ct); await Audit("draft.team_added", "team", team.Id, $"{activeCount + 1} drafted teams", ct); SetStatus($"{team.Name} added to the website draft.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostRemoveDraftTeamAsync(Guid id, Guid teamId, CancellationToken ct)
    { var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft is null) return NotFound(); if (draft.State != DraftState.Setup) { TempData["StatusMessage"] = "Draft teams cannot be removed after the draft starts."; return RedirectToPage(new { id }); } var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id && x.FormationType == TeamFormationType.Drafted && x.Active, ct); if (team is null) return NotFound(); if (await db.Teams.CountAsync(x => x.EventId == id && x.FormationType == TeamFormationType.Drafted && x.Active, ct) <= 2) { TempData["StatusMessage"] = "The website draft needs at least two teams."; return RedirectToPage(new { id }); } if (await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.LeftAt == null, ct)) { TempData["StatusMessage"] = $"Remove the players from {team.Name} before removing the team."; return RedirectToPage(new { id }); } team.SetActive(false); team.SetDraftPosition(null); await db.SaveChangesAsync(ct); await Audit("draft.team_removed", "team", team.Id, "Removed from website draft setup", ct); SetStatus($"{team.Name} removed from the website draft.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostRemoveExternalTeamAsync(Guid id, Guid teamId, CancellationToken ct)
    { var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft?.State is DraftState.Running or DraftState.Paused) { SetStatus("External teams cannot be removed while the draft is in progress.", UiMessageType.Error); return RedirectToPage(new { id }); } var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id && x.FormationType == TeamFormationType.Preformed && x.Active, ct); if (team is null) return NotFound(); if (await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.LeftAt == null, ct)) { SetStatus($"Remove the players from {team.Name} before removing the external team.", UiMessageType.Error); return RedirectToPage(new { id }); } team.SetActive(false); team.SetDraftPosition(null); await db.SaveChangesAsync(ct); await Audit("external_team.removed", "team", team.Id, "External team removed", ct); SetStatus($"External team {team.Name} removed.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostRemoveParticipantAsync(Guid id, Guid participantId, CancellationToken ct)
    { var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct); if (draft?.State != DraftState.Setup) { TempData["StatusMessage"] = "Participants cannot be removed after the draft starts."; return RedirectToPage(new { id }); } var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.Id == participantId && x.EventId == id && x.Source != SignupSource.AdminCreated, ct); if (participant is null) return NotFound(); if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct)) { TempData["StatusMessage"] = "Remove this player from their roster before removing them from the event."; return RedirectToPage(new { id }); } var participantName = await PrimaryName(participantId, ct); var wasConfirmed = participant.SignupStatus == SignupStatus.Confirmed; participant.Remove(time.GetUtcNow(), "Removed from participant pool"); await characterService.ReleaseAllAsync(participantId, AdminId, ct); await db.SaveChangesAsync(ct); var promoted = wasConfirmed ? await signupService.PromoteAvailablePlacesAsync(id, ct) : 0; await Audit("participant.removed", "participant", participant.Id, $"Removed from draft participant pool; promoted {promoted}", ct); SetStatus($"{participantName} removed. {promoted} player(s) promoted from the waiting list.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostUpdateTeamAsync(Guid id, Guid teamId, string name, string? affiliation, string? imageUrl, CancellationToken ct)
    { var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id, ct); if (team is null) return NotFound(); team.Update(name.Trim(), team.Slug, Clean(affiliation), Clean(imageUrl)); await db.SaveChangesAsync(ct); await Audit("team.updated", "team", team.Id, name, ct); SetStatus($"{team.Name} updated.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostAddMemberAsync(Guid id, Guid teamId, Guid? participantId, TeamMembershipRole role, string reason, CancellationToken ct)
    { if (await db.Events.AnyAsync(x => x.Id == id && x.DraftLocked, ct)) { TempData["StatusMessage"] = "Website signup assignments are locked because the draft has started."; return RedirectToPage(new { id }); } if (participantId is null) { TempData["StatusMessage"] = "Choose a participant."; return RedirectToPage(new { id }); } var participantName = await db.PrimaryCharacters().Where(x => x.ParticipantId == participantId.Value && x.EventId == id).Select(x => x.Name).SingleOrDefaultAsync(ct); if (participantName is null) return NotFound(); var teamName = await AddMembership(id, teamId, participantId.Value, role, reason, null, ct); SetStatus($"{participantName} added to {teamName}.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostAddExternalMemberAsync(Guid id, Guid teamId, string name, TeamMembershipRole role, string reason, CancellationToken ct)
    { var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == id && x.FormationType == TeamFormationType.Preformed, ct); if (team is null) return BadRequest(); var sequence = (await db.EventParticipants.Where(x => x.EventId == id).MaxAsync(x => (long?)x.SignupSequence, ct) ?? 0) + 1; var participant = new EventParticipant(Guid.NewGuid(), id, SignupStatus.Confirmed, sequence, time.GetUtcNow(), SignupSource.AdminCreated, null); db.EventParticipants.Add(participant); await characterService.ApplyFixedSignupAssignmentsAsync(participant, name, 1m, null, EhbSource.AdminCorrection, AdminId, ct); await db.SaveChangesAsync(ct); await AddMembership(id, teamId, participant.Id, role, reason, null, ct); SetStatus($"{name.Trim()} added to {team.Name}.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostRemoveMemberAsync(Guid id, Guid membershipId, string reason, CancellationToken ct)
    { var membership = await db.TeamMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.LeftAt == null, ct); if (membership is null) return NotFound(); var team = await db.Teams.AsNoTracking().SingleAsync(x => x.Id == membership.TeamId, ct); if (team.EventId != id) return BadRequest(); var participant = await db.EventParticipants.SingleAsync(x => x.Id == membership.EventParticipantId, ct); var participantName = await PrimaryName(participant.Id, ct); var draftLocked = await db.Events.AnyAsync(x => x.Id == id && x.DraftLocked, ct); if (team.FormationType == TeamFormationType.Drafted && draftLocked) { TempData["StatusMessage"] = "Drafted-team rosters are locked after the draft starts. Undo a draft pick instead."; return RedirectToPage(new { id }); } if (team.FormationType == TeamFormationType.Preformed && participant.Source != SignupSource.AdminCreated && draftLocked) { TempData["StatusMessage"] = "This website signup cannot return to the participant pool after the draft has started."; return RedirectToPage(new { id }); } membership.Leave(time.GetUtcNow(), reason); if (team.FormationType == TeamFormationType.Preformed && participant.Source == SignupSource.AdminCreated) { participant.Remove(time.GetUtcNow(), reason); await characterService.ReleaseAllAsync(participant.Id, AdminId, ct); } if (membership.Role is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain) await RevokeCaptainCredentialScope(membership.EventParticipantId, ct); await db.SaveChangesAsync(ct); await Audit("team.member_removed", "membership", membership.Id, reason, ct); SetStatus(team.FormationType == TeamFormationType.Preformed && participant.Source == SignupSource.AdminCreated ? $"{participantName} removed from {team.Name}." : $"{participantName} returned to the participant pool.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostChangeRoleAsync(Guid id, Guid membershipId, TeamMembershipRole role, CancellationToken ct)
    { var membership = await db.TeamMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.LeftAt == null, ct); if (membership is null) return NotFound(); var team = await db.Teams.AsNoTracking().SingleAsync(x => x.Id == membership.TeamId, ct); if (team.EventId != id) return BadRequest(); var wasCaptain = membership.Role is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain; membership.ChangeRole(role); if (wasCaptain && role is not (TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain)) await RevokeCaptainCredentialScope(membership.EventParticipantId, ct); await db.SaveChangesAsync(ct); await Audit("team.member_role_changed", "membership", membership.Id, role.ToString(), ct); var participantName = await PrimaryName(membership.EventParticipantId, ct); var roleLabel = role == TeamMembershipRole.CoCaptain ? "co-captain" : role.ToString().ToLowerInvariant(); SetStatus($"{participantName} is now {roleLabel}.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostMoveMemberAsync(Guid id, Guid membershipId, Guid targetTeamId, string reason, CancellationToken ct)
    { await using var tx = await db.Database.BeginTransactionAsync(ct); var membership = await db.TeamMemberships.SingleOrDefaultAsync(x => x.Id == membershipId && x.LeftAt == null, ct); if (membership is null) return NotFound(); var source = await db.Teams.AsNoTracking().SingleAsync(x => x.Id == membership.TeamId, ct); var target = await db.Teams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetTeamId && x.EventId == id, ct); if (source.EventId != id || source.FormationType != TeamFormationType.Preformed || target?.FormationType != TeamFormationType.Preformed) return BadRequest(); var participantName = await PrimaryName(membership.EventParticipantId, ct); membership.Leave(time.GetUtcNow(), reason); if (membership.Role is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain) await RevokeCaptainCredentialScope(membership.EventParticipantId, ct); db.TeamMemberships.Add(new TeamMembership(Guid.NewGuid(), target.Id, membership.EventParticipantId, membership.Role, time.GetUtcNow(), null, reason)); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await Audit("team.member_moved", "membership", membership.Id, $"{source.Name} → {target.Name}: {reason}", ct); SetStatus($"{participantName} moved from {source.Name} to {target.Name}.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostScrambleAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct);
        if (draft.State is not (DraftState.Running or DraftState.Paused)) return BadRequest();
        if (!RequireControl(draft, id)) return RedirectToPage(new { id });
        if (await db.DraftPicks.AnyAsync(x => x.DraftSessionId == draft.Id && x.UndoneAt == null, ct))
        {
            TempData["StatusMessage"] = "The team order cannot be changed after the first pick.";
            return RedirectToPage(new { id });
        }

        var teams = await db.Teams.Where(x => x.EventId == id && x.Active && x.IncludedInDraft).ToListAsync(ct);
        if (teams.Count < 2) return BadRequest();
        for (var i = teams.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (teams[i], teams[j]) = (teams[j], teams[i]);
        }
        for (var i = 0; i < teams.Count; i++) teams[i].SetDraftPosition(i + 1);
        draft.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration);
        await db.SaveChangesAsync(ct);
        await Audit("draft.order_scrambled", "draft", draft.Id, string.Join(", ", teams.Select(x => x.Name)), ct);
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostStartAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); var teams = await OrderedDraftTeams(id, ct);
        if (teams.Count < 2) { TempData["StatusMessage"] = "At least two drafted teams are required."; return RedirectToPage(new { id }); }
        var teamIds = teams.Select(x => x.Id).ToList(); var rosterSizes = await db.TeamMemberships.Where(x => teamIds.Contains(x.TeamId) && x.LeftAt == null).GroupBy(x => x.TeamId).ToDictionaryAsync(x => x.Key, x => x.Count(), ct); var requiredPlayers = teams.Sum(x => Math.Max(0, draft.TargetTeamSize - rosterSizes.GetValueOrDefault(x.Id))); var assignedIds = await db.TeamMemberships.Where(x => x.LeftAt == null).Select(x => x.EventParticipantId).ToListAsync(ct); var availablePlayers = await db.EventParticipants.CountAsync(x => x.EventId == id && x.Source != SignupSource.AdminCreated && x.SignupStatus == SignupStatus.Confirmed && !assignedIds.Contains(x.Id), ct);
        if (availablePlayers < requiredPlayers) { TempData["StatusMessage"] = $"The draft needs {requiredPlayers} available website signup(s), but only {availablePlayers} are available. Promote or add participants before starting."; return RedirectToPage(new { id }); }
        try
        {
            var now = time.GetUtcNow();
            foreach (var team in teams) team.SetDraftPosition(null);
            draft.AcquireControl(AdminId, now, DraftControlLease.Duration); draft.Start(now); var bingoEvent = await db.Events.SingleAsync(x => x.Id == id, ct); bingoEvent.SetDraftLocked(true); bingoEvent.CloseSignups(); await db.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (IsDraftConflict(exception)) { return DraftConflict(id, exception); }
        await Audit("draft.control_acquired", "draft", draft.Id, $"Controller: {User.Identity!.Name}", ct); await Audit("draft.started", "draft", draft.Id, $"{teams.Count} teams; awaiting team-order draw; {availablePlayers} available for {requiredPlayers} places", ct); await NotifyDraft(id, ct); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostConfigureAsync(Guid id, int teamCount, int targetSize, CancellationToken ct)
    { if (teamCount is < 2 or > 20 || targetSize is < 1 or > 100) return BadRequest(); var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (draft.State != DraftState.Setup) return BadRequest(); draft.ConfigureTargetSize(targetSize); var teams = await db.Teams.Where(x => x.EventId == id && x.FormationType == TeamFormationType.Drafted).OrderBy(x => x.Name).ToListAsync(ct); var active = teams.Where(x => x.Active).ToList(); if (teamCount < active.Count) { var removeCount = active.Count - teamCount; var occupiedIds = await db.TeamMemberships.Where(x => x.LeftAt == null && active.Select(team => team.Id).Contains(x.TeamId)).Select(x => x.TeamId).Distinct().ToListAsync(ct); var remove = active.Where(x => !occupiedIds.Contains(x.Id)).OrderByDescending(x => x.DraftPosition).ThenByDescending(x => x.Name).Take(removeCount).ToList(); if (remove.Count < removeCount) { TempData["StatusMessage"] = $"Only empty teams can be removed. Remove players from {removeCount - remove.Count} more team(s), or use × on a specific empty team."; return RedirectToPage(new { id }); } foreach (var team in remove) { team.SetActive(false); team.SetDraftPosition(null); } } else if (teamCount > active.Count) { var inactive = teams.Where(x => !x.Active).Take(teamCount - active.Count).ToList(); foreach (var team in inactive) team.SetActive(true); for (var number = active.Count + inactive.Count + 1; number <= teamCount; number++) db.Teams.Add(new Team(Guid.NewGuid(), id, $"Team {number}", await UniqueTeamSlug(id, $"Team {number}", ct), TeamFormationType.Drafted, null, true)); } await db.SaveChangesAsync(ct); await Audit("draft.setup_updated", "draft", draft.Id, $"{teamCount} teams; target {targetSize}", ct); SetStatus($"Draft setup updated to {teamCount} teams with {targetSize} players each.", UiMessageType.Success); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostPauseAsync(Guid id, CancellationToken ct) { var d = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (!RequireControl(d, id)) return RedirectToPage(new { id }); try { d.Pause(); d.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration); await db.SaveChangesAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.paused", "draft", d.Id, "Paused by admin", ct); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostResumeAsync(Guid id, CancellationToken ct) { var d = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (!RequireControl(d, id)) return RedirectToPage(new { id }); try { d.Resume(); d.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration); await db.SaveChangesAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.resumed", "draft", d.Id, "Resumed by admin", ct); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostPickAsync(Guid id, Guid participantId, CancellationToken ct)
    { await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct); var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (draft.State != DraftState.Running) return BadRequest(); if (!RequireControl(draft, id)) return RedirectToPage(new { id }); if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct)) return BadRequest(); var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.Id == participantId && x.EventId == id && x.SignupStatus == SignupStatus.Confirmed, ct); if (participant is null) return BadRequest(); var participantName = await PrimaryName(participantId, ct); var teams = await OrderedDraftTeams(id, ct); if (teams.Count < 2 || teams.Any(x => x.DraftPosition is null)) { TempData["StatusMessage"] = "Scramble the teams before making the first pick."; return RedirectToPage(new { id }); } var teamIds = teams.Select(x => x.Id).ToList(); var activePickTeams = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).OrderBy(x => x.PickNumber).Select(x => x.TeamId).ToListAsync(ct); var rosterSizes = await db.TeamMemberships.Where(x => teamIds.Contains(x.TeamId) && x.LeftAt == null).GroupBy(x => x.TeamId).ToDictionaryAsync(x => x.Key, x => x.Count(), ct); var turn = SnakeDraftOrder.GetNextEligibleTurn(activePickTeams, teamIds, rosterSizes, draft.TargetTeamSize); if (turn is null) { TempData["StatusMessage"] = "Every drafted team has reached its target size."; return RedirectToPage(new { id }); } var pick = new DraftPick(Guid.NewGuid(), draft.Id, turn.TeamId, participantId, turn.PickNumber, turn.RoundNumber, time.GetUtcNow()); db.DraftPicks.Add(pick); db.TeamMemberships.Add(new TeamMembership(Guid.NewGuid(), turn.TeamId, participantId, TeamMembershipRole.Participant, time.GetUtcNow(), pick.Id, "Snake draft pick")); try { draft.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.pick_recorded", "pick", pick.Id, $"#{turn.PickNumber} {participantName}", ct); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostUndoAsync(Guid id, CancellationToken ct)
    { await using var tx = await db.Database.BeginTransactionAsync(ct); var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (draft.State is not (DraftState.Running or DraftState.Paused)) return BadRequest(); if (!RequireControl(draft, id)) return RedirectToPage(new { id }); var pick = await db.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).OrderByDescending(x => x.PickNumber).FirstOrDefaultAsync(ct); if (pick is null) return RedirectToPage(new { id }); var membership = await db.TeamMemberships.SingleAsync(x => x.AssignedByDraftPickId == pick.Id && x.LeftAt == null, ct); pick.Undo(time.GetUtcNow()); membership.Leave(time.GetUtcNow(), "Draft pick undone"); try { draft.RenewControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.pick_undone", "pick", pick.Id, $"#{pick.PickNumber}", ct); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostFinalizeAsync(Guid id, CancellationToken ct)
    { await using var tx = await db.Database.BeginTransactionAsync(ct); var draft = await db.DraftSessions.SingleAsync(x => x.EventId == id, ct); if (!RequireControl(draft, id)) return RedirectToPage(new { id }); var teams = await db.Teams.Where(x => x.EventId == id && x.Active).ToListAsync(ct); var draftedTeamIds = teams.Where(x => x.IncludedInDraft).Select(x => x.Id).ToList(); var rosterSizes = await db.TeamMemberships.Where(x => draftedTeamIds.Contains(x.TeamId) && x.LeftAt == null).GroupBy(x => x.TeamId).ToDictionaryAsync(x => x.Key, x => x.Count(), ct); var incomplete = teams.Where(x => x.IncludedInDraft && rosterSizes.GetValueOrDefault(x.Id) < draft.TargetTeamSize).Select(x => $"{x.Name} ({rosterSizes.GetValueOrDefault(x.Id)}/{draft.TargetTeamSize})").ToList(); if (incomplete.Count > 0) { TempData["StatusMessage"] = $"Fill every drafted team before finalizing: {string.Join(", ", incomplete)}."; return RedirectToPage(new { id }); } draft.Finalize(time.GetUtcNow()); foreach (var team in teams) team.Finalize(time.GetUtcNow()); try { await db.SaveChangesAsync(ct); } catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); } await Audit("draft.finalized", "draft", draft.Id, $"{teams.Count} rosters published", ct); await tx.CommitAsync(ct); await NotifyDraft(id, ct); return RedirectToPage(new { id }); }

    public async Task<IActionResult> OnPostTakeControlAsync(Guid id, CancellationToken ct)
    {
        var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null) return NotFound();
        try
        {
            var previous = draft.AcquireControl(AdminId, time.GetUtcNow(), DraftControlLease.Duration, force: true);
            await db.SaveChangesAsync(ct);
            await Audit(previous is null ? "draft.control_acquired" : "draft.control_taken_over", "draft", draft.Id,
                previous is null ? $"Controller: {User.Identity!.Name}" : $"New controller: {User.Identity!.Name}; previous account: {previous}", ct);
        }
        catch (Exception ex) when (IsDraftConflict(ex)) { return DraftConflict(id, ex); }
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
        await NotifyDraft(id, ct);
        return RedirectToPage(new { id });
    }

    private async Task<string> AddMembership(Guid eventId, Guid teamId, Guid participantId, TeamMembershipRole role, string reason, Guid? pickId, CancellationToken ct) { var team = await db.Teams.SingleOrDefaultAsync(x => x.Id == teamId && x.EventId == eventId, ct); if (team is null) throw new InvalidOperationException("Team not found."); if (await db.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct)) throw new InvalidOperationException("Participant is already assigned to a team."); db.TeamMemberships.Add(new TeamMembership(Guid.NewGuid(), teamId, participantId, role, time.GetUtcNow(), pickId, reason)); await db.SaveChangesAsync(ct); await Audit("team.member_added", "team", teamId, $"{participantId}: {reason}", ct); return team.Name; }
    private async Task RevokeCaptainCredentialScope(Guid participantId, CancellationToken ct) { foreach (var access in await db.AccountEventAccesses.Where(access => access.ParticipantId == participantId && access.Enabled).ToListAsync(ct)) access.Disable(); }
    private async Task<List<Team>> OrderedDraftTeams(Guid id, CancellationToken ct) => await db.Teams.Where(x => x.EventId == id && x.Active && x.IncludedInDraft).OrderBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
    private async Task<string> UniqueTeamSlug(Guid eventId, string name, CancellationToken ct) { var root = EventSlugGenerator.Generate(name); var slug = root; for (var n = 2; await db.Teams.AnyAsync(x => x.EventId == eventId && x.Slug == slug, ct); n++) slug = $"{root}-{n}"; return slug; }
    private async Task<bool> Load(Guid id, string? sort, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ev is null) return false;
        EventName = ev.Name;
        Sort = sort is "name" or "signup" or "status" ? sort : "ehb";
        var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (draft is null)
        {
            ConfirmedCount = await db.EventParticipants.CountAsync(x => x.EventId == id && x.Source != SignupSource.AdminCreated && x.SignupStatus == SignupStatus.Confirmed, ct);
            Setup = new() { TeamCount = ev.ExpectedTeamCount ?? 2, TargetSize = ev.ExpectedTeamSize ?? 14 };
            return true;
        }

        Draft = new(draft.Id, draft.State, draft.TargetTeamSize);
        var teams = await db.Teams.AsNoTracking().Where(x => x.EventId == id && x.Active).OrderBy(x => x.IncludedInDraft ? 0 : 1).ThenBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
        var memberships = await db.TeamMemberships.AsNoTracking().Where(x => x.LeftAt == null && teams.Select(t => t.Id).Contains(x.TeamId)).ToListAsync(ct);
        var assignedParticipantIds = memberships.Select(x => x.EventParticipantId).ToList();
        var participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == id && (x.SignupStatus == SignupStatus.Confirmed || assignedParticipantIds.Contains(x.Id))).ToListAsync(ct);
        var authorities = await db.PrimaryCharacters().AsNoTracking().Where(x => x.EventId == id).ToDictionaryAsync(x => x.ParticipantId, ct);
        var eligibleParticipants = participants.Where(x => x.Source != SignupSource.AdminCreated && x.SignupStatus == SignupStatus.Confirmed).ToList();
        var teamMap = teams.ToDictionary(x => x.Id);
        var membershipByParticipant = memberships.ToDictionary(x => x.EventParticipantId);
        var internalParticipants = participants.Where(x => x.Source != SignupSource.AdminCreated);
        IEnumerable<EventParticipant> ordered = Sort switch
        {
            "name" => internalParticipants.OrderBy(x => authorities[x.Id].Name),
            "signup" => internalParticipants.OrderBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence),
            "status" => internalParticipants.OrderBy(x => membershipByParticipant.ContainsKey(x.Id)).ThenByDescending(x => authorities[x.Id].Ehb),
            _ => internalParticipants.OrderByDescending(x => authorities[x.Id].Ehb)
        };
        Participants = ordered.Select(x =>
        {
            membershipByParticipant.TryGetValue(x.Id, out var membership);
            var authority = authorities[x.Id];
            return new ParticipantView(x.Id, authority.Name, authority.Ehb, x.SignedUpAt, x.CaptainVolunteer, membership?.TeamId, membership is null ? null : teamMap[membership.TeamId].Name, x.SignupStatus);
        }).ToList();

        var activePicks = await db.DraftPicks.AsNoTracking().Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).OrderBy(x => x.PickNumber).ToListAsync(ct);
        var pickNumberByParticipant = activePicks.ToDictionary(x => x.EventParticipantId, x => x.PickNumber);
        var teamPickNumberByParticipant = activePicks.GroupBy(x => x.TeamId).SelectMany(group => group.OrderBy(x => x.PickNumber).Select((pick, index) => new { pick.EventParticipantId, TeamPickNumber = index + 1 })).ToDictionary(x => x.EventParticipantId, x => x.TeamPickNumber);
        var draftedOrder = teams.Where(x => x.IncludedInDraft).ToList();
        DraftOrderReady = draftedOrder.Count >= 2 && draftedOrder.All(x => x.DraftPosition is not null);
        if (draft.State == DraftState.Running && DraftOrderReady)
        {
            var orderedTeams = draftedOrder.OrderBy(x => x.DraftPosition).ToList();
            var rosterSizes = memberships.Where(x => orderedTeams.Any(team => team.Id == x.TeamId)).GroupBy(x => x.TeamId).ToDictionary(x => x.Key, x => x.Count());
            var turn = SnakeDraftOrder.GetNextEligibleTurn(activePicks.Select(x => x.TeamId).ToList(), orderedTeams.Select(x => x.Id).ToList(), rosterSizes, draft.TargetTeamSize);
            if (turn is not null) CurrentTurn = new(turn.PickNumber, turn.RoundNumber, turn.TeamId, teamMap[turn.TeamId].Name);
        }
        var latest = activePicks.LastOrDefault();
        if (latest is not null) LatestPick = new(latest.PickNumber, authorities[latest.EventParticipantId].Name, teamMap[latest.TeamId].Name);
        Teams = teams.Select(team => new TeamView(
            team.Id, team.Name, team.FormationType, team.AffiliationName, team.ImageUrl, team.DraftPosition,
            CurrentTurn?.TeamId == team.Id,
            memberships.Where(m => m.TeamId == team.Id)
                .OrderBy(m => m.Role == TeamMembershipRole.Captain ? 0 : m.Role == TeamMembershipRole.CoCaptain ? 1 : 2)
                .ThenBy(m => pickNumberByParticipant.GetValueOrDefault(m.EventParticipantId, int.MaxValue))
                .Select(m =>
                {
                    var participant = participants.Single(p => p.Id == m.EventParticipantId);
                    var authority = authorities[m.EventParticipantId];
                    teamPickNumberByParticipant.TryGetValue(m.EventParticipantId, out var pickNumber);
                    return new MemberView(m.Id, authority.Name, authority.Ehb, m.Role, participant.Source == SignupSource.AdminCreated, pickNumber == 0 ? null : pickNumber);
                }).ToList(),
            team.FormationType == TeamFormationType.Drafted
                ? memberships.Where(m => m.TeamId == team.Id).Sum(m => authorities[m.EventParticipantId].Ehb)
                : 0m)).ToList();
        ConfirmedCount = eligibleParticipants.Count;
        AssignedCount = memberships.Count(m => participants.Single(p => p.Id == m.EventParticipantId).Source != SignupSource.AdminCreated);
        AvailableCount = eligibleParticipants.Count(x => !membershipByParticipant.ContainsKey(x.Id));
        var draftedTeams = teams.Count(x => x.IncludedInDraft);
        var draftedAssigned = memberships.Count(m => teamMap[m.TeamId].IncludedInDraft);
        Remainder = Math.Max(0, AvailableCount - Math.Max(0, draftedTeams * draft.TargetTeamSize - draftedAssigned));
        return true;
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
        catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; return false; }
    }
    private RedirectToPageResult DraftConflict(Guid id, Exception exception)
    {
        db.ChangeTracker.Clear();
        TempData["StatusMessage"] = exception is DbUpdateConcurrencyException
            ? "Another administrator changed the draft first. Nothing from your stale action was saved; the latest draft has been loaded."
            : exception.Message;
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
    private Task Audit(string action, string target, Guid targetId, string details, CancellationToken ct) => audit.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, target, targetId.ToString(), details, ct); private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    private void StoreCredentials(IReadOnlyList<GeneratedCaptainCredential> credentials) { if (credentials.Count > 0) TempData["GeneratedCaptainCredentials"] = JsonSerializer.Serialize(credentials); }
    public sealed class SetupInput { [Range(2, 20)] public int TeamCount { get; set; } = 2; [Range(1, 100)] public int TargetSize { get; set; } = 14; }
    public sealed record DraftView(Guid Id, DraftState State, int TargetTeamSize); public sealed record ParticipantView(Guid Id, string Name, decimal Ehb, DateTimeOffset SignedUpAt, bool CaptainVolunteer, Guid? TeamId, string? TeamName, SignupStatus SignupStatus); public sealed record TeamView(Guid Id, string Name, TeamFormationType FormationType, string? Affiliation, string? ImageUrl, int? DraftPosition, bool IsCurrent, IReadOnlyList<MemberView> Members, decimal TotalEhb); public sealed record MemberView(Guid MembershipId, string Name, decimal Ehb, TeamMembershipRole Role, bool External, int? PickNumber); public sealed record TurnView(int PickNumber, int RoundNumber, Guid TeamId, string TeamName); public sealed record PickView(int PickNumber, string PlayerName, string TeamName);
}
