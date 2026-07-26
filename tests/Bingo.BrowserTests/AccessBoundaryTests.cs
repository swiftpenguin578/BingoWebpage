using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Web.Pages.Account;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bingo.BrowserTests;

public sealed class AccessBoundaryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AccessBoundaryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
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
        Assert.Contains("Use your public username and password", content);
    }

    [Fact]
    public async Task AccessChangedLoginShowsTheApprovedSignInAgainFeedback()
    {
        using var response = await _client.GetAsync("/Account/Login?accessChanged=true");
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Your access changed. Please sign in again.", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousAccountPagesRenderDanishFeedbackAndInstructions()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.AcceptLanguage.ParseAdd("da");
        using var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Brug dit offentlige brugernavn", content);
        Assert.Contains("Brugernavn", content);
        Assert.Contains("Adgangskode", content);
        Assert.DoesNotContain("InvalidOperationException", content);
    }

    [Fact]
    public async Task DiscordTicketHandlerFetchesUserInfoAndMapsClaims()
    {
        using var configured = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DiscordAuthentication:ClientId", "test-client");
            builder.UseSetting("DiscordAuthentication:ClientSecret", "test-secret");
        });
        var options = configured.Services.GetRequiredService<IOptionsMonitor<OAuthOptions>>().Get("Discord");
        var handler = new DiscordUserResponseHandler("{\"id\":\"discord-42\",\"global_name\":\"Ticket Display\",\"username\":\"fallback\"}");
        using var client = new HttpClient(handler);
        using var tokenJson = JsonDocument.Parse("{\"access_token\":\"opaque-test-token\",\"token_type\":\"Bearer\"}");
        using var token = OAuthTokenResponse.Success(tokenJson);
        var identity = new ClaimsIdentity("Discord");
        var context = new OAuthCreatingTicketContext(new ClaimsPrincipal(identity), new AuthenticationProperties(), new DefaultHttpContext(), new AuthenticationScheme("Discord", null, typeof(OAuthHandler<OAuthOptions>)), options, client, token, default);

        await options.Events.CreatingTicket(context);

        Assert.Equal("https://discord.com/api/users/@me", handler.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal("opaque-test-token", handler.Authorization.Parameter);
        Assert.Equal("discord-42", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Ticket Display", identity.FindFirst(ClaimTypes.Name)?.Value);
    }

    [Fact]
    public async Task DiscordLoginRouteChallengesAndUsesDistinctMiddlewareAndCompletionRoutes()
    {
        using var configured = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DiscordAuthentication:ClientId", "test-client");
            builder.UseSetting("DiscordAuthentication:ClientSecret", "test-secret");
        });
        using var client = configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var challenge = await client.GetAsync("/Account/DiscordLogin");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        Assert.StartsWith("https://discord.com/api/oauth2/authorize", challenge.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Contains("/Account/DiscordCallback", Uri.UnescapeDataString(challenge.Headers.Location.ToString()), StringComparison.Ordinal);

        using var completion = await client.GetAsync("/Account/DiscordComplete");
        Assert.True(completion.StatusCode == HttpStatusCode.Redirect, await completion.Content.ReadAsStringAsync());
        Assert.Equal("/Account/Login", completion.Headers.Location?.ToString());
        Assert.NotEqual("/Account/DiscordComplete", configured.Services.GetRequiredService<IOptionsMonitor<OAuthOptions>>().Get("Discord").CallbackPath.Value);
        Assert.Equal("/Account/DiscordCallback", configured.Services.GetRequiredService<IOptionsMonitor<OAuthOptions>>().Get("Discord").CallbackPath.Value);
    }

    [Fact]
    public void DanishNotificationMessagesRenderFromSemanticTypes()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("da");
            var text = _factory.Services.GetRequiredService<IStringLocalizer<Bingo.Web.SharedResource>>();
            Assert.Equal("Administratoradgang givet", text["Admin access granted"]);
            Assert.Equal("En administrator gav din konto administratoradgang.", text["An administrator granted your account Admin access."]);
            Assert.Equal("Du er logget ind.", text["Signed in successfully."]);
            Assert.Equal("Du er logget ind med Discord.", text["Signed in with Discord."]);
            Assert.Equal("Din konto er klar.", text["Your account is ready."]);
            Assert.Equal("Du er logget ud.", text["You have signed out."]);
            Assert.Equal("Din adgang er ændret. Log ind igen.", text["Your access changed. Please sign in again."]);
            Assert.Equal("Brugernavn", text["Username"]);
            Assert.Equal("Adgangskode", text["Password"]);
            Assert.Equal("Nuværende adgangskode", text["Current password"]);
            Assert.Equal("Ny adgangskode", text["New password"]);
            Assert.Equal("Bekræft adgangskode", text["Confirm password"]);
            Assert.Equal("Bekræft ny adgangskode", text["Confirm new password"]);
            Assert.Equal("Ingen notifikationer.", text["No notifications."]);
        }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
    }

    [Fact]
    public void PasswordLoginTicketsHaveAbsoluteNonSlidingLifetimes()
    {
        var issuedAt = new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
        var session = ChangePasswordModel.CreatePasswordSessionProperties(false, issuedAt);
        var remembered = ChangePasswordModel.CreatePasswordSessionProperties(true, issuedAt);

        Assert.False(session.IsPersistent);
        Assert.Equal(issuedAt.AddHours(12), session.ExpiresUtc);
        Assert.True(remembered.IsPersistent);
        Assert.Equal(issuedAt.AddDays(30), remembered.ExpiresUtc);
        Assert.False(session.AllowRefresh);
        Assert.False(remembered.AllowRefresh);
    }

    [Fact]
    public async Task LoginEndpointAppliesIdentifierAndNetworkThrottlesWithGenericSafeLogging()
    {
        var logs = new CapturingLoggerProvider();
        using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        var throttle = configured.Services.GetRequiredService<LoginThrottleService>();
        for (var index = 0; index < 5; index++) throttle.RecordFailure("ALICE", "other-network");
        var identifierResponse = await PostLoginAsync(configured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }), "alice", "not-a-password");
        Assert.Contains("The username or password is incorrect.", identifierResponse, StringComparison.Ordinal);

        using var networkConfigured = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        var networkThrottle = networkConfigured.Services.GetRequiredService<LoginThrottleService>();
        for (var index = 0; index < 5; index++) networkThrottle.RecordFailure("unrelated", "unknown");
        var networkResponse = await PostLoginAsync(networkConfigured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }), "bob", "another-not-a-password");
        Assert.Contains("The username or password is incorrect.", networkResponse, StringComparison.Ordinal);
        Assert.Contains(logs.Messages, message => message.Contains("Password login blocked", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("alice", StringComparison.OrdinalIgnoreCase) || message.Contains("bob", StringComparison.OrdinalIgnoreCase) || message.Contains("password", StringComparison.OrdinalIgnoreCase) && !message.Contains("Password login blocked", StringComparison.Ordinal));
    }

    private static async Task<string> PostLoginAsync(HttpClient client, string username, string password)
    {
        var page = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = token }));
        return await response.Content.ReadAsStringAsync();
    }

    private readonly WebApplicationFactory<Program> _factory;

    private sealed class DiscordUserResponseHandler(string json) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri; Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }
    }
    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
    }
}
