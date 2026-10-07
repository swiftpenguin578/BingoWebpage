using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bingo.BrowserTests;

// T1 item 4: route access, binding markers, shared design checks 1–3 and Danish completeness for
// the bound Audit page. Server rules stay in AuditHistoryIntegrationTests.
[Collection(BrowserTestGroup.Name)]
public sealed class AuditUiTests(BrowserTestApplicationFactory factory)
{
    [Fact]
    public async Task AuditRoutesRequireSignIn()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var route in new[] { "/Admin/Audit", "/Admin/Audit/Index", "/Admin/Audit?entry=00000000-0000-0000-0000-000000000001" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
        }
    }

    [Fact]
    public void AuditPageBindsTheReferenceAndFollowsTheSharedDesignRules()
    {
        var root = FindRepositoryRoot();
        var pages = Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Audit");
        var index = File.ReadAllText(Path.Combine(pages, "Index.cshtml"));
        var model = File.ReadAllText(Path.Combine(pages, "Index.cshtml.cs"));
        var script = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "admin-audit.js"));
        Assert.Contains("[AdminDesign]", model);
        Assert.Contains("ViewData[\"PageFamily\"] = \"audit\";", index);
        foreach (var name in new[] { "event", "action", "actor", "type", "from", "to", "page", "entry" })
            Assert.Contains($"[BindProperty(SupportsGet = true, Name = \"{name}\")]", model);
        Assert.DoesNotContain("ActorUsername.Contains", model);
        Assert.Contains("ui.update(", script);
        Assert.DoesNotContain(" fetch(", script);
        Assert.DoesNotContain("setTimeout", script);
        Assert.DoesNotContain("setInterval", script);
        foreach (var file in new[] { "Index.cshtml", "_AuditEntryDrawer.cshtml" })
            Assert.DoesNotContain("style=\"", File.ReadAllText(Path.Combine(pages, file)));
        var rules = Regex.Replace(File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "admin-design-audit.css")), @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgb\(|hsl\(|font-family|box-shadow", rules);
        Assert.DoesNotMatch(@"\)\s+\.(btn|card|modal|drawer|toast|pill|tbl|banner|badge|fpanel|menu)\s*\{", rules);
    }

    [Fact]
    public void EveryAuditPageStringHasADanishEntry()
    {
        var root = FindRepositoryRoot();
        var danish = XDocument.Load(Path.Combine(root, "src", "Bingo.Web", "Resources", "AdminCommunityResource.da.resx"))
            .Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value);
        var missing = new List<string>();
        var files = Directory.GetFiles(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Audit"), "*.cs*")
            .Append(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminAuditLoadStates.cshtml"));
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var keys = Regex.Matches(source, @"\bD\[\s*""(?<key>(?:[^""\\]|\\.)*)""").Select(match => match.Groups["key"].Value).ToList();
            foreach (Match block in Regex.Matches(source, @"var (?:columns|presets) = new\[\] \{(?<body>.*?)\};", RegexOptions.Singleline))
                keys.AddRange(Regex.Matches(block.Groups["body"].Value, @"""(?<key>(?:[^""\\]|\\.)*)""").Select(match => match.Groups["key"].Value).Where(key => key is not ("" or "c-name")));
            foreach (var key in keys.Distinct())
                if (!danish.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) missing.Add($"{Path.GetFileName(file)}: {key}");
        }
        foreach (var area in Bingo.Web.Pages.Admin.Audit.AuditAreas.All)
            if (!danish.ContainsKey(area.Label)) missing.Add("area: " + area.Label);
        Assert.True(missing.Count == 0, "Missing Danish entries:\n" + string.Join('\n', missing));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
