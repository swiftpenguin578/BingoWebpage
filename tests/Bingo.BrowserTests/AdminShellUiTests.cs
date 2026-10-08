namespace Bingo.BrowserTests;

public sealed class AdminShellUiTests
{
    [Fact]
    public void AdminPagesUseTheDedicatedShellAndRealNavigationDestinations()
    {
        // A10 (U10 part 2): the old _AdminLayout and its fallback are retired; every Admin page renders in the
        // redesigned shell, so the old-layout markup/CSS assertions are replaced by the same contract on the new shell.
        var root = FindRepositoryRoot();
        var adminRoot = Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin");
        var shared = Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared");
        var viewStart = File.ReadAllText(Path.Combine(adminRoot, "_ViewStart.cshtml"));
        var sidebar = File.ReadAllText(Path.Combine(shared, "_AdminDesignSidebar.cshtml"));
        var shellService = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Navigation", "SharedShellService.cs"));
        var questions = File.ReadAllText(Path.Combine(adminRoot, "Events", "Questions.cshtml"));

        Assert.Contains("Layout = \"_AdminDesignLayout\"", viewStart);
        Assert.DoesNotContain("_AdminLayout\"", viewStart);
        Assert.DoesNotContain("AdminDesignAttribute", viewStart);
        foreach (var name in new[] { "_AdminLayout.cshtml", "_AdminOverlayLayout.cshtml", "_AdminConfirmation.cshtml" })
            Assert.False(File.Exists(Path.Combine(shared, name)), name);
        foreach (var page in Directory.EnumerateFiles(adminRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(page);
            if (markup.Contains("@page", StringComparison.Ordinal)
                && !page.EndsWith(Path.Combine("Events", "Questions.cshtml"), StringComparison.Ordinal)
                && !page.EndsWith("PublicUi.cshtml", StringComparison.Ordinal))
                Assert.DoesNotContain("Layout =", markup);
        }

        foreach (var destination in new[] { "href=\"/Admin\"", "href=\"/Admin/Events/Index\"", "href=\"/Admin/Catalogue/Index\"", "href=\"/Admin/Accounts/Index\"", "href=\"/Admin/Audit/Index\"" })
            Assert.Contains(destination, sidebar);
        Assert.Contains("data-shell-event-context", sidebar);
        Assert.Contains("aria-current", sidebar);
        // U4 (brief 85 "Retired"; 42c §1.3): the header blocker count and #readiness anchor are gone.
        Assert.DoesNotContain("admin-header-blockers", sidebar);
        Assert.DoesNotContain("#readiness", sidebar);
        Assert.DoesNotContain("GetAdminEventBlockerCountAsync", shellService);
        Assert.DoesNotContain("initializeAdminMenu", File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "site.js")));

        Assert.DoesNotContain("_AdminOverlayLayout", questions);
        Assert.DoesNotContain("ViewData[\"Layout\"]", questions);
    }

    [Fact]
    public void RetiredAdminScriptsAreGone()
    {
        // U10 item 2: no page loads these any more (rg proof in review-notes/98e-u10-evidence.md).
        var js = Path.Combine(FindRepositoryRoot(), "src", "Bingo.Web", "wwwroot", "js");
        // U10 part 2 item 3: the old layout's confirmation, lifecycle and editor-guard scripts went with it.
        foreach (var name in new[] { "draft-scramble.js", "event-manage.js", "admin-collaboration.js", "event-create-datetime.js", "event-create-validation.js", "admin-confirmation.js", "admin-lifecycle-confirm.js", "admin-editor-guard.js" })
            Assert.False(File.Exists(Path.Combine(js, name)), name);
        Assert.DoesNotContain("initializeAdminAccountSearch", File.ReadAllText(Path.Combine(js, "site.js")));
    }

    [Fact]
    public void PublicPagesRetainThePublicLayout()
    {
        var root = FindRepositoryRoot();
        var publicViewStart = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "_ViewStart.cshtml"));
        var publicLayout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_Layout.cshtml"));
        var board = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "Board.cshtml"));
        var teamBoard = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "TeamBoard.cshtml"));
        var siteCss = BrowserTestFiles.ReadActiveStyles(root);

        Assert.Contains("Layout = \"_Layout\"", publicViewStart);
        Assert.Contains("landing-shell-header", publicLayout);
        Assert.Contains("landing-shell-nav", publicLayout);
        Assert.Contains("aria-label=\"@T[\"Public navigation\"]\"", publicLayout);
        Assert.Contains("dk-legacy-public-mark.png", publicLayout);
        Assert.Contains("landing-shell-brand-mark", publicLayout);
        Assert.Contains("landing-shell-links", publicLayout);
        Assert.Contains("landing-shell-link", publicLayout);
        Assert.Contains("asp-page=\"/Index\"", publicLayout);
        Assert.DoesNotContain("admin-layout", publicLayout);
        Assert.Contains("breadcrumb-bar", publicLayout);
        Assert.Contains("shell.Breadcrumbs", publicLayout);
        Assert.Contains("User.IsInRole(\"Admin\")", publicLayout);
        Assert.Contains("User.IsInRole(\"SuperAdmin\")", publicLayout);
        Assert.Contains("captainNavigation", publicLayout);
        Assert.Contains("data-notification-inbox", publicLayout);
        Assert.Contains("@T[\"Notifications\"]", publicLayout);
        Assert.Contains("public-ui-header-popover", publicLayout);
        Assert.Contains("data-public-ui-popover", publicLayout);
        Assert.Contains("@T[\"Settings\"]", publicLayout);
        Assert.Contains("@T[\"Language\"]", publicLayout);
        Assert.Contains("@T[\"Account settings\"]", publicLayout);
        Assert.Contains("@T[\"Sign out\"]", publicLayout);
        Assert.Contains("@T[\"Sign in\"]", publicLayout);
        Assert.Contains("<noscript>", publicLayout);
        Assert.Contains("currentPage", publicLayout);
        Assert.Contains("isPublicBoardsPage", publicLayout);
        Assert.Contains("@if (captainNavigation is not null)", publicLayout);
        Assert.Contains("href=\"@captainNavigation.Url\">@T[\"Captain\"]", publicLayout);
        Assert.Contains("class=\"public-ui-header-context-link @(isSubmissionsPage ? \"is-current\" : null)\"", publicLayout);
        Assert.Contains("isSubmissionsPage", publicLayout);
        Assert.Contains("StartsWith(\"/Events/\"", publicLayout);
        Assert.Contains("StartsWith(\"/Evidence\"", publicLayout);
        Assert.Contains("aria-current=\"@(isPublicBoardsPage ? \"page\" : null)\"", publicLayout);
        Assert.Contains("aria-current=\"@(isSubmissionsPage ? \"page\" : null)\"", publicLayout);
        Assert.DoesNotContain("navbar navbar-expand-sm navbar-toggleable-sm border-bottom", publicLayout);
        Assert.Contains("body.public-ui-page-canvas", siteCss);
        Assert.Contains(".landing-shell-header", siteCss);
        Assert.Contains("body.public-ui-page-canvas .landing-shell-link", siteCss);
        Assert.Contains(".landing-shell-menu-panel", siteCss);
        Assert.Contains(".landing-shell-header :is(a, summary, button):focus-visible", siteCss);
        Assert.False(File.Exists(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminLayout.cshtml"))); // U10 part 2 (A10)
        Assert.Contains("ViewData[\"BodyClass\"] = \"public-event-shell public-ui-pass1 public-board-page public-board-overview\"", board);
        Assert.DoesNotContain("public-board-page", teamBoard);
        Assert.Contains("public-ui-header-context-nav", publicLayout);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
