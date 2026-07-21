namespace Bingo.BrowserTests;

public sealed class BoardEditingUiTests
{
    [Fact]
    public void BoardEditingLeaseUsesExplicitReleaseInsteadOfPageExitRequest()
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
        Assert.Contains("data-release-board-editing", boardMarkup);
        Assert.Contains("after five minutes without board activity", boardMarkup);
        Assert.DoesNotContain("pagehide", collaborationScript);
        Assert.DoesNotContain("keepalive: true", collaborationScript);
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
        var siteStyles = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "wwwroot", "css", "site.css"));

        Assert.Contains("One objective is one target", boardMarkup);
        Assert.Contains("Select multiple bosses", boardMarkup);
        Assert.Contains("Add another objective", boardMarkup);
        Assert.Contains("Bosses or activities", requirementMarkup);
        Assert.Contains("Eligible drops", requirementMarkup);
        Assert.Contains("Counting options", requirementMarkup);
        Assert.Contains("individual-drop-weights-toggle", requirementMarkup);
        Assert.Contains("DropWeights", requirementMarkup);
        Assert.Contains("Counts for @drop.CreditedWeight", boardMarkup);
        Assert.Contains("tile-dialog-drop-list", boardMarkup);
        Assert.Contains("Custom tile image URL", boardMarkup);
        Assert.Contains("tile-image-url", boardMarkup);
        Assert.Contains("color-scheme: dark", siteStyles);
        Assert.Contains("catalogue-compact-action catalogue-add-action\">Edit board", boardMarkup);
        Assert.Contains("catalogue-compact-action neutral-outline-action\">Finish editing", boardMarkup);
        Assert.Contains("catalogue-compact-action warning-outline-action\">Take over editing", boardMarkup);
        Assert.Contains("catalogue-compact-action btn-outline-danger\">Remove tile", boardMarkup);
        Assert.Contains(".board-page .board-editor", siteStyles);
        Assert.Contains("align-items: stretch", siteStyles);
        Assert.DoesNotContain("Optional settings for reviewing proof", boardMarkup);
        Assert.Contains("rows=\"1\"", boardMarkup);
        Assert.Contains(".create-tile-dialog > .dialog-close", siteStyles);
        Assert.Contains("position: absolute", siteStyles);
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
