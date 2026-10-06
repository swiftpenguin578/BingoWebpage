using System.Security.Claims;
using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
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

public sealed class Slice3FinalizationAtomicityIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("slice3_finalization_atomicity").WithUsername("bingo").WithPassword("bingo_test_password"));
    private readonly DateTimeOffset now = new(2026, 7, 27, 20, 0, 0, TimeSpan.Zero);
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
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
            var admin = Account.CreateWebsite(actorId, "Finalization Admin", "FINALIZATION ADMIN", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            setup.AddRange(admin, AwaitingReview(eventId, actorId));
            setup.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), eventId, EventState.Live, EventState.AwaitingFinalReview, actorId, now.AddHours(-2), "Event ended", effectiveAt: now.AddHours(-2)));
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
            var page = FinalizeHandler(blocked, actorId, teamId);
            Assert.IsType<RedirectToPageResult>(await page.OnPostFinalizeAsync(eventId, CancellationToken.None));
            var message = page.TempData["StatusMessage"]?.ToString() ?? string.Empty;
            Assert.Contains("competing-current", message, StringComparison.Ordinal);
            Assert.Contains("End it first", message, StringComparison.Ordinal);
            Assert.Contains("publish its results", message, StringComparison.Ordinal);
        }
        await AssertNoFinalizationAsync(eventId);
        await using (var transition = new ApplicationDbContext(options))
        {
            var competing = await transition.Events.SingleAsync(value => value.Slug == "competing-current");
            competing.EndEvent(now.AddHours(-1));
            await transition.SaveChangesAsync();
        }
        await using (var blockedDuringReview = new ApplicationDbContext(options))
        {
            var page = FinalizeHandler(blockedDuringReview, actorId, teamId);
            Assert.IsType<RedirectToPageResult>(await page.OnPostFinalizeAsync(eventId, CancellationToken.None));
            var message = page.TempData["StatusMessage"]?.ToString() ?? string.Empty;
            Assert.Contains("competing-current", message, StringComparison.Ordinal);
            Assert.Contains("Publish official results", message, StringComparison.Ordinal);
            Assert.DoesNotContain("End it first", message, StringComparison.Ordinal);
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
            Assert.Equal(EventState.Archived, item.State);
            Assert.True(item.ResultsPublished);
            var snapshot = Assert.Single(await verify.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
            var placement = Assert.Single(await verify.OfficialPlacements.Where(value => value.FinalizationId == snapshot.Id).ToListAsync());
            Assert.Equal(teamId, placement.TeamId);
            Assert.Single(await verify.EventStateTransitions.Where(value => value.EventId == eventId && value.FromState == EventState.AwaitingFinalReview && value.ToState == EventState.Archived).ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.results_published").ToListAsync());
        }

        Assert.IsType<RedirectToPageResult>(await FinalizeInNewContext(eventId, actorId, teamId));
        await using var repeated = new ApplicationDbContext(options);
        Assert.Single(await repeated.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Single(await repeated.EventStateTransitions.Where(value => value.EventId == eventId && value.ToState == EventState.Archived).ToListAsync());
        Assert.Single(await repeated.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.results_published").ToListAsync());

        var publishedVersion = await repeated.Events.Where(value => value.Id == eventId).Select(value => value.Version).SingleAsync();
        var reopenActor = new LifecycleActor(actorId, "finalization-admin");
        var reopenBlockerId = Guid.NewGuid();
        await using (var setupReopenBlocker = new ApplicationDbContext(options))
        {
            setupReopenBlocker.Events.Add(Live(reopenBlockerId, actorId));
            await setupReopenBlocker.SaveChangesAsync();
        }
        await using (var blockedReopen = new ApplicationDbContext(options))
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EventFinalizationService(blockedReopen, new ReadyBoard(teamId), new FixedClock(now))
                    .UnfinalizeAsync(eventId, "valid reason", true, reopenActor, publishedVersion));
            Assert.Contains("competing-current", failure.Message, StringComparison.Ordinal);
            Assert.Contains("End it first", failure.Message, StringComparison.Ordinal);
            Assert.Contains("publish its results", failure.Message, StringComparison.Ordinal);
        }
        await using (var moveReopenBlockerToReview = new ApplicationDbContext(options))
        {
            var blocker = await moveReopenBlockerToReview.Events.SingleAsync(value => value.Id == reopenBlockerId);
            blocker.EndEvent(now.AddHours(-1));
            await moveReopenBlockerToReview.SaveChangesAsync();
        }
        await using (var blockedReopenDuringReview = new ApplicationDbContext(options))
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EventFinalizationService(blockedReopenDuringReview, new ReadyBoard(teamId), new FixedClock(now))
                    .UnfinalizeAsync(eventId, "valid reason", true, reopenActor, publishedVersion));
            Assert.Contains("competing-current", failure.Message, StringComparison.Ordinal);
            Assert.Contains("Publish official results", failure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("End it first", failure.Message, StringComparison.Ordinal);
        }
        await using (var removeReopenBlocker = new ApplicationDbContext(options))
            await removeReopenBlocker.Events.Where(value => value.Id == reopenBlockerId).ExecuteDeleteAsync();

        var overlongReason = new string('r', IEventFinalizationService.MaximumUnfinalizeReasonLength + 1);
        await using (var overlong = new ApplicationDbContext(options))
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EventFinalizationService(overlong, new ReadyBoard(teamId), new FixedClock(now))
                    .UnfinalizeAsync(eventId, overlongReason, true, reopenActor, publishedVersion));
            Assert.Equal("The reopening reason must be 2000 characters or fewer.", failure.Message);
        }
        await using (var verifyOverlong = new ApplicationDbContext(options))
        {
            var item = await verifyOverlong.Events.SingleAsync(value => value.Id == eventId);
            Assert.Equal(EventState.Archived, item.State);
            Assert.Equal(publishedVersion, item.Version);
            Assert.Null((await verifyOverlong.EventFinalizations.SingleAsync(value => value.EventId == eventId)).UnfinalizedAt);
        }

        var exactReason = new string('r', IEventFinalizationService.MaximumUnfinalizeReasonLength);
        await using (var reopen = new ApplicationDbContext(options))
            await new EventFinalizationService(reopen, new ReadyBoard(teamId), new FixedClock(now))
                .UnfinalizeAsync(eventId, exactReason, true, reopenActor, publishedVersion);
        await using (var verifyReopen = new ApplicationDbContext(options))
        {
            var item = await verifyReopen.Events.SingleAsync(value => value.Id == eventId);
            Assert.Equal(EventState.AwaitingFinalReview, item.State);
            Assert.Equal(publishedVersion + 1, item.Version);
            var finalization = await verifyReopen.EventFinalizations.SingleAsync(value => value.EventId == eventId);
            Assert.Equal(IEventFinalizationService.MaximumUnfinalizeReasonLength, finalization.UnfinalizeReason!.Length);
            var transition = await verifyReopen.EventStateTransitions.SingleAsync(value => value.EventId == eventId && value.FromState == EventState.Archived && value.ToState == EventState.AwaitingFinalReview);
            Assert.Equal(1_000, transition.Reason!.Length);
            Assert.EndsWith("…", transition.Reason, StringComparison.Ordinal);
            var audit = await verifyReopen.AuditEntries.SingleAsync(value => value.EventId == eventId && value.Action == "event.unfinalized");
            Assert.Equal(IEventFinalizationService.MaximumUnfinalizeReasonLength, audit.Details!.Length);
        }
    }

    [Fact]
    public async Task FinalizationRequiresTheBoundConfirmationValueWithoutJavaScript()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Events.Add(AwaitingReview(eventId, actorId));
            await setup.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var page = FinalizeHandler(db, actorId, Guid.NewGuid(), withConfirmation: false);
            Assert.IsType<RedirectToPageResult>(await page.OnPostFinalizeAsync(eventId, CancellationToken.None));
            Assert.Contains("Confirm that these placements", page.TempData["StatusMessage"]?.ToString());
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.AwaitingFinalReview, await verify.Events.Where(x => x.Id == eventId).Select(x => x.State).SingleAsync());
        Assert.Empty(await verify.EventFinalizations.Where(x => x.EventId == eventId).ToListAsync());
    }

    [Fact]
    public async Task ReopenReasonOverLimitIsRejectedByPageBeforeTheServiceRuns()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        long initialVersion;
        await using (var setup = new ApplicationDbContext(options))
        {
            var seeded = AwaitingReview(eventId, actorId);
            setup.Events.Add(seeded);
            await setup.SaveChangesAsync();
            initialVersion = seeded.Version;
        }

        var service = new RecordingFinalizationService();
        await using (var db = new ApplicationDbContext(options))
        {
            var page = FinalizeHandler(db, actorId, Guid.NewGuid(), finalization: service);
            page.Reason = new string('r', IEventFinalizationService.MaximumUnfinalizeReasonLength + 1);
            page.ConfirmLifecycleAction = true;
            Assert.IsType<RedirectToPageResult>(await page.OnPostUnfinalizeAsync(eventId, CancellationToken.None));
            Assert.Contains("2000", page.TempData["StatusMessage"]?.ToString(), StringComparison.Ordinal);
        }

        Assert.False(service.Unfinalized);
        await using var verify = new ApplicationDbContext(options);
        var item = await verify.Events.SingleAsync(value => value.Id == eventId);
        Assert.Equal(EventState.AwaitingFinalReview, item.State);
        Assert.Equal(initialVersion, item.Version);
        Assert.Empty(await verify.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
    }

    [Theory]
    [InlineData("2026-01-15T13:00", 1)]
    [InlineData("2026-07-15T14:00", 7)]
    public async Task FinalizeCorrectionParsesEventLocalWinterAndSummerWallTime(string correctedAtLocal, int month)
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, month, 1, 12, 0, 0, TimeSpan.Zero).AddMicroseconds(123456);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Events.Add(new BingoEvent(eventId, "Local completion", $"local-completion-{Guid.NewGuid():N}", "Europe/Copenhagen", actorId, createdAt, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb));
            await setup.SaveChangesAsync();
        }

        var service = new RecordingFinalizationService();
        await using (var db = new ApplicationDbContext(options))
        {
            var page = FinalizeHandler(db, actorId, teamId, finalization: service);
            page.Reason = "Event-local correction coverage.";
            var retired = Assert.IsType<BadRequestObjectResult>(await page.OnPostCorrectCompletionAsync(eventId, teamId, correctedAtLocal, CancellationToken.None));
            Assert.Equal("This final-review action is retired. Resolve the underlying records and publish official results.", retired.Value);
        }

        Assert.Null(service.CorrectedAt);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Draft, await verify.Events.Where(x => x.Id == eventId).Select(x => x.State).SingleAsync());
        Assert.Empty(await verify.TeamCompletionCorrections.Where(x => x.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventId).ToListAsync());
    }

    [Theory]
    [InlineData("2026-03-29T02:30", "does not exist")]
    [InlineData("2026-10-25T02:30", "ambiguous")]
    public async Task FinalizeCorrectionRejectsInvalidAndAmbiguousEventLocalWallTime(string correctedAtLocal, string expectedMessage)
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMicroseconds(234567);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Events.Add(new BingoEvent(eventId, "Invalid local completion", $"invalid-local-{Guid.NewGuid():N}", "Europe/Copenhagen", actorId, createdAt, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb));
            await setup.SaveChangesAsync();
        }

        var service = new RecordingFinalizationService();
        await using (var db = new ApplicationDbContext(options))
        {
            var page = FinalizeHandler(db, actorId, teamId, finalization: service);
            var retired = Assert.IsType<BadRequestObjectResult>(await page.OnPostCorrectCompletionAsync(eventId, teamId, correctedAtLocal, CancellationToken.None));
            Assert.Equal("This final-review action is retired. Resolve the underlying records and publish official results.", retired.Value);
            Assert.DoesNotContain(expectedMessage, retired.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Null(service.CorrectedAt);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Draft, await verify.Events.Where(x => x.Id == eventId).Select(x => x.State).SingleAsync());
        Assert.Empty(await verify.TeamCompletionCorrections.Where(x => x.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventId).ToListAsync());
    }

    [Fact]
    public async Task RetiredCompletionInspectionRejectsWithoutAuditOrWrites()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var admin = Account.CreateWebsite(actorId, "Review Admin", "REVIEW ADMIN", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            setup.AddRange(admin, AwaitingReview(eventId, actorId), new Team(teamId, eventId, "Winners", "winners", TeamFormationType.Drafted, null, true, now));
            setup.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), eventId, EventState.Live, EventState.AwaitingFinalReview, actorId, now.AddHours(-2), "Event ended", effectiveAt: now.AddHours(-2)));
            await setup.SaveChangesAsync();
        }

        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnReviewAudit()).Options;
        await using (var failing = new ApplicationDbContext(failingOptions))
        {
            var service = new EventFinalizationService(failing, new ReadyBoard(teamId, completed: true), new FixedClock(now));
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcknowledgeCompletionTimeAsync(eventId, teamId, actorId, 1, Guid.NewGuid(), expectedInspectionKey: "retired"));
            Assert.Contains("retired", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Empty(await verify.FinalReviewResolutions.Where(x => x.EventId == eventId).ToListAsync());
            Assert.Empty(await verify.TeamCompletionCorrections.Where(x => x.EventId == eventId).ToListAsync());
            Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.completion_time_inspected").ToListAsync());
        }
    }

    [Fact]
    public async Task RetiredFinalReviewMutationsRejectWithoutVersionOrAuditResidue()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var admin = Account.CreateWebsite(actorId, "Review Concurrency Admin", "REVIEW CONCURRENCY ADMIN", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            setup.AddRange(admin, AwaitingReview(eventId, actorId), new Team(teamId, eventId, "Winners", "winners", TeamFormationType.Drafted, null, true, now));
            setup.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), eventId, EventState.Live, EventState.AwaitingFinalReview, actorId, now.AddHours(-2), "Event ended", effectiveAt: now.AddHours(-2)));
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var service = new EventFinalizationService(db, new ReadyBoard(teamId, completed: true), new FixedClock(now));
        var beforeVersion = await db.Events.Where(x => x.Id == eventId).Select(x => x.Version).SingleAsync();
        var acknowledge = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcknowledgeCompletionTimeAsync(eventId, teamId, actorId));
        var correct = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectCompletionAsync(eventId, teamId, now, "retired", actorId));
        var resolve = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveBlockerAsync(eventId, "retired", "retired", true, actorId));
        Assert.Contains("retired", acknowledge.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retired", correct.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retired", resolve.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeVersion, await db.Events.Where(x => x.Id == eventId).Select(x => x.Version).SingleAsync());
        Assert.Empty(await db.FinalReviewResolutions.Where(x => x.EventId == eventId).ToListAsync());
        Assert.Empty(await db.TeamCompletionCorrections.Where(x => x.EventId == eventId).ToListAsync());
        Assert.Empty(await db.AuditEntries.Where(x => x.EventId == eventId).ToListAsync());
    }

    private async Task<IActionResult> FinalizeInNewContext(Guid eventId, Guid actorId, Guid teamId)
    {
        await using var db = new ApplicationDbContext(options);
        return await FinalizeHandler(db, actorId, teamId).OnPostFinalizeAsync(eventId, CancellationToken.None);
    }

    private FinalizeModel FinalizeHandler(ApplicationDbContext db, Guid actorId, Guid teamId, bool withConfirmation = true, IEventFinalizationService? finalization = null)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim(ClaimTypes.Name, "finalization-admin")], "test"))
        };
        return new FinalizeModel(finalization ?? new EventFinalizationService(db, new ReadyBoard(teamId), new FixedClock(now)), db, null!)
        {
            FinalizeConfirmation = withConfirmation ? "PUBLISH_OFFICIAL_RESULTS" : null,
            ExpectedVersion = db.Events.AsNoTracking().Where(x => x.Slug == "atomic-finalization").Select(x => x.State == EventState.Finalized ? x.Version - 1 : x.Version).SingleOrDefault(),
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
        };
    }

    private BingoEvent AwaitingReview(Guid eventId, Guid actorId)
    {
        var item = new BingoEvent(eventId, "atomic-finalization", "atomic-finalization", "UTC", actorId, now.AddDays(-2), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-3), now.AddHours(-2), 20);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now.AddHours(-3));
        item.EndEvent(now.AddHours(-2));
        return item;
    }

    private BingoEvent Live(Guid eventId, Guid actorId)
    {
        var item = new BingoEvent(eventId, "competing-current", "competing-current", "UTC", actorId, now.AddDays(-2), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-3), now.AddHours(2), 20);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now.AddHours(-3));
        return item;
    }

    private async Task AssertNoFinalizationAsync(Guid eventId)
    {
        await using var verify = new ApplicationDbContext(options);
        var eventItem = await verify.Events.SingleAsync(value => value.Id == eventId);
        Assert.Equal(EventState.AwaitingFinalReview, eventItem.State);
        Assert.False(eventItem.ResultsPublished);
        Assert.Null(eventItem.FinalizedAt);
        Assert.Null(eventItem.ArchivedAt);
        Assert.Empty(await verify.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.OfficialPlacements.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.EventStateTransitions.Where(value => value.EventId == eventId && value.ToState == EventState.Finalized).ToListAsync());
        Assert.Empty(await verify.EventStateTransitions.Where(value => value.EventId == eventId && value.ToState == EventState.Archived).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.results_published").ToListAsync());
        Assert.Empty(await verify.PersonalNotifications.Where(value => value.EventId == eventId && value.Title == "event.results_published").ToListAsync());
    }

    private sealed class ReadyBoard(Guid teamId, bool completed = false) : IPublicBoardService
    {
        public Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<PublicEventBoard?>(new PublicEventBoard(Guid.Empty, "Atomic finalization", eventSlug, EventState.AwaitingFinalReview, 1, 1, 0,
                [new PublicTeamBoard(teamId, "Winners", "winners", null, null, 1, false,
                    new([], 0, [], [], completed, completed ? new DateTimeOffset(2026, 7, 27, 19, 0, 0, TimeSpan.Zero) : null, completed ? 1 : 0, []), [])], [], []));
        public Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default) => Task.FromResult<PublicTileDetails?>(null);
    }

    private sealed class RecordingFinalizationService : IEventFinalizationService
    {
        public DateTimeOffset? CorrectedAt { get; private set; }
        public bool Unfinalized { get; private set; }
        public Task<FinalReviewReadiness?> GetReadinessAsync(Guid eventId, CancellationToken ct = default) => Task.FromResult<FinalReviewReadiness?>(null);
        public Task ResolveBlockerAsync(Guid eventId, string blockerKey, string reason, bool confirmed, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task AcknowledgeCompletionTimeAsync(Guid eventId, Guid teamId, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, string? expectedInspectionKey = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task CorrectCompletionAsync(Guid eventId, Guid teamId, DateTimeOffset correctedAt, string reason, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default) { CorrectedAt = correctedAt; return Task.CompletedTask; }
        public Task<FinalizationOperationResult> FinalizeAsync(Guid eventId, LifecycleActor actor, long? expectedVersion = null, CancellationToken ct = default) => Task.FromResult(new FinalizationOperationResult(true, false));
        public Task UnfinalizeAsync(Guid eventId, string reason, bool confirmed, LifecycleActor actor, long? expectedVersion = null, CancellationToken ct = default) { Unfinalized = true; return Task.CompletedTask; }
        public Task ArchiveAsync(Guid eventId, bool confirmed, LifecycleActor actor, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ThrowOnFinalizationAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == "event.results_published")
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated finalization audit failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class ThrowOnReviewAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == "event.completion_time_inspected")
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated review audit failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class FixedClock(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
