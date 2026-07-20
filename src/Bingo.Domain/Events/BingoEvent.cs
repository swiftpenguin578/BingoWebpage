namespace Bingo.Domain.Events;

public sealed class BingoEvent
{
    private BingoEvent()
    {
    }

    public BingoEvent(
        Guid id,
        string name,
        string slug,
        string description,
        string timezone,
        DateTimeOffset signupOpensAt,
        DateTimeOffset signupClosesAt,
        DateTimeOffset eventStartsAt,
        DateTimeOffset eventEndsAt,
        DateTimeOffset submissionCutoffAt,
        int participantCap,
        Guid createdByAccountId,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(participantCap, 1);
        Id = id;
        Name = name;
        Slug = slug;
        Description = description;
        Timezone = timezone;
        SignupOpensAt = signupOpensAt.ToUniversalTime();
        SignupClosesAt = signupClosesAt.ToUniversalTime();
        EventStartsAt = eventStartsAt.ToUniversalTime();
        EventEndsAt = eventEndsAt.ToUniversalTime();
        SubmissionCutoffAt = submissionCutoffAt.ToUniversalTime();
        ParticipantCap = participantCap;
        CreatedByAccountId = createdByAccountId;
        CreatedAt = createdAt.ToUniversalTime();
        State = EventState.Draft;
        WaitingListEnabled = true;
        AllowPrivateSignupEditing = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Timezone { get; private set; } = string.Empty;
    public EventState State { get; private set; }
    public DateTimeOffset SignupOpensAt { get; private set; }
    public DateTimeOffset SignupClosesAt { get; private set; }
    public DateTimeOffset EventStartsAt { get; private set; }
    public DateTimeOffset EventEndsAt { get; private set; }
    public DateTimeOffset SubmissionCutoffAt { get; private set; }
    public DateTimeOffset? ReopenedSubmissionCutoffAt { get; private set; }
    public int ParticipantCap { get; private set; }
    public bool WaitingListEnabled { get; private set; }
    public bool AllowPrivateSignupEditing { get; private set; }
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
    public bool EvidenceCodeEnabled { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public Guid CreatedByAccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool AcceptsSignups(DateTimeOffset now) =>
        State == EventState.SignupOpen && now < SignupClosesAt;

    public bool AcceptsNewSubmissions(DateTimeOffset now)
    {
        var cutoff = ReopenedSubmissionCutoffAt is not null && ReopenedSubmissionCutoffAt > SubmissionCutoffAt
            ? ReopenedSubmissionCutoffAt.Value
            : SubmissionCutoffAt;
        return (State is EventState.Live or EventState.AwaitingFinalReview)
            && now >= EventStartsAt
            && now <= cutoff;
    }

    public void ConfigureSignup(bool waitingListEnabled, bool allowPrivateEditing, bool requireCode, string? codeHash)
    {
        WaitingListEnabled = waitingListEnabled;
        AllowPrivateSignupEditing = allowPrivateEditing;
        RequireSignupCode = requireCode;
        SignupCodeHash = requireCode ? codeHash : null;
    }

    public void ConfigurePlanning(
        string? publicRules,
        string? buyInDescription,
        string? prizeDescription,
        int? expectedTeamCount,
        int? expectedTeamSize,
        int? expectedBoardRows,
        int? expectedBoardColumns)
    {
        PublicRules = publicRules;
        BuyInDescription = buyInDescription;
        PrizeDescription = prizeDescription;
        ExpectedTeamCount = expectedTeamCount;
        ExpectedTeamSize = expectedTeamSize;
        ExpectedBoardRows = expectedBoardRows;
        ExpectedBoardColumns = expectedBoardColumns;
    }

    public void OpenSignups(DateTimeOffset? manuallyOpenedAt = null)
    {
        if (State is not (EventState.Draft or EventState.SignupClosed))
        {
            throw new InvalidOperationException("Signups can only be opened while the event is being prepared.");
        }

        if (manuallyOpenedAt is not null)
        {
            var openedAt = manuallyOpenedAt.Value.ToUniversalTime();
            SignupOpensAt = new DateTimeOffset(openedAt.Year, openedAt.Month, openedAt.Day, openedAt.Hour, openedAt.Minute, 0, TimeSpan.Zero);
            SignupClosesAt = RoundUpToHalfHour(SignupOpensAt.AddMonths(3));
        }

        State = EventState.SignupOpen;
    }

    public void CloseSignups()
    {
        if (State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
        {
            throw new InvalidOperationException("Signups cannot be changed after the event has started.");
        }

        State = EventState.SignupClosed;
    }

    public bool OpenSignupsIfScheduled(DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        if (State != EventState.Draft || now < SignupOpensAt || now >= SignupClosesAt) return false;
        OpenSignups();
        return true;
    }

    public bool CloseSignupsIfScheduled(DateTimeOffset now)
    {
        if (State != EventState.SignupOpen || now.ToUniversalTime() < SignupClosesAt) return false;
        CloseSignups();
        return true;
    }

    public void StartEvent(DateTimeOffset now)
    {
        if (State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
        {
            throw new InvalidOperationException("Only an event that has not started can be started.");
        }

        if (now < EventStartsAt) EventStartsAt = now.ToUniversalTime();
        State = EventState.Live;
    }

    public void EndEvent() { if (State != EventState.Live) throw new InvalidOperationException("Only a live event can be ended."); State = EventState.AwaitingFinalReview; }
    public void ReopenSubmissions(DateTimeOffset until, DateTimeOffset now) { if (State is EventState.Finalized or EventState.Archived) throw new InvalidOperationException("Finalized events cannot accept submissions."); if (State is not (EventState.Live or EventState.AwaitingFinalReview)) throw new InvalidOperationException("Only a started event can accept submissions."); if (until <= now) throw new InvalidOperationException("The new cutoff must be in the future."); ReopenedSubmissionCutoffAt = until.ToUniversalTime(); }
    public void SetEvidenceCodeEnabled(bool enabled) => EvidenceCodeEnabled = enabled;
    public void FinalizeResults(DateTimeOffset now) { if (State != EventState.AwaitingFinalReview) throw new InvalidOperationException("Only an event awaiting final review can be finalized."); State = EventState.Finalized; ResultsPublished = true; FinalizedAt = now.ToUniversalTime(); ArchivedAt = null; }
    public void Unfinalize() { if (State is not (EventState.Finalized or EventState.Archived)) throw new InvalidOperationException("Only a finalized or archived event can be reopened for corrections."); State = EventState.AwaitingFinalReview; ResultsPublished = false; ArchivedAt = null; }
    public void Archive(DateTimeOffset now) { if (State != EventState.Finalized) throw new InvalidOperationException("Finalize the event before archiving it."); State = EventState.Archived; ArchivedAt = now.ToUniversalTime(); }

    public void IncreaseParticipantCap(int newCap)
    {
        if (newCap < ParticipantCap)
        {
            throw new InvalidOperationException("The participant cap cannot be lowered.");
        }

        ParticipantCap = newCap;
    }

    public void ChangeSignupClosing(DateTimeOffset newClosing, DateTimeOffset now)
        => ChangeSignupWindow(SignupOpensAt, newClosing, now);

    public void ChangeSignupWindow(DateTimeOffset newOpening, DateTimeOffset newClosing, DateTimeOffset now)
    {
        if (State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
        {
            throw new InvalidOperationException("The signup window cannot change after the event has started.");
        }

        if (newClosing <= now) throw new InvalidOperationException("The signup closing time must be in the future.");
        if (newOpening < now && (State != EventState.SignupOpen || newOpening.ToUniversalTime() != SignupOpensAt))
        {
            throw new InvalidOperationException("The signup opening time cannot be changed to a time in the past.");
        }
        if (newOpening >= newClosing) throw new InvalidOperationException("Signups must open before they close.");
        var previousState = State;
        SignupOpensAt = newOpening.ToUniversalTime();
        SignupClosesAt = newClosing.ToUniversalTime();

        if (newOpening > now)
        {
            State = EventState.Draft;
        }
        else if (previousState != EventState.SignupClosed)
        {
            State = EventState.SignupOpen;
        }
    }

    public void SetDraftLocked(bool locked) => DraftLocked = locked;

    private static DateTimeOffset RoundUpToHalfHour(DateTimeOffset value)
    {
        value = value.ToUniversalTime();
        var startOfMinute = new DateTimeOffset(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, TimeSpan.Zero);
        if (value.Minute % 30 == 0 && value.Second == 0 && value.Millisecond == 0) return startOfMinute;
        return startOfMinute.AddMinutes(30 - value.Minute % 30);
    }
}
