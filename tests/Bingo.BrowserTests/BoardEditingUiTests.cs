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
