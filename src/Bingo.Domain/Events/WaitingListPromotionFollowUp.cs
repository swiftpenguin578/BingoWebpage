namespace Bingo.Domain.Events;

/// <summary>Purpose-specific unresolved Admin follow-up created by a waiting-list replacement.</summary>
public sealed class WaitingListPromotionFollowUp
{
    private WaitingListPromotionFollowUp() { }

    public WaitingListPromotionFollowUp(
        Guid id,
        Guid eventId,
        Guid endedMembershipId,
        Guid replacementMembershipId,
        Guid promotedParticipantId,
        DateTimeOffset createdAt)
    {
        Id = id;
        EventId = eventId;
        EndedMembershipId = endedMembershipId;
        ReplacementMembershipId = replacementMembershipId;
        PromotedParticipantId = promotedParticipantId;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid EndedMembershipId { get; private set; }
    public Guid ReplacementMembershipId { get; private set; }
    public Guid PromotedParticipantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CompletedByAccountId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void MarkComplete(Guid actorAccountId, DateTimeOffset completedAt)
    {
        if (CompletedAt is not null) return;
        CompletedByAccountId = actorAccountId;
        CompletedAt = completedAt.ToUniversalTime();
    }
}
