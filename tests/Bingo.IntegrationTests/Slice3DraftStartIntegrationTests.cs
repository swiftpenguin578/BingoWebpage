using System.Security.Claims;
using Bingo.Application.Events;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3DraftStartIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice3_draft_start")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private readonly DateTimeOffset now = new(2026, 7, 27, 18, 0, 0, TimeSpan.Zero);
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
    public async Task SignupClosedStartsDraftOnceWithoutReclosingSignupOrDuplicatingSideEffects()
    {
        var eventId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var closedAt = now.AddHours(-1);
        await using (var setup = new ApplicationDbContext(options))
        {
            var seededItem = SeedDraftSetup(setup, eventId, "closed-draft-start", EventState.SignupClosed, adminId, closedAt);
            setup.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), eventId, EventState.SignupOpen, EventState.SignupClosed, adminId, closedAt, "Signup closed before draft."));
            await setup.SaveChangesAsync();
            Assert.Equal(closedAt, seededItem.ActualSignupClosedAt);
        }

        var notifier = new CountingNotifier();
        var results = await Task.WhenAll(StartInNewContext(eventId, adminId, notifier), StartInNewContext(eventId, adminId, notifier));
        Assert.All(results, result => Assert.IsType<RedirectToPageResult>(result.Result));
        await StartInNewContext(eventId, adminId, notifier);

        await using var verify = new ApplicationDbContext(options);
        var item = await verify.Events.SingleAsync(value => value.Id == eventId);
        var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == eventId);
        Assert.Equal(EventState.SignupClosed, item.State);
        Assert.Equal(closedAt, item.ActualSignupClosedAt);
        Assert.True(item.DraftLocked);
        Assert.Equal(DraftState.Running, draft.State);
        Assert.Equal(adminId, draft.ControllerAccountId);
        Assert.All(await verify.Teams.Where(value => value.EventId == eventId).ToListAsync(), team => Assert.Null(team.DraftPosition));
        Assert.Single(await verify.EventStateTransitions.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.TargetId == draft.Id.ToString() && value.Action == "draft.control_acquired").ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(value => value.TargetId == draft.Id.ToString() && value.Action == "draft.started").ToListAsync());
        Assert.Equal(1, notifier.DraftChanges);
    }

    [Fact]
    public async Task NonSignupClosedStatesRejectBeforeAnyDraftMutation()
    {
        var adminId = Guid.NewGuid();
        var states = new[]
        {
            EventState.Draft, EventState.SignupOpen, EventState.Live, EventState.AwaitingFinalReview,
            EventState.Finalized, EventState.Archived, EventState.Cancelled, EventState.Discarded
        };
        var ids = new Dictionary<EventState, Guid>();
        await using (var setup = new ApplicationDbContext(options))
        {
            foreach (var state in states)
            {
                var eventId = Guid.NewGuid();
                ids[state] = eventId;
                SeedDraftSetup(setup, eventId, $"rejected-{state.ToString().ToLowerInvariant()}", state, adminId, now.AddHours(-1));
            }
            await setup.SaveChangesAsync();
        }

        var notifier = new CountingNotifier();
        foreach (var state in states)
        {
            var result = await StartInNewContext(ids[state], adminId, notifier);
            Assert.IsType<RedirectToPageResult>(result.Result);
            Assert.Contains(
                state is EventState.Draft or EventState.SignupOpen ? "Close signup" : "Signup Closed",
                result.Message,
                StringComparison.Ordinal);
        }

        await using var verify = new ApplicationDbContext(options);
        foreach (var eventId in ids.Values)
        {
            var item = await verify.Events.SingleAsync(value => value.Id == eventId);
            var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == eventId);
            Assert.False(item.DraftLocked);
            Assert.Equal(DraftState.Setup, draft.State);
            Assert.Equal(adminId, draft.ControllerAccountId);
            Assert.Equal([1, 2], await verify.Teams.Where(value => value.EventId == eventId).OrderBy(value => value.DraftPosition).Select(value => value.DraftPosition).ToArrayAsync());
            Assert.Empty(await verify.AuditEntries.Where(value => value.TargetId == draft.Id.ToString()).ToListAsync());
        }
        Assert.Equal(0, notifier.DraftChanges);
    }

    private BingoEvent SeedDraftSetup(ApplicationDbContext db, Guid eventId, string slug, EventState state, Guid adminId, DateTimeOffset closedAt)
    {
        var item = new BingoEvent(eventId, slug, slug, "UTC", adminId, now.AddDays(-2));
        item.ConfigureSchedule(now.AddDays(-2), closedAt, null, now.AddDays(1), now.AddDays(2), 20);
        item.ConfigureSignup(true, false, null);
        if (state is not (EventState.Draft or EventState.Cancelled or EventState.Discarded)) item.OpenSignups(now.AddDays(-2));
        if (state is not (EventState.Draft or EventState.SignupOpen or EventState.Cancelled or EventState.Discarded)) item.CloseSignups(closedAt);
        if (state is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived) item.StartEvent(now);
        if (state is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived) item.EndEvent(now.AddHours(1));
        if (state is EventState.Finalized or EventState.Archived) item.FinalizeResults(now.AddHours(2));
        if (state is EventState.Archived) item.Archive(now.AddHours(3));
        if (state is EventState.Cancelled) item.Cancel(adminId, now, "Cancelled test event.", true);
        if (state is EventState.Discarded) item.Discard(adminId, now, false);

        var draft = new DraftSession(Guid.NewGuid(), eventId, 1);
        draft.AcquireControl(adminId, now, DraftControlLease.Duration);
        var firstTeam = new Team(Guid.NewGuid(), eventId, "Team One", $"{slug}-one", TeamFormationType.Drafted, null, true);
        var secondTeam = new Team(Guid.NewGuid(), eventId, "Team Two", $"{slug}-two", TeamFormationType.Drafted, null, true);
        firstTeam.SetDraftPosition(1);
        secondTeam.SetDraftPosition(2);
        var firstParticipant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        var secondParticipant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 2, now, SignupSource.Website);
        var firstCharacter = new OsrsCharacter(Guid.NewGuid(), $"{slug} one", $"{slug.ToUpperInvariant()} ONE", now);
        var secondCharacter = new OsrsCharacter(Guid.NewGuid(), $"{slug} two", $"{slug.ToUpperInvariant()} TWO", now);
        db.AddRange(
            item,
            draft,
            firstTeam,
            secondTeam,
            firstParticipant, secondParticipant, firstCharacter, secondCharacter,
            new EventParticipantCharacter(Guid.NewGuid(), eventId, firstParticipant.Id, firstCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 1m, EhbSource.Manual, null),
            new EventParticipantCharacter(Guid.NewGuid(), eventId, secondParticipant.Id, secondCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 1m, EhbSource.Manual, null),
            new TeamMembership(Guid.NewGuid(), firstTeam.Id, firstParticipant.Id, TeamMembershipRole.Captain, now, null, "Captain preassignment"),
            new TeamMembership(Guid.NewGuid(), secondTeam.Id, secondParticipant.Id, TeamMembershipRole.Captain, now, null, "Captain preassignment"));
        return item;
    }

    private async Task<(IActionResult Result, string Message)> StartInNewContext(Guid eventId, Guid adminId, CountingNotifier notifier)
    {
        await using var db = new ApplicationDbContext(options);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Name, "draft-admin")], "test"))
        };
        var model = new DraftModel(db, new FixedTimeProvider(now), new AuditWriter(db, new FixedTimeProvider(now)), notifier, null!, null!)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
        };
        var result = await model.OnPostStartAsync(eventId, CancellationToken.None);
        return (result, model.TempData["StatusMessage"]?.ToString() ?? string.Empty);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class CountingNotifier : IAdminCollaborationNotifier
    {
        private int draftChanges;
        public int DraftChanges => draftChanges;
        public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref draftChanges);
            return Task.CompletedTask;
        }
        public Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
