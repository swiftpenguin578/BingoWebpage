using Bingo.Domain.Events;
using Bingo.Domain.Evidence;

namespace Bingo.Domain.Tests;

public sealed class EvidenceRulesTests
{
    [Fact]
    public void SubmissionTimestampAndExpectedCodeAreImmutableSnapshots()
    {
        var submittedAt = new DateTimeOffset(2026, 7, 13, 20, 15, 0, TimeSpan.FromHours(2));
        var submission = CreateSubmission(submittedAt, " funny-code ");

        submission.EditPending(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 2, "corrected");

        Assert.Equal(submittedAt.ToUniversalTime(), submission.SubmittedAt);
        Assert.Equal("funny-code", submission.ExpectedEvidenceCode);
        Assert.Equal(SubmissionStatus.Pending, submission.Status);
    }

    [Fact]
    public void WithdrawKeepsHistoryButCannotBeApproved()
    {
        var submission = CreateSubmission(DateTimeOffset.UtcNow, null);
        submission.Withdraw(DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(SubmissionStatus.Withdrawn, submission.Status);
        Assert.Throws<InvalidOperationException>(() => submission.Approve(1, DateTimeOffset.UtcNow.AddMinutes(2)));
    }

    [Fact]
    public void HidingApprovedEvidenceAlsoHidesPlayer()
    {
        var submission = CreateSubmission(DateTimeOffset.UtcNow, null);
        submission.Approve(1, DateTimeOffset.UtcNow.AddMinutes(1));

        submission.SetPublicEvidenceHidden(true);

        Assert.True(submission.PublicEvidenceHidden);
        Assert.True(submission.PublicPlayerHidden);
    }

    [Fact]
    public void RequestedChangesRequireANoteAndCanBeResubmitted()
    {
        var submission = CreateSubmission(DateTimeOffset.UtcNow, null);
        Assert.Throws<ArgumentException>(() => submission.RequestChanges(" ", DateTimeOffset.UtcNow));

        submission.RequestChanges("Show the full game message", DateTimeOffset.UtcNow);
        submission.Resubmit();

        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        Assert.Null(submission.CurrentReviewerNote);
    }

    [Fact]
    public void AwaitingFinalReviewStillAcceptsSubmissionsUntilCutoff()
    {
        var starts = new DateTimeOffset(2026, 7, 13, 18, 0, 0, TimeSpan.Zero);
        var ends = starts.AddHours(5);
        var ev = CreateEvent(starts, ends, ends.AddMinutes(30));
        ev.OpenSignups();
        ev.CloseSignups();
        ev.StartEvent(starts);
        ev.EndEvent();

        Assert.True(ev.AcceptsNewSubmissions(ends.AddMinutes(15)));
        Assert.False(ev.AcceptsNewSubmissions(ends.AddMinutes(31)));
    }

    [Fact]
    public void ReopeningSubmissionWindowDoesNotPutEndedEventBackIntoLiveState()
    {
        var starts = DateTimeOffset.UtcNow.AddHours(-2);
        var ends = starts.AddHours(1);
        var ev = CreateEvent(starts, ends, ends.AddMinutes(30));
        ev.OpenSignups();
        ev.CloseSignups();
        ev.StartEvent(starts);
        ev.EndEvent();

        ev.ReopenSubmissions(DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow);

        Assert.Equal(EventState.AwaitingFinalReview, ev.State);
        Assert.True(ev.AcceptsNewSubmissions(DateTimeOffset.UtcNow.AddMinutes(30)));
    }

    private static Submission CreateSubmission(DateTimeOffset at, string? code) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
        Guid.NewGuid(), Guid.NewGuid(), 1, at, null, code);

    private static BingoEvent CreateEvent(DateTimeOffset starts, DateTimeOffset ends, DateTimeOffset cutoff) => new(
        Guid.NewGuid(), "Evidence test", "evidence-test", "", "UTC", starts.AddDays(-10), starts.AddDays(-2),
        starts, ends, cutoff, 20, Guid.NewGuid(), starts.AddDays(-20));
}
