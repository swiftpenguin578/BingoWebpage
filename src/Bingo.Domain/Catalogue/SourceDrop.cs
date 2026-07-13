namespace Bingo.Domain.Catalogue;

public sealed class SourceDrop
{
    private SourceDrop() { }
    public SourceDrop(Guid id, Guid bossActivityId, Guid itemId, string displayRate, decimal? numericProbability, decimal? defaultEhbEstimate, DateTimeOffset updatedAt) { Id = id; BossActivityId = bossActivityId; ItemId = itemId; DisplayRate = displayRate; NumericProbability = numericProbability; DefaultEhbEstimate = defaultEhbEstimate; DataUpdatedAt = updatedAt.ToUniversalTime(); Active = true; }
    public Guid Id { get; private set; }
    public Guid BossActivityId { get; private set; }
    public Guid ItemId { get; private set; }
    public string DisplayRate { get; private set; } = string.Empty; public decimal? NumericProbability { get; private set; }
    public string? RateConditionNote { get; private set; }
    public decimal? DefaultEhbEstimate { get; private set; }
    public string? DataSource { get; private set; }
    public DateTimeOffset DataUpdatedAt { get; private set; }
    public bool Active { get; private set; }
    public void Update(string displayRate, decimal? probability, string? condition, decimal? ehb, string? source, DateTimeOffset now) { DisplayRate = displayRate; NumericProbability = probability; RateConditionNote = condition; DefaultEhbEstimate = ehb; DataSource = source; DataUpdatedAt = now.ToUniversalTime(); }
    public void SetActive(bool active) => Active = active;
}
