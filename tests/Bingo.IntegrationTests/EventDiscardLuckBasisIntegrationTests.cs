using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovedEmptyEventDiscardsLuckBasesButProtectedParticipantKeepsHistory(bool protectedHistory)
    {
        // This legacy discard proof needs an otherwise empty event. The shared
        // price-board fixture also serves start/readiness tests and therefore
        // carries a finalized participant history by default.
        var (actor, _, boss, drops, eventId) = await PriceBoardFixtureAsync(includeParticipant: false);
        await CreatePriceBoardTileAsync(eventId, actor.Id, boss.Id, drops);
        await ApprovePriceBoardAsync(eventId, actor.Id);
        Guid approvalId;
        string basisBefore;
        await using (var setup = new ApplicationDbContext(options))
        {
            approvalId = (await setup.Boards.SingleAsync(x => x.EventId == eventId)).ActiveApprovalSnapshotId!.Value;
            var bases = await setup.EventLuckOutcomeBases.Where(x => x.EventId == eventId).OrderBy(x => x.SourceDropId).ToListAsync();
            Assert.Equal(2, bases.Count);
            basisBefore = System.Text.Json.JsonSerializer.Serialize(bases);
            if (protectedHistory)
            {
                setup.Add(new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, DateTimeOffset.UtcNow, SignupSource.AdminCreated));
                await setup.SaveChangesAsync();
            }
        }
        await using (var mutation = new ApplicationDbContext(options))
        {
            var ev = await mutation.Events.SingleAsync(x => x.Id == eventId);
            var result = await new EventDestructiveLifecycleService(mutation, TimeProvider.System)
                .DiscardAsync(eventId, ev.Version, true, new LifecycleActor(actor.Id, actor.LoginName));
            Assert.Equal(!protectedHistory, result.Succeeded);
            if (protectedHistory) Assert.Contains("protected participant history", result.Error);
        }
        await using var verify = new ApplicationDbContext(options);
        var after = await verify.Events.SingleAsync(x => x.Id == eventId);
        if (protectedHistory)
        {
            Assert.Equal(EventState.SignupClosed, after.State);
            Assert.Equal(approvalId, (await verify.Boards.SingleAsync(x => x.EventId == eventId)).ActiveApprovalSnapshotId);
            Assert.Equal(basisBefore, System.Text.Json.JsonSerializer.Serialize(await verify.EventLuckOutcomeBases.Where(x => x.EventId == eventId).OrderBy(x => x.SourceDropId).ToListAsync()));
            Assert.True(await verify.BoardApprovalSnapshots.AnyAsync(x => x.Id == approvalId));
            Assert.Single(await verify.EventParticipants.Where(x => x.EventId == eventId).ToListAsync());
            Assert.False(await verify.AuditEntries.AnyAsync(x => x.EventId == eventId && x.Action == "event.discarded"));
        }
        else
        {
            Assert.Equal(EventState.Discarded, after.State);
            Assert.False(await verify.EventLuckOutcomeBases.AnyAsync(x => x.EventId == eventId));
            Assert.False(await verify.Boards.AnyAsync(x => x.EventId == eventId));
            Assert.False(await verify.BoardApprovalSnapshots.AnyAsync(x => x.Id == approvalId));
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.discarded").ToListAsync());
        }
    }
}
