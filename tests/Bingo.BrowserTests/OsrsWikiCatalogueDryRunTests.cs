using Bingo.Web.Catalogue;

namespace Bingo.BrowserTests;

public sealed class OsrsWikiCatalogueDryRunTests
{
    [Fact]
    public void ParsesStandardUniqueDropRowsAndNestedReferences()
    {
        const string wiki = """
            ===Uniques===
            {{DropsTableHead}}
            {{DropsLine|name=Bandos chestplate|quantity=1|rarity=1/381|raritynotes=<ref group="d">A specific piece.</ref>}}
            {{DropsLine|name=Bandos hilt|quantity=1|rarity=1/508}}
            {{DropsTableBottom}}
            """;

        var drops = OsrsWikiCatalogueDryRunService.ParseDropLines(wiki);

        Assert.Collection(drops,
            drop =>
            {
                Assert.Equal("Bandos chestplate", drop.Name);
                Assert.Equal(1m / 381m, drop.Rates.Single().Probability);
                Assert.Equal("A specific piece.", drop.Condition);
            },
            drop => Assert.Equal("Bandos hilt", drop.Name));
    }

    [Fact]
    public void PreservesAlternateRatesForManualReview()
    {
        const string wiki = """
            {{DropsLine|name=Avernic treads|quantity=1|rarity=1/1350|altrarity=1/540|altraritydash=yes|raritynotes=<ref group=d>Only dropped at delve 4 and deeper.</ref>}}
            """;

        var drop = Assert.Single(OsrsWikiCatalogueDryRunService.ParseDropLines(wiki));

        Assert.Equal(2, drop.Rates.Count);
        Assert.Equal("1/1350", drop.Rates[0].DisplayRate);
        Assert.Equal("1/540", drop.Rates[1].DisplayRate);
        Assert.Equal("Only dropped at delve 4 and deeper.", drop.Condition);
    }

    [Fact]
    public void ExpandsWikiArithmeticUsedByCascadingPreRollRates()
    {
        const string wiki = "{{DropsLine|name=Hydra's claw|quantity=1|rarity=1/{{#expr:1000/(1999/2000*1999/2000) round 1}}}}";

        var drop = Assert.Single(OsrsWikiCatalogueDryRunService.ParseDropLines(wiki));

        Assert.Equal("1/1001", drop.Rates.Single().DisplayRate);
        Assert.Equal(1m / 1001m, drop.Rates.Single().Probability);
    }

    [Fact]
    public void MarksNonNumericWikiRatesWithoutInventingAProbability()
    {
        const string wiki = "{{DropsLine|name=Demon tear|quantity=50|rarity=Always}}";

        var drop = Assert.Single(OsrsWikiCatalogueDryRunService.ParseDropLines(wiki));

        Assert.Equal("Always", drop.Rates.Single().DisplayRate);
        Assert.Null(drop.Rates.Single().Probability);
    }

    [Fact]
    public void ParsesRewardDropRowsUsedByChestsAndRaids()
    {
        const string wiki = "{{DropsLineReward|name=Twisted bow|quantity=1|rarity=2/69}}";

        var drop = Assert.Single(OsrsWikiCatalogueDryRunService.ParseDropLines(wiki));

        Assert.Equal("Twisted bow", drop.Name);
        Assert.Equal(2m / 69m, drop.Rates.Single().Probability);
    }

    [Fact]
    public void KeepsBarrowsRewardRollsVisibleForReview()
    {
        const string wiki = "{{DropsLineReward|name=Ahrim's hood|quantity=1|rarity=1/2448|rolls=7}}";

        var drop = Assert.Single(OsrsWikiCatalogueDryRunService.ParseDropLines(wiki));

        Assert.Equal("1/2448 × 7 rolls", drop.Rates.Single().DisplayRate);
        Assert.Null(drop.Rates.Single().Probability);
        Assert.Contains("combined per-chest probability", drop.Condition);
    }

    [Theory]
    [InlineData("Category:Pets", true)]
    [InlineData("category:jars", true)]
    [InlineData("Category:Treasure Trails", false)]
    [InlineData("Category:Collection log items", false)]
    [InlineData(null, false)]
    public void OnlyPetAndJarCategoriesQualifyForTertiaryImport(string? category, bool expected)
    {
        Assert.Equal(expected, OsrsWikiCatalogueDryRunService.IsPetOrJarCategory(category));
    }

    [Theory]
    [InlineData("Category:Pets", true)]
    [InlineData("category:pets", true)]
    [InlineData("Category:Jars", false)]
    [InlineData(null, false)]
    public void PetCategoryUsesTheInventoryItemImage(string? category, bool expected)
    {
        Assert.Equal(expected, OsrsWikiCatalogueDryRunService.IsPetCategory(category));
    }

    [Theory]
    [InlineData("Category:Collection log items", true)]
    [InlineData("category:pets", true)]
    [InlineData("Category:Jars", true)]
    [InlineData("Category:Weapons", false)]
    [InlineData(null, false)]
    public void SpecialSectionsKeepOnlyCollectionLogPetsAndJars(string? category, bool expected)
    {
        Assert.Equal(expected, OsrsWikiCatalogueDryRunService.IsSpecialItemCategory(category));
    }
}
