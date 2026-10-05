using System.Net;
using System.Text.RegularExpressions;

namespace Bingo.IntegrationTests;

public sealed partial class IdentityFieldConflictIntegrationTests
{
    [Fact]
    public async Task UnconfirmedTimezoneRendersSharedFieldErrorAndPermanentCopyLink()
    {
        await EditAsync(item => item.MarkFirstPublic(Now));
        using var client = await ClientAsync("first-admin");
        var before = await ReadAsync();
        var fields = Fields(await client.GetStringAsync(Route));
        fields["Input.Timezone"] = "Europe/Copenhagen";
        var html = await PostPageAsync(client, fields);
        var error = Regex.Match(html, "<div class=\"field-err field-validation-error\" id=\"error-ConfirmTimezoneChange\">(?<body>.*?)</div>", RegexOptions.Singleline);
        Assert.True(error.Success);
        Assert.Contains("Review the participant-facing time preview and confirm this timezone change.", WebUtility.HtmlDecode(error.Groups["body"].Value));
        Assert.Contains("id=\"identity-link\">Event link</h2>", html);
        Assert.Contains("/Events/identity-conflict/Signups", html);
        Assert.Matches("data-copy-url=\"[^\"]*/Events/identity-conflict/Signups\"", html);
        Assert.Equal(before.Version, (await ReadAsync()).Version);
        Assert.Equal("UTC", (await ReadAsync()).Timezone);
        Assert.Equal(0, await AuditCountAsync());
    }

    [Fact]
    public async Task ModelLevelRefusalRendersInVisibleSharedErrorBannerWithoutSaving()
    {
        using var client = await ClientAsync("first-admin");
        var before = await ReadAsync();
        var fields = Fields(await client.GetStringAsync(Route));
        fields["Input.Name"] = "Retained refused draft";
        fields["Input.Banner"] = "retired-input";
        var html = await PostPageAsync(client, fields);
        var banner = Regex.Match(html, "<div class=\"banner is-error\"(?<attributes>[^>]*)>(?<body>.*?)</div>", RegexOptions.Singleline);
        Assert.True(banner.Success);
        Assert.Contains("role=\"alert\"", banner.Groups["attributes"].Value);
        Assert.DoesNotContain("hidden", banner.Groups["attributes"].Value);
        var message = Regex.Match(banner.Groups["body"].Value, "<span data-component-text(?:=\"\")?>(?<text>.*?)</span>", RegexOptions.Singleline);
        Assert.True(message.Success);
        Assert.Equal("Event banners are no longer supported. Reload the page and submit only identity fields.", WebUtility.HtmlDecode(message.Groups["text"].Value));
        Assert.Equal("Retained refused draft", Fields(html)["Input.Name"]);
        var after = await ReadAsync();
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(0, await AuditCountAsync());
    }
}
