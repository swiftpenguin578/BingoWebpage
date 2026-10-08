using System.Text;
using System.Globalization;
using Bingo.Domain.Events;

namespace Bingo.Web.TestData;

/// <summary>The same route/account inventory drives the printed guide and executable URL proof.</summary>
public static class UiReviewScenarioCatalogue
{
    public static IReadOnlyList<UiReviewLink> Links(UiReviewScenarios scenarios, Uri appUrl)
    {
        var links = new List<UiReviewLink>();
        void Add(string page, string name, string route, string username = "ReviewAdmin", bool hidden = false, bool anonymous = false) =>
            links.Add(new(page, name, new Uri(appUrl, route).AbsoluteUri, username, hidden, anonymous));
        Add("Dashboard / Events", "One visible current event and more than eight upcoming setups", "/Admin/Index");
        Add("Dashboard / Events", "Events directory — discarded excluded", "/Admin/Events/Index");
        Add("Dashboard / Events", "Create a private draft", "/Admin/Events?create=1");
        Add("Dashboard / Events", "Current and upcoming — more than25 rows, page2", "/Admin/Events?view=current&page=2");
        Add("Dashboard / Events", "Case-insensitive names including Æ/Ø/Å", "/Admin/Events?view=current&sort=identity&direction=asc");
        Add("Dashboard / Events", "Postponed start and failed signup opening", "/Admin/Events?view=current&attention=1");
        Add("Dashboard / Events", "Cancelled with Confirmed participants", "/Admin/Events?view=past&phase=cancelled");
        Add("Dashboard / Events", "No capacity and no dates", "/Admin/Events?view=current&search=No");
        Add("Dashboard / Events", "Imported history and shared first; current phase follows this profile", "/Admin");
        foreach (var item in scenarios.Events.Where(value => value.State != EventState.Discarded))
        {
            if (item.Hidden)
                Add("Hidden / Audit", item.Name + " — SuperAdmin hidden overview", $"/Admin/Events/Manage/{item.Id}?hidden=true", "ReviewOwner", hidden: true);
            else
                Add("Identity / phases / switcher", $"{item.Name} [{item.State}]", $"/Admin/Events/Identity/{item.Id}");
        }
        foreach (var slug in new[] { "ur-draft", "ur-signups-open", "ur-signups-closed" })
        {
            var item = scenarios.Events.Single(value => value.Slug == slug);
            Add("Schedule / Signup setup", item.Name + " — schedule", $"/Admin/Events/Schedule/{item.Id}");
            Add("Schedule / Signup setup", item.Name + " — signup settings", $"/Admin/Events/SignupSetup/{item.Id}");
        }
        foreach (var item in scenarios.Events.Where(value => !value.Hidden && value.State is EventState.Live or EventState.AwaitingFinalReview or EventState.Archived or EventState.Cancelled))
            Add("Schedule", $"{item.Name} [{item.State}] — end-only or read-only schedule", $"/Admin/Events/Schedule/{item.Id}");
        foreach (var item in scenarios.Events.Where(value => !value.Hidden && value.State != EventState.Discarded))
            Add("Signup setup", $"{item.Name} [{item.State}] — signup form", $"/Admin/Events/SignupSetup/{item.Id}?tab=form");
        var current = scenarios.Events.Single(value => value.Id == scenarios.CurrentEventId);
        var closed = scenarios.Events.Single(value => value.Slug == "ur-signups-closed");
        var archived = scenarios.Events.Single(value => value.Slug == "ur-archived");
        foreach (var item in new[] { current, closed, archived })
        {
            Add("Overview / Participants / Teams", item.Name + " — overview", $"/Admin/Events/Manage/{item.Id}");
            Add("Overview / Participants / Teams", item.Name + " — participants and former members", $"/Admin/Events/Participants/{item.Id}");
            Add("Overview / Participants / Teams", item.Name + " — finalized affiliated rosters",
                item.State == EventState.Archived ? $"/Events/{item.Slug}/Teams" : $"/Admin/Events/Draft/{item.Id}");
            Add("Public board / affiliated teams", item.Name + " — public teams", $"/Events/{item.Slug}/Teams", "ReviewParticipant");
            Add("Public board / affiliated teams", item.Name + " — public board", $"/Events/{item.Slug}/Board", "ReviewParticipant");
        }
        Add("Board / Review", "Published board with exceptional working-copy correction", $"/Admin/Events/Board/{current.Id}");
        Add("Board / Review", "Pending evidence queue", $"/Admin/Review/Index?eventId={current.Id}");
        Add("Board / Review", "Blocked approval — approve or reject the earlier upload first", $"/Admin/Review/Details/{scenarios.BlockedSubmissionId}");
        if (current.State == EventState.AwaitingFinalReview)
            Add("Final review / WOM", "Current final review — pending evidence and WOM end update", $"/Admin/Events/Finalize/{current.Id}");
        Add("Final review / WOM", "WOM end update Pending", $"/Admin/Events/WiseOldMan/{current.Id}");
        Add("Final review / WOM", "WOM end update Rejected — archived history", $"/Admin/Events/WiseOldMan/{archived.Id}");
        var unavailable = scenarios.Events.Single(value => value.Slug == "ur-wom-unavailable");
        Add("Final review / WOM", "WOM end could-not-update — archived history", $"/Admin/Events/WiseOldMan/{unavailable.Id}");
        Add("Accounts / Catalogue / Audit", "Every global role and disabled account", "/Admin/Accounts/Index", "ReviewOwner");
        // T1 Accounts binding scenarios (lane T).
        Guid AccountId(string username) => scenarios.Accounts.Single(value => value.Username == username).Id;
        Add("Accounts", "Directory as the Super Admin — Transfer ownership in the header", "/Admin/Accounts", "ReviewOwner");
        Add("Accounts", "Your own drawer as the Super Admin — Ownership row", $"/Admin/Accounts?account={AccountId("ReviewOwner")}", "ReviewOwner");
        Add("Accounts", "Admin account drawer as the Super Admin — Revoke Admin", $"/Admin/Accounts?account={AccountId("ReviewAdmin")}", "ReviewOwner");
        Add("Accounts", "Plain Admin, Admin role filter — Admin accounts explained, not offered", "/Admin/Accounts?role=admin", "ReviewAdmin");
        Add("Accounts", "Plain Admin — the Super Admin account is protected", $"/Admin/Accounts?account={AccountId("ReviewOwner")}", "ReviewAdmin");
        Add("Accounts", "Disabled account — notice, reason and Restore", $"/Admin/Accounts?account={AccountId("ReviewDisabled")}", "ReviewAdmin");
        Add("Accounts", "Captain — events, team roles, reset link and Disable", $"/Admin/Accounts?account={AccountId("ReviewCaptain")}", "ReviewAdmin");
        Add("Accounts", "Former member — ended team role", $"/Admin/Accounts?account={AccountId("ReviewFormer")}", "ReviewAdmin");
        Add("Accounts", "Search ignores case", "/Admin/Accounts?q=REVIEWSECOND", "ReviewAdmin");
        Add("Accounts", "No accounts match / Clear search and filter", "/Admin/Accounts?q=nobody-here&role=superadmin", "ReviewAdmin");
        Add("Accounts", "Link to an account that isn’t available", "/Admin/Accounts?account=00000000-0000-0000-0000-000000000001", "ReviewAdmin");
        Add("Accounts / Catalogue / Audit", "Reviewed local catalogue", "/Admin/Catalogue/Index");
        // U5 Participants binding scenarios: the open-signup event is full (capacity 4) with a waiting list and one withdrawn participant.
        if (scenarios.Participants is { } people)
        {
            var root = $"/Admin/Events/Participants/{people.EventId}";
            Add("Participants", "Full event — 4 of 4 confirmed, waiting list, Paid/Unpaid, search, sort", root, "ReviewAdmin");
            Add("Participants", "Waiting tab — Confirm and add a place, Move to waiting list", root + "?tab=waiting", "ReviewAdmin");
            Add("Participants", "Withdrawn tab — Restore, Restore and add a place", root + "?tab=withdrawn", "ReviewAdmin");
            Add("Participants", "Search by Discord name, account or RSN", root + "?q=ur", "ReviewAdmin");
            Add("Participants", "No matches / Clear search and filters", root + "?q=nobody-here&pay=paid", "ReviewAdmin");
            if (people.ConfirmedId is { } confirmedId) Add("Participants", "Participant drawer — accounts, captain answers, private note, one Save", root + $"?participant={confirmedId}", "ReviewAdmin");
            if (people.WaitingId is { } waitingId) Add("Participants", "Waiting participant drawer — Move/Confirm and Withdraw", root + $"?participant={waitingId}", "ReviewAdmin");
            if (people.WithdrawnId is { } withdrawnId) Add("Participants", "Withdrawn participant drawer — read-only, Restore to edit", root + $"?participant={withdrawnId}", "ReviewAdmin");
            Add("Participants", "Add participant — search ReviewWebsite, full event: Confirm and add a place or Waiting list", root + "?add=1", "ReviewAdmin");
            Add("Participants", "Link to a participant that isn’t in this event", root + "?participant=00000000-0000-0000-0000-000000000001", "ReviewAdmin");
            Add("Participants", "Old participant page URL redirects to the drawer", people.ConfirmedId is { } oldId ? $"/Admin/Events/Participant/{people.EventId}/Participants/{oldId}" : root, "ReviewAdmin");
            foreach (var slug in new[] { "ur-cancelled", "ur-archived", "ur-current" })
            {
                var terminal = scenarios.Events.Single(value => value.Slug == slug);
                Add("Participants", terminal.Name + $" [{terminal.State}] — read-only roster; payment and private note only where allowed", $"/Admin/Events/Participants/{terminal.Id}", "ReviewAdmin");
            }
        }

        // T2 Catalogue binding scenarios (lane T).
        Add("Catalogue", "Directory — search, category and Active/Inactive tabs", "/Admin/Catalogue");
        Add("Catalogue", "Search by drop name — “matches …” under the activity", "/Admin/Catalogue?q=onyx");
        Add("Catalogue", "No activities match / Clear search and filters", "/Admin/Catalogue?q=nothing-here&cat=Minigame");
        Add("Catalogue", "Inactive tab", "/Admin/Catalogue?status=inactive");
        Add("Catalogue", "Add activity (Team size beside Kills per hour)", "/Admin/Catalogue?new=1");
        if (scenarios.CatalogueActivityId is { } activity && scenarios.CatalogueDropId is { } drop)
        {
            Add("Catalogue", "Activity drawer — Settings, Wise Old Man metric, drops, Add drop, Availability", $"/Admin/Catalogue?activity={activity}");
            Add("Catalogue", "Drop editor — live rate preview, How the rate is counted, Value and item mapping", $"/Admin/Catalogue?activity={activity}&drop={drop}");
            Add("Catalogue", "S10 — Deactivate drop… names draft boards; hidden event only counted (plain Admin)", $"/Admin/Catalogue?activity={activity}&drop={drop}");
            Add("Catalogue", "S10 — Deactivate… the activity as the Super Admin: hidden event named; roll group editable; Delete", $"/Admin/Catalogue?activity={activity}&drop={drop}", "ReviewOwner");
        }
        Add("Catalogue", "Link to an activity that isn’t available", "/Admin/Catalogue?activity=00000000-0000-0000-0000-000000000001");
        Add("Hidden / Audit", "Audit including hidden events", "/Admin/Audit/Index", "ReviewOwner");
        Add("Hidden / Audit", "Plain Admin retained audit — including hidden-event history", "/Admin/Audit/Index");
        // T1 Audit binding scenarios (lane T).
        var hiddenReview = scenarios.Events.First(value => value.Hidden);
        Add("Audit", "History, newest first — filters, chips and the entry drawer", "/Admin/Audit");
        Add("Audit", "Participants area (S11) — moved team membership keys", "/Admin/Audit?action=participant.");
        Add("Audit", "Signups area (S11) — automatic signup opening failures, Automated", "/Admin/Audit?action=signup.");
        Add("Audit", "Actor search ignores case and a leading @", "/Admin/Audit?actor=%40reviewowner");
        Add("Audit", "Hidden event history, marked Hidden", $"/Admin/Audit?event={hiddenReview.Id}", "ReviewOwner");
        Add("Audit", "Link with unrecognised filters — notice, the rest applies", "/Admin/Audit?type=spaceship&from=2027-13-40&actor=ReviewOwner");
        Add("Audit", "Entry link that isn’t available", "/Admin/Audit?entry=00000000-0000-0000-0000-000000000001");
        foreach (var account in scenarios.Accounts)
        {
            if (account.Disabled)
                Add("Accounts / sign-in", account.Username + " — attempt sign-in; disabled account is refused", "/Account/Login", account.Username, anonymous: true);
            else
                Add("Accounts / sign-in", account.Username + " — " + RoleDescription(account.Username), "/Account/MyEvents", account.Username);
        }
        Add("Captain / Co-captain / Participant", "Captain evidence workspace", $"/Submissions?eventId={current.Id}", "ReviewCaptain");
        Add("Captain / Co-captain / Participant", "Co-captain evidence workspace", $"/Submissions?eventId={current.Id}", "ReviewCoCaptain");
        Add("Captain / Co-captain / Participant", "Participant evidence ledger", $"/Submissions?eventId={current.Id}", "ReviewParticipant");
        Add("Captain / Co-captain / Participant", "Former member history", "/Account/MyEvents", "ReviewFormer");
        return links;
    }

