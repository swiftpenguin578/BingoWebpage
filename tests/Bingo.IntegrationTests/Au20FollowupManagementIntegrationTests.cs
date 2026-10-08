using System.Net;
using System.Text.Json;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class EventCompetitionManagementIntegrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Au20FollowupLateRemoteApplyCompletesWithoutResendButDriftStillRejects(bool targetApplied)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await Au20PendingEndAsync(clock);
        var remote = f.RemoteCompetition!;
        DateTimeOffset? requestedEnd = null;
        var writes = 0;
        using var http = new HttpClient(new Au20HttpHandler(async request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            writes++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            requestedEnd = body.RootElement.GetProperty("endsAt").GetDateTimeOffset();
            throw new TaskCanceledException("Controlled timeout before remote commit");
        }))
        { BaseAddress = new Uri("https://controlled.invalid/") };
        var client = Au20HttpClient(http, clock);
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        async Task Pass()
        {
            await using var db = CreateDb();
            await new EventCompetitionManagementService(db, client, reads, new PassthroughCredentialProtector(), clock).ProcessDueAsync();
        }
        await Pass();
        Assert.Equal(1, writes);
        Assert.Equal(2, reads.Calls); // Pre-write old state and immediate old-state read-back.
        await using (var pending = CreateDb())
        {
            var op = await pending.EventCompetitionManagementOperations.SingleAsync();
            Assert.Equal(EventCompetitionManagementOperationPhase.Retry, op.Phase);
            Assert.Equal(clock.GetUtcNow().AddMinutes(1), op.NextAttemptAt);
        }
        Assert.NotNull(requestedEnd);
        remote = remote with { EndsAt = targetApplied ? requestedEnd.Value : requestedEnd.Value.AddMinutes(7) };
        clock.Advance(TimeSpan.FromMinutes(1) - TimeSpan.FromMicroseconds(1));
        await Pass();
        Assert.Equal(2, reads.Calls);
        clock.Advance(TimeSpan.FromMicroseconds(1));
        await Pass();
        Assert.Equal(1, writes); // Neither a confirmed target nor genuine drift permits a second PUT.
        Assert.Equal(3, reads.Calls);
        await using var verify = CreateDb();
        var operation = await verify.EventCompetitionManagementOperations.SingleAsync();
        Assert.Equal(targetApplied ? EventCompetitionManagementOperationPhase.Succeeded : EventCompetitionManagementOperationPhase.Failed, operation.Phase);
        Assert.Equal(targetApplied ? EventCompetitionEndUpdateStatus.Succeeded : EventCompetitionEndUpdateStatus.Rejected,
            (await verify.EventCompetitionSynchronizations.SingleAsync()).EndUpdateStatus);
        Assert.Equal(targetApplied ? EventCompetitionManagementStatus.Active : EventCompetitionManagementStatus.Conflict,
            (await verify.EventCompetitionManagements.SingleAsync()).Status);
        if (!targetApplied) Assert.Equal("ExternalDrift", operation.SafeErrorCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Au20FollowupPreWriteTargetMatchRequiresRosterWhenIncluded(bool rosterMatches)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await SeedEventAsync(clock, false, competitionId: 2099);
        var remote = f.RemoteCompetition!;
        if (rosterMatches) remote = remote with { Participants = [new(await PublishedCharacterNameAsync(f.EventId), null, null)] };
        var writes = 0;
        using var http = new HttpClient(new Au20HttpHandler(_ =>
        {
            writes++;
            return Task.FromResult(Au20HttpReceipt(remote));
        }))
        { BaseAddress = new Uri("https://controlled.invalid/") };
        var reads = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        await using var db = CreateDb();
        var result = await new EventCompetitionManagementService(db, Au20HttpClient(http, clock), reads, new PassthroughCredentialProtector(), clock).QueueUpdateAsync(f.EventId);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(rosterMatches ? 0 : 1, writes);
        Assert.Equal(1, reads.Calls);
        Assert.Equal(EventCompetitionManagementOperationPhase.Succeeded, (await db.EventCompetitionManagementOperations.SingleAsync()).Phase);
    }
}
