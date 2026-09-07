namespace Bingo.Domain.Signups;

public sealed class SignupQuestion
{
    public const string DeletedReason = "deleted";
    private SignupQuestion() { }
    public SignupQuestion(Guid id, Guid signupFormId, Guid eventId, string key, string label, SignupQuestionType type, bool required, int position, string? options, SignupSystemField systemField = SignupSystemField.None, EventCharacterRole? accountAnswerRole = null, string? helpText = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A stable question key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A question label is required.", nameof(label));
        if (type == SignupQuestionType.Account != (accountAnswerRole is not null)) throw new ArgumentException("Only Account questions have an account role.");
        if (type == SignupQuestionType.SingleChoice != !string.IsNullOrWhiteSpace(options)) throw new ArgumentException("Only single-choice questions have options.");
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        Id = id; SignupFormId = signupFormId; EventId = eventId; Key = key.Trim(); Label = label.Trim(); HelpText = helpText?.Trim(); Type = type; Required = required; Position = position; Options = options?.Trim(); SystemField = systemField; AccountAnswerRole = accountAnswerRole; Active = true;
    }
    // Temporary source compatibility for retained Slice 3 callers. New questions must provide their form id.
    public SignupQuestion(Guid id, Guid eventId, string key, string label, SignupQuestionType type, bool required, int position, string? options)
        : this(id, Guid.Empty, eventId, key, label, type, required, position, options) { }
    public Guid Id { get; private set; }
    public Guid SignupFormId { get; private set; }
    public Guid EventId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string? HelpText { get; private set; }
    public SignupQuestionType Type { get; private set; }
    public bool Required { get; private set; }
    public int Position { get; private set; }
    public string? Options { get; private set; }
    public SignupSystemField SystemField { get; private set; }
    public EventCharacterRole? AccountAnswerRole { get; private set; }
    public bool PublicOnSignupBoard { get; private set; } = true;
    public bool Active { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public Guid? DisabledByAccountId { get; private set; }
    public string? DisabledReason { get; private set; }
    public Guid? ReplacedBySignupQuestionId { get; private set; }
    public int Version { get; private set; }

    public void Deactivate(Guid? actorId = null, DateTimeOffset? now = null, string? reason = null)
    {
        if (SystemField is SignupSystemField.PrimaryRegularAccount or SignupSystemField.CaptainVolunteer) throw new InvalidOperationException("Required system questions cannot be disabled.");
        Active = false; DisabledAt = (now ?? DateTimeOffset.UtcNow).ToUniversalTime(); DisabledByAccountId = actorId; DisabledReason = reason?.Trim(); Version++;
    }
    public void Delete(Guid actorId, DateTimeOffset now)
    {
        if (SystemField != SignupSystemField.None) throw new InvalidOperationException("Standard questions cannot be removed.");
        if (!Active) throw new InvalidOperationException("That question cannot be removed.");
        Deactivate(actorId, now, DeletedReason);
    }
    public void UpdateDefinition(string label, string? helpText, SignupQuestionType type, bool required, string? options, EventCharacterRole? accountAnswerRole)
    {
        if (SystemField != SignupSystemField.None) throw new InvalidOperationException("System questions cannot be structurally changed.");
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A question label is required.", nameof(label));
        if (type == SignupQuestionType.Account != (accountAnswerRole is not null)) throw new ArgumentException("Only Account questions have an account role.");
        if (type == SignupQuestionType.SingleChoice != !string.IsNullOrWhiteSpace(options)) throw new ArgumentException("Only single-choice questions have options.");
        Label = label.Trim(); HelpText = helpText?.Trim(); Type = type; Required = required; Options = options?.Trim(); AccountAnswerRole = accountAnswerRole; Version++;
    }
    public void UpdatePresentation(string label, string? helpText)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A question label is required.", nameof(label));
        Label = label.Trim(); HelpText = helpText?.Trim(); Version++;
    }
    public void MoveTo(int position) { ArgumentOutOfRangeException.ThrowIfNegative(position); Position = position; Version++; }
    public void ReplaceWith(Guid replacementId) { if (Active) throw new InvalidOperationException("Disable a question before replacing it."); ReplacedBySignupQuestionId = replacementId; Version++; }
    public void AdvanceVersion() => Version++;
}
