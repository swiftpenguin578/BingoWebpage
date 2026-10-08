using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.BrowserTests;

[Collection(BrowserTestGroup.Name)]
public sealed class U2EventCreateHttpTests(BrowserTestApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostSessionReturnsLogin302WithoutCreatingOrReturningReadbackData(bool checkAgain)
    {
        var (client, actor, token) = await SignedInAsync();
        using (client)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var current = await db.Accounts.SingleAsync(row => row.Id == actor.Id);
                current.Disable(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)); await db.SaveChangesAsync();
            }
            var key = Guid.NewGuid();
            using var request = new HttpRequestMessage(checkAgain ? HttpMethod.Get : HttpMethod.Post,
                "/Admin/Events/Create" + (checkAgain ? $"?handler=CheckAgain&requestId={key}" : ""));
            request.Headers.Add("X-Requested-With", "XMLHttpRequest"); request.Headers.Add("Accept", "application/json");
            if (!checkAgain) { request.Headers.Add("RequestVerificationToken", token); request.Content = Payload(key, "Never persisted"); }
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Account/Login", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
            Assert.Contains("accessChanged=true", response.Headers.Location!.OriginalString);
            Assert.DoesNotContain("eventId", await response.Content.ReadAsStringAsync());
            using var verify = factory.Services.CreateScope();
            Assert.False(await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Events.AnyAsync(row => row.CreatedByAccountId == actor.Id));
        }
    }

    [Fact]
    public async Task ModalJsonRetainsValidationAndOneKeyReplayWhileNativeEndpointsRemainAvailable()
    {
        var (client, actor, token) = await SignedInAsync();
        using (client)
        {
            using var entry = await client.GetAsync("/Admin/Events/Create");
            Assert.Equal(HttpStatusCode.Redirect, entry.StatusCode); Assert.Equal("/Admin/Events?create=1", entry.Headers.Location!.OriginalString);
            var key = Guid.NewGuid();
            async Task<HttpResponseMessage> Post(string name)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/Events/Create") { Content = Payload(key, name) };
                request.Headers.Add("RequestVerificationToken", token); request.Headers.Add("X-Requested-With", "XMLHttpRequest"); request.Headers.Add("Accept", "application/json");
                return await client.SendAsync(request);
            }
            using (var invalid = await Post(string.Concat(Enumerable.Repeat("😀", 51))))
            {
                Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
                using var json = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
                Assert.Equal("invalid", json.RootElement.GetProperty("outcome").GetString()); Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Input.Name", out _));
            }
            Guid id;
            using (var created = await Post("U2 JSON private draft"))
            {
                Assert.Equal(HttpStatusCode.OK, created.StatusCode); using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
                Assert.Equal("completed", json.RootElement.GetProperty("outcome").GetString()); id = json.RootElement.GetProperty("eventId").GetGuid();
            }
            using (var replay = await Post("U2 JSON private draft"))
            { using var json = JsonDocument.Parse(await replay.Content.ReadAsStringAsync()); Assert.Equal(id, json.RootElement.GetProperty("eventId").GetGuid()); }
            using var found = await client.GetAsync($"/Admin/Events/Create?handler=CheckAgain&requestId={key}");
            Assert.Equal(HttpStatusCode.OK, found.StatusCode); Assert.True(found.Headers.CacheControl?.NoStore);
            using var check = JsonDocument.Parse(await found.Content.ReadAsStringAsync()); Assert.Equal(id, check.RootElement.GetProperty("eventId").GetGuid());
            using var scope = factory.Services.CreateScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Events.CountAsync(row => row.CreatedByAccountId == actor.Id));
        }
    }
    private async Task<(HttpClient Client, Account Actor, string Token)> SignedInAsync()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero); var name = "u2-create-" + Guid.NewGuid().ToString("N");
        const string password = "U2-create-password-123!";
        var actor = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now); actor.SetGlobalRole(GlobalRole.Admin);
        actor.SetPassword(new PasswordHasher<Account>().HashPassword(actor, password), false, now, incrementVersion: false);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); db.Add(actor); await db.SaveChangesAsync(); }
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = name, ["Input.Password"] = password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        var page = await client.GetStringAsync("/Admin/Events?create=1");
        token = WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        return (client, actor, token);
    }
    private static FormUrlEncodedContent Payload(Guid key, string name) => new(new Dictionary<string, string> { ["Input.RequestId"] = key.ToString(), ["Input.Name"] = name, ["Input.Timezone"] = "UTC" });
}
