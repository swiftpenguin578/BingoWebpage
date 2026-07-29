using Bingo.Domain.Events;

namespace Bingo.Domain.Tests;

public sealed class EventLifecycleFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> EveryTransition()
    {
        foreach (var from in Enum.GetValues<EventState>())
            foreach (var to in Enum.GetValues<EventState>())
                yield return [from, to, EventStatePolicy.CanTransition(from, to)];
    }

    [Theory]
    [MemberData(nameof(EveryTransition))]
    public void TransitionGraphMatchesTheApprovedContract(EventState from, EventState to, bool allowed)
    {
        Assert.Equal(allowed, EventStatePolicy.CanTransition(from, to));
    }

    [Fact]
    public void PrivateDraftCanPersistWithoutOptionalPlanningFields()
    {
        var item = new BingoEvent(Guid.NewGuid(), "Minimal", "minimal", "Europe/Copenhagen", Guid.NewGuid(), Now);

        Assert.Equal(EventState.Draft, item.State);
        Assert.Null(item.Description);
        Assert.Null(item.SignupOpensAt);
        Assert.Null(item.SignupClosesAt);
        Assert.Null(item.DraftAt);
        Assert.Null(item.EventStartsAt);
        Assert.Null(item.EventEndsAt);
        Assert.Null(item.SubmissionCutoffAt);
        Assert.Null(item.ParticipantCap);
        Assert.Null(item.FirstPublicAt);
    }

    [Fact]
    public void IdentityCanBeEditedWhilePrivateButThePublicSlugIsLockedAfterPublication()
    {
        var item = new BingoEvent(Guid.NewGuid(), "Original", "original", "Europe/Copenhagen", Guid.NewGuid(), Now);

        item.UpdateIdentity("Renamed", "renamed", "An optional description", "UTC");
        item.MarkFirstPublic(Now.AddHours(1));

        Assert.Equal("Renamed", item.Name);
        Assert.Equal("renamed", item.Slug);
        Assert.Equal("UTC", item.Timezone);
        Assert.Throws<InvalidOperationException>(() => item.UpdateIdentity("Renamed again", "another-link", "Updated description", "UTC"));

        item.UpdateIdentity("Renamed again", "renamed", "Updated description", "Europe/London");
        Assert.Equal("Renamed again", item.Name);
        Assert.Equal("renamed", item.Slug);
        Assert.Equal("Europe/London", item.Timezone);
    }

    [Fact]
    public void InitialScheduleRetainsOptionalInstantsInUtc()
    {
        var item = new BingoEvent(Guid.NewGuid(), "Minimal", "minimal", "Europe/Copenhagen", Guid.NewGuid(), Now);
        var localOffset = TimeSpan.FromHours(2);

        item.ConfigureInitialSchedule(new DateTimeOffset(2026, 8, 1, 10, 0, 0, localOffset), null, null, null, null, null);

        Assert.Equal(new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero), item.SignupOpensAt);
        Assert.Null(item.ParticipantCap);
    }

    [Fact]
    public void NormalSubmissionCutoffTracksTheCurrentEventEndAndIsAbsentWithoutOne()
    {
        var item = new BingoEvent(Guid.NewGuid(), "Minimal", "minimal", "Europe/Copenhagen", Guid.NewGuid(), Now);

        item.ConfigureInitialSchedule(null, null, null, Now.AddDays(1), Now.AddDays(2), null);
        Assert.Equal(Now.AddDays(2).AddMinutes(30), item.SubmissionCutoffAt);

        item.ConfigureInitialSchedule(null, null, null, Now.AddDays(3), Now.AddDays(4), null);
        Assert.Equal(Now.AddDays(4).AddMinutes(30), item.SubmissionCutoffAt);

        item.ConfigureInitialSchedule(null, null, null, Now.AddDays(3), null, null);
        Assert.Null(item.SubmissionCutoffAt);
    }

    [Fact]
    public void ScheduleKeepsLifecycleHistoryAndRejectsInvalidWindows()
    {
        var item = new BingoEvent(Guid.NewGuid(), "Minimal", "minimal", "Europe/Copenhagen", Guid.NewGuid(), Now);
        Assert.Throws<InvalidOperationException>(() => item.ConfigureSchedule(null, null, null, Now.AddDays(2), Now.AddDays(1), 10));
        Assert.Throws<InvalidOperationException>(() => item.ConfigureSchedule(null, Now.AddDays(3), null, Now.AddDays(2), Now.AddDays(4), 10));

        item.ConfigureSchedule(Now.AddHours(1), Now.AddDays(1), Now.AddHours(2), Now.AddDays(2), Now.AddDays(4), 10);
        item.OpenSignups(Now);
        item.ConfigureSchedule(Now.AddHours(3), Now.AddDays(1), Now.AddHours(4), Now.AddDays(2), Now.AddDays(5), 10);

        Assert.Equal(Now, item.ActualSignupOpenedAt);
        Assert.Equal(Now.AddDays(5).AddMinutes(30), item.SubmissionCutoffAt);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 9)]
    public void PlanningRejectsBoardDimensionsOutsideTheApprovedRange(int rows, int columns)
    {
        var item = new BingoEvent(Guid.NewGuid(), "Minimal", "minimal", "Europe/Copenhagen", Guid.NewGuid(), Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => item.ConfigurePlanning(null, null, null, null, null, rows, columns));
    }

    [Fact]
    public void AllNineStatesExposeExactlyTheirApprovedCapabilities()
    {
        foreach (var state in Enum.GetValues<EventState>())
            foreach (var capability in Enum.GetValues<EventCapability>())
            {
                var expected = capability switch
                {
                    EventCapability.ConfigureIdentityOrSchedule or EventCapability.ConfigureSignup or EventCapability.CancelOrDiscard => state is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed,
                    EventCapability.ParticipantSignup => state == EventState.SignupOpen,
                    EventCapability.ReopenSignup or EventCapability.StartEvent => state == EventState.SignupClosed,
                    EventCapability.LiveSubmission => state == EventState.Live,
                    EventCapability.ReviewEvidence => state is EventState.Live or EventState.AwaitingFinalReview,
                    EventCapability.ConfigureEvidenceCodes => state is EventState.Draft or EventState.SignupClosed or EventState.Live,
                    EventCapability.Finalize => state == EventState.AwaitingFinalReview,
                    EventCapability.Archive => state == EventState.Finalized,
                    EventCapability.Unfinalize => state is EventState.Finalized or EventState.Archived,
                    _ => false
                };
                Assert.Equal(expected, EventStatePolicy.Allows(state, capability));
            }
    }

    [Fact]
    public void StateForbiddenFlagsCannotReenableSignupsOrSubmissions()
    {
        var item = Event();
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(Now);
        item.CloseSignups();
        item.StartEvent(Now.AddDays(2));
        item.EndEvent(Now.AddDays(3));

        Assert.False(item.AcceptsSignups(Now.AddDays(3)));
        Assert.True(item.AcceptsNewSubmissions(Now.AddDays(3)));
        item.FinalizeResults(Now.AddDays(4));
        Assert.False(item.AcceptsNewSubmissions(Now.AddDays(4)));
    }

    [Fact]
    public void CancelledAndDiscardedAreTerminalAndLiveCannotEnterEither()
    {
        var cancelled = Event();
        cancelled.OpenSignups(Now);
        cancelled.Cancel(Guid.NewGuid(), Now, "Cannot proceed", protectedHistoryExists: true);
        Assert.Equal(EventState.Cancelled, cancelled.State);
        Assert.Throws<InvalidOperationException>(() => cancelled.OpenSignups());
        Assert.Throws<InvalidOperationException>(() => cancelled.Discard(Guid.NewGuid(), Now, false));

        var discarded = Event();
        discarded.Discard(Guid.NewGuid(), Now, protectedHistoryExists: false);
        Assert.Equal(EventState.Discarded, discarded.State);
        Assert.Throws<InvalidOperationException>(() => discarded.Cancel(Guid.NewGuid(), Now, "No", true));

        var live = Event();
        live.OpenSignups(Now);
        live.CloseSignups();
        live.StartEvent(Now);
        Assert.Throws<InvalidOperationException>(() => live.Cancel(Guid.NewGuid(), Now, "No", true));
        Assert.Throws<InvalidOperationException>(() => live.Discard(Guid.NewGuid(), Now, false));
    }

    [Fact]
    public void ScheduledAttemptNormalizesStableBlockerCodesAndResolvesOnce()
    {
        var attempt = new ScheduledEventStartAttempt(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddMinutes(1), false, [" board_unpublished ", "DRAFT_NOT_FINALIZED", "board_unpublished"]);

        Assert.Equal("BOARD_UNPUBLISHED,DRAFT_NOT_FINALIZED", attempt.BlockerCodes);
        attempt.Resolve(Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(2), attempt.ResolvedAt);
        Assert.Throws<InvalidOperationException>(() => attempt.Resolve(Now.AddMinutes(3)));
        Assert.Throws<ArgumentException>(() => new ScheduledEventStartAttempt(Guid.NewGuid(), Guid.NewGuid(), Now, Now, true, ["BOARD_UNPUBLISHED"]));
    }

    private static BingoEvent Event() => new(Guid.NewGuid(), "Test", $"test-{Guid.NewGuid():N}", "Test", "UTC", Now, Now.AddDays(1), Now.AddDays(2), Now.AddDays(3), Now.AddDays(4), 20, Guid.NewGuid(), Now);
}
