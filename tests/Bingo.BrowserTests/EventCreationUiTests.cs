using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bingo.BrowserTests;

public sealed class EventCreationUiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public EventCreationUiTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task AnonymousVisitorsAreRedirectedFromCreationAndIdentityRoutes()
    {
        using var creation = await client.GetAsync("/Admin/Events/Create");
        using var identity = await client.GetAsync($"/Admin/Events/Identity/{Guid.NewGuid()}");
        using var schedule = await client.GetAsync($"/Admin/Events/Schedule/{Guid.NewGuid()}");
        using var banner = await client.GetAsync($"/Admin/Events/Banner/{Guid.NewGuid()}");
        using var participants = await client.GetAsync($"/Admin/Events/Participants/{Guid.NewGuid()}");
        using var participantsPost = await client.PostAsync($"/Admin/Events/Participants/{Guid.NewGuid()}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["participantId"] = Guid.NewGuid().ToString(), ["payment"] = "Paid" }));

        Assert.Equal(System.Net.HttpStatusCode.Redirect, creation.StatusCode);
        Assert.Equal("/Account/Login", creation.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, identity.StatusCode);
        Assert.Equal("/Account/Login", identity.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, schedule.StatusCode);
        Assert.Equal("/Account/Login", schedule.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, banner.StatusCode);
        Assert.Equal("/Account/Login", banner.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, participants.StatusCode);
        Assert.Equal("/Account/Login", participants.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, participantsPost.StatusCode);
        Assert.Equal("/Account/Login", participantsPost.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public void CreationAndIdentityMarkupKeepTheGuidedAndNativeRouteBoundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var creation = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Create.cshtml"));
        var identity = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml"));
        var manage = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var manageHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs"));
        var eventIndexHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Index.cshtml.cs"));
        var eventIndex = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Index.cshtml"));
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Index.cshtml"));
        var schedule = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Schedule.cshtml"));
        var scheduleHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Schedule.cshtml.cs"));
        var publicBoard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Events", "Board.cshtml"));
        var questions = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Questions.cshtml"));
        var participant = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participant.cshtml"));
        var participants = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participants.cshtml"));
        var participantForm = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "_InternalParticipantForm.cshtml"));
        var participantsHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participants.cshtml.cs"));
        var siteScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        var signupQuestionsOverlayScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "signup-questions-overlay.js"));
        var siteCss = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "css", "site.css"));
        var roadmap = File.ReadAllText(Path.Combine(repositoryRoot, "UI_OVERHAUL_ROADMAP.md"));
        var creationScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-create.js"));
        var dateTimeScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-create-datetime.js"));
        var validationScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-create-validation.js"));
        var manageScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-manage.js"));
        var adminLayout = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Shared", "_AdminLayout.cshtml"));
        var createHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Create.cshtml.cs"));

        Assert.Contains("data-create-panel=\"0\"", creation);
        Assert.Contains("data-create-panel=\"4\"", creation);
        Assert.Equal(5, Count(creation, "data-create-step=\""));
        foreach (var shortTitle in new[] { "Details", "Schedule", "Signup", "Estimates", "Review" })
            Assert.Contains($">@T[\"{shortTitle}\"]</strong>", creation);
        Assert.Contains("aria-label=\"@T[\"Schedule and capacity\"]\"", creation);
        Assert.Contains("aria-label=\"@T[\"Planning\"]\"", creation);
        Assert.Equal(5, Count(creation, "event-create-step-check"));
        Assert.DoesNotContain(".event-create-step.is-complete > span::before", siteCss);
        Assert.Contains(".admin-shell-body .event-create-step.is-complete .event-create-step-check { display: block; }", siteCss);
        Assert.Contains("Step 2 of 5", creation);
        Assert.DoesNotContain("event-schedule-context", creation);
        Assert.Contains("event-schedule-groups", creation);
        Assert.Contains("schedule-waiting-list", creation);
        Assert.DoesNotContain("event-submission-note", creation);
        Assert.Contains("Maximum players", creation);
        Assert.DoesNotContain("Planning estimates", creation);
        Assert.Contains("grid-template-columns: 1fr", siteCss);
        Assert.Contains(".admin-shell-body .event-create-layout { grid-template-columns: 1fr;", siteCss);
        Assert.Contains(".admin-shell-body .event-create-steps { position: relative;", siteCss);
        Assert.Contains("max-width: 48rem", siteCss);
        Assert.Contains("class=\"event-create-heading admin-events-heading\"", creation);
        Assert.Contains("class=\"admin-module-description\"", creation);
        Assert.Contains(".admin-module-description { max-width: 44rem; margin: 0.4rem 0 0; color: var(--admin-muted); font-size: 0.75rem; line-height: 1.45; }", siteCss);
        Assert.Contains(".admin-events-heading .admin-module-description { margin-top: 0.25rem; color: var(--admin-muted); font-size: 0.75rem; line-height: 1.5; }", siteCss);
        Assert.Contains("event-wom-link\" open", creation);
        Assert.Contains("event-create-name-field", creation);
        Assert.Contains("event-create-label-row", creation);
        Assert.Contains("event-create-inline-error", creation);
        Assert.Contains("data-val-required=\"Required\"", creation);
        Assert.Contains("aria-describedby=\"Input_Name-error\"", creation);
        Assert.Contains(".admin-shell-body .event-wom-link { margin: 1rem 0 0;", siteCss);
        Assert.Contains(".admin-shell-body .event-create-name-field .event-create-inline-error { color: var(--admin-danger); font-size: 0.625rem; font-weight: 600; line-height: 1.25; }", siteCss);
        Assert.Contains(".admin-shell-body .event-wom-link-body { display: grid; gap: 0.75rem; padding: 0 0 0.75rem; }", siteCss);
        Assert.Contains(".admin-shell-body .event-create-workspace.panel .event-create-panel { padding: 1.25rem; }", siteCss);
        Assert.Contains("has-event-datetime-adapters", dateTimeScript);
        Assert.Contains("data-datetime-canonical", creation);
        Assert.Contains("bingoEventDateTime", creationScript);
        Assert.Contains("defaultTime", dateTimeScript);
        Assert.Contains("time.value = timeValue ? timeValue.slice(0, 5) : defaultTime;", dateTimeScript);
        Assert.Contains("canonical.value = date.value && time.value ? `${date.value}T${time.value}` : \"\";", dateTimeScript);
        Assert.Contains("function format", dateTimeScript);
        Assert.Contains("${match[3]}/${match[2]}/${match[1]} ${match[4]}:${match[5]}", dateTimeScript);
        Assert.Contains("bingoEventCreateValidation", creationScript);
        Assert.Contains("validator.settings.ignore = \":hidden:not([name])\";", validationScript);
        Assert.Contains("function validateCurrentStep", validationScript);
        Assert.Contains("function navigateForward", validationScript);
        Assert.Contains("advanceTo(currentStep + 1)", creationScript);
        Assert.Contains("target <= currentStep", creationScript);
        Assert.Contains("input-validation-error, [aria-invalid='true']", validationScript);
        Assert.Contains("target.focus({ preventScroll: true })", validationScript);
        Assert.Contains("event-wom-link", creation);
        Assert.Contains("cached activity, EHB, and team leaderboard synchronization", creation);
        Assert.Contains("Plus+Jakarta+Sans:wght@400;500;600;700", adminLayout);
        Assert.Contains("Cinzel:wght@500;700;900", adminLayout);
        Assert.Contains("font-family: \"Plus Jakarta Sans\"", siteCss);
        Assert.Contains("font-family: Cinzel", siteCss);
        Assert.Contains("event-datetime-part > span:first-child", siteCss);
        Assert.Contains("input[type=\"file\"].form-control { display: flex; align-items: center;", siteCss);
        Assert.Contains("padding: 1.25rem", siteCss);
        Assert.Contains(".admin-shell-body textarea::placeholder", siteCss);
        Assert.Contains("Private signup editing", creation);
        Assert.Contains("Opening signups later exposes only the event information and signup form.", creation);
        Assert.Contains("enctype=\"multipart/form-data\"", creation);
        Assert.Contains("<noscript>", creation);
        Assert.Contains("create-event-noscript-submit", creation);
        Assert.Contains("Input.BuyInDescription", creation);
        Assert.Contains("@T[\"Account\"]", creation);
        Assert.Contains("Required Regular account selector. EHB belongs to this account answer.", creation);
        Assert.Contains("@T[\"Captain volunteer\"]", creation);
        Assert.Contains("<option value=\"Text\">@T[\"Text\"]</option>", creation);
        Assert.DoesNotContain("ShortText", creation);
        Assert.DoesNotContain("LongText", creation);
        Assert.DoesNotContain("Second OSRS account", creation);
        Assert.DoesNotContain("Discord name", creation);
        Assert.DoesNotContain("Comments and availability", creation);
        Assert.DoesNotContain("SubmissionCutoff", creation);
        Assert.DoesNotContain("ExpectedTeamCount", creation);
        Assert.DoesNotContain("ExpectedTeamSize", creation);
        Assert.DoesNotContain("Submission cutoff", identity);
        Assert.DoesNotContain("DateTimeOffset.MinValue", manageHandler);
        Assert.DoesNotContain("DateTimeOffset.MinValue", eventIndexHandler);
        Assert.Contains("T[\"Not set\"]", manage);
        Assert.Contains("asp-page-handler=\"StartEvent\"", manage);
        Assert.Contains("ConfirmStartEvent", manage);
        Assert.Contains(".event-control-workspace", manage);
        Assert.Contains("data-manage-overview", manage);
        Assert.DoesNotContain("event-participants-section", manage);
        Assert.DoesNotContain("event-manage-links", manage);
        Assert.Contains("event-information", manage);
        Assert.Contains("@T[\"Event information\"]", manage);
        Assert.DoesNotContain("ParticipantSearch", manageHandler);
        Assert.Contains("@page \"{id:guid}\"", participants);
        Assert.Contains("asp-page=\"Questions\"", participants);
        Assert.Contains("asp-page-handler=\"Payment\"", participants);
        Assert.Contains("asp-page-handler=\"Withdraw\"", participants);
        Assert.Contains("OnPostCreateInternalParticipantAsync", participantsHandler);
        Assert.Contains("Readiness checks", manage);
        Assert.DoesNotContain("OperationalStatusLabel", manage);
        Assert.DoesNotContain("OperationalStatusClass", manageHandler);
        Assert.DoesNotContain("Lifecycle status", manage);
        Assert.DoesNotContain("Next decision", manage);
        Assert.DoesNotContain("event-overview-decision", manage);
        Assert.DoesNotContain("Important information", manage);
        Assert.Contains("event-overview-information-summary", manage);
        Assert.Contains("event-overview-information-name", manage);
        Assert.Contains("event-overview-information-status", manage);
        Assert.DoesNotContain("event-overview-status-pill", manage);
        Assert.Contains("var eventStatus = AdminEventStatePresentation.For(eventView.State, T);", manage);
        Assert.Contains("<span class=\"admin-status-pill @eventStatus.Modifier\">@eventStatus.Label</span>", manage);
        Assert.Contains("href=\"/Admin/Events/Participants/@eventContext.Id\"", adminLayout);
        Assert.Contains("Public pages", manage);
        Assert.Contains("data-copy-url=\"@signupUrl\"", manage);
        Assert.Contains("data-copy-url=\"@signupTableUrl\"", manage);
        Assert.Contains("showPublicBoard", manage);
        Assert.Contains("eventView.BoardPublished", manage);
        Assert.Contains("data-copy-url=\"@publicBoardUrl\"", manage);
        Assert.Contains("@page \"/Events/{slug}/Board\"", publicBoard);
        Assert.DoesNotContain("/Events/Results", manage);
        Assert.Contains("Model.EffectiveTimeline", manage);
        Assert.Contains("EffectiveTimelineFor", manageHandler);
        Assert.DoesNotContain("T[\"Not yet\"]", manage);
        Assert.DoesNotContain("Actual event end", manage);
        Assert.Contains("event-overview-summary", manage);
        Assert.Contains("event-overview-row-blocked", manage);
        Assert.DoesNotContain("event-overview-slug", manage);
        Assert.DoesNotContain("admin-state admin-state-@statusClass", manage);
        Assert.DoesNotContain("statusLabel", manage);
        Assert.DoesNotContain("event-overview-public", manage);
        Assert.DoesNotContain("event-overview-meta", manage);
        Assert.DoesNotContain("event-overview-hero", manage);
        Assert.Contains("event-overview-metric", manage);
        Assert.DoesNotContain("SignupProgressPercent", manageHandler);
        Assert.DoesNotContain("event-overview-progress", manage);
        Assert.DoesNotContain("asp-page-handler=\"Capacity\"", manage);
        Assert.DoesNotContain("Input.ParticipantCap", schedule);
        Assert.Contains("SignupAdministration.ParticipantCap", participants);
        Assert.Contains("asp-page-handler=\"SignupAdministration\"", participants);
        Assert.Contains("UpdateSignupAdministrationAsync", participantsHandler);
        Assert.Contains("SaveScheduleAsync", scheduleHandler);
        Assert.DoesNotContain("CompetitionId", identity);
        Assert.Contains("asp-page-handler=\"Competition\"", manage);
        Assert.Contains("BoardRows", manageHandler);
        Assert.Contains("ConfiguredBoardTileCount", manageHandler);
        Assert.Contains("ExpectedBoardCellCount", manageHandler);
        Assert.Contains("boardTileSummary", manage);
        Assert.Contains("Wise Old Man sync", manage);
        Assert.Contains("CompetitionIntegration?.Configured", manage);
        Assert.DoesNotContain("@foreach (var item in Model.SignupReadiness.Blockers)", manage);
        Assert.DoesNotContain("@foreach (var item in Model.SignupReadiness.LaterTasks)", manage);
        Assert.Contains("event-control-status", manage);
        Assert.Contains("action-danger-outline", manage);
        Assert.DoesNotContain("event-overview-header-action-warning", manage);
        Assert.Contains("partialUpdateTargets = \"#app-notice-region, .event-overview, .event-overview-dates-panel, .event-control-workspace\"", manage);
        Assert.Contains("event-danger-zone", manage);
        Assert.Equal(1, Count(manage, "event-danger-zone-container"));
        Assert.Contains("event-confirmation-box", manage);
        Assert.Contains("event-confirmation-actions", manage);
        Assert.Contains(".event-manage-page .event-danger-zone-form", siteCss);
        Assert.Contains("width: min(26rem, 100%);", siteCss);
        Assert.Contains("min-height: 2.75rem;", siteCss);
        Assert.Contains("PrepareStartConfirmation", manage);
        Assert.Contains("PrepareEndConfirmation", manage);
        Assert.Contains("PrepareResumeConfirmation", manage);
        Assert.Contains("PrepareDestructiveConfirmation", manage);
        Assert.DoesNotContain("event-danger-zone-disclosure", manage);
        Assert.DoesNotContain("<details class=\"event-danger-zone", manage);
        Assert.DoesNotContain("I acknowledge", manage);
        Assert.DoesNotContain("I confirm", manage);
        Assert.Contains("event-create-cancel", manage);
        Assert.DoesNotContain("action-accent-outline", manage);
        Assert.DoesNotContain("action-neutral-outline", manage);
        Assert.DoesNotContain("btn-outline-light", manage);
        Assert.DoesNotContain("btn-primary", manage);
        Assert.DoesNotContain("btn-danger", manage);
        Assert.Contains("Schedule handling", manage);
        Assert.Contains("Keep current schedule", manage);
        Assert.Contains("Use competition dates", manage);
        Assert.DoesNotContain("type=\"radio\"", manage);
        Assert.DoesNotContain("Operations", manage);
        Assert.DoesNotContain("Confirmed participants", manage);
        Assert.Contains("event-control-status", manage);
        Assert.Contains("event-control-static", manage);
        Assert.DoesNotContain("event-control-disclosure", manage);
        Assert.DoesNotContain("event-control-details", manage);
        Assert.Contains("event-wom-status-danger", manage);
        Assert.Contains("event-wom-status-success", manage);
        Assert.Contains("m4.9 4.9 14.2 14.2", manage);
        Assert.Contains("event-wom-form", manage);
        Assert.Contains("event-wom-header", manage);
        Assert.Contains("event-wom-schedule-field", manage);
        Assert.Contains("Link a competition to sync EHB activity.", manage);
        Assert.DoesNotContain("Wise Old Man remains read-only.", manage);
        Assert.Contains(">Link competition</button>", manage);
        Assert.DoesNotContain("Configure synchronization", manage);
        Assert.DoesNotContain("unknown remaining of unknown", manage);
        Assert.Contains("hasRequestBudget", manage);
        Assert.Contains("event-wom-budget", manage);
        Assert.Contains(".event-manage-page .event-wom-form { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: end; justify-content: flex-end;", siteCss);
        Assert.Contains(".event-manage-page .event-wom-form > .event-create-field { width: 7rem; flex: 0 0 7rem; }", siteCss);
        Assert.Contains("width: 100%; max-width: 100%; min-width: 0;", siteCss);
        Assert.Contains(".event-manage-page .event-wom-form .event-create-field label { color: var(--admin-muted); font-size: 0.625rem;", siteCss);
        Assert.Contains(".event-manage-page .event-wom-form > .event-create-field input { width: 100%;", siteCss);
        Assert.Contains(".event-manage-page .event-wom-form input[type=\"number\"] { appearance: textfield;", siteCss);
        Assert.Contains(".event-manage-page .event-wom-select-chevron", siteCss);
        Assert.Contains(".event-manage-page .event-wom-select-wrap .form-select { width: 100%; overflow: hidden; padding-right: 1.9rem; appearance: none;", siteCss);
        Assert.Contains("text-overflow: ellipsis; white-space: nowrap;", siteCss);
        Assert.Contains(".event-manage-page .event-wom-form .event-wom-schedule-field { width: 11rem; max-width: 11rem; flex: 0 0 11rem; }", siteCss);
        Assert.Contains(".event-manage-page .event-wom-form > button { flex: 0 0 auto; align-self: end; width: fit-content; min-width: 0; max-width: 100%;", siteCss);
        Assert.DoesNotContain("Without this option, both event instants must already be within five minutes.", manage);
        Assert.Contains(".event-control-status a { display: inline-flex; align-items: center; gap: 0.3rem; color: #aaa9a5;", siteCss);
        Assert.Contains(".event-manage-page .event-wom-section { grid-template-columns: minmax(9rem, 0.7fr) minmax(15rem, 1.3fr);", siteCss);
        Assert.Contains(".event-manage-page .event-wom-section > .event-control-static { grid-column: 2; width: 100%;", siteCss);
        Assert.Contains("justify-content: flex-end; width: 100%; max-width: 100%; min-width: 0;", siteCss);
        Assert.Contains(".event-manage-page .event-wom-status-value.event-wom-status-danger", siteCss);
        Assert.Contains(".event-manage-page .event-wom-form { align-items: stretch; flex-direction: column; width: 100%; }", siteCss);
        Assert.DoesNotContain(".event-control-status::before", siteCss);
        Assert.Contains(".event-manage-page .event-admin-lower { grid-area: operations; min-width: 0; margin-top: 0; padding-top: 0; border-top: 0; }", siteCss);
        Assert.Contains(".event-manage-page .event-danger-zone .action-danger-outline svg", siteCss);
        Assert.True(manage.IndexOf("competition-integration-heading", StringComparison.Ordinal) < manage.IndexOf("event-danger-zone-container", StringComparison.Ordinal));
        Assert.Contains("class=\"event-create-field\"", manage);
        Assert.Contains("event-admin-lower", manage);
        Assert.Contains("admin-section-heading event-overview-section-heading", manage);
        Assert.Contains("event-create-cancel action-danger-outline", manage);
        Assert.Contains(".event-manage-page .event-admin-controls > .event-admin-control-group", siteCss);
        Assert.Contains(".event-manage-page .event-admin-control-group { margin-top: 0.65rem; padding: 0.9rem 1rem;", siteCss);
        Assert.Contains(".event-manage-page .event-admin-control-group.event-control-group-warning", siteCss);
        Assert.Contains(".event-manage-page .event-admin-controls > .event-admin-control-group:first-of-type", siteCss);
        Assert.Contains(".event-manage-page .event-signup-group { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; }", siteCss);
        Assert.Contains(".event-control-status a::after { content: \"↗\"", siteCss);
        Assert.Contains(".event-manage-page .event-control-workspace {", siteCss);
        Assert.Contains(".event-manage-page .event-admin-controls > .admin-section-heading", siteCss);
        Assert.Contains(".event-admin-event-actions { display: flex;", siteCss);
        Assert.DoesNotContain("--event-control-height", siteCss);
        Assert.DoesNotContain("--event-control-button-width", siteCss);
        Assert.DoesNotContain(".event-admin-event-actions .btn", siteCss);
        Assert.DoesNotContain(".event-code-toggle-form .btn", siteCss);
        Assert.DoesNotContain(".event-admin-action-form", siteCss);
        Assert.DoesNotContain(".event-admin-inline-form", siteCss);
        Assert.DoesNotContain(".event-choice-field select", siteCss);
        Assert.Contains(".admin-shell-body .admin-field .form-control,", siteCss);
        Assert.Contains(".admin-shell-body .admin-button-primary", siteCss);
        Assert.Contains(".admin-shell-body .admin-button-secondary", siteCss);
        Assert.Contains(".admin-shell-body .admin-header-create-action", siteCss);
        Assert.Contains(".admin-shell-body .admin-field .form-control:-webkit-autofill", siteCss);
        Assert.Contains("admin-header-create-action", eventIndex);
        Assert.Contains("admin-button-secondary", eventIndex);
        Assert.Contains("admin-events-state-filter admin-field", eventIndex);
        Assert.Contains("admin-button-primary", creation);
        Assert.Contains("admin-button-secondary", creation);
        Assert.DoesNotContain("admin-events-create-action", eventIndex);
        Assert.DoesNotContain("admin-workspace-action", eventIndex);
        Assert.DoesNotContain("admin-events-page .admin-events-search input:focus-visible", siteCss);
        Assert.DoesNotContain("event-create-page .btn-primary", siteCss);
        Assert.DoesNotContain("event-create-page .btn-outline-light", siteCss);
        Assert.Contains("Shared Admin UI contract", roadmap);
        Assert.Contains("standard is `24px` (`1.5rem`)", roadmap);
        Assert.Contains("Typography ownership is explicit", roadmap);
        Assert.Contains("Shared Admin fields use `.admin-shell-body .event-create-field .form-control`", roadmap);
        Assert.Contains("Button roles reuse the exact Create-page `event-create-cancel` treatment", roadmap);
        Assert.Contains("event-admin-controls { padding: 1.25rem; }", siteCss);
        Assert.Contains(".event-manage-page .event-control-workspace > .panel { background: var(--admin-surface); border: 0; border-radius: 0.75rem; }", siteCss);
        Assert.Contains("Action acknowledgement/confirmation checkboxes are not used", roadmap);
        Assert.Equal(1, Count(manage, "asp-page-handler=\"StartEvent\""));
        Assert.Equal(1, Count(manage, "asp-page-handler=\"EndEvent\""));
        Assert.Equal(1, Count(manage, "asp-page-handler=\"ResumeEvent\""));
        Assert.DoesNotContain("Lifecycle State Preview", manage);
        Assert.DoesNotContain("onUpdateState", manage);
        Assert.Contains("EventDate(DateTimeOffset? value", manageHandler);
        Assert.Contains("finalizationService.GetReadinessAsync", manageHandler);
        Assert.Contains("AddResolutionRoute", manageHandler);
        Assert.Contains("SIGNUP_FORM_MISSING", manageHandler);
        Assert.Contains("DistinctBy(x => (x.Code, x.Description, x.Route))", manageHandler);
        Assert.DoesNotContain("event-overview-heading-action", siteCss);
        Assert.DoesNotContain("event-overview-hero", siteCss);
        Assert.Contains(".event-overview-metric-header { display: flex;", siteCss);
        Assert.Contains(".event-overview-metric-value-row > strong {", siteCss);
        Assert.Contains(".event-overview-metric-context {", siteCss);
        Assert.Contains(".event-overview-metric-dot {", siteCss);
        Assert.Contains("event-manage-layout { display: grid; grid-template-columns: minmax(0, 2fr) minmax(18rem, 1fr); grid-template-areas: \"summary summary\" \"readiness dates\" \"operations dates\";", siteCss);
        Assert.Contains(".event-manage-main,\n.event-overview { display: contents;", siteCss);
        Assert.Contains("background: var(--admin-surface); border: 1px solid var(--admin-border); border-radius: 0.75rem", siteCss);
        Assert.DoesNotContain("event-overview-meta", siteCss);
        Assert.Contains("event-overview-summary { display: grid; grid-area: summary; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1.25rem;", siteCss);
        Assert.Contains("event-overview-readiness-panel { grid-area: readiness; align-self: start; }", siteCss);
        Assert.Contains("@media (max-width: 1100px)", siteCss);
        Assert.Contains(".event-manage-layout { grid-template-columns: 1fr; grid-template-areas: \"summary\" \"readiness\" \"operations\"; }", siteCss);
        Assert.Contains(".event-overview-dates-panel { display: none; }", siteCss);
        Assert.Contains(".event-overview-summary { grid-template-columns: repeat(2, minmax(0, 1fr)); }", siteCss);
        Assert.DoesNotContain("grid-template-areas: \"summary\" \"readiness\" \"operations\" \"dates\";", siteCss);
        Assert.Contains("@media (max-width: 600px)", siteCss);
        Assert.Contains(".event-manage-page .event-admin-event-actions,\n  .event-manage-page .event-code-toggle-form { width: 100%; max-width: 100%; justify-self: stretch; }", siteCss);
        Assert.Contains(".event-manage-page .event-wom-form { align-items: stretch; flex-direction: column; width: 100%; }", siteCss);
        Assert.Contains(".event-danger-zone-container { align-items: stretch; flex-direction: column; }", siteCss);
        Assert.DoesNotContain("admin-operational-status", siteCss);
        Assert.DoesNotContain(".admin-state::before", siteCss);
        Assert.DoesNotContain(".admin-state::after", siteCss);
        Assert.Contains(".admin-state { display: inline-flex; width: fit-content; min-height: 0;", siteCss);
        Assert.Contains("padding: 0.125rem 0.5rem", siteCss);
        Assert.DoesNotContain("AdminEventStatusPresenter", manageHandler);
        Assert.Contains("admin-state admin-state-@item.State.ToString().ToLowerInvariant()", eventIndex);
        Assert.Contains("admin-state admin-state-@item.State.ToString().ToLowerInvariant()", dashboard);
        Assert.DoesNotContain("admin-state admin-state-@eventContext.State.ToString().ToLowerInvariant()", adminLayout);
        Assert.Contains("admin-state admin-state-@option.State.ToString().ToLowerInvariant()", adminLayout);
        Assert.Contains("event-overview-dates-panel { position: sticky; top: calc(var(--admin-header-height) + 1rem); grid-area: dates; align-self: start;", siteCss);
        Assert.Contains("event-overview-date-row", manage);
        Assert.DoesNotContain("event-overview-date-marker", manage);
        Assert.DoesNotContain("event-overview-dates::before", siteCss);
        Assert.DoesNotContain("event-overview-icon", manage);
        Assert.Contains("event-overview-blocker-link", siteCss);
        Assert.Contains("text-decoration: none", siteCss);
        Assert.Contains("--admin-chart-yellow", siteCss);
        Assert.Contains("--admin-chart-green", siteCss);
        Assert.Contains("--event-status-yellow: var(--admin-warning);", siteCss);
        Assert.DoesNotContain("event-overview-status-pill", siteCss);
        Assert.Contains(".admin-status-pill.is-cyan", siteCss);
        Assert.Contains("text-transform: none", siteCss);
        Assert.Contains("border: 0; border-radius: 999px", siteCss);
        Assert.Contains(".event-public-pages { margin-top: 0.75rem; padding-top: 0.2rem;", siteCss);
        Assert.Contains(".event-public-pages h3 { margin: 0 0 0.6rem; padding: 0.35rem 0 0;", siteCss);
        Assert.Contains("@media (min-width: 901px)", siteCss);
        Assert.Contains("event-overview-row", siteCss);
        Assert.Contains("m21.73 18-8-14", manage);
        Assert.Contains("BlockerActionLabel", manage);
        Assert.Contains("All start blockers are resolved. Confirm to start the event now.", manage);
        Assert.Contains("asp-page-handler=\"Discard\"", manage);
        Assert.Contains("asp-page-handler=\"Cancel\"", manage);
        Assert.Contains("ConfirmDestructiveAction", manage);
        Assert.Contains("CancellationReason", manage);
        Assert.Contains("var showStartControl = showAllControlStages || eventView.State == EventState.SignupClosed", manage);
        Assert.Contains("ShowAllControlStages", manageHandler);
        Assert.Contains("preview-all-controls", manageHandler);
        Assert.Contains("@T[\"{0} blockers\", signupBlockerCount]", manage);
        Assert.DoesNotContain("@T[\"{0} blockers · {1} warnings\"", manage);
        Assert.Contains("Automatic start postponed", manageHandler);
        Assert.Contains("StartReadiness?.Blockers ?? []", manageHandler);
        Assert.Contains("item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed", manageHandler);
        Assert.Contains("LIFECYCLE_STATE_INVALID", manageHandler);
        Assert.Contains("Signup has not been opened and closed", manageHandler);
        Assert.Contains("Signup is still open. Close signup", manageHandler);
        Assert.Contains("@blocker.Route", manage);
        Assert.Contains("eventLifecycle.StartNowAsync", manageHandler);
        Assert.Contains("eventLifecycle.EndNowAsync", manageHandler);
        Assert.Contains("eventLifecycle.ResumePrematureEndAsync", manageHandler);
        Assert.Contains("asp-page-handler=\"ResumeEvent\"", manage);
        Assert.Contains("ConfirmResumeEvent", manage);
        Assert.Contains("ReplacementEventEndsAt", manage);
        Assert.Contains("ResumeReason", manage);
        Assert.Contains("destructiveLifecycle.DiscardAsync", manageHandler);
        Assert.Contains("destructiveLifecycle.CancelAsync", manageHandler);
        Assert.Contains("@page \"{id:guid}\"", identity);
        Assert.Contains("enctype=\"multipart/form-data\"", identity);
        Assert.Contains("Input.ConfirmTimezoneChange", identity);
        Assert.Contains("identity-editor-panel", identity);
        Assert.Contains("identity-primary-fields", identity);
        Assert.Contains("identity-editor-section", identity);
        Assert.Contains("Update the details shown across signup and public event pages.", identity);
        Assert.DoesNotContain("admin-module-eyebrow", identity);
        Assert.DoesNotContain("@Model.EventName", identity);
        Assert.Contains("identity-remove-banner-form", identity);
        Assert.Contains("asp-page-handler=\"RemoveBanner\"", identity);
        Assert.Contains("compact-evidence-drop", identity);
        Assert.Contains("compact-evidence-preview", identity);
        Assert.Contains("action-remove-x", identity);
        Assert.DoesNotContain("name=\"Input.RemoveBanner\"", identity);
        Assert.DoesNotContain("type=\"checkbox\"", identity);
        Assert.DoesNotContain("btn-outline-light", identity);
        Assert.DoesNotContain("btn-primary", identity);
        Assert.DoesNotContain("dialog-actions", identity);
        Assert.Contains("identity-editor-page .event-create-heading { padding-bottom: 0; border-bottom: 0; }", siteCss);
        Assert.Contains("identity-timezone-field { grid-column: 1 / -1; max-width: 22rem; }", siteCss);
        Assert.Contains("identity-editor-panel > .admin-section-heading > p { margin: 0; }", siteCss);
        Assert.Equal(2, Count(identity, "class=\"admin-section-heading\""));
        Assert.Contains("identity-editor-section .admin-section-heading { display: block; }", siteCss);
        Assert.Contains("identity-editor-section .admin-section-heading h2 { font-family: inherit; font-size: 0.75rem; line-height: 1.35; }", siteCss);
        Assert.Contains("new(\"Europe/Copenhagen\", \"Copenhagen (Europe/Copenhagen)\")", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml.cs")));
        Assert.Contains("new(\"UTC\", \"UTC\")", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml.cs")));
        Assert.DoesNotContain("Europe/London", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml.cs")));
        Assert.Contains("@page \"{id:guid}\"", schedule);
        Assert.Contains("datetime-local", schedule);
        Assert.Equal(10, Count(schedule, "step=\"300\""));
        Assert.DoesNotContain("item.Code", schedule);
        Assert.Contains("item.Code == \"TEAM_ACCESS_MISSING\"", manage);
        Assert.Contains("$\"/Admin/Events/Draft/{eventId}\"", manageHandler);
        Assert.DoesNotContain("$\"/Admin/Events/Teams/{eventId}\"", manageHandler);
        Assert.Contains("yyyy-MM-ddTHH:mm", scheduleHandler);
        Assert.Contains("SignupAdministration.WaitingListEnabled", participants);
        Assert.Contains("SignupQuestionType.Text", questions);
        Assert.DoesNotContain("ShortText", questions);
        Assert.DoesNotContain("LongText", questions);
        Assert.DoesNotContain("asp-page-handler=\"WaitingList\"", questions);
        Assert.DoesNotContain("WaitingListEnabled", questions);
        Assert.Contains("asp-page-handler=\"SignupCode\"", questions);
        Assert.Contains("asp-page=\"/Admin/Events/Questions\" asp-route-id=\"@eventId\"", questions);
        Assert.Contains("<script src=\"~/js/event-manage.js\" asp-append-version=\"true\"></script>", questions);
        Assert.Contains("Save signup code", questions);
        Assert.Contains("data-signup-questions-trigger", participants);
        Assert.Contains("data-signup-questions-dialog", adminLayout);
        Assert.Contains("data-signup-questions-content", adminLayout);
        Assert.DoesNotContain("data-signup-questions-frame", adminLayout);
        Assert.Contains("showModal", signupQuestionsOverlayScript);
        Assert.Contains("popstate", signupQuestionsOverlayScript);
        Assert.Contains("window.fetch", signupQuestionsOverlayScript);
        Assert.Contains("replaceChildren", signupQuestionsOverlayScript);
        Assert.DoesNotContain("postMessage", signupQuestionsOverlayScript);
        Assert.DoesNotContain("data-participant-add-panel", participants);
        Assert.Contains("data-participant-add-trigger", participants);
        Assert.Contains("asp-page-handler=\"CreateInternalParticipant\"", participantForm);
        Assert.Contains("participant-add-dialog", manageScript);
        Assert.Contains("participantAddOverlay", manageScript);
        Assert.Contains("window.innerWidth > 900", manageScript);
        Assert.Contains("dialog.showModal()", manageScript);
        Assert.DoesNotContain("<header class=\"event-create-heading admin-events-heading\">", questions.Substring(questions.IndexOf("data-overlay=\"@(Model.IsOverlay", StringComparison.Ordinal)));
        Assert.Contains("Questions added after the first response are always optional.", questions);
        Assert.Contains("@if (Model.HasFirstResponse)", questions);
        Assert.Contains("data-required-field", questions);
        Assert.Contains("[Bind(Prefix = \"Input\")] QuestionInput input", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Questions.cshtml.cs")));
        Assert.Contains("[Bind(Prefix = \"Replacement\")] QuestionInput replacementInput", File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Questions.cshtml.cs")));
        var questionsHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Questions.cshtml.cs"));
        Assert.Contains("[Bind(Prefix = \"Edit\")] EditQuestionInput edit", questionsHandler);
        Assert.Contains("HasStructuralEditInput", questionsHandler);
        Assert.Contains("question.UpdatePresentation(edit.Label, edit.HelpText)", questionsHandler);
        Assert.DoesNotContain("<input type=\"hidden\" name=\"Edit.Type\"", questions);
        Assert.DoesNotContain("<input type=\"hidden\" name=\"Edit.Required\"", questions);
        Assert.DoesNotContain("selected=\"@(question.Type == SignupQuestionType.Text)\"", questions);
        Assert.DoesNotContain("checked=\"@question.Required\"", questions);
        Assert.Contains("Replacement.Label", questions);
        Assert.Contains("aria-label=\"Participant signup preview\"", questions);
        Assert.Contains("event-manage.js", questions);
        Assert.Contains("participant-payment-control", participant);
        Assert.DoesNotContain("data-payment-save", participant);
        Assert.DoesNotContain("data-save-state", participant);
        Assert.Contains("data-owner-account-picker", participant);
        Assert.Contains("DestinationOwnerAccountId", participant);
        Assert.DoesNotContain("DestinationUsernameConfirmation", participant);
        Assert.Contains("@T[\"Remove\"]", participant);
        Assert.Contains("<script src=\"~/js/event-manage.js\" asp-append-version=\"true\"></script>", participant);
        Assert.Contains("asp-page-handler=\"FillVacancy\"", participant);
        Assert.Contains("asp-page-handler=\"CompletePromotionFollowUp\"", participant);
        Assert.Contains("Mark follow-up complete", participant);
        Assert.Contains("ReplacementWaitingParticipantId", participant);
        Assert.DoesNotContain("selected=\"False\"", participant);
        Assert.Contains("minuteIncrement: 5", siteScript);
        Assert.Contains("time_24hr: true", siteScript);
        Assert.Contains("length: 24", siteScript);
        Assert.Contains("length: 12", siteScript);
        Assert.Contains("querySelectorAll(\"[data-date-time-picker]\")", siteScript);
        Assert.Contains("dateFormat: \"Y-m-d\\\\TH:i\"", siteScript);
        Assert.Contains("altFormat: \"d/m/Y H:i\"", siteScript);
        Assert.Contains("initializeBingoDateTimePicker", manageScript);
        Assert.DoesNotContain("minuteIncrement: 30", creationScript);
        Assert.DoesNotContain("minuteIncrement: 30", manageScript);
        Assert.DoesNotContain("bingoFiveMinuteTimes", creationScript);
        Assert.DoesNotContain("bingoFiveMinuteTimes", manageScript);
        Assert.Equal(0, Count(creation, "data-date-time-picker"));
        Assert.Equal(5, Count(creation, "data-datetime-control"));
        Assert.Equal(5, Count(creation, "data-datetime-canonical"));
        Assert.Equal(5, Count(creation, "data-datetime-date"));
        Assert.Equal(5, Count(creation, "data-datetime-time"));
        Assert.Contains("class=\"event-create-page schedule-editor-page\"", schedule);
        Assert.Contains("class=\"admin-module-description\"", schedule);
        Assert.Contains("Set the signup, event, and draft times in {0}.", schedule);
        Assert.DoesNotContain("schedule-timezone-note", schedule);
        Assert.Contains("class=\"event-schedule-groups\"", schedule);
        Assert.DoesNotContain("schedule-readiness", schedule);
        Assert.Contains("Reason for retroactive change", schedule);
        Assert.Contains("Required only when changing a time that has already passed.", schedule);
        Assert.Contains("<input asp-for=\"Input.Reason\" class=\"form-control\" />", schedule);
        Assert.DoesNotContain("<textarea asp-for=\"Input.Reason\"", schedule);
        Assert.Contains("class=\"admin-signup-warning-list\"", schedule);
        Assert.Contains("class=\"event-schedule-group-heading\"", schedule);
        Assert.Contains("class=\"event-schedule-groups\"", schedule);
        Assert.Contains("class=\"event-schedule-group schedule-capacity-warning-component\"", schedule);
        Assert.Contains("class=\"schedule-capacity-warning-grid@(hasSignupWarning ? \" has-schedule-warning\" : \"\")\"", schedule);
        Assert.Contains("class=\"admin-field schedule-capacity-label\"", schedule);
        Assert.Contains("class=\"admin-field schedule-warning-label\"", schedule);
        Assert.Contains("class=\"schedule-capacity-control\"", schedule);
        Assert.Contains("class=\"schedule-warning-row\"", schedule);
        Assert.Contains("class=\"event-confirmation-actions schedule-warning-action\"", schedule);
        Assert.Contains(".admin-shell-body .schedule-editor-panel .schedule-capacity-warning-grid { display: grid; grid-template-columns: 1fr; grid-template-areas: \"capacity-label\" \"capacity-control\"; gap: 0.75rem 1.25rem; align-items: stretch; }", siteCss);
        Assert.Contains("grid-template-areas: \"capacity-label warning-label\" \"capacity-control warning-row\" \". warning-action\";", siteCss);
        Assert.Contains(".admin-shell-body .schedule-editor-panel .schedule-capacity-warning-grid .schedule-warning-row { display: grid; grid-template-rows: minmax(0, 1fr) auto; grid-area: warning-row; min-width: 0; }", siteCss);
        Assert.Contains(".admin-shell-body .schedule-editor-panel .schedule-warning-row .admin-signup-warning-list li { box-sizing: border-box; display: flex; align-items: center; }", siteCss);
        Assert.DoesNotContain("schedule-capacity-warning-row", schedule);
        Assert.DoesNotContain("schedule-capacity-warning-row", siteCss);
        Assert.DoesNotContain("event-schedule-groups.has-schedule-warning", siteCss);
        Assert.DoesNotContain("admin-signup-warning-ack", schedule);
        Assert.DoesNotContain("admin-signup-warning-heading", schedule);
        Assert.DoesNotContain("schedule-warning-heading", schedule);
        Assert.DoesNotContain("These warnings must be acknowledged before automatic signup opening can be scheduled.", schedule);
        Assert.DoesNotContain("class=\"event-confirmation-box schedule-confirmation-box\" aria-labelledby=\"schedule-warning-heading\"", schedule);
        Assert.Contains("Model.ScheduledSignupOpeningEnabled", schedule);
        Assert.DoesNotContain("class=\"check-row\"", schedule);
        Assert.DoesNotContain("type=\"checkbox\"", schedule);
        Assert.Contains("event-create-datetime.js", schedule);
        Assert.Equal(5, Count(schedule, "data-datetime-control"));
        Assert.Equal(5, Count(schedule, "data-datetime-canonical"));
        Assert.Equal(5, Count(schedule, "data-datetime-date"));
        Assert.Equal(5, Count(schedule, "data-datetime-time"));
        foreach (var field in new[] { "SignupOpensLocal", "SignupClosesLocal", "DraftLocal", "EventStartsLocal", "EventEndsLocal" })
        {
            Assert.Contains($"<input asp-for=\"Input.{field}\" type=\"datetime-local\" step=\"300\" class=\"form-control event-datetime-canonical\" data-datetime-canonical", creation);
            Assert.Contains($"<input asp-for=\"Input.{field}\" type=\"datetime-local\" step=\"300\" class=\"form-control event-datetime-canonical\" data-datetime-canonical", schedule);
        }
        Assert.DoesNotContain("data-datetime-canonical tabindex=", creation);
        Assert.DoesNotContain("data-datetime-canonical aria-hidden=", creation);
        Assert.DoesNotContain("data-datetime-canonical tabindex=", schedule);
        Assert.DoesNotContain("data-datetime-canonical aria-hidden=", schedule);
        foreach (var field in new[] { "SignupOpensLocal", "SignupClosesLocal", "EventStartsLocal", "EventEndsLocal" })
            Assert.Contains($"#Input_{field}", creationScript);
        Assert.DoesNotContain("data-event-datetime-picker", creation);
        Assert.DoesNotContain("data-date-target", creation);
        Assert.DoesNotContain("data-time-target", creation);
        Assert.DoesNotContain("SignupOpensDate", creation);
        Assert.DoesNotContain("SignupOpensTime", creation);
        Assert.DoesNotContain("SignupClosesDate", creation);
        Assert.DoesNotContain("SignupClosesTime", creation);
        Assert.DoesNotContain("EventStartsDate", creation);
        Assert.DoesNotContain("EventStartsTime", creation);
        Assert.DoesNotContain("EventEndsDate", creation);
        Assert.DoesNotContain("EventEndsTime", creation);
        Assert.DoesNotContain("HalfHourTimes", creation);
        Assert.DoesNotContain("FiveMinuteTimes", creation);
        Assert.Equal(5, Count(creation, "type=\"datetime-local\""));
        Assert.Equal(5, Count(schedule, "type=\"datetime-local\""));
        Assert.DoesNotContain("ScheduleOpening", schedule);
        Assert.Contains("bingoDateTimeHours", siteScript);
        Assert.Contains("bingoDateTimeMinutes", siteScript);
        Assert.Contains("length: 24", siteScript);
        Assert.Contains("length: 12", siteScript);
        Assert.DoesNotContain("bingoFiveMinuteTimes", siteScript);
        Assert.Contains("new(\"Europe/Copenhagen\"", createHandler);
        Assert.Contains("new(\"UTC\"", createHandler);
        Assert.DoesNotContain("new(\"Europe/London\"", createHandler);
    }

    [Fact]
    public void ParticipantsUseSharedDestructiveConfirmationAndCapacityLabelRoles()
    {
        var root = FindRepositoryRoot();
        var participants = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participants.cshtml"));
        var siteCss = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));
        var roadmap = File.ReadAllText(Path.Combine(root, "UI_OVERHAUL_ROADMAP.md"));

        Assert.Contains("<details class=\"admin-destructive-confirmation\">", participants);
        Assert.Contains("role=\"alertdialog\"", participants);
        Assert.Contains("asp-page-handler=\"Withdraw\"", participants);
        Assert.Contains("@T[\"Remove {0}?\", participant.Name]", participants);
        Assert.Contains("@T[\"Cancel\"]", participants);
        Assert.Contains("admin-destructive-confirmation", roadmap);
        Assert.Contains(".admin-shell-body .admin-setting-toggle-copy > strong", siteCss);
        Assert.Contains("font-family: inherit; font-size: 0.75rem; font-weight: 500; line-height: 1.25", siteCss);
        Assert.Contains("border-width: 0.5px", siteCss);
    }

    [Fact]
    public void ParticipantCapacityKeepsWaitingListInOuterRowsAndResetsNarrowly()
    {
        var root = FindRepositoryRoot();
        var participants = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participants.cshtml"));
        var siteCss = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));

        Assert.Contains("<div class=\"participant-shared-row\">", participants);
        Assert.Contains("<strong id=\"waiting-list-enabled-heading\" class=\"participant-shared-row-heading\">@T[\"Waiting list enabled\"]</strong>", participants);
        Assert.Contains("<label class=\"admin-setting-toggle participant-shared-row-content\">", participants);
        Assert.Contains("aria-labelledby=\"waiting-list-enabled-heading\" aria-describedby=\"waiting-list-enabled-support\"", participants);
        Assert.Contains("<span id=\"waiting-list-enabled-support\" class=\"admin-setting-toggle-copy\"><small>@T[\"Keep accepting signups after capacity is reached.\"]</small></span>", participants);
        var sharedRow = participants[participants.IndexOf("<div class=\"participant-shared-row\">", StringComparison.Ordinal)..];
        Assert.True(sharedRow.IndexOf("participant-shared-row-heading", StringComparison.Ordinal) < sharedRow.IndexOf("participant-shared-row-content", StringComparison.Ordinal));
        Assert.Contains(".participant-capacity-region .participant-capacity-field > label { grid-column: 1; grid-row: 1; }", siteCss);
        Assert.Contains(".participant-capacity-region .participant-capacity-field > input { grid-column: 1; grid-row: 2; }", siteCss);
        Assert.Contains(".admin-shell-body .event-participants-page .participant-shared-row { display: contents; }", siteCss);
        Assert.Contains(".admin-shell-body .event-participants-page .participant-shared-row-heading { grid-column: 2; grid-row: 1;", siteCss);
        Assert.Contains(".admin-shell-body .event-participants-page .participant-shared-row-content { display: grid; grid-column: 2; grid-row: 2;", siteCss);
        Assert.DoesNotContain("participant-shared-row-toggle", participants + siteCss);

        var desktopPlacement = siteCss.IndexOf("participant-shared-row-content { display: grid; grid-column: 2; grid-row: 2;", StringComparison.Ordinal);
        var narrowReset = siteCss.IndexOf("@media (max-width: 700px)", desktopPlacement, StringComparison.Ordinal);
        Assert.True(narrowReset > desktopPlacement);
        var narrowCss = siteCss[narrowReset..];
        Assert.Contains(".participant-capacity-region .participant-settings-form .event-create-fields { width: 100%; grid-template-columns: 1fr; grid-template-rows: none; }", narrowCss);
        Assert.Contains(".admin-shell-body .event-participants-page .participant-shared-row { display: grid; grid-template-columns: 1fr; grid-column: auto; grid-row: auto;", narrowCss);
        Assert.Contains(".admin-shell-body .event-participants-page .participant-shared-row-heading,", narrowCss);
        Assert.Contains(".admin-shell-body .event-participants-page .participant-shared-row-content { display: flex;", narrowCss);
    }

    [Fact]
    public void ParticipantActionsKeepRouteFallbackAndShareDesktopDialogContract()
    {
        var root = FindRepositoryRoot();
        var participants = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participants.cshtml"));
        var participantsHandler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participants.cshtml.cs"));
        var questions = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Questions.cshtml"));
        var participant = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participant.cshtml"));
        var participantHandler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participant.cshtml.cs"));
        var participantForm = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "_InternalParticipantForm.cshtml"));
        var adminLayout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminLayout.cshtml"));
        var manageScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "event-manage.js"));
        var questionsScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "signup-questions-overlay.js"));
        var siteCss = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "css", "site.css"));

        Assert.Contains("asp-page=\"Questions\"", participants);
        Assert.Contains("data-signup-questions-trigger=\"true\"", participants);
        Assert.DoesNotContain("data-participant-add-panel", participants);
        Assert.Contains("data-participant-add-trigger", participants);
        Assert.Contains("asp-route-addParticipant=\"1\"", participants);
        Assert.Contains("participant-add-route-page", participants);
        Assert.Contains("participant-edit-action", participants);
        Assert.Contains("@T[\"Participants / Signups\"]", adminLayout);
        Assert.Contains("data-participant-edit-page", participant);
        Assert.Contains("data-participant-edit-close", participant);
        Assert.Contains("data-participants-url", participant);
        Assert.Contains("WebsiteOwner", participant);
        Assert.Contains("WebsiteUsername", participantsHandler);
        Assert.Contains("class=\"participant-primary-value\">@participant.Name</span>", participants);
        Assert.Contains("@(participant.WebsiteUsername ?? T[\"Unlinked\"])", participants);
        Assert.DoesNotContain("<strong>@participant.Name</strong>", participants);
        var participantActionCell = participants[participants.IndexOf("<td class=\"event-row-action participant-actions-cell\"", StringComparison.Ordinal)..];
        Assert.True(participantActionCell.IndexOf("participant-edit-action", StringComparison.Ordinal) < participantActionCell.IndexOf("@if (group.Table == \"current\")", StringComparison.Ordinal));
        Assert.Contains("name=\"overlay\" value=\"@overlayValue\"", participant);
        Assert.Contains("[FromForm] bool overlay", participantHandler);
        Assert.Contains("RedirectToParticipant", participantHandler);
        Assert.Contains("participantEditOverlay", manageScript);
        Assert.Contains("participantEditBase", manageScript);
        Assert.Contains("initializeParticipantEditDialog", manageScript);
        Assert.Contains("initializeOwnerAccountPicker(content);", manageScript);
        Assert.DoesNotContain("establishDirectReturn", manageScript);
        Assert.Contains("loadDirectWorkspace", manageScript);
        Assert.Contains("main.hidden = true", manageScript);
        Assert.Contains("fetch(editorRequestUrl()", manageScript);
        Assert.Contains("data-edit-path", participant);
        Assert.DoesNotContain("participant-status-badge", participant);
        Assert.Contains("<summary class=\"admin-button-secondary\">@T[\"Restore participant\"]</summary>", participant);
        Assert.Contains("<button class=\"admin-button-secondary\" type=\"submit\">@T[\"Confirm restoration\"]</button>", participant);
        Assert.DoesNotContain("admin-button-accent-outline", participant);
        Assert.DoesNotContain("<summary class=\"admin-button-primary\">@T[\"Restore participant\"]</summary>", participant);
        Assert.DoesNotContain("<button class=\"admin-button-primary\" type=\"submit\">@T[\"Confirm restoration\"]</button>", participant);
        Assert.DoesNotContain("<summary class=\"admin-button-secondary action-accent-outline\">@T[\"Restore participant\"]</summary>", participant);
        Assert.DoesNotContain("<button class=\"admin-button-secondary action-accent-outline\" type=\"submit\">@T[\"Confirm restoration\"]</button>", participant);
        Assert.DoesNotContain("participant-secondary-actions-panel\">\n                @if (Model.CanAdminRestore)", participant);
        Assert.Contains("@if (Model.CanAdminWithdraw || Model.CanAdminRestore)", participant);
        Assert.Contains(".admin-shell-body .admin-button-secondary,", siteCss);
        Assert.DoesNotContain(".admin-shell-body .admin-button-accent-outline", siteCss);
        Assert.Contains(".admin-shell-body .participant-confirmation-box > summary { cursor: pointer; }", siteCss);
        Assert.Contains("<tr class=\"participant-table-empty\" hidden=\"@(group.Rows.Count > 0 ? \"hidden\" : null)\"><td colspan=\"8\">", participants);
        Assert.Contains("<form method=\"post\" asp-page-handler=\"Restore\" class=\"event-confirmation-form\" data-update-targets=\"@partialTargets\">", participant);
        Assert.Contains("<form method=\"post\" asp-page-handler=\"FillVacancy\" class=\"form-stack\" data-update-targets=\"@partialTargets\">", participant);
        Assert.Contains("<form method=\"post\" asp-page-handler=\"CompletePromotionFollowUp\" data-update-targets=\"@partialTargets\">", participant);
        Assert.Contains("@media (max-width: 900px)", siteCss);
        Assert.Contains(".admin-shell-body .participant-edit-dialog-page", siteCss);
        Assert.DoesNotContain("Participant lifecycle", participant);
        Assert.Contains("participant-lifecycle-footer", participant);
        Assert.Contains("@T[\"Remove participant\"]", participant);
        Assert.Contains("PrivateWithdrawalNote", participant);
        Assert.Contains("role=\"alertdialog\"", participant);
        Assert.Contains(".participant-admin-page .participant-lifecycle-footer .participant-confirmation-box[open] > .event-confirmation-box", siteCss);
        Assert.Contains("top: 50%;", siteCss);
        Assert.Contains("transform: translate(-50%, -50%);", siteCss);
        Assert.Contains("font-size: 17px;", siteCss);
        Assert.Contains("font-weight: 600;", siteCss);
        Assert.DoesNotContain("<hr", participant);
        Assert.Contains(".admin-shell-body .participant-admin-page .participant-edit-card > footer { padding-top: 0; border-top: 0; }", siteCss);
        Assert.Contains(".admin-shell-body .participant-admin-page .participant-read-only-details > header h2 { color: var(--admin-text); font-family: inherit; font-size: 0.75rem; font-weight: 600; line-height: 1.25; }", siteCss);
        Assert.Contains("font-family: inherit", siteCss[siteCss.LastIndexOf("/* Participant edit reuses", StringComparison.Ordinal)..]);
        Assert.Contains("class=\"event-overview-dates\"", participant);
        Assert.Contains("class=\"event-overview-date-copy\"", participant);
        Assert.DoesNotContain("participant-status-pill @(Model.Status", participant);
        Assert.Contains("data-participant-add-route-trigger hidden", participants);
        Assert.Contains("public bool AddParticipant", participantsHandler);
        Assert.Contains("asp-page-handler=\"CreateInternalParticipant\"", participantForm);
        Assert.Contains("ParticipantSearch", participantForm);
        Assert.Contains("ParticipantPayment", participantForm);
        Assert.Contains("ParticipantTeamId", participantForm);
        Assert.Contains("participant-account-controls", participantForm);
        Assert.Contains("aria-label=\"@T[\"EHB\"]\"", participantForm);
        Assert.Contains("type=\"text\" />", participantForm);
        Assert.Contains("InternalParticipant.OwnerAccountId", participantForm);
        Assert.DoesNotContain("OwnerUsername", participantForm);
        Assert.Contains("OnGetSearchOwnerAccountsAsync", participantsHandler);
        Assert.Contains("item.Active && item.AccountType == AccountType.WebsiteAccount", participantsHandler);
        Assert.Contains("Take(10)", participantsHandler);
        Assert.Contains("InternalParticipant.OwnerAccountId", participantsHandler);
        Assert.DoesNotContain("OwnerUsername", participantsHandler);
        Assert.Contains("data-owner-account-search", participantForm);
        Assert.Contains("data-owner-account-results", participantForm);
        Assert.Contains("data-owner-account-id", participantForm);
        Assert.Contains("data-signup-questions-dialog", adminLayout);
        Assert.Contains("data-signup-questions-content", adminLayout);
        Assert.DoesNotContain("<iframe", adminLayout);
        Assert.Contains("admin-route-dialog", manageScript);
        Assert.Contains("participantAddOverlay", manageScript);
        Assert.Contains("history.back()", manageScript);
        Assert.Contains("window.innerWidth > 900", manageScript);
        Assert.Contains("const routePage = page.querySelector(\".participant-add-route-page\")", manageScript);
        Assert.Contains("const interactionTarget = trigger instanceof HTMLElement ? trigger : routePage", manageScript);
        Assert.Contains("const directRouteFallback = routePage instanceof HTMLElement && trigger?.hidden === true", manageScript);
        Assert.Contains("hide(false, false)", manageScript);
        Assert.Contains("const focusTarget = directRouteFallback && returnToParticipants ? trigger : opener", manageScript);
        Assert.Contains("!focusTarget.hidden", manageScript);
        Assert.True(participants.IndexOf("data-participant-add-route-trigger hidden", StringComparison.Ordinal) < participants.IndexOf("<section class=\"participant-add-route-page\"", StringComparison.Ordinal));
        Assert.Contains("setRouteVisibility(true)", manageScript);
        Assert.Contains("showModal()", manageScript);
        Assert.Contains("initializeOwnerAccountPicker", manageScript);
        Assert.Contains("AbortController", manageScript);
        Assert.Contains("ownerId.value = \"\"", manageScript);
        Assert.Contains("option.role = \"option\"", manageScript);
        Assert.Contains("if (!query) {\n          clearResults();\n          return;\n        }", manageScript);
        Assert.Contains("window.innerWidth > 900", questionsScript);
        Assert.Contains("history.back()", questionsScript);
        Assert.Contains("signup-questions-page-title", questions);
        Assert.Contains("aria-describedby=\"@(Model.IsOverlay ? \"signup-questions-dialog-description\" : null)\"", questions);
        Assert.Contains("aria-label=\"@T[\"Move {0} up\", question.Label]\"", questions);
        Assert.Contains("aria-label=\"@T[\"Move {0} down\", question.Label]\"", questions);
        Assert.Contains("@T[\"Signup access\"]", questions);
        Assert.Contains(".admin-dialog-page-kicker { margin: 0; color: var(--admin-muted);", siteCss);
        Assert.DoesNotContain("class=\"step-kicker\"", questions);
        Assert.Contains("@if (!Model.IsOverlay)", questions);
        Assert.Contains("max-height: min(38rem, calc(100dvh - 7rem))", siteCss);
        Assert.Contains("width: min(41rem, calc(100vw - 3rem))", siteCss);
        Assert.Contains("participant-account-controls { display: grid;", siteCss);
        Assert.Contains("class=\"btn admin-button-create\" type=\"submit\">+ @T[\"Create participant\"]", participantForm);
        Assert.Contains(".admin-shell-body .admin-button-create", siteCss);
        Assert.DoesNotContain("participant-add-dialog-page .admin-dialog-page-actions .admin-button-primary", siteCss);
        Assert.Contains("font-family: inherit; font-size: 0.875rem; font-weight: 600", siteCss);
        Assert.Contains(".participant-add-dialog", siteCss);
    }

    [Fact]
    public void QuestionsMoveAndEditMutationsPreserveSubmittedOverlayState()
    {
        var root = FindRepositoryRoot();
        var questionsHandler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Questions.cshtml.cs"));
        var move = questionsHandler[questionsHandler.IndexOf("OnPostMoveAsync", StringComparison.Ordinal)..questionsHandler.IndexOf("OnPostEditAsync", StringComparison.Ordinal)];
        var edit = questionsHandler[questionsHandler.IndexOf("OnPostEditAsync", StringComparison.Ordinal)..questionsHandler.IndexOf("OnPostReplaceAsync", StringComparison.Ordinal)];

        Assert.Contains("[FromForm] bool overlay", move);
        Assert.Contains("Question order saved.", move);
        Assert.Contains("RedirectToQuestions(id, overlay)", move);
        Assert.Contains("[FromForm] bool overlay", edit);
        Assert.Contains("Question saved.", edit);
        Assert.Contains("RedirectToQuestions(id, overlay)", edit);
        Assert.Contains("Overlay = overlay;", questionsHandler);
        Assert.Contains("overlay = (overlay ?? Overlay) ? \"1\" : null", questionsHandler);
        Assert.Contains("ModelState.Remove(\"overlay\")", questionsHandler);
    }

    [Theory]
    [InlineData("scheduled action", "scheduledBlockerCount == 0", "event-control-status", ".event-manage-page .event-admin-controls > .event-admin-control-group > .event-control-status")]
    [InlineData("pre-live signup", "showSignupControls", "event-signup-group", ".event-manage-page .event-signup-group { display: grid; grid-template-columns: minmax(0, 1fr) auto;")]
    [InlineData("signup-open schedule", "Automatic signup opening scheduled for:", "event-control-supporting", ".event-manage-page .event-signup-group")]
    [InlineData("signup-closed start", "showStartControl", "PrepareStartConfirmation", ".event-manage-page .event-admin-event-actions")]
    [InlineData("live controls", "showLiveControls", "PrepareEndConfirmation", ".event-manage-page .event-admin-event-actions")]
    [InlineData("final review", "showFinalReviewControls", "ReopenSubmissions", ".event-manage-page .event-reopen-form > button")]
    [InlineData("resume subsection", "event-control-subsection", "PrepareResumeConfirmation", ".event-manage-page .event-control-subsection")]
    [InlineData("verification codes", "event-admin-code-group", "event-code-toggle-form", ".event-manage-page .event-code-toggle-form")]
    [InlineData("terminal", "eventView.State == EventState.Cancelled", "Event cancelled", ".event-manage-page .event-admin-controls > .event-admin-control-group > .event-control-status")]
    [InlineData("Wise Old Man", "integrationEditable", "event-wom-form", ".event-manage-page .event-wom-form")]
    [InlineData("danger zone", "showDestructivePreLiveControl", "event-danger-zone-container", ".event-manage-page .event-danger-zone-form")]
    public void ManageEventControlBranchesKeepRightOwnedActions(string branch, string branchMarker, string markupMarker, string cssMarker)
    {
        var repositoryRoot = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var siteCss = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "css", "site.css"));

        Assert.True(manage.Contains(branchMarker, StringComparison.Ordinal), $"{branch}: missing branch marker");
        Assert.Contains(markupMarker, manage);
        Assert.Contains(cssMarker, siteCss);
        Assert.Contains("<h3>@T[\"Manual signup control\"]</h3>", manage);
        Assert.Contains("class=\"event-control-status event-control-supporting\"", manage);
        Assert.DoesNotContain("event-signup-primary", manage);
        Assert.Contains(".event-manage-page .event-admin-controls .event-create-cancel { grid-column: auto; grid-row: auto;", siteCss);
        Assert.Contains(".event-manage-page .event-admin-event-actions { width: fit-content; max-width: 100%; justify-self: end; align-self: center; align-items: center; }", siteCss);
        Assert.Contains(".event-manage-page .event-evidence-actions { align-items: flex-start; }", siteCss);
        Assert.Contains("                        </div>\n                    </section>\n\n                    @if (Model.SignupReadiness", manage);
        Assert.DoesNotContain("                        }\n                    </section>\n\n                    @if (Model.SignupReadiness", manage);
        var startActions = manage.IndexOf("@if (showStartControl)", StringComparison.Ordinal);
        Assert.True(startActions >= 0);
        Assert.True(manage.IndexOf("Model.StartReadiness?.Blockers.Count > 0", startActions, StringComparison.Ordinal) < manage.IndexOf("asp-page-handler=\"PrepareStartConfirmation\"", startActions, StringComparison.Ordinal));
    }

    [Fact]
    public void ManageSignupConfirmationUsesConditionalProposalWarningsAndCompactActionPlacement()
    {
        var repositoryRoot = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var manageHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs"));
        var siteCss = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "css", "site.css"));

        Assert.Contains("<p class=\"event-control-status event-control-supporting is-yellow\">@T[\"Confirmation required\"]</p>", manage);
        Assert.Contains("class=\"admin-signup-warning-ack\"", manage);
        Assert.Contains("class=\"admin-signup-warning-list\"", manage);
        Assert.Contains("class=\"event-control-copy\"", manage);
        Assert.DoesNotContain("admin-signup-warning-heading", manage);
        Assert.Contains("@foreach (var warning in Model.SignupReadiness!.Warnings)", manage);
        Assert.True(manage.IndexOf("@warning.Description", StringComparison.Ordinal) < manage.IndexOf("@T[\"Confirm warnings\"]", StringComparison.Ordinal));
        Assert.Contains("@if (Model.SignupCloseRequiresAcceptance)", manage);
        Assert.Contains("Model.EventDate(Model.SignupReadiness!.CloseDecision.ProposedClose)", manage);
        Assert.Contains("SignupCloseRequiresAcceptance => (EventView?.State is EventState.Draft or EventState.SignupClosed) && SignupReadiness?.CloseDecision.RequiresAcceptance == true", manageHandler);
        Assert.DoesNotContain("eventView.State is EventState.Draft or EventState.SignupClosed) && !Model.SignupCloseAcknowledged", manage);
        Assert.DoesNotContain(".event-manage-page .event-signup-group > .event-confirmation-box { grid-column: 1 / -1;", siteCss);
        Assert.Contains(".event-manage-page .event-signup-group .event-admin-event-actions { display: grid;", siteCss);
        Assert.Contains(".event-manage-page .event-code-history h3 { margin-bottom: 0.55rem; }", siteCss);
        Assert.DoesNotContain(".admin-shell-body .event-create-cancel { display: inline-block;", siteCss);
        Assert.Contains(".admin-shell-body .event-create-page .event-create-cancel { display: inline-flex;", siteCss);
        Assert.Contains(".event-manage-page .event-signup-group .event-create-cancel { display: inline-flex;", siteCss);
        Assert.Contains(".event-manage-page .event-signup-group .admin-signup-warning-ack { width: min(28rem, 100%); }", siteCss);
    }

    [Fact]
    public void SignupCloseDecisionRequiresAcceptanceOnlyForAnInvalidCloseWithAProposal()
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var valid = SignupCloseDecision.Evaluate(now.AddDays(1), now.AddHours(2), now.AddDays(2), now);
        var proposed = SignupCloseDecision.Evaluate(null, now.AddDays(1), now.AddDays(2), now);
        var unavailable = SignupCloseDecision.Evaluate(null, null, now.AddMinutes(-1), now);

        Assert.False(valid.RequiresAcceptance);
        Assert.True(proposed.RequiresAcceptance);
        Assert.Equal(now.AddDays(1), proposed.ProposedClose);
        Assert.False(unavailable.RequiresAcceptance);
        Assert.Null(unavailable.ProposedClose);
    }

    [Fact]
    public void ManageOverviewUsesFourStableLifecycleAwareMetrics()
    {
        var repositoryRoot = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var manageHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs"));

        Assert.Equal(4, Count(manage, "class=\"event-overview-metric\""));
        Assert.Equal(4, Count(manage, "class=\"event-overview-metric-header\""));
        Assert.Equal(4, Count(manage, "class=\"event-overview-metric-body\""));
        Assert.Equal(4, Count(manage, "class=\"event-overview-metric-value-row\""));
        Assert.Contains("participantPhase ? T[\"Participants\"] : T[\"Teams & roster\"]", manage);
        Assert.Contains("T[nextMilestone.Label]", manage);
        Assert.Contains("T[\"Submissions\"]", manage);
        Assert.Contains("event-overview-metric-context", manage);
        Assert.Contains("event-overview-metric-dot", manage);
        Assert.Contains("event-overview-metric-arrow", manage);
        Assert.Contains("EventDateOnly(nextMilestone.At", manage);
        Assert.DoesNotContain("EventTime(nextMilestone.At", manage);
        Assert.Contains("milestoneDotClass", manage);
        Assert.Contains("milestoneContext", manage);
        Assert.Contains("reviewedSubmissionCount", manage);
        Assert.Contains("peopleDotClass", manage);
        Assert.Contains("boardDotClass", manage);
        Assert.Contains("submissionDotClass", manage);
        Assert.Contains("M7 17 17 7M7 7h10v10", manage);
        Assert.DoesNotContain("event-overview-progress", manage);
        Assert.Contains("NextMilestoneFor(eventView)", manage);
        Assert.Contains("EventState.Draft => new(\"Signup opens\"", manageHandler);
        Assert.Contains("EventState.SignupOpen => new(\"Signup closes\"", manageHandler);
        Assert.Contains("EventState.SignupClosed => new(\"Event starts\"", manageHandler);
        Assert.Contains("EventState.Live => new(\"Event ends\"", manageHandler);
        Assert.Contains("EventState.AwaitingFinalReview => new(\"Submission cutoff\"", manageHandler);
        Assert.DoesNotContain("OverviewMetricProfile", manageHandler);
    }

    [Fact]
    public void ManageImportantInformationUsesOneEffectiveTimeline()
    {
        var now = new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
        var rows = ManageModel.EffectiveTimelineFor(new(
            SignupOpensAt: now.AddHours(1),
            SignupClosesAt: now.AddHours(2),
            DraftAt: now.AddHours(3),
            EventStartsAt: now.AddHours(4),
            EventEndsAt: now.AddHours(5),
            ActualSignupOpenedAt: now.AddHours(-5),
            ActualSignupClosedAt: now.AddHours(-4),
            ActualStartedAt: now.AddHours(-3),
            ActualEndedAt: now.AddHours(-2),
            SubmissionCutoffAt: now.AddHours(6),
            SubmissionsClosedAt: now.AddHours(-1),
            CancelledAt: null));

        Assert.Contains(rows, row => row.Label == "Signups opened");
        Assert.Contains(rows, row => row.Label == "Signups closed");
        Assert.Contains(rows, row => row.Label == "Event started");
        Assert.Contains(rows, row => row.Label == "Event ended");
        Assert.Contains(rows, row => row.Label == "Submissions closed");
        Assert.Contains(rows, row => row.Label == "Draft time");
        foreach (var scheduledLabel in new[] { "Signup opens", "Signup closes", "Event starts", "Event ends", "Submission cutoff" })
            Assert.DoesNotContain(rows, row => row.Label == scheduledLabel);
        Assert.DoesNotContain(rows, row => row.Label.Contains("Not yet", StringComparison.Ordinal));

        var cancelledRows = ManageModel.EffectiveTimelineFor(new(
            SignupOpensAt: now.AddHours(1),
            SignupClosesAt: now.AddHours(2),
            DraftAt: now.AddHours(-4),
            EventStartsAt: now.AddHours(3),
            EventEndsAt: now.AddHours(4),
            ActualSignupOpenedAt: now.AddHours(-6),
            ActualSignupClosedAt: null,
            ActualStartedAt: null,
            ActualEndedAt: null,
            SubmissionCutoffAt: now.AddHours(5),
            SubmissionsClosedAt: null,
            CancelledAt: now));

        Assert.Contains(cancelledRows, row => row.Label == "Signups opened");
        Assert.Contains(cancelledRows, row => row.Label == "Draft time");
        Assert.Contains(cancelledRows, row => row.Label == "Event cancelled" && row.At == now);
        foreach (var futureLabel in new[] { "Signup closes", "Event starts", "Event ends", "Submission cutoff" })
            Assert.DoesNotContain(cancelledRows, row => row.Label == futureLabel);
    }

    [Theory]
    [InlineData("SCHEDULE_INVALID", "/Admin/Events/Schedule/{0}", "Review schedule")]
    [InlineData("SIGNUP_FORM_MISSING", "/Admin/Events/Questions/{0}", "Review signup form")]
    [InlineData("SIGNUP_QUESTIONS_INVALID", "/Admin/Events/Questions/{0}", "Review signup form")]
    [InlineData("SIGNUP_CODE_UNUSABLE", "/Admin/Events/Questions/{0}", "Review signup form")]
    [InlineData("BOARD_NOT_PUBLISHED", "/Admin/Events/Board/{0}", "Review board")]
    [InlineData("DRAFT_NOT_FINALIZED", "/Admin/Events/Draft/{0}", "Review teams and draft")]
    [InlineData("TEAM_ACCESS_MISSING", "/Admin/Events/Draft/{0}", "Review teams and draft")]
    [InlineData("CURRENT_EVENT_EXISTS", "/Admin/Events", "Review events")]
    [InlineData("EVENT_WINDOW_OVERLAP", "/Admin/Events", "Review events")]
    public void ManageBlockerMappingUsesTheRealResolutionRoute(string code, string routeTemplate, string label)
    {
        var eventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var resolved = ManageModel.ResolveBlocker(new ReadinessItem(code, "test"), eventId, EventState.SignupClosed);

        Assert.Equal(routeTemplate.Replace("{0}", eventId.ToString()), resolved.Route);
        Assert.Equal(label, ManageModel.BlockerActionLabel(resolved));
    }

    [Fact]
    public void ManageParticipantPlayingMappingPreservesItsParticipantCorrectionRoute()
    {
        var eventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var participantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var route = $"/Admin/Events/Participant/{eventId}/Participants/{participantId}";
        var resolved = ManageModel.ResolveBlocker(new ReadinessItem("PARTICIPANT_PLAYING_ASSIGNMENT_INVALID", "test", route), eventId, EventState.SignupClosed);

        Assert.Equal(route, resolved.Route);
        Assert.Equal("Review participants", ManageModel.BlockerActionLabel(resolved));
    }

    private static int Count(string value, string search)
        => value.Split(search, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
