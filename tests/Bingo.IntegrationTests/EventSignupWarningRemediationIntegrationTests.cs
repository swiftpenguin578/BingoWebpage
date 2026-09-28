using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class EventSignupWarningRemediationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_signup_warning_remediation").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly DateTimeOffset now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransactionalOpeningUsesSharedConfirmationAndTreatsWarningsAsInformational(bool reopening)
    {
        var eventId = await SeedAsync(reopening, publicText: false);
        await using var db = new ApplicationDbContext(options);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DiscordAuthentication:ClientId"] = "fixture-client",
            ["DiscordAuthentication:ClientSecret"] = "fixture-secret"
        }).Build();
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var displayed = await evaluator.GetSignupReadinessAsync(eventId, reopening ? SignupOpeningMode.Reopen : SignupOpeningMode.OpenNow, now);
        Assert.NotNull(displayed);
        await AddTextAsync(eventId);
        var item = await db.Events.AsNoTracking().SingleAsync(item => item.Id == eventId);
        var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
        var actor = new LifecycleActor(item.CreatedByAccountId, "warning-admin");
        var unconfirmed = reopening
            ? await service.ReopenAsync(eventId, item.Version, [], false, actor)
            : await service.OpenAsync(eventId, item.Version, [], false, actor);
        Assert.False(unconfirmed.Succeeded);
        Assert.Equal("Confirm that you want to change the signup lifecycle.", unconfirmed.Error);
        Assert.Equal(item.State, (await db.Events.AsNoTracking().SingleAsync(item => item.Id == eventId)).State);
        Assert.Empty(await db.EventStateTransitions.ToListAsync());
        Assert.Empty(await db.AuditEntries.ToListAsync());
        var confirmed = reopening
            ? await service.ReopenAsync(eventId, item.Version, [], true, actor)
            : await service.OpenAsync(eventId, item.Version, [], true, actor);
        Assert.True(confirmed.Succeeded, confirmed.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacySignupConfirmationHandlerCannotBypassSharedConfirmation(bool reopening)
    {
        var eventId = await SeedAsync(reopening, publicText: false);
        await using var factory = Factory();
        using var client = await LoginAsync(factory);
        var route = $"/Admin/Events/Manage/{eventId}";
        var page = await client.GetStringAsync(route);
        var posted = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", InputValue(page, "__RequestVerificationToken")),
            new("EventVersion", InputValue(page, "EventVersion"))
        };

        using var rejected = await client.PostAsync(route + "?handler=ConfirmSignup", new FormUrlEncodedContent(posted));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        var item = await verify.Events.SingleAsync(item => item.Id == eventId);
        Assert.Equal(reopening ? EventState.SignupClosed : EventState.Draft, item.State);
        Assert.Empty(await verify.EventStateTransitions.ToListAsync());
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicTextWarningUsesDanishOnManualAndScheduledConfirmation(bool scheduled)
    {
        var eventId = await SeedAsync(reopening: false, publicText: true);
        await using var factory = Factory();
        using var client = await LoginAsync(factory);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da");
        var route = scheduled ? $"/Admin/Events/Schedule/{eventId}" : $"/Admin/Events/Manage/{eventId}?confirm=signup";
        var page = WebUtility.HtmlDecode(await client.GetStringAsync(route));
        Assert.Contains("Svar på tekstspørgsmål vil være offentlige i tilmeldingsoversigten.", page);
        Assert.DoesNotContain("Answers to text questions will be public on the signup table.", page);
    }

    private async Task<Guid> SeedAsync(bool reopening, bool publicText)
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "warning-admin", "WARNING-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "warning-test-password"), false, now, incrementVersion: false);
        var item = new BingoEvent(Guid.NewGuid(), "Warning fixture", "warning-fixture", "UTC", admin.Id, now);
        item.UpdateIdentity(item.Name, item.Slug, "Public fixture description", "UTC");
        item.ConfigureSchedule(now.AddHours(1), now.AddDays(1), null, now.AddDays(2), now.AddDays(3), 20);
        item.ConfigureSignup(true, false, null);
        item.ConfigureScheduledSignupOpening(true, []);
        if (reopening)
        {
            item.OpenSignups(now);
            item.CloseSignups(now);
        }
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        db.AddRange(admin, item, form,
            new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing),
            new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer));
        await db.SaveChangesAsync();
        if (publicText) await AddTextAsync(item.Id);
        return item.Id;
    }

    private async Task AddTextAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        var form = await db.SignupForms.SingleAsync(form => form.EventId == eventId);
        db.SignupQuestions.Add(new SignupQuestion(Guid.NewGuid(), form.Id, eventId, "public_text", "Public text", SignupQuestionType.Text, false, 2, null));
        form.AdvanceVersion();
        await db.SaveChangesAsync();
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .UseSetting("DiscordAuthentication:ClientId", "fixture-client").UseSetting("DiscordAuthentication:ClientSecret", "fixture-secret");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "warning-admin",
            ["Input.Password"] = "warning-test-password",
            ["__RequestVerificationToken"] = InputValue(page, "__RequestVerificationToken")
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static string InputValue(string page, string name)
        => WebUtility.HtmlDecode(Regex.Match(page, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"[^>]*>").Groups[1].Value);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
}
