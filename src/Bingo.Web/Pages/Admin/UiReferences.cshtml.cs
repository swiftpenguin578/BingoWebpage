using Bingo.Application.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class UiReferencesModel(IWebHostEnvironment environment) : PageModel
{
    private static readonly Dictionary<string, string> ImageFiles = new(StringComparer.Ordinal)
    {
        ["pub-ref-01"] = "pub-ref-01-landing.png",
        ["pub-ref-02"] = "pub-ref-02-live-event-overview.png",
        ["pub-ref-03"] = "pub-ref-03-team-board.png",
        ["pub-ref-04"] = "pub-ref-04-leaderboard.png",
        ["pub-ref-05"] = "pub-ref-05-signup.png",
        ["pub-ref-06"] = "pub-ref-06-signup-confirmation.png",
        ["pub-ref-07"] = "pub-ref-07-account-settings.png",
        ["pub-ref-08"] = "pub-ref-08-change-password.png",
        ["pub-ref-09"] = "authentication-status-reference.png",
        ["pub-ref-10"] = "account-overview-reference.png",
        ["pub-ref-11"] = "notifications-reference.png",
        ["pub-ref-12"] = "guidance-editorial-reference.png",
        ["pub-ref-13"] = "public-signups-directory-reference.png",
        ["pub-ref-14-main"] = "recent-drops-reference.png",
        ["pub-ref-14-states"] = "recent-drops-states-reference.png",
        ["pub-ref-15"] = "tile-detail-evidence-reference.png",
        ["pub-ref-16"] = "public-teams-roster-reference.png",
        ["pub-ref-17"] = "captain-team-operations-reference.png"
    };

    public IReadOnlyList<UiReference> References { get; } =
    [
        new("PUB-REF-01", "Landing", "/", [new("pub-ref-01", "Canonical reference")]),
        new("PUB-REF-02", "Live event overview", "/Events/{slug}/Board", [new("pub-ref-02", "Canonical reference")]),
        new("PUB-REF-03", "Team-specific board", "/Events/{slug}/Board/{teamSlug}", [new("pub-ref-03", "Canonical reference")]),
        new("PUB-REF-04", "Event leaderboard", "/Events/{slug}/Board?view=leaderboards", [new("pub-ref-04", "Canonical reference")]),
        new("PUB-REF-05", "Event signup", "/Events/{slug}/Signup", [new("pub-ref-05", "Canonical reference")]),
        new("PUB-REF-06", "Signup confirmation", "/Events/{slug}/Confirmation", [new("pub-ref-06", "Canonical reference")]),
        new("PUB-REF-07", "Account settings", "/Account/Settings", [new("pub-ref-07", "Canonical reference")]),
        new("PUB-REF-08", "Change password", "/Account/ChangePassword", [new("pub-ref-08", "Canonical reference")]),
        new("PUB-REF-09", "Authentication and status", "Login, Onboarding, AccessDenied, StatusCode, Error", [new("pub-ref-09", "Reference family")]),
        new("PUB-REF-10", "Account overview", "/Account/MyAccounts, /Account/MyEvents", [new("pub-ref-10", "Reference family")]),
        new("PUB-REF-11", "Notifications", "/Notifications", [new("pub-ref-11", "Canonical reference")]),
        new("PUB-REF-12", "Guidance and editorial", "/HowTo, /Privacy", [new("pub-ref-12", "Reference family")]),
        new("PUB-REF-13", "Public signups directory", "/Events/{slug}/Signups", [new("pub-ref-13", "Canonical reference")]),
        new("PUB-REF-14", "Recent drops", "/Events/{slug}/Board?view=drops", [new("pub-ref-14-main", "Main state"), new("pub-ref-14-states", "States and responsive variants")]),
        new("PUB-REF-15", "Tile detail and evidence viewer", "/Events/{slug}/Board/{teamSlug}/Tiles/{tileId}", [new("pub-ref-15", "Canonical reference")]),
        new("PUB-REF-16", "Public teams and roster", "/Events/{slug}/Teams", [new("pub-ref-16", "Canonical reference")]),
        new("PUB-REF-17", "Captain team operations", "/Captain", [new("pub-ref-17", "Canonical reference")])
    ];

    public IActionResult OnGetImage(string? key)
    {
        if (key is null || !ImageFiles.TryGetValue(key, out var fileName)) return NotFound();

        var directory = Directory.Exists(Path.Combine(environment.ContentRootPath, "docs", "references", "public-ui"))
            ? Path.Combine(environment.ContentRootPath, "docs", "references", "public-ui")
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "docs", "references", "public-ui"));
        var path = Path.Combine(directory, fileName);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/png") : NotFound();
    }

    public sealed record UiReference(string Id, string Title, string AppliesTo, IReadOnlyList<UiReferenceVariant> Variants);
    public sealed record UiReferenceVariant(string Key, string Label);
}
