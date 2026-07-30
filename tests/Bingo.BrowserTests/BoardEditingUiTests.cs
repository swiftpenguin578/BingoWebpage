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
        var dialogInteraction = boardMarkup[boardMarkup.IndexOf("const createDialog", StringComparison.Ordinal)..];
        Assert.Contains("createDialog.showModal();", dialogInteraction);
        Assert.DoesNotContain("window.location", dialogInteraction, StringComparison.Ordinal);
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
        var draftCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "Draft.cshtml.cs"));
        var previewMarkup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Bingo.Web", "Pages", "Admin", "Events", "BoardPreview.cshtml"));
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
        Assert.Contains("Custom tile image", boardMarkup);
        Assert.Contains("Optional managed upload", boardMarkup);
        Assert.Contains("enctype=\"multipart/form-data\"", boardMarkup);
        Assert.Contains("TileDraft.Image", boardMarkup);
        Assert.DoesNotContain("Custom tile image URL", boardMarkup);
        Assert.DoesNotContain("tile-image-url", boardMarkup);
        Assert.Contains("asp-page=\"BoardPreview\"", boardMarkup);
        Assert.Contains("Preview/{teamSlug?}/{tileId:guid?}", previewMarkup);
        Assert.Contains("asp-route-teamSlug=\"@team.Slug\"", previewMarkup);
        Assert.Contains("asp-route-tileId=\"@tile.Id\"", previewMarkup);
        Assert.DoesNotContain("Total EHB", previewMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("EHB per player", previewMarkup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Publish\"", boardMarkup);
        Assert.Contains("asp-page-handler=\"Approve\"", boardMarkup);
        Assert.Contains("asp-page-handler=\"Unapprove\"", boardMarkup);
        Assert.Contains("asp-page-handler=\"CorrectPublished\"", boardMarkup);
        Assert.Contains("Approval is private. Publication is a separate action after draft finalization.", boardMarkup);
        Assert.Contains("Finalize the team draft before publishing this approved board.", boardMarkup);
        Assert.Contains("Publish board? The approved board is ready.", draftCode);
        Assert.Contains("color-scheme: dark", siteStyles);
        Assert.Contains("catalogue-compact-action catalogue-add-action\">Edit board", boardMarkup);
        Assert.Contains("catalogue-compact-action neutral-outline-action\">Finish editing", boardMarkup);
        Assert.Contains("catalogue-compact-action warning-outline-action\">Take over editing", boardMarkup);
        Assert.Contains("catalogue-compact-action btn-outline-danger\">Remove tile", boardMarkup);
        Assert.Contains(".board-page .board-editor", siteStyles);
        Assert.Contains("align-items: stretch", siteStyles);
        Assert.DoesNotContain("Optional settings for reviewing proof", boardMarkup);
        Assert.Contains("Manual total EHB estimate", boardMarkup);
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
