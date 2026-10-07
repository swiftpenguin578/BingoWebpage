using System.Globalization;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

/// <summary>
/// U5-Q3 / brief 87 0b: the no-store current state of one participant, used to open
/// the drawer and to re-read after a lost save response. Read-only; never mutates.
/// </summary>
public sealed class ParticipantDrawerReader(ApplicationDbContext db, Func<string, object[], string> localize)
{
    public async Task<ParticipantDrawerView?> ReadAsync(BingoEvent bingoEvent, Guid participantId, CancellationToken ct)
    {
        var participant = await db.EventParticipants.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == bingoEvent.Id && x.Id == participantId, ct);
        if (participant is null) return null;
        var questions = await db.SignupQuestions.AsNoTracking()
            .Where(x => x.EventId == bingoEvent.Id && x.Active).OrderBy(x => x.Position).ToListAsync(ct);
        var primaryQuestion = questions.FirstOrDefault(x => x.Type == SignupQuestionType.Account && x.AccountAnswerRole == EventCharacterRole.Playing && x.SystemField == SignupSystemField.PrimaryRegularAccount);
        var withdrawn = participant.SignupStatus == SignupStatus.Withdrawn;

        // A withdrawn participant's accounts are released but kept: show the set released
        // by the withdrawal (what Restore reacquires), read-only.
        var assignmentRows = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                                    join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                    where assignment.EventParticipantId == participantId &&
                                          (withdrawn ? assignment.ReleasedAt != null && participant.WithdrawnAt != null && assignment.ReleasedAt >= participant.WithdrawnAt : assignment.ReleasedAt == null)
                                    orderby assignment.RegistrationOrder
                                    select new { assignment.Id, character.DisplayName, assignment.EhbSnapshot, assignment.EventRole, assignment.SignupQuestionId }).ToListAsync(ct);
        var accounts = assignmentRows
            .Select(x => new ParticipantDrawerAccount(x.Id, x.DisplayName, x.EhbSnapshot, x.EventRole == EventCharacterRole.Playing ? "playing" : "alt",
                primaryQuestion is not null && x.SignupQuestionId == primaryQuestion.Id))
            .OrderByDescending(x => x.Primary).ToList();
        if (accounts.Count > 0 && !accounts.Any(x => x.Primary && x.Role == "playing") && accounts.FirstOrDefault(x => x.Role == "playing") is { } fallback)
            accounts[accounts.IndexOf(fallback)] = fallback with { Primary = true };

        // The Discord display name stays private (it was never on the admin detail page); only the link state shows.
        var owner = participant.AccountId is { } ownerId
            ? await db.Accounts.AsNoTracking().Where(x => x.Id == ownerId).Select(x => new { x.Id, Username = x.PublicUsername ?? x.LoginName, Linked = x.DiscordUserId != null, x.CreatedAt }).SingleOrDefaultAsync(ct)
            : null;
        var saved = owner is null ? [] : await (from link in db.AccountOsrsCharacters.AsNoTracking()
                                                join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
                                                where link.AccountId == owner.Id && link.Active
                                                orderby link.Position
                                                select new ParticipantDrawerSavedAccount(character.DisplayName, link.SavedEhb)).ToListAsync(ct);

        var answers = await db.SignupAnswers.AsNoTracking().Where(x => x.EventParticipantId == participantId && x.OsrsCharacterId == null)
            .ToDictionaryAsync(x => x.SignupQuestionId, x => x.Value, ct);
        var answerViews = questions.Where(x => x.Type != SignupQuestionType.Account)
            .Select(x => new ParticipantDrawerAnswer(x.Id, x.Label, x.Type.ToString(),
                x.SystemField switch { SignupSystemField.CaptainVolunteer => "captain", SignupSystemField.CoCaptainName => "cocaptain", _ => null },
                x.Options?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                x.SystemField == SignupSystemField.CaptainVolunteer ? (participant.CaptainVolunteer ? "true" : "false") : answers.GetValueOrDefault(x.Id)))
            .ToList();
        // Answers kept for a retired (deactivated, not deleted) question stay readable, as on the
        // old detail page; they are read-only and never part of a save.
        var answeredIds = answers.Keys.ToList();
        var retired = await db.SignupQuestions.AsNoTracking()
            .Where(x => x.EventId == bingoEvent.Id && !x.Active && x.Type != SignupQuestionType.Account && x.DisabledReason != SignupQuestion.DeletedReason && answeredIds.Contains(x.Id))
            .OrderBy(x => x.Position).ToListAsync(ct);
        answerViews.AddRange(retired.Select(x => new ParticipantDrawerAnswer(x.Id, x.Label, x.Type.ToString(), null, [], answers[x.Id], Retired: true)));

        var team = await (from membership in db.TeamMemberships.AsNoTracking()
                          join item in db.Teams.AsNoTracking() on membership.TeamId equals item.Id
                          where membership.EventParticipantId == participantId && membership.LeftAt == null
                          select item.Name).FirstOrDefaultAsync(ct);
        var counts = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == bingoEvent.Id)
            .GroupBy(x => x.SignupStatus).Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
        int Count(SignupStatus status) => counts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        var waitingPosition = participant.SignupStatus == SignupStatus.WaitingList ? await WaitingPositionAsync(bingoEvent.Id, participantId, ct) : (int?)null;
        var total = counts.Sum(x => x.Count);

        var policy = ParticipantRosterPolicy.For(bingoEvent, localize);
        var editable = policy.Editable && !withdrawn;
        var playingSlots = questions.Count(x => x.Type == SignupQuestionType.Account && x.AccountAnswerRole == EventCharacterRole.Playing);
        var altQuestions = questions.Where(x => x.Type == SignupQuestionType.Account && x.AccountAnswerRole == EventCharacterRole.Informational).Select(x => x.Label).ToList();
        var signedUp = Bingo.Web.UI.DateTimePresentation.Format(participant.SignedUpAt, "d MMM yyyy, HH:mm", bingoEvent.Timezone, CultureInfo.CurrentCulture);
        return new ParticipantDrawerView(
            participant.Id, participant.SignupStatus switch { SignupStatus.Confirmed => "confirmed", SignupStatus.WaitingList => "waiting", _ => "withdrawn" },
            waitingPosition, Count(SignupStatus.WaitingList), participant.ResponseVersion,
            participant.PaymentStatus == PaymentStatus.Paid, participant.AdminNotes ?? string.Empty,
            participant.SignupSequence, total, signedUp,
            participant.Source switch { SignupSource.Website => localize("Website signup", []), SignupSource.CsvImport => localize("CSV import", []), _ => localize("Added by an admin", []) },
            team, owner?.Username, owner?.Linked == true,
            owner is null ? null : owner.CreatedAt.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            accounts, saved, answerViews, playingSlots, altQuestions,
            editable, policy.PrivateEditable, policy.Reason, bingoEvent.Version,
            bingoEvent.ParticipantCap ?? 0, Count(SignupStatus.Confirmed));
    }

    private async Task<int> WaitingPositionAsync(Guid eventId, Guid participantId, CancellationToken ct)
    {
        var ids = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == eventId && x.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(x => x.WaitingListedAt ?? x.SignedUpAt).ThenBy(x => x.SignupSequence).ThenBy(x => x.Id)
            .Select(x => x.Id).ToListAsync(ct);
        return ids.IndexOf(participantId) + 1;
    }
}

