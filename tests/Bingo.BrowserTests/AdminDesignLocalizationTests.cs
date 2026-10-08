using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bingo.Web.UI;

namespace Bingo.BrowserTests;

public sealed class AdminDesignLocalizationTests
{
    [Fact]
    public void EveryNewLayoutPageDeclaresALiteralUntranslatedFamily()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var web = Path.Combine(Assert.IsType<DirectoryInfo>(directory).FullName, "src", "Bingo.Web");
        foreach (var type in typeof(AdminDesignAttribute).Assembly.GetTypes().Where(type => type.GetCustomAttribute<AdminDesignAttribute>() is not null))
        {
            var relative = type.FullName!["Bingo.Web.Pages.".Length..].Replace('.', Path.DirectorySeparatorChar);
            var markup = File.ReadAllText(Path.Combine(web, "Pages", relative[..^"Model".Length] + ".cshtml"));
            Assert.Matches("""ViewData\["PageFamily"\]\s*=\s*"[a-z][a-z0-9-]*";""", markup);
        }
        var layout = File.ReadAllText(Path.Combine(web, "Pages", "Shared", "_AdminDesignLayout.cshtml"));
        Assert.Contains("ViewData[\"PageFamily\"] as string ?? throw", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("title.ToLowerInvariant()", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void ScopedAdminEventWordingHasEnglishFallbackAndDanishEventTerminology()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var resources = Path.Combine(Assert.IsType<DirectoryInfo>(directory).FullName, "src", "Bingo.Web", "Resources");
        var english = XDocument.Load(Path.Combine(resources, "SharedResource.resx")).Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")!.Value);
        var danish = XDocument.Load(Path.Combine(resources, "SharedResource.da.resx")).Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")!.Value);
        Assert.Equal("Events", danish["AdminDesign.Events"]); Assert.Equal("Event", danish["AdminDesign.Event"]); Assert.Equal("Alle events", danish["All events"]);
        Assert.Equal("Eventet har endnu ikke været offentligt. En ændring af tidszonen gemmes direkte uden bekræftelse.", danish["AdminDesign.This event has not been public yet. A timezone change saves directly without a confirmation."]);
        Assert.EndsWith("omdøber det.", danish["AdminDesign.Permanent. It was created with the event and stays the same when you rename it."]);
        Assert.EndsWith("læser om det.", danish["AdminDesign.The event's name and the text players read about it."]);
        Assert.Equal(" Dine andre ændringer ({0}) gemmes samtidig.", danish["AdminDesign. Your other changes ({0}) are saved at the same time."]); // Planner58-4: exact reference sentence binding.
        Assert.Equal("{0} er afsluttet, så dets identitet ikke kan ændres.", danish["AdminDesign.{0} is finished, so its identity can’t be changed."]);
        Assert.Equal("{0} er arkiveret, så dets identitet ikke kan ændres.", danish["AdminDesign.{0} is archived, so its identity can’t be changed."]);
        Assert.Equal("{0} blev aflyst, så dets identitet ikke kan ændres.", danish["AdminDesign.{0} was cancelled, so its identity can’t be changed."]);
        Assert.Equal("Tilmelding åbner", danish["AdminDesign.Signups open"]);
        Assert.Equal("Tilmelding lukker", danish["AdminDesign.Signups close"]);
        Assert.Equal("Tilmelding åben", danish["Signups open"]); Assert.Equal("Tilmeldinger lukker", danish["Signups close"]); // Legacy keys remain unchanged.
        Assert.Equal("Aflyst", danish["Cancelled"]); Assert.Equal("Annuller", danish["Cancel"]);
        Assert.Equal("Bingoer", danish["Events"]); Assert.Equal("Bingo", danish["Event"]); // Legacy pages unchanged.
        foreach (var (key, value) in english)
        {
            Assert.StartsWith("AdminDesign.", key); Assert.Equal(key["AdminDesign.".Length..], value);
            Assert.DoesNotMatch("(?i)bingo|begivenhed", danish[key]);
        }
    }

    [Fact]
    public void EveryNewShellAndOptedInPageLiteralHasANonEmptyDanishEntry()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var root = Assert.IsType<DirectoryInfo>(directory).FullName;
        var web = Path.Combine(root, "src", "Bingo.Web");
        var entries = XDocument.Load(Path.Combine(web, "Resources", "SharedResource.da.resx"))
            .Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value);
        var communityEntries = XDocument.Load(Path.Combine(web, "Resources", "AdminCommunityResource.da.resx"))
            .Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value);
        Assert.All(communityEntries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"Empty Danish community entry: {entry.Key}"));
        var sources = Directory.GetFiles(Path.Combine(web, "Pages", "Shared"), "_AdminDesign*.cshtml").ToDictionary(path => path, File.ReadAllText);
        foreach (var type in typeof(AdminDesignAttribute).Assembly.GetTypes().Where(type => type.GetCustomAttribute<AdminDesignAttribute>() is not null))
        {
            Assert.EndsWith("Model", type.Name, StringComparison.Ordinal);
            var relative = type.FullName!["Bingo.Web.Pages.".Length..].Replace('.', Path.DirectorySeparatorChar);
            var markup = Path.Combine(web, "Pages", relative[..^"Model".Length] + ".cshtml");
            sources.Add(markup, File.ReadAllText(markup));
            sources.Add(markup + ".cs", File.ReadAllText(markup + ".cs"));
        }
        foreach (var partial in Directory.GetFiles(Path.Combine(web, "Pages", "Shared"), "_Admin*.cshtml")
            .Where(path => File.ReadAllText(path).Contains("IStringLocalizer<AdminCommunityResource>", StringComparison.Ordinal)))
            sources.TryAdd(partial, File.ReadAllText(partial));
        var service = File.ReadAllText(Path.Combine(web, "Navigation", "SharedShellService.cs"));
        sources.Add("SharedShellService.GetAdminDesignAsync", service.Split("GetAdminDesignAsync", 2)[1].Split("private async Task<SubmissionNavigation", 2)[0]);
        sources.Add("AdminEventStatePresentation", File.ReadAllText(Path.Combine(web, "UI", "AdminEventStatePresentation.cs")));
        var missing = new List<string>();
        foreach (var (path, source) in sources)
        {
            foreach (Match match in Regex.Matches(source, "(?:T|text|localizer)\\s*\\[\\s*\"(?<key>[^\"]+)\"|Localize\\(\\s*\"(?<key>[^\"]+)\"|ViewData\\[\"Title\"\\]\\s*=\\s*\"(?<key>[^\"]+)\""))
            {
                var key = match.Groups["key"].Value;
                if (!entries.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) missing.Add($"{Path.GetFileName(path)}: {key}");
            }
        }
        // D and L belong to AdminCommunityResource; localizer in Events' model
        // still belongs to SharedResource. Scan conditional first-key expressions,
        // not only the first literal, and include the current dynamic label sources.
        foreach (var (path, source) in sources)
        {
            foreach (Match call in Regex.Matches(source, """(?<owner>\bD|\bL|\bT|\btext|\blocalizer)\s*[\[(]"""))
            {
                var owner = call.Groups["owner"].Value;
                var resource = owner is "D" or "L" || owner == "text" && source.Contains("IStringLocalizer<AdminCommunityResource>", StringComparison.Ordinal)
                    ? communityEntries : entries;
                var start = call.Index + call.Length;
                var depth = 0;
                var end = start;
                for (; end < source.Length; end++)
                {
                    var character = source[end];
                    if (character == '"')
                    {
                        for (end++; end < source.Length; end++)
                        {
                            if (source[end] == '\\') end++;
                            else if (source[end] == '"') break;
                        }
                    }
                    else if (character is '(' or '[' or '{') depth++;
                    else if (character is ')' or ']' or '}') { if (depth == 0) break; depth--; }
                    else if (character == ',' && depth == 0) break; // First argument is the resource key.
                }
                var expression = source[start..end];
                foreach (Match literal in Regex.Matches(expression, "\"(?:\\\\.|[^\"\\\\])*\""))
                {
                    var before = expression[..literal.Index].TrimEnd();
                    // A literal key or a conditional key branch, not a string
                    // used inside the condition or another function's arguments.
                    if (before.Length == 0 || before[^1] is '?' or ':')
                        Check(path, literal.Value[1..^1], resource);
                }
            }
            // Dashboard uses D[stat.Label]; scan the named tuple label declarations,
            // not arbitrary literals in metric/hint expressions.
            if (source.Contains("D[stat.Label]", StringComparison.Ordinal))
            {
                var dynamicLabels = Regex.Matches(source, """Label:[ ]*"(?<key>[^"]+)["]""");
                Assert.NotEmpty(dynamicLabels);
                foreach (Match label in dynamicLabels)
                    Check(path, label.Groups["key"].Value, communityEntries);
            }
            if (!source.Contains("IStringLocalizer<AdminCommunityResource>", StringComparison.Ordinal)) continue;
            foreach (Match declaration in Regex.Matches(source, """(?:var (?:labels|views|columns|emptyTitle|emptyText)\s*=(?:"[^"]*"|[^";])*;|string Phase\([\s\S]*?};)"""))
                foreach (Match literal in Regex.Matches(declaration.Value, "\"(?<key>[^\"]+)\""))
                {
                    var key = literal.Groups["key"].Value;
                    if (char.IsUpper(key[0])) Check(path, key, communityEntries);
                }
        }
        void Check(string path, string key, Dictionary<string, string?> resource)
        {
            if (!resource.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                missing.Add($"{Path.GetFileName(path)}: {key}");
        }
        Assert.True(missing.Count == 0, "Missing Danish entries:\n" + string.Join('\n', missing.Distinct().Order()));
    }

    // M3 (U4 review): the Overview JSON outcomes pass service refusal texts through Localize/LocalizeRefusal,
    // so every user-facing sentence literal in the services behind the Overview dialogs needs a Danish entry.
    [Fact]
    public void EveryOverviewRefusalAndOutcomeTextHasADanishEntry()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var root = Assert.IsType<DirectoryInfo>(directory).FullName;
        var entries = XDocument.Load(Path.Combine(root, "src", "Bingo.Web", "Resources", "SharedResource.da.resx"))
            .Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value);
        string[] sources =
        [
            Path.Combine("src", "Bingo.Infrastructure", "Events", "EventLifecycleService.cs"),
            Path.Combine("src", "Bingo.Infrastructure", "Events", "EventDestructiveLifecycleService.cs"),
            Path.Combine("src", "Bingo.Infrastructure", "Events", "EventSignupLifecycleService.cs"),
            Path.Combine("src", "Bingo.Infrastructure", "Events", "EventQuarantineService.cs"),
            Path.Combine("src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs")
        ];
        var missing = new List<string>();
        foreach (var relative in sources)
            foreach (var line in File.ReadLines(Path.Combine(root, relative)).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)))
                foreach (Match literal in Regex.Matches(line, "(?<![$@\"])\"(?<text>(?:\\\\.|[^\"\\\\])*)\""))
                {
                    var text = literal.Groups["text"].Value;
                    if (literal.Index > 0 && line[literal.Index - 1] == '$') continue;
                    // A sentence shown to an admin: capitalised, several words, ends with a full stop or question mark.
                    if (text.Length > 12 && text.Contains(' ') && char.IsUpper(text[0]) && (text.EndsWith('.') || text.EndsWith('?')) && (!entries.TryGetValue(text, out var value) || string.IsNullOrWhiteSpace(value)))
                        missing.Add($"{Path.GetFileName(relative)}: {text}");
                }
        // Texts the services format with a name or category are localized by pattern in LocalizeRefusal or listed in full.
        foreach (var key in new[]
        {
            "This event window overlaps {0} ({1}).", "The replacement lifecycle window overlaps {0}.",
            "This event has protected participant history and cannot be discarded. Cancel it instead.",
            "This event has protected team history and cannot be discarded. Cancel it instead.",
            "This event has protected event access history and cannot be discarded. Cancel it instead.",
            "This event has protected evidence history and cannot be discarded. Cancel it instead.",
            "This event has protected submission history and cannot be discarded. Cancel it instead.",
            "Publish the results of {0} first.", "{0} is still the current event. Contact the Super Admin to archive it."
        })
            if (!entries.TryGetValue(key, out var danish) || string.IsNullOrWhiteSpace(danish)) missing.Add($"pattern: {key}");
        Assert.True(missing.Count == 0, "Missing Danish entries:\n" + string.Join('\n', missing.Distinct().Order()));
    }
    [Fact]
    public void OverviewRefusalsLocalizeTheFieldLabelAndTheOverlapWindow()
    {
        // U10 item 4: the DST refusal names the localized field label and the overlap window uses the request culture.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var manage = File.ReadAllText(Path.Combine(Assert.IsType<DirectoryInfo>(directory).FullName, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs"));
        Assert.Contains("daylight-saving time. Choose another time.\", Localize(label))", manage);
        Assert.Contains("LocalizeWindow(overlap.Groups[2].Value)", manage);
        Assert.DoesNotContain("Reopen cutoff", manage);
        Assert.DoesNotContain("Activation time", manage);
    }
}
