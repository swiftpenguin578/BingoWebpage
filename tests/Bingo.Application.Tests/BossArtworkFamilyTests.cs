using Bingo.Application.Catalogue;

namespace Bingo.Application.Tests;

public sealed class BossArtworkFamilyTests
{
    [Theory]
    [InlineData("Callisto", "Artio")]
    [InlineData("Venenatis", "Spindel")]
    [InlineData("Vet'ion", "Calvar'ion")]
    [InlineData("Chambers of Xeric", "Chambers of Xeric (CM)")]
    [InlineData("The Nightmare", "Phosani's Nightmare")]
    [InlineData("Theatre of Blood", "Theatre of Blood (HM)")]
    [InlineData("Tombs of Amascut", "Tombs of Amascut (Expert Mode)")]
    [InlineData("The Gauntlet", "The Corrupted Gauntlet")]
    public void VariantsShareAnArtworkFamily(string primary, string variant)
    {
        Assert.Equal(BossArtworkFamily.Key(primary), BossArtworkFamily.Key(variant));
        Assert.True(BossArtworkFamily.Priority(primary) < BossArtworkFamily.Priority(variant));
    }

    [Fact]
    public void UnrelatedBossesRemainDistinct()
    {
        Assert.NotEqual(BossArtworkFamily.Key("Nex"), BossArtworkFamily.Key("Yama"));
    }
}
