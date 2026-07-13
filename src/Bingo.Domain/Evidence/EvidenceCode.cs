namespace Bingo.Domain.Evidence;

public sealed class EvidenceCode
{
    private EvidenceCode() { }

    public EvidenceCode(Guid id, Guid eventId, string code, DateTimeOffset activatesAt, Guid createdByAccountId, DateTimeOffset createdAt, string? note)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Evidence code is required.", nameof(code));
        Id = id; EventId = eventId; Code = code.Trim().ToUpperInvariant(); ActivatesAt = activatesAt.ToUniversalTime();
        CreatedByAccountId = createdByAccountId; CreatedAt = createdAt.ToUniversalTime(); Note = Clean(note);
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateTimeOffset ActivatesAt { get; private set; }
    public DateTimeOffset? RetiresAt { get; private set; }
    public Guid CreatedByAccountId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? Note { get; private set; }

    public void Retire(DateTimeOffset at)
    {
        var value = at.ToUniversalTime();
        if (value < ActivatesAt) throw new InvalidOperationException("A code cannot retire before it activates.");
        RetiresAt = value;
    }
    public void SetRetiresAt(DateTimeOffset? at)
    {
        if (at is not null && at.Value.ToUniversalTime() < ActivatesAt) throw new InvalidOperationException("A code cannot retire before it activates.");
        RetiresAt = at?.ToUniversalTime();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
