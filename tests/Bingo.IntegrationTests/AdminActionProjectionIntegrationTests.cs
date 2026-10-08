using System.Security.Claims;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AdminActionProjectionIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_admin_action_projection")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        );
    private readonly DateTimeOffset now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task EvidenceIsAggregatedAndNotificationReadStateDoesNotResolveIt()
    {
        var fixture = await SeedPendingEventAsync("projection-pending", pendingCount: 2);
        var notice = new PersonalNotification(Guid.NewGuid(), fixture.Admin.Id, "evidence.rejected", "retained reminder", "/Submissions", now, fixture.Event.Id);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.PersonalNotifications.Add(notice);
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var shell = Shell(db);
        var projection = await shell.GetAdminActionsAsync(CancellationToken.None);
        var summary = projection.ForEvent(fixture.Event.Id);
        Assert.Equal(2, projection.Count);
        Assert.Equal(2, summary.PendingEvidenceCount);
        Assert.Equal(2, summary.Count);
        var action = Assert.Single(projection.Items);
        Assert.Equal($"/Admin/Review/Index?eventId={fixture.Event.Id}", action.Url);
        Assert.Contains("2 pending", action.Detail, StringComparison.Ordinal);

        var auditCountBeforeRead = await db.AuditEntries.CountAsync();
        var page = new Bingo.Web.Pages.NotificationsModel(db, new FixedTimeProvider(now), new PassthroughLocalizer())
        {
            PageContext = new PageContext(new ActionContext(new DefaultHttpContext { User = Principal(fixture.Admin) }, new RouteData(), new PageActionDescriptor()))
        };
        Assert.IsType<RedirectResult>(await page.OnGetAsync(notice.Id, CancellationToken.None));
        Assert.NotNull(await db.PersonalNotifications.Where(item => item.Id == notice.Id).Select(item => item.ReadAt).SingleAsync());
        Assert.Equal(auditCountBeforeRead, await db.AuditEntries.CountAsync());
        Assert.Equal(2, (await shell.GetAdminActionsAsync(CancellationToken.None)).Count);

        var pending = await db.Submissions.Where(item => item.EventId == fixture.Event.Id).ToListAsync();
        foreach (var submission in pending) submission.Reject("resolved", now);
        await db.SaveChangesAsync();
        Assert.Equal(0, (await shell.GetAdminActionsAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task ResolvingCurrentIssueRemovesActionWithoutReadingAnUnrelatedNotification()
    {
        var fixture = await SeedPendingEventAsync("projection-resolve", pendingCount: 1);
        var notice = new PersonalNotification(Guid.NewGuid(), fixture.Admin.Id, "event.signup_opening_failed", "unread reminder", "/Admin/Events/Manage", now, fixture.Event.Id);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.PersonalNotifications.Add(notice);
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var shell = Shell(db);
        Assert.Equal(1, (await shell.GetAdminActionsAsync(CancellationToken.None)).Count);
        var submission = await db.Submissions.SingleAsync(item => item.EventId == fixture.Event.Id);
        submission.Reject("resolved", now);
        await db.SaveChangesAsync();

        Assert.Equal(0, (await shell.GetAdminActionsAsync(CancellationToken.None)).Count);
        Assert.Null(await db.PersonalNotifications.Where(item => item.Id == notice.Id).Select(item => item.ReadAt).SingleAsync());
    }

    [Fact]
    public async Task ScheduledActionsFollowCurrentBoundaryAndLifecycleState()
    {
        var admin = Admin("scheduled");
        var openingEvent = DraftEvent(admin, "projection-opening", now.AddHours(-1));
        var openingAttempt = new ScheduledSignupOpeningAttempt(Guid.NewGuid(), openingEvent.Id, openingEvent.SignupOpensAt!.Value, now.AddMinutes(-5), false, ["DESCRIPTION_REQUIRED"], ["Add a description."]);
        var startEvent = ClosedEvent(admin, "projection-start", now.AddHours(-1));
        var startAttempt = new ScheduledEventStartAttempt(Guid.NewGuid(), startEvent.Id, startEvent.EventStartsAt!.Value, now.AddMinutes(-4), false, ["BOARD_NOT_PUBLISHED"]);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, openingEvent, openingAttempt, startEvent, startAttempt);
            await setup.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var projection = await Shell(db).GetAdminActionsAsync(CancellationToken.None);
            Assert.Equal(2, projection.Count);
            Assert.True(projection.ForEvent(openingEvent.Id).ScheduledOpeningFailed);
            Assert.True(projection.ForEvent(startEvent.Id).ScheduledStartPostponed);

            var currentOpening = await db.Events.SingleAsync(item => item.Id == openingEvent.Id);
            currentOpening.ConfigureSchedule(now.AddHours(2), now.AddHours(3), null, now.AddHours(4), now.AddDays(1), 10);
            await db.SaveChangesAsync();
            Assert.False((await Shell(db).GetAdminActionsAsync(CancellationToken.None)).ForEvent(openingEvent.Id).HasActions);

            var currentStart = await db.Events.SingleAsync(item => item.Id == startEvent.Id);
            currentStart.StartEvent(now);
            await db.SaveChangesAsync();
            Assert.False((await Shell(db).GetAdminActionsAsync(CancellationToken.None)).ForEvent(startEvent.Id).HasActions);
        }
    }

    [Fact]
    public async Task HiddenEventsAreExcludedFromActionsAndPersonalNotifications()
    {
        var fixture = await SeedPendingEventAsync("projection-hidden", pendingCount: 1, hide: true);
        var notice = new PersonalNotification(Guid.NewGuid(), fixture.Admin.Id, "event.cancelled", "hidden reminder", "/Events/hidden", now, fixture.Event.Id);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.PersonalNotifications.Add(notice);
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var shell = Shell(db);
        var projection = await shell.GetAdminActionsAsync(CancellationToken.None);
        Assert.Equal(0, projection.Count);
        Assert.DoesNotContain(fixture.Event.Id, projection.EventIds);

        var inbox = await shell.GetNotificationsAsync(Principal(fixture.Admin), CancellationToken.None);
        Assert.Equal(0, inbox.PersonalCount);
        Assert.DoesNotContain(inbox.Items, item => item.Id == notice.Id);
    }

    private SharedShellService Shell(ApplicationDbContext db) => new(db, new PassthroughLocalizer(), new FixedTimeProvider(now));

    private async Task<PendingFixture> SeedPendingEventAsync(string slug, int pendingCount, bool hide = false)
    {
        var admin = Admin(slug);
        var item = DraftEvent(admin, slug, now.AddHours(-1));
        var team = new Team(Guid.NewGuid(), item.Id, "Projection team", $"projection-team-{slug}", TeamFormationType.Preformed, null, false, now);
        var board = new Board(Guid.NewGuid(), item.Id, "Projection board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Projection tile", "", "", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, false, false, "Projection requirement", true);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddMinutes(-30), SignupSource.AdminCreated);
        participant.AssignOwner(admin);
        var character = new OsrsCharacter(Guid.NewGuid(), "Projection player", "PROJECTION PLAYER", now);
        var submissions = Enumerable.Range(0, pendingCount)
            .Select(index => new Submission(Guid.NewGuid(), item.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, admin.Id, 1, now.AddMinutes(-index - 1), null, null))
            .ToArray();
        if (hide)
        {
            item.ConfigureSignup(true, false, null);
            item.OpenSignups(now.AddHours(-2));
            item.CloseSignups(now.AddHours(-1));
            item.StartEvent(now.AddHours(-2));
            item.EndEvent(now.AddHours(-1));
            item.Hide(admin.Id, now.AddMinutes(-30), item.Name, "hidden for projection test");
        }

        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, item, team, board, tile, requirement, participant, character);
        db.Submissions.AddRange(submissions);
        await db.SaveChangesAsync();
        return new(admin, item);
    }

    private BingoEvent DraftEvent(Account admin, string slug, DateTimeOffset opening)
    {
        var item = new BingoEvent(Guid.NewGuid(), $"{slug} event", $"{slug}-{Guid.NewGuid():N}", "UTC", admin.Id, now.AddDays(-1), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(opening, opening.AddHours(1), null, now.AddHours(2), now.AddDays(1), 10);
        return item;
    }

    private BingoEvent ClosedEvent(Account admin, string slug, DateTimeOffset startedAt)
    {
        var item = new BingoEvent(Guid.NewGuid(), $"{slug} event", $"{slug}-{Guid.NewGuid():N}", "UTC", admin.Id, now.AddDays(-1), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, startedAt, now.AddDays(1), 10);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        return item;
    }

    private static Account Admin(string suffix)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), $"Admin {suffix}", $"ADMIN-{suffix.ToUpperInvariant()}-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        account.SetGlobalRole(GlobalRole.Admin);
        return account;
    }

    private static ClaimsPrincipal Principal(Account account) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
        new Claim(ClaimTypes.Name, account.PublicUsername!),
        new Claim(ClaimTypes.Role, "Admin")
    ], "test"));

    private sealed record PendingFixture(Account Admin, BingoEvent Event);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class PassthroughLocalizer : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
