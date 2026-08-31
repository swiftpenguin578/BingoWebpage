using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext, IEventLifecycleService eventLifecycle, IEventCompetitionSynchronizationService competitionSynchronization, TimeProvider timeProvider, IStringLocalizer<SharedResource> localizer) : PageModel
{
    public int PendingReviewCount { get; private set; }
    public int AttentionEventCount { get; private set; }
    public IReadOnlyList<EventSummary> ActiveEvents { get; private set; } = [];
    public IReadOnlyList<EventSummary> AttentionEvents { get; private set; } = [];
    public IReadOnlyList<MilestoneSummary> UpcomingMilestones { get; private set; } = [];
    public IReadOnlyList<PendingEvidenceSummary> PendingEvidence { get; private set; } = [];
    public IReadOnlyList<EventSummary> LifecycleReadiness { get; private set; } = [];
    public IReadOnlyList<WiseOldManSummary> WiseOldManSynchronizations { get; private set; } = [];
    public IReadOnlyList<AuditSummary> RecentAudits { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var allEvents = await dbContext.Events.AsNoTracking().Where(item => item.HiddenAt == null && item.State != EventState.Discarded)
            .Select(item => new EventSummary(
                item.Id,
                item.Name,
                item.Slug,
                item.State,
                item.Timezone,
                item.SignupOpensAt,
                item.SignupClosesAt,
                item.EventStartsAt,
                item.EventEndsAt,
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
            EventStartReadiness? startReadiness = null;
            if (item.State == EventState.SignupClosed && item.DraftFinalized && item.BoardPublished && !item.StartPostponed)
                startReadiness = await eventLifecycle.GetStartReadinessAsync(item.Id, cancellationToken);
            allEvents[index] = item with
            {
                DisplayPhase = EventDisplayPhaseProjection.From(new(item.State, item.DraftFinalized, item.BoardPublished, item.StartPostponed, startReadiness?.CanProceed)),
                StartReadinessBlockers = startReadiness?.Blockers ?? []
            };
        }

        var ordered = allEvents
            .OrderBy(item => SortGroup(item.State))
            .ThenBy(item => SortDate(item))
            .ThenBy(item => item.Name)
            .ToList();

        PendingReviewCount = allEvents.Sum(item => item.PendingReviews);
        AttentionEventCount = allEvents.Count(IsAttention);
        ActiveEvents = ordered.Where(item => item.State == EventState.Live || IsUpcoming(item)).Take(6).ToList();
        AttentionEvents = ordered.Where(IsAttention).OrderByDescending(item => item.PendingReviews).ThenByDescending(item => item.StartPostponed).Take(6).ToList();

        var relevantEvents = ordered.Where(item => item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview).Take(8).ToList();
        UpcomingMilestones = relevantEvents
            .Select(item => NextMilestone(item, now))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderBy(item => item.At)
            .ThenBy(item => item.EventName)
            .Take(8)
            .ToList();
        LifecycleReadiness = relevantEvents.Take(6).ToList();

        var pendingEvidenceByEvent = await dbContext.Submissions.AsNoTracking()
            .Where(item => item.Status == SubmissionStatus.Pending)
            .GroupBy(item => item.EventId)
            .Select(group => new PendingEvidenceAggregate(group.Key, group.Count(), group.Min(item => item.SubmittedAt)))
            .ToListAsync(cancellationToken);
        var eventNames = allEvents.ToDictionary(item => item.Id);
        PendingEvidence = pendingEvidenceByEvent
            .Where(item => eventNames.ContainsKey(item.EventId))
            .Select(item => new PendingEvidenceSummary(item.EventId, eventNames[item.EventId].Name, eventNames[item.EventId].Slug, eventNames[item.EventId].Timezone, item.Count, item.OldestSubmittedAt))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.OldestSubmittedAt)
            .ToList();

        var wiseOldMan = new List<WiseOldManSummary>();
        foreach (var item in relevantEvents)
        {
            var synchronization = await competitionSynchronization.GetAsync(item.Id, cancellationToken);
            if (synchronization is { Configured: true })
                wiseOldMan.Add(new(item.Id, item.Name, synchronization.CompetitionId!.Value, synchronization.LastSuccessfulAt, synchronization.NormalDueAt, synchronization.RetryDueAt, synchronization.Complete, synchronization.MissingAccounts, synchronization.LastError));
        }
        WiseOldManSynchronizations = wiseOldMan;

        RecentAudits = await (from audit in dbContext.AuditEntries.AsNoTracking()
                              join bingoEvent in dbContext.Events.AsNoTracking() on audit.EventId equals bingoEvent.Id into events
                              from bingoEvent in events.DefaultIfEmpty()
                              where audit.EventId == null || bingoEvent.HiddenAt == null
                              orderby audit.OccurredAt descending
                              select new AuditSummary(audit.OccurredAt, audit.ActorUsername, audit.Action, audit.Details, bingoEvent == null ? null : bingoEvent.Name))
            .Take(4)
            .ToListAsync(cancellationToken);
    }

    public string StatusLabel(EventSummary item) => item.DisplayPhase switch
    {
        EventDisplayPhase.SignupsClosed => localizer["Signups closed"],
        EventDisplayPhase.DraftFinalized => localizer["Draft finalized"],
        EventDisplayPhase.EventReady => localizer["Event ready · Starts {0}", FormatDate(item.EventStartsAt, item.Timezone)],
        EventDisplayPhase.BoardPublished => localizer["Board published · Not ready"],
        EventDisplayPhase.StartPostponed => localizer["Start postponed"],
        EventDisplayPhase.Live => localizer["Live"],
        _ => item.State switch
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

    public string AttentionLabel(EventSummary item)
    {
        if (item.PendingReviews > 0) return localizer["{0} pending review(s)", item.PendingReviews];
        if (item.StartPostponed) return localizer["Start postponed"];
        return item.DisplayPhase switch
        {
            EventDisplayPhase.SignupsClosed => localizer["Setup incomplete"],
            EventDisplayPhase.DraftFinalized => localizer["Board not published"],
            EventDisplayPhase.BoardPublished => localizer["Start not ready"],
            _ when item.State == EventState.AwaitingFinalReview => localizer["Final review"],
            _ => localizer["Needs attention"]
        };
    }

    public string FormatDate(DateTimeOffset? value, string timezoneId)
    {
        if (value is null) return localizer["Not set"];
        return DateTimePresentation.Format(value.Value, "dd MMM yyyy, HH:mm", timezoneId, System.Globalization.CultureInfo.CurrentCulture);
    }

    public string SignupSummary(EventSummary item)
    {
        var now = timeProvider.GetUtcNow();
        if (item.State == EventState.SignupOpen) return item.SignupClosesAt is null ? localizer["Open"] : localizer["Open until {0}", FormatDate(item.SignupClosesAt, item.Timezone)];
        if (item.State == EventState.Draft && item.SignupOpensAt is { } opensAt && now < opensAt) return localizer["Opens {0}", FormatDate(opensAt, item.Timezone)];
        if (item.State == EventState.Draft) return localizer["Not open"];
        return localizer["Closed"];
    }

    public string ReadinessSummary(EventSummary item)
    {
        if (item.StartBlockers.Count > 0) return localizer["{0} blocker(s) prevent the next transition", item.StartBlockers.Count];
        if (IsAttention(item)) return AttentionLabel(item);
        return localizer["No active blockers"];
    }

    public string FormatDateTime(DateTimeOffset? value, string timezoneId) => FormatDate(value, timezoneId);

    public string SynchronizationDueLabel(WiseOldManSummary item) => item.RetryDueAt is { } retry
        ? DateTimePresentation.Format(retry, "dd MMM yyyy, HH:mm", provider: System.Globalization.CultureInfo.CurrentCulture)
        : item.NormalDueAt is { } due ? DateTimePresentation.Format(due, "dd MMM yyyy, HH:mm", provider: System.Globalization.CultureInfo.CurrentCulture) : localizer["Not scheduled"];

    public string SynchronizationWarning(WiseOldManSummary item) => !string.IsNullOrWhiteSpace(item.LastError)
        ? item.LastError!
        : item.MissingAccounts.Count > 0
            ? localizer["Some current Playing accounts are missing."]
            : localizer["Latest synchronization is incomplete."];

    private MilestoneSummary? NextMilestone(EventSummary item, DateTimeOffset now)
    {
        (string Label, DateTimeOffset? At)? milestone = item.State switch
        {
            EventState.Draft when item.SignupOpensAt > now => (localizer["Signups open"].Value, item.SignupOpensAt),
            EventState.Draft => (localizer["Event starts"].Value, item.EventStartsAt),
            EventState.SignupOpen => (localizer["Signups close"].Value, item.SignupClosesAt),
            EventState.SignupClosed => (localizer["Event starts"].Value, item.EventStartsAt),
            EventState.Live => (localizer["Event ends"].Value, item.EventEndsAt),
            _ => null
        };
        return milestone is { At: { } at }
            ? new(item.Id, item.Name, item.Slug, milestone.Value.Label, at, item.Timezone)
            : null;
    }

    private static bool IsUpcoming(EventSummary item) => item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;

    private static bool IsAttention(EventSummary item) => item.PendingReviews > 0
        || item.StartPostponed
        || item.State == EventState.AwaitingFinalReview
        || item.DisplayPhase is EventDisplayPhase.SignupsClosed or EventDisplayPhase.DraftFinalized or EventDisplayPhase.BoardPublished;

    private static int SortGroup(EventState state) => state switch
    {
        EventState.Live => 0,
        EventState.Draft or EventState.SignupOpen or EventState.SignupClosed => 1,
        EventState.AwaitingFinalReview => 2,
        EventState.Finalized => 3,
        EventState.Archived => 4,
        _ => 5
    };

    private static long? SortDate(EventSummary item) => SortGroup(item.State) switch
    {
        1 => item.EventStartsAt?.UtcTicks,
        2 => item.EventEndsAt is { } eventEndsAt ? -eventEndsAt.UtcTicks : null,
        3 => item.EventEndsAt is { } finalizedAt ? -finalizedAt.UtcTicks : null,
        4 => item.EventEndsAt is { } archivedAt ? -archivedAt.UtcTicks : null,
        _ => item.EventStartsAt is { } eventStartsAt ? -eventStartsAt.UtcTicks : null
    };

    public sealed record EventSummary(Guid Id, string Name, string Slug, EventState State, string Timezone, DateTimeOffset? SignupOpensAt, DateTimeOffset? SignupClosesAt, DateTimeOffset? EventStartsAt, DateTimeOffset? EventEndsAt, int ParticipantCap, int Confirmed, int Waiting, int PendingReviews, bool DraftFinalized, bool BoardPublished, bool StartPostponed, EventDisplayPhase DisplayPhase, IReadOnlyList<ReadinessItem>? StartReadinessBlockers = null)
    {
        public IReadOnlyList<ReadinessItem> StartBlockers => StartReadinessBlockers ?? [];
    }

    public sealed record MilestoneSummary(Guid EventId, string EventName, string EventSlug, string Label, DateTimeOffset At, string Timezone);
    public sealed record PendingEvidenceSummary(Guid EventId, string EventName, string EventSlug, string Timezone, int Count, DateTimeOffset OldestSubmittedAt);
    public sealed record WiseOldManSummary(Guid EventId, string EventName, long CompetitionId, DateTimeOffset? LastSuccessfulAt, DateTimeOffset? NormalDueAt, DateTimeOffset? RetryDueAt, bool? Complete, IReadOnlyList<string> MissingAccounts, string? LastError);
    private sealed record PendingEvidenceAggregate(Guid EventId, int Count, DateTimeOffset OldestSubmittedAt);
    public sealed record AuditSummary(DateTimeOffset OccurredAt, string ActorUsername, string Action, string? Details, string? EventName);
}
