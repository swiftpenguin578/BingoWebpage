namespace Bingo.Domain.Teams;

public sealed class DraftPick
{
    private DraftPick() { }
    public DraftPick(Guid id, Guid draftSessionId, Guid teamId, Guid participantId, int pickNumber, int roundNumber, DateTimeOffset pickedAt)
    { Id = id; DraftSessionId = draftSessionId; TeamId = teamId; EventParticipantId = participantId; PickNumber = pickNumber; RoundNumber = roundNumber; PickedAt = pickedAt.ToUniversalTime(); }
    public Guid Id { get; private set; }
    public Guid DraftSessionId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid EventParticipantId { get; private set; }
    public int PickNumber { get; private set; }
    public int RoundNumber { get; private set; }
    public DateTimeOffset PickedAt { get; private set; }
    public DateTimeOffset? UndoneAt { get; private set; }
    public void Undo(DateTimeOffset now) { if (UndoneAt is not null) throw new InvalidOperationException("This pick was already undone."); UndoneAt = now.ToUniversalTime(); }
}
