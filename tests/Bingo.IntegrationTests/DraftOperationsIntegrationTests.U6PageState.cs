using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bingo.IntegrationTests;

// U6 (A10): Teams / Draft renders in the browser from its embedded page state, so the
// page tests read that state and the page script instead of server-rendered forms.
public sealed partial class DraftOperationsIntegrationTests
{
    internal static JsonElement DraftPageState(string html)
    {
        var match = Regex.Match(html, "<template data-draft-state>(.*?)</template>", RegexOptions.Singleline);
        Assert.True(match.Success, "The Teams / Draft page embeds its state.");
        return JsonDocument.Parse(match.Groups[1].Value.Replace("<\\/", "</", StringComparison.Ordinal)).RootElement.Clone();
    }

    // The page posts a handler through its script (the old page rendered a form for it).
    internal static void AssertDraftPageOffers(string handler)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var script = File.ReadAllText(Path.Combine(directory!.FullName, "src", "Bingo.Web", "wwwroot", "js", "admin-draft.js"));
        Assert.Contains($"'{handler}'", script, StringComparison.Ordinal);
    }
}
