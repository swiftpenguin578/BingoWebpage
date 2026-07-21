using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Catalogue;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Catalogue;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext, IAuditWriter auditWriter, TimeProvider timeProvider) : PageModel
{
    [BindProperty] public BossInput Boss { get; set; } = new(); [BindProperty] public BossDropInput BossDrop { get; set; } = new();
    public IReadOnlyList<BossRow> Bosses { get; private set; } = []; public IReadOnlyList<DropRow> Drops { get; private set; } = [];
    public Task OnGetAsync(CancellationToken ct) => LoadAsync(ct);
    public async Task<IActionResult> OnPostBossAsync(CancellationToken ct)
    {
        ModelState.Clear();
        if (!TryValidateModel(Boss, nameof(Boss)))
        {
            SetStatus(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), UiMessageType.Error);
            return RedirectToPage();
        }

        var name = Boss.Name.Trim();
        var existingNames = await dbContext.BossActivities.Select(x => x.Name).ToListAsync(ct);
        if (existingNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus($"A boss or activity named {name} already exists.", UiMessageType.Warning);
            return RedirectToPage();
        }

        var slug = await UniqueSlug(name, ct);
        var entity = new BossActivity(Guid.NewGuid(), name, slug, Boss.Category, Boss.EfficientRate, timeProvider.GetUtcNow());
        entity.Update(name, Boss.Category, Boss.EfficientRate, null, Clean(Boss.DataSource), Clean(Boss.Notes), timeProvider.GetUtcNow(), OsrsWikiImageUrl.Normalize(Boss.ImageUrl));
        dbContext.BossActivities.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        await Audit("catalogue.boss_created", "boss_activity", entity.Id, entity.Name, ct);
        SetStatus($"{entity.Name} added to the catalogue.", UiMessageType.Success);
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostBossDropAsync(CancellationToken ct)
    {
        ModelState.Clear();
        if (!TryValidateModel(BossDrop, nameof(BossDrop))) { SetStatus(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), UiMessageType.Error); return RedirectToPage(); }
        var boss = await dbContext.BossActivities.SingleOrDefaultAsync(x => x.Id == BossDrop.BossActivityId, ct); if (boss is null) return NotFound();
        var itemName = BossDrop.ItemName.Trim(); var normalizedName = itemName.ToUpperInvariant(); var item = await dbContext.CatalogueItems.SingleOrDefaultAsync(x => x.NormalizedName == normalizedName, ct);
        if (item is null) { item = new CatalogueItem(Guid.NewGuid(), itemName, normalizedName); item.Update(itemName, normalizedName, null, null, OsrsWikiImageUrl.Normalize(BossDrop.ImageUrl)); dbContext.CatalogueItems.Add(item); }
        else { item.SetActive(true); if (!string.IsNullOrWhiteSpace(BossDrop.ImageUrl)) item.Update(item.Name, item.NormalizedName, item.ExternalIdentifier, item.Notes, OsrsWikiImageUrl.Normalize(BossDrop.ImageUrl)); }
        var existing = await dbContext.SourceDrops.SingleOrDefaultAsync(x => x.BossActivityId == boss.Id && x.ItemId == item.Id, ct);
        if (existing?.Active == true) { SetStatus($"{item.Name} is already listed for {boss.Name}.", UiMessageType.Warning); return RedirectToPage(); }
        var parsedRate = DropRateParser.TryParse(BossDrop.DisplayRate); var rolls = parsedRate?.ExplicitMultipleRolls == true && BossDrop.RollsPerCompletion == 1 ? parsedRate.RollsPerCompletion : BossDrop.RollsPerCompletion; var probability = BossDrop.NumericProbability ?? parsedRate?.ProbabilityPerRoll; var effectiveProbability = EffectiveProbability(probability, rolls); var now = timeProvider.GetUtcNow(); decimal? calculatedEhb = boss.EfficientCompletionsPerHour is > 0 && effectiveProbability is > 0 ? 1 / (boss.EfficientCompletionsPerHour.Value * effectiveProbability.Value) : null;
        if (existing is null) { existing = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, BossDrop.DisplayRate.Trim(), probability, calculatedEhb, now); dbContext.SourceDrops.Add(existing); }
        existing.Update(BossDrop.DisplayRate.Trim(), probability, Clean(BossDrop.Condition), calculatedEhb, Clean(BossDrop.DataSource), now); existing.SetActive(true);
        existing.SetRateMechanics(DropProbabilityScope.Participant, false, null, 1, rolls, BossDrop.RollGroup);
        var existingVariants = await dbContext.SourceDropRateVariants.Where(x => x.SourceDropId == existing.Id).ToListAsync(ct);
        dbContext.SourceDropRateVariants.RemoveRange(existingVariants);
        dbContext.SourceDropRateVariants.Add(new SourceDropRateVariant(Guid.NewGuid(), existing.Id, 1, "Default", BossDrop.DisplayRate.Trim(), probability, Clean(BossDrop.Condition)));
        await dbContext.SaveChangesAsync(ct); await Audit("catalogue.drop_created", "source_drop", existing.Id, $"{boss.Name}: {item.Name}", ct); SetStatus($"Added {item.Name} to {boss.Name}.", UiMessageType.Success); return RedirectToPage();
    }
    public async Task<IActionResult> OnPostToggleBossAsync(Guid recordId, CancellationToken ct) { var entity = await dbContext.BossActivities.SingleAsync(x => x.Id == recordId, ct); entity.SetActive(!entity.Active); await dbContext.SaveChangesAsync(ct); await Audit("catalogue.boss_toggled", "boss_activity", entity.Id, entity.Active.ToString(), ct); SetStatus($"{entity.Name} {(entity.Active ? "reactivated" : "deactivated")}.", UiMessageType.Success); return RedirectToPage(); }
    public async Task<IActionResult> OnPostToggleDropAsync(Guid recordId, CancellationToken ct) { var entity = await dbContext.SourceDrops.SingleAsync(x => x.Id == recordId, ct); var itemName = await dbContext.CatalogueItems.Where(x => x.Id == entity.ItemId).Select(x => x.Name).SingleAsync(ct); entity.SetActive(!entity.Active); await dbContext.SaveChangesAsync(ct); await Audit("catalogue.drop_toggled", "source_drop", entity.Id, entity.Active.ToString(), ct); SetStatus($"{itemName} {(entity.Active ? "reactivated" : "deactivated")}.", UiMessageType.Success); return RedirectToPage(); }
    public async Task<IActionResult> OnPostUpdateBossAsync(Guid recordId, string name, string category, decimal? efficientRate, string? dataSource, string? imageUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category)) return BadRequest();
        var entity = await dbContext.BossActivities.SingleAsync(x => x.Id == recordId, ct);
        var cleanName = name.Trim();
        var otherNames = await dbContext.BossActivities.Where(x => x.Id != recordId).Select(x => x.Name).ToListAsync(ct);
        if (otherNames.Any(x => string.Equals(x, cleanName, StringComparison.OrdinalIgnoreCase))) { SetStatus($"A boss or activity named {cleanName} already exists.", UiMessageType.Warning); return RedirectToPage(); }
        entity.Update(cleanName, category.Trim(), efficientRate, entity.ExternalIdentifier, Clean(dataSource), entity.Notes, timeProvider.GetUtcNow(), OsrsWikiImageUrl.Normalize(imageUrl));
        var drops = await dbContext.SourceDrops.Where(x => x.BossActivityId == recordId).ToListAsync(ct);
        foreach (var drop in drops)
        {
            var effective = drop.EffectiveProbabilityPerRoll();
            var expectedPerCompletion = effective * drop.RollsPerCompletion;
            drop.Update(drop.DisplayRate, drop.NumericProbability, drop.RateConditionNote, efficientRate is > 0 && expectedPerCompletion is > 0 ? 1 / (efficientRate.Value * expectedPerCompletion.Value) : null, drop.DataSource, timeProvider.GetUtcNow());
        }
        await dbContext.SaveChangesAsync(ct); await Audit("catalogue.boss_updated", "boss_activity", entity.Id, entity.Name, ct); SetStatus($"{entity.Name} updated.", UiMessageType.Success); return RedirectToPage();
    }
    public async Task<IActionResult> OnPostUpdateDropAsync(Guid recordId, string itemName, string displayRate, string originalDisplayRate, decimal? numericProbability, decimal? originalNumericProbability, DropProbabilityScope probabilityScope, bool conditionalOnParent, decimal? parentProbability, int assumedParticipants, int rollsPerCompletion, string? rollGroup, string? dataSource, string? imageUrl, bool useExistingItem, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(displayRate) || numericProbability is <= 0 or > 1) return BadRequest();
        var entity = await dbContext.SourceDrops.SingleAsync(x => x.Id == recordId, ct);
        var item = await dbContext.CatalogueItems.SingleAsync(x => x.Id == entity.ItemId, ct);
        var cleanItemName = itemName.Trim();
        var normalizedItemName = cleanItemName.ToUpperInvariant();
        var matchingItem = await dbContext.CatalogueItems.SingleOrDefaultAsync(x => x.Id != item.Id && x.NormalizedName == normalizedItemName, ct);
        if (matchingItem is not null && !useExistingItem)
        {
            SetStatus($"An item named {cleanItemName} already exists. Confirm that you want to use the shared item.", UiMessageType.Warning);
            return RedirectToPage();
        }
        if (matchingItem is not null && await dbContext.SourceDrops.AnyAsync(x => x.Id != entity.Id && x.BossActivityId == entity.BossActivityId && x.ItemId == matchingItem.Id, ct))
        {
            SetStatus($"This boss already has a drop named {cleanItemName}.", UiMessageType.Warning);
            return RedirectToPage();
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
        var effectiveProbability = EffectiveProbability(probability, rollsPerCompletion);
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
        var rateVariants = await dbContext.SourceDropRateVariants.Where(x => x.SourceDropId == entity.Id).OrderBy(x => x.Position).ToListAsync(ct);
        if (rateVariants.Count <= 1)
        {
            if (rateVariants.Count == 0) dbContext.SourceDropRateVariants.Add(new SourceDropRateVariant(Guid.NewGuid(), entity.Id, 1, "Default", cleanDisplayRate, probability, entity.RateConditionNote));
            else rateVariants[0].Update(1, rateVariants[0].Label, cleanDisplayRate, probability, entity.RateConditionNote);
        }
        await dbContext.SaveChangesAsync(ct); await Audit("catalogue.drop_updated", "source_drop", entity.Id, entity.DisplayRate, ct); SetStatus("Drop details updated.", UiMessageType.Success); return RedirectToPage();
    }
    private async Task LoadAsync(CancellationToken ct)
    {
        var bosses = await dbContext.BossActivities.AsNoTracking().OrderByDescending(x => x.Active).ThenBy(x => x.Name).Select(x => new BossRow(x.Id, x.Name, x.Category, x.EfficientCompletionsPerHour, x.Active, x.ImageUrl, x.DataSource, x.DataUpdatedAt)).ToListAsync(ct);
        Bosses = bosses.Select(x => x with { ImageUrl = OsrsWikiImageUrl.Normalize(x.ImageUrl) }).ToList();

        var drops = await (from d in dbContext.SourceDrops.AsNoTracking() join i in dbContext.CatalogueItems on d.ItemId equals i.Id select new DropRow(d.Id, d.BossActivityId, i.Name, d.DisplayRate, d.NumericProbability, d.DefaultEhbEstimate, d.ProbabilityScope, d.ConditionalOnParent, d.ParentProbability, d.AssumedParticipants, d.RollsPerCompletion, d.RollGroup, d.Active, d.DataSource, i.ImageUrl, d.DataUpdatedAt, Array.Empty<RateVariantRow>())).ToListAsync(ct);
        var variants = await dbContext.SourceDropRateVariants.AsNoTracking()
            .OrderBy(x => x.Position)
            .Select(x => new RateVariantRow(x.SourceDropId, x.Position, x.Label, x.DisplayRate, x.NumericProbability, x.Condition))
            .ToListAsync(ct);
        var variantsByDrop = variants.ToLookup(x => x.SourceDropId);
        Drops = drops.Select(x => x with
        {
            ImageUrl = OsrsWikiImageUrl.Normalize(x.ImageUrl),
            RateVariants = variantsByDrop[x.Id].ToList()
        }).ToList();
    }
    private async Task<string> UniqueSlug(string name, CancellationToken ct) { var root = EventSlugGenerator.Generate(name); var slug = root; for (var n = 2; await dbContext.BossActivities.AnyAsync(x => x.Slug == slug, ct); n++) slug = $"{root}-{n}"; return slug; }
    private static decimal? EffectiveProbability(decimal? raw, int rolls) { if (raw is not > 0 || rolls < 1) return null; var value = raw.Value * rolls; return value is > 0 and <= 1 ? value : null; }
    private Task Audit(string action, string type, Guid id, string details, CancellationToken ct) => auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, type, id.ToString(), details, ct); private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    public sealed class BossInput { [Required, StringLength(200)] public string Name { get; set; } = string.Empty; [Required] public string Category { get; set; } = "Boss"; [Range(0.0001, 100000), Display(Name = "Efficient completions per hour")] public decimal? EfficientRate { get; set; } [Display(Name = "Data source")] public string? DataSource { get; set; } [Url, Display(Name = "Image URL")] public string? ImageUrl { get; set; } public string? Notes { get; set; } }
    public sealed class BossDropInput { [Required] public Guid BossActivityId { get; set; } [Required, StringLength(200), Display(Name = "Item name")] public string ItemName { get; set; } = string.Empty; [Required, StringLength(200), Display(Name = "Displayed drop rate")] public string DisplayRate { get; set; } = string.Empty; [Range(0.000000000001, 1), Display(Name = "Numeric probability")] public decimal? NumericProbability { get; set; } public DropProbabilityScope ProbabilityScope { get; set; } = DropProbabilityScope.Participant; public bool ConditionalOnParent { get; set; } [Range(0.000000000001, 1)] public decimal? ParentProbability { get; set; } [Range(1, 100)] public int AssumedParticipants { get; set; } = 1; [Range(1, 100)] public int RollsPerCompletion { get; set; } = 1; [StringLength(120)] public string RollGroup { get; set; } = "default"; [StringLength(2000), Display(Name = "Condition or note")] public string? Condition { get; set; } [StringLength(300), Display(Name = "Data source")] public string? DataSource { get; set; } [Url, Display(Name = "Item image URL")] public string? ImageUrl { get; set; } }
    public sealed record BossRow(Guid Id, string Name, string Category, decimal? EfficientRate, bool Active, string? ImageUrl, string? DataSource, DateTimeOffset UpdatedAt);
    public sealed record DropRow(Guid Id, Guid BossActivityId, string ItemName, string DisplayRate, decimal? Probability, decimal? DefaultEhb, DropProbabilityScope ProbabilityScope, bool ConditionalOnParent, decimal? ParentProbability, int AssumedParticipants, int RollsPerCompletion, string RollGroup, bool Active, string? DataSource, string? ImageUrl, DateTimeOffset UpdatedAt, IReadOnlyList<RateVariantRow> RateVariants);
    public sealed record RateVariantRow(Guid SourceDropId, int Position, string Label, string DisplayRate, decimal? Probability, string? Condition);
}
