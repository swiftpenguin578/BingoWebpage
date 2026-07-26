using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Events;

/// <summary>Disables emergency credentials whenever their authoritative submission window closes.</summary>
public sealed class EmergencyCredentialLifecycleService(ApplicationDbContext db, TimeProvider time)
{
    public async Task ApplyAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var accesses = await db.AccountEventAccesses
            .FromSqlInterpolated($"SELECT a.* FROM account_event_accesses a JOIN events e ON e.id = a.\"EventId\" WHERE a.\"Enabled\" AND CASE WHEN e.reopened_submission_cutoff_at > e.submission_cutoff_at THEN e.reopened_submission_cutoff_at ELSE e.submission_cutoff_at END <= {now} FOR UPDATE")
            .ToListAsync(ct);

        foreach (var access in accesses)
        {
            var account = await db.Accounts.SingleAsync(value => value.Id == access.AccountId, ct);
            access.DisableAtCutoff();
            account.Disable(now, null, "Submission cutoff reached");
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, null, "System", "account.emergency_cutoff_disabled", "account", account.Id.ToString(), "Emergency credential disabled at submission cutoff.", access.EventId));
        }

        if (accesses.Count > 0) await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
