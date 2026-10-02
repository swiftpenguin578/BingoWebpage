using System.Globalization;
using System.Security.Claims;
using Bingo.Application.Dashboard;
using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Navigation;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext, IEventLifecycleService eventLifecycle, TimeProvider timeProvider, IStringLocalizer<SharedResource> localizer, SharedShellService adminActionProjection, IAdminDashboardService dashboard) : PageModel
{
    private static readonly string[] KnownStates = ["all", "draft", "signupopen", "signupclosed", "live", "awaitingfinalreview", "finalized", "archived", "cancelled", "hidden"];
    private static readonly string[] KnownSorts = ["identity", "state", "dates", "signups", "attention"];

    [BindProperty(SupportsGet = true, Name = "view")]
    public string? View { get; set; }

    public string ActiveView { get; private set; } = "all";
    public int PopulationCount { get; private set; }

    [BindProperty(SupportsGet = true, Name = "filter")]
    public string? Filter { get; set; }

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
    public IReadOnlyList<StateOption> StateOptions { get; private set; } = [];
    public bool ActionProjectionUnavailable { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var activeRosterEventIds =
            from cycle in dbContext.DraftPublicationCycles.AsNoTracking()
            join draft in dbContext.DraftSessions.AsNoTracking() on cycle.DraftSessionId equals draft.Id
            where draft.State == DraftState.Finalized
                  && cycle.SupersededAt == null
                  && dbContext.DraftPublicationRosters.Any(roster => roster.DraftPublicationCycleId == cycle.Id)
            select draft.EventId;
        var actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ? accountId : Guid.Empty;
        var actor = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(account =>
            account.Id == actorId && account.AccountType == AccountType.WebsiteAccount && account.Active && account.DisabledAt == null &&
            (account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin), cancellationToken);
        if (actor is null) throw new UnauthorizedAccessException("An active website Admin is required to read the directory.");
        var isSuperAdmin = actor.GlobalRole == GlobalRole.SuperAdmin;
        ActiveFilter = KnownStates.Contains(Filter ?? string.Empty, StringComparer.OrdinalIgnoreCase) && (isSuperAdmin || !string.Equals(Filter, "hidden", StringComparison.OrdinalIgnoreCase))
            ? Filter!.ToLowerInvariant()
            : "all";
        ActiveView = (View ?? "all").ToLowerInvariant() switch
        {
            "current" => "current",
            "past" => "past",
            "hidden" when isSuperAdmin => "hidden",
            _ => "all"
        };
        if (ActiveFilter == "hidden") ActiveView = "hidden";
        ActiveSearch = Search?.Trim() ?? string.Empty;
        ActiveAttention = string.Equals(Attention, "1", StringComparison.Ordinal);
        ActiveSort = KnownSorts.Contains(Sort ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? Sort!.ToLowerInvariant()
            : string.Empty;
        ActiveSortDirection = string.Equals(Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

        var allEvents = await dbContext.Events.AsNoTracking().Where(item => item.State != EventState.Discarded && (ActiveView == "hidden" ? item.HiddenAt != null && isSuperAdmin : item.HiddenAt == null))
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
                dbContext.Submissions.Count(submission => submission.EventId == item.Id && submission.Status == SubmissionStatus.Pending),
                activeRosterEventIds.Contains(item.Id),
                dbContext.Boards.Any(board => board.EventId == item.Id && board.State == BoardState.Published),
                dbContext.ScheduledEventStartAttempts.Any(attempt => attempt.EventId == item.Id && attempt.ScheduledFor == item.EventStartsAt && attempt.ScheduledFor <= now && !attempt.Started && attempt.ResolvedAt == null && (item.State == EventState.Draft || item.State == EventState.SignupOpen || item.State == EventState.SignupClosed)),
                EventDisplayPhase.Lifecycle,
                item.HiddenAt != null)
            {
                ActualStartedAt = item.ActualStartedAt,
                ActualEndedAt = item.ActualEndedAt,
                CancelledAt = item.CancelledAt
            })
            .ToListAsync(cancellationToken);

        var participation = await dashboard.GetEventParticipationAsync(actorId,
            allEvents.Where(item => !item.IsPreparation).Select(item => item.Id).ToArray(), cancellationToken);
        var actionProjection = await adminActionProjection.GetAdminActionsSafelyAsync(cancellationToken);
        ActionProjectionUnavailable = !actionProjection.IsAvailable;

        for (var index = 0; index < allEvents.Count; index++)
        {
            var item = allEvents[index];
            bool? startReady = null;
            if (item.State == EventState.SignupClosed && item.DraftFinalized && item.BoardPublished && !item.StartPostponed)
                startReady = (await eventLifecycle.GetStartReadinessAsync(item.Id, cancellationToken))?.CanProceed;
            var actions = actionProjection.ForEvent(item.Id);
            allEvents[index] = item with
            {
                DisplayPhase = EventDisplayPhaseProjection.From(new(item.State, item.DraftFinalized, item.BoardPublished, item.StartPostponed, startReady)),
                ActionCount = actions.Count,
                PendingReviews = actions.PendingEvidenceCount,
                AttentionCategoryCount = actions.CategoryCount,
                Participation = participation.GetValueOrDefault(item.Id),
                ScheduledOpeningFailed = actions.ScheduledOpeningFailed,
                ScheduledStartPostponed = actions.ScheduledStartPostponed
            };
        }

        var population = allEvents.Where(item => ActiveView switch
        {
            "current" => item.IsPreparation || item.State == EventState.Live,
            "past" => !item.IsPreparation && item.State != EventState.Live,
            _ => true
        }).ToList();
        PopulationCount = population.Count;
        var filtered = population.Where(MatchesActiveFilter).Where(MatchesSearch)
            .Where(item => !ActiveAttention || item.AttentionCategoryCount > 0);
        Events = OrderEvents(filtered).ToList();
        StateOptions =
        [
            new("all", localizer["All states"]),
            new("draft", localizer["Setup"]),
            new("signupopen", localizer["Signups open"]),
            new("signupclosed", localizer["Signups closed"]),
            new("live", localizer["Live"]),
            new("awaitingfinalreview", localizer["Final review"]),
            new("finalized", localizer["Finished"]),
            new("archived", localizer["Archived"]),
            new("cancelled", localizer["Cancelled"])
        ];
        if (isSuperAdmin) StateOptions = StateOptions.Append(new("hidden", localizer["Hidden · quarantine"])).ToList();
    }

    public string StatusLabel(EventRow item) => item.DisplayPhase switch
    {
        EventDisplayPhase.SignupsClosed => localizer["Signups closed"],
        EventDisplayPhase.DraftFinalized => localizer["Draft finalized"],
        EventDisplayPhase.EventReady => localizer["Event ready · Starts {0}", FormatDate(item.EventStartsAt, item.Timezone)],
        EventDisplayPhase.BoardPublished => localizer["Board published · Not ready"],
        EventDisplayPhase.StartPostponed => localizer["Start postponed"],
        EventDisplayPhase.Live => localizer["Live"],
        _ => item.IsHidden ? localizer["Hidden"] : item.State switch
        {
            EventState.Draft => localizer["Setup"],
            EventState.SignupOpen => localizer["Signups open"],
            EventState.SignupClosed => localizer["Signups closed"],
            EventState.AwaitingFinalReview => localizer["Final review"],
            EventState.Finalized => localizer["Finished"],
            EventState.Archived => localizer["Archived"],
            EventState.Cancelled => localizer["Cancelled"],
            _ => localizer["Unknown"]
        }
    };

    public bool IsActiveSort(string key) => string.Equals(ActiveSort, key, StringComparison.Ordinal);

    public string NextSortDirection(string key) => NextSortDirection(key, ActiveSort, ActiveSortDirection);

    public string AriaSort(string key) => AriaSort(key, ActiveSort, ActiveSortDirection);

    public string SortAriaLabel(string label, string key) => SortAriaLabel(label, key, ActiveSort, ActiveSortDirection);

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
            return source.OrderBy(item => SortGroup(item.State)).ThenBy(item => SortDate(item) is null).ThenBy(item => SortDate(item)).ThenBy(item => item.Id);

        var descending = ActiveSortDirection == "desc";
        IOrderedEnumerable<EventRow> ordered = ActiveSort switch
        {
            "identity" => descending ? source.OrderByDescending(item => item.Name, StringComparer.Ordinal) : source.OrderBy(item => item.Name, StringComparer.Ordinal),
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
        bool DraftFinalized,
        bool BoardPublished,
        bool StartPostponed,
        EventDisplayPhase DisplayPhase,
        bool IsHidden = false,
        int ActionCount = 0,
        bool ScheduledOpeningFailed = false,
        bool ScheduledStartPostponed = false)
    {
        public DateTimeOffset? ActualStartedAt { get; init; }
        public DateTimeOffset? ActualEndedAt { get; init; }
        public DateTimeOffset? CancelledAt { get; init; }
        public EventParticipationSummary? Participation { get; init; }
        public int AttentionCategoryCount { get; init; }
        public int AttentionPriority => ScheduledStartPostponed || ScheduledOpeningFailed ? 2 : PendingReviews > 0 ? 1 : 0;
        public bool IsPreparation => State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
        public long? ParticipantCount => IsPreparation ? Confirmed : Participation?.Participants.IsAvailable == true ? Participation.Participants.Value : null;
        public DateTimeOffset? PastDate => State == EventState.Cancelled ? CancelledAt
            : ActualEndedAt ?? (State == EventState.Archived ? ArchivedAt ?? FinalizedAt : FinalizedAt) ?? EventEndsAt;
    }

    public sealed record StateOption(string Value, string Label);
}
