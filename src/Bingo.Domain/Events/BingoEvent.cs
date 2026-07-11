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
    public Guid CreatedByAccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool AcceptsSignups(DateTimeOffset now) =>
        State == EventState.SignupOpen && now >= SignupOpensAt && now < SignupClosesAt;

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
        State = EventState.SignupOpen;
        if (manuallyOpenedAt is not null && manuallyOpenedAt < SignupOpensAt)
        {
            SignupOpensAt = manuallyOpenedAt.Value.ToUniversalTime();
        }
    }

    public void CloseSignups() => State = EventState.SignupClosed;

    public void IncreaseParticipantCap(int newCap)
    {
        if (newCap < ParticipantCap)
        {
            throw new InvalidOperationException("The participant cap cannot be lowered.");
        }

        ParticipantCap = newCap;
    }

    public void ExtendSignupClosing(DateTimeOffset newClosing)
    {
        if (newClosing < SignupClosesAt) throw new InvalidOperationException("The signup closing time cannot be shortened.");
        SignupClosesAt = newClosing.ToUniversalTime();
    }

    public void SetDraftLocked(bool locked) => DraftLocked = locked;
}
