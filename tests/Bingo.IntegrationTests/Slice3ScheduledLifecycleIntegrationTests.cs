using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Navigation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3ScheduledLifecycleIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice3_scheduled")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private readonly DateTimeOffset now = new(2026, 7, 27, 15, 0, 0, TimeSpan.Zero);
    private readonly IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["DiscordAuthentication:ClientId"] = "test-client",
            ["DiscordAuthentication:ClientSecret"] = "test-secret"
        }).Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ScheduledOpeningAndClosingUseExactBoundariesAndAreIdempotent()
    {
        var eventId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = ReadyDraft(setup, eventId, "scheduled-window", now, now.AddHours(1), now.AddDays(1));
            item.ConfigureScheduledSignupOpening(true, []);
            setup.Events.Add(item);
            await setup.SaveChangesAsync();
        }

        var clock = new MutableTimeProvider(now);
        await using (var db = new ApplicationDbContext(options))
        {
            var lifecycle = Services(db, clock);
            await lifecycle.ProcessDueAsync();
            await lifecycle.ProcessDueAsync();
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            var item = await verify.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.SignupOpen, item.State);
            Assert.Equal(now, item.ActualSignupOpenedAt);
            Assert.Equal(now, item.FirstPublicAt);
            Assert.Single(await verify.ScheduledSignupOpeningAttempts.Where(x => x.EventId == eventId).ToListAsync());
            Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == eventId).ToListAsync());
        }

        clock.Set(now.AddHours(1));
        await using (var db = new ApplicationDbContext(options))
        {
            var lifecycle = Services(db, clock);
            await lifecycle.ProcessDueAsync();
            await lifecycle.ProcessDueAsync();
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            var item = await verify.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.SignupClosed, item.State);
            Assert.Equal(now.AddHours(1), item.ActualSignupClosedAt);
            Assert.Equal(2, await verify.EventStateTransitions.CountAsync(x => x.EventId == eventId));
            Assert.Equal(2, await verify.AuditEntries.CountAsync(x => x.EventId == eventId && x.Action.Contains("automatically")));
        }

        var failedId = Guid.NewGuid();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Opening Admin", "OPENING ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        await using (var setup = new ApplicationDbContext(options))
        {
            var failed = new BingoEvent(failedId, "Failed opening", "failed-opening", "UTC", Guid.NewGuid(), now);
            failed.UpdateIdentity("Failed opening", "failed-opening", "Public event description", "UTC");
            failed.ConfigureSchedule(clock.GetUtcNow(), now.AddDays(2), null, now.AddDays(3), now.AddDays(4), 20);
            failed.ConfigureSignup(false, false, null);
            failed.ConfigureScheduledSignupOpening(true, []);
            AddReadySignupForm(setup, failedId);
            setup.AddRange(admin, failed);
            await setup.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var lifecycle = Services(db, clock);
            await lifecycle.ProcessDueAsync();
            await lifecycle.ProcessDueAsync();
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.Draft, (await verify.Events.SingleAsync(x => x.Id == failedId)).State);
            var attempt = Assert.Single(await verify.ScheduledSignupOpeningAttempts.Where(x => x.EventId == failedId).ToListAsync());
            Assert.Contains("UNACKNOWLEDGED_WAITING_LIST_DISABLED", attempt.Blockers);
            Assert.Single(await verify.PersonalNotifications.Where(x => x.RecipientAccountId == admin.Id && x.Title == "Scheduled signup opening failed").ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == failedId && x.Action == "event.signup_opening_failed").ToListAsync());
        }
    }

    [Fact]
    public async Task LateScheduledEndUsesEffectiveInstantAndResumeThenSecondEndPreservesHistory()
    {
        var eventId = Guid.NewGuid();
        var scheduledEnd = now.AddHours(-1);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = ReadyDraft(setup, eventId, "slice9-authoritative-end", now.AddDays(-3), now.AddDays(-2), scheduledEnd);
            item.OpenSignups(now.AddDays(-3));
            item.CloseSignups(now.AddDays(-2));
            item.StartEvent(now.AddDays(-2).AddMinutes(1));
            setup.Events.Add(item);
            await setup.SaveChangesAsync();
        }

        var clock = new MutableTimeProvider(now);
        await using (var worker = new ApplicationDbContext(options))
            await Services(worker, clock).ProcessDueAsync();

        var scheduledVersion = await VersionAsync(eventId);
        await using (var beforeCatchUp = new ApplicationDbContext(options))
        {
            var item = await beforeCatchUp.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.AwaitingFinalReview, item.State);
            Assert.Equal(scheduledEnd, item.ActualEndedAt);
            Assert.False(item.AcceptsNewSubmissions(now));
            Assert.Equal(scheduledEnd.AddMinutes(30), item.SubmissionsClosedAt);
        }

        await using (var resume = new ApplicationDbContext(options))
        {
            var result = await Services(resume, clock).ResumePrematureEndAsync(eventId, scheduledVersion, true, "The first end was premature.", now.AddHours(2), new LifecycleActor(Guid.NewGuid(), "admin"));
            Assert.True(result.Succeeded, result.Error);
        }

        var liveVersion = await VersionAsync(eventId);
        await using (var repeated = new ApplicationDbContext(options))
        {
            var result = await Services(repeated, clock).ResumePrematureEndAsync(eventId, liveVersion, true, "Repeated request.", now.AddHours(3), new LifecycleActor(Guid.NewGuid(), "admin"));
            Assert.False(result.Succeeded);
            Assert.Contains("final review", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        clock.Set(now.AddHours(3));
        await using (var secondEnd = new ApplicationDbContext(options))
        {
            var result = await Services(secondEnd, clock).EndNowAsync(eventId, await VersionAsync(eventId), true, null, new LifecycleActor(Guid.NewGuid(), "admin"));
            Assert.True(result.Succeeded, result.Error);
        }

        await using var verify = new ApplicationDbContext(options);
        var itemAfterSecondEnd = await verify.Events.SingleAsync(x => x.Id == eventId);
        Assert.Equal(EventState.AwaitingFinalReview, itemAfterSecondEnd.State);
        Assert.Equal(now.AddHours(3), itemAfterSecondEnd.ActualEndedAt);
        Assert.Equal(now.AddHours(2).AddMinutes(30), itemAfterSecondEnd.SubmissionsClosedAt);
        var transitions = await verify.EventStateTransitions.Where(x => x.EventId == eventId).OrderBy(x => x.PerformedAt).ToListAsync();
        Assert.Equal(3, transitions.Count);
        Assert.Equal((EventState.Live, EventState.AwaitingFinalReview), (transitions[0].FromState, transitions[0].ToState));
        Assert.Equal(scheduledEnd, transitions[0].EffectiveAt);
        Assert.True(transitions[0].PerformedAt > transitions[0].EffectiveAt);
        Assert.Equal((EventState.AwaitingFinalReview, EventState.Live), (transitions[1].FromState, transitions[1].ToState));
        Assert.Equal((EventState.Live, EventState.AwaitingFinalReview), (transitions[2].FromState, transitions[2].ToState));
        Assert.Equal(now.AddHours(3), transitions[2].EffectiveAt);
        Assert.Equal(3, await verify.AuditEntries.CountAsync(x => x.EventId == eventId && (x.Action == "event.ended_automatically" || x.Action == "event.resumed" || x.Action == "event.ended")));
    }

    [Fact]
    public async Task OverlappingScheduledOpeningNotifiesExactlyOnceAndCorrectedRetryOpens()
    {
        var blockingId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Opening Admin", "OPENING ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var superAdmin = Account.CreateWebsite(Guid.NewGuid(), "Opening Owner", "OPENING OWNER", now);
        superAdmin.SetGlobalRole(GlobalRole.SuperAdmin);
        var disabledAdmin = Account.CreateWebsite(Guid.NewGuid(), "Disabled Admin", "DISABLED ADMIN", now);
        disabledAdmin.SetGlobalRole(GlobalRole.Admin);
        disabledAdmin.Disable(now, superAdmin.Id, "Test recipient filtering.");
        var scheduledFor = now.AddMinutes(5);

        await using (var setup = new ApplicationDbContext(options))
        {
            var blocking = ReadyDraft(setup, blockingId, "blocking-live-event", now.AddDays(-2), now.AddDays(-1), now.AddDays(3));
            blocking.OpenSignups(now.AddDays(-2));
            blocking.CloseSignups(now.AddDays(-1));
            blocking.StartEvent(now);
            var candidate = ReadyDraft(setup, candidateId, "overlapping-opening", scheduledFor, now.AddHours(1), now.AddDays(4));
            candidate.ConfigureScheduledSignupOpening(true, []);
            setup.AddRange(admin, superAdmin, disabledAdmin, blocking, candidate);
            await setup.SaveChangesAsync();
        }

        var clock = new MutableTimeProvider(now);
        await RunDueInNewContext(clock);
        await using (var beforeDue = new ApplicationDbContext(options))
        {
            Assert.Empty(await beforeDue.ScheduledSignupOpeningAttempts.Where(x => x.EventId == candidateId).ToListAsync());
            Assert.Empty(await beforeDue.PersonalNotifications.Where(x => x.Title == "Scheduled signup opening failed").ToListAsync());
        }

        clock.Set(scheduledFor);
        await Task.WhenAll(RunDueInNewContext(clock), RunDueInNewContext(clock));

        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.Draft, (await verify.Events.SingleAsync(x => x.Id == candidateId)).State);
            var failed = Assert.Single(await verify.ScheduledSignupOpeningAttempts.Where(x => x.EventId == candidateId).ToListAsync());
            Assert.False(failed.Opened);
            Assert.Contains("EVENT_WINDOW_OVERLAP", failed.Blockers);
            Assert.Contains("blocking-live-event", failed.BlockerDetails);
            Assert.Contains("UTC", failed.BlockerDetails);
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == candidateId && x.Action == "event.signup_opening_failed").ToListAsync());
            var notifications = await verify.PersonalNotifications.Where(x => x.Title == "Scheduled signup opening failed").OrderBy(x => x.RecipientAccountId).ToListAsync();
            Assert.Equal(2, notifications.Count);
            Assert.Contains(notifications, x => x.RecipientAccountId == admin.Id);
            Assert.Contains(notifications, x => x.RecipientAccountId == superAdmin.Id);
            Assert.DoesNotContain(notifications, x => x.RecipientAccountId == disabledAdmin.Id);
            Assert.All(notifications, x =>
            {
                Assert.Contains("blocking-live-event", x.Detail);
                Assert.Contains("UTC", x.Detail);
                Assert.Equal($"/Admin/Events/Manage/{candidateId}", x.Route);
            });

            var principal = Principal(admin);
            var shell = await new SharedShellService(verify, new PassthroughLocalizer()).GetNotificationsAsync(principal, CancellationToken.None);
            var preview = Assert.Single(shell.Items);
            Assert.Equal("Scheduled signup opening failed", preview.Title);
            Assert.Contains("blocking-live-event", preview.Detail);

            var page = new Bingo.Web.Pages.NotificationsModel(verify, clock, new PassthroughLocalizer())
            {
                PageContext = new PageContext(new ActionContext(new DefaultHttpContext { User = principal }, new RouteData(), new PageActionDescriptor()))
            };
            Assert.IsType<PageResult>(await page.OnGetAsync(null, CancellationToken.None));
            Assert.Contains(page.Notifications, x => x.Type == "Scheduled signup opening failed" && x.Detail.Contains("blocking-live-event", StringComparison.Ordinal));
        }

        var retryAt = now.AddMinutes(10);
        await using (var correction = new ApplicationDbContext(options))
        {
            var candidate = await correction.Events.SingleAsync(x => x.Id == candidateId);
            candidate.ConfigureSchedule(retryAt, retryAt.AddMinutes(30), null, now.AddDays(3).AddMinutes(5), now.AddDays(5), 20);
            candidate.ConfigureScheduledSignupOpening(true, []);
            await correction.SaveChangesAsync();
        }
        clock.Set(retryAt);
        await Task.WhenAll(RunDueInNewContext(clock), RunDueInNewContext(clock));

        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.SignupOpen, (await verify.Events.SingleAsync(x => x.Id == candidateId)).State);
            var attempts = await verify.ScheduledSignupOpeningAttempts.Where(x => x.EventId == candidateId).OrderBy(x => x.ScheduledFor).ToListAsync();
            Assert.Equal(2, attempts.Count);
            Assert.False(attempts[0].Opened);
            Assert.NotNull(attempts[0].ResolvedAt);
            Assert.True(attempts[1].Opened);
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == candidateId && x.Action == "event.signup_opening_failed").ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == candidateId && x.Action == "event.signup_opened_automatically").ToListAsync());
            Assert.Equal(2, await verify.PersonalNotifications.CountAsync(x => x.Title == "Scheduled signup opening failed"));
        }
    }

    [Fact]
    public async Task OverdueDraftAndSignupOpenPostponeOnceAndProjectReadableLifecycleBlockers()
    {
        var eventId = Guid.NewGuid();
        var signupOpenId = Guid.NewGuid();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Admin", "ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = ReadyDraft(setup, eventId, "postponed-start", now.AddHours(-2), now.AddHours(-1), now.AddDays(1));
            var signupOpen = new BingoEvent(signupOpenId, "open-postponed-start", "open-postponed-start", "UTC", Guid.NewGuid(), now);
            signupOpen.UpdateIdentity("open-postponed-start", "open-postponed-start", "Public event description", "UTC");
            signupOpen.ConfigureSchedule(now.AddHours(-4), null, null, now.AddHours(-3), now.AddHours(2), 20);
            signupOpen.ConfigureSignup(true, false, null);
            signupOpen.OpenSignups(now.AddHours(-4));
            setup.Accounts.Add(admin);
            setup.Events.AddRange(item, signupOpen);
            await setup.SaveChangesAsync();
        }

        var clock = new MutableTimeProvider(now);
        await Task.WhenAll(RunDueInNewContext(clock), RunDueInNewContext(clock));
        await RunDueInNewContext(clock);

        await using (var verify = new ApplicationDbContext(options))
        {
            var attempt = Assert.Single(await verify.ScheduledEventStartAttempts.Where(x => x.EventId == eventId).ToListAsync());
            Assert.False(attempt.Started);
            Assert.Contains("LIFECYCLE_STATE_INVALID", attempt.Blockers);
            Assert.Contains("BOARD_NOT_PUBLISHED", attempt.Blockers);
            Assert.Contains("DRAFT_NOT_FINALIZED", attempt.Blockers);
            var draftEvent = await verify.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.Draft, draftEvent.State);
            Assert.Equal(now.AddHours(-1), draftEvent.EventStartsAt);
            var signupOpenAttempt = Assert.Single(await verify.ScheduledEventStartAttempts.Where(x => x.EventId == signupOpenId).ToListAsync());
            Assert.Contains("LIFECYCLE_STATE_INVALID", signupOpenAttempt.Blockers);
            var signupOpenEvent = await verify.Events.SingleAsync(x => x.Id == signupOpenId);
            Assert.Equal(EventState.SignupOpen, signupOpenEvent.State);
            Assert.Equal(now.AddHours(-3), signupOpenEvent.EventStartsAt);
            var notifications = await verify.PersonalNotifications.Where(x => x.RecipientAccountId == admin.Id && x.Title == "Automatic start postponed").ToListAsync();
            Assert.Equal(2, notifications.Count);
            Assert.Contains(notifications, notification => notification.Detail.Contains("has not been opened and closed", StringComparison.Ordinal));
            Assert.Contains(notifications, notification => notification.Detail.Contains("Signup is still open", StringComparison.Ordinal));
            Assert.Equal(2, await verify.AuditEntries.CountAsync(x => (x.EventId == eventId || x.EventId == signupOpenId) && x.Action == "event.start_postponed"));

            var draftPage = Manage(verify, admin, clock);
            Assert.IsType<PageResult>(await draftPage.OnGetAsync(eventId, CancellationToken.None));
            Assert.Equal("Automatic start postponed", draftPage.ScheduledAction?.Title);
            Assert.Contains(draftPage.ScheduledAction!.Blockers, blocker => blocker.Code == "LIFECYCLE_STATE_INVALID" && blocker.Description.Contains("opened and closed", StringComparison.Ordinal) && blocker.Route == $"/Admin/Events/Manage/{eventId}");
            Assert.Contains(draftPage.ScheduledAction.Blockers, blocker => blocker.Code == "DRAFT_NOT_FINALIZED" && blocker.Route == $"/Admin/Events/Draft/{eventId}");
            Assert.Contains(draftPage.ScheduledAction.Blockers, blocker => blocker.Code == "BOARD_NOT_PUBLISHED" && blocker.Route == $"/Admin/Events/Board/{eventId}");

            var signupOpenPage = Manage(verify, admin, clock);
            Assert.IsType<PageResult>(await signupOpenPage.OnGetAsync(signupOpenId, CancellationToken.None));
            Assert.Contains(signupOpenPage.ScheduledAction!.Blockers, blocker => blocker.Code == "LIFECYCLE_STATE_INVALID" && blocker.Description.Contains("Close signup", StringComparison.Ordinal) && blocker.Route == $"/Admin/Events/Manage/{signupOpenId}");
        }

    }

    [Fact]
    public async Task BlockedScheduledStartNotifiesOnceAndManualResolutionPreservesAttemptHistory()
    {
        var eventId = Guid.NewGuid();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Admin", "ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "focused-test-password"), false, now, incrementVersion: false);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = ReadyDraft(setup, eventId, "postponed-start", now.AddHours(-2), now.AddHours(-1), now.AddDays(1));
            item.OpenSignups(now.AddHours(-2));
            item.CloseSignups(now.AddHours(-1));
            setup.Accounts.Add(admin);
            setup.Events.Add(item);
            await setup.SaveChangesAsync();
        }

        var clock = new MutableTimeProvider(now);
        await using (var db = new ApplicationDbContext(options))
        {
            var lifecycle = Services(db, clock);
            await lifecycle.ProcessDueAsync();
            await lifecycle.ProcessDueAsync();
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            var attempt = Assert.Single(await verify.ScheduledEventStartAttempts.Where(x => x.EventId == eventId).ToListAsync());
            Assert.False(attempt.Started);
            Assert.Contains("BOARD_NOT_PUBLISHED", attempt.Blockers);
            Assert.Contains("DRAFT_NOT_FINALIZED", attempt.Blockers);
            Assert.Single(await verify.PersonalNotifications.Where(x => x.RecipientAccountId == admin.Id && x.Title == "Automatic start postponed").ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.start_postponed").ToListAsync());
        }

        await using (var correction = new ApplicationDbContext(options))
        {
            await AddReadyBoardAndDraftAsync(correction, eventId);

            var readyPage = Manage(correction, admin, clock);
            Assert.IsType<PageResult>(await readyPage.OnGetAsync(eventId, CancellationToken.None));
            Assert.NotNull(readyPage.ScheduledAction);
            Assert.Empty(readyPage.ScheduledAction!.Blockers);
            Assert.True(readyPage.StartReadiness!.CanProceed);
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var login = await client.GetStringAsync("/Account/Login");
        using var loggedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "Admin",
            ["Input.Password"] = "focused-test-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.True(loggedIn.IsSuccessStatusCode);

        var readyHtml = await client.GetStringAsync($"/Admin/Events/Manage/{eventId}");
        Assert.Contains("data-manage-overview", readyHtml, StringComparison.Ordinal);
        Assert.Contains("Readiness checks", readyHtml, StringComparison.Ordinal);
        Assert.Contains("Important information", readyHtml, StringComparison.Ordinal);
        Assert.Contains("Review teams and draft", readyHtml, StringComparison.Ordinal);
        Assert.Contains("All start blockers are resolved. Confirm to start the event now.", readyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Finalize the team draft. <a", readyHtml, StringComparison.Ordinal);
        var eventVersion = InputValue(readyHtml, "EventVersion");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Admin/Events/Manage/{eventId}?handler=StartEvent")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["EventVersion"] = eventVersion,
                ["ConfirmStartEvent"] = "true",
                ["__RequestVerificationToken"] = AntiforgeryToken(readyHtml)
            })
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var started = await client.SendAsync(request);
        var enhancedHtml = await started.Content.ReadAsStringAsync();
        Assert.True(started.IsSuccessStatusCode);
        Assert.Contains("The bingo is currently live.", enhancedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<h3>Automatic start postponed</h3>", enhancedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Start event now", enhancedHtml, StringComparison.Ordinal);

        var freshHtml = await client.GetStringAsync($"/Admin/Events/Manage/{eventId}");
        Assert.Contains("Review submissions", freshHtml, StringComparison.Ordinal);
        Assert.Contains("Effective event end", freshHtml, StringComparison.Ordinal);
        Assert.Contains("The bingo is currently live.", freshHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<h3>Automatic start postponed</h3>", freshHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Start event now", freshHtml, StringComparison.Ordinal);

        using var repeated = await client.PostAsync($"/Admin/Events/Manage/{eventId}?handler=StartEvent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = eventVersion,
            ["ConfirmStartEvent"] = "true",
            ["__RequestVerificationToken"] = AntiforgeryToken(freshHtml)
        }));
        Assert.True(repeated.IsSuccessStatusCode);

        var liveVersion = await VersionAsync(eventId);
        await Task.WhenAll(
            RunManualStartInNewContext(eventId, liveVersion, clock),
            RunManualStartInNewContext(eventId, liveVersion, clock));

        await using (var verify = new ApplicationDbContext(options))
        {
            var item = await verify.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.Live, item.State);
            Assert.NotNull(item.ActualStartedAt);
            Assert.Equal(now.AddHours(-1), item.EventStartsAt);
            var attempt = await verify.ScheduledEventStartAttempts.SingleAsync(x => x.EventId == eventId);
            Assert.NotNull(attempt.ResolvedAt);
            Assert.Equal(now.AddHours(-1), attempt.ScheduledFor);
            Assert.Contains("DRAFT_NOT_FINALIZED", attempt.Blockers);
            Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == eventId && x.ToState == EventState.Live).ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.started").ToListAsync());

            var livePage = Manage(verify, admin, clock);
            Assert.IsType<PageResult>(await livePage.OnGetAsync(eventId, CancellationToken.None));
            Assert.Null(livePage.ScheduledAction);
        }
    }

    [Fact]
    public async Task ManualEarlyStartNeedsReasonAndSingletonCurrentStateBlocksWithoutMutation()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var first = ReadyDraft(setup, firstId, "early-start", now.AddHours(-1), now.AddDays(1), now.AddDays(2));
            first.OpenSignups(now.AddHours(-2));
            first.CloseSignups(now.AddHours(-1));
            var second = ReadyDraft(setup, secondId, "second-current", now.AddHours(-1), now.AddDays(3), now.AddDays(4));
            second.OpenSignups(now.AddHours(-2));
            second.CloseSignups(now.AddHours(-1));
            setup.Events.AddRange(first, second);
            await setup.SaveChangesAsync();
            await AddReadyBoardAndDraftAsync(setup, firstId);
            await AddReadyBoardAndDraftAsync(setup, secondId);
            await setup.SaveChangesAsync();
        }

        var actor = new LifecycleActor(Guid.NewGuid(), "admin");
        var clock = new MutableTimeProvider(now);
        await using (var db = new ApplicationDbContext(options))
        {
            var service = Services(db, clock);
            var first = await db.Events.AsNoTracking().SingleAsync(x => x.Id == firstId);
            Assert.False((await service.StartNowAsync(firstId, first.Version, true, null, actor)).Succeeded);
            Assert.True((await service.StartNowAsync(firstId, first.Version, true, "Approved early start.", actor)).Succeeded);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var service = Services(db, clock);
            var second = await db.Events.AsNoTracking().SingleAsync(x => x.Id == secondId);
            var blocked = await service.StartNowAsync(secondId, second.Version, true, "Approved early start.", actor);
            Assert.False(blocked.Succeeded);
            Assert.Contains("early-start", blocked.Error);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.SignupClosed, (await verify.Events.SingleAsync(x => x.Id == secondId)).State);
            Assert.Empty(await verify.EventStateTransitions.Where(x => x.EventId == secondId && x.ToState == EventState.Live).ToListAsync());
            Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == secondId && x.Action.Contains("started")).ToListAsync());
        }
    }

    [Fact]
    public async Task ConcurrentWorkerAndManualCommandsProduceOneOpeningClosingStartAndCurrentWinner()
    {
        var openingId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = ReadyDraft(setup, openingId, "opening-race", now, now.AddHours(1), now.AddDays(1));
            item.ConfigureScheduledSignupOpening(true, []);
            setup.Events.Add(item);
            await setup.SaveChangesAsync();
        }
        var clock = new MutableTimeProvider(now);
        var openingVersion = await VersionAsync(openingId);
        await Task.WhenAll(
            RunDueInNewContext(clock),
            RunManualOpenInNewContext(openingId, openingVersion, clock));
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.SignupOpen, (await verify.Events.SingleAsync(x => x.Id == openingId)).State);
            Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == openingId && x.ToState == EventState.SignupOpen).ToListAsync());
        }

        clock.Set(now.AddHours(1));
        var closingVersion = await VersionAsync(openingId);
        await Task.WhenAll(
            RunDueInNewContext(clock),
            RunManualCloseInNewContext(openingId, closingVersion, clock));
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.SignupClosed, (await verify.Events.SingleAsync(x => x.Id == openingId)).State);
            Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == openingId && x.ToState == EventState.SignupClosed).ToListAsync());
        }

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            foreach (var pair in new[] { (firstId, "current-race-one"), (secondId, "current-race-two") })
            {
                var item = ReadyDraft(setup, pair.Item1, pair.Item2, now.AddHours(-2), clock.GetUtcNow(), now.AddDays(2));
                item.OpenSignups(now.AddHours(-2));
                item.CloseSignups(now.AddHours(-1));
                setup.Events.Add(item);
                await setup.SaveChangesAsync();
                await AddReadyBoardAndDraftAsync(setup, pair.Item1);
            }
            await setup.SaveChangesAsync();
        }
        var firstVersion = await VersionAsync(firstId);
        var secondVersion = await VersionAsync(secondId);
        await Task.WhenAll(
            RunManualStartInNewContext(firstId, firstVersion, clock),
            RunManualStartInNewContext(secondId, secondVersion, clock));
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(1, await verify.Events.CountAsync(x => (x.Id == firstId || x.Id == secondId) && x.State == EventState.Live));
            Assert.Equal(1, await verify.EventStateTransitions.CountAsync(x => (x.EventId == firstId || x.EventId == secondId) && x.ToState == EventState.Live));
            Assert.Equal(1, await verify.AuditEntries.CountAsync(x => (x.EventId == firstId || x.EventId == secondId) && x.Action == "event.started"));
        }
    }

    [Fact]
    public async Task ScheduledStartPersistenceFailureRollsBackAttemptAuditNotificationAndState()
    {
        var eventId = Guid.NewGuid();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Rollback Admin", "ROLLBACK ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = ReadyDraft(setup, eventId, "rollback-start", now.AddHours(-2), now.AddHours(-1), now.AddDays(1));
            item.OpenSignups(now.AddHours(-2));
            item.CloseSignups(now.AddHours(-1));
            setup.AddRange(item, admin);
            await setup.SaveChangesAsync();
            await AddReadyBoardAndDraftAsync(setup, eventId);
            for (var index = 0; index < 10; index++)
                setup.Teams.Add(new Team(Guid.NewGuid(), eventId, $"{new string((char)('A' + index), 120)}", $"rollback-team-{index}", TeamFormationType.Drafted, null, true));
            await setup.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
            await Services(db, new MutableTimeProvider(now)).ProcessDueAsync();

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.SignupClosed, (await verify.Events.SingleAsync(x => x.Id == eventId)).State);
        Assert.Empty(await verify.ScheduledEventStartAttempts.Where(x => x.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.EventStateTransitions.Where(x => x.EventId == eventId && x.ToState == EventState.Live).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.start_postponed").ToListAsync());
        Assert.Empty(await verify.PersonalNotifications.Where(x => x.RecipientAccountId == admin.Id).ToListAsync());
    }

    private async Task<long> VersionAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.Events.AsNoTracking().Where(x => x.Id == eventId).Select(x => x.Version).SingleAsync();
    }

    private async Task RunDueInNewContext(TimeProvider clock)
    {
        await using var db = new ApplicationDbContext(options);
        await Services(db, clock).ProcessDueAsync();
    }

    private async Task RunManualOpenInNewContext(Guid eventId, long version, TimeProvider clock)
    {
        await using var db = new ApplicationDbContext(options);
        var readiness = new EventReadinessEvaluator(db, configuration);
        await new EventSignupLifecycleService(db, readiness, clock).OpenAsync(eventId, version, true, false, new LifecycleActor(Guid.NewGuid(), "manual-admin"));
    }

    private async Task RunManualCloseInNewContext(Guid eventId, long version, TimeProvider clock)
    {
        await using var db = new ApplicationDbContext(options);
        var readiness = new EventReadinessEvaluator(db, configuration);
        await new EventSignupLifecycleService(db, readiness, clock).CloseAsync(eventId, version, new LifecycleActor(Guid.NewGuid(), "manual-admin"));
    }

    private async Task RunManualStartInNewContext(Guid eventId, long version, TimeProvider clock)
    {
        await using var db = new ApplicationDbContext(options);
        await Services(db, clock).StartNowAsync(eventId, version, true, null, new LifecycleActor(Guid.NewGuid(), "manual-admin"));
    }

    private EventLifecycleService Services(ApplicationDbContext db, TimeProvider clock)
    {
        var readiness = new EventReadinessEvaluator(db, configuration);
        var signup = new EventSignupLifecycleService(db, readiness, clock);
        return new EventLifecycleService(db, signup, clock);
    }

    private Bingo.Web.Pages.Admin.Events.ManageModel Manage(ApplicationDbContext db, Account admin, TimeProvider clock)
    {
        var readiness = new EventReadinessEvaluator(db, configuration);
        var signup = new EventSignupLifecycleService(db, readiness, clock);
        var context = new DefaultHttpContext { User = Principal(admin) };
        return new Bingo.Web.Pages.Admin.Events.ManageModel(db, null!, null!, null!, readiness, signup, new EventLifecycleService(db, signup, clock), null!, clock)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
        };
    }

    private BingoEvent ReadyDraft(ApplicationDbContext db, Guid id, string slug, DateTimeOffset opening, DateTimeOffset closing, DateTimeOffset end)
    {
        var item = new BingoEvent(id, slug, slug, "UTC", Guid.NewGuid(), now);
        item.UpdateIdentity(slug, slug, "Public event description", "UTC");
        item.ConfigureSchedule(opening, closing, null, closing, end, 20);
        item.ConfigureSignup(true, false, null);
        AddReadySignupForm(db, id);
        return item;
    }

    private void AddReadySignupForm(ApplicationDbContext db, Guid eventId)
    {
        var form = new SignupForm(Guid.NewGuid(), eventId, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, eventId, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, eventId, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
        db.AddRange(form, regular, captain);
    }

    private async Task AddReadyBoardAndDraftAsync(ApplicationDbContext db, Guid eventId)
    {
        var board = new Board(Guid.NewGuid(), eventId, "Published board", 1, 1);
        var draft = new DraftSession(Guid.NewGuid(), eventId, 1);
        draft.Start(now.AddHours(-2));
        draft.Finalize(now.AddHours(-1));
        db.Boards.Add(board);
        db.DraftSessions.Add(draft);
        await BoardApprovalFixture.PublishAsync(db, board, now);
    }

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset current = value;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static ClaimsPrincipal Principal(Account account) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
        new Claim(ClaimTypes.Name, account.PublicUsername!),
        new Claim(ClaimTypes.Role, "Admin")
    ], "test"));

    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static string InputValue(string page, string name) => Regex.Match(page, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

    private sealed class PassthroughLocalizer : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
