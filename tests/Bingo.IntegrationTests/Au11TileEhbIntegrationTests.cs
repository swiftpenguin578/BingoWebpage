using System.Net;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task Au11SetChangeResetAndApprovalRetainCalculatedBaselineAndCatalogue()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        foreach (var estimate in new decimal?[] { 12m, 25m, null, 8m })
        {
            var displayed = await client.GetStringAsync(fixture.Path);
            using var response = await PostAsync(client, fixture.Path + "?handler=EditTile", displayed,
                Au11EditFields(fixture, ApprovalBatchInput(displayed, "BoardVersion"), estimate));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var view = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
            Assert.Equal(estimate ?? 1m, view.Tiles.Single().Ehb);
            Assert.Equal(estimate, view.TileEditors.Single().ManualEhb);
            Assert.Equal(1m, view.TileEditors.Single().CalculatedEhb);
            Assert.All(view.Lines, line => Assert.Equal(estimate ?? 1m, line.Ehb));
            Assert.Equal(estimate ?? 1m, view.Statistics!.TotalEhb);
        }
        var form = await client.GetStringAsync(fixture.Path);
        var result = await PostApprovalBatchAsync(client, fixture, form);
        Assert.Contains("Board approved privately.", result);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(8m, (await verify.BoardApprovalTileSnapshots.SingleAsync()).EstimatedEhb);
        Assert.Equal(8m, (await verify.BoardApprovalSnapshots.SingleAsync()).TotalEhbEstimate);
        Assert.Equal(.1m, (await verify.SourceDrops.SingleAsync(x => x.Id == fixture.Drop.Id)).NumericProbability);
        Assert.Equal(10m, (await verify.BossActivities.SingleAsync(x => x.Id == fixture.Boss.Id)).EfficientCompletionsPerHour);
        Assert.Equal(.1m, (await verify.BoardApprovalRequirementDropSnapshots.SingleAsync()).NumericProbability);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100001")]
    [InlineData("not-a-number")]
    public async Task Au11InvalidOverrideIsRejectedThroughModelBinding(string value)
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var fields = Au11EditFields(fixture, ApprovalBatchInput(displayed, "BoardVersion"), null);
        fields["TileDraft.ManualEhb"] = value;
        using var response = await PostAsync(client, fixture.Path + "?handler=EditTile", displayed, fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await verify.TileTemplates.SingleAsync()).ManualEhbOverride);
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == fixture.Event.Id).ToListAsync());
    }

    [Fact]
    public async Task Au11StaleEditorCannotReplaceAnOverride()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var version = ApprovalBatchInput(displayed, "BoardVersion");
        using var first = await PostAsync(client, fixture.Path + "?handler=EditTile", displayed, Au11EditFields(fixture, version, 9m));
        using var stale = await PostAsync(client, fixture.Path + "?handler=EditTile", displayed, Au11EditFields(fixture, version, 20m));
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(9m, (await verify.TileTemplates.SingleAsync()).ManualEhbOverride);
    }

    [Theory]
    [InlineData(SubmissionStatus.Pending)]
    [InlineData(SubmissionStatus.Rejected)]
    [InlineData(SubmissionStatus.Approved)]
    [InlineData(SubmissionStatus.Reversed)]
    public async Task Au11SubmittedEvidenceLocksOverrideAndPreservesPublishedSnapshot(SubmissionStatus status)
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await AddCompletionAsync(fixture);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Entry(await setup.Submissions.SingleAsync()).Property(x => x.Status).CurrentValue = status;
            if (status != SubmissionStatus.Approved) setup.SubmissionContributions.RemoveRange(await setup.SubmissionContributions.ToListAsync());
            await setup.SaveChangesAsync();
        }
        var view = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using var edit = new ApplicationDbContext(options);
        var page = Page(edit, fixture.Admin.Id);
        page.BoardVersion = view.BoardView!.Version;
        page.TileDraft = new BoardModel.TileDraftInput
        {
            TileId = fixture.Tile.Id,
            Name = fixture.Tile.NameSnapshot,
            ManualEhb = 19m,
            ChangeManualEhbOverride = true,
            Requirements = [new() { RequirementId = fixture.Requirement.Id, Kind = "drops", Target = 1,
                BossIds = [fixture.Boss.Id], DropIds = [fixture.Drop.Id] }]
        };
        Assert.IsType<RedirectToPageResult>(await page.OnPostEditTileAsync(fixture.Event.Id, CancellationToken.None));
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await verify.TileTemplates.SingleAsync()).ManualEhbOverride);
        Assert.Equal(1m, (await verify.BoardApprovalTileSnapshots.SingleAsync()).EstimatedEhb);
        Assert.Contains("submitted evidence", string.Join(" ", page.TempData.Values));
    }

    [Fact]
    public async Task Au11EffectiveSnapshotFeedsWeightedPartialCreditAndPlayerTotalsWithoutDoubleCounting()
    {
        var fixture = await SeedApprovalBatchAsync();
        var now = CompletionFixtureNow;
        Guid secondId;
        await using (var setup = new ApplicationDbContext(options))
        {
            var template = await setup.TileTemplates.SingleAsync();
            template.Update(template.Name, template.Description, ObjectiveType.DropRequirements, "", 30m);
            setup.Entry(await setup.BoardRequirementSnapshots.SingleAsync()).Property(x => x.TargetContribution).CurrentValue = 3;
            var firstDrop = await setup.BoardRequirementDropSnapshots.SingleAsync();
            setup.Entry(firstDrop).Property(x => x.CreditedWeight).CurrentValue = 2;
            var second = new BoardRequirementSnapshot(Guid.NewGuid(), fixture.Tile.Id, 2, 2, true, false, "Second", false);
            secondId = second.Id;
            setup.AddRange(second, new BoardRequirementBossSnapshot(Guid.NewGuid(), second.Id, fixture.Boss.Id, fixture.Boss.Name, 10m),
                new BoardRequirementDropSnapshot(Guid.NewGuid(), second.Id, fixture.Drop.Id, fixture.Item.Id, fixture.Boss.Name, fixture.Item.Name, "1/10", .1m, null, null));
            await setup.SaveChangesAsync();
        }
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var form = await client.GetStringAsync(fixture.Path);
        Assert.Contains("Board approved privately.", await PostApprovalBatchAsync(client, fixture, form));
        await using (var publish = new ApplicationDbContext(options))
        {
            (await publish.Boards.SingleAsync()).Publish(now);
            publish.Entry(await publish.Events.SingleAsync()).Property(x => x.State).CurrentValue = Bingo.Domain.Events.EventState.SignupClosed;
            await publish.SaveChangesAsync();
        }
        await AddPublicBoardTeamAsync(fixture);
        foreach (var (requirementId, amount, expected) in new[] { (fixture.Requirement.Id, 2, 12m), (fixture.Requirement.Id, 2, 18m), (secondId, 2, 30m) })
        {
            await using (var setup = new ApplicationDbContext(options))
            {
                var team = await setup.Teams.SingleAsync();
                var player = await setup.EventParticipants.SingleAsync();
                var character = await setup.OsrsCharacters.SingleAsync();
                var drop = await setup.BoardRequirementDropSnapshots.SingleAsync(x => x.RequirementId == requirementId);
                var at = now.AddMinutes(await setup.Submissions.CountAsync());
                var submission = new Submission(Guid.NewGuid(), fixture.Event.Id, team.Id, fixture.Tile.Id, requirementId, drop.Id,
                    player.Id, character.Id, character.DisplayName, fixture.Admin.Id, amount, at, null, null);
                submission.Approve(amount, at);
                setup.AddRange(submission, new SubmissionContribution(Guid.NewGuid(), submission.Id, team.Id, requirementId, drop.Id, player.Id, amount, at));
                await setup.SaveChangesAsync();
            }
            await using var read = new ApplicationDbContext(options);
            var board = await new PublicBoardService(read, TimeProvider.System).GetEventBoardAsync(fixture.Event.Slug);
            Assert.NotNull(board);
            Assert.Equal(30m, board.TotalBoardEhb);
            Assert.Equal(expected, board.Teams.Single().Progress.EhbTiebreak);
            Assert.Equal(expected, board.PlayerLeaderboard.Single().EstimatedEhb);
            Assert.Equal(expected, board.Teams.Single().Progress.Players.Single().EstimatedEhb);
        }
    }

    private static Dictionary<string, string> Au11EditFields(ApprovalBatchFixture fixture, string version, decimal? estimate) => new()
    {
        ["BoardVersion"] = version,
        ["TileDraft.ChangeManualEhbOverride"] = "true",
        ["TileDraft.TileId"] = fixture.Tile.Id.ToString(),
        ["TileDraft.Name"] = fixture.Tile.NameSnapshot,
        ["TileDraft.ManualEhb"] = estimate?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
        ["TileDraft.Requirements[0].Kind"] = "drops",
        ["TileDraft.Requirements[0].Target"] = "1",
        ["TileDraft.Requirements[0].BossIds"] = fixture.Boss.Id.ToString(),
        ["TileDraft.Requirements[0].DropIds"] = fixture.Drop.Id.ToString()
    };
}
