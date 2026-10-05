using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bingo.Web.UI;

namespace Bingo.BrowserTests;

public sealed class AdminDesignLocalizationTests
{
    [Fact]
    public void ScopedAdminEventWordingHasEnglishFallbackAndDanishEventTerminology()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var resources = Path.Combine(Assert.IsType<DirectoryInfo>(directory).FullName, "src", "Bingo.Web", "Resources");
        var english = XDocument.Load(Path.Combine(resources, "SharedResource.resx")).Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")!.Value);
        var danish = XDocument.Load(Path.Combine(resources, "SharedResource.da.resx")).Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")!.Value);
        Assert.Equal("Events", danish["AdminDesign.Events"]); Assert.Equal("Event", danish["AdminDesign.Event"]); Assert.Equal("Alle events", danish["All events"]);
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
        var sources = Directory.GetFiles(Path.Combine(web, "Pages", "Shared"), "_AdminDesign*.cshtml").ToDictionary(path => path, File.ReadAllText);
        foreach (var type in typeof(AdminDesignAttribute).Assembly.GetTypes().Where(type => type.GetCustomAttribute<AdminDesignAttribute>() is not null))
        {
            Assert.EndsWith("Model", type.Name, StringComparison.Ordinal);
            var relative = type.FullName!["Bingo.Web.Pages.".Length..].Replace('.', Path.DirectorySeparatorChar);
            var markup = Path.Combine(web, "Pages", relative[..^"Model".Length] + ".cshtml");
            sources.Add(markup, File.ReadAllText(markup));
            sources.Add(markup + ".cs", File.ReadAllText(markup + ".cs"));
        }
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
        Assert.True(missing.Count == 0, "Missing Danish entries:\n" + string.Join('\n', missing.Distinct().Order()));
    }
}
