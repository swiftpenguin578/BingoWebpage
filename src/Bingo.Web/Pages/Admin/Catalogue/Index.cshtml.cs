using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Catalogue;
using Bingo.Domain.Auditing;
using Bingo.Domain.Catalogue;
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
public sealed class IndexModel(ApplicationDbContext dbContext, TimeProvider timeProvider, IStringLocalizer<SharedResource>? text = null) : PageModel
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
        ModelState.Clear();
        if (!TryValidateModel(Boss, nameof(Boss)))
        {
            SetStatus(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), UiMessageType.Error);
            return CataloguePage();
        }

        var name = Boss.Name.Trim();
        var existingNames = await dbContext.BossActivities.Select(x => x.Name).ToListAsync(ct);
        if (existingNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus(Localize("A boss or activity named {0} already exists.", name), UiMessageType.Warning);
            return CataloguePage();
        }

        var slug = await UniqueSlug(name, ct);
        var entity = new BossActivity(Guid.NewGuid(), name, slug, Boss.Category, Boss.EfficientRate, timeProvider.GetUtcNow());
        entity.Update(name, Boss.Category, Boss.EfficientRate, null, Clean(Boss.DataSource), Clean(Boss.Notes), timeProvider.GetUtcNow(), OsrsWikiImageUrl.Normalize(Boss.ImageUrl));
        dbContext.BossActivities.Add(entity);
        BossId = entity.Id;
        AddBoss = false;
        return await SaveAsync("catalogue.boss_created", "boss_activity", entity.Id, entity.Name, "{}", State(entity), $"{entity.Name} added to the catalogue.", ct);
    }
    public async Task<IActionResult> OnPostBossDropAsync(CancellationToken ct)
    {
        ModelState.Clear();
        if (!TryValidateModel(BossDrop, nameof(BossDrop))) { SetStatus(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), UiMessageType.Error); return CataloguePage(); }
        var boss = await dbContext.BossActivities.SingleOrDefaultAsync(x => x.Id == BossDrop.BossActivityId, ct); if (boss is null) return NotFound();
        var itemName = BossDrop.ItemName.Trim(); var normalizedName = itemName.ToUpperInvariant(); var item = await dbContext.CatalogueItems.SingleOrDefaultAsync(x => x.NormalizedName == normalizedName, ct);
        if (item is null) { item = new CatalogueItem(Guid.NewGuid(), itemName, normalizedName); item.Update(itemName, normalizedName, null, null, OsrsWikiImageUrl.Normalize(BossDrop.ImageUrl)); dbContext.CatalogueItems.Add(item); }
        else { item.SetActive(true); if (!string.IsNullOrWhiteSpace(BossDrop.ImageUrl)) item.Update(item.Name, item.NormalizedName, item.ExternalIdentifier, item.Notes, OsrsWikiImageUrl.Normalize(BossDrop.ImageUrl)); }
        var existing = await dbContext.SourceDrops.SingleOrDefaultAsync(x => x.BossActivityId == boss.Id && x.ItemId == item.Id, ct);
        if (existing?.Active == true) { SetStatus(Localize("{0} is already listed for {1}.", item.Name, boss.Name), UiMessageType.Warning); return CataloguePage(); }
        var parsedRate = DropRateParser.TryParse(BossDrop.DisplayRate); var rolls = parsedRate?.ExplicitMultipleRolls == true && BossDrop.RollsPerCompletion == 1 ? parsedRate.RollsPerCompletion : BossDrop.RollsPerCompletion; var probability = BossDrop.NumericProbability ?? parsedRate?.ProbabilityPerRoll; var effectiveProbability = SourceDrop.CalculateProbabilityPerCompletion(probability, rolls); var now = timeProvider.GetUtcNow(); decimal? calculatedEhb = boss.EfficientCompletionsPerHour is > 0 && effectiveProbability is > 0 ? 1 / (boss.EfficientCompletionsPerHour.Value * effectiveProbability.Value) : null;
        if (existing is null) { existing = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, BossDrop.DisplayRate.Trim(), probability, calculatedEhb, now); dbContext.SourceDrops.Add(existing); }
        existing.Update(BossDrop.DisplayRate.Trim(), probability, Clean(BossDrop.Condition), calculatedEhb, Clean(BossDrop.DataSource), now); existing.SetActive(true);
        existing.SetRateMechanics(DropProbabilityScope.Participant, false, null, 1, rolls, BossDrop.RollGroup);
        BossId = boss.Id;
        return await SaveAsync("catalogue.drop_created", "source_drop", existing.Id, $"{boss.Name}: {item.Name}", "{}", State(existing), $"Added {item.Name} to {boss.Name}.", ct);
    }
    public async Task<IActionResult> OnPostToggleBossAsync(Guid recordId, long expectedVersion, CancellationToken ct) { var entity = await dbContext.BossActivities.SingleAsync(x => x.Id == recordId, ct); if (entity.Version != expectedVersion) return Stale(); var before = State(entity); entity.SetActive(!entity.Active); return await SaveAsync("catalogue.boss_toggled", "boss_activity", entity.Id, entity.Name, before, State(entity), $"{entity.Name} {(entity.Active ? "reactivated" : "deactivated")}.", ct); }
    public async Task<IActionResult> OnPostToggleDropAsync(Guid recordId, long expectedVersion, CancellationToken ct) { DropId = recordId; var entity = await dbContext.SourceDrops.SingleAsync(x => x.Id == recordId, ct); if (entity.Version != expectedVersion) return Stale(); var itemName = await dbContext.CatalogueItems.Where(x => x.Id == entity.ItemId).Select(x => x.Name).SingleAsync(ct); var before = State(entity); entity.SetActive(!entity.Active); return await SaveAsync("catalogue.drop_toggled", "source_drop", entity.Id, itemName, before, State(entity), $"{itemName} {(entity.Active ? "reactivated" : "deactivated")}.", ct); }
    public async Task<IActionResult> OnPostUpdateBossAsync(Guid recordId, long expectedVersion, string name, string category, decimal? efficientRate, string? dataSource, string? imageUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category)) return BadRequest();
        var entity = await dbContext.BossActivities.SingleAsync(x => x.Id == recordId, ct);
        if (entity.Version != expectedVersion) return Stale(); var before = State(entity); var cleanName = name.Trim();
        var otherNames = await dbContext.BossActivities.Where(x => x.Id != recordId).Select(x => x.Name).ToListAsync(ct);
        if (otherNames.Any(x => string.Equals(x, cleanName, StringComparison.OrdinalIgnoreCase))) { SetStatus(Localize("A boss or activity named {0} already exists.", cleanName), UiMessageType.Warning); return CataloguePage(); }
        entity.Update(cleanName, category.Trim(), efficientRate, entity.ExternalIdentifier, Clean(dataSource), entity.Notes, timeProvider.GetUtcNow(), OsrsWikiImageUrl.Normalize(imageUrl));
        var drops = await dbContext.SourceDrops.Where(x => x.BossActivityId == recordId).ToListAsync(ct);
        foreach (var drop in drops)
        {
            var effective = drop.EffectiveProbabilityPerCompletion();
            drop.Update(drop.DisplayRate, drop.NumericProbability, drop.RateConditionNote, efficientRate is > 0 && effective is > 0 ? 1 / (efficientRate.Value * effective.Value) : null, drop.DataSource, timeProvider.GetUtcNow());
        }
        return await SaveAsync("catalogue.boss_updated", "boss_activity", entity.Id, entity.Name, before, State(entity), $"{entity.Name} updated.", ct);
    }
    public async Task<IActionResult> OnPostUpdateDropAsync(Guid recordId, long expectedVersion, string itemName, string displayRate, string originalDisplayRate, decimal? numericProbability, decimal? originalNumericProbability, DropProbabilityScope probabilityScope, bool conditionalOnParent, decimal? parentProbability, int assumedParticipants, int rollsPerCompletion, string? rollGroup, string? dataSource, string? imageUrl, bool useExistingItem, CancellationToken ct)
    {
        DropId = recordId;
        if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(displayRate) || numericProbability is <= 0 or > 1) return BadRequest();
        var entity = await dbContext.SourceDrops.SingleAsync(x => x.Id == recordId, ct);
        if (entity.Version != expectedVersion) return Stale(); var before = State(entity); var item = await dbContext.CatalogueItems.SingleAsync(x => x.Id == entity.ItemId, ct);
        var cleanItemName = itemName.Trim();
        var normalizedItemName = cleanItemName.ToUpperInvariant();
        var matchingItem = await dbContext.CatalogueItems.SingleOrDefaultAsync(x => x.Id != item.Id && x.NormalizedName == normalizedItemName, ct);
        if (matchingItem is not null && !useExistingItem)
        {
            SetStatus(Localize("An item named {0} already exists. Confirm that you want to use the shared item.", cleanItemName), UiMessageType.Warning);
            return CataloguePage();
        }
        if (matchingItem is not null && await dbContext.SourceDrops.AnyAsync(x => x.Id != entity.Id && x.BossActivityId == entity.BossActivityId && x.ItemId == matchingItem.Id, ct))
        {
            SetStatus(Localize("This boss already has a drop named {0}.", cleanItemName), UiMessageType.Warning);
            return CataloguePage();
        }
        var cleanDisplayRate = displayRate.Trim();
        var displayRateChanged = !string.Equals(cleanDisplayRate, originalDisplayRate.Trim(), StringComparison.Ordinal);
        var numericProbabilityChanged = numericProbability != originalNumericProbability;
        var parsedRate = DropRateParser.TryParse(cleanDisplayRate); var parsedProbability = parsedRate?.ProbabilityPerRoll;
        var probability = numericProbabilityChanged
            ? numericProbability
            : displayRateChanged && parsedProbability is > 0
                ? parsedProbability
                : numericProbability ?? parsedProbability;
        if (parsedRate?.ExplicitMultipleRolls == true && rollsPerCompletion == 1) rollsPerCompletion = parsedRate.RollsPerCompletion;
        if (rollsPerCompletion < 1) return BadRequest();
        var effectiveProbability = SourceDrop.CalculateProbabilityPerCompletion(probability, rollsPerCompletion);
        var bossRate = await dbContext.BossActivities.Where(x => x.Id == entity.BossActivityId).Select(x => x.EfficientCompletionsPerHour).SingleAsync(ct); decimal? calculatedEhb = bossRate is > 0 && effectiveProbability is > 0 ? 1 / (bossRate.Value * effectiveProbability.Value) : null;
        entity.Update(cleanDisplayRate, probability, entity.RateConditionNote, calculatedEhb, Clean(dataSource), timeProvider.GetUtcNow());
        entity.SetRateMechanics(DropProbabilityScope.Participant, false, null, 1, rollsPerCompletion, rollGroup);
        if (matchingItem is null)
        {
            item.Update(cleanItemName, normalizedItemName, item.ExternalIdentifier, item.Notes, OsrsWikiImageUrl.Normalize(imageUrl));
        }
        else
        {
            entity.ChangeItem(matchingItem.Id);
            matchingItem.SetActive(true);
            if (!string.IsNullOrWhiteSpace(imageUrl)) matchingItem.Update(matchingItem.Name, matchingItem.NormalizedName, matchingItem.ExternalIdentifier, matchingItem.Notes, OsrsWikiImageUrl.Normalize(imageUrl));
            if (!await dbContext.SourceDrops.AnyAsync(x => x.Id != entity.Id && x.ItemId == item.Id, ct)) dbContext.CatalogueItems.Remove(item);
        }
        return await SaveAsync("catalogue.drop_updated", "source_drop", entity.Id, entity.DisplayRate, before, State(entity), "Drop details updated.", ct);
    }
    public async Task<IActionResult> OnPostDeleteAsync(string recordType, Guid recordId, long expectedVersion, string confirmation, CancellationToken ct)
    {
        if (!User.IsInRole("SuperAdmin")) return Forbid();
        if (!string.Equals(confirmation, "DELETE", StringComparison.Ordinal)) { SetStatus(Localize("Type DELETE to permanently remove an unused catalogue record."), UiMessageType.Warning); return CataloguePage(); }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
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
                SetStatus(prepared?.Status == DeletePreparationStatus.Referenced
                    ? Localize("This record is referenced and cannot be permanently deleted. Deactivate it instead.")
                    : Localize("This record was changed by another administrator. Current values are shown; review them before trying again."), UiMessageType.Warning);
                return CataloguePage();
            }
            var candidate = prepared.Candidate!;
            await dbContext.SaveChangesAsync(ct);
            dbContext.AuditEntries.Add(CreateAudit(candidate.Action, candidate.Type, candidate.Id, candidate.Details, candidate.Before, "{}"));
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            SetStatus(Localize("Unused catalogue record permanently deleted."), UiMessageType.Success);
        }
        catch (Exception exception) when (IsExpectedCatalogueRace(exception))
        {
            await transaction.RollbackAsync(ct);
            SetStatus(Localize("This record was changed by another administrator or became referenced. It was not deleted; review the current catalogue and deactivate it instead if needed."), UiMessageType.Warning);
        }
        return CataloguePage();
    }
    private async Task<DeletePreparation> PrepareBossDeletionAsync(Guid id, long version, CancellationToken ct)
    {
        var entity = await dbContext.BossActivities.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null || entity.Version != version) return new(DeletePreparationStatus.Stale, null);
        var referenced = await dbContext.SourceDrops.AnyAsync(x => x.BossActivityId == id, ct)
            || await dbContext.TemplateRequirementBosses.AnyAsync(x => x.BossActivityId == id, ct)
            || await dbContext.BoardRequirementBossSnapshots.AnyAsync(x => x.BossActivityId == id, ct)
            || await dbContext.BoardApprovalRequirementBossSnapshots.AnyAsync(x => x.BossActivityId == id, ct);
        if (referenced) return new(DeletePreparationStatus.Referenced, null);
        var candidate = new DeleteCandidate("catalogue.boss_deleted", "boss_activity", id, entity.Name, State(entity));
        dbContext.BossActivities.Remove(entity);
        return new(DeletePreparationStatus.Ready, candidate);
    }
    private async Task<DeletePreparation> PrepareDropDeletionAsync(Guid id, long version, CancellationToken ct)
    {
        var entity = await dbContext.SourceDrops.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null || entity.Version != version) return new(DeletePreparationStatus.Stale, null);
        var referenced = await dbContext.TemplateRequirementDrops.AnyAsync(x => x.SourceDropId == id, ct)
            || await dbContext.BoardRequirementDropSnapshots.AnyAsync(x => x.SourceDropId == id, ct)
            || await dbContext.BoardApprovalRequirementDropSnapshots.AnyAsync(x => x.SourceDropId == id, ct);
        if (referenced) return new(DeletePreparationStatus.Referenced, null);
        var candidate = new DeleteCandidate("catalogue.drop_deleted", "source_drop", id, entity.DisplayRate, State(entity));
        dbContext.SourceDrops.Remove(entity);
        return new(DeletePreparationStatus.Ready, candidate);
    }
    private async Task LoadAsync(CancellationToken ct)
    {
        var bosses = await dbContext.BossActivities.AsNoTracking().OrderByDescending(x => x.Active).ThenBy(x => x.Name).Select(x => new BossRow(x.Id, x.Name, x.Category, x.EfficientCompletionsPerHour, x.Active, x.ImageUrl, x.DataSource, x.DataUpdatedAt, x.Version)).ToListAsync(ct);
        Bosses = bosses.Select(x => x with { ImageUrl = OsrsWikiImageUrl.Normalize(x.ImageUrl) }).ToList();

        var drops = await (from d in dbContext.SourceDrops.AsNoTracking() join i in dbContext.CatalogueItems on d.ItemId equals i.Id select new DropRow(d.Id, d.BossActivityId, i.Name, d.DisplayRate, d.NumericProbability, d.DefaultEhbEstimate, d.ProbabilityScope, d.ConditionalOnParent, d.ParentProbability, d.AssumedParticipants, d.RollsPerCompletion, d.RollGroup, d.Active, d.DataSource, i.ImageUrl, d.DataUpdatedAt, d.Version)).ToListAsync(ct);
        Drops = drops.Select(x => x with
        {
            ImageUrl = OsrsWikiImageUrl.Normalize(x.ImageUrl)
        }).ToList();
    }
    private async Task<string> UniqueSlug(string name, CancellationToken ct) { var root = EventSlugGenerator.Generate(name); var slug = root; for (var n = 2; await dbContext.BossActivities.AnyAsync(x => x.Slug == slug, ct); n++) slug = $"{root}-{n}"; return slug; }
    private async Task<IActionResult> SaveAsync(string action, string type, Guid id, string details, string before, string after, string message, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await dbContext.SaveChangesAsync(ct);
            dbContext.AuditEntries.Add(CreateAudit(action, type, id, details, before, after));
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            SetStatus(message, UiMessageType.Success);
            return CataloguePage();
        }
        catch (Exception exception) when (IsExpectedCatalogueRace(exception))
        {
            await transaction.RollbackAsync(ct);
            return Stale();
        }
    }
    private RedirectToPageResult Stale() { SetStatus(Localize("This record was changed by another administrator. Current values are shown; review them before saving."), UiMessageType.Warning); return CataloguePage(); }
    private RedirectToPageResult CataloguePage() => RedirectToPage(null, new { bossId = BossId, dropId = DropId, addBoss = AddBoss ? "true" : null, overlay = IsOverlay ? "1" : null });
    private static string State(object entity) => JsonSerializer.Serialize(entity);
    private AuditEntry CreateAudit(string action, string type, Guid id, string details, string before, string after) => new(Guid.NewGuid(), timeProvider.GetUtcNow(), User.GetAccountId(), User.Identity!.Name!, action, type, id.ToString(), details, beforeState: before, afterState: after);
    private static bool IsExpectedCatalogueRace(Exception exception) => exception is DbUpdateConcurrencyException || exception is DbUpdateException { InnerException: PostgresException { SqlState: "23503" or "23505" or "40001" or "40P01" } } || exception is PostgresException { SqlState: "23503" or "23505" or "40001" or "40P01" };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    public sealed class BossInput { [Required, StringLength(200)] public string Name { get; set; } = string.Empty; [Required] public string Category { get; set; } = "Boss"; [Range(0.0001, 100000), Display(Name = "Efficient completions per hour")] public decimal? EfficientRate { get; set; } [Display(Name = "Data source")] public string? DataSource { get; set; } [Url, Display(Name = "Image URL")] public string? ImageUrl { get; set; } public string? Notes { get; set; } }
    public sealed class BossDropInput { [Required] public Guid BossActivityId { get; set; } [Required, StringLength(200), Display(Name = "Item name")] public string ItemName { get; set; } = string.Empty; [Required, StringLength(200), Display(Name = "Displayed drop rate")] public string DisplayRate { get; set; } = string.Empty; [Range(0.000000000001, 1), Display(Name = "Numeric probability")] public decimal? NumericProbability { get; set; } public DropProbabilityScope ProbabilityScope { get; set; } = DropProbabilityScope.Participant; public bool ConditionalOnParent { get; set; } [Range(0.000000000001, 1)] public decimal? ParentProbability { get; set; } [Range(1, 100)] public int AssumedParticipants { get; set; } = 1; [Range(1, 100)] public int RollsPerCompletion { get; set; } = 1; [StringLength(120)] public string RollGroup { get; set; } = "default"; [StringLength(2000), Display(Name = "Condition or note")] public string? Condition { get; set; } [StringLength(300), Display(Name = "Data source")] public string? DataSource { get; set; } [Url, Display(Name = "Item image URL")] public string? ImageUrl { get; set; } }
    public sealed record BossRow(Guid Id, string Name, string Category, decimal? EfficientRate, bool Active, string? ImageUrl, string? DataSource, DateTimeOffset UpdatedAt, long Version);
    public sealed record DropRow(Guid Id, Guid BossActivityId, string ItemName, string DisplayRate, decimal? Probability, decimal? DefaultEhb, DropProbabilityScope ProbabilityScope, bool ConditionalOnParent, decimal? ParentProbability, int AssumedParticipants, int RollsPerCompletion, string RollGroup, bool Active, string? DataSource, string? ImageUrl, DateTimeOffset UpdatedAt, long Version);
    private enum DeletePreparationStatus { Ready, Stale, Referenced }
    private sealed record DeletePreparation(DeletePreparationStatus Status, DeleteCandidate? Candidate);
    private sealed record DeleteCandidate(string Action, string Type, Guid Id, string Details, string Before);
}
