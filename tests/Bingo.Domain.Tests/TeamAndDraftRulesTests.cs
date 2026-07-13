using Bingo.Domain.Teams;

namespace Bingo.Domain.Tests;

public sealed class TeamAndDraftRulesTests
{
    [Fact]
    public void PreformedTeamCannotReceiveDraftTurns() => Assert.Throws<InvalidOperationException>(() => new Team(Guid.NewGuid(), Guid.NewGuid(), "External", "external", TeamFormationType.Preformed, "Clan", true));

    [Fact]
    public void ConfigurationLocksWhenDraftStarts()
    {
        var draft = new DraftSession(Guid.NewGuid(), Guid.NewGuid(), 14);
        draft.Start(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => draft.ConfigureTargetSize(15));
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
}
