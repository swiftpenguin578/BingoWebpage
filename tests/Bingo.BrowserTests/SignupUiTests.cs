namespace Bingo.BrowserTests;

public sealed class SignupUiTests
{
    [Fact]
    public void AccountSelectionsExposeSavedEhbAndOnlyChangeItAfterASelectionChange()
    {
        var markup = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Bingo.Web", "Pages", "Events", "Signup.cshtml"));

        Assert.Contains("data-saved-ehb", markup);
        Assert.Contains("CultureInfo.InvariantCulture", markup);
        Assert.Contains("data-ehb-input", markup);
        Assert.Contains("select.addEventListener(\"change\"", markup);
        Assert.Contains("ehb.value = select.selectedOptions[0]?.dataset.savedEhb ?? \"\";", markup);
        Assert.Contains("name=\"Input.AccountAnswers[@question.Id].Ehb\"", markup);
        Assert.Contains("Html.ValidationMessage($\"Input.AccountAnswers[{question.Id}].OsrsCharacterId\")", markup);
        Assert.Contains("asp-validation-summary=\"All\"", markup);
        Assert.Contains("data-feedback-target", markup);
        Assert.Contains("<form method=\"post\"", markup);
        Assert.Contains("View signup table", markup);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
