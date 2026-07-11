namespace Bingo.Domain.Signups;

public sealed class SignupAnswer
{
    private SignupAnswer() { }
    public SignupAnswer(Guid id, Guid participantId, Guid questionId, string labelSnapshot, string value)
    {
        Id = id; EventParticipantId = participantId; SignupQuestionId = questionId; QuestionLabelSnapshot = labelSnapshot; Value = value;
    }
    public Guid Id { get; private set; }
    public Guid EventParticipantId { get; private set; }
    public Guid SignupQuestionId { get; private set; }
    public string QuestionLabelSnapshot { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    public void Update(string value) => Value = value;
}
