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

        // A10 (U10 part 2, user ruling 8 October 2026): the old Create page is retired. The file is a route stub;
        // GET and a refused non-dialog POST return to the Events directory with the Create dialog open.
        Assert.Contains("@page", creation);
        Assert.DoesNotContain("<form", creation);
        Assert.DoesNotContain("_ValidationScriptsPartial", creation);
        Assert.Contains("public IActionResult OnGet() => RedirectToPage(\"Index\", new { create = 1 });", createHandler);
        Assert.Contains("return RedirectToPage(\"Index\", new { create = 1 });", createHandler);
        Assert.DoesNotContain("Page()", createHandler);

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

        // U4 / OS-1 (brief 85): Overview.dc.html replaces the old control panels. Lifecycle dialogs
        // are built from the presenter's dialog models (fields, confirm flags, reasons) and post
        // through AdminFetch; the handlers and their service calls are unchanged.
        var presenter = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "OverviewPresenter.cs"));
        Assert.Contains("@page \"{id:guid}\"", manage);
        Assert.Contains("data-current=\"@Model.CurrentJson\"", manage);
        Assert.DoesNotContain("asp-page-handler=\"Prepare", manage);
        Assert.DoesNotContain("EventNameConfirmation", manage);
        foreach (var (handler, confirm) in new[] { ("StartEvent", "ConfirmStartEvent"), ("EndEvent", "ConfirmEndEvent"), ("ResumeEvent", "ConfirmResumeEvent"), ("Discard", "ConfirmDestructiveAction"), ("Cancel", "ConfirmDestructiveAction"), ("Hide", "ConfirmDestructiveAction"), ("RestoreHidden", "ConfirmDestructiveAction") })
        {
            Assert.Contains($"\"{handler}\"", presenter);
            Assert.Contains($"confirmField: \"{confirm}\"", presenter);
        }
        Assert.Contains("reasonField: \"ResumeReason\"", presenter);
        Assert.Contains("untilField: \"ReplacementEventEndsAtLocal\"", presenter);
        Assert.Contains("reasonField: \"CancellationReason\"", presenter);
        Assert.Contains("reasonField: \"QuarantineReason\"", presenter);

        Assert.Contains("eventLifecycle.StartNowAsync(id, EventVersion, ConfirmStartEvent, StartReason, Actor, ct)", manageHandler);
        Assert.Contains("eventLifecycle.EndNowAsync(id, EventVersion, ConfirmEndEvent, EndReason, Actor, ct)", manageHandler);
        Assert.Contains("eventLifecycle.ResumePrematureEndAsync(id, EventVersion, ConfirmResumeEvent, ResumeReason, replacementEnd, Actor, ct)", manageHandler);
        Assert.Contains("destructiveLifecycle.DiscardAsync(id, EventVersion, ConfirmDestructiveAction, Actor, ct)", manageHandler);
        Assert.Contains("destructiveLifecycle.CancelAsync(id, EventVersion, ConfirmDestructiveAction, CancellationReason, Actor, ct)", manageHandler);
        Assert.Contains("private LifecycleActor Actor => new(User.GetAccountId()!.Value, User.Identity!.Name!);", manageHandler);

        // U4: repair links are built by the presenter from the shared event-page URL helper.
        foreach (var route in new[] { "Identity", "Schedule", "Draft", "Board", "SignupSetup", "Participants" })
            Assert.Contains($"Url(\"/Admin/Events/{route}\")", presenter);
    }

    [Fact]
    public void ParticipantsBindTheReferenceWorkspaceWithQueryDrawerAndTransientConfirmations()
    {
        // A10 (U5, brief 87): the retired Participants markup (route-page Add, table rows,
        // _InternalParticipantForm, event-manage.js dialogs) is replaced by Participants.dc.html
        // on the new layout. Same behaviours, new contract: query-backed drawer and Add (A2),
        // transient confirmations, shared transport and busy timing, retired filters gone.
        var root = FindRepositoryRoot();
        var participants = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participants.cshtml"));
        var participantsHandler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participants.cshtml.cs"));
        var script = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "admin-participants.js"));
        Assert.False(File.Exists(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "_InternalParticipantForm.cshtml")));
        Assert.Contains("[AdminDesign]", participantsHandler);
        Assert.Contains("ViewData[\"PageFamily\"] = \"participants\";", participants);
        Assert.Contains("\"participant=\" + participant", participants);
        Assert.Contains("data-participant-open=", participants);
        Assert.Contains("data-menu-target=\"participant-menu\"", participants);
        Assert.Contains("data-participant-add", participants);
        foreach (var retired in new[] { "ParticipantDiscord", "ParticipantCaptain", "ParticipantSource", "ParticipantTeamId", "Move up in queue", "Move down in queue" })
        {
            Assert.DoesNotContain(retired, participants);
            Assert.DoesNotContain(retired, participantsHandler);
            Assert.DoesNotContain(retired, script);
        }
        Assert.Contains("[FromQuery(Name = \"page\")]", participantsHandler);
        Assert.DoesNotContain("BindProperty(Name = \"page\"", participantsHandler);
        Assert.Contains("confirmation: true", script);
        Assert.Contains("ui.busy(() => window.AdminFetch.request(", script);
        Assert.Contains("ui.registerUrlState(", script);
        Assert.DoesNotContain("setTimeout(() => window.AdminFetch", script);
    }

    [Fact]
    public void SignupSetupRetainsVersionedMoveAndEditAndRetiresQuestionsOverlay()
    {
        var root = FindRepositoryRoot();
        var handler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "SignupSetup.cshtml.cs"));
        var retired = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Questions.cshtml.cs"));
        Assert.Contains("OnPostMoveAsync", handler);
        Assert.Contains("OnPostEditAsync", handler);
        Assert.Contains("HasCurrentFormBaselineAsync", handler);
        Assert.Contains("tab = \"form\"", retired);
    }

    // U4 / OS-1 (brief 85 "Retired"; README "What needs integration"): the old Manage control
    // branches and their blocker labels are replaced by the Overview composition. These
    // source checks pin the replacement: one dialog trigger per action, no old markup.
    [Fact]
    public void OverviewMarkupUsesDialogTriggersAndNoRetiredReadinessBlock()
    {
        var repositoryRoot = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        Assert.Contains("data-overview-action=\"@a.Key\"", manage);
        Assert.Contains("data-overview-action=\"@r.Key\"", manage);
        Assert.Contains("asp-page-handler=\"RestoreHidden\"", manage);
        foreach (var retired in new[] { "event-overview-readiness-panel", "Readiness checks", "TEAM_ACCESS_MISSING", "preview-all-controls", "asp-page-handler=\"Prepare", "event-signup-toggle-form" })
            Assert.DoesNotContain(retired, manage, StringComparison.Ordinal);
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

    // U4 / A-Overview-3 (replaces the retired blocker-route mapping, brief 85): every
    // requirement row links to the page that fixes it; capacity belongs to Signup setup.
    [Fact]
    public void OverviewChecklistRowsLinkToTheirOwningPages()
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var item = new Bingo.Domain.Events.BingoEvent(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Checklist", "checklist", "UTC", Guid.NewGuid(), now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var readiness = new SignupReadiness([new("DESCRIPTION_REQUIRED", "d"), new("PARTICIPANT_CAP_REQUIRED", "c"), new("EVENT_START_REQUIRED", "s"), new("SIGNUP_QUESTIONS_INVALID", "q"), new("SIGNUP_CODE_UNUSABLE", "code")], [], [], SignupCloseDecision.Evaluate(null, null, null, now));
        var input = new OverviewInput(item, now, false, 0, 0, 0, 0, 0, 0, false, false, 0, 0, [], readiness, null, null, null, false, null, null, null, [], null, true, null, null, [], "https://bingo.example");
        var view = new OverviewPresenter(input, (key, args) => args.Length == 0 ? key : string.Format(System.Globalization.CultureInfo.InvariantCulture, key, args), System.Globalization.CultureInfo.InvariantCulture).Build();
        string Link(string label) => Assert.Single(view.Now.Checks, check => check.Label == label).LinkHref!;
        Assert.Equal($"/Admin/Events/Identity/{item.Id}", Link("Public description"));
        Assert.Equal($"/Admin/Events/SignupSetup/{item.Id}", Link("Participant capacity"));
        Assert.Equal($"/Admin/Events/Schedule/{item.Id}", Link("Event start and end"));
        Assert.Equal($"/Admin/Events/SignupSetup/{item.Id}?tab=form", Link("Signup form"));
        Assert.Equal($"/Admin/Events/SignupSetup/{item.Id}", Link("Signup code"));
        Assert.False(view.Dialogs["open"].Ready);
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
