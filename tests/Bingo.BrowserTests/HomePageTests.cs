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
    public async Task HomePageLoadsTheFoundation()
    {
        using var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("The bingo foundation is running.", content);
    }

    [Fact]
    public async Task LivenessEndpointDoesNotRequireTheDatabase()
    {
        using var response = await _client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
    }
}
