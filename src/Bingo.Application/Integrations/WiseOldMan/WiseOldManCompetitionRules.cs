using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bingo.Application.Integrations.WiseOldMan;

public static class WiseOldManCompetitionRules
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public const int MaximumCompetitionTitleLength = 50;
    public const int MaximumTeamNameLength = 30;
    public const int MaximumPlayerNameLength = 12;

    public static int ProviderCharacterCount(string value) => value.EnumerateRunes().Count();

    public static string NormalizePlayerName(string value)
    {
        var builder = new StringBuilder(value.Trim().Length);
        var pendingSpace = false;
        foreach (var rune in value.Trim().EnumerateRunes())
        {
            if (rune.Value is '-' or '_' || Rune.IsWhiteSpace(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace && builder.Length > 0) builder.Append(' ');
            pendingSpace = false;
            builder.Append(rune.ToString().ToLowerInvariant());
        }

        return builder.ToString();
    }

    public static string NormalizeTeamName(string value)
        => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeTitle(string value) => value.Trim();

    public static IReadOnlyList<string> Validate(
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        IReadOnlyList<WiseOldManCompetitionWriteTeam> teams,
        DateTimeOffset now)
    {
        var errors = new List<string>();
        var normalizedTitle = NormalizeTitle(title);
        if (ProviderCharacterCount(normalizedTitle) is < 1 or > MaximumCompetitionTitleLength)
            errors.Add("The event name must be between 1 and 50 characters for WOM.");
        if (endsAt <= startsAt) errors.Add("The WOM competition end must be after its start.");
        if (startsAt <= now.ToUniversalTime() || endsAt <= now.ToUniversalTime())
            errors.Add("The WOM competition schedule must be in the future.");

        var duplicateTeams = teams
            .GroupBy(team => NormalizeTeamName(team.Name), StringComparer.OrdinalIgnoreCase)
            .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
            .Select(group => string.IsNullOrWhiteSpace(group.Key) ? "(unnamed team)" : group.First().Name)
            .ToArray();
        foreach (var duplicate in duplicateTeams)
            errors.Add($"Team name '{duplicate}' is empty or duplicated after WOM normalization.");

        var players = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in teams)
        {
            var name = NormalizeTeamName(team.Name);
            if (ProviderCharacterCount(name) is < 1 or > MaximumTeamNameLength)
                errors.Add($"Team '{team.Name}' must be between 1 and 30 characters for WOM.");
            if (team.Participants.Count == 0)
                errors.Add($"Cannot create WOM competition with empty teams. Affected team: {team.Name}.");

            foreach (var participant in team.Participants)
            {
                var normalized = NormalizePlayerName(participant);
                if (ProviderCharacterCount(normalized) is < 1 or > MaximumPlayerNameLength || normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != ' '))
                {
                    errors.Add($"Player name '{participant}' is not a valid WOM name.");
                    continue;
                }

                if (!players.TryAdd(normalized, participant))
                    errors.Add($"Player name '{participant}' is duplicated after WOM normalization.");
            }
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static string Fingerprint(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
