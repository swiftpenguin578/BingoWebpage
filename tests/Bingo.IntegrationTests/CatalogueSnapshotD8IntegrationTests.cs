using System.Text.Json.Nodes;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task D8SnapshotImportCarriesTeamSizeAndRejectsRetiredContextBeforeWrites()
    {
        var (_, boss, item, drop) = await PriceFixtureAsync();
        var path = Path.Combine(Path.GetTempPath(), $"catalogue-d8-{Guid.NewGuid():N}.json");
        try
        {
            await using (var exported = new ApplicationDbContext(options))
            {
                (await exported.BossActivities.SingleAsync(value => value.Id == boss.Id)).SetTeamSize(4);
                await exported.SaveChangesAsync();
                await CatalogueSnapshotTestFixture.WriteAsync(exported, path);
            }

            var originalSnapshot = await File.ReadAllTextAsync(path);
            var invalidSnapshot = JsonNode.Parse(originalSnapshot)!.AsObject();
            invalidSnapshot["drops"]!.AsArray()[0]!["assumedParticipants"] = 2;
            await File.WriteAllTextAsync(path, invalidSnapshot.ToJsonString());

            long bossVersion;
            long itemVersion;
            long dropVersion;
            int auditCount;
            await using (var before = new ApplicationDbContext(options))
            {
                bossVersion = await before.BossActivities.Where(value => value.Id == boss.Id).Select(value => value.Version).SingleAsync();
                itemVersion = await before.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.Version).SingleAsync();
                dropVersion = await before.SourceDrops.Where(value => value.Id == drop.Id).Select(value => value.Version).SingleAsync();
                auditCount = await before.AuditEntries.CountAsync();
            }

            await using (var rejected = new ApplicationDbContext(options))
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new CatalogueSnapshotService(rejected, TimeProvider.System).ApplyAsync(path));
                Assert.Contains("no records were changed", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            await using (var unchanged = new ApplicationDbContext(options))
            {
                Assert.Equal(bossVersion, await unchanged.BossActivities.Where(value => value.Id == boss.Id).Select(value => value.Version).SingleAsync());
                Assert.Equal(itemVersion, await unchanged.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.Version).SingleAsync());
                Assert.Equal(dropVersion, await unchanged.SourceDrops.Where(value => value.Id == drop.Id).Select(value => value.Version).SingleAsync());
                Assert.Equal(auditCount, await unchanged.AuditEntries.CountAsync());
            }

            await File.WriteAllTextAsync(path, originalSnapshot);
            await using (var applied = new ApplicationDbContext(options))
            {
                (await applied.BossActivities.SingleAsync(value => value.Id == boss.Id)).SetTeamSize(1);
                await applied.SaveChangesAsync();
                await new CatalogueSnapshotService(applied, TimeProvider.System).ApplyAsync(path);
            }

            await using var verify = new ApplicationDbContext(options);
            Assert.Equal(4, await verify.BossActivities.Where(value => value.Id == boss.Id).Select(value => value.TeamSize).SingleAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
