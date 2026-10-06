using System.Globalization;
using System.Security.Claims;
using Bingo.Application.Dashboard;
using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Navigation;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
[AdminDesign]
public sealed class IndexModel(ApplicationDbContext dbContext, IEventLifecycleService eventLifecycle, TimeProvider timeProvider, IStringLocalizer<SharedResource> localizer, SharedShellService adminActionProjection, IAdminDashboardService dashboard) : PageModel
{
    private static readonly string[] KnownStates = ["all", "draft", "signupopen", "signupclosed", "live", "awaitingfinalreview", "finalized", "archived", "cancelled"];
    private static readonly string[] KnownSorts = ["identity", "state", "dates", "signups", "attention"];

    [BindProperty(SupportsGet = true, Name = "view")]
    public string? View { get; set; }

    public string ActiveView { get; private set; } = "all";
    public int PopulationCount { get; private set; }

    [BindProperty(SupportsGet = true, Name = "filter")]
    public string? Filter { get; set; }

    [BindProperty(SupportsGet = true, Name = "phase")]
    public string? Phase { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    [FromQuery(Name = "page")]
    public int PageNumber { get; set; } = 1;

    public int TotalCount { get; private set; }
    public int PageCount => Math.Max(1, (TotalCount + 24) / 25);
    public int FirstRow => TotalCount == 0 ? 0 : (PageNumber - 1) * 25 + 1;
    public int LastRow => Math.Min(PageNumber * 25, TotalCount);
    public int VisibleCount { get; private set; }
    public int CurrentCount { get; private set; }
    public int PastCount => VisibleCount - CurrentCount;
    public int HiddenCount { get; private set; }
    public int LiveCount { get; private set; }
    public int UpcomingCount { get; private set; }
    public int AttentionCount { get; private set; }
    public bool IsSuperAdmin { get; private set; }
    public bool DroppedLinkParts { get; private set; }
    public IReadOnlyList<string> DuplicateNames { get; private set; } = [];

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "sort")]
    public string? Sort { get; set; }

    [BindProperty(SupportsGet = true, Name = "direction")]
    public string? Direction { get; set; }

    [BindProperty(SupportsGet = true, Name = "attention")]
    public string? Attention { get; set; }

    public bool ActiveAttention { get; private set; }
    public string ActiveFilter { get; private set; } = "all";
    public string ActiveSearch { get; private set; } = string.Empty;
    public string ActiveSort { get; private set; } = string.Empty;
    public string ActiveSortDirection { get; private set; } = "asc";
    public string SortIconPath => ActiveSortDirection == "desc" ? "m6 9 6 6 6-6" : "m18 15-6-6-6 6";
    public IReadOnlyList<EventRow> Events { get; private set; } = [];
    public IReadOnlyList<EventRow> DuplicateEvents { get; private set; } = [];
    public IReadOnlyList<StateOption> StateOptions { get; private set; } = [];
    public bool ActionProjectionUnavailable { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Keep the existing constructor contract; directory reads no longer query readiness.
        _ = eventLifecycle;
        var actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ? accountId : Guid.Empty;
        var actor = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(account =>
            account.Id == actorId && account.AccountType == AccountType.WebsiteAccount && account.Active && account.DisabledAt == null &&
            (account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin), cancellationToken);
        if (actor is null) throw new UnauthorizedAccessException("An active website Admin is required to read the directory.");
        IsSuperAdmin = actor.GlobalRole == GlobalRole.SuperAdmin;
        var requestedView = (View ?? "all").ToLowerInvariant();
        var requestedPhase = (Phase ?? Filter ?? "all").ToLowerInvariant();
        if (Phase is null && requestedPhase == "hidden") { requestedView = "hidden"; requestedPhase = "all"; }
        ActiveView = requestedView switch
        {
            "current" => "current",
            "past" => "past",
            "hidden" when IsSuperAdmin => "hidden",
            _ => "all"
        };
        ActiveFilter = KnownStates.Contains(requestedPhase) && PhaseAllowed(requestedPhase) ? requestedPhase : "all";
        DroppedLinkParts = ActiveView != requestedView || ActiveFilter != requestedPhase;
        ActiveSearch = Search?.Trim() ?? string.Empty;
        ActiveAttention = string.Equals(Attention, "1", StringComparison.Ordinal);
        ActiveSort = KnownSorts.Contains(Sort ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? Sort!.ToLowerInvariant()
            : string.Empty;
        ActiveSortDirection = string.Equals(Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        DroppedLinkParts |= !string.IsNullOrEmpty(Sort) && ActiveSort.Length == 0
            || !string.IsNullOrEmpty(Direction) && Direction.ToLowerInvariant() is not ("asc" or "desc")
            || !string.IsNullOrEmpty(Attention) && Attention is not ("0" or "1")
            || Request.Query.Keys.Any(key => key.ToLowerInvariant() is not ("view" or "phase" or "filter" or "search" or "attention" or "sort" or "direction" or "page" or "create"))
            || !ModelState.IsValid;

        var allEvents = await dbContext.Events.AsNoTracking().Where(item => item.State != EventState.Discarded && (item.HiddenAt == null || IsSuperAdmin))
            .Select(item => new EventRow(
                item.Id,
                item.Name,
                item.Slug,
                item.State,
                item.Timezone,
                item.SignupOpensAt,
                item.SignupClosesAt,
                item.EventStartsAt,
                item.EventEndsAt,
                item.FinalizedAt,
                item.ArchivedAt,
                item.ParticipantCap,
                dbContext.EventParticipants.Count(participant => participant.EventId == item.Id && participant.SignupStatus == SignupStatus.Confirmed),
                dbContext.EventParticipants.Count(participant => participant.EventId == item.Id && participant.SignupStatus == SignupStatus.WaitingList),
                0,
                item.HiddenAt != null)
            {
                ActualStartedAt = item.ActualStartedAt,
                ActualEndedAt = item.ActualEndedAt,
                CancelledAt = item.CancelledAt,
                HiddenAt = item.HiddenAt
            })
            .ToListAsync(cancellationToken);

        var participation = await dashboard.GetEventParticipationAsync(actorId,
            allEvents.Where(item => !item.IsPreparation).Select(item => item.Id).ToArray(), cancellationToken);
        var actionProjection = await adminActionProjection.GetAdminActionsSafelyAsync(cancellationToken);
        ActionProjectionUnavailable = !actionProjection.IsAvailable;

        for (var index = 0; index < allEvents.Count; index++)
        {
            var item = allEvents[index];
            var actions = actionProjection.ForEvent(item.Id);
            allEvents[index] = item with
            {
                ActionCount = actions.Count,
                PendingReviews = actions.PendingEvidenceCount,
                AttentionCategoryCount = actions.CategoryCount,
                Participation = participation.GetValueOrDefault(item.Id),
                ScheduledOpeningFailed = actions.ScheduledOpeningFailed,
                ScheduledStartPostponed = actions.ScheduledStartPostponed
            };
        }

        DuplicateNames = allEvents.Select(item => item.Name).ToList();
        DuplicateEvents = allEvents;
        HiddenCount = allEvents.Count(item => item.IsHidden);
        var visible = allEvents.Where(item => !item.IsHidden).ToList();
        VisibleCount = visible.Count;
        LiveCount = visible.Count(item => item.State == EventState.Live);
        UpcomingCount = visible.Count(item => item.IsPreparation);
        CurrentCount = LiveCount + UpcomingCount;
        AttentionCount = visible.Count(item => item.AttentionCategoryCount > 0);
        var population = allEvents.Where(item => ActiveView == "hidden" ? item.IsHidden : !item.IsHidden).Where(item => ActiveView switch
        {
            "current" => item.IsPreparation || item.State == EventState.Live,
            "past" => !item.IsPreparation && item.State != EventState.Live,
            _ => true
        }).ToList();
        PopulationCount = population.Count;
        var filtered = population.Where(MatchesActiveFilter).Where(MatchesSearch)
            .Where(item => !ActiveAttention || item.AttentionCategoryCount > 0);
        var ordered = OrderEvents(filtered).ToList();
        TotalCount = ordered.Count;
        var requestedPage = PageNumber;
        PageNumber = Math.Clamp(PageNumber, 1, PageCount);
        DroppedLinkParts |= requestedPage != PageNumber;
        Events = ordered.Skip((PageNumber - 1) * 25).Take(25).ToList();
        StateOptions =
        [
            new("all", localizer["All phases"]),
            new("draft", localizer["Setup"]),
            new("signupopen", localizer["Signups open"]),
            new("signupclosed", localizer["Signups closed"]),
            new("live", localizer["Live"]),
            new("awaitingfinalreview", localizer["Final review"]),
            new("finalized", localizer["Finished"]),
            new("archived", localizer["Archived"]),
            new("cancelled", localizer["Cancelled"])
        ];
        StateOptions = StateOptions.Where(option => PhaseAllowed(option.Value)).ToList();
    }


    public bool IsActiveSort(string key) => string.Equals(ActiveSort, key, StringComparison.Ordinal);

    public string NextSortDirection(string key) => NextSortDirection(key, ActiveSort, ActiveSortDirection);

    public string AriaSort(string key) => AriaSort(key, ActiveSort, ActiveSortDirection);

    public string SortAriaLabel(string label, string key) => SortAriaLabel(label, key, ActiveSort, ActiveSortDirection);

    private bool PhaseAllowed(string phase, string? view = null) => phase == "all" || (view ?? ActiveView) switch
    {
        "current" => phase is "draft" or "signupopen" or "signupclosed" or "live",
        "past" => phase is "awaitingfinalreview" or "finalized" or "archived" or "cancelled",
        _ => true
    };

    public string DirectoryUrl(string? view = null, string? phase = null, string? search = null,
        bool? attention = null, string? sort = null, string? direction = null, int page = 1) =>
        Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/Admin/Events", new Dictionary<string, string?>
        {
            ["view"] = view ?? ActiveView, ["phase"] = phase ?? (PhaseAllowed(ActiveFilter, view) ? ActiveFilter : "all"),
            ["search"] = search ?? ActiveSearch, ["attention"] = (attention ?? ActiveAttention) ? "1" : null,
            ["sort"] = sort ?? ActiveSort, ["direction"] = direction ?? ActiveSortDirection,
            ["page"] = page.ToString(CultureInfo.InvariantCulture)
        });

    public string Range(EventRow item)
    {
        if (item.EventStartsAt is not { } start) return localizer["Not scheduled"];
        var a = DateTimePresentation.ToTimezone(start, item.Timezone);
        if (item.EventEndsAt is not { } end) return a.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        var b = DateTimePresentation.ToTimezone(end, item.Timezone);
        return a.ToString(a.Year != b.Year ? "d MMM yyyy" : a.Month != b.Month ? "d MMM" : "%d", CultureInfo.CurrentCulture)
            + "–" + b.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
    }

    public string ContextDate(EventRow item)
    {
        string Day(DateTimeOffset value) => DateTimePresentation.Format(value, "d MMM", item.Timezone, CultureInfo.CurrentCulture);
        string Relative(DateTimeOffset value)
        {
            var days = (DateTimePresentation.ToTimezone(value, item.Timezone).Date - DateTimePresentation.ToTimezone(timeProvider.GetUtcNow(), item.Timezone).Date).Days;
            return days <= 0 ? localizer["today"] : days == 1 ? localizer["tomorrow"] : localizer["in {0} days", days];
        }
        var sub = item.State switch
        {
            EventState.Live when item.EventEndsAt is { } ends => localizer["Ends {0}", Relative(ends)].Value,
            EventState.SignupOpen when item.SignupClosesAt is { } closes => localizer["Signups close {0}", Day(closes)].Value,
            EventState.Draft when item.SignupOpensAt is { } opens => localizer["Signups open {0}", Day(opens)].Value,
            EventState.SignupClosed when item.ScheduledStartPostponed && item.EventStartsAt is { } due => localizer["Start was due {0}", Day(due)].Value,
            EventState.Draft or EventState.SignupOpen or EventState.SignupClosed when item.EventStartsAt is { } starts => localizer["Starts {0}", Relative(starts)].Value,
            EventState.AwaitingFinalReview when item.ActualEndedAt is { } ended => localizer["Ended {0} days ago", Math.Max(0, (DateTimePresentation.ToTimezone(timeProvider.GetUtcNow(), item.Timezone).Date - DateTimePresentation.ToTimezone(ended, item.Timezone).Date).Days)].Value,
            EventState.Cancelled => localizer["Didn’t take place"].Value,
            _ => ""
        };
        return sub + (item.EventStartsAt is not null && item.Timezone != "Europe/Copenhagen" ? " · " + item.Timezone : "");
    }

    public string PeopleMain(EventRow item) => item.IsPreparation
        ? item.ParticipantCap is { } cap ? $"{item.Confirmed:N0} / {cap:N0}" : item.Confirmed > 0 ? localizer["{0} confirmed", item.Confirmed].Value : "—"
        : item.State == EventState.Cancelled ? localizer["{0} confirmed", item.Confirmed].Value
        : item.ParticipantCount is not { } count ? "—"
        : item.State == EventState.Live ? localizer[count == 1 ? "{0} player" : "{0} players", count].Value : localizer["{0} played", count].Value;

    public string PeopleSub(EventRow item) => item.State == EventState.Cancelled ? localizer["when it was cancelled"]
        : !item.IsPreparation ? item.ParticipantCount is null ? localizer["Participant count unavailable"] : ""
        : item.ParticipantCap is not { } cap ? localizer["No capacity set"]
        : item.Waiting > 0 ? (item.Confirmed >= cap ? localizer["Full"].Value + " · " : "") + localizer["{0} waiting", item.Waiting].Value
        : item.Confirmed >= cap ? localizer["Full"]
        : item.State == EventState.Draft ? localizer["Signups not open"]
        : localizer["{0} spots left", cap - item.Confirmed];

    public IReadOnlyList<string> AttentionLabels(EventRow item)
    {
        var labels = new List<string>();
        if (item.ScheduledStartPostponed) labels.Add(localizer["Start postponed"]);
        if (item.ScheduledOpeningFailed) labels.Add(localizer["Signup opening failed"]);
        if (item.PendingReviews > 0) labels.Add(localizer["{0} to review", item.PendingReviews]);
        return labels;
    }

    public static string NextSortDirection(string key, string? activeSort, string activeDirection) =>
        string.Equals(key, activeSort, StringComparison.Ordinal) && string.Equals(activeDirection, "asc", StringComparison.Ordinal)
            ? "desc"
            : "asc";

    public static string AriaSort(string key, string? activeSort, string activeDirection) =>
        string.Equals(key, activeSort, StringComparison.Ordinal)
            ? string.Equals(activeDirection, "desc", StringComparison.Ordinal) ? "descending" : "ascending"
            : "none";

    public static string SortAriaLabel(string label, string key, string? activeSort, string activeDirection) =>
        string.Equals(key, activeSort, StringComparison.Ordinal)
            ? $"Currently sorted by {label} {activeDirection}. Activate to sort {label} {NextSortDirection(key, activeSort, activeDirection)}."
            : $"Sort by {label} ascending.";

    public string FormatDate(DateTimeOffset? value, string timezoneId)
    {
        if (value is null) return localizer["Not set"];
        return DateTimePresentation.Format(value.Value, "dd MMM yyyy, HH:mm", timezoneId, CultureInfo.CurrentCulture);
    }

    public string SignupSummary(EventRow item)
    {
        var now = timeProvider.GetUtcNow();
        if (item.State == EventState.SignupOpen) return item.SignupClosesAt is null ? localizer["Open"] : localizer["Open until {0}", FormatDate(item.SignupClosesAt, item.Timezone)];
        if (item.State == EventState.Draft && item.SignupOpensAt is { } opensAt && now < opensAt) return localizer["Opens {0}", FormatDate(opensAt, item.Timezone)];
        if (item.State == EventState.Draft) return localizer["Not open"];
        return localizer["Closed"];
    }

    public string AttentionSummary(EventRow item)
    {
        if (item.AttentionCategoryCount == 0) return "—";
        var summary = item.ScheduledStartPostponed ? localizer["Start postponed"].Value
            : item.ScheduledOpeningFailed ? localizer["Signup opening failed"].Value
            : localizer["{0} to review", item.PendingReviews].Value;
        return item.AttentionCategoryCount > 1 ? $"{summary} +{item.AttentionCategoryCount - 1}" : summary;
    }

    private bool MatchesActiveFilter(EventRow item) => ActiveFilter switch
    {
        "draft" => item.State == EventState.Draft,
        "signupopen" => item.State == EventState.SignupOpen,
        "signupclosed" => item.State == EventState.SignupClosed,
        "live" => item.State == EventState.Live,
        "awaitingfinalreview" => item.State == EventState.AwaitingFinalReview,
        "finalized" => item.State == EventState.Finalized,
        "archived" => item.State == EventState.Archived,
        "cancelled" => item.State == EventState.Cancelled,
        "hidden" => item.IsHidden,
        _ => true
    };

    private bool MatchesSearch(EventRow item) => string.IsNullOrWhiteSpace(ActiveSearch)
        || item.Name.Contains(ActiveSearch, StringComparison.OrdinalIgnoreCase);

    private IEnumerable<EventRow> OrderEvents(IEnumerable<EventRow> source)
    {
        if (string.IsNullOrEmpty(ActiveSort))
            return ActiveView == "hidden" ? source.OrderByDescending(item => item.HiddenAt).ThenBy(item => item.Id)
                : source.OrderBy(item => SortGroup(item.State)).ThenBy(item => SortDate(item) is null).ThenBy(item => SortDate(item)).ThenBy(item => item.Id);

        var descending = ActiveSortDirection == "desc";
        IOrderedEnumerable<EventRow> ordered = ActiveSort switch
        {
            "identity" => descending ? source.OrderByDescending(item => item.Name, StringComparer.Create(CultureInfo.CurrentCulture, true)) : source.OrderBy(item => item.Name, StringComparer.Create(CultureInfo.CurrentCulture, true)),
            "state" => descending ? source.OrderByDescending(item => (int)item.State) : source.OrderBy(item => (int)item.State),
            "dates" => descending
                ? source.OrderBy(item => item.EventStartsAt is null).ThenByDescending(item => item.EventStartsAt).ThenByDescending(item => item.EventEndsAt)
                : source.OrderBy(item => item.EventStartsAt is null).ThenBy(item => item.EventStartsAt).ThenBy(item => item.EventEndsAt),
            "signups" => descending
                ? source.OrderBy(item => item.ParticipantCount is null).ThenByDescending(item => item.ParticipantCount)
                : source.OrderBy(item => item.ParticipantCount is null).ThenBy(item => item.ParticipantCount),
            "attention" => descending ? source.OrderByDescending(item => item.AttentionPriority).ThenByDescending(item => item.AttentionCategoryCount).ThenByDescending(item => item.PendingReviews) : source.OrderBy(item => item.AttentionPriority).ThenBy(item => item.AttentionCategoryCount).ThenBy(item => item.PendingReviews),
            _ => source.OrderBy(item => SortGroup(item.State)).ThenBy(item => SortDate(item))
        };

        return ordered.ThenBy(item => item.Id);
    }

    private static int SortGroup(EventState state) => state switch
    {
        EventState.Live => 0,
        EventState.Draft or EventState.SignupOpen or EventState.SignupClosed => 1,
        _ => 2
    };

    private static long? SortDate(EventRow item) => item.IsPreparation
        ? (item.EventStartsAt ?? item.SignupOpensAt ?? item.SignupClosesAt)?.UtcTicks
        : item.State == EventState.Live
            ? item.ActualStartedAt is { } started ? -started.UtcTicks : null
            : item.PastDate is { } past ? -past.UtcTicks : null;

    public sealed record EventRow(
        Guid Id,
        string Name,
        string Slug,
        EventState State,
        string Timezone,
        DateTimeOffset? SignupOpensAt,
        DateTimeOffset? SignupClosesAt,
        DateTimeOffset? EventStartsAt,
        DateTimeOffset? EventEndsAt,
        DateTimeOffset? FinalizedAt,
        DateTimeOffset? ArchivedAt,
        int? ParticipantCap,
        int Confirmed,
        int Waiting,
        int PendingReviews,
        bool IsHidden = false,
        int ActionCount = 0,
        bool ScheduledOpeningFailed = false,
        bool ScheduledStartPostponed = false)
    {
        public DateTimeOffset? ActualStartedAt { get; init; }
        public DateTimeOffset? ActualEndedAt { get; init; }
        public DateTimeOffset? CancelledAt { get; init; }
        public DateTimeOffset? HiddenAt { get; init; }
        public EventParticipationSummary? Participation { get; init; }
        public int AttentionCategoryCount { get; init; }
        public int AttentionPriority => ScheduledStartPostponed || ScheduledOpeningFailed ? 2 : PendingReviews > 0 ? 1 : 0;
        public bool IsPreparation => State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
        public long? ParticipantCount => IsPreparation || State == EventState.Cancelled ? Confirmed : Participation?.Participants.IsAvailable == true ? Participation.Participants.Value : null;
        public DateTimeOffset? PastDate => State == EventState.Cancelled ? CancelledAt
            : ActualEndedAt ?? (State == EventState.Archived ? ArchivedAt ?? FinalizedAt : FinalizedAt) ?? EventEndsAt;
    }

    public sealed record StateOption(string Value, string Label);
}
