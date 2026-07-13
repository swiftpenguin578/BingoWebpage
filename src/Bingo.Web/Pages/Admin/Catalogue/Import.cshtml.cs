using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
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
public sealed class ImportModel(ApplicationDbContext db, IAuditWriter audit, TimeProvider time) : PageModel
{
    [BindProperty, Required, Display(Name = "Catalogue CSV")] public IFormFile? Upload { get; set; }
    public string RequiredHeader => "record_type,name,category,efficient_rate,source,item,display_rate,numeric_probability,default_ehb,data_source,image_url";

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Upload is null || Upload.Length == 0) { ModelState.AddModelError(nameof(Upload), "Choose a non-empty CSV file."); return Page(); }
        string text; await using (var stream = Upload.OpenReadStream()) using (var reader = new StreamReader(stream)) text = await reader.ReadToEndAsync(ct);
        var rows = ParseCsv(text); if (rows.Count < 2) { ModelState.AddModelError(string.Empty, "The CSV contains no data rows."); return Page(); }
        var headers = rows[0].Select((value, index) => (value: value.Trim().ToLowerInvariant(), index)).ToDictionary(x => x.value, x => x.index);
        foreach (var required in new[] { "record_type", "name" }) if (!headers.ContainsKey(required)) { ModelState.AddModelError(string.Empty, $"Missing required column: {required}."); return Page(); }
        var data = rows.Skip(1).Select((values, row) => new ImportRow(row + 2, values, headers)).ToList();
        var errors = new List<string>(); var imported = 0;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var row in data.Where(x => x.Type is "boss" or "activity"))
        {
            if (string.IsNullOrWhiteSpace(row.Name)) { errors.Add($"Row {row.Number}: boss/activity name is required."); continue; }
            var existing = await db.BossActivities.SingleOrDefaultAsync(x => x.Name == row.Name, ct);
            var rate = ParseDecimal(row["efficient_rate"], row.Number, "efficient_rate", errors); if (errors.Count > 0) continue;
            if (existing is null) { var slug = await UniqueSlug(row.Name, ct); existing = new BossActivity(Guid.NewGuid(), row.Name, slug, row["category"] ?? "Boss", rate, time.GetUtcNow()); db.BossActivities.Add(existing); }
            existing.Update(row.Name, row["category"] ?? "Boss", rate, existing.ExternalIdentifier, row["data_source"], existing.Notes, time.GetUtcNow(), row["image_url"]); imported++;
        }
        foreach (var row in data.Where(x => x.Type == "item"))
        {
            if (string.IsNullOrWhiteSpace(row.Name)) { errors.Add($"Row {row.Number}: item name is required."); continue; }
            var normalized = row.Name.ToUpperInvariant(); var existing = await db.CatalogueItems.SingleOrDefaultAsync(x => x.NormalizedName == normalized, ct);
            if (existing is null) { existing = new CatalogueItem(Guid.NewGuid(), row.Name, normalized); db.CatalogueItems.Add(existing); }
            existing.Update(row.Name, normalized, existing.ExternalIdentifier, existing.Notes, row["image_url"]); imported++;
        }
        await db.SaveChangesAsync(ct);
        foreach (var row in data.Where(x => x.Type == "drop"))
        {
            var sourceName = row["source"]; var itemName = row["item"]; var displayRate = row["display_rate"];
            var source = await db.BossActivities.SingleOrDefaultAsync(x => x.Name == sourceName, ct); var normalizedItemName = (itemName ?? string.Empty).ToUpperInvariant(); var item = await db.CatalogueItems.SingleOrDefaultAsync(x => x.NormalizedName == normalizedItemName, ct);
            if (source is null || item is null || string.IsNullOrWhiteSpace(displayRate)) { errors.Add($"Row {row.Number}: drop needs an existing source, item, and displayed rate."); continue; }
            var probability = ParseDecimal(row["numeric_probability"], row.Number, "numeric_probability", errors); var ehb = ParseDecimal(row["default_ehb"], row.Number, "default_ehb", errors); if (probability is <= 0 or > 1) errors.Add($"Row {row.Number}: numeric_probability must be greater than 0 and no more than 1."); if (errors.Count > 0) continue;
            var existing = await db.SourceDrops.SingleOrDefaultAsync(x => x.BossActivityId == source.Id && x.ItemId == item.Id, ct);
            if (existing is null) { existing = new SourceDrop(Guid.NewGuid(), source.Id, item.Id, displayRate, probability, ehb, time.GetUtcNow()); db.SourceDrops.Add(existing); }
            existing.Update(displayRate, probability, existing.RateConditionNote, ehb, row["data_source"], time.GetUtcNow()); imported++;
        }
        if (errors.Count > 0) { await transaction.RollbackAsync(ct); foreach (var error in errors) ModelState.AddModelError(string.Empty, error); return Page(); }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        await audit.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "catalogue.csv_imported", "catalogue", "initial", $"{imported} rows imported or updated", ct);
        TempData["StatusMessage"] = $"Imported or updated {imported} catalogue rows."; return RedirectToPage("Index");
    }

    private async Task<string> UniqueSlug(string name, CancellationToken ct) { var root = EventSlugGenerator.Generate(name); var slug = root; for (var n = 2; await db.BossActivities.AnyAsync(x => x.Slug == slug, ct); n++) slug = $"{root}-{n}"; return slug; }
    private static decimal? ParseDecimal(string? raw, int row, string column, List<string> errors) { if (string.IsNullOrWhiteSpace(raw)) return null; if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) return value; errors.Add($"Row {row}: {column} is not a valid number."); return null; }
    private static List<List<string>> ParseCsv(string text) { var records = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false; for (var i = 0; i < text.Length; i++) { var c = text[i]; if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { row.Add(field.ToString()); field.Clear(); } else if ((c == '\n' || c == '\r') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; row.Add(field.ToString()); field.Clear(); if (row.Any(v => v.Length > 0)) records.Add(row); row = []; } else field.Append(c); } row.Add(field.ToString()); if (row.Any(v => v.Length > 0)) records.Add(row); return records; }
    private sealed class ImportRow(int number, List<string> values, Dictionary<string, int> headers) { public int Number => number; public string Type => (this["record_type"] ?? "").Trim().ToLowerInvariant(); public string Name => (this["name"] ?? "").Trim(); public string? this[string name] => headers.TryGetValue(name, out var index) && index < values.Count && !string.IsNullOrWhiteSpace(values[index]) ? values[index].Trim() : null; }
}
