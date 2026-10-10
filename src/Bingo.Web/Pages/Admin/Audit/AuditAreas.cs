using Bingo.Domain.Auditing;

namespace Bingo.Web.Pages.Admin.Audit;

/// <summary>
/// S11 (Q5 (a), 6 October 2026): every audit key belongs to exactly one area. Areas are prefix
/// sets plus explicit key lists, not pure prefixes. The URL token is the area's main prefix.
/// </summary>
public sealed record AuditArea(string Token, string Label, IReadOnlyList<string> Prefixes, IReadOnlyList<string> Include, IReadOnlyList<string> Exclude)
{
    public bool Contains(string key) =>
        (Prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)) || Include.Contains(key, StringComparer.Ordinal))
        && !Exclude.Contains(key, StringComparer.Ordinal);

    public IQueryable<AuditEntry> Apply(IQueryable<AuditEntry> query)
    {
        // At most three prefixes per area; unused slots repeat the first so the SQL stays a fixed shape.
        var first = Prefixes[0];
        var second = Prefixes.Count > 1 ? Prefixes[1] : first;
        var third = Prefixes.Count > 2 ? Prefixes[2] : first;
        var include = Include.ToArray();
        var exclude = Exclude.ToArray();
        return query.Where(entry => (entry.Action.StartsWith(first) || entry.Action.StartsWith(second) || entry.Action.StartsWith(third) || include.Contains(entry.Action))
            && !exclude.Contains(entry.Action));
    }
}

public static class AuditAreas
{
    // Moved from Events to Signups (Q5).
    public static readonly IReadOnlyList<string> SignupEventKeys =
    [
        "event.signup_opened", "event.signup_opened_automatically", "event.signup_opening_failed", "event.signup_closed",
        "event.signup_closed_automatically", "event.signup_reopened", "event.signup_code_changed",
        "event.signup_administration_updated", "event.capacity_increased"
    ];

    // Moved from Teams to Participants (Q5).
    public static readonly IReadOnlyList<string> ParticipantTeamKeys = ["team.member_added", "team.member_moved", "team.member_removed", "team.membership_role_changed"];

    public static readonly IReadOnlyList<AuditArea> All =
    [
        // Brief 147 A7: "logout" has no area prefix and belongs to Accounts.
        new("account.", "Accounts", ["account."], ["logout"], []),
        // evidence_code.* and historical_import.* are event-level records with no area of their own.
        new("event.", "Events", ["event.", "evidence_code.", "historical_import."], [], SignupEventKeys),
        new("signup.", "Signups", ["signup.", "signup_question.", "signup_cocaptain."], SignupEventKeys, []),
        new("participant.", "Participants", ["participant."], ["roster.finalized_added", "roster.finalized_removed", "roster.finalized_added.wom_sync", "roster.finalized_removed.wom_sync", .. ParticipantTeamKeys], []),
        new("team.", "Teams", ["team."], [], ParticipantTeamKeys),
        new("draft.", "Draft", ["draft."], [], []),
        new("board.", "Board", ["board."], [], []),
        new("submission.", "Evidence", ["submission."], [], []),
        new("catalogue.", "Catalogue", ["catalogue."], [], [])
    ];

    public static AuditArea? Find(string token) => All.FirstOrDefault(area => area.Token == token);

    public static AuditArea? For(string key) => All.FirstOrDefault(area => area.Contains(key));
}
