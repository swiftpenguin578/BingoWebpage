using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;

namespace Bingo.Domain.Tests;

public sealed class Slice9LiveWithdrawalDomainTests
{
    [Fact]
    public void LiveWithdrawalCanStoreWholeMinuteEligibilityWithoutChangingImmediateStatus()
    {
        var requestedAt = new DateTimeOffset(2026, 8, 2, 12, 34, 27, TimeSpan.Zero);
        var eligibilityEndsAt = new DateTimeOffset(2026, 8, 2, 12, 35, 0, TimeSpan.Zero);
        var participant = new EventParticipant(Guid.NewGuid(), Guid.NewGuid(), SignupStatus.Confirmed, 1, requestedAt, SignupSource.Website);

        participant.Withdraw(requestedAt, "Admin withdrawal", Guid.NewGuid(), eligibilityEndsAt);

        Assert.Equal(SignupStatus.Withdrawn, participant.SignupStatus);
        Assert.Equal(eligibilityEndsAt, participant.WithdrawnAt);
    }

    [Fact]
    public void ReplacementMembershipLinksOnlyToTheEndedMembership()
    {
        var now = DateTimeOffset.UtcNow;
        var ended = new TeamMembership(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TeamMembershipRole.Captain, now, Guid.NewGuid(), "Draft pick");
        ended.Leave(now, "Live Admin withdrawal");
        var replacement = new TeamMembership(Guid.NewGuid(), ended.TeamId, Guid.NewGuid(), TeamMembershipRole.Participant, now, null, "Live roster replacement");
        replacement.SetSource(TeamMembershipSource.Replacement, ended.Id);

        Assert.Equal(TeamMembershipSource.Replacement, replacement.Source);
        Assert.Equal(ended.Id, replacement.ReplacesMembershipId);
        Assert.Null(replacement.AssignedByDraftPickId);
        Assert.Null(replacement.LeftAt);
    }
}
