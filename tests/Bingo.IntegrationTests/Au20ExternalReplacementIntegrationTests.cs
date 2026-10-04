using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class EventCompetitionManagementIntegrationTests
{
    private async Task<(Fixture Fixture, WiseOldManCompetition Remote)> Au20ExternalAsync(TestClock clock, bool live, bool invalid)
    {
        var f = await SeedEventAsync(clock, live);
        await using var db = CreateDb();
        var ev = await db.Events.SingleAsync(x => x.Id == f.EventId);
        var remote = new WiseOldManCompetition(9801, "External", ev.EventStartsAt!.Value, ev.EventEndsAt!.Value, clock.GetUtcNow(), []);
        var state = new EventCompetitionSynchronization(Guid.NewGuid(), f.EventId, 1, remote.Id, remote.Title, remote.StartsAt, remote.EndsAt, "fixture", clock.GetUtcNow(), EventCompetitionProvenance.External);
        var management = new EventCompetitionManagement(Guid.NewGuid(), f.EventId, state.Id, remote.Id, remote.Title, remote.StartsAt, remote.EndsAt, "protected:old-secret", "old-applied", clock.GetUtcNow(), EventCompetitionProvenance.External);
        var old = new EventCompetitionManagementOperation(Guid.NewGuid(), f.EventId, management.Id, EventCompetitionManagementOperationType.Update, "{}", "old", ev.Version, clock.GetUtcNow());
        old.Succeed(remote.Id, "old", clock.GetUtcNow());
        management.MarkApplied(old.Id, "old", RemoteFingerprint(remote), "[]", remote.Title, remote.StartsAt, remote.EndsAt, clock.GetUtcNow());
        if (invalid) management.MarkCredentialInvalid(clock.GetUtcNow());
        db.AddRange(state, management, old);
        await db.SaveChangesAsync();
        return (f, remote);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public async Task Au20ExternalOptionOneRetiresOldCodeAndOperation(bool live, bool invalid, bool disconnect)
    {
        var clock = new TestClock(NonMicrosecondFixtureNow);
        var (f, old) = await Au20ExternalAsync(clock, live, invalid);
        var next = old with { Id = 9802, Title = "Replacement" };
        var provider = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, next));
        var writes = new RecordingManagementClient();
        await using (var db = CreateDb())
        {
            var sync = new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), clock);
            var result = await sync.ConfigureAsync(f.EventId, f.EventVersion, disconnect ? null : next.Id, f.Actor);
            Assert.True(result.Succeeded, result.Error);
            var view = await CreateService(db, writes, provider, clock).GetAsync(f.EventId);
            Assert.False(view!.Enabled);
            Assert.Null(view.OperationId);
            Assert.Null(view.LastAppliedAt);
            Assert.Equal(disconnect ? 0 : 1, provider.Calls); // readback never fetches
        }
        await using (var db = CreateDb())
        {
            var state = await db.EventCompetitionSynchronizations.SingleAsync();
            var management = await db.EventCompetitionManagements.SingleAsync();
            Assert.Equal(disconnect ? null : next.Id, state.CompetitionId);
            Assert.Equal(EventCompetitionManagementStatus.Deleted, management.Status);
            Assert.Empty(management.ProtectedVerificationCode);
            Assert.Equal(EventCompetitionEndUpdateStatus.NotRequired, state.EndUpdateStatus);
            Assert.Equal(EventCompetitionManagementOperationPhase.Succeeded, (await db.EventCompetitionManagementOperations.SingleAsync()).Phase);
            if (!disconnect)
            {
                var ev = await db.Events.SingleAsync();
                Assert.True((await CreateService(db, writes, provider, clock).AdoptCredentialAsync(f.EventId, ev.Version, "new-secret", f.Actor)).Succeeded);
                Assert.Equal("protected:new-secret", management.ProtectedVerificationCode);
                Assert.Equal(next.Id, management.CompetitionId);
                Assert.Null((await CreateService(db, writes, provider, clock).GetAsync(f.EventId))!.OperationId);
            }
        }
        Assert.Empty(writes.Updates);
        Assert.Equal(0, writes.DeleteCalls);
    }

    [Theory]
    [InlineData(EventCompetitionManagementOperationPhase.Pending)]
    [InlineData(EventCompetitionManagementOperationPhase.Sending)]
    [InlineData(EventCompetitionManagementOperationPhase.Unknown)]
    public async Task Au20ExternalReplacementWaitsForUnresolvedOperation(EventCompetitionManagementOperationPhase phase)
    {
        var clock = new TestClock(NonMicrosecondFixtureNow);
        var (f, old) = await Au20ExternalAsync(clock, false, false);
        await using (var db = CreateDb())
        {
            var m = await db.EventCompetitionManagements.SingleAsync();
            var op = new EventCompetitionManagementOperation(Guid.NewGuid(), f.EventId, m.Id, EventCompetitionManagementOperationType.Update, "{}", "pending", f.EventVersion, clock.GetUtcNow());
            if (phase == EventCompetitionManagementOperationPhase.Sending) op.Claim(clock.GetUtcNow());
            if (phase == EventCompetitionManagementOperationPhase.Unknown) op.MarkUnknown("Ambiguous", "Unknown", clock.GetUtcNow());
            db.Add(op); await db.SaveChangesAsync();
        }
        var provider = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, old with { Id = 9802 }));
        await using var test = CreateDb();
        Assert.False((await new EventCompetitionSynchronizationService(test, provider, new FixedStatus(), clock).ConfigureAsync(f.EventId, f.EventVersion, 9802, f.Actor)).Succeeded);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Au20ExternalDeleteAndLiveDisconnectAreRefused(bool live)
    {
        var clock = new TestClock(NonMicrosecondFixtureNow);
        var (f, remote) = await Au20ExternalAsync(clock, live, false);
        var provider = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        var writes = new RecordingManagementClient();
        await using var db = CreateDb();
        Assert.False((await CreateService(db, writes, provider, clock).DeleteAsync(f.EventId, f.EventVersion, remote.Id, true, f.Actor)).Succeeded);
        if (live) Assert.False((await new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), clock).ConfigureAsync(f.EventId, f.EventVersion, null, f.Actor)).Succeeded);
        Assert.Equal(0, writes.DeleteCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Au20CreateSendingOrUnknownPreventsExternalLink(bool unknown)
    {
        var clock = new TestClock(NonMicrosecondFixtureNow);
        var f = await SeedEventAsync(clock, false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new RecordingManagementClient { CreateHandler = async (p, _) =>
        {
            entered.SetResult(); await release.Task;
            return unknown ? new(WiseOldManCompetitionWriteStatus.Unknown) : new(WiseOldManCompetitionWriteStatus.Success,
                new(9803, p.Title, p.StartsAt, p.EndsAt, clock.GetUtcNow(), []), ProtectedVerificationCode: "protected:created");
        }};
        var provider = new RecordingCompetitionClient(_ => throw new InvalidOperationException("Link validation must not run during Create."));
        await using var create = CreateDb();
        var task = CreateService(create, writes, provider, clock).CreateAsync(f.EventId, f.EventVersion, f.Actor);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var link = CreateDb();
            Assert.False((await new EventCompetitionSynchronizationService(link, provider, new FixedStatus(), clock).ConfigureAsync(f.EventId, f.EventVersion, 9802, f.Actor)).Succeeded);
        }
        finally { release.TrySetResult(); }
        await task;
        await using var verify = CreateDb();
        if (unknown)
            Assert.False((await new EventCompetitionSynchronizationService(verify, provider, new FixedStatus(), clock).ConfigureAsync(f.EventId, f.EventVersion, 9802, f.Actor)).Succeeded);
        else Assert.Equal(9803, (await verify.EventCompetitionSynchronizations.SingleAsync()).CompetitionId);
        Assert.Equal(1, writes.CreateCalls);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Au20ExternalLinkFirstPreventsWebsiteCreate()
    {
        var clock = new TestClock(NonMicrosecondFixtureNow);
        var f = await SeedEventAsync(clock, false);
        await using var db = CreateDb();
        var ev = await db.Events.SingleAsync();
        var remote = new WiseOldManCompetition(9804, "External", ev.EventStartsAt!.Value, ev.EventEndsAt!.Value, clock.GetUtcNow(), []);
        var provider = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        Assert.True((await new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), clock).ConfigureAsync(f.EventId, ev.Version, remote.Id, f.Actor)).Succeeded);
        var writes = new RecordingManagementClient();
        await using var create = CreateDb();
        var current = await create.Events.SingleAsync();
        Assert.False((await CreateService(create, writes, provider, clock).CreateAsync(f.EventId, current.Version, f.Actor)).Succeeded);
        Assert.Equal(0, writes.CreateCalls);
    }

    [Fact]
    public async Task Au20LateFetchResponseCannotOverwriteExternalReplacement()
    {
        var clock = new TestClock(NonMicrosecondFixtureNow);
        var (f, old) = await Au20ExternalAsync(clock, true, false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = new Au20HeldReadClient(async () => { entered.SetResult(); await release.Task; return new(WiseOldManCompetitionStatus.Success, old); });
        await using var fetching = CreateDb();
        var pending = new EventCompetitionSynchronizationService(fetching, held, new FixedStatus(), clock).RefreshAsync(f.EventId, f.Actor);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var replacing = CreateDb();
            var next = old with { Id = 9805 };
            var provider = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, next));
            Assert.True((await new EventCompetitionSynchronizationService(replacing, provider, new FixedStatus(), clock).ConfigureAsync(f.EventId, f.EventVersion, next.Id, f.Actor)).Succeeded);
        }
        finally { release.TrySetResult(); }
        Assert.False((await pending).Succeeded);
        await using var verify = CreateDb();
        var state = await verify.EventCompetitionSynchronizations.SingleAsync();
        Assert.Equal(9805, state.CompetitionId);
        Assert.Null(state.LastSuccessfulAt);
        Assert.Empty(await verify.EventCompetitionCharacterActivities.ToListAsync());
    }

    [Fact]
    public async Task Au20CreateStartedDuringLinkValidationWinsWithoutBeingOverwritten()
    {
        var clock = new TestClock(NonMicrosecondFixtureNow);
        var f = await SeedEventAsync(clock, false);
        await using var linking = CreateDb();
        var ev = await linking.Events.SingleAsync();
        var remote = new WiseOldManCompetition(9806, "External", ev.EventStartsAt!.Value, ev.EventEndsAt!.Value, clock.GetUtcNow(), []);
        var readEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = new Au20HeldReadClient(async () => { readEntered.SetResult(); await readRelease.Task; return new(WiseOldManCompetitionStatus.Success, remote); });
        var link = new EventCompetitionSynchronizationService(linking, held, new FixedStatus(), clock).ConfigureAsync(f.EventId, f.EventVersion, remote.Id, f.Actor);
        await readEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var writes = new RecordingManagementClient { CreateHandler = (p, _) => Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Success,
            new(9807, p.Title, p.StartsAt, p.EndsAt, clock.GetUtcNow(), []), ProtectedVerificationCode: "protected:created")) };
        try
        {
            await using var creating = CreateDb();
            Assert.True((await CreateService(creating, writes, new RecordingCompetitionClient(_ => throw new InvalidOperationException()), clock).CreateAsync(f.EventId, f.EventVersion, f.Actor)).Succeeded);
        }
        finally { readRelease.TrySetResult(); }
        Assert.False((await link).Succeeded);
        await using var verify = CreateDb();
        Assert.Equal(9807, (await verify.EventCompetitionSynchronizations.SingleAsync()).CompetitionId);
    }

    [Fact]
    public async Task Au20ExternalReplacementRejectsOneSecondMismatchWithoutRetiringCode()
    {
        var clock = new TestClock(NonMicrosecondFixtureNow);
        var (f, old) = await Au20ExternalAsync(clock, true, true);
        var provider = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, old with { Id = 9808, EndsAt = old.EndsAt.AddSeconds(1) }));
        await using var db = CreateDb();
        Assert.False((await new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), clock).ConfigureAsync(f.EventId, f.EventVersion, 9808, f.Actor)).Succeeded);
        Assert.Equal(old.Id, (await db.EventCompetitionSynchronizations.SingleAsync()).CompetitionId);
        Assert.Equal("protected:old-secret", (await db.EventCompetitionManagements.SingleAsync()).ProtectedVerificationCode);
    }

    private sealed class Au20HeldReadClient(Func<Task<WiseOldManCompetitionResult>> handler) : IWiseOldManCompetitionClient
    {
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default) => handler();
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default) => handler();
    }
}
