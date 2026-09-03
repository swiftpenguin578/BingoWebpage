namespace Bingo.BrowserTests;

public sealed class AdminShellUiTests
{
    [Fact]
    public void AdminPagesUseTheDedicatedShellAndRealNavigationDestinations()
    {
        var root = FindRepositoryRoot();
        var adminRoot = Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin");
        var viewStart = File.ReadAllText(Path.Combine(adminRoot, "_ViewStart.cshtml"));
        var layout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminLayout.cshtml"));
        var shellService = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Navigation", "SharedShellService.cs"));
        var questions = File.ReadAllText(Path.Combine(adminRoot, "Events", "Questions.cshtml"));
        var overlayLayout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminOverlayLayout.cshtml"));
        var styles = BrowserTestFiles.ReadActiveStyles(root);

        Assert.Contains("Layout = \"_AdminLayout\"", viewStart);
        foreach (var page in Directory.EnumerateFiles(adminRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(page);
            if (markup.Contains("@page", StringComparison.Ordinal)
                && !page.EndsWith(Path.Combine("Events", "Questions.cshtml"), StringComparison.Ordinal)
                && !page.EndsWith("PublicUi.cshtml", StringComparison.Ordinal))
                Assert.DoesNotContain("Layout =", markup);
        }

        foreach (var destination in new[] { "/Admin", "/Admin/Events/Index", "/Admin/Catalogue/Index", "/Admin/Accounts/Index", "/Admin/Audit/Index" })
            Assert.Contains(destination, layout);
        Assert.Contains("href=\"/Admin\" aria-current=\"@(isCurrent(\"/Admin\") ? \"page\" : null)\"><svg class=\"admin-nav-icon\"", layout);
        Assert.Contains("data-admin-menu-toggle", layout);
        Assert.Contains("data-admin-menu-scrim", layout);
        Assert.Contains("aria-current", layout);
        Assert.Contains("data-notification-inbox", layout);
        Assert.Contains("Exit Admin", layout);
        Assert.DoesNotContain("class=\"admin-nav-label\"", layout);
        Assert.Contains("data-admin-event-navigation", layout);
        Assert.Contains("admin-header-blockers", layout);
        Assert.Contains("eventContext?.BlockerCount ?? 0", layout);
        Assert.Contains("blockerHref", layout);
        Assert.Contains("GetAdminEventBlockerCountAsync", shellService);
        Assert.Contains("IEventReadinessEvaluator", shellService);
        Assert.Contains("admin-event-item", layout);
        Assert.Contains("data-admin-event-section=\"participants\"", layout);
        Assert.Contains("data-admin-event-section=\"schedule\"", layout);
        Assert.Contains("@T[\"Identity\"]", layout);
        Assert.Contains("href=\"/Admin/Events/Participants/@eventContext.Id\"", layout);
        Assert.Contains("href=\"/Admin/Events/Schedule/@eventContext.Id\"", layout);
        Assert.Contains("@T[\"Review\"]", layout);
        Assert.Contains("data-admin-event-section=\"evidence\"", layout);
        Assert.Contains("/Admin/Review?eventId=", layout);
        Assert.Contains("admin-brand-mark", layout);
        Assert.Contains("DK Legacy", layout);
        Assert.Contains("~/images/branding/dk-legacy-admin-mark.png", layout);
        Assert.Contains("admin-account-avatar", layout);
        Assert.Contains("admin-selected-event-empty", layout);
        Assert.Contains("Select an event", layout);
        Assert.Contains("admin-event-selector-chevron", layout);
        Assert.Contains("m6 9 6 6 6-6", layout);
        Assert.Contains("summary::marker", styles);
        Assert.DoesNotContain("details[open] .admin-event-selector-chevron", styles);
        Assert.DoesNotContain("@eventContext.Name (@eventContext.StatusLabel)", layout);
        Assert.DoesNotContain("admin-selected-event-state-slot", layout);
        Assert.DoesNotContain("admin-state admin-state-@eventContext.State", layout);
        Assert.Contains("flex: 0 0 25px", styles);
        Assert.Contains("width: 25px; height: 25px", styles);
        Assert.Contains("padding: 0.125rem 0.5rem", styles);
        Assert.Contains("margin-left: auto; flex: 0 0 auto", styles);
        Assert.Contains("min-height: 2.25rem", styles);
        Assert.Contains("border-radius: 999px", styles);
        Assert.Contains("data-admin-event-selector", layout);
        Assert.Contains("admin-event-option", layout);
        Assert.Contains("asp-page=\"/Admin/Events/Manage\"", layout);
        Assert.Contains("asp-route-id=\"@option.Id\"", layout);
        Assert.Contains("role=\"option\"", layout);
        Assert.Contains("admin-event-selector", File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "site.js")));
        var siteJs = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        Assert.Contains("previouslyFocused", siteJs);
        Assert.Contains("event.key === \"Tab\"", siteJs);
        Assert.Contains("event.shiftKey && (!sidebar.contains(document.activeElement)", siteJs);
        Assert.Contains("!event.shiftKey && (!sidebar.contains(document.activeElement)", siteJs);
        Assert.Contains("setOpen(false, true)", siteJs);
        Assert.Contains("window.innerWidth > 900", siteJs);
        Assert.Contains("stroke=\"currentColor\"", layout);
        Assert.DoesNotContain(">⌂<", layout);
        Assert.Contains("--admin-page: #181818", styles);
        Assert.Contains("--admin-surface: #1d1d1d", styles);
        Assert.Contains("border-right: 1px solid var(--admin-divider)", styles);
        Assert.DoesNotContain(".admin-event-nav::before", styles);
        Assert.DoesNotContain("--admin-event-spine-center:", styles);
        Assert.DoesNotContain("--admin-event-branch-length:", styles);
        Assert.DoesNotContain("--admin-event-connector-gap:", styles);
        Assert.Contains("--admin-event-link-left", styles);
        Assert.DoesNotContain("var(--admin-event-branch-end)", styles);
        Assert.DoesNotContain(".admin-sidebar-footer::before { display: block", styles);
        Assert.DoesNotContain("breadcrumb-bar", layout);
        Assert.DoesNotContain("shell.Breadcrumbs", layout);
        Assert.DoesNotContain("BreadcrumbAction", layout);

        Assert.Contains("Layout = \"_AdminOverlayLayout\"", questions);
        Assert.Contains("if (!Model.IsOverlay)", questions);
        Assert.DoesNotContain("ViewData[\"Layout\"]", questions);
        Assert.DoesNotContain("admin-header", overlayLayout);
        Assert.DoesNotContain("admin-sidebar", overlayLayout);
        Assert.Contains("@RenderBody()", overlayLayout);
    }

    [Fact]
    public void PublicPagesRetainThePublicLayout()
    {
        var root = FindRepositoryRoot();
        var publicViewStart = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "_ViewStart.cshtml"));
        var publicLayout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_Layout.cshtml"));
        var board = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "Board.cshtml"));
        var teamBoard = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "TeamBoard.cshtml"));
        var adminLayout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminLayout.cshtml"));
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
        Assert.Contains("isCaptainPage", publicLayout);
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
        Assert.Contains("admin-shell", adminLayout);
        Assert.DoesNotContain("app-nav", adminLayout);
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
