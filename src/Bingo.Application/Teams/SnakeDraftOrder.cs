using Bingo.Domain.Teams;

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
        DraftRosterDistribution distribution)
    {
        if (orderedTeamIds.Count != distribution.DraftedTeamCount)
            throw new InvalidOperationException("The draft order does not match the derived distribution.");
        if (currentRosterSizes.Values.Sum() >= distribution.IncludedParticipants)
            return null;
        if (distribution.ValidateCurrentRosters(currentRosterSizes).Count != 0)
            return null;
        var logicalTurn = 0;
        foreach (var pickedTeamId in activePickTeamIds)
        {
            while (GetTurn(logicalTurn, orderedTeamIds).TeamId != pickedTeamId) logicalTurn++;
            logicalTurn++;
        }

        // A larger preassigned roster must never receive another ordinary snake turn while a
        // smaller roster exists.  This is deliberately evaluated before the upper size cap.
        var smallestRoster = orderedTeamIds.Min(teamId => currentRosterSizes.GetValueOrDefault(teamId));
        var guard = 0;
        while (guard++ < orderedTeamIds.Count * (distribution.IncludedParticipants + 1))
        {
            var snakeTurn = GetTurn(logicalTurn++, orderedTeamIds);
            var size = currentRosterSizes.GetValueOrDefault(snakeTurn.TeamId);
            if (size > smallestRoster || size >= distribution.LargerSize)
                continue;
            return snakeTurn with { PickNumber = activePickTeamIds.Count + 1 };
        }
        return null;
    }

    public static DraftTurn? GetNextEligibleTurn(
        IReadOnlyList<Guid> activePickTeamIds,
        IReadOnlyList<Guid> orderedTeamIds,
        IReadOnlyDictionary<Guid, int> currentRosterSizes,
        int legacyTargetTeamSize) =>
        GetNextEligibleTurn(activePickTeamIds, orderedTeamIds, currentRosterSizes,
            DraftRosterDistribution.Derive(legacyTargetTeamSize * orderedTeamIds.Count, orderedTeamIds.Count));

    public static IReadOnlyDictionary<Guid, int> ProjectFinalRosterSizes(
        IReadOnlyList<Guid> activePickTeamIds,
        IReadOnlyList<Guid> orderedTeamIds,
        IReadOnlyDictionary<Guid, int> currentRosterSizes,
        DraftRosterDistribution distribution)
    {
        var sizes = orderedTeamIds.ToDictionary(id => id, id => currentRosterSizes.GetValueOrDefault(id));
        var picks = activePickTeamIds.ToList();
        while (sizes.Values.Sum() < distribution.IncludedParticipants)
        {
            var next = GetNextEligibleTurn(picks, orderedTeamIds, sizes, distribution);
            if (next is null) break;
            sizes[next.TeamId]++;
            picks.Add(next.TeamId);
        }
        return sizes;
    }
}

public sealed record DraftTurn(int PickNumber, int RoundNumber, Guid TeamId);
