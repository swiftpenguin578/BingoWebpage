using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Theory]
    [InlineData(21, 59, 55, 7, 1)]
    [InlineData(21, 59, 0, 0, 0)]
    [InlineData(22, 0, 0, 4000, 1)]
    public async Task Au20EarlyEndRetainsActualPrecisionAndQueuesCeilingMinute(int hour, int minuteOfHour, int seconds, int ticks, int addedMinute)
    {
        var minute = new DateTimeOffset(2026, 10, 4, hour, minuteOfHour, 0, TimeSpan.Zero);
        var clicked = minute.AddSeconds(seconds).AddTicks(ticks);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "au20-early", "AU20-EARLY", minute);
        admin.SetGlobalRole(GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "AU20 early", $"au20-{Guid.NewGuid():N}", "", "UTC",
            minute.AddHours(-4), minute.AddHours(-3), minute.AddHours(-2), minute.AddHours(1), null, 20, admin.Id, minute);
        item.OpenSignups(minute.AddHours(-4)); item.CloseSignups(minute.AddHours(-3)); item.StartEvent(minute.AddHours(-2));
        var state = new EventCompetitionSynchronization(Guid.NewGuid(), item.Id, 1, 2003, "ID only", item.EventStartsAt, item.EventEndsAt, "", minute);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, item, state); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await new EventLifecycleService(db, null!, new TestClock(clicked)).EndNowAsync(item.Id, item.Version, true, "Early end", new(admin.Id, admin.LoginName));
        Assert.True(result.Succeeded, result.Error);
        db.ChangeTracker.Clear();
        var saved = await db.Events.SingleAsync(x => x.Id == item.Id);
        var actual = clicked.AddTicks(-(clicked.Ticks % TimeSpan.TicksPerMicrosecond));
        Assert.Equal(minute.AddMinutes(addedMinute), saved.EventEndsAt);
        Assert.Equal(actual, saved.ActualEndedAt);
        Assert.Equal(actual.AddMinutes(30), saved.SubmissionCutoffAt);
        var pending = await db.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == item.Id);
        Assert.Equal(EventCompetitionEndUpdateStatus.Pending, pending.EndUpdateStatus);
        Assert.Equal(saved.EventEndsAt, pending.EndUpdateTargetAt);
        Assert.Equal(actual, pending.EndUpdateRequestedAt);
        Assert.Equal(minute.AddHours(1), pending.CompetitionEndsAt);
        Assert.Empty(await db.EventCompetitionManagementOperations.ToListAsync());
        Assert.Equal(actual, (await db.EventStateTransitions.SingleAsync(x => x.EventId == item.Id)).EffectiveAt);
    }

    [Fact]
    public async Task Au20ResumeRequiresExplicitFutureValidatedEndAndDoesNotRound()
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "au20-validation", "AU20-VALIDATION", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "AU20 validation", $"au20-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-4), now.AddHours(-3), now.AddHours(-2), now.AddHours(1), null, 20, admin.Id, now);
        item.OpenSignups(now.AddHours(-4)); item.CloseSignups(now.AddHours(-3)); item.StartEvent(now.AddHours(-2)); item.EndEvent(now.AddMinutes(-1));
        await using (var seed = new ApplicationDbContext(options)) { seed.AddRange(admin, item); await seed.SaveChangesAsync(); }
        foreach (var candidate in new DateTimeOffset?[] { null, now, now.AddMinutes(-5), now.AddMinutes(1), now.AddMinutes(5).AddTicks(7) })
        {
            await using var db = new ApplicationDbContext(options);
            var result = await new EventLifecycleService(db, null!, new TestClock(now)).ResumePrematureEndAsync(item.Id, item.Version, true,
                "Resume", candidate, new(admin.Id, admin.LoginName));
            Assert.False(result.Succeeded);
        }
        await using var valid = new ApplicationDbContext(options);
        var replacement = now.AddMinutes(35).ToOffset(TimeSpan.FromHours(2));
        var resumed = await new EventLifecycleService(valid, null!, new TestClock(now)).ResumePrematureEndAsync(item.Id, item.Version,
            true, "Resume", replacement, new(admin.Id, admin.LoginName));
        Assert.True(resumed.Succeeded, resumed.Error);
        Assert.Equal(replacement, (await valid.Events.AsNoTracking().SingleAsync()).EventEndsAt);
    }

    [Fact]
    public async Task Au20EndStateMigrationDownAndUpPreservePopulatedHistory()
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var item = new BingoEvent(Guid.NewGuid(), "AU20 migration", $"au20-{Guid.NewGuid():N}", "UTC", Guid.NewGuid(), now, PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(now.AddHours(1), now.AddHours(2), null, now.AddHours(3), now.AddHours(4), 20);
        var state = new EventCompetitionSynchronization(Guid.NewGuid(), item.Id, 1, 2004, "Existing competition", item.EventStartsAt, item.EventEndsAt, "existing", now);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(item, state); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261004093705_ClearLegacyDropTileEhbOverrides");
        Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM event_competition_synchronizations WHERE event_id = {item.Id}").SingleAsync());
        await migrator.MigrateAsync();
        var saved = await db.EventCompetitionSynchronizations.SingleAsync();
        Assert.Equal(EventCompetitionEndUpdateStatus.NotRequired, saved.EndUpdateStatus);
        Assert.Null(saved.EndUpdateTargetAt);
        Assert.Null(saved.EndUpdateRequestedAt);
        Assert.Null(saved.EndUpdateErrorCode);
        Assert.Equal("existing", saved.AssignmentFingerprint);
        Assert.Equal(item.EventEndsAt, (await db.Events.SingleAsync()).EventEndsAt);
    }
}
