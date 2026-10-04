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
}
