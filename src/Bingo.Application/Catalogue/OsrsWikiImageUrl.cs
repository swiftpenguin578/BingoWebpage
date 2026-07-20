namespace Bingo.Application.Catalogue;

public static class OsrsWikiImageUrl
{
    private const string WikiHost = "oldschool.runescape.wiki";
    private const string DirectFilePrefix = "https://oldschool.runescape.wiki/w/Special:Redirect/file/";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, WikiHost, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var fragment = Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));
        var fileName = FileNameAfter(fragment, "/media/File:");

        if (fileName is null)
        {
            var path = Uri.UnescapeDataString(uri.AbsolutePath);
            fileName = FileNameAfter(path, "/w/File:")
                ?? FileNameAfter(path, "/wiki/File:");
        }

        return fileName is null
            ? trimmed
            : DirectFilePrefix + Uri.EscapeDataString(fileName);
    }

    private static string? FileNameAfter(string value, string marker)
    {
        if (!value.StartsWith(marker, StringComparison.OrdinalIgnoreCase)) return null;
        var fileName = value[marker.Length..].Trim();
        return fileName.Length == 0 ? null : fileName;
    }
}
