using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext, IEventLifecycleService eventLifecycle, TimeProvider timeProvider, IStringLocalizer<SharedResource> localizer) : PageModel
{
    private static readonly string[] KnownStates = ["all", "draft", "signupopen", "signupclosed", "live", "awaitingfinalreview", "finalized", "archived", "cancelled"];

    [BindProperty(SupportsGet = true, Name = "filter")]
    public string? Filter { get; set; }

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    public string ActiveFilter { get; private set; } = "all";
    public string ActiveSearch { get; private set; } = string.Empty;
    public IReadOnlyList<EventRow> Events { get; private set; } = [];
    public IReadOnlyList<StateOption> StateOptions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        ActiveFilter = KnownStates.Contains(Filter ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            ? Filter!.ToLowerInvariant()
            : "all";
        ActiveSearch = Search?.Trim() ?? string.Empty;

        var allEvents = await dbContext.Events.AsNoTracking().Where(item => item.State != EventState.Discarded)
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
                item.ParticipantCap ?? 0,
                dbContext.EventParticipants.Count(participant => participant.EventId == item.Id && participant.SignupStatus == SignupStatus.Confirmed),
                dbContext.EventParticipants.Count(participant => participant.EventId == item.Id && participant.SignupStatus == SignupStatus.WaitingList),
                dbContext.Submissions.Count(submission => submission.EventId == item.Id && submission.Status == SubmissionStatus.Pending),
                dbContext.DraftSessions.Any(session => session.EventId == item.Id && session.State == DraftState.Finalized),
                dbContext.Boards.Any(board => board.EventId == item.Id && board.State == BoardState.Published),
                dbContext.ScheduledEventStartAttempts.Any(attempt => attempt.EventId == item.Id && attempt.ScheduledFor <= now && !attempt.Started && attempt.ResolvedAt == null),
                EventDisplayPhase.Lifecycle))
            .ToListAsync(cancellationToken);

        for (var index = 0; index < allEvents.Count; index++)
        {
            var item = allEvents[index];
            bool? startReady = null;
            if (item.State == EventState.SignupClosed && item.DraftFinalized && item.BoardPublished && !item.StartPostponed)
                startReady = (await eventLifecycle.GetStartReadinessAsync(item.Id, cancellationToken))?.CanProceed;
            allEvents[index] = item with { DisplayPhase = EventDisplayPhaseProjection.From(new(item.State, item.DraftFinalized, item.BoardPublished, item.StartPostponed, startReady)) };
        }

        var ordered = allEvents
            .OrderBy(item => SortGroup(item.State))
            .ThenBy(item => SortDate(item))
            .ThenBy(item => item.Name);

        Events = ordered.Where(MatchesActiveFilter).Where(MatchesSearch).ToList();
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
    }

    public string FormatDate(DateTimeOffset? value, string timezoneId)
    {
        if (value is null) return localizer["Not set"];
        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return TimeZoneInfo.ConvertTime(value.Value, timezone).ToString("dd MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
        }
        catch (TimeZoneNotFoundException)
        {
            return value.Value.ToUniversalTime().ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.CurrentCulture);
        }
        catch (InvalidTimeZoneException)
        {
            return value.Value.ToUniversalTime().ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.CurrentCulture);
        }
    }

    public string SignupSummary(EventRow item)
    {
        var now = timeProvider.GetUtcNow();
        if (item.State == EventState.SignupOpen) return item.SignupClosesAt is null ? localizer["Open"] : localizer["Open until {0}", FormatDate(item.SignupClosesAt, item.Timezone)];
        if (item.State == EventState.Draft && item.SignupOpensAt is { } opensAt && now < opensAt) return localizer["Opens {0}", FormatDate(opensAt, item.Timezone)];
        if (item.State == EventState.Draft) return localizer["Not open"];
        return localizer["Closed"];
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
        _ => true
    };

    private bool MatchesSearch(EventRow item) => string.IsNullOrWhiteSpace(ActiveSearch)
        || item.Name.Contains(ActiveSearch, StringComparison.OrdinalIgnoreCase)
        || item.Slug.Contains(ActiveSearch, StringComparison.OrdinalIgnoreCase);

    private static int SortGroup(EventState state) => state switch
    {
        EventState.Live => 0,
        EventState.Draft or EventState.SignupOpen or EventState.SignupClosed => 1,
        EventState.AwaitingFinalReview => 2,
        EventState.Finalized => 3,
        EventState.Archived => 4,
        _ => 5
    };

    private static long? SortDate(EventRow item) => SortGroup(item.State) switch
    {
        1 => item.EventStartsAt?.UtcTicks,
        2 => item.EventEndsAt is { } eventEndsAt ? -eventEndsAt.UtcTicks : null,
        3 => (item.FinalizedAt ?? item.EventEndsAt) is { } finalizedAt ? -finalizedAt.UtcTicks : null,
        4 => (item.ArchivedAt ?? item.FinalizedAt ?? item.EventEndsAt) is { } archivedAt ? -archivedAt.UtcTicks : null,
        _ => item.EventStartsAt is { } eventStartsAt ? -eventStartsAt.UtcTicks : null
    };

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
        int ParticipantCap,
        int Confirmed,
        int Waiting,
        int PendingReviews,
        bool DraftFinalized,
        bool BoardPublished,
        bool StartPostponed,
        EventDisplayPhase DisplayPhase);

    public sealed record StateOption(string Value, string Label);
}
