using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Auditing;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.UI;

/// <summary>
/// Brief 147 item 1 (approved inventory, artifacts/audit-overhaul/inventory.md): one plain sentence
/// per recorded action, "who did what to whom", with names resolved at display time. Unknown or
/// deleted records fall back to the stored name or label, else the stored id; unreadable payloads
/// never fail the page. Masked actions (A5) get sentences without their masked values.
/// </summary>
internal sealed class AuditSentences
{
    private readonly AuditEntry entry;
    private readonly AuditNames names;
    private readonly IStringLocalizer<AuditResource> text;
    private readonly JsonElement? before;
    private readonly JsonElement? after;
    private readonly JsonElement? details;
    private readonly Guid? target;

    private AuditSentences(AuditEntry entry, AuditNames names, IStringLocalizer<AuditResource> text)
    {
        this.entry = entry;
        this.names = names;
        this.text = text;
        before = AuditNameResolver.Parse(entry.BeforeState);
        after = AuditNameResolver.Parse(entry.AfterState);
        details = AuditNameResolver.Parse(entry.Details);
        // WriteAudit/AuditMutation producers store {before, after} inside Details.
        if (before is null && after is null && details is { ValueKind: JsonValueKind.Object } nested)
        {
            before = Child(nested, "before");
            after = Child(nested, "after");
        }
        target = Guid.TryParse(entry.TargetId, out var id) ? id : null;
    }

