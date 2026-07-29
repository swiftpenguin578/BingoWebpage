using System.Security.Claims;
using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3FinalizationAtomicityIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("slice3_finalization_atomicity").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly DateTimeOffset now = new(2026, 7, 27, 20, 0, 0, TimeSpan.Zero);
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
    public async Task RealFinalizeHandlerRollsBackCompletelyThenCreatesOneCompleteOccurrenceUnderRetries()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Events.Add(AwaitingReview(eventId, actorId));
            await setup.SaveChangesAsync();
        }

        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnFinalizationAudit()).Options;
        await using (var failing = new ApplicationDbContext(failingOptions))
        {
            var failed = FinalizeHandler(failing, actorId, teamId);
            Assert.IsType<RedirectToPageResult>(await failed.OnPostFinalizeAsync(eventId, CancellationToken.None));
        }
        await AssertNoFinalizationAsync(eventId);

        var competingId = Guid.NewGuid();
        await using (var competing = new ApplicationDbContext(options))
        {
            competing.Events.Add(Live(competingId, actorId));
            await competing.SaveChangesAsync();
        }
        await using (var blocked = new ApplicationDbContext(options))
        {
            Assert.IsType<RedirectToPageResult>(await FinalizeHandler(blocked, actorId, teamId).OnPostFinalizeAsync(eventId, CancellationToken.None));
        }
        await AssertNoFinalizationAsync(eventId);
        await using (var removeCompeting = new ApplicationDbContext(options))
            await removeCompeting.Events.Where(value => value.Id == competingId).ExecuteDeleteAsync();

        var results = await Task.WhenAll(
            FinalizeInNewContext(eventId, actorId, teamId),
            FinalizeInNewContext(eventId, actorId, teamId));
        Assert.All(results, result => Assert.IsType<RedirectToPageResult>(result));

        await using (var verify = new ApplicationDbContext(options))
        {
            var item = await verify.Events.SingleAsync(value => value.Id == eventId);
            Assert.Equal(EventState.Finalized, item.State);
            Assert.True(item.ResultsPublished);
            var snapshot = Assert.Single(await verify.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
            var placement = Assert.Single(await verify.OfficialPlacements.Where(value => value.FinalizationId == snapshot.Id).ToListAsync());
            Assert.Equal(teamId, placement.TeamId);
            Assert.Single(await verify.EventStateTransitions.Where(value => value.EventId == eventId && value.FromState == EventState.AwaitingFinalReview && value.ToState == EventState.Finalized).ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.finalized").ToListAsync());
        }

        Assert.IsType<RedirectToPageResult>(await FinalizeInNewContext(eventId, actorId, teamId));
        await using var repeated = new ApplicationDbContext(options);
        Assert.Single(await repeated.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Single(await repeated.EventStateTransitions.Where(value => value.EventId == eventId && value.ToState == EventState.Finalized).ToListAsync());
        Assert.Single(await repeated.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.finalized").ToListAsync());
    }

    private async Task<IActionResult> FinalizeInNewContext(Guid eventId, Guid actorId, Guid teamId)
    {
        await using var db = new ApplicationDbContext(options);
        return await FinalizeHandler(db, actorId, teamId).OnPostFinalizeAsync(eventId, CancellationToken.None);
    }

    private FinalizeModel FinalizeHandler(ApplicationDbContext db, Guid actorId, Guid teamId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim(ClaimTypes.Name, "finalization-admin")], "test"))
        };
        return new FinalizeModel(new EventFinalizationService(db, new ReadyBoard(teamId), new FixedClock(now)), db, null!)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
        };
    }

    private BingoEvent AwaitingReview(Guid eventId, Guid actorId)
    {
        var item = new BingoEvent(eventId, "atomic-finalization", "atomic-finalization", "UTC", actorId, now.AddDays(-2));
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-3), now.AddHours(-2), 20);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now.AddHours(-3));
        item.EndEvent(now.AddHours(-2));
        return item;
    }

    private BingoEvent Live(Guid eventId, Guid actorId)
    {
        var item = new BingoEvent(eventId, "competing-current", "competing-current", "UTC", actorId, now.AddDays(-2));
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-3), now.AddHours(2), 20);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now.AddHours(-3));
        return item;
    }

    private async Task AssertNoFinalizationAsync(Guid eventId)
    {
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.AwaitingFinalReview, (await verify.Events.SingleAsync(value => value.Id == eventId)).State);
        Assert.Empty(await verify.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.OfficialPlacements.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.EventStateTransitions.Where(value => value.EventId == eventId && value.ToState == EventState.Finalized).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.finalized").ToListAsync());
    }

    private sealed class ReadyBoard(Guid teamId) : IPublicBoardService
    {
        public Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<PublicEventBoard?>(new PublicEventBoard(Guid.Empty, "Atomic finalization", eventSlug, EventState.AwaitingFinalReview, 1, 1, 0,
                [new PublicTeamBoard(teamId, "Winners", "winners", null, null, 1, false,
                    new([], 0, [], [], false, null, 0, []), [])], [], []));
        public Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default) => Task.FromResult<PublicTileDetails?>(null);
    }

    private sealed class ThrowOnFinalizationAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == "event.finalized")
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated finalization audit failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class FixedClock(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
