namespace Bingo.Domain.Signups;

/// <summary>Immutable committed identity of one event form-field addition.</summary>
public sealed class SignupQuestionCreationOperation
{
    private SignupQuestionCreationOperation() { }
    public SignupQuestionCreationOperation(Guid requestId, Guid actorAccountId, Guid eventId, string inputFingerprint, Guid questionId)
    {
        RequestId = requestId; ActorAccountId = actorAccountId; EventId = eventId;
        InputFingerprint = inputFingerprint; QuestionId = questionId;
    }
    public Guid RequestId { get; private set; }
    public Guid ActorAccountId { get; private set; }
    public Guid EventId { get; private set; }
    public string InputFingerprint { get; private set; } = string.Empty;
    public Guid QuestionId { get; private set; }
}
