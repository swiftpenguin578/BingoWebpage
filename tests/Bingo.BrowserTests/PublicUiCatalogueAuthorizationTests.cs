using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bingo.BrowserTests;

public sealed class PublicUiCatalogueAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestScheme = "PublicUiTest";
    private readonly WebApplicationFactory<Program> factory;

    public PublicUiCatalogueAuthorizationTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task AnonymousVisitorCannotOpenTheCatalogue()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/Admin/PublicUi");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task NonAdminCannotOpenTheCatalogue()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
            }).AddScheme<PublicUiTestAuthOptions, PublicUiTestAuthHandler>(TestScheme, options => options.Role = "User")));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/Admin/PublicUi");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanRenderTheCatalogue()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
            }).AddScheme<PublicUiTestAuthOptions, PublicUiTestAuthHandler>(TestScheme, options => options.Role = "Admin")));
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/Admin/PublicUi");
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Public UI Catalogue", content, StringComparison.Ordinal);
        Assert.Contains("public-ui-surface", content, StringComparison.Ordinal);
        Assert.Contains("public-ui-surface--flat", content, StringComparison.Ordinal);
        Assert.Equal(6, content.Split("public-ui-surface--accent", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, content.Split("public-ui-surface--neutral", StringSplitOptions.None).Length - 1);
        Assert.Contains("Visualization accent", content, StringComparison.Ordinal);
        Assert.Contains("Leaderboards", content, StringComparison.Ordinal);
        Assert.Contains("EHB", content, StringComparison.Ordinal);
        Assert.Contains("Drop EHB", content, StringComparison.Ordinal);
        Assert.Contains("public-ui-view-switcher", content, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-switcher", content, StringComparison.Ordinal);
        Assert.DoesNotContain("public-ui-table-nav", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity EHB", content, StringComparison.Ordinal);
        Assert.Contains("Total drops", content, StringComparison.Ordinal);
        Assert.Contains("Bingo standings", content, StringComparison.Ordinal);
        Assert.Equal(2, content.Split("data-public-leaderboard-expand-heading", StringSplitOptions.None).Length - 1);
        Assert.Equal(4, content.Split("data-public-leaderboard-expand-control", StringSplitOptions.None).Length - 1);
        Assert.Contains("aria-label=\"Toggle Dragon Knights details\"", content, StringComparison.Ordinal);
        Assert.Contains("<path d=\"m6 9 6 6 6-6\" />", content, StringComparison.Ordinal);
        Assert.Contains("Cached activity is current", content, StringComparison.Ordinal);
        Assert.Contains("Provisional coverage: 18/24 current accounts matched", content, StringComparison.Ordinal);
        Assert.Contains("public-ui-section-heading public-ui-positive-delta--success\">+2487.0", content, StringComparison.Ordinal);
        Assert.Contains("<strong class=\"public-ui-section-heading\">Dragon Knights</strong>", content, StringComparison.Ordinal);
        Assert.Contains("<strong class=\"public-ui-section-heading\">6</strong>", content, StringComparison.Ordinal);
        Assert.Contains("<strong class=\"public-ui-section-heading public-ui-positive-delta--success\">+842.7</strong>", content, StringComparison.Ordinal);
        Assert.Contains("<strong class=\"public-ui-section-heading\">128</strong>", content, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary><strong class=\"public-ui-section-heading\">Dragon Knights</strong><small", content, StringComparison.Ordinal);
        Assert.Equal(2, content.Split("<table class=\"public-ui-table\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("<thead>", content, StringComparison.Ordinal);
        Assert.Contains("scope=\"col\"", content, StringComparison.Ordinal);
        Assert.Contains("<details ", content, StringComparison.Ordinal);
        Assert.Contains("public-ui-leaderboard-detail-summary", content, StringComparison.Ordinal);
        Assert.Contains("<section class=\"public-ui-standings", content, StringComparison.Ordinal);
        Assert.DoesNotContain("public-ranking-row", content, StringComparison.Ordinal);
        Assert.DoesNotContain("activity-preview-table", content, StringComparison.Ordinal);
        Assert.DoesNotContain("bingo-standings", content, StringComparison.Ordinal);
        Assert.DoesNotContain("performance-", content, StringComparison.Ordinal);

        var cssParts = new List<string>();
        foreach (var stylesheet in new[] { "site.transitional.foundation.css", "site.public-ui.css", "site.transitional.application.css" })
        {
            using var cssResponse = await client.GetAsync($"/css/{stylesheet}");
            cssResponse.EnsureSuccessStatusCode();
            cssParts.Add(await cssResponse.Content.ReadAsStringAsync());
        }
        var css = string.Join("\n", cssParts);
        Assert.Contains("--public-ui-flat-surface: rgb(28, 29, 31);", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-surface--flat { background: var(--public-ui-charcoal-surface); background-image: none;", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-surface { --public-ui-text-supporting: rgba(255, 255, 255, 0.6);", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-overline, .public-ui-role-label { color: var(--public-ui-text-muted);", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-component-header .public-ui-supporting-text { color: var(--public-ui-text-supporting);", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-catalogue-type-sample--supporting strong { color: var(--public-ui-text-muted);", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-data-example .public-ui-data-label { color: #fff;", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-data-row .public-ui-supporting-text { overflow: hidden; color: var(--public-ui-text-muted);", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--public-ui-flat-text-muted", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--public-ui-flat-text-supporting", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".public-ui-surface--flat .public-ui-overline", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-surface--flat::before", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-surface--flat::after", css, StringComparison.Ordinal);
        Assert.True(css.IndexOf(".public-ui-surface--flat::before", StringComparison.Ordinal) > css.IndexOf(".public-ui-surface::after", StringComparison.Ordinal));
        Assert.Contains(".public-ui-action--standard:hover:not(:disabled):not([aria-disabled=\"true\"]) { color: #171717; background: #e7e7e2; border-color: rgba(23, 23, 23, 0.2); text-decoration: none; }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-action--commit:hover:not(:disabled):not([aria-disabled=\"true\"]) { color: #fff; background: color-mix(in srgb, var(--public-ui-data-blue) 88%, #000); border-color: color-mix(in srgb, var(--public-ui-data-blue) 88%, #000); text-decoration: none; }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-action--text:hover:not(:disabled):not([aria-disabled=\"true\"]) { color: #fff; background: transparent; border-color: transparent; }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-action--hyperlink:hover:not(:disabled):not([aria-disabled=\"true\"]) { color: #fff; background: transparent; text-decoration: none; }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-action--hyperlink:hover:not(:disabled):not([aria-disabled=\"true\"])::after { color: var(--public-ui-data-blue); }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-action--destructive:hover:not(:disabled):not([aria-disabled=\"true\"]) { color: #ffc0bd; background: rgba(214, 77, 77, 0.07); border-color: rgba(214, 77, 77, 0.42); }", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".public-ui-action--hyperlink:hover { color: #f0ca7a", css, StringComparison.Ordinal);
        var tablePrimitiveIndex = css.IndexOf("/* Public UI catalogue table primitive */", StringComparison.Ordinal);
        var tableNarrowIndex = css.IndexOf("/* Public UI catalogue table narrow overrides */", StringComparison.Ordinal);
        var tableMediaIndex = css.IndexOf("@media", tablePrimitiveIndex, StringComparison.Ordinal);
        Assert.True(tablePrimitiveIndex >= 0);
        Assert.True(tableMediaIndex > tablePrimitiveIndex);
        Assert.True(tableNarrowIndex > tableMediaIndex);
        Assert.True(css.IndexOf(".public-ui-table {", tablePrimitiveIndex, StringComparison.Ordinal) < tableMediaIndex);
        Assert.True(css.IndexOf(".public-ui-standings {", tablePrimitiveIndex, StringComparison.Ordinal) < tableMediaIndex);
        Assert.Contains(".public-ui-table details summary:focus-visible", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-table th { letter-spacing: normal; text-transform: none; }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-table tbody td { vertical-align: middle; }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-positive-delta--success { color: var(--success); }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-table { width: 100%; min-width: 42rem; border: 0;", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-surface:has(.public-ui-table), .public-ui-surface:has(.public-ui-table) .public-ui-surface-content { min-height: 0; }", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-table .public-ui-leaderboard-expand-control {", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 2rem; min-height: 2rem;", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-table .public-ui-leaderboard-expand-control:focus-visible { outline: 2px solid var(--public-ui-data-blue);", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-view-switcher--two { grid-template-columns: repeat(2, minmax(0, 1fr)); }", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".public-ui-table-nav", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-standings {", css, StringComparison.Ordinal);
        Assert.Contains(".public-ui-section:has(> .public-ui-table)", css, StringComparison.Ordinal);
    }

    private sealed class PublicUiTestAuthOptions : AuthenticationSchemeOptions
    {
        public string Role { get; set; } = "User";
    }

    private sealed class PublicUiTestAuthHandler(IOptionsMonitor<PublicUiTestAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<PublicUiTestAuthOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "public-ui-test"), new Claim(ClaimTypes.Role, Options.Role)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
