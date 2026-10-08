using Bingo.Application.Boards;
using Bingo.Domain.Boards;

namespace Bingo.Application.Tests;

public sealed class Au11TileEhbTests
{
    [Fact]
    public void OverrideRequiresValidMechanicsAndPreservesFallback()
    {
        Assert.Equal(7m, EhbCalculator.CalculateTileEstimate(ObjectiveType.DropRequirements, [(false, 2m), (false, 5m)], null));
        Assert.Equal(20m, EhbCalculator.CalculateTileEstimate(ObjectiveType.DropRequirements, [(false, 2m), (false, 5m)], 20m));
        Assert.Equal(0m, EhbCalculator.CalculateTileEstimate(ObjectiveType.DropRequirements, [(false, null)], 20m));
        Assert.Equal(0m, EhbCalculator.CalculateTileEstimate(ObjectiveType.DropRequirements, [(false, 2m), (true, null)], 20m));
        Assert.Equal(0m, EhbCalculator.CalculateTileEstimate(ObjectiveType.Manual, [(true, null)], null));
        Assert.Equal(20m, EhbCalculator.CalculateTileEstimate(ObjectiveType.Manual, [(true, null)], 20m));
        Assert.Equal(0m, EhbCalculator.CalculateTileEstimate(ObjectiveType.DropRequirements, [(false, 2m)], -1m));
    }
}
