namespace Bingo.Application.Stats;

public interface ILuckCheckpointConversionService
{
    Task<LuckCheckpointConversionReport> RunAsync(CancellationToken cancellationToken = default);
}

public enum LuckCheckpointConversionOutcome
{
    Converted,
    AlreadyConverted,
    CouldNotConvert
}

public sealed record LuckCheckpointConversionEvent(
    Guid EventId,
    LuckCheckpointConversionOutcome Outcome,
    string? Reason = null);

public sealed record LuckCheckpointConversionReport(IReadOnlyList<LuckCheckpointConversionEvent> Events)
{
    public int ConvertedCount => Events.Count(x => x.Outcome == LuckCheckpointConversionOutcome.Converted);
    public int AlreadyConvertedCount => Events.Count(x => x.Outcome == LuckCheckpointConversionOutcome.AlreadyConverted);
    public int CouldNotConvertCount => Events.Count(x => x.Outcome == LuckCheckpointConversionOutcome.CouldNotConvert);
    public bool Succeeded => CouldNotConvertCount == 0;
}