    public static string Markdown(UiReviewScenarios scenarios, Uri appUrl)
    {
        var text = new StringBuilder($"# UI review — {scenarios.Profile}\n\nRebuilt at {scenarios.BuiltAt:O}. Synthetic local data only.\n\nApp: {appUrl.AbsoluteUri.TrimEnd('/')} · References: http://127.0.0.1:5320\n\nAll review accounts use the local-only password `{UiReviewScenarioSeeder.Password}`.\nThe disabled account is deliberately refused at sign-in. Sign out before changing accounts.\nHidden scenarios require ReviewOwner; they do not count as visible current events.\nDiscarded scenarios have no review link and are excluded from page lists.\n\n");
        foreach (var group in Links(scenarios, appUrl).GroupBy(value => value.Page))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"## {group.Key}").AppendLine();
            foreach (var link in group)
                text.AppendLine(CultureInfo.InvariantCulture, $"- [{link.Name}]({link.Url}) — sign in: **{link.Username}**{(link.Anonymous ? " (attempt is refused)" : string.Empty)}");
            text.AppendLine();
        }
        return text.ToString();
    }

    private static string RoleDescription(string username) => username switch
    {
        "ReviewOwner" => "SuperAdmin", "ReviewAdmin" => "plain Admin", "ReviewCaptain" or "ReviewSecondCaptain" => "captain",
        "ReviewCoCaptain" or "ReviewSecondCoCaptain" => "co-captain", "ReviewParticipant" or "ReviewSecondMember" => "participant", "ReviewFormer" => "former team member",
        _ => "plain website account"
    };
}

public sealed record UiReviewLink(string Page, string Name, string Url, string Username, bool Hidden, bool Anonymous);
