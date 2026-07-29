namespace Bingo.Domain.Access;

/// <summary>A recipient-specific, durable in-site notification.</summary>
public sealed class PersonalNotification
{
    private PersonalNotification() { }

    public PersonalNotification(Guid id, Guid recipientAccountId, string title, string detail, string route, DateTimeOffset createdAt)
    {
        Id = id;
        RecipientAccountId = recipientAccountId;
        Title = title;
        Detail = detail;
        Route = route;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid RecipientAccountId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
    public string Route { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now.ToUniversalTime();
}
