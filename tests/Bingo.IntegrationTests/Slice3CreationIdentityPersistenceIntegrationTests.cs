using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3CreationIdentityPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice3_creation_identity")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly DateTimeOffset now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task CreationHttpRecoversInvalidSchedulesAndRetainsInputsWithoutResidue()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "create-admin", "CREATE-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "create-test-password"), false, now, incrementVersion: false);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Accounts.Add(admin);
            await setup.SaveChangesAsync();
        }
        var storage = new MemoryStorage();
        var competition = new StubCompetitionClient(now.AddDays(2), now.AddDays(3));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.AddSingleton<IEvidenceStorage>(storage);
                services.AddSingleton<IWiseOldManCompetitionClient>(competition);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = admin.LoginName,
            ["Input.Password"] = "create-test-password",
            ["__RequestVerificationToken"] = Token(loginPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var createPage = await client.GetStringAsync("/Admin/Events/Create");
        var token = Token(createPage);
        var baseline = new Dictionary<string, string>
        {
            ["Input.Name"] = "Retained draft",
            ["Input.Timezone"] = "UTC",
            ["Input.Description"] = "Retained description",
            ["Input.ParticipantCap"] = "20",
            ["Input.WaitingListEnabled"] = "true",
            ["Input.CustomQuestions[0].Label"] = "Retained question",
            ["Input.CustomQuestions[0].Type"] = "Text"
        };
        foreach (var (field, message) in new[]
        {
            ("SignupOpensLocal", "Signup opening must be in the future."),
            ("SignupClosesLocal", "Signup closing must be in the future."),
            ("DraftLocal", "Draft time must be in the future."),
            ("EventStartsLocal", "Event start must be in the future."),
            ("EventEndsLocal", "Event end must be in the future.")
        })
        {
            await InvalidAsync(new() { [$"Input.{field}"] = "2026-07-27T12:00" }, field, message);
        }
        await InvalidAsync(new() { ["Input.SignupClosesLocal"] = "2026-07-30T12:00", ["Input.EventStartsLocal"] = "2026-07-29T12:00" }, "SignupClosesLocal", "Signup closing must be no later than event start.");
        await InvalidAsync(new() { ["Input.SignupOpensLocal"] = "2026-07-29T12:00", ["Input.SignupClosesLocal"] = "2026-07-29T12:00" }, "SignupClosesLocal", "Signup closing must be after signup opening.");
        await InvalidAsync(new() { ["Input.EventStartsLocal"] = "2026-07-29T12:00", ["Input.EventEndsLocal"] = "2026-07-28T12:00" }, "EventEndsLocal", "Event end must be after event start.");
        competition.Starts = now.AddDays(-1);
        await InvalidAsync(new() { ["Input.CompetitionId"] = "123" }, "CompetitionId", "Event start must be in the future.");
        competition.Starts = now.AddDays(2);
        competition.Ends = now;
        await InvalidAsync(new() { ["Input.CompetitionId"] = "123" }, "CompetitionId", "Event end must be in the future.");
        competition.Ends = now.AddDays(1);
        await InvalidAsync(new() { ["Input.CompetitionId"] = "123" }, "CompetitionId", "Event end must be after event start.");
        competition.Ends = now.AddDays(3);
        await InvalidAsync(new() { ["Input.CompetitionId"] = "123", ["Input.SignupClosesLocal"] = "2026-07-30T12:00" }, "SignupClosesLocal", "Signup closing must be no later than event start.");
        Assert.Equal(0, storage.StoreCalls);

        using (var minimal = await PostAsync(new() { ["Input.Name"] = "HTTP minimal", ["Input.Timezone"] = "UTC" }, banner: false))
            Assert.Equal(HttpStatusCode.Redirect, minimal.StatusCode);
        var valid = new Dictionary<string, string>(baseline)
        {
            ["Input.CompetitionId"] = "123",
            ["Input.SignupOpensLocal"] = "2026-07-28T12:00",
            ["Input.SignupClosesLocal"] = "2026-07-29T12:00",
            ["Input.DraftLocal"] = "2026-07-29T12:00"
        };
        using (var created = await PostAsync(valid, banner: true)) Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(2, await verify.Events.CountAsync());
        var saved = await verify.Events.SingleAsync(item => item.Name == baseline["Input.Name"]);
        Assert.Equal(competition.Starts, saved.EventStartsAt);
        Assert.Equal(competition.Ends, saved.EventEndsAt);
        Assert.Equal(EventState.Draft, saved.State);
        Assert.Equal(2, await verify.SignupForms.CountAsync());
        Assert.Equal(7, await verify.SignupQuestions.CountAsync());
        Assert.Equal(3, await verify.AuditEntries.CountAsync());
        Assert.Single(await verify.EventCompetitionSynchronizations.ToListAsync());
        Assert.Single(await verify.EventBannerAssets.ToListAsync());
        Assert.Equal(1, storage.FileCount);

        async Task InvalidAsync(Dictionary<string, string> values, string field, string message)
        {
            var posted = new Dictionary<string, string>(baseline);
            foreach (var pair in values) posted[pair.Key] = pair.Value;
            using var response = await PostAsync(posted, banner: true);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains(message, html);
            Assert.Contains($"data-valmsg-for=\"Input.{field}\"", html);
            Assert.Contains("Retained draft", html);
            Assert.Contains("Retained description", html);
            Assert.Contains("Retained question", html);
            foreach (var value in values.Values) Assert.Contains(value, html);
            await using var db = new ApplicationDbContext(options);
            Assert.Empty(await db.Events.ToListAsync());
            Assert.Empty(await db.SignupForms.ToListAsync());
            Assert.Empty(await db.SignupQuestions.ToListAsync());
            Assert.Empty(await db.AuditEntries.ToListAsync());
            Assert.Empty(await db.EventCompetitionSynchronizations.ToListAsync());
            Assert.Empty(await db.EventBannerAssets.ToListAsync());
            Assert.Equal(0, storage.FileCount);
        }
        Task<HttpResponseMessage> PostAsync(Dictionary<string, string> values, bool banner)
        {
            var form = new MultipartFormDataContent();
            foreach (var pair in values) form.Add(new StringContent(pair.Value), pair.Key);
            form.Add(new StringContent(token), "__RequestVerificationToken");
            if (banner) form.Add(new ByteArrayContent([1, 2, 3]), "Input.Banner", "test-banner.png");
            return client.PostAsync("/Admin/Events/Create", form);
        }
        static string Token(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    [Fact]
    public async Task CreationAllocatesDistinctAutomaticSlugsAndPreservesExplicitConflicts()
    {
        await using var db = new ApplicationDbContext(options);
        var actor = Guid.NewGuid();
        foreach (var name in new[] { "Duplicate display name", "Duplicate display name", new string('a', 50), new string('a', 50) })
        {
            var page = Creation(db, new MemoryStorage(), actor, new() { Name = name, Timezone = "UTC" });
            Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(CancellationToken.None));
        }
        var before = await db.Events.AsNoTracking().OrderBy(item => item.Slug).Select(item => new { item.Id, item.Slug }).ToListAsync();
        Assert.Equal(4, before.Select(item => item.Slug).Distinct().Count());
        Assert.Contains(before, item => item.Slug == "duplicate-display-name");
        Assert.Contains(before, item => item.Slug == "duplicate-display-name-2");
        Assert.All(before, item => Assert.InRange(item.Slug.Length, 1, 120));
        var explicitConflict = Creation(db, new MemoryStorage(), actor, new() { Name = "Another display name", Slug = "duplicate-display-name", Timezone = "UTC" });
        Assert.IsType<PageResult>(await explicitConflict.OnPostAsync(CancellationToken.None));
        Assert.Contains(explicitConflict.ModelState["Input.Slug"]!.Errors, error => error.ErrorMessage == "That event link is already in use.");
        Assert.Equal(before, await db.Events.AsNoTracking().OrderBy(item => item.Slug).Select(item => new { item.Id, item.Slug }).ToListAsync());
        Assert.Equal(4, await db.SignupForms.CountAsync());
        Assert.Equal(12, await db.SignupQuestions.CountAsync());
        Assert.Equal(4, await db.AuditEntries.CountAsync());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CreationSlugRaceRollsBackUploadsAndRetriesOnlyAutomaticAllocation(bool automatic, bool exhaustRetries)
    {
        var actor = Guid.NewGuid();
        var storage = new MemoryStorage
        {
            BeforeStore = async call =>
            {
                if (call > 1 && !exhaustRetries) return;
                await using var rivalDb = new ApplicationDbContext(options);
                var rival = Creation(rivalDb, new MemoryStorage(), actor, new()
                {
                    Name = "Concurrent winner",
                    Timezone = "UTC",
                    Slug = call == 1 ? "racing-draft" : $"racing-draft-{call}"
                });
                Assert.IsType<RedirectToPageResult>(await rival.OnPostAsync(CancellationToken.None));
            }
        };
        await using var db = new ApplicationDbContext(options);
        var page = Creation(db, storage, actor, new() { Name = "Racing draft", Slug = automatic ? null : "racing-draft", Timezone = "UTC", Banner = Upload("race.png") });
        var result = await page.OnPostAsync(CancellationToken.None);
        var succeeded = automatic && !exhaustRetries;
        if (succeeded) Assert.IsType<RedirectToPageResult>(result);
        else
        {
            Assert.IsType<PageResult>(result);
            Assert.Contains(page.ModelState["Input.Slug"]!.Errors, error => error.ErrorMessage == (automatic
                ? "An event link could not be allocated. Try creating the event again." : "That event link is already in use."));
        }
        await using var verify = new ApplicationDbContext(options);
        var expectedCount = exhaustRetries ? 3 : succeeded ? 2 : 1;
        Assert.Equal(expectedCount, await verify.Events.CountAsync());
        Assert.Equal(expectedCount, await verify.SignupForms.CountAsync());
        Assert.Equal(expectedCount * 3, await verify.SignupQuestions.CountAsync());
        Assert.Equal(expectedCount, await verify.AuditEntries.CountAsync());
        Assert.Equal(succeeded ? 1 : 0, await verify.EventBannerAssets.CountAsync());
        Assert.Equal(succeeded ? 1 : 0, storage.FileCount);
        Assert.Equal(exhaustRetries ? 3 : succeeded ? 2 : 1, storage.StoreCalls);
        Assert.Equal(exhaustRetries ? 3 : 1, storage.DeleteCalls);
        Assert.Equal("racing-draft", (await verify.Events.SingleAsync(item => item.Slug == "racing-draft")).Slug);
        if (succeeded) Assert.Equal("racing-draft-2", (await verify.Events.SingleAsync(item => item.Name == "Racing draft")).Slug);
    }

    [Fact]
    public async Task CreationHandlerCommitsMinimalDraftAndAuditAndRejectsPartialOptionalInput()
    {
        var actor = Guid.NewGuid();
        var storage = new MemoryStorage();
        await using var db = new ApplicationDbContext(options);
        var success = Creation(db, storage, actor, new CreateModel.CreateInput { Name = "Minimal draft", Timezone = "Europe/Copenhagen" });

        Assert.IsType<RedirectToPageResult>(await success.OnPostAsync(CancellationToken.None));
        var created = await db.Events.SingleAsync();
        Assert.Equal(EventState.Draft, created.State);
        Assert.Null(created.Description);
        Assert.Null(created.ParticipantCap);
        Assert.False(created.ScheduledSignupOpeningEnabled);
        Assert.Equal(created.Id, (await db.AuditEntries.SingleAsync()).EventId);

        var invalid = Creation(db, storage, actor, new CreateModel.CreateInput { Name = "Partial schedule", Timezone = "Europe/Copenhagen", SignupOpensLocal = "2026-08-01" });
        Assert.IsType<PageResult>(await invalid.OnPostAsync(CancellationToken.None));
        Assert.True(invalid.ModelState.ContainsKey("Input.SignupOpensLocal"));
        Assert.Equal("2026-08-01", invalid.Input.SignupOpensLocal);
        Assert.Equal(1, await db.Events.CountAsync());

        var invalidDimensions = Creation(db, storage, actor, new CreateModel.CreateInput { Name = "Oversized board", Timezone = "Europe/Copenhagen", ExpectedBoardRows = 9, ExpectedBoardColumns = 5 });
        Assert.IsType<PageResult>(await invalidDimensions.OnPostAsync(CancellationToken.None));
        Assert.True(invalidDimensions.ModelState.ContainsKey("Input.ExpectedBoardRows"));
        Assert.Equal(9, invalidDimensions.Input.ExpectedBoardRows);
        Assert.Equal(1, await db.Events.CountAsync());

        var fiveMinuteFallback = Creation(db, storage, actor, new CreateModel.CreateInput
        {
            Name = "Five minute fallback",
            Timezone = "UTC",
            Description = "Public event description",
            ParticipantCap = 20,
            WaitingListEnabled = true,
            SignupOpensLocal = "2026-08-01T10:05",
            SignupClosesLocal = "2026-08-01T11:10",
            DraftLocal = "2026-08-01T11:15",
            EventStartsLocal = "2026-08-01T12:20",
            EventEndsLocal = "2026-08-01T13:25"
        });
        Assert.IsType<RedirectToPageResult>(await fiveMinuteFallback.OnPostAsync(CancellationToken.None));
        var fallbackEvent = await db.Events.SingleAsync(x => x.Name == "Five minute fallback");
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 10, 5, 0, TimeSpan.Zero), fallbackEvent.SignupOpensAt);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 11, 15, 0, TimeSpan.Zero), fallbackEvent.DraftAt);
        Assert.True(fallbackEvent.ScheduledSignupOpeningEnabled);

        var due = new FixedTimeProvider(new DateTimeOffset(2026, 8, 1, 10, 5, 0, TimeSpan.Zero));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DiscordAuthentication:ClientId"] = "test",
            ["DiscordAuthentication:ClientSecret"] = "test"
        }).Build();
        await new EventLifecycleService(db, new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), due), due).ProcessDueAsync();
        Assert.Equal(EventState.SignupOpen, (await db.Events.SingleAsync(x => x.Id == fallbackEvent.Id)).State);
    }

    [Fact]
    public async Task CreationAllowsOnlyCopenhagenAndUtcTimezones()
    {
        var actor = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);

        foreach (var timezone in new[] { "Europe/Copenhagen", "UTC" })
        {
            var creation = Creation(db, new MemoryStorage(), actor, new CreateModel.CreateInput
            {
                Name = $"Supported {timezone}",
                Timezone = timezone
            });

            Assert.IsType<RedirectToPageResult>(await creation.OnPostAsync(CancellationToken.None));
        }

        var invalid = Creation(db, new MemoryStorage(), actor, new CreateModel.CreateInput
        {
            Name = "Unsupported timezone",
            Timezone = "Europe/London"
        });

        Assert.IsType<PageResult>(await invalid.OnPostAsync(CancellationToken.None));
        Assert.True(invalid.ModelState.ContainsKey("Input.Timezone"));
        Assert.Equal(2, await db.Events.CountAsync());
    }

    [Fact]
    public async Task CreationDefaultsUseZeroBasedSignupQuestionsAndOpenUntilAQuestionIsActuallyMalformed()
    {
        var actor = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);
        var creation = Creation(db, new MemoryStorage(), actor, new CreateModel.CreateInput
        {
            Name = "Default signup readiness",
            Timezone = "UTC",
            Description = "A public description for signup readiness.",
            ParticipantCap = 20,
            SignupClosesLocal = "2026-07-29T12:00",
            EventStartsLocal = "2026-07-30T12:00",
            EventEndsLocal = "2026-08-01T12:00"
        });

        Assert.IsType<RedirectToPageResult>(await creation.OnPostAsync(CancellationToken.None));
        var bingoEvent = await db.Events.SingleAsync(item => item.Name == "Default signup readiness");
        var questions = await db.SignupQuestions.Where(question => question.EventId == bingoEvent.Id && question.Active).OrderBy(question => question.Position).ToListAsync();
        Assert.Collection(questions,
            question =>
            {
                Assert.Equal("primary_regular_account", question.Key);
                Assert.Equal(SignupQuestionType.Account, question.Type);
                Assert.True(question.Required);
                Assert.Equal(SignupSystemField.PrimaryRegularAccount, question.SystemField);
                Assert.Equal(EventCharacterRole.Playing, question.AccountAnswerRole);
                Assert.Equal(0, question.Position);
            },
            question =>
            {
                Assert.Equal("captain_volunteer", question.Key);
                Assert.Equal(SignupQuestionType.YesNo, question.Type);
                Assert.False(question.Required);
                Assert.Equal(SignupSystemField.CaptainVolunteer, question.SystemField);
                Assert.Equal(1, question.Position);
            },
            question =>
            {
                Assert.Equal(SignupQuestion.CoCaptainKey, question.Key);
                Assert.Equal(SignupQuestionType.Text, question.Type);
                Assert.False(question.Required);
                Assert.Equal(SignupSystemField.CoCaptainName, question.SystemField);
                Assert.Equal(2, question.Position);
            });

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DiscordAuthentication:ClientId"] = "test", ["DiscordAuthentication:ClientSecret"] = "test" }).Build();
        var readiness = new EventReadinessEvaluator(db, configuration);
        var beforeOpen = await readiness.GetSignupReadinessAsync(bingoEvent.Id, SignupOpeningMode.OpenNow, now);
        Assert.DoesNotContain(beforeOpen!.Blockers, blocker => blocker.Code == "SIGNUP_QUESTIONS_INVALID");
        var lifecycle = new EventSignupLifecycleService(db, readiness, new FixedTimeProvider(now));
        var opened = await lifecycle.OpenAsync(bingoEvent.Id, bingoEvent.Version, acknowledgedWarningCodes: beforeOpen.Warnings.Select(warning => warning.Code).ToArray(), acceptProposedClose: true, actor: new LifecycleActor(actor, "admin"));
        Assert.True(opened.Succeeded, opened.Error);
        Assert.Equal(EventState.SignupOpen, await db.Events.Where(item => item.Id == bingoEvent.Id).Select(item => item.State).SingleAsync());

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE signup_questions SET position = {-1} WHERE id = {questions[0].Id}");
        db.ChangeTracker.Clear();
        var malformed = await readiness.GetSignupReadinessAsync(bingoEvent.Id, SignupOpeningMode.OpenNow, now);
        Assert.Contains(malformed!.Blockers, blocker => blocker.Code == "SIGNUP_QUESTIONS_INVALID");
    }

    [Fact]
    public async Task IdentityHandlerPreservesUtcAndMapsLocksConcurrencyAndSlugCollisionsToSafeFeedback()
    {
        var actor = Guid.NewGuid();
        var eventId = await SeedEventAsync("identity-target", actor);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            var edit = Identity(db, new MemoryStorage(), actor, new IdentityModel.InputModel { Name = "Renamed", Slug = "renamed", Description = "Description", Timezone = "Europe/Copenhagen", Version = item.Version });
            Assert.IsType<RedirectToPageResult>(await edit.OnPostAsync(eventId, CancellationToken.None));
        }

        DateTimeOffset originalStart;
        long publicVersion;
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            item.MarkFirstPublic(now);
            item.ConfigureInitialSchedule(null, null, null, now.AddDays(1), now.AddDays(2), null);
            item.OpenSignups();
            item.CloseSignups();
            item.StartEvent(now);
            await db.SaveChangesAsync();
            originalStart = item.EventStartsAt!.Value;
            publicVersion = item.Version;
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var liveIdentity = Identity(db, new MemoryStorage(), actor, new IdentityModel.InputModel { Name = "Live update", Slug = "renamed", Description = "Changed", Timezone = "UTC", Version = publicVersion, ConfirmTimezoneChange = true });
            Assert.IsType<PageResult>(await liveIdentity.OnPostAsync(eventId, CancellationToken.None));
            Assert.True(liveIdentity.ModelState.ContainsKey(string.Empty));
        }

        var editableId = await SeedEventAsync("identity-editable", actor);
        await using (var db = new ApplicationDbContext(options))
        {
            var live = await db.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal("Europe/Copenhagen", live.Timezone);
            Assert.Equal("Renamed", live.Name);
            Assert.Equal(originalStart, live.EventStartsAt);
            var editable = await db.Events.SingleAsync(x => x.Id == editableId);
            editable.MarkFirstPublic(now);
            await db.SaveChangesAsync();
            var locked = Identity(db, new MemoryStorage(), actor, new IdentityModel.InputModel { Name = editable.Name, Slug = "cannot-change", Description = editable.Description, Timezone = editable.Timezone, Version = editable.Version });
            Assert.IsType<PageResult>(await locked.OnPostAsync(editableId, CancellationToken.None));
            Assert.True(locked.ModelState.ContainsKey("Input.Slug"));

            db.Events.Add(new BingoEvent(Guid.NewGuid(), "Other", "taken-link", "UTC", actor, now));
            await db.SaveChangesAsync();
            editable = await db.Events.SingleAsync(x => x.Id == editableId);
            var collision = Identity(db, new MemoryStorage(), actor, new IdentityModel.InputModel { Name = editable.Name, Slug = "taken-link", Description = editable.Description, Timezone = editable.Timezone, Version = editable.Version });
            Assert.IsType<PageResult>(await collision.OnPostAsync(editableId, CancellationToken.None));
            Assert.True(collision.ModelState.ContainsKey("Input.Slug"));
        }

        var staleEventId = await SeedEventAsync("identity-stale", actor);
        await using (var staleDb = new ApplicationDbContext(options))
        {
            var staleVersion = (await staleDb.Events.AsNoTracking().SingleAsync(x => x.Id == staleEventId)).Version;
            await using (var winnerDb = new ApplicationDbContext(options))
            {
                var winner = await winnerDb.Events.SingleAsync(x => x.Id == staleEventId);
                winner.UpdateIdentity("Winner", winner.Slug, winner.Description, "Europe/Copenhagen");
                await winnerDb.SaveChangesAsync();
            }
            var stale = Identity(staleDb, new MemoryStorage(), actor, new IdentityModel.InputModel { Name = "Stale", Slug = "renamed", Description = "Description", Timezone = "UTC", Version = staleVersion });
            Assert.IsType<PageResult>(await stale.OnPostAsync(staleEventId, CancellationToken.None));
            Assert.True(stale.ModelState.ContainsKey(string.Empty));
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var raceTarget = new BingoEvent(Guid.NewGuid(), "Race target", "race-target", "UTC", actor, now);
            var rival = new BingoEvent(Guid.NewGuid(), "Race rival", "race-rival", "UTC", actor, now);
            db.Events.AddRange(raceTarget, rival);
            await db.SaveChangesAsync();
            await ForceSlugRaceAsync(db, raceTarget.Id, rival.Id);
            try
            {
                var raced = Identity(db, new MemoryStorage(), actor, new IdentityModel.InputModel { Name = raceTarget.Name, Slug = "race-winner", Timezone = "UTC", Version = raceTarget.Version });
                Assert.IsType<PageResult>(await raced.OnPostAsync(raceTarget.Id, CancellationToken.None));
                Assert.True(raced.ModelState.ContainsKey("Input.Slug"));
                Assert.Equal("race-winner", raced.Input.Slug);
                Assert.Equal("race-target", (await db.Events.AsNoTracking().SingleAsync(x => x.Id == raceTarget.Id)).Slug);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS slice3_force_slug_race ON events; DROP FUNCTION IF EXISTS slice3_force_slug_race();");
            }
        }
    }

    [Fact]
    public async Task BannerHandlersServeReplaceRemoveAndRollbackWithoutChangingTheActiveReference()
    {
        var actor = Guid.NewGuid();
        var eventId = await SeedEventAsync("banner-target", actor);
        var storage = new MemoryStorage();
        Guid firstBanner;
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            var add = Identity(db, storage, actor, new IdentityModel.InputModel { Name = item.Name, Slug = item.Slug, Timezone = item.Timezone, Version = item.Version, Banner = Upload("first.png") });
            Assert.IsType<RedirectToPageResult>(await add.OnPostAsync(eventId, CancellationToken.None));
            firstBanner = (await db.Events.SingleAsync(x => x.Id == eventId)).BannerAssetId!.Value;
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET banner_asset_id = {Guid.NewGuid()} WHERE id = {eventId}"));
            var response = await new BannerModel(db, storage).OnGetAsync(eventId, CancellationToken.None);
            var file = Assert.IsType<FileStreamResult>(response);
            Assert.Equal("image/png", file.ContentType);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            var replace = Identity(db, storage, actor, new IdentityModel.InputModel { Name = item.Name, Slug = item.Slug, Timezone = item.Timezone, Version = item.Version, Banner = Upload("second.png") });
            Assert.IsType<RedirectToPageResult>(await replace.OnPostAsync(eventId, CancellationToken.None));
            var active = (await db.Events.SingleAsync(x => x.Id == eventId)).BannerAssetId!.Value;
            Assert.NotEqual(firstBanner, active);
            Assert.NotNull((await db.EventBannerAssets.SingleAsync(x => x.Id == firstBanner)).ReplacedAt);

            var failing = Identity(db, new MemoryStorage { ReturnOversizedFilename = true }, actor, new IdentityModel.InputModel { Name = item.Name, Slug = item.Slug, Timezone = item.Timezone, Version = (await db.Events.SingleAsync(x => x.Id == eventId)).Version, Banner = Upload("failure.png") });
            Assert.IsType<PageResult>(await failing.OnPostAsync(eventId, CancellationToken.None));
            Assert.Equal(active, (await db.Events.SingleAsync(x => x.Id == eventId)).BannerAssetId);

            var remove = Identity(db, storage, actor, new IdentityModel.InputModel { Name = item.Name, Slug = item.Slug, Timezone = item.Timezone, Version = (await db.Events.SingleAsync(x => x.Id == eventId)).Version, RemoveBanner = true });
            Assert.IsType<RedirectToPageResult>(await remove.OnPostAsync(eventId, CancellationToken.None));
            Assert.Null((await db.Events.SingleAsync(x => x.Id == eventId)).BannerAssetId);
            Assert.IsType<NotFoundResult>(await new BannerModel(db, storage).OnGetAsync(eventId, CancellationToken.None));
        }
    }

    [Fact]
    public async Task ScheduleHandlerLoadsMachineValuesAndPreservesUntouchedInstants()
    {
        var actor = Guid.NewGuid();
        var eventId = await SeedEventAsync("schedule-round-trip", actor);
        var opens = new DateTimeOffset(2026, 8, 1, 8, 10, 0, TimeSpan.Zero);
        var closes = new DateTimeOffset(2026, 8, 2, 9, 20, 0, TimeSpan.Zero);
        var draft = new DateTimeOffset(2026, 8, 3, 10, 30, 0, TimeSpan.Zero);
        var starts = new DateTimeOffset(2026, 8, 4, 11, 40, 0, TimeSpan.Zero);
        var ends = new DateTimeOffset(2026, 8, 5, 12, 50, 0, TimeSpan.Zero);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            item.UpdateIdentity(item.Name, item.Slug, "Public description", "Europe/Copenhagen");
            item.ConfigureSchedule(opens, closes, draft, starts, ends, 20);
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var model = Schedule(db, actor);
            Assert.IsType<PageResult>(await model.OnGetAsync(eventId, CancellationToken.None));
            Assert.Equal("2026-08-01T10:10", model.Input.SignupOpensLocal);
            Assert.Equal("2026-08-02T11:20", model.Input.SignupClosesLocal);
            Assert.Equal("2026-08-03T12:30", model.Input.DraftLocal);
            Assert.Equal("2026-08-04T13:40", model.Input.EventStartsLocal);
            Assert.Equal("2026-08-05T14:50", model.Input.EventEndsLocal);
            model.Input.EventStartsLocal = "not-a-date";
            Assert.IsType<PageResult>(await model.OnPostAsync(eventId, CancellationToken.None));
            Assert.True(model.ModelState.ContainsKey("Input.EventStartsLocal"));
            Assert.Equal("not-a-date", model.Input.EventStartsLocal);
            Assert.Equal("2026-08-01T10:10", model.Input.SignupOpensLocal);
            Assert.Equal("2026-08-02T11:20", model.Input.SignupClosesLocal);
            Assert.Equal("2026-08-03T12:30", model.Input.DraftLocal);
            Assert.Equal("2026-08-05T14:50", model.Input.EventEndsLocal);
            Assert.DoesNotContain([model.Input.SignupOpensLocal, model.Input.SignupClosesLocal, model.Input.DraftLocal, model.Input.EventEndsLocal], value => value?.Contains('/') == true);

            model = Schedule(db, actor);
            Assert.IsType<PageResult>(await model.OnGetAsync(eventId, CancellationToken.None));
            model.Input.EventEndsLocal = "2026-08-06T14:50";
            model.Input.ParticipantCap = 25;
            model.Input.ScheduledSignupOpeningEnabled = true;
            model.Input.ConfirmChanges = true;
            Assert.IsType<RedirectToPageResult>(await model.OnPostAsync(eventId, CancellationToken.None));
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var saved = await db.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(opens, saved.SignupOpensAt);
            Assert.Equal(closes, saved.SignupClosesAt);
            Assert.Equal(draft, saved.DraftAt);
            Assert.Equal(starts, saved.EventStartsAt);
            Assert.Equal(new DateTimeOffset(2026, 8, 6, 12, 50, 0, TimeSpan.Zero), saved.EventEndsAt);
            Assert.Equal(25, saved.ParticipantCap);
            Assert.True(saved.ScheduledSignupOpeningEnabled);

            var model = Schedule(db, actor);
            Assert.IsType<PageResult>(await model.OnGetAsync(eventId, CancellationToken.None));
            Assert.Equal("2026-08-01T10:10", model.Input.SignupOpensLocal);
            Assert.Equal("2026-08-02T11:20", model.Input.SignupClosesLocal);
            Assert.Equal("2026-08-03T12:30", model.Input.DraftLocal);
            Assert.Equal("2026-08-04T13:40", model.Input.EventStartsLocal);
            Assert.Equal("2026-08-06T14:50", model.Input.EventEndsLocal);
            model.Input.SignupOpensLocal = null;
            Assert.IsType<RedirectToPageResult>(await model.OnPostAsync(eventId, CancellationToken.None));
            Assert.Null((await db.Events.SingleAsync(x => x.Id == eventId)).SignupOpensAt);
            Assert.False((await db.Events.SingleAsync(x => x.Id == eventId)).ScheduledSignupOpeningEnabled);
        }

        var canonicalEventId = await SeedEventAsync("canonical-schedule-post", actor);
        await using (var db = new ApplicationDbContext(options))
        {
            var model = Schedule(db, actor);
            Assert.IsType<PageResult>(await model.OnGetAsync(canonicalEventId, CancellationToken.None));
            model.Input.EventStartsLocal = "2026-07-27T18:40";
            model.Input.EventEndsLocal = "2026-07-28T18:40";
            Assert.IsType<RedirectToPageResult>(await model.OnPostAsync(canonicalEventId, CancellationToken.None));
            var saved = await db.Events.SingleAsync(x => x.Id == canonicalEventId);
            Assert.Equal(new DateTimeOffset(2026, 7, 27, 16, 40, 0, TimeSpan.Zero), saved.EventStartsAt);
            Assert.Equal(new DateTimeOffset(2026, 7, 28, 16, 40, 0, TimeSpan.Zero), saved.EventEndsAt);
        }
    }

    [Theory]
    [InlineData("2026-03-29T02:30")]
    [InlineData("2026-10-25T02:30")]
    public async Task ScheduleHandlerRejectsInvalidOrAmbiguousLocalWallTimeWithoutChangingScheduleVersionOrAudit(string localTime)
    {
        var actor = Guid.NewGuid();
        var eventId = await SeedEventAsync("schedule-dst-rejection", actor);
        var opens = new DateTimeOffset(2026, 8, 1, 8, 10, 0, TimeSpan.Zero);
        var closes = new DateTimeOffset(2026, 8, 2, 9, 20, 0, TimeSpan.Zero);
        var draft = new DateTimeOffset(2026, 8, 3, 10, 30, 0, TimeSpan.Zero);
        var starts = new DateTimeOffset(2026, 8, 4, 11, 40, 0, TimeSpan.Zero);
        var ends = new DateTimeOffset(2026, 8, 5, 12, 50, 0, TimeSpan.Zero);

        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            item.ConfigureSchedule(opens, closes, draft, starts, ends, 20);
            await db.SaveChangesAsync();
        }

        await using var verify = new ApplicationDbContext(options);
        var before = await verify.Events.AsNoTracking().Where(x => x.Id == eventId).Select(x => new
        {
            x.Version,
            x.SignupOpensAt,
            x.SignupClosesAt,
            x.DraftAt,
            x.EventStartsAt,
            x.EventEndsAt,
            x.SubmissionCutoffAt,
            x.ParticipantCap,
            x.ScheduledSignupOpeningEnabled
        }).SingleAsync();
        var auditCount = await verify.AuditEntries.CountAsync(x => x.EventId == eventId);
        var model = Schedule(verify, actor);

        Assert.IsType<PageResult>(await model.OnGetAsync(eventId, CancellationToken.None));
        model.Input.EventStartsLocal = localTime;

        Assert.IsType<PageResult>(await model.OnPostAsync(eventId, CancellationToken.None));
        Assert.False(model.ModelState.IsValid);
        Assert.True(model.ModelState.ContainsKey("Input.EventStartsLocal"));

        var after = await verify.Events.AsNoTracking().Where(x => x.Id == eventId).Select(x => new
        {
            x.Version,
            x.SignupOpensAt,
            x.SignupClosesAt,
            x.DraftAt,
            x.EventStartsAt,
            x.EventEndsAt,
            x.SubmissionCutoffAt,
            x.ParticipantCap,
            x.ScheduledSignupOpeningEnabled
        }).SingleAsync();
        Assert.Equal(before, after);
        Assert.Equal(auditCount, await verify.AuditEntries.CountAsync(x => x.EventId == eventId));
    }

    [Fact]
    public async Task PublicSchedulePreviewPreservesLockedSignupOpeningWhenPostOmitsIt()
    {
        var actor = Guid.NewGuid();
        var eventId = await SeedEventAsync("public-schedule-preview", actor);
        var signupOpens = now.AddDays(-2);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            item.UpdateIdentity(item.Name, item.Slug, "Public description", item.Timezone);
            item.ConfigureSchedule(signupOpens, now.AddDays(-1), null, now.AddDays(1), now.AddDays(3), 20);
            item.MarkFirstPublic(now.AddDays(-2));
            item.OpenSignups(now.AddDays(-2));
            item.CloseSignups(now.AddDays(-1));
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var model = Schedule(db, actor);
            Assert.IsType<PageResult>(await model.OnGetAsync(eventId, CancellationToken.None));
            model.Input.SignupOpensLocal = null;
            model.Input.EventEndsLocal = "2026-07-31T14:00";

            Assert.IsType<PageResult>(await model.OnPostAsync(eventId, CancellationToken.None));

            Assert.DoesNotContain(model.ChangePreview, value => value.Label == "Signup opens");
            Assert.Contains(model.ChangePreview, value => value.Label == "Event ends");
            Assert.Contains(model.ChangePreview, value => value.Label == "Submission cutoff");
            Assert.True(model.ModelState.ContainsKey("Input.ConfirmChanges"));
        }
    }

    [Fact]
    public async Task SignupSettingsHandlerDisablesWaitingListOnlyAfterConfirmingQueuedPromotion()
    {
        var actor = Guid.NewGuid();
        var eventId = await SeedEventAsync("waiting-list-settings", actor);
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        item.ConfigureSchedule(null, null, null, null, null, 10);
        item.ConfigureSignup(false, false, null);
        await db.SaveChangesAsync();
        var settings = new ParticipantsModel(db, new SignupService(db, new SecretHasher(), new FixedTimeProvider(now))) { SignupAdministration = new ParticipantsModel.SignupAdministrationInput { ParticipantCap = 10, WaitingListEnabled = true, Version = item.Version } };
        SetAdmin(settings, actor);
        Assert.IsType<RedirectToPageResult>(await settings.OnPostSignupAdministrationAsync(eventId, CancellationToken.None));
        Assert.True((await db.Events.SingleAsync(x => x.Id == eventId)).WaitingListEnabled);
        db.EventParticipants.Add(new Bingo.Domain.Signups.EventParticipant(Guid.NewGuid(), eventId, Bingo.Domain.Signups.SignupStatus.WaitingList, 1, now, Bingo.Domain.Signups.SignupSource.Website, null));
        await db.SaveChangesAsync();
        item = await db.Events.SingleAsync(x => x.Id == eventId);
        settings = new ParticipantsModel(db, new SignupService(db, new SecretHasher(), new FixedTimeProvider(now))) { SignupAdministration = new ParticipantsModel.SignupAdministrationInput { ParticipantCap = item.ParticipantCap!.Value, WaitingListEnabled = false, Version = item.Version } };
        SetAdmin(settings, actor);
        Assert.IsType<RedirectToPageResult>(await settings.OnPostSignupAdministrationAsync(eventId, CancellationToken.None));
        Assert.True((await db.Events.SingleAsync(x => x.Id == eventId)).WaitingListEnabled);

        item = await db.Events.SingleAsync(x => x.Id == eventId);
        settings = new ParticipantsModel(db, new SignupService(db, new SecretHasher(), new FixedTimeProvider(now))) { SignupAdministration = new ParticipantsModel.SignupAdministrationInput { ParticipantCap = item.ParticipantCap!.Value, WaitingListEnabled = false, Version = item.Version, ConfirmWaitingListDisablement = true } };
        SetAdmin(settings, actor);
        Assert.IsType<RedirectToPageResult>(await settings.OnPostSignupAdministrationAsync(eventId, CancellationToken.None));
        Assert.False((await db.Events.SingleAsync(x => x.Id == eventId)).WaitingListEnabled);
        Assert.Equal(SignupStatus.Confirmed, await db.EventParticipants.Where(x => x.EventId == eventId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.EventId == eventId && x.Action == "participant.promoted").ToListAsync());
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.EventId == eventId && x.Action == "event.signup_administration_updated"));
    }

    private async Task<Guid> SeedEventAsync(string slug, Guid actor)
    {
        var item = new BingoEvent(Guid.NewGuid(), slug, slug, "Europe/Copenhagen", actor, now);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(item, form, regular, captain);
        await db.SaveChangesAsync();
        return item.Id;
    }

