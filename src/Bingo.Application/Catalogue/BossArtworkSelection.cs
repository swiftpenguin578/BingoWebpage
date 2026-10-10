namespace Bingo.Application.Catalogue;

/// <summary>
/// The one rule for which boss artwork a board tile shows when it has no image of its own: per
/// requirement one image per boss family (the family's priority boss, then name), requirements in
/// position order, distinct images, at most four. The public board and the admin preview both use
/// it so they cannot drift apart. Boss order inside a requirement is by priority, then boss name.
/// </summary>
public static class BossArtworkSelection
{
    public const int MaximumPerTile = 4;

    public readonly record struct Candidate(string BossName, string? ImageUrl);

    public static IReadOnlyList<string> ForRequirement(IEnumerable<Candidate> bosses) => bosses
        .Select(value => new { value.BossName, ImageUrl = OsrsWikiImageUrl.Normalize(value.ImageUrl) })
        .Where(value => !string.IsNullOrWhiteSpace(value.ImageUrl))
        .GroupBy(value => BossArtworkFamily.Key(value.BossName), StringComparer.OrdinalIgnoreCase)
        .Select(family => family.OrderBy(value => BossArtworkFamily.Priority(value.BossName)).ThenBy(value => value.BossName, StringComparer.Ordinal).First())
        .OrderBy(value => BossArtworkFamily.Priority(value.BossName)).ThenBy(value => value.BossName, StringComparer.Ordinal)
        .Select(value => value.ImageUrl!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <param name="requirementsInPositionOrder">Each requirement's boss candidates, requirements already in position order.</param>
    public static IReadOnlyList<string> ForTile(IEnumerable<IEnumerable<Candidate>> requirementsInPositionOrder) => requirementsInPositionOrder
        .SelectMany(ForRequirement)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(MaximumPerTile)
        .ToList();
}
