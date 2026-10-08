using System.Globalization;
using System.Text.Json;
using Bingo.Application.Catalogue;
using Bingo.Domain.Catalogue;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Catalogue;

// WA-5 Catalogue (T2 item 2): one URL for the directory (?q=&cat=&status=), the activity drawer
// (?activity={id}), its open drop editor (&drop={id}) and Add activity (?new=1). Ids, not slugs (A2).
// Bound from the query string only: "page"/"handler" style route values must never win (T1 finding).
[AdminDesign]
public sealed partial class IndexModel
{
    public const int SearchLimit = 100;

    [BindProperty(SupportsGet = true, Name = "q"), FromQuery(Name = "q")] public string? Query { get; set; }
    [BindProperty(SupportsGet = true, Name = "cat"), FromQuery(Name = "cat")] public string? CategoryQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "status"), FromQuery(Name = "status")] public string? StatusQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "activity"), FromQuery(Name = "activity")] public string? ActivityQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "drop"), FromQuery(Name = "drop")] public string? DropQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "new"), FromQuery(Name = "new")] public string? NewQuery { get; set; }

    public string Search { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public bool InactiveTab { get; private set; }
    public Guid? RequestedActivity { get; private set; }
    public Guid? RequestedDrop { get; private set; }
    public bool AddRequested { get; private set; }
    public bool DrawerRequested => RequestedActivity is not null || AddRequested;
    public bool IsSuperAdmin => User.IsInRole("SuperAdmin");
    public DateTimeOffset UtcNow { get; private set; }

    public IReadOnlyList<ActivityRow> Activities { get; private set; } = [];
    public int ActiveCount => Activities.Count(x => x.Active);
    public int InactiveCount => Activities.Count(x => !x.Active);
    public int ActiveDropCount { get; private set; }
    public ActivityDrawer? Drawer { get; private set; }

    public sealed record ActivityRow(Guid Id, string Name, string Category, decimal? Rate, bool Active, string? ImageUrl, int ActiveDrops, int InactiveDrops,
        int WithoutProbability, int WithoutValue, IReadOnlyList<string> DropNames, string? MatchedBy, bool Visible);
    public sealed record ActivityDrawer(BossActivity? Activity, IReadOnlyList<DropView> Drops, string ItemIndexJson, Guid? OpenDrop, bool Missing);
    public sealed record DropView(SourceDrop Drop, CatalogueItem Item, decimal? ChancePerCompletion, IReadOnlyList<SharedItemActivity> OtherActivities);

    public string DirectoryUrl(Guid? activity = null, Guid? drop = null, bool add = false)
    {
        var values = new List<string>();
        if (Search.Length > 0) values.Add("q=" + Uri.EscapeDataString(Search));
        if (Category.Length > 0) values.Add("cat=" + Uri.EscapeDataString(Category));
        if (InactiveTab) values.Add("status=inactive");
        if (activity is { } id) { values.Add("activity=" + id); if (drop is { } dropId) values.Add("drop=" + dropId); }
        else if (add) values.Add("new=1");
        return "/Admin/Catalogue" + (values.Count == 0 ? string.Empty : "?" + string.Join("&", values));
    }
    public string CanonicalUrl => DirectoryUrl(RequestedActivity, Drawer?.OpenDrop, AddRequested);

    private async Task LoadDirectoryAsync(CancellationToken ct)
    {
        UtcNow = timeProvider.GetUtcNow();
        Search = (Query ?? string.Empty).Trim() is { Length: > 0 } search ? search[..Math.Min(search.Length, SearchLimit)] : string.Empty;
        Category = ValidCategory(CategoryQuery) ? CategoryQuery!.Trim() : string.Empty;
        InactiveTab = string.Equals(StatusQuery, "inactive", StringComparison.Ordinal);
        RequestedActivity = Guid.TryParse(ActivityQuery, out var activityId) ? activityId : null;
        RequestedDrop = RequestedActivity is not null && Guid.TryParse(DropQuery, out var dropId) ? dropId : null;
        AddRequested = RequestedActivity is null && string.Equals(NewQuery, "1", StringComparison.Ordinal);

        var bosses = ApiBosses.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Id).ToList();
        var drops = await dbContext.SourceDrops.AsNoTracking().ToListAsync(ct);
        var dropsByBoss = drops.ToLookup(x => x.BossActivityId);
        ActiveDropCount = drops.Count(x => x.Active && ApiBosses.TryGetValue(x.BossActivityId, out var owner) && owner.Active);
        var term = Search.ToLowerInvariant();
        Activities = bosses.Select(boss =>
        {
            var own = dropsByBoss[boss.Id].ToList();
            var active = own.Where(x => x.Active).ToList();
            var names = own.Select(x => ApiItems.TryGetValue(x.ItemId, out var item) ? item.Name : string.Empty).Where(x => x.Length > 0).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
            string? matched = null;
            var visible = boss.Active != InactiveTab && (Category.Length == 0 || boss.Category == Category);
            if (visible && term.Length > 0 && !boss.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                matched = names.FirstOrDefault(x => x.Contains(term, StringComparison.OrdinalIgnoreCase));
                visible = matched is not null;
            }
            return new ActivityRow(boss.Id, boss.Name, boss.Category, boss.EfficientCompletionsPerHour, boss.Active, boss.ImageUrl,
                active.Count, own.Count - active.Count,
                active.Count(x => x.NumericProbability is not > 0),
                active.Count(x => !ApiItems.TryGetValue(x.ItemId, out var item) || item.CatalogueValueGp is null),
                names, matched, visible);
        }).ToList();

        if (AddRequested) { Drawer = new ActivityDrawer(null, [], ItemIndex(drops), null, false); return; }
        if (RequestedActivity is not { } requested) return;
        if (!ApiBosses.TryGetValue(requested, out var activity)) { Drawer = new ActivityDrawer(null, [], "{}", null, true); return; }
        var views = dropsByBoss[requested]
            .Where(x => ApiItems.ContainsKey(x.ItemId))
            .Select(x => new DropView(x, ApiItems[x.ItemId], SourceDrop.CalculateProbabilityPerCompletion(x.NumericProbability, x.RollsPerCompletion),
                drops.Where(other => other.ItemId == x.ItemId && other.BossActivityId != requested)
                    .Select(other => ApiBosses.TryGetValue(other.BossActivityId, out var b) ? new SharedItemActivity(b.Id, b.Name) : null)
                    .OfType<SharedItemActivity>().DistinctBy(other => other.Id).OrderBy(other => other.Name, StringComparer.CurrentCultureIgnoreCase).ToList()))
            .OrderByDescending(x => x.Drop.Active).ThenBy(x => x.Item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var open = RequestedDrop is { } wanted && views.Any(x => x.Drop.Id == wanted) ? wanted : (Guid?)null;
        Drawer = new ActivityDrawer(activity, views, ItemIndex(drops), open, false);
    }

    /// <summary>Shared items by normalized name, for the live "already a shared item" notes (AU21 explicit adoption).</summary>
    private string ItemIndex(IReadOnlyList<SourceDrop> drops)
    {
        var uses = drops.ToLookup(x => x.ItemId);
        var index = ApiItems.Values.ToDictionary(x => x.NormalizedName, x => new
        {
            id = x.Id,
            name = x.Name,
            image = x.ImageUrl,
            value = x.CatalogueValueGp is { } value ? (x.PriceSource == CataloguePriceSource.Untradeable ? -1 : value) : (long?)null,
            uses = uses[x.Id].Select(d => new { activity = d.BossActivityId, name = ApiBosses.TryGetValue(d.BossActivityId, out var b) ? b.Name : string.Empty, active = d.Active })
                .OrderBy(d => d.name, StringComparer.CurrentCultureIgnoreCase).ToArray()
        });
        return JsonSerializer.Serialize(index);
    }

    /* ---------------- presentation helpers (server-rendered text; the page script mirrors them for previews) ---------------- */
    public static string Number(decimal value, int decimals = 2) => value.ToString("#,##0." + new string('#', decimals), CultureInfo.CurrentCulture);
    public static string OneIn(decimal? chance) => chance is > 0 ? Number(Math.Round(1 / chance.Value, 0, MidpointRounding.AwayFromZero), 0) : string.Empty;
    public static string Gp(long value) => value.ToString("#,##0", CultureInfo.CurrentCulture);
    public static string Ehb(decimal value) => Number(value, value < 10 ? 2 : 1);
    public static bool IsMinigame(string category) => string.Equals(category, "Minigame", StringComparison.Ordinal);
    public static string When(DateTimeOffset value) => DateTimePresentation.Format(value, "d MMM, HH:mm", provider: CultureInfo.CurrentCulture);
}
