using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Catalogue;

public sealed class OsrsWikiCatalogueDryRunService(
    IHttpClientFactory httpClientFactory,
    ApplicationDbContext db) : IDisposable
{
    private const string WikiBaseUrl = "https://oldschool.runescape.wiki";
    private const int MaxWikiRequestAttempts = 6;
    private static readonly TimeSpan WikiRequestSpacing = TimeSpan.FromMilliseconds(250);
    private static readonly string[] CandidateSectionNames =
        ["unique", "tertiary", "pre-roll", "weapons and armour", "weapons and armor", "armour and weapons"];
    private readonly SemaphoreSlim wikiRequestGate = new(1, 1);
    private DateTimeOffset nextWikiRequestAt = DateTimeOffset.MinValue;
    private static readonly Dictionary<string, WikiBossRule> WikiBossRules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Abyssal Sire"] = new("Abyssal Sire", ["Pre-roll"], ["Unsired"]),
        ["Alchemical Hydra"] = new(
            "Alchemical Hydra",
            AllowedItems:
            [
                "Hydra's eye", "Hydra's fang", "Hydra's heart", "Hydra tail",
                "Hydra leather", "Hydra's claw", "Ikkle hydra", "Jar of chemicals"
            ],
            InventoryImageItems: ["Ikkle hydra"]),
        ["Barrows Chests"] = new("Chest (Barrows)", ["Pre-roll"]),
        ["Bryophyta"] = new("Chest (Bryophyta's lair)", ["Other"], ["Bryophyta's essence"]),
        ["Chambers of Xeric"] = new("Ancient chest", ["Unique drop table", "Tertiary"]),
        ["Chambers of Xeric (CM)"] = new(
            "Ancient chest",
            ["Unique drop table", "Tertiary"],
            TertiaryExtras: ["Metamorphic dust", "Twisted ancestral colour kit"]),
        ["Crazy Archaeologist"] = new("Crazy archaeologist", ["Pre-roll"], ["Malediction shard 2", "Odium shard 2"]),
        ["Kree'Arra"] = new("Kree'arra"),
        ["Lunar Chests"] = new("Lunar Chest", ["Uniques"]),
        ["Nightmare"] = new(
            "The Nightmare",
            RenameTo: "The Nightmare",
            RateOverrides: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nightmare staff"] = "1/300",
                ["Inquisitor's great helm"] = "1/420",
                ["Inquisitor's hauberk"] = "1/420",
                ["Inquisitor's plateskirt"] = "1/420",
                ["Inquisitor's mace"] = "1/750",
                ["Eldritch orb"] = "1/960",
                ["Harmonised orb"] = "1/960",
                ["Volatile orb"] = "1/960",
                ["Little nightmare"] = "1/4000",
                ["Jar of dreams"] = "1/2000"
            },
            InventoryImageItems: ["Little nightmare"],
            Note: "Rates assume a five-player party and a non-MVP recipient. At five players, the additional unique-table roll chance is 0%."),
        ["Obor"] = new("Chest (Obor's lair)", ["Uniques"], ["Hill giant club"]),
        ["Phantom Muspah"] = new("Phantom Muspah", ["Unique"], ["Venator shard"]),
        ["Phosani's Nightmare"] = new("Phosani's Nightmare"),
        ["Shellbane Gryphon"] = new("Shellbane gryphon", ["Pre-roll", "Tertiary"]),
        ["Sol Heredit"] = new(
            "Rewards Chest (Fortis Colosseum)",
            AllowedItems:
            [
                "Dizana's quiver", "Smol heredit", "Echo crystal", "Sunfire fanatic helm",
                "Sunfire fanatic cuirass", "Sunfire fanatic chausses", "Tonalztics of ralos (uncharged)"
            ],
            SectionIndexes: ["14"],
            InventoryImageItems: ["Smol heredit"],
            Note: "Uses the Wave 12 reward rates."),
        ["The Corrupted Gauntlet"] = new(
            "Reward Chest (The Gauntlet)",
            AllowedItems: ["Crystal weapon seed", "Crystal armour seed", "Enhanced crystal weapon seed", "Youngllef"],
            SectionIndexes: ["20"],
            KeepAllSelectedTertiary: true),
        ["Theatre of Blood"] = new("Monumental chest", SectionIndexes: ["3", "6"]),
        ["Theatre of Blood (HM)"] = new("Theatre of Blood/Hard Mode"),
        ["The Royal Titans"] = new("Royal Titans", ["Pre-roll", "Tertiary drops"]),
        ["Thermonuclear Smoke Devil"] = new("Thermonuclear smoke devil", ["Pre-roll", "Tertiary"]),
        ["Tombs of Amascut"] = new(
            "Chest (Tombs of Amascut)",
            SectionIndexes: ["8", "10"],
            Note: "Unique item weights are shown after a purple is rolled; the overall purple chance depends on raid level and reward points."),
        ["Tombs of Amascut (Expert Mode)"] = new(
            "Chest (Tombs of Amascut)",
            SectionIndexes: ["8", "10"],
            Note: "Unique item weights are shown after a purple is rolled; the overall purple chance depends on raid level and reward points.")
    };

    public async Task<WikiCatalogueDryRunReport> RunAsync(CancellationToken ct = default)
    {
        var bosses = await db.BossActivities
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.Name)
            .Select(x => new ExistingBoss(x.Id, x.Name, x.EfficientCompletionsPerHour))
            .ToListAsync(ct);
        var existingDrops = await (
            from drop in db.SourceDrops.AsNoTracking()
            join item in db.CatalogueItems.AsNoTracking() on drop.ItemId equals item.Id
            select new ExistingDrop(drop.BossActivityId, item.Name, drop.DisplayRate, drop.NumericProbability))
            .ToListAsync(ct);
        var dropsByBoss = existingDrops.ToLookup(x => x.BossActivityId);

        using var gate = new SemaphoreSlim(4);
        var tasks = bosses.Select(async boss =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return await PreviewBossAsync(boss, dropsByBoss[boss.Id].ToList(), ct);
            }
            finally
            {
                gate.Release();
            }
        });

        var previews = (await Task.WhenAll(tasks)).OrderBy(x => x.BossName).ToList();
        return new WikiCatalogueDryRunReport(DateTimeOffset.UtcNow, previews);
    }

    public async Task<WikiCatalogueImportResult> ApplyReviewedImportAsync(CancellationToken ct = default)
    {
        var report = await RunAsync(ct);
        var importable = report.Bosses.Where(x => x.Drops.Count > 0).ToList();
        if (importable.Count == 0)
        {
            throw new InvalidOperationException("The Wiki preview did not contain any drops to import.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var bosses = await db.BossActivities.ToListAsync(ct);
        var items = await db.CatalogueItems.ToListAsync(ct);
        var sourceDrops = await db.SourceDrops.ToListAsync(ct);
        var variants = await db.SourceDropRateVariants.ToListAsync(ct);
        var added = 0;
        var updated = 0;
        var removed = 0;

        foreach (var preview in importable)
        {
            var boss = bosses.Single(x => x.Name.Equals(preview.BossName, StringComparison.OrdinalIgnoreCase));
            var rule = WikiBossRules.GetValueOrDefault(preview.BossName);
            var finalBossName = rule?.RenameTo ?? boss.Name;
            boss.Update(
                finalBossName,
                boss.Category,
                boss.EfficientCompletionsPerHour,
                boss.ExternalIdentifier,
                preview.WikiUrl,
                boss.Notes,
                now,
                BuildBossImageUrl(finalBossName));

            var bossDrops = sourceDrops.Where(x => x.BossActivityId == boss.Id).ToList();
            var retainedIds = new HashSet<Guid>();
            foreach (var candidate in preview.Drops)
            {
                var sourceDrop = bossDrops.FirstOrDefault(existing =>
                {
                    var existingItem = items.Single(item => item.Id == existing.ItemId);
                    return Normalize(existingItem.Name) == Normalize(candidate.Name);
                });

                var normalizedItemName = candidate.Name.Trim().ToUpperInvariant();
                var sharedItem = items.FirstOrDefault(x => x.NormalizedName == normalizedItemName);
                if (sourceDrop is null)
                {
                    sharedItem ??= new CatalogueItem(Guid.NewGuid(), candidate.Name, normalizedItemName);
                    if (!items.Contains(sharedItem))
                    {
                        items.Add(sharedItem);
                        db.CatalogueItems.Add(sharedItem);
                    }
                    sourceDrop = new SourceDrop(Guid.NewGuid(), boss.Id, sharedItem.Id, candidate.Rates[0].DisplayRate, null, null, now);
                    sourceDrops.Add(sourceDrop);
                    db.SourceDrops.Add(sourceDrop);
                    added++;
                }
                else
                {
                    sharedItem ??= items.Single(x => x.Id == sourceDrop.ItemId);
                    if (sourceDrop.ItemId != sharedItem.Id)
                    {
                        sourceDrop.ChangeItem(sharedItem.Id);
                    }
                    updated++;
                }

                sharedItem.SetActive(true);
                sharedItem.Update(candidate.Name, normalizedItemName, sharedItem.ExternalIdentifier, sharedItem.Notes, candidate.ImageUrl);
                retainedIds.Add(sourceDrop.Id);

                var preserveReviewedPersonalRate = candidate.NeedsReview && sourceDrop.NumericProbability is > 0 && sourceDrop.RateConditionNote?.Contains("Personal per-completion rate", StringComparison.OrdinalIgnoreCase) == true;
                var hasOneReliableRate = candidate.Rates.Count == 1 && !candidate.NeedsReview && candidate.Rates[0].Probability is > 0;
                var probability = hasOneReliableRate ? candidate.Rates[0].Probability : null;
                var rolls = hasOneReliableRate ? candidate.Rates[0].RollsPerCompletion : 1;
                var displayRate = preserveReviewedPersonalRate ? sourceDrop.DisplayRate : candidate.Rates.Count == 1
                    ? candidate.Rates[0].DisplayRate
                    : $"{candidate.Rates[0].DisplayRate} (+{candidate.Rates.Count - 1} variants)";
                if (preserveReviewedPersonalRate) probability = sourceDrop.NumericProbability;
                var condition = preserveReviewedPersonalRate ? sourceDrop.RateConditionNote : JoinNotes(candidate.Condition, candidate.ReviewReason);
                var effectiveProbability = probability;
                decimal? ehb = boss.EfficientCompletionsPerHour is > 0 && effectiveProbability is > 0
                    ? 1 / (boss.EfficientCompletionsPerHour.Value * effectiveProbability.Value * rolls)
                    : null;
                sourceDrop.Update(displayRate, probability, condition, ehb, candidate.DataSource, now);
                sourceDrop.SetRateMechanics(
                    Bingo.Domain.Catalogue.DropProbabilityScope.Participant,
                    false,
                    null,
                    1,
                    rolls,
                    preserveReviewedPersonalRate ? sourceDrop.RollGroup : "default");
                sourceDrop.SetActive(true);

                var oldVariants = variants.Where(x => x.SourceDropId == sourceDrop.Id).ToList();
                db.SourceDropRateVariants.RemoveRange(oldVariants);
                variants.RemoveAll(x => x.SourceDropId == sourceDrop.Id);
                var position = 1;
                foreach (var rate in candidate.Rates)
                {
                    var variant = new SourceDropRateVariant(
                        Guid.NewGuid(), sourceDrop.Id, position++, rate.Label, rate.DisplayRate,
                        rate.Probability, candidate.Condition);
                    variants.Add(variant);
                    db.SourceDropRateVariants.Add(variant);
                }
            }

            var obsolete = bossDrops.Where(x => !retainedIds.Contains(x.Id)).ToList();
            if (obsolete.Count > 0)
            {
                var obsoleteIds = obsolete.Select(x => x.Id).ToHashSet();
                db.SourceDropRateVariants.RemoveRange(variants.Where(x => obsoleteIds.Contains(x.SourceDropId)));
                db.SourceDrops.RemoveRange(obsolete);
                sourceDrops.RemoveAll(x => obsoleteIds.Contains(x.Id));
                removed += obsolete.Count;
            }
        }

        await db.SaveChangesAsync(ct);

        var usedItemIds = await db.SourceDrops.Select(x => x.ItemId).Distinct().ToListAsync(ct);
        var orphanedItems = await db.CatalogueItems.Where(x => !usedItemIds.Contains(x.Id)).ToListAsync(ct);
        db.CatalogueItems.RemoveRange(orphanedItems);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new WikiCatalogueImportResult(importable.Count, added, updated, removed, orphanedItems.Count, report.ReviewCount);
    }

    private async Task<WikiBossPreview> PreviewBossAsync(
        ExistingBoss boss,
        IReadOnlyList<ExistingDrop> existingDrops,
        CancellationToken ct)
    {
        var rule = WikiBossRules.GetValueOrDefault(boss.Name) ?? new WikiBossRule(boss.Name);
        var page = rule.Page;
        try
        {
            var sections = await GetSectionsAsync(page, ct);
            if (sections is null)
            {
                return WikiBossPreview.Unmatched(boss.Name, page, "The Wiki page was not found.");
            }

            var selected = SelectSections(sections, rule);
            if (selected.Count == 0)
            {
                return WikiBossPreview.Unmatched(boss.Name, page, "No unique or tertiary drop section was found.");
            }

            var candidates = new List<WikiDropPreview>();
            foreach (var section in selected)
            {
                var wikitext = await GetSectionWikitextAsync(section.FromTitle, section.Index, ct);
                var parsedDrops = ParseDropLines(wikitext);
                var isTertiary = section.Line.Contains("tertiary", StringComparison.OrdinalIgnoreCase);
                var needsSpecialItemFilter = section.Line.Contains("pre-roll", StringComparison.OrdinalIgnoreCase)
                    || section.Line.Contains("weapons and armour", StringComparison.OrdinalIgnoreCase)
                    || section.Line.Contains("weapons and armor", StringComparison.OrdinalIgnoreCase)
                    || section.Line.Contains("armour and weapons", StringComparison.OrdinalIgnoreCase);
                var inventoryImageItems = (rule.InventoryImageItems ?? [])
                    .Select(Normalize)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (isTertiary || needsSpecialItemFilter)
                {
                    var categoryMatches = await GetCatalogueCategoryMatchesAsync(parsedDrops.Select(x => x.Name), ct);
                    inventoryImageItems.UnionWith(categoryMatches.Pets);
                    if (isTertiary && !rule.KeepAllSelectedTertiary)
                    {
                        foreach (var extra in rule.TertiaryExtras ?? []) categoryMatches.Accepted.Add(Normalize(extra));
                        categoryMatches.Accepted.UnionWith(categoryMatches.SpecialItems);
                        parsedDrops = parsedDrops.Where(x => categoryMatches.Accepted.Contains(Normalize(x.Name))).ToList();
                    }
                    else if (needsSpecialItemFilter)
                    {
                        parsedDrops = parsedDrops.Where(x => categoryMatches.SpecialItems.Contains(Normalize(x.Name))).ToList();
                    }
                }
                if (rule.AllowedItems is not null)
                {
                    var allowedItems = rule.AllowedItems.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    parsedDrops = parsedDrops.Where(x => allowedItems.Contains(Normalize(x.Name))).ToList();
                }

                foreach (var wikiParsed in parsedDrops)
                {
                    var parsed = ApplyRateOverride(wikiParsed, rule);
                    var existing = existingDrops.FirstOrDefault(x => Normalize(x.ItemName) == Normalize(parsed.Name));
                    var hasVariableRate = parsed.Rates.Count != 1 || parsed.Rates.Any(x => x.Probability is null);
                    var isPurpleConditional = IsPurpleConditional(boss.Name, section.Line, parsed.Name);
                    var needsReview = hasVariableRate || isPurpleConditional;
                    var reviewReason = isPurpleConditional
                        ? "This is the chance after receiving a purple reward, not the complete per-raid probability."
                        : hasVariableRate ? "This drop has a conditional, alternate, or non-numeric rate." : null;
                    var action = existing is null ? WikiDropAction.Add : RatesMatch(existing, parsed.Rates[0]) ? WikiDropAction.Unchanged : WikiDropAction.Replace;
                    candidates.Add(new WikiDropPreview(
                        parsed.Name,
                        isTertiary && !rule.KeepAllSelectedTertiary
                            ? "Tertiary pet or jar"
                            : BuildSectionLabel(page, section),
                        action,
                        parsed.Rates,
                        parsed.Condition,
                        needsReview,
                        reviewReason,
                        BuildItemImageUrl(parsed.Name, inventoryImageItems.Contains(Normalize(parsed.Name))),
                        BuildWikiPageUrl(page)));
                }
            }

            candidates = candidates
                .GroupBy(x => Normalize(x.Name))
                .Select(group => group.First())
                .OrderBy(x => x.Name)
                .ToList();

            var notes = new List<string>();
            if (candidates.Count == 0) notes.Add("The relevant sections contained no standard Wiki drop rows.");
            if (!string.IsNullOrWhiteSpace(rule.Note)) notes.Add(rule.Note);
            if (!string.IsNullOrWhiteSpace(rule.RenameTo))
            {
                notes.Add($"Rename the catalogue entry to {rule.RenameTo} during the reviewed import.");
            }
            var note = notes.Count == 0 ? null : string.Join(' ', notes);

            return new WikiBossPreview(
                boss.Name,
                page,
                BuildWikiPageUrl(page),
                BuildBossImageUrl(rule.RenameTo ?? boss.Name),
                candidates,
                candidates.Count == 0 || !string.IsNullOrWhiteSpace(rule.RenameTo)
                    ? WikiBossPreviewStatus.NeedsReview
                    : WikiBossPreviewStatus.Ready,
                note);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return WikiBossPreview.Unmatched(boss.Name, page, exception.Message);
        }
    }

    private static List<WikiSection> SelectSections(IReadOnlyList<WikiSection> sections, WikiBossRule rule)
    {
        if (rule.Sections is not null)
        {
            return sections
                .Where(section => rule.Sections.Any(expected => section.Line.Equals(expected, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (rule.SectionIndexes is not null)
        {
            return sections
                .Where(section => rule.SectionIndexes.Contains(section.Index, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        return sections
            .Where(section => CandidateSectionNames.Any(candidate => section.Line.Contains(candidate, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static bool IsPurpleConditional(string bossName, string sectionName, string dropName) =>
        ((bossName.Equals("Chambers of Xeric", StringComparison.OrdinalIgnoreCase) ||
          bossName.Equals("Chambers of Xeric (CM)", StringComparison.OrdinalIgnoreCase)) &&
         (sectionName.Equals("Unique drop table", StringComparison.OrdinalIgnoreCase) ||
          dropName.Equals("Olmlet", StringComparison.OrdinalIgnoreCase))) ||
        (bossName.Equals("Theatre of Blood", StringComparison.OrdinalIgnoreCase) &&
         sectionName.Equals("Normal mode", StringComparison.OrdinalIgnoreCase)) ||
        ((bossName.Equals("Tombs of Amascut", StringComparison.OrdinalIgnoreCase) ||
          bossName.Equals("Tombs of Amascut (Expert Mode)", StringComparison.OrdinalIgnoreCase)) &&
         sectionName.Equals("Uniques", StringComparison.OrdinalIgnoreCase));

    private static ParsedWikiDrop ApplyRateOverride(ParsedWikiDrop parsed, WikiBossRule rule)
    {
        if (rule.RateOverrides is null || !rule.RateOverrides.TryGetValue(parsed.Name, out var displayRate))
        {
            return parsed;
        }

        return parsed with
        {
            Rates = [new WikiRateVariant("Five-player party", displayRate, ParseProbability(displayRate))],
            Condition = null
        };
    }

    private async Task<IReadOnlyList<WikiSection>?> GetSectionsAsync(string page, CancellationToken ct)
    {
        using var document = await GetWikiJsonAsync(new Dictionary<string, string>
        {
            ["action"] = "parse",
            ["page"] = page,
            ["prop"] = "sections",
            ["format"] = "json",
            ["formatversion"] = "2"
        }, ct);
        if (document.RootElement.TryGetProperty("error", out _)) return null;
        var result = new List<WikiSection>();
        foreach (var section in document.RootElement.GetProperty("parse").GetProperty("sections").EnumerateArray())
        {
            result.Add(new WikiSection(
                section.GetProperty("index").GetString()!,
                section.GetProperty("line").GetString()!,
                section.TryGetProperty("fromtitle", out var fromTitle)
                    ? fromTitle.GetString() ?? page
                    : page));
        }
        return result;
    }

    private async Task<string> GetSectionWikitextAsync(string page, string section, CancellationToken ct)
    {
        using var document = await GetWikiJsonAsync(new Dictionary<string, string>
        {
            ["action"] = "parse",
            ["page"] = page,
            ["section"] = section,
            ["prop"] = "wikitext",
            ["format"] = "json",
            ["formatversion"] = "2"
        }, ct);
        return document.RootElement.GetProperty("parse").GetProperty("wikitext").GetString() ?? string.Empty;
    }

    private async Task<WikiCategoryMatches> GetCatalogueCategoryMatchesAsync(IEnumerable<string> itemNames, CancellationToken ct)
    {
        var names = itemNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return new WikiCategoryMatches([], [], []);

        var acceptedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var petTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var specialItemTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in names.Chunk(40))
        {
            using var document = await GetWikiJsonAsync(new Dictionary<string, string>
            {
                ["action"] = "query",
                ["titles"] = string.Join('|', batch),
                ["prop"] = "categories",
                ["cllimit"] = "max",
                ["redirects"] = "1",
                ["format"] = "json",
                ["formatversion"] = "2"
            }, ct);
            var query = document.RootElement.GetProperty("query");
            foreach (var page in query.GetProperty("pages").EnumerateArray())
            {
                if (!page.TryGetProperty("categories", out var categories)) continue;
                var categoryNames = categories.EnumerateArray()
                    .Select(category => category.GetProperty("title").GetString())
                    .ToList();
                var normalizedTitle = Normalize(page.GetProperty("title").GetString()!);
                if (categoryNames.Any(IsPetOrJarCategory))
                {
                    acceptedTitles.Add(normalizedTitle);
                }
                if (categoryNames.Any(IsSpecialItemCategory))
                {
                    specialItemTitles.Add(normalizedTitle);
                }
                if (categoryNames.Any(IsPetCategory))
                {
                    petTitles.Add(normalizedTitle);
                }
            }
            if (query.TryGetProperty("redirects", out var redirects))
            {
                foreach (var redirect in redirects.EnumerateArray())
                {
                    if (acceptedTitles.Contains(Normalize(redirect.GetProperty("to").GetString()!)))
                    {
                        acceptedTitles.Add(Normalize(redirect.GetProperty("from").GetString()!));
                    }
                    if (petTitles.Contains(Normalize(redirect.GetProperty("to").GetString()!)))
                    {
                        petTitles.Add(Normalize(redirect.GetProperty("from").GetString()!));
                    }
                    if (specialItemTitles.Contains(Normalize(redirect.GetProperty("to").GetString()!)))
                    {
                        specialItemTitles.Add(Normalize(redirect.GetProperty("from").GetString()!));
                    }
                }
            }
        }
        specialItemTitles.UnionWith(acceptedTitles);
        return new WikiCategoryMatches(acceptedTitles, petTitles, specialItemTitles);
    }

    public static bool IsPetOrJarCategory(string? category) =>
        string.Equals(category, "Category:Pets", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(category, "Category:Jars", StringComparison.OrdinalIgnoreCase);

    public static bool IsPetCategory(string? category) =>
        string.Equals(category, "Category:Pets", StringComparison.OrdinalIgnoreCase);

    public static bool IsSpecialItemCategory(string? category) =>
        IsPetOrJarCategory(category) ||
        string.Equals(category, "Category:Collection log items", StringComparison.OrdinalIgnoreCase);

    private async Task<JsonDocument> GetWikiJsonAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        var query = string.Join("&", values.Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));
        var client = httpClientFactory.CreateClient("OsrsWiki");
        await wikiRequestGate.WaitAsync(ct);

        try
        {
            for (var attempt = 0; attempt < MaxWikiRequestAttempts; attempt++)
            {
                var spacingDelay = nextWikiRequestAt - DateTimeOffset.UtcNow;
                if (spacingDelay > TimeSpan.Zero)
                {
                    await Task.Delay(spacingDelay, ct);
                }

                nextWikiRequestAt = DateTimeOffset.UtcNow + WikiRequestSpacing;
                using var response = await client.GetAsync(
                    $"api.php?{query}",
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt == MaxWikiRequestAttempts - 1)
                    {
                        throw new HttpRequestException(
                            "The OSRS Wiki is still limiting requests after several retries. Please wait a moment and run the preview again.",
                            null,
                            response.StatusCode);
                    }

                    await Task.Delay(GetRetryDelay(response, attempt), ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            }
        }
        finally
        {
            wikiRequestGate.Release();
        }

        throw new InvalidOperationException("The OSRS Wiki request retry loop ended unexpectedly.");
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        var requestedDelay = retryAfter?.Delta;
        if (requestedDelay is null && retryAfter?.Date is { } retryDate)
        {
            requestedDelay = retryDate - DateTimeOffset.UtcNow;
        }

        var fallbackDelay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt + 1)));
        if (requestedDelay is null || requestedDelay <= TimeSpan.Zero)
        {
            return fallbackDelay;
        }

        return requestedDelay > TimeSpan.FromSeconds(30)
            ? TimeSpan.FromSeconds(30)
            : requestedDelay.Value;
    }

    public void Dispose() => wikiRequestGate.Dispose();

    public static IReadOnlyList<ParsedWikiDrop> ParseDropLines(string wikitext)
    {
        var results = new List<ParsedWikiDrop>();
        foreach (var template in ExtractTemplates(wikitext, "DropsLine"))
        {
            var fields = ParseTemplateFields(template);
            if (!fields.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name)) continue;

            var rates = fields
                .Where(x => x.Key.Equals("rarity", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(x.Key, @"^altrarity\d*$", RegexOptions.IgnoreCase))
                .Select((x, index) => new WikiRateVariant(index == 0 ? "Default" : $"Alternative {index}", CleanWikiText(x.Value), ParseProbability(x.Value)))
                .ToList();
            if (rates.Count == 0) rates.Add(new WikiRateVariant("Default", "Not supplied", null));

            fields.TryGetValue("raritynotes", out var condition);
            if (fields.TryGetValue("rolls", out var rewardRolls) && !string.IsNullOrWhiteSpace(rewardRolls))
            {
                var cleanedRolls = CleanWikiText(rewardRolls);
                var parsedRolls = int.TryParse(cleanedRolls, out var count) && count > 0 ? count : 1;
                rates = rates
                    .Select(rate => rate with
                    {
                        Label = "Per reward roll",
                        DisplayRate = $"{cleanedRolls} × {rate.DisplayRate}",
                        RollsPerCompletion = parsedRolls
                    })
                    .ToList();
                var rollNote = parsedRolls > 1
                    ? $"The Wiki supplies {parsedRolls} reward rolls; the stored probability is per roll."
                    : $"The Wiki supplies an unrecognized reward-roll value ({cleanedRolls}); review it manually.";
                condition = string.IsNullOrWhiteSpace(condition) ? rollNote : $"{CleanWikiText(condition)} {rollNote}";
            }
            results.Add(new ParsedWikiDrop(
                CleanWikiText(name),
                rates,
                string.IsNullOrWhiteSpace(condition) ? null : CleanWikiText(condition)));
        }
        return results;
    }

    private static List<string> ExtractTemplates(string source, string templateName)
    {
        var result = new List<string>();
        var marker = "{{" + templateName;
        var searchIndex = 0;
        while ((searchIndex = source.IndexOf(marker, searchIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var depth = 0;
            var end = searchIndex;
            for (var index = searchIndex; index < source.Length - 1; index++)
            {
                if (source[index] == '{' && source[index + 1] == '{') { depth++; index++; continue; }
                if (source[index] == '}' && source[index + 1] == '}')
                {
                    depth--; index++;
                    if (depth == 0) { end = index + 1; break; }
                }
            }
            if (end <= searchIndex) break;
            result.Add(source[searchIndex..end]);
            searchIndex = end;
        }
        return result;
    }

    private static Dictionary<string, string> ParseTemplateFields(string template)
    {
        var content = template[2..^2];
        var parts = new List<string>();
        var start = 0;
        var templateDepth = 0;
        var linkDepth = 0;
        for (var index = 0; index < content.Length; index++)
        {
            if (index + 1 < content.Length && content[index] == '{' && content[index + 1] == '{') { templateDepth++; index++; continue; }
            if (index + 1 < content.Length && content[index] == '}' && content[index + 1] == '}') { templateDepth--; index++; continue; }
            if (index + 1 < content.Length && content[index] == '[' && content[index + 1] == '[') { linkDepth++; index++; continue; }
            if (index + 1 < content.Length && content[index] == ']' && content[index + 1] == ']') { linkDepth--; index++; continue; }
            if (content[index] == '|' && templateDepth == 0 && linkDepth == 0) { parts.Add(content[start..index]); start = index + 1; }
        }
        parts.Add(content[start..]);
        return parts.Skip(1)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToDictionary(part => part[0].Trim(), part => part[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static decimal? ParseProbability(string value)
    {
        var cleaned = CleanWikiText(value).Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        var fraction = Regex.Match(cleaned, @"^(?<numerator>\d+(?:\.\d+)?)\s*/\s*(?<denominator>\d+(?:\.\d+)?)$");
        if (fraction.Success &&
            decimal.TryParse(fraction.Groups["numerator"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numerator) &&
            decimal.TryParse(fraction.Groups["denominator"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var denominator) && denominator > 0)
        {
            return numerator / denominator;
        }
        var percentage = Regex.Match(cleaned, @"^(?<value>\d+(?:\.\d+)?)%$");
        return percentage.Success && decimal.TryParse(percentage.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent)
            ? percent / 100m : null;
    }

    private static string CleanWikiText(string value)
    {
        var cleaned = ExpandWikiExpressions(value);
        cleaned = Regex.Replace(cleaned, @"<ref\b[^>]*>(.*?)</ref>", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleaned = Regex.Replace(cleaned, @"<ref\b[^>]*/>", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[\[(?:[^\]|]+\|)?([^\]]+)\]\]", "$1");
        cleaned = Regex.Replace(cleaned, @"''+", string.Empty);
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", string.Empty);
        return WebUtility.HtmlDecode(cleaned).Trim();
    }

    private static string ExpandWikiExpressions(string value) => Regex.Replace(
        value,
        @"\{\{#expr:(?<expression>.*?)\s+round\s+(?<digits>\d+)\}\}",
        match =>
        {
            if (!int.TryParse(match.Groups["digits"].Value, out var digits) ||
                !ArithmeticExpression.TryEvaluate(match.Groups["expression"].Value, out var result))
            {
                return match.Value;
            }

            var rounded = decimal.Round(result, digits, MidpointRounding.AwayFromZero);
            var format = digits == 0 ? "0" : $"0.{new string('#', digits)}";
            return rounded.ToString(format, CultureInfo.InvariantCulture);
        },
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private sealed class ArithmeticExpression(string source)
    {
        private int position;

        public static bool TryEvaluate(string source, out decimal value)
        {
            try
            {
                var parser = new ArithmeticExpression(source);
                value = parser.ParseExpression();
                parser.SkipWhitespace();
                return parser.position == source.Length;
            }
            catch (Exception exception) when (exception is FormatException or DivideByZeroException or OverflowException)
            {
                value = 0;
                return false;
            }
        }

        private decimal ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Take('+')) value += ParseTerm();
                else if (Take('-')) value -= ParseTerm();
                else return value;
            }
        }

        private decimal ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Take('*')) value *= ParseFactor();
                else if (Take('/')) value /= ParseFactor();
                else return value;
            }
        }

        private decimal ParseFactor()
        {
            SkipWhitespace();
            if (Take('-')) return -ParseFactor();
            if (Take('('))
            {
                var value = ParseExpression();
                SkipWhitespace();
                if (!Take(')')) throw new FormatException("Missing closing parenthesis.");
                return value;
            }

            var start = position;
            while (position < source.Length && (char.IsDigit(source[position]) || source[position] == '.')) position++;
            if (start == position || !decimal.TryParse(source[start..position], NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException("Invalid arithmetic expression.");
            }
            return number;
        }

        private bool Take(char expected)
        {
            if (position >= source.Length || source[position] != expected) return false;
            position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (position < source.Length && char.IsWhiteSpace(source[position])) position++;
        }
    }

    private static bool RatesMatch(ExistingDrop existing, WikiRateVariant rate) =>
        string.Equals(existing.DisplayRate.Trim(), rate.DisplayRate.Trim(), StringComparison.OrdinalIgnoreCase) ||
        existing.Probability is not null && rate.Probability is not null && Math.Abs(existing.Probability.Value - rate.Probability.Value) < 0.0000000001m;

    private static string Normalize(string value) => Regex.Replace(value.Trim().ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
    private static string BuildSectionLabel(string page, WikiSection section)
    {
        var sourcePage = section.FromTitle.Replace('_', ' ');
        return Normalize(sourcePage) == Normalize(page)
            ? section.Line
            : $"{section.Line} — {sourcePage}";
    }
    private static string BuildWikiPageUrl(string page) => $"{WikiBaseUrl}/w/{Uri.EscapeDataString(page.Replace(' ', '_'))}";
    private static string BuildBossImageUrl(string bossName) =>
        $"{WikiBaseUrl}/w/Special:Redirect/file/{Uri.EscapeDataString(bossName.Replace(' ', '_') + ".png")}";
    private static string BuildItemImageUrl(string itemName, bool useInventoryImage) =>
        $"{WikiBaseUrl}/w/Special:Redirect/file/{Uri.EscapeDataString(itemName.Replace(' ', '_') + (useInventoryImage ? ".png" : "_detail.png"))}";

    private static string? JoinNotes(string? first, string? second)
    {
        var values = new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).ToList();
        return values.Count == 0 ? null : string.Join(' ', values);
    }

    private sealed record ExistingBoss(Guid Id, string Name, decimal? EfficientRate);
    private sealed record ExistingDrop(Guid BossActivityId, string ItemName, string DisplayRate, decimal? Probability);
    private sealed record WikiSection(string Index, string Line, string FromTitle);
    private sealed record WikiCategoryMatches(HashSet<string> Accepted, HashSet<string> Pets, HashSet<string> SpecialItems);
    private sealed record WikiBossRule(
        string Page,
        IReadOnlyList<string>? Sections = null,
        IReadOnlyList<string>? AllowedItems = null,
        IReadOnlyList<string>? TertiaryExtras = null,
        string? RenameTo = null,
        IReadOnlyList<string>? SectionIndexes = null,
        bool KeepAllSelectedTertiary = false,
        string? Note = null,
        IReadOnlyDictionary<string, string>? RateOverrides = null,
        IReadOnlyList<string>? InventoryImageItems = null);
}

public sealed record WikiCatalogueDryRunReport(DateTimeOffset GeneratedAt, IReadOnlyList<WikiBossPreview> Bosses)
{
    public int CandidateCount => Bosses.Sum(x => x.Drops.Count);
    public int ReviewCount => Bosses.Sum(x => x.Drops.Count(drop => drop.NeedsReview)) + Bosses.Count(x => x.Status == WikiBossPreviewStatus.NeedsReview);
    public int UnmatchedCount => Bosses.Count(x => x.Status == WikiBossPreviewStatus.Unmatched);
}

public sealed record WikiBossPreview(
    string BossName,
    string WikiPage,
    string WikiUrl,
    string BossImageUrl,
    IReadOnlyList<WikiDropPreview> Drops,
    WikiBossPreviewStatus Status,
    string? Note)
{
    public static WikiBossPreview Unmatched(string bossName, string page, string note) =>
        new(bossName, page, $"https://oldschool.runescape.wiki/w/{Uri.EscapeDataString(page.Replace(' ', '_'))}",
            $"https://oldschool.runescape.wiki/Special:Redirect/file/{Uri.EscapeDataString(bossName.Replace(' ', '_') + ".png")}",
            [], WikiBossPreviewStatus.Unmatched, note);
}

public sealed record WikiDropPreview(
    string Name,
    string Section,
    WikiDropAction Action,
    IReadOnlyList<WikiRateVariant> Rates,
    string? Condition,
    bool NeedsReview,
    string? ReviewReason,
    string ImageUrl,
    string DataSource);

public sealed record ParsedWikiDrop(string Name, IReadOnlyList<WikiRateVariant> Rates, string? Condition);
public sealed record WikiRateVariant(string Label, string DisplayRate, decimal? Probability, int RollsPerCompletion = 1);
public enum WikiBossPreviewStatus { Ready, NeedsReview, Unmatched }
public enum WikiDropAction { Add, Replace, Unchanged }
public sealed record WikiCatalogueImportResult(
    int BossesUpdated,
    int DropsAdded,
    int DropsUpdated,
    int DropsRemoved,
    int OrphanedItemsRemoved,
    int ReviewCount);
