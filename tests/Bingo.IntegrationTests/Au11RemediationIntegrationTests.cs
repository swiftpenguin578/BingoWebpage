using System.Net;
using Bingo.Domain.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Au11A1CurrentPageKindSwitchClearsAndDropSavePreservesOverride(bool switchKind)
    {
        var fixture = await SeedApprovalBatchAsync(manual: switchKind);
        if (!switchKind)
        {
            await using var setup = new ApplicationDbContext(options);
            var template = await setup.TileTemplates.SingleAsync();
            template.Update(template.Name, template.Description, ObjectiveType.DropRequirements, "", 12m);
            await setup.SaveChangesAsync();
        }
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var fields = Au11EditFields(fixture, ApprovalBatchInput(displayed, "BoardVersion"), switchKind ? 7m : null);
        fields.Remove("TileDraft.ChangeManualEhbOverride");
        if (!switchKind) fields.Remove("TileDraft.ManualEhb");
        using var response = await PostAsync(client, fixture.Path + "?handler=EditTile", displayed, fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.TileTemplates.SingleAsync();
        Assert.Equal(ObjectiveType.DropRequirements, saved.ObjectiveType);
        Assert.Equal(switchKind ? null : (decimal?)12m, saved.ManualEhbOverride);
        Assert.Equal(switchKind ? 1m : 12m, (await verify.BoardTiles.SingleAsync()).EstimatedEhbSnapshot);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Au11A3DiscardComparesPublishedAutomaticEstimateAtStoredPrecision(bool publishedOverride)
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        var frozenEhb = publishedOverride ? 5m : 3.3333m;
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Entry(await setup.BoardApprovalRequirementBossSnapshots.SingleAsync()).Property(x => x.EfficientRate).CurrentValue = 3m;
            setup.Entry(await setup.BoardApprovalTileSnapshots.SingleAsync()).Property(x => x.EstimatedEhb).CurrentValue = frozenEhb;
            setup.Entry(await setup.BoardApprovalSnapshots.SingleAsync()).Property(x => x.TotalEhbEstimate).CurrentValue = frozenEhb;
            var template = await setup.TileTemplates.SingleAsync();
            template.Update(template.Name, "Private edit", ObjectiveType.DropRequirements, "", 99m);
            await setup.SaveChangesAsync();
        }
        var view = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using (var discard = new ApplicationDbContext(options))
        {
            var page = Page(discard, fixture.Admin.Id);
            page.BoardVersion = view.BoardView!.Version;
            await page.OnPostDiscardCorrectionAsync(fixture.Event.Id, true, CancellationToken.None);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(publishedOverride ? 5m : (decimal?)null, (await verify.TileTemplates.SingleAsync()).ManualEhbOverride);
        Assert.Equal(frozenEhb, (await verify.BoardTiles.SingleAsync()).EstimatedEhbSnapshot);
        Assert.Equal(frozenEhb, (await verify.BoardApprovalTileSnapshots.SingleAsync()).EstimatedEhb);
        Assert.False((await verify.Boards.SingleAsync()).PublishedCorrectionInProgress);
    }

}
