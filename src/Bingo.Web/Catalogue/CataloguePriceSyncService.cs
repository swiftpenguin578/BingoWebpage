using System.Data;
using System.Text.Json;
using Bingo.Application.Catalogue;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Catalogue;

/// <summary>Explicit operator refresh only. Reporting never mutates the catalogue.</summary>
public sealed class CataloguePriceSyncService(ApplicationDbContext db, ICatalogueApiClient api, TimeProvider time)
{
    public sealed record ItemReport(Guid Id, string Name, string? ItemId, long? ValueGp, string Source, string Status);
    public sealed record Report(bool Applied, IReadOnlyList<ItemReport> Items, IReadOnlyList<string> UnresolvedSources, string? Error);

    public async Task<Report> RunAsync(bool apply, Guid? actorId, CancellationToken ct)
    {
        var mappings = await api.GetItemsAsync(ct);
        var prices = await api.GetHourlyPricesAsync(ct);
        var metrics = await api.GetBossMetricsAsync(ct);
        if (!mappings.Available || !prices.Available || !metrics.Available)
            return new(false, [], [], "Provider data unavailable; nothing changed. Retry the report later.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        Account? actor = null;
        if (apply)
        {
            actor = await db.Accounts.SingleOrDefaultAsync(x => x.Id == actorId && x.Active && x.GlobalRole == GlobalRole.SuperAdmin, ct);
            if (actor is null) throw new InvalidOperationException("An active Super Admin actor is required to apply catalogue prices.");
            // Include actor authorization in the serializable/concurrency boundary.
            db.Entry(actor).Property(x => x.Version).IsModified = true;
        }
        var items = await db.CatalogueItems.OrderBy(x => x.Name).ToListAsync(ct);
        var bosses = await db.BossActivities.ToListAsync(ct);
        var reports = new List<ItemReport>(); var unresolvedSources = new List<string>();
        var now = time.GetUtcNow();
        var audits = new List<(string Action, string Type, Guid Id, string Name, string Before, Func<string> After)>();
        foreach (var item in items)
        {
            var before = JsonSerializer.Serialize(item);
            var candidates = mappings.Data!.Where(x => item.ExternalIdentifier is { } id
                ? int.TryParse(id, out var numericId) && x.Id == numericId
                : string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            var status = candidates.Length == 1 ? "Verified" : "Needs mapping or explicit untradeable classification";
            var proposedId = item.ExternalIdentifier; var value = item.CatalogueValueGp; var source = item.PriceSource;
            if (candidates.Length == 1)
            {
                var match = candidates[0]; proposedId = match.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (source is not (CataloguePriceSource.Manual or CataloguePriceSource.Untradeable))
                {
                    if (prices.Data!.Values.TryGetValue(match.Id, out var fetched) && fetched is not null)
                    {
                        if (CataloguePricing.IsSuspiciousMove(value, fetched.Value))
                            status = $"Rejected unusual API candidate {fetched.Value} GP; trusted value retained. Review or enter a checked manual value.";
                        else { value = fetched; source = CataloguePriceSource.Api; }
                    }
                    else status = value is null ? "Missing price; catalogue value required" : "No trades this hour; stored value retained";
                }
                if (apply)
                {
                    item.ConfigureApi(proposedId);
                    item.RecordMapping(ApiMappingStatus.Verified, now, match.Name, match.Icon);
                    item.ApplyApiPrice(prices.Data!.Values.GetValueOrDefault(match.Id), prices.Data.Hour);
                }
            }
            else if (source == CataloguePriceSource.Untradeable) status = "Explicit untradeable classification retained";
            else if (apply && item.ExternalIdentifier is not null) item.RecordMapping(ApiMappingStatus.Unsupported, now);
            reports.Add(new(item.Id, item.Name, proposedId, value, source.ToString(), status));
            if (apply && JsonSerializer.Serialize(item) != before)
                audits.Add(("catalogue.item_api_refreshed", "catalogue_item", item.Id, item.Name, before, () => JsonSerializer.Serialize(item)));
        }
        foreach (var boss in bosses)
        {
            var candidate = boss.ExternalIdentifier;
            if (candidate is null)
            {
                var matches = metrics.Data!.Where(x => string.Equals(x.Replace('_', ' '), boss.Name.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
                candidate = matches.Length == 1 ? matches[0] : null;
            }
            var supported = candidate is not null && metrics.Data!.Contains(candidate);
            if (!supported) unresolvedSources.Add(boss.Name);
            if (!apply) continue;
            var before = JsonSerializer.Serialize(boss);
            if (supported) boss.ConfigureApi(candidate);
            boss.RecordMapping(supported ? ApiMappingStatus.Verified : ApiMappingStatus.Unsupported, now);
            if (JsonSerializer.Serialize(boss) != before)
                audits.Add(("catalogue.boss_api_verified", "boss_activity", boss.Id, boss.Name, before, () => JsonSerializer.Serialize(boss)));
        }
        if (apply)
        {
            await db.SaveChangesAsync(ct);
            foreach (var audit in audits)
                db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor!.Id, actor.LoginName, audit.Action, audit.Type,
                    audit.Id.ToString(), audit.Name, beforeState: audit.Before, afterState: audit.After()));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else await transaction.RollbackAsync(ct);
        return new(apply, reports, unresolvedSources, null);
    }
}