/// <summary>Participants lifecycle: roster edits only before the draft; payment/note always (D16).</summary>
public sealed record ParticipantRosterPolicy(bool Editable, bool PrivateEditable, string Reason)
{
    public static ParticipantRosterPolicy For(BingoEvent item, Func<string, object[], string> localize)
    {
        var editable = !item.DraftLocked && item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
        var reason = editable ? string.Empty : item.State switch
        {
            EventState.Draft or EventState.SignupOpen or EventState.SignupClosed => localize("The team draft for {0} has started.", [item.Name]),
            EventState.Live => localize("{0} is in progress.", [item.Name]),
            EventState.AwaitingFinalReview => localize("{0} is awaiting final review.", [item.Name]),
            EventState.Finalized => localize("{0} is finalized.", [item.Name]),
            EventState.Archived => localize("{0} is archived.", [item.Name]),
            EventState.Cancelled => localize("{0} was cancelled.", [item.Name]),
            _ => localize("Roster changes aren’t allowed in this event phase.", [])
        };
        return new(editable, item.State != EventState.Discarded, reason);
    }
}

public sealed record ParticipantDrawerAccount(Guid AssignmentId, string Name, decimal? Ehb, string Role, bool Primary);
public sealed record ParticipantDrawerSavedAccount(string Name, decimal? Ehb);
public sealed record ParticipantDrawerAnswer(Guid QuestionId, string Label, string Type, string? System, IReadOnlyList<string> Options, string? Value, bool Retired = false);
public sealed record ParticipantDrawerView(
    Guid Id, string Status, int? WaitingPosition, int WaitingCount, int ResponseVersion,
    bool Paid, string AdminNote, long SignupSequence, int TotalCount, string SignedUp, string Source,
    string? Team, string? Username, bool DiscordLinked, string? MemberSince,
    IReadOnlyList<ParticipantDrawerAccount> Accounts, IReadOnlyList<ParticipantDrawerSavedAccount> SavedAccounts,
    IReadOnlyList<ParticipantDrawerAnswer> Answers, int PlayingSlots, IReadOnlyList<string> AltSlots,
    bool Editable, bool PrivateEditable, string LockReason, long EventVersion, int Capacity, int Confirmed);
