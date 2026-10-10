using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bingo.Web.Pages.Admin.Accounts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bingo.BrowserTests;

// A10 (T1): the old Accounts/Manage/Transfer markup assertions were rewritten for the bound
// Accounts.dc.html page (directory, drawer, confirmations, Transfer dialog). Server rules stay
// in the integration tests; these check routes, binding markers and the shared design rules.
[Collection(BrowserTestGroup.Name)]
public sealed class AccountsUiTests
{
    private readonly HttpClient client;

    public AccountsUiTests(BrowserTestApplicationFactory factory) => client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task AccountsRoutesRequireAdminAccess()
    {
        foreach (var route in new[] { "/Admin/Accounts", "/Admin/Accounts/Index", "/Admin/Accounts/Create", $"/Admin/Accounts/Manage/{Guid.NewGuid()}", "/Admin/Accounts/Transfer", $"/Admin/Accounts?account={Guid.NewGuid()}" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
        }
    }

    [Fact]
    public void AccountsPageBindsTheReferenceComposition()
    {
        var root = FindRepositoryRoot();
        var pages = Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts");
        var index = File.ReadAllText(Path.Combine(pages, "Index.cshtml"));
        var model = File.ReadAllText(Path.Combine(pages, "Index.cshtml.cs"));
        var drawer = File.ReadAllText(Path.Combine(pages, "_AccountDrawer.cshtml"));
        var transfer = File.ReadAllText(Path.Combine(pages, "_AccountTransfer.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "admin-accounts.js"));

        Assert.Contains("[AdminDesign]", model);
        Assert.Contains("ViewData[\"PageFamily\"] = \"accounts\";", index);
        foreach (var name in new[] { "q", "role", "page", "account" })
            Assert.Contains($"[BindProperty(SupportsGet = true, Name = \"{name}\"), FromQuery(Name = \"{name}\")]", model); // query-only: "page" is also a route value
        Assert.DoesNotContain("WebsiteSearch", index + model);
        Assert.Contains("class=\"tbl ac-tbl sticky-first\"", index);
        Assert.Contains("class=\"seg\" role=\"radiogroup\"", index);
        // T1-9: no "username" wording or name that invites username autofill; password-manager ignore markers.
        var search = System.Text.RegularExpressions.Regex.Match(index, "<input[^>]*id=\"ac-search\"[^>]*>").Value;
        foreach (var marker in new[] { "autocomplete=\"off\"", "data-1p-ignore", "data-lpignore=\"true\"", "data-bwignore", "data-form-type=\"other\"", "D[\"Search accounts\"]" })
            Assert.Contains(marker, search);
        Assert.DoesNotContain("user", search, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-admin-page-script", index);
        Assert.Contains("data-admin-page-style", index);
        // C-ACC-1 replaces the reference's secret note; AU24 typed username is on the confirmation step.
        Assert.Contains("The link stops working after 60 minutes, after one use, or if this account’s role, status or ownership changes.", drawer + script);
        Assert.DoesNotContain("Works once, for 60 minutes", drawer + script + index);
        Assert.Contains("Input.DestinationUsernameConfirmation", script);
        Assert.Contains("data-transfer-confirm-field", transfer);
        Assert.Contains("data-account-confirm=\"Disable\"", drawer);
        Assert.Contains("maxlength=\"500\"", drawer);
        // D5/A1: the secret never reaches storage, the URL, history or TempData.
        Assert.DoesNotContain("localStorage", script);
        Assert.DoesNotContain("sessionStorage", script);
        Assert.DoesNotContain("CredentialLink", model);
        Assert.Contains("result.accountId === data.id", script);
        // C-CMP-2 transport and busy timing come from the shell.
        Assert.Contains("window.AdminFetch.request", script);
        Assert.Contains("ui.busy(", script);
        Assert.DoesNotContain(" fetch(", script);
        Assert.DoesNotContain("setInterval", script);
        // Timers: the 250 ms search debounce and the 2 s "Copied" label only.
        Assert.Equal(2, Regex.Count(script, @"setTimeout\("));
    }

    [Fact]
    public void AccountsPageStylesFollowTheSharedDesignRules()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "admin-design-accounts.css"));
        var rules = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgb\(|hsl\(|font-family|box-shadow|border-radius:(?!var\(--dk-)", rules); // radii only from tokens
        foreach (Match selector in Regex.Matches(rules, @"([^{}]+)\{[^{}]*\}"))
        {
            var text = selector.Groups[1].Value.Trim();
            if (text.StartsWith('@')) continue;
            foreach (var part in text.Split(','))
                Assert.Matches(@"^\s*:where\(\[data-page-family=""accounts""\]\)", part.Replace("@media (max-width:640px){", string.Empty, StringComparison.Ordinal));
        }
        Assert.DoesNotMatch(@"\)\s+\.(btn|card|modal|drawer|toast|pill|tbl|banner|badge)\s*\{", rules);
        foreach (var file in new[] { "Index.cshtml", "_AccountDrawer.cshtml", "_AccountTransfer.cshtml", "_AccountChange.cshtml" })
            Assert.DoesNotContain("style=\"", File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", file)));
    }

    [Fact]
    public void EveryAccountsStringHasADanishEntry()
    {
        var root = FindRepositoryRoot();
        var danish = XDocument.Load(Path.Combine(root, "src", "Bingo.Web", "Resources", "AdminCommunityResource.da.resx"))
            .Descendants("data").ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value);
        var missing = new List<string>();
        var files = Directory.GetFiles(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts"), "*.cs*")
            .Append(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminAccountsLoadStates.cshtml"));
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var keys = Regex.Matches(source, @"\b(?:D\[|L\()\s*""(?<key>(?:[^""\\]|\\.)*)""").Select(match => match.Groups["key"].Value).ToList();
            foreach (Match block in Regex.Matches(source, @"var (?:labels|roleOptions|columns) = new\[\] \{(?<body>.*?)\};", RegexOptions.Singleline))
                keys.AddRange(Regex.Matches(block.Groups["body"].Value, @"""(?<key>(?:[^""\\]|\\.)*)""").Select(match => match.Groups["key"].Value).Where(key => key is not ("" or "user" or "admin" or "superadmin" or "c-name")));
            foreach (var key in keys.Distinct())
                if (!danish.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) missing.Add($"{Path.GetFileName(file)}: {key}");
        }
        Assert.True(missing.Count == 0, "Missing Danish entries:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void LaneResourceNamesAreUniqueIgnoringCase()
    {
        // .resx names are case-insensitive: a duplicate such as "accounts"/"Accounts" is silently
        // dropped by the build, leaving English text in the Danish UI.
        var root = FindRepositoryRoot();
        foreach (var file in new[] { "AdminCommunityResource.da.resx", "AuditResource.da.resx" })
        {
            var names = XDocument.Load(Path.Combine(root, "src", "Bingo.Web", "Resources", file)).Descendants("data").Select(item => item.Attribute("name")!.Value);
            var duplicates = names.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            Assert.True(duplicates.Count == 0, $"{file}: duplicate names ignoring case: {string.Join(", ", duplicates)}");
        }
    }

    [Theory]
    // Spring DST (29 March 2026, Copenhagen 23-hour day): calendar dates, never elapsed hours.
    [InlineData("2026-03-29T10:00:00Z", "2026-03-28T22:30:00Z", "Yesterday 23:30")]
    [InlineData("2026-03-29T10:00:00Z", "2026-03-28T23:30:00Z", "Today 00:30")]
    [InlineData("2026-03-29T22:30:00Z", "2026-03-28T22:59:59.999999Z", "28 Mar 2026, 23:59")]
    // Autumn DST (25 October 2026, 25-hour day).
    [InlineData("2026-10-25T23:30:00Z", "2026-10-24T22:00:00Z", "Yesterday 00:00")]
    [InlineData("2026-10-25T23:30:00Z", "2026-10-25T23:00:00Z", "Today 00:00")]
    [InlineData("2026-10-25T23:30:00Z", "2026-10-24T21:59:59.999999Z", "24 Oct 2026, 23:59")]
    public void LastLoginComparesCopenhagenCalendarDates(string now, string value, string expected)
    {
        var english = CultureInfo.GetCultureInfo("en-GB");
        Assert.Equal(expected, AccountsPresentation.RelativeLogin(DateTimeOffset.Parse(value, CultureInfo.InvariantCulture), DateTimeOffset.Parse(now, CultureInfo.InvariantCulture), "Never", "Today {0}", "Yesterday {0}", english));
        Assert.Equal("Never", AccountsPresentation.RelativeLogin(null, DateTimeOffset.UnixEpoch, "Never", "Today {0}", "Yesterday {0}", english));
    }

    [Theory]
    [InlineData("ReviewOwner", "R")]
    [InlineData("mia_holm", "MH")]
    [InlineData("__", "?")]
    public void InitialsFollowTheReference(string username, string expected) => Assert.Equal(expected, AccountsPresentation.Initials(username));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
