using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3LifecyclePersistenceIntegrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260726201926_TransitionParticipantCharacterAuthority";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice3_lifecycle_rehearsal")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task RetainedLifecycleValuesAndFirstPublicBackfillAreDeterministic()
    {
        var accountId = Guid.NewGuid();
        var created = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var states = new[]
        {
            (EventState.Draft, false), (EventState.SignupOpen, true), (EventState.SignupClosed, true),
            (EventState.Live, true), (EventState.AwaitingFinalReview, true),
            (EventState.Finalized, true), (EventState.Archived, true)
        };
        Guid retainedEndEventId = Guid.Empty;
        var retainedTransitionPerformedAt = created.AddDays(10);

        await using (var retained = new ApplicationDbContext(options))
        {
            await retained.Database.EnsureDeletedAsync();
            await retained.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await retained.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO accounts (id, password_hash, disabled_at, created_at, last_login_at, must_change_password, account_type, active, authorization_version, login_name, normalized_login_name, password_changed_at, onboarding_completed_at, global_role, public_username, normalized_public_username, password_version, version)
                VALUES ({accountId}, {"hash"}, NULL, {created}, NULL, FALSE, {"WebsiteAccount"}, TRUE, 1, {"slice3-retained"}, {"SLICE3-RETAINED"}, {created}, {created}, {"User"}, {"slice3-retained"}, {"SLICE3-RETAINED"}, 1, 1);
                """);
            foreach (var (state, _) in states)
            {
                var id = Guid.NewGuid();
                if (state == EventState.AwaitingFinalReview) retainedEndEventId = id;
                var opening = created.AddHours((int)state + 1);
                DateTimeOffset? finalizedAt = state is EventState.Finalized or EventState.Archived ? opening.AddDays(3) : null;
                DateTimeOffset? archivedAt = state == EventState.Archived ? opening.AddDays(4) : null;
                await retained.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, allow_private_signup_editing, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, finalized_at, archived_at, created_by_account_id, created_at)
                    VALUES ({id}, {$"retained-{state}"}, {$"retained-{state}"}, {"retained description"}, {"UTC"}, {state.ToString()}, {opening}, {opening.AddHours(1)}, {opening.AddDays(1)}, {opening.AddDays(2)}, {opening.AddDays(2).AddHours(1)}, 19, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {finalizedAt}, {archivedAt}, {accountId}, {created});
                    """);
            }
            await retained.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO event_state_transitions (id, event_id, from_state, to_state, performed_by_account_id, performed_at, reason)
                VALUES ({Guid.NewGuid()}, {retainedEndEventId}, {"Live"}, {"AwaitingFinalReview"}, {accountId}, {retainedTransitionPerformedAt}, {"retained scheduled end"});
                """);
            await retained.GetService<IMigrator>().MigrateAsync();
        }

        await using var migrated = new ApplicationDbContext(options);
        var rows = await migrated.Events.AsNoTracking().OrderBy(item => item.Name).ToListAsync();
        Assert.Equal(states.Length, rows.Count);
        foreach (var (state, publicState) in states)
        {
            var row = rows.Single(item => item.State == state);
            Assert.Equal("retained description", row.Description);
            Assert.Equal(19, row.ParticipantCap);
            Assert.Equal(created.AddHours((int)state + 1), row.SignupOpensAt);
            Assert.Equal(publicState ? row.SignupOpensAt : null, row.FirstPublicAt);
            Assert.Equal(state is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived ? row.EventStartsAt : null, row.ActualStartedAt);
            Assert.Equal(state is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived ? row.EventEndsAt : null, row.ActualEndedAt);
            Assert.Equal(state is EventState.Finalized or EventState.Archived ? row.SignupOpensAt!.Value.AddDays(3) : null, row.FinalizedAt);
            Assert.Equal(state == EventState.Archived ? row.SignupOpensAt!.Value.AddDays(4) : null, row.ArchivedAt);
        }
        var retainedTransition = await migrated.EventStateTransitions.SingleAsync(x => x.EventId == retainedEndEventId);
        Assert.Equal(retainedTransitionPerformedAt, retainedTransition.EffectiveAt);
        var retainedEnd = rows.Single(x => x.Id == retainedEndEventId);
        Assert.Equal(retainedEnd.SubmissionCutoffAt, retainedEnd.SubmissionsClosedAt);
    }

    [Fact]
    public async Task CleanMigrationPersistsNullableDraftAttemptsAndEventConcurrency()
    {
        await using (var clean = new ApplicationDbContext(options))
        {
            await clean.Database.EnsureDeletedAsync();
            await clean.Database.MigrateAsync();
            var draft = new BingoEvent(Guid.NewGuid(), "Minimal", "slice3-minimal", "Europe/Copenhagen", Guid.NewGuid(), DateTimeOffset.UtcNow);
            clean.Events.Add(draft);
            await clean.SaveChangesAsync();
            Assert.Null((await clean.Events.SingleAsync()).Description);
        }

        Guid eventId;
        await using (var first = new ApplicationDbContext(options))
            eventId = (await first.Events.SingleAsync()).Id;

        await using (var left = new ApplicationDbContext(options))
        await using (var right = new ApplicationDbContext(options))
        {
            var firstCopy = await left.Events.SingleAsync(item => item.Id == eventId);
            var staleCopy = await right.Events.SingleAsync(item => item.Id == eventId);
            firstCopy.UpdateIdentity("First update", firstCopy.Slug, firstCopy.Description, firstCopy.Timezone);
            await left.SaveChangesAsync();
            staleCopy.UpdateIdentity("Stale update", staleCopy.Slug, staleCopy.Description, staleCopy.Timezone);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync());
        }

        var scheduledFor = DateTimeOffset.UtcNow.AddHours(1);
        await using (var firstAttempt = new ApplicationDbContext(options))
        {
            firstAttempt.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), eventId, EventState.Draft, EventState.SignupOpen, null, DateTimeOffset.UtcNow, "scheduled opening", scheduled: true));
            firstAttempt.ScheduledEventStartAttempts.Add(new ScheduledEventStartAttempt(Guid.NewGuid(), eventId, scheduledFor, DateTimeOffset.UtcNow, false, ["BOARD_UNPUBLISHED"]));
            await firstAttempt.SaveChangesAsync();
        }
        await using (var persisted = new ApplicationDbContext(options))
        {
            var transition = await persisted.EventStateTransitions.SingleAsync();
            Assert.Null(transition.PerformedByAccountId);
            Assert.True(transition.Scheduled);
        }
        await using var retry = new ApplicationDbContext(options);
        retry.ScheduledEventStartAttempts.Add(new ScheduledEventStartAttempt(Guid.NewGuid(), eventId, scheduledFor, DateTimeOffset.UtcNow, false, ["BOARD_UNPUBLISHED"]));
        await Assert.ThrowsAsync<DbUpdateException>(() => retry.SaveChangesAsync());
    }
}
