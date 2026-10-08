using System.Data;
using Bingo.Application.Stats;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Stats;

public sealed class LuckCheckpointConversionService(ApplicationDbContext db, TimeProvider clock) : ILuckCheckpointConversionService
{
    public async Task<LuckCheckpointConversionReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var eventIds = await db.EventStatsLuckCheckpoints.AsNoTracking()
            .Where(x => x.SchemaVersion == 1 || x.ConvertedFromSchemaVersion == 1)
            .OrderBy(x => x.EventId)
            .Select(x => x.EventId)
            .ToListAsync(cancellationToken);
        var results = new List<LuckCheckpointConversionEvent>(eventIds.Count);
        foreach (var eventId in eventIds)
            results.Add(await ConvertOneAsync(eventId, cancellationToken));
        return new(results);
    }

    private async Task<LuckCheckpointConversionEvent> ConvertOneAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var current = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
            if (current is null)
                return await CouldNotAsync(eventId, transaction, "checkpoint-row-not-found");

            if (current.SchemaVersion != 1)
            {
                var outcome = current.SchemaVersion == EventStatsLuckCheckpoint.CurrentSchemaVersion && current.ConvertedFromSchemaVersion == 1
                    ? LuckCheckpointConversionOutcome.AlreadyConverted
                    : LuckCheckpointConversionOutcome.CouldNotConvert;
                var reason = outcome == LuckCheckpointConversionOutcome.CouldNotConvert
                    ? $"unsupported-schema-version:{current.SchemaVersion}"
                    : null;
                await transaction.CommitAsync(cancellationToken);
                return new(eventId, outcome, reason);
            }

            var conversion = await PublicStatsService.ConvertLegacyCheckpointWithDiagnosticAsync(db, clock, eventId, cancellationToken);
            if (conversion.Converted)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(eventId, LuckCheckpointConversionOutcome.Converted);
            }

            // The conversion helper only writes after the complete retained payload has
            // validated. Re-read under the event lock so a concurrent idempotent attempt is
            // reported as Already converted rather than masking a successful conversion.
            current = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
            var alreadyConverted = current?.SchemaVersion == EventStatsLuckCheckpoint.CurrentSchemaVersion && current.ConvertedFromSchemaVersion == 1;
            await transaction.CommitAsync(cancellationToken);
            return alreadyConverted
                ? new(eventId, LuckCheckpointConversionOutcome.AlreadyConverted)
                : new(eventId, LuckCheckpointConversionOutcome.CouldNotConvert, conversion.Diagnostic ?? "conversion-failed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new(eventId, LuckCheckpointConversionOutcome.CouldNotConvert, BoundedReason("operation-failed", exception.Message));
        }
    }

    private static async Task<LuckCheckpointConversionEvent> CouldNotAsync(
        Guid eventId,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        string reason)
    {
        await transaction.CommitAsync(CancellationToken.None);
        return new(eventId, LuckCheckpointConversionOutcome.CouldNotConvert, reason);
    }

    private static string BoundedReason(string code, string detail)
    {
        var value = code + ": " + detail.Replace('\n', ' ').Replace('\r', ' ');
        return value.Length <= 240 ? value : value[..240];
    }
}
