using System.Net;
using System.Text.Json;
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
        })) { BaseAddress = new Uri("https://controlled.invalid/") };
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
        })) { BaseAddress = new Uri("https://controlled.invalid/") };
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
        })) { BaseAddress = new Uri("https://controlled.invalid/") };
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
