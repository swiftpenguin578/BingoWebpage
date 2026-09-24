namespace Bingo.Application.Boards;

public static class TileDescriptionFormatter
{
    public const int MaximumManualDescriptionLength = 4000;
    public const int MaximumFrozenDescriptionLength = 16000;

    public static string Format(IEnumerable<TileDescriptionRequirement> requirements)
    {
        var ordered = requirements.OrderBy(value => value.Position).ToList();
        if (ordered.Count == 0) return string.Empty;
        if (ordered.Select(value => value.ManualObjective).Distinct().Count() != 1)
            throw new ArgumentException("Tile descriptions cannot combine manual and drop objectives.", nameof(requirements));

        if (ordered[0].ManualObjective)
            return string.Join(" & ", ordered.Select(value => value.Description.Trim()));

        return string.Join(" & ", ordered.Select(FormatDropRequirement));
    }

    private static string FormatDropRequirement(TileDescriptionRequirement requirement)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requirement.Target, 1);
        var selectedDrops = requirement.SelectedDrops
            .Where(value => !string.IsNullOrWhiteSpace(value.ItemName) && !string.IsNullOrWhiteSpace(value.SourceName))
            .ToList();
        var distinctItems = selectedDrops
            .GroupBy(value => value.ItemId)
            .Select(group => group.OrderBy(value => value.ItemName, StringComparer.OrdinalIgnoreCase).First().ItemName)
            .ToList();
        if (distinctItems.Count == 1)
            return $"Collect {requirement.Target} {distinctItems[0]}";

        var wording = $"Collect {requirement.Target} eligible drop{(requirement.Target == 1 ? string.Empty : "s")}";
        var sources = selectedDrops.Select(value => value.SourceName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToList();
        return sources.Count == 0 ? wording : $"{wording} from {string.Join(" or ", sources)}";
    }
}

public sealed record TileDescriptionRequirement(
    int Position,
    int Target,
    bool ManualObjective,
    string Description,
    IReadOnlyList<TileDescriptionDrop> SelectedDrops);

public sealed record TileDescriptionDrop(Guid ItemId, string ItemName, string SourceName);
