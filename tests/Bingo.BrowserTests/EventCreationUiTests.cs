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

        Assert.Equal(System.Net.HttpStatusCode.Redirect, creation.StatusCode);
        Assert.Equal("/Account/Login", creation.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, identity.StatusCode);
        Assert.Equal("/Account/Login", identity.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, schedule.StatusCode);
        Assert.Equal("/Account/Login", schedule.Headers.Location?.AbsolutePath);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, banner.StatusCode);
        Assert.Equal("/Account/Login", banner.Headers.Location?.AbsolutePath);
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
        var schedule = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Schedule.cshtml"));
        var scheduleHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Schedule.cshtml.cs"));
        var questions = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Questions.cshtml"));
        var participant = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participant.cshtml"));
        var siteScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "site.js"));
        var creationScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-create.js"));
        var manageScript = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "js", "event-manage.js"));

        Assert.Contains("data-create-panel=\"0\"", creation);
        Assert.Contains("data-create-panel=\"4\"", creation);
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
        Assert.Contains("All start blockers are resolved. Confirm to start the event now.", manage);
        Assert.Contains("asp-page-handler=\"Discard\"", manage);
        Assert.Contains("asp-page-handler=\"Cancel\"", manage);
        Assert.Contains("ConfirmDestructiveAction", manage);
        Assert.Contains("CancellationReason", manage);
        Assert.Contains("var showStartControl = eventView.State == EventState.SignupClosed", manage);
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
        Assert.Contains("@page \"{id:guid}\"", schedule);
        Assert.Contains("datetime-local", schedule);
        Assert.Equal(5, Count(schedule, "step=\"300\""));
        Assert.DoesNotContain("item.Code", schedule);
        Assert.Contains("item.Code == \"TEAM_ACCESS_MISSING\"", manage);
        Assert.Contains("yyyy-MM-ddTHH:mm", scheduleHandler);
        Assert.Contains("Settings.WaitingListEnabled", questions);
        Assert.Contains("SignupQuestionType.Text", questions);
        Assert.DoesNotContain("ShortText", questions);
        Assert.DoesNotContain("LongText", questions);
        Assert.Contains("asp-page-handler=\"WaitingList\"", questions);
        Assert.Contains("asp-page-handler=\"SignupCode\"", questions);
        Assert.Contains("data-auto-submit", questions);
        Assert.Contains("<script src=\"~/js/event-manage.js\" asp-append-version=\"true\"></script>", questions);
        Assert.Contains("<noscript><button class=\"btn btn-outline-light\" type=\"submit\">Save waiting list</button></noscript>", questions);
        Assert.Equal(1, Count(questions, "Save waiting list"));
        Assert.Contains("Save signup code", questions);
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
        Assert.Contains("data-auto-submit data-payment-save", participant);
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
        Assert.Equal(5, Count(creation, "data-date-time-picker"));
        Assert.Equal(5, Count(schedule, "data-date-time-picker"));
        foreach (var field in new[] { "SignupOpensLocal", "SignupClosesLocal", "DraftLocal", "EventStartsLocal", "EventEndsLocal" })
        {
            Assert.Contains($"<input asp-for=\"Input.{field}\" type=\"datetime-local\" step=\"300\" class=\"form-control\" data-date-time-picker", creation);
            Assert.Contains($"<input asp-for=\"Input.{field}\" type=\"datetime-local\" step=\"300\" class=\"form-control\" data-date-time-picker", schedule);
        }
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
        Assert.Contains("data-date-time-picker", schedule);
        Assert.DoesNotContain("ScheduleOpening", schedule);
        Assert.Contains("bingoDateTimeHours", siteScript);
        Assert.Contains("bingoDateTimeMinutes", siteScript);
        Assert.Contains("length: 24", siteScript);
        Assert.Contains("length: 12", siteScript);
        Assert.DoesNotContain("bingoFiveMinuteTimes", siteScript);
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
