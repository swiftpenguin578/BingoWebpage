using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Signups;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ManageModel(ApplicationDbContext dbContext, ISignupService signupService, IAuditWriter auditWriter, TimeProvider timeProvider) : PageModel
{
    public EventDetails? EventView { get; private set; }
    public IReadOnlyList<ParticipantRow> Participants { get; private set; } = [];
    public IReadOnlyList<EvidenceCodeRow> EvidenceCodes { get; private set; } = [];
    [BindProperty, Range(1, 10000), Display(Name = "New participant cap")] public int NewCap { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Signups open")] public DateTimeOffset NewSignupOpening { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "New signup closing")] public DateTimeOffset NewSignupClosing { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Reason for reopening")] public string? StateReason { get; set; }
    [BindProperty, StringLength(100), Display(Name = "Evidence code")] public string? NewEvidenceCode { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Activates at")] public DateTimeOffset? EvidenceCodeActivatesAt { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Code note")] public string? EvidenceCodeNote { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Reopen until")] public DateTimeOffset? ReopenUntil { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await LoadAsync(id, ct) ? Page() : NotFound();
    public async Task<IActionResult> OnPostStateAsync(Guid id, EventState target, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(e => e.Id == id, ct); if (item is null) return NotFound();
        if (HasBindingErrors(nameof(StateReason))) { TempData["StatusMessage"] = "Check the signup details and try again."; return RedirectToPage(new { id }); }
        if (item.DraftLocked && target == EventState.SignupOpen) { TempData["StatusMessage"] = "Signups cannot be reopened after the draft has started."; return RedirectToPage(new { id }); }
        var from = item.State;
        if (from == EventState.SignupClosed && target == EventState.SignupOpen && string.IsNullOrWhiteSpace(StateReason)) { TempData["StatusMessage"] = "A reason is required when reopening signups."; return RedirectToPage(new { id }); }
        try
        {
            if (target == EventState.SignupOpen) item.OpenSignups(timeProvider.GetUtcNow());
            else if (target == EventState.SignupClosed) item.CloseSignups();
            else return BadRequest();
        }
        catch (InvalidOperationException ex)
        {
            TempData["StatusMessage"] = ex.Message;
            return RedirectToPage(new { id });
        }
        dbContext.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), id, from, item.State, User.GetAccountId()!.Value, timeProvider.GetUtcNow(), StateReason));
        await dbContext.SaveChangesAsync(ct); await AuditAsync("event.state_changed", item, $"{from} → {item.State}", ct);
        SetStatus(item.State == EventState.SignupOpen ? "Signups opened." : "Signups closed.", UiMessageType.Success);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostCapacityAsync(Guid id, CancellationToken ct)
    {
        if (HasBindingErrors(nameof(NewCap))) { TempData["StatusMessage"] = "Enter a valid player cap."; return RedirectToPage(new { id }); }
        if (await dbContext.Events.AnyAsync(e => e.Id == id && e.DraftLocked, ct)) { TempData["StatusMessage"] = "The participant cap cannot change after the draft has started."; return RedirectToPage(new { id }); }
        var before = await dbContext.Events.AsNoTracking().Where(e => e.Id == id).Select(e => e.ParticipantCap).SingleOrDefaultAsync(ct);
        try { var promoted = await signupService.IncreaseCapacityAndPromoteAsync(id, NewCap, ct); await AuditAsync("event.capacity_increased", await dbContext.Events.FindAsync([id], ct) ?? throw new InvalidOperationException(), $"{before} → {NewCap}; promoted {promoted}", ct); TempData["StatusMessage"] = $"Capacity increased. {promoted} participant(s) promoted."; }
        catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostSignupWindowAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(e => e.Id == id, ct); if (item is null) return NotFound();
        if (HasBindingErrors(nameof(NewSignupOpening), nameof(NewSignupClosing))) { TempData["StatusMessage"] = "Choose valid signup opening and closing times."; return RedirectToPage(new { id }); }
        var now = timeProvider.GetUtcNow();
        var futureOpeningIsOnHalfHour = NewSignupOpening <= now || NewSignupOpening.Minute is 0 or 30;
        if (!futureOpeningIsOnHalfHour || NewSignupClosing.Minute is not (0 or 30) || NewSignupOpening.Second != 0 || NewSignupClosing.Second != 0 || NewSignupOpening.Millisecond != 0 || NewSignupClosing.Millisecond != 0) { TempData["StatusMessage"] = "Future signup times must use a full or half hour, such as 18:30 or 19:00."; return RedirectToPage(new { id }); }
        var previousState = item.State;
        try
        {
            item.ChangeSignupWindow(NewSignupOpening, NewSignupClosing, now);
            if (previousState != item.State) dbContext.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), id, previousState, item.State, User.GetAccountId()!.Value, timeProvider.GetUtcNow(), "Signup window changed."));
            await dbContext.SaveChangesAsync(ct);
            await AuditAsync("event.signup_window_changed", item, $"{NewSignupOpening:O} → {NewSignupClosing:O}", ct);
            SetStatus($"Signup window updated: {NewSignupOpening.ToLocalTime():dd MMM yyyy, HH:mm} to {NewSignupClosing.ToLocalTime():dd MMM yyyy, HH:mm}.", UiMessageType.Success);
        }
        catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostRemoveAsync(Guid id, Guid participantId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) { TempData["StatusMessage"] = "A reason is required when removing a player."; return RedirectToPage(new { id }); }
        if (await dbContext.Events.AnyAsync(e => e.Id == id && e.DraftLocked, ct)) { TempData["StatusMessage"] = "Participants are locked after the draft has started. Undo or correct the draft instead of changing the signup pool."; return RedirectToPage(new { id }); }
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == id, ct); if (participant is null) return NotFound();
        if (await dbContext.TeamMemberships.AnyAsync(membership => membership.EventParticipantId == participantId && membership.LeftAt == null, ct)) { TempData["StatusMessage"] = "This player belongs to a team. Change or remove their roster membership from Teams and draft first."; return RedirectToPage(new { id }); }
        var wasConfirmed = participant.SignupStatus == SignupStatus.Confirmed; participant.Remove(timeProvider.GetUtcNow(), reason); await dbContext.SaveChangesAsync(ct);
        var promoted = wasConfirmed ? await signupService.PromoteAvailablePlacesAsync(id, ct) : 0;
        await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participant.removed", "participant", participant.Id.ToString(), $"Reason: {reason}; promoted {promoted}", ct);
        SetStatus($"{participant.PrimaryAccountName} removed. {promoted} player(s) promoted from the waiting list.", UiMessageType.Success);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, Guid participantId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) { TempData["StatusMessage"] = "A reason is required when withdrawing a player."; return RedirectToPage(new { id }); }
        if (await dbContext.Events.AnyAsync(e => e.Id == id && e.DraftLocked, ct)) { TempData["StatusMessage"] = "Participants are locked after the draft has started. Undo or correct the draft instead of changing the signup pool."; return RedirectToPage(new { id }); }
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == id, ct); if (participant is null) return NotFound(); if (await dbContext.TeamMemberships.AnyAsync(membership => membership.EventParticipantId == participantId && membership.LeftAt == null, ct)) { TempData["StatusMessage"] = "This player belongs to a team. Change or remove their roster membership from Teams and draft first."; return RedirectToPage(new { id }); }
        var wasConfirmed = participant.SignupStatus == SignupStatus.Confirmed; participant.Withdraw(timeProvider.GetUtcNow(), reason); await dbContext.SaveChangesAsync(ct); var promoted = wasConfirmed ? await signupService.PromoteAvailablePlacesAsync(id, ct) : 0; await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participant.withdrawn", "participant", participant.Id.ToString(), $"Reason: {reason}; promoted {promoted}", ct); SetStatus($"{participant.PrimaryAccountName} withdrawn. {promoted} player(s) promoted from the waiting list.", UiMessageType.Success); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostPaymentAsync(Guid id, Guid participantId, PaymentStatus payment, CancellationToken ct)
    {
        if (HasBindingErrors("payment")) { TempData["StatusMessage"] = "Choose a valid payment status."; return RedirectToPage(new { id }); }
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == id, ct); if (participant is null) return NotFound(); if (!Enum.IsDefined(payment)) { TempData["StatusMessage"] = "Choose a valid payment status."; return RedirectToPage(new { id }); }
        participant.SetPaymentStatus(payment); await dbContext.SaveChangesAsync(ct); await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "participant.payment_updated", "participant", participant.Id.ToString(), $"Payment: {payment}", ct); TempData["StatusMessage"] = $"Payment updated for {participant.PrimaryAccountName}."; return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostStartEventAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound(); if (!await dbContext.Boards.AnyAsync(x => x.EventId == id && x.State == Bingo.Domain.Boards.BoardState.Published, ct)) { TempData["StatusMessage"] = "Publish the board before starting the event."; return RedirectToPage(new { id }); }
        if (!await dbContext.DraftSessions.AnyAsync(x => x.EventId == id && x.State == DraftState.Finalized, ct)) { TempData["StatusMessage"] = "Finalize the team rosters before starting the event."; return RedirectToPage(new { id }); }
        try { item.StartEvent(timeProvider.GetUtcNow()); await dbContext.SaveChangesAsync(ct); await AuditAsync("event.started", item, "Event entered Live state", ct); SetStatus("Event started.", UiMessageType.Success); } catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostEndEventAsync(Guid id, CancellationToken ct)
    { var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound(); try { item.EndEvent(); await dbContext.SaveChangesAsync(ct); await AuditAsync("event.ended", item, "Awaiting final review", ct); SetStatus("Event ended and moved to final review.", UiMessageType.Success); } catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; } return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostReopenSubmissionsAsync(Guid id, CancellationToken ct)
    { var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound(); if (HasBindingErrors(nameof(ReopenUntil), nameof(StateReason)) || ReopenUntil is null || string.IsNullOrWhiteSpace(StateReason)) { TempData["StatusMessage"] = "A valid future cutoff and reason are required."; return RedirectToPage(new { id }); } try { item.ReopenSubmissions(ReopenUntil.Value, timeProvider.GetUtcNow()); await dbContext.SaveChangesAsync(ct); await AuditAsync("event.submissions_reopened", item, $"Until {ReopenUntil:O}; {StateReason}", ct); TempData["StatusMessage"] = $"Submissions reopened until {ReopenUntil.Value.ToLocalTime():g}."; } catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; } return RedirectToPage(new { id }); }
    public Task<IActionResult> OnPostEnableEvidenceCodesAsync(Guid id, CancellationToken ct) => SetEvidenceCodeMode(id, true, ct);
    public Task<IActionResult> OnPostDisableEvidenceCodesAsync(Guid id, CancellationToken ct) => SetEvidenceCodeMode(id, false, ct);
    private async Task<IActionResult> SetEvidenceCodeMode(Guid id, bool enabled, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound(); item.SetEvidenceCodeEnabled(enabled); await dbContext.SaveChangesAsync(ct); await AuditAsync("event.evidence_code_mode", item, enabled ? "Enabled" : "Disabled", ct); TempData["StatusMessage"] = enabled ? "Verification codes enabled." : "Verification codes disabled."; return RedirectToPage(new { id });
    }
    public Task<IActionResult> OnPostCreateEvidenceCodeAsync(Guid id, CancellationToken ct) => CreateEvidenceCode(id, NewEvidenceCode, ct);
    private async Task<IActionResult> CreateEvidenceCode(Guid id, string? code, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound(); if (HasBindingErrors(nameof(NewEvidenceCode), nameof(EvidenceCodeActivatesAt), nameof(EvidenceCodeNote))) { TempData["StatusMessage"] = "Check the verification code details and try again."; return RedirectToPage(new { id }); }
        if (!item.EvidenceCodeEnabled) { TempData["StatusMessage"] = "Enable evidence codes first."; return RedirectToPage(new { id }); }
        if (string.IsNullOrWhiteSpace(code)) { TempData["StatusMessage"] = "Enter or generate a code first."; return RedirectToPage(new { id }); }
        var activates = (EvidenceCodeActivatesAt ?? timeProvider.GetUtcNow()).ToUniversalTime(); if (await dbContext.EvidenceCodes.AnyAsync(x => x.EventId == id && x.ActivatesAt == activates, ct)) { TempData["StatusMessage"] = "Another code already activates at that exact time."; return RedirectToPage(new { id }); }
        var created = new EvidenceCode(Guid.NewGuid(), id, code, activates, User.GetAccountId()!.Value, timeProvider.GetUtcNow(), EvidenceCodeNote); dbContext.EvidenceCodes.Add(created); var codes = await dbContext.EvidenceCodes.Where(x => x.EventId == id).OrderBy(x => x.ActivatesAt).ToListAsync(ct); codes.Add(created); codes = codes.OrderBy(x => x.ActivatesAt).ToList(); for (var index = 0; index < codes.Count; index++) codes[index].SetRetiresAt(index + 1 < codes.Count ? codes[index + 1].ActivatesAt : null); await dbContext.SaveChangesAsync(ct); await AuditAsync("evidence_code.created", item, $"{created.Code}; activates {activates:O}", ct); TempData["StatusMessage"] = $"Evidence code {created.Code} saved."; return RedirectToPage(new { id });
    }
    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct); if (item is null) return false;
        var participants = await dbContext.EventParticipants.AsNoTracking().Where(p => p.EventId == id && p.Source != SignupSource.AdminCreated).OrderBy(p => p.SignedUpAt).ThenBy(p => p.SignupSequence).ToListAsync(ct);
        var waiting = participants.Where(p => p.SignupStatus == SignupStatus.WaitingList).Select((p, i) => (p.Id, Position: i + 1)).ToDictionary(x => x.Id, x => x.Position);
        var activeTeamIds = await dbContext.Teams.AsNoTracking().Where(team => team.EventId == id && team.Active).Select(team => team.Id).ToListAsync(ct);
        var membershipCounts = activeTeamIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.TeamMemberships.AsNoTracking().Where(membership => activeTeamIds.Contains(membership.TeamId) && membership.LeftAt == null).GroupBy(membership => membership.TeamId).ToDictionaryAsync(group => group.Key, group => group.Count(), ct);
        var teamSizes = activeTeamIds.Select(teamId => membershipCounts.GetValueOrDefault(teamId)).ToList();
        var actualTeamSize = teamSizes.Count == 0 ? null : teamSizes.Min() == teamSizes.Max() ? teamSizes[0].ToString(CultureInfo.InvariantCulture) : $"{teamSizes.Min()}–{teamSizes.Max()}";
        var board = await dbContext.Boards.AsNoTracking().Where(value => value.EventId == id).Select(value => new { value.Rows, value.Columns, value.State }).SingleOrDefaultAsync(ct);
        var boardSize = board is null ? null : $"{board.Rows} × {board.Columns}";
        var draftReady = await dbContext.DraftSessions.AsNoTracking().AnyAsync(session => session.EventId == id && session.State == DraftState.Finalized, ct);
        var canStartEvent = board?.State == BoardState.Published && draftReady;
        Participants = participants.Select(p => new ParticipantRow(p.Id, p.SignupSequence, p.PrimaryAccountName, p.EhbSnapshot, p.SignupStatus, p.PaymentStatus, p.SignedUpAt, p.CaptainVolunteer, waiting.TryGetValue(p.Id, out var position) ? position : null)).ToList();
        EvidenceCodes = await dbContext.EvidenceCodes.AsNoTracking().Where(x => x.EventId == id).OrderByDescending(x => x.ActivatesAt).Select(x => new EvidenceCodeRow(x.Id, x.Code, x.ActivatesAt, x.RetiresAt, x.Note)).ToListAsync(ct); EventView = new EventDetails(item.Id, item.Name, item.Slug, item.State, item.SignupOpensAt, item.SignupClosesAt, item.EventStartsAt, item.EventEndsAt, item.SubmissionCutoffAt, item.ReopenedSubmissionCutoffAt, item.ParticipantCap, participants.Count(p => p.SignupStatus == SignupStatus.Confirmed), waiting.Count, item.DraftLocked, item.EvidenceCodeEnabled, activeTeamIds.Count, actualTeamSize, boardSize, item.ExpectedTeamCount, item.ExpectedTeamSize, item.ExpectedBoardRows is not null && item.ExpectedBoardColumns is not null ? $"{item.ExpectedBoardRows} × {item.ExpectedBoardColumns}" : null, canStartEvent); NewCap = item.ParticipantCap; NewSignupOpening = item.SignupOpensAt; NewSignupClosing = item.SignupClosesAt; EvidenceCodeActivatesAt = timeProvider.GetUtcNow(); ReopenUntil = timeProvider.GetUtcNow().AddHours(1); return true;
    }
    private Task AuditAsync(string action, BingoEvent item, string details, CancellationToken ct) => auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, "event", item.Id.ToString(), details, ct);
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    private bool HasBindingErrors(params string[] fields) => fields.Any(field => ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0);
    public sealed record EventDetails(Guid Id, string Name, string Slug, EventState State, DateTimeOffset SignupOpensAt, DateTimeOffset SignupClosesAt, DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset SubmissionCutoff, DateTimeOffset? ReopenedCutoff, int ParticipantCap, int Confirmed, int Waiting, bool DraftLocked, bool EvidenceCodeEnabled, int ActualTeamCount, string? ActualTeamSize, string? ActualBoardSize, int? ExpectedTeamCount, int? ExpectedTeamSize, string? ExpectedBoardSize, bool CanStartEvent);
    public sealed record ParticipantRow(Guid Id, long Sequence, string Name, decimal Ehb, SignupStatus Status, PaymentStatus Payment, DateTimeOffset SignedUpAt, bool CaptainVolunteer, int? WaitingPosition);
    public sealed record EvidenceCodeRow(Guid Id, string Code, DateTimeOffset ActivatesAt, DateTimeOffset? RetiresAt, string? Note);
}
