using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages;
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
    public async Task DevelopmentFixtureFilteringIsEnvironmentSpecificAndFailsClosedForMultipleRealCurrentEvents()
    {
        await using var db = new ApplicationDbContext(options);
        await AddCurrentEventAsync(db, "fixture-current", fixture: true);

        var development = new IndexModel(db, new TestEnvironment(Environments.Development));
        await development.OnGetAsync(CancellationToken.None);
        Assert.Empty(development.Events);

        var production = new IndexModel(db, new TestEnvironment(Environments.Production));
        await production.OnGetAsync(CancellationToken.None);
        Assert.Equal("fixture-current", Assert.Single(production.Events).Slug);

        await AddCurrentEventAsync(db, "real-current-one", fixture: false);
        development = new IndexModel(db, new TestEnvironment(Environments.Development));
        await development.OnGetAsync(CancellationToken.None);
        Assert.Equal("real-current-one", Assert.Single(development.Events).Slug);
        production = new IndexModel(db, new TestEnvironment(Environments.Production));
        await production.OnGetAsync(CancellationToken.None);
        Assert.Empty(production.Events);

        await AddCurrentEventAsync(db, "real-current-two", fixture: false);
        development = new IndexModel(db, new TestEnvironment(Environments.Development));
        await development.OnGetAsync(CancellationToken.None);
        Assert.Empty(development.Events);

        Assert.NotNull(await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == "fixture-current"));
        Assert.NotNull(await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == db.Events.Where(e => e.Slug == "fixture-current").Select(e => e.Id).Single()));
    }

    private async Task AddCurrentEventAsync(ApplicationDbContext db, string slug, bool fixture)
    {
        var eventId = Guid.NewGuid();
        var item = new BingoEvent(eventId, slug, slug, "UTC", Guid.NewGuid(), now);
        item.UpdateIdentity(slug, slug, "Public event description", "UTC");
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddDays(-1), now.AddDays(1), 20);
        item.ConfigureSignup(true, true, false, null);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now);
        db.Events.Add(item);
        if (fixture)
            db.Entry(item).Property(nameof(BingoEvent.IsDevelopmentFixture)).CurrentValue = true;

        var board = new Board(Guid.NewGuid(), eventId, $"{slug} board", 1, 1);
        board.Publish(now);
        var team = new Team(Guid.NewGuid(), eventId, $"{slug} team", $"{slug}-team", TeamFormationType.Drafted, null, true);
        team.Finalize(now);
        db.AddRange(board, team);
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
