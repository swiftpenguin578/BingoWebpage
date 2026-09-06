using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Events;
using Bingo.Web.Pages;
using Bingo.Web.Pages.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3PublicCurrentSelectionIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice3_current_selection")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private readonly DateTimeOffset now = new(2026, 7, 27, 15, 0, 0, TimeSpan.Zero);
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
    public async Task DevelopmentFixtureFilteringIsEnvironmentSpecificAndPublicOverviewListsAllEligibleEvents()
    {
        await using var db = new ApplicationDbContext(options);
        await AddCurrentEventAsync(db, "fixture-current", fixture: true);
        await AddCurrentEventAsync(db, "fixture-unpublished", fixture: true, publishBoard: false);

        var development = new IndexModel(db, new TestEnvironment(Environments.Development));
        await development.OnGetAsync(CancellationToken.None);
        var developmentFixture = Assert.Single(development.Events);
        Assert.Equal("fixture-current", developmentFixture.Slug);
        Assert.Equal(EventDestination.Board, developmentFixture.Destination);

        var production = new IndexModel(db, new TestEnvironment(Environments.Production));
        await production.OnGetAsync(CancellationToken.None);
        var boardOnly = Assert.Single(production.Events);
        Assert.Equal("fixture-current", boardOnly.Slug);
        Assert.Equal(EventDestination.Board, boardOnly.Destination);

        await AddCurrentEventAsync(db, "real-current-one", fixture: false);
        development = new IndexModel(db, new TestEnvironment(Environments.Development));
        await development.OnGetAsync(CancellationToken.None);
        Assert.Equal(2, development.Events.Count);
        Assert.Contains(development.Events, item => item.Slug == "fixture-current");
        Assert.Contains(development.Events, item => item.Slug == "real-current-one");
        production = new IndexModel(db, new TestEnvironment(Environments.Production));
        await production.OnGetAsync(CancellationToken.None);
        Assert.Equal(2, production.Events.Count);
        Assert.Contains(production.Events, item => item.Slug == "fixture-current");
        Assert.Contains(production.Events, item => item.Slug == "real-current-one");

        await AddCurrentEventAsync(db, "real-current-two", fixture: false);
        development = new IndexModel(db, new TestEnvironment(Environments.Development));
        await development.OnGetAsync(CancellationToken.None);
        Assert.Equal(3, development.Events.Count);
        Assert.Contains(development.Events, item => item.Slug == "fixture-current");
        Assert.DoesNotContain(development.Events, item => item.Slug == "fixture-unpublished");
        Assert.Contains(development.Events, item => item.Slug == "real-current-one");
        Assert.Contains(development.Events, item => item.Slug == "real-current-two");

        Assert.NotNull(await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == "fixture-current"));
        Assert.NotNull(await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == db.Events.Where(e => e.Slug == "fixture-current").Select(e => e.Id).Single()));
    }

    [Fact]
    public async Task PublicOverviewRequiresAPublishedFactAndUsesRosterOrBoardWithoutFirstPublicAt()
    {
        await using var db = new ApplicationDbContext(options);
        await AddPublicationCaseAsync(db, "private", rosterPublished: false, boardPublished: false);
        await AddPublicationCaseAsync(db, "roster-only", rosterPublished: true, boardPublished: false);
        await AddPublicationCaseAsync(db, "board-published", rosterPublished: false, boardPublished: true);

        var model = new IndexModel(db, new TestEnvironment(Environments.Development));
        await model.OnGetAsync(CancellationToken.None);

        Assert.DoesNotContain(model.Events, item => item.Slug == "private");
        Assert.Equal(EventDestination.Roster, Assert.Single(model.Events, item => item.Slug == "roster-only").Destination);
        Assert.Equal(EventDestination.Board, Assert.Single(model.Events, item => item.Slug == "board-published").Destination);
    }

    [Fact]
    public async Task PublicSignupOpenDiscoveryRoutesToSignupAndPrivateSlugFailsClosed()
    {
        await using var db = new ApplicationDbContext(options);
        await AddSignupOnlyEventAsync(db, "public-signup", publicEvent: true);
        await AddSignupOnlyEventAsync(db, "private-signup", publicEvent: false);

        var model = new IndexModel(db, new TestEnvironment(Environments.Development));
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(EventDestination.Signup, Assert.Single(model.Events, item => item.Slug == "public-signup").Destination);
        Assert.DoesNotContain(model.Events, item => item.Slug == "private-signup");

        var signup = new SignupModel(db, null!, TimeProvider.System);
        Assert.IsType<NotFoundResult>(await signup.GetForTestAsync("private-signup", CancellationToken.None));
    }

    [Fact]
    public async Task TeamsContextNavigationAppearsOnlyAfterBoardPublication()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var slug = $"teams-navigation-{Guid.NewGuid():N}";
        var item = new BingoEvent(eventId, "Teams navigation", slug, "UTC", actorId, now);
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddDays(-1), now.AddDays(2), 1);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.MarkFirstPublic(now.AddDays(-1));
        var participant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now.AddDays(-2), SignupSource.Website);
        var team = new Team(Guid.NewGuid(), eventId, "Navigation team", "navigation-team", TeamFormationType.Drafted, null, true);
        team.Finalize(now.AddDays(-1));
        var draft = new DraftSession(Guid.NewGuid(), eventId, 1);
        var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddDays(-1), actorId);
        var roster = new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, participant.Id, TeamMembershipRole.Participant, 1, "Navigation player");
        var board = new Board(Guid.NewGuid(), eventId, "Navigation board", 1, 1);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(item, participant, team, draft, cycle, roster, board);
            await db.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient();
        var before = await client.GetStringAsync($"/Events/{slug}/Teams");
        Assert.DoesNotContain("public-ui-header-context-nav", before, StringComparison.Ordinal);
        Assert.DoesNotContain($"/Events/{slug}/Board", before, StringComparison.Ordinal);

        await using (var db = new ApplicationDbContext(options))
        {
            var publishedBoard = await db.Boards.SingleAsync(value => value.Id == board.Id);
            await BoardApprovalFixture.PublishAsync(db, publishedBoard, now);
            var publishedEvent = await db.Events.SingleAsync(value => value.Id == eventId);
            publishedEvent.SetBoardPublication(true, now);
            await db.SaveChangesAsync();
        }

        var after = await client.GetStringAsync($"/Events/{slug}/Teams");
        Assert.Contains("public-ui-header-context-nav", after, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Events/{slug}/Board\"", after, StringComparison.Ordinal);
        Assert.Contains("Teams / Hold", after, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", after, StringComparison.Ordinal);
    }

    private async Task AddCurrentEventAsync(ApplicationDbContext db, string slug, bool fixture, bool publishBoard = true)
    {
        var eventId = Guid.NewGuid();
        var item = new BingoEvent(eventId, slug, slug, "UTC", Guid.NewGuid(), now);
        item.UpdateIdentity(slug, slug, "Public event description", "UTC");
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddDays(-1), now.AddDays(1), 20);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(now.AddDays(-2));
        item.MarkFirstPublic(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now);
        db.Events.Add(item);
        if (fixture)
            db.Entry(item).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;

        var team = new Team(Guid.NewGuid(), eventId, $"{slug} team", $"{slug}-team", TeamFormationType.Drafted, null, true);
        team.Finalize(now);
        db.Add(team);
        if (publishBoard)
        {
            var board = new Board(Guid.NewGuid(), eventId, $"{slug} board", 1, 1);
            db.Add(board);
            await BoardApprovalFixture.PublishAsync(db, board, now);
        }
        await db.SaveChangesAsync();
    }

    private async Task AddPublicationCaseAsync(ApplicationDbContext db, string slug, bool rosterPublished, bool boardPublished)
    {
        var eventId = Guid.NewGuid();
        var item = new BingoEvent(eventId, slug, slug, "UTC", Guid.NewGuid(), now);
        item.UpdateIdentity(slug, slug, "Public event description", "UTC");
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddDays(-1), now.AddDays(1), 20);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        db.Events.Add(item);

        if (rosterPublished)
        {
            var draft = new DraftSession(Guid.NewGuid(), eventId, 1);
            db.Add(draft);
            db.Add(new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now, Guid.NewGuid()));
        }

        if (boardPublished)
        {
            var board = new Board(Guid.NewGuid(), eventId, $"{slug} board", 1, 1);
            db.Add(board);
            await BoardApprovalFixture.PublishAsync(db, board, now);
        }

        await db.SaveChangesAsync();
    }

    private async Task AddSignupOnlyEventAsync(ApplicationDbContext db, string slug, bool publicEvent)
    {
        var eventId = Guid.NewGuid();
        var item = new BingoEvent(eventId, slug, slug, "UTC", Guid.NewGuid(), now);
        item.UpdateIdentity(slug, slug, "Public signup event", "UTC");
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(5), null, now.AddDays(7), now.AddDays(12), 6);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(now.AddDays(-2));
        if (publicEvent) item.MarkFirstPublic(now.AddDays(-2));
        db.Events.Add(item);
        await db.SaveChangesAsync();
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Bingo.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
