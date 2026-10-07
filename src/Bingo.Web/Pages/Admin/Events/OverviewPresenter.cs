using System.Globalization;
using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Web.Navigation;
using Bingo.Web.UI;

namespace Bingo.Web.Pages.Admin.Events;

// U4 / OS-1: binds Overview.dc.html (vmStages, vmIssues, vmNow, vmGlance, vmLinks,
// vmMore and the lifecycle dialogs) to server data. Presentation only: every rule
// here mirrors a service rule, and the services re-check every action.
public sealed record OverviewInput(
    BingoEvent Event, DateTimeOffset Now, bool SuperAdmin,
    int Confirmed, int Waiting, int TeamCount, int Players,
    int BoardConfigured, int BoardTotal, bool BoardPublished, bool RosterPublished,
    int Pending, int Approved,
    IReadOnlyList<ReadinessItem> StartBlockers, SignupReadiness? Signup, SignupReadiness? Reopen, ReadinessItem? Overlap,
    FinalReviewReadiness? Final, bool EverFinalized,
    ScheduledEventStartAttempt? Postponed, ScheduledSignupOpeningAttempt? FailedOpening,
    EventCompetitionView? Wom, IReadOnlyList<ManageModel.EvidenceCodeRow> Codes, ReadinessSubject? OtherCurrent,
    bool CanDiscard, string? CancelledBy, string? HiddenBy,
    IReadOnlyList<OverviewPlacing> Placings, string PublicBase);

public sealed record OverviewPlacing(string Team, int Placement, int CompletedTiles);
public sealed record OverviewStage(string Name, string When, string Cls, bool Done, bool Ended, bool Current, string Sr);
public sealed record OverviewIssue(string Kind, string Title, string Text, string? ActionLabel, string? ActionHref, string? ActionKey, bool ActionInert = false);
public sealed record OverviewCheck(string Label, bool Done, string? Sub, string? LinkLabel, string? LinkHref);
public sealed record OverviewButton(string Key, string Label, string Cls, bool Inert);
public sealed record OverviewTransition(string Cls, string Title, string Sub, bool HasSchedule, IReadOnlyList<OverviewButton> Actions);
public sealed record OverviewFact(string Label, string Value, string? Sub = null, string? LinkHref = null, string? LinkTitle = null, bool Codes = false, bool Mono = false);
public sealed record OverviewLink(string Key, string Label, string Url, string Path, string? Note);
public sealed record OverviewActionRow(string Key, string Title, string Sub, string Label, string Cls);
public sealed record OverviewActionGroup(string Label, IReadOnlyList<OverviewActionRow> Rows);
public sealed record OverviewNow(string Title, string? Aside, string Lead, OverviewTransition? Transition,
    string? CheckTitle, IReadOnlyList<OverviewCheck> Checks, string? SecondCheckTitle, IReadOnlyList<OverviewCheck> SecondChecks,
    string? Later, string? Winner, string? WinnerSub, IReadOnlyList<OverviewFact> Placings,
    string? CancelWhen, string? CancelReason, string? FootNote, string? FootLabel, string? FootHref, string FootCls);
public sealed record OverviewDialog(string Key, string Handler, string Title, IReadOnlyList<string> Effects, string Confirm, string Cls,
    bool Applicable, bool Ready, string? Requirement, string Label,
    bool NeedsReason, string? ReasonField, string? ReasonLabel, string? ReasonHint,
    bool NeedsUntil, string? UntilField, string? UntilLabel, string? UntilHint, string? UntilDefault, string? UntilMin,
    string? ConfirmField, string? DonePhase, bool? DoneHidden, bool DoneGone, bool DoneReopened, string Success);
public sealed record OverviewCodeRow(string Code, string When, string? Note, string State, string PillCls);
public sealed record OverviewCodes(bool Enabled, bool Available, string EnabledSub, IReadOnlyList<OverviewCodeRow> List, string Timezone, string DefaultFrom);
public sealed record OverviewHidden(string Stage, string When, string Reason);
public sealed record OverviewView(string Name, string Phase, string BadgeClass, string Summary,
    IReadOnlyList<OverviewStage> Stages, IReadOnlyList<OverviewIssue> Issues, OverviewNow Now,
    IReadOnlyList<OverviewActionGroup> More, IReadOnlyList<OverviewFact> Glance,
    bool LinksShow, bool LinksPrivate, IReadOnlyList<OverviewLink> Links,
    IReadOnlyDictionary<string, OverviewDialog> Dialogs, OverviewCodes Codes, OverviewHidden? Hidden);

