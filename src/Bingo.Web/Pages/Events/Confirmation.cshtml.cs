using Bingo.Application.Signups;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Events;

public sealed class ConfirmationModel(ApplicationDbContext db, TimeProvider timeProvider, ISignupService signupService, IStringLocalizer<SharedResource> text) : PageModel
{
    public string EventName { get; private set; } = string.Empty;
    public string EventSlug { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int? WaitingPosition { get; private set; }
    public bool CanEdit { get; private set; }
    public bool CanWithdraw { get; private set; }
    public bool CanRejoin { get; private set; }
    public bool CanViewTable { get; private set; }
    public bool IsReadOnly { get; private set; }
    public string NextStep { get; private set; } = "Your signup will appear in the event roster when it is published.";
    [BindProperty] public bool ConfirmLifecycleAction { get; set; }
    public IReadOnlyList<AccountView> Accounts { get; private set; } = [];
    public IReadOnlyList<AnswerView> Answers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug, Guid? participantId, CancellationToken ct)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Slug == slug && item.HiddenAt == null, ct)) return NotFound();
        if (participantId is not null)
        {
            var accountId = User.GetAccountId();
            if (accountId is null || User.FindFirst("bingo:account_type")?.Value != "WebsiteAccount")
                return RedirectToPage("/Account/Login", new { ReturnUrl = Url.Page("/Events/Confirmation", new { slug, participantId }) });
            var row = await (from participant in db.EventParticipants.AsNoTracking()
                             join item in db.Events.AsNoTracking() on participant.EventId equals item.Id
                             where participant.Id == participantId && participant.AccountId == accountId && item.HiddenAt == null && item.Slug == slug
                             select new { Participant = participant, Event = item }).SingleOrDefaultAsync(ct);
            if (row is null) return Forbid();
            var rosterExists = await db.DraftPublicationCycles.AsNoTracking().AnyAsync(x => x.SupersededAt == null && db.DraftSessions.Any(d => d.Id == x.DraftSessionId && d.EventId == row.Event.Id), ct);
            if (!User.IsInRole("Admin"))
            {
                var destination = EventDestinationPolicy.Decide(EventDestinationPolicy.From(row.Event, rosterExists), false);
                if (destination == EventDestination.Roster) return RedirectToPage("Teams", new { slug, participantId });
                if (destination == EventDestination.Board)
                {
                    var teamSlug = await (from membership in db.TeamMemberships.AsNoTracking()
                                          join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                                          where membership.EventParticipantId == row.Participant.Id && membership.LeftAt == null
                                          select team.Slug).SingleOrDefaultAsync(ct);
                    return teamSlug is null
                        ? RedirectToPage("Board", new { slug })
                        : RedirectToPage("TeamBoard", new { slug, teamSlug, participantId });
                }
            }
            EventName = row.Event.Name;
            EventSlug = row.Event.Slug;
            CanViewTable = EventDestinationPolicy.MayUseSignupTable(EventDestinationPolicy.From(row.Event, rosterExists), User.IsInRole("Admin"));
            Status = DisplayStatus(row.Participant.SignupStatus.ToString());
            CanEdit = row.Participant.SignupStatus is SignupStatus.Confirmed or SignupStatus.WaitingList && row.Event.State == EventState.SignupOpen && row.Event.AcceptsSignups(timeProvider.GetUtcNow());
            CanWithdraw = !row.Event.DraftLocked && row.Event.State is EventState.SignupOpen or EventState.SignupClosed && row.Participant.SignupStatus is SignupStatus.Confirmed or SignupStatus.WaitingList;
            CanRejoin = !row.Event.DraftLocked && row.Event.AcceptsSignups(timeProvider.GetUtcNow()) && row.Participant.SignupStatus == SignupStatus.Withdrawn;
            IsReadOnly = !CanEdit && !CanWithdraw && !CanRejoin && (row.Participant.SignupStatus is SignupStatus.Confirmed or SignupStatus.WaitingList);
            if (row.Participant.SignupStatus == SignupStatus.WaitingList)
                WaitingPosition = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == row.Participant.EventId && x.SignupStatus == SignupStatus.WaitingList && (x.SignedUpAt < row.Participant.SignedUpAt || x.SignedUpAt == row.Participant.SignedUpAt && x.SignupSequence <= row.Participant.SignupSequence)).CountAsync(ct);
            NextStep = row.Participant.SignupStatus switch
            {
                SignupStatus.Withdrawn when CanRejoin => "You can rejoin using the button below. Your previous place is not reserved.",
                SignupStatus.Withdrawn when row.Event.DraftLocked => "The draft has started. Self-service rejoining is unavailable.",
                SignupStatus.Withdrawn => "Signup is closed. Contact an Admin to ask about rejoining.",
                _ => "Your signup will appear in the event roster when it is published."
            };

            var questions = await db.SignupQuestions.AsNoTracking().Where(x => x.EventId == row.Participant.EventId && x.DisabledReason != SignupQuestion.DeletedReason).ToDictionaryAsync(x => x.Id, ct);
            var assignments = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                                     join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                     where assignment.EventParticipantId == row.Participant.Id && assignment.ReleasedAt == null && assignment.SignupQuestionId != null
                                     orderby assignment.RegistrationOrder
                                     select new { Assignment = assignment, character.DisplayName }).ToListAsync(ct);
            Accounts = assignments.Where(x => questions.ContainsKey(x.Assignment.SignupQuestionId!.Value)).Select(x =>
            {
                var question = questions[x.Assignment.SignupQuestionId!.Value];
                return new AccountView(question.Label, x.DisplayName, question.AccountAnswerRole == EventCharacterRole.Playing ? x.Assignment.EhbSnapshot : null);
            }).ToList();
            var answers = await db.SignupAnswers.AsNoTracking().Where(x => x.EventParticipantId == row.Participant.Id && x.OsrsCharacterId == null).ToListAsync(ct);
            Answers = answers.Where(x => questions.TryGetValue(x.SignupQuestionId, out var question) && question.SystemField is SignupSystemField.None or SignupSystemField.CoCaptainName)
                .Select(x => new AnswerView(questions[x.SignupQuestionId].SystemField == SignupSystemField.CoCaptainName ? text[SignupQuestion.CoCaptainLabel].Value : questions[x.SignupQuestionId].Label, x.Value)).ToList();
            Answers = Answers.Append(new AnswerView(text["Captain volunteer"].Value, text[row.Participant.CaptainVolunteer ? "Yes" : "No"].Value)).ToList();
            return Page();
        }

        // Temporary private-link compatibility remains until Pass 4.7; authenticated signups never issue a token.
        if (TempData["SignupEventName"] is not string name) return RedirectToPage("/Index");
        EventName = name;
        EventSlug = slug;
        Status = DisplayStatus(TempData["SignupStatus"]?.ToString() ?? "Received");
        WaitingPosition = TempData["WaitingPosition"] as int?;
        return Page();
    }

    public async Task<IActionResult> OnPostWithdrawAsync(string slug, CancellationToken ct)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Slug == slug && item.HiddenAt == null, ct)) return NotFound();
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var participant = await OwnedParticipantAsync(slug, accountId.Value, ct);
        if (participant is null) return Forbid();
        if (!ConfirmLifecycleAction) { SetStatus(text["Confirm that you want to withdraw before continuing."].Value, false); return RedirectToPage(new { slug, participantId = participant.ParticipantId }); }
        var result = await signupService.WithdrawAsync(participant.EventId, participant.ParticipantId, accountId, User.Identity?.Name ?? "participant", false, cancellationToken: ct);
        SetStatus(result.Succeeded ? text["Your signup has been withdrawn."].Value : text[result.Error ?? "Your signup could not be withdrawn."].Value, result.Succeeded);
        return RedirectToPage(new { slug, participantId = participant.ParticipantId });
    }

    public async Task<IActionResult> OnPostRejoinAsync(string slug, CancellationToken ct)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Slug == slug && item.HiddenAt == null, ct)) return NotFound();
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var participant = await OwnedParticipantAsync(slug, accountId.Value, ct);
        if (participant is null) return Forbid();
        if (!ConfirmLifecycleAction) { SetStatus(text["Confirm that you want to rejoin before continuing."].Value, false); return RedirectToPage(new { slug, participantId = participant.ParticipantId }); }
        var result = await signupService.RejoinAsync(participant.EventId, participant.ParticipantId, accountId.Value, User.Identity?.Name ?? "participant", ct);
        SetStatus(result.Succeeded ? text["Your signup has been restored."].Value : text[result.Error ?? "Your signup could not be restored."].Value, result.Succeeded);
        return RedirectToPage(new { slug, participantId = participant.ParticipantId });
    }

    private async Task<OwnedParticipant?> OwnedParticipantAsync(string slug, Guid accountId, CancellationToken ct) =>
        await (from participant in db.EventParticipants
               join bingoEvent in db.Events on participant.EventId equals bingoEvent.Id
               where bingoEvent.HiddenAt == null && bingoEvent.Slug == slug && participant.AccountId == accountId
               select new OwnedParticipant(participant.EventId, participant.Id)).SingleOrDefaultAsync(ct);
    private static string DisplayStatus(string status) => status == nameof(SignupStatus.WaitingList) ? "Waiting list" : status;
    private void SetStatus(string message, bool success) { TempData["StatusMessage"] = message; TempData[Bingo.Web.UI.UiMessage.TypeKey] = (success ? Bingo.Web.UI.UiMessageType.Success : Bingo.Web.UI.UiMessageType.Error).ToString(); }

    private sealed record OwnedParticipant(Guid EventId, Guid ParticipantId);

    public sealed record AccountView(string Label, string CharacterName, decimal? Ehb);
    public sealed record AnswerView(string Label, string Value);
}
