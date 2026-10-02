namespace Bingo.Domain.Events;

public enum EventIdentityField { Name, Description, BuyInDescription, Timezone }
public enum EventIdentityResolution { None, KeepMine, UseCurrent }

public sealed record EventIdentityValues(string Name, string? Description, string? BuyInDescription, string Timezone)
{
    public EventIdentityValues Canonical() => new(Name.Trim(), Clean(Description), Clean(BuyInDescription), Timezone.Trim());
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record EventIdentityResolutions(
    EventIdentityResolution Name, EventIdentityResolution Description,
    EventIdentityResolution BuyInDescription, EventIdentityResolution Timezone,
    EventIdentityValues? Reviewed);

/// <summary>Three-way comparison for one atomic Identity edit; originals never silently rebase.</summary>
public sealed record EventIdentityComparison(EventIdentityValues Values, IReadOnlyList<EventIdentityField> Conflicts)
{
    public static EventIdentityComparison Compare(EventIdentityValues original, EventIdentityValues intended,
        EventIdentityValues current, EventIdentityResolutions resolutions)
    {
        original = original.Canonical(); intended = intended.Canonical(); current = current.Canonical();
        var reviewed = resolutions.Reviewed?.Canonical();
        var conflicts = new List<EventIdentityField>();
        string? Merge(EventIdentityField field, string? before, string? mine, string? latest,
            EventIdentityResolution choice, string? seen)
        {
            if (string.Equals(mine, before, StringComparison.Ordinal)
                || string.Equals(mine, latest, StringComparison.Ordinal)) return latest;
            if (choice != EventIdentityResolution.None)
            {
                if (choice is EventIdentityResolution.KeepMine or EventIdentityResolution.UseCurrent
                    && reviewed is not null && string.Equals(latest, seen, StringComparison.Ordinal))
                    return choice == EventIdentityResolution.KeepMine ? mine : latest;
            }
            else if (string.Equals(latest, before, StringComparison.Ordinal)) return mine;
            conflicts.Add(field);
            return mine;
        }
        var merged = new EventIdentityValues(
            Merge(EventIdentityField.Name, original.Name, intended.Name, current.Name, resolutions.Name, reviewed?.Name)!,
            Merge(EventIdentityField.Description, original.Description, intended.Description, current.Description, resolutions.Description, reviewed?.Description),
            Merge(EventIdentityField.BuyInDescription, original.BuyInDescription, intended.BuyInDescription, current.BuyInDescription, resolutions.BuyInDescription, reviewed?.BuyInDescription),
            Merge(EventIdentityField.Timezone, original.Timezone, intended.Timezone, current.Timezone, resolutions.Timezone, reviewed?.Timezone)!);
        return new(merged, conflicts);
    }
}
