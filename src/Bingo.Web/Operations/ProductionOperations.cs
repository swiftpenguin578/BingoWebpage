using System.Collections.Concurrent;
using System.Text;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bingo.Web.Operations;

public sealed class WorkerHeartbeatRegistry(TimeProvider time)
{
    public const string EventLifecycleWorker = "event-lifecycle";
    public const string CompetitionSynchronizationWorker = "competition-synchronization";
    private readonly ConcurrentDictionary<string, long> beats = new(StringComparer.Ordinal);

    public void Beat(string workerName) => beats[workerName] = time.GetTimestamp();

    public bool IsFresh(string workerName, TimeSpan maximumAge) =>
        beats.TryGetValue(workerName, out var timestamp) && time.GetElapsedTime(timestamp) <= maximumAge;
}

public sealed class ProductionReadinessHealthCheck(
    R2EvidenceStorage storage,
    WorkerHeartbeatRegistry heartbeats) : IHealthCheck
{
    private static readonly TimeSpan HeartbeatMaximumAge = TimeSpan.FromSeconds(90);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await storage.CheckAvailabilityAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return HealthCheckResult.Unhealthy();
        }

        return heartbeats.IsFresh(WorkerHeartbeatRegistry.EventLifecycleWorker, HeartbeatMaximumAge) &&
               heartbeats.IsFresh(WorkerHeartbeatRegistry.CompetitionSynchronizationWorker, HeartbeatMaximumAge)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}

public sealed class ProductionPreflight(
    ApplicationDbContext db,
    IConfiguration configuration,
    IServiceProvider services,
    CatalogueSnapshotService catalogue)
{
    public const string KeyRingSetting = "DataProtection:KeyRingPath";

    public static string RequireAbsoluteKeyRingPath(IConfiguration configuration)
    {
        var path = configuration[KeyRingSetting];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            throw new InvalidOperationException("Production data protection failed [configuration]: set DataProtection:KeyRingPath to an absolute path mounted as a writable volume.");
        return path;
    }

    public static void ValidateDataProtection(IServiceProvider services, IConfiguration configuration)
    {
        var path = RequireAbsoluteKeyRingPath(configuration);
        try
        {
            Directory.CreateDirectory(path);
            var probePath = Path.Combine(path, $".bingo-write-check-{Guid.NewGuid():N}");
            try
            {
                File.WriteAllText(probePath, "ok", Encoding.UTF8);
            }
            finally
            {
                if (File.Exists(probePath)) File.Delete(probePath);
            }

            var provider = services.GetRequiredService<IDataProtectionProvider>();
            var protector = provider.CreateProtector("Bingo.Production.StartupValidation");
            var protectedValue = protector.Protect("round-trip");
            if (protector.Unprotect(protectedValue) != "round-trip") throw new InvalidOperationException();
        }
        catch
        {
            throw new InvalidOperationException("Production data protection failed [storage]: ensure the configured absolute key-ring directory can be created and written by the web user, then rerun the command.");
        }
    }

    public async Task ValidateAsync(string catalogueSnapshotPath, CancellationToken cancellationToken)
    {
        await ValidateMigrationsAsync(cancellationToken);
        await ValidateR2Async(cancellationToken);
        try
        {
            await catalogue.ValidateBaselineAsync(catalogueSnapshotPath, cancellationToken);
        }
        catch
        {
            throw new InvalidOperationException("Production preflight failed [catalogue]: apply the reviewed catalogue snapshot on clean data, or correct the reported retained catalogue records, then rerun preflight.");
        }

        int ownerCount;
        try
        {
            ownerCount = await db.Accounts.CountAsync(account => account.Active && account.GlobalRole == Bingo.Domain.Access.GlobalRole.SuperAdmin, cancellationToken);
        }
        catch
        {
            throw new InvalidOperationException("Production preflight failed [ownership]: verify the database and provision exactly one active Super Admin, then rerun preflight.");
        }

        if (ownerCount != 1)
            throw new InvalidOperationException("Production preflight failed [ownership]: provision or recover exactly one active Super Admin, then rerun preflight.");
    }

    private async Task ValidateMigrationsAsync(CancellationToken cancellationToken)
    {
        IEnumerable<string> pending;
        try
        {
            pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        }
        catch
        {
            throw new InvalidOperationException("Production preflight failed [postgresql]: verify database connectivity and credentials, then rerun preflight.");
        }

        if (pending.Any())
            throw new InvalidOperationException("Production preflight failed [migrations]: run the explicit --migrate command, then rerun preflight.");
    }

    private async Task ValidateR2Async(CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(configuration["EvidenceStorage:Provider"], "R2", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            R2EvidenceStorage.ValidateConfiguration(configuration);
            await services.GetRequiredService<R2EvidenceStorage>().CheckAvailabilityAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            throw new InvalidOperationException("Production preflight failed [R2]: configure the required R2 settings and ensure the bucket is reachable, then rerun preflight.");
        }
    }
}
