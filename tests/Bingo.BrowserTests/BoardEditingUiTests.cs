namespace Bingo.BrowserTests;

public sealed class BoardEditingUiTests
{
    [Fact]
    public void TileEditorKeepsTheBoardEditingLeaseDuringSubmission()
    {
        var repositoryRoot = FindRepositoryRoot();
        var boardMarkup = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Bingo.Web",
            "Pages",
            "Admin",
            "Events",
            "Board.cshtml"));
        var collaborationScript = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Bingo.Web",
            "wwwroot",
            "js",
            "admin-collaboration.js"));

        Assert.Contains("id=\"create-tile-form\" data-native-submit", boardMarkup);
        Assert.Contains("preserveBoardEditingOnPageHide = true", collaborationScript);
        Assert.Contains("form:not([data-release-board-editing])", collaborationScript);
    }

    [Fact]
    public void TileActionsSurviveBoardCellSwaps()
    {
        var repositoryRoot = FindRepositoryRoot();
        var boardMarkup = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Bingo.Web",
            "Pages",
            "Admin",
            "Events",
            "Board.cshtml"));

        Assert.Contains("event.target.closest('.create-tile-button')", boardMarkup);
        Assert.Contains("event.target.closest('.edit-tile-button')", boardMarkup);
        Assert.Contains("event.target.closest('.tile-details-button')", boardMarkup);
        Assert.Contains("(() => {", boardMarkup);
        Assert.Contains("})();", boardMarkup);
        Assert.DoesNotContain("document.querySelectorAll('.create-tile-button').forEach", boardMarkup);
    }

    [Fact]
    public void TileEditorExplainsObjectivesAndUsesCompactBoardActions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var boardMarkup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Board.cshtml"));
        var requirementMarkup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "_BoardRequirementEditor.cshtml"));

        Assert.Contains("Multiple bosses in one objective", boardMarkup);
        Assert.Contains("Multiple objectives", boardMarkup);
        Assert.Contains("Add another objective", boardMarkup);
        Assert.Contains("Where can this be completed?", requirementMarkup);
        Assert.Contains("Counting options", requirementMarkup);
        Assert.Contains("catalogue-compact-action catalogue-add-action\">Edit board", boardMarkup);
        Assert.Contains("catalogue-compact-action neutral-outline-action\">Finish editing", boardMarkup);
        Assert.Contains("catalogue-compact-action warning-outline-action\">Take over editing", boardMarkup);
        Assert.Contains("catalogue-compact-action btn-outline-danger\">Remove tile", boardMarkup);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the BingoWebpage repository root.");
    }
}
