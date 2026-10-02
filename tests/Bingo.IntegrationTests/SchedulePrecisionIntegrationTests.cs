using System.Data;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed partial class SchedulePrecisionIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_schedule_precision").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private WebApplicationFactory<Program> factory = null!;
    private Guid eventId;
    private string Route => $"/Admin/Events/Schedule/{eventId}";

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        foreach (var name in new[] { "first-admin", "second-admin", "ordinary-account" })
        {
            var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), Now);
            if (name != "ordinary-account") account.SetGlobalRole(GlobalRole.Admin);
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "synthetic-identity-password"), false, Now, incrementVersion: false);
            db.Accounts.Add(account);
        }
        eventId = Guid.NewGuid();
        db.Events.Add(new BingoEvent(eventId, "Original", "identity-conflict", "UTC", db.Accounts.Local.First().Id, Now));
        await db.SaveChangesAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services => {
                services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedClock());
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            }));
    }

    public async Task DisposeAsync() { await factory.DisposeAsync(); await database.DisposeAsync(); }

    // AU10 cases live in the accompanying partial file.
    private async Task<HttpClient> ClientAsync(string name)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var token = Fields(await client.GetStringAsync("/Account/Login"))["__RequestVerificationToken"];
        using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Input.Username"] = name, ["Input.Password"] = "synthetic-identity-password", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode); return client;
    }
    private async Task<string> PostPageAsync(HttpClient client, Dictionary<string, string> fields)
    {
        using var response = await client.PostAsync(Route, new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadAsStringAsync();
    }
    private async Task EditAsync(Action<BingoEvent> edit)
    {
        await using var db = new ApplicationDbContext(options); var item = await db.Events.SingleAsync(item => item.Id == eventId); edit(item); await db.SaveChangesAsync();
    }
    private async Task<BingoEvent> ReadAsync() { await using var db = new ApplicationDbContext(options); return await db.Events.AsNoTracking().SingleAsync(item => item.Id == eventId); }
    private async Task<int> AuditCountAsync() { await using var db = new ApplicationDbContext(options); return await db.AuditEntries.CountAsync(entry => entry.EventId == eventId && entry.Action == "event.schedule_updated"); }
    private static Dictionary<string, string> Fields(string html)
    {
        var result = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>"))
        {
            var name = Attribute(input.Value, "name"); if (name.Length == 0) continue;
            if (!result.ContainsKey(name)) result[name] = Attribute(input.Value, "value");
        }
        foreach (Match textarea in Regex.Matches(html, "<textarea\\b([^>]*)>(.*?)</textarea>", RegexOptions.Singleline))
            result[Attribute(textarea.Groups[1].Value, "name")] = WebUtility.HtmlDecode(textarea.Groups[2].Value).TrimStart('\r', '\n');
        foreach (Match select in Regex.Matches(html, "<select\\b([^>]*)>(.*?)</select>", RegexOptions.Singleline))
        {
            var selected = Regex.Matches(select.Groups[2].Value, "<option\\b([^>]*)>").Cast<Match>().FirstOrDefault(option => option.Groups[1].Value.Contains("selected", StringComparison.Ordinal));
            if (selected is not null) result[Attribute(select.Groups[1].Value, "name")] = Attribute(selected.Groups[1].Value, "value");
        }
        return result;
    }
    private static string Attribute(string tag, string name) => WebUtility.HtmlDecode(Regex.Match(tag, $"\\b{Regex.Escape(name)}=\"([^\"]*)\"").Groups[1].Value);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
