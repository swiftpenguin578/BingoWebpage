using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Events;
using Bingo.Web.UI;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Events;

public sealed class SignupsModel(ApplicationDbContext db, ITeamCaptainAuthorityService captainAuthority, IStringLocalizer<SharedResource> text) : PageModel
{
    public string EventName { get; private set; } = string.Empty;
    public string EventDescription { get; private set; } = string.Empty;
    public DateTimeOffset? EventStartsAt { get; private set; }
    public DateTimeOffset? SignupClosesAt { get; private set; }
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public string EventStatus { get; private set; } = string.Empty;
    public int? ParticipantCap { get; private set; }
    public int ConfirmedCount { get; private set; }
    public int WaitingCount { get; private set; }
    public IReadOnlyList<string> Headings { get; private set; } = [];
    public IReadOnlyList<ParticipantView> Confirmed { get; private set; } = [];
    public IReadOnlyList<ParticipantView> Waiting { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug, ct);
        if (item is null) return NotFound();
        var rosterExists = await db.DraftPublicationCycles.AsNoTracking().AnyAsync(x => x.SupersededAt == null && db.DraftSessions.Any(d => d.Id == x.DraftSessionId && d.EventId == item.Id), ct);
        var policy = EventDestinationPolicy.From(item, rosterExists);
        var administrator = await HasHistoricalTableAccessAsync(ct);
        var accountId = User.GetAccountId();
        var captainDraftAccess = !administrator && accountId is { } owner && await captainAuthority.HasDraftSignupTableAccessAsync(owner, item.Id, ct);
        var destination = EventDestinationPolicy.Decide(policy, administrator);
        if (!EventDestinationPolicy.MayUseSignupTable(policy, administrator) && !captainDraftAccess)
        {
            if (rosterExists) return RedirectToPage("Teams", new { slug });
            return destination switch
            {
                EventDestination.Roster => RedirectToPage("Teams", new { slug }),
                EventDestination.Board => RedirectToPage("Board", new { slug }),
                _ => NotFound()
            };
        }

        EventName = item.Name;
        EventDescription = item.Description ?? string.Empty;
        EventStartsAt = item.EventStartsAt;
        SignupClosesAt = item.SignupClosesAt;
        EventTimezone = item.Timezone;
        EventStatus = item.State switch
        {
            EventState.Draft => text["Setup"].Value,
            EventState.SignupOpen => text["Signups open"].Value,
            EventState.SignupClosed => text["Signups closed"].Value,
            EventState.Live => text["Live"].Value,
            EventState.AwaitingFinalReview => text["Final review"].Value,
            EventState.Finalized => text["Finished"].Value,
            EventState.Archived => text["Archived"].Value,
            EventState.Cancelled => text["Cancelled"].Value,
            _ => item.State.ToString()
        };
        ParticipantCap = item.ParticipantCap;
        // Retained historical questions remain visible to administrators, but public
        // projections must honour the same explicit board-visibility flag.
        var questions = await db.SignupQuestions.AsNoTracking().Where(x => x.EventId == item.Id && (captainDraftAccess ? x.Active : x.PublicOnSignupBoard)).OrderBy(x => x.Position).ToListAsync(ct);
        var accountQuestions = questions.Where(x => x.Type == SignupQuestionType.Account).ToList();
        var regularCount = accountQuestions.Count(x => x.AccountAnswerRole == EventCharacterRole.Playing);
        var altCount = accountQuestions.Count(x => x.AccountAnswerRole == EventCharacterRole.Informational);
        var columns = accountQuestions.Select((q, index) => new Column(q.Id, AccountHeading(q.AccountAnswerRole, accountQuestions.Where(x => x.AccountAnswerRole == q.AccountAnswerRole).ToList().IndexOf(q) + 1, regularCount, altCount)))
            .Concat(questions.Where(x => x.SystemField == SignupSystemField.None && x.Type != SignupQuestionType.Account).Select(x => new Column(x.Id, x.Label)))
            .Append(new Column(null, text["Captain volunteer"].Value))
            .ToList();
        Headings = columns.Select(x => x.Heading).ToList();

        var participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == item.Id && (captainDraftAccess ? x.SignupStatus == SignupStatus.Confirmed : x.SignupStatus == SignupStatus.Confirmed || x.SignupStatus == SignupStatus.WaitingList)).OrderBy(x => x.SignedUpAt).ThenBy(x => x.SignupSequence).ToListAsync(ct);
        var participantIds = participants.Select(x => x.Id).ToList();
        var assignments = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                                 join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                 where participantIds.Contains(assignment.EventParticipantId) && assignment.ReleasedAt == null
                                 select new { assignment.EventParticipantId, assignment.SignupQuestionId, assignment.EventRole, assignment.EhbSnapshot, character.DisplayName }).ToListAsync(ct);
        var answers = await db.SignupAnswers.AsNoTracking().Where(x => participantIds.Contains(x.EventParticipantId)).ToListAsync(ct);
        var views = participants.Select((participant, index) =>
        {
            var values = columns.Select(column => ValueFor(column, participant, assignments, answers, text)).ToList();
            var name = assignments.FirstOrDefault(x => x.EventParticipantId == participant.Id && x.EventRole == EventCharacterRole.Playing)?.DisplayName ?? text["Unknown account"].Value;
            return new ParticipantView(name, participant.SignupStatus == SignupStatus.WaitingList ? index - participants.FindIndex(x => x.SignupStatus == SignupStatus.WaitingList) + 1 : null, values);
        }).ToList();
        Confirmed = views.Where((_, index) => participants[index].SignupStatus == SignupStatus.Confirmed).ToList();
        Waiting = views.Where((_, index) => participants[index].SignupStatus == SignupStatus.WaitingList).ToList();
        ConfirmedCount = Confirmed.Count;
        WaitingCount = Waiting.Count;
        return Page();
    }

    private async Task<bool> HasHistoricalTableAccessAsync(CancellationToken ct)
    {
        var accountId = User.GetAccountId();
        return accountId is not null && await db.Accounts.AsNoTracking().AnyAsync(account =>
            account.Id == accountId && account.Active && account.AccountType == AccountType.WebsiteAccount &&
            (account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin), ct);
    }

    private static string ValueFor(Column column, EventParticipant participant, IReadOnlyList<dynamic> assignments, IReadOnlyList<SignupAnswer> answers, IStringLocalizer<SharedResource> text)
    {
        if (column.QuestionId is null) return participant.CaptainVolunteer ? text["Yes"].Value : text["No"].Value;
        var assignment = assignments.FirstOrDefault(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == column.QuestionId);
        if (assignment is not null) return assignment.EventRole == EventCharacterRole.Playing ? $"{assignment.DisplayName} — {assignment.EhbSnapshot:0.##} {text["EHB"].Value}" : assignment.DisplayName;
        return answers.FirstOrDefault(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == column.QuestionId)?.Value ?? text["Not answered"].Value;
    }

    private string AccountHeading(EventCharacterRole? role, int ordinal, int regularCount, int altCount) => role == EventCharacterRole.Playing ? regularCount == 1 ? text["Account"].Value : $"{text["Account"].Value} {ordinal}" : altCount == 1 ? text["Alt account"].Value : $"{text["Alt account"].Value} {ordinal}";
    private sealed record Column(Guid? QuestionId, string Heading);
    public sealed record ParticipantView(string Name, int? WaitingPosition, IReadOnlyList<string> Values);
}
