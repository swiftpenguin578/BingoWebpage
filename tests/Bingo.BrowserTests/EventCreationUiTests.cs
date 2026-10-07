using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.BrowserTests;

[Collection(BrowserTestGroup.Name)]
public sealed class EventCreationUiTests
{
    private readonly BrowserTestApplicationFactory factory;
    private readonly HttpClient client;

    public EventCreationUiTests(BrowserTestApplicationFactory factory)
    {
        this.factory = factory;
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
        Assert.Equal(System.Net.HttpStatusCode.NotFound, banner.StatusCode);
        Assert.Null(banner.Headers.Location);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, participants.StatusCode);
        Assert.Equal("/Account/Login", participants.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, participantsPost.StatusCode);
        Assert.Equal("/Account/Login", participantsPost.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public void ScheduleEndUsesTheCanonicalDateTimeAdapterAndOneSharedConfirmation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var schedule = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Schedule.cshtml"));
        var scheduleHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Schedule.cshtml.cs"));

        // OS-1 / RC03: Schedule uses the shared reference .dtp; legacy consumers retain their adapter.
        Assert.Contains("data-date-time=", schedule);
        Assert.Contains("admin-schedule.js", schedule);
        Assert.DoesNotContain("event-create-datetime.js", schedule);
        var module = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "admin-schedule.js"));
        Assert.Contains("mountDateTime", module);
        Assert.Contains("ui.openLayer", module);
        Assert.Contains("data.set('Input.EventEndReason',reason)", module);
        Assert.Contains("schedules.SaveScheduleAsync(id, Input.Version, values, Input.ConfirmChanges", scheduleHandler);
        Assert.Contains("Input.EventEndReason", scheduleHandler);
    }

    [Fact]
    public void RetiredConfirmSignupHandlerIsAbsentFromTheManagePageHandlerDescriptor()
    {
        var repositoryRoot = FindRepositoryRoot();
        var manageHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs"));
        Assert.Contains("BadRequest(Localize(\"The old signup confirmation handler is retired.", manageHandler);
        Assert.DoesNotContain("signupLifecycle.CloseAsync(id, EventVersion, Actor, ct)", manageHandler);

        var descriptor = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<CompiledPageActionDescriptor>()
            .Single(item => item.ViewEnginePath == "/Admin/Events/Manage");

        Assert.DoesNotContain(descriptor.HandlerMethods, method => method.HttpMethod == "POST" && method.Name == "ConfirmSignup");
    }

    [Fact]
    public void CreationAndIdentityMarkupKeepTheMinimalCreationAndNativeRouteBoundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var creation = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Create.cshtml"));
        var createHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Create.cshtml.cs"));
        var creationService = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Infrastructure", "Events", "EventCreationService.cs"));
        var identity = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml"));
        var identityScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-identity.js"));
        var identityHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml.cs"));
        var manage = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var lifecycleConfirmScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "admin-lifecycle-confirm.js"));
        var manageHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs"));

        foreach (var handler in new[] { createHandler, identityHandler, manageHandler })
            Assert.Contains("[Authorize(Policy = AuthorizationPolicies.Admin)]", handler);

        Assert.Contains("@page", creation);
        Assert.Contains("<form method=\"post\" class=\"event-create-form\">", creation);
        Assert.Contains("asp-validation-summary=\"ModelOnly\"", creation);
        Assert.DoesNotContain("event-create-steps", creation);
        Assert.DoesNotContain("data-create-panel", creation);
        Assert.DoesNotContain("enctype=\"multipart/form-data\"", creation);
        Assert.Contains("asp-validation-for=\"Input.Name\"", creation);
        Assert.Contains("asp-for=\"Input.Timezone\"", creation);
        Assert.Contains("Europe/Copenhagen", creation);
        Assert.Contains("empty 5 × 5 board", creation);
        Assert.DoesNotContain("Input.Slug", creation);
        Assert.DoesNotContain("Input.Description", creation);
        Assert.DoesNotContain("Input.Banner", creation);
        Assert.DoesNotContain("SignupOpensLocal", creation);

        Assert.Contains("public async Task<IActionResult> OnPostAsync(CancellationToken ct)", createHandler);
        Assert.Contains("ContainsRetiredWizardInputAsync", createHandler);
        Assert.Contains("BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)", creationService);
        Assert.Contains("item.ConfigureSignup(", creationService);
        Assert.Contains("new Board(Guid.NewGuid(), item.Id, \"Main board\", 5, 5)", creationService);
        Assert.Contains("DefaultQuestions(form.Id, item.Id)", creationService);
        Assert.Contains("IX_events_slug", creationService);
        Assert.DoesNotContain("ConfigureInitialSchedule(", createHandler);
        Assert.DoesNotContain("ConfigurePlanning(", createHandler);
        Assert.DoesNotContain("storage.", createHandler);
        Assert.DoesNotContain("competitionClient.", createHandler);
        Assert.Contains("\"event.created\"", creationService);
        Assert.Contains("return RedirectToPage(\"Manage\", new { id = result.EventId });", createHandler);
        Assert.Contains("creation.CreateAsync(Input.RequestId, Input.Name, Input.Timezone,", createHandler);

        Assert.Contains("@page \"{id:guid}\"", identity);
        Assert.Contains("<form method=\"post\" class=\"identity-editor-form\">", identity);
        // A10 / brief60 item1: the shared banner retains model-level server errors.
        Assert.Contains("<partial name=\"_AdminDesignBanner\"", identity);
        Assert.Contains("new AdminDesignBanner(\"is-error\", \"error\", string.Join(\" \", ViewData.ModelState[string.Empty]?.Errors.Select(error => error.ErrorMessage) ?? [])", identity);
        Assert.Contains("Hidden: !(ViewData.ModelState[string.Empty]?.Errors.Any() ?? false)", identity);
        Assert.Contains("<input asp-for=\"Input.Version\" type=\"hidden\" />", identity);
        Assert.Contains("asp-for=\"Input.Name\"", identity);
        Assert.Contains("@T[\"AdminDesign.Event link\"]", identity);
        Assert.Contains("asp-for=\"Input.Timezone\"", identity);
        Assert.Contains("asp-for=\"Input.Description\"", identity);
        Assert.Contains("asp-for=\"Input.BuyInDescription\"", identity);
        Assert.DoesNotContain("asp-for=\"Input.Slug\"", identity);
        Assert.DoesNotContain("asp-for=\"Input.Banner\"", identity);
        Assert.DoesNotContain("identity-remove-banner-form", identity);
        Assert.Contains("data-identity-editor", identity);
        Assert.Contains("data-identity-timezone-preview", identity);
        Assert.DoesNotContain("data-identity-cancel", identity);
        Assert.Contains("event-identity.js", identity);
        Assert.Contains("ui.openLayer", identityScript);
        Assert.Contains("ui.registerDraft", identityScript);
        Assert.DoesNotContain("history.pushState", identityScript);
        Assert.Contains("root.replaceWith(nextRoot)", identityScript);
        Assert.Contains("window.AdminFetch.request", identityScript);
        Assert.DoesNotContain("event-confirmation-box identity-timezone-confirmation", identity);
        Assert.Contains("UTC fallback", identity);
        Assert.Contains("@if (Model.CanEditTimezone)", identity);
        Assert.Contains("<partial name=\"_AdminDesignFieldError\" model='new AdminDesignFieldError(\"ConfirmTimezoneChange\", ViewData.ModelState[\"Input.ConfirmTimezoneChange\"]?.Errors.FirstOrDefault()?.ErrorMessage ?? \"\")' />", identity);
        Assert.Contains("data.set('Input.ConfirmTimezoneChange', 'true')", identityScript);

        Assert.DoesNotContain("Url.Page(\"/Events/Signup\"", identity);
        Assert.Contains("Url.Page(\"/Events/Signups\"", identity);
        Assert.DoesNotContain("Url.Page(\"/Events/Board\"", identity);
        Assert.DoesNotContain("Model.ShowPublicBoard", identity);
        Assert.DoesNotContain("Copy signup form link", identity);
        Assert.Contains("data-copy-url=\"@signupTableUrl\"", identity);
        Assert.DoesNotContain("Copy public board link", identity);

        Assert.Contains("if (!Input.HasBaseline && Input.Version != item.Version) AddStaleError();", identityHandler);
        Assert.Contains("EventIdentityComparison.Compare(", identityHandler);
        Assert.Contains("new(Input.OriginalName!, Input.OriginalDescription, Input.OriginalBuyInDescription, Input.OriginalTimezone!), proposed, Values(item)", identityHandler);
        Assert.Contains("if (timezoneChanged && supportedTimezone && item.FirstPublicAt is not null)", identityHandler);
        Assert.Contains("!string.Equals(Input.TimezoneConfirmationSchedule, ScheduleFingerprint(item), StringComparison.Ordinal)", identityHandler);
        Assert.Contains("item.UpdateIdentity(proposed.Name, item.Slug, proposed.Description, proposed.BuyInDescription, proposed.Timezone)", identityHandler);
        Assert.Contains("EventCapability.ConfigureIdentity", identityHandler);
        Assert.DoesNotContain("TimezoneReason", identity);
        Assert.DoesNotContain("TimezoneReason", identityHandler);
        Assert.Contains("catch (DbUpdateConcurrencyException)", identityHandler);
        Assert.DoesNotContain("OnPostRemoveBannerAsync", identityHandler);
        Assert.Contains("HasRetiredBannerMutationAsync", identityHandler);
        Assert.Contains("\"event.identity_updated\"", identityHandler);
        Assert.Contains("return RedirectToPage(\"Identity\", new { id });", identityHandler);

        Assert.Contains("@page \"{id:guid}\"", manage);
        Assert.Contains("aria-label=\"@T[\"Operational overview\"]\" data-manage-overview", manage);
        Assert.Contains("href=\"@blocker.Route\"", manage);
        Assert.Contains("data-update-targets=\"@partialUpdateTargets\"", manage);
        Assert.Contains("<article class=\"panel event-admin-controls\">", manage);
        Assert.Contains("<h2>@T[\"Event controls\"]</h2>", manage);
        Assert.Contains("data-lifecycle-confirm", manage);
        Assert.Contains("window.adminConfirmation?.open", lifecycleConfirmScript);
        Assert.DoesNotContain("data-confirmation-box tabindex=\"-1\"", manage);
        Assert.DoesNotContain("event-signup-primary", manage);
        Assert.DoesNotContain("event-signup-toggle-form", manage);
        Assert.DoesNotContain("event-wom-form", manage);
        Assert.DoesNotContain("asp-page-handler=\"Prepare", manage);

        foreach (var confirmation in new[]
                 {
                     (Handler: "StartEvent", Name: "ConfirmStartEvent"),
                     (Handler: "EndEvent", Name: "ConfirmEndEvent"),
                     (Handler: "ResumeEvent", Name: "ConfirmResumeEvent")
                 })
        {
            Assert.Contains($"asp-page-handler=\"{confirmation.Handler}\"", manage);
            Assert.Contains($"data-confirm-field=\"{confirmation.Name}\"", manage);
            Assert.Contains($"name=\"{confirmation.Name}\" value=\"false\" data-confirm-field-value", manage);
        }

        Assert.Contains("asp-for=\"ReplacementEventEndsAtLocal\"", manage);
        Assert.Contains("data-confirm-reason-field=\"ResumeReason\"", manage);
        Assert.Contains("name=\"ResumeReason\" data-confirm-reason-value", manage);
        Assert.DoesNotContain("<textarea asp-for=\"ResumeReason\"", manage);
        Assert.Contains("aria-labelledby=\"resume-event-heading\"", manage);
        Assert.Contains("id=\"resume-event-heading\"", manage);

        Assert.Contains("asp-page-handler=\"@destructiveHandler\"", manage);
        Assert.Contains("var destructiveHandler = Model.CanDiscard ? \"Discard\" : \"Cancel\";", manage);
        Assert.Equal(3, Count(manage, "name=\"ConfirmDestructiveAction\" value=\"false\" data-confirm-field-value"));
        Assert.Contains("data-confirm-reason-field=\"CancellationReason\"", manage);
        Assert.Contains("data-confirm-require-reason=\"@(Model.CanDiscard ? \"false\" : \"true\")\"", manage);
        Assert.DoesNotContain("<textarea asp-for=\"CancellationReason\"", manage);
        Assert.Contains("aria-labelledby=\"event-danger-heading\"", manage);
        Assert.Contains("id=\"event-danger-heading\"", manage);

        var hideFormStart = manage.IndexOf("asp-page-handler=\"Hide\"", StringComparison.Ordinal);
        var hideFormEnd = manage.IndexOf("</form>", hideFormStart, StringComparison.Ordinal);
        Assert.True(hideFormStart >= 0 && hideFormEnd > hideFormStart);
        var hideForm = manage[hideFormStart..hideFormEnd];
        Assert.Contains("data-lifecycle-confirm", hideForm);
        Assert.Contains("data-confirm-field=\"ConfirmDestructiveAction\"", hideForm);
        Assert.Contains("data-confirm-reason-field=\"QuarantineReason\"", hideForm);
        Assert.Contains("data-confirm-require-reason=\"true\"", hideForm);
        Assert.DoesNotContain("EventNameConfirmation", hideForm);

        var restoreFormStart = manage.IndexOf("asp-page-handler=\"RestoreHidden\"", StringComparison.Ordinal);
        var restoreFormEnd = manage.IndexOf("</form>", restoreFormStart, StringComparison.Ordinal);
        Assert.True(restoreFormStart >= 0 && restoreFormEnd > restoreFormStart);
        var restoreForm = manage[restoreFormStart..restoreFormEnd];
        Assert.Contains("data-lifecycle-confirm", restoreForm);
        Assert.Contains("data-confirm-field=\"ConfirmDestructiveAction\"", restoreForm);
        Assert.DoesNotContain("data-confirm-require-reason=\"true\"", restoreForm);
        Assert.DoesNotContain("EventNameConfirmation", manage);

        Assert.Contains("eventLifecycle.StartNowAsync(id, EventVersion, ConfirmStartEvent, StartReason, Actor, ct)", manageHandler);
        Assert.Contains("eventLifecycle.EndNowAsync(id, EventVersion, ConfirmEndEvent, EndReason, Actor, ct)", manageHandler);
        Assert.Contains("eventLifecycle.ResumePrematureEndAsync(id, EventVersion, ConfirmResumeEvent, ResumeReason, replacementEnd, Actor, ct)", manageHandler);
        Assert.Contains("destructiveLifecycle.DiscardAsync(id, EventVersion, ConfirmDestructiveAction, Actor, ct)", manageHandler);
        Assert.Contains("destructiveLifecycle.CancelAsync(id, EventVersion, ConfirmDestructiveAction, CancellationReason, Actor, ct)", manageHandler);
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
        Assert.DoesNotContain("CanTransferOwnership", participantHandler);
        Assert.DoesNotContain("ConfirmOwnershipTransfer", participant);
        Assert.DoesNotContain("participant-ownership-confirmation", participant);
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
        Assert.Contains("Every new participant must use an existing active website account.", participantForm);
        Assert.DoesNotContain("owner is optional", participantForm, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("data-signup-question-confirmation=\"true\"", questions);
        Assert.Contains("data-signup-question-confirmation-impact", questions);
        Assert.DoesNotContain("signup-question-remove", questions);
        Assert.Contains("window.adminConfirmation.open", questionsScript);
        Assert.Contains("onConfirm: () => submitForm(form, submitter)", questionsScript);
        Assert.Contains("signup-questions-page-title", questions);
        Assert.Contains("aria-describedby=\"@(Model.IsOverlay ? \"signup-questions-dialog-description\" : null)\"", questions);
        Assert.Contains("aria-label=\"@T[\"Move {0} up\", question.Label]\"", questions);
        Assert.Contains("aria-label=\"@T[\"Move {0} down\", question.Label]\"", questions);
        Assert.DoesNotContain("@T[\"Signup access\"]", questions);
        Assert.Contains("asp-page-handler=\"SignupCode\"", participants);
        Assert.Contains("data-signup-code-toggle", participants);
        Assert.Contains("OnPostSignupCodeAsync", participantsHandler);
        Assert.Contains("catch (DbUpdateConcurrencyException)", participantsHandler);
        Assert.Contains(".admin-dialog-page-kicker { margin: 0; color: var(--admin-muted);", siteCss);
        Assert.DoesNotContain("class=\"step-kicker\"", questions);
        Assert.Contains("@if (!Model.IsOverlay)", questions);
        Assert.Contains("max-height: min(38rem, calc(100dvh - 7rem))", siteCss);
        Assert.Contains("width: min(41rem, calc(100vw - 3rem))", siteCss);
        Assert.Contains("participant-account-controls { display: grid;", siteCss);
        Assert.Contains("class=\"btn admin-button-create\" type=\"submit\" data-wom-validation-normal-submit", participantForm);
        Assert.Contains("hidden=\"@(Model.WomValidationConfirmationRequired ? \"hidden\" : null)\"", participantForm);
        Assert.Contains(">+ @T[\"Create participant\"]", participantForm);
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
    [InlineData("scheduled action", "Model.ScheduledAction is not null || showAllControlStages", "Scheduled lifecycle action", ".event-manage-page .event-admin-controls > .event-admin-control-group > .event-control-status", "@if (Model.ScheduledAction is not null || showAllControlStages)", "@if (showSignupControls)")]
    [InlineData("pre-live signup", "showSignupControls", "event-signup-group", ".event-manage-page .event-signup-group { display: grid; grid-template-columns: minmax(0, 1fr) auto;", "@if (showSignupControls)", "@if (showStartControl || showLiveControls || showFinalReviewControls || showResultsControl)")]
    [InlineData("signup-open schedule", "Automatic signup opening scheduled for:", "event-control-supporting", ".event-manage-page .event-signup-group", "@if (showSignupControls)", "@if (showStartControl || showLiveControls || showFinalReviewControls || showResultsControl)")]
    [InlineData("signup-closed start", "showStartControl", "asp-page-handler=\"StartEvent\"", ".event-manage-page .event-admin-event-actions", "@if (showStartControl)", "@if (showLiveControls)")]
    [InlineData("live controls", "showLiveControls", "asp-page-handler=\"EndEvent\"", ".event-manage-page .event-admin-event-actions", "@if (showLiveControls)", "@if (showFinalReviewControls)")]
    [InlineData("final review", "showFinalReviewControls", "asp-page=\"Finalize\"", ".event-manage-page .event-reopen-form > button", "@if (showFinalReviewControls)", "@if (showResultsControl)")]
    [InlineData("resume subsection", "event-control-subsection", "asp-page-handler=\"ResumeEvent\"", ".event-manage-page .event-control-subsection", "<section class=\"event-admin-control-group event-control-subsection\"", "</section>\n                    }")]
    [InlineData("verification codes", "event-admin-code-group", "event-code-toggle-form", ".event-manage-page .event-code-toggle-form", "<section class=\"event-admin-control-group event-admin-code-group\">", "@if (showAllControlStages || eventView.State == EventState.Cancelled)")]
    [InlineData("terminal", "eventView.State == EventState.Cancelled", "Event cancelled", ".event-manage-page .event-admin-controls > .event-admin-control-group > .event-control-status", "@if (showAllControlStages || eventView.State == EventState.Cancelled)", "@if (User.IsInRole(\"SuperAdmin\")")]
    [InlineData("Wise Old Man", "Model.CompetitionIntegration?.Configured == true", "asp-page=\"WiseOldMan\"", ".event-overview-dates-panel", "<div class=\"event-overview-date-row @(Model.CompetitionIntegration?.Configured == true ?", "</div>\n            </dl>")]
    [InlineData("danger zone", "showDestructivePreLiveControl", "event-danger-zone-container", ".event-manage-page .event-danger-zone-form", "@if (showDestructivePreLiveControl)", "</section>\n                }\n            </article>")]
    public void ManageEventControlBranchesKeepRightOwnedActions(string branch, string branchMarker, string actionMarker, string cssMarker, string controlStartMarker, string controlEndMarker)
    {
        var repositoryRoot = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var siteCss = BrowserTestFiles.ReadActiveStyles(repositoryRoot);

        Assert.True(manage.Contains(branchMarker, StringComparison.Ordinal), $"{branch}: missing branch marker");
        Assert.Contains(cssMarker, siteCss);

        var controlStart = manage.IndexOf(controlStartMarker, StringComparison.Ordinal);
        Assert.True(controlStart >= 0, $"{branch}: missing control start marker");
        var controlEnd = manage.IndexOf(controlEndMarker, controlStart + controlStartMarker.Length, StringComparison.Ordinal);
        Assert.True(controlEnd > controlStart, $"{branch}: missing control end marker");
        var control = manage[controlStart..controlEnd];
        Assert.Contains(branchMarker, control);
        Assert.Contains(actionMarker, control);

        Assert.Contains("<article class=\"panel event-admin-controls\">", manage);
        Assert.Contains("<h3>@T[\"Manual signup control\"]</h3>", manage);
        Assert.Contains("class=\"event-control-status event-control-supporting\"", manage);
        Assert.DoesNotContain("event-signup-primary", manage);
        Assert.DoesNotContain("event-signup-toggle-form", manage);
        Assert.DoesNotContain("event-wom-form", manage);
        Assert.DoesNotContain("asp-page-handler=\"Prepare", manage);
        Assert.Contains(".event-manage-page .event-admin-controls .event-create-cancel { grid-column: auto; grid-row: auto;", siteCss);
        Assert.Contains(".event-manage-page .event-admin-event-actions { width: fit-content; max-width: 100%; justify-self: end; align-self: center; align-items: center; }", siteCss);
        Assert.Contains(".event-manage-page .event-evidence-actions { align-items: flex-start; }", siteCss);
        Assert.Contains(".event-manage-page .event-admin-control-group { margin-top:", siteCss);
        Assert.Contains("data-lifecycle-confirm", manage);

        var lifecycleControls = manage.IndexOf("@if (showStartControl || showLiveControls || showFinalReviewControls || showResultsControl)", StringComparison.Ordinal);
        Assert.True(lifecycleControls >= 0);
        var startReadiness = manage.IndexOf("Model.StartReadiness?.Blockers.Count > 0", lifecycleControls, StringComparison.Ordinal);
        var startAction = manage.IndexOf("asp-page-handler=\"StartEvent\"", lifecycleControls, StringComparison.Ordinal);
        Assert.True(startReadiness >= 0 && startAction > startReadiness);
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
