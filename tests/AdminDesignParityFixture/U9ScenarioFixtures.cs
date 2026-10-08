using Bingo.Application.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Events;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.AdminDesignParityFixture;

// RC09 / A15 / WA-9: controlled PostgreSQL scenarios, never a provider or shared review database.
internal static class U9ScenarioFixtures
{
    public static async Task SeedAsync(IServiceProvider services, IReadOnlyDictionary<string, Guid> ids, DateTimeOffset now, string variant)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        // The general UR names are deliberately longer than WOM's 12-character limit.
        // Use valid synthetic current names for this opt-in Create/management fixture only.
        var characterNumber = 0;
        foreach (var character in await db.OsrsCharacters.OrderBy(value => value.Id).ToListAsync())
        {
            var name = $"U9 Player {++characterNumber}";
            db.Entry(character).Property(value => value.DisplayName).CurrentValue = name;
            db.Entry(character).Property(value => value.NormalizedName).CurrentValue = name.ToUpperInvariant();
        }
        var protector = services.GetRequiredService<ICompetitionCredentialProtector>();
        foreach (var (slug, provenance, competitionId) in new[] {
            ("ur-signups-open", EventCompetitionProvenance.External, 92001L),
            ("ur-upcoming-01", EventCompetitionProvenance.WebsiteCreated, 92002L),
            ("ur-upcoming-04", EventCompetitionProvenance.Unknown, 92003L) })
        {
            var item = await db.Events.SingleAsync(value => value.Id == ids[slug]);
            var sync = new EventCompetitionSynchronization(Guid.NewGuid(), item.Id, 1, competitionId, "Controlled U9 competition", item.EventStartsAt, item.EventEndsAt, "u9-fixture", now, provenance);
            db.EventCompetitionSynchronizations.Add(sync);
            db.EventCompetitionManagements.Add(new EventCompetitionManagement(Guid.NewGuid(), item.Id, sync.Id, competitionId,
                "Controlled U9 competition", item.EventStartsAt!.Value, item.EventEndsAt!.Value,
                provenance == EventCompetitionProvenance.Unknown ? "" : protector.Protect("synthetic-u9-code"), "u9-fixture", now.AddHours(-2), provenance));
        }
        foreach (var (slug, unknown) in new[] { ("ur-upcoming-02", false), ("ur-upcoming-03", true) })
        {
            var item = await db.Events.SingleAsync(value => value.Id == ids[slug]);
            var operation = new EventCompetitionManagementOperation(Guid.NewGuid(), item.Id, null, EventCompetitionManagementOperationType.Create,
                "{}", "u9-fixture", item.Version, now);
            if (unknown) operation.MarkUnknown("UnknownOutcome", "Controlled response loss", now, now.AddMinutes(1));
            db.EventCompetitionManagementOperations.Add(operation);
        }
        var active = await db.Events.SingleAsync(value => value.Id == ids["ur-current"]);
        var current = await db.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == active.Id);
        // Reuse the production fingerprint for a controlled current generation (as the
        // existing dashboard fixture does); arbitrary fixture text would reset retry eligibility.
        var fingerprintMethod = typeof(EventCompetitionSynchronizationService).GetMethod("StatsAssignmentFingerprintAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var fingerprint = await (Task<string>)fingerprintMethod.Invoke(null, [db, active.Id, CancellationToken.None])!;
        current.BeginReplacementGeneration(fingerprint, now);
        current.MarkHistoricalSuccess(now.AddHours(-2), now.AddHours(-2));
        if (variant == "rejected") current.RejectEndUpdate(active.EventEndsAt!.Value, "SYNTHETIC_REJECTED");
        if (variant is "rate-limited" or "fetch-ready")
        {
            current.CompleteEndUpdate(active.EventEndsAt!.Value);
            current.MakeNormalRefreshDue(now);
            if (variant == "rate-limited") current.MarkFailure(now, "RateLimited", "Controlled rate limit", now.AddMinutes(5), active.ActualStartedAt);
        }
        await db.SaveChangesAsync();
    }
}
