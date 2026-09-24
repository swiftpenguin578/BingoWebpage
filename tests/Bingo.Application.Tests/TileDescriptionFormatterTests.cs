using Bingo.Application.Boards;

namespace Bingo.Application.Tests;

public sealed class TileDescriptionFormatterTests
{
    [Fact]
    public void SingleDistinctCatalogueItemUsesItsName()
    {
        var item = Guid.NewGuid();
        var description = TileDescriptionFormatter.Format(
        [
            new(1, 1, false, "", [new(item, "Ahrim's robe top", "Barrows Chests")])
        ]);

        Assert.Equal("Collect 1 Ahrim's robe top", description);
    }

    [Fact]
    public void MultipleItemsUseOnlyTheSelectedSourcesAsAlternativesAndDeduplicateAliases()
    {
        var sharedItem = Guid.NewGuid();
        var otherItem = Guid.NewGuid();
        var description = TileDescriptionFormatter.Format(
        [
            new(1, 1, false, "", [
                new(sharedItem, "Ahrim's robe top", "Barrows Chests"),
                new(sharedItem, "Ahrim's robe top", "Barrows Chests"),
                new(otherItem, "Blood shard", "Lunar Chests")])
        ]);

        Assert.Equal("Collect 1 eligible drop from Barrows Chests or Lunar Chests", description);
    }

    [Fact]
    public void IndependentRequirementsKeepPositionOrderAndConfiguredWeightedTarget()
    {
        var earlier = new TileDescriptionRequirement(2, 5, false, "", [
            new(Guid.NewGuid(), "Scythe", "Theatre of Blood"),
            new(Guid.NewGuid(), "Justiciar piece", "Theatre of Blood")]);
        var later = new TileDescriptionRequirement(1, 1, false, "", [
            new(Guid.NewGuid(), "Ahrim's robe top", "Barrows Chests"),
            new(Guid.NewGuid(), "Karil's crossbow", "Barrows Chests")]);

        var description = TileDescriptionFormatter.Format([earlier, later]);

        Assert.Equal("Collect 1 eligible drop from Barrows Chests & Collect 5 eligible drops from Theatre of Blood", description);
    }

    [Fact]
    public void ManualObjectiveDescriptionsAreCombinedWithoutRewritingTheirWording()
    {
        var description = TileDescriptionFormatter.Format(
        [
            new(2, 1, true, "Complete the second challenge", []),
            new(1, 1, true, "Finish the first challenge", [])
        ]);

        Assert.Equal("Finish the first challenge & Complete the second challenge", description);
    }
}
