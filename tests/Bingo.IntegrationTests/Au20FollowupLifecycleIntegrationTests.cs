using System.Data;
using System.Data.Common;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Bingo.IntegrationTests;

public sealed partial class EventCompetitionManagementIntegrationTests
{
    [Theory]
    [InlineData("end", false, false)]
    [InlineData("resume", false, false)]
    [InlineData("start", false, false)]
    [InlineData("end", true, false)]
    [InlineData("resume", true, false)]
    [InlineData("start", true, false)]
    [InlineData("end", true, true)]
    [InlineData("resume", true, true)]
    public async Task Au20FollowupRealCommitConflictPreservesLifecycleOutcome(string action, bool failRollback, bool exhaust)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await SeedEventAsync(clock, action != "start", competitionId: 2088);
        await using (var setup = CreateDb())
        {
            // Test-owned SSI dependency rows, solely in this test's isolated PostgreSQL database.
            await setup.Database.ExecuteSqlRawAsync("CREATE TABLE au20_commit_probe (id int PRIMARY KEY, value int NOT NULL); INSERT INTO au20_commit_probe VALUES (1, 0), (2, 0)");
            if (action == "resume")
                Assert.True((await new EventLifecycleService(setup, null!, clock).EndNowAsync(f.EventId, f.EventVersion, true, "Fixture end", f.Actor)).Succeeded);
            if (action == "start")
            {
                var form = new SignupForm(Guid.NewGuid(), f.EventId, clock.GetUtcNow());
                var primary = new SignupQuestion(Guid.NewGuid(), form.Id, f.EventId, "primary_regular_account", "Account", SignupQuestionType.Account,
                    true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
                setup.AddRange(form, primary);
                (await setup.EventParticipantCharacters.SingleAsync()).SetSignupQuestion(primary.Id);
                var board = new Board(Guid.NewGuid(), f.EventId, "Ready board", 1, 1);
                setup.Boards.Add(board);
                await BoardApprovalFixture.PublishAsync(setup, board, clock.GetUtcNow());
            }
        }
        clock.Advance(TimeSpan.FromSeconds(55) + TimeSpan.FromTicks(7));
        var clicked = clock.GetUtcNow();
        long version;
        int transitions;
        await using (var read = CreateDb())
        {
            version = (await read.Events.SingleAsync()).Version;
            transitions = await read.EventStateTransitions.CountAsync();
        }
        var interceptor = new Au20CommitConflictInterceptor(connectionString, clock, failRollback, exhaust);
        await using var db = CreateDb(interceptor);
        var service = new EventLifecycleService(db, null!, clock);
        if (action == "start")
        {
            var readiness = await service.GetStartReadinessAsync(f.EventId);
            Assert.True(readiness!.CanProceed, string.Join("; ", readiness.Blockers.Select(x => x.Description)));
        }
        var result = action switch
        {
            "start" => await service.StartNowAsync(f.EventId, version, true, "Start", f.Actor),
            "resume" => await service.ResumePrematureEndAsync(f.EventId, version, true, "Resume", new DateTimeOffset(2026, 10, 4, 14, 0, 0, TimeSpan.Zero), f.Actor),
            _ => await service.EndNowAsync(f.EventId, version, true, "End", f.Actor)
        };
        var conflicts = exhaust ? 3 : 1;
        Assert.Equal(conflicts, interceptor.CommitFailures.Count);
        Assert.All(interceptor.CommitFailures, error => Assert.Equal("40001", Assert.IsType<PostgresException>(error).SqlState));
        Assert.Equal(conflicts, interceptor.RollbackAttempts);
        if (!failRollback)
        {
            // Observed with this project's Npgsql: COMMIT's 40001 completes the transaction,
            // then the real rollback throws. Keep the exact driver observation executable.
            Assert.Equal(0, interceptor.CompletedRollbacks);
            Assert.Equal(conflicts, interceptor.RollbackFailures.Count);
            Assert.All(interceptor.RollbackFailures, error =>
                Assert.Contains("This NpgsqlTransaction has completed", Assert.IsType<InvalidOperationException>(error).Message));
        }
        else Assert.Equal(conflicts, interceptor.InjectedRollbackFailures);
        Assert.Equal(action != "start" && !exhaust, result.Succeeded);
        await using var verify = CreateDb();
        var ev = await verify.Events.SingleAsync();
        if (action == "start" || exhaust)
        {
            Assert.Contains("try again", result.Error);
            Assert.Equal(version, ev.Version);
            Assert.Equal(transitions, await verify.EventStateTransitions.CountAsync());
            Assert.Equal(action == "start" ? EventState.SignupClosed : action == "resume" ? EventState.AwaitingFinalReview : EventState.Live, ev.State);
        }
        else
        {
            var expected = clicked.AddTicks(-(clicked.Ticks % TimeSpan.TicksPerMicrosecond));
            Assert.Equal(expected, (await verify.EventCompetitionSynchronizations.SingleAsync()).EndUpdateRequestedAt);
            Assert.Equal(transitions + 1, await verify.EventStateTransitions.CountAsync());
            Assert.Equal(action == "resume" ? EventState.Live : EventState.AwaitingFinalReview, ev.State);
            if (action == "end")
            {
                Assert.Equal(expected, ev.ActualEndedAt);
                Assert.Equal(new DateTimeOffset(2026, 10, 4, 12, 1, 0, TimeSpan.Zero), ev.EventEndsAt);
                Assert.Equal(expected, (await verify.EventStateTransitions.SingleAsync()).EffectiveAt);
            }
        }
    }

    private sealed class Au20CommitConflictInterceptor(string connectionString, TestClock clock, bool failRollback, bool exhaust) : DbTransactionInterceptor
    {
        public List<Exception> CommitFailures { get; } = [];
        public List<Exception> RollbackFailures { get; } = [];
        public int RollbackAttempts { get; private set; }
        public int CompletedRollbacks { get; private set; }
        public int InjectedRollbackFailures { get; private set; }
        private int collisions;

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!exhaust && collisions > 0) return result;
            collisions++;
            // Create a real rw-dependency cycle after all lifecycle writes, immediately before COMMIT.
            await Command(transaction, "SELECT sum(value) FROM au20_commit_probe", cancellationToken);
            await using var other = new NpgsqlConnection(connectionString);
            await other.OpenAsync(cancellationToken);
            await using var competing = await other.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            await Command(competing, "SELECT sum(value) FROM au20_commit_probe", cancellationToken);
            await Command(transaction, "UPDATE au20_commit_probe SET value = value + 1 WHERE id = 2", cancellationToken);
            await Command(competing, "UPDATE au20_commit_probe SET value = value + 1 WHERE id = 1", cancellationToken);
            await competing.CommitAsync(cancellationToken);
            clock.Advance(TimeSpan.FromMinutes(2));
            return result; // The real Npgsql COMMIT, not this interceptor, must throw 40001.
        }

        public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Action == "Commit") CommitFailures.Add(eventData.Exception);
            else if (eventData.Action == "Rollback") RollbackFailures.Add(eventData.Exception);
            return Task.CompletedTask;
        }

        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            RollbackAttempts++;
            if (failRollback)
            {
                InjectedRollbackFailures++;
                throw new InvalidOperationException("Controlled rollback failure after real PostgreSQL commit conflict");
            }
            return ValueTask.FromResult(result);
        }

        public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            CompletedRollbacks++;
            return Task.CompletedTask;
        }

        private static async Task Command(DbTransaction transaction, string sql, CancellationToken ct)
        {
            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            await command.ExecuteScalarAsync(ct);
        }
    }
}
