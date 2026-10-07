namespace Bingo.BrowserTests;

// U7 (brief 88, A10): the legacy inline Board script and its dialog markup are replaced
// by the admin-board.js page module (Board.dc.html). These source checks keep the same
// behaviours on the new page: opening the Board never releases or takes the lease by
// itself, the lease is changed only by explicit chip actions, cell actions survive
// re-rendering, and every command goes through the shared transport.
public sealed class BoardEditingUiTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root, .. parts]));
    private static string Markup => Read("src", "Bingo.Web", "Pages", "Admin", "Events", "Board.cshtml");
    private static string Module => Read("src", "Bingo.Web", "wwwroot", "js", "admin-board.js");

    [Fact]
    public void BoardOpensWithoutChangingTheLeaseAndChangesItOnlyThroughExplicitChipActions()
    {
        Assert.DoesNotContain("<script>", Markup);
        Assert.Contains("ViewData[\"PageFamily\"] = \"board\";", Markup);
        Assert.Contains("src=\"~/js/admin-board.js\" asp-append-version=\"true\" data-admin-page-script", Markup);
        Assert.Contains("name=\"BoardVersion\" value=\"@(view?.Version ?? 0)\"", Markup);
        Assert.Contains("ctx.command('AcquireEditing', {})", Module);
        Assert.Contains("ctx.command('ReleaseEditing', {})", Module);
        Assert.Contains("ctx.command('TakeEditing', {})", Module);
        Assert.Contains("Editing control renews while you work and lapses after five minutes without board activity.", Module);
        // RC05 B2: the takeover confirmation never says anyone's draft is lost.
        Assert.Contains("Anything they haven’t saved stays in their browser; nothing on the board changes.", Module);
        Assert.DoesNotContain("location.reload", Module);
        Assert.DoesNotContain("keepalive: true", Module);
    }

    [Fact]
    public void CellActionsAreBoundOnEveryRenderAndKeyboardMovesUseOneDelegatedHandler()
    {
        Assert.Contains("cell = button('bcell btile'", Module);
        Assert.Contains("() => open(pos)", Module);
        Assert.Contains("on(root, 'keydown', event => {", Module);
        Assert.Contains("const outcome = await ctx.command('Move', { sourceId: tile.id, targetPosition: to });", Module);
        Assert.Contains("window.AdminFetch.request(ctx.url(handler)", Module);
        Assert.Contains("ui.busy(", Module);
    }

    private static string Editor => Read("src", "Bingo.Web", "wwwroot", "js", "admin-board-editor.js");

    [Fact]
    public void TileEditorSendsExplicitOverrideIntentAndSettlesUnknownSavesByTheCompleteIntent()
    {
        // AU11: every set, change and reset sends ChangeManualEhbOverride explicitly.
        Assert.Contains("form.append('TileDraft.ChangeManualEhbOverride', change ? 'true' : 'false');", Editor);
        // RC05 B1: an unknown save compares the whole intent with the Readback and keeps the draft.
        Assert.Contains("if (state && matches(state, want))", Editor);
        Assert.DoesNotContain("safe to try again", Editor);
        // RC05 B2: the opening version is kept; saving on a changed board needs an explicit choice.
        Assert.Contains("form.append('BoardVersion', String(ed.openingVersion));", Editor);
        Assert.Contains("ed.openingVersion = ctx.view.version;", Editor);
        // RC05 B5: the drawer lives in the query.
        Assert.Contains("ui.setUrl({ tile: posName(pos, ctx.view.cols) }, TILE_SCHEMA, { record: true });", Editor);
    }

    private static string Publication => Read("src", "Bingo.Web", "wwwroot", "js", "admin-board-publication.js");

    [Fact]
    public void PublicationUsesOneConfirmationEachAndTheReasonDialogIsTheCorrectionConfirmation()
    {
        // Approve has no confirmation; Publish has one and sends confirmed=true.
        Assert.Contains("ctx.command('ApproveState', { ApprovalCatalogueFingerprint: ctx.fingerprint(), confirmed: false })", Publication);
        Assert.Contains("ctx.command('PublishState', { confirmed: true })", Publication);
        // The correction-reason dialog is the confirmation: no checkbox, reason at most 2,000 characters.
        Assert.Contains("ctx.command('CorrectPublished', { confirmed: true, reason }", Publication);
        Assert.Contains("r.length > 2000 ? t('Use 2,000 characters or fewer.')", Publication);
        Assert.DoesNotContain("type = 'checkbox'", Publication);
        // Discard warns that all unpublished edits are lost; publish and discard are different outcomes (RC05 B1).
        Assert.Contains("All unpublished edits in this correction will be lost", Publication);
        Assert.Contains("s.activeApprovalId === ctx.view.activeApprovalId", Publication);
        Assert.Contains("s.activeApprovalId !== ctx.view.activeApprovalId", Publication);
        // AU19: empty positions collapse into one item that jumps to the first empty position.
        Assert.Contains("t('({0} empty).', empty.length)", Publication);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
