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
        Assert.Contains("data-confirm-wom-value=\"FETCH\"", workspace);
        Assert.Contains("Manual fetches should only be used when fresh data is genuinely needed.", workspace);
        Assert.Contains("LocalizeManagedError", handler);
        Assert.Contains("asp-page=\"WiseOldMan\"", manage);
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
        Assert.Contains("asp-page=\"WiseOldMan\"", manage);
        Assert.Contains("ConfigureAsync(id, EventVersion, CompetitionId, Actor, cancellationToken: ct)", handler, StringComparison.Ordinal);
    }

    [Fact]
    public void NewAndChangedNamesKeepTheProviderLimitsWhileHistoryCanRemainLonger()
    {
        var root = FindRepositoryRoot();
        var create = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Create.cshtml"));
        var identity = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Identity.cshtml"));
        var draft = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Draft.cshtml"));
        var handlers = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Draft.cshtml.cs"));

        Assert.Contains("maxlength=\"50\"", create);
        Assert.Contains("maxlength=\"50\"", identity);
        Assert.Contains("maxlength=\"30\"", draft);
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

        Assert.Contains("name=\"includedInDraft\"", draft);
        Assert.Contains("name=\"includedInDraft\" type=\"hidden\" value=\"false\"", draft);
        Assert.True(draft.Split("name=\"includedInDraft\" type=\"hidden\" value=\"false\"", StringSplitOptions.None).Length - 1 >= 3);
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
