namespace Bingo.Domain.Events;

public sealed class BingoEvent
{
    private BingoEvent() { }

    /// <summary>Creates the smallest permitted private event draft.</summary>
    public BingoEvent(Guid id, string name, string slug, string timezone, Guid createdByAccountId, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An event name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("An event slug is required.", nameof(slug));
        if (string.IsNullOrWhiteSpace(timezone)) throw new ArgumentException("A timezone is required.", nameof(timezone));
        Id = id;
        Name = name.Trim();
        Slug = slug.Trim();
        Timezone = timezone.Trim();
        CreatedByAccountId = createdByAccountId;
        CreatedAt = createdAt.ToUniversalTime();
        State = EventState.Draft;
        WaitingListEnabled = true;
    }

    public static BingoEvent CreateArchivedHistorical(
        Guid id, string name, string slug, string? description, string timezone,
        DateTimeOffset? signupOpensAt, DateTimeOffset? signupClosesAt,
        DateTimeOffset eventStartsAt, DateTimeOffset eventEndsAt, Guid createdByAccountId,
        DateTimeOffset createdAt, string? publicRules, int teamCount, int teamSize,
        int boardRows, int boardColumns)
    {
        var item = new BingoEvent(id, name, slug, description, timezone, signupOpensAt, signupClosesAt,
            eventStartsAt, eventEndsAt, eventEndsAt, teamCount * teamSize, createdByAccountId, createdAt);
        item.ConfigureSignup(false, false, null);
        item.ConfigurePlanning(publicRules, null, null, teamCount, teamSize, boardRows, boardColumns);
        item.State = EventState.Archived;
        item.FirstPublicAt = eventStartsAt;
        item.DraftAt = eventStartsAt;
        item.ActualStartedAt = eventStartsAt;
        item.ActualEndedAt = eventEndsAt;
        item.SubmissionsClosedAt = eventEndsAt;
        item.ParticipantListPublished = true;
        item.DraftResultsPublished = true;
        item.TeamRostersPublished = true;
        item.BoardPublished = true;
        item.ResultsPublished = true;
        item.DraftLocked = true;
        item.FinalizedAt = eventEndsAt;
        item.ArchivedAt = eventEndsAt;
        return item;
    }

