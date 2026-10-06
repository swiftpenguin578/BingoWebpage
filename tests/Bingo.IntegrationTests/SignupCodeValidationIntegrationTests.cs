using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Security;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class SignupCodeValidationIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_signup_code_validation")
        .WithUsername("bingo").WithPassword("bingo_test_password"));
    private readonly CountingHasher hasher = new();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task AuthenticatedCodePostEnforcesLengthAndPreservesRetainClearAndRequiredSemantics()
    {
        var (admin, eventId) = await SeedAsync();
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, admin.LoginName);
        var route = $"/Admin/Events/Participants/{eventId}";
        var page = await client.GetStringAsync(route);
        var before = await SnapshotAsync(eventId);
        var tooLong = new string('x', 101);
        var accepted = new string('y', 100);

        // The initial enable failure must retain the enabled control even though the database is disabled.
        page = await RejectLengthAsync(page);
        using (var missing = await client.PostAsync($"{route}?handler=SignupCode", Post(page, true, null)))
        {
            Assert.Equal(HttpStatusCode.Redirect, missing.StatusCode);
            Assert.Contains("Enter a new signup code", await client.GetStringAsync(missing.Headers.Location));
        }
        Assert.Equal(before, await SnapshotAsync(eventId));
        Assert.Equal(0, hasher.HashCalls);

        // The other form's missing/invalid capacity must not reject this valid code.
        using (var enabled = await client.PostAsync($"{route}?handler=SignupCode", Post(page, true, accepted)))
            Assert.Equal(HttpStatusCode.Redirect, enabled.StatusCode);
        var enabledState = await CodeStateAsync(eventId);
        Assert.True(enabledState.Required);
        Assert.True(new SecretHasher().Verify(accepted, enabledState.Hash!));
        Assert.Equal(1, hasher.HashCalls);
        Assert.Equal(1, enabledState.Audits);
        Assert.Equal(2, enabledState.EventVersion);
        Assert.Equal(2, enabledState.FormVersion);
        page = await client.GetStringAsync(route);
        Assert.DoesNotContain(accepted, page);
        Assert.DoesNotContain(enabledState.Hash!, page);

        before = await SnapshotAsync(eventId);
        page = await RejectLengthAsync(page);
        using (var retained = await client.PostAsync($"{route}?handler=SignupCode", Post(page, true, null)))
            Assert.Equal(HttpStatusCode.Redirect, retained.StatusCode);
        var retainedState = await CodeStateAsync(eventId);
        Assert.Equal(enabledState with { EventVersion = 3, FormVersion = 4, Audits = 2 }, retainedState);
        Assert.Equal(1, hasher.HashCalls);

        page = await client.GetStringAsync(route);
        // Disabling clears the stored protection even if the submitted field contains an otherwise valid value.
        using (var disabled = await client.PostAsync($"{route}?handler=SignupCode", Post(page, false, accepted)))
            Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);
        Assert.Equal(new CodeState(false, null, 4, 6, 3), await CodeStateAsync(eventId));
        Assert.Equal(1, hasher.HashCalls);
        await using var verify = new ApplicationDbContext(options);
        var audits = await verify.AuditEntries.Where(item => item.EventId == eventId).OrderBy(item => item.OccurredAt).ToListAsync();
        var auditJson = JsonSerializer.Serialize(audits);
        Assert.DoesNotContain(accepted, auditJson);
        Assert.DoesNotContain(tooLong, auditJson);
        Assert.DoesNotContain(enabledState.Hash!, auditJson);
        Assert.Equal("disabled,enabled,retained", string.Join(",", audits.Select(item => JsonDocument.Parse(item.Details!).RootElement.GetProperty("changeKind").GetString()).Order()));

        async Task<string> RejectLengthAsync(string inputPage)
        {
            var callsBefore = hasher.HashCalls;
            using var rejected = await client.PostAsync($"{route}?handler=SignupCode", Post(inputPage, true, tooLong));
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            var html = await rejected.Content.ReadAsStringAsync();
            var error = Regex.Match(html, "<span[^>]*id=\"signup-code-error\"[^>]*>(.*?)</span>", RegexOptions.Singleline).Groups[1].Value;
            Assert.Contains("maximum length of 100", error);
            Assert.Contains("aria-describedby=\"signup-code-help signup-code-error\"", html);
            Assert.Contains("checked=\"checked\"", InputTag(html, "SignupCode_RequireSignupCode"));
            Assert.DoesNotContain("hidden", Regex.Match(html, "<div class=\"signup-code-control\"[^>]*>").Value);
            Assert.DoesNotContain(tooLong, html);
            Assert.Equal(string.Empty, InputValue(html, "SignupCode_NewSignupCode"));
            Assert.Equal(InputValue(inputPage, "SignupCode_Version"), InputValue(html, "SignupCode_Version"));
            Assert.DoesNotContain("field-validation-error", Regex.Match(html, "<span[^>]*data-valmsg-for=\"SignupAdministration.ParticipantCap\"[^>]*>").Value);
            Assert.Equal(callsBefore, hasher.HashCalls);
            Assert.Equal(before, await SnapshotAsync(eventId));
            return html;
        }
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("member")]
    [InlineData("locked")]
    [InlineData("stale")]
    public async Task GuardedOverlongPostLeavesCodeVersionsAndAuditUnchanged(string guard)
    {
        var (admin, eventId) = await SeedAsync();
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var route = $"/Admin/Events/Participants/{eventId}";
        string page;
        if (guard == "anonymous") page = await client.GetStringAsync("/Account/Login");
        else
        {
            await LoginAsync(client, admin.LoginName);
            page = await client.GetStringAsync(route);
        }
        if (guard != "anonymous")
        {
            await using var db = new ApplicationDbContext(options);
            if (guard == "member")
                (await db.Accounts.SingleAsync(item => item.Id == admin.Id)).SetGlobalRole(GlobalRole.User);
            else
            {
                var bingoEvent = await db.Events.SingleAsync(item => item.Id == eventId);
                if (guard == "locked") bingoEvent.SetDraftLocked(true, Now);
                if (guard == "stale") bingoEvent.AdvanceVersion();
            }
            await db.SaveChangesAsync();
        }
        var before = await SnapshotAsync(eventId);
        using var rejected = await client.PostAsync($"{route}?handler=SignupCode", Post(page, true, new string('z', 101), guard == "locked" ? (await CodeStateAsync(eventId)).EventVersion : null));
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        if (guard == "anonymous" || guard == "member")
            Assert.Contains("/Account/", rejected.Headers.Location!.OriginalString);
        else
        {
            var html = await client.GetStringAsync(rejected.Headers.Location);
            Assert.Contains(guard == "locked" ? "Signup settings are locked because the draft has started or this event has moved on." : "This event changed while you were editing it", html);
        }
        Assert.Equal(before, await SnapshotAsync(eventId));
        Assert.Equal(0, hasher.HashCalls);
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .ConfigureServices(services =>
        {
            services.RemoveAll<ISecretHasher>();
            services.AddSingleton<ISecretHasher>(hasher);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock());
        }));

    private async Task<(Account Admin, Guid EventId)> SeedAsync()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "code-admin", "CODE-ADMIN", Now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "synthetic-test-password"), false, Now, incrementVersion: false);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Code validation", $"code-validation-{Guid.NewGuid():N}", "UTC", admin.Id, Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, Now);
        db.AddRange(admin, bingoEvent, form);
        await db.SaveChangesAsync();
        return (admin, bingoEvent.Id);
    }

    private async Task<string> SnapshotAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new
        {
            Event = await db.Events.AsNoTracking().SingleAsync(item => item.Id == eventId),
            Form = await db.SignupForms.AsNoTracking().SingleAsync(item => item.EventId == eventId),
            Audits = await db.AuditEntries.AsNoTracking().Where(item => item.EventId == eventId).OrderBy(item => item.Id).ToListAsync()
        });
    }

    private async Task<CodeState> CodeStateAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        var bingoEvent = await db.Events.AsNoTracking().SingleAsync(item => item.Id == eventId);
        var form = await db.SignupForms.AsNoTracking().SingleAsync(item => item.EventId == eventId);
        Assert.Equal(bingoEvent.RequireSignupCode, form.RequireSignupCode);
        Assert.Equal(bingoEvent.SignupCodeHash, form.SignupCodeHash);
        return new(bingoEvent.RequireSignupCode, bingoEvent.SignupCodeHash, bingoEvent.Version, form.Version,
            await db.AuditEntries.CountAsync(item => item.EventId == eventId));
    }

    private static async Task LoginAsync(HttpClient client, string login)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = login, ["Input.Password"] = "synthetic-test-password",
            ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static FormUrlEncodedContent Post(string page, bool required, string? code, long? expectedVersion = null) => new(new Dictionary<string, string>
    {
        ["SignupCode.RequireSignupCode"] = required ? "true" : "false",
        ["SignupCode.NewSignupCode"] = code ?? string.Empty,
        ["SignupCode.Version"] = expectedVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? (InputValue(page, "SignupCode_Version") is { Length: > 0 } version ? version : "0"),
        ["SignupAdministration.ParticipantCap"] = "0",
        ["__RequestVerificationToken"] = Token(page)
    });

    private static string Token(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private static string InputTag(string page, string id) => Regex.Match(page, $"<input(?=[^>]*\\bid=\"{Regex.Escape(id)}\")[^>]*>").Value;
    private static string InputValue(string page, string id) => WebUtility.HtmlDecode(Regex.Match(InputTag(page, id), "value=\"([^\"]*)\"").Groups[1].Value);
    private sealed record CodeState(bool Required, string? Hash, long EventVersion, int FormVersion, int Audits);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class CountingHasher : ISecretHasher
    {
        private readonly SecretHasher inner = new();
        public int HashCalls { get; private set; }
        public string Hash(string value) { HashCalls++; return inner.Hash(value); }
        public bool Verify(string value, string encodedHash) => inner.Verify(value, encodedHash);
    }
}
