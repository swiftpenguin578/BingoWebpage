namespace Bingo.Domain.Catalogue;

public sealed class SourceDropRateVariant
{
    private SourceDropRateVariant() { }

    public SourceDropRateVariant(
        Guid id,
        Guid sourceDropId,
        int position,
        string label,
        string displayRate,
        decimal? numericProbability,
        string? condition)
    {
        Id = id;
        SourceDropId = sourceDropId;
        Update(position, label, displayRate, numericProbability, condition);
    }

    public Guid Id { get; private set; }
    public Guid SourceDropId { get; private set; }
    public int Position { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string DisplayRate { get; private set; } = string.Empty;
    public decimal? NumericProbability { get; private set; }
    public string? Condition { get; private set; }

    public void Update(
        int position,
        string label,
        string displayRate,
        decimal? numericProbability,
        string? condition)
    {
        Position = position;
        Label = label;
        DisplayRate = displayRate;
        NumericProbability = numericProbability;
        Condition = condition;
    }
}
