using System.Globalization;
using Bingo.Application.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Audit;

// Read-only administrative history. Query names follow the reference (README :2023-2026), with the
// event id instead of a slug (:2163-2166). Unknown or invalid link parts are dropped with a notice and
// the rest still applies (C-AUD-4). Hidden-event history stays visible to every Admin (AU16).
[Authorize(Policy = AuthorizationPolicies.Admin)]
[AdminDesign]
public sealed class IndexModel(ApplicationDbContext dbContext, TimeProvider? time = null) : PageModel
{
    public const int PageSize = 25;
    public const int TextLimit = 100;
    private const int MaxPage = int.MaxValue / PageSize;
    private static readonly string[] KnownKeys = ["event", "action", "actor", "type", "from", "to", "page", "entry"];

    // Query-only binding: "page" (and "handler") are also Razor Pages route values, which would
    // otherwise win over the query string (as on Events, Index.cshtml.cs:39-40).
    [BindProperty(SupportsGet = true, Name = "event"), FromQuery(Name = "event")] public string? EventQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "action"), FromQuery(Name = "action")] public string? ActionQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "actor"), FromQuery(Name = "actor")] public string? ActorQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "type"), FromQuery(Name = "type")] public string? TypeQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "from"), FromQuery(Name = "from")] public string? FromQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "to"), FromQuery(Name = "to")] public string? ToQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "page"), FromQuery(Name = "page")] public string? PageQuery { get; set; }
    [BindProperty(SupportsGet = true, Name = "entry"), FromQuery(Name = "entry")] public string? EntryQuery { get; set; }

    public Guid? EventId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public DateOnly? From { get; private set; }
    public DateOnly? To { get; private set; }
    public int PageNumber { get; private set; } = 1;
    public Guid? EntryId { get; private set; }
    public bool DroppedLinkParts { get; private set; }
    public bool Filtered => EventId is not null || Action.Length > 0 || Actor.Length > 0 || Type.Length > 0 || From is not null || To is not null;

    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];
    public IReadOnlyList<EventOption> EventOptions { get; private set; } = [];
    public IReadOnlyDictionary<Guid, EventOption> EventsById { get; private set; } = new Dictionary<Guid, EventOption>();
    public AuditEntry? SelectedEntry { get; private set; }
    public bool EntryRequested => EntryQuery is not null;
    public bool EntryUnavailable { get; private set; }
    public bool HasNextPage { get; private set; }
    public DateOnly Today { get; private set; }
    public DateTimeOffset Now { get; private set; }

    /// <summary>"UTC+02:00" for an instant in the display zone (summer and winter time).</summary>
    public static string Offset(DateTimeOffset value)
    {
        var offset = DateTimePresentation.ToTimezone(value).Offset;
        return "UTC" + (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>Brief 147: names for this page's entries, resolved in one batch.</summary>
    public AuditNames Names { get; private set; } = AuditNames.Empty;

    // A6: an entry stored without an event shows the event derived from its team, membership or
    // draft (display only; the event filter still uses the stored event id).
    public EventOption? EventOf(AuditEntry entry) => Names.EventFor(entry) is { } id ? EventsById.GetValueOrDefault(id) : null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var events = await dbContext.Events.AsNoTracking()
            .Select(item => new EventOption(item.Id, item.Name, item.HiddenAt != null, item.State, item.EventStartsAt, item.SignupOpensAt, item.SignupClosesAt,
                item.ActualStartedAt, item.State == EventState.Cancelled ? item.CancelledAt : item.ActualEndedAt ?? (item.State == EventState.Archived ? item.ArchivedAt ?? item.FinalizedAt : item.FinalizedAt) ?? item.EventEndsAt))
            .ToListAsync(cancellationToken);
        EventsById = events.ToDictionary(item => item.Id);
        // Ordered as on Events; Discarded events are omitted from the menu (Q7) while their entries stay listed.
        EventOptions = events.Where(item => item.State != EventState.Discarded)
            .OrderBy(item => item.SortGroup).ThenBy(item => item.SortDate is null).ThenBy(item => item.SortDate).ThenBy(item => item.Id).ToList();
        Now = (time ?? TimeProvider.System).GetUtcNow();
        Today = DateOnly.FromDateTime(DateTimePresentation.ToTimezone(Now).DateTime);

        ReadLink();
        var query = Filter(dbContext.AuditEntries.AsNoTracking());

        if (EntryQuery is not null)
        {
            // AU16/D4: read by id with the same visibility and filters as the list.
            SelectedEntry = EntryId is { } entryId ? await query.Where(entry => entry.Id == entryId).SingleOrDefaultAsync(cancellationToken) : null;
            EntryUnavailable = SelectedEntry is null;
        }

        var rows = await query.OrderByDescending(entry => entry.OccurredAt).ThenByDescending(entry => entry.Id)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize + 1).ToListAsync(cancellationToken);
        HasNextPage = rows.Count > PageSize;
        Entries = rows.Take(PageSize).ToList();
        Names = await AuditNameResolver.ResolveAsync(dbContext, SelectedEntry is null ? Entries : Entries.Append(SelectedEntry), cancellationToken);
    }

    private void ReadLink()
    {
        var keys = PageContext?.HttpContext?.Request.Query.Keys ?? [];
        DroppedLinkParts = keys.Any(key => !KnownKeys.Contains(key, StringComparer.OrdinalIgnoreCase));

        if (EventQuery is { Length: > 0 } eventText)
        {
            if (Guid.TryParse(eventText, out var eventId) && EventsById.ContainsKey(eventId)) EventId = eventId;
            else DroppedLinkParts = true;
        }

        if (ActionQuery is { Length: > 0 } actionText)
        {
            var action = actionText.Trim().ToLowerInvariant();
            if (AuditAreas.Find(action) is not null || action.Length <= TextLimit && IsActionKey(action)) Action = action;
            else DroppedLinkParts = true;
        }

        if (ActorQuery is { Length: > 0 } actorText)
        {
            // C-AUD-3: a leading "@" is ignored; matching ignores case.
            var actor = actorText.Trim();
            if (actor.StartsWith('@')) actor = actor[1..].TrimStart();
            if (actor.Length <= TextLimit) Actor = actor;
            else DroppedLinkParts = true;
        }

        if (TypeQuery is { Length: > 0 } typeText)
        {
            var type = typeText.Trim().ToLowerInvariant();
            if (AuditPresenter.TargetLabels.ContainsKey(type)) Type = type;
            else DroppedLinkParts = true;
        }

        From = ReadDate(FromQuery);
        To = ReadDate(ToQuery, end: true);
        if (From is { } from && To is { } to && from > to)
        {
            // The reference keeps the start and drops the end.
            To = null;
            DroppedLinkParts = true;
        }

        if (PageQuery is not null)
        {
            // Strict positive integers only (RC06): "2.5", "2junk", "0" and "+2" are dropped.
            if (PageQuery.Length is > 0 and <= 9 && PageQuery.All(char.IsAsciiDigit)
                && int.TryParse(PageQuery, NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page is >= 1 and <= MaxPage)
                PageNumber = page;
            else DroppedLinkParts = true;
        }

        if (EntryQuery is not null) EntryId = Guid.TryParse(EntryQuery, out var entryId) ? entryId : null;
    }

    private DateOnly? ReadDate(string? value, bool end = false)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { DroppedLinkParts = true; return null; }
        try
        {
            _ = DisplayMidnightUtc(end ? date.AddDays(1) : date);
            return date;
        }
        catch (ArgumentOutOfRangeException) { DroppedLinkParts = true; return null; }
    }

    private IQueryable<AuditEntry> Filter(IQueryable<AuditEntry> query)
    {
        if (Action.Length > 0)
            // RC06 A2: an area uses its key set (S11); a specific action is an exact match.
            query = AuditAreas.Find(Action) is { } area ? area.Apply(query) : query.Where(entry => entry.Action == Action);
        if (Actor.Length > 0)
        {
            var pattern = "%" + Actor.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal) + "%";
            query = query.Where(entry => EF.Functions.ILike(entry.ActorUsername, pattern, @"\"));
        }
        if (Type.Length > 0) query = query.Where(entry => entry.TargetType == Type);
        if (EventId is { } eventId) query = query.Where(entry => entry.EventId == eventId);
        // RC06 A3: whole Copenhagen days, both included; the end is the next local midnight.
        if (From is { } from) { var start = DisplayMidnightUtc(from); query = query.Where(entry => entry.OccurredAt >= start); }
        if (To is { } to) { var end = DisplayMidnightUtc(to.AddDays(1)); query = query.Where(entry => entry.OccurredAt < end); }
        return query;
    }

    public static bool IsActionKey(string value)
    {
        // One or two dots ("area.action" or a composed "area.action.suffix" such as ".wom_sync").
        var parts = value.Split('.');
        return parts.Length is 2 or 3 && parts.All(part => part.Length > 0)
            && value.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character is '_' or '.');
    }

    public string AuditUrl(Guid? eventId = null, bool clearEvent = false, string? action = null, string? actor = null, string? type = null,
        DateOnly? from = null, DateOnly? to = null, bool clearDates = false, int? page = null, Guid? entry = null)
    {
        var values = new List<string>();
        void Add(string key, string? value) { if (!string.IsNullOrEmpty(value)) values.Add(key + "=" + Uri.EscapeDataString(value)); }
        Add("event", clearEvent ? null : (eventId ?? EventId)?.ToString());
        Add("action", action ?? Action);
        Add("actor", actor ?? Actor);
        Add("type", type ?? Type);
        Add("from", clearDates ? null : (from ?? From)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add("to", clearDates ? null : (to ?? To)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var p = page ?? PageNumber;
        if (p > 1) Add("page", p.ToString(CultureInfo.InvariantCulture));
        Add("entry", entry?.ToString());
        return "/Admin/Audit" + (values.Count > 0 ? "?" + string.Join("&", values) : string.Empty);
    }

    public sealed record EventOption(Guid Id, string Name, bool IsHidden, EventState State, DateTimeOffset? EventStartsAt, DateTimeOffset? SignupOpensAt,
        DateTimeOffset? SignupClosesAt, DateTimeOffset? ActualStartedAt, DateTimeOffset? PastDate)
    {
        public bool IsPreparation => State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
        public int SortGroup => State switch { EventState.Live => 0, EventState.Draft or EventState.SignupOpen or EventState.SignupClosed => 1, _ => 2 };
        public long? SortDate => IsPreparation
            ? (EventStartsAt ?? SignupOpensAt ?? SignupClosesAt)?.UtcTicks
            : State == EventState.Live ? ActualStartedAt is { } started ? -started.UtcTicks : null
            : PastDate is { } past ? -past.UtcTicks : null;
    }

    public static DateTimeOffset DisplayMidnightUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        TimeZoneInfo timezone;
        try { timezone = TimeZoneInfo.FindSystemTimeZoneById(DateTimePresentation.DefaultTimezoneId); }
        catch (TimeZoneNotFoundException) { timezone = TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { timezone = TimeZoneInfo.Utc; }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timezone));
    }
}
