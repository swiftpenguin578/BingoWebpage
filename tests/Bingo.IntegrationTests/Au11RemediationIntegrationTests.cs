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

    [Fact]
    public async Task Au11R2MidpointDiscardMatchesPostgresStoredPrecision()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        // Frozen calculation: 33 guaranteed drops at 32 completions/hour = 1.03125.
        const decimal unroundedEhb = 1.03125m;
        const decimal storedEhb = 1.0313m;
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Entry(await setup.BoardApprovalRequirementSnapshots.SingleAsync()).Property(x => x.TargetContribution).CurrentValue = 33;
            setup.Entry(await setup.BoardApprovalRequirementBossSnapshots.SingleAsync()).Property(x => x.EfficientRate).CurrentValue = 32m;
            setup.Entry(await setup.BoardApprovalRequirementDropSnapshots.SingleAsync()).Property(x => x.NumericProbability).CurrentValue = 1m;
            setup.Entry(await setup.BoardApprovalTileSnapshots.SingleAsync()).Property(x => x.EstimatedEhb).CurrentValue = unroundedEhb;
            setup.Entry(await setup.BoardApprovalSnapshots.SingleAsync()).Property(x => x.TotalEhbEstimate).CurrentValue = unroundedEhb;
            var template = await setup.TileTemplates.SingleAsync();
            template.Update(template.Name, "Private correction", ObjectiveType.DropRequirements, "", 99m);
            await setup.SaveChangesAsync();
        }
        await using (var roundTrip = new ApplicationDbContext(options))
            Assert.Equal(storedEhb, (await roundTrip.BoardApprovalTileSnapshots.SingleAsync()).EstimatedEhb);
        var view = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using (var discard = new ApplicationDbContext(options))
        {
            var page = Page(discard, fixture.Admin.Id);
            page.BoardVersion = view.BoardView!.Version;
            await page.OnPostDiscardCorrectionAsync(fixture.Event.Id, true, CancellationToken.None);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await verify.TileTemplates.SingleAsync()).ManualEhbOverride);
        Assert.Equal(storedEhb, (await verify.BoardTiles.SingleAsync()).EstimatedEhbSnapshot);
        Assert.Equal(storedEhb, (await verify.BoardApprovalTileSnapshots.SingleAsync()).EstimatedEhb);
        Assert.False((await verify.Boards.SingleAsync()).PublishedCorrectionInProgress);
    }

    [Theory]
    [InlineData("en", "0.5", true)]
    [InlineData("da", "0.5", true)]
    [InlineData("en", "0,5", false)]
    [InlineData("da", "0,5", false)]
    [InlineData("da", "1,000.5", false)]
    [InlineData("da", "invalid", false)]
    [InlineData("da", "0.00001", false)]
    [InlineData("da", "100001", false)]
    public async Task Au11A4HalfEhbPostUsesHtmlDecimalSyntax(string language, string input, bool accepted)
    {
        var fixture = await SeedApprovalBatchAsync(manual: true);
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var fields = Au11EditFields(fixture, ApprovalBatchInput(displayed, "BoardVersion"), .5m);
        fields["TileDraft.ManualEhb"] = input;
        fields["TileDraft.Requirements[0].Kind"] = "challenge";
        fields["TileDraft.Requirements[0].Description"] = "Half EHB challenge";
        fields.Remove("TileDraft.Requirements[0].BossIds");
        fields.Remove("TileDraft.Requirements[0].DropIds");
        using var response = await PostAsync(client, fixture.Path + "?handler=EditTile", displayed, fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(accepted ? .5m : 7m, (await verify.TileTemplates.SingleAsync()).ManualEhbOverride);
        Assert.Equal(accepted ? .5m : 7m, (await verify.BoardTiles.SingleAsync()).EstimatedEhbSnapshot);
        if (!accepted) Assert.Contains("alert", await client.GetStringAsync(fixture.Path), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("da")]
    public async Task Au11A4BlankDropOverrideRemainsNullable(string language)
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var fields = Au11EditFields(fixture, ApprovalBatchInput(displayed, "BoardVersion"), null);
        fields["TileDraft.ManualEhb"] = "";
        fields["TileDraft.Name"] = "Blank estimate accepted";
        using var response = await PostAsync(client, fixture.Path + "?handler=EditTile", displayed, fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.TileTemplates.SingleAsync();
        Assert.Null(saved.ManualEhbOverride);
        Assert.Equal("Blank estimate accepted", saved.Name);
    }
}
