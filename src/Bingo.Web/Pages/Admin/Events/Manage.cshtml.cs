using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Events;
using Bingo.Application.Signups;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ManageModel(ApplicationDbContext dbContext, ISignupService signupService, EventParticipantCharacterService characterService, IAuditWriter auditWriter, IEventReadinessEvaluator readinessEvaluator, IEventSignupLifecycleService signupLifecycle, IEventLifecycleService eventLifecycle, IEventDestructiveLifecycleService destructiveLifecycle, TimeProvider timeProvider) : PageModel
{
    public EventDetails? EventView { get; private set; }
    public IReadOnlyList<ParticipantRow> Participants { get; private set; } = [];
    public IReadOnlyList<TeamOption> ParticipantTeams { get; private set; } = [];
    public IReadOnlyList<ParticipantModel.QuestionView> ActiveSignupQuestions { get; private set; } = [];
    [BindProperty] public InternalParticipantInput InternalParticipant { get; set; } = new();
    public int TotalParticipantCount { get; private set; }
    public int WithdrawnParticipantCount { get; private set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantSearch { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantStatus { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantPayment { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantDiscord { get; set; }
    [BindProperty(SupportsGet = true)] public bool? ParticipantCaptain { get; set; }
    [BindProperty(SupportsGet = true)] public string? ParticipantSource { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ParticipantTeamId { get; set; }
    public IReadOnlyList<EvidenceCodeRow> EvidenceCodes { get; private set; } = [];
    public SignupReadiness? SignupReadiness { get; private set; }
    public EventStartReadiness? StartReadiness { get; private set; }
    public ScheduledActionView? ScheduledAction { get; private set; }
    public bool CanDiscard { get; private set; }
    public string? PrivateCancellationReason { get; private set; }
    [BindProperty, Range(1, 10000), Display(Name = "New participant cap")] public int NewCap { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Signups open")] public DateTimeOffset NewSignupOpening { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "New signup closing")] public DateTimeOffset NewSignupClosing { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Reason for reopening")] public string? StateReason { get; set; }
    [BindProperty, StringLength(100), Display(Name = "Evidence code")] public string? NewEvidenceCode { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Activates at")] public DateTimeOffset? EvidenceCodeActivatesAt { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Code note")] public string? EvidenceCodeNote { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Reopen until")] public DateTimeOffset? ReopenUntil { get; set; }
    [BindProperty] public long EventVersion { get; set; }
    [BindProperty] public bool AcknowledgeSignupWarnings { get; set; }
    [BindProperty] public bool AcceptProposedClose { get; set; }
    [BindProperty] public bool ConfirmStartEvent { get; set; }
    [BindProperty, StringLength(2000)] public string? StartReason { get; set; }
    [BindProperty] public bool ConfirmEndEvent { get; set; }
    [BindProperty, StringLength(2000)] public string? EndReason { get; set; }
    [BindProperty] public bool ConfirmResumeEvent { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Replacement event end")] public DateTimeOffset ReplacementEventEndsAt { get; set; }
    [BindProperty, StringLength(2000), Display(Name = "Reason for resuming")]
    public string? ResumeReason { get; set; }
    [BindProperty] public bool ConfirmDestructiveAction { get; set; }
    [BindProperty, StringLength(2000)] public string? CancellationReason { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) { _ = characterService; return await LoadAsync(id, ct) ? Page() : NotFound(); }
    public async Task<IActionResult> OnPostStateAsync(Guid id, EventState target, CancellationToken ct)
    {
        if (target == EventState.SignupClosed) return await OnPostCloseSignupAsync(id, ct);
        var current = await dbContext.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.State).SingleOrDefaultAsync(ct);
        return current == EventState.SignupClosed ? await OnPostReopenSignupAsync(id, ct) : await OnPostOpenSignupAsync(id, ct);
    }
    public async Task<IActionResult> OnPostOpenSignupAsync(Guid id, CancellationToken ct) => await SignupResult(await signupLifecycle.OpenAsync(id, EventVersion, AcknowledgeSignupWarnings, AcceptProposedClose, Actor, ct), id, "Signups opened.");
    public async Task<IActionResult> OnPostCloseSignupAsync(Guid id, CancellationToken ct) => await SignupResult(await signupLifecycle.CloseAsync(id, EventVersion, Actor, ct), id, "Signups closed.");
    public async Task<IActionResult> OnPostReopenSignupAsync(Guid id, CancellationToken ct) => await SignupResult(await signupLifecycle.ReopenAsync(id, EventVersion, AcknowledgeSignupWarnings, AcceptProposedClose, Actor, ct), id, "Signups reopened.");
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
        if (!await dbContext.Events.AnyAsync(e => e.Id == id, ct)) return NotFound();
        return RedirectToPage("Schedule", new { id });
    }
    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, Guid participantId, CancellationToken ct)
    {
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == id, ct); if (participant is null) return NotFound(); if (await dbContext.TeamMemberships.AnyAsync(membership => membership.EventParticipantId == participantId && membership.LeftAt == null, ct)) { TempData["StatusMessage"] = "This player belongs to a team. Change or remove their roster membership from Teams and draft first."; return RedirectToPage(new { id }); }
        var result = await signupService.WithdrawAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", true, cancellationToken: ct);
        SetStatus(result.Succeeded ? "Participant withdrawn. The waiting list was promoted where a place became available." : result.Error ?? "The participant could not be withdrawn.", result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostPaymentAsync(Guid id, Guid participantId, PaymentStatus payment, CancellationToken ct)
    {
        var result = await signupService.SetPaymentAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", payment, ct);
        SetStatus(result.Succeeded ? "Payment saved." : result.Error ?? "Payment could not be saved.", result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(null, null, new { id, ParticipantSearch, ParticipantStatus, ParticipantPayment, ParticipantDiscord, ParticipantCaptain, ParticipantSource, ParticipantTeamId }, "players");
    }
    public async Task<IActionResult> OnPostCreateInternalParticipantAsync(Guid id, CancellationToken ct)
    {
        var actorId = User.GetAccountId(); if (actorId is null) return Forbid();
        Guid? ownerId = null;
        if (!string.IsNullOrWhiteSpace(InternalParticipant.OwnerUsername))
        {
            ownerId = await dbContext.Accounts.AsNoTracking().Where(x => x.LoginName == InternalParticipant.OwnerUsername.Trim()).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (ownerId is null)
            {
                SetStatus("The selected owner must be an active website account.", UiMessageType.Error);
                return RedirectToPage(null, null, new { id, ParticipantSearch, ParticipantStatus, ParticipantPayment, ParticipantDiscord, ParticipantCaptain, ParticipantSource, ParticipantTeamId }, "players");
            }
        }
        var result = await signupService.CreateAdminParticipantAsync(new AdminParticipantChangeRequest(id, null, actorId.Value, User.Identity?.Name ?? "Admin", ownerId,
            InternalParticipant.AccountAnswers.ToDictionary(x => x.Key, x => new AdminAccountAnswer(x.Value.CharacterName, x.Value.Ehb)), InternalParticipant.Answers), ct);
        SetStatus(result.Succeeded ? result.Status == SignupStatus.WaitingList ? $"Internal participant created at waiting-list position {result.WaitingPosition}." : "Internal participant created." : result.Error ?? "Internal participant could not be created.", result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(null, null, new { id, ParticipantSearch, ParticipantStatus, ParticipantPayment, ParticipantDiscord, ParticipantCaptain, ParticipantSource, ParticipantTeamId }, "players");
    }
    public async Task<IActionResult> OnPostStartEventAsync(Guid id, CancellationToken ct)
    {
        var result = await eventLifecycle.StartNowAsync(id, EventVersion, ConfirmStartEvent, StartReason, Actor, ct);
        SetStatus(result.Succeeded ? "Event started." : result.Error ?? "The event could not be started.", result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostEndEventAsync(Guid id, CancellationToken ct)
    {
        var result = await eventLifecycle.EndNowAsync(id, EventVersion, ConfirmEndEvent, EndReason, Actor, ct);
        SetStatus(result.Succeeded ? "Event ended and moved to final review." : result.Error ?? "The event could not be ended.", result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostResumeEventAsync(Guid id, CancellationToken ct)
    {
        var result = await eventLifecycle.ResumePrematureEndAsync(id, EventVersion, ConfirmResumeEvent, ResumeReason, ReplacementEventEndsAt, Actor, ct);
        SetStatus(result.Succeeded ? "Event resumed and returned to live play." : result.Error ?? "The event could not be resumed.", result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostDiscardAsync(Guid id, CancellationToken ct)
    {
        var result = await destructiveLifecycle.DiscardAsync(id, EventVersion, ConfirmDestructiveAction, Actor, ct);
        SetStatus(result.Succeeded ? "Event discarded." : result.Error ?? "The event could not be discarded.", result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return result.Succeeded ? RedirectToPage("Index") : RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken ct)
    {
        var result = await destructiveLifecycle.CancelAsync(id, EventVersion, ConfirmDestructiveAction, CancellationReason, Actor, ct);
        SetStatus(result.Succeeded ? "Event cancelled." : result.Error ?? "The event could not be cancelled.", result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
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
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id && e.State != EventState.Discarded, ct); if (item is null) return false;
        var allParticipants = await dbContext.EventParticipants.AsNoTracking().Where(p => p.EventId == id).OrderBy(p => p.SignedUpAt).ThenBy(p => p.SignupSequence).ToListAsync(ct);
        ActiveSignupQuestions = await dbContext.SignupQuestions.AsNoTracking().Where(x => x.EventId == id && x.Active).OrderBy(x => x.Position)
            .Select(x => new ParticipantModel.QuestionView(x.Id, x.Label, x.Type, x.Required, true, x.AccountAnswerRole, x.Options == null ? Array.Empty<string>() : x.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), null)).ToListAsync(ct);
        TotalParticipantCount = allParticipants.Count;
        WithdrawnParticipantCount = allParticipants.Count(p => p.SignupStatus == SignupStatus.Withdrawn);
        var participants = allParticipants.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(ParticipantSearch))
        {
            var search = ParticipantSearch.Trim().ToUpperInvariant();
            var matchingIds = await dbContext.AdminPrimaryCharacters().AsNoTracking().Where(x => x.EventId == id && x.NormalizedName.Contains(search)).Select(x => x.ParticipantId).ToListAsync(ct);
            participants = participants.Where(p => matchingIds.Contains(p.Id));
        }
        if (Enum.TryParse<SignupStatus>(ParticipantStatus, true, out var status)) participants = participants.Where(p => p.SignupStatus == status);
        if (ParticipantPayment == "paid") participants = participants.Where(p => p.PaymentReceived);
        if (ParticipantPayment == "unpaid") participants = participants.Where(p => !p.PaymentReceived);
        if (ParticipantCaptain is not null) participants = participants.Where(p => p.CaptainVolunteer == ParticipantCaptain);
        if (Enum.TryParse<SignupSource>(ParticipantSource, true, out var source)) participants = participants.Where(p => p.Source == source);
        var memberships = await dbContext.TeamMemberships.AsNoTracking().Where(m => m.LeftAt == null).ToListAsync(ct);
        if (ParticipantTeamId is { } selectedTeam) participants = participants.Where(p => memberships.Any(m => m.EventParticipantId == p.Id && m.TeamId == selectedTeam));
        var ownerIds = allParticipants.Where(p => p.AccountId != null).Select(p => p.AccountId!.Value).Distinct().ToList();
        var owners = await dbContext.Accounts.AsNoTracking().Where(a => ownerIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        if (ParticipantDiscord == "linked") participants = participants.Where(p => p.AccountId is { } owner && owners.TryGetValue(owner, out var account) && account.DiscordUserId != null);
        if (ParticipantDiscord == "unlinked") participants = participants.Where(p => p.AccountId is null || !owners.TryGetValue(p.AccountId.Value, out var account) || account.DiscordUserId == null);
        var filteredParticipants = participants.ToList();
        var authorities = await dbContext.AdminPrimaryCharacters().AsNoTracking().Where(x => x.EventId == id).ToDictionaryAsync(x => x.ParticipantId, ct);
        var waiting = allParticipants.Where(p => p.SignupStatus == SignupStatus.WaitingList).Select((p, i) => (p.Id, Position: i + 1)).ToDictionary(x => x.Id, x => x.Position);
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
        var postponed = await dbContext.ScheduledEventStartAttempts.AsNoTracking().Where(x => x.EventId == id && x.ScheduledFor <= timeProvider.GetUtcNow() && !x.Started && x.ResolvedAt == null).OrderByDescending(x => x.AttemptedAt).FirstOrDefaultAsync(ct);
        StartReadiness = await eventLifecycle.GetStartReadinessAsync(id, ct);
        var teams = await dbContext.Teams.AsNoTracking().Where(team => team.EventId == id).OrderBy(team => team.Name).ToListAsync(ct);
        ParticipantTeams = teams.Select(team => new TeamOption(team.Id, team.Name)).ToList();
        var teamNames = teams.ToDictionary(team => team.Id, team => team.Name);
        Participants = filteredParticipants.Select(p => new ParticipantRow(p.Id, p.SignupSequence, authorities.TryGetValue(p.Id, out var primary) ? primary.Name : "External roster member", authorities.TryGetValue(p.Id, out primary) ? primary.Ehb : 0m, p.SignupStatus, p.PaymentStatus, p.SignedUpAt, p.CaptainVolunteer, waiting.TryGetValue(p.Id, out var position) ? position : null, p.Source, p.AccountId is { } owner && owners.TryGetValue(owner, out var account) ? account.LoginName : null, p.AccountId is { } linkedOwner && owners.TryGetValue(linkedOwner, out var discordAccount) && discordAccount.DiscordUserId is not null, memberships.Where(m => m.EventParticipantId == p.Id).Select(m => teamNames.GetValueOrDefault(m.TeamId)).FirstOrDefault())).ToList();
        EvidenceCodes = await dbContext.EvidenceCodes.AsNoTracking().Where(x => x.EventId == id).OrderByDescending(x => x.ActivatesAt).Select(x => new EvidenceCodeRow(x.Id, x.Code, x.ActivatesAt, x.RetiresAt, x.Note)).ToListAsync(ct);
        EventView = new EventDetails(item.Id, item.Name, item.Slug, item.State, item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ActualSignupOpenedAt, item.ActualSignupClosedAt, item.ActualStartedAt, item.ActualEndedAt, item.SubmissionsClosedAt, item.ScheduledSignupOpeningEnabled, item.ReopenedSubmissionCutoffAt, item.ParticipantCap ?? 0, allParticipants.Count(p => p.SignupStatus == SignupStatus.Confirmed), waiting.Count, item.DraftLocked, item.EvidenceCodeEnabled, activeTeamIds.Count, actualTeamSize, boardSize, item.ExpectedTeamCount, item.ExpectedTeamSize, item.ExpectedBoardRows is not null && item.ExpectedBoardColumns is not null ? $"{item.ExpectedBoardRows} × {item.ExpectedBoardColumns}" : null, canStartEvent, EventDisplayPhaseProjection.From(new(item.State, draftReady, board?.State == BoardState.Published, postponed is not null, StartReadiness?.CanProceed)));
        SignupReadiness = await readinessEvaluator.GetSignupReadinessAsync(id, item.State == EventState.SignupClosed ? SignupOpeningMode.Reopen : SignupOpeningMode.OpenNow, timeProvider.GetUtcNow(), ct);
        CanDiscard = item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed
            && !await dbContext.EventParticipants.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.Teams.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.AccountEventAccesses.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.Submissions.AnyAsync(x => x.EventId == id, ct);
        PrivateCancellationReason = item.State == EventState.Cancelled ? item.CancellationReason : null;
        var failedOpening = await dbContext.ScheduledSignupOpeningAttempts.AsNoTracking().Where(x => x.EventId == id && !x.Opened && x.ResolvedAt == null).OrderByDescending(x => x.AttemptedAt).FirstOrDefaultAsync(ct);
        ScheduledAction = postponed is not null && item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed
            ? new("Automatic start postponed", postponed.ScheduledFor, postponed.AttemptedAt, StartReadiness?.Blockers ?? [])
            : failedOpening is not null ? new("Scheduled signup opening failed", failedOpening.ScheduledFor, failedOpening.AttemptedAt,
                failedOpening.Details.Count > 0
                    ? failedOpening.Details.Select(detail => new ReadinessItem("SCHEDULED_OPENING_FAILED", detail, $"/Admin/Events/Schedule/{id}")).ToArray()
                    : failedOpening.Blockers.Select(code => DescribeBlocker(code, id, item.State)).ToArray()) : null;
        EventVersion = item.Version; NewCap = item.ParticipantCap ?? 0; NewSignupOpening = item.SignupOpensAt ?? timeProvider.GetUtcNow(); NewSignupClosing = item.SignupClosesAt ?? timeProvider.GetUtcNow().AddDays(1); EvidenceCodeActivatesAt = timeProvider.GetUtcNow(); ReopenUntil = timeProvider.GetUtcNow().AddHours(1); ReplacementEventEndsAt = item.EventEndsAt ?? timeProvider.GetUtcNow().AddHours(1); return true;
    }
    private LifecycleActor Actor => new(User.GetAccountId()!.Value, User.Identity!.Name!);
    private Task<IActionResult> SignupResult(SignupLifecycleResult result, Guid id, string success)
    { TempData["StatusMessage"] = result.Succeeded ? success : result.ProposedClose is { } close ? $"{result.Error} Proposed close: {close.ToLocalTime():dd MMM yyyy, HH:mm}." : result.Error; if (result.Succeeded) TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString(); return Task.FromResult<IActionResult>(RedirectToPage(new { id })); }
    private Task AuditAsync(string action, BingoEvent item, string details, CancellationToken ct) => auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, "event", item.Id.ToString(), details, ct);
    private Task<string> PrimaryNameAsync(Guid participantId, CancellationToken ct) =>
        dbContext.AdminPrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => x.Name).SingleAsync(ct);
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    private bool HasBindingErrors(params string[] fields) => fields.Any(field => ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0);
    public sealed record EventDetails(Guid Id, string Name, string Slug, EventState State, DateTimeOffset? SignupOpensAt, DateTimeOffset? SignupClosesAt, DateTimeOffset? DraftAt, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? ActualSignupOpenedAt, DateTimeOffset? ActualSignupClosedAt, DateTimeOffset? ActualStartedAt, DateTimeOffset? ActualEndedAt, DateTimeOffset? SubmissionsClosedAt, bool ScheduledSignupOpeningEnabled, DateTimeOffset? ReopenedCutoff, int ParticipantCap, int Confirmed, int Waiting, bool DraftLocked, bool EvidenceCodeEnabled, int ActualTeamCount, string? ActualTeamSize, string? ActualBoardSize, int? ExpectedTeamCount, int? ExpectedTeamSize, string? ExpectedBoardSize, bool CanStartEvent, EventDisplayPhase DisplayPhase);
    private static ReadinessItem DescribeBlocker(string code, Guid eventId, EventState eventState) => code switch
    {
        "DRAFT_NOT_FINALIZED" => new(code, "Finalize the team draft.", $"/Admin/Events/Draft/{eventId}"),
        "BOARD_NOT_PUBLISHED" => new(code, "Publish the approved board.", $"/Admin/Events/Board/{eventId}"),
        "TEAM_ACCESS_MISSING" => new(code, "Give every active team a current Captain or enabled emergency credential.", $"/Admin/Events/Teams/{eventId}"),
        "LIFECYCLE_STATE_INVALID" when eventState == EventState.Draft => new(code, "Signup has not been opened and closed. Open signup, then close it before starting the event.", $"/Admin/Events/Manage/{eventId}"),
        "LIFECYCLE_STATE_INVALID" when eventState == EventState.SignupOpen => new(code, "Signup is still open. Close signup before starting the event.", $"/Admin/Events/Manage/{eventId}"),
        "LIFECYCLE_STATE_INVALID" => new(code, "The scheduled start was postponed until signup lifecycle requirements are resolved.", $"/Admin/Events/Manage/{eventId}"),
        "SCHEDULE_INVALID" => new(code, "Configure a valid event start and end.", $"/Admin/Events/Schedule/{eventId}"),
        "CURRENT_EVENT_EXISTS" => new(code, "Another event is already Live, in final review, or finalized.", "/Admin/Events"),
        "EVENT_WINDOW_OVERLAP" => new(code, "The event window overlaps another active lifecycle window.", "/Admin/Events"),
        "DESCRIPTION_REQUIRED" => new(code, "Add a public event description.", $"/Admin/Events/Identity/{eventId}"),
        "PARTICIPANT_CAP_REQUIRED" => new(code, "Set a participant capacity.", $"/Admin/Events/Schedule/{eventId}"),
        _ when code.StartsWith("UNACKNOWLEDGED_", StringComparison.Ordinal) => new(code, "A signup warning became active after scheduling and needs Admin review.", $"/Admin/Events/Schedule/{eventId}"),
        _ => new(code, "Review the event configuration and resolve this lifecycle blocker.", $"/Admin/Events/Manage/{eventId}")
    };
    public sealed record ScheduledActionView(string Title, DateTimeOffset ScheduledFor, DateTimeOffset AttemptedAt, IReadOnlyList<ReadinessItem> Blockers);
    public sealed record ParticipantRow(Guid Id, long Sequence, string Name, decimal Ehb, SignupStatus Status, PaymentStatus Payment, DateTimeOffset SignedUpAt, bool CaptainVolunteer, int? WaitingPosition, SignupSource Source, string? WebsiteUsername, bool DiscordLinked, string? TeamName);
    public sealed record TeamOption(Guid Id, string Name);
    public sealed class InternalParticipantInput
    {
        [StringLength(100)] public string? OwnerUsername { get; set; }
        public Dictionary<Guid, ParticipantModel.AccountInput> AccountAnswers { get; set; } = [];
        public Dictionary<Guid, string> Answers { get; set; } = [];
    }
    public sealed record EvidenceCodeRow(Guid Id, string Code, DateTimeOffset ActivatesAt, DateTimeOffset? RetiresAt, string? Note);
}
