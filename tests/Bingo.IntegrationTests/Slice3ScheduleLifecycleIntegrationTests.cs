using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3ScheduleLifecycleIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_slice3_schedule").WithUsername("bingo").WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly DateTimeOffset now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DiscordAuthentication:ClientId"] = "test-client", ["DiscordAuthentication:ClientSecret"] = "test-secret" }).Build();

    public async Task InitializeAsync() { await database.StartAsync(); options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options; await using var db = new ApplicationDbContext(options); }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ReadinessUsesStableCodesAndOpenCloseReopenAreOneTransactionalLifecycle()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("lifecycle", waitingList: true);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var initial = await evaluator.GetSignupReadinessAsync(eventId, SignupOpeningMode.OpenNow, now);
            Assert.Empty(initial!.Warnings);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var version = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId)).Version;
            Assert.False((await service.OpenAsync(eventId, version, [], false, actor)).Succeeded);
            Assert.True((await service.OpenAsync(eventId, version, [], true, actor)).Succeeded);
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
            var unconfirmedClose = await service.CloseAsync(eventId, opened.Version, false, actor);
            Assert.False(unconfirmedClose.Succeeded);
            Assert.Equal(EventState.SignupOpen, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId)).State);
            Assert.True((await service.CloseAsync(eventId, opened.Version, true, actor)).Succeeded);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var closed = await db.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(now, closed.ActualSignupClosedAt);
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var reopeningReadiness = await evaluator.GetSignupReadinessAsync(eventId, SignupOpeningMode.Reopen, now);
            Assert.Contains(reopeningReadiness!.Warnings, item => item.Code == "REOPENING_POPULATED_SIGNUP");
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            Assert.False((await service.ReopenAsync(eventId, closed.Version, [], false, actor)).Succeeded);
            Assert.True((await service.ReopenAsync(eventId, closed.Version, [], true, actor)).Succeeded);
            Assert.Equal(3, await db.EventStateTransitions.CountAsync(x => x.EventId == eventId));
            Assert.Equal(3, await db.AuditEntries.CountAsync(x => x.EventId == eventId && x.Action.StartsWith("event.signup_")));
        }
    }

    [Theory]
    [InlineData(false, "text")]
    [InlineData(false, "none")]
    [InlineData(false, "inactive")]
    [InlineData(false, "private")]
    [InlineData(true, "text")]
    [InlineData(true, "none")]
    [InlineData(true, "inactive")]
    [InlineData(true, "private")]
    public async Task PublicTextReadinessIsInformationalForManualAndScheduledOpening(bool scheduled, string questionKind)
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("public-text", waitingList: true, publicTextQuestion: questionKind != "none");
        await using var db = new ApplicationDbContext(options);
        var form = await db.SignupForms.SingleAsync(item => item.EventId == eventId);
        db.SignupQuestions.Add(new SignupQuestion(Guid.NewGuid(), form.Id, eventId, SignupQuestion.CoCaptainKey, SignupQuestion.CoCaptainLabel,
            SignupQuestionType.Text, false, 3, null, SignupSystemField.CoCaptainName));
        if (questionKind is "inactive" or "private")
        {
            var question = await db.SignupQuestions.SingleAsync(item => item.EventId == eventId && item.SystemField == SignupSystemField.None);
            if (questionKind == "inactive") question.Deactivate();
            else db.Entry(question).Property(item => item.PublicOnSignupBoard).CurrentValue = false;
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var readiness = await evaluator.GetSignupReadinessAsync(eventId, scheduled ? SignupOpeningMode.ScheduleOpening : SignupOpeningMode.OpenNow, now);
        Assert.True(readiness!.CanProceed);
        var hasPublicText = questionKind == "text";
        Assert.Equal(hasPublicText, readiness.Warnings.Any(item => item.Code == "PUBLIC_FREE_TEXT"));
        Assert.Equal(hasPublicText ? 1 : 0, readiness.Warnings.Count);
        var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
        var item = await db.Events.SingleAsync(item => item.Id == eventId);
        var values = new EventScheduleValues(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap, scheduled);
        var version = item.Version;
        var first = scheduled
            ? await service.SaveScheduleAsync(eventId, version, values, false, actor)
            : await service.OpenAsync(eventId, version, [], false, actor);
        Assert.Equal(scheduled, first.Succeeded);
        db.ChangeTracker.Clear();
        if (!scheduled)
        {
            var unchanged = await db.Events.AsNoTracking().SingleAsync(item => item.Id == eventId);
            Assert.Equal(EventState.Draft, unchanged.State);
            Assert.False(unchanged.ScheduledSignupOpeningEnabled);
            Assert.Equal(version, unchanged.Version);
            Assert.Empty(await db.AuditEntries.ToListAsync());
            var confirmed = await service.OpenAsync(eventId, version, [], true, actor);
            Assert.True(confirmed.Succeeded, confirmed.Error);
        }
        if (scheduled)
        {
            db.ChangeTracker.Clear();
            var saved = await db.Events.SingleAsync(item => item.Id == eventId);
            Assert.Equal(hasPublicText ? "PUBLIC_FREE_TEXT" : string.Empty, saved.ScheduledSignupWarningCodes);
            var due = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now.AddHours(1)));
            await due.ProcessDueSignupAsync();
            Assert.True((await db.ScheduledSignupOpeningAttempts.SingleAsync()).Opened);
        }
        db.ChangeTracker.Clear();
        var opened = await db.Events.SingleAsync(item => item.Id == eventId);
        Assert.Equal(EventState.SignupOpen, opened.State);
        Assert.NotNull(opened.FirstPublicAt);
        Assert.Single(await db.EventStateTransitions.ToListAsync());
    }

    [Fact]
    public async Task ScheduledOpeningDoesNotBlockOnNewInformationalWarning()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("new-text-warning", waitingList: true);
        await using var db = new ApplicationDbContext(options);
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
        var item = await db.Events.SingleAsync(item => item.Id == eventId);
        var values = new EventScheduleValues(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap, true);
        Assert.True((await service.SaveScheduleAsync(eventId, item.Version, values, true, actor)).Succeeded);
        var form = await db.SignupForms.SingleAsync(item => item.EventId == eventId);
        db.SignupQuestions.Add(new SignupQuestion(Guid.NewGuid(), form.Id, eventId, "new_text", "New public question", SignupQuestionType.Text, false, 2, null));
        await db.SaveChangesAsync();
        await new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now.AddHours(1))).ProcessDueSignupAsync();
        db.ChangeTracker.Clear();
        var opened = await db.Events.SingleAsync(item => item.Id == eventId);
        Assert.Equal(EventState.SignupOpen, opened.State);
        Assert.NotNull(opened.FirstPublicAt);
        Assert.False(opened.ScheduledSignupOpeningEnabled);
        var attempt = await db.ScheduledSignupOpeningAttempts.SingleAsync();
        Assert.True(attempt.Opened);
        Assert.Empty(attempt.BlockerCodes);
        Assert.Single(await db.EventStateTransitions.ToListAsync());
    }

    [Fact]
    public async Task ScheduleStaleCurrentEventAndAuditFailureLeaveNoPartialMutation()
    {
        var actor = await SeedAdminActorAsync();
        var first = await SeedReadyDraftAsync("current-first", waitingList: true);
        var second = await SeedReadyDraftAsync("current-second", waitingList: true);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration); var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var firstVersion = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == first)).Version;
            Assert.True((await service.OpenAsync(first, firstVersion, [], true, actor)).Succeeded);
            var secondVersion = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == second)).Version;
            var blocked = await service.OpenAsync(second, secondVersion, [], false, actor);
            Assert.False(blocked.Succeeded); Assert.Contains("overlaps", blocked.Error);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration); var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var item = await db.Events.SingleAsync(x => x.Id == second);
            var originalVersion = item.Version;
            var values = Values(item, item.EventEndsAt!.Value.AddDays(2)) with
            {
                EventStartsAt = item.EventEndsAt.Value
            };
            Assert.True((await service.SaveScheduleAsync(second, originalVersion, values, false, actor)).Succeeded);
            var stale = await service.SaveScheduleAsync(second, originalVersion, values, false, actor);
            Assert.False(stale.Succeeded); Assert.Contains("changed", stale.Error);

            var current = await db.Events.AsNoTracking().SingleAsync(x => x.Id == first);
            // Lifecycle authorization resolves the persisted account identity
            // before writing the audit row. A forged overlong display value must
            // not turn an otherwise authorized close into a partial failure.
            var closed = await service.CloseAsync(first, current.Version, true, new LifecycleActor(actor.Id, new string('x', 101)));
            Assert.True(closed.Succeeded, closed.Error);
            db.ChangeTracker.Clear();
            Assert.Equal(EventState.SignupClosed, (await db.Events.SingleAsync(x => x.Id == first)).State);
            var audit = Assert.Single(await db.AuditEntries.Where(x => x.EventId == first && x.Action == "event.signup_closed").ToListAsync());
            Assert.Equal(actor.Username, audit.ActorUsername);
            Assert.NotEqual(new string('x', 101), audit.ActorUsername);
        }
    }

    [Fact]
    public async Task ScheduleCloseAuditPersistenceFailureRollsBackCloseStateTransitionAndAudit()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("audit-failure-close", waitingList: true);

        await using (var setup = new ApplicationDbContext(options))
        {
            var service = new EventSignupLifecycleService(setup, new EventReadinessEvaluator(setup, configuration), new FixedTimeProvider(now));
            var item = await setup.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
            var opened = await service.OpenAsync(eventId, item.Version, [], true, actor);
            Assert.True(opened.Succeeded, opened.Error);
        }

        SignupLifecycleResult closeResult;
        await using (var triggerSetup = new ApplicationDbContext(options))
        {
            await triggerSetup.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER IF EXISTS slice3_fail_signup_closed_audit ON audit_entries;
                DROP FUNCTION IF EXISTS slice3_fail_signup_closed_audit();
                CREATE FUNCTION slice3_fail_signup_closed_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.action = 'event.signup_closed' THEN
                        RAISE EXCEPTION 'slice3 forced signup-close audit failure';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER slice3_fail_signup_closed_audit
                AFTER INSERT ON audit_entries
                FOR EACH ROW EXECUTE FUNCTION slice3_fail_signup_closed_audit();
                """);
        }

        try
        {
            await using (var failing = new ApplicationDbContext(options))
            {
                var service = new EventSignupLifecycleService(failing, new EventReadinessEvaluator(failing, configuration), new FixedTimeProvider(now));
                var item = await failing.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
                closeResult = await service.CloseAsync(eventId, item.Version, true, actor);
            }
        }
        finally
        {
            await using var triggerCleanup = new ApplicationDbContext(options);
            await triggerCleanup.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER IF EXISTS slice3_fail_signup_closed_audit ON audit_entries;
                DROP FUNCTION IF EXISTS slice3_fail_signup_closed_audit();
                """);
        }

        Assert.False(closeResult.Succeeded);
        Assert.Equal("The signup lifecycle change could not be saved. Try again.", closeResult.Error);

        await using var verify = new ApplicationDbContext(options);
        var persisted = await verify.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        Assert.Equal(EventState.SignupOpen, persisted.State);
        Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == eventId && x.ToState == EventState.SignupOpen).ToListAsync());
        Assert.Empty(await verify.EventStateTransitions.Where(x => x.EventId == eventId && x.ToState == EventState.SignupClosed).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.signup_opened").ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.signup_closed").ToListAsync());
    }

    [Fact]
    public async Task ProposedCloseAndPublicScheduleConfirmationAreAuthoritative()
    {
        var actor = await SeedAdminActorAsync();
        var proposalEvent = await SeedReadyDraftAsync("proposed-close", waitingList: true, signupClose: false);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var readiness = await evaluator.GetSignupReadinessAsync(proposalEvent, SignupOpeningMode.OpenNow, now);
            Assert.True(readiness!.CloseDecision.RequiresAcceptance);
            Assert.Equal(now.AddDays(2), readiness.CloseDecision.ProposedClose);
            var version = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == proposalEvent)).Version;
            var proposal = await service.OpenAsync(proposalEvent, version, [], false, actor);
            Assert.False(proposal.Succeeded);
            Assert.Equal(now.AddDays(2), proposal.ProposedClose);
            Assert.True((await service.OpenAsync(proposalEvent, version, [], true, actor)).Succeeded);
        }

        var validEvent = await SeedReadyDraftAsync("valid-close", waitingList: true, startDays: 20, endDays: 22);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var readiness = await evaluator.GetSignupReadinessAsync(validEvent, SignupOpeningMode.OpenNow, now);
            Assert.False(readiness!.CloseDecision.RequiresAcceptance);
            var version = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == validEvent)).Version;
            Assert.True((await service.OpenAsync(validEvent, version, [], true, actor)).Succeeded);
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
            var values = new EventScheduleValues(now.AddHours(1), item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap, true);
            Assert.False((await service.SaveScheduleAsync(publicEvent, item.Version, values, false, actor)).Succeeded);
            var locked = await service.SaveScheduleAsync(publicEvent, item.Version, values, true, actor);
            Assert.False(locked.Succeeded);
            Assert.Contains("locked", locked.Error);
        }
    }

    [Fact]
    public async Task OpenSignupsCannotClearSignupCloseAndValidReplacementRemainsAllowed()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("open-signup-close-clear", waitingList: true);
        await using var db = new ApplicationDbContext(options);
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
        var draft = await db.Events.SingleAsync(x => x.Id == eventId);
        var opened = await service.OpenAsync(eventId, draft.Version, [], true, actor);
        Assert.True(opened.Succeeded, opened.Error);
        db.ChangeTracker.Clear();

        var openedItem = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        var cleared = Values(openedItem, openedItem.EventEndsAt!.Value) with { SignupClosesAt = null };
        var clearedResult = await service.SaveScheduleAsync(eventId, openedItem.Version, cleared, true, actor);
        Assert.False(clearedResult.Succeeded);
        Assert.Equal("Signups are open, so they need a closing time. Set a new closing time or close signups now.", clearedResult.Error);
        db.ChangeTracker.Clear();
        var clearedEvent = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        Assert.Equal(EventState.SignupOpen, clearedEvent.State);
        Assert.Equal(openedItem.SignupClosesAt, clearedEvent.SignupClosesAt);
        Assert.Equal(openedItem.Version, clearedEvent.Version);
        Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.EventId == eventId && x.Action == "event.signup_opened"));

        var replacementClose = now.AddDays(1).AddHours(1);
        var replacement = Values(clearedEvent, clearedEvent.EventEndsAt!.Value) with { SignupClosesAt = replacementClose };
        var replacementResult = await service.SaveScheduleAsync(eventId, clearedEvent.Version, replacement, true, actor);
        Assert.True(replacementResult.Succeeded, replacementResult.Error);
        db.ChangeTracker.Clear();
        var replacedEvent = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        Assert.Equal(replacementClose, replacedEvent.SignupClosesAt);
        Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.EventId == eventId && x.Action == "event.schedule_updated"));
    }

    [Fact]
    public async Task ManualOpeningUsesActualBoundaryWithoutRewritingScheduledOpening()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("manual-opening-boundary", waitingList: true);
        DateTimeOffset scheduledOpening;
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == eventId);
            scheduledOpening = item.SignupOpensAt!.Value;
            Assert.True(scheduledOpening > now);
            item.ConfigureScheduledSignupOpening(true, []);
            Assert.True(item.ScheduledSignupOpeningEnabled);
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));
            var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
            var opened = await service.OpenAsync(eventId, item.Version, [], true, actor);
            Assert.True(opened.Succeeded, opened.Error);
        }

        var proposedClose = now.AddMinutes(30);
        await using var lifecycleDb = new ApplicationDbContext(options);
        var evaluator = new EventReadinessEvaluator(lifecycleDb, configuration);
        var serviceAfterOpening = new EventSignupLifecycleService(lifecycleDb, evaluator, new FixedTimeProvider(now));
        var openedItem = await lifecycleDb.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        Assert.Equal(scheduledOpening, openedItem.SignupOpensAt);
        Assert.Equal(now, openedItem.ActualSignupOpenedAt);

        var values = Values(openedItem, openedItem.EventEndsAt!.Value) with { SignupClosesAt = proposedClose };
        var readiness = await evaluator.GetSignupReadinessAsync(eventId, SignupOpeningMode.OpenNow, now, values);
        Assert.DoesNotContain(readiness!.Blockers, blocker => blocker.Code == "PROPOSED_SCHEDULE_INVALID");
        Assert.True(readiness.CloseDecision.IsValid);

        var saved = await serviceAfterOpening.SaveScheduleAsync(eventId, openedItem.Version, values, true, actor);
        Assert.True(saved.Succeeded, saved.Error);
        lifecycleDb.ChangeTracker.Clear();
        var persisted = await lifecycleDb.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        Assert.Equal(scheduledOpening, persisted.SignupOpensAt);
        Assert.Equal(now, persisted.ActualSignupOpenedAt);
        Assert.Equal(proposedClose, persisted.SignupClosesAt);

        foreach (var rejectedClose in new[] { now, now.AddMinutes(-1) })
        {
            var rejected = await serviceAfterOpening.SaveScheduleAsync(eventId, persisted.Version, values with { SignupClosesAt = rejectedClose }, true, actor);
            Assert.False(rejected.Succeeded);
            lifecycleDb.ChangeTracker.Clear();
            var unchanged = await lifecycleDb.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
            Assert.Equal(persisted.Version, unchanged.Version);
            Assert.Equal(persisted.SignupOpensAt, unchanged.SignupOpensAt);
            Assert.Equal(persisted.ActualSignupOpenedAt, unchanged.ActualSignupOpenedAt);
            Assert.Equal(persisted.SignupClosesAt, unchanged.SignupClosesAt);
        }
    }

    [Fact]
    public async Task NonOverlappingSignupWindowsAreAllowedButOverlapAndPastReopenNeedSafeConfirmation()
    {
        var actor = await SeedAdminActorAsync();
        var first = await SeedReadyDraftAsync("window-first", waitingList: true);
        var second = await SeedReadyDraftAsync("window-second", waitingList: true, startDays: 4, endDays: 6);
        var overlap = await SeedReadyDraftAsync("window-overlap", waitingList: true, startDays: 5, endDays: 7);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            Assert.True((await service.OpenAsync(first, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == first)).Version, [], true, actor)).Succeeded);
            Assert.True((await service.OpenAsync(second, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == second)).Version, [], true, actor)).Succeeded);
            var blocked = await service.OpenAsync(overlap, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == overlap)).Version, [], false, actor);
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
            var proposal = await service.ReopenAsync(reopening, item.Version, [], false, actor);
            Assert.False(proposal.Succeeded);
            Assert.Equal(item.EventStartsAt, proposal.ProposedClose);
            Assert.True((await service.ReopenAsync(reopening, item.Version, [], true, actor)).Succeeded);
        }
    }

    [Fact]
    public async Task ClosedSignupCapacityIncreasePromotesTheWaitingQueue()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("closed-capacity", waitingList: true, startDays: 20, endDays: 22);
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        var owner = Account.CreateWebsite(Guid.NewGuid(), "waiting-owner", "WAITING-OWNER", now);
        item.OpenSignups(now);
        item.CloseSignups(now);
        var waiting = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.WaitingList, 2, now.AddMinutes(1), SignupSource.Website);
        waiting.TransferOwner(owner);
        db.AddRange(owner, new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now, SignupSource.Website), waiting);
        item.ConfigureSchedule(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, 1);
        await db.SaveChangesAsync();
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
        var result = await service.SaveScheduleAsync(eventId, item.Version, Values(item, item.EventEndsAt!.Value) with { ParticipantCap = 2 }, false, actor);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.PromotedParticipants);
        Assert.Equal(2, await db.EventParticipants.CountAsync(x => x.EventId == eventId && x.SignupStatus == SignupStatus.Confirmed));
        Assert.Single(await db.AuditEntries.Where(x => x.EventId == eventId && x.Action == "participant.promoted").ToListAsync());
        Assert.Equal(2, await db.PersonalNotifications.CountAsync(x => x.Title == "participant.promoted"));
    }

    [Fact]
    public async Task HistoricalBoundariesStayUnchangedAndDirectMutationsFailClosed()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("historical-boundaries", waitingList: true);
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        item.ConfigureSchedule(now.AddHours(-1), item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap);
        item.ConfigureScheduledSignupOpening(true, []);
        await db.SaveChangesAsync();
        var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));

        var unchangedHistory = Values(item, item.EventEndsAt!.Value) with { ParticipantCap = 21 };
        Assert.True((await service.SaveScheduleAsync(eventId, item.Version, unchangedHistory, false, actor)).Succeeded);
        var current = await db.Events.SingleAsync(x => x.Id == eventId);
        Assert.True(current.ScheduledSignupOpeningEnabled);
        Assert.Equal(now.AddHours(-1), current.SignupOpensAt);

        var changedPastBoundary = Values(current, current.EventEndsAt!.Value) with { SignupOpensAt = now.AddHours(1) };
        var rejected = await service.SaveScheduleAsync(eventId, current.Version, changedPastBoundary, true, actor);
        Assert.False(rejected.Succeeded);
        Assert.Contains("locked", rejected.Error);

        var publishedId = await SeedReadyDraftAsync("published-boundaries", waitingList: true, startDays: 10, endDays: 12);
        var published = await db.Events.SingleAsync(x => x.Id == publishedId);
        published.MarkFirstPublic(now);
        await db.SaveChangesAsync();
        var clearedEnd = Values(published, published.EventEndsAt!.Value) with { EventEndsAt = null };
        var clearRejected = await service.SaveScheduleAsync(publishedId, published.Version, clearedEnd, true, actor);
        Assert.False(clearRejected.Succeeded);
        Assert.Contains("cannot be cleared", clearRejected.Error);
    }

    [Fact]
    public async Task UnchangedOverdueAutomaticOpeningPreservesWarningScopeForDirectScheduleChanges()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("overdue-opening-warning", waitingList: true);
        var scheduledFor = now.AddHours(-1);
        var attemptId = Guid.NewGuid();
        var attemptedAt = now.AddMinutes(-30);
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        item.ConfigureSchedule(scheduledFor, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap);
        item.ConfigureScheduledSignupOpening(true, ["ORIGINAL_WARNING"]);
        db.ScheduledSignupOpeningAttempts.Add(new ScheduledSignupOpeningAttempt(attemptId, eventId, scheduledFor, attemptedAt, false, ["ORIGINAL_BLOCKER"]));
        await db.SaveChangesAsync();
        var version = item.Version;
        var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));
        var changedValues = Values(item, item.EventEndsAt!.Value) with { ParticipantCap = item.ParticipantCap + 1 };

        var savedResult = await service.SaveScheduleAsync(eventId, version, changedValues, false, actor);
        Assert.True(savedResult.Succeeded, savedResult.Error);
        db.ChangeTracker.Clear();
        var saved = await db.Events.SingleAsync(x => x.Id == eventId);
        Assert.Equal(scheduledFor, saved.SignupOpensAt);
        Assert.True(saved.ScheduledSignupOpeningEnabled);
        Assert.Equal("ORIGINAL_WARNING", saved.ScheduledSignupWarningCodes);
        var attempt = Assert.Single(await db.ScheduledSignupOpeningAttempts.Where(x => x.EventId == eventId).ToListAsync());
        Assert.Equal(attemptId, attempt.Id);
        Assert.Equal(scheduledFor, attempt.ScheduledFor);
        Assert.Equal(attemptedAt, attempt.AttemptedAt);
        Assert.False(attempt.Opened);
        Assert.Null(attempt.ResolvedAt);
        Assert.Equal("ORIGINAL_BLOCKER", attempt.BlockerCodes);
    }

    [Fact]
    public async Task ScheduleEditCannotCreateAnOperationalEventOverlap()
    {
        var actor = await SeedAdminActorAsync();
        var first = await SeedReadyDraftAsync("schedule-overlap-first", waitingList: true, startDays: 2, endDays: 4);
        var second = await SeedReadyDraftAsync("schedule-overlap-second", waitingList: true, startDays: 5, endDays: 7);
        await using var db = new ApplicationDbContext(options);
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
        Assert.True((await service.OpenAsync(first, (await db.Events.SingleAsync(x => x.Id == first)).Version, [], true, actor)).Succeeded);
        Assert.True((await service.OpenAsync(second, (await db.Events.SingleAsync(x => x.Id == second)).Version, [], true, actor)).Succeeded);
        var item = await db.Events.SingleAsync(x => x.Id == second);
        var overlapping = Values(item, now.AddDays(5)) with { SignupClosesAt = now.AddDays(3), EventStartsAt = now.AddDays(3).AddHours(12) };
        var rejected = await service.SaveScheduleAsync(second, item.Version, overlapping, true, actor);
        Assert.False(rejected.Succeeded);
        Assert.Contains("overlaps", rejected.Error);
    }

    [Fact]
    public async Task PrivateDraftSaveRejectsCompleteOverlappingWindowBeforeOpeningReadiness()
    {
        var actor = await SeedAdminActorAsync();
        _ = await SeedOperationalEventAsync("draft-save-operational", EventState.Live, now.AddDays(2), now.AddDays(4));
        var candidate = await SeedReadyDraftAsync("draft-save-candidate", waitingList: true, startDays: 8, endDays: 10);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = await setup.Events.SingleAsync(x => x.Id == candidate);
            item.UpdateIdentity(item.Name, item.Slug, null, item.Timezone);
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var evaluator = new EventReadinessEvaluator(db, configuration);
        var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
        var itemBefore = await db.Events.AsNoTracking().SingleAsync(x => x.Id == candidate);
        var auditBefore = await db.AuditEntries.CountAsync(x => x.EventId == candidate);
        var overlapping = Values(itemBefore, now.AddDays(5)) with
        {
            SignupOpensAt = now.AddDays(1),
            SignupClosesAt = now.AddDays(2),
            EventStartsAt = now.AddDays(3),
            EventEndsAt = now.AddDays(5),
            ParticipantCap = itemBefore.ParticipantCap,
            ScheduledSignupOpeningEnabled = false
        };

        var rejected = await service.SaveScheduleAsync(candidate, itemBefore.Version, overlapping, false, actor);
        Assert.False(rejected.Succeeded);
        Assert.Contains("overlaps", rejected.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("draft-save-operational", rejected.Error, StringComparison.Ordinal);
        db.ChangeTracker.Clear();
        var unchanged = await db.Events.AsNoTracking().SingleAsync(x => x.Id == candidate);
        Assert.Equal(itemBefore.Version, unchanged.Version);
        Assert.Equal(itemBefore.EventStartsAt, unchanged.EventStartsAt);
        Assert.Equal(itemBefore.EventEndsAt, unchanged.EventEndsAt);
        Assert.Null(unchanged.Description);
        Assert.Equal(auditBefore, await db.AuditEntries.CountAsync(x => x.EventId == candidate));

        var adjacent = overlapping with { EventStartsAt = now.AddDays(4), EventEndsAt = now.AddDays(6) };
        var saved = await service.SaveScheduleAsync(candidate, unchanged.Version, adjacent, false, actor);
        Assert.True(saved.Succeeded, saved.Error);
        db.ChangeTracker.Clear();
        var persisted = await db.Events.AsNoTracking().SingleAsync(x => x.Id == candidate);
        Assert.Equal(adjacent.EventStartsAt, persisted.EventStartsAt);
        Assert.Equal(adjacent.EventEndsAt, persisted.EventEndsAt);
    }

    [Fact]
    public async Task PrivateDraftCanScheduleIncompleteWindowButOpeningStaysBlockedUntilReady()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("partial-schedule", waitingList: true, startDays: 8, endDays: 10);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = await setup.Events.SingleAsync(x => x.Id == eventId);
            item.UpdateIdentity(item.Name, item.Slug, null, item.Timezone);
            await setup.SaveChangesAsync();
        }

        var opening = now.AddHours(2);
        var closing = now.AddHours(3);
        await using (var db = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(db, configuration);
            var service = new EventSignupLifecycleService(db, evaluator, new FixedTimeProvider(now));
            var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
            var values = new EventScheduleValues(opening, closing, null, null, null, null, true);
            var saved = await service.SaveScheduleAsync(eventId, item.Version, values, false, actor);
            Assert.True(saved.Succeeded, saved.Error);
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.Draft, saved.State);
            Assert.True(saved.ScheduledSignupOpeningEnabled);
            Assert.Equal(opening, saved.SignupOpensAt);
            Assert.Equal(closing, saved.SignupClosesAt);
            Assert.Null(saved.EventStartsAt);
            Assert.Null(saved.EventEndsAt);
            var evaluator = new EventReadinessEvaluator(verify, configuration);
            var service = new EventSignupLifecycleService(verify, evaluator, new FixedTimeProvider(now));
            var manual = await service.OpenAsync(eventId, saved.Version, [], true, actor);
            Assert.False(manual.Succeeded);
            Assert.Contains("description", manual.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(EventState.Draft, (await verify.Events.AsNoTracking().SingleAsync(x => x.Id == eventId)).State);
        }

        await using (var dueDb = new ApplicationDbContext(options))
        {
            var evaluator = new EventReadinessEvaluator(dueDb, configuration);
            var due = new EventSignupLifecycleService(dueDb, evaluator, new FixedTimeProvider(now.AddHours(4)));
            await due.ProcessDueSignupAsync();
            var blocked = await dueDb.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.Draft, blocked.State);
            Assert.False(blocked.ScheduledSignupOpeningEnabled);
            var attempt = await dueDb.ScheduledSignupOpeningAttempts.AsNoTracking().SingleAsync(x => x.EventId == eventId);
            Assert.False(attempt.Opened);
            Assert.Contains("DESCRIPTION_REQUIRED", attempt.BlockerCodes);
            Assert.Contains("EVENT_START_REQUIRED", attempt.BlockerCodes);
        }
    }

    [Fact]
    public async Task FutureSignupCanFollowEveryNonOverlappingOperationalStateButOverlapNeverMutates()
    {
        var actor = await SeedAdminActorAsync();
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
            Assert.True((await service.OpenAsync(future, futureVersion, [], true, actor)).Succeeded);
            var overlapVersion = (await db.Events.AsNoTracking().SingleAsync(x => x.Id == overlapping)).Version;
            var rejected = await service.OpenAsync(overlapping, overlapVersion, [], false, actor);
            Assert.False(rejected.Succeeded);
            Assert.Contains(state.ToString(), rejected.Error);
            Assert.Equal(EventState.Draft, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == overlapping)).State);
            Assert.Empty(await db.AuditEntries.Where(x => x.EventId == overlapping).ToListAsync());
        }
    }

    [Fact]
    public async Task ScheduleMatrixUsesDraftStateForRunningPausedAndFinalizedEvents()
    {
        var actor = await SeedAdminActorAsync();
        var runningId = await SeedReadyDraftAsync("matrix-running", waitingList: true);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = await setup.Events.SingleAsync(x => x.Id == runningId);
            item.OpenSignups(now);
            item.CloseSignups(now);
            var draft = new DraftSession(Guid.NewGuid(), runningId, 1);
            draft.Start(now.AddMinutes(-10));
            item.SetDraftLocked(true);
            setup.DraftSessions.Add(draft);
            await setup.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == runningId);
            var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));
            var proposedEnd = item.EventEndsAt!.Value.AddDays(1);
            var saved = await service.SaveScheduleAsync(runningId, item.Version, Values(item, proposedEnd), true, actor);
            Assert.True(saved.Succeeded, saved.Error);
            Assert.Equal(proposedEnd, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == runningId)).EventEndsAt);
        }

        await using (var setup = new ApplicationDbContext(options))
        {
            var draft = await setup.DraftSessions.SingleAsync(x => x.EventId == runningId);
            draft.Pause();
            await setup.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == runningId);
            var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));
            var proposedStart = item.EventStartsAt!.Value.AddDays(1);
            var saved = await service.SaveScheduleAsync(runningId, item.Version, Values(item, item.EventEndsAt!.Value) with { EventStartsAt = proposedStart }, true, actor);
            Assert.True(saved.Succeeded, saved.Error);
            Assert.Equal(proposedStart, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == runningId)).EventStartsAt);
        }

        var finalizedId = await SeedReadyDraftAsync("matrix-finalized", waitingList: true, startDays: 10, endDays: 12);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = await setup.Events.SingleAsync(x => x.Id == finalizedId);
            item.OpenSignups(now);
            item.CloseSignups(now);
            var draft = new DraftSession(Guid.NewGuid(), finalizedId, 1);
            draft.Start(now.AddMinutes(-10));
            draft.Finalize(now.AddMinutes(-5));
            item.SetDraftLocked(true);
            setup.DraftSessions.Add(draft);
            await setup.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == finalizedId);
            var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));
            var values = Values(item, now.AddDays(14)) with { EventStartsAt = now.AddDays(13) };
            var saved = await service.SaveScheduleAsync(finalizedId, item.Version, values, true, actor);
            Assert.True(saved.Succeeded, saved.Error);
            var persisted = await db.Events.AsNoTracking().SingleAsync(x => x.Id == finalizedId);
            Assert.Equal(now.AddDays(13), persisted.EventStartsAt);
            Assert.Equal(now.AddDays(14).AddMinutes(30), persisted.SubmissionCutoffAt);
        }
    }

    [Fact]
    public async Task LiveEndChangeRequiresConfirmationAndReasonAndRedrivesCutoffAtomically()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("live-end-change", waitingList: true);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = await setup.Events.SingleAsync(x => x.Id == eventId);
            item.OpenSignups(now);
            item.CloseSignups(now);
            item.StartEvent(now);
            await setup.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
            var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));
            var values = Values(item, item.EventEndsAt!.Value.AddHours(1));
            Assert.False((await service.SaveScheduleAsync(eventId, item.Version, values, false, actor, reason: "Extend for the live event.")).Succeeded);
            Assert.Contains("reason", (await service.SaveScheduleAsync(eventId, item.Version, values, true, actor)).Error, StringComparison.OrdinalIgnoreCase);
            var saved = await service.SaveScheduleAsync(eventId, item.Version, values, true, actor, reason: "Extend for the live event.");
            Assert.True(saved.Succeeded, saved.Error);
            var persisted = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
            Assert.Equal(values.EventEndsAt, persisted.EventEndsAt);
            Assert.Equal(values.EventEndsAt!.Value.AddMinutes(30), persisted.SubmissionCutoffAt);
            Assert.Single(await db.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.schedule_updated").ToListAsync());
        }
    }

    [Fact]
    public async Task LiveEndChangeAllowsUnchangedLegacyCapacityBelowConfirmedCount()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("live-over-cap", waitingList: true);
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = await setup.Events.SingleAsync(x => x.Id == eventId);
            item.ConfigureSchedule(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, 1);
            item.OpenSignups(now);
            item.CloseSignups(now);
            setup.EventParticipants.AddRange(
                new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated),
                new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 2, now.AddMinutes(1), SignupSource.AdminCreated));
            item.StartEvent(now);
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var current = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        Assert.Equal(1, current.ParticipantCap);
        Assert.Equal(2, await db.EventParticipants.CountAsync(x => x.EventId == eventId && x.SignupStatus == SignupStatus.Confirmed));
        var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));
        var values = Values(current, current.EventEndsAt!.Value.AddHours(1));
        var result = await service.SaveScheduleAsync(eventId, current.Version, values, true, actor, reason: "Extend for the live event.");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(values.EventEndsAt, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId)).EventEndsAt);
        Assert.Equal(1, await db.Events.Where(x => x.Id == eventId).Select(x => x.ParticipantCap).SingleAsync());
    }

    [Fact]
    public async Task LiveEndChangeRejectsWiseOldManWindowMismatchWithoutMutation()
    {
        var actor = await SeedAdminActorAsync();
        var eventId = await SeedReadyDraftAsync("live-end-wom", waitingList: true);
        await using (var setup = new ApplicationDbContext(options))
        {
            var liveItem = await setup.Events.SingleAsync(x => x.Id == eventId);
            liveItem.OpenSignups(now);
            liveItem.CloseSignups(now);
            liveItem.StartEvent(now);
            setup.EventCompetitionSynchronizations.Add(new EventCompetitionSynchronization(Guid.NewGuid(), eventId, 1, 42, "Live competition", liveItem.EventStartsAt, liveItem.EventEndsAt, "", now));
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new FixedTimeProvider(now));
        var result = await service.SaveScheduleAsync(eventId, item.Version, Values(item, item.EventEndsAt!.Value.AddMinutes(10)), true, actor, reason: "Extend for the live event.");
        Assert.False(result.Succeeded);
        Assert.Contains("configured website UTC window exactly", result.Error);
        Assert.Equal(item.EventEndsAt, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId)).EventEndsAt);
    }

    private async Task<Guid> SeedReadyDraftAsync(string slug, bool waitingList, bool signupClose = true, int startDays = 2, int endDays = 4, bool publicTextQuestion = false)
    {
        await using var db = new ApplicationDbContext(options);
        var item = new BingoEvent(Guid.NewGuid(), slug, slug, "UTC", Guid.NewGuid(), now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.UpdateIdentity(slug, slug, "Public description", "UTC");
        item.ConfigureSchedule(now.AddHours(1), signupClose ? now.AddDays(startDays - 1) : null, null, now.AddDays(startDays), now.AddDays(endDays), 20);
        item.ConfigureSignup(waitingList, false, null);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
        db.AddRange(item, form, regular, captain);
        if (publicTextQuestion) db.Add(new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "public_text", "Tell us something", SignupQuestionType.Text, false, 2, null));
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<LifecycleActor> SeedAdminActorAsync()
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), $"schedule-admin-{Guid.NewGuid():N}", $"SCHEDULE-ADMIN-{Guid.NewGuid():N}", now);
        account.SetGlobalRole(GlobalRole.Admin);
        await using var db = new ApplicationDbContext(options);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return new(account.Id, account.LoginName);
    }
    private async Task<Guid> SeedOperationalEventAsync(string slug, EventState state, DateTimeOffset start, DateTimeOffset end)
    {
        await using var db = new ApplicationDbContext(options);
        var item = new BingoEvent(Guid.NewGuid(), slug, slug, "UTC", Guid.NewGuid(), now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
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
    private static EventScheduleValues Values(BingoEvent item, DateTimeOffset end) => new(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, end, item.ParticipantCap, item.ScheduledSignupOpeningEnabled);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
}
