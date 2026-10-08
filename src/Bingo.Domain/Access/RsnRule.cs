using System.Text.RegularExpressions;

namespace Bingo.Domain.Access;

/// <summary>
/// U5-Q4: the shared RSN rule for every new or renamed OSRS account name
/// (1–12 letters, digits, spaces, '-' or '_'). Stored names are never
/// re-validated on read; callers apply this only to typed input.
/// </summary>
public static partial class RsnRule
{
    public const string Message = "Use up to 12 letters, numbers, spaces, - or _.";

    public static bool IsValid(string? name) => name is not null && Pattern().IsMatch(name.Trim());

    [GeneratedRegex("^[A-Za-z0-9 _-]{1,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
