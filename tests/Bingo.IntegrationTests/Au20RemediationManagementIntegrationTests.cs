using System.Net;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.WiseOldMan;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bingo.IntegrationTests;

public sealed partial class EventCompetitionManagementIntegrationTests
{
    private static WiseOldManCompetitionManagementClient Au20HttpClient(HttpClient http, TestClock clock) =>
        new(new Au20HttpFactory(http), new WiseOldManRequestLimiter(clock, NullLogger<WiseOldManRequestLimiter>.Instance), clock, new PassthroughCredentialProtector());

    [Theory]
    [InlineData("timeout")]
    [InlineData("network")]
    [InlineData("408")]
    public async Task Au20RemediationRealHttpTemporaryWritesRetryOldWindow(string secondFailure)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await Au20PendingEndAsync(clock);
        var remote = f.RemoteCompetition!;
        var attempts = new List<DateTimeOffset>();
        using var http = new HttpClient(new Au20HttpHandler(async request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            attempts.Add(clock.GetUtcNow());
            if (attempts.Count == 1) return new(HttpStatusCode.BadGateway);
            if (attempts.Count == 2)
            {
                if (secondFailure == "timeout") throw new TaskCanceledException("Controlled timeout");
                if (secondFailure == "network") throw new HttpRequestException("Controlled network failure");
                return new(HttpStatusCode.RequestTimeout);
            }
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            remote = remote with { EndsAt = body.RootElement.GetProperty("endsAt").GetDateTimeOffset() };
            return Au20HttpReceipt(remote);
        }))
        { BaseAddress = new Uri("https://controlled.invalid/") };
        var client = Au20HttpClient(http, clock);
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        async Task Pass()
        {
            await using var db = CreateDb();
            await new EventCompetitionManagementService(db, client, reads, new PassthroughCredentialProtector(), clock).ProcessDueAsync();
        }
        foreach (var minutes in new[] { 1, 2 })
        {
            await Pass();
            await using var db = CreateDb();
            Assert.Equal(EventCompetitionManagementOperationPhase.Retry, (await db.EventCompetitionManagementOperations.SingleAsync()).Phase);
            Assert.Equal(clock.GetUtcNow().AddMinutes(minutes), (await db.EventCompetitionManagementOperations.SingleAsync()).NextAttemptAt);
            var count = attempts.Count;
            await Pass(); Assert.Equal(count, attempts.Count);
            clock.Advance(TimeSpan.FromMinutes(minutes) - TimeSpan.FromMicroseconds(1));
            await Pass(); Assert.Equal(count, attempts.Count);
            clock.Advance(TimeSpan.FromMicroseconds(1));
        }
        await Pass();
        Assert.Equal(new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2) }, attempts.Zip(attempts.Skip(1), (a, b) => b - a));
        await using var verify = CreateDb();
        Assert.Equal(EventCompetitionEndUpdateStatus.Succeeded, (await verify.EventCompetitionSynchronizations.SingleAsync()).EndUpdateStatus);
        Assert.Equal(EventCompetitionManagementOperationPhase.Succeeded, (await verify.EventCompetitionManagementOperations.SingleAsync()).Phase);
    }

    [Fact]
    public async Task Au20RemediationExpiredClaimReadsOldWindowThenResends()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await Au20PendingEndAsync(clock);
        var remote = f.RemoteCompetition!;
        await using (var db = CreateDb())
        {
            var ev = await db.Events.SingleAsync();
            var m = await db.EventCompetitionManagements.SingleAsync();
            var payload = new WiseOldManCompetitionWritePayload(ev.Name, ev.EventStartsAt!.Value, ev.EventEndsAt!.Value, [], false);
            var op = new EventCompetitionManagementOperation(Guid.NewGuid(), f.EventId, m.Id, EventCompetitionManagementOperationType.Update,
                JsonSerializer.Serialize(payload, JsonSerializerOptions.Web), WiseOldManCompetitionRules.Fingerprint(new { Title = ev.Name, StartsAt = ev.EventStartsAt.Value, EndsAt = ev.EventEndsAt.Value, RosterLocked = true }), ev.Version, clock.GetUtcNow());
            op.Claim(clock.GetUtcNow()); m.MarkPending(op.Id, clock.GetUtcNow()); db.Add(op); await db.SaveChangesAsync();
        }
        var calls = 0;
        using var http = new HttpClient(new Au20HttpHandler(async request =>
        {
            calls++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            remote = remote with { EndsAt = body.RootElement.GetProperty("endsAt").GetDateTimeOffset() };
            return Au20HttpReceipt(remote);
        }))
        { BaseAddress = new Uri("https://controlled.invalid/") };
        var client = Au20HttpClient(http, clock);
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        async Task Pass() { await using var db = CreateDb(); await new EventCompetitionManagementService(db, client, reads, new PassthroughCredentialProtector(), clock).ProcessDueAsync(); }
        clock.Advance(TimeSpan.FromMinutes(5)); await Pass();
        Assert.Equal(0, calls);
        await using (var verify = CreateDb()) Assert.Equal(EventCompetitionManagementOperationPhase.Unknown, (await verify.EventCompetitionManagementOperations.SingleAsync()).Phase);
        clock.Advance(TimeSpan.FromMinutes(1)); await Pass();
        Assert.Equal(1, calls);
        await using var final = CreateDb();
        Assert.Equal(EventCompetitionEndUpdateStatus.Succeeded, (await final.EventCompetitionSynchronizations.SingleAsync()).EndUpdateStatus);
    }

    [Fact]
    public async Task Au20RemediationRealHttpRosterReceiptCompletesWithoutReconciliation()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await SeedEventAsync(clock, false, competitionId: 2099);
        var writes = 0;
        using var http = new HttpClient(new Au20HttpHandler(async request =>
        {
            writes++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.NotEmpty(body.RootElement.GetProperty("teams").EnumerateArray());
            return Au20HttpReceipt(f.RemoteCompetition!); // Real adapter parses an empty participant list.
        }))
        { BaseAddress = new Uri("https://controlled.invalid/") };
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, f.RemoteCompetition));
        await using var db = CreateDb();
        var result = await new EventCompetitionManagementService(db, Au20HttpClient(http, clock), reads, new PassthroughCredentialProtector(), clock).QueueUpdateAsync(f.EventId);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(1, writes);
        Assert.Equal(1, reads.Calls); // The required source check only, no reconciliation.
        Assert.Equal(EventCompetitionManagementOperationPhase.Succeeded, (await db.EventCompetitionManagementOperations.SingleAsync()).Phase);
        Assert.Contains(await PublishedCharacterNameAsync(f.EventId), (await db.EventCompetitionManagements.SingleAsync()).LastAcknowledgedRosterJson, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Au20RemediationDeletedExternalCompetitionConflictCanBeReplacedOrDisconnected(bool live)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var (f, remote) = await Au20ExternalAsync(clock, live, false);
        var writes = new RecordingManagementClient();
        await using (var failed = CreateDb())
        {
            await CreateService(failed, writes, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.NotFound)), clock).QueueUpdateAsync(f.EventId);
            Assert.Equal(EventCompetitionManagementStatus.Conflict, (await failed.EventCompetitionManagements.SingleAsync()).Status);
            Assert.Equal(EventCompetitionManagementOperationPhase.Failed, (await failed.EventCompetitionManagementOperations.OrderByDescending(x => x.CreatedAt).FirstAsync(x => x.SafeErrorCode == "SourceMissing")).Phase);
        }
        await using var db = CreateDb();
        var provider = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote with { Id = 9990 }));
        var result = await new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), clock).ConfigureAsync(f.EventId, f.EventVersion, live ? 9990 : null, f.Actor);
        Assert.True(result.Succeeded, result.Error);
        Assert.Empty((await db.EventCompetitionManagements.SingleAsync()).ProtectedVerificationCode);
        Assert.Equal(live ? 9990 : (long?)null, (await db.EventCompetitionSynchronizations.SingleAsync()).CompetitionId);
        Assert.Equal(0, writes.DeleteCalls);
    }

    [Theory]
    [InlineData(WiseOldManCompetitionWriteStatus.Validation)]
    [InlineData(WiseOldManCompetitionWriteStatus.Unauthorized)]
    public async Task Au20RemediationLateOldTargetRejectionCannotBlockResume(WiseOldManCompetitionWriteStatus rejection)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await Au20PendingEndAsync(clock);
        var remote = f.RemoteCompetition!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var call = 0;
        var writes = new RecordingManagementClient
        {
            UpdateHandler = async (_, payload, _, _) =>
        {
            if (++call == 1)
            {
                entered.SetResult(); await release.Task;
                return new(rejection, ErrorCode: "COMPETITION_START_DATE_AFTER_END_DATE");
            }
            remote = Success(remote, payload).Competition!;
            return new(WiseOldManCompetitionWriteStatus.Success, remote);
        }
        };
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        await using var original = CreateDb();
        var sending = CreateService(original, writes, reads, clock).QueueUpdateAsync(f.EventId);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var target = clock.GetUtcNow().AddHours(3);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await using var resume = CreateDb();
            var ev = await resume.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(resume, null!, clock).ResumePrematureEndAsync(f.EventId, ev.Version, true, "New target", target, f.Actor)).Succeeded);
            clock.Advance(TimeSpan.FromSeconds(1));
        }
        finally { release.TrySetResult(); }
        await sending;
        Assert.Equal(1, call);
        await using (var read = CreateDb())
        {
            var state = await read.EventCompetitionSynchronizations.SingleAsync();
            Assert.Equal(target, state.EndUpdateTargetAt);
            Assert.NotEqual(EventCompetitionEndUpdateStatus.Rejected, state.EndUpdateStatus);
            Assert.True((await read.EventCompetitionManagements.SingleAsync()).CanWrite);
        }
        clock.Advance(TimeSpan.FromMinutes(1));
        await using var final = CreateDb();
        await CreateService(final, writes, reads, clock).ProcessDueAsync();
        Assert.Equal(2, call);
        var operations = await final.EventCompetitionManagementOperations.OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Contains(operations, x => x.Phase == EventCompetitionManagementOperationPhase.Failed);
        Assert.Equal(1, Assert.Single(operations, x => x.Phase == EventCompetitionManagementOperationPhase.Succeeded).AttemptCount);
        Assert.Equal(EventCompetitionEndUpdateStatus.Succeeded, (await final.EventCompetitionSynchronizations.SingleAsync()).EndUpdateStatus);
    }

    [Fact]
    public async Task Au20RemediationResumeRestartsBackoffAfterLongOldTargetRetry()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await Au20PendingEndAsync(clock);
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, f.RemoteCompetition));
        var writes = new RecordingManagementClient { UpdateHandler = (_, _, _, _) => Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.RateLimited)) };
        foreach (var delay in new[] { 1, 2, 4, 8, 16 })
        {
            await using var db = CreateDb(); await CreateService(db, writes, reads, clock).ProcessDueAsync();
            clock.Advance(TimeSpan.FromMinutes(delay));
        }
        await using (var sixth = CreateDb())
        {
            await CreateService(sixth, writes, reads, clock).ProcessDueAsync();
            Assert.Equal(clock.GetUtcNow().AddMinutes(30), (await sixth.EventCompetitionManagementOperations.SingleAsync()).NextAttemptAt);
        }
        await using (var resume = CreateDb())
        {
            var ev = await resume.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(resume, null!, clock).ResumePrematureEndAsync(f.EventId, ev.Version, true, "New target", new DateTimeOffset(2026, 10, 4, 15, 0, 0, TimeSpan.Zero), f.Actor)).Succeeded);
        }
        await using (var queue = CreateDb())
        {
            await CreateService(queue, writes, reads, clock).QueueUpdateAsync(f.EventId);
            Assert.Equal(0, (await queue.EventCompetitionManagementOperations.SingleAsync()).AttemptCount);
        }
        await using var attempt = CreateDb();
        await CreateService(attempt, writes, reads, clock).ProcessDueAsync();
        var op = await attempt.EventCompetitionManagementOperations.SingleAsync();
        Assert.Equal(1, op.AttemptCount);
        Assert.Equal(clock.GetUtcNow().AddMinutes(1), op.NextAttemptAt);
        Assert.Equal(7, writes.Updates.Count);
    }

    [Fact]
    public async Task Au20RemediationFetchRejectsOneSecondMismatchWithoutReplacingCache()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await SeedEventAsync(clock, true, competitionId: 2098);
        var remote = f.RemoteCompetition!;
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        DateTimeOffset? previousSuccess;
        await using (var first = CreateDb())
        {
            Assert.True((await new EventCompetitionSynchronizationService(first, reads, new FixedStatus(), clock).RefreshAsync(f.EventId, f.Actor)).Succeeded);
            previousSuccess = (await first.EventCompetitionSynchronizations.SingleAsync()).LastSuccessfulAt;
        }
        clock.Advance(TimeSpan.FromHours(1));
        remote = remote with { EndsAt = remote.EndsAt.AddSeconds(1) };
        await using var db = CreateDb();
        var result = await new EventCompetitionSynchronizationService(db, reads, new FixedStatus(), clock).RefreshAsync(f.EventId, f.Actor);
        Assert.False(result.Succeeded);
        Assert.Equal("ScheduleMismatch", result.ErrorKind);
        Assert.Equal(previousSuccess, (await db.EventCompetitionSynchronizations.SingleAsync()).LastSuccessfulAt);
        Assert.Equal(2, reads.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Au20RemediationManualAndScheduledFetchResumeAfterEndUpdate(bool manual)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await Au20PendingEndAsync(clock);
        var remote = f.RemoteCompetition!;
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        await using (var blocked = CreateDb())
        {
            var sync = new EventCompetitionSynchronizationService(blocked, reads, new FixedStatus(), clock);
            Assert.Equal(EventCompetitionRefreshSkipReason.EndWindowUnmatched, (await sync.RefreshAsync(f.EventId, f.Actor)).SkipReason);
            await sync.ProcessDueAsync();
            Assert.Equal(0, reads.Calls);
        }
        var writes = new RecordingManagementClient
        {
            UpdateHandler = (_, payload, _, _) =>
        {
            remote = Success(remote, payload).Competition!;
            return Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Success, remote));
        }
        };
        await using (var update = CreateDb()) await CreateService(update, writes, reads, clock).ProcessDueAsync();
        var before = reads.Calls;
        await using var db = CreateDb();
        var resumed = new EventCompetitionSynchronizationService(db, reads, new FixedStatus(), clock);
        if (manual) Assert.True((await resumed.RefreshAsync(f.EventId, f.Actor)).Succeeded);
        else await resumed.ProcessDueAsync();
        Assert.Equal(before + 1, reads.Calls);
        Assert.Equal(clock.GetUtcNow(), (await db.EventCompetitionSynchronizations.SingleAsync()).LastSuccessfulAt);
    }

    [Fact]
    public async Task Au20RemediationExternalDeleteIsRefusedAtDispatch()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var (f, remote) = await Au20ExternalAsync(clock, false, false);
        await using (var setup = CreateDb())
        {
            var m = await setup.EventCompetitionManagements.SingleAsync();
            var op = new EventCompetitionManagementOperation(Guid.NewGuid(), f.EventId, m.Id, EventCompetitionManagementOperationType.Delete,
                JsonSerializer.Serialize(new { competitionId = remote.Id }), "controlled-delete", f.EventVersion, clock.GetUtcNow());
            op.SetActor(f.Actor.Id, f.Actor.Username); setup.Add(op); m.MarkPending(op.Id, clock.GetUtcNow()); await setup.SaveChangesAsync();
        }
        var writes = new RecordingManagementClient();
        var reads = new RecordingCompetitionClient(_ => throw new InvalidOperationException("Dispatch must refuse before provider access."));
        await using var db = CreateDb();
        await CreateService(db, writes, reads, clock).ProcessDueAsync();
        var denied = await db.EventCompetitionManagementOperations.SingleAsync(x => x.Type == EventCompetitionManagementOperationType.Delete);
        Assert.Equal(EventCompetitionManagementOperationPhase.Failed, denied.Phase);
        Assert.Equal("ExternalDeleteDenied", denied.SafeErrorCode);
        Assert.Equal(0, writes.DeleteCalls); Assert.Equal(0, reads.Calls);
        Assert.Equal(remote.Id, (await db.EventCompetitionSynchronizations.SingleAsync()).CompetitionId);
    }

    [Fact]
    public async Task Au20RemediationExternalReplacementIsReadOnlyInFinalReview()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var (f, old) = await Au20ExternalAsync(clock, true, false);
        await using var db = CreateDb();
        Assert.True((await new EventLifecycleService(db, null!, clock).EndNowAsync(f.EventId, f.EventVersion, true, "End", f.Actor)).Succeeded);
        var ev = await db.Events.SingleAsync();
        var next = old with { Id = 9992, EndsAt = ev.EventEndsAt!.Value };
        var result = await new EventCompetitionSynchronizationService(db, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, next)), new FixedStatus(), clock)
            .ConfigureAsync(f.EventId, ev.Version, next.Id, f.Actor);
        Assert.False(result.Succeeded);
        Assert.Contains("read-only after live play", result.Error);
        Assert.Equal(old.Id, (await db.EventCompetitionSynchronizations.SingleAsync()).CompetitionId);
        Assert.NotEmpty((await db.EventCompetitionManagements.SingleAsync()).ProtectedVerificationCode);
    }

    private static HttpResponseMessage Au20HttpReceipt(WiseOldManCompetition remote) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { id = remote.Id, title = remote.Title, startsAt = remote.StartsAt, endsAt = remote.EndsAt }))
    };
    private sealed class Au20HttpFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class Au20HttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
