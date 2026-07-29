using Bingo.Domain.Teams;

namespace Bingo.Domain.Tests;

public sealed class TeamAndDraftRulesTests
{
    [Fact]
    public void FirstPickMarkerIsPermanentWhenPicksAreLaterUndone()
    {
        var at = DateTimeOffset.UtcNow;
        var draft = new DraftSession(Guid.NewGuid(), Guid.NewGuid(), 2);

        draft.RecordFirstPick(at);
        draft.RecordFirstPick(at.AddHours(1));

        Assert.Equal(at, draft.FirstPickRecordedAt);
    }

    [Fact]
    public void TeamSlugRemainsStableWhenMetadataIsUpdated()
    {
        var team = new Team(Guid.NewGuid(), Guid.NewGuid(), "Old", "stable-slug", TeamFormationType.Drafted, null, true);

        team.Update("New", "attempted-new-slug", "Clan", "https://legacy.invalid/image.png");

        Assert.Equal("New", team.Name);
        Assert.Equal("stable-slug", team.Slug);
        Assert.Null(team.ImageUrl);
    }

    [Theory]
    [InlineData(11, 3, 4, 3, 2, 1)]
    [InlineData(12, 3, 4, 4, 0, 3)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    public void DistributionUsesDeterministicRemainders(int participants, int teams, int larger, int smaller, int largerTeams, int smallerTeams)
    {
        var result = DraftRosterDistribution.Derive(participants, teams);
        Assert.Equal((larger, smaller, largerTeams, smallerTeams), (result.LargerSize, result.SmallerSize, result.LargerTeamCount, result.SmallerTeamCount));
    }
    [Fact]
    public void PreformedTeamCannotReceiveDraftTurns() => Assert.Throws<InvalidOperationException>(() => new Team(Guid.NewGuid(), Guid.NewGuid(), "External", "external", TeamFormationType.Preformed, "Clan", true));

    [Fact]
    public void LegacyTargetSizeIsNotPartOfTheDraftStateTransition()
    {
        var draft = new DraftSession(Guid.NewGuid(), Guid.NewGuid(), 14);
        draft.Start(DateTimeOffset.UtcNow);
        Assert.Equal(14, draft.TargetTeamSize);
    }

    [Fact]
    public void PauseResumeAndFinalizeFollowValidTransitions()
    {
        var draft = new DraftSession(Guid.NewGuid(), Guid.NewGuid(), 14);
        draft.Start(DateTimeOffset.UtcNow); draft.Pause(); draft.Resume(); draft.Finalize(DateTimeOffset.UtcNow);
        Assert.Equal(DraftState.Finalized, draft.State);
        Assert.NotNull(draft.FinalizedAt);
    }

    [Fact]
    public void ReopenRetainsThePickLockButWithdrawsDraftControl()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = Guid.NewGuid();
        var draft = new DraftSession(Guid.NewGuid(), Guid.NewGuid(), 2);
        draft.AcquireControl(controller, now, TimeSpan.FromMinutes(5));
        draft.Start(now);
        draft.RecordFirstPick(now);
        draft.Finalize(now.AddMinutes(1));

        draft.Reopen(now.AddMinutes(2));

        Assert.Equal(DraftState.Running, draft.State);
        Assert.Null(draft.FinalizedAt);
        Assert.Null(draft.ControllerAccountId);
        Assert.NotNull(draft.FirstPickRecordedAt);
    }

    [Fact]
    public void UndoCanOnlyHappenOnce()
    {
        var pick = new DraftPick(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1, DateTimeOffset.UtcNow);
        pick.Undo(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => pick.Undo(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ActiveControllerBlocksAnotherAdministratorUnlessTheyExplicitlyTakeOver()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var draft = new DraftSession(Guid.NewGuid(), Guid.NewGuid(), 14);

        draft.AcquireControl(first, now, TimeSpan.FromMinutes(5));
        Assert.Throws<InvalidOperationException>(() => draft.AcquireControl(second, now.AddMinutes(1), TimeSpan.FromMinutes(5)));

        var previous = draft.AcquireControl(second, now.AddMinutes(1), TimeSpan.FromMinutes(5), force: true);
        Assert.Equal(first, previous);
        Assert.Equal(second, draft.ControllerAccountId);
        Assert.Throws<InvalidOperationException>(() => draft.RequireControl(first, now.AddMinutes(2)));
    }

    [Fact]
    public void ExpiredOrReleasedControlCanBeAcquiredByAnotherAdministrator()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var draft = new DraftSession(Guid.NewGuid(), Guid.NewGuid(), 14);

        draft.AcquireControl(first, now, TimeSpan.FromMinutes(1));
        draft.AcquireControl(second, now.AddMinutes(2), TimeSpan.FromMinutes(5));
        draft.ReleaseControl(second, now.AddMinutes(3));

        Assert.Null(draft.ControllerAccountId);
        Assert.False(draft.HasActiveController(now.AddMinutes(3)));
    }

    [Fact]
    public void DraftHeartbeatDoesNotInvalidateTheControllersOpenAction()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = Guid.NewGuid();
        var draft = new DraftSession(Guid.NewGuid(), Guid.NewGuid(), 14);
        draft.AcquireControl(controller, now, TimeSpan.FromMinutes(5));
        var controlVersion = draft.ControlVersion;

        draft.RenewControl(controller, now.AddMinutes(2), TimeSpan.FromMinutes(5));

        Assert.Equal(controlVersion, draft.ControlVersion);
    }

    [Theory]
    [InlineData(73, 5, 15, 14, 3)]
    [InlineData(12, 3, 4, 4, 0)]
    [InlineData(5, 2, 3, 2, 1)]
    public void DerivedDistributionCoversUnevenAndEqualTotals(int participants, int teams, int larger, int smaller, int largerTeamCount)
    {
        var distribution = DraftRosterDistribution.Derive(participants, teams);
        Assert.Equal((larger, smaller, largerTeamCount), (distribution.LargerSize, distribution.SmallerSize, distribution.LargerTeamCount));
    }

    [Fact]
    public void DerivedDistributionRejectsPreassignmentsThatCannotEndWithinOneSeat()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var distribution = DraftRosterDistribution.Derive(7, ids.Length);
        var blockers = distribution.ValidateCurrentRosters(new Dictionary<Guid, int> { [ids[0]] = 3, [ids[1]] = 3, [ids[2]] = 0 });
        Assert.Contains(blockers, blocker => blocker.Contains("maximum size difference", StringComparison.Ordinal));
    }

    [Fact]
    public void NamedLargerSeatsRespectForcedCaptainPreassignments()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var distribution = DraftRosterDistribution.Derive(8, ids.Length);
        var targets = distribution.AllocateNamedFinalSizes(ids, new Dictionary<Guid, int> { [ids[0]] = 3, [ids[1]] = 1, [ids[2]] = 1 });
        Assert.Equal(3, targets[ids[0]]);
        Assert.Equal(8, targets.Values.Sum());
        Assert.Equal(1, targets.Values.Max() - targets.Values.Min());
    }
}
