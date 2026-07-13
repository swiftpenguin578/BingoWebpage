namespace Bingo.Domain.Catalogue;

public sealed class BossActivity
{
    private BossActivity() { }
    public BossActivity(Guid id, string name, string slug, string category, decimal? efficientCompletionsPerHour, DateTimeOffset updatedAt)
    { Id = id; Name = name; Slug = slug; Category = category; EfficientCompletionsPerHour = efficientCompletionsPerHour; DataUpdatedAt = updatedAt.ToUniversalTime(); Active = true; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty; public string Slug { get; private set; } = string.Empty; public string Category { get; private set; } = string.Empty; public decimal? EfficientCompletionsPerHour { get; private set; }
    public string? ExternalIdentifier { get; private set; }
    public string? DataSource { get; private set; }
    public string? ImageUrl { get; private set; }
    public DateTimeOffset DataUpdatedAt { get; private set; }
    public bool Active { get; private set; }
    public string? Notes { get; private set; }
    public void Update(string name, string category, decimal? rate, string? externalId, string? source, string? notes, DateTimeOffset now, string? imageUrl = null) { Name = name; Category = category; EfficientCompletionsPerHour = rate; ExternalIdentifier = externalId; DataSource = source; Notes = notes; ImageUrl = imageUrl; DataUpdatedAt = now.ToUniversalTime(); }
    public void SetActive(bool active) => Active = active;
}
