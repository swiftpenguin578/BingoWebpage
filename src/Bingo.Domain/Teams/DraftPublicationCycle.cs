namespace Bingo.Domain.Teams;

public sealed class DraftPublicationCycle
{
    private DraftPublicationCycle() { }
    public DraftPublicationCycle(Guid id, Guid draftSessionId, int cycleNumber, DateTimeOffset publishedAt, Guid publishedByAccountId)
    { Id = id; DraftSessionId = draftSessionId; CycleNumber = cycleNumber; PublishedAt = publishedAt.ToUniversalTime(); PublishedByAccountId = publishedByAccountId; }
    public Guid Id { get; private set; }
    public Guid DraftSessionId { get; private set; }
    public int CycleNumber { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public Guid PublishedByAccountId { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public Guid? SupersededByAccountId { get; private set; }
    public string? ReopenReason { get; private set; }
    public void Supersede(DateTimeOffset now, Guid actorId, string reason) { SupersededAt = now.ToUniversalTime(); SupersededByAccountId = actorId; ReopenReason = reason; }
}