    public static string? Build(AuditEntry entry, AuditNames names, IStringLocalizer<AuditResource> text)
    {
        try { return new AuditSentences(entry, names, text).Sentence(); }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or KeyNotFoundException or ArgumentException)
        {
            // Historic payloads are evidence, not a page failure: no sentence, the record line remains.
            return null;
        }
    }

    /// <summary>
    /// Brief 147 item 2: the website account and/or playing (OSRS) account an entry concerns,
    /// derived at display time from stored data. A participant's owner and character are looked up
    /// now, so they are marked as current (A4); names stored in the entry are used as they are.
    /// </summary>
    public static IReadOnlyList<AuditAffectedAccount> Affected(AuditEntry entry, AuditNames names)
    {
        try { return new AuditSentences(entry, names, NoText.Instance).AffectedAccounts(); }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or KeyNotFoundException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>B2: a capped team removal stores the full count; older entries list every id.</summary>
    public static int? AffectedTotal(AuditEntry entry)
    {
        if (entry.Action != "draft.team_removed") return null;
        try { return new AuditSentences(entry, AuditNames.Empty, NoText.Instance).EndedMemberCount(); }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or ArgumentException) { return null; }
    }

    private List<AuditAffectedAccount> AffectedAccounts()
    {
        var result = new List<AuditAffectedAccount>();
        void AddAccount(Guid? id, string? stored = null)
        {
            if (id is null && stored is null) return;
            result.Add(new(id is { } key && names.Accounts.TryGetValue(key, out var name) ? name : stored ?? id!.Value.ToString(), null, false));
        }
        void AddParticipant(Guid? participantId, string? storedCharacter = null)
        {
            if (participantId is not { } key) return;
            var known = names.Participants.GetValueOrDefault(key);
            var website = known?.AccountId is { } owner ? names.Accounts.GetValueOrDefault(owner) ?? owner.ToString() : null;
            // Neither a name nor an owner: the stored participant id (never an empty line).
            var playing = storedCharacter ?? known?.Character ?? (website is null && key != Guid.Empty ? key.ToString() : null);
            // A11: an affected participant belongs to an event, so the column names their event primary
            // account; if that cannot be resolved, the stored or derived character, then the website account.
            result.Add(new(website, playing, website is not null, names.EventPrimaries.GetValueOrDefault(key) ?? storedCharacter ?? known?.Character));
        }
        void AddMembership(Guid? membershipId)
        {
            if (membershipId is { } key && names.Memberships.TryGetValue(key, out var membership)) AddParticipant(membership.ParticipantId);
        }

        switch (entry.Action)
        {
            case var key when key.StartsWith("account.", StringComparison.Ordinal) || key == "logout":
                AddAccount(target, target is null ? entry.TargetId : null);
                break;
            case "participant.primary_switched":
                AddParticipant(target, Id(after, "primaryCharacterId") is { } primary && names.Characters.TryGetValue(primary, out var primaryName) ? primaryName : null);
                break;
            case "participant.event_account_added" or "participant.event_account_corrected":
                AddParticipant(target, Id(after, "characterId") is { } added && names.Characters.TryGetValue(added, out var addedName) ? addedName : null);
                break;
            case "participant.event_account_removed":
                AddParticipant(target, Id(before, "characterId") is { } removed && names.Characters.TryGetValue(removed, out var removedName) ? removedName : null);
                break;
            case "participant.prelive_replaced" or "participant.live_replaced":
                AddParticipant(Id(before, "departedParticipantId"));
                AddParticipant(Id(after, "replacementParticipantId"), Str(after, "replacementName"));
                break;
            case "participant.ownership_transferred":
                AddAccount(Id(before, "accountId"));
                AddAccount(Id(after, "accountId"), Str(after, "username"));
                break;
            case var key when key.StartsWith("participant.", StringComparison.Ordinal) && entry.TargetType == "participant":
                AddParticipant(target);
                break;
            case "roster.finalized_added" or "roster.finalized_removed":
                var ownerId = Id(details, "ownerAccountId");
                var character = Str(after, "participant", "name") ?? Str(before, "participant", "name") ?? FirstPlaying(details)
                    ?? (Id(after, "participant", "id") is { } rosterParticipant ? names.Participants.GetValueOrDefault(rosterParticipant)?.Character : null);
                var rosterWebsite = ownerId is { } owner ? names.Accounts.GetValueOrDefault(owner) ?? owner.ToString() : null;
                var rosterPrimary = (Id(after, "participant", "id") ?? Id(before, "participant", "id")) is { } rosterId ? names.EventPrimaries.GetValueOrDefault(rosterId) : null;
                result.Add(new(rosterWebsite, character, false, rosterPrimary ?? character));
                break;
            case "team.member_added" when entry.TargetType == "team":
                AddParticipant(Plain(entry.Details) is { Length: > 36 } addedText && Guid.TryParse(addedText[..36], out var addedParticipant) ? addedParticipant : null);
                break;
            case "team.member_added" or "team.member_removed" or "team.member_moved" or "team.membership_role_changed" or "team.role_roster_published"
                or "roster.finalized_added.wom_sync" or "roster.finalized_removed.wom_sync":
                AddMembership(target);
                break;
            case "draft.pick_recorded" or "draft.pick_undone":
                AddParticipant(Id(after, "pick", "EventParticipantId"));
                break;
            case "draft.team_removed":
                foreach (var ended in Ids(after, "EndedMembershipIds")) AddMembership(ended);
                break;
            case "team.inclusion_changed":
                foreach (var participant in Ids(after, "AffectedParticipantIds")) AddParticipant(participant);
                break;
            case "event.signup_administration_updated":
                foreach (var participant in Ids(details, "promotedParticipantIds")) AddParticipant(participant);
                break;
            case var key when key.StartsWith("submission.", StringComparison.Ordinal):
                var credited = Id(after, "CreditedParticipantId") ?? Id(before, "CreditedParticipantId")
                    ?? (target is { } submission && names.Submissions.TryGetValue(submission, out var known) ? known.ParticipantId : null);
                if (credited is not null || Str(after, "CreditedCharacterName") is not null)
                    AddParticipant(credited ?? Guid.Empty, Str(after, "CreditedCharacterName") ?? Str(before, "CreditedCharacterName")
                        ?? (target is { } stored && names.Submissions.TryGetValue(stored, out var storedSubmission) ? storedSubmission.CharacterName : null));
                break;
        }
        return result.Where(account => account.Website is not null || account.Playing is not null)
            .DistinctBy(account => (account.Website, account.Playing, account.Column)).ToList();
    }

    private static string? FirstPlaying(JsonElement? element)
    {
        foreach (var name in new[] { "participantBefore", "participantAfter" })
            if (Path(element, [name, "activePlayingAssignments"]) is { ValueKind: JsonValueKind.Array } assignments)
                foreach (var assignment in assignments.EnumerateArray())
                    if (Str(assignment, "DisplayName") is { } display) return display;
        return null;
    }

    private static IEnumerable<Guid> Ids(JsonElement? element, params string[] path)
    {
        if (Path(element, path) is not { ValueKind: JsonValueKind.Array } values) yield break;
        foreach (var value in values.EnumerateArray())
            if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)) yield return id;
    }

    private sealed class NoText : IStringLocalizer<AuditResource>
    {
        public static readonly NoText Instance = new();
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private string Actor => entry.ActorUsername;

    private string L(string format, params object?[] args) => text[format, args.Select(arg => arg ?? "—").ToArray()].Value;

    private string? Sentence() => entry.Action switch
    {
        // Accounts
        "account.admin_granted" => L("{0} gave {1} Admin access.", Actor, TargetAccount()),
        "account.admin_revoked" => L("{0} removed Admin access from {1}.", Actor, TargetAccount()),
        "account.ownership_transferred" => Str(after, "roleOrState") == "SuperAdmin"
            ? L("{0} made {1} the site owner.", Actor, TargetAccount())
            : L("{0} handed over site ownership; {1} is now an Admin.", Actor, TargetAccount()),
        "account.disabled" => L("{0} disabled the account {1}.", Actor, TargetAccount()),
        "account.restored" => L("{0} restored the account {1}.", Actor, TargetAccount()),
        "account.username_changed" => L("{0} changed their username from {1} to {2}.", Actor, Str(before, "username"), Str(after, "username")),
        "account.onboarded" => L("{0} finished setting up their account.", Actor),
        "account.reset_link_created" => L("{0} created a password-reset link for {1}.", Actor, TargetAccount()),
        "account.password_reset" => L("{0} reset their password with a reset link.", TargetAccount()),
        "account.owner_recovery_password_reset" => L("The owner password for {0} was reset through operator recovery.", TargetAccount()),
        "account.password_changed" => L("{0} changed their password.", Actor),
        "account.discord_linked" => L("{0} linked a Discord account.", Actor),
        "account.discord_replaced" => L("{0} replaced their linked Discord account.", Actor),
        "account.discord_unlinked" => L("{0} unlinked their Discord account.", Actor),
        "account.owner_bootstrapped" => L("{0} was set up as the site owner by the operator.", TargetAccount()),
        "account.owner_recovered" => L("Site ownership was recovered to {0} by the operator.", TargetAccount()),
        "account.owner_recovery_reset_link_created" => L("An owner password-reset link was created for {0} by the operator.", TargetAccount()),
        "account.retained_owner_promoted" => L("{0} was promoted to site owner by operator recovery.", TargetAccount()),
        "account.bootstrap_created" => L("{0} was created as the first administrator (development setup).", TargetAccount()),
        "account.captain_auto_created" => L("{0} created the captain account {1}.", Actor, TargetAccount()),
        "account.captain_auto_disabled" => L("{0} disabled the captain account {1}.", Actor, TargetAccount()),
        "account.role_changed" => L("{0} changed the role of {1}.", Actor, TargetAccount()),
        "logout" => L("{0} signed out.", Actor),

        // Events
        "event.created" => L("{0} created the event {1}.", Actor, EventName()),
        "event.updated" => L("{0} updated the event {1}.", Actor, EventName()),
        "event.identity_updated" => L("{0} changed the details of {1}.", Actor, EventName()),
        "event.schedule_updated" => L("{0} changed the schedule of {1}.", Actor, EventName()),
        "event.started" => L("{0} started {1}.", Actor, EventName()),
        "event.ended" => L("{0} ended {1}.", Actor, EventName()),
        "event.resumed" => L("{0} resumed {1}.", Actor, EventName()),
        "event.started_automatically" => L("{0} started automatically at its scheduled time.", EventName()),
        "event.ended_automatically" => L("{0} ended automatically at its scheduled time.", EventName()),
        "event.start_postponed" => L("The automatic start of {0} was postponed; {1} start blocker(s) were open.", EventName(), Count(details, "blockerCodes")),
        "event.hidden" => L("{0} hid {1}.", Actor, EventName()),
        "event.restored" => L("{0} made {1} visible again.", Actor, EventName()),
        "event.cancelled" => L("{0} cancelled {1}.", Actor, EventName()),
        "event.discarded" => L("{0} discarded the empty event setup {1}.", Actor, EventName()),
        "event.results_published" => L("{0} published the official results of {1}.", Actor, EventName()),
        "event.legacy_finalized_archived" => L("{0} was archived from the old finalized state.", EventName()),
        "event.unfinalized" => L("{0} reopened {1} for final review.", Actor, EventName()),
        "event.finalized" => L("{0} finalized {1}.", Actor, EventName()),
        "event.final_review_overridden" => L("{0} overrode a final-review blocker for {1}.", Actor, EventName()),
        "event.completion_time_corrected" => L("{0} corrected the completion time of {1}.", Actor, Team(Id(details, "teamId"))),
        "event.submissions_reopened" => ReopenedUntil() is { } until
            ? L("{0} reopened evidence uploads for {1} until {2}.", Actor, EventName(), until)
            : L("{0} reopened evidence uploads for {1}.", Actor, EventName()),
        "event.evidence_code_mode" => L("{0} changed the evidence code setting of {1}.", Actor, EventName()),
        "evidence_code.created" => L("{0} added an evidence code for {1}.", Actor, EventName()),
        "event.competition_linked" => Competition() is { } linked ? L("{0} linked WOM competition {1} to {2}.", Actor, linked, EventName()) : L("{0} linked a WOM competition to {1}.", Actor, EventName()),
        "event.competition_changed" => Competition() is { } changed ? L("{0} switched {1} to WOM competition {2}.", Actor, EventName(), changed) : L("{0} switched {1} to another WOM competition.", Actor, EventName()),
        "event.competition_cleared" => L("{0} removed the WOM competition from {1}.", Actor, EventName()),
        "event.competition_created" => L("{0} created WOM competition {1} for {2}.", Actor, Competition() ?? "—", EventName()),
        "event.competition_deleted" => L("{0} deleted WOM competition {1} of {2}.", Actor, Competition() ?? "—", EventName()),
        "event.competition_credential_adopted" => L("{0} added the WOM management access for {1}.", Actor, EventName()),
        "event.competition_credential_replaced" => L("{0} replaced the WOM management access for {1}.", Actor, EventName()),
        "historical_import.applied" => L("{0} imported the historical event {1}.", Actor, EventName()),

        // Signups
        "event.signup_opened" => L("{0} opened signups for {1}.", Actor, EventName()),
        "event.signup_reopened" => L("{0} reopened signups for {1}.", Actor, EventName()),
        "event.signup_closed" => L("{0} closed signups for {1}.", Actor, EventName()),
        "event.signup_opened_automatically" => L("Signups for {0} opened automatically.", EventName()),
        "event.signup_closed_automatically" => L("Signups for {0} closed automatically.", EventName()),
        "event.signup_opening_failed" => L("Signups for {0} could not open automatically; {1} blocker(s) were open.", EventName(), Count(details, "blockerCodes")),
        "event.signup_code_changed" => L("{0} changed the signup code settings of {1}.", Actor, EventName()),
        "event.signup_administration_updated" => Int(details, "promoted") is > 0 and var moved
            ? L("{0} changed the participant cap of {1} from {2} to {3}; {4} moved up from the waiting list.", Actor, EventName(), Str(before, "participantCap"), Str(after, "participantCap"), moved)
            : L("{0} changed the participant cap of {1} from {2} to {3}.", Actor, EventName(), Str(before, "participantCap"), Str(after, "participantCap")),
        "event.capacity_increased" => CapacityPromoted() is > 0 and var raised
            ? L("The participant cap of {0} was raised from {1} to {2}; {3} moved up from the waiting list.", EventName(), Scalar(entry.BeforeState), Scalar(entry.AfterState), raised)
            : L("The participant cap of {0} was raised from {1} to {2}.", EventName(), Scalar(entry.BeforeState), Scalar(entry.AfterState)),
        "signup_question.created" => L("{0} added the signup question “{1}”.", Actor, Question()),
        "signup_question.account_added" => L("{0} added the account question “{1}”.", Actor, Question()),
        "signup_question.edited" => L("{0} edited the signup question “{1}”.", Actor, Question()),
        "signup_question.account_edited" => L("{0} edited the account question “{1}”.", Actor, Question()),
        "signup_question.reordered" => L("{0} moved the signup question “{1}” from position {2} to {3}.", Actor, Question(), Str(before, "from"), Str(after, "to")),
        "signup_question.deleted" => L("{0} deleted the signup question “{1}”.", Actor, Question()),
        "signup_cocaptain.disabled" => L("{0} turned off co-captain signups for {1}.", Actor, EventName()),
        "signup_cocaptain.enabled" => L("{0} turned on co-captain signups for {1}.", Actor, EventName()),

        // Participants
        "participant.payment_updated" => Str(after, "payment") == "Paid" ? L("{0} marked {1} as paid.", Actor, TargetPlayer()) : L("{0} marked {1} as unpaid.", Actor, TargetPlayer()),
        "participant.admin_note_updated" => L("{0} changed the private admin note on {1}.", Actor, TargetPlayer()),
        "participant.corrected" => L("{0} corrected the signup of {1}.", Actor, TargetPlayer()),
        "participant.admin_created" => L("{0} added {1} to {2}.", Actor, TargetPlayer(), EventName()),
        "participant.admin_saved_created" => L("{0} added {1} to {2} from saved accounts.", Actor, TargetPlayer(), EventName()),
        "participant.primary_switched" => L("{0} made {1} the main account instead of {2}.", Actor, Character(Id(after, "primaryCharacterId")), Character(Id(after, "previousPrimaryCharacterId") ?? Id(before, "primaryCharacterId"))),
        "participant.event_account_added" => L("{0} added the account {1} to {2}.", Actor, Character(Id(after, "characterId")), TargetPlayer()),
        "participant.event_account_removed" => L("{0} removed the account {1} from {2}.", Actor, Character(Id(before, "characterId")), TargetPlayer()),
        "participant.event_account_corrected" => Id(before, "characterId") is { } oldCharacter && Id(after, "characterId") is { } newCharacter && oldCharacter != newCharacter
            ? L("{0} replaced the account {1} with {2} for {3}.", Actor, Character(oldCharacter), Character(newCharacter), TargetPlayer())
            : L("{0} corrected the account {1} of {2}.", Actor, Character(Id(after, "characterId")), TargetPlayer()),
        "participant.admin_confirmed" => L("{0} confirmed {1} from the waiting list.", Actor, TargetPlayer()),
        "participant.admin_moved_to_waiting" => Id(details, "promotedParticipantId") is { } next
            ? L("{0} moved {1} to the waiting list; {2} moved up.", Actor, TargetPlayer(), Player(next))
            : L("{0} moved {1} to the waiting list.", Actor, TargetPlayer()),
        "participant.promoted" => L("{0} moved up from the waiting list.", TargetPlayer()),
        "participant.rejoined" => L("{0} signed up again.", Actor),
        "participant.admin_restored" => L("{0} restored the signup of {1}.", Actor, TargetPlayer()),
        "participant.withdrawn" => L("{0} withdrew their signup.", Actor),
        "participant.admin_withdrawn" => L("{0} withdrew {1}.", Actor, TargetPlayer()),
        "participant.live_withdrawn" => L("{0} withdrew {1} from {2} during the event.", Actor, TargetPlayer(), MembershipTeam(Id(before, "membershipId"))),
        "participant.prelive_replaced" => L("{0} replaced {1} with {2} on {3} before the event.", Actor, Player(Id(before, "departedParticipantId")), Player(Id(after, "replacementParticipantId"), Str(after, "replacementName")), MembershipTeam(target)),
        "participant.live_replaced" => L("{0} replaced {1} with {2} on {3} during the event.", Actor, Player(Id(before, "departedParticipantId")), Player(Id(after, "replacementParticipantId"), Str(after, "replacementName")), MembershipTeam(target)),
        "participant.ownership_transferred" => L("{0} moved {1} to the account {2}.", Actor, TargetPlayer(), Account(Id(after, "accountId"), Str(after, "username"))),
        "participant.promotion_follow_up_completed" => L("{0} completed the follow-up for a waiting-list promotion.", Actor),
        "roster.finalized_added" => L("{0} added {1} to {2} on the published roster.", Actor, RosterPlayer(), RosterTeam()),
        "roster.finalized_removed" => L("{0} removed {1} from {2} on the published roster.", Actor, RosterPlayer(), RosterTeam()),
        "roster.finalized_added.wom_sync" => L("WOM sync after adding {0} to {1}: {2}.", MembershipPlayer(target), MembershipTeam(target), Value(Str(details, "status"))),
        "roster.finalized_removed.wom_sync" => L("WOM sync after removing {0} from {1}: {2}.", MembershipPlayer(target), MembershipTeam(target), Value(Str(details, "status"))),
        "team.member_added" => L("{0} added {1} to {2}.", Actor, AddedPlayer(), entry.TargetType == "team" ? Team(target) : MembershipTeam(target)),
        "team.member_removed" => L("{0} removed {1} from {2}.", Actor, MembershipPlayer(target), MembershipTeam(target)),
        "team.member_moved" => MovedTeams() is var (source, destination)
            ? L("{0} moved {1} from {2} to {3}.", Actor, MembershipPlayer(target), source, destination)
            : L("{0} moved {1} to {2}.", Actor, MembershipPlayer(target), MembershipTeam(target)),
        "team.membership_role_changed" => L("{0} changed the role of {1} in {2} from {3} to {4}.", Actor, MembershipPlayer(target), Team(Id(after, "teamId")) is var roleTeam && roleTeam != "—" ? roleTeam : MembershipTeam(target), Value(Str(before, "role")), Value(Str(after, "role"))),

        // Teams
        "team.created" => L("{0} created the team {1}.", Actor, Team(target)),
        "team.updated" => L("{0} updated the team {1}.", Actor, Team(target, Str(after, "Name"))),
        "team.inclusion_changed" => Str(after, "Team", "IncludedInDraft") == "true"
            ? L("{0} included {1} in the draft.", Actor, Team(target, Str(after, "Team", "Name")))
            : L("{0} left {1} out of the draft.", Actor, Team(target, Str(after, "Team", "Name"))),
        "team.preformed_roster_csv_imported" => L("{0} imported {1} members into {2} from a CSV file.", Actor, Match(entry.Details, @"Imported (\d+)") ?? "—", Team(target)),
        "team.role_roster_published" => L("{0} republished the team roles after changing the role of {1}.", Actor, MembershipPlayer(target)),
        "team.preformed_corrected" => L("{0} corrected the pre-formed team {1} and republished the roster.", Actor, Team(target)),

        // Draft
        "draft.team_removed" => EndedMemberCount() is > 0 and var ended
            ? L("{0} removed the team {1}; {2} members left the team.", Actor, Team(target, Str(after, "Name") ?? Str(before, "Name")), ended)
            : L("{0} removed the team {1}.", Actor, Team(target, Str(after, "Name") ?? Str(before, "Name"))),
        "draft.order_scrambled" => L("{0} shuffled the draft order.", Actor),
        "draft.started" => Int(after, "IncludedParticipants") is { } included
            ? L("{0} started the draft with {1} participants.", Actor, included)
            : L("{0} started the draft.", Actor),
        "draft.pick_recorded" => L("{0} recorded pick {1}: {2} picked {3}.", Actor, Str(after, "pick", "PickNumber"), Team(Id(after, "pick", "TeamId")), Player(Id(after, "pick", "EventParticipantId"))),
        "draft.pick_undone" => L("{0} undid pick {1}: {2} is no longer on {3}.", Actor, Str(after, "pick", "PickNumber"), Player(Id(after, "pick", "EventParticipantId")), Team(Id(after, "pick", "TeamId"))),
        "draft.cancelled" => L("{0} cancelled the draft and returned to setup.", Actor),
        "draft.finalized" => Match(entry.Details, @"(\d+) frozen roster entries") is { } frozen
            ? L("{0} finalized the draft and published {1} roster entries.", Actor, frozen)
            : L("{0} finalized the draft and published the rosters.", Actor),
        "draft.reopened" => L("{0} reopened the draft.", Actor),
        "draft.control_acquired" => L("{0} took control of the draft.", Actor),
        "draft.control_taken_over" => L("{0} took over control of the draft from {1}.", Actor, Account(Id(before, "ControllerAccountId"))),
        "draft.control_released" => L("{0} released control of the draft.", Actor),

        // Board
        "board.created" => L("{0} created the board “{1}”.", Actor, BoardName()),
        "board.updated" => L("{0} updated the board “{1}”.", Actor, BoardName()),
        "board.editing_acquired" => L("{0} started editing the board.", Actor),
        "board.editing_taken_over" => L("{0} took over editing the board from {1}.", Actor, Account(Id(before, "EditorAccountId"))),
        "board.editing_released" => L("{0} stopped editing the board.", Actor),
        "board.tile_created" => L("{0} added the tile “{1}” at row {2}, column {3}.", Actor, TileName(after, "tile"), Position(after, "tile", "RowIndex"), Position(after, "tile", "ColumnIndex")),
        "board.tile_edited" => L("{0} edited the tile “{1}”.", Actor, TileName(after, "tile")),
        "board.tile_removed" => L("{0} removed the tile “{1}” from row {2}, column {3}.", Actor, TileName(before, "tile"), Position(before, "tile", "RowIndex"), Position(before, "tile", "ColumnIndex")),
        "board.tile_moved" => L("{0} moved the tile “{1}” from row {2}, column {3} to row {4}, column {5}.", Actor, Tile(Id(after, "source", "Id")),
            Position(before, "source", "RowIndex"), Position(before, "source", "ColumnIndex"), Position(after, "source", "RowIndex"), Position(after, "source", "ColumnIndex")),
        "board.tiles_swapped" => L("{0} swapped the tiles “{1}” and “{2}”.", Actor, Tile(Id(after, "source", "Id")), Tile(Id(after, "target", "Id"))),
        "board.resized" => L("{0} resized the board from {1}×{2} to {3}×{4}.", Actor, Str(before, "Rows"), Str(before, "Columns"), Str(after, "Rows"), Str(after, "Columns")),
        "board.expected_team_size_changed" => L("{0} changed the planning team size from {1} to {2}.", Actor, Str(before, "ExpectedTeamSize"), Str(after, "ExpectedTeamSize")),
        "board.approved" => L("{0} approved the board.", Actor),
        "board.unapproved" => L("{0} withdrew the board approval.", Actor),
        "board.auto_unapproved" => L("The board approval was withdrawn because {0} changed competitive content.", Actor),
        "board.published" => L("{0} published the board.", Actor),
        "board.published_correction_started" => L("{0} started a correction of the published board.", Actor),
        "board.published_correction_discarded" => L("{0} discarded the unpublished board correction.", Actor),
        "board.published_corrected" => L("{0} published the corrected board.", Actor),

        // Evidence
        "submission.created" => L("{0} submitted evidence for “{1}”, credited to {2}.", Actor, SubmissionTile(), SubmissionPlayer()),
        "submission.approved" => L("{0} approved the evidence for “{1}” by {2}.", Actor, SubmissionTile(), SubmissionPlayer()),
        "submission.rejected" => L("{0} rejected the evidence for “{1}” by {2}.", Actor, SubmissionTile(), SubmissionPlayer()),
        "submission.reversed" => L("{0} reversed the approval of the evidence for “{1}” by {2}.", Actor, SubmissionTile(), SubmissionPlayer()),
        "submission.withdrawn" => L("{0} withdrew the evidence for “{1}”.", Actor, SubmissionTile()),
        "submission.corrected" => L("{0} corrected the evidence for “{1}”.", Actor, SubmissionTile()),
        "submission.contribution_rebalanced" => L("The contribution of the evidence for “{0}” by {1} was recalculated after another approval was reversed.", SubmissionTile(), SubmissionPlayer()),
        "submission.request_changes" => L("{0} asked for changes to the evidence for “{1}”.", Actor, SubmissionTile()),
        "submission.duplicate" => L("{0} marked the evidence for “{1}” as a duplicate.", Actor, SubmissionTile()),
        "submission.hidden" => L("{0} hid the evidence for “{1}” from the public page.", Actor, SubmissionTile()),
        "submission.shown" => L("{0} made the evidence for “{1}” public again.", Actor, SubmissionTile()),
        "submission.replaced" => L("{0} replaced the screenshot for “{1}”.", Actor, SubmissionTile()),
        "submission.resubmitted" => L("{0} resubmitted evidence for “{1}”.", Actor, SubmissionTile()),
        "submission.historical_action" => L("{0} performed an older review action on the evidence for “{1}”.", Actor, SubmissionTile()),

        // Catalogue
        "catalogue.boss_created" => L("{0} added {1} to the catalogue.", Actor, Boss(target)),
        "catalogue.boss_updated" => L("{0} updated {1}.", Actor, Boss(target)),
        "catalogue.boss_toggled" => Str(after, "Active") == "false" ? L("{0} deactivated {1}.", Actor, Boss(target)) : L("{0} reactivated {1}.", Actor, Boss(target)),
        "catalogue.boss_deleted" => L("{0} deleted {1} from the catalogue.", Actor, Boss(target)),
        "catalogue.boss_api_updated" => L("{0} refreshed {1} from the wiki API.", Actor, Boss(target)),
        "catalogue.boss_api_verified" => L("{0} verified {1} against the wiki API.", Actor, Boss(target)),
        "catalogue.drop_created" => L("{0} added {1} as a drop from {2}.", Actor, DropItem(), DropBoss()),
        "catalogue.drop_updated" => L("{0} updated the {1} drop from {2}.", Actor, DropItem(), DropBoss()),
        "catalogue.drop_toggled" => Str(after, "Active") == "false" ? L("{0} deactivated the {1} drop from {2}.", Actor, DropItem(), DropBoss()) : L("{0} reactivated the {1} drop from {2}.", Actor, DropItem(), DropBoss()),
        "catalogue.drop_deleted" => L("{0} deleted the {1} drop from {2}.", Actor, DropItem(), DropBoss()),
        "catalogue.item_api_updated" => PriceChange() is var (oldPrice, newPrice)
            ? L("{0} updated the price of {1} from {2} to {3} gp.", Actor, Item(target), oldPrice, newPrice)
            : L("{0} updated the price data of {1}.", Actor, Item(target)),
        "catalogue.item_api_refreshed" => PriceChange() is var (oldRefresh, newRefresh)
            ? L("{0} refreshed the price of {1} from {2} to {3} gp.", Actor, Item(target), oldRefresh, newRefresh)
            : L("{0} refreshed the price data of {1}.", Actor, Item(target)),
        "catalogue.artwork_updated" => L("{0} adjusted the artwork of {1}.", Actor, Item(target)),
        "catalogue.artwork_reset" => L("{0} reset the artwork of {1}.", Actor, Item(target)),
        "catalogue.event_start_price_checked" => L("At the start of {0} the price {1} gp for {2} was rejected.", EventName(), Gp(Str(after, "candidate") ?? Str(after, "RejectedPriceGp")), Item(target)),
        _ => null
    };

    // ---- Name lookups with fallbacks (stored name → id) ----

    private string TargetAccount() => Account(target, entry.TargetId);

    private string Account(Guid? id, string? stored = null) =>
        id is { } key && names.Accounts.TryGetValue(key, out var name) ? name : stored ?? id?.ToString() ?? "—";

    private string Character(Guid? id) =>
        id is { } key && names.Characters.TryGetValue(key, out var name) ? name : id?.ToString() ?? "—";

    private string TargetPlayer() => Player(target);

    private string Player(Guid? participantId, string? stored = null) =>
        participantId is { } key && names.Participants.TryGetValue(key, out var participant) && participant.Character is { Length: > 0 } character
            ? character : stored ?? participantId?.ToString() ?? "—";

    private string MembershipPlayer(Guid? membershipId) =>
        membershipId is { } key && names.Memberships.TryGetValue(key, out var membership) ? Player(membership.ParticipantId) : membershipId?.ToString() ?? "—";

    private string MembershipTeam(Guid? membershipId) =>
        membershipId is { } key && names.Memberships.TryGetValue(key, out var membership) ? Team(membership.TeamId) : "—";

    private string Team(Guid? id, string? stored = null) =>
        id is { } key && names.Teams.TryGetValue(key, out var name) ? name : stored ?? id?.ToString() ?? "—";

    private string EventName()
    {
        var id = names.EventFor(entry) ?? (entry.TargetType == "event" ? target : null);
        return id is { } key && names.Events.TryGetValue(key, out var name) ? name
            : Str(after, "Name") is { } stored && entry.TargetType == "event" ? stored
            : id?.ToString() ?? "—";
    }

    private string BoardName() =>
        target is { } key && names.Boards.TryGetValue(key, out var name) ? name : Str(after, "Name") ?? Str(before, "Name") ?? entry.TargetId ?? "—";

    private string Tile(Guid? id) =>
        id is { } key && names.Tiles.TryGetValue(key, out var name) ? name : id?.ToString() ?? "—";

    private string TileName(JsonElement? state, string tile) =>
        Str(state, tile, "NameSnapshot") ?? Str(state, tile, "NameSnapshot", "Preview") ?? Tile(Id(state, tile, "Id"));

    private string Question() =>
        target is { } key && names.Questions.TryGetValue(key, out var label) ? label : Str(after, "Label") ?? Str(before, "Label") ?? entry.TargetId ?? "—";

    private string Boss(Guid? id) =>
        id is { } key && names.Bosses.TryGetValue(key, out var name) ? name : Str(after, "Name") ?? Str(before, "Name") ?? Plain(entry.Details) ?? id?.ToString() ?? "—";

    private string Item(Guid? id) =>
        id is { } key && names.Items.TryGetValue(key, out var name) ? name : Str(after, "Name") ?? Str(before, "Name") ?? Plain(entry.Details) ?? id?.ToString() ?? "—";

    private (string? Boss, string? Item) StoredDropText()
    {
        var plain = Plain(entry.Details);
        var split = plain?.IndexOf(": ", StringComparison.Ordinal) ?? -1;
        return split > 0 ? (plain![..split], plain[(split + 2)..]) : (null, null);
    }

    private string DropItem()
    {
        if (target is { } key && names.Drops.TryGetValue(key, out var drop) && drop.Item is { } item) return item;
        if ((Id(after, "ItemId") ?? Id(before, "ItemId") ?? Id(after, "Drop", "ItemId")) is { } itemId && names.Items.TryGetValue(itemId, out var byId)) return byId;
        return Str(after, "Item", "Name") ?? StoredDropText().Item ?? (entry.Action == "catalogue.drop_toggled" ? Plain(entry.Details) : null) ?? entry.TargetId ?? "—";
    }

    private string DropBoss()
    {
        if (target is { } key && names.Drops.TryGetValue(key, out var drop) && drop.Boss is { } boss) return boss;
        if ((Id(after, "BossActivityId") ?? Id(before, "BossActivityId") ?? Id(after, "Drop", "BossActivityId")) is { } bossId && names.Bosses.TryGetValue(bossId, out var byId)) return byId;
        return StoredDropText().Boss ?? "—";
    }

    private string SubmissionTile()
    {
        var tileId = Id(after, "BoardTileId") ?? Id(before, "BoardTileId")
            ?? (target is { } key && names.Submissions.TryGetValue(key, out var submission) ? submission.TileId : null);
        return Tile(tileId);
    }

    private string SubmissionPlayer() =>
        Str(after, "CreditedCharacterName") ?? Str(before, "CreditedCharacterName")
        ?? (target is { } key && names.Submissions.TryGetValue(key, out var submission) ? submission.CharacterName : null)
        ?? "—";

    // B3: published name → stored character name → the participant's current character → id.
    private string RosterPlayer() => Str(after, "participant", "name") ?? Str(before, "participant", "name") ?? FirstPlaying(details) ?? Player(Id(after, "participant", "id"));

    /// <summary>B3: the character name stored in a finalized-roster entry's participant snapshot.</summary>
    public static string? StoredRosterCharacter(AuditEntry entry) => FirstPlaying(AuditNameResolver.Parse(entry.Details));

    private string RosterTeam() => Team(Id(after, "teamId"), Str(after, "teamName"));

    private string AddedPlayer()
    {
        if (entry.TargetType == "team")
            return Plain(entry.Details) is { } text && text.Length > 36 && Guid.TryParse(text[..36], out var participant) ? Player(participant) : "—";
        return MembershipPlayer(target);
    }

    private (string Source, string Destination)? MovedTeams()
    {
        var match = Regex.Match(entry.Details ?? string.Empty, @"\A(?<source>.+?) → (?<destination>.+?): ", RegexOptions.Singleline);
        return match.Success ? (match.Groups["source"].Value, match.Groups["destination"].Value) : null;
    }

    private int? EndedMemberCount() => Int(after, "EndedMembershipCount") ?? Count(after, "EndedMembershipIds");

    // Review 156 L5: an oversized stored number is no number, not a page failure.
    private int? CapacityPromoted() => int.TryParse(Match(entry.Details, @"promoted (\d+)"), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    private string? Competition()
    {
        var match = Regex.Match(entry.Details ?? string.Empty, @"competition (?<id>\d+)(?: \((?<title>.*)\))?\.?\z");
        if (!match.Success) return null;
        return match.Groups["title"].Success ? $"{match.Groups["id"].Value} ({match.Groups["title"].Value})" : match.Groups["id"].Value;
    }

    private string? ReopenedUntil()
    {
        var value = Match(entry.Details, @"\AUntil ([^;]+);");
        return value is not null && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var until)
            ? DateTimePresentation.ToTimezone(until).ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture)
            : null;
    }

    private (string Old, string New)? PriceChange()
    {
        var old = Str(before, "CatalogueValueGp");
        var updated = Str(after, "CatalogueValueGp");
        return old != updated && updated is not null ? (Gp(old), Gp(updated)) : null;
    }

    private static string Gp(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number.ToString("N0", CultureInfo.CurrentCulture) : value ?? "—";

    private static string Position(JsonElement? state, params string[] path) =>
        Int(state, path) is { } index ? (index + 1).ToString(CultureInfo.CurrentCulture) : "—";

    private string Value(string? raw) => raw is null ? "—" : text[AuditPresenter.ValueLabel(raw)].Value;

    private static string? Scalar(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim() is "null" ? "—" : raw.Trim().Trim('"');
    }

    private static string? Plain(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.TrimStart().StartsWith('{') || value.TrimStart().StartsWith('[') ? null : value;

    private static string? Match(string? value, string pattern)
    {
        if (value is null) return null;
        var match = Regex.Match(value, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    // ---- JSON access (property names compared without case; scalars as text) ----

    private static JsonElement? Child(JsonElement? element, string name)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value) return null;
        foreach (var property in value.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return null;
    }

    private static JsonElement? Path(JsonElement? element, string[] path)
    {
        var current = element;
        foreach (var name in path) current = Child(current, name);
        return current;
    }

    private static string? Str(JsonElement? element, params string[] path) => Path(element, path) switch
    {
        { ValueKind: JsonValueKind.String } value => value.GetString(),
        { ValueKind: JsonValueKind.Number } value => value.GetRawText(),
        { ValueKind: JsonValueKind.True } => "true",
        { ValueKind: JsonValueKind.False } => "false",
        _ => null
    };

    private static Guid? Id(JsonElement? element, params string[] path) => Guid.TryParse(Str(element, path), out var id) ? id : null;

    private static int? Int(JsonElement? element, params string[] path) =>
        Path(element, path) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number) ? number : null;

    private static int? Count(JsonElement? element, params string[] path) =>
        Path(element, path) is { ValueKind: JsonValueKind.Array } value ? value.GetArrayLength() : null;
}
