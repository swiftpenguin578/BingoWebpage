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
        var styles = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));

        Assert.Contains("Layout = \"_AdminLayout\"", viewStart);
        foreach (var page in Directory.EnumerateFiles(adminRoot, "*.cshtml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(page);
            if (markup.Contains("@page", StringComparison.Ordinal) && !page.EndsWith(Path.Combine("Events", "Questions.cshtml"), StringComparison.Ordinal))
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
        Assert.Contains("DKL Bingo", layout);
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
        Assert.Contains("flex: 0 0 auto", styles);
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

        Assert.Contains("Layout = \"_Layout\"", publicViewStart);
        Assert.Contains("app-nav", publicLayout);
        Assert.DoesNotContain("admin-layout", publicLayout);
        Assert.Contains("breadcrumb-bar", publicLayout);
        Assert.Contains("shell.Breadcrumbs", publicLayout);
    }

    [Fact]
    public void AdminPass1BDashboardAndEventsDirectoryUseAuthoritativeRoutesAndContent()
    {
        var root = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Index.cshtml"));
        var dashboardModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Index.cshtml.cs"));
        var events = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Index.cshtml"));
        var eventsModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Index.cshtml.cs"));
        var siteJs = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        var siteCss = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));
        var roadmap = File.ReadAllText(Path.Combine(root, "UI_OVERHAUL_ROADMAP.md"));

        Assert.Contains("admin-dashboard-page", dashboard);
        Assert.Contains("asp-page=\"/Admin/Events/Create\"", dashboard);
        Assert.Contains("asp-page=\"/Admin/Events/Index\"", dashboard);
        Assert.Contains("asp-page=\"/Admin/Review/Index\"", dashboard);
        Assert.Contains("asp-page=\"/Admin/Audit/Index\"", dashboard);
        Assert.Contains("asp-page=\"/Admin/Events/Manage\"", dashboard);
        Assert.Contains("UpcomingMilestones", dashboardModel);
        Assert.Contains("PendingEvidence", dashboardModel);
        Assert.Contains("LifecycleReadiness", dashboardModel);
        Assert.Contains("WiseOldManSynchronizations", dashboardModel);
        Assert.Contains("PendingReviewCount", dashboardModel);
        Assert.Contains("AttentionEvents", dashboardModel);
        Assert.Contains("RecentAudits", dashboardModel);
        Assert.Contains("competitionSynchronization.GetAsync", dashboardModel);
        Assert.Contains("ReadinessSummary", dashboardModel);
        Assert.Contains("Upcoming milestones", dashboard);
        Assert.Contains("Pending evidence", dashboard);
        Assert.Contains("Lifecycle readiness", dashboard);
        Assert.Contains("Full audit", dashboard);
        Assert.DoesNotContain("Total Events", dashboard);

        Assert.Contains("admin-events-page", events);
        Assert.Contains("<form class=\"admin-events-toolbar\" method=\"get\"", events);
        Assert.Contains("type=\"search\"", events);
        Assert.Contains("class=\"admin-search-field admin-events-search\"", events);
        Assert.Contains("class=\"admin-search-field-input form-control\"", events);
        Assert.Contains("name=\"search\"", events);
        Assert.Contains("name=\"filter\"", events);
        var toolbar = events[events.IndexOf("data-admin-event-directory-search", StringComparison.Ordinal)..events.IndexOf("</form>", StringComparison.Ordinal)];
        Assert.Equal(1, toolbar.Split("circle cx=\"11\" cy=\"11\" r=\"8\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("admin-search-submit", toolbar);
        Assert.Contains("data-admin-event-search-input", events);
        Assert.Contains("data-admin-event-state-filter", events);
        Assert.Contains("class=\"admin-filter-select form-select\"", events);
        Assert.Contains("Model.StateOptions", events);
        Assert.Contains("Model.Events", events);
        Assert.Contains("asp-page=\"Create\"", events);
        Assert.Contains("asp-page=\"Manage\"", events);
        Assert.Contains("AdminEventStatePresentation.For(item.State, T)", events);
        Assert.Contains("admin-status-pill @statePill.Modifier", events);
        Assert.Contains("@T[\"Actions\"]", events);
        Assert.Contains("@T[\"Workspace\"]", events);
        Assert.Contains("@item.Slug", events);
        Assert.Contains("class=\"admin-events-table-wrap\"", events);
        Assert.DoesNotContain("table-wrap panel admin-events-table-wrap", events);
        Assert.DoesNotContain("event-list-status", events);
        Assert.Contains("event-row-action", events);
        Assert.Contains("event-state-cell", events);
        Assert.Contains(".admin-shell-body .admin-search-field-input", siteCss);
        Assert.Contains(".admin-shell-body .admin-search-field-input::-webkit-search-cancel-button", siteCss);
        Assert.Contains(".admin-shell-body .admin-filter-select", siteCss);
        Assert.DoesNotContain(".admin-shell-body .admin-filter-select { vertical-align", siteCss);
        Assert.Contains("box-sizing: border-box", siteCss);
        Assert.Contains("padding: 0.4375rem 2rem 0.4375rem 0.6rem", siteCss);
        Assert.Contains("line-height: 1.25rem", siteCss);
        Assert.Contains("height: 2.25rem", siteCss);
        Assert.Contains("appearance: none", siteCss);
        Assert.Contains(".admin-status-pill { display: inline-flex; width: fit-content;", siteCss);
        Assert.Contains("white-space: nowrap; overflow-wrap: normal", siteCss);
        Assert.Contains("align-content: start; align-items: flex-start; flex-direction: column; gap: 0.75rem; min-height: 0", siteCss);
        Assert.Contains("adjacent or stacked dividers are prohibited globally", roadmap);
        Assert.Contains("`/Admin/Events/Create` is the canonical spacing source", roadmap);
        Assert.Contains("tight label-to-control-to-help/validation uses `.admin-shell-body .admin-field, .admin-shell-body .event-create-field { gap: 0.25rem; }`", roadmap);
        Assert.Contains("related controls in one feature use `.admin-shell-body .event-create-fields { gap: 0.75rem; }`", roadmap);
        Assert.DoesNotContain(".admin-events-page .admin-events-toolbar + .admin-events-table-wrap", siteCss);
        Assert.Contains("Search", eventsModel);
        Assert.Contains("MatchesSearch", eventsModel);
        Assert.Contains("StateOption", eventsModel);
        Assert.Contains("data-admin-event-table", events);
        Assert.Contains("data-admin-event-row", events);
        Assert.Contains("data-event-name", events);
        Assert.Contains("data-event-slug", events);
        Assert.Contains("data-event-state", events);
        var directoryJs = siteJs[siteJs.IndexOf("function initializeAdminEventDirectorySearch", StringComparison.Ordinal)..siteJs.IndexOf("document.addEventListener(\"bingo:content-updated\"", StringComparison.Ordinal)];
        Assert.Contains("input.addEventListener(\"input\", apply)", directoryJs);
        Assert.Contains("state.addEventListener(\"change\", apply)", directoryJs);
        Assert.Contains("matchesSearch && matchesState", directoryJs);
        Assert.Contains("event.preventDefault()", directoryJs);
        Assert.Contains("row.hidden", directoryJs);
        Assert.Contains("form?.closest(\".admin-events-page\")", directoryJs);
        Assert.Contains("page?.querySelector(\"[data-admin-event-table]\")", directoryJs);
        Assert.DoesNotContain("form?.querySelector(\"[data-admin-event-table]\")", directoryJs);
        Assert.DoesNotContain("requestSubmit", directoryJs);
        Assert.DoesNotContain("350", directoryJs);
        Assert.Equal(1, siteCss.Split("/* Admin Events directory:", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void AdminLifecycleStatePillHasOneCompleteMappingAndSharedGeometry()
    {
        var root = FindRepositoryRoot();
        var presentation = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "UI", "AdminEventStatePresentation.cs"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));

        foreach (var (state, label, modifier) in new[]
        {
            ("Draft", "Setup", "is-yellow"),
            ("SignupOpen", "Signups open", "is-cyan"),
            ("SignupClosed", "Signups closed", "is-orange"),
            ("Live", "Live", "is-green"),
            ("AwaitingFinalReview", "Final review", "is-purple"),
            ("Finalized", "Finished", "is-blue"),
            ("Archived", "Archived", "is-muted"),
            ("Cancelled", "Cancelled", "is-danger"),
            ("Discarded", "Discarded", "is-pink")
        })
            Assert.Contains($"EventState.{state} => new(text[\"{label}\"], \"{modifier}\")", presentation);

        Assert.Contains(".admin-status-pill { display: inline-flex; width: fit-content; min-height: 1.6rem;", styles);
        Assert.Contains("padding: 0.25rem 0.65rem", styles);
        Assert.Contains("font-weight: 400", styles);
        Assert.Contains("border: 0; border-radius: 999px", styles);
        Assert.Contains(".admin-status-pill.is-danger", styles);
        Assert.DoesNotContain(".event-overview-status-pill", styles);
        Assert.DoesNotContain(".admin-state", styles);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