    // Compatibility constructor retained until the Slice 3 creation surface moves to the minimal draft command.
    public BingoEvent(Guid id, string name, string slug, string? description, string timezone,
        DateTimeOffset? signupOpensAt, DateTimeOffset? signupClosesAt, DateTimeOffset? eventStartsAt,
        DateTimeOffset? eventEndsAt, DateTimeOffset? submissionCutoffAt, int? participantCap,
        Guid createdByAccountId, DateTimeOffset createdAt)
        : this(id, name, slug, timezone, createdByAccountId, createdAt)
    {
        Description = Clean(description);
        SignupOpensAt = Utc(signupOpensAt);
        SignupClosesAt = Utc(signupClosesAt);
        EventStartsAt = Utc(eventStartsAt);
        EventEndsAt = Utc(eventEndsAt);
        SetNormalSubmissionCutoff(eventEndsAt);
        if (participantCap is < 1) throw new ArgumentOutOfRangeException(nameof(participantCap));
        ParticipantCap = participantCap;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Timezone { get; private set; } = string.Empty;
    public EventState State { get; private set; }
    public DateTimeOffset? HiddenAt { get; private set; }
    public Guid? HiddenByAccountId { get; private set; }
    public string? HiddenReason { get; private set; }
    public bool IsHidden => HiddenAt is not null;
    public Guid? BannerAssetId { get; private set; }
    public DateTimeOffset? FirstPublicAt { get; private set; }
    public DateTimeOffset? SignupOpensAt { get; private set; }
    public DateTimeOffset? SignupClosesAt { get; private set; }
    public DateTimeOffset? DraftAt { get; private set; }
    public DateTimeOffset? EventStartsAt { get; private set; }
    public DateTimeOffset? EventEndsAt { get; private set; }
    public DateTimeOffset? SubmissionCutoffAt { get; private set; }
    public DateTimeOffset? ActualSignupOpenedAt { get; private set; }
    public DateTimeOffset? ActualSignupClosedAt { get; private set; }
    public DateTimeOffset? ActualStartedAt { get; private set; }
    public DateTimeOffset? ActualEndedAt { get; private set; }
    public DateTimeOffset? SubmissionsClosedAt { get; private set; }
    public bool ScheduledSignupOpeningEnabled { get; private set; }
    public string ScheduledSignupWarningCodes { get; private set; } = string.Empty;
    public DateTimeOffset? ReopenedSubmissionCutoffAt { get; private set; }
    public int? ParticipantCap { get; private set; }
    public bool WaitingListEnabled { get; private set; }
    public bool RequireSignupCode { get; private set; }
    public string? SignupCodeHash { get; private set; }
    public string? PublicRules { get; private set; }
    public string? BuyInDescription { get; private set; }
    public string? PrizeDescription { get; private set; }
    public int? ExpectedTeamCount { get; private set; }
    public int? ExpectedTeamSize { get; private set; }
    public int? ExpectedBoardRows { get; private set; }
    public int? ExpectedBoardColumns { get; private set; }
    public bool ParticipantListPublished { get; private set; }
    public bool DraftResultsPublished { get; private set; }
    public bool TeamRostersPublished { get; private set; }
    public bool BoardPublished { get; private set; }
    public bool ResultsPublished { get; private set; }
    public bool DraftLocked { get; private set; }
    /// <summary>Only the Development scenario seeder may mark fixtures; production commands never honor it.</summary>
    public bool IsDevelopmentFixture { get; private set; }
    public bool EvidenceCodeEnabled { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledByAccountId { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset? DiscardedAt { get; private set; }
    public Guid? DiscardedByAccountId { get; private set; }
    public long Version { get; private set; } = 1;
    public Guid CreatedByAccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool AcceptsSignups(DateTimeOffset now) =>
        !IsHidden && State == EventState.SignupOpen && SignupClosesAt is { } closing && now.ToUniversalTime() < closing;

    public bool AcceptsNewSubmissions(DateTimeOffset now) =>
        !IsHidden && State is (EventState.Live or EventState.AwaitingFinalReview)
        && ActualStartedAt is not null
        && now.ToUniversalTime() >= ActualStartedAt
        && now.ToUniversalTime() <= ActiveSubmissionCutoff();

    /// <summary>Emergency credentials stop exactly at the cutoff; ordinary evidence is inclusive.</summary>
    public bool AcceptsEmergencySubmissions(DateTimeOffset now) =>
        !IsHidden && State is (EventState.Live or EventState.AwaitingFinalReview)
        && ActualStartedAt is not null
        && now.ToUniversalTime() >= ActualStartedAt
        && now.ToUniversalTime() < ActiveSubmissionCutoff();

    public void AdvanceVersion() => Version++;

    public void Hide(Guid actorId, DateTimeOffset hiddenAt, string confirmation, string reason)
    {
        if (IsHidden) throw new InvalidOperationException("The event is already hidden.");
        if (State is not (EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived))
            throw new InvalidOperationException("Only events after Live play can be hidden.");
        RequireNameConfirmation(confirmation);
        var validatedReason = RequiredReason(reason, nameof(reason));
        HiddenAt = hiddenAt.ToUniversalTime();
        HiddenByAccountId = actorId;
        HiddenReason = validatedReason;
    }

    public void Restore(string confirmation, string reason)
    {
        if (!IsHidden) throw new InvalidOperationException("The event is not hidden.");
        RequireNameConfirmation(confirmation);
        _ = RequiredReason(reason, nameof(reason));
        HiddenAt = null;
        HiddenByAccountId = null;
        HiddenReason = null;
    }

    public void ConfigureSignup(bool waitingListEnabled, bool requireCode, string? codeHash)
    {
        EnsureCapability(EventCapability.ConfigureSignup);
        if (DraftLocked) throw new InvalidOperationException("Signup settings are locked because the draft has started.");
        WaitingListEnabled = waitingListEnabled;
        RequireSignupCode = requireCode;
        SignupCodeHash = requireCode ? codeHash : null;
    }

    public void ConfigurePlanning(string? publicRules, string? buyInDescription, string? prizeDescription,
        int? expectedTeamCount, int? expectedTeamSize, int? expectedBoardRows, int? expectedBoardColumns)
    {
        EnsureCapability(EventCapability.ConfigureIdentityOrSchedule);
        PublicRules = Clean(publicRules);
        BuyInDescription = Clean(buyInDescription);
        PrizeDescription = Clean(prizeDescription);
        ExpectedTeamCount = expectedTeamCount;
        ExpectedTeamSize = expectedTeamSize;
        ValidateBoardDimension(expectedBoardRows, nameof(expectedBoardRows));
        ValidateBoardDimension(expectedBoardColumns, nameof(expectedBoardColumns));
        ExpectedBoardRows = expectedBoardRows;
        ExpectedBoardColumns = expectedBoardColumns;
    }

    public void SetBannerAsset(Guid? bannerAssetId) { EnsureIdentityEditable(); BannerAssetId = bannerAssetId; }
    public void SetDraftAt(DateTimeOffset? draftAt) { EnsureCapability(EventCapability.ConfigureIdentityOrSchedule); DraftAt = Utc(draftAt); }
    public void MarkFirstPublic(DateTimeOffset exposedAt) { EnsureNotHidden(); if (FirstPublicAt is null) FirstPublicAt = exposedAt.ToUniversalTime(); }

    public void ConfigureScheduledSignupOpening(bool enabled, IEnumerable<string> acknowledgedWarningCodes)
    {
        EnsureNotHidden();
        if (State != EventState.Draft) throw new InvalidOperationException("A scheduled signup opening can only be configured for a private draft.");
        ScheduledSignupOpeningEnabled = enabled;
        ScheduledSignupWarningCodes = enabled
            ? string.Join(',', acknowledgedWarningCodes.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal))
            : string.Empty;
    }

    public void UpdateIdentity(string name, string? slug, string? description, string timezone)
    {
        EnsureNotHidden();
        if (State == EventState.Live)
        {
            if (!string.Equals(Name, name.Trim(), StringComparison.Ordinal) ||
                !string.Equals(Slug, slug?.Trim(), StringComparison.Ordinal) ||
                !string.Equals(Description, Clean(description), StringComparison.Ordinal))
                throw new InvalidOperationException("Only the display timezone can change while the event is Live.");
        }
        else EnsureIdentityEditable();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An event name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(timezone)) throw new ArgumentException("A timezone is required.", nameof(timezone));
        if (FirstPublicAt is not null && !string.IsNullOrWhiteSpace(slug) && !string.Equals(Slug, slug.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("The public event link is locked after first publication.");
        Name = name.Trim();
        if (FirstPublicAt is null && !string.IsNullOrWhiteSpace(slug)) Slug = slug.Trim();
        Description = Clean(description);
        Timezone = timezone.Trim();
    }

    public void ConfigureInitialSchedule(DateTimeOffset? signupOpensAt, DateTimeOffset? signupClosesAt, DateTimeOffset? draftAt,
        DateTimeOffset? eventStartsAt, DateTimeOffset? eventEndsAt, int? participantCap)
        => ConfigureSchedule(signupOpensAt, signupClosesAt, draftAt, eventStartsAt, eventEndsAt, participantCap);

    public void ConfigureSchedule(DateTimeOffset? signupOpensAt, DateTimeOffset? signupClosesAt, DateTimeOffset? draftAt,
        DateTimeOffset? eventStartsAt, DateTimeOffset? eventEndsAt, int? participantCap)
    {
        EnsureCapability(EventCapability.ConfigureIdentityOrSchedule);
        if (DraftLocked) throw new InvalidOperationException("The schedule is locked because the draft has started.");
        if (participantCap is < 1) throw new ArgumentOutOfRangeException(nameof(participantCap));
        if (FirstPublicAt is not null && ParticipantCap is { } current && (participantCap is null || participantCap < current))
            throw new InvalidOperationException("The participant cap cannot be lowered after signup has first been public.");
        if (eventStartsAt is { } starts && eventEndsAt is { } ends && ends <= starts)
            throw new InvalidOperationException("Event end must be after event start.");
        if (signupClosesAt is { } closing && eventStartsAt is { } eventStart && closing > eventStart)
            throw new InvalidOperationException("Signup closing must be no later than event start.");
        if ((ActualSignupOpenedAt ?? signupOpensAt) is { } opening && signupClosesAt is { } closes && closes <= opening)
            throw new InvalidOperationException("Signup closing must be after signup opening.");
        SignupOpensAt = Utc(signupOpensAt);
        SignupClosesAt = Utc(signupClosesAt);
        DraftAt = Utc(draftAt);
        EventStartsAt = Utc(eventStartsAt);
        EventEndsAt = Utc(eventEndsAt);
        SetNormalSubmissionCutoff(eventEndsAt);
        ParticipantCap = participantCap;
    }

    public void OpenSignups() => OpenSignups(null);

    public void OpenSignups(DateTimeOffset? actualOpenedAt)
    {
        EnsureNotHidden();
        if (!EventStatePolicy.CanTransition(State, EventState.SignupOpen)) throw TransitionException(EventState.SignupOpen);
        if (State == EventState.SignupClosed && DraftLocked) throw new InvalidOperationException("Signups cannot reopen after the draft is locked.");
        if (actualOpenedAt is { } opened) ActualSignupOpenedAt ??= opened.ToUniversalTime();
        State = EventState.SignupOpen;
        ScheduledSignupOpeningEnabled = false;
    }

    public void CloseSignups() => CloseSignups(null);

    public void CloseSignups(DateTimeOffset? actualClosedAt)
    {
        EnsureNotHidden();
        if (!EventStatePolicy.CanTransition(State, EventState.SignupClosed)) throw TransitionException(EventState.SignupClosed);
        if (actualClosedAt is { } closed) ActualSignupClosedAt ??= closed.ToUniversalTime();
        State = EventState.SignupClosed;
    }

    public bool OpenSignupsIfScheduled(DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        if (State != EventState.Draft || SignupOpensAt is not { } opening || SignupClosesAt is not { } closing || now < opening || now >= closing) return false;
        OpenSignups(now);
        return true;
    }

    public bool CloseSignupsIfScheduled(DateTimeOffset now)
    {
        if (State != EventState.SignupOpen || SignupClosesAt is not { } closing || now.ToUniversalTime() < closing) return false;
        CloseSignups(now);
        return true;
    }

    public void StartEvent(DateTimeOffset now)
    {
        EnsureCapability(EventCapability.StartEvent);
        ActualStartedAt = now.ToUniversalTime();
        State = EventState.Live;
    }

    public void EndEvent() => EndEvent(EventEndsAt ?? throw new InvalidOperationException("An event end time is required."));
    public void EndEvent(DateTimeOffset effectiveEndedAt)
    {
        EnsureNotHidden();
        if (!EventStatePolicy.CanTransition(State, EventState.AwaitingFinalReview)) throw TransitionException(EventState.AwaitingFinalReview);
        ActualEndedAt = effectiveEndedAt.ToUniversalTime();
        State = EventState.AwaitingFinalReview;
    }

    public void ResumePrematureEnd(DateTimeOffset replacementEventEndsAt, DateTimeOffset resumedAt)
    {
        EnsureCapability(EventCapability.ResumeEvent);
        replacementEventEndsAt = replacementEventEndsAt.ToUniversalTime();
        resumedAt = resumedAt.ToUniversalTime();
        if (replacementEventEndsAt <= resumedAt) throw new InvalidOperationException("The replacement event end must be in the future.");
        EventEndsAt = replacementEventEndsAt;
        SetNormalSubmissionCutoff(replacementEventEndsAt);
        ReopenedSubmissionCutoffAt = null;
        ActualEndedAt = null;
        SubmissionsClosedAt = null;
        State = EventState.Live;
    }

    public bool CloseSubmissionsIfDue(DateTimeOffset now)
    {
        EnsureNotHidden();
        var cutoff = ActiveSubmissionCutoff();
        if (SubmissionsClosedAt is not null || cutoff == DateTimeOffset.MinValue || now.ToUniversalTime() < cutoff) return false;
        SubmissionsClosedAt = cutoff;
        return true;
    }

    public void ReopenSubmissions(DateTimeOffset until, DateTimeOffset now)
    {
        EnsureCapability(EventCapability.ReviewEvidence);
        if (until <= now) throw new InvalidOperationException("The new cutoff must be in the future.");
        ReopenedSubmissionCutoffAt = until.ToUniversalTime();
        SubmissionsClosedAt = null;
    }

    public void SetEvidenceCodeEnabled(bool enabled)
    {
        EnsureCapability(EventCapability.ConfigureEvidenceCodes);
        EvidenceCodeEnabled = enabled;
    }

    public void FinalizeResults(DateTimeOffset now)
    {
        EnsureCapability(EventCapability.Finalize);
        State = EventState.Finalized;
        ResultsPublished = true;
        FinalizedAt = now.ToUniversalTime();
        ArchivedAt = null;
    }

    public void Unfinalize(string reason)
    {
        EnsureCapability(EventCapability.Unfinalize);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An unfinalization reason is required.", nameof(reason));
        State = EventState.AwaitingFinalReview;
        ResultsPublished = false;
        ArchivedAt = null;
        ReopenedSubmissionCutoffAt = null;
        SubmissionsClosedAt ??= SubmissionCutoffAt;
    }

    public void Archive(DateTimeOffset now)
    {
        EnsureCapability(EventCapability.Archive);
        State = EventState.Archived;
        ArchivedAt = now.ToUniversalTime();
    }

    public void Cancel(Guid actorId, DateTimeOffset cancelledAt, string reason, bool protectedHistoryExists)
    {
        EnsureCapability(EventCapability.CancelOrDiscard);
        if (!protectedHistoryExists) throw new InvalidOperationException("Discard an empty event instead of cancelling it.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A cancellation reason is required.", nameof(reason));
        State = EventState.Cancelled;
        CancelledByAccountId = actorId;
        CancelledAt = cancelledAt.ToUniversalTime();
        CancellationReason = reason.Trim();
        ScheduledSignupOpeningEnabled = false;
    }

    public void Discard(Guid actorId, DateTimeOffset discardedAt, bool protectedHistoryExists)
    {
        EnsureCapability(EventCapability.CancelOrDiscard);
        if (protectedHistoryExists) throw new InvalidOperationException("An event with protected history cannot be discarded.");
        State = EventState.Discarded;
        DiscardedByAccountId = actorId;
        DiscardedAt = discardedAt.ToUniversalTime();
        BannerAssetId = null;
        Description = null;
        SignupOpensAt = null;
        SignupClosesAt = null;
        DraftAt = null;
        EventStartsAt = null;
        EventEndsAt = null;
        SubmissionCutoffAt = null;
        ScheduledSignupOpeningEnabled = false;
        ScheduledSignupWarningCodes = string.Empty;
        ReopenedSubmissionCutoffAt = null;
        ParticipantCap = null;
        SignupCodeHash = null;
        RequireSignupCode = false;
        PublicRules = null;
        BuyInDescription = null;
        PrizeDescription = null;
        ExpectedTeamCount = null;
        ExpectedTeamSize = null;
        ExpectedBoardRows = null;
        ExpectedBoardColumns = null;
        ParticipantListPublished = false;
        DraftResultsPublished = false;
        TeamRostersPublished = false;
        BoardPublished = false;
        ResultsPublished = false;
        DraftLocked = false;
        EvidenceCodeEnabled = false;
    }

    public void IncreaseParticipantCap(int newCap)
    {
        EnsureNotHidden();
        if (State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed) || DraftLocked) throw new InvalidOperationException("The participant cap cannot change in this event state.");
        if (FirstPublicAt is not null && ParticipantCap is { } current && newCap < current) throw new InvalidOperationException("The participant cap cannot be lowered after signup has first been public.");
        ArgumentOutOfRangeException.ThrowIfLessThan(newCap, 1);
        ParticipantCap = newCap;
    }

    internal void MarkAsDevelopmentFixture() => IsDevelopmentFixture = true;

    public void ChangeSignupClosing(DateTimeOffset newClosing, DateTimeOffset now)
        => ChangeSignupWindow(SignupOpensAt ?? throw new InvalidOperationException("A signup opening time is required."), newClosing, now);

    public void ChangeSignupWindow(DateTimeOffset newOpening, DateTimeOffset newClosing, DateTimeOffset now)
    {
        EnsureCapability(EventCapability.ConfigureIdentityOrSchedule);
        now = now.ToUniversalTime();
        if (newClosing <= now) throw new InvalidOperationException("The signup closing time must be in the future.");
        if (newOpening < now) throw new InvalidOperationException("The signup opening time cannot be changed to a time in the past.");
        if (newOpening >= newClosing) throw new InvalidOperationException("Signups must open before they close.");
        SignupOpensAt = newOpening.ToUniversalTime();
        SignupClosesAt = newClosing.ToUniversalTime();
    }

    public void SetDraftLocked(bool locked)
    {
        EnsureNotHidden();
        if (EventStatePolicy.IsTerminal(State)) throw new InvalidOperationException("The draft lock is unavailable in this event state.");
        if (!locked && State is not (EventState.SignupOpen or EventState.SignupClosed)) throw new InvalidOperationException("A locked draft cannot reopen after live play begins.");
        DraftLocked = locked;
    }

    /// <summary>Draft roster publication is independent from board publication and may be withdrawn only before live play.</summary>
    public void SetDraftRosterPublication(bool published)
    {
        EnsureNotHidden();
        if (State is not (EventState.SignupOpen or EventState.SignupClosed))
            throw new InvalidOperationException("Draft roster publication can only change before live play.");
        TeamRostersPublished = published;
        DraftResultsPublished = published;
    }

    public void SetBoardPublication(bool published, DateTimeOffset now)
    {
        EnsureNotHidden();
        if (State is not (EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized))
            throw new InvalidOperationException("Board publication is unavailable in this event state.");
        BoardPublished = published;
        if (published) MarkFirstPublic(now);
    }

    private DateTimeOffset ActiveSubmissionCutoff()
    {
        var cutoff = ReopenedSubmissionCutoffAt is { } reopened && (SubmissionCutoffAt is null || reopened > SubmissionCutoffAt.Value) ? reopened : SubmissionCutoffAt;
        return cutoff ?? DateTimeOffset.MinValue;
    }

    private void EnsureCapability(EventCapability capability)
    {
        EnsureNotHidden();
        if (!EventStatePolicy.Allows(State, capability)) throw new InvalidOperationException($"{capability} is unavailable while the event is {State}.");
    }

    private void EnsureIdentityEditable()
    {
        EnsureNotHidden();
        if (State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
            throw new InvalidOperationException("Event identity cannot change in this event state.");
    }

    private void EnsureNotHidden()
    {
        if (IsHidden) throw new InvalidOperationException("The event is hidden and must be restored before it can be changed.");
    }

    private void RequireNameConfirmation(string confirmation)
    {
        if (!string.Equals(confirmation, Name, StringComparison.Ordinal))
            throw new InvalidOperationException("Enter the exact event name to confirm this action.");
    }

    private static string RequiredReason(string reason, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", parameterName);
        var trimmed = reason.Trim();
        if (trimmed.Length > 2_000) throw new ArgumentException("A reason cannot exceed 2000 characters.", parameterName);
        return trimmed;
    }

    private InvalidOperationException TransitionException(EventState target) => new($"The event cannot transition from {State} to {target}.");
    private static DateTimeOffset? Utc(DateTimeOffset? value) => value?.ToUniversalTime();
    private void SetNormalSubmissionCutoff(DateTimeOffset? eventEndsAt) => SubmissionCutoffAt = Utc(eventEndsAt)?.AddMinutes(30);
    private static void ValidateBoardDimension(int? value, string paramName)
    {
        if (value is < 1 or > 8) throw new ArgumentOutOfRangeException(paramName, "Board dimensions must be between 1 and 8.");
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset RoundToMinute(DateTimeOffset value) => new(value.UtcDateTime.Year, value.UtcDateTime.Month, value.UtcDateTime.Day, value.UtcDateTime.Hour, value.UtcDateTime.Minute, 0, TimeSpan.Zero);
    private static DateTimeOffset RoundUpToHalfHour(DateTimeOffset value)
    {
        value = value.ToUniversalTime();
        var minute = new DateTimeOffset(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, TimeSpan.Zero);
        return value.Minute % 30 == 0 && value.Second == 0 && value.Millisecond == 0 ? minute : minute.AddMinutes(30 - value.Minute % 30);
    }
}
