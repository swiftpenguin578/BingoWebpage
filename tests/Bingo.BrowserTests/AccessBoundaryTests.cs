using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bingo.BrowserTests;

public sealed class AccessBoundaryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AccessBoundaryTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task AnonymousVisitorIsRedirectedFromAdminPages()
    {
        using var response = await _client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task LoginPageIsPublic()
    {
        using var response = await _client.GetAsync("/Account/Login");
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Admin and captain accounts only", content);
    }
}
