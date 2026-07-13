namespace Bingo.Domain.Teams;

public sealed class TeamMembership
{
    private TeamMembership() { }
    public TeamMembership(Guid id, Guid teamId, Guid participantId, TeamMembershipRole role, DateTimeOffset joinedAt, Guid? assignedByDraftPickId, string? assignmentReason)
    { Id = id; TeamId = teamId; EventParticipantId = participantId; Role = role; JoinedAt = joinedAt.ToUniversalTime(); AssignedByDraftPickId = assignedByDraftPickId; AssignmentReason = assignmentReason; }
    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid EventParticipantId { get; private set; }
    public TeamMembershipRole Role { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }
    public Guid? AssignedByDraftPickId { get; private set; }
    public string? AssignmentReason { get; private set; }
    public void ChangeRole(TeamMembershipRole role) => Role = role;
    public void Leave(DateTimeOffset now, string reason) { LeftAt = now.ToUniversalTime(); AssignmentReason = reason; }
}
