using Bingo.Application.Catalogue;
using Bingo.Domain.Catalogue;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Catalogue;

public sealed partial class IndexModel
{
    public IReadOnlyDictionary<Guid, CatalogueItem> ApiItems { get; private set; } = new Dictionary<Guid, CatalogueItem>();
    public IReadOnlyDictionary<Guid, BossActivity> ApiBosses { get; private set; } = new Dictionary<Guid, BossActivity>();
    public static string MappingLabel(ApiMappingStatus status) => status switch
    {
        ApiMappingStatus.Verified => "Verified",
        ApiMappingStatus.Unsupported => "Unsupported",
        ApiMappingStatus.TemporarilyUnavailable => "Temporarily unavailable",
        _ => "Not configured"
    };

    private static bool RateLimited(string? error) => error == "The price API is temporarily limiting requests. Retry validation later.";

    public async Task<IActionResult> OnPostSuggestItemApiAsync(string? itemName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(itemName) || itemName.Length > 200) return BadRequest();
        // T2: "reason" lets the page show the reference's outcome text (unavailable / rate-limited / no-match).
        if (catalogueApi is null) return new JsonResult(new { error = Localize("The API is temporarily unavailable."), reason = "unavailable" });
        var result = await catalogueApi.GetItemsAsync(ct);
        if (!result.Available) return new JsonResult(new { error = Localize(ProviderFailure(result.Error, "The API is temporarily unavailable.")), reason = RateLimited(result.Error) ? "rate-limited" : "unavailable" });
        var matches = result.Data!.Where(x => string.Equals(x.Name, itemName.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return new JsonResult(matches.Length == 1
            ? new { id = (int?)matches[0].Id, name = (string?)matches[0].Name, error = (string?)null, reason = (string?)null }
            : new { id = (int?)null, name = (string?)null, error = (string?)Localize("No unique exact-name match. Enter the exact item ID, or classify an untradeable explicitly. No value was changed."), reason = (string?)"no-match" });
    }

    public async Task<IActionResult> OnPostSuggestBossApiAsync(string? itemName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(itemName) || itemName.Length > 200) return BadRequest();
        var metrics = catalogueApi is null ? new CatalogueApiResult<IReadOnlySet<string>>(null) : await catalogueApi.GetBossMetricsAsync(ct);
        if (!metrics.Available) return new JsonResult(new { error = Localize("The API is temporarily unavailable."), reason = RateLimited(metrics.Error) ? "rate-limited" : "unavailable" });
        var matches = metrics.Data!.Where(x => string.Equals(x.Replace('_', ' '), itemName.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return new JsonResult(matches.Length == 1
            ? new { id = (string?)matches[0], name = (string?)matches[0], error = (string?)null, reason = (string?)null }
            : new { id = (string?)null, name = (string?)null, error = (string?)Localize("No exact activity match. Enter the metric for the correct activity or raid mode and validate it."), reason = (string?)"no-match" });
    }

    public async Task<IActionResult> OnPostItemApiAsync(Guid recordId, Guid expectedItemId, long expectedItemVersion, string? externalIdentifier,
        string priceMode, long? manualValue, string operation, CancellationToken ct)
    {
        DropId = recordId;
        var drop = await dbContext.SourceDrops.SingleOrDefaultAsync(x => x.Id == recordId, ct);
        if (drop is null) return NotFound();
        BossId = drop.BossActivityId;
        var item = await dbContext.CatalogueItems.SingleAsync(x => x.Id == drop.ItemId, ct);
        if (item.Id != expectedItemId || item.Version != expectedItemVersion) return Stale();
        dbContext.Entry(drop).Property(x => x.Version).IsModified = true;
        externalIdentifier = Clean(externalIdentifier);
        if (externalIdentifier is not null && (!int.TryParse(externalIdentifier, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsedId) || parsedId <= 0)
            || manualValue is < 0 || priceMode is not ("Api" or "Manual" or "Untradeable") || operation is not ("save" or "validate")
            || (priceMode == "Manual" && manualValue is null) || (ModelState.TryGetValue(nameof(manualValue), out var manualState) && manualState.Errors.Count > 0)) return BadRequest();
        var before = State(item);
        var previousExternalIdentifier = item.ExternalIdentifier;
        var previousValue = item.CatalogueValueGp;
        var previousPriceSource = item.PriceSource;
        item.ConfigureApi(externalIdentifier);
        externalIdentifier = item.ExternalIdentifier;
        dbContext.Entry(item).Property(x => x.Version).IsModified = true;
        if (priceMode == "Manual") item.SetPrice(manualValue, CataloguePriceSource.Manual, timeProvider.GetUtcNow());
        else if (priceMode == "Untradeable") item.SetPrice(0, CataloguePriceSource.Untradeable, timeProvider.GetUtcNow());
        var messageType = UiMessageType.Success;
        var message = "API settings saved.";
        var missingHourlyPrice = false;
        // T2: structured result so the page shows the reference's outcome text.
        string result = operation == "validate" && externalIdentifier is not null ? "verified-kept" : "saved";
        var rateLimited = false;
        if (operation == "validate" && externalIdentifier is not null)
        {
            var mapping = catalogueApi is null ? new CatalogueApiResult<IReadOnlyList<ApiItem>>(null) : await catalogueApi.GetItemsAsync(ct);
            var match = mapping.Data?.SingleOrDefault(x => x.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) == externalIdentifier);
            rateLimited = RateLimited(mapping.Error);
            result = !mapping.Available ? "unavailable" : match is null ? "unsupported" : "verified-kept";
            item.RecordMapping(!mapping.Available ? ApiMappingStatus.TemporarilyUnavailable : match is null ? ApiMappingStatus.Unsupported : ApiMappingStatus.Verified,
                timeProvider.GetUtcNow(), match?.Name, match?.Icon);
            if (match is not null && priceMode == "Api")
            {
                var prices = await catalogueApi!.GetHourlyPricesAsync(ct);
                if (prices.Data is { } hourly && hourly.Values.TryGetValue(match.Id, out var value) && value is not null)
                {
                    // Explicit selection opts a previously manual item back into API pricing.
                    if (item.ApplyApiPrice(value, hourly.Hour, replaceFixedValue: true))
                    { message = "Mapping verified and hourly price saved."; result = "verified-price"; }
                    else
                    {
                        result = "verified-rejected";
                        messageType = UiMessageType.Warning;
                        message = "Mapping verified. The API price was rejected as an unusual change. The trusted catalogue value was kept. Review the candidate or enter a checked manual value.";
                    }
                }
                else
                {
                    missingHourlyPrice = true;
                    result = prices.Available ? "verified-no-price" : "verified-price-unavailable";
                    messageType = UiMessageType.Information;
                    message = prices.Available ? "Mapping verified. No hourly price is available."
                        : ProviderFailure(prices.Error, "Mapping verified. The price API is temporarily unavailable; retry later.");
                }
            }
            else
            {
                messageType = match is null ? UiMessageType.Information : UiMessageType.Success; message = match is not null ? "Mapping verified. Your selected catalogue value was kept."
                : mapping.Available ? "Settings saved. This ID is not in the tradeable item mapping; review the exact variant or untradeable classification."
                : ProviderFailure(mapping.Error, "Settings saved. The API is temporarily unavailable; retry validation later.");
            }
        }
        var feedback = Localize(message);
        if (previousPriceSource == CataloguePriceSource.Api && previousValue is not null && item.CatalogueValueGp is null)
        {
            feedback += " " + Localize("Changing the item ID cleared its old API price. Enter a manual catalogue value or retry validation when a price is available.");
            messageType = UiMessageType.Information;
        }
        else if (missingHourlyPrice)
            feedback += " " + Localize(item.CatalogueValueGp is not null ? "The stored catalogue value was kept."
                : "No catalogue value is stored. Enter a manual value or retry validation when a price is available.");
        return await SaveAsync("catalogue.item_api_updated", "catalogue_item", item.Id, item.Name, before, () => State(item), feedback, ct, messageType,
            data: new Dictionary<string, object?>
            {
                ["activityId"] = drop.BossActivityId, ["dropId"] = drop.Id, ["tone"] = messageType.ToString(), ["result"] = result, ["rateLimited"] = rateLimited,
                ["value"] = item.CatalogueValueGp, ["cleared"] = previousPriceSource == CataloguePriceSource.Api && previousValue is not null && item.CatalogueValueGp is null,
                ["idChanged"] = !string.Equals(previousExternalIdentifier, item.ExternalIdentifier, StringComparison.Ordinal), ["mode"] = priceMode
            });
    }

    public async Task<IActionResult> OnPostBossApiAsync(Guid recordId, long expectedVersion, string? externalIdentifier, string operation, CancellationToken ct)
    {
        var boss = await dbContext.BossActivities.SingleOrDefaultAsync(x => x.Id == recordId, ct);
        if (boss is null) return NotFound();
        BossId = recordId;
        if (boss.Version != expectedVersion) return Stale();
        externalIdentifier = Clean(externalIdentifier);
        if (externalIdentifier?.Length > 200 || operation is not ("save" or "validate")) return BadRequest();
        var before = State(boss);
        dbContext.Entry(boss).Property(x => x.Version).IsModified = true;
        boss.ConfigureApi(externalIdentifier);
        if (operation == "validate" && externalIdentifier is not null)
        {
            var metrics = catalogueApi is null ? new CatalogueApiResult<IReadOnlySet<string>>(null) : await catalogueApi.GetBossMetricsAsync(ct);
            boss.RecordMapping(!metrics.Available ? ApiMappingStatus.TemporarilyUnavailable
                : metrics.Data!.Contains(externalIdentifier) ? ApiMappingStatus.Verified : ApiMappingStatus.Unsupported, timeProvider.GetUtcNow());
        }
        return await SaveAsync("catalogue.boss_api_updated", "boss_activity", boss.Id, boss.Name, before, () => State(boss),
            Localize("API settings saved: {0}. A verified metric does not guarantee activity data for every player.", Localize(MappingLabel(boss.MappingStatus))), ct,
            operation == "validate" && boss.MappingStatus != ApiMappingStatus.Verified ? UiMessageType.Information : UiMessageType.Success,
            data: new Dictionary<string, object?> { ["activityId"] = boss.Id, ["status"] = boss.MappingStatus.ToString(), ["tone"] = (operation == "validate" && boss.MappingStatus != ApiMappingStatus.Verified ? UiMessageType.Information : UiMessageType.Success).ToString() });
    }
}
