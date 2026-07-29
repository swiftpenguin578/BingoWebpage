namespace Bingo.Domain.Events;

/// <summary>Durable, event-owned managed-banner deletion work created only while discarding an event.</summary>
public sealed class EventBannerCleanup
{
    private EventBannerCleanup() { }

    public EventBannerCleanup(Guid id, Guid eventId, string storageKey, DateTimeOffset queuedAt)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("A storage key is required.", nameof(storageKey));
        Id = id;
        EventId = eventId;
        StorageKey = storageKey;
        QueuedAt = queuedAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; private set; }
    public DateTimeOffset? LastAttemptedAt { get; private set; }
    public string? LastFailure { get; private set; }
    public int AttemptCount { get; private set; }

    public void RecordFailure(DateTimeOffset attemptedAt, string message)
    {
        LastAttemptedAt = attemptedAt.ToUniversalTime();
        AttemptCount++;
        LastFailure = message.Length <= 1_000 ? message : message[..1_000];
    }
}
