using Bingo.Domain.Boards;
using Bingo.Web.Catalogue;
using Bingo.Web.Pages.Admin.Events;

namespace Bingo.BrowserTests;

// The Board page sends the team board's boss artwork per tile (same rule as PublicBoardService):
// one image per boss family, the family's priority boss first, at most four, as public URLs.
public sealed class BoardPreviewBossArtTests
{
    private static BoardModel.BossArtworkSource Boss(string name, string file) =>
        new(Guid.NewGuid(), name, "https://oldschool.runescape.wiki/images/" + file);

    private static BoardRequirementSnapshot Requirement(Guid tileId, int position) =>
        new(Guid.NewGuid(), tileId, position, 1, false, false, "Requirement " + position, false);

    [Fact]
    public void BossTileGetsOneImagePerBossFamilyAtMostFourAsPublicUrls()
    {
        var tile = Guid.NewGuid();
        var requirement = Requirement(tile, 0);
        var bosses = new[] { Boss("Artio", "Artio.png"), Boss("Callisto", "Callisto.png"), Boss("Vorkath", "Vorkath.png"), Boss("Chambers of Xeric (CM)", "CoXCM.png"), Boss("Chambers of Xeric", "CoX.png"), Boss("Zulrah", "Zulrah.png"), Boss("Nex", "Nex.png") };
        var ids = new Dictionary<Guid, List<Guid>> { [requirement.Id] = bosses.Select(value => value.BossId).ToList() };

        var art = BoardModel.BuildBossArtByTile(new Dictionary<Guid, IReadOnlyList<BoardRequirementSnapshot>> { [tile] = [requirement] }, ids, bosses)[tile];

        // Callisto/Artio and both Chambers variants collapse to one image each (priority boss wins); the fifth family is cut.
        Assert.Equal(4, art.Count);
        Assert.All(art, url => Assert.StartsWith(OsrsWikiImageCache.EndpointPath + "?source=", url));
        string[] expectedOrder = ["Callisto.png", "Vorkath.png", "CoX.png", "Zulrah.png"];
        var decoded = art.Select(Uri.UnescapeDataString).ToArray();
        Assert.True(Enumerable.Range(0, 4).All(index => decoded.Any(url => url.EndsWith(expectedOrder[index], StringComparison.Ordinal))));
        Assert.DoesNotContain(decoded, url => url.EndsWith("Artio.png", StringComparison.Ordinal) || url.EndsWith("CoXCM.png", StringComparison.Ordinal));
    }

    [Fact]
    public void TileWithoutBossesOrBossImagesGetsNoArtAndSharedImagesAreNotRepeated()
    {
        var plain = Guid.NewGuid(); var shared = Guid.NewGuid();
        var plainRequirement = Requirement(plain, 0); var first = Requirement(shared, 0); var second = Requirement(shared, 1);
        var boss = Boss("Vorkath", "Vorkath.png");
        var ids = new Dictionary<Guid, List<Guid>> { [first.Id] = [boss.BossId], [second.Id] = [boss.BossId], [plainRequirement.Id] = [Guid.NewGuid()] };
        var result = BoardModel.BuildBossArtByTile(new Dictionary<Guid, IReadOnlyList<BoardRequirementSnapshot>> { [plain] = [plainRequirement], [shared] = [first, second] }, ids, [boss]);
        Assert.Empty(result[plain]);
        Assert.Single(result[shared]);
    }
}
