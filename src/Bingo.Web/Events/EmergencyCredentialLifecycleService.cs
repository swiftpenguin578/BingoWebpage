using Bingo.Infrastructure.Persistence;

namespace Bingo.Web.Events;

/// <summary>Retained compatibility type; retirement no longer rewrites historical credential state.</summary>
public sealed class EmergencyCredentialLifecycleService
{
    public EmergencyCredentialLifecycleService(ApplicationDbContext db, TimeProvider time) { }
    public Task ApplyAsync(CancellationToken ct) => Task.CompletedTask;
}
