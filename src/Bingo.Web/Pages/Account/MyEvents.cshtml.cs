using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Account;

[Authorize]
public sealed class MyEventsModel(ApplicationDbContext db, IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IReadOnlyList<EventRow> Current { get; private set; } = [];
    public IReadOnlyList<EventRow> History { get; private set; } = [];
    public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> EvidenceHistory { get; private set; } = new Dictionary<Guid, IReadOnlyList<Guid>>();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var accountId = User.GetAccountId();
        if (accountId is null || !await db.Accounts.AsNoTracking().AnyAsync(x => x.Id == accountId && x.AccountType == AccountType.WebsiteAccount, ct)) return Forbid();
        var rows = await (from participant in db.EventParticipants.AsNoTracking()
                          join item in db.Events.AsNoTracking() on participant.EventId equals item.Id
                          where participant.AccountId == accountId
                          orderby item.EventStartsAt descending, participant.SignedUpAt descending
                          select new EventRow(item.Id, item.Name, item.Slug, item.State, item.FirstPublicAt, item.ActualSignupOpenedAt, item.DraftLocked, item.TeamRostersPublished, item.DraftResultsPublished, item.BoardPublished, item.ResultsPublished, db.DraftPublicationCycles.Any(cycle => cycle.SupersededAt == null && db.DraftSessions.Any(draft => draft.Id == cycle.DraftSessionId && draft.EventId == item.Id)), db.Boards.Any(board => board.EventId == item.Id && board.State == BoardState.Published), participant.Id, participant.SignupStatus, participant.SignedUpAt,
                              db.TeamMemberships.Where(membership => membership.EventParticipantId == participant.Id && membership.LeftAt == null)
                                  .Join(db.Teams, membership => membership.TeamId, team => team.Id, (_, team) => team.Slug).FirstOrDefault())).ToListAsync(ct);
        Current = rows.Where(x => x.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview).ToList();
        History = rows.Where(x => !Current.Contains(x)).ToList();
        var historyEventIds = History.Select(x => x.EventId).ToList();
        if (historyEventIds.Count > 0)
        {
            var evidenceRows = await (from asset in db.EvidenceAssets.AsNoTracking()
                                      join submission in db.Submissions.AsNoTracking() on asset.SubmissionId equals submission.Id
                                      join bingoEvent in db.Events.AsNoTracking() on submission.EventId equals bingoEvent.Id
                                      where historyEventIds.Contains(submission.EventId) && bingoEvent.State == EventState.Archived && submission.CreditedParticipantId != Guid.Empty &&
                                            (submission.Status == Bingo.Domain.Evidence.SubmissionStatus.Rejected || submission.Status == Bingo.Domain.Evidence.SubmissionStatus.Withdrawn) &&
                                            db.EventParticipants.Any(participant => participant.Id == submission.CreditedParticipantId && participant.AccountId == accountId.Value)
                                      select new { submission.EventId, asset.Id }).ToListAsync(ct);
            EvidenceHistory = evidenceRows.GroupBy(x => x.EventId).ToDictionary(group => group.Key, group => (IReadOnlyList<Guid>)group.Select(x => x.Id).ToList());
        }
        return Page();
    }

    public string DisplayLabel(EventRow item) => item.DisplayPhase switch
    {
        EventDisplayPhase.SignupsClosed => localizer["Signups closed"],
        EventDisplayPhase.DraftFinalized => localizer["Draft finalized"],
        EventDisplayPhase.BoardPublished or EventDisplayPhase.EventReady => localizer["Board published"],
        EventDisplayPhase.StartPostponed => localizer["Start postponed"],
        EventDisplayPhase.Live => localizer["Live"],
        _ => item.Status == SignupStatus.WaitingList ? localizer["Waiting list"] : localizer[item.Status.ToString()]
    };

    public string DisplayStateModifier(EventRow item) => item.DisplayPhase switch
    {
        EventDisplayPhase.Live => "public-ui-state--success",
        EventDisplayPhase.StartPostponed => "public-ui-state--warning",
        EventDisplayPhase.BoardPublished or EventDisplayPhase.EventReady => "public-ui-state--information",
        EventDisplayPhase.SignupsClosed or EventDisplayPhase.DraftFinalized => "public-ui-state--neutral",
        _ => item.Status switch
        {
            SignupStatus.WaitingList => "public-ui-state--warning",
            SignupStatus.Withdrawn => "public-ui-state--neutral",
            _ => "public-ui-state--success"
        }
    };

    public sealed record EventRow(Guid EventId, string Name, string Slug, EventState State, DateTimeOffset? FirstPublicAt, DateTimeOffset? ActualSignupOpenedAt, bool DraftLocked, bool TeamRostersPublished, bool DraftResultsPublished, bool BoardPublished, bool ResultsPublished, bool RosterExists, bool PublishedBoardExists, Guid ParticipantId, SignupStatus Status, DateTimeOffset SignedUpAt, string? TeamSlug)
    {
        private EventDestination Destination => EventDestinationPolicy.Decide(new EventRouteState(State, FirstPublicAt, ActualSignupOpenedAt is not null || State is EventState.SignupOpen or EventState.SignupClosed || DraftLocked, RosterExists || TeamRostersPublished || DraftResultsPublished, BoardPublished || PublishedBoardExists, ResultsPublished), false);
        public string DestinationPage => Destination switch
        {
            EventDestination.SignupTable => "/Events/Confirmation",
            EventDestination.Roster => "/Events/Teams",
            EventDestination.Board when TeamSlug is not null => "/Events/TeamBoard",
            EventDestination.Board => "/Events/Board",
            _ => "/Events/Confirmation"
        };
        public bool IsConfirmation => Destination == EventDestination.SignupTable && State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
        public EventDisplayPhase DisplayPhase => EventDisplayPhaseProjection.From(new(State, RosterExists || TeamRostersPublished || DraftResultsPublished, BoardPublished || PublishedBoardExists));
    }
}
