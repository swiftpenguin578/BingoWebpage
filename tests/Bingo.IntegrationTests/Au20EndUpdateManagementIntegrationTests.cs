using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class EventCompetitionManagementIntegrationTests
{
    private async Task<Fixture> Au20PendingEndAsync(TestClock clock, long id = 2090)
    {
        var fixture = await SeedEventAsync(clock, live: true, competitionId: id);
        await using var db = CreateDb();
        var ended = await new EventLifecycleService(db, null!, clock).EndNowAsync(fixture.EventId, fixture.EventVersion, true, "Controlled early end", fixture.Actor);
        Assert.True(ended.Succeeded, ended.Error);
        return fixture;
    }

    [Fact]
    public async Task Au20EndUpdateBackoffIsPersistedAndRepeatedWorkerPassesNeverAccelerateIt()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await Au20PendingEndAsync(clock);
        var remote = fixture.RemoteCompetition!;
        var writes = new RecordingManagementClient { UpdateHandler = (_, _, _, _) => Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Unavailable)) };
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        foreach (var minutes in new[] { 1, 2, 4, 8, 16, 30, 30 })
        {
            var before = writes.Updates.Count;
            await using (var db = CreateDb()) await CreateService(db, writes, reads, clock).ProcessDueAsync();
            Assert.Equal(before + 1, writes.Updates.Count);
            await using (var verify = CreateDb())
            {
                var operation = await verify.EventCompetitionManagementOperations.SingleAsync();
                Assert.Equal(clock.GetUtcNow().AddMinutes(minutes), operation.NextAttemptAt);
                // U9-Q2: expose only the operation's stored end-update retry time.
                var view = await CreateService(verify, writes, reads, clock).GetAsync(fixture.EventId);
                Assert.Equal(operation.NextAttemptAt, view!.EndUpdateNextAttemptAt);
                Assert.Equal(before + 1, operation.AttemptCount);
                Assert.Equal(EventCompetitionManagementOperationPhase.Retry, operation.Phase);
            }
            await using (var repeat = CreateDb()) await CreateService(repeat, writes, reads, clock).ProcessDueAsync();
            Assert.Equal(before + 1, writes.Updates.Count);
            clock.Advance(TimeSpan.FromMinutes(minutes) - TimeSpan.FromTicks(10));
            await using (var early = CreateDb()) await CreateService(early, writes, reads, clock).ProcessDueAsync();
            Assert.Equal(before + 1, writes.Updates.Count);
            clock.Advance(TimeSpan.FromTicks(10));
        }
        writes.UpdateHandler = (_, payload, _, _) => { remote = Success(remote, payload).Competition!; return Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Success, remote)); };
        await using (var db = CreateDb()) await CreateService(db, writes, reads, clock).ProcessDueAsync();
        Assert.Equal(8, writes.Updates.Count);
        await using var final = CreateDb();
        var state = await final.EventCompetitionSynchronizations.SingleAsync();
        Assert.Equal(EventCompetitionEndUpdateStatus.Succeeded, state.EndUpdateStatus);
        Assert.Equal(state.EndUpdateTargetAt, state.CompetitionEndsAt);
        Assert.Equal(1, state.Generation);
        var refreshed = await new EventCompetitionSynchronizationService(final, reads, new FixedStatus(), clock).RefreshForFinalReviewAsync(fixture.EventId);
        Assert.True(refreshed.Succeeded, refreshed.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task Au20RateLimitUsesLaterOfBackoffAndProviderDue(int providerMinutes)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await Au20PendingEndAsync(clock);
        var writes = new RecordingManagementClient { UpdateHandler = (_, _, _, _) => Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.RateLimited, RetryAt: clock.GetUtcNow().AddMinutes(providerMinutes))) };
        await using var db = CreateDb();
        await CreateService(db, writes, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, fixture.RemoteCompetition)), clock).ProcessDueAsync();
        Assert.Equal(clock.GetUtcNow().AddMinutes(Math.Max(1, providerMinutes)), (await db.EventCompetitionManagementOperations.SingleAsync()).NextAttemptAt);
    }

    [Theory]
    [InlineData(WiseOldManCompetitionWriteStatus.Validation, "COMPETITION_START_DATE_AFTER_END_DATE")]
    [InlineData(WiseOldManCompetitionWriteStatus.Unauthorized, "InvalidCredentials")]
    [InlineData(WiseOldManCompetitionWriteStatus.NotFound, "NotFound")]
    [InlineData(WiseOldManCompetitionWriteStatus.Validation, "OTHER_REJECTION_secret")]
    public async Task Au20PermanentRejectionNeverRetriesOrMarksTheEndMatched(WiseOldManCompetitionWriteStatus status, string code)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await Au20PendingEndAsync(clock);
        var writes = new RecordingManagementClient { UpdateHandler = (_, _, _, _) => Task.FromResult(new WiseOldManCompetitionWriteResult(status, ErrorCode: code, Message: "Rejected secret")) };
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, fixture.RemoteCompetition));
        await using (var db = CreateDb()) await CreateService(db, writes, reads, clock).ProcessDueAsync();
        clock.Advance(TimeSpan.FromDays(1));
        await using (var repeat = CreateDb()) await CreateService(repeat, writes, reads, clock).ProcessDueAsync();
        Assert.Single(writes.Updates);
        await using var verify = CreateDb();
        var state = await verify.EventCompetitionSynchronizations.SingleAsync();
        Assert.Equal(EventCompetitionEndUpdateStatus.Rejected, state.EndUpdateStatus);
        Assert.NotEqual(state.EndUpdateTargetAt, state.CompetitionEndsAt);
        Assert.DoesNotContain("secret", state.EndUpdateErrorCode);
        var operation = await verify.EventCompetitionManagementOperations.SingleAsync();
        Assert.Equal(EventCompetitionManagementOperationPhase.Failed, operation.Phase);
        Assert.DoesNotContain("secret", operation.SafeError);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Au20AbsentOrInvalidCredentialStopsWithoutWriting(bool missing)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await Au20PendingEndAsync(clock);
        await using (var setup = CreateDb())
        {
            var management = await setup.EventCompetitionManagements.SingleAsync();
            if (missing) setup.Remove(management); else management.MarkCredentialInvalid(clock.GetUtcNow());
            await setup.SaveChangesAsync();
        }
        var writes = new RecordingManagementClient();
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, fixture.RemoteCompetition));
        await using var db = CreateDb();
        await CreateService(db, writes, reads, clock).ProcessDueAsync();
        Assert.Empty(writes.Updates); Assert.Equal(0, reads.Calls);
        Assert.Equal(EventCompetitionEndUpdateStatus.Rejected, (await db.EventCompetitionSynchronizations.SingleAsync()).EndUpdateStatus);
    }

    [Fact]
    public async Task Au20UnknownEndUpdateReconcilesWithSpacedReadsWithoutASecondWrite()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await Au20PendingEndAsync(clock);
        var unavailable = false;
        WiseOldManCompetition? confirmed = null;
        var reads = new RecordingCompetitionClient(_ => confirmed is not null ? new(WiseOldManCompetitionStatus.Success, confirmed)
            : unavailable ? new(WiseOldManCompetitionStatus.Unavailable) : new(WiseOldManCompetitionStatus.Success, fixture.RemoteCompetition));
        var writes = new RecordingManagementClient { UpdateHandler = (_, payload, _, _) => { unavailable = true; return Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Unknown)); } };
        await using (var db = CreateDb()) await CreateService(db, writes, reads, clock).ProcessDueAsync();
        foreach (var minutes in new[] { 1, 2, 4, 8 })
        {
            var before = reads.Calls;
            await using (var early = CreateDb()) await CreateService(early, writes, reads, clock).ProcessDueAsync();
            Assert.Equal(before, reads.Calls);
            clock.Advance(TimeSpan.FromMinutes(minutes));
            await using (var due = CreateDb()) await CreateService(due, writes, reads, clock).ProcessDueAsync();
            Assert.Equal(before + 1, reads.Calls);
            Assert.Single(writes.Updates);
            await using var verify = CreateDb();
            Assert.Equal(clock.GetUtcNow().AddMinutes(minutes * 2), (await verify.EventCompetitionManagementOperations.SingleAsync()).NextAttemptAt);
        }
        confirmed = Success(fixture.RemoteCompetition!, writes.Updates.Single()).Competition;
        clock.Advance(TimeSpan.FromMinutes(16));
        await using var last = CreateDb();
        await CreateService(last, writes, reads, clock).ProcessDueAsync();
        Assert.Single(writes.Updates);
        Assert.Equal(EventCompetitionEndUpdateStatus.Succeeded, (await last.EventCompetitionSynchronizations.SingleAsync()).EndUpdateStatus);
    }

    [Fact]
    public async Task Au20OfficialHistoryStopsADueEndRetry()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await Au20PendingEndAsync(clock);
        var writes = new RecordingManagementClient { UpdateHandler = (_, _, _, _) => Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Unavailable)) };
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, fixture.RemoteCompetition));
        await using (var first = CreateDb()) await CreateService(first, writes, reads, clock).ProcessDueAsync();
        await using (var publish = CreateDb())
        {
            var cycle = await publish.EventStateTransitions.SingleAsync(x => x.EventId == fixture.EventId);
            publish.Add(new EventFinalizationSnapshot(Guid.NewGuid(), fixture.EventId, 1, clock.GetUtcNow(), fixture.Actor.Id,
                cycle.Id, null, "{}", "[]"));
            await publish.SaveChangesAsync();
        }
        clock.Advance(TimeSpan.FromDays(1));
        await using var due = CreateDb();
        await CreateService(due, writes, reads, clock).ProcessDueAsync();
        Assert.Single(writes.Updates);
        Assert.Equal(EventCompetitionManagementOperationPhase.Cancelled, (await due.EventCompetitionManagementOperations.SingleAsync()).Phase);
    }

    [Fact]
    public async Task Au20ConcurrentEndWorkersHaveOnlyOneProviderWriteInFlight()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await Au20PendingEndAsync(clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new RecordingManagementClient { UpdateHandler = async (_, payload, _, ct) => { entered.SetResult(); await release.Task.WaitAsync(ct); return Success(fixture.RemoteCompetition!, payload); } };
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, fixture.RemoteCompetition));
        var first = Task.Run(async () => { await using var db = CreateDb(); await CreateService(db, writes, reads, clock).ProcessDueAsync(); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            await using var second = CreateDb();
            await CreateService(second, writes, reads, clock).ProcessDueAsync();
            Assert.Single(writes.Updates);
        }
        finally { release.TrySetResult(); }
        await first.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Single(writes.Updates);
    }
}
