namespace Bingo.BrowserTests;

public sealed class ManagedCompetitionUiTests
{
    [Fact]
    public void ManagePageUsesSeparateManagedActionsAndOneTimeProtectedCredentialInput()
    {
        var root = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var workspace = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "WiseOldMan.cshtml"));
        var handler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "WiseOldMan.cshtml.cs"));

        Assert.Contains("asp-page-handler=\"CreateManagedCompetition\"", workspace);
        Assert.Contains("asp-page-handler=\"DeleteManagedCompetition\"", workspace);
        Assert.Contains("ConfirmManagedCompetitionDelete", workspace);
        Assert.Contains("OnPostCreateManagedCompetitionAsync", handler);
        Assert.Contains("OnPostDeleteManagedCompetitionAsync", handler);
        Assert.Contains("asp-page-handler=\"AdoptCompetitionCredential\"", workspace);
        Assert.Contains("asp-for=\"CompetitionVerificationCode\"", workspace);
        Assert.Contains("type=\"password\"", workspace);
        // A10 / AU15: shared workspace keeps Fetch a direct action with no confirmation.
        Assert.Contains("asp-page-handler=\"FetchCompetition\" id=\"wom-fetch-form\" data-wom-action=\"fetch\"", workspace);
        Assert.DoesNotContain("id=\"wom-fetch-form\" data-lifecycle-confirm", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("data-confirm-wom-value", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("FetchConfirmation", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("FetchConfirmation", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("Type FETCH", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("Type FETCH", handler, StringComparison.Ordinal);
        Assert.Contains("LocalizeManagedError", handler);
        // U4 / OS-1: Overview links WOM through its presenter (glance row, A-Overview-7, A15).
        Assert.Contains("Url(\"/Admin/Events/WiseOldMan\")", File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "OverviewPresenter.cs")));
        Assert.DoesNotContain("asp-page-handler=\"CreateManagedCompetition\"", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"http-secret\"", workspace, StringComparison.Ordinal);
    }

    [Fact]
    public void CompetitionLinkMarkupDoesNotOfferProviderScheduleImport()
    {
        var root = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var workspace = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "WiseOldMan.cshtml"));
        var handler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "WiseOldMan.cshtml.cs"));

        Assert.DoesNotContain("SynchronizeCompetitionSchedule", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmCompetitionSchedule", workspace, StringComparison.Ordinal);
        // U4 / OS-1: Overview links WOM through its presenter (glance row, A-Overview-7, A15).
        Assert.Contains("Url(\"/Admin/Events/WiseOldMan\")", File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "OverviewPresenter.cs")));
        Assert.Contains("ConfigureAsync(id, EventVersion, CompetitionId, Actor, cancellationToken: ct)", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void NewAndChangedNamesKeepTheProviderLimitsWhileHistoryCanRemainLonger()
    {
        var root = FindRepositoryRoot();
        // A10 (U10 part 2): Create.cshtml is retired; the Events directory dialog owns the 50-codepoint new-name limit.
        var createTemplate = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Shared", "_AdminEventCreateTemplate.cshtml"));
        var createScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "admin-event-create.js"));
        var identity = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml"));
        var draft = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Draft.cshtml"));
        var handlers = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Draft.cshtml.cs"));

        Assert.Contains("Event names must be 50 characters or fewer.", createTemplate);
        Assert.Contains("data-create-count", createTemplate);
        Assert.Contains("Array.from(name.value.trim()).length>50?t('Event names must be 50 characters or fewer.')", createScript);
        Assert.Contains("data-codepoint-limit=\"50\"", identity);
        // A10 (U6): the Teams page renders client-side; its team form enforces the 30-character provider limit.
        var draftScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "admin-draft.js"));
        Assert.Contains("length > 30) return t('Use 30 characters or fewer.')", draftScript);
        Assert.Contains("MaximumTeamNameLength", handlers);
        Assert.Contains("!string.Equals(team.Name, name.Trim()", handlers);
    }

    [Fact]
    public void TeamParticipationUsesIncludedFlagAndRetiresLegacyRosterCreationSurfaces()
    {
        var root = FindRepositoryRoot();
        var draft = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Draft.cshtml"));
        var handlers = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Draft.cshtml.cs"));
        var publicTeams = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "Teams.cshtml"));
        var publicHandlers = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Events", "Teams.cshtml.cs"));
        var participant = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participant.cshtml"));
        var participantHandler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Participant.cshtml.cs"));

        // A10 (U6): the team form posts includedInDraft from its checkbox (true and false); no formationType.
        var draftScript = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "wwwroot", "js", "admin-draft.js"));
        Assert.Contains("includedInDraft: String(included)", draftScript);
        Assert.Contains("values.includedInDraft = String(included)", draftScript);
        Assert.DoesNotContain("formationType", draftScript);
        Assert.DoesNotContain("name=\"formationType\"", draft);
        Assert.DoesNotContain("asp-page-handler=\"AddExternalMember\"", draft);
        Assert.DoesNotContain("RosterCsv", draft);
        Assert.DoesNotContain("OnPostRemoveExternalTeamAsync", handlers);
        Assert.DoesNotContain("OnPostAddExternalMemberAsync", handlers);
        Assert.DoesNotContain("OnGetRosterCsvTemplateAsync", handlers);
        Assert.DoesNotContain("OnPostPreviewRosterCsvAsync", handlers);
        Assert.DoesNotContain("OnPostApplyRosterCsvAsync", handlers);
        Assert.Null(typeof(Bingo.Web.Pages.Admin.Events.DraftModel).GetMethod("OnPostRemoveExternalTeamAsync"));
        Assert.Null(typeof(Bingo.Web.Pages.Admin.Events.DraftModel).GetMethod("OnPostAddExternalMemberAsync"));
        Assert.Null(typeof(Bingo.Web.Pages.Admin.Events.DraftModel).GetMethod("OnGetRosterCsvTemplateAsync"));
        Assert.Null(typeof(Bingo.Web.Pages.Admin.Events.DraftModel).GetMethod("OnPostPreviewRosterCsvAsync"));
        Assert.Null(typeof(Bingo.Web.Pages.Admin.Events.DraftModel).GetMethod("OnPostApplyRosterCsvAsync"));
        Assert.Contains("IncludedInDraft", handlers);
        Assert.Contains("IncludedInDraft", publicTeams);
        Assert.Contains("IncludedInDraft", publicHandlers);
        Assert.DoesNotContain("FormationType", publicTeams);
        Assert.DoesNotContain("FormationType", publicHandlers);
        Assert.DoesNotContain("OnPostTransferOwnershipAsync", participantHandler);
        Assert.DoesNotContain("TransferOwnership", participant);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
