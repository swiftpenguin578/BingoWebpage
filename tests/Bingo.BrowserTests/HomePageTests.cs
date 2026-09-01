using Microsoft.AspNetCore.Mvc.Testing;

namespace Bingo.BrowserTests;

public sealed class HomePageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HomePageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task HomePageLoadsThePublicEventEntryPoint()
    {
        using var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Every team. Every tile. Live.", content);
    }

    [Fact]
    public async Task LivenessEndpointDoesNotRequireTheDatabase()
    {
        using var response = await _client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MissingPageHasAFriendlyNotFoundResponse()
    {
        using var response = await _client.GetAsync("/this-page-does-not-exist");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("That page could not be found.", content);
        Assert.Contains("Return to public boards", content);
    }

    [Fact]
    public void PublicLandingHasExplicitRosterAndBoardDestinations()
    {
        var markup = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Bingo.Web", "Pages", "Index.cshtml"));
        Assert.Contains("Roster available", markup);
        Assert.Contains("Draft finalized", markup);
        Assert.Contains("Board published", markup);
        Assert.Contains("Start postponed", markup);
        Assert.Contains("View roster", markup);
        Assert.Contains("View board", markup);
        Assert.Contains("/Events/Teams", markup);
        Assert.Contains("/Events/Signup", markup);
        Assert.Contains("EventDestination.Signup", markup);
        Assert.Contains("landing-page", markup);
        Assert.Contains("landing-hero", markup);
        Assert.Contains("landing-event", markup);
        Assert.Contains("landing-ledger", markup);
        Assert.Contains("landing-features", markup);
        Assert.Contains("login-artwork.svg", markup);
        Assert.Contains("login-artwork-dark.svg", markup);
        Assert.Contains("DK Legacy Bingo", markup);
        Assert.Contains("Every team. Every tile. Live.", markup);
        Assert.Contains("Follow live boards, reviewed submissions and rankings from one place.", markup);
        Assert.Contains("View live event", markup);
        Assert.Contains("How it works", markup);
        Assert.DoesNotContain("scrollIntoView({ block: \"start\" })", markup);
        Assert.Contains("Live boards", markup);
        Assert.Contains("Reviewed drops", markup);
        Assert.Contains("Rankings & stats", markup);
        Assert.Contains("Current events", markup);
        Assert.Contains("Previous events", markup);
        Assert.DoesNotContain("public-ui-event-directory-row", markup);
        Assert.DoesNotContain("public-ui-component-header", markup);
        Assert.DoesNotContain("public-ui-surface", markup);
        Assert.DoesNotContain("data-public-ui-dialog", markup);
        Assert.Contains("var anonymousSignup = User.Identity?.IsAuthenticated != true && bingoEvent.Destination == Bingo.Web.Events.EventDestination.Signup;", markup);
        Assert.Contains("Url.Page(\"/Account/Login\", new { ReturnUrl = signupUrl })", markup);
        Assert.DoesNotContain("class=\"public-home\"", markup);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
