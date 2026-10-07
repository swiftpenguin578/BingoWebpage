using Bingo.Domain.Events;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Catalogue;
using Bingo.Domain.Auditing;
using Bingo.Domain.Access;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Catalogue;

[Authorize(Policy = AuthorizationPolicies.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed partial class IndexModel(ApplicationDbContext dbContext, TimeProvider timeProvider, IStringLocalizer<SharedResource>? text = null, ICatalogueApiClient? catalogueApi = null, IStringLocalizer<AdminCommunityResource>? community = null) : PageModel
{
    [BindProperty] public BossInput Boss { get; set; } = new(); [BindProperty] public BossDropInput BossDrop { get; set; } = new();
    [BindProperty(SupportsGet = true)] public Guid? BossId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? DropId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Overlay { get; set; }
    [BindProperty(SupportsGet = true)] public bool AddBoss { get; set; }
    public bool IsOverlay => Overlay || string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
    public bool IsEditorRoute => BossId.HasValue || AddBoss;
    public static string RateLabel(string category) => string.Equals(category, "Minigame", StringComparison.Ordinal) ? "Runs per hour" : "Kills per hour";
    public static string RateUnit(string category, decimal? rate)
    {
        var unit = string.Equals(category, "Minigame", StringComparison.Ordinal) ? "run" : "kill";
        return rate == 1m ? unit : $"{unit}s";
    }
    public IReadOnlyList<BossRow> Bosses { get; private set; } = []; public IReadOnlyList<DropRow> Drops { get; private set; } = [];
    public Task OnGetAsync(CancellationToken ct) => LoadAsync(ct);
    public async Task<IActionResult> OnPostBossAsync(CancellationToken ct)
    {
        if (HasOperatorFields("Boss.ExternalIdentifier", "Boss.DataSource", "Boss.Notes")
            || Boss.ExternalIdentifier is not null || Boss.DataSource is not null || Boss.Notes is not null) return RetiredFields();
        if (HasInvalidTeamSizeBinding("Boss.TeamSize", Boss.TeamSize)) return Invalid("teamSize", Localize("Enter a whole number for team size."));
        if (Boss.TeamSize < 1) return Invalid("teamSize", Localize("Team size must be at least 1."));
        ModelState.Clear();
        if (!TryValidateModel(Boss, nameof(Boss))) return InvalidModel(nameof(Boss));
        // C-CAT-2 / T2-Q4 (a): the category is one of the fixed list.
        if (!ValidCategory(Boss.Category)) return Invalid("category", L(CategoryInvalid));
        Boss.Category = Boss.Category.Trim();

        var name = Boss.Name.Trim();
        var existingNames = await dbContext.BossActivities.Select(x => x.Name).ToListAsync(ct);
        if (existingNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
            return Invalid("name", Localize("A boss or activity named {0} already exists.", name));

        var slug = await UniqueSlug(name, ct);
        var entity = new BossActivity(Guid.NewGuid(), name, slug, Boss.Category, Boss.EfficientRate, timeProvider.GetUtcNow());
        entity.SetTeamSize(Boss.TeamSize);
        entity.Update(name, Boss.Category, Boss.EfficientRate, Clean(Boss.ExternalIdentifier), Clean(Boss.DataSource), Clean(Boss.Notes), timeProvider.GetUtcNow(), OsrsWikiImageUrl.Normalize(Boss.ImageUrl));
        dbContext.BossActivities.Add(entity);
        BossId = entity.Id;
        AddBoss = false;
        return await SaveAsync("catalogue.boss_created", "boss_activity", entity.Id, entity.Name, "{}", State(entity), $"{entity.Name} added to the catalogue.", ct,
            data: new Dictionary<string, object?> { ["activityId"] = entity.Id, ["name"] = entity.Name });
    }
    public async Task<IActionResult> OnPostBossDropAsync(CancellationToken ct, Guid[]? sharedItemConfirmationActivityIds = null)
    {
        if (HasOperatorFields("BossDrop.NumericProbability", "BossDrop.ProbabilityScope", "BossDrop.ConditionalOnParent", "BossDrop.ParentProbability", "BossDrop.AssumedParticipants", "BossDrop.RollsPerCompletion", "BossDrop.RollGroup", "BossDrop.Condition", "BossDrop.DataSource")
            || BossDrop.NumericProbability is not null || BossDrop.ProbabilityScope != DropProbabilityScope.Participant || BossDrop.ConditionalOnParent || BossDrop.ParentProbability is not null
            || BossDrop.AssumedParticipants != 1 || BossDrop.RollsPerCompletion != 1 || BossDrop.RollGroup != "default" || BossDrop.Condition is not null || BossDrop.DataSource is not null) return RetiredFields();
        // Preserve binding failures for the optional numeric API inputs before validating this form.
        if ((ModelState.TryGetValue("BossDrop.InitialWikiItemId", out var idState) && idState.Errors.Count > 0)
            || (ModelState.TryGetValue("BossDrop.InitialValueGp", out var valueState) && valueState.Errors.Count > 0)) return BadRequest();
        ModelState.Clear();
        if (!TryValidateModel(BossDrop, nameof(BossDrop))) return InvalidModel(nameof(BossDrop));
        var boss = await dbContext.BossActivities.SingleOrDefaultAsync(x => x.Id == BossDrop.BossActivityId, ct); if (boss is null) return NotFound();
        var parsedRate = DropRateParser.TryParse(BossDrop.DisplayRate);
        if (parsedRate is null) return Invalid("rate", Localize("Enter a valid drop rate, for example 1/100."));
        var itemName = BossDrop.ItemName.Trim(); var normalizedName = itemName.ToUpperInvariant(); var item = await dbContext.CatalogueItems.SingleOrDefaultAsync(x => x.NormalizedName == normalizedName, ct);
        if (item is not null && !BossDrop.UseExistingItem)
            return Respond("shared", Localize("An item named {0} already exists as a shared item. Confirm using that shared item, or change the name. Nothing was saved.", item.Name),
                UiMessageType.Warning, new Dictionary<string, object?> { ["field"] = "useShared", ["itemName"] = item.Name });
        ApiItem? fetchedItem = null;
        ApiHourlyPrices? initialPrices = null;
        var initialMappingStatus = ApiMappingStatus.NotConfigured;
        DateTimeOffset? initialMappingCheckedAt = null;
        string? fetchFailure = null;
        if (item is null && BossDrop.FetchPrice && !BossDrop.Untradeable)
        {
            var mapping = catalogueApi is null ? new CatalogueApiResult<IReadOnlyList<ApiItem>>(null) : await catalogueApi.GetItemsAsync(ct);
            initialMappingCheckedAt = timeProvider.GetUtcNow();
            var matches = mapping.Data?.Where(x => BossDrop.InitialWikiItemId is { } wikiId ? x.Id == wikiId : string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            fetchedItem = matches?.Length == 1 ? matches[0] : null;
            initialMappingStatus = !mapping.Available ? ApiMappingStatus.TemporarilyUnavailable
                : fetchedItem is null ? ApiMappingStatus.Unsupported : ApiMappingStatus.Verified;
            if (!mapping.Available) fetchFailure = ProviderFailure(mapping.Error, "The item mapping API is temporarily unavailable.");
            else if (fetchedItem is null) fetchFailure = BossDrop.InitialWikiItemId is not null
                ? "The item ID is not in the tradeable item mapping."
                : "No unique exact-name item match was found.";
            else
            {
                var prices = await catalogueApi!.GetHourlyPricesAsync(ct);
                initialPrices = prices.Data;
                if (!prices.Available) fetchFailure = ProviderFailure(prices.Error, "The price API is temporarily unavailable.");
                else if (initialPrices!.Values.GetValueOrDefault(fetchedItem.Id) is null)
                    fetchFailure = "No hourly price is available for the matched item.";
            }
        }
        var initialApiValue = fetchedItem is null ? null : initialPrices?.Values.GetValueOrDefault(fetchedItem.Id);
        if (item is null && initialApiValue is null && BossDrop.InitialValueGp is null && !BossDrop.Untradeable)
        {
            if (fetchFailure is null) return Invalid("value", Localize("Enter a catalogue value for this new item. Zero is valid; untradeable items default to zero."));
            return Respond("provider", Localize(fetchFailure) + " " + Localize("The drop was not added. Enter a manual catalogue value, review the exact item mapping, or retry the price fetch later."),
                UiMessageType.Error, new Dictionary<string, object?> { ["field"] = "value", ["reason"] = Localize(fetchFailure) });
        }
        if (item is null)
        {
            item = new CatalogueItem(Guid.NewGuid(), itemName, normalizedName);
            item.Update(itemName, normalizedName, null, null, OsrsWikiImageUrl.Normalize(BossDrop.ImageUrl));
            item.ConfigureApi((fetchedItem?.Id ?? BossDrop.InitialWikiItemId)?.ToString(CultureInfo.InvariantCulture));
            item.SetPrice(BossDrop.Untradeable ? 0 : initialApiValue ?? BossDrop.InitialValueGp,
                BossDrop.Untradeable ? CataloguePriceSource.Untradeable : initialApiValue is not null ? CataloguePriceSource.Api : CataloguePriceSource.Manual,
                initialApiValue is not null ? initialPrices!.Hour : timeProvider.GetUtcNow());
            if (initialMappingCheckedAt is not null && item.ExternalIdentifier is not null)
                item.RecordMapping(initialMappingStatus, initialMappingCheckedAt, fetchedItem?.Name, fetchedItem?.Icon);
            dbContext.CatalogueItems.Add(item);
        }
        var targetItem = item!;
        var itemBefore = State(targetItem);
        var existing = await dbContext.SourceDrops.SingleOrDefaultAsync(x => x.BossActivityId == boss.Id && x.ItemId == targetItem.Id, ct);
        if (existing?.Active == true) return Invalid("name", Localize("{0} is already listed for {1}.", targetItem.Name, boss.Name));
        var dropBefore = existing is null ? null : State(existing);
        var normalizedImageUrl = OsrsWikiImageUrl.Normalize(BossDrop.ImageUrl);
        var metadataChangesTarget = !string.IsNullOrWhiteSpace(BossDrop.ImageUrl)
            && !ImagesEquivalent(normalizedImageUrl, targetItem.ImageUrl);
        var sharedActivities = metadataChangesTarget
            ? await GetSharedItemActivitiesAsync(targetItem.Id, boss.Id, ct)
            : [];
        if (metadataChangesTarget && !SharedItemConfirmationMatches(sharedItemConfirmationActivityIds, sharedActivities))
            return RefuseSharedItemChange(sharedActivities);
        var rolls = parsedRate.RollsPerCompletion;
        var probability = parsedRate.ProbabilityPerRoll; var effectiveProbability = SourceDrop.CalculateProbabilityPerCompletion(probability, rolls); var now = timeProvider.GetUtcNow(); decimal? calculatedEhb = boss.EfficientCompletionsPerHour is > 0 && effectiveProbability is > 0 ? 1 / (boss.EfficientCompletionsPerHour.Value * effectiveProbability.Value) : null;
        if (existing is null) { existing = new SourceDrop(Guid.NewGuid(), boss.Id, targetItem.Id, BossDrop.DisplayRate.Trim(), probability, calculatedEhb, now); dbContext.SourceDrops.Add(existing); }
        existing.Update(BossDrop.DisplayRate.Trim(), probability, existing.RateConditionNote, calculatedEhb, existing.DataSource, now); existing.SetActive(true);
        existing.SetRateMechanics(existing.ProbabilityScope, existing.ConditionalOnParent, existing.ParentProbability, existing.AssumedParticipants, rolls, existing.RollGroup);
        targetItem.SetActive(true);
        if (metadataChangesTarget) targetItem.Update(targetItem.Name, targetItem.NormalizedName, targetItem.ExternalIdentifier, targetItem.Notes, normalizedImageUrl);
        BossId = boss.Id;
        var message = Localize("Added {0} to {1}.", targetItem.Name, boss.Name);
        if (fetchFailure is not null)
            message += " " + Localize(fetchFailure) + " " + Localize("The entered manual value ({0} GP) was used and stays fixed during bulk refreshes. Review the mapping and choose API hourly average, then validate again to use API pricing.", targetItem.CatalogueValueGp!);
        var before = dropBefore is null ? "{}" : $"{{\"Drop\":{dropBefore},\"Item\":{itemBefore}}}";
        var created = existing;
        return await SaveAsync(dropBefore is null ? "catalogue.drop_created" : "catalogue.drop_updated", "source_drop", existing.Id, $"{boss.Name}: {targetItem.Name}", before, () => State(new { Drop = existing, Item = targetItem }), message, ct,
            fetchFailure is null ? UiMessageType.Success : UiMessageType.Information,
            metadataChangesTarget ? async cancellationToken =>
            {
                // D9: recheck the exact current set before commit.
                var currentActivities = await GetSharedItemActivitiesAsync(targetItem.Id, boss.Id, cancellationToken);
                return SharedItemConfirmationMatches(sharedItemConfirmationActivityIds, currentActivities) ? null : RefuseSharedItemChange(currentActivities);
            } : null,
            new Dictionary<string, object?>
            {
                ["activityId"] = boss.Id, ["dropId"] = created.Id, ["itemName"] = targetItem.Name, ["reactivated"] = dropBefore is not null,
                ["notice"] = fetchFailure is null ? null : Localize(fetchFailure) + " " + Localize("The entered manual value ({0} GP) was used and stays fixed during bulk refreshes. Review the mapping and choose API hourly average, then validate again to use API pricing.", targetItem.CatalogueValueGp!)
            });
    }
    // S10: deactivating an activity or drop needs the explicit confirmation (confirmed=true) that showed its
    // impact; without it nothing changes and the response carries the impact. Reactivation needs none.
    public async Task<IActionResult> OnPostToggleBossAsync(Guid recordId, long expectedVersion, CancellationToken ct, bool confirmed = false)
    {
        BossId = recordId;
        var entity = await dbContext.BossActivities.SingleOrDefaultAsync(x => x.Id == recordId, ct);
        if (entity is null) return NotFound();
        if (entity.Version != expectedVersion) return Stale();
        if (entity.Active && !confirmed) return await ConfirmDeactivationAsync("boss", recordId, ct);
        var before = State(entity); entity.SetActive(!entity.Active);
        return await SaveAsync("catalogue.boss_toggled", "boss_activity", entity.Id, entity.Name, before, State(entity), $"{entity.Name} {(entity.Active ? "reactivated" : "deactivated")}.", ct,
            data: new Dictionary<string, object?> { ["activityId"] = entity.Id, ["active"] = entity.Active, ["name"] = entity.Name });
    }
    public async Task<IActionResult> OnPostToggleDropAsync(Guid recordId, long expectedVersion, CancellationToken ct, bool confirmed = false)
    {
        DropId = recordId;
        var entity = await dbContext.SourceDrops.SingleOrDefaultAsync(x => x.Id == recordId, ct);
        if (entity is null) return NotFound();
        BossId = entity.BossActivityId;
        if (entity.Version != expectedVersion) return Stale();
        if (entity.Active && !confirmed) return await ConfirmDeactivationAsync("drop", recordId, ct);
        var itemName = await dbContext.CatalogueItems.Where(x => x.Id == entity.ItemId).Select(x => x.Name).SingleAsync(ct);
        var before = State(entity); entity.SetActive(!entity.Active);
        return await SaveAsync("catalogue.drop_toggled", "source_drop", entity.Id, itemName, before, State(entity), $"{itemName} {(entity.Active ? "reactivated" : "deactivated")}.", ct,
            data: new Dictionary<string, object?> { ["activityId"] = entity.BossActivityId, ["dropId"] = entity.Id, ["active"] = entity.Active, ["name"] = itemName });
    }
    private async Task<IActionResult> ConfirmDeactivationAsync(string recordType, Guid recordId, CancellationToken ct)
    {
        var impact = await BuildDeactivationImpactAsync(recordType, recordId, ct);
        if (impact is null) return NotFound();
        return Respond("confirm", L("Confirm the deactivation first. Nothing was changed."), UiMessageType.Warning,
            new Dictionary<string, object?> { ["impact"] = ImpactJson(impact) });
    }
    public async Task<IActionResult> OnPostUpdateBossAsync(Guid recordId, long expectedVersion, string name, string category, decimal? efficientRate, string? dataSource, string? imageUrl, CancellationToken ct, int? teamSize = null)
    {
        BossId = recordId;
        if (HasOperatorFields("dataSource") || dataSource is not null) return RetiredFields();
        if (HasInvalidTeamSizeBinding("teamSize", teamSize)) return Invalid("teamSize", Localize("Enter a whole number for team size."));
        if (teamSize is < 1) return Invalid("teamSize", Localize("Team size must be at least 1."));
        var entity = await dbContext.BossActivities.SingleOrDefaultAsync(x => x.Id == recordId, ct);
        if (entity is null) return NotFound();
        if (entity.Version != expectedVersion) return Stale();
        var validationInput = new BossInput
        {
            Name = name ?? string.Empty,
            Category = category ?? string.Empty,
            EfficientRate = efficientRate,
            TeamSize = teamSize ?? entity.TeamSize,
            ImageUrl = imageUrl
        };
        ModelState.Clear();
        if (!TryValidateModel(validationInput, nameof(Boss))) return InvalidModel(nameof(Boss));
        // C-CAT-2 / T2-Q4 (a): the category is one of the fixed list on edit too.
        if (!ValidCategory(validationInput.Category)) return Invalid("category", L(CategoryInvalid));

        var before = State(entity); var cleanName = validationInput.Name.Trim();
        var efficientRateChanged = validationInput.EfficientRate != entity.EfficientCompletionsPerHour;
        var otherNames = await dbContext.BossActivities.Where(x => x.Id != recordId).Select(x => x.Name).ToListAsync(ct);
        if (otherNames.Any(x => string.Equals(x, cleanName, StringComparison.OrdinalIgnoreCase))) return Invalid("name", Localize("A boss or activity named {0} already exists.", cleanName));
        entity.Update(cleanName, validationInput.Category.Trim(), validationInput.EfficientRate, entity.ExternalIdentifier, entity.DataSource, entity.Notes, timeProvider.GetUtcNow(), OsrsWikiImageUrl.Normalize(validationInput.ImageUrl));
        if (teamSize is { } requestedTeamSize) entity.SetTeamSize(requestedTeamSize);
        var drops = await dbContext.SourceDrops.Where(x => x.BossActivityId == recordId).ToListAsync(ct);
        if (teamSize is null || efficientRateChanged)
        {
            foreach (var drop in drops)
            {
                var effective = drop.EffectiveProbabilityPerCompletion();
                drop.Update(drop.DisplayRate, drop.NumericProbability, drop.RateConditionNote, validationInput.EfficientRate is > 0 && effective is > 0 ? 1 / (validationInput.EfficientRate.Value * effective.Value) : null, drop.DataSource, timeProvider.GetUtcNow());
            }
        }
        return await SaveAsync("catalogue.boss_updated", "boss_activity", entity.Id, entity.Name, before, State(entity), $"{entity.Name} updated.", ct,
            data: new Dictionary<string, object?> { ["activityId"] = entity.Id, ["name"] = entity.Name, ["rateChanged"] = efficientRateChanged, ["dropCount"] = drops.Count });
    }
    public async Task<IActionResult> OnPostUpdateDropAsync(Guid recordId, long expectedVersion, long expectedItemVersion, string itemName, string displayRate, string originalDisplayRate, decimal? numericProbability, decimal? originalNumericProbability, DropProbabilityScope probabilityScope, bool conditionalOnParent, decimal? parentProbability, int assumedParticipants, int rollsPerCompletion, string? rollGroup, string? dataSource, string? imageUrl, bool useExistingItem, CancellationToken ct, Guid[]? sharedItemConfirmationActivityIds = null)
    {
        if (HasOperatorFields("numericProbability", "probabilityScope", "conditionalOnParent", "parentProbability", "assumedParticipants", "rollsPerCompletion", "dataSource")) return RetiredFields();
        DropId = recordId;
        if (JsonRequest && string.IsNullOrWhiteSpace(itemName)) return Invalid("name", L("Enter the item name."));
        if (JsonRequest && string.IsNullOrWhiteSpace(displayRate)) return Invalid("rate", L("Enter the drop rate, like 1/512."));
        if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(displayRate) || numericProbability is <= 0 or > 1) return BadRequest();
        // C-CAT-2 / T2-Q4 (a): the shared item's name has the same 200-character limit as on add.
        if (itemName.Trim().Length > ItemNameLimit) return Invalid("name", L(ItemNameTooLong));
        var entity = await dbContext.SourceDrops.SingleOrDefaultAsync(x => x.Id == recordId, ct);
        if (entity is null) return NotFound();
        BossId = entity.BossActivityId;
        if (entity.Version != expectedVersion) return Stale();
        if ((numericProbability is not null && numericProbability != entity.NumericProbability)
            || (probabilityScope != default && probabilityScope != entity.ProbabilityScope)
            || (conditionalOnParent && !entity.ConditionalOnParent) || (parentProbability is not null && parentProbability != entity.ParentProbability)
            || (assumedParticipants > 0 && assumedParticipants != entity.AssumedParticipants) || (rollsPerCompletion > 0 && rollsPerCompletion != entity.RollsPerCompletion)
            || (dataSource is not null && dataSource != entity.DataSource)) return RetiredFields();
        var item = await dbContext.CatalogueItems.SingleAsync(x => x.Id == entity.ItemId, ct);
        if (item.Version != expectedItemVersion) return Stale();
        var rollGroupSupplied = rollGroup is not null || HasOperatorFields("rollGroup");
        var requestedRollGroup = rollGroup?.Trim();
        var rollGroupChanged = rollGroupSupplied && !string.Equals(requestedRollGroup, entity.RollGroup, StringComparison.Ordinal);
        // Unchanged rule (AU23): only an active Super Admin changes the roll group; wording per item 13.
        if (rollGroupChanged && !await IsActiveSuperAdminAsync(ct)) return RollGroupRefused();
        if (rollGroupSupplied && (string.IsNullOrWhiteSpace(requestedRollGroup) || requestedRollGroup.Length > 120))
            return Invalid("rollGroup", L("Enter a roll group of 120 characters or fewer."));
        var cleanItemName = itemName.Trim();
        var normalizedItemName = cleanItemName.ToUpperInvariant();
        var matchingItem = await dbContext.CatalogueItems.SingleOrDefaultAsync(x => x.Id != item.Id && x.NormalizedName == normalizedItemName, ct);
        if (matchingItem is not null && !useExistingItem)
            return Respond("shared", Localize("An item named {0} already exists. Confirm that you want to use the shared item.", cleanItemName),
                UiMessageType.Warning, new Dictionary<string, object?> { ["field"] = "useShared", ["itemName"] = matchingItem.Name });
        if (matchingItem is not null && await dbContext.SourceDrops.AnyAsync(x => x.Id != entity.Id && x.BossActivityId == entity.BossActivityId && x.ItemId == matchingItem.Id, ct))
            return Invalid("name", Localize("This boss already has a drop named {0}.", cleanItemName));
        var targetItem = matchingItem ?? item;
        var normalizedImageUrl = OsrsWikiImageUrl.Normalize(imageUrl);
        var targetImageUrl = matchingItem is null || !string.IsNullOrWhiteSpace(imageUrl) ? normalizedImageUrl : targetItem.ImageUrl;
        var metadataChangesTarget = (matchingItem is null && !string.Equals(cleanItemName, item.Name, StringComparison.Ordinal))
            || !ImagesEquivalent(targetImageUrl, targetItem.ImageUrl);
        var sharedActivities = metadataChangesTarget
            ? await GetSharedItemActivitiesAsync(targetItem.Id, entity.BossActivityId, ct)
            : [];
        if (metadataChangesTarget && !SharedItemConfirmationMatches(sharedItemConfirmationActivityIds, sharedActivities))
            return RefuseSharedItemChange(sharedActivities);
        var affectedItems = matchingItem is null ? new[] { item } : new[] { item, matchingItem };
        var before = State(new { Drop = entity, Items = affectedItems });
        var cleanDisplayRate = displayRate.Trim();
        var displayRateChanged = !string.Equals(cleanDisplayRate, entity.DisplayRate, StringComparison.Ordinal);
        var parsedRate = DropRateParser.TryParse(cleanDisplayRate);
        if (displayRateChanged && parsedRate is null) return Invalid("rate", Localize("Enter a valid drop rate, for example 1/100."));
        var probability = displayRateChanged ? parsedRate!.ProbabilityPerRoll : entity.NumericProbability;
        rollsPerCompletion = displayRateChanged ? parsedRate!.RollsPerCompletion : entity.RollsPerCompletion;
        var effectiveProbability = SourceDrop.CalculateProbabilityPerCompletion(probability, rollsPerCompletion);
        var bossRate = await dbContext.BossActivities.Where(x => x.Id == entity.BossActivityId).Select(x => x.EfficientCompletionsPerHour).SingleAsync(ct); decimal? calculatedEhb = bossRate is > 0 && effectiveProbability is > 0 ? 1 / (bossRate.Value * effectiveProbability.Value) : null;
        entity.Update(cleanDisplayRate, probability, entity.RateConditionNote, calculatedEhb, entity.DataSource, timeProvider.GetUtcNow());
        if (displayRateChanged || rollGroupChanged)
            entity.SetRateMechanics(entity.ProbabilityScope, entity.ConditionalOnParent, entity.ParentProbability, entity.AssumedParticipants, rollsPerCompletion, rollGroupChanged ? requestedRollGroup : entity.RollGroup);
        if (matchingItem is null)
        {
            if (!string.Equals(cleanItemName, item.Name, StringComparison.Ordinal)
                || !ImagesEquivalent(normalizedImageUrl, item.ImageUrl))
                item.Update(cleanItemName, normalizedItemName, item.ExternalIdentifier, item.Notes, normalizedImageUrl);
        }
        else
        {
            entity.ChangeItem(matchingItem.Id);
            matchingItem.SetActive(true);
            if (!string.IsNullOrWhiteSpace(imageUrl) && !ImagesEquivalent(normalizedImageUrl, matchingItem.ImageUrl))
                matchingItem.Update(matchingItem.Name, matchingItem.NormalizedName, matchingItem.ExternalIdentifier, matchingItem.Notes, normalizedImageUrl);
            // Retain the previous shared identity and its price/history metadata.
        }
        // Enlist even unchanged shared metadata in the optimistic write check. A
        // concurrent item edit must also reject a form that only changes the drop.
        foreach (var affectedItem in affectedItems.Where(x => dbContext.Entry(x).State != EntityState.Deleted))
            dbContext.Entry(affectedItem).Property(x => x.Version).IsModified = true;
        var retainedItems = affectedItems.Where(x => dbContext.Entry(x).State != EntityState.Deleted).ToArray();
        return await SaveAsync("catalogue.drop_updated", "source_drop", entity.Id, entity.DisplayRate, before,
            () => State(new { Drop = entity, Items = retainedItems }), "Drop details updated.", ct,
            UiMessageType.Success,
            metadataChangesTarget ? async cancellationToken =>
            {
                // D7: recheck the exact current set before commit.
                var currentActivities = await GetSharedItemActivitiesAsync(targetItem.Id, entity.BossActivityId, cancellationToken);
                return SharedItemConfirmationMatches(sharedItemConfirmationActivityIds, currentActivities) ? null : RefuseSharedItemChange(currentActivities);
            } : null,
            new Dictionary<string, object?> { ["activityId"] = entity.BossActivityId, ["dropId"] = entity.Id, ["itemName"] = targetItem.Name });
    }
    public async Task<IActionResult> OnGetDeletionImpactAsync(string recordType, Guid recordId, long expectedVersion, CancellationToken ct)
    {
        // A script read gets a refusal it can show; an XHR Forbid() is a bare 403 (C-CMP-2 keeps it apart from session loss).
        if (!User.IsInRole("SuperAdmin")) return JsonRequest ? new JsonResult(new { outcome = "refused", canDelete = false, message = L("Only the Super Admin can delete catalogue records.") }) : Forbid();
        Response.Headers.CacheControl = "no-store";
        string? name;
        long? version;
        if (recordType == "boss")
        {
            var boss = await dbContext.BossActivities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recordId, ct);
            name = boss?.Name; version = boss?.Version;
        }
        else if (recordType == "drop")
        {
            var drop = await (
                from d in dbContext.SourceDrops.AsNoTracking()
                join i in dbContext.CatalogueItems on d.ItemId equals i.Id
                where d.Id == recordId
                select new { i.Name, d.Version }).SingleOrDefaultAsync(ct);
            name = drop?.Name; version = drop?.Version;
        }
        else return BadRequest();
        if (version is null || version != expectedVersion)
            return new JsonResult(new { outcome = "stale", canDelete = false, message = Localize("This record was changed by another administrator. Current values are shown; review them before saving.") });
        var referenced = recordType == "boss" ? await BossHasReferencesAsync(recordId, ct) : await DropHasReferencesAsync(recordId, ct);
        return new JsonResult(new
        {
            outcome = referenced ? "referenced" : "deletable",
            canDelete = !referenced,
            title = Localize("Delete {0} permanently?", name!),
            message = Localize(referenced ? "This record is referenced and cannot be permanently deleted. Deactivate it instead."
                : "No dependencies were found. This permanently deletes the unused catalogue record. Shared items, prices and Audit history are retained. This cannot be undone.")
        });
    }
    public async Task<IActionResult> OnPostDeleteAsync(string recordType, Guid recordId, long expectedVersion, bool confirmed, CancellationToken ct)
    {
        if (!User.IsInRole("SuperAdmin")) return JsonRequest ? new JsonResult(new { outcome = "refused", message = L("Only the Super Admin can delete catalogue records.") }) : Forbid();
        if (!confirmed) return Respond("confirm", Localize("Review the deletion impact and confirm before deleting this record."), UiMessageType.Warning);
        Guid? parentActivity = recordType == "drop" ? await dbContext.SourceDrops.AsNoTracking().Where(x => x.Id == recordId).Select(x => (Guid?)x.BossActivityId).SingleOrDefaultAsync(ct) : null;
        // Acquire the record lock before dependency reads. READ COMMITTED makes
        // references committed while waiting visible to the subsequent recheck.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var prepared = recordType switch
            {
                "boss" => await PrepareBossDeletionAsync(recordId, expectedVersion, ct),
                "drop" => await PrepareDropDeletionAsync(recordId, expectedVersion, ct),
                _ => null
            };
            if (prepared is null || prepared.Status != DeletePreparationStatus.Ready)
            {
                await transaction.RollbackAsync(ct);
                return prepared?.Status == DeletePreparationStatus.Referenced
                    ? Respond("referenced", Localize("This record is referenced and cannot be permanently deleted. Deactivate it instead."), UiMessageType.Warning)
                    : Respond("stale", Localize("This record was changed by another administrator. Current values are shown; review them before trying again."), UiMessageType.Warning);
            }
            var candidate = prepared.Candidate!;
            await dbContext.SaveChangesAsync(ct);
            dbContext.AuditEntries.Add(CreateAudit(candidate.Action, candidate.Type, candidate.Id, candidate.Details, candidate.Before, "{}"));
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Respond("completed", Localize("Unused catalogue record permanently deleted."), UiMessageType.Success,
                new Dictionary<string, object?> { ["recordType"] = recordType, ["recordId"] = recordId, ["activityId"] = parentActivity });
        }
        catch (Exception exception) when (IsExpectedCatalogueRace(exception))
        {
            await transaction.RollbackAsync(ct);
            return Respond("stale", Localize("This record was changed by another administrator or became referenced. It was not deleted; review the current catalogue and deactivate it instead if needed."), UiMessageType.Warning);
        }
    }
    private async Task<DeletePreparation> PrepareBossDeletionAsync(Guid id, long version, CancellationToken ct)
    {
        var entity = await dbContext.BossActivities.FromSqlInterpolated($"SELECT * FROM boss_activities WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (entity is null || entity.Version != version) return new(DeletePreparationStatus.Stale, null);
        var referenced = await BossHasReferencesAsync(id, ct);
        if (referenced) return new(DeletePreparationStatus.Referenced, null);
        var candidate = new DeleteCandidate("catalogue.boss_deleted", "boss_activity", id, entity.Name, State(entity));
        dbContext.BossActivities.Remove(entity);
        return new(DeletePreparationStatus.Ready, candidate);
    }
    private async Task<DeletePreparation> PrepareDropDeletionAsync(Guid id, long version, CancellationToken ct)
    {
        var entity = await dbContext.SourceDrops.FromSqlInterpolated($"SELECT * FROM source_drops WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (entity is null || entity.Version != version) return new(DeletePreparationStatus.Stale, null);
        var referenced = await DropHasReferencesAsync(id, ct);
        if (referenced) return new(DeletePreparationStatus.Referenced, null);
        var candidate = new DeleteCandidate("catalogue.drop_deleted", "source_drop", id, entity.DisplayRate, State(entity));
        dbContext.SourceDrops.Remove(entity);
        return new(DeletePreparationStatus.Ready, candidate);
    }
    private async Task<bool> BossHasReferencesAsync(Guid id, CancellationToken ct) =>
        await dbContext.SourceDrops.AnyAsync(x => x.BossActivityId == id, ct)
        || await dbContext.TemplateRequirementBosses.AnyAsync(x => x.BossActivityId == id, ct)
        || await dbContext.BoardRequirementBossSnapshots.AnyAsync(x => x.BossActivityId == id, ct)
        || await dbContext.BoardApprovalRequirementBossSnapshots.AnyAsync(x => x.BossActivityId == id, ct)
        || await dbContext.EventLuckOutcomeBases.AnyAsync(x => x.BossActivityId == id, ct);
    private async Task<bool> DropHasReferencesAsync(Guid id, CancellationToken ct) =>
        await dbContext.TemplateRequirementDrops.AnyAsync(x => x.SourceDropId == id, ct)
        || await dbContext.BoardRequirementDropSnapshots.AnyAsync(x => x.SourceDropId == id, ct)
        || await dbContext.BoardApprovalRequirementDropSnapshots.AnyAsync(x => x.SourceDropId == id, ct)
        || await dbContext.EventLuckOutcomeBases.AnyAsync(x => x.SourceDropId == id, ct);
    private async Task<IReadOnlyList<SharedItemActivity>> GetSharedItemActivitiesAsync(Guid itemId, Guid excludedBossId, CancellationToken ct)
    {
        var activities = await (from drop in dbContext.SourceDrops.AsNoTracking()
                                join boss in dbContext.BossActivities.AsNoTracking() on drop.BossActivityId equals boss.Id
                                where drop.ItemId == itemId && drop.BossActivityId != excludedBossId
                                select new { boss.Id, boss.Name }).Distinct().OrderBy(x => x.Name).ToListAsync(ct);
        return activities.Select(x => new SharedItemActivity(x.Id, x.Name)).ToArray();
    }

    private static bool SharedItemConfirmationMatches(IReadOnlyCollection<Guid>? confirmedActivityIds, IReadOnlyCollection<SharedItemActivity> affectedActivities)
    {
        var confirmed = confirmedActivityIds?.ToHashSet() ?? [];
        return confirmed.SetEquals(affectedActivities.Select(x => x.Id));
    }

    private static bool ImagesEquivalent(string? left, string? right) =>
        string.Equals(OsrsWikiImageUrl.Normalize(left), OsrsWikiImageUrl.Normalize(right), StringComparison.Ordinal);

    private async Task<bool> IsActiveSuperAdminAsync(CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        return actorId is { } id && await dbContext.Accounts.AsNoTracking().AnyAsync(account =>
            account.Id == id
            && account.AccountType == AccountType.WebsiteAccount
            && account.Active
            && account.DisabledAt == null
            && account.GlobalRole == GlobalRole.SuperAdmin, ct);
    }

    private bool HasOperatorFields(params string[] names) => Request.HasFormContentType && names.Any(name => Request.Form.ContainsKey(name));
    private bool HasInvalidTeamSizeBinding(string key, int? boundValue)
    {
        if (!Request.HasFormContentType || !Request.Form.TryGetValue(key, out var rawValue)) return false;
        var raw = rawValue.ToString();
        return string.IsNullOrWhiteSpace(raw)
            || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || boundValue != parsed
            || (ModelState.TryGetValue(key, out var state) && state.Errors.Count > 0);
    }
    private static string ProviderFailure(string? error, string fallback) => error == "The price API is temporarily limiting requests. Retry validation later." ? error : fallback;
    private async Task LoadAsync(CancellationToken ct)
    {
        ApiItems = await dbContext.CatalogueItems.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        ApiBosses = await dbContext.BossActivities.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        var bosses = await dbContext.BossActivities.AsNoTracking().OrderByDescending(x => x.Active).ThenBy(x => x.Name).Select(x => new BossRow(x.Id, x.Name, x.Category, x.EfficientCompletionsPerHour, x.TeamSize, x.Active, x.ImageUrl, x.DataSource, x.DataUpdatedAt, x.Version)).ToListAsync(ct);
        Bosses = bosses.Select(x => x with { ImageUrl = OsrsWikiImageUrl.Normalize(x.ImageUrl) }).ToList();

        var drops = await (from d in dbContext.SourceDrops.AsNoTracking() join i in dbContext.CatalogueItems on d.ItemId equals i.Id select new DropRow(d.Id, d.BossActivityId, i.Name, d.DisplayRate, d.NumericProbability, d.DefaultEhbEstimate, d.ProbabilityScope, d.ConditionalOnParent, d.ParentProbability, d.AssumedParticipants, d.RollsPerCompletion, d.RollGroup, d.Active, d.DataSource, i.ImageUrl, d.DataUpdatedAt, d.Version, i.Version, i.Id)).ToListAsync(ct);
        Drops = drops.Select(x => x with
        {
            ImageUrl = OsrsWikiImageUrl.Normalize(x.ImageUrl)
        }).ToList();
    }
    private async Task<string> UniqueSlug(string name, CancellationToken ct) { var root = EventSlugGenerator.Generate(name); var slug = root; for (var n = 2; await dbContext.BossActivities.AnyAsync(x => x.Slug == slug, ct); n++) slug = $"{root}-{n}"; return slug; }
    private Task<IActionResult> SaveAsync(string action, string type, Guid id, string details, string before, string after, string message, CancellationToken ct, IReadOnlyDictionary<string, object?>? data = null) =>
        SaveAsync(action, type, id, details, before, () => after, message, ct, data: data);
    private async Task<IActionResult> SaveAsync(string action, string type, Guid id, string details, string before, Func<string> after, string message, CancellationToken ct, UiMessageType messageType = UiMessageType.Success, Func<CancellationToken, Task<IActionResult?>>? beforeCommitGuard = null, IReadOnlyDictionary<string, object?>? data = null)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (beforeCommitGuard is not null && await beforeCommitGuard(ct) is { } refusal)
            {
                await transaction.RollbackAsync(ct);
                return refusal;
            }
            var catalogueChanges = BoardEstimateService.CatalogueChangeSet.Capture(dbContext);
            // A new source-drop connection must serialize with activity deletion.
            var parentIds = dbContext.ChangeTracker.Entries<SourceDrop>().Where(x => x.State == EntityState.Added)
                .Select(x => x.Entity.BossActivityId).Distinct().ToArray();
            if (parentIds.Length > 0)
            {
                var parents = await dbContext.BossActivities.FromSqlInterpolated($"SELECT * FROM boss_activities WHERE id = ANY({parentIds}) ORDER BY id FOR SHARE")
                    .AsNoTracking().ToListAsync(ct);
                if (parents.Count != parentIds.Length) throw new DbUpdateConcurrencyException("The selected activity is no longer available.");
            }
            await dbContext.SaveChangesAsync(ct);
            // Working estimate refreshes are derived cache maintenance. They
            // intentionally remain outside the user mutation's Audit owner.
            await BoardEstimateService.RefreshDependentDraftTilesAsync(dbContext, catalogueChanges, timeProvider.GetUtcNow(), ct);
            await dbContext.SaveChangesAsync(ct);
            dbContext.AuditEntries.Add(CreateAudit(action, type, id, details, before, after()));
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Respond("completed", message, messageType, data);
        }
        catch (Exception exception) when (IsExpectedCatalogueRace(exception))
        {
            await transaction.RollbackAsync(ct);
            return Stale();
        }
    }
    private RedirectToPageResult CataloguePage() => RedirectToPage(null, new { bossId = BossId, dropId = DropId, addBoss = AddBoss ? "true" : null, overlay = IsOverlay ? "1" : null });
    private static string State(object entity) => JsonSerializer.Serialize(entity);
    private AuditEntry CreateAudit(string action, string type, Guid id, string details, string before, string after) => new(Guid.NewGuid(), timeProvider.GetUtcNow(), User.GetAccountId(), User.Identity!.Name!, action, type, id.ToString(), details, beforeState: before, afterState: after);
    private static bool IsExpectedCatalogueRace(Exception exception) => exception is DbUpdateConcurrencyException || exception is DbUpdateException { InnerException: PostgresException { SqlState: "23503" or "23505" or "40001" or "40P01" } } || exception is PostgresException { SqlState: "23503" or "23505" or "40001" or "40P01" };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    public sealed class BossInput { [StringLength(200)] public string? ExternalIdentifier { get; set; } [Required, StringLength(200)] public string Name { get; set; } = string.Empty; [Required] public string Category { get; set; } = "Boss"; [Range(0.0001, 100000), Display(Name = "Efficient completions per hour")] public decimal? EfficientRate { get; set; } [Range(1, int.MaxValue), Display(Name = "Team size")] public int TeamSize { get; set; } = 1; [Display(Name = "Data source")] public string? DataSource { get; set; } [Url, Display(Name = "Image URL")] public string? ImageUrl { get; set; } public string? Notes { get; set; } }
    public sealed class BossDropInput { public bool FetchPrice { get; set; } [Range(1, int.MaxValue)] public int? InitialWikiItemId { get; set; } [Range(typeof(long), "0", "9223372036854775807")] public long? InitialValueGp { get; set; } public bool Untradeable { get; set; } public bool UseExistingItem { get; set; } [Required] public Guid BossActivityId { get; set; } [Required, StringLength(200), Display(Name = "Item name")] public string ItemName { get; set; } = string.Empty; [Required, StringLength(200), Display(Name = "Displayed drop rate")] public string DisplayRate { get; set; } = string.Empty; [Range(0.000000000001, 1), Display(Name = "Numeric probability")] public decimal? NumericProbability { get; set; } public DropProbabilityScope ProbabilityScope { get; set; } = DropProbabilityScope.Participant; public bool ConditionalOnParent { get; set; } [Range(0.000000000001, 1)] public decimal? ParentProbability { get; set; } [Range(1, 100)] public int AssumedParticipants { get; set; } = 1; [Range(1, 100)] public int RollsPerCompletion { get; set; } = 1; [StringLength(120)] public string RollGroup { get; set; } = "default"; [StringLength(2000), Display(Name = "Condition or note")] public string? Condition { get; set; } [StringLength(300), Display(Name = "Data source")] public string? DataSource { get; set; } [Url, Display(Name = "Item image URL")] public string? ImageUrl { get; set; } }
    public sealed record SharedItemActivity(Guid Id, string Name);
    public sealed record BossRow(Guid Id, string Name, string Category, decimal? EfficientRate, int TeamSize, bool Active, string? ImageUrl, string? DataSource, DateTimeOffset UpdatedAt, long Version);
    public sealed record DropRow(Guid Id, Guid BossActivityId, string ItemName, string DisplayRate, decimal? Probability, decimal? DefaultEhb, DropProbabilityScope ProbabilityScope, bool ConditionalOnParent, decimal? ParentProbability, int AssumedParticipants, int RollsPerCompletion, string RollGroup, bool Active, string? DataSource, string? ImageUrl, DateTimeOffset UpdatedAt, long Version, long ItemVersion, Guid ItemId = default);
    private enum DeletePreparationStatus { Ready, Stale, Referenced }
    private sealed record DeletePreparation(DeletePreparationStatus Status, DeleteCandidate? Candidate);
    private sealed record DeleteCandidate(string Action, string Type, Guid Id, string Details, string Before);
}
