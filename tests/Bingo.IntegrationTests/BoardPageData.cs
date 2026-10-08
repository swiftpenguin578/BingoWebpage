using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bingo.IntegrationTests;

/// <summary>
/// A10: the U7 Board page ships its state as JSON (data-view, data-labels) and the
/// browser module paints it, so tests read the transport instead of rendered markup.
/// </summary>
internal static class BoardPageData
{
    public static JsonElement Attribute(string html, string name)
    {
        var match = Regex.Match(html, $"data-{name}=\"([^\"]*)\"");
        Assert.True(match.Success, $"data-{name} is missing from the Board page.");
        return JsonDocument.Parse(WebUtility.HtmlDecode(match.Groups[1].Value)).RootElement.Clone();
    }

    public static JsonElement View(string html) => Attribute(html, "view");

    public static JsonElement Labels(string html) => Attribute(html, "labels");

    public static async Task<JsonElement> EditorDataAsync(HttpClient client, string path, Guid tileId)
    {
        using var response = await client.GetAsync($"{path}?handler=EditorData&tileId={tileId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
