using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task CreationHttpAcceptsOnlyNameAndTimezoneAndCreatesAtomicDefaultAggregate()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "create-admin", "CREATE-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "create-test-password"), false, now, incrementVersion: false);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Accounts.Add(admin);
            await setup.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
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
        using (var retired = await PostAsync(new()
        {
            ["Input.Name"] = "Retired wizard post",
            ["Input.Timezone"] = "UTC",
            ["Input.Description"] = "This must not be accepted."
        }))
        {
            Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
            var html = WebUtility.HtmlDecode(await retired.Content.ReadAsStringAsync());
            Assert.Contains("only a name and timezone", html);
        }

        await using (var empty = new ApplicationDbContext(options))
        {
            Assert.Empty(await empty.Events.ToListAsync());
            Assert.Empty(await empty.SignupForms.ToListAsync());
            Assert.Empty(await empty.SignupQuestions.ToListAsync());
            Assert.Empty(await empty.Boards.ToListAsync());
            Assert.Empty(await empty.AuditEntries.ToListAsync());
        }

        using (var created = await PostAsync(new() { ["Input.Name"] = "HTTP minimal", ["Input.Timezone"] = "UTC" }))
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.Events.SingleAsync(item => item.Name == "HTTP minimal");
        Assert.Equal(EventState.Draft, saved.State);
        Assert.Equal("UTC", saved.Timezone);
        Assert.Equal("http-minimal", saved.Slug);
        Assert.Null(saved.Description);
        Assert.Null(saved.ParticipantCap);
        Assert.Single(await verify.SignupForms.Where(form => form.EventId == saved.Id).ToListAsync());
        var questions = await verify.SignupQuestions.Where(question => question.EventId == saved.Id).OrderBy(question => question.Position).ToListAsync();
        Assert.Equal(3, questions.Count);
        Assert.Equal(["primary_regular_account", "captain_volunteer", SignupQuestion.CoCaptainKey], questions.Select(question => question.Key).ToArray());
        Assert.True(questions[1].Required);
        var board = await verify.Boards.SingleAsync(item => item.EventId == saved.Id);
        Assert.Equal(5, board.Rows);
        Assert.Equal(5, board.Columns);
        Assert.Single(await verify.AuditEntries.Where(entry => entry.EventId == saved.Id && entry.Action == "event.created").ToListAsync());

        saved.ConfigureInitialSchedule(now.AddDays(-4), now.AddDays(-3), null, now.AddDays(-1), now.AddDays(1), null);
        saved.MarkFirstPublic(now.AddDays(-4));
        saved.OpenSignups(now.AddDays(-4));
        saved.CloseSignups(now.AddDays(-3));
        await verify.SaveChangesAsync();
        var identityVersion = saved.Version;
        var identityPage = await client.GetStringAsync($"/Admin/Events/Identity/{saved.Id}");
        var identityToken = Token(identityPage);
        var identityValues = new Dictionary<string, string>
        {
            ["Input.Name"] = saved.Name,
            ["Input.Description"] = string.Empty,
            ["Input.BuyInDescription"] = string.Empty,
            ["Input.Timezone"] = "Europe/Copenhagen",
            ["Input.Version"] = identityVersion.ToString(CultureInfo.InvariantCulture),
            ["Input.TimezoneConfirmationOriginal"] = "UTC",
            ["Input.TimezoneConfirmationProposed"] = "Europe/Copenhagen"
        };
        using (var preview = await PostIdentityAsync(identityValues, identityToken))
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            var html = WebUtility.HtmlDecode(await preview.Content.ReadAsStringAsync());
            Assert.Contains("<span class=\"compare-label\" role=\"rowheader\">Signups open</span>", html);
            Assert.Contains("<span class=\"compare-label\" role=\"rowheader\">Signups close</span>", html);
            Assert.Contains("name=\"Input.Version\"", html);
            Assert.Contains("value=\"" + identityVersion.ToString(CultureInfo.InvariantCulture) + "\"", html);
            Assert.Contains("name=\"Input.TimezoneConfirmationOriginal\"", html);
            Assert.Contains("value=\"UTC\"", html);
            Assert.Contains("name=\"Input.TimezoneConfirmationProposed\"", html);
            Assert.Contains("value=\"Europe/Copenhagen\"", html);
            identityToken = Token(html);
            identityValues["Input.TimezoneConfirmationSchedule"] = Regex.Match(html, "id=\"Input_TimezoneConfirmationSchedule\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        }

        identityValues["Input.ConfirmTimezoneChange"] = "true";
        using (var confirmed = await PostIdentityAsync(identityValues, identityToken))
            Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        verify.ChangeTracker.Clear();
        saved = await verify.Events.SingleAsync(item => item.Id == saved.Id);
        Assert.Equal("Europe/Copenhagen", saved.Timezone);

        var forgedSlugPage = await client.GetStringAsync($"/Admin/Events/Identity/{saved.Id}");
        using (var forgedSlug = await client.PostAsync($"/Admin/Events/Identity/{saved.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Name"] = saved.Name,
            ["Input.Slug"] = "forged-public-link",
            ["Input.Description"] = string.Empty,
            ["Input.BuyInDescription"] = string.Empty,
            ["Input.Timezone"] = saved.Timezone,
            ["Input.Version"] = saved.Version.ToString(CultureInfo.InvariantCulture),
            ["__RequestVerificationToken"] = Token(forgedSlugPage)
        })))
        {
            Assert.Equal(HttpStatusCode.OK, forgedSlug.StatusCode);
            var html = WebUtility.HtmlDecode(await forgedSlug.Content.ReadAsStringAsync());
            Assert.Contains("public event link is permanent", html, StringComparison.OrdinalIgnoreCase);
        }

        var staleVersion = saved.Version;
        var stalePage = await client.GetStringAsync($"/Admin/Events/Identity/{saved.Id}");
        var staleToken = Token(stalePage);
        saved.UpdateIdentity("HTTP winner", saved.Slug, saved.Description, saved.BuyInDescription, saved.Timezone);
        await verify.SaveChangesAsync();
        verify.ChangeTracker.Clear();
        var staleValues = new Dictionary<string, string>
        {
            ["Input.Name"] = "Stale proposal",
            ["Input.Description"] = string.Empty,
            ["Input.BuyInDescription"] = string.Empty,
            ["Input.Timezone"] = "Europe/Copenhagen",
            ["Input.Version"] = staleVersion.ToString(CultureInfo.InvariantCulture),
            ["Input.TimezoneConfirmationOriginal"] = "Europe/Copenhagen",
            ["Input.TimezoneConfirmationProposed"] = "Europe/Copenhagen"
        };
        using (var stale = await PostIdentityAsync(staleValues, staleToken))
        {
            Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
            var html = WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync());
            Assert.Contains("This event changed while you were editing it", html);
            Assert.Contains("name=\"Input.Version\"", html);
            Assert.Contains("value=\"" + staleVersion.ToString(CultureInfo.InvariantCulture) + "\"", html);
            Assert.Contains("Stale proposal", html);
        }

        async Task<HttpResponseMessage> PostAsync(Dictionary<string, string> values)
        {
            values["__RequestVerificationToken"] = token;
            values["Input.RequestId"] = Regex.Match(createPage, "id=\"Input_RequestId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            return await client.PostAsync("/Admin/Events/Create", new FormUrlEncodedContent(values));
        }

        async Task<HttpResponseMessage> PostIdentityAsync(Dictionary<string, string> values, string requestToken)
        {
            values["__RequestVerificationToken"] = requestToken;
            return await client.PostAsync($"/Admin/Events/Identity/{saved.Id}", new FormUrlEncodedContent(values));
        }

        static string Token(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    [Fact]
    public async Task CreationAllocatesDistinctAutomaticSlugsAndRejectsRetiredExplicitLinks()
    {
        await using var db = new ApplicationDbContext(options);
        var actor = Guid.NewGuid();
        foreach (var name in new[] { "Duplicate display name", "Duplicate display name", new string('a', 50), new string('a', 50) })
        {
            var page = Creation(db, actor, new() { Name = name, Timezone = "UTC" });
            Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(CancellationToken.None));
        }
        var before = await db.Events.AsNoTracking().OrderBy(item => item.Slug).Select(item => new { item.Id, item.Slug }).ToListAsync();
        Assert.Equal(4, before.Select(item => item.Slug).Distinct().Count());
        Assert.Contains(before, item => item.Slug == "duplicate-display-name");
        Assert.Contains(before, item => item.Slug == "duplicate-display-name-2");
        Assert.All(before, item => Assert.InRange(item.Slug.Length, 1, 120));
        var retiredExplicitLink = Creation(db, actor, new() { Name = "Another display name", Slug = "duplicate-display-name", Timezone = "UTC" });
        Assert.IsType<PageResult>(await retiredExplicitLink.OnPostAsync(CancellationToken.None));
        Assert.Contains(retiredExplicitLink.ModelState[string.Empty]!.Errors, error => error.ErrorMessage.Contains("only a name and timezone", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(before, await db.Events.AsNoTracking().OrderBy(item => item.Slug).Select(item => new { item.Id, item.Slug }).ToListAsync());
        Assert.Equal(4, await db.SignupForms.CountAsync());
        Assert.Equal(12, await db.SignupQuestions.CountAsync());
        Assert.Equal(4, await db.Boards.CountAsync());
        Assert.Equal(4, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task ConcurrentCreationAllocatesDistinctSlugsAndLeavesNoPartialAggregates()
    {
        var actor = Guid.NewGuid();
        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var first = Creation(firstDb, actor, new() { Name = "Concurrent draft", Timezone = "UTC" });
        var second = Creation(secondDb, actor, new() { Name = "Concurrent draft", Timezone = "UTC" });
        var results = await Task.WhenAll(first.OnPostAsync(CancellationToken.None), second.OnPostAsync(CancellationToken.None));
        Assert.All(results, result => Assert.IsType<RedirectToPageResult>(result));

        await using var verify = new ApplicationDbContext(options);
        var events = await verify.Events.Where(item => item.Name == "Concurrent draft").OrderBy(item => item.Slug).ToListAsync();
        var eventIds = events.Select(item => item.Id).ToArray();
        Assert.Equal(2, events.Count);
        Assert.Equal(["concurrent-draft", "concurrent-draft-2"], events.Select(item => item.Slug).ToArray());
        Assert.Equal(2, await verify.SignupForms.CountAsync(form => eventIds.Contains(form.EventId)));
        Assert.Equal(6, await verify.SignupQuestions.CountAsync(question => eventIds.Contains(question.EventId)));
        Assert.Equal(2, await verify.Boards.CountAsync(board => eventIds.Contains(board.EventId)));
        Assert.Equal(2, await verify.AuditEntries.CountAsync(entry => entry.EventId.HasValue && eventIds.Contains(entry.EventId.Value)));
    }

    [Fact]
    public async Task CreationHandlerCommitsMinimalDraftAndRejectsRetiredOptionalInputWithoutResidue()
    {
        var actor = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);
        var success = Creation(db, actor, new CreateModel.CreateInput { Name = "Minimal draft", Timezone = "Europe/Copenhagen" });

        Assert.IsType<RedirectToPageResult>(await success.OnPostAsync(CancellationToken.None));
        var created = await db.Events.SingleAsync();
        Assert.Equal(EventState.Draft, created.State);
        Assert.Null(created.Description);
        Assert.Null(created.ParticipantCap);
        Assert.False(created.ScheduledSignupOpeningEnabled);
        Assert.Equal(created.Id, (await db.AuditEntries.SingleAsync()).EventId);
        Assert.Equal(5, await db.Boards.Where(board => board.EventId == created.Id).Select(board => board.Rows).SingleAsync());
        Assert.Equal(5, await db.Boards.Where(board => board.EventId == created.Id).Select(board => board.Columns).SingleAsync());

        var invalid = Creation(db, actor, new CreateModel.CreateInput
        {
            Name = "Retired optional input",
            Timezone = "Europe/Copenhagen",
            Description = "Description is no longer accepted during creation.",
            SignupOpensLocal = "2026-08-01",
            ExpectedBoardRows = 4,
            WaitingListEnabled = false
        });
        Assert.IsType<PageResult>(await invalid.OnPostAsync(CancellationToken.None));
        Assert.Contains(invalid.ModelState[string.Empty]!.Errors, error => error.ErrorMessage.Contains("only a name and timezone", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Description is no longer accepted during creation.", invalid.Input.Description);
        Assert.Equal("2026-08-01", invalid.Input.SignupOpensLocal);
        Assert.Equal(1, await db.Events.CountAsync());
    }

    [Fact]
    public async Task CreationRollsBackTheWholeAggregateWhenARequiredInsertFails()
    {
        var actor = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION slice3_fail_board_insert() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'slice3 forced board failure';
            END;
            $$;
            CREATE TRIGGER slice3_fail_board_insert BEFORE INSERT ON boards FOR EACH ROW EXECUTE FUNCTION slice3_fail_board_insert();
            """);
        try
        {
            var failed = Creation(db, actor, new CreateModel.CreateInput { Name = "Rollback draft", Timezone = "UTC" });
            Assert.IsType<PageResult>(await failed.OnPostAsync(CancellationToken.None));
            Assert.Contains(failed.ModelState[string.Empty]!.Errors, error => error.ErrorMessage.Contains("creation outcome could not be confirmed", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(await db.Events.ToListAsync());
            Assert.Empty(await db.EventCreationOperations.ToListAsync());
            Assert.Empty(await db.SignupForms.ToListAsync());
            Assert.Empty(await db.SignupQuestions.ToListAsync());
            Assert.Empty(await db.Boards.ToListAsync());
            Assert.Empty(await db.AuditEntries.ToListAsync());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS slice3_fail_board_insert ON boards; DROP FUNCTION IF EXISTS slice3_fail_board_insert();");
        }
    }

    [Fact]
    public async Task CreationAllowsOnlyCopenhagenAndUtcTimezones()
    {
        var actor = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);

        foreach (var timezone in new[] { "Europe/Copenhagen", "UTC" })
        {
            var creation = Creation(db, actor, new CreateModel.CreateInput
            {
                Name = $"Supported {timezone}",
                Timezone = timezone
            });

            Assert.IsType<RedirectToPageResult>(await creation.OnPostAsync(CancellationToken.None));
        }

        var invalid = Creation(db, actor, new CreateModel.CreateInput
        {
            Name = "Unsupported timezone",
            Timezone = "Europe/London"
        });

        Assert.IsType<PageResult>(await invalid.OnPostAsync(CancellationToken.None));
        Assert.True(invalid.ModelState.ContainsKey("Input.Timezone"));
        Assert.Equal(2, await db.Events.CountAsync());
    }

    [Fact]
    public async Task CreationDefaultsUseTheRequiredSignupQuestionsAndAnEmptyFiveByFiveBoard()
    {
        var actor = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);
        var creation = Creation(db, actor, new CreateModel.CreateInput
        {
            Name = "Default signup readiness",
            Timezone = "UTC"
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
                Assert.True(question.Required);
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
        var board = await db.Boards.SingleAsync(item => item.EventId == bingoEvent.Id);
        Assert.Equal(5, board.Rows);
        Assert.Equal(5, board.Columns);
        Assert.Empty(await db.BoardTiles.Where(tile => tile.BoardId == board.Id).ToListAsync());
        Assert.Equal(EventState.Draft, bingoEvent.State);
        Assert.Null(bingoEvent.PublicRules);
        Assert.Null(bingoEvent.PrizeDescription);
        Assert.Null(bingoEvent.ExpectedTeamCount);
        Assert.Null(bingoEvent.ExpectedBoardRows);
        Assert.Null(bingoEvent.ExpectedBoardColumns);
    }

    [Fact]
    public async Task IdentityHandlerPreservesUtcAndSupportsLiveContentEditsButLocksSlugAndPostLiveTimezone()
    {
        var actor = Guid.NewGuid();
        var eventId = await SeedEventAsync("identity-target", actor);
        DateTimeOffset originalStart;
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            item.ConfigureInitialSchedule(null, null, null, now.AddDays(1), now.AddDays(2), null);
            originalStart = item.EventStartsAt!.Value;
            await db.SaveChangesAsync();
            item = await db.Events.SingleAsync(x => x.Id == eventId);
            var edit = Identity(db, actor, new IdentityModel.InputModel
            {
                Name = "Renamed",
                Slug = item.Slug,
                Description = "Description",
                BuyInDescription = "Buy-in details",
                Timezone = "UTC",
                Version = item.Version
            });
            Assert.IsType<RedirectToPageResult>(await edit.OnPostAsync(eventId, CancellationToken.None));
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal("Renamed", item.Name);
            Assert.Equal("identity-target", item.Slug);
            Assert.Equal("UTC", item.Timezone);
            Assert.Equal("Description", item.Description);
            Assert.Equal("Buy-in details", item.BuyInDescription);
            Assert.Equal(originalStart, item.EventStartsAt);
            Assert.Single(await db.AuditEntries.Where(entry => entry.EventId == eventId && entry.Action == "event.identity_updated").ToListAsync());

            var lockedSlug = Identity(db, actor, new IdentityModel.InputModel
            {
                Name = item.Name,
                Slug = "cannot-change",
                Description = item.Description,
                BuyInDescription = item.BuyInDescription,
                Timezone = item.Timezone,
                Version = item.Version
            });
            Assert.IsType<PageResult>(await lockedSlug.OnPostAsync(eventId, CancellationToken.None));
            Assert.True(lockedSlug.ModelState.ContainsKey("Input.Slug"));
        }

        var liveEventId = await SeedEventAsync("identity-live", actor);
        await using (var db = new ApplicationDbContext(options))
        {
            var live = await db.Events.SingleAsync(x => x.Id == liveEventId);
            live.MarkFirstPublic(now);
            live.ConfigureInitialSchedule(null, null, null, now.AddDays(1), now.AddDays(2), null);
            live.OpenSignups();
            live.CloseSignups();
            live.StartEvent(now);
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var live = await db.Events.SingleAsync(x => x.Id == liveEventId);
            var liveIdentity = Identity(db, actor, new IdentityModel.InputModel
            {
                Name = "Live update",
                Slug = live.Slug,
                Description = "Changed",
                BuyInDescription = "Live buy-in",
                Timezone = live.Timezone,
                Version = live.Version
            });
            Assert.IsType<RedirectToPageResult>(await liveIdentity.OnPostAsync(liveEventId, CancellationToken.None));
            live = await db.Events.AsNoTracking().SingleAsync(x => x.Id == liveEventId);
            Assert.Equal("Live update", live.Name);
            Assert.Equal("Changed", live.Description);
            Assert.Equal("Live buy-in", live.BuyInDescription);

            var timezoneLocked = Identity(db, actor, new IdentityModel.InputModel
            {
                Name = live.Name,
                Slug = live.Slug,
                Description = live.Description,
                BuyInDescription = live.BuyInDescription,
                Timezone = "UTC",
                Version = live.Version,
                ConfirmTimezoneChange = true,
                TimezoneConfirmationOriginal = live.Timezone,
                TimezoneConfirmationProposed = "UTC"
            });
            Assert.IsType<PageResult>(await timezoneLocked.OnPostAsync(liveEventId, CancellationToken.None));
            Assert.True(timezoneLocked.ModelState.ContainsKey("Input.Timezone"));
            Assert.Equal("Europe/Copenhagen", (await db.Events.AsNoTracking().SingleAsync(x => x.Id == liveEventId)).Timezone);
        }

        var previewId = await SeedEventAsync("identity-preview", actor);
        DateTimeOffset previewStart;
        long previewVersion;
        await using (var db = new ApplicationDbContext(options))
        {
            var previewEvent = await db.Events.SingleAsync(x => x.Id == previewId);
            previewEvent.MarkFirstPublic(now);
            previewEvent.ConfigureInitialSchedule(null, null, null, now.AddDays(3), now.AddDays(4), null);
            previewStart = previewEvent.EventStartsAt!.Value;
            await db.SaveChangesAsync();
            previewVersion = previewEvent.Version;
            var preview = Identity(db, actor, new IdentityModel.InputModel
            {
                Name = previewEvent.Name,
                Slug = previewEvent.Slug,
                Timezone = "UTC",
                Version = previewVersion
            });
            Assert.IsType<PageResult>(await preview.OnPostAsync(previewId, CancellationToken.None));
            Assert.True(preview.ModelState.ContainsKey("Input.ConfirmTimezoneChange"));
            Assert.NotEmpty(preview.TimezonePreview);
            Assert.Contains(preview.TimezonePreview, row => row.Scheduled && row.CurrentOffset == "UTC+02:00" && row.NewOffset == "UTC+00:00");

            var invalidTimezone = Identity(db, actor, new IdentityModel.InputModel
            {
                Name = previewEvent.Name,
                Slug = previewEvent.Slug,
                Timezone = "Mars/Olympus",
                Version = previewVersion
            });
            Assert.IsType<PageResult>(await invalidTimezone.OnPostAsync(previewId, CancellationToken.None));
            Assert.True(invalidTimezone.ModelState.ContainsKey("Input.Timezone"));
            Assert.Empty(invalidTimezone.TimezonePreview);

            var confirmed = Identity(db, actor, new IdentityModel.InputModel
            {
                Name = previewEvent.Name,
                Slug = previewEvent.Slug,
                Timezone = "UTC",
                Version = previewVersion,
                ConfirmTimezoneChange = true,
                TimezoneConfirmationOriginal = "Europe/Copenhagen",
                TimezoneConfirmationProposed = "UTC",
                TimezoneConfirmationSchedule = preview.Input.TimezoneConfirmationSchedule
            });
            Assert.IsType<RedirectToPageResult>(await confirmed.OnPostAsync(previewId, CancellationToken.None));
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var previewEvent = await db.Events.SingleAsync(x => x.Id == previewId);
            Assert.Equal("UTC", previewEvent.Timezone);
            Assert.Equal(previewStart, previewEvent.EventStartsAt);
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
            var stale = Identity(staleDb, actor, new IdentityModel.InputModel { Name = "Stale", Slug = "identity-stale", Description = "Description", Timezone = "UTC", Version = staleVersion });
            Assert.IsType<PageResult>(await stale.OnPostAsync(staleEventId, CancellationToken.None));
            Assert.True(stale.ModelState.ContainsKey(string.Empty));
        }
    }

    [Fact]
    public async Task IdentityTreatsWrappedProviderConflictsAsStale()
    {
        var actor = Guid.NewGuid();
        var eventId = await SeedEventAsync("identity-wrapped-conflict", actor);
        var interceptedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .AddInterceptors(new WrappedProviderConflict())
            .Options;
        await using var db = new ApplicationDbContext(interceptedOptions);
        var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        var model = Identity(db, actor, new IdentityModel.InputModel
        {
            Name = "Wrapped conflict proposal",
            Slug = item.Slug,
            Description = item.Description,
            BuyInDescription = item.BuyInDescription,
            Timezone = item.Timezone,
            Version = item.Version
        });

        Assert.IsType<PageResult>(await model.OnPostAsync(eventId, CancellationToken.None));
        Assert.Contains(model.ModelState[string.Empty]!.Errors, error => error.ErrorMessage.Contains("event changed", StringComparison.OrdinalIgnoreCase));
        await using var verify = new ApplicationDbContext(options);
        var unchanged = await verify.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        Assert.Equal("identity-wrapped-conflict", unchanged.Name);
        Assert.Empty(await verify.AuditEntries.Where(entry => entry.EventId == eventId && entry.Action == "event.identity_updated").ToListAsync());
    }

    [Fact]
    public async Task IdentityShowsExplicitUtcFallbackForRetainedUnresolvableTimezone()
    {
        var actor = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Events.Add(new BingoEvent(eventId, "Retained timezone", "retained-timezone", "Legacy/Unknown", actor, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb));
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var model = Identity(db, actor, new IdentityModel.InputModel());
        Assert.IsType<PageResult>(await model.OnGetAsync(eventId, CancellationToken.None));
        Assert.True(model.HasUnresolvableTimezone);
        Assert.Equal("Legacy/Unknown", model.StoredTimezoneId);
        Assert.Contains("UTC fallback", model.EventDate(now), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScheduleHandlerLoadsMachineValuesAndPreservesUntouchedInstants()
    {
        var actor = Guid.NewGuid();
        await using (var accountDb = new ApplicationDbContext(options))
        {
            var admin = Account.CreateWebsite(actor, "schedule-admin", "SCHEDULE-ADMIN", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            accountDb.Accounts.Add(admin);
            await accountDb.SaveChangesAsync();
        }
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
            Assert.Equal(20, saved.ParticipantCap);
            Assert.False(saved.ScheduledSignupOpeningEnabled);

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

    [Fact]
    public async Task SchedulePageSavesFutureOpeningWithAnIncompletePrivateWindow()
    {
        var actor = Guid.NewGuid();
        await using (var accountDb = new ApplicationDbContext(options))
        {
            var admin = Account.CreateWebsite(actor, "partial-schedule-admin", "PARTIAL-SCHEDULE-ADMIN", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            accountDb.Accounts.Add(admin);
            await accountDb.SaveChangesAsync();
        }
        var eventId = await SeedEventAsync("partial-schedule-page", actor);
        await using var db = new ApplicationDbContext(options);
        var model = Schedule(db, actor);
        Assert.IsType<PageResult>(await model.OnGetAsync(eventId, CancellationToken.None));
        model.Input.SignupOpensLocal = "2026-08-01T10:00";
        model.Input.SignupClosesLocal = "2026-08-02T10:00";
        model.Input.ScheduledSignupOpeningEnabled = true;

        Assert.IsType<RedirectToPageResult>(await model.OnPostAsync(eventId, CancellationToken.None));
        db.ChangeTracker.Clear();
        var saved = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        Assert.Equal(EventState.Draft, saved.State);
        Assert.True(saved.ScheduledSignupOpeningEnabled);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero), saved.SignupOpensAt);
        Assert.Equal(new DateTimeOffset(2026, 8, 2, 8, 0, 0, TimeSpan.Zero), saved.SignupClosesAt);
        Assert.Null(saved.EventStartsAt);
        Assert.Null(saved.EventEndsAt);
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
            var admin = Account.CreateWebsite(actor, "schedule-preview-admin", "SCHEDULE-PREVIEW-ADMIN", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            db.Accounts.Add(admin);
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
    public async Task SignupSettingsHandlerPromotesWaitingParticipantOnlyAfterCapacityIncrease()
    {
        var actor = Guid.NewGuid();
        await using (var accountDb = new ApplicationDbContext(options))
        {
            var admin = Account.CreateWebsite(actor, "waiting-list-admin", "WAITING-LIST-ADMIN", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            accountDb.Accounts.Add(admin);
            await accountDb.SaveChangesAsync();
        }

        var eventId = await SeedEventAsync("waiting-list-settings", actor);
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        item.ConfigureSchedule(null, null, null, null, null, 1);
        item.ConfigureSignup(true, false, null);
        await db.SaveChangesAsync();
        db.EventParticipants.AddRange(
            new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now, SignupSource.Website),
            new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.WaitingList, 2, now.AddMinutes(1), SignupSource.Website));
        await db.SaveChangesAsync();

        var settings = new ParticipantsModel(db, new SignupService(db, new SecretHasher(), new FixedTimeProvider(now))) { SignupAdministration = new ParticipantsModel.SignupAdministrationInput { ParticipantCap = 1, WaitingListEnabled = true, Version = item.Version } };
        SetAdmin(settings, actor);
        Assert.IsType<RedirectToPageResult>(await settings.OnPostSignupAdministrationAsync(eventId, CancellationToken.None));

        item = await db.Events.SingleAsync(x => x.Id == eventId);
        Assert.True(item.WaitingListEnabled);
        Assert.Equal(1, item.ParticipantCap);
        Assert.Equal(1, await db.EventParticipants.CountAsync(x => x.EventId == eventId && x.SignupStatus == SignupStatus.Confirmed));
        Assert.Equal(1, await db.EventParticipants.CountAsync(x => x.EventId == eventId && x.SignupStatus == SignupStatus.WaitingList));
        Assert.Empty(await db.AuditEntries.Where(x => x.EventId == eventId && x.Action == "participant.promoted").ToListAsync());
        var unchangedAdministration = Assert.Single(await db.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.signup_administration_updated").ToListAsync());
        Assert.Equal(actor, unchangedAdministration.ActorAccountId);
        Assert.Equal("waiting-list-admin", unchangedAdministration.ActorUsername);
        Assert.NotEqual("admin", unchangedAdministration.ActorUsername);

        settings = new ParticipantsModel(db, new SignupService(db, new SecretHasher(), new FixedTimeProvider(now))) { SignupAdministration = new ParticipantsModel.SignupAdministrationInput { ParticipantCap = 2, WaitingListEnabled = true, Version = item.Version } };
        SetAdmin(settings, actor);
        Assert.IsType<RedirectToPageResult>(await settings.OnPostSignupAdministrationAsync(eventId, CancellationToken.None));

        item = await db.Events.SingleAsync(x => x.Id == eventId);
        Assert.True(item.WaitingListEnabled);
        Assert.Equal(2, item.ParticipantCap);
        Assert.Equal(2, await db.EventParticipants.CountAsync(x => x.EventId == eventId && x.SignupStatus == SignupStatus.Confirmed));
        Assert.Empty(await db.EventParticipants.Where(x => x.EventId == eventId && x.SignupStatus == SignupStatus.WaitingList).ToListAsync());
        var promotion = Assert.Single(await db.AuditEntries.Where(x => x.EventId == eventId && x.Action == "participant.promoted").ToListAsync());
        Assert.Equal(actor, promotion.ActorAccountId);
        Assert.Equal("waiting-list-admin", promotion.ActorUsername);
        Assert.NotEqual("admin", promotion.ActorUsername);
        var administrationAudits = await db.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.signup_administration_updated").ToListAsync();
        Assert.Equal(2, administrationAudits.Count);
        Assert.All(administrationAudits, audit =>
        {
            Assert.Equal(actor, audit.ActorAccountId);
            Assert.Equal("waiting-list-admin", audit.ActorUsername);
            Assert.NotEqual("admin", audit.ActorUsername);
        });
    }

    private async Task<Guid> SeedEventAsync(string slug, Guid actor)
    {
        var item = new BingoEvent(Guid.NewGuid(), slug, slug, "Europe/Copenhagen", actor, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
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

    private CreateModel Creation(ApplicationDbContext db, Guid actor, CreateModel.CreateInput input)
    {
        if (!db.Accounts.Any(x => x.Id == actor))
        {
            var account = Account.CreateWebsite(actor, $"create-{actor:N}", $"CREATE-{actor:N}", now);
            account.SetGlobalRole(GlobalRole.Admin);
            db.Accounts.Add(account);
            db.SaveChanges();
        }
        input.RequestId = Guid.NewGuid();
        var model = new CreateModel(new EventCreationService(db, new FixedTimeProvider(now))) { Input = input };
        SetAdmin(model, actor);
        return model;
    }

    private IdentityModel Identity(ApplicationDbContext db, Guid actor, IdentityModel.InputModel input)
    {
        var model = new IdentityModel(db, new FixedTimeProvider(now)) { Input = input };
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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }

    private sealed class WrappedProviderConflict : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData _, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Controlled wrapped provider conflict", new PostgresException("Controlled deadlock", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected));
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
