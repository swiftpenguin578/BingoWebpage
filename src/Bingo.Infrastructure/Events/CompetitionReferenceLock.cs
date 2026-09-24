using System.Data;
using System.Data.Common;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bingo.Infrastructure.Events;

/// <summary>
/// Serializes managed provider writes with local manual-link changes for one WOM
/// competition. The lock is session scoped so it can cover the provider call
/// and the receipt transaction together.
/// </summary>
internal static class CompetitionReferenceLock
{
    public static Task<IAsyncDisposable> AcquireAsync(
        ApplicationDbContext db,
        long competitionId,
        CancellationToken cancellationToken) =>
        AcquireManyAsync(db, [competitionId], cancellationToken);

    public static async Task<IAsyncDisposable> AcquireManyAsync(
        ApplicationDbContext db,
        IEnumerable<long> competitionIds,
        CancellationToken cancellationToken)
    {
        var ids = competitionIds.Distinct().OrderBy(id => id).ToArray();
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await connection.OpenAsync(cancellationToken);
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction();

        var locked = new List<long>(ids.Length);
        try
        {
            foreach (var id in ids)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_lock(@competition_id)";
                command.Transaction = transaction;
                var parameter = command.CreateParameter();
                parameter.ParameterName = "competition_id";
                parameter.Value = id;
                command.Parameters.Add(parameter);
                await command.ExecuteNonQueryAsync(cancellationToken);
                locked.Add(id);
            }

            return new Lease(connection, locked, closeAfter, transaction);
        }
        catch
        {
            foreach (var id in locked.AsEnumerable().Reverse())
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(@competition_id)";
                command.Transaction = transaction;
                var parameter = command.CreateParameter();
                parameter.ParameterName = "competition_id";
                parameter.Value = id;
                command.Parameters.Add(parameter);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            if (closeAfter) await connection.CloseAsync();
            throw;
        }
    }

    private sealed class Lease(DbConnection connection, IReadOnlyList<long> ids, bool closeAfter, DbTransaction? transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                foreach (var id in ids.Reverse())
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT pg_advisory_unlock(@competition_id)";
                    command.Transaction = transaction;
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "competition_id";
                    parameter.Value = id;
                    command.Parameters.Add(parameter);
                    await command.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                if (closeAfter) await connection.CloseAsync();
            }
        }
    }
}
