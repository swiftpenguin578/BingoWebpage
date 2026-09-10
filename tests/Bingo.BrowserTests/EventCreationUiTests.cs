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
        var createHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Create.cshtml.cs"));
        var creationScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-create.js"));
        var dateTimeScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-create-datetime.js"));
        var validationScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-create-validation.js"));
        var identity = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml"));
        var identityHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml.cs"));
        var manage = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var manageHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs"));

        foreach (var handler in new[] { createHandler, identityHandler, manageHandler })
            Assert.Contains("[Authorize(Policy = AuthorizationPolicies.Admin)]", handler);

        Assert.Contains("@page", creation);
        Assert.Contains("<form method=\"post\" enctype=\"multipart/form-data\"", creation);
        Assert.Contains("asp-validation-summary=\"ModelOnly\"", creation);
        Assert.Contains("<nav class=\"event-create-steps\" aria-label=\"@T[\"Event setup steps\"]\">", creation);

        var createPanels = new[]
        {
            (Index: 0, Id: "event-details-panel"),
            (Index: 1, Id: "schedule-panel"),
            (Index: 2, Id: "signup-panel"),
            (Index: 3, Id: "planning-panel"),
            (Index: 4, Id: "review-panel")
        };

        foreach (var panel in createPanels)
        {
            Assert.Contains($"data-create-step=\"{panel.Index}\" aria-controls=\"{panel.Id}\"", creation);
            Assert.Contains($"id=\"{panel.Id}\" class=\"event-create-panel\" data-create-panel=\"{panel.Index}\"", creation);
        }

        Assert.Equal(5, Count(creation, "data-create-step=\""));
        Assert.Equal(5, Count(creation, "data-create-panel=\""));
        Assert.Contains("asp-validation-for=\"Input.Name\"", creation);
        Assert.Contains("aria-describedby=\"Input_Name-error\" data-val-required=\"Required\"", creation);
        Assert.Contains("data-create-previous", creation);
        Assert.Contains("data-create-next", creation);
        Assert.Contains("data-create-submit hidden", creation);
        Assert.Contains("<noscript><button", creation);

        foreach (var field in new[]
                 {
                     "SignupOpensLocal",
                     "SignupClosesLocal",
                     "DraftLocal",
                     "EventStartsLocal",
                     "EventEndsLocal"
                 })
        {
            Assert.Contains(
                $"<input asp-for=\"Input.{field}\" type=\"datetime-local\" step=\"300\" class=\"form-control event-datetime-canonical\" data-datetime-canonical",
                creation);
        }

        Assert.Equal(5, Count(creation, "data-datetime-control"));
        Assert.Equal(5, Count(creation, "data-datetime-canonical"));
        Assert.Equal(5, Count(creation, "data-datetime-date"));
        Assert.Equal(5, Count(creation, "data-datetime-time"));
        Assert.DoesNotContain("data-datetime-canonical tabindex=", creation);
        Assert.DoesNotContain("data-datetime-canonical aria-hidden=", creation);

        Assert.Contains("<script src=\"~/js/event-create-datetime.js\" asp-append-version=\"true\"></script>", creation);
        Assert.Contains("<script src=\"~/js/event-create-validation.js\" asp-append-version=\"true\"></script>", creation);
        Assert.Contains("<script src=\"~/js/event-create.js\" asp-append-version=\"true\"></script>", creation);
        Assert.Contains("step.toggleAttribute(\"aria-current\", active)", creationScript);
        Assert.Contains("window.bingoEventCreateValidation.navigateForward", creationScript);
        Assert.Contains("window.bingoEventCreateValidation.validateCurrentStep", creationScript);
        Assert.Contains("window.bingoEventCreateValidation.validateAndReveal", creationScript);
        Assert.Contains("validator.settings.ignore = \":hidden:not([name])\";", validationScript);
        Assert.Contains("invalid?.closest(\"[data-create-panel]\")", validationScript);
        Assert.Contains("target.focus({ preventScroll: true })", validationScript);
        Assert.Contains("canonical.value = date.value && time.value ? `${date.value}T${time.value}` : \"\";", dateTimeScript);
        Assert.Contains("canonical.tabIndex = -1;", dateTimeScript);
        Assert.Contains("canonical.setAttribute(\"aria-hidden\", \"true\");", dateTimeScript);

        Assert.Contains("public async Task<IActionResult> OnPostAsync(CancellationToken ct)", createHandler);
        Assert.Contains("if (!ModelState.IsValid) return Page();", createHandler);
        Assert.Contains("BeginTransactionAsync(IsolationLevel.Serializable, ct)", createHandler);
        Assert.Contains("item.ConfigureInitialSchedule(", createHandler);
        Assert.Contains("item.ConfigureSignup(", createHandler);
        Assert.Contains("item.ConfigurePlanning(", createHandler);
        Assert.Contains("\"event.created\"", createHandler);
        Assert.Contains("return RedirectToPage(\"Manage\", new { id = item.Id });", createHandler);

        Assert.Contains("@page \"{id:guid}\"", identity);
        Assert.Contains("<form method=\"post\" enctype=\"multipart/form-data\"", identity);
        Assert.Contains("asp-validation-summary=\"ModelOnly\"", identity);
        Assert.Contains("<input asp-for=\"Input.Version\" type=\"hidden\" />", identity);

        foreach (var field in new[] { "Name", "Slug", "Timezone", "Description", "Banner" })
            Assert.Contains($"asp-for=\"Input.{field}\"", identity);

        Assert.Contains("readonly=\"@(Model.IsSlugLocked || Model.EventState == EventState.Live ? \"readonly\" : null)\"", identity);
        Assert.Contains("disabled=\"@(Model.EventState == EventState.Live ? \"disabled\" : null)\"", identity);
        Assert.Contains("asp-validation-for=\"Input.ConfirmTimezoneChange\"", identity);
        Assert.Contains("name=\"Input.ConfirmTimezoneChange\" value=\"true\"", identity);
        Assert.Contains("accept=\"image/png,image/jpeg,image/webp\" aria-describedby=\"identity-banner-help\"", identity);
        Assert.Contains("form=\"identity-remove-banner-form\" aria-label=\"@T[\"Remove current banner\"]\"", identity);
        Assert.Contains("<form id=\"identity-remove-banner-form\" method=\"post\" asp-page-handler=\"RemoveBanner\"", identity);
        Assert.Contains("<input asp-for=\"BannerVersion\" type=\"hidden\" />", identity);

        Assert.Contains("Url.Page(\"/Events/Signup\"", identity);
        Assert.Contains("Url.Page(\"/Events/Signups\"", identity);
        Assert.Contains("Url.Page(\"/Events/Board\"", identity);
        Assert.Contains("@if (Model.ShowPublicBoard)", identity);
        Assert.Contains("aria-label=\"@T[\"Copy signup form link\"]\"", identity);
        Assert.Contains("aria-label=\"@T[\"Copy public signup table link\"]\"", identity);
        Assert.Contains("aria-label=\"@T[\"Copy public board link\"]\"", identity);

        Assert.Contains("if (Input.Version != item.Version)", identityHandler);
        Assert.Contains("item.FirstPublicAt is not null && !Input.ConfirmTimezoneChange", identityHandler);
        Assert.Contains("if (item.State == EventState.Live)", identityHandler);
        Assert.DoesNotContain("TimezoneReason", identity);
        Assert.DoesNotContain("TimezoneReason", identityHandler);
        Assert.Contains("catch (DbUpdateConcurrencyException)", identityHandler);
        Assert.Contains("public async Task<IActionResult> OnPostRemoveBannerAsync", identityHandler);
        Assert.Contains("if (BannerVersion != item.Version)", identityHandler);
        Assert.Contains("\"event.identity_updated\"", identityHandler);
        Assert.Contains("return RedirectToPage(\"Manage\", new { id });", identityHandler);

        Assert.Contains("@page \"{id:guid}\"", manage);
        Assert.Contains("aria-label=\"@T[\"Operational overview\"]\" data-manage-overview", manage);
        Assert.Contains("href=\"@blocker.Route\"", manage);
        Assert.Contains("data-update-targets=\"@partialUpdateTargets\"", manage);
        Assert.Contains("data-confirmation-box tabindex=\"-1\"", manage);

        foreach (var prepareHandler in new[]
                 {
                     "PrepareStartConfirmation",
                     "PrepareEndConfirmation",
                     "PrepareResumeConfirmation",
                     "PrepareDestructiveConfirmation"
                 })
        {
            Assert.Contains($"asp-page-handler=\"{prepareHandler}\"", manage);
        }

        foreach (var confirmation in new[]
                 {
                     (Handler: "StartEvent", Name: "ConfirmStartEvent"),
                     (Handler: "EndEvent", Name: "ConfirmEndEvent"),
                     (Handler: "ResumeEvent", Name: "ConfirmResumeEvent")
                 })
        {
            Assert.Contains($"asp-page-handler=\"{confirmation.Handler}\"", manage);
            Assert.Contains($"name=\"{confirmation.Name}\" value=\"true\"", manage);
        }

        Assert.Contains("asp-for=\"ReplacementEventEndsAtLocal\"", manage);
        Assert.Contains("<textarea asp-for=\"ResumeReason\" class=\"form-control\" rows=\"3\" required>", manage);
        Assert.Contains("aria-labelledby=\"resume-event-heading\"", manage);
        Assert.Contains("id=\"resume-event-heading\"", manage);

        Assert.Contains("asp-page-handler=\"Discard\"", manage);
        Assert.Contains("asp-page-handler=\"Cancel\"", manage);
        Assert.Equal(2, Count(manage, "name=\"ConfirmDestructiveAction\" value=\"true\""));
        Assert.Contains("<textarea asp-for=\"CancellationReason\" class=\"form-control\" rows=\"2\" required>", manage);
        Assert.Contains("aria-labelledby=\"event-danger-heading\"", manage);
        Assert.Contains("id=\"event-danger-heading\"", manage);

        Assert.Contains("asp-page-handler=\"Competition\"", manage);
        Assert.Contains("<input asp-for=\"EventVersion\" type=\"hidden\" />", manage);
        Assert.Contains("asp-for=\"CompetitionId\"", manage);
        Assert.Contains("<select asp-for=\"SynchronizeCompetitionSchedule\"", manage);
        Assert.Contains("asp-for=\"ConfirmCompetitionSchedule\"", manage);

        Assert.Contains("eventLifecycle.StartNowAsync(id, EventVersion, ConfirmStartEvent, StartReason, Actor, ct)", manageHandler);
        Assert.Contains("eventLifecycle.EndNowAsync(id, EventVersion, ConfirmEndEvent, EndReason, Actor, ct)", manageHandler);
        Assert.Contains("eventLifecycle.ResumePrematureEndAsync(id, EventVersion, ConfirmResumeEvent, ResumeReason, replacementEnd, Actor, ct)", manageHandler);
        Assert.Contains("destructiveLifecycle.DiscardAsync(id, EventVersion, ConfirmDestructiveAction, Actor, ct)", manageHandler);
        Assert.Contains("destructiveLifecycle.CancelAsync(id, EventVersion, ConfirmDestructiveAction, CancellationReason, Actor, ct)", manageHandler);
        Assert.Contains("ConfigureAsync(id, EventVersion, CompetitionId, SynchronizeCompetitionSchedule, Actor, ConfirmCompetitionSchedule, ct)", manageHandler);
        Assert.Contains("private LifecycleActor Actor => new(User.GetAccountId()!.Value, User.Identity!.Name!);", manageHandler);
        Assert.Contains("OverviewBlockers = overviewBlockers.DistinctBy", manageHandler);

        foreach (var route in new[] { "Identity", "Schedule", "Draft", "Board", "Questions", "Manage" })
            Assert.Contains($"$\"/Admin/Events/{route}/{{eventId}}\"", manageHandler);
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
        var signup = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "Signup.cshtml"));
        var adminLayout = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminLayout.cshtml"));
        var manageScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "event-manage.js"));
        var questionsScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "signup-questions-overlay.js"));
        var siteCss = BrowserTestFiles.ReadActiveStyles(root);

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
        var participantActionStart = participants.IndexOf("<td class=\"event-row-action participant-actions-cell\"", StringComparison.Ordinal);
        var participantActionEnd = participants.IndexOf("</td>", participantActionStart, StringComparison.Ordinal);
        Assert.True(participantActionStart >= 0 && participantActionEnd > participantActionStart);
        var participantActionCell = participants[participantActionStart..participantActionEnd];
        Assert.Contains("class=\"participant-edit-action\"", participantActionCell);
        Assert.Contains("asp-page=\"Participant\" asp-route-id=\"@eventView.Id\" asp-route-participantId=\"@participant.Id\"", participantActionCell);
        Assert.Contains("@if (group.Table == \"current\" && eventView.State != EventState.Live)", participantActionCell);
        Assert.Contains("@if (eventView.CanEditParticipant)", participantActionCell);
        Assert.DoesNotContain("Manage live participant", participants);
        Assert.Contains("name=\"overlay\" value=\"@overlayValue\"", participant);
        Assert.Contains("[FromForm] bool overlay", participantHandler);
        Assert.Contains("RedirectToParticipant", participantHandler);
        Assert.Contains("CanEditPrivateMetadata", participantHandler);
        Assert.Contains("CanTransferOwnership", participantHandler);
        Assert.Contains("ConfirmOwnershipTransfer", participant);
        Assert.Contains("participant-ownership-confirmation", participant);
        Assert.Contains("participantEditOverlay", manageScript);
        Assert.Contains("participantEditBase", manageScript);
        Assert.Contains("initializeParticipantEditDialog", manageScript);
        Assert.Equal(2, manageScript.Split("initializeOwnerAccountPicker(currentEditor);", StringSplitOptions.None).Length - 1);
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
        Assert.Contains("<tr class=\"participant-table-empty\" hidden=\"@(group.Rows.Count > 0 ? \"hidden\" : null)\"><td colspan=\"9\">", participants);
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
        Assert.Contains("type=\"text\" placeholder=\"@T[\"Not answered\"]\" data-co-captain-input=\"true\" disabled=", participant);
        Assert.Contains("class=\"signup-sheet__input\" type=\"text\" value=\"@Model.Input.Answers.GetValueOrDefault(question.Id)\" data-co-captain-input", signup);
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
        Assert.DoesNotContain("window.innerWidth > 900", manageScript);
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
        Assert.DoesNotContain("window.innerWidth > 900", questionsScript);
        Assert.Contains("if (!dialog.open) dialog.showModal();", questionsScript);
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
        var siteCss = BrowserTestFiles.ReadActiveStyles(repositoryRoot);

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
