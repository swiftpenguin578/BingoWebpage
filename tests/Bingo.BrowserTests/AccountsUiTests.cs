using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bingo.BrowserTests;

[Collection(BrowserTestGroup.Name)]
public sealed class AccountsUiTests
{
    private readonly HttpClient client;

    public AccountsUiTests(BrowserTestApplicationFactory factory) => client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

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
    public void AccountsMarkupKeepsWebsiteAdministrationAndRetiresEmergencyControls()
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
        var styles = BrowserTestFiles.ReadActiveStyles(root);
        var script = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        var manageDialogScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "account-manage-dialog.js"));

        Assert.Contains("admin-accounts-page", accounts);
        Assert.Equal(1, accounts.Split("data-admin-account-section=", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, accounts.Split("class=\"admin-events-directory-controls\"", StringSplitOptions.None).Length - 1);
        var accountToolbarForms = accounts.Split("<form class=\"admin-events-toolbar\" method=\"get\" asp-page=\"./Index\"", StringSplitOptions.None);
        Assert.Equal(2, accountToolbarForms.Length);
        Assert.Contains("<button class=\"visually-hidden\" type=\"submit\">@T[\"Search accounts\"]</button>", accountToolbarForms[1]);
        Assert.Contains("name=\"WebsiteSearch\"", accounts);
        Assert.Contains("name=\"WebsiteRole\"", accounts);
        Assert.Contains("Search accounts", accounts);
        Assert.Equal(1, accounts.Split("<button class=\"visually-hidden\" type=\"submit\">@T[\"Search accounts\"]</button>", StringSplitOptions.None).Length - 1);
        Assert.Contains("Any role", accounts);
        Assert.Contains("Transfer ownership", accounts);
        Assert.Contains("Current event roles", accounts);
        Assert.Equal(1, accounts.Split("class=\"admin-accounts-section-heading\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, accounts.Split("class=\"admin-accounts-table-wrap\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(7, accounts.Split("class=\"admin-accounts-table-header-label\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("admin-accounts-website-table", accounts);
        Assert.Contains("admin-accounts-col-event-roles", accounts);
        Assert.Contains("admin-accounts-col-last-login", accounts);
        Assert.Contains("title=\"@account.EventRoleSummary\"", accounts);
        Assert.Contains("new WebsiteAccountRow(x.Id, x.PublicUsername!, x.GlobalRole!.Value, x.Active, x.DiscordUserId != null, x.DiscordDisplayName, x.LastLoginAt, \"\")", model);
        Assert.Contains("WebsiteAccountRow(Guid Id, string Username, GlobalRole Role, bool Active, bool DiscordLinked, string? DiscordDisplayName, DateTimeOffset? LastLoginAt, string EventRoleSummary)", model);
        Assert.Contains("@if (account.DiscordLinked && !string.IsNullOrWhiteSpace(account.DiscordDisplayName))", accounts);
        Assert.Contains("<small class=\"admin-account-discord-name\">@account.DiscordDisplayName</small>", accounts);
        Assert.Contains("<span class=\"admin-account-discord-status @(account.DiscordLinked ? \"is-linked\" : \"is-unlinked\")\">@(account.DiscordLinked ? T[\"Linked\"] : T[\"Not linked\"])</span>", accounts);
        Assert.Contains(".admin-accounts-table .admin-account-discord-name { display: block; margin-top: 0.15rem; color: var(--admin-muted); font-size: 0.625rem; font-weight: 400; line-height: 1.35; overflow-wrap: anywhere; }", styles);
        Assert.Contains(".admin-accounts-table .admin-account-discord-status.is-linked { color: var(--admin-text); }", styles);
        Assert.Contains(".admin-accounts-table .admin-account-discord-status.is-unlinked { color: var(--admin-status-orange); }", styles);
        Assert.Equal(1, accounts.Split("event-overview-row-action", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, accounts.Split("data-account-manage-trigger=\"true\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("data-account-dialog-trigger", accounts);
        Assert.Contains("data-account-manage-trigger", manageDialogScript);
        Assert.Contains("data-account-create-trigger", manageDialogScript);
        Assert.Equal(1, accounts.Split("asp-page=\"Manage\" asp-route-id=", StringSplitOptions.None).Length - 1);
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

        Assert.Contains("OnGet() => NotFound()", createModel);
        Assert.Contains("OnPost() => NotFound()", createModel);
        Assert.DoesNotContain("Create emergency credential", accounts);
        Assert.DoesNotContain("data-account-emergency-action", manage);
        Assert.DoesNotContain("admin-destructive-confirmation", manage);
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
        Assert.Contains("admin-account-characters-panel", manage);
        Assert.Contains("admin-account-character-row", manage);
        Assert.Contains("ParticipationStateClass(role.ParticipationState)", manage);
        Assert.Contains("\"Confirmed\" => \"is-green\"", manage);
        Assert.Contains("\"WaitingList\" => \"is-orange\"", manage);
        Assert.Contains("\"Withdrawn\" => \"is-danger\"", manage);
        Assert.Contains("_ => \"is-muted\"", manage);
        Assert.DoesNotContain("account-manage-dialog.js", transfer);
        Assert.Contains("<select asp-for=\"Input.DestinationId\"", transfer);
        Assert.Contains("data-account-transfer", transfer);
        Assert.Contains("account-transfer.js", transfer);
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
        var accountsSearchStart = styles.IndexOf(".admin-shell-body .admin-accounts-page .admin-search-field-input:focus,", StringComparison.Ordinal);
        Assert.True(accountsSearchStart > genericSearchStart);
        Assert.Contains("background: var(--admin-search);", styles[accountsSearchStart..]);
        var roleFilterStart = styles.IndexOf(".admin-shell-body .admin-filter-select {", StringComparison.Ordinal);
        var roleFilterEnd = styles.IndexOf('}', roleFilterStart);
        Assert.True(roleFilterStart >= 0 && roleFilterEnd > roleFilterStart);
        Assert.Contains("transition: none;", styles[roleFilterStart..roleFilterEnd]);
        Assert.Contains("@media (max-width: 1100px)", styles);
        Assert.Contains(".admin-shell-body .admin-accounts-page,", styles);
        Assert.Contains("padding: 0.65rem 1rem; color: var(--admin-text);", styles);
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
        Assert.Contains("accountManageBound", manageDialogScript);
        Assert.Contains("bingo:account-directory-updated", manageDialogScript);
        Assert.DoesNotContain("window.innerWidth > 900", manageDialogScript);
        var accountLoadStart = manageDialogScript.IndexOf("const load = async", StringComparison.Ordinal);
        var accountLoadEnd = manageDialogScript.IndexOf("const hydrateDirectory", accountLoadStart, StringComparison.Ordinal);
        Assert.True(accountLoadStart >= 0 && accountLoadEnd > accountLoadStart);
        var accountLoad = manageDialogScript[accountLoadStart..accountLoadEnd];
        Assert.Contains("if (!dialog.open) dialog.showModal();", accountLoad);
        Assert.Contains("destination.searchParams.delete(\"overlay\");", accountLoad);
        Assert.Contains("fallback(destination.href);", accountLoad);
        Assert.Contains(".admin-route-dialog { width: 100vw; max-width: none; max-height: 100dvh; }", styles);
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
        Assert.DoesNotContain("data-account-emergency-action", manageDialogScript);
        Assert.Contains("window.adminConfirmation.open", manageDialogScript);
        Assert.DoesNotContain("admin-account-inline-confirmation", manageDialogScript);
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
        Assert.DoesNotContain("admin-account-emergency-confirmation", manage);
        Assert.DoesNotContain("admin-account-emergency-confirmation", manageDialogScript);
        Assert.DoesNotContain("admin-account-emergency-confirmation", styles);
        Assert.Contains("data-account-editor-discard role=\"alertdialog\"", manage);
        Assert.DoesNotContain("Issue a one-time credential link.", manage);
        Assert.DoesNotContain("Stop this fallback login.", manage);
        Assert.DoesNotContain("Allow the configured fallback login.", manage);
        Assert.DoesNotContain("<summary class=\"admin-button-create\"><strong>Enable emergency credential</strong>", manage);
        Assert.Contains(".admin-account-confirmation-dialog {", styles);
        Assert.Contains("background: var(--admin-surface-raised);", styles);
        Assert.Contains("border: 1px solid #2b2b2b;", styles);
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
