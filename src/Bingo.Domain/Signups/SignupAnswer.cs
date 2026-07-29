namespace Bingo.Domain.Signups;

public sealed class SignupAnswer
{
    private SignupAnswer() { }
    public SignupAnswer(Guid id, Guid participantId, Guid questionId, string labelSnapshot, string value, Guid? osrsCharacterId = null)
    {
        if (string.IsNullOrWhiteSpace(labelSnapshot)) throw new ArgumentException("A question label snapshot is required.", nameof(labelSnapshot));
        Id = id; EventParticipantId = participantId; SignupQuestionId = questionId; QuestionLabelSnapshot = labelSnapshot.Trim(); Value = value.Trim(); OsrsCharacterId = osrsCharacterId;
    }
    public Guid Id { get; private set; }
    public Guid EventParticipantId { get; private set; }
    public Guid SignupQuestionId { get; private set; }
    public string QuestionLabelSnapshot { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public Guid? OsrsCharacterId { get; private set; }

    public void Update(string value) => Value = value;
    public void SetAccountCharacter(Guid characterId) { if (characterId == Guid.Empty) throw new ArgumentException("A character is required.", nameof(characterId)); OsrsCharacterId = characterId; Value = string.Empty; }
}
