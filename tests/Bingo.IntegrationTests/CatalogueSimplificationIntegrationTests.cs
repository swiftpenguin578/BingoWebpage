using System.Data.Common;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task AffectedCatalogueRefreshKeepsPerTileFingerprintsStableAndUpdatesBoardTotal()
    {
        var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        var owner = Website($"brd02-refresh-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "BRD-02 refresh", $"brd02-refresh-{Guid.NewGuid():N}", "UTC", owner.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "BRD-02 board", 1, 2);
        var firstBoss = new BossActivity(Guid.NewGuid(), "First boss", $"brd02-first-{Guid.NewGuid():N}", "Boss", 10m, now);
        var secondBoss = new BossActivity(Guid.NewGuid(), "Second boss", $"brd02-second-{Guid.NewGuid():N}", "Boss", 10m, now);
        var firstItem = new CatalogueItem(Guid.NewGuid(), "First item", $"BRD02-FIRST-{Guid.NewGuid():N}");
        var secondItem = new CatalogueItem(Guid.NewGuid(), "Second item", $"BRD02-SECOND-{Guid.NewGuid():N}");
        var firstDrop = new SourceDrop(Guid.NewGuid(), firstBoss.Id, firstItem.Id, "1/10", .1m, null, now);
        var secondDrop = new SourceDrop(Guid.NewGuid(), secondBoss.Id, secondItem.Id, "1/20", .05m, null, now);
        var firstTemplate = new TileTemplate(Guid.NewGuid(), "First tile", string.Empty, ObjectiveType.DropRequirements, string.Empty, null);
        var secondTemplate = new TileTemplate(Guid.NewGuid(), "Second tile", string.Empty, ObjectiveType.DropRequirements, string.Empty, null);
        var firstTile = new BoardTile(Guid.NewGuid(), board.Id, firstTemplate.Id, 0, 0, "First tile", string.Empty, string.Empty, 1m);
        var secondTile = new BoardTile(Guid.NewGuid(), board.Id, secondTemplate.Id, 0, 1, "Second tile", string.Empty, string.Empty, 2m);
        var firstRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), firstTile.Id, 1, 1, true, false, "First drop", false);
        var secondRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), secondTile.Id, 1, 1, true, false, "Second drop", false);
        var firstBossSnapshot = new BoardRequirementBossSnapshot(Guid.NewGuid(), firstRequirement.Id, firstBoss.Id, firstBoss.Name, firstBoss.EfficientCompletionsPerHour);
        var secondBossSnapshot = new BoardRequirementBossSnapshot(Guid.NewGuid(), secondRequirement.Id, secondBoss.Id, secondBoss.Name, secondBoss.EfficientCompletionsPerHour);
        var firstDropSnapshot = new BoardRequirementDropSnapshot(Guid.NewGuid(), firstRequirement.Id, firstDrop.Id, firstItem.Id, firstBoss.Name, firstItem.Name, firstDrop.DisplayRate, firstDrop.NumericProbability, null, null);
        var secondDropSnapshot = new BoardRequirementDropSnapshot(Guid.NewGuid(), secondRequirement.Id, secondDrop.Id, secondItem.Id, secondBoss.Name, secondItem.Name, secondDrop.DisplayRate, secondDrop.NumericProbability, null, null);

        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(owner, bingoEvent, board, firstBoss, secondBoss, firstItem, secondItem, firstDrop, secondDrop,
                firstTemplate, secondTemplate, firstTile, secondTile, firstRequirement, secondRequirement,
                firstBossSnapshot, secondBossSnapshot, firstDropSnapshot, secondDropSnapshot);
            await setup.SaveChangesAsync();
        }

        var initialAt = now.AddMinutes(1);
        await using (var initial = new ApplicationDbContext(options))
        {
            await BoardEstimateService.RefreshTilesAsync(initial, [firstTile.Id, secondTile.Id], initialAt, CancellationToken.None);
            await initial.SaveChangesAsync();
        }

        string firstFingerprint;
        string secondFingerprint;
        decimal initialTotal;
        DateTimeOffset secondCalculatedAt;
        await using (var verifyInitial = new ApplicationDbContext(options))
        {
            var stored = await verifyInitial.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.ColumnIndex).ToListAsync();
            firstFingerprint = stored[0].EstimateCatalogueFingerprint!;
            secondFingerprint = stored[1].EstimateCatalogueFingerprint!;
            secondCalculatedAt = stored[1].EstimateCalculatedAt!.Value;
            initialTotal = await verifyInitial.Boards.Where(x => x.Id == board.Id).Select(x => x.TotalEhbEstimate).SingleAsync();
            Assert.Equal(3m, initialTotal);
        }

        var changedAt = now.AddMinutes(2);
        await using (var change = new ApplicationDbContext(options))
        {
            var currentBoss = await change.BossActivities.SingleAsync(x => x.Id == firstBoss.Id);
            var currentDrop = await change.SourceDrops.SingleAsync(x => x.Id == firstDrop.Id);
            currentBoss.Update("First boss updated", "Boss", 20m, null, "test", null, changedAt);
            currentBoss.AdvanceVersion();
            currentDrop.Update("1/5", .2m, null, null, "test", changedAt);
            currentDrop.AdvanceVersion();
            await change.SaveChangesAsync();

            var refreshed = await BoardEstimateService.RefreshDependentDraftTilesAsync(
                change,
                new BoardEstimateService.CatalogueChangeSet(
                    new HashSet<Guid> { firstBoss.Id },
                    new HashSet<Guid> { firstDrop.Id },
                    new HashSet<Guid>()),
                changedAt,
                CancellationToken.None);
            Assert.Equal(1, refreshed.Refreshed);
            Assert.Equal(0, refreshed.Invalid);
            await change.SaveChangesAsync();
        }

        await using (var verifyChanged = new ApplicationDbContext(options))
        {
            var changed = await verifyChanged.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.ColumnIndex).ToListAsync();
            var changedTotal = await verifyChanged.Boards.Where(x => x.Id == board.Id).Select(x => x.TotalEhbEstimate).SingleAsync();
            Assert.Equal(.25m, changed[0].EstimatedEhbSnapshot);
            Assert.Equal(2m, changed[1].EstimatedEhbSnapshot);
            Assert.NotEqual(firstFingerprint, changed[0].EstimateCatalogueFingerprint);
            Assert.Equal(secondFingerprint, changed[1].EstimateCatalogueFingerprint);
            Assert.Equal(secondCalculatedAt, changed[1].EstimateCalculatedAt);
            Assert.Equal(2.25m, changedTotal);
        }

        await using (var overview = new ApplicationDbContext(options))
        {
            var stale = await BoardEstimateService.RefreshStaleDraftTilesAsync(overview, bingoEvent.Id, changedAt.AddMinutes(1), CancellationToken.None);
            Assert.Empty(stale);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cat01OrdinaryRateSavePreservesOperatorMechanicsAndSourceMetadata(bool editRate)
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var instant = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        await using (var setup = new ApplicationDbContext(options))
        {
            var configured = await setup.SourceDrops.SingleAsync(x => x.Id == drop.Id);
            configured.SetRateMechanics(DropProbabilityScope.Team, true, .25m, 4, 7, "operator-group");
            configured.Update("1/1000", .001m, "retained condition", 10, "operator source", instant);
            await setup.SaveChangesAsync();
        }
        await using (var saving = new ApplicationDbContext(options))
        {
            var current = await saving.SourceDrops.SingleAsync(x => x.Id == drop.Id);
            var shared = await saving.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            var page = CataloguePage(saving, actor.Id, true);
            await page.OnPostUpdateDropAsync(drop.Id, current.Version, shared.Version, shared.Name, editRate ? "1/500" : current.DisplayRate,
                current.DisplayRate, null, null, default, false, null, 0, 0, null, null, null, false, default);
        }
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.SourceDrops.SingleAsync(x => x.Id == drop.Id);
        Assert.Equal(DropProbabilityScope.Team, saved.ProbabilityScope);
        Assert.True(saved.ConditionalOnParent); Assert.Equal(.25m, saved.ParentProbability);
        Assert.Equal(4, saved.AssumedParticipants); Assert.Equal(editRate ? 1 : 7, saved.RollsPerCompletion);
        Assert.Equal("operator-group", saved.RollGroup); Assert.Equal("operator source", saved.DataSource);
        Assert.Equal("retained condition", saved.RateConditionNote);
        Assert.Equal(editRate ? .002m : .001m, saved.NumericProbability);
    }

    [Theory]
    [InlineData("boss", true)]
    [InlineData("boss", false)]
    [InlineData("drop", true)]
    [InlineData("drop", false)]
    public async Task Cat01DeletionRaceMustNotLeaveDanglingTemplateReference(string recordType, bool referenceFirst)
    {
        var now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        var actor = Website($"cat01-race-{Guid.NewGuid():N}", now);
        actor.SetGlobalRole(GlobalRole.SuperAdmin);
        var boss = new BossActivity(Guid.NewGuid(), "Race activity", $"cat01-{Guid.NewGuid():N}", "Boss", 10, now);
        var item = new CatalogueItem(Guid.NewGuid(), "Race item", $"CAT01-{Guid.NewGuid():N}");
        var drop = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, "1/100", .01m, 10, now);
        item.SetPrice(0, CataloguePriceSource.Manual, now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Race event", $"cat01-event-{Guid.NewGuid():N}", "UTC", actor.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Race board", 1, 1);
        board.AcquireEditing(actor.Id, now, TimeSpan.FromDays(36500));
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(actor, boss, item, bingoEvent, board);
            if (recordType == "drop") setup.Add(drop);
            await setup.SaveChangesAsync();
        }
        var barrier = new CatalogueRaceSaveBarrier(referenceFirst);
        var query = new CatalogueRaceQueryBarrier(recordType, referenceFirst ? "FOR UPDATE" : "FOR SHARE");
        var deletionOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options)
            .AddInterceptors(referenceFirst ? query : barrier).Options;
        var writerOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options)
            .AddInterceptors(referenceFirst ? barrier : query).Options;
        await using var deleting = new ApplicationDbContext(deletionOptions);
        await using var referring = new ApplicationDbContext(writerOptions);
        var page = CataloguePage(deleting, actor.Id, true);
        var writer = Page(referring, actor.Id);
        writer.BoardVersion = 1;
        writer.TileDraft = new BoardModel.TileDraftInput
        {
            Position = 0,
            Name = "Race tile",
            ManualEhb = recordType == "boss" ? 1m : null,
            Requirements = [new BoardModel.RequirementInput
            {
                Kind = recordType == "boss" ? "challenge" : "drops", Description = "Collect", Target = 1,
                BossIds = [boss.Id], DropIds = recordType == "drop" ? [drop.Id] : []
            }]
        };
        var targetId = recordType == "boss" ? boss.Id : drop.Id;
        var first = referenceFirst ? writer.OnPostCreateTileAsync(bingoEvent.Id, default)
            : page.OnPostDeleteAsync(recordType, targetId, 1, true, default);
        await barrier.Ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var second = referenceFirst ? page.OnPostDeleteAsync(recordType, targetId, 1, true, default)
            : writer.OnPostCreateTileAsync(bingoEvent.Id, default);
        try
        {
            await query.Attempted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(second.IsCompleted, "The opposing mutation waits for the first transaction's catalogue lock.");
        }
        finally { barrier.Release.TrySetResult(true); }
        await Task.WhenAll(first, second);
        await using var verify = new ApplicationDbContext(options);
        var exists = recordType == "boss" ? await verify.BossActivities.AnyAsync(x => x.Id == boss.Id)
            : await verify.SourceDrops.AnyAsync(x => x.Id == drop.Id);
        Assert.Equal(referenceFirst, exists);
        Assert.Equal(referenceFirst ? "committed" : "failed", writer.TempData["BoardTileOutcome"]);
        Assert.Equal(referenceFirst ? 1 : 0, await verify.TemplateRequirementBosses.CountAsync(x => x.BossActivityId == boss.Id));
        Assert.Equal(referenceFirst && recordType == "drop" ? 1 : 0, await verify.TemplateRequirementDrops.CountAsync(x => x.SourceDropId == drop.Id));
        if (referenceFirst) Assert.Contains("referenced", page.TempData["StatusMessage"]?.ToString(), StringComparison.Ordinal);
        else Assert.Contains("reload", writer.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CatalogueRaceSaveBarrier(bool referenceFirst) : SaveChangesInterceptor
    {
        public TaskCompletionSource<bool> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (referenceFirst
                ? eventData.Context!.ChangeTracker.Entries<TemplateRequirementBoss>().Any(x => x.State == EntityState.Added)
                : eventData.Context!.ChangeTracker.Entries().Any(x => x.State == EntityState.Deleted && x.Entity is SourceDrop or BossActivity))
            {
                Ready.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class CatalogueRaceQueryBarrier(string recordType, string mode) : DbCommandInterceptor
    {
        public TaskCompletionSource<bool> Attempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(recordType == "boss" ? "boss_activities" : "source_drops", StringComparison.Ordinal)
                && command.CommandText.Contains(mode, StringComparison.Ordinal)) Attempted.TrySetResult(true);
            return ValueTask.FromResult(result);
        }
    }
}