#pragma warning disable EF1002 // Test-only DDL embeds locally generated canonical GUID values in a trigger body.
    private static Task<int> ForceSlugRaceAsync(ApplicationDbContext db, Guid targetId, Guid rivalId) => db.Database.ExecuteSqlRawAsync($"""
        CREATE OR REPLACE FUNCTION slice3_force_slug_race() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW.id = '{targetId}' THEN UPDATE events SET slug = NEW.slug WHERE id = '{rivalId}'; END IF;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER slice3_force_slug_race BEFORE UPDATE ON events FOR EACH ROW EXECUTE FUNCTION slice3_force_slug_race();
        """);
#pragma warning restore EF1002

    private CreateModel Creation(ApplicationDbContext db, IEvidenceStorage storage, Guid actor, CreateModel.CreateInput input)
    {
        var model = new CreateModel(db, new SecretHasher(), storage, new FixedTimeProvider(now)) { Input = input };
        SetAdmin(model, actor);
        return model;
    }

    private IdentityModel Identity(ApplicationDbContext db, IEvidenceStorage storage, Guid actor, IdentityModel.InputModel input)
    {
        var model = new IdentityModel(db, storage, new FixedTimeProvider(now)) { Input = input };
        SetAdmin(model, actor);
        return model;
    }

    private ScheduleModel Schedule(ApplicationDbContext db, Guid actor)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DiscordAuthentication:ClientId"] = "test", ["DiscordAuthentication:ClientSecret"] = "test" }).Build();
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var model = new ScheduleModel(db, new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now)), evaluator, new FixedTimeProvider(now));
        SetAdmin(model, actor);
        return model;
    }

    private static void SetAdmin(PageModel model, Guid actor)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Name, "admin")], "test")) };
        model.PageContext = new PageContext { HttpContext = context };
        model.TempData = new TempDataDictionary(context, new EmptyTempDataProvider());
    }

    private static FormFile Upload(string filename) => new(new MemoryStream([1, 2, 3]), 0, 3, "Input.Banner", filename) { Headers = new HeaderDictionary(), ContentType = "image/png" };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }

    private sealed class MemoryStorage : IEvidenceStorage
    {
        private readonly Dictionary<string, byte[]> files = [];
        public bool ReturnOversizedFilename { get; init; }
        public Func<int, Task>? BeforeStore { get; init; }
        public int FileCount => files.Count;
        public int StoreCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public async Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default)
        {
            StoreCalls++;
            if (BeforeStore is not null) await BeforeStore(StoreCalls);
            var key = $"{eventId:N}/{submissionId:N}.png";
            files[key] = [1, 2, 3];
            return new StoredEvidence(key, ReturnOversizedFilename ? new string('x', 300) : originalFilename, "image/png", 3, 1, 1, new string('a', 64));
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => files.TryGetValue(storageKey, out var value) ? Task.FromResult<Stream>(new MemoryStream(value)) : throw new FileNotFoundException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { DeleteCalls++; files.Remove(storageKey); return Task.CompletedTask; }
    }

    private sealed class StubCompetitionClient(DateTimeOffset starts, DateTimeOffset ends) : IWiseOldManCompetitionClient
    {
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default) => GetCompetitionAsync(competitionId, cancellationToken);

        public DateTimeOffset Starts { get; set; } = starts;
        public DateTimeOffset Ends { get; set; } = ends;
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Success, new(competitionId, "Fixture competition", Starts, Ends, null, [])));
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
