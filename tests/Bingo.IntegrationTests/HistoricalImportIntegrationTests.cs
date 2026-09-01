using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bingo.Application.Boards;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.WiseOldMan;
using Bingo.Web.Catalogue;
using Bingo.Web.HistoricalImport;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class HistoricalImportIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_historical_import_tests")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private static readonly IReadOnlyDictionary<string, int[]> ExpectedCountersByTeam = new Dictionary<string, int[]>(StringComparer.Ordinal)
    {
        ["Touch Kids, not grass"] = [0, 0, 10, 0, 1, 1, 3, 4, 2, 1, 3, 2, 6, 3, 3, 5, 1, 5, 6, 2, 0, 0, 10, 0, 0],
        ["Såeh cs?"] = [3, 3, 10, 0, 0, 1, 0, 3, 1, 1, 3, 2, 6, 2, 0, 5, 1, 5, 2, 2, 6, 0, 10, 2, 0],
        ["Morytania Monkeys"] = [3, 1, 10, 3, 2, 0, 0, 0, 2, 0, 3, 2, 0, 3, 0, 5, 1, 5, 6, 2, 2, 0, 0, 2, 0],
        ["The Agency"] = [3, 6, 10, 2, 2, 1, 3, 4, 2, 1, 3, 2, 6, 3, 0, 5, 1, 5, 6, 2, 6, 3, 10, 2, 6],
        ["Xen0%_d_rops"] = [3, 6, 0, 0, 2, 1, 3, 0, 0, 1, 3, 2, 0, 0, 2, 5, 1, 5, 6, 2, 6, 3, 10, 2, 6],
        ["Zalamalikum"] = [0, 1, 10, 0, 0, 0, 1, 4, 0, 0, 0, 0, 6, 0, 0, 5, 1, 5, 6, 2, 0, 0, 10, 0, 0]
    };
    private static readonly int[] ExpectedTileTargets = [3, 6, 10, 3, 2, 1, 3, 4, 2, 1, 3, 2, 6, 3, 3, 5, 1, 5, 6, 2, 6, 3, 10, 2, 6];

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task AppliesFictionalOperatorInputAndExactRerunIsNoOp()
    {
        var now = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        const string owner = "historical-import-test-owner";
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "data", "historical-import", "det-store-danske-sommerbingo-2026.json");
        var inputPath = Path.Combine(Path.GetTempPath(), $"historical-import-{Guid.NewGuid():N}.json");
        var manifestVariantPath = Path.Combine(Path.GetTempPath(), $"historical-import-manifest-variant-{Guid.NewGuid():N}.json");
        var overflowManifestPath = Path.Combine(Path.GetTempPath(), $"historical-import-manifest-{Guid.NewGuid():N}.json");
        try
        {
            await using (var db = new ApplicationDbContext(options))
            {
                await new OperatorRecoveryService(db, clock, new PasswordHasher<Account>())
                    .BootstrapOwnerAsync(owner, "historical-import-test-password", owner, CancellationToken.None);
                await new CatalogueSnapshotService(db, clock)
                    .ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));
                var maggotItemIds = await db.CatalogueItems
                    .Where(value => value.Name == "Crimson kisten" || value.Name == "Elder venator fang")
                    .Select(value => value.Id)
                    .ToListAsync();
                var maggotBossId = await db.BossActivities.Where(value => value.Name == "Maggot King").Select(value => value.Id).SingleAsync();
                Assert.Equal(2, await db.SourceDrops.CountAsync(value => value.Active && value.BossActivityId == maggotBossId && maggotItemIds.Contains(value.ItemId)));
                var marquessId = await db.CatalogueItems.Where(item => item.Name == "Maggot marquess").Select(item => item.Id).SingleAsync();
                Assert.Equal(1, await db.SourceDrops.CountAsync(value => value.Active && value.BossActivityId == maggotBossId && value.ItemId == marquessId));
                await db.SaveChangesAsync();
            }
            await File.WriteAllTextAsync(inputPath, "{}");
            await using (var db = new ApplicationDbContext(options))
            {
                var preflight = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath,
                    inputPath, owner, null, false));
                Assert.False(preflight.Succeeded);
                Assert.Contains(preflight.Errors, error => error.Contains("exactly 90 participant records", StringComparison.Ordinal));
                Assert.False(await db.Events.AnyAsync(value => value.Slug == HistoricalEventImporter.EventSlug));
            }
            await File.WriteAllTextAsync(inputPath, JsonSerializer.Serialize(CreateFictionalInput(reverseApprovedMappings: true)));
            await using (var db = new ApplicationDbContext(options))
            {
                var reversedMapping = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath, inputPath, owner, null, false));
                Assert.False(reversedMapping.Succeeded);
                Assert.Contains(reversedMapping.Errors, error => error.Contains("Ezzi primary", StringComparison.Ordinal));
                Assert.False(await db.Events.AnyAsync(value => value.Slug == HistoricalEventImporter.EventSlug));
            }
            await File.WriteAllTextAsync(inputPath, JsonSerializer.Serialize(CreateFictionalInput()));
            await WriteManifestVariantAsync(manifestPath, manifestVariantPath);
            await using (var db = new ApplicationDbContext(options))
            {
                var alteredManifest = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestVariantPath, inputPath, owner, null, false));
                Assert.False(alteredManifest.Succeeded);
                Assert.Contains(alteredManifest.Errors, error => error.Contains("public historical manifest SHA-256", StringComparison.Ordinal));
                Assert.False(await db.Events.AnyAsync(value => value.Slug == HistoricalEventImporter.EventSlug));
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var missingActor = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath, inputPath, "missing-historical-import-actor", "Det Store Danske Sommerbingo 2026", true));
                Assert.False(missingActor.Succeeded);
                Assert.Contains(missingActor.Errors, error => error.Contains("No active SuperAdmin", StringComparison.Ordinal));
                Assert.False(await db.Events.AnyAsync(value => value.Slug == HistoricalEventImporter.EventSlug));
            }

            await using (var db = new ApplicationDbContext(options))
            {
                var fang = await (from drop in db.SourceDrops
                                  join item in db.CatalogueItems on drop.ItemId equals item.Id
                                  where item.Name == "Elder venator fang"
                                  select drop).SingleAsync();
                fang.SetActive(false);
                await db.SaveChangesAsync();
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var missingCatalogueDrop = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath,
                    inputPath, owner, null, false));
                Assert.False(missingCatalogueDrop.Succeeded);
                Assert.Contains(missingCatalogueDrop.Errors, error => error.Contains("Elder venator fang", StringComparison.Ordinal) && error.Contains("no active source drop", StringComparison.Ordinal));
                Assert.False(await db.Events.AnyAsync(value => value.Slug == HistoricalEventImporter.EventSlug));
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var fang = await (from drop in db.SourceDrops
                                  join item in db.CatalogueItems on drop.ItemId equals item.Id
                                  where item.Name == "Elder venator fang"
                                  select drop).SingleAsync();
                fang.SetActive(true);
                var maggotBoss = await db.BossActivities.SingleAsync(value => value.Name == "Maggot King");
                maggotBoss.Update(maggotBoss.Name, maggotBoss.Category, 32m, maggotBoss.ExternalIdentifier, maggotBoss.DataSource, maggotBoss.Notes, now);
                await db.SaveChangesAsync();
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var wrongCatalogueEhb = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath, inputPath, owner, null, false));
                Assert.False(wrongCatalogueEhb.Succeeded);
                Assert.Contains(wrongCatalogueEhb.Errors, error => error.Contains("exactly 31.1487", StringComparison.Ordinal));
                Assert.False(await db.Events.AnyAsync(value => value.Slug == HistoricalEventImporter.EventSlug));
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var maggotBoss = await db.BossActivities.SingleAsync(value => value.Name == "Maggot King");
                maggotBoss.Update(maggotBoss.Name, maggotBoss.Category, 33m, maggotBoss.ExternalIdentifier, maggotBoss.DataSource, maggotBoss.Notes, now);
                await db.SaveChangesAsync();
            }
            await WriteCounterOverflowManifestAsync(manifestPath, overflowManifestPath);
            await using (var db = new ApplicationDbContext(options))
            {
                var overTarget = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    overflowManifestPath, inputPath, owner, null, false));
                Assert.False(overTarget.Succeeded);
                Assert.Contains(overTarget.Errors, error => error.Contains("public historical manifest SHA-256", StringComparison.Ordinal));
                Assert.False(await db.Events.AnyAsync(value => value.Slug == HistoricalEventImporter.EventSlug));
            }

            var historicalEventId = StableGuid("event:dkl-sommerbingo-2026");
            var collidingTeamId = StableGuid($"{historicalEventId:N}:team:touch-kids-not-grass");
            await using (var db = new ApplicationDbContext(options))
            {
                var blockerEvent = new BingoEvent(Guid.NewGuid(), "Import collision blocker", "import-collision-blocker", "UTC", Guid.NewGuid(), now);
                db.Events.Add(blockerEvent);
                db.Teams.Add(new Team(collidingTeamId, blockerEvent.Id, "Collision blocker", "collision-blocker", TeamFormationType.Preformed, null, false, now));
                await db.SaveChangesAsync();
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var rolledBack = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath,
                    inputPath, owner, "Det Store Danske Sommerbingo 2026", true));
                Assert.False(rolledBack.Succeeded);
                Assert.Contains(rolledBack.Errors, error => error.StartsWith("Historical import transaction rolled back:", StringComparison.Ordinal));
            }
            await using (var db = new ApplicationDbContext(options))
            {
                Assert.False(await db.Events.AnyAsync(value => value.Slug == HistoricalEventImporter.EventSlug));
                var blocker = await db.Events.SingleAsync(value => value.Slug == "import-collision-blocker");
                db.Teams.RemoveRange(await db.Teams.Where(value => value.EventId == blocker.Id).ToListAsync());
                db.Events.Remove(blocker);
                await db.SaveChangesAsync();
            }

            HistoricalImportResult applied;
            await using (var db = new ApplicationDbContext(options))
            {
                applied = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath,
                    inputPath, owner, "Det Store Danske Sommerbingo 2026", true));
            }
            Assert.True(applied.Succeeded, string.Join(Environment.NewLine, applied.Errors));
            Assert.True(applied.Applied);

            await using (var db = new ApplicationDbContext(options))
            {
                var historical = await db.Events.SingleAsync(value => value.Slug == HistoricalEventImporter.EventSlug);
                Assert.Equal(EventState.Archived, historical.State);
                Assert.Equal(new DateTimeOffset(2026, 7, 19, 16, 0, 0, TimeSpan.Zero), historical.ArchivedAt);
                Assert.Equal(6, await db.Teams.CountAsync(value => value.EventId == historical.Id));
                Assert.Equal(90, await db.EventParticipants.CountAsync(value => value.EventId == historical.Id));
                Assert.Equal(93, await db.EventParticipantCharacters.CountAsync(value => value.EventId == historical.Id));
                Assert.Equal(25, await db.BoardTiles.CountAsync(value => value.BoardId == db.Boards.Where(board => board.EventId == historical.Id).Select(board => board.Id).Single()));
                var synchronization = await db.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == historical.Id);
                Assert.True(synchronization.LatestComplete);
                Assert.Null(synchronization.NormalDueAt);
                Assert.Equal(93, await db.EventCompetitionCharacterActivities.CountAsync(value => value.EventId == historical.Id));
                Assert.Equal(6, await db.OfficialPlacements.CountAsync(value => value.EventId == historical.Id));
                var officialOrder = await db.OfficialPlacements.Where(value => value.EventId == historical.Id)
                    .OrderBy(value => value.Placement).Select(value => value.TeamName).ToListAsync();
                Assert.Equal(["The Agency", "Xen0%_d_rops", "Touch Kids, not grass", "Morytania Monkeys", "Zalamalikum", "Såeh cs?"], officialOrder);
                var publicBoard = await new PublicBoardService(db, clock).GetEventBoardAsync(HistoricalEventImporter.EventSlug);
                Assert.NotNull(publicBoard);
                Assert.Equal(officialOrder, publicBoard!.Teams.Select(value => value.TeamName).ToList());
                Assert.Equal([1, 2, 3, 4, 5, 6], publicBoard.Teams.Select(value => value.Rank).ToArray());
                Assert.All(await db.Submissions.Where(value => value.EventId == historical.Id).ToListAsync(), value => Assert.Null(value.DropSnapshotId));
                var slayerTile = await db.BoardTiles.SingleAsync(value => value.NameSnapshot == "Superior Slayer");
                var slayerTemplate = await db.TileTemplates.SingleAsync(value => value.Id == slayerTile.TileTemplateId);
                Assert.Equal(ObjectiveType.Manual, slayerTemplate.ObjectiveType);
                Assert.Equal(21m, slayerTile.EstimatedEhbSnapshot);
                Assert.Contains("Imbued heart", slayerTile.DescriptionSnapshot, StringComparison.Ordinal);
                Assert.Contains("Eternal gem", slayerTile.DescriptionSnapshot, StringComparison.Ordinal);
                Assert.Contains("Mist battlestaff", slayerTile.DescriptionSnapshot, StringComparison.Ordinal);
                Assert.Contains("Dust battlestaff", slayerTile.DescriptionSnapshot, StringComparison.Ordinal);
                Assert.Equal(0, await db.CatalogueItems.CountAsync(value => value.Name == "Imbued heart" || value.Name == "Eternal gem" || value.Name == "Mist battlestaff" || value.Name == "Dust battlestaff"));
                Assert.Equal(0, await (from drop in db.SourceDrops
                                       join catalogueItem in db.CatalogueItems on drop.ItemId equals catalogueItem.Id
                                       where catalogueItem.Name == "Imbued heart" || catalogueItem.Name == "Eternal gem" || catalogueItem.Name == "Mist battlestaff" || catalogueItem.Name == "Dust battlestaff"
                                       select drop).CountAsync());
                var slayerRequirementIds = await db.BoardRequirementSnapshots.Where(value => value.BoardTileId == slayerTile.Id).Select(value => value.Id).ToListAsync();
                Assert.Equal(0, await db.BoardRequirementDropSnapshots.CountAsync(value => slayerRequirementIds.Contains(value.RequirementId)));
                var slayerTemplateRequirementIds = await db.TileTemplateRequirements.Where(value => value.TileTemplateId == slayerTemplate.Id).Select(value => value.Id).ToListAsync();
                Assert.Equal(0, await db.TemplateRequirementDrops.CountAsync(value => slayerTemplateRequirementIds.Contains(value.RequirementId)));
                var slayerApprovalRequirementIds = await (from approvalRequirement in db.BoardApprovalRequirementSnapshots
                                                          join approvalTile in db.BoardApprovalTileSnapshots on approvalRequirement.ApprovalTileSnapshotId equals approvalTile.Id
                                                          where approvalTile.BoardTileId == slayerTile.Id
                                                          select approvalRequirement.Id).ToListAsync();
                Assert.Equal(0, await db.BoardApprovalRequirementDropSnapshots.CountAsync(value => slayerApprovalRequirementIds.Contains(value.ApprovalRequirementSnapshotId)));
                var slayerSubmissions = await db.Submissions.Where(value => value.EventId == historical.Id && value.BoardTileId == slayerTile.Id).ToListAsync();
                Assert.All(slayerSubmissions, value => Assert.Null(value.DropSnapshotId));
                var slayerSubmissionIds = slayerSubmissions.Select(value => value.Id).ToList();
                Assert.All(await db.SubmissionContributions.Where(value => slayerSubmissionIds.Contains(value.SubmissionId)).ToListAsync(), value => Assert.Null(value.DropSnapshotId));
                var boardId = await db.Boards.Where(value => value.EventId == historical.Id).Select(value => value.Id).SingleAsync();
                var tiles = await db.BoardTiles.Where(value => value.BoardId == boardId)
                    .OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex).ToListAsync();
                var teams = await db.Teams.Where(value => value.EventId == historical.Id).ToListAsync();
                var submissionCounts = await db.Submissions.Where(value => value.EventId == historical.Id)
                    .GroupBy(value => new { value.TeamId, value.BoardTileId })
                    .Select(value => new { value.Key.TeamId, value.Key.BoardTileId, Count = value.Count() })
                    .ToListAsync();
                var submissionCountByTile = submissionCounts.ToDictionary(value => (value.TeamId, value.BoardTileId), value => value.Count);
                var tileIds = tiles.Select(value => value.Id).ToArray();
                var targetByTile = await db.BoardRequirementSnapshots
                    .Where(value => tileIds.Contains(value.BoardTileId))
                    .GroupBy(value => value.BoardTileId)
                    .Select(value => new { BoardTileId = value.Key, Target = value.Sum(requirement => requirement.TargetContribution) })
                    .ToDictionaryAsync(value => value.BoardTileId, value => value.Target);
                Assert.Equal(ExpectedTileTargets, tiles.Select(value => targetByTile[value.Id]).ToArray());
                foreach (var expectedTeam in ExpectedCountersByTeam)
                {
                    var team = teams.Single(value => value.Name == expectedTeam.Key);
                    var actualCounters = tiles.Select(tile => submissionCountByTile.GetValueOrDefault((team.Id, tile.Id))).ToArray();
                    Assert.Equal(expectedTeam.Value, actualCounters);
                }
                var projection = await new CachedEventCompetitionActivityProjection(db, clock).GetAsync(historical.Id);
                Assert.Equal(EventCompetitionActivityState.Complete, projection.State);
                Assert.True(projection.HasRankings);
                Assert.Equal(93, projection.ExpectedAccountCount);
                Assert.Equal(93, projection.MatchedAccountCount);
                Assert.All(projection.Teams, value =>
                {
                    Assert.Equal(15, value.ExpectedParticipantCount);
                    Assert.Equal(15, value.ContributingParticipantCount);
                    Assert.NotEmpty(value.MvpNames);
                });
                Assert.Equal(17m, projection.Teams.Single(value => value.TeamName == "Touch Kids, not grass").TotalGainedEhb);
                Assert.Equal(15m, projection.Teams.Single(value => value.TeamName == "Xen0%_d_rops").TotalGainedEhb);
                var maggotDrops = await db.BoardApprovalRequirementDropSnapshots
                    .Where(value => value.ItemName == "Crimson kisten" || value.ItemName == "Elder venator fang")
                    .ToListAsync();
                Assert.Equal(2, maggotDrops.Count);
                Assert.Equal(["1/520", "1/340"], maggotDrops.OrderBy(value => value.ItemName).Select(value => value.DisplayRate).ToArray());
                Assert.All(maggotDrops, value => Assert.NotNull(value.NumericProbability));
                Assert.All(maggotDrops, value => Assert.NotNull(value.EhbPerContribution));
                Assert.DoesNotContain(await db.BoardApprovalRequirementDropSnapshots.ToListAsync(), value => value.ItemName == "Maggot marquess");
                var maggotTile = await db.BoardTiles.SingleAsync(value => value.NameSnapshot == "Maggot King");
                var maggotBoss = await db.BossActivities.SingleAsync(value => value.Name == "Maggot King");
                var maggotSourceDrops = await (from drop in db.SourceDrops
                                               join catalogueItem in db.CatalogueItems on drop.ItemId equals catalogueItem.Id
                                               where drop.BossActivityId == maggotBoss.Id && catalogueItem.Name != "Maggot marquess"
                                               select drop).ToListAsync();
                var maggotRequirementId = await db.BoardRequirementSnapshots.Where(value => value.BoardTileId == maggotTile.Id).Select(value => value.Id).SingleAsync();
                var maggotBoardDrops = await db.BoardRequirementDropSnapshots
                    .Where(value => value.RequirementId == maggotRequirementId
                        && (value.ItemName == "Crimson kisten" || value.ItemName == "Elder venator fang"))
                    .ToListAsync();
                Assert.Equal(maggotSourceDrops.Select(value => value.Id).OrderBy(value => value), maggotBoardDrops.Select(value => value.SourceDropId).OrderBy(value => value));
                Assert.Equal(31.1487m, maggotTile.EstimatedEhbSnapshot);
            }

            await using (var db = new ApplicationDbContext(options))
            {
                var noOp = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath,
                    inputPath, owner, "Det Store Danske Sommerbingo 2026", true));
                Assert.True(noOp.Succeeded, string.Join(Environment.NewLine, noOp.Errors));
                Assert.True(noOp.NoOp);
                Assert.False(noOp.Applied);
            }

            await File.AppendAllTextAsync(inputPath, Environment.NewLine);
            await using (var db = new ApplicationDbContext(options))
            {
                var divergent = await new HistoricalEventImporter(db, clock).RunAsync(new HistoricalImportOptions(
                    manifestPath,
                    inputPath, owner, "Det Store Danske Sommerbingo 2026", true));
                Assert.False(divergent.Succeeded);
                Assert.Contains(divergent.Errors, error => error.Contains("different manifest/input hash", StringComparison.Ordinal));
            }
        }
        finally
        {
            if (File.Exists(inputPath)) File.Delete(inputPath);
            if (File.Exists(manifestVariantPath)) File.Delete(manifestVariantPath);
            if (File.Exists(overflowManifestPath)) File.Delete(overflowManifestPath);
        }
    }

    private static object CreateFictionalInput(bool reverseApprovedMappings = false)
    {
        var teams = new[] { "touch-kids-not-grass", "saeh-cs", "morytania-monkeys", "the-agency", "xen0-d-rops", "zalamalikum" };
        var participants = new List<object>();
        for (var teamIndex = 0; teamIndex < teams.Length; teamIndex++)
            for (var memberIndex = 0; memberIndex < 15; memberIndex++)
            {
                var number = teamIndex * 15 + memberIndex + 1;
                var accounts = new List<object> { Account($"Import Player {number:00}", 100m + number, 101m + number, 1m) };
                if (teamIndex == 0 && memberIndex == 0)
                    accounts = reverseApprovedMappings
                        ? [Account("Also Ezzi", 20m, 21m, 1m), Account("Ezzi", 200m, 201m, 1m)]
                        : [Account("Ezzi", 200m, 201m, 1m), Account("Also Ezzi", 20m, 21m, 1m)];
                else if (teamIndex == 0 && memberIndex == 1)
                    accounts = reverseApprovedMappings
                        ? [Account("w olles", 21m, 22m, 1m), Account("wolles", 210m, 211m, 1m)]
                        : [Account("wolles", 210m, 211m, 1m), Account("w olles", 21m, 22m, 1m)];
                else if (teamIndex == 4 && memberIndex == 0)
                    accounts = [Account("Xen Import Player", 220m, 221m, 1m), Account("Coxophobia", 0m, 0m, 0m)];
                participants.Add(new { participantKey = $"import-{number:00}", displayName = $"Import Player {number:00}", teamSlug = teams[teamIndex], accounts });
            }
        return new
        {
            sourceEventId = "dkl-sommerbingo-2026",
            competitionId = 145197,
            competitionTitle = "Det Store Danske Sommerbingo 2026",
            startsAt = "2026-07-14T16:00:00Z",
            endsAt = "2026-07-19T16:00:00Z",
            accounts = participants
        };
    }

    private static object Account(string username, decimal start, decimal end, decimal gained) => new
    {
        username,
        startEhb = start,
        endEhb = end,
        gainedEhb = gained,
        fetchedAt = "2026-07-20T12:00:00Z",
        upstreamUpdatedAt = "2026-07-20T12:00:00Z"
    };

    private static async Task WriteCounterOverflowManifestAsync(string sourcePath, string targetPath)
    {
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(sourcePath))!.AsObject();
        var firstTeam = manifest["teams"]!.AsArray()[0]!.AsObject();
        var counters = firstTeam["counters"]!.AsArray();
        counters[0] = JsonValue.Create(counters[0]!.GetValue<int>() - 1);
        await File.WriteAllTextAsync(targetPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task WriteManifestVariantAsync(string sourcePath, string targetPath)
    {
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(sourcePath))!.AsObject();
        var firstTile = manifest["tiles"]!.AsArray()[0]!.AsObject();
        var firstRequirement = firstTile["requirements"]!.AsArray()[0]!.AsObject();
        firstRequirement["description"] = "Collect 3 Nex uniques; the pet does not count (manifest variant)";
        await File.WriteAllTextAsync(targetPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Guid StableGuid(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes("historical-import:v1:" + value)).AsSpan(0, 16));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
