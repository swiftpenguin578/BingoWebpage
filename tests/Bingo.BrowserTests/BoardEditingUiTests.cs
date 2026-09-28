using System.Text.RegularExpressions;

namespace Bingo.BrowserTests;

public sealed class BoardEditingUiTests
{
    [Fact]
    public void BoardOpensInEditorWithoutExplicitReleaseAction()
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
        Assert.DoesNotContain("data-release-board-editing", boardMarkup);
        Assert.DoesNotContain("Finish editing", boardMarkup);
        Assert.DoesNotContain("asp-page-handler=\"Unapprove\"", boardMarkup);
        Assert.DoesNotContain("asp-page-handler=\"Create\"", boardMarkup);
        Assert.Contains("Editing control renews while you work and expires after five minutes without board activity", boardMarkup);
        Assert.Contains("after five minutes without board activity", boardMarkup);
        Assert.Contains("asp-page-handler=\"AcquireEditing\"", boardMarkup);
        Assert.Contains("name=\"BoardVersion\" value=\"@Model.BoardView.Version\"", boardMarkup);
        Assert.DoesNotContain("await connection.invoke('RenewBoardEditing', boardRoot.dataset.adminBoardEvent)", collaborationScript);
        Assert.Contains("let lastBoardRenewal = 0;", collaborationScript);
        Assert.Contains("lastBoardRenewal !== 0", collaborationScript);
        var renewalStart = collaborationScript.IndexOf("let lastBoardRenewal = 0;", StringComparison.Ordinal);
        var renewalEnd = collaborationScript.IndexOf("['pointerdown', 'keydown', 'input', 'dragstart']", renewalStart, StringComparison.Ordinal);
        Assert.True(renewalStart >= 0 && renewalEnd > renewalStart);
        var renewalBlock = collaborationScript[renewalStart..renewalEnd];
        var guard = renewalBlock.IndexOf("lastBoardRenewal !== 0", StringComparison.Ordinal);
        var timestamp = renewalBlock.IndexOf("lastBoardRenewal = Date.now();", StringComparison.Ordinal);
        var invoke = renewalBlock.IndexOf("connection.invoke('RenewBoardEditing'", StringComparison.Ordinal);
        Assert.True(guard >= 0 && timestamp > guard && invoke > timestamp);
        Assert.DoesNotContain("pagehide", collaborationScript);
        Assert.DoesNotContain("keepalive: true", collaborationScript);
    }

    [Fact]
    public void AutomaticTileDescriptionPlaceholderIsLocalizedAndEditorInputStaysSeparate()
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
        var danishResources = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Bingo.Web",
            "Resources",
            "SharedResource.da.resx"));

        Assert.Contains("placeholder=\"@T[\"Auto-generated from tile requirements\"]\"", boardMarkup);
        Assert.Contains("name=\"Auto-generated from tile requirements\"", danishResources);
        Assert.Contains("<value>Genereres automatisk ud fra tile-krav</value>", danishResources);
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

        Assert.Equal(3, Regex.Count(boardMarkup, "class=\"admin-destructive-confirmation board-publication-confirmation\""));
        Assert.Equal(3, Regex.Count(boardMarkup, "type=\"hidden\" name=\"confirmed\" value=\"true\""));
        Assert.Contains("asp-page-handler=\"Publish\"", boardMarkup);
        Assert.Contains("asp-page-handler=\"DiscardCorrection\"", boardMarkup);
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
