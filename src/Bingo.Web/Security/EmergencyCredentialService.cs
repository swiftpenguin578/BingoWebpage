using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;

namespace Bingo.Web.Security;

/// <summary>Fail-closed compatibility boundary for the retired credential command.</summary>
public sealed class EmergencyCredentialService
{
    public EmergencyCredentialService(ApplicationDbContext db, TimeProvider time) { }
    public Task<Account> CreateAsync(Guid actorId, string username, Guid eventId, Guid teamId, CancellationToken ct) =>
        throw new InvalidOperationException("This account is not available.");
}
