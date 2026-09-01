namespace Bingo.Web.HistoricalImport;

public sealed record HistoricalImportResult(bool Succeeded, bool Applied, bool NoOp, IReadOnlyList<string> Errors, string Summary)
{
    public static HistoricalImportResult Failure(IEnumerable<string> errors) =>
        new(false, false, false, errors.ToArray(), "Historical import preflight failed.");
}

public sealed record HistoricalImportOptions(string ManifestPath, string? InputPath, string? ActorUsername, string? Confirmation, bool Apply);
