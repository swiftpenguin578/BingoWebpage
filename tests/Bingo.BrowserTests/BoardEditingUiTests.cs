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
        Assert.Contains("asp-page-handler=\"TeamSize\" class=\"inline-stat-form\" data-auto-submit><input type=\"hidden\" name=\"BoardVersion\" value=\"@Model.BoardView.Version\" />", boardMarkup);
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
        var dialogInteraction = boardMarkup[boardMarkup.IndexOf("const createDialog", StringComparison.Ordinal)..];
        Assert.Contains("showBoardDialog(createDialog);", dialogInteraction);
        Assert.DoesNotContain("window.location", dialogInteraction, StringComparison.Ordinal);
        Assert.Contains("(() => {", boardMarkup);
        Assert.Contains("})();", boardMarkup);
        Assert.DoesNotContain("document.querySelectorAll('.create-tile-button').forEach", boardMarkup);
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
