using Bingo.Domain.Teams;

namespace Bingo.Application.Teams;

public sealed record TeamCaptainRoleChange(Guid EventId, Guid MembershipId, TeamMembershipRole Role, Guid ActorAccountId, string ActorUsername);
public sealed record TeamCaptainRoleChangeResult(bool Succeeded, string? Error = null, string? ParticipantName = null);

/// <summary>Authoritative event/team captain boundary. Website ownership is explicit participant ownership only.</summary>
public interface ITeamCaptainAuthorityService
{
    Task<TeamCaptainRoleChangeResult> ChangeRoleAsync(TeamCaptainRoleChange change, CancellationToken ct = default);
    Task<bool> HasDraftSignupTableAccessAsync(Guid accountId, Guid eventId, CancellationToken ct = default);
    Task<bool> HasCurrentCaptainAuthorityAsync(Guid accountId, Guid eventId, Guid? teamId = null, CancellationToken ct = default);
}
