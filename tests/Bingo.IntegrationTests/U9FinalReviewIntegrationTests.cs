using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Au12PlacementRuleIntegrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(1L)]
    public async Task BFinal2Br12ReopenMissingZeroStaleVersionMakesNoWrites(long? supplied)
    {
        var fixture = await SeedAsync(PlacementRule.CreditedEhbThenScoreTime, false);
        await using var db = new ApplicationDbContext(options);
        var service = new EventFinalizationService(db, new PublicBoardService(db, new Clock()), new Clock());
        var before = (await service.GetReadinessAsync(fixture.EventId))!;
        await service.FinalizeAsync(fixture.EventId, new(fixture.AdminId, "admin"), before.EventVersion);
        db.ChangeTracker.Clear();
        var published = (await service.GetReadinessAsync(fixture.EventId))!;
        var transitions = await db.EventStateTransitions.CountAsync();
        var audits = await db.AuditEntries.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnfinalizeAsync(fixture.EventId, "Correction", true, new(fixture.AdminId, "admin"), supplied));
        db.ChangeTracker.Clear();
        var after = (await service.GetReadinessAsync(fixture.EventId))!;
        Assert.Equal(EventState.Archived, after.State);
        Assert.Equal(published.EventVersion, after.EventVersion);
        Assert.True(Assert.Single(after.History).Active);
        Assert.Null(Assert.Single(after.History).UnfinalizedAt);
        Assert.Equal(transitions, await db.EventStateTransitions.CountAsync());
        Assert.Equal(audits, await db.AuditEntries.CountAsync());
    }

    [Theory]
    [InlineData(EventState.Live, "Other is still live. End it first, then publish its results before reopening this event.")]
    [InlineData(EventState.AwaitingFinalReview, "Publish the results of Other first.")]
    [InlineData(EventState.Finalized, "Other is still the current event. Contact the Super Admin to archive it.")]
    public async Task U9Q1BlockingCurrentReadMatchesReopenRefusal(EventState state, string reason)
    {
        var fixture = await SeedAsync(PlacementRule.CreditedEhbThenScoreTime, false);
        await using var db = new ApplicationDbContext(options);
        var service = new EventFinalizationService(db, new PublicBoardService(db, new Clock()), new Clock());
        var before = (await service.GetReadinessAsync(fixture.EventId))!;
        await service.FinalizeAsync(fixture.EventId, new(fixture.AdminId, "admin"), before.EventVersion);
        var other = new BingoEvent(Guid.NewGuid(), "Other", "other", "UTC", fixture.AdminId, Now, PlacementRule.CreditedEhbThenScoreTime);
        db.Add(other);
        // Controlled retained-state fixture exercises all three current-event states.
        db.Entry(other).Property(x => x.State).CurrentValue = state;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var current = (await service.GetReadinessAsync(fixture.EventId))!;
        Assert.Equal(new FinalReviewBlockingEvent(other.Id, "Other", state, reason), current.BlockingCurrentEvent);
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UnfinalizeAsync(fixture.EventId, "Correction", true, new(fixture.AdminId, "admin"), current.EventVersion));
        Assert.Equal(reason, refusal.Message);
        db.ChangeTracker.Clear();
        Assert.True(Assert.Single((await service.GetReadinessAsync(fixture.EventId))!.History).Active);
    }
}
