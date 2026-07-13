using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Catalogue;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
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
    public async Task<IActionResult> OnPostBossAsync(CancellationToken ct) { ModelState.Clear(); if (!TryValidateModel(Boss, nameof(Boss))) { await LoadAsync(ct); return Page(); } var slug = await UniqueSlug(Boss.Name, ct); var entity = new BossActivity(Guid.NewGuid(), Boss.Name.Trim(), slug, Boss.Category, Boss.EfficientRate, timeProvider.GetUtcNow()); entity.Update(Boss.Name.Trim(), Boss.Category, Boss.EfficientRate, null, Clean(Boss.DataSource), Clean(Boss.Notes), timeProvider.GetUtcNow(), Clean(Boss.ImageUrl)); dbContext.BossActivities.Add(entity); await dbContext.SaveChangesAsync(ct); await Audit("catalogue.boss_created", "boss_activity", entity.Id, entity.Name, ct); return RedirectToPage(); }
    public async Task<IActionResult> OnPostBossDropAsync(CancellationToken ct)
    {
        ModelState.Clear();
        if (!TryValidateModel(BossDrop, nameof(BossDrop))) { TempData["StatusMessage"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)); return RedirectToPage(); }
        var boss = await dbContext.BossActivities.SingleOrDefaultAsync(x => x.Id == BossDrop.BossActivityId, ct); if (boss is null) return NotFound();
        var itemName = BossDrop.ItemName.Trim(); var normalizedName = itemName.ToUpperInvariant(); var item = await dbContext.CatalogueItems.SingleOrDefaultAsync(x => x.NormalizedName == normalizedName, ct);
        if (item is null) { item = new CatalogueItem(Guid.NewGuid(), itemName, normalizedName); item.Update(itemName, normalizedName, null, null, Clean(BossDrop.ImageUrl)); dbContext.CatalogueItems.Add(item); }
        else { item.SetActive(true); if (!string.IsNullOrWhiteSpace(BossDrop.ImageUrl)) item.Update(item.Name, item.NormalizedName, item.ExternalIdentifier, item.Notes, Clean(BossDrop.ImageUrl)); }
        var existing = await dbContext.SourceDrops.SingleOrDefaultAsync(x => x.BossActivityId == boss.Id && x.ItemId == item.Id, ct);
        if (existing?.Active == true) { TempData["StatusMessage"] = $"{item.Name} is already listed for {boss.Name}."; return RedirectToPage(); }
        var probability = BossDrop.NumericProbability ?? DropRateParser.TryParseProbability(BossDrop.DisplayRate); var now = timeProvider.GetUtcNow(); decimal? calculatedEhb = boss.EfficientCompletionsPerHour is > 0 && probability is > 0 ? 1 / (boss.EfficientCompletionsPerHour.Value * probability.Value) : null;
        if (existing is null) { existing = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, BossDrop.DisplayRate.Trim(), probability, calculatedEhb, now); dbContext.SourceDrops.Add(existing); }
        existing.Update(BossDrop.DisplayRate.Trim(), probability, Clean(BossDrop.Condition), calculatedEhb, Clean(BossDrop.DataSource), now); existing.SetActive(true);
        await dbContext.SaveChangesAsync(ct); await Audit("catalogue.drop_created", "source_drop", existing.Id, $"{boss.Name}: {item.Name}", ct); TempData["StatusMessage"] = $"Added {item.Name} to {boss.Name}."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostToggleBossAsync(Guid recordId, CancellationToken ct) { var entity = await dbContext.BossActivities.SingleAsync(x => x.Id == recordId, ct); entity.SetActive(!entity.Active); await dbContext.SaveChangesAsync(ct); await Audit("catalogue.boss_toggled", "boss_activity", entity.Id, entity.Active.ToString(), ct); return RedirectToPage(); }
    public async Task<IActionResult> OnPostToggleDropAsync(Guid recordId, CancellationToken ct) { var entity = await dbContext.SourceDrops.SingleAsync(x => x.Id == recordId, ct); entity.SetActive(!entity.Active); await dbContext.SaveChangesAsync(ct); await Audit("catalogue.drop_toggled", "source_drop", entity.Id, entity.Active.ToString(), ct); return RedirectToPage(); }
    public async Task<IActionResult> OnPostUpdateBossAsync(Guid recordId, string name, string category, decimal? efficientRate, string? dataSource, string? imageUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category)) return BadRequest();
        var entity = await dbContext.BossActivities.SingleAsync(x => x.Id == recordId, ct);
        entity.Update(name.Trim(), category.Trim(), efficientRate, entity.ExternalIdentifier, Clean(dataSource), entity.Notes, timeProvider.GetUtcNow(), Clean(imageUrl));
        await dbContext.SaveChangesAsync(ct); await Audit("catalogue.boss_updated", "boss_activity", entity.Id, entity.Name, ct); return RedirectToPage();
    }
    public async Task<IActionResult> OnPostUpdateDropAsync(Guid recordId, string displayRate, decimal? numericProbability, string? dataSource, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(displayRate) || numericProbability is <= 0 or > 1) return BadRequest();
        var entity = await dbContext.SourceDrops.SingleAsync(x => x.Id == recordId, ct);
        var probability = numericProbability ?? DropRateParser.TryParseProbability(displayRate); var bossRate = await dbContext.BossActivities.Where(x => x.Id == entity.BossActivityId).Select(x => x.EfficientCompletionsPerHour).SingleAsync(ct); decimal? calculatedEhb = bossRate is > 0 && probability is > 0 ? 1 / (bossRate.Value * probability.Value) : null;
        entity.Update(displayRate.Trim(), probability, entity.RateConditionNote, calculatedEhb, Clean(dataSource), timeProvider.GetUtcNow()); await dbContext.SaveChangesAsync(ct); await Audit("catalogue.drop_updated", "source_drop", entity.Id, entity.DisplayRate, ct); return RedirectToPage();
    }
    private async Task LoadAsync(CancellationToken ct) { Bosses = await dbContext.BossActivities.AsNoTracking().OrderByDescending(x => x.Active).ThenBy(x => x.Name).Select(x => new BossRow(x.Id, x.Name, x.Category, x.EfficientCompletionsPerHour, x.Active, x.ImageUrl, x.DataSource, x.DataUpdatedAt)).ToListAsync(ct); Drops = await (from d in dbContext.SourceDrops.AsNoTracking() join i in dbContext.CatalogueItems on d.ItemId equals i.Id select new DropRow(d.Id, d.BossActivityId, i.Name, d.DisplayRate, d.NumericProbability, d.DefaultEhbEstimate, d.Active, d.DataSource, d.DataUpdatedAt)).ToListAsync(ct); }
    private async Task<string> UniqueSlug(string name, CancellationToken ct) { var root = EventSlugGenerator.Generate(name); var slug = root; for (var n = 2; await dbContext.BossActivities.AnyAsync(x => x.Slug == slug, ct); n++) slug = $"{root}-{n}"; return slug; }
    private Task Audit(string action, string type, Guid id, string details, CancellationToken ct) => auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, type, id.ToString(), details, ct); private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public sealed class BossInput { [Required, StringLength(200)] public string Name { get; set; } = string.Empty; [Required] public string Category { get; set; } = "Boss"; [Range(0.0001, 100000), Display(Name = "Efficient completions per hour")] public decimal? EfficientRate { get; set; } [Display(Name = "Data source")] public string? DataSource { get; set; } [Url, Display(Name = "Image URL")] public string? ImageUrl { get; set; } public string? Notes { get; set; } }
    public sealed class BossDropInput { [Required] public Guid BossActivityId { get; set; } [Required, StringLength(200), Display(Name = "Item name")] public string ItemName { get; set; } = string.Empty; [Required, StringLength(200), Display(Name = "Displayed drop rate")] public string DisplayRate { get; set; } = string.Empty; [Range(0.000000000001, 1), Display(Name = "Numeric probability")] public decimal? NumericProbability { get; set; } [StringLength(2000), Display(Name = "Condition or note")] public string? Condition { get; set; } [StringLength(300), Display(Name = "Data source")] public string? DataSource { get; set; } [Url, Display(Name = "Item image URL")] public string? ImageUrl { get; set; } }
    public sealed record BossRow(Guid Id, string Name, string Category, decimal? EfficientRate, bool Active, string? ImageUrl, string? DataSource, DateTimeOffset UpdatedAt); public sealed record DropRow(Guid Id, Guid BossActivityId, string ItemName, string DisplayRate, decimal? Probability, decimal? DefaultEhb, bool Active, string? DataSource, DateTimeOffset UpdatedAt);
}
