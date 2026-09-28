using Bingo.Application.Auditing;
using Bingo.Domain.Access;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Bingo.Web.Security;

/// <summary>Legacy provisioning commands cannot create or modify retained emergency identities.</summary>
public sealed class CaptainAccountProvisioner
{
    public CaptainAccountProvisioner(ApplicationDbContext db, IPasswordHasher<Account> passwordHasher, IAuditWriter auditWriter, TimeProvider time) { }
    public Task<IReadOnlyList<GeneratedCaptainCredential>> ProvisionEventAsync(Guid eventId, Guid actorId, string actorName, CancellationToken ct) =>
        throw new InvalidOperationException("This account is not available.");
    public Task<GeneratedCaptainCredential?> ProvisionParticipantAsync(Guid eventId, Guid teamId, Guid participantId, Guid actorId, string actorName, CancellationToken ct) =>
        throw new InvalidOperationException("This account is not available.");
    public Task DisableParticipantAsync(Guid participantId, Guid actorId, string actorName, string reason, CancellationToken ct) =>
        throw new InvalidOperationException("This account is not available.");
}

public sealed record GeneratedCaptainCredential(string PlayerName, string TeamName, TeamMembershipRole Role, string Username, string SetupToken)
{
    public string Password => SetupToken;
}
