using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3ScheduleLifecycleIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_slice3_schedule").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly DateTimeOffset now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DiscordAuthentication:ClientId"] = "test-client", ["DiscordAuthentication:ClientSecret"] = "test-secret" }).Build();

    public async Task InitializeAsync() { await database.StartAsync(); options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options; await using var db = new ApplicationDbContext(options); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ReadinessUsesStableCodesAndOpenCloseReopenAreOneTransactionalLifecycle()
    {
        var actor = new LifecycleActor(Guid.NewGuid(), "schedule-admin");
        var eventId = await SeedReadyDraftAsync("lifecycle", waitingList: false);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var initial = await evaluator.GetSignupReadinessAsync(eventId, SignupOpeningMode.OpenNow, now);
            Assert.Contains(initial!.Warnings, item => item.Code == "WAITING_LIST_DISABLED");
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var version = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId)).Version;
            Assert.False((await service.OpenAsync(eventId, version, false, false, actor)).Succeeded);
            Assert.True((await service.OpenAsync(eventId, version, true, false, actor)).Succeeded);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var opened = await db.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.SignupOpen, opened.State);
            Assert.Equal(now, opened.ActualSignupOpenedAt);
            Assert.NotNull(opened.FirstPublicAt);
            db.EventParticipants.Add(new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now, SignupSource.Website));
            await db.SaveChangesAsync();
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            Assert.True((await service.CloseAsync(eventId, opened.Version, actor)).Succeeded);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var closed = await db.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(now, closed.ActualSignupClosedAt);
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            Assert.False((await service.ReopenAsync(eventId, closed.Version, false, false, actor)).Succeeded);
            Assert.True((await service.ReopenAsync(eventId, closed.Version, true, false, actor)).Succeeded);
            Assert.Equal(3, await db.EventStateTransitions.CountAsync(x => x.EventId == eventId));
            Assert.Equal(3, await db.AuditEntries.CountAsync(x => x.EventId == eventId && x.Action.StartsWith("event.signup_")));
        }
    }

    [Fact]
    public async Task ScheduleStaleCurrentEventAndAuditFailureLeaveNoPartialMutation()
    {
        var actor = new LifecycleActor(Guid.NewGuid(), "schedule-admin");
        var first = await SeedReadyDraftAsync("current-first", waitingList: true);
        var second = await SeedReadyDraftAsync("current-second", waitingList: true);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration); var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var firstVersion = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == first)).Version;
            Assert.True((await service.OpenAsync(first, firstVersion, true, false, actor)).Succeeded);
            var secondVersion = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == second)).Version;
            var blocked = await service.OpenAsync(second, secondVersion, true, false, actor);
            Assert.False(blocked.Succeeded); Assert.Contains("overlaps", blocked.Error);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration); var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var item = await db.Events.SingleAsync(x => x.Id == second);
            var originalVersion = item.Version;
            var values = Values(item, item.EventEndsAt!.Value.AddDays(1));
            Assert.True((await service.SaveScheduleAsync(second, originalVersion, values, false, false, null, actor)).Succeeded);
            var stale = await service.SaveScheduleAsync(second, originalVersion, values, false, false, null, actor);
            Assert.False(stale.Succeeded); Assert.Contains("changed", stale.Error);

            var current = await db.Events.AsNoTracking().SingleAsync(x => x.Id == first);
            var failed = await service.CloseAsync(first, current.Version, new LifecycleActor(actor.Id, new string('x', 101)));
            Assert.False(failed.Succeeded);
            db.ChangeTracker.Clear();
            Assert.Equal(EventState.SignupOpen, (await db.Events.SingleAsync(x => x.Id == first)).State);
        }
    }

    [Fact]
    public async Task ProposedCloseAndPublicScheduleConfirmationAreAuthoritative()
    {
        var actor = new LifecycleActor(Guid.NewGuid(), "schedule-admin");
        var proposalEvent = await SeedReadyDraftAsync("proposed-close", waitingList: true, signupClose: false);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var readiness = await evaluator.GetSignupReadinessAsync(proposalEvent, SignupOpeningMode.OpenNow, now);
            Assert.True(readiness!.CloseDecision.RequiresAcceptance);
            Assert.Equal(now.AddDays(2), readiness.CloseDecision.ProposedClose);
            var version = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == proposalEvent)).Version;
            var proposal = await service.OpenAsync(proposalEvent, version, true, false, actor);
            Assert.False(proposal.Succeeded);
            Assert.Equal(now.AddDays(2), proposal.ProposedClose);
            Assert.True((await service.OpenAsync(proposalEvent, version, true, true, actor)).Succeeded);
        }

        var validEvent = await SeedReadyDraftAsync("valid-close", waitingList: true, startDays: 20, endDays: 22);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var readiness = await evaluator.GetSignupReadinessAsync(validEvent, SignupOpeningMode.OpenNow, now);
            Assert.False(readiness!.CloseDecision.RequiresAcceptance);
            var version = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == validEvent)).Version;
            Assert.True((await service.OpenAsync(validEvent, version, true, false, actor)).Succeeded);
        }

        var publicEvent = await SeedReadyDraftAsync("public-schedule", waitingList: true);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == publicEvent);
            item.MarkFirstPublic(now);
            item.ConfigureSchedule(now.AddDays(-1), now.AddDays(1), null, now.AddDays(2), now.AddDays(4), item.ParticipantCap);
            await db.SaveChangesAsync();

            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var values = new EventScheduleValues(now.AddHours(1), item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap);
            Assert.False((await service.SaveScheduleAsync(publicEvent, item.Version, values, false, false, null, actor)).Succeeded);
            Assert.False((await service.SaveScheduleAsync(publicEvent, item.Version, values, false, true, null, actor)).Succeeded);
            Assert.True((await service.SaveScheduleAsync(publicEvent, item.Version, values, false, true, "Correcting the announced schedule.", actor)).Succeeded);
        }
    }

    [Fact]
    public async Task NonOverlappingSignupWindowsAreAllowedButOverlapAndPastReopenNeedSafeConfirmation()
    {
        var actor = new LifecycleActor(Guid.NewGuid(), "schedule-admin");
        var first = await SeedReadyDraftAsync("window-first", waitingList: true);
        var second = await SeedReadyDraftAsync("window-second", waitingList: true, startDays: 4, endDays: 6);
        var overlap = await SeedReadyDraftAsync("window-overlap", waitingList: true, startDays: 5, endDays: 7);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            Assert.True((await service.OpenAsync(first, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == first)).Version, true, false, actor)).Succeeded);
            Assert.True((await service.OpenAsync(second, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == second)).Version, true, false, actor)).Succeeded);
            var blocked = await service.OpenAsync(overlap, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == overlap)).Version, true, false, actor);
            Assert.False(blocked.Succeeded);
            Assert.Contains("overlaps", blocked.Error);
        }

        var reopening = await SeedReadyDraftAsync("reopen-expired", waitingList: true, signupClose: false, startDays: 10, endDays: 12);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == reopening);
            item.ConfigureSchedule(now.AddDays(-2), now.AddMinutes(-1), item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap);
            item.OpenSignups(now.AddDays(-1));
            item.CloseSignups(now);
            await db.SaveChangesAsync();
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var proposal = await service.ReopenAsync(reopening, item.Version, true, false, actor);
            Assert.False(proposal.Succeeded);
            Assert.Equal(item.EventStartsAt, proposal.ProposedClose);
            Assert.True((await service.ReopenAsync(reopening, item.Version, true, true, actor)).Succeeded);
        }
    }

    [Fact]
    public async Task ClosedSignupCapacityIncreasePromotesTheWaitingQueue()
    {
        var actor = new LifecycleActor(Guid.NewGuid(), "schedule-admin");
        var eventId = await SeedReadyDraftAsync("closed-capacity", waitingList: true, startDays: 20, endDays: 22);
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        item.OpenSignups(now);
        item.CloseSignups(now);
        db.EventParticipants.AddRange(new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now, SignupSource.Website), new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.WaitingList, 2, now.AddMinutes(1), SignupSource.Website));
        item.ConfigureSchedule(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, 1);
        await db.SaveChangesAsync();
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
        var result = await service.SaveScheduleAsync(eventId, item.Version, Values(item, item.EventEndsAt!.Value) with { ParticipantCap = 2 }, false, false, null, actor);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.PromotedParticipants);
        Assert.Equal(2, await db.EventParticipants.CountAsync(x => x.EventId == eventId && x.SignupStatus == SignupStatus.Confirmed));
    }

    [Fact]
    public async Task FutureSignupCanFollowEveryNonOverlappingOperationalStateButOverlapNeverMutates()
    {
        var actor = new LifecycleActor(Guid.NewGuid(), "schedule-admin");
        var states = new[] { EventState.Live, EventState.AwaitingFinalReview, EventState.Finalized };
        for (var index = 0; index < states.Length; index++)
        {
            var state = states[index];
            var start = now.AddDays(30 + index * 10);
            var end = start.AddDays(2);
            var candidate = await SeedOperationalEventAsync($"{state}-window", state, start, end);
            var future = await SeedReadyDraftAsync($"{state}-future", waitingList: true, startDays: 32 + index * 10, endDays: 34 + index * 10);
            var overlapping = await SeedReadyDraftAsync($"{state}-overlap", waitingList: true, startDays: 31 + index * 10, endDays: 33 + index * 10);
            await using var db = new ApplicationDbContext(options);
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var futureVersion = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == future)).Version;
            Assert.True((await service.OpenAsync(future, futureVersion, true, false, actor)).Succeeded);
            var overlapVersion = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == overlapping)).Version;
            var rejected = await service.OpenAsync(overlapping, overlapVersion, true, false, actor);
            Assert.False(rejected.Succeeded);
            Assert.Contains(state.ToString(), rejected.Error);
            Assert.Equal(EventState.Draft, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == overlapping)).State);
            Assert.Empty(await db.AuditEntries.Where(x => x.EventId == overlapping).ToListAsync());
        }
    }

    private async Task<Guid> SeedReadyDraftAsync(string slug, bool waitingList, bool signupClose = true, int startDays = 2, int endDays = 4)
    {
        await using var db = new ApplicationDbContext(options);
        var item = new BingoEvent(Guid.NewGuid(), slug, slug, "UTC", Guid.NewGuid(), now);
        item.UpdateIdentity(slug, slug, "Public description", "UTC");
        item.ConfigureSchedule(now.AddHours(1), signupClose ? now.AddDays(startDays - 1) : null, null, now.AddDays(startDays), now.AddDays(endDays), 20);
        item.ConfigureSignup(waitingList, false, null);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
        db.AddRange(item, form, regular, captain);
        await db.SaveChangesAsync();
        return item.Id;
    }
    private async Task<Guid> SeedOperationalEventAsync(string slug, EventState state, DateTimeOffset start, DateTimeOffset end)
    {
        await using var db = new ApplicationDbContext(options);
        var item = new BingoEvent(Guid.NewGuid(), slug, slug, "UTC", Guid.NewGuid(), now);
        item.UpdateIdentity(slug, slug, "Public description", "UTC");
        item.ConfigureSchedule(now.AddHours(1), start.AddHours(-1), null, start, end, 20);
        item.OpenSignups(now);
        item.CloseSignups(now);
        item.StartEvent(start);
        if (state is EventState.AwaitingFinalReview or EventState.Finalized) item.EndEvent(end);
        if (state == EventState.Finalized) item.FinalizeResults(end.AddHours(1));
        db.Events.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }
    private static EventScheduleValues Values(BingoEvent item, DateTimeOffset end) => new(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, end, item.ParticipantCap);
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
}
