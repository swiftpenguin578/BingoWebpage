using Bingo.Domain.Announcements;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;

namespace Bingo.Domain.Tests;

public sealed class DropAnnouncementRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EventStartsTrackingAtCreationAndFinalizationMovesToASeparateGeneration()
    {
        var item = new BingoEvent(Guid.NewGuid(), "Event", "event", "UTC", Guid.NewGuid(), Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);

        Assert.Equal(Now, item.AnnouncementsTrackingStartedAt);
        Assert.Equal(1, item.AnnouncementGeneration);

        item.ClearAnnouncements();

        Assert.Equal(2, item.AnnouncementGeneration);
    }

    [Fact]
    public void ApprovalStoresImmutableAnnouncementIdentityAndCompletionFact()
    {
        var submission = new Submission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), Guid.NewGuid(), "Player", Guid.NewGuid(), 1, Now, null, null);

        submission.Approve(1, Now, 7, true);

        Assert.Equal(7, submission.AnnouncementGeneration);
        Assert.True(submission.CompletedTileAtApproval);
    }

    [Fact]
    public void AcknowledgementsKeepBannerAndDropsStateIndependentAndIdempotent()
    {
        var row = new DropAnnouncementAcknowledgement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        row.AcknowledgeBanner(Now);
        row.AcknowledgeBanner(Now.AddMinutes(1));
        row.AcknowledgeDrops(Now.AddMinutes(2));

        Assert.Equal(Now, row.BannerAcknowledgedAt);
        Assert.Equal(Now.AddMinutes(2), row.DropsAcknowledgedAt);
    }
}
