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
        Assert.Contains("class=\"tile-editor-section-heading\"><h3>Tile details</h3>", boardMarkup);
        Assert.Contains("class=\"information-callout tile-objective-guide\" role=\"note\"", boardMarkup);
        Assert.Contains("Add another objective", boardMarkup);
        Assert.Contains("class=\"btn admin-button-secondary add-objective-button\"", boardMarkup);
        Assert.Contains("class=\"manual-ehb-override\"><summary><span>Manual EHB override</span>", boardMarkup);
        Assert.Contains("Bosses or activities", requirementMarkup);
        Assert.Contains("class=\"boss-picker-label\"", requirementMarkup);
        Assert.Contains("bossPickerLabel.textContent", boardMarkup);
        Assert.DoesNotContain("bossSummary.textContent", boardMarkup, StringComparison.Ordinal);
        Assert.Contains("Eligible drops", requirementMarkup);
        Assert.Contains("Counting options", requirementMarkup);
        Assert.Contains("class=\"objective-counting-options\"><div class=\"counting-options-heading\"", requirementMarkup);
        Assert.DoesNotContain("<details class=\"objective-counting-options\"", requirementMarkup, StringComparison.Ordinal);
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
        Assert.DoesNotContain("board-compact-page-heading", boardMarkup);
        Assert.DoesNotContain("<label class=\"admin-field\"", boardMarkup);
        Assert.DoesNotContain("<label class=\"admin-field\"", requirementMarkup);
        Assert.Contains("class=\"admin-status-pill @boardStatusModifier\"", boardMarkup);
        Assert.Contains("class=\"btn admin-button-secondary\" asp-page=\"BoardPreview\"", boardMarkup);
        Assert.Contains("Publish board? The approved board is ready.", draftCode);
        Assert.Contains("color-scheme: dark", siteStyles);
        Assert.Contains("admin-button-secondary\">Edit board", boardMarkup);
        Assert.Contains("admin-button-secondary\">Approve board", boardMarkup);
        Assert.Contains("admin-button-secondary\">Finish editing", boardMarkup);
        Assert.Contains("admin-button-secondary\">Take over editing", boardMarkup);
        Assert.Contains("admin-button-secondary action-danger-outline\">Remove tile", boardMarkup);
        Assert.Contains(".board-page .board-editor", siteStyles);
        Assert.Contains("align-items: stretch", siteStyles);
        Assert.DoesNotContain("Optional settings for reviewing proof", boardMarkup);
        Assert.Contains("Manual total EHB estimate", boardMarkup);
        Assert.Contains("width: min(41rem, calc(100vw - 2rem));", siteStyles);
        Assert.DoesNotContain(".admin-shell-body .board-page .tile-dialog {\n  width: min(54rem", siteStyles);
        Assert.Contains("admin-route-dialog-close", boardMarkup);
        Assert.Contains("action-danger-outline", boardMarkup);
        Assert.DoesNotContain("catalogue-compact-action", boardMarkup);
        Assert.DoesNotContain("catalogue-compact-action", requirementMarkup);
        Assert.Contains("admin-shell-body .board-page .tile-dialog-section", siteStyles);
        Assert.Contains("var(--admin-focus-accent-strong)", siteStyles);
        Assert.Contains("<details class=\"panel line-summary team-workload-summary\" open>", boardMarkup);
        Assert.Contains(".admin-shell-body .board-page .team-workload-summary > div {\n  border-bottom: 0;\n}", siteStyles);
        Assert.Contains(".admin-shell-body .board-page .eligible-drop-group {\n  color: var(--admin-text-soft);\n  background: var(--admin-surface-raised);\n  border: 0;\n  border-radius: 0.75rem;\n  padding: 1.25rem;\n  gap: 0.75rem;\n}", siteStyles);
        Assert.Contains(".admin-shell-body .board-page .eligible-drop-option {\n  display: flex;", siteStyles);
        Assert.Contains("border: 1px solid var(--admin-border-strong);", siteStyles);
        Assert.Contains("class=\"eligible-drop-heading\"><strong>Eligible drops</strong><div class=\"eligible-drop-actions\"", requirementMarkup);
        Assert.Contains(".admin-shell-body .board-page .eligible-drop-option:has(input:checked)", siteStyles);
        Assert.Contains("position: absolute", siteStyles);
        Assert.Contains("color: var(--admin-muted);\n  font-size: 0.625rem;\n  font-weight: 700;\n  letter-spacing: 0.1em;\n  line-height: 1.2;\n  text-transform: uppercase;", siteStyles);
        Assert.Contains("event.target !== dialog || window.matchMedia?.('(max-width: 900px)')?.matches", boardMarkup);
        Assert.Contains("dialog.close();", boardMarkup);
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
