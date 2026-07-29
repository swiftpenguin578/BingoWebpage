namespace Bingo.Domain.Teams;

public sealed class TeamMembershipRoleTransition
{
    private TeamMembershipRoleTransition() { }
    public TeamMembershipRoleTransition(Guid id, Guid membershipId, TeamMembershipRole fromRole, TeamMembershipRole toRole, Guid? changedByAccountId, DateTimeOffset changedAt)
    { Id = id; TeamMembershipId = membershipId; FromRole = fromRole; ToRole = toRole; ChangedByAccountId = changedByAccountId; ChangedAt = changedAt.ToUniversalTime(); }
    public Guid Id { get; private set; }
    public Guid TeamMembershipId { get; private set; }
    public TeamMembershipRole FromRole { get; private set; }
    public TeamMembershipRole ToRole { get; private set; }
    public Guid? ChangedByAccountId { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
}
