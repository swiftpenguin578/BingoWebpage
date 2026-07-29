namespace Bingo.Domain.Teams;

/// <summary>Authoritative, non-persisted sizing preview for the website draft.</summary>
public sealed record DraftRosterDistribution(int IncludedParticipants, int DraftedTeamCount, int LargerSize, int SmallerSize, int LargerTeamCount, int SmallerTeamCount)
{
    public static DraftRosterDistribution Derive(int includedParticipants, int draftedTeamCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(includedParticipants);
        ArgumentOutOfRangeException.ThrowIfNegative(draftedTeamCount);
        if (draftedTeamCount == 0) return new(includedParticipants, 0, 0, 0, 0, 0);
        var smaller = includedParticipants / draftedTeamCount;
        var largerCount = includedParticipants % draftedTeamCount;
        return new(includedParticipants, draftedTeamCount, smaller + (largerCount == 0 ? 0 : 1), smaller, largerCount, draftedTeamCount - largerCount);
    }

    public bool CanAccommodate(int currentRosterSize) => currentRosterSize <= LargerSize;

    public bool IsBalanced(IReadOnlyCollection<int> rosterSizes) =>
        rosterSizes.Count == DraftedTeamCount && rosterSizes.All(size => size is >= 0 && size <= LargerSize) &&
        rosterSizes.Sum() == IncludedParticipants;

    /// <summary>
    /// Validates the part of the distribution that is independent of presentation and persistence.
    /// A caller supplies only active drafted rosters; pre-formed rosters never participate here.
    /// </summary>
    public IReadOnlyList<string> ValidateCurrentRosters(IReadOnlyDictionary<Guid, int> currentRosterSizes)
    {
        var blockers = new List<string>();
        if (DraftedTeamCount < 2)
            blockers.Add("At least two active drafted teams are required.");
        if (currentRosterSizes.Count != DraftedTeamCount)
            blockers.Add("Every active drafted team must have a current roster projection.");
        if (currentRosterSizes.Any(pair => pair.Value < 0 || pair.Value > LargerSize))
            blockers.Add("Existing drafted-team assignments cannot fit the balanced final roster sizes.");
        if (currentRosterSizes.Values.Sum() > IncludedParticipants)
            blockers.Add("Existing drafted-team assignments exceed the included participant total.");

        var forcedLargerTeams = currentRosterSizes.Values.Count(size => size > SmallerSize);
        if (forcedLargerTeams > LargerTeamCount)
            blockers.Add("Existing drafted-team assignments make a final maximum size difference of one impossible.");
        return blockers;
    }

    /// <summary>Returns stable named final-size denominators. Teams already above the smaller size are forced larger; the randomized order breaks remaining ties.</summary>
    public IReadOnlyDictionary<Guid, int> AllocateNamedFinalSizes(IReadOnlyList<Guid> orderedTeamIds, IReadOnlyDictionary<Guid, int> currentRosterSizes)
    {
        if (orderedTeamIds.Count != DraftedTeamCount)
            throw new InvalidOperationException("The named drafted-team order does not match the derived distribution.");
        if (ValidateCurrentRosters(currentRosterSizes).Count != 0)
            throw new InvalidOperationException("The current drafted-team rosters cannot produce a balanced distribution.");

        var forced = orderedTeamIds.Where(id => currentRosterSizes.GetValueOrDefault(id) > SmallerSize).ToHashSet();
        var larger = new HashSet<Guid>(forced);
        foreach (var teamId in orderedTeamIds)
        {
            if (larger.Count == LargerTeamCount) break;
            larger.Add(teamId);
        }
        return orderedTeamIds.ToDictionary(id => id, id => larger.Contains(id) ? LargerSize : SmallerSize);
    }
}
