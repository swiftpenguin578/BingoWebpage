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
        Add("Dashboard / Events", "Create a private draft", "/Admin/Events/Create");
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
            Add("Schedule / Signup setup", item.Name + " — questions", $"/Admin/Events/Questions/{item.Id}");
        }
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
        Add("Accounts / Catalogue / Audit", "Reviewed local catalogue", "/Admin/Catalogue/Index");
        Add("Hidden / Audit", "Audit including hidden events", "/Admin/Audit/Index", "ReviewOwner");
        Add("Hidden / Audit", "Plain Admin retained audit — including hidden-event history", "/Admin/Audit/Index");
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
