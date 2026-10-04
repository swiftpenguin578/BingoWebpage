using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    public async Task Au20ExactConfiguredWindowSurvivesPostgresAndTimezoneConversion(int differenceSeconds, bool succeeds)
    {
        var raw = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero).AddTicks(7);
        var clock = new TestClock(raw);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "au20-admin", "AU20-ADMIN", raw);
        admin.SetGlobalRole(GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "AU20 exact", $"au20-{Guid.NewGuid():N}", "", "UTC",
            raw.AddHours(-4), raw.AddHours(-3), raw.AddHours(-2), raw.AddHours(1), null, 20, admin.Id, raw);
        item.OpenSignups(raw.AddHours(-4)); item.CloseSignups(raw.AddHours(-3)); item.StartEvent(raw.AddHours(-2).AddSeconds(19));
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, item); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        item = await db.Events.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(0, item.EventStartsAt!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.NotEqual(raw.AddHours(-2), item.EventStartsAt);
        var remote = new WiseOldManCompetition(2001, "AU20 exact", item.EventStartsAt.Value.ToOffset(TimeSpan.FromHours(2)),
            item.EventEndsAt!.Value.AddSeconds(differenceSeconds).ToOffset(TimeSpan.FromHours(-4)), raw, []);
        var provider = new FakeCompetitionClient([new(WiseOldManCompetitionStatus.Success, remote)]);
        var sync = new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), clock);
        var link = await sync.ConfigureAsync(item.Id, item.Version, remote.Id, new(admin.Id, admin.LoginName));
        Assert.Equal(succeeds, link.Succeeded);
        if (!succeeds) { Assert.Contains("exactly", link.Error); return; }
        item.EndEvent(raw.AddMinutes(-1)); await db.SaveChangesAsync();
        var final = await sync.RefreshForFinalReviewAsync(item.Id);
        Assert.True(final.Succeeded, final.Message);
        Assert.Equal(2, provider.Calls);
        var read = await sync.GetAsync(item.Id);
        Assert.Equal(EventCompetitionRefreshSkipReason.NotDue, read!.RefreshSkipReason);
        Assert.False(read.CanRefresh);
        var manual = await sync.RefreshAsync(item.Id, new(admin.Id, admin.LoginName));
        Assert.True(manual.Skipped);
        Assert.Equal(read.RefreshSkipReason, manual.SkipReason);
        Assert.Equal(read.NextEligibleAt, manual.RetryAt);
        Assert.Equal(2, provider.Calls); // Readback and cooldown perform no fetch.
    }

    [Fact]
    public async Task Au20ResumePersistsReplacementAndPendingUpdateDespiteProviderMismatch()
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "au20-resume", "AU20-RESUME", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "AU20 resume", $"au20-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-4), now.AddHours(-3), now.AddHours(-2), now.AddHours(-1), null, 20, admin.Id, now);
        item.OpenSignups(now.AddHours(-4)); item.CloseSignups(now.AddHours(-3)); item.StartEvent(now.AddHours(-2)); item.EndEvent(now.AddHours(-1));
        var replacement = now.AddHours(1);
        var state = new EventCompetitionSynchronization(Guid.NewGuid(), item.Id, 1, 2002, "AU20 resume", item.EventStartsAt,
            replacement.AddSeconds(1), "", now);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, item, state); await db.SaveChangesAsync();
        var result = await new EventLifecycleService(db, null!, new TestClock(now)).ResumePrematureEndAsync(item.Id, item.Version,
            true, "Resume", replacement, new(admin.Id, admin.LoginName));
        Assert.True(result.Succeeded, result.Error);
        db.ChangeTracker.Clear();
        var saved = await db.Events.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(replacement, saved.EventEndsAt);
        Assert.Null(saved.ActualEndedAt);
        var pending = await db.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == item.Id);
        Assert.Equal(EventCompetitionEndUpdateStatus.Pending, pending.EndUpdateStatus);
        Assert.Equal(replacement, pending.EndUpdateTargetAt);
        Assert.Equal(replacement.AddSeconds(1), pending.CompetitionEndsAt);
    }
}