public sealed class OverviewPresenter(OverviewInput input, Func<string, object[], string> text, CultureInfo culture)
{
    private readonly BingoEvent e = input.Event;
    private readonly DateTimeOffset now = input.Now;
    private string T(string key, params object[] args) => text(key, args);
    private string Url(string page) => SharedShellService.AdminDesignEventUrl(page, e.Id);
    // Same label as the sidebar Board item (resource key "board").
    private string BoardLabel => culture.TextInfo.ToTitleCase(T("board"));
    private string Nf(int value) => value.ToString("N0", culture);
    private string Plural(int n, string one, string many) => T(n == 1 ? one : many, Nf(n));
    private TimeZoneInfo Zone => TryZone(e.Timezone);
    private static TimeZoneInfo TryZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    // Reference fmt(): day, short month, the year only when it isn't this year; 24-hour time.
    public string Fmt(DateTimeOffset? value, bool dateOnly = false, bool year = false)
    {
        if (value is null) return string.Empty;
        var local = TimeZoneInfo.ConvertTime(value.Value, Zone);
        var thisYear = TimeZoneInfo.ConvertTime(now, Zone).Year == local.Year && !year;
        var date = local.ToString(thisYear ? "d MMM" : "d MMM yyyy", culture);
        return dateOnly ? date : date + ", " + local.ToString("HH':'mm", culture);
    }
    private string Range(DateTimeOffset? start, DateTimeOffset? end) => start is null || end is null ? T("Not scheduled") : Fmt(start, true) + " – " + Fmt(end, true, true);
    private string InDays(DateTimeOffset value)
    {
        var days = (TimeZoneInfo.ConvertTime(value, Zone).Date - TimeZoneInfo.ConvertTime(now, Zone).Date).Days;
        return days <= 0 ? T("today") : days == 1 ? T("tomorrow") : T("in {0} days", Nf(days));
    }
    private static string LocalInput(DateTimeOffset value, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(value, zone).ToString("yyyy-MM-dd'T'HH':'mm", CultureInfo.InvariantCulture);
    private static DateTimeOffset CeilFive(DateTimeOffset value)
    {
        var step = TimeSpan.FromMinutes(5).Ticks;
        return new DateTimeOffset((value.UtcTicks + step - 1) / step * step, TimeSpan.Zero);
    }

    private bool IsPre => e.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
    private bool Hidden => e.IsHidden;
    private DateTimeOffset? UploadsUntil => new[] { e.SubmissionCutoffAt, e.ReopenedSubmissionCutoffAt }.Where(x => x is not null).Max();
    private bool UploadsOpen => e.AcceptsNewSubmissions(now);
    private bool CodesAvailable => IsPre || e.State == EventState.Live || e.State == EventState.AwaitingFinalReview && UploadsOpen;
    private bool WomLinked => input.Wom?.Configured == true;
    // A-Overview-7 (brief 85 default): the old Admin home rule (Pages/Admin/Index.cshtml:228).
    private bool WomFailed => e.State == EventState.Live && input.Wom is { Configured: true } wom
        && (!string.IsNullOrWhiteSpace(wom.LastError) || wom.MissingAccounts.Count > 0 || wom.Complete == false);

    public string PhaseLabel => e.State switch
    {
        EventState.Draft => T("Setup"), EventState.SignupOpen => T("Signups open"), EventState.SignupClosed => T("Signups closed"),
        EventState.Live => T("Live"), EventState.AwaitingFinalReview => T("Final review"), EventState.Finalized => T("Finished"),
        EventState.Archived => T("Archived"), EventState.Cancelled => T("Cancelled"), _ => e.State.ToString()
    };

    public OverviewView Build()
    {
        var phase = Hidden ? T("Hidden") : PhaseLabel;
        var badge = Hidden ? "badge-outline" : AdminDesignPhasePresentation.For(e.State).BadgeClass;
        var summary = Range(e.EventStartsAt, e.EventEndsAt) + " · " + e.Timezone + (e.State == EventState.Draft ? " · " + T("Private draft") : string.Empty);
        var dialogs = Dialogs();
        if (Hidden)
            return new(e.Name, phase, badge, summary, [], [], EmptyNow(), [], [], false, false, [], dialogs, Codes(),
                new(PhaseLabel, Fmt(e.HiddenAt) + (input.HiddenBy is null ? string.Empty : " " + T("by {0}", input.HiddenBy)), e.HiddenReason ?? string.Empty));
        var links = Links(out var show, out var isPrivate);
        return new(e.Name, phase, badge, summary, Stages(), Issues(dialogs), Now(dialogs), More(dialogs), Glance(), show, isPrivate, links, dialogs, Codes(), null);
    }

    private static OverviewNow EmptyNow() => new(string.Empty, null, string.Empty, null, null, [], null, [], null, null, null, [], null, null, null, null, null, string.Empty);

    // vmStages
    private List<OverviewStage> Stages()
    {
        var finished = e.State == EventState.Finalized;
        var index = e.State switch
        {
            EventState.Draft => 0, EventState.SignupOpen => 1, EventState.SignupClosed => 2, EventState.Live => 3,
            EventState.AwaitingFinalReview => 4, _ => 5
        };
        string Sched(DateTimeOffset? at, string verb) => at is null ? T("Not scheduled") : T(verb + " {0} · scheduled", Fmt(at));
        var published = e.ArchivedAt ?? e.FinalizedAt;
        var stages = new List<(string Name, string Done, string Cur, string Up)>
        {
            (T("Setup"), T("Private draft"), T("Private draft"), string.Empty),
            (T("Signups open"), e.ActualSignupOpenedAt is { } opened ? T("Opened {0}", Fmt(opened)) : T("Opened"), e.ActualSignupOpenedAt is { } o2 ? T("Opened {0}", Fmt(o2)) : string.Empty,
                e.SignupOpensAt is { } opens ? (e.ScheduledSignupOpeningEnabled ? T("Opens {0} · scheduled", Fmt(opens)) : T("Planned {0} · opens manually", Fmt(opens))) : T("Not scheduled")),
            (T("Signups closed"), e.ActualSignupClosedAt is { } closed ? T("Closed {0}", Fmt(closed)) : T("Closed"), e.ActualSignupClosedAt is { } c2 ? T("Closed {0}", Fmt(c2)) : string.Empty, Sched(e.SignupClosesAt, "Closes")),
            (T("Live"), e.ActualStartedAt is { } started ? T("Started {0}", Fmt(started)) : T("Started"), e.ActualStartedAt is { } s2 ? T("Started {0}", Fmt(s2)) : string.Empty,
                input.Postponed is { } postponed ? T("Was due {0} · postponed", Fmt(postponed.ScheduledFor)) : Sched(e.EventStartsAt, "Starts")),
            (T("Final review"), e.ActualEndedAt is { } ended ? T("Ended {0}", Fmt(ended)) : T("Ended"), e.ActualEndedAt is { } e2 ? T("Ended {0}", Fmt(e2)) : string.Empty,
                e.EventEndsAt is { } ends ? T("Ends {0} · scheduled", Fmt(ends)) : T("After the event ends")),
            (finished ? T("Finished") : T("Archived"), published is { } p ? T("Results published {0}", Fmt(p, true)) : string.Empty, published is { } p2 ? T("Results published {0}", Fmt(p2, true)) : string.Empty, T("When official results are published"))
        };
        if (e.State == EventState.Cancelled)
        {
            var reached = e.ActualSignupClosedAt is not null ? 3 : e.ActualSignupOpenedAt is not null ? 2 : 1;
            return stages.Take(reached).Select(s => new OverviewStage(s.Name, s.Done, "is-done", true, false, false, T("(done)")))
                .Append(new OverviewStage(T("Cancelled"), T("Cancelled {0}", Fmt(e.CancelledAt)), "is-ended", false, true, true, T("(where the event ended)"))).ToList();
        }
        return stages.Select((s, i) =>
        {
            var current = i == index;
            var overdue = i > index && i == 3 && input.Postponed is not null;
            var when = i < index ? s.Done : current ? (s.Cur.Length > 0 ? s.Cur : s.Done) : s.Up;
            return new OverviewStage(s.Name, when, i < index ? "is-done" : current ? "is-current" : overdue ? "is-overdue" : string.Empty, i < index, false, current,
                i < index ? T("(done)") : current ? T("(current stage)") : overdue ? T("(overdue: the scheduled start didn’t happen)") : T("(not yet)"));
        }).ToList();
    }

    // Checklists, mapped from the server readiness codes (README "What needs integration").
    private List<OverviewCheck> SignupChecks(SignupReadiness? readiness)
    {
        var blockers = readiness?.Blockers.Select(x => x.Code).ToHashSet() ?? [];
        var list = new List<OverviewCheck>
        {
            new(T("Public description"), !blockers.Contains("DESCRIPTION_REQUIRED"), blockers.Contains("DESCRIPTION_REQUIRED") ? T("Players read it on the signup page.") : null, T("Identity"), Url("/Admin/Events/Identity")),
            new(T("Participant capacity"), !blockers.Contains("PARTICIPANT_CAP_REQUIRED"), e.ParticipantCap is > 0 ? T("{0} places", Nf(e.ParticipantCap.Value)) : T("How many players can be confirmed."), T("Signup setup"), Url("/Admin/Events/SignupSetup")),
            new(T("Event start and end"), !blockers.Overlaps(["EVENT_START_REQUIRED", "EVENT_END_REQUIRED", "EVENT_WINDOW_INVALID"]),
                e.EventStartsAt is not null && e.EventEndsAt is not null ? Range(e.EventStartsAt, e.EventEndsAt) : T("Signups need an event window."), T("Schedule"), Url("/Admin/Events/Schedule"))
        };
        if (e.ScheduledSignupOpeningEnabled)
            list.Add(new(T("Signup closing time"), e.SignupClosesAt is not null, e.SignupClosesAt is { } close ? Fmt(close) : T("Needed for the automatic opening on {0}.", Fmt(e.SignupOpensAt, true)), T("Schedule"), Url("/Admin/Events/Schedule")));
        var formOk = !blockers.Overlaps(["SIGNUP_FORM_MISSING", "SIGNUP_QUESTIONS_INVALID"]);
        list.Add(new(T("Signup form"), formOk, formOk ? T("Account and captain questions are ready.") : T("Some signup questions are missing or incomplete."), T("Signup setup"), Url("/Admin/Events/SignupSetup") + "?tab=form"));
        // A-Overview-3: every other server refusal, as a row when it applies.
        if (blockers.Contains("SIGNUP_CODE_UNUSABLE"))
            list.Add(new(T("Signup code"), false, T("Protection is on, but no code is set."), T("Signup setup"), Url("/Admin/Events/SignupSetup")));
        if (blockers.Contains("DISCORD_AUTH_UNAVAILABLE"))
            list.Add(new(T("Discord sign-in"), false, T("Players sign up with Discord, and it isn’t configured on this site. Ask the site operator."), null, null));
        if (input.Overlap is { Subject: { } other })
            list.Add(new(T("No overlap with another public event"), false, T("The event window overlaps {0}.", other.Name), T("Schedule"), Url("/Admin/Events/Schedule")));
        if (readiness is not null && !readiness.CloseDecision.IsValid && !readiness.CloseDecision.RequiresAcceptance && !blockers.Overlaps(["EVENT_START_REQUIRED", "EVENT_END_REQUIRED"]))
            list.Add(new(T("A future time to close signups"), false, T("Set a future closing time, draft time or event start."), T("Schedule"), Url("/Admin/Events/Schedule")));
        var known = new HashSet<string>(["DESCRIPTION_REQUIRED", "PARTICIPANT_CAP_REQUIRED", "EVENT_START_REQUIRED", "EVENT_END_REQUIRED", "EVENT_WINDOW_INVALID", "SIGNUP_FORM_MISSING", "SIGNUP_QUESTIONS_INVALID", "SIGNUP_CODE_UNUSABLE", "DISCORD_AUTH_UNAVAILABLE", "LIFECYCLE_STATE_INVALID", "DRAFT_LOCKED"]);
        foreach (var unknown in readiness?.Blockers.Where(x => !known.Contains(x.Code)) ?? [])
            list.Add(new(T(unknown.Description), false, null, null, null));
        return list;
    }
    private List<OverviewCheck> StartChecks()
    {
        var codes = input.StartBlockers.Select(x => x.Code).ToHashSet();
        var playing = input.StartBlockers.Count(x => x.Code == "PARTICIPANT_PLAYING_ASSIGNMENT_INVALID");
        var list = new List<OverviewCheck>
        {
            new(T("Team draft finalized"), !codes.Contains("DRAFT_NOT_FINALIZED"), null, T("Teams / Draft"), Url("/Admin/Events/Draft")),
            new(T("Board published"), !codes.Contains("BOARD_NOT_PUBLISHED"), codes.Contains("BOARD_NOT_PUBLISHED") ? T("{0} of {1} tiles configured", Nf(input.BoardConfigured), Nf(input.BoardTotal)) : null, BoardLabel, Url("/Admin/Events/Board")),
            new(T("Drop values set in the catalogue"), !codes.Contains("DROP_PRICE_MISSING"), input.StartBlockers.FirstOrDefault(x => x.Code == "DROP_PRICE_MISSING")?.Description, T("Catalogue"), "/Admin/Catalogue/Index"),
            new(T("A playing account for every participant"), playing == 0, playing > 0 ? Plural(playing, "{0} participant needs one", "{0} participants need one") : null, T("Participants"), Url("/Admin/Events/Participants"))
        };
        if (codes.Contains("SCHEDULE_INVALID"))
            list.Add(new(T("Event start and end"), false, T("Set a valid event start and end."), T("Schedule"), Url("/Admin/Events/Schedule")));
        if (codes.Contains("EVENT_END_PASSED"))
            list.Add(new(T("Event end still ahead"), false, T("The configured end has passed. Replace the schedule, or cancel the event."), T("Schedule"), Url("/Admin/Events/Schedule")));
        // S2 (decided): another event is still current.
        if (input.StartBlockers.FirstOrDefault(x => x.Code == "CURRENT_EVENT_EXISTS")?.Subject is { } other)
            list.Add(CurrentEventCheck(other));
        return list;
    }
    private OverviewCheck CurrentEventCheck(ReadinessSubject other) => other.State == EventState.Finalized
        ? new(T("{0} is still the current event. Contact the Super Admin to archive it.", other.Name), false, null, T("Open event"), SharedShellService.AdminDesignEventUrl("/Admin/Events/Manage", other.EventId))
        : new(T("Publish the results of {0} first", other.Name), false, null, T("Open event"), SharedShellService.AdminDesignEventUrl("/Admin/Events/Manage", other.EventId));
    private List<OverviewCheck> PublishChecks()
    {
        var blockers = input.Final?.Blockers.Where(x => !x.Resolved).ToList() ?? [];
        var until = UploadsUntil;
        var placementsOk = !blockers.Any(x => x.Key == "calculated-placements");
        var list = new List<OverviewCheck>
        {
            new(T("Uploads closed"), !UploadsOpen, UploadsOpen ? T("Open until {0}", Fmt(until)) : until is null ? null : T("Closed {0}", Fmt(until)), null, null),
            new(T("Every submission reviewed"), input.Pending == 0, input.Pending > 0 ? Plural(input.Pending, "{0} submission still to review", "{0} submissions still to review") : null, T("Review"), Url("/Admin/Review/Index")),
            new(T("Placements calculated"), placementsOk, placementsOk ? T("From approved submissions; not official until published.") : T("Placements can’t be calculated yet."), placementsOk ? null : T("Final review"), Url("/Admin/Events/Finalize"))
        };
        // U4-Q2 (c): the reference rows plus one row for the server's other blockers.
        var other = blockers.Count(x => x.Key != "submission-window" && !x.Key.StartsWith("pending-submissions", StringComparison.Ordinal) && x.Key != "calculated-placements");
        if (other > 0) list.Add(new(Plural(other, "{0} more on Final review", "{0} more on Final review"), false, null, T("Final review"), Url("/Admin/Events/Finalize")));
        return list;
    }
    private static bool Ready(IEnumerable<OverviewCheck> list) => list.All(x => x.Done);

    // vmIssues: actual failures and pending work, each once.
    private List<OverviewIssue> Issues(Dictionary<string, OverviewDialog> dialogs)
    {
        var list = new List<OverviewIssue>();
        if (input.Postponed is { } postponed && IsPre)
        {
            // U4-Q1 (c): every pre-Live phase, with the phase-specific reason.
            string reason;
            if (e.State == EventState.SignupClosed)
            {
                var left = StartChecks().Count(x => !x.Done);
                reason = left > 0 ? T(left == 1 ? "{0} requirement below still needs doing." : "{0} requirements below still need doing.", Nf(left)) : T("Everything is ready now, so you can start the event.");
            }
            else
            {
                reason = e.State == EventState.Draft ? T("Open and close signups first.") : T("Close signups first.");
                if (input.OtherCurrent is { } other) reason += " " + CurrentEventCheck(other).Label + (other.State == EventState.Finalized ? string.Empty : ".");
            }
            list.Add(new("failure", T("Automatic start postponed"), T("It was due {0} and won’t retry.", Fmt(postponed.ScheduledFor)) + " " + reason, null, null, null));
        }
        if (input.FailedOpening is { } failed && e.State == EventState.Draft)
        {
            // S3 (decided): reason wording from the blocker codes (proposal, brief 85).
            var open = dialogs["open"];
            list.Add(new("failure", T("Signups didn’t open automatically"), T("It was due {0}: {1}.", Fmt(failed.ScheduledFor), OpeningReason(failed)), T("Open signups now"), null, "open", !open.Ready));
        }
        if (WomFailed)
            list.Add(new("failure", T("Wise Old Man sync failed"), T("Last attempt {0}. Lifecycle actions aren’t affected.", Fmt(input.Wom!.LastAttemptAt)), T("Open WOM"), Url("/Admin/Events/WiseOldMan"), null));
        if (e.State == EventState.Live && input.Pending > 0)
            list.Add(new("pending", Plural(input.Pending, "{0} submission to review", "{0} submissions to review"), T("Teams are waiting for decisions on their evidence."), T("Review submissions"), Url("/Admin/Review/Index"), null));
        // A15: Final review while the WOM end update is Pending or Rejected, linking WOM.
        var endStatus = input.Wom?.EndUpdateStatus ?? EventCompetitionEndUpdateStatus.NotRequired;
        if (e.State == EventState.AwaitingFinalReview && endStatus is EventCompetitionEndUpdateStatus.Pending or EventCompetitionEndUpdateStatus.Rejected)
            list.Add(endStatus == EventCompetitionEndUpdateStatus.Pending
                ? new("pending", T("Wise Old Man end not updated yet"), T("The competition should end {0}. Until it does, no more updates are fetched.", Fmt(input.Wom!.EndUpdateTargetAt)), T("Open WOM"), Url("/Admin/Events/WiseOldMan"), null)
                : new("failure", T("Wise Old Man rejected the new end"), T("The competition should end {0}. Until it does, no more updates are fetched.", Fmt(input.Wom!.EndUpdateTargetAt)), T("Open WOM"), Url("/Admin/Events/WiseOldMan"), null));
        return list;
    }
    private string OpeningReason(ScheduledSignupOpeningAttempt failed)
    {
        var reasons = failed.Blockers.Select(code => code switch
        {
            "DESCRIPTION_REQUIRED" => T("the public description was missing"),
            "PARTICIPANT_CAP_REQUIRED" => T("no participant capacity was set"),
            "EVENT_START_REQUIRED" or "EVENT_END_REQUIRED" => T("the event start or end was missing"),
            "EVENT_WINDOW_INVALID" => T("the event ended before it started"),
            "SIGNUP_CLOSE_REQUIRED" => T("no signup closing time was set"),
            "SIGNUP_CLOSE_NOT_FUTURE" => T("the signup closing time had passed"),
            "SIGNUP_CLOSE_AFTER_EVENT_START" => T("signups would have closed after the event start"),
            "SCHEDULED_OPENING_INVALID" or "SCHEDULED_WINDOW_INVALID" => T("the opening and closing times didn’t fit together"),
            "LIFECYCLE_STATE_INVALID" => T("the event was no longer a private draft"),
            "DRAFT_LOCKED" => T("the team draft had started"),
            "DISCORD_AUTH_UNAVAILABLE" => T("Discord sign-in wasn’t configured"),
            "SIGNUP_FORM_MISSING" => T("there was no signup form"),
            "SIGNUP_QUESTIONS_INVALID" => T("some signup questions were incomplete"),
            "SIGNUP_CODE_UNUSABLE" => T("a signup code was required but none was set"),
            "EVENT_WINDOW_OVERLAP" => T("the event window overlapped another public event"),
            _ => T("a requirement wasn’t met")
        }).Distinct().ToList();
        if (reasons.Count == 0) return T("a requirement wasn’t met");
        return reasons.Count == 1 ? reasons[0] : T("{0} and {1}", string.Join(", ", reasons.Take(reasons.Count - 1)), reasons[^1]);
    }

    private static OverviewButton Button(Dictionary<string, OverviewDialog> dialogs, string key, string cls) => new(key, dialogs[key].Label, cls, !dialogs[key].Ready);

    // vmNow
    private OverviewNow Now(Dictionary<string, OverviewDialog> d)
    {
        var later = T("Before the start: finalize the team draft and publish the board.");
        switch (e.State)
        {
            case EventState.Draft:
            {
                OverviewTransition tr = e.ScheduledSignupOpeningEnabled && e.SignupOpensAt is { } opens
                    ? new(string.Empty, T("Signups open automatically on {0}", Fmt(opens)), T("If something below is still missing then, the opening is skipped and admins are notified."), false, [Button(d, "open", "btn-primary")])
                    : e.SignupOpensAt is { } planned
                        ? new(string.Empty, T("Signups are planned for {0}", Fmt(planned)), T("They open only when you open them, unless you turn on automatic opening on Schedule."), false, [Button(d, "open", "btn-primary")])
                        : new("is-none", T("No opening time is scheduled"), T("Signups open when you open them. To schedule the opening, set a time on Schedule."), true, [Button(d, "open", "btn-primary")]);
                return new(T("Private draft"), null, T("Players can’t see this event yet. It becomes public when signups open."), tr,
                    T("Before you can open signups"), SignupChecks(input.Signup), null, [], T("Later, before the start: finalize the team draft and publish the board."),
                    null, null, [], null, null, null, null, null, string.Empty);
            }
            case EventState.SignupOpen:
            {
                var lead = (e.ParticipantCap is > 0 ? T("{0} of {1} places confirmed", Nf(input.Confirmed), Nf(e.ParticipantCap.Value)) : T("{0} confirmed", Nf(input.Confirmed)))
                    + (input.Waiting > 0 ? T(", {0} waiting", Nf(input.Waiting)) : string.Empty) + ". " + T("Opened {0}.", Fmt(e.ActualSignupOpenedAt));
                OverviewTransition tr = e.SignupClosesAt is { } closes
                    ? new(string.Empty, T("Signups close automatically on {0}", Fmt(closes)), T("Players already on the list aren’t affected."), false, [Button(d, "close", string.Empty)])
                    : new("is-none", T("No closing time is set"), T("Signups stay open until you close them."), true, [Button(d, "close", string.Empty)]);
                return new(T("Signups are open"), null, lead, tr, null, [], null, [], later + (e.DraftAt is { } draft ? " " + T("The team draft is planned for {0}.", Fmt(draft)) : string.Empty),
                    null, null, [], null, null, null, null, null, string.Empty);
            }
            case EventState.SignupClosed:
            {
                var checks = StartChecks();
                var ready = Ready(checks);
                var lead = T("Signups closed {0}.", Fmt(e.ActualSignupClosedAt)) + " " + T("{0} confirmed", Nf(input.Confirmed)) + (input.Waiting > 0 ? T(", {0} waiting", Nf(input.Waiting)) : string.Empty) + ".";
                var buttons = new List<OverviewButton>();
                if (d["reopen"].Applicable) buttons.Add(Button(d, "reopen", string.Empty));
                buttons.Add(Button(d, "start", "btn-primary"));
                OverviewTransition tr = input.Postponed is not null
                    ? new("is-none", T("Start the event yourself"), ready ? T("Everything below is done. The automatic start won’t retry.") : T("The automatic start won’t retry. Finish everything below, then start it."), false, buttons)
                    : e.EventStartsAt is { } starts
                        ? new(string.Empty, T("Starts automatically on {0}", Fmt(starts)), T("If something below is still missing then, the start is postponed and admins are notified."), false, buttons)
                        : new("is-none", T("No start time is scheduled"), T("Set the event window on Schedule."), true, buttons);
                // A-Overview-3: Reopen uses the signup checklist; shown only while it blocks Reopen.
                var reopenChecks = d["reopen"].Applicable ? SignupChecks(input.Reopen).Where(x => !x.Done).ToList() : [];
                return new(T("Getting ready to start"), null, lead, tr, T("Before you can start the event"), checks,
                    reopenChecks.Count > 0 ? T("Before you can reopen signups") : null, reopenChecks, null, null, null, [], null, null, null, null, null, string.Empty);
            }
            case EventState.Live:
            {
                var lead = T("Started {0}.", Fmt(e.ActualStartedAt)) + " " + Plural(input.TeamCount, "{0} team", "{0} teams") + ", " + Plural(input.Players, "{0} player", "{0} players") + ".";
                var tr = e.EventEndsAt is { } ends
                    ? new OverviewTransition(string.Empty, T("Ends automatically on {0}", Fmt(ends)), T("Uploads close 30 minutes later, at {0}.", Fmt(ends.AddMinutes(30))), false, [Button(d, "end", string.Empty)])
                    : new OverviewTransition("is-none", T("No end time is scheduled"), T("Set the event window on Schedule."), true, [Button(d, "end", string.Empty)]);
                return new(T("Live"), e.EventEndsAt is { } end ? T("Ends {0}", InDays(end)) : null, lead, tr, null, [], null, [], null,
                    null, null, [], null, null, null, null, null, string.Empty);
            }
            case EventState.AwaitingFinalReview:
            {
                var until = UploadsUntil;
                OverviewTransition tr = e.ReopenedSubmissionCutoffAt is { } reopened && UploadsOpen && reopened == until
                    ? new(string.Empty, T("Uploads reopened until {0}", Fmt(reopened)), T("Results can be published once they close."), false, [])
                    : UploadsOpen
                        ? new(string.Empty, T("Uploads close on {0}", Fmt(until)), T("30 minutes after the end, for drops from before it."), false, [])
                        : new("is-none", T("Uploads closed {0}", Fmt(until)), T("If a team needs more time, reopen uploads under Other actions."), false, []);
                return new(T("Final review"), null, T("Ended {0}. Check the outstanding work, then publish the official results from Final review.", Fmt(e.ActualEndedAt)), tr,
                    T("Before you can publish official results"), PublishChecks(), null, [], null, null, null, [], null, null,
                    T("Publishing makes the results official and archives the event."), T("Open Final review"), Url("/Admin/Events/Finalize"), "btn-primary");
            }
            case EventState.Archived or EventState.Finalized:
            {
                var published = e.ArchivedAt ?? e.FinalizedAt;
                var winners = input.Placings.Where(x => x.Placement == 1).Select(x => x.Team).ToList();
                var placings = input.Placings.OrderBy(x => x.Placement).Take(3).Select(x => new OverviewFact(Ordinal(x.Placement) + " · " + x.Team,
                    input.BoardTotal > 0 ? T("{0} of {1} tiles", Nf(x.CompletedTiles), Nf(input.BoardTotal)) : Plural(x.CompletedTiles, "{0} tile", "{0} tiles"))).ToList();
                return new(T("Results are official"), null, e.State == EventState.Archived ? T("Published {0}. The event is archived.", Fmt(published)) : T("Published {0}.", Fmt(published)), null, null, [], null, [], null,
                    winners.Count > 0 ? string.Join(" · ", winners) : null, winners.Count > 0 ? Plural(input.TeamCount, "{0} team competed", "{0} teams competed") : null, placings,
                    null, null, T("To correct them, reopen the results from Final review."), T("Final review & results"), Url("/Admin/Events/Finalize"), string.Empty);
            }
            case EventState.Cancelled:
                return new(T("This event was cancelled"), null, T("Its history is kept and nothing can be changed. Participants and signups stay readable on their pages."), null, null, [], null, [], null,
                    null, null, [], Fmt(e.CancelledAt) + (input.CancelledBy is null ? string.Empty : " " + T("by {0}", input.CancelledBy)), e.CancellationReason ?? string.Empty, null, null, null, string.Empty);
        }
        return EmptyNow();
    }
    private string Ordinal(int placement) => placement switch { 1 => T("1st"), 2 => T("2nd"), 3 => T("3rd"), _ => T("{0}th", Nf(placement)) };

    // vmMore: secondary and exceptional actions.
    private List<OverviewActionGroup> More(Dictionary<string, OverviewDialog> d)
    {
        var groups = new List<OverviewActionGroup>();
        OverviewActionRow Row(string key, string cls, string sub) => new(key, d[key].Label.Replace("…", string.Empty, StringComparison.Ordinal), sub, d[key].Label, cls);
        var recovery = new List<OverviewActionRow>();
        if (d["resume"].Applicable) recovery.Add(Row("resume", string.Empty, T("If it ended by mistake. It goes back to Live; the paused time stays in its history.")));
        if (d["reopenUploads"].Applicable) recovery.Add(Row("reopenUploads", string.Empty, T("Give teams more time to upload evidence from the event window.")));
        if (recovery.Count > 0) groups.Add(new(T("Recovery"), recovery));
        if (d["del"].Applicable) groups.Add(new(T("Remove"), [Row("del", "btn-outline-danger", T("This setup is empty, so it can be removed permanently."))]));
        if (d["cancel"].Applicable) groups.Add(new(T("Remove"), [Row("cancel", "btn-outline-danger", T("Stops the event for good and keeps its history. Players are notified."))]));
        if (d["hide"].Applicable) groups.Add(new(T("Quarantine · SuperAdmin"), [Row("hide", "btn-outline-danger", T("Removes it from every ordinary page and count. Data is kept and it can be restored."))]));
        return groups;
    }

    // vmGlance
    private List<OverviewFact> Glance()
    {
        var rows = new List<OverviewFact>();
        if (IsPre || e.State == EventState.Cancelled)
            rows.Add(new(T("Participants"), e.ParticipantCap is > 0 ? Nf(input.Confirmed) + " / " + Nf(e.ParticipantCap.Value) : Nf(input.Confirmed),
                input.Waiting > 0 ? T("{0} waiting", Nf(input.Waiting)) : e.ParticipantCap is > 0 ? T("confirmed") : T("No capacity set"), Url("/Admin/Events/Participants"), T("Open {0}", T("Participants"))));
        else
            rows.Add(new(T("Teams"), Plural(input.TeamCount, "{0} team", "{0} teams"), Plural(input.Players, "{0} player", "{0} players"), Url("/Admin/Events/Draft"), T("Open {0}", T("Teams"))));
        if (e.State != EventState.SignupClosed && (e.State != EventState.Cancelled || input.BoardTotal > 0))
            rows.Add(new(BoardLabel, input.BoardPublished ? T("Published") : input.BoardTotal > 0 ? T("{0} / {1} tiles", Nf(input.BoardConfigured), Nf(input.BoardTotal)) : T("Not set up"),
                input.BoardPublished ? T("{0} tiles", Nf(input.BoardTotal)) : input.BoardTotal > 0 ? T("Not published") : null,
                e.State == EventState.Cancelled ? null : Url("/Admin/Events/Board"), T("Open {0}", BoardLabel)));
        if (e.State is EventState.Live or EventState.AwaitingFinalReview or EventState.Archived or EventState.Finalized)
            rows.Add(new(T("Approved submissions"), Nf(input.Approved), null, e.State is EventState.Archived or EventState.Finalized ? null : Url("/Admin/Review/Index"), T("Open {0}", T("Review"))));
        if (e.State is EventState.Live or EventState.AwaitingFinalReview)
            rows.Add(new(T("Uploads"), e.ReopenedSubmissionCutoffAt is not null && UploadsOpen && e.ReopenedSubmissionCutoffAt == UploadsUntil ? T("Reopened") : UploadsOpen ? T("Open") : T("Closed"),
                UploadsOpen ? T("until {0}", Fmt(UploadsUntil)) : Fmt(UploadsUntil)));
        if (CodesAvailable || e.EvidenceCodeEnabled)
        {
            var active = ActiveCode(); var next = NextCode();
            var value = !e.EvidenceCodeEnabled ? T("Off") : active is not null ? active.Code : next is not null ? T("Starts {0}", Fmt(next.ActivatesAt)) : T("No code yet");
            var sub = !CodesAvailable ? T("Closed with the upload window") : e.EvidenceCodeEnabled
                ? (active is null ? string.Empty : T("since {0}", Fmt(active.ActivatesAt, true))) + (active is not null && next is not null ? " · " + T("next {0}", Fmt(next.ActivatesAt, true)) : string.Empty)
                : T("Not required in screenshots");
            rows.Add(new(T("Evidence codes"), value, sub.Length > 0 ? sub : null, CodesAvailable ? "#codes" : null, T("Manage evidence codes"), CodesAvailable, e.EvidenceCodeEnabled && active is not null));
        }
        if (e.State is not EventState.Cancelled and not EventState.Draft)
            rows.Add(new(T("Wise Old Man"), WomLinked ? T("Linked") : T("Not linked"), WomLinked && input.Wom!.LastSuccessfulAt is { } synced ? Synced(synced) : null, Url("/Admin/Events/WiseOldMan"), T("Open {0}", T("WOM"))));
        return rows;
    }
    private string Synced(DateTimeOffset at)
    {
        var minutes = (int)Math.Max(0, (now - at).TotalMinutes);
        return minutes < 1 ? T("Synced just now") : minutes < 60 ? T("Synced {0} min ago", Nf(minutes)) : minutes < 24 * 60 ? T("Synced {0} h ago", Nf(minutes / 60)) : T("Synced {0}", Fmt(at));
    }
    private ManageModel.EvidenceCodeRow? ActiveCode() => input.Codes.Where(x => x.ActivatesAt <= now).OrderBy(x => x.ActivatesAt).LastOrDefault();
    private ManageModel.EvidenceCodeRow? NextCode() => input.Codes.Where(x => x.ActivatesAt > now).OrderBy(x => x.ActivatesAt).FirstOrDefault();

    // vmLinks: EventDestinationPolicy facts decide, not the phase (RC01 R1).
    private List<OverviewLink> Links(out bool show, out bool isPrivate)
    {
        var cancelled = e.State == EventState.Cancelled;
        show = true; isPrivate = false;
        if (e.FirstPublicAt is null) { show = !cancelled; isPrivate = !cancelled; return []; }
        var results = e.State is EventState.Archived or EventState.Finalized;
        var board = input.BoardPublished && e.BoardPublished;
        var roster = input.RosterPublished;
        var destination = board || results ? "board" : roster ? "teams" : "signups";
        var accepting = e.State == EventState.SignupOpen && e.SignupClosesAt is { } closes && closes > now;
        var note = cancelled ? T("Visitors see that the event was cancelled.")
            : destination == "board" ? T("Visitors are taken to the board. Admins still see the signup list.")
            : destination == "teams" ? T("Visitors are taken to the teams. Admins still see the signup list.")
            : T("Shows the signup list. Once teams or the board are published, visitors are taken there instead.");
        var root = input.PublicBase + "/Events/" + Uri.EscapeDataString(e.Slug);
        OverviewLink Link(string key, string label, string path, string? linkNote = null) => new(key, label, root + path, "/Events/" + Uri.EscapeDataString(e.Slug) + path, linkNote);
        var list = new List<OverviewLink> { Link("signups", T("Signup list · permanent link"), "/Signups", note) };
        if (!cancelled)
        {
            if (accepting && destination == "signups") list.Add(Link("signup", T("Signup form"), "/Signup"));
            if (roster) list.Add(Link("teams", T("Teams"), "/Teams"));
            if (board) list.Add(Link("board", BoardLabel, "/Board"));
        }
        return list;
    }

    // Evidence codes dialog model (RC01 R2).
    private OverviewCodes Codes()
    {
        var active = ActiveCode();
        var rows = input.Codes.OrderByDescending(x => x.ActivatesAt).Select(x =>
        {
            var isActive = active is not null && x.Id == active.Id; var future = x.ActivatesAt > now;
            return new OverviewCodeRow(x.Code, future ? T("Starts {0}", Fmt(x.ActivatesAt)) : isActive ? T("Active since {0}", Fmt(x.ActivatesAt)) : T("From {0}", Fmt(x.ActivatesAt)),
                string.IsNullOrWhiteSpace(x.Note) ? null : x.Note, isActive ? T("Active") : future ? T("Scheduled") : T("Ended"), isActive ? string.Empty : "is-neutral");
        }).ToList();
        return new(e.EvidenceCodeEnabled, CodesAvailable, e.EvidenceCodeEnabled ? T("Reviewers expect the active code in every screenshot.") : T("Off: screenshots don’t need a code."),
            rows, e.Timezone, LocalInput(CeilFive(now.AddDays(1)), Zone));
    }

    // Lifecycle dialogs: title, consequences, inputs and applicability, recomputed
    // from the current state every time (stale re-evaluation reads them again).
    private Dictionary<string, OverviewDialog> Dialogs()
    {
        var name = e.Name;
        var d = new Dictionary<string, OverviewDialog>();
        var tz = e.Timezone;
        var reopenReady = input.Reopen is { } r && Ready(SignupChecks(r));
        var openReady = Ready(SignupChecks(input.Signup)) && input.Signup is not null;
        var publicText = (input.Signup?.Warnings ?? []).Concat(input.Reopen?.Warnings ?? []).Any(x => x.Code == "PUBLIC_FREE_TEXT");
        OverviewDialog Dialog(string key, string handler, string title, IEnumerable<string> effects, string confirm, string cls, bool applicable, bool ready, string label,
            string? requirement = null, string? reasonField = null, string? reasonLabel = null, string? reasonHint = null,
            string? untilField = null, string? untilLabel = null, string? untilHint = null, string? untilDefault = null,
            string? confirmField = null, string? donePhase = null, bool? doneHidden = null, bool doneGone = false, bool doneReopened = false, string? success = null) =>
            new(key, handler, title, effects.ToList(), confirm, cls, applicable, ready, requirement, label,
                reasonField is not null, reasonField, reasonLabel, reasonHint, untilField is not null, untilField, untilLabel, untilHint, untilDefault,
                untilField is null ? null : LocalInput(now, Zone), confirmField, donePhase, doneHidden, doneGone, doneReopened, success ?? T(key switch
                {
                    "open" => "Signups are open for {0}.", "close" => "Signups are closed for {0}.", "reopen" => "Signups are open again for {0}.",
                    "start" => "{0} is live.", "end" => "{0} ended and is in final review.", "resume" => "{0} is live again.",
                    "del" => "{0} was deleted.", "cancel" => "{0} was cancelled.", "hide" => "{0} is hidden.", _ => "{0} was restored."
                }, name));

        string Close(bool reopen)
        {
            var decision = (reopen ? input.Reopen : input.Signup)?.CloseDecision;
            if (decision is { IsValid: true } && e.SignupClosesAt is { } scheduled)
                return reopen ? T("Signups close automatically on {0} ({1}).", Fmt(scheduled), T("the scheduled closing time")) : T("Signups close automatically on {0}.", Fmt(scheduled));
            if (decision?.ProposedClose is { } proposed)
            {
                var why = e.DraftAt == proposed ? T("the team draft time") : T("the event start");
                return reopen ? T("Signups close automatically on {0} ({1}).", Fmt(proposed), why) : T("No closing time is set, so signups will close at {0}, {1}. You can change it on Schedule.", why, Fmt(proposed));
            }
            return T("There’s no future time to close signups at; set one on Schedule first.");
        }
        var people = Plural(input.Confirmed, "{0} confirmed player", "{0} confirmed players");

        var openEffects = new List<string> { T("Players can sign up right away, and the event becomes public: anyone with its links can see the signup list and the form."), Close(false) };
        if (e.ScheduledSignupOpeningEnabled && e.SignupOpensAt is { } autoOpen) openEffects.Add(T("The automatic opening on {0} is turned off; this replaces it.", Fmt(autoOpen)));
        openEffects.Add(T("From now on, a timezone change on Identity needs a review of the affected times."));
        if (publicText) openEffects.Add(T("Answers to text questions will be public on the signup table."));
        d["open"] = Dialog("open", "OpenSignup", T("Open signups for {0}?", name), openEffects, T("Open signups"), "btn-primary",
            e.State == EventState.Draft && !e.DraftLocked, openReady, T("Open signups now"), confirmField: "ConfirmSignupAction", donePhase: nameof(EventState.SignupOpen));

        var closeEffects = new List<string> { T("New signups stop now. The {0}{1} keep their places.", people, input.Waiting > 0 ? T(" and {0} on the waiting list", Nf(input.Waiting)) : string.Empty) };
        if (e.SignupClosesAt is { } scheduledClose && scheduledClose > now) closeEffects.Add(T("The scheduled close on {0} is no longer needed.", Fmt(scheduledClose)));
        closeEffects.Add(T("You can reopen signups until the team draft is locked."));
        d["close"] = Dialog("close", "CloseSignup", T("Close signups for {0}?", name), closeEffects, T("Close signups"), "btn-primary",
            e.State == EventState.SignupOpen, true, T("Close signups now"), confirmField: "ConfirmSignupAction", donePhase: nameof(EventState.SignupClosed));

        var reopenEffects = new List<string> { T("Signups open again. The {0}{1} keep their places, in the same order.", people, input.Waiting > 0 ? T(" and {0} waiting", Nf(input.Waiting)) : string.Empty), Close(true), T("Reopening isn’t possible once the team draft is locked.") };
        if (publicText) reopenEffects.Add(T("Answers to text questions will be public on the signup table."));
        d["reopen"] = Dialog("reopen", "ReopenSignup", T("Reopen signups for {0}?", name), reopenEffects, T("Reopen signups"), "btn-primary",
            e.State == EventState.SignupClosed && !e.DraftLocked, reopenReady, T("Reopen signups"), confirmField: "ConfirmSignupAction", donePhase: nameof(EventState.SignupOpen));

        var startEffects = new List<string>();
        if (input.Postponed is { } postponed) startEffects.Add(T("Its automatic start on {0} was postponed; this starts it now.", Fmt(postponed.ScheduledFor)));
        else if (e.EventStartsAt is { } start && start > now) startEffects.Add(T("This starts it now, ahead of its scheduled start on {0} ({1}).", Fmt(start), InDays(start)));
        startEffects.Add(e.EventEndsAt is { } plannedEnd ? T("Teams can upload evidence from now until 30 minutes after the end ({0}).", Fmt(plannedEnd.AddMinutes(30))) : T("Teams can upload evidence from now until 30 minutes after the end."));
        startEffects.Add(T("Drop values are fixed at today’s catalogue prices."));
        // F5 / A-Overview-5: the first refresh is due one hour after the start (EventLifecycleService).
        if (WomLinked) startEffects.Add(T("The first Wise Old Man refresh runs in 1 hour."));
        d["start"] = Dialog("start", "StartEvent", T("Start {0} now?", name), startEffects, T("Start event"), "btn-primary",
            e.State == EventState.SignupClosed, Ready(StartChecks()), T("Start event now"), confirmField: "ConfirmStartEvent", donePhase: nameof(EventState.Live));

        // README: a reason only before the scheduled end. AU20: the precise actual end is kept
        // and the configured end becomes the end, rounded up to the minute (proposal).
        var early = e.EventEndsAt is { } configuredEnd && configuredEnd > now;
        var endEffects = new List<string>();
        if (early) endEffects.Add(T("This ends it now, before its scheduled end on {0}. Its end time becomes {1}.", Fmt(e.EventEndsAt), Fmt(new DateTimeOffset((now.UtcTicks + TimeSpan.TicksPerMinute - 1) / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, TimeSpan.Zero))));
        endEffects.Add(T("Uploads stay open for 30 minutes, until {0}, for drops from before the end.", Fmt(now.AddMinutes(30))));
        endEffects.Add(T("The event moves to final review. If this was a mistake, you can resume it from there with a new end."));
        d["end"] = Dialog("end", "EndEvent", T("End {0} now?", name), endEffects, T("End event"), "btn-primary",
            e.State == EventState.Live, true, T("End event now"), reasonField: early ? "EndReason" : null, reasonLabel: T("Why are you ending it early?"),
            reasonHint: T("Required before the scheduled end. Recorded in the event history."), confirmField: "ConfirmEndEvent", donePhase: nameof(EventState.AwaitingFinalReview));

        // AU20: Resume always asks for a validated future replacement end (proposal wording).
        // OS-3: no Resume after finalization history. U4-Q4 (b): another current event is a
        // requirement shown inside the dialog.
        var resumeDefault = e.EventEndsAt is { } previous && previous > now.AddMinutes(5) ? CeilFive(previous) : CeilFive(now.AddDays(2));
        d["resume"] = Dialog("resume", "ResumeEvent", T("Resume {0}?", name),
            [T("The event goes back to Live. The time it was paused stays in its history."), T("It runs until the new end you choose, and uploads stay open until 30 minutes after it.")],
            T("Resume event"), "btn-primary", e.State == EventState.AwaitingFinalReview && !input.EverFinalized, input.OtherCurrent is null, T("Resume event…"),
            requirement: input.OtherCurrent is { } current ? CurrentEventCheck(current).Label + (current.State == EventState.Finalized ? string.Empty : ".") : null,
            reasonField: "ResumeReason", reasonLabel: T("Why are you resuming it?"), reasonHint: T("Recorded in the event history."),
            untilField: "ReplacementEventEndsAtLocal", untilLabel: T("New event end"),
            untilHint: e.EventEndsAt is { } oldEnd && oldEnd <= now ? T("The scheduled end ({0}) has passed. Choose a new end in 5-minute steps; times are in {1}.", Fmt(oldEnd), tz)
                : T("Choose the end again, even if it stays {0}. In 5-minute steps; times are in {1}.", Fmt(e.EventEndsAt), tz),
            untilDefault: LocalInput(resumeDefault, Zone), confirmField: "ConfirmResumeEvent", donePhase: nameof(EventState.Live));

        d["reopenUploads"] = Dialog("reopenUploads", "ReopenSubmissions", T("Reopen uploads for {0}?", name),
            [T("Teams can upload again until the time you choose, for drops from the event window."), T("Official results can’t be published until uploads close again.")],
            T("Reopen uploads"), "btn-primary", e.State == EventState.AwaitingFinalReview, true, T("Reopen uploads…"),
            reasonField: "StateReason", reasonLabel: T("Why do teams need more time?"), reasonHint: T("Recorded in the event history."),
            untilField: "ReopenUntilLocal", untilLabel: T("Accept uploads until"), untilHint: T("In 5-minute steps, {0}.", tz),
            untilDefault: LocalInput(CeilFive(now.AddHours(2)), Zone), doneReopened: true, success: T("Uploads are open until {0}.", "{0}"));

        d["del"] = Dialog("del", "Discard", T("Delete {0} permanently?", name),
            [T("This empty setup is removed for good. It has no participants, teams or submissions, so nothing needs to be kept."), T("Its links stop working. This can’t be undone.")],
            T("Delete event"), "btn-danger", IsPre && input.CanDiscard, true, T("Delete event…"), confirmField: "ConfirmDestructiveAction", doneGone: true);
        d["cancel"] = Dialog("cancel", "Cancel", T("Cancel {0}?", name),
            [T("The event stops for good, and its history is kept: {0}{1} stay on record.", people, input.Waiting > 0 ? T(" and {0} waiting", Nf(input.Waiting)) : string.Empty),
             T("Those players are notified that it was cancelled."), T("Scheduled openings and starts won’t run. This can’t be undone.")],
            T("Cancel event"), "btn-danger", IsPre && !input.CanDiscard, true, T("Cancel event…"),
            reasonField: "CancellationReason", reasonLabel: T("Reason"), reasonHint: T("Required. Visible to admins only."), confirmField: "ConfirmDestructiveAction", donePhase: nameof(EventState.Cancelled));
        d["hide"] = Dialog("hide", "Hide", T("Hide {0}?", name),
            [T("The event disappears from the directory, the Dashboard and its public pages. All its data is kept."), T("Nobody is notified. A SuperAdmin can restore it.")],
            T("Hide event"), "btn-danger", input.SuperAdmin && !Hidden && e.State is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived, true, T("Hide event…"),
            reasonField: "QuarantineReason", reasonLabel: T("Reason"), reasonHint: T("Required. Kept with the quarantine record."), confirmField: "ConfirmDestructiveAction", doneHidden: true);
        d["restore"] = Dialog("restore", "RestoreHidden", T("Restore {0}?", name),
            [T("It returns to the directory, the Dashboard and its public pages, as before it was hidden."), T("No notifications are sent.")],
            T("Restore event"), "btn-primary", input.SuperAdmin && Hidden, true, T("Restore…"), confirmField: "ConfirmDestructiveAction", doneHidden: false);
        return d;
    }
}
