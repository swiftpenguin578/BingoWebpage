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
        Assert.Contains("Follow every team, tile and drop.", content);
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
}
