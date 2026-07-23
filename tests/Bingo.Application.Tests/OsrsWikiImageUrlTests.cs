using Bingo.Application.Catalogue;

namespace Bingo.Application.Tests;

public sealed class OsrsWikiImageUrlTests
{
    [Theory]
    [InlineData(
        "https://oldschool.runescape.wiki/w/Hydra%27s_claw#/media/File:Hydra's_claw_detail.png",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/Hydra%27s_claw_detail.png")]
    [InlineData(
        "https://oldschool.runescape.wiki/w/Alchemical_Hydra#/media/File:Alchemical_Hydra_(serpentine).png",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/Alchemical_Hydra_%28serpentine%29.png")]
    [InlineData(
        "https://oldschool.runescape.wiki/w/File:Hydra%27s_claw_detail.png",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/Hydra%27s_claw_detail.png")]
    [InlineData(
        "https://oldschool.runescape.wiki/Special:Redirect/file/Nex.png",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/Nex.png")]
    public void NormalizeConvertsWikiFilePagesToDirectFileRedirects(string input, string expected)
    {
        Assert.Equal(expected, OsrsWikiImageUrl.Normalize(input));
    }

    [Fact]
    public void NormalizePreservesOtherAbsoluteUrls()
    {
        const string url = "https://example.com/images/hydra.png";
        Assert.Equal(url, OsrsWikiImageUrl.Normalize(url));
    }
}
