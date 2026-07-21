using Bingo.Domain.Catalogue;

namespace Bingo.Domain.Tests;

public sealed class SourceDropRateMechanicsTests
{
    [Fact]
    public void StoredProbabilityIsAlwaysTheFinalPersonalProbability()
    {
        var drop = new SourceDrop(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1/518.7 personal", 1m / 518.7m, null, DateTimeOffset.UtcNow);
        drop.SetRateMechanics(DropProbabilityScope.Team, true, 1m / 9.1m, 3, 1, "purple table");

        Assert.Equal(1m / 518.7m, drop.EffectiveProbabilityPerRoll());
    }

    [Fact]
    public void ParticipantRateIsNotDividedByTeamSize()
    {
        var drop = new SourceDrop(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1/27.3", 1m / 27.3m, null, DateTimeOffset.UtcNow);
        drop.SetRateMechanics(DropProbabilityScope.Participant, false, null, 1, 1, "purple table");

        Assert.Equal(1m / 27.3m, drop.EffectiveProbabilityPerRoll());
        Assert.Equal(1, drop.AssumedParticipants);
    }
}
