using Bingo.Application.Events;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bingo.IntegrationTests;

public sealed partial class Au12PlacementRuleIntegrationTests
{
    [Fact]
    public async Task Au11A2PopulatedCleanupClearsOnlyDropTemplatesAndDownCannotRestore()
    {
        var fixture = await SeedAsync(PlacementRule.CreditedEhbThenScoreTime, false);
        await using var db = new ApplicationDbContext(options);
        var service = new EventFinalizationService(db, new PublicBoardService(db, new Clock()), new Clock());
        var readiness = await service.GetReadinessAsync(fixture.EventId);
        await service.FinalizeAsync(fixture.EventId, new LifecycleActor(fixture.AdminId, "admin"), readiness!.EventVersion);
        await db.GetService<IMigrator>().MigrateAsync("20261004002948_AddEventPlacementRule");
        var old = new TileTemplate(Guid.NewGuid(), "Legacy override", "", ObjectiveType.DropRequirements, "", 25m);
        var zero = new TileTemplate(Guid.NewGuid(), "Legacy zero", "", ObjectiveType.DropRequirements, "", null);
        var manual = new TileTemplate(Guid.NewGuid(), "Manual", "", ObjectiveType.Manual, "", 7m);
        db.AddRange(old, zero, manual);
        db.Entry(zero).Property(x => x.ManualEhbOverride).CurrentValue = 0m;
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.TileTemplates.CountAsync(x => x.ObjectiveType == ObjectiveType.DropRequirements && x.ManualEhbOverride != null));
        var tiles = await db.BoardTiles.OrderBy(x => x.Id).Select(x => x.EstimatedEhbSnapshot).ToListAsync();
        var approval = await db.BoardApprovalTileSnapshots.OrderBy(x => x.Id).Select(x => x.EstimatedEhb).ToListAsync();
        var official = await db.OfficialPlacements.OrderBy(x => x.Id).Select(x => new { x.Placement, x.EhbTiebreak }).ToListAsync();
        var input = (await db.EventFinalizations.SingleAsync()).CalculationInputsJson;
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(0, await db.TileTemplates.CountAsync(x => x.ObjectiveType == ObjectiveType.DropRequirements && x.ManualEhbOverride != null));
        Assert.Equal(7m, (await db.TileTemplates.SingleAsync(x => x.Id == manual.Id)).ManualEhbOverride);
        Assert.Equal(tiles, await db.BoardTiles.OrderBy(x => x.Id).Select(x => x.EstimatedEhbSnapshot).ToListAsync());
        Assert.Equal(approval, await db.BoardApprovalTileSnapshots.OrderBy(x => x.Id).Select(x => x.EstimatedEhb).ToListAsync());
        Assert.Equal(official, await db.OfficialPlacements.OrderBy(x => x.Id).Select(x => new { x.Placement, x.EhbTiebreak }).ToListAsync());
        Assert.Equal(input, (await db.EventFinalizations.SingleAsync()).CalculationInputsJson);
        await db.GetService<IMigrator>().MigrateAsync("20261004002948_AddEventPlacementRule");
        db.ChangeTracker.Clear();
        Assert.Null((await db.TileTemplates.SingleAsync(x => x.Id == old.Id)).ManualEhbOverride);
        Assert.Null((await db.TileTemplates.SingleAsync(x => x.Id == zero.Id)).ManualEhbOverride);
        Assert.Equal(7m, (await db.TileTemplates.SingleAsync(x => x.Id == manual.Id)).ManualEhbOverride);
    }
}
