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
    public TeamMembershipSource Source { get; private set; } = TeamMembershipSource.RetainedConversion;
    public Guid? ReplacesMembershipId { get; private set; }
    public long Version { get; private set; } = 1;
    public string? AssignmentReason { get; private set; }
    public void ChangeRole(TeamMembershipRole role) => Role = role;
    public void Leave(DateTimeOffset now, string reason) { LeftAt = now.ToUniversalTime(); AssignmentReason = reason; }
    public void SetSource(TeamMembershipSource source, Guid? replacesMembershipId = null) { Source = source; ReplacesMembershipId = replacesMembershipId; }
    public void AdvanceVersion() => Version++;
}
