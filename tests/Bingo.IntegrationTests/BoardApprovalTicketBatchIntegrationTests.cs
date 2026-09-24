using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Evidence;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Theory]
    [InlineData("boss", false)]
    [InlineData("drop", false)]
    [InlineData("item", false)]
    [InlineData("item", true)]
    public async Task BoardApprovalBatchRejectsStaleRenderedCatalogueThenApprovesRefreshedInputs(string changed, bool correction)
    {
        var fixture = await SeedApprovalBatchAsync(correction: correction);
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        await using (var edit = new ApplicationDbContext(options))
        {
            if (changed == "boss")
                (await edit.BossActivities.SingleAsync(x => x.Id == fixture.Boss.Id)).Update("Reviewed boss", "Boss", 20m, null, null, null, DateTimeOffset.UtcNow);
            else if (changed == "drop")
                (await edit.SourceDrops.SingleAsync(x => x.Id == fixture.Drop.Id)).Update("1/5", .2m, null, null, null, DateTimeOffset.UtcNow);
            else
                (await edit.CatalogueItems.SingleAsync(x => x.Id == fixture.Item.Id)).Update("Reviewed item", "REVIEWED ITEM", null, null);
            await edit.SaveChangesAsync();
        }
        var stale = await PostApprovalBatchAsync(client, fixture, displayed);
        Assert.Contains("The catalogue changed after you opened this board.", stale, StringComparison.Ordinal);
        await AssertApprovalBatchUnchangedAsync(fixture);
        var refreshed = await client.GetStringAsync(fixture.Path);
        Assert.NotEqual(ApprovalBatchFields(displayed)["ApprovalCatalogueFingerprint"], ApprovalBatchFields(refreshed)["ApprovalCatalogueFingerprint"]);
        var successful = await PostApprovalBatchAsync(client, fixture, refreshed);
        Assert.Contains(correction ? "Corrected board published as a replacement snapshot." : "Board approved privately.", successful, StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        var board = await verify.Boards.SingleAsync(x => x.Id == fixture.Board.Id);
        Assert.Equal(correction ? BoardState.Published : BoardState.Validated, board.State);
        var approval = await verify.BoardApprovalSnapshots.SingleAsync(x => x.Id == board.ActiveApprovalSnapshotId);
        Assert.Equal(changed == "item" ? 1m : .5m, approval.TotalEhbEstimate);
        Assert.Equal(fixture.PriorApprovalId, approval.SupersedesApprovalSnapshotId);
        if (correction)
        {
            var replacementDrop = await verify.BoardApprovalRequirementDropSnapshots
                .Join(verify.BoardApprovalRequirementSnapshots, drop => drop.ApprovalRequirementSnapshotId, requirement => requirement.Id, (drop, requirement) => new { drop, requirement })
                .Join(verify.BoardApprovalTileSnapshots.Where(tile => tile.ApprovalSnapshotId == approval.Id), pair => pair.requirement.ApprovalTileSnapshotId, tile => tile.Id, (pair, tile) => pair.drop).SingleAsync();
            Assert.Equal("Batch item", replacementDrop.ItemName); // C20 preserves the published objective facts after C23's fresh inspection.
        }
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == fixture.Event.Id && (x.Action == "board.approved" || x.Action == "board.published_corrected")).ToListAsync());
        if (fixture.PriorApprovalId is { } priorId)
        {
            Assert.Equal(1m, (await verify.BoardApprovalSnapshots.SingleAsync(x => x.Id == priorId)).TotalEhbEstimate);
            Assert.Equal("Batch item", (await verify.BoardApprovalRequirementDropSnapshots.Join(verify.BoardApprovalRequirementSnapshots, d => d.ApprovalRequirementSnapshotId, r => r.Id, (d, r) => new { d, r }).Join(verify.BoardApprovalTileSnapshots.Where(x => x.ApprovalSnapshotId == priorId), x => x.r.ApprovalTileSnapshotId, t => t.Id, (x, t) => x.d).SingleAsync()).ItemName);
        }
    }

    [Fact]
    public async Task BoardApprovalBatchIgnoresUnrelatedCatalogueChangesButRequiresTheRenderedToken()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var missing = ApprovalBatchFields(displayed); missing.Remove("ApprovalCatalogueFingerprint");
        using (var response = await PostAsync(client, fixture.Path + "?handler=Approve", displayed, missing))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await AssertApprovalBatchUnchangedAsync(fixture);
        await using (var edit = new ApplicationDbContext(options))
        {
            edit.BossActivities.Add(new BossActivity(Guid.NewGuid(), "Unrelated", "unrelated-batch", "Boss", 999m, DateTimeOffset.UtcNow));
            await edit.SaveChangesAsync();
        }
        var successful = await PostApprovalBatchAsync(client, fixture, displayed);
        Assert.Contains("Board approved privately.", successful, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BoardApprovalBatchLocksRelevantCatalogueThroughTheFinalSave()
    {
        var fixture = await SeedApprovalBatchAsync();
        var displayed = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        var barrier = new ApprovalBatchSaveBarrier();
        var approvalOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(barrier).Options;
        await using var approvalDb = new ApplicationDbContext(approvalOptions);
        var page = Page(approvalDb, fixture.Admin.Id);
        page.BoardVersion = displayed.BoardVersion;
        page.ApprovalCatalogueFingerprint = displayed.ApprovalCatalogueFingerprint;
        var approval = page.OnPostApproveAsync(fixture.Event.Id, false, CancellationToken.None);
        try
        {
            await barrier.Ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            foreach (var (table, id) in new[] { ("boss_activities", fixture.Boss.Id), ("source_drops", fixture.Drop.Id), ("catalogue_items", fixture.Item.Id) })
            {
                await using var connection = new NpgsqlConnection(database.GetConnectionString());
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await using var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '200ms'", connection, transaction);
                await timeout.ExecuteNonQueryAsync();
                await using var update = new NpgsqlCommand($"UPDATE {table} SET version = version + 1 WHERE id = @id", connection, transaction);
                update.Parameters.AddWithValue("id", id);
                var conflict = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, conflict.SqlState);
            }
        }
        finally { barrier.Release.TrySetResult(true); }
        await approval;
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(1m, (await verify.BoardApprovalSnapshots.SingleAsync(x => x.BoardId == fixture.Board.Id)).TotalEhbEstimate);
        Assert.Equal(1, await verify.BoardApprovalSnapshots.CountAsync(x => x.BoardId == fixture.Board.Id));
    }

    [Theory]
    [InlineData("position", "Fill every board position before approving the board.", "Udfyld alle boardets felter")]
    [InlineData("source", "Every catalogue objective needs an active eligible drop before approval.", "Hvert katalogmål skal have et aktivt")]
    [InlineData("identity", "A catalogue item identity changed. Reselect the affected drop", "Identiteten på et katalogitem er ændret")]
    [InlineData("estimate", "Batch tile needs automatic EHB. Correct the catalogue rates", "Batch tile kræver automatisk EHB")]
    [InlineData("manual", "Batch tile needs a positive manual EHB estimate.", "Batch tile kræver et positivt manuelt EHB-estimat")]
    [InlineData("mixed", "Use separate tiles for catalogue drops and custom challenges.", "Brug separate felter til katalogdrops")]
    public async Task BoardApprovalBatchReturnsLocalizedSafeValidationWithoutResidue(string failure, string english, string danish)
    {
        var fixture = await SeedApprovalBatchAsync(manual: failure == "manual", missingEstimate: failure is "manual" or "estimate");
        await using (var edit = new ApplicationDbContext(options))
        {
            if (failure == "position") (await edit.Boards.SingleAsync(x => x.Id == fixture.Board.Id)).Resize(1, 2, 1);
            if (failure == "source") edit.BoardRequirementDropSnapshots.RemoveRange(await edit.BoardRequirementDropSnapshots.Where(x => x.RequirementId == fixture.Requirement.Id).ToListAsync());
            if (failure == "identity")
            {
                var item = new CatalogueItem(Guid.NewGuid(), "New identity", "NEW IDENTITY"); item.SetPrice(0, CataloguePriceSource.Manual, DateTimeOffset.UtcNow); edit.CatalogueItems.Add(item);
                edit.Entry(await edit.SourceDrops.SingleAsync(x => x.Id == fixture.Drop.Id)).Property(x => x.ItemId).CurrentValue = item.Id;
            }
            if (failure == "mixed") edit.BoardRequirementSnapshots.Add(new BoardRequirementSnapshot(Guid.NewGuid(), fixture.Tile.Id, 2, 1, true, false, "Manual challenge", true));
            await edit.SaveChangesAsync();
        }
        // Each culture uses a freshly rendered real form, so validation is reached after freshness succeeds.
        await using var factory = ApprovalBatchFactory();
        foreach (var (culture, expected) in new[] { ("en", english), ("da", danish) })
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
            await LoginAsync(client, fixture.Admin.LoginName);
            var displayed = await client.GetStringAsync(fixture.Path);
            var result = await PostApprovalBatchAsync(client, fixture, displayed);
            Assert.Contains(expected, WebUtility.HtmlDecode(result), StringComparison.Ordinal);
            Assert.DoesNotContain("InvalidOperationException", result, StringComparison.Ordinal);
            await AssertApprovalBatchUnchangedAsync(fixture);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoardApprovalBatchKeepsUnexpectedFailuresGenericAndRollsBack(bool databaseFailure)
    {
        var fixture = await SeedApprovalBatchAsync();
        var failure = new ApprovalBatchFailure(databaseFailure);
        await using var factory = ApprovalBatchFactory(failure);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var result = await PostApprovalBatchAsync(client, fixture, displayed);
        Assert.True(failure.Triggered);
        Assert.Contains("The board approval could not be changed.", result, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-internal-marker", result, StringComparison.Ordinal);
        await AssertApprovalBatchUnchangedAsync(fixture);
    }

    [Fact]
    public async Task PublishedCorrectionSwitchesCompletionFactsToNewGenerationAndRetainsPriorFacts()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        var completion = await AddCompletionAsync(fixture);
        var view = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using (var approvalDb = new ApplicationDbContext(options))
        {
            var page = Page(approvalDb, fixture.Admin.Id);
            page.BoardVersion = view.BoardView!.Version;
            page.ApprovalCatalogueFingerprint = view.ApprovalCatalogueFingerprint;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(fixture.Event.Id, true, CancellationToken.None));
            Assert.Contains("Corrected board published as a replacement snapshot.", page.TempData["StatusMessage"]?.ToString(), StringComparison.Ordinal);
        }

        await using var verify = new ApplicationDbContext(options);
        var activeId = await verify.Boards.Where(value => value.Id == fixture.Board.Id).Select(value => value.ActiveApprovalSnapshotId).SingleAsync();
        Assert.NotEqual(fixture.PriorApprovalId, activeId);
        var prior = await verify.TileCompletionFacts.SingleAsync(value => value.TeamId == completion.TeamId && value.ApprovalSnapshotId == fixture.PriorApprovalId);
        var current = await verify.TileCompletionFacts.SingleAsync(value => value.TeamId == completion.TeamId && value.ApprovalSnapshotId == activeId);
        Assert.True(prior.IsComplete);
        Assert.Equal(completion.SubmittedAt, prior.CompletedAt);
        Assert.Contains(completion.SubmissionId.ToString("D"), prior.QualifyingContributionsJson!, StringComparison.OrdinalIgnoreCase);
        Assert.True(current.IsComplete);
        Assert.Equal(completion.SubmittedAt, current.CompletedAt);
        Assert.Contains(completion.SubmissionId.ToString("D"), current.QualifyingContributionsJson!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await verify.TileCompletionFacts.CountAsync(value => value.TeamId == completion.TeamId));
    }

    [Fact]
    public async Task FailedReplacementCompletionFactWriteRollsBackPublicationGenerationAndFacts()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        var completion = await AddCompletionAsync(fixture);
        var view = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        var failure = new CompletionFactFailure();
        var faultOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(failure).Options;
        await using (var approvalDb = new ApplicationDbContext(faultOptions))
        {
            var page = Page(approvalDb, fixture.Admin.Id);
            page.BoardVersion = view.BoardView!.Version;
            page.ApprovalCatalogueFingerprint = view.ApprovalCatalogueFingerprint;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(fixture.Event.Id, true, CancellationToken.None));
        }

        Assert.True(failure.Triggered);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(fixture.PriorApprovalId, await verify.Boards.Where(value => value.Id == fixture.Board.Id).Select(value => value.ActiveApprovalSnapshotId).SingleAsync());
        Assert.Equal(BoardState.Published, await verify.Boards.Where(value => value.Id == fixture.Board.Id).Select(value => value.State).SingleAsync());
        Assert.Single(await verify.BoardApprovalSnapshots.Where(value => value.BoardId == fixture.Board.Id).ToListAsync());
        var fact = await verify.TileCompletionFacts.SingleAsync(value => value.TeamId == completion.TeamId);
        Assert.Equal(fixture.PriorApprovalId, fact.ApprovalSnapshotId);
        Assert.Equal(completion.SubmittedAt, fact.CompletedAt);
        Assert.Contains(completion.SubmissionId.ToString("D"), fact.QualifyingContributionsJson!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == fixture.Event.Id && value.Action == "board.published_corrected").ToListAsync());
    }

    [Fact]
    public async Task BoardApprovalBatchReportsCommittedApprovalWhenNotificationFails()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var factory = ApprovalBatchFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<Bingo.Application.Teams.IAdminCollaborationNotifier, ApprovalBatchNotificationFailure>()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var result = await PostApprovalBatchAsync(client, fixture, displayed);
        Assert.Contains("Board approval was saved, but the live update could not be sent.", result, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-internal-marker", result, StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(BoardState.Validated, (await verify.Boards.SingleAsync(x => x.Id == fixture.Board.Id)).State);
        Assert.Single(await verify.BoardApprovalSnapshots.Where(x => x.BoardId == fixture.Board.Id).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == fixture.Event.Id && x.Action == "board.approved").ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoardApprovalBatchPreservesValidSameKindMultipleObjectives(bool manual)
    {
        var fixture = await SeedApprovalBatchAsync(manual: manual);
        await using (var edit = new ApplicationDbContext(options))
        {
            var second = new BoardRequirementSnapshot(Guid.NewGuid(), fixture.Tile.Id, 2, 1, true, false, "Second objective", manual);
            edit.BoardRequirementSnapshots.Add(second);
            if (!manual)
            {
                edit.BoardRequirementBossSnapshots.Add(new BoardRequirementBossSnapshot(Guid.NewGuid(), second.Id, fixture.Boss.Id, fixture.Boss.Name, 10m));
                edit.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(Guid.NewGuid(), second.Id, fixture.Drop.Id, fixture.Item.Id, fixture.Boss.Name, fixture.Item.Name, "1/10", .1m, null, null));
                // Simulate an old override without using the now-protected template write API.
                edit.Entry(await edit.TileTemplates.SingleAsync(x => x.Id == fixture.Template.Id)).Property(x => x.ManualEhbOverride).CurrentValue = 99m;
            }
            await edit.SaveChangesAsync();
        }
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var live = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        Assert.Equal(manual ? 7m : 2m, live.Tiles.Single().Ehb);
        if (!manual) Assert.Null(live.TileEditors.Single().ManualEhb);
        var result = await PostApprovalBatchAsync(client, fixture, displayed);
        Assert.Contains("Board approved privately.", result, StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(manual ? 7m : 2m, (await verify.BoardApprovalSnapshots.SingleAsync(x => x.BoardId == fixture.Board.Id)).TotalEhbEstimate);
    }

    [Fact]
    public async Task BlankTileDescriptionDerivesAndFreezesWeightedTargetWhileManualTextRemainsAuthored()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await AddPublicBoardTeamAsync(fixture);

        await SaveTileDescriptionAsync(fixture, "Authored wording", 2, [fixture.Drop.Id], []);
        await using (var verifyManual = new ApplicationDbContext(options))
        {
            var template = await verifyManual.TileTemplates.SingleAsync(x => x.Id == fixture.Template.Id);
            var tile = await verifyManual.BoardTiles.SingleAsync(x => x.Id == fixture.Tile.Id);
            Assert.Equal("Authored wording", template.Description);
            Assert.Equal("Authored wording", tile.DescriptionSnapshot);
            Assert.False(template.DescriptionIsAutomatic);
            Assert.False(tile.DescriptionIsAutomatic);
        }

        var secondItem = new CatalogueItem(Guid.NewGuid(), "Batch item two", "BATCH ITEM TWO");
        secondItem.SetPrice(0, CataloguePriceSource.Manual, DateTimeOffset.UtcNow);
        var secondDrop = new SourceDrop(Guid.NewGuid(), fixture.Boss.Id, secondItem.Id, "1/20", .05m, null, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(secondItem, secondDrop);
            await setup.SaveChangesAsync();
        }

        await SaveTileDescriptionAsync(fixture, null, 5, [fixture.Drop.Id, secondDrop.Id], [(fixture.Drop.Id, 5)]);
        var automaticDraft = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        Assert.Equal("Collect 5 eligible drops from Batch boss", Assert.Single(automaticDraft.Tiles).Description);
        Assert.Equal(string.Empty, Assert.Single(automaticDraft.TileEditors).Description);

        await using (var verifyAutomatic = new ApplicationDbContext(options))
        {
            var template = await verifyAutomatic.TileTemplates.SingleAsync(x => x.Id == fixture.Template.Id);
            var tile = await verifyAutomatic.BoardTiles.SingleAsync(x => x.Id == fixture.Tile.Id);
            Assert.Equal(string.Empty, template.Description);
            Assert.Equal(string.Empty, tile.DescriptionSnapshot);
            Assert.True(template.DescriptionIsAutomatic);
            Assert.True(tile.DescriptionIsAutomatic);
            Assert.True((await verifyAutomatic.BoardRequirementSnapshots.SingleAsync(x => x.BoardTileId == fixture.Tile.Id)).AllowHigherWeightings);
        }

        var approvalView = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using (var approveDb = new ApplicationDbContext(options))
        {
            var page = Page(approveDb, fixture.Admin.Id);
            page.BoardVersion = approvalView.BoardView!.Version;
            page.ApprovalCatalogueFingerprint = approvalView.ApprovalCatalogueFingerprint;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(fixture.Event.Id, true, CancellationToken.None));
            Assert.Equal("Corrected board published as a replacement snapshot. The prior public snapshot remains in history.", page.TempData["StatusMessage"]?.ToString());
        }

        const string frozenDescription = "Collect 5 eligible drops from Batch boss";
        await using (var rename = new ApplicationDbContext(options))
        {
            (await rename.CatalogueItems.SingleAsync(x => x.Id == secondItem.Id)).Update("Renamed batch item", "RENAMED BATCH ITEM", null, null);
            await rename.SaveChangesAsync();
        }

        await using var publicDb = new ApplicationDbContext(options);
        var activeApprovalId = (await publicDb.Boards.SingleAsync(x => x.Id == fixture.Board.Id)).ActiveApprovalSnapshotId;
        var approval = await publicDb.BoardApprovalSnapshots.SingleAsync(x => x.Id == activeApprovalId);
        var frozen = await publicDb.BoardApprovalTileSnapshots.SingleAsync(x => x.ApprovalSnapshotId == approval.Id);
        Assert.Equal(frozenDescription, frozen.Description);
        Assert.True(frozen.DescriptionIsAutomatic);
        var publicBoard = await new Bingo.Infrastructure.Boards.PublicBoardService(publicDb, TimeProvider.System).GetEventBoardAsync(fixture.Event.Slug);
        var team = Assert.Single(publicBoard!.Teams);
        var publicTile = Assert.Single(team.Tiles);
        Assert.Equal(frozenDescription, publicTile.Description);
        var publicDetails = await new Bingo.Infrastructure.Boards.PublicBoardService(publicDb, TimeProvider.System).GetTileAsync(fixture.Event.Slug, team.TeamSlug, fixture.Tile.Id);
        Assert.Equal(frozenDescription, publicDetails!.Description);
    }

    [Fact]
    public async Task DerivedDescriptionMigrationMarksOnlyLegacyWorkingBlanksAndLeavesFrozenTextUntouched()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        var manualTemplate = new TileTemplate(Guid.NewGuid(), "Manual tile", "Keep this authored template", ObjectiveType.DropRequirements, "", null);
        var manualTile = new BoardTile(Guid.NewGuid(), fixture.Board.Id, manualTemplate.Id, 0, 1, manualTemplate.Name, "Keep this authored tile", "", 1m);
        await using (var setup = new ApplicationDbContext(options))
        {
            (await setup.Boards.SingleAsync(x => x.Id == fixture.Board.Id)).Resize(1, 2, 1);
            setup.AddRange(manualTemplate, manualTile);
            await setup.SaveChangesAsync();
        }

        const string previousMigration = "20260922163951_AddWiseOldManUpdateAllSlots";
        await using (var downgrade = new ApplicationDbContext(options))
            await downgrade.GetService<IMigrator>().MigrateAsync(previousMigration);

        const string legacyTemplateBlank = " \t\n ";
        const string legacyTileBlank = "\t  \n";
        await using (var legacy = new ApplicationDbContext(options))
        {
            await legacy.Database.ExecuteSqlInterpolatedAsync($"UPDATE tile_templates SET description = {legacyTemplateBlank} WHERE id = {fixture.Template.Id}");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_tiles SET description_snapshot = {legacyTileBlank} WHERE id = {fixture.Tile.Id}");
        }

        await using (var upgrade = new ApplicationDbContext(options))
            await upgrade.GetService<IMigrator>().MigrateAsync();

        await using var verify = new ApplicationDbContext(options);
        var automaticTemplate = await verify.TileTemplates.SingleAsync(x => x.Id == fixture.Template.Id);
        var automaticTile = await verify.BoardTiles.SingleAsync(x => x.Id == fixture.Tile.Id);
        Assert.Equal(legacyTemplateBlank, automaticTemplate.Description);
        Assert.True(automaticTemplate.DescriptionIsAutomatic);
        Assert.Equal(legacyTileBlank, automaticTile.DescriptionSnapshot);
        Assert.True(automaticTile.DescriptionIsAutomatic);

        var retainedManualTemplate = await verify.TileTemplates.SingleAsync(x => x.Id == manualTemplate.Id);
        var retainedManualTile = await verify.BoardTiles.SingleAsync(x => x.Id == manualTile.Id);
        Assert.Equal("Keep this authored template", retainedManualTemplate.Description);
        Assert.False(retainedManualTemplate.DescriptionIsAutomatic);
        Assert.Equal("Keep this authored tile", retainedManualTile.DescriptionSnapshot);
        Assert.False(retainedManualTile.DescriptionIsAutomatic);

        var frozen = await verify.BoardApprovalTileSnapshots.SingleAsync(x => x.ApprovalSnapshotId == fixture.PriorApprovalId);
        Assert.Equal("Batch objective", frozen.Description);
        Assert.False(frozen.DescriptionIsAutomatic);
    }

    [Fact]
    public async Task DiscardCorrectionRestoresPreviousManualDescriptionAndMode()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await SaveTileDescriptionAsync(fixture, null, 1, [fixture.Drop.Id], []);
        await using (var changed = new ApplicationDbContext(options))
        {
            Assert.True((await changed.TileTemplates.SingleAsync(x => x.Id == fixture.Template.Id)).DescriptionIsAutomatic);
        }

        var view = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using (var discardDb = new ApplicationDbContext(options))
        {
            var page = Page(discardDb, fixture.Admin.Id);
            page.BoardVersion = view.BoardView!.Version;
            Assert.IsType<RedirectToPageResult>(await page.OnPostDiscardCorrectionAsync(fixture.Event.Id, true, CancellationToken.None));
        }

        await using var verify = new ApplicationDbContext(options);
        var tile = await verify.BoardTiles.SingleAsync(x => x.Id == fixture.Tile.Id);
        var template = await verify.TileTemplates.SingleAsync(x => x.Id == fixture.Template.Id);
        Assert.Equal("Batch objective", tile.DescriptionSnapshot);
        Assert.Equal("Batch objective", template.Description);
        Assert.False(tile.DescriptionIsAutomatic);
        Assert.False(template.DescriptionIsAutomatic);
        var activeApproval = await verify.BoardApprovalTileSnapshots.SingleAsync(x => x.ApprovalSnapshotId == fixture.PriorApprovalId);
        Assert.Equal("Batch objective", activeApproval.Description);
        Assert.False(activeApproval.DescriptionIsAutomatic);
    }

    [Fact]
    public async Task DiscardAutomaticCorrectionRestoresBlankWorkingTextWithoutChangingFrozenCopy()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await SaveTileDescriptionAsync(fixture, null, 1, [fixture.Drop.Id], []);

        var approvalView = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using (var approveDb = new ApplicationDbContext(options))
        {
            var page = Page(approveDb, fixture.Admin.Id);
            page.BoardVersion = approvalView.BoardView!.Version;
            page.ApprovalCatalogueFingerprint = approvalView.ApprovalCatalogueFingerprint;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(fixture.Event.Id, true, CancellationToken.None));
        }

        const string frozenDescription = "Collect 1 Batch item";
        await using (var reopenDb = new ApplicationDbContext(options))
        {
            var page = Page(reopenDb, fixture.Admin.Id);
            Assert.IsType<RedirectToPageResult>(await page.OnPostCorrectPublishedAsync(fixture.Event.Id, true, "Test automatic description restoration", CancellationToken.None));
        }
        await SaveTileDescriptionAsync(fixture, "Private correction wording", 1, [fixture.Drop.Id], []);

        var discardView = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using (var discardDb = new ApplicationDbContext(options))
        {
            var page = Page(discardDb, fixture.Admin.Id);
            page.BoardVersion = discardView.BoardView!.Version;
            Assert.IsType<RedirectToPageResult>(await page.OnPostDiscardCorrectionAsync(fixture.Event.Id, true, CancellationToken.None));
        }

        await using var verify = new ApplicationDbContext(options);
        var tile = await verify.BoardTiles.SingleAsync(x => x.Id == fixture.Tile.Id);
        var template = await verify.TileTemplates.SingleAsync(x => x.Id == fixture.Template.Id);
        Assert.Equal(string.Empty, tile.DescriptionSnapshot);
        Assert.True(tile.DescriptionIsAutomatic);
        Assert.Equal(string.Empty, template.Description);
        Assert.True(template.DescriptionIsAutomatic);
        var activeApprovalId = (await verify.Boards.SingleAsync(x => x.Id == fixture.Board.Id)).ActiveApprovalSnapshotId;
        var frozen = await verify.BoardApprovalTileSnapshots.SingleAsync(x => x.ApprovalSnapshotId == activeApprovalId);
        Assert.Equal(frozenDescription, frozen.Description);
        Assert.True(frozen.DescriptionIsAutomatic);
        var publication = await verify.PublishedObjectivesAsync(fixture.Event.Id);
        Assert.Equal(frozenDescription, Assert.Single(publication!.Tiles).DescriptionSnapshot);
    }

    [Theory]
    [InlineData("en", "Auto-generated from tile requirements")]
    [InlineData("da", "Genereres automatisk ud fra tile-krav")]
    public async Task BoardDescriptionPlaceholderUsesRequestedCulture(string culture, string expected)
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        await LoginAsync(client, fixture.Admin.LoginName);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(fixture.Path));

        Assert.Contains($"placeholder=\"{expected}\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CreateTile", false)]
    [InlineData("EditTile", false)]
    [InlineData("CreateTile", true)]
    [InlineData("EditTile", true)]
    public async Task BoardApprovalBatchRejectsForgedManualOverrideOrMixedSavesAtomically(string handler, bool mixed)
    {
        var fixture = await SeedApprovalBatchAsync(missingEstimate: true);
        await using (var edit = new ApplicationDbContext(options))
        {
            (await edit.Boards.SingleAsync(x => x.Id == fixture.Board.Id)).Resize(1, 2, 1);
            await edit.SaveChangesAsync();
        }
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        var values = new Dictionary<string, string>
        {
            ["BoardVersion"] = ApprovalBatchInput(displayed, "BoardVersion"),
            ["TileDraft.TileId"] = fixture.Tile.Id.ToString(),
            ["TileDraft.Position"] = "1",
            ["TileDraft.Name"] = "Forged change",
            ["TileDraft.ManualEhb"] = "99",
            ["TileDraft.Requirements[0].Kind"] = "drops",
            ["TileDraft.Requirements[0].Target"] = "1",
            ["TileDraft.Requirements[0].BossIds"] = fixture.Boss.Id.ToString(),
            ["TileDraft.Requirements[0].DropIds"] = fixture.Drop.Id.ToString()
        };
        if (mixed)
        {
            values["TileDraft.Requirements[1].Kind"] = "challenge";
            values["TileDraft.Requirements[1].Description"] = "Forged mixed challenge";
            values["TileDraft.Requirements[1].Target"] = "1";
        }
        using var response = await PostAsync(client, fixture.Path + "?handler=" + handler, displayed, values);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var result = WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location));
        Assert.Contains(mixed ? "Use separate tiles for catalogue drops and custom challenges." : "Catalogue tiles use automatic EHB.", result, StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal("Batch tile", (await verify.BoardTiles.SingleAsync(x => x.BoardId == fixture.Board.Id)).NameSnapshot);
        Assert.Equal(1, await verify.BoardRequirementSnapshots.CountAsync(x => x.BoardTileId == fixture.Tile.Id));
        Assert.Equal(1, await verify.TileTemplates.CountAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == fixture.Event.Id).ToListAsync());
        Assert.Equal(long.Parse(values["BoardVersion"], System.Globalization.CultureInfo.InvariantCulture), (await verify.Boards.SingleAsync(x => x.Id == fixture.Board.Id)).Version);
    }

    private WebApplicationFactory<Program> ApprovalBatchFactory(SaveChangesInterceptor? interceptor = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            if (interceptor is not null) builder.ConfigureServices(services => services.AddDbContext<ApplicationDbContext>(settings => settings.AddInterceptors(interceptor)));
        });

    private static Dictionary<string, string> ApprovalBatchFields(string html) => new()
    {
        ["BoardVersion"] = ApprovalBatchInput(html, "BoardVersion"),
        ["ApprovalCatalogueFingerprint"] = ApprovalBatchInput(html, "ApprovalCatalogueFingerprint"),
        ["confirmed"] = "true"
    };
    private static string ApprovalBatchInput(string html, string name)
    {
        var value = Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        Assert.NotEmpty(value);
        return WebUtility.HtmlDecode(value);
    }
    private static async Task<string> PostApprovalBatchAsync(HttpClient client, ApprovalBatchFixture fixture, string displayed)
    {
        using var response = await PostAsync(client, fixture.Path + "?handler=Approve", displayed, ApprovalBatchFields(displayed));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await client.GetStringAsync(response.Headers.Location);
    }
    private async Task SaveTileDescriptionAsync(ApprovalBatchFixture fixture, string? description, int target, IReadOnlyList<Guid> dropIds, IReadOnlyList<(Guid DropId, int Weight)> dropWeights)
    {
        var displayed = await LoadBoardAsync(fixture.Event.Id, fixture.Admin.Id);
        await using var db = new ApplicationDbContext(options);
        var requirementId = await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == fixture.Tile.Id).Select(x => x.Id).SingleAsync();
        var page = Page(db, fixture.Admin.Id);
        page.BoardVersion = displayed.BoardView!.Version;
        page.TileDraft = new BoardModel.TileDraftInput
        {
            TileId = fixture.Tile.Id,
            Position = 0,
            Name = fixture.Tile.NameSnapshot,
            Description = description,
            Requirements = [new BoardModel.RequirementInput
            {
                RequirementId = requirementId,
                Kind = "drops",
                Target = target,
                DuplicatesAllowed = true,
                BossIds = [fixture.Boss.Id],
                DropIds = dropIds.ToList(),
                DropWeights = dropWeights.ToDictionary(value => value.DropId, value => value.Weight)
            }]
        };
        Assert.IsType<RedirectToPageResult>(await page.OnPostEditTileAsync(fixture.Event.Id, CancellationToken.None));
        Assert.Equal("committed", page.TempData["BoardTileOutcome"]?.ToString());
    }
    private async Task AddPublicBoardTeamAsync(ApprovalBatchFixture fixture)
    {
        var now = DateTimeOffset.UtcNow;
        var team = new Bingo.Domain.Teams.Team(Guid.NewGuid(), fixture.Event.Id, "Batch team", "batch-team", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false, now);
        team.Finalize(now);
        await using var db = new ApplicationDbContext(options);
        db.Teams.Add(team);
        await db.SaveChangesAsync();
    }
    private async Task<ManualCompletionFixture> AddCompletionAsync(ApprovalBatchFixture fixture)
    {
        var now = DateTimeOffset.UtcNow;
        var team = new Team(Guid.NewGuid(), fixture.Event.Id, "Completion team", "completion-team", TeamFormationType.Preformed, null, false, now);
        team.Finalize(now);
        var participant = new EventParticipant(Guid.NewGuid(), fixture.Event.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), "Completion player", "COMPLETION PLAYER", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), fixture.Event.Id, participant.Id, character.Id, 0, now,
            fixture.Admin.Id, null, EventCharacterRole.Playing, 1m, EhbSource.Manual, null);
        await using var db = new ApplicationDbContext(options);
        var workingDropId = await db.BoardRequirementDropSnapshots.Where(value => value.RequirementId == fixture.Requirement.Id)
            .Select(value => value.Id).SingleAsync();
        var submission = new Submission(Guid.NewGuid(), fixture.Event.Id, team.Id, fixture.Tile.Id, fixture.Requirement.Id, workingDropId,
            participant.Id, character.Id, character.DisplayName, fixture.Admin.Id, 1, now.AddMinutes(-10), null, null);
        submission.Approve(1, now);
        var contribution = new SubmissionContribution(Guid.NewGuid(), submission.Id, team.Id, fixture.Requirement.Id, workingDropId, participant.Id, 1, now);
        db.AddRange(team, participant, character, assignment,
            new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, "completion fixture"),
            submission, contribution);
        await db.SaveChangesAsync();
        var publication = await db.ApprovalObjectivesAsync(fixture.Board.Id, fixture.PriorApprovalId!.Value)
            ?? throw new InvalidOperationException("The prior published approval is unavailable.");
        await TileCompletionFactReconciler.ReconcileAsync(db, fixture.Event.Id, publication, [team.Id], now);
        await db.SaveChangesAsync();
        var fact = await db.TileCompletionFacts.SingleAsync(value => value.TeamId == team.Id && value.ApprovalSnapshotId == fixture.PriorApprovalId);
        Assert.True(fact.IsComplete);
        return new(team.Id, submission.Id, submission.SubmittedAt);
    }
    private async Task<ApprovalBatchFixture> SeedApprovalBatchAsync(bool manual = false, bool missingEstimate = false, bool correction = false)
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website("batch-approval-admin", now); admin.SetGlobalRole(GlobalRole.Admin); SetPassword(admin, now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Batch approval", "batch-approval", "UTC", admin.Id, now);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Batch board", 1, 1);
        board.AcquireEditing(admin.Id, now, TimeSpan.FromMinutes(10));
        var template = new TileTemplate(Guid.NewGuid(), "Batch tile", "Batch objective", manual ? ObjectiveType.Manual : ObjectiveType.DropRequirements, "", manual && !missingEstimate ? 7m : null);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, template.Name, template.Description, "", manual ? 7m : 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Batch objective", manual);
        var boss = new BossActivity(Guid.NewGuid(), "Batch boss", "batch-boss", "Boss", missingEstimate ? null : 10m, now);
        var item = new CatalogueItem(Guid.NewGuid(), "Batch item", "BATCH ITEM");
        item.SetPrice(0, CataloguePriceSource.Manual, now);
        var drop = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, "1/10", .1m, null, now);
        var source = new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop.Id, item.Id, boss.Name, item.Name, "1/10", .1m, null, null);
        await using var setup = new ApplicationDbContext(options);
        setup.AddRange(admin, bingoEvent, board, template, tile, requirement, boss, item, drop);
        if (!manual)
        {
            setup.AddRange(new BoardRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour), source);
            if (missingEstimate) setup.Entry(template).Property(x => x.ManualEhbOverride).CurrentValue = 99m;
        }
        await setup.SaveChangesAsync();
        if (correction)
        {
            setup.Entry(bingoEvent).Property(x => x.State).CurrentValue = EventState.SignupClosed;
            board.SetTotalEhb(1m);
            await BoardApprovalFixture.PublishAsync(setup, board, now, [tile], [requirement], [source]);
            var publishedRequirement = await setup.BoardApprovalRequirementSnapshots.SingleAsync(x => x.BoardRequirementSnapshotId == requirement.Id);
            setup.BoardApprovalRequirementBossSnapshots.Add(new(Guid.NewGuid(), publishedRequirement.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour, boss.Version));
            board.BeginPublishedCorrection();
            await setup.SaveChangesAsync();
        }
        return new(admin, bingoEvent, board, template, tile, requirement, boss, item, drop, board.ActiveApprovalSnapshotId);
    }
    private async Task AssertApprovalBatchUnchangedAsync(ApprovalBatchFixture fixture)
    {
        await using var verify = new ApplicationDbContext(options);
        var board = await verify.Boards.SingleAsync(x => x.Id == fixture.Board.Id);
        Assert.Equal(fixture.PriorApprovalId, board.ActiveApprovalSnapshotId);
        Assert.Equal(fixture.PriorApprovalId is null ? BoardState.Draft : BoardState.Published, board.State);
        Assert.Equal(fixture.PriorApprovalId is null ? 0 : 1, await verify.BoardApprovalSnapshots.CountAsync(x => x.BoardId == board.Id));
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == fixture.Event.Id).ToListAsync());
        if (fixture.PriorApprovalId is null)
        {
            Assert.Empty(await verify.BoardApprovalTileSnapshots.ToListAsync());
            Assert.Empty(await verify.BoardApprovalRequirementSnapshots.ToListAsync());
            Assert.Empty(await verify.BoardApprovalRequirementBossSnapshots.ToListAsync());
            Assert.Empty(await verify.BoardApprovalRequirementDropSnapshots.ToListAsync());
        }
    }
    private sealed record ApprovalBatchFixture(Account Admin, BingoEvent Event, Board Board, TileTemplate Template, BoardTile Tile, BoardRequirementSnapshot Requirement, BossActivity Boss, CatalogueItem Item, SourceDrop Drop, Guid? PriorApprovalId)
    {
        public string Path => $"/Admin/Events/Board/{Event.Id}";
    }
    private sealed record ManualCompletionFixture(Guid TeamId, Guid SubmissionId, DateTimeOffset SubmittedAt);
    private sealed class ApprovalBatchSaveBarrier : SaveChangesInterceptor
    {
        public TaskCompletionSource<bool> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<BoardApprovalSnapshot>().Any(x => x.State == EntityState.Added))
            { Ready.TrySetResult(true); await Release.Task.WaitAsync(cancellationToken); }
            return result;
        }
    }
    private sealed class ApprovalBatchNotificationFailure : Bingo.Application.Teams.IAdminCollaborationNotifier
    {
        public Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("sensitive-internal-marker");
        public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyEventsControlChangedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class ApprovalBatchFailure(bool databaseFailure) : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.Entity.Action == "board.approved"))
            {
                Triggered = true;
                throw databaseFailure ? new DbUpdateException("sensitive-internal-marker") : new InvalidOperationException("sensitive-internal-marker");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class CompletionFactFailure : SaveChangesInterceptor
    {
        public bool Triggered { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<TileCompletionFact>().Any(entry => entry.State == EntityState.Added))
            {
                Triggered = true;
                throw new DbUpdateException("injected completion fact write failure");
            }
            return ValueTask.FromResult(result);
        }
    }
}
