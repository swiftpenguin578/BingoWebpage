using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bingo.BrowserTests;

// U8 Review: the in-place decision outcomes pass service refusal texts through Localize, so every
// user-facing sentence in the review service and page model needs a Danish entry (rule 17).
public sealed class ReviewLocalizationTests
{
    [Fact]
    public void EveryReviewRefusalAndOutcomeTextHasADanishEntry()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var root = Assert.IsType<DirectoryInfo>(directory).FullName;
        var entries = XDocument.Load(Path.Combine(root, "src", "Bingo.Web", "Resources", "SharedResource.da.resx"))
            .Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value);
        string[] sources =
        [
            Path.Combine("src", "Bingo.Infrastructure", "Evidence", "SubmissionService.cs"),
            Path.Combine("src", "Bingo.Infrastructure", "Evidence", "SubmissionService.Readback.cs"),
            Path.Combine("src", "Bingo.Web", "Pages", "Admin", "Review", "Details.cshtml.cs")
        ];
        var missing = new List<string>();
        foreach (var relative in sources)
            foreach (var line in File.ReadLines(Path.Combine(root, relative)).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)))
                foreach (Match literal in Regex.Matches(line, "(?<![$@\"])\"(?<text>(?:\\\\.|[^\"\\\\])*)\""))
                {
                    var text = literal.Groups["text"].Value;
                    if (literal.Index > 0 && line[literal.Index - 1] == '$') continue;
                    if (text.Length > 12 && text.Contains(' ') && char.IsUpper(text[0]) && (text.EndsWith('.') || text.EndsWith('?')) && (!entries.TryGetValue(text, out var value) || string.IsNullOrWhiteSpace(value)))
                        missing.Add($"{Path.GetFileName(relative)}: {text}");
                }
        Assert.True(missing.Count == 0, "Missing Danish entries:\n" + string.Join('\n', missing.Distinct().Order()));
    }
}
