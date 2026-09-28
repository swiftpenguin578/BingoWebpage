using System.Data;
using System.Data.Common;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Signups;

// Acquire before beginning the transaction: a waiting Serializable transaction
// must not retain a snapshot taken before the preceding switch committed.
internal static class ParticipantAttributionLock
{
    public static async Task<IAsyncDisposable> AcquireAsync(ApplicationDbContext db, Guid participantId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Attribution locking must precede the transaction.");
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await connection.OpenAsync(cancellationToken);
        try
        {
            await ExecuteAsync(connection, participantId, "pg_advisory_lock", cancellationToken);
            return new Lease(connection, participantId, closeAfter);
        }
        catch
        {
            if (closeAfter) await connection.CloseAsync();
            throw;
        }
    }

    public static DateTimeOffset AtDatabasePrecision(DateTimeOffset instant)
    {
        var utc = instant.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }

    private static async Task ExecuteAsync(DbConnection connection, Guid participantId, string function, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {function}(7303005, hashtext(@participant_id::text))";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "participant_id";
        parameter.Value = participantId;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed class Lease(DbConnection connection, Guid participantId, bool closeAfter) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await ExecuteAsync(connection, participantId, "pg_advisory_unlock", CancellationToken.None); }
            finally { if (closeAfter) await connection.CloseAsync(); }
        }
    }
}
