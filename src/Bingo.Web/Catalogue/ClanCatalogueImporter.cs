using System.Globalization;
using System.Text;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Catalogue;

public sealed class ClanCatalogueImporter(ApplicationDbContext db, TimeProvider time)
{
    private const string DataSource = "Clan point-math catalogue";
    private static readonly Dictionary<string, string> CanonicalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kalphite queen"] = "Kalphite Queen", ["Grotesque guardians"] = "Grotesque Guardians", ["Abyssal sire"] = "Abyssal Sire",
        ["Thermy smoke devil"] = "Thermonuclear Smoke Devil", ["Hydra"] = "Alchemical Hydra", ["Chaos elemental"] = "Chaos Elemental",
        ["King black dragon"] = "King Black Dragon", ["Vetion"] = "Vet'ion", ["Bandos"] = "General Graardor",
        ["Armadyl"] = "Kree'Arra", ["Saradomin"] = "Commander Zilyana", ["Zamorak"] = "K'ril Tsutsaroth", ["Gauntlet"] = "The Gauntlet", ["COX"] = "Chambers of Xeric",
        ["CM"] = "Chambers of Xeric (CM)", ["TOB"] = "Theatre of Blood", ["HM"] = "Theatre of Blood (HM)", ["TOA"] = "Tombs of Amascut",
        ["duke"] = "Duke Sucellus", ["Leviathan"] = "The Leviathan", ["Whisperer"] = "The Whisperer", ["Royal Titans"] = "The Royal Titans",
        ["Colosseum"] = "Sol Heredit", ["Delve"] = "Doom of Mokhaiotl", ["Maggot king"] = "Maggot King"
    };

    public async Task<ImportResult> ImportAsync(string path, CancellationToken ct = default)
    {
        var records = ParseCsv(await File.ReadAllTextAsync(path, ct)); if (records.Count < 2) throw new InvalidOperationException("The clan catalogue CSV is empty.");
        var headers = records[0].Select((value, index) => (value, index)).ToDictionary(x => x.value.Trim(), x => x.index, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "Boss", "Points", "Drop rate", "EHB" }) if (!headers.ContainsKey(required)) throw new InvalidOperationException($"Missing CSV column: {required}.");
        string? currentSection = null; decimal? currentEhb = null; var sections = new List<Section>(); var skipped = new List<string>();
        foreach (var values in records.Skip(1))
        {
            var name = Get(values, headers, "Boss"); if (string.IsNullOrWhiteSpace(name)) continue; var points = Get(values, headers, "Points"); var rate = Get(values, headers, "Drop rate"); var ehbText = Get(values, headers, "EHB");
            if (string.IsNullOrWhiteSpace(points) && string.IsNullOrWhiteSpace(rate)) { currentSection = name.Trim(); currentEhb = null; sections.Add(new Section(currentSection, [])); continue; }
            if (currentSection is null) { skipped.Add(name); continue; }
            if (currentSection.Equals("Armadyl", StringComparison.OrdinalIgnoreCase) && name.Equals("Crossbow", StringComparison.OrdinalIgnoreCase)) { currentSection = "Saradomin"; currentEhb = null; sections.Add(new Section(currentSection, [])); }
            var denominator = ParseDecimal(rate); var rowEhb = ParseDecimal(ehbText); if (rowEhb is > 0) currentEhb = rowEhb;
            sections[^1].Drops.Add(new DropRow(name.Trim(), denominator)); sections[^1].EfficientRate ??= currentEhb;
        }

        sections = ExpandCombinedSections(sections); var importedBosses = 0; var importedDrops = 0;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var section in sections)
        {
            if (section.Name.Equals("Miscellaneous", StringComparison.OrdinalIgnoreCase)) { skipped.AddRange(section.Drops.Select(x => x.Name)); continue; }
            var canonicalName = CanonicalNames.GetValueOrDefault(section.Name, section.Name); var category = canonicalName.Equals("Zalcano", StringComparison.OrdinalIgnoreCase) ? "Skilling boss" : "Boss"; var boss = await FindBossAsync(canonicalName, ct);
            if (boss is null) { var slug = await UniqueSlugAsync(canonicalName, ct); boss = new BossActivity(Guid.NewGuid(), canonicalName, slug, category, section.EfficientRate, time.GetUtcNow()); db.BossActivities.Add(boss); importedBosses++; }
            boss.Update(boss.Name, category, section.EfficientRate ?? boss.EfficientCompletionsPerHour, boss.ExternalIdentifier, DataSource, boss.Notes, time.GetUtcNow(), boss.ImageUrl); boss.SetActive(true);
            foreach (var sourceRow in section.Drops)
            {
                if (sourceRow.Denominator is not > 0) { skipped.Add($"{section.Name}: {sourceRow.Name}"); continue; }
                var normalizedName = sourceRow.Name.ToUpperInvariant(); var item = await db.CatalogueItems.SingleOrDefaultAsync(x => x.NormalizedName == normalizedName, ct) ?? db.CatalogueItems.Local.SingleOrDefault(x => x.NormalizedName == normalizedName);
                if (item is null) { item = new CatalogueItem(Guid.NewGuid(), sourceRow.Name, normalizedName); db.CatalogueItems.Add(item); } item.SetActive(true);
                var existing = await db.SourceDrops.SingleOrDefaultAsync(x => x.BossActivityId == boss.Id && x.ItemId == item.Id, ct) ?? db.SourceDrops.Local.SingleOrDefault(x => x.BossActivityId == boss.Id && x.ItemId == item.Id);
                var probability = 1m / sourceRow.Denominator.Value; decimal? dropEhb = boss.EfficientCompletionsPerHour is > 0 ? 1m / (boss.EfficientCompletionsPerHour.Value * probability) : null; var displayRate = $"1/{sourceRow.Denominator.Value.ToString("0.####", CultureInfo.InvariantCulture)}";
                if (existing is null) { existing = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, displayRate, probability, dropEhb, time.GetUtcNow()); db.SourceDrops.Add(existing); importedDrops++; }
                existing.Update(displayRate, probability, null, dropEhb, DataSource, time.GetUtcNow()); existing.SetActive(true);
            }
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct); return new(importedBosses, importedDrops, skipped);
    }

    private async Task<BossActivity?> FindBossAsync(string name, CancellationToken ct) { var byName = await db.BossActivities.SingleOrDefaultAsync(x => x.Name == name, ct) ?? db.BossActivities.Local.SingleOrDefault(x => x.Name == name); if (byName is not null) return byName; var slug = EventSlugGenerator.Generate(name); return await db.BossActivities.SingleOrDefaultAsync(x => x.Slug == slug, ct) ?? db.BossActivities.Local.SingleOrDefault(x => x.Slug == slug); }
    private static List<Section> ExpandCombinedSections(List<Section> sections)
    {
        var expanded = new List<Section>();
        foreach (var section in sections)
        {
            if (section.Name.Equals("Artio/Calisto", StringComparison.OrdinalIgnoreCase)) { expanded.Add(new Section("Artio", [.. section.Drops]) { EfficientRate = section.EfficientRate }); expanded.Add(new Section("Callisto", [.. section.Drops]) { EfficientRate = section.EfficientRate }); continue; }
            if (section.Name.Equals("Venenatis", StringComparison.OrdinalIgnoreCase)) { expanded.Add(section); expanded.Add(new Section("Spindel", [.. section.Drops]) { EfficientRate = section.EfficientRate }); continue; }
            if (section.Name.Equals("Vetion", StringComparison.OrdinalIgnoreCase)) { expanded.Add(section); expanded.Add(new Section("Calvar'ion", [.. section.Drops]) { EfficientRate = section.EfficientRate }); continue; }
            if (!section.Name.Equals("Dagganoth Kings", StringComparison.OrdinalIgnoreCase)) { expanded.Add(section); continue; }
            foreach (var group in section.Drops.GroupBy(drop => drop.Name switch { "Archers ring" => "Dagannoth Supreme", "Seers ring" => "Dagannoth Prime", _ => "Dagannoth Rex" })) expanded.Add(new Section(group.Key, group.ToList()) { EfficientRate = section.EfficientRate });
        }
        return expanded;
    }
    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct) { var root = EventSlugGenerator.Generate(name); var slug = root; for (var index = 2; await db.BossActivities.AnyAsync(x => x.Slug == slug, ct) || db.BossActivities.Local.Any(x => x.Slug == slug); index++) slug = $"{root}-{index}"; return slug; }
    private static decimal? ParseDecimal(string value) => decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static string Get(List<string> values, Dictionary<string, int> headers, string name) => headers.TryGetValue(name, out var index) && index < values.Count ? values[index].Trim() : string.Empty;
    private static List<List<string>> ParseCsv(string text) { var records = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder(); var quoted = false; for (var index = 0; index < text.Length; index++) { var character = text[index]; if (character == '"') { if (quoted && index + 1 < text.Length && text[index + 1] == '"') { field.Append('"'); index++; } else quoted = !quoted; } else if (character == ',' && !quoted) { row.Add(field.ToString()); field.Clear(); } else if ((character == '\n' || character == '\r') && !quoted) { if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++; row.Add(field.ToString()); field.Clear(); if (row.Any(value => value.Length > 0)) records.Add(row); row = []; } else field.Append(character); } row.Add(field.ToString()); if (row.Any(value => value.Length > 0)) records.Add(row); return records; }

    private sealed record Section(string Name, List<DropRow> Drops) { public decimal? EfficientRate { get; set; } }
    private sealed record DropRow(string Name, decimal? Denominator);
    public sealed record ImportResult(int BossesCreated, int DropsCreated, IReadOnlyList<string> Skipped);
}
