using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bingo.BrowserTests;

public sealed class AccountsUiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public AccountsUiTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task AccountsRoutesRequireAdminAccess()
    {
        foreach (var route in new[] { "/Admin/Accounts/Index", "/Admin/Accounts/Create", $"/Admin/Accounts/Manage/{Guid.NewGuid()}", "/Admin/Accounts/Transfer" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
        }
    }

    [Fact]
    public void EmergencyCredentialActionsUseRightAlignedSharedButtons()
    {
        var root = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Manage.cshtml"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));

        Assert.Contains(".admin-shell-body .admin-account-route-page.is-emergency-credential .admin-account-emergency-actions { display: flex; flex-wrap: wrap; gap: 0.45rem; align-items: center; justify-content: flex-end; }", styles);
        Assert.Contains("<div class=\"admin-account-emergency-actions\">", manage);
        Assert.Contains("class=\"admin-button-secondary\" data-account-emergency-action=\"true\" data-account-handler=\"GenerateEmergencyLink\"", manage);
        Assert.Contains("class=\"action-danger-outline\" data-account-emergency-action=\"true\" data-account-handler=\"DisableEmergency\"", manage);
        Assert.Contains("class=\"admin-button-secondary\" data-account-emergency-action=\"true\" data-account-handler=\"EnableEmergency\"", manage);
    }

    [Fact]
    public void AccountsMarkupKeepsTheTwoDatasetsAndDirectSafetyBoundaries()
    {
        var root = FindRepositoryRoot();
        var accounts = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Index.cshtml"));
        var model = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Index.cshtml.cs"));
        var create = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Create.cshtml"));
        var createModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Create.cshtml.cs"));
        var manage = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Manage.cshtml"));
        var transfer = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Transfer.cshtml"));
        var transferModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Transfer.cshtml.cs"));
        var manageModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Manage.cshtml.cs"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));
        var contract = File.ReadAllText(Path.Combine(root, "ADMIN_UI_CONTRACT.md"));
        var script = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        var manageDialogScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "account-manage-dialog.js"));

        Assert.Contains("admin-accounts-page", accounts);
        Assert.Equal(2, accounts.Split("data-admin-account-section=", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, accounts.Split("class=\"admin-events-directory-controls\"", StringSplitOptions.None).Length - 1);
        var accountToolbarForms = accounts.Split("<form class=\"admin-events-toolbar\" method=\"get\" asp-page=\"./Index\"", StringSplitOptions.None);
        Assert.Equal(3, accountToolbarForms.Length);
        Assert.Contains("<button class=\"visually-hidden\" type=\"submit\">Search accounts</button>", accountToolbarForms[1]);
        Assert.Contains("<button class=\"visually-hidden\" type=\"submit\">Search emergency credentials</button>", accountToolbarForms[2]);
        Assert.Contains("name=\"WebsiteSearch\"", accounts);
        Assert.Contains("name=\"WebsiteRole\"", accounts);
        Assert.Contains("Search accounts", accounts);
        Assert.Equal(1, accounts.Split("<button class=\"visually-hidden\" type=\"submit\">Search accounts</button>", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, accounts.Split("<button class=\"visually-hidden\" type=\"submit\">Search emergency credentials</button>", StringSplitOptions.None).Length - 1);
        Assert.Contains("Any role", accounts);
        Assert.Contains("Create emergency credential", accounts);
        Assert.Contains("Transfer ownership", accounts);
        Assert.Contains("Current event roles", accounts);
        Assert.Contains("Login username", accounts);
        Assert.Contains("Disabled at cutoff", accounts);
        Assert.Equal(2, accounts.Split("class=\"admin-accounts-section-heading\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, accounts.Split("class=\"admin-accounts-table-wrap\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(15, accounts.Split("class=\"admin-accounts-table-header-label\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("admin-accounts-website-table", accounts);
        Assert.Contains("admin-accounts-emergency-table", accounts);
        Assert.Contains("admin-accounts-col-event-roles", accounts);
        Assert.Contains("admin-accounts-col-last-login", accounts);
        Assert.Contains("title=\"@account.EventRoleSummary\"", accounts);
        Assert.Contains("title=\"@account.EventName\"", accounts);
        Assert.Contains("new WebsiteAccountRow(x.Id, x.PublicUsername!, x.GlobalRole!.Value, x.Active, x.DiscordUserId != null, x.DiscordDisplayName, x.LastLoginAt, \"\")", model);
        Assert.Contains("WebsiteAccountRow(Guid Id, string Username, GlobalRole Role, bool Active, bool DiscordLinked, string? DiscordDisplayName, DateTimeOffset? LastLoginAt, string EventRoleSummary)", model);
        Assert.Contains("@if (account.DiscordLinked && !string.IsNullOrWhiteSpace(account.DiscordDisplayName))", accounts);
        Assert.Contains("<small class=\"admin-account-discord-name\">@account.DiscordDisplayName</small>", accounts);
        Assert.Contains("<span class=\"admin-account-discord-status @(account.DiscordLinked ? \"is-linked\" : \"is-unlinked\")\">@(account.DiscordLinked ? \"Linked\" : \"Not linked\")</span>", accounts);
        Assert.Contains(".admin-accounts-table .admin-account-discord-name { display: block; margin-top: 0.15rem; color: var(--admin-muted); font-size: 0.625rem; font-weight: 400; line-height: 1.35; overflow-wrap: anywhere; }", styles);
        Assert.Contains(".admin-accounts-table .admin-account-discord-status.is-linked { color: var(--admin-text); }", styles);
        Assert.Contains(".admin-accounts-table .admin-account-discord-status.is-unlinked { color: var(--admin-status-orange); }", styles);
        Assert.Equal(2, accounts.Split("event-overview-row-action", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, accounts.Split("data-account-manage-trigger=\"true\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("data-account-create-trigger=\"true\"", accounts);
        Assert.Equal(1, accounts.Split("data-account-create-trigger=\"true\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("data-account-dialog-trigger", accounts);
        Assert.Contains("data-account-manage-trigger", manageDialogScript);
        Assert.Contains("data-account-create-trigger", manageDialogScript);
        Assert.Equal(2, accounts.Split("asp-page=\"Manage\" asp-route-id=", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, accounts.Split("account-manage-dialog.js", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("data-account-manage-trigger", create);
        Assert.DoesNotContain("data-account-manage-trigger", transfer);
        Assert.DoesNotContain(">Details<", accounts);
        Assert.DoesNotContain("WebsiteState", model);
        Assert.DoesNotContain("WebsiteDiscord", model);
        Assert.DoesNotContain("WebsiteEventId", model);
        Assert.DoesNotContain("EmergencyEventId", model);
        Assert.DoesNotContain("EmergencyTeamId", model);
        Assert.DoesNotContain("EmergencySetup", model);
        Assert.DoesNotContain("EmergencyState", model);
        Assert.DoesNotContain("EmergencyCutoff", model);
        Assert.DoesNotContain("PasswordHash", accounts);

        Assert.Contains("Input.EventId", create);
        Assert.Contains("Input.TeamId", create);
        var teamSelect = create.IndexOf("<select asp-for=\"Input.TeamId\"", StringComparison.Ordinal);
        var teamError = create.IndexOf("<span asp-validation-for=\"Input.TeamId\"", teamSelect, StringComparison.Ordinal);
        Assert.True(teamSelect >= 0 && teamError > teamSelect);
        Assert.Contains("Choose an event above to load its active teams.", create);
        Assert.Contains("admin-button-create", create);
        Assert.Contains("admin-button-secondary", create);
        Assert.DoesNotContain("Layout = \"_AdminOverlayLayout\"", create);
        Assert.Contains("Model.IsOverlay", create);
        Assert.Contains("data-account-dialog-kind=\"create\"", create);
        Assert.Contains("data-account-create-trigger", accounts);
        Assert.Contains("name=\"overlay\"", create);
        Assert.Contains("public bool IsOverlay => Overlay || string.Equals(Request.Query[\"overlay\"], \"1\"", createModel);
        Assert.Contains("ResolveSubmittedOverlay", createModel);
        Assert.Contains("admin-destructive-confirmation", manage);
        Assert.Contains("item.Role != GlobalRole.SuperAdmin", manage);
        Assert.Contains("GenerateResetLink", manage);
        Assert.Contains("Disable reason", manage);
        Assert.Contains("@page \"{id:guid}\"", manage);
        Assert.DoesNotContain("Layout = \"_AdminOverlayLayout\"", manage);
        Assert.Contains("admin-dialog-page", manage);
        Assert.Contains("data-account-dialog-page=\"true\"", manage);
        Assert.Contains("data-account-dialog-kind=\"manage\"", manage);
        Assert.Contains("data-account-dialog-overlay", manage);
        Assert.Contains("data-account-manage-page", manage);
        Assert.Contains("admin-route-dialog-close", manage);
        Assert.Contains("data-account-dialog-close", manage);
        Assert.Contains("data-account-manage-close", manage);
        Assert.Contains("name=\"overlay\"", manage);
        Assert.Contains("overlay = IsOverlay ? \"1\" : null", manageModel);
        Assert.Contains("ResolveSubmittedOverlay", manageModel);
        Assert.Contains("public bool IsOverlay => Overlay || string.Equals(Request.Query[\"overlay\"], \"1\"", manageModel);
        Assert.Contains("is-website-account", manage);
        Assert.Contains("is-emergency-credential", manage);
        Assert.Contains("admin-account-characters-panel", manage);
        Assert.Contains("admin-account-character-row", manage);
        Assert.Contains("ParticipationStateClass(role.ParticipationState)", manage);
        Assert.Contains("\"Confirmed\" => \"is-green\"", manage);
        Assert.Contains("\"WaitingList\" => \"is-orange\"", manage);
        Assert.Contains("\"Withdrawn\" => \"is-danger\"", manage);
        Assert.Contains("_ => \"is-muted\"", manage);
        Assert.Contains("account-manage-dialog.js", create);
        Assert.DoesNotContain("account-manage-dialog.js", transfer);
        Assert.Contains("<select asp-for=\"Input.DestinationUsername\"", transfer);
        Assert.Contains("GlobalRole != GlobalRole.SuperAdmin", transferModel);
        Assert.Contains("TransferOwnershipAsync", transferModel);
        Assert.Contains("former owner remains an Admin", transfer);
        Assert.Contains("grid-template-columns: 7rem minmax(0, 1fr)", styles);
        Assert.Contains(".admin-accounts-page .admin-events-directory-controls { grid-template-columns: minmax(14rem, 18rem) minmax(0, 1fr) auto; gap: 0.75rem; align-items: center; }", styles);
        Assert.Contains(".admin-accounts-page .admin-events-directory-controls > .admin-events-toolbar { display: contents; }", styles);
        Assert.Contains(".admin-accounts-page .admin-events-toolbar > .admin-search-field { grid-column: 1;", styles);
        Assert.Contains(".admin-accounts-page .admin-events-state-filter { grid-column: 2; justify-self: end; width: 11rem; }", styles);
        Assert.Contains(".admin-accounts-actions { grid-column: 3; }", styles);
        Assert.Contains(".admin-accounts-page .admin-events-directory-controls > .admin-events-toolbar { display: grid; grid-template-columns: 1fr; }", styles);
        var genericSearchStart = styles.IndexOf(".admin-shell-body .admin-search-field-input {", StringComparison.Ordinal);
        var accountsSearchStart = styles.IndexOf(".admin-shell-body .admin-accounts-page .admin-search-field-input.form-control {", StringComparison.Ordinal);
        Assert.True(accountsSearchStart > genericSearchStart);
        Assert.Contains("min-height: 2.25rem; border-color: transparent; border-radius: 999px; padding-right: 2.25rem;", styles[accountsSearchStart..]);
        var roleFilterStart = styles.IndexOf(".admin-shell-body .admin-filter-select {", StringComparison.Ordinal);
        var roleFilterEnd = styles.IndexOf('}', roleFilterStart);
        Assert.True(roleFilterStart >= 0 && roleFilterEnd > roleFilterStart);
        Assert.Contains("transition: none;", styles[roleFilterStart..roleFilterEnd]);
        Assert.Contains("@media (max-width: 1100px)", styles);
        Assert.Contains(".admin-shell-body .admin-accounts-page,", styles);
        Assert.Contains("padding: 0.65rem 1rem; color: var(--admin-text);", styles);
        Assert.Contains(".admin-accounts-emergency-table .admin-accounts-col-login-username { width: 16%; }", styles);
        Assert.Contains(".admin-accounts-emergency-table .admin-accounts-col-event { width: 16%; }", styles);
        Assert.Contains(".admin-accounts-emergency-table .admin-accounts-col-team { width: 14%; }", styles);
        Assert.Contains(".admin-accounts-emergency-table .admin-accounts-col-setup { width: 9%; }", styles);
        Assert.Contains(".admin-accounts-emergency-table .admin-accounts-col-state { width: 10%; }", styles);
        Assert.Contains(".admin-accounts-emergency-table .admin-accounts-col-cutoff { width: 12%; }", styles);
        Assert.Contains(".admin-accounts-emergency-table .admin-accounts-col-last-login { width: 8%; }", styles);
        Assert.Contains(".admin-accounts-emergency-table .admin-accounts-col-actions { width: 15%; }", styles);
        var accountHeaderStart = styles.IndexOf(".admin-accounts-table th {", StringComparison.Ordinal);
        var accountHeaderEnd = styles.IndexOf('}', accountHeaderStart);
        Assert.True(accountHeaderStart >= 0 && accountHeaderEnd > accountHeaderStart);
        var accountHeaderStyles = styles[accountHeaderStart..accountHeaderEnd];
        Assert.Contains("padding: 0.65rem 1rem", accountHeaderStyles);
        Assert.Contains("line-height: 1.25", accountHeaderStyles);
        Assert.Contains("vertical-align: top", accountHeaderStyles);
        Assert.Contains(".admin-accounts-table th > .admin-accounts-table-header-label { display: inline-flex; min-height: 1.5rem; align-items: center; line-height: 1.25; }", styles);
        var eventHeaderStart = styles.IndexOf(".admin-events-page .event-table th {", StringComparison.Ordinal);
        var eventHeaderEnd = styles.IndexOf('}', eventHeaderStart);
        Assert.True(eventHeaderStart >= 0 && eventHeaderEnd > eventHeaderStart);
        var eventHeaderStyles = styles[eventHeaderStart..eventHeaderEnd];
        Assert.Contains("padding: 0.65rem 1rem", eventHeaderStyles);
        Assert.Contains("font-size: 0.6875rem", eventHeaderStyles);
        Assert.Contains("font-weight: 500", eventHeaderStyles);
        Assert.Contains("line-height: 1.25", eventHeaderStyles);
        Assert.Contains("vertical-align: top", accountHeaderStyles);
        Assert.DoesNotContain(".admin-accounts-emergency-table .admin-accounts-col-actions { width: 7%; }", styles);
        Assert.Contains("Single-line `<th>` cells use `.6rem 1rem` padding with `.6875rem / 500 / 1.25` text; height is content-derived, not fixed.", contract);
        Assert.Contains("border-radius: 999px", styles);
        Assert.Contains("text-transform: none", styles);
        Assert.Contains("admin-account-ellipsis", styles);
        Assert.Contains("width: 15%;", styles);
        Assert.Contains(".admin-account-action-confirmation[open] > .event-confirmation-box", styles);
        Assert.Contains("initializeAdminAccountSearch", script);
        Assert.Contains("data-admin-search-clear", accounts);
        Assert.Contains("const target = new URL(form.action || window.location.href, window.location.href);", script);
        Assert.Contains("target.searchParams.delete(pageParameter);", script);
        Assert.Contains("window.fetch(target", script);
        Assert.Contains("window.setTimeout(() => apply", script);
        Assert.Contains("nextInput.setSelectionRange", script);
        Assert.Contains("bingo:account-directory-updated", script);
        Assert.Contains("if (window.location.hash === `#${input.id}`)", script);
        Assert.Contains("input.focus({ preventScroll: true });", script);
        Assert.Contains("window.history.replaceState(window.history.state, \"\", `${target.pathname}${target.search}`);", script);
        Assert.Contains("website-account-search", accounts);
        Assert.Contains("emergency-account-search", accounts);
        Assert.Contains("accountManageBound", manageDialogScript);
        Assert.Contains("bingo:account-directory-updated", manageDialogScript);
        Assert.Contains("window.innerWidth > 900", manageDialogScript);
        Assert.Contains("window.fetch", manageDialogScript);
        Assert.Contains("if (!response.ok)", manageDialogScript);
        Assert.Contains("window.location.assign(href)", manageDialogScript);
        Assert.Contains("history.pushState", manageDialogScript);
        Assert.Contains("history.back()", manageDialogScript);
        Assert.Contains("history.replaceState", manageDialogScript);
        Assert.Contains("window.addEventListener(\"popstate\"", manageDialogScript);
        Assert.Contains("event.key !== \"Escape\"", manageDialogScript);
        Assert.Contains("data-account-confirmation-cancel", manageDialogScript);
        Assert.Contains("data-account-dialog-page", manageDialogScript);
        Assert.Contains("data-account-emergency-action", manageDialogScript);
        Assert.Contains("admin-account-confirmation-dialog", manageDialogScript);
        Assert.Contains("data-account-confirmation-submit", manageDialogScript);
        Assert.DoesNotContain("createPostRequest", manageDialogScript);
        Assert.DoesNotContain("accountOptionBound", manageDialogScript);
        Assert.Contains("hydrateDirectory", manageDialogScript);
        Assert.Contains("bootstrapDirectOverlay", manageDialogScript);
        Assert.Contains("/Admin/Accounts/Index", manageDialogScript);
        Assert.Contains("showModal", manageDialogScript);
        Assert.Contains("admin-account-dialog-page", styles);
        Assert.Contains(".admin-shell-body .admin-account-route-page.is-website-account", styles);
        Assert.Contains(".admin-shell-body .admin-account-route-page.is-emergency-credential", styles);
        var emergencyTokenStart = styles.IndexOf(".admin-shell-body .admin-account-route-page.is-emergency-credential {", StringComparison.Ordinal);
        var emergencyTokenEnd = styles.IndexOf('}', emergencyTokenStart);
        Assert.True(emergencyTokenStart >= 0 && emergencyTokenEnd > emergencyTokenStart);
        var emergencyTokenStyles = styles[emergencyTokenStart..emergencyTokenEnd];
        Assert.Contains("--admin-text: #eaeae5;", emergencyTokenStyles);
        Assert.Contains("--admin-text-soft: #c2c2be;", emergencyTokenStyles);
        Assert.Contains("--admin-muted: #969692;", emergencyTokenStyles);
        Assert.Contains("--admin-divider: #2b2b2b;", emergencyTokenStyles);
        Assert.Contains("admin-account-emergency-actions", manage);
        Assert.Equal(3, manage.Split("data-account-emergency-action=\"true\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("data-account-handler=\"GenerateEmergencyLink\"", manage);
        Assert.Contains("data-account-handler=\"DisableEmergency\"", manage);
        Assert.Contains("data-account-handler=\"EnableEmergency\"", manage);
        Assert.Contains("class=\"admin-button-secondary\" data-account-emergency-action=\"true\"", manage);
        Assert.Contains("class=\"action-danger-outline\" data-account-emergency-action=\"true\"", manage);
        Assert.DoesNotContain("admin-account-emergency-confirmation", manage);
        Assert.DoesNotContain("admin-account-emergency-confirmation", manageDialogScript);
        Assert.DoesNotContain("admin-account-emergency-confirmation", styles);
        Assert.DoesNotContain("role=\"alertdialog\"", manage);
        Assert.DoesNotContain("Issue a one-time credential link.", manage);
        Assert.DoesNotContain("Stop this fallback login.", manage);
        Assert.DoesNotContain("Allow the configured fallback login.", manage);
        Assert.DoesNotContain("<summary class=\"admin-button-create\"><strong>Enable emergency credential</strong>", manage);
        Assert.Contains(".admin-account-emergency-actions { display: flex; flex-wrap: wrap; gap: 0.45rem;", styles);
        Assert.Contains(".admin-account-confirmation-dialog {", styles);
        Assert.Contains("background: var(--admin-surface-raised); border: 1px solid #2b2b2b;", styles);
        Assert.Contains(".admin-account-confirmation-heading h2 {", styles);
        Assert.Contains("color: #eaeae5; font-family: inherit; font-size: 0.875rem; font-weight: 600; line-height: 1.3", styles);
        Assert.Contains("color: #969692; font-size: 0.75rem; font-weight: 400; line-height: 1.45", styles);
        var characterRowStart = styles.IndexOf(".admin-shell-body .admin-account-route-page.is-website-account .admin-account-characters-panel .admin-account-character-row", StringComparison.Ordinal);
        var characterRowEnd = styles.IndexOf('}', characterRowStart);
        Assert.True(characterRowStart >= 0 && characterRowEnd > characterRowStart);
        var characterRowStyles = styles[characterRowStart..characterRowEnd];
        Assert.Contains("gap: 0.75rem", characterRowStyles);
        Assert.Contains("padding: 1rem", characterRowStyles);
        Assert.Contains("background: var(--admin-surface-raised)", characterRowStyles);
        Assert.Contains("border: 0", characterRowStyles);
        Assert.DoesNotContain("border-bottom", characterRowStyles);
        Assert.Contains("admin-status-pill @ParticipationStateClass(role.ParticipationState)", manage);
    }

    [Fact]
    public void AccountsPopupCorrectionsUseTheApprovedOverlayAndHistoryContracts()
    {
        var root = FindRepositoryRoot();
        var create = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Create.cshtml"));
        var createModel = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Create.cshtml.cs"));
        var manage = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Accounts", "Manage.cshtml"));
        var styles = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));
        var script = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "account-manage-dialog.js"));
        var adminLayout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminLayout.cshtml"));

        Assert.Contains("<button class=\"admin-button-secondary\" type=\"submit\">Load teams</button>", create);
        Assert.Contains(".admin-account-fields .admin-account-scope-note { display: grid; grid-column: 2; grid-row: 1; align-self: end;", styles);
        Assert.DoesNotContain("data-account-enhanced-validation", script);
        Assert.Contains("exception.Message", createModel);
        Assert.Contains("TempData[\"StatusMessage\"]", createModel);
        Assert.Contains("_TransientToast", adminLayout);

        Assert.Contains("class=\"admin-account-event-copy\"", manage);
        Assert.Contains("class=\"admin-account-event-support\"", manage);
        Assert.Contains("No team role recorded.", manage);
        Assert.DoesNotContain("<ul>@foreach (var teamRole in role.TeamRoles)", manage);
        Assert.Contains("class=\"admin-account-final-actions\"", manage);
        Assert.Contains("data-account-status-message", manage);
        Assert.Contains("data-account-final-action=\"true\" data-account-handler=\"GenerateResetLink\"", manage);
        Assert.Contains("data-account-final-action=\"true\" data-account-handler=\"Disable\"", manage);
        Assert.Contains("data-account-final-action=\"true\" data-account-handler=\"Restore\"", manage);
        Assert.Contains("data-account-confirmation-reason=\"true\" data-account-confirmation-reason-label=\"Disable reason\"", manage);
        Assert.Contains(".admin-account-final-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 0.45rem; width: fit-content;", styles);
        Assert.Contains(".admin-shell-body .admin-account-final-actions .action-danger-outline,", styles);
        Assert.Contains(".admin-shell-body .admin-account-emergency-actions .action-danger-outline,", styles);
        Assert.Contains("min-height: 2rem;", styles);
        Assert.Contains("padding: 0.375rem 0.75rem;", styles);
        Assert.Contains("font-size: 0.6875rem;", styles);
        Assert.Contains("font-weight: 600;", styles);
        Assert.Contains(".admin-account-event-history { display: grid; gap: 0.75rem;", styles);
        var historyRowStart = styles.IndexOf(".admin-account-event-history article {", StringComparison.Ordinal);
        var historyRowEnd = styles.IndexOf('}', historyRowStart);
        Assert.True(historyRowStart >= 0 && historyRowEnd > historyRowStart);
        var historyRowStyles = styles[historyRowStart..historyRowEnd];
        Assert.Contains("display: flex", historyRowStyles);
        Assert.Contains("flex-wrap: wrap", historyRowStyles);
        Assert.Contains("justify-content: space-between", historyRowStyles);
        Assert.Contains("align-items: center", historyRowStyles);
        Assert.Contains("gap: 0.75rem", historyRowStyles);
        Assert.Contains("padding: 1rem", historyRowStyles);
        Assert.Contains("background: var(--admin-surface-raised)", historyRowStyles);
        Assert.Contains("border: 0", historyRowStyles);
        Assert.Contains("border-radius: 0.75rem", historyRowStyles);
        Assert.DoesNotContain(".admin-account-event-history ul", styles);
        Assert.Contains("--admin-focus-accent-strong: var(--admin-navigation-accent);", styles);
        var confirmationStart = styles.IndexOf(".admin-shell-body .admin-account-confirmation-dialog {", StringComparison.Ordinal);
        var confirmationEnd = styles.IndexOf('}', confirmationStart);
        Assert.True(confirmationStart >= 0 && confirmationEnd > confirmationStart);
        var confirmationStyles = styles[confirmationStart..confirmationEnd];
        Assert.Contains("--admin-border: var(--admin-divider);", confirmationStyles);
        Assert.Contains("--admin-border-strong: var(--admin-divider);", confirmationStyles);
        Assert.DoesNotContain("--admin-border: #1b2533", confirmationStyles);
        Assert.Contains(".admin-shell-body .admin-account-dialog-page :is(.admin-button-secondary, .action-danger-outline, .admin-button-create, .admin-route-dialog-close):focus-visible", styles);
        Assert.Contains(".admin-shell-body .admin-account-confirmation-dialog :is(.admin-button-secondary, .action-danger-outline, .admin-button-create, .admin-route-dialog-close):focus-visible", styles);
        Assert.Contains("outline: 2px solid var(--admin-focus-accent-strong);", styles);
        Assert.Contains("outline-offset: 2px;", styles);
        Assert.Contains("const statusMessage = (html) =>", script);
        Assert.Contains("window.showBingoToast?.(message, \"success\")", script);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
