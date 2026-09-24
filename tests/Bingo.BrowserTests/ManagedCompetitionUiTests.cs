namespace Bingo.BrowserTests;

public sealed class ManagedCompetitionUiTests
{
    [Fact]
    public void ManagePageUsesSeparateManagedActionsAndKeepsCredentialsOutOfMarkup()
    {
        var root = FindRepositoryRoot();
        var manage = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml"));
        var handler = File.ReadAllText(Path.Combine(root, "src", "Bingo.Web", "Pages", "Admin", "Events", "Manage.cshtml.cs"));

        Assert.Contains("asp-page-handler=\"CreateManagedCompetition\"", manage);
        Assert.Contains("asp-page-handler=\"DeleteManagedCompetition\"", manage);
        Assert.Contains("ConfirmManagedCompetitionDelete", manage);
        Assert.Contains("OnPostCreateManagedCompetitionAsync", handler);
        Assert.Contains("OnPostDeleteManagedCompetitionAsync", handler);
        Assert.Contains("LocalizeManagedError", handler);
        Assert.DoesNotContain("verificationCode", manage, StringComparison.OrdinalIgnoreCase);
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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
