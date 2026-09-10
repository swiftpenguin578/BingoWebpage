using System.Text.RegularExpressions;

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
        Assert.Contains("continueBoardAction(() => { prepareCreateTileEditor(createButton.dataset.position); showBoardDialog(createDialog); });", dialogInteraction);
        Assert.Contains("continueBoardAction(() => { prepareEditTileEditor(tile); showBoardDialog(createDialog); });", dialogInteraction);
        Assert.Contains("continueBoardAction(() => { closeTileEditorNow(); showBoardDialog(document.getElementById(detailsButton.dataset.detailsId)); });", dialogInteraction);

        var navigationStart = boardMarkup.IndexOf(
            "document.addEventListener('click', event => {\n    const link = event.target.closest?.('a[href]');",
            StringComparison.Ordinal);
        var navigationEnd = boardMarkup.IndexOf(
            "document.addEventListener('submit', event => {",
            navigationStart,
            StringComparison.Ordinal);
        Assert.True(navigationStart >= 0 && navigationEnd > navigationStart);
        var linkNavigation = boardMarkup[navigationStart..navigationEnd];
        Assert.Contains("if (!link || !createDialog?.open || !tileEditorGuard.dirtyForms().length) return;", linkNavigation);
        Assert.Contains("if ((link.target && link.target.toLowerCase() !== '_self') || link.hasAttribute('download') || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || (event.button != null && event.button !== 0)) return;", linkNavigation);
        Assert.Contains("event.preventDefault(); event.stopImmediatePropagation();", linkNavigation);
        Assert.Contains("continueBoardAction(() => { closeTileEditorNow(); window.location.assign(link.href); });", linkNavigation);
        Assert.Contains("(() => {", boardMarkup);
        Assert.Contains("})();", boardMarkup);
        Assert.DoesNotContain("document.querySelectorAll('.create-tile-button').forEach", boardMarkup);
    }

    [Fact]
    public void BoardPublicationUsesExplicitConfirmationDialogsAndBoundConfirmationValues()
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

        Assert.Equal(2, Regex.Count(boardMarkup, "class=\"admin-destructive-confirmation board-publication-confirmation\""));
        Assert.Equal(2, Regex.Count(boardMarkup, "type=\"hidden\" name=\"confirmed\" value=\"true\""));
        Assert.Contains("asp-page-handler=\"Publish\"", boardMarkup);
        Assert.Contains("asp-page-handler=\"Approve\"", boardMarkup);
        Assert.Contains("document.querySelectorAll('.board-publication-confirmation [data-confirmation-cancel]').forEach(button => button.addEventListener('click', () => button.closest('details')?.removeAttribute('open')));", boardMarkup);
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
