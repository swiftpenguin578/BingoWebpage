namespace Bingo.Application.Teams;

public static class SnakeDraftOrder
{
    public static DraftTurn GetTurn(int completedPickCount, IReadOnlyList<Guid> orderedTeamIds)
    {
        if (orderedTeamIds.Count < 2) throw new InvalidOperationException("A snake draft requires at least two teams.");
        ArgumentOutOfRangeException.ThrowIfNegative(completedPickCount);
        var roundIndex = completedPickCount / orderedTeamIds.Count;
        var positionInRound = completedPickCount % orderedTeamIds.Count;
        var teamIndex = roundIndex % 2 == 0 ? positionInRound : orderedTeamIds.Count - positionInRound - 1;
        return new DraftTurn(completedPickCount + 1, roundIndex + 1, orderedTeamIds[teamIndex]);
    }

    public static DraftTurn? GetNextEligibleTurn(
        IReadOnlyList<Guid> activePickTeamIds,
        IReadOnlyList<Guid> orderedTeamIds,
        IReadOnlyDictionary<Guid, int> currentRosterSizes,
        int targetTeamSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(targetTeamSize, 1);
        var logicalTurn = 0;
        foreach (var pickedTeamId in activePickTeamIds)
        {
            while (GetTurn(logicalTurn, orderedTeamIds).TeamId != pickedTeamId) logicalTurn++;
            logicalTurn++;
        }

        if (orderedTeamIds.All(teamId => currentRosterSizes.GetValueOrDefault(teamId) >= targetTeamSize)) return null;
        while (currentRosterSizes.GetValueOrDefault(GetTurn(logicalTurn, orderedTeamIds).TeamId) >= targetTeamSize) logicalTurn++;
        var snakeTurn = GetTurn(logicalTurn, orderedTeamIds);
        return snakeTurn with { PickNumber = activePickTeamIds.Count + 1 };
    }
}

public sealed record DraftTurn(int PickNumber, int RoundNumber, Guid TeamId);
