using System.Text.Json;
using System.Text.Json.Serialization;
using Bingo.Application.Catalogue;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class CataloguePopulationMigrationIntegrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260915192848_SaveStatsGuidanceAndArtwork";
    private const string Migration = "20260916100000_PopulateRetainedCatalogue";
    private const string MigrationActor = "system/catalogue-migration";
    private const string SnapshotVersion = "step11-2026-09-16";
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithLoopbackPort()
        .WithDatabase("bingo_catalogue_population")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private DbContextOptions<HistoricalApplicationDbContext> historicalOptions = null!;

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(database);
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetOwnedConnectionString())
            .Options;
        historicalOptions = new DbContextOptionsBuilder<HistoricalApplicationDbContext>()
            .UseNpgsql(database.GetOwnedConnectionString())
            .Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task RetainedMigrationPopulatesEveryApprovedRecordPreservesProtectedStateAndFeedsStartCapture()
    {
        var snapshot = await ReadSnapshotAsync();
        AssertSnapshotShape(snapshot);
        var fixture = await SeedRetainedCatalogueAsync(snapshot, includeProtectedHistory: true);

        await using (var migrate = new ApplicationDbContext(options))
            await migrate.GetService<IMigrator>().MigrateAsync();

        await using (var verify = new ApplicationDbContext(options))
        {
            var items = await verify.CatalogueItems.AsNoTracking().ToDictionaryAsync(x => x.NormalizedName);
            var bosses = await verify.BossActivities.AsNoTracking().ToDictionaryAsync(x => x.Slug);
            Assert.Equal(312, items.Count);
            Assert.Equal(69, bosses.Count);
            Assert.NotEqual(snapshot.Items.Single(x => x.NormalizedName == "AHRIM'S ROBETOP").Id, items["AHRIM'S ROBETOP"].Id);
            Assert.NotEqual(snapshot.Bosses.Single(x => x.Slug == "araxxor").Id, bosses["araxxor"].Id);

            foreach (var record in snapshot.Items)
            {
                var item = items[record.NormalizedName];
                Assert.Equal(record.Name, item.Name);
                if (record.Name == fixture.ManualItemName)
                {
                    Assert.Equal(fixture.ManualItemExternalId, item.ExternalIdentifier);
                    Assert.Equal(123L, item.CatalogueValueGp);
                    Assert.Equal(CataloguePriceSource.Manual, item.PriceSource);
                    Assert.Equal(fixture.ManualPriceObservedAt, item.PriceObservedAt);
                    Assert.Equal(ApiMappingStatus.TemporarilyUnavailable, item.MappingStatus);
                    Assert.Equal(fixture.ManualMappingCheckedAt, item.MappingCheckedAt);
                    Assert.Equal("manual mapping", item.MatchedApiName);
                    Assert.Equal("manual.png", item.MatchedApiIcon);
                    Assert.Equal(777L, item.RejectedPriceGp);
                    Assert.Equal(fixture.ManualRejectedObservedAt, item.RejectedPriceObservedAt);
                    Assert.Equal("operator note", item.Notes);
                    Assert.Equal("operator-image.png", item.ImageUrl);
                    Assert.Equal(10m, item.ArtworkX);
                    Assert.Equal(20m, item.ArtworkY);
                    Assert.Equal(30m, item.ArtworkWidth);
                    Assert.Equal(40m, item.ArtworkHeight);
                    Assert.Equal(1.2m, item.ArtworkScale);
                    Assert.Equal(5m, item.ArtworkRotation);
                    Assert.Equal(fixture.BaselineItemVersions[record.NormalizedName], item.Version);
                    continue;
                }

                if (record.Name == fixture.NewerItemName)
                {
                    Assert.Equal(record.ExternalIdentifier, item.ExternalIdentifier);
                    Assert.Equal(987654L, item.CatalogueValueGp);
                    Assert.Equal(CataloguePriceSource.Api, item.PriceSource);
                    Assert.Equal(fixture.NewerPriceObservedAt, item.PriceObservedAt);
                    Assert.Equal(record.MappingStatus, item.MappingStatus);
                    Assert.Equal(record.MappingCheckedAt, item.MappingCheckedAt);
                    Assert.Equal(record.MatchedApiName, item.MatchedApiName);
                    Assert.Equal(record.MatchedApiIcon, item.MatchedApiIcon);
                    Assert.Equal(fixture.BaselineItemVersions[record.NormalizedName] + 1, item.Version);
                    continue;
                }

                Assert.Equal(record.ExternalIdentifier, item.ExternalIdentifier);
                Assert.Equal(record.CatalogueValueGp, item.CatalogueValueGp);
                Assert.Equal(record.PriceSource, item.PriceSource);
                Assert.Equal(record.PriceObservedAt, item.PriceObservedAt);
                Assert.Equal(record.MappingStatus, item.MappingStatus);
                Assert.Equal(record.MappingCheckedAt, item.MappingCheckedAt);
                Assert.Equal(record.MatchedApiName, item.MatchedApiName);
                Assert.Equal(record.MatchedApiIcon, item.MatchedApiIcon);
                Assert.Equal(fixture.BaselineItemVersions[record.NormalizedName] + 1, item.Version);
            }

            foreach (var record in snapshot.Bosses)
            {
                var boss = bosses[record.Slug];
                if (record.Name is "Abyssal Sire" or "Zalcano")
                {
                    Assert.Equal(record.Name == "Zalcano" ? "zalcano" : "manual_boss", boss.ExternalIdentifier);
                    Assert.Equal(ApiMappingStatus.TemporarilyUnavailable, boss.MappingStatus);
                    Assert.Equal(fixture.ConflictingBossMappingCheckedAt, boss.MappingCheckedAt);
                    if (record.Name == "Abyssal Sire")
                    {
                        Assert.Equal("operator boss source", boss.DataSource);
                        Assert.Equal("operator boss note", boss.Notes);
                        Assert.Equal("operator-boss.png", boss.ImageUrl);
                    }
                    Assert.Equal(fixture.BaselineBossVersions[record.Slug], boss.Version);
                    continue;
                }

                Assert.Equal(record.ExternalIdentifier, boss.ExternalIdentifier);
                Assert.Equal(record.MappingStatus, boss.MappingStatus);
                Assert.Equal(record.MappingCheckedAt, boss.MappingCheckedAt);
                Assert.Equal(fixture.BaselineBossVersions[record.Slug] + 1, boss.Version);
            }

            var extraItem = await verify.CatalogueItems.AsNoTracking().SingleAsync(x => x.Id == fixture.ExtraItemId);
            var extraBoss = await verify.BossActivities.AsNoTracking().SingleAsync(x => x.Id == fixture.ExtraBossId);
            var extraDrop = await verify.SourceDrops.AsNoTracking().SingleAsync(x => x.Id == fixture.ExtraDropId);
            Assert.Equal("Extra retained item", extraItem.Name);
            Assert.Equal("Extra retained boss", extraBoss.Name);
            Assert.Equal(fixture.ExtraDropDisplayRate, extraDrop.DisplayRate);
            Assert.Equal(fixture.ExtraDropProbability, extraDrop.NumericProbability);
            Assert.Equal(fixture.ExtraDropEhb, extraDrop.DefaultEhbEstimate);
            Assert.Equal(fixture.ExtraDropVersion, extraDrop.Version);
            Assert.Equal(fixture.BaselineSourceDropFingerprint, await SourceDropFingerprintAsync(verify));

            var frozenPrice = await verify.EventItemPrices.AsNoTracking().SingleAsync(x => x.EventId == fixture.ProtectedEventId && x.ItemId == fixture.ManualItemId);
            Assert.Equal(123L, frozenPrice.ValueGp);
            Assert.Equal(EventItemPriceSource.CatalogueFallback, frozenPrice.Source);
            Assert.Equal(CataloguePriceSource.Manual, frozenPrice.FallbackCatalogueSource);
            Assert.Equal(fixture.ProtectedEventStartedAt, frozenPrice.CapturedAt);
            Assert.Equal(fixture.ProtectedEventStartedAt.AddHours(-1), frozenPrice.SelectedHour);
            var protectedEvent = await verify.Events.AsNoTracking().SingleAsync(x => x.Id == fixture.ProtectedEventId);
            Assert.Equal(EventState.Live, protectedEvent.State);
            Assert.Equal(fixture.ProtectedEventStartedAt, protectedEvent.ActualStartedAt);
            Assert.Equal(fixture.ProtectedEventStartedAt, protectedEvent.ItemPricesCapturedAt);
            Assert.Equal(fixture.BaselineAuditCount + 376, await verify.AuditEntries.CountAsync());

            var migrationAudits = await verify.AuditEntries.AsNoTracking()
                .Where(x => x.ActorUsername == MigrationActor)
                .ToListAsync();
            Assert.Equal(376, migrationAudits.Count);
            Assert.All(migrationAudits, audit =>
            {
                Assert.Null(audit.ActorAccountId);
                Assert.Contains(SnapshotVersion, audit.Details, StringComparison.Ordinal);
                Assert.NotNull(audit.BeforeState);
                Assert.NotNull(audit.AfterState);
            });

            var representative = migrationAudits.Single(x => x.TargetId == items["AHRIM'S ROBETOP"].Id.ToString());
            using var before = JsonDocument.Parse(representative.BeforeState!);
            using var after = JsonDocument.Parse(representative.AfterState!);
            Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("CatalogueValueGp").ValueKind);
            Assert.Equal(snapshot.Items.Single(x => x.NormalizedName == "AHRIM'S ROBETOP").CatalogueValueGp, after.RootElement.GetProperty("CatalogueValueGp").GetInt64());
            Assert.Equal(fixture.BaselineItemVersions["AHRIM'S ROBETOP"], before.RootElement.GetProperty("Version").GetInt64());
            Assert.Equal(fixture.BaselineItemVersions["AHRIM'S ROBETOP"] + 1, after.RootElement.GetProperty("Version").GetInt64());
        }

    }

    [Fact]
    public async Task RetainedMigrationLetsExistingPreLiveSignupStartThroughNormalBoundaryWithProviderOutage()
    {
        var snapshot = await ReadSnapshotAsync();
        AssertSnapshotShape(snapshot);
        var fixture = await SeedRetainedCatalogueAsync(snapshot, includeProtectedHistory: false, includePreLiveStartFixture: true);

        await using (var migrate = new ApplicationDbContext(options))
            await migrate.GetService<IMigrator>().MigrateAsync();
        await PublishPreLiveBoardAsync(fixture);
        await PublishPreLiveRosterAsync(fixture);

        Guid apiItemId;
        Guid untradeableItemId;
        var approvedApi = snapshot.Items.First(item => item.PriceSource == CataloguePriceSource.Api && item.Name is not ("Ahrim's hood" or "Adamant boots"));
        var approvedUntradeable = snapshot.Items.First(item => item.PriceSource == CataloguePriceSource.Untradeable);
        await using (var beforeStart = new ApplicationDbContext(options))
        {
            var existingEvent = await beforeStart.Events.AsNoTracking().SingleAsync(item => item.Id == fixture.PreLiveEventId);
            var existingSignup = await beforeStart.EventParticipants.AsNoTracking().SingleAsync(item => item.Id == fixture.PreLiveParticipantId);
            Assert.Equal(EventState.SignupClosed, existingEvent.State);
            Assert.Equal(fixture.PreLiveEventVersion, existingEvent.Version);
            Assert.Null(existingEvent.ActualStartedAt);
            Assert.Null(existingEvent.ItemPricesCapturedAt);
            Assert.Equal(fixture.PreLiveEventId, existingEvent.Id);
            Assert.Equal(fixture.PreLiveAccountId, existingEvent.CreatedByAccountId);
            Assert.Equal(SignupStatus.Confirmed, existingSignup.SignupStatus);
            Assert.Equal(fixture.PreLiveAccountId, existingSignup.AccountId);
            Assert.Equal(1, existingSignup.SignupSequence);
            Assert.Equal(fixture.PreLiveParticipantId, existingSignup.Id);
            apiItemId = await beforeStart.CatalogueItems.Where(item => item.NormalizedName == approvedApi.NormalizedName).Select(item => item.Id).SingleAsync();
            untradeableItemId = await beforeStart.CatalogueItems.Where(item => item.NormalizedName == approvedUntradeable.NormalizedName).Select(item => item.Id).SingleAsync();
        }

        var startedAt = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);
        await using (var start = new ApplicationDbContext(options))
        {
            var clock = new FixedTimeProvider(startedAt);
            var signup = new EventSignupLifecycleService(start, new EventReadinessEvaluator(start, new ConfigurationBuilder().Build()), clock);
            var lifecycle = new EventLifecycleService(start, signup, clock,
                new EventItemPriceService(start, clock, new UnavailableCatalogueApi()));
            var result = await lifecycle.StartNowAsync(
                fixture.PreLiveEventId,
                fixture.PreLiveEventVersion,
                confirmed: true,
                reason: "Start retained pre-live event after catalogue migration",
                new LifecycleActor(fixture.PreLiveAccountId, "catalogue-migration-admin"));
            Assert.True(result.Succeeded, result.Error);
        }

        await using var verify = new ApplicationDbContext(options);
        var startedEvent = await verify.Events.AsNoTracking().SingleAsync(item => item.Id == fixture.PreLiveEventId);
        var retainedSignup = await verify.EventParticipants.AsNoTracking().SingleAsync(item => item.Id == fixture.PreLiveParticipantId);
        Assert.Equal(EventState.Live, startedEvent.State);
        Assert.Equal(startedAt, startedEvent.ActualStartedAt);
        Assert.Equal(startedAt, startedEvent.ItemPricesCapturedAt);
        Assert.Equal(fixture.PreLiveEventId, startedEvent.Id);
        Assert.Equal(fixture.PreLiveAccountId, startedEvent.CreatedByAccountId);
        Assert.Equal(SignupStatus.Confirmed, retainedSignup.SignupStatus);
        Assert.Equal(fixture.PreLiveParticipantId, retainedSignup.Id);
        Assert.Equal(fixture.PreLiveAccountId, retainedSignup.AccountId);

        var captured = await verify.EventItemPrices.AsNoTracking()
            .Where(price => price.EventId == fixture.PreLiveEventId)
            .ToDictionaryAsync(price => price.ItemId);
        Assert.Equal(snapshot.Items.Length, captured.Count);
        var apiPrice = captured[apiItemId];
        Assert.Equal(approvedApi.CatalogueValueGp, apiPrice.ValueGp);
        Assert.Equal(EventItemPriceSource.CatalogueFallback, apiPrice.Source);
        Assert.Equal(CataloguePriceSource.Api, apiPrice.FallbackCatalogueSource);
        Assert.Equal(approvedApi.PriceObservedAt, apiPrice.PriceObservedAt);
        Assert.Equal(EventPriceFallbackReason.ProviderUnavailable, apiPrice.FallbackReason);
        var untradeablePrice = captured[untradeableItemId];
        Assert.Equal(0, untradeablePrice.ValueGp);
        Assert.Equal(EventItemPriceSource.CatalogueFallback, untradeablePrice.Source);
        Assert.Equal(CataloguePriceSource.Untradeable, untradeablePrice.FallbackCatalogueSource);
        Assert.Equal(EventPriceFallbackReason.NoMapping, untradeablePrice.FallbackReason);
    }

    private async Task PublishPreLiveRosterAsync(RetainedFixture fixture)
    {
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(value => value.Id == fixture.PreLiveEventId);
        var draft = await db.DraftSessions.SingleAsync(value => value.EventId == fixture.PreLiveEventId);
        var team = await db.Teams.SingleAsync(value => value.EventId == fixture.PreLiveEventId);
        var characterName = await (
            from assignment in db.EventParticipantCharacters
            join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
            where assignment.EventParticipantId == fixture.PreLiveParticipantId && assignment.EventRole == EventCharacterRole.Playing
            select character.DisplayName).SingleAsync();
        var publication = new DraftPublicationCycle(
            Guid.NewGuid(), draft.Id, 1, draft.FinalizedAt!.Value, fixture.PreLiveAccountId, DraftPublicationMethod.DirectRoster);
        var roster = new DraftPublicationRoster(
            Guid.NewGuid(), publication.Id, team.Id, fixture.PreLiveParticipantId, TeamMembershipRole.Participant,
            null, characterName);
        db.AddRange(publication, roster);
        await db.SaveChangesAsync();
    }

    private async Task PublishPreLiveBoardAsync(RetainedFixture fixture)
    {
        await using var db = new ApplicationDbContext(options);
        var board = await db.Boards.SingleAsync(value => value.EventId == fixture.PreLiveEventId);
        await BoardApprovalFixture.PublishAsync(db, board, new DateTimeOffset(2026, 9, 18, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task RetainedMigrationFailsAtomicallyWhenExactIdentityIsMissingAndCanRetryAfterRepair()
    {
        var snapshot = await ReadSnapshotAsync();
        var fixture = await SeedRetainedCatalogueAsync(snapshot, includeProtectedHistory: false);

        await using (var corrupt = new ApplicationDbContext(options))
        {
            await corrupt.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalogue_items SET name = {"Ahrim hood mismatch"} WHERE normalized_name = {"AHRIM'S HOOD"};");
        }

        await using (var migrate = new ApplicationDbContext(options))
        {
            await Assert.ThrowsAsync<PostgresException>(() => migrate.GetService<IMigrator>().MigrateAsync());
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.False(await HasAppliedMigrationAsync(verify, Migration));
            Assert.Equal(fixture.BaselineAuditCount, await verify.AuditEntries.CountAsync());
            var untouched = await verify.CatalogueItems.AsNoTracking().SingleAsync(x => x.NormalizedName == "AHRIM'S ROBETOP");
            Assert.Null(untouched.ExternalIdentifier);
            Assert.Null(untouched.CatalogueValueGp);
            Assert.Equal(fixture.BaselineItemVersions["AHRIM'S ROBETOP"], untouched.Version);
            Assert.Equal("Ahrim hood mismatch", (await verify.CatalogueItems.AsNoTracking().SingleAsync(x => x.NormalizedName == "AHRIM'S HOOD")).Name);
        }

        await using (var repair = new ApplicationDbContext(options))
        {
            await repair.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalogue_items SET name = {"Ahrim's hood"} WHERE normalized_name = {"AHRIM'S HOOD"};");
            await repair.GetService<IMigrator>().MigrateAsync();
        }

        await using var completed = new ApplicationDbContext(options);
        Assert.True(await HasAppliedMigrationAsync(completed, Migration));
        Assert.Equal(312, await completed.CatalogueItems.CountAsync());
        Assert.Equal(69, await completed.BossActivities.CountAsync());
    }

    [Fact]
    public async Task RetainedMigrationIsRepeatSafeThroughTheDeploymentHistoryBoundary()
    {
        var snapshot = await ReadSnapshotAsync();
        var fixture = await SeedRetainedCatalogueAsync(snapshot, includeProtectedHistory: false);

        await using (var first = new ApplicationDbContext(options))
            await first.GetService<IMigrator>().MigrateAsync();

        await using var beforeRepeat = new ApplicationDbContext(options);
        var beforeItem = await beforeRepeat.CatalogueItems.AsNoTracking().SingleAsync(x => x.NormalizedName == "AHRIM'S HOOD");
        var beforeBoss = await beforeRepeat.BossActivities.AsNoTracking().SingleAsync(x => x.Slug == "zalcano");
        var auditCount = await beforeRepeat.AuditEntries.CountAsync();
        var migrationCount = await beforeRepeat.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = {Migration}").SingleAsync();

        await using (var second = new ApplicationDbContext(options))
            await second.Database.MigrateAsync();

        await using var afterRepeat = new ApplicationDbContext(options);
        var afterItem = await afterRepeat.CatalogueItems.AsNoTracking().SingleAsync(x => x.NormalizedName == "AHRIM'S HOOD");
        var afterBoss = await afterRepeat.BossActivities.AsNoTracking().SingleAsync(x => x.Slug == "zalcano");
        Assert.Equal(1, migrationCount);
        Assert.Equal(auditCount, await afterRepeat.AuditEntries.CountAsync());
        Assert.Equal(beforeItem.Version, afterItem.Version);
        Assert.Equal(beforeItem.ExternalIdentifier, afterItem.ExternalIdentifier);
        Assert.Equal(beforeItem.CatalogueValueGp, afterItem.CatalogueValueGp);
        Assert.Equal(beforeItem.PriceObservedAt, afterItem.PriceObservedAt);
        Assert.Equal(beforeBoss.Version, afterBoss.Version);
        Assert.Equal(beforeBoss.ExternalIdentifier, afterBoss.ExternalIdentifier);
        Assert.Equal(fixture.BaselineAuditCount + 376, auditCount);
    }

    [Fact]
    public async Task CleanBootstrapMigrationDefersToTheReviewedFullSnapshotImporter()
    {
        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json");
        await using (var migrateToPrevious = new ApplicationDbContext(options))
            await migrateToPrevious.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        await using (var historical = new HistoricalApplicationDbContext(historicalOptions))
        {
            Assert.True(await historical.CatalogueItems.CountAsync() < 311);
            Assert.True(await historical.BossActivities.CountAsync() < 68);
            Assert.Empty(await historical.Accounts.ToListAsync());
            Assert.Empty(await historical.Events.ToListAsync());
        }

        await using (var migrate = new ApplicationDbContext(options))
        {
            await migrate.GetService<IMigrator>().MigrateAsync();
            Assert.True(await HasAppliedMigrationAsync(migrate, Migration));
        }

        await using (var verifyMigration = new ApplicationDbContext(options))
        {
            Assert.True(await verifyMigration.CatalogueItems.CountAsync() < 311);
            Assert.True(await verifyMigration.BossActivities.CountAsync() < 68);
            Assert.Empty(await verifyMigration.AuditEntries.Where(x => x.ActorUsername == MigrationActor).ToListAsync());
        }

        await using (var apply = new ApplicationDbContext(options))
        {
            var counts = await new CatalogueSnapshotService(apply, TimeProvider.System).ApplyAsync(snapshotPath);
            Assert.Equal(new CatalogueSnapshotService.SnapshotCounts(68, 311, 441), counts);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(311, await verify.CatalogueItems.CountAsync());
        Assert.Equal(195, await verify.CatalogueItems.CountAsync(x => x.PriceSource == CataloguePriceSource.Api));
        Assert.Equal(116, await verify.CatalogueItems.CountAsync(x => x.PriceSource == CataloguePriceSource.Untradeable));
        Assert.Equal(68, await verify.BossActivities.CountAsync(x => x.ExternalIdentifier != null && x.MappingStatus == ApiMappingStatus.Verified));
    }

    private async Task<RetainedFixture> SeedRetainedCatalogueAsync(
        CatalogueSnapshotService.CatalogueSnapshot snapshot,
        bool includeProtectedHistory,
        bool includePreLiveStartFixture = false)
    {
        await using (var migrateToPrevious = new ApplicationDbContext(options))
            await migrateToPrevious.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        await using var migrate = new HistoricalApplicationDbContext(historicalOptions);
        var now = new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);
        var items = await migrate.CatalogueItems.ToListAsync();
        var bosses = await migrate.BossActivities.ToListAsync();
        var baselineItemVersions = new Dictionary<string, long>(StringComparer.Ordinal);
        var baselineBossVersions = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var record in snapshot.Items)
        {
            var item = items.SingleOrDefault(x => x.NormalizedName == record.NormalizedName);
            if (item is null)
            {
                item = new CatalogueItem(Guid.NewGuid(), record.Name, record.NormalizedName);
                migrate.CatalogueItems.Add(item);
                items.Add(item);
            }
            else
            {
                item.Update(record.Name, record.NormalizedName, null, item.Notes, item.ImageUrl);
            }
        }

        foreach (var record in snapshot.Bosses)
        {
            var boss = bosses.SingleOrDefault(x => x.Slug == record.Slug);
            if (boss is null)
            {
                boss = new BossActivity(Guid.NewGuid(), record.Name, record.Slug, record.Category, record.EfficientCompletionsPerHour, now);
                migrate.BossActivities.Add(boss);
                bosses.Add(boss);
            }
            else
            {
                boss.Update(record.Name, record.Category, record.EfficientCompletionsPerHour, boss.ExternalIdentifier, boss.DataSource, boss.Notes, now, boss.ImageUrl);
            }
        }

        var manualItem = items.Single(x => x.Name == "Adamant boots");
        var newerItem = items.Single(x => x.Name == "Ahrim's hood");
        var manualAt = new DateTimeOffset(2026, 9, 16, 7, 0, 0, TimeSpan.Zero);
        var rejectedAt = new DateTimeOffset(2026, 9, 16, 7, 5, 0, TimeSpan.Zero);
        manualItem.ConfigureApi("999999");
        manualItem.SetPrice(123, CataloguePriceSource.Manual, manualAt);
        manualItem.RecordMapping(ApiMappingStatus.TemporarilyUnavailable, manualAt, "manual mapping", "manual.png");
        manualItem.RestorePriceRejection(777, rejectedAt);
        manualItem.Update(manualItem.Name, manualItem.NormalizedName, "999999", "operator note", "operator-image.png");
        manualItem.SetArtwork(10, 20, 30, 40, 1.2m, 5);
        var newerAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
        newerItem.SetPrice(987654, CataloguePriceSource.Api, newerAt);

        var conflictingBossAt = new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
        var abyssalSire = bosses.Single(x => x.Slug == "abyssal-sire");
        abyssalSire.ConfigureApi("manual_boss");
        abyssalSire.Update(abyssalSire.Name, abyssalSire.Category, abyssalSire.EfficientCompletionsPerHour,
            "manual_boss", "operator boss source", "operator boss note", now, "operator-boss.png");
        abyssalSire.RecordMapping(ApiMappingStatus.TemporarilyUnavailable, conflictingBossAt);
        var zalcano = bosses.Single(x => x.Slug == "zalcano");
        zalcano.ConfigureApi("zalcano");
        zalcano.RecordMapping(ApiMappingStatus.TemporarilyUnavailable, conflictingBossAt);
        var retainedPriorKey = bosses.Single(x => x.Slug == "araxxor");
        retainedPriorKey.ConfigureApi("araxxor");

        var extraItem = new CatalogueItem(Guid.NewGuid(), "Extra retained item", "EXTRA RETAINED ITEM");
        var extraBoss = new BossActivity(Guid.NewGuid(), "Extra retained boss", "extra-retained-boss", "Boss", 4m, now);
        var extraDrop = new SourceDrop(Guid.NewGuid(), extraBoss.Id, extraItem.Id, "1/37", .027027027m, 9.999m, now);
        migrate.AddRange(extraItem, extraBoss, extraDrop);

        BingoEvent? protectedEvent = null;
        BingoEvent? preLiveEvent = null;
        EventParticipant? preLiveParticipant = null;
        Account? preLiveAccount = null;
        if (includePreLiveStartFixture)
        {
            preLiveAccount = Account.CreateWebsite(Guid.NewGuid(), "catalogue-migration-prelive", "CATALOGUE-MIGRATION-PRELIVE", now);
            preLiveAccount.SetGlobalRole(GlobalRole.Admin);
            preLiveEvent = new BingoEvent(Guid.NewGuid(), "Retained pre-live event", "retained-pre-live-event", "UTC", preLiveAccount.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
            preLiveEvent.ConfigureSchedule(
                now.AddDays(-2),
                now.AddDays(-1),
                null,
                now.AddHours(-1),
                now.AddDays(3),
                20);
            preLiveEvent.ConfigureSignup(true, false, null);
            preLiveEvent.OpenSignups(now.AddDays(-2));
            preLiveEvent.CloseSignups(now.AddDays(-1));

            var form = new SignupForm(Guid.NewGuid(), preLiveEvent.Id, now.AddDays(-2));
            form.Publish(now.AddDays(-2));
            var primaryQuestion = new SignupQuestion(
                Guid.NewGuid(), form.Id, preLiveEvent.Id, "primary_regular_account", "Account",
                SignupQuestionType.Account, true, 0, null,
                SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var captainQuestion = new SignupQuestion(
                Guid.NewGuid(), form.Id, preLiveEvent.Id, "captain_volunteer", "Captain volunteer",
                SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
            preLiveParticipant = new EventParticipant(
                Guid.NewGuid(), preLiveEvent.Id, SignupStatus.Confirmed, 1, now.AddHours(-2), SignupSource.Website);
            preLiveParticipant.AssignOwner(preLiveAccount);
            var character = new OsrsCharacter(Guid.NewGuid(), "Retained pre-live player", "RETAINED PRE-LIVE PLAYER", now.AddHours(-2));
            var assignment = new EventParticipantCharacter(
                Guid.NewGuid(), preLiveEvent.Id, preLiveParticipant.Id, character.Id, 0,
                now.AddHours(-2), preLiveAccount.Id, primaryQuestion.Id, EventCharacterRole.Playing,
                0m, EhbSource.Manual, null);
            var team = new Team(Guid.NewGuid(), preLiveEvent.Id, "Retained pre-live team", "retained-pre-live-team", TeamFormationType.Drafted, null, true);
            team.Finalize(now.AddHours(-2));
            var membership = new TeamMembership(
                Guid.NewGuid(), team.Id, preLiveParticipant.Id, TeamMembershipRole.Participant,
                now.AddHours(-2), null, "Retained pre-live fixture");
            var board = new Board(Guid.NewGuid(), preLiveEvent.Id, "Published retained board", 1, 1);
            var draft = new DraftSession(Guid.NewGuid(), preLiveEvent.Id, 1);
            draft.FinalizeDirect(now.AddHours(-2));
            preLiveEvent.SetDraftRosterPublication(true);
            migrate.AddRange(preLiveAccount, preLiveEvent, form, primaryQuestion, captainQuestion,
                preLiveParticipant, character, assignment, team, membership, board, draft);
        }

        if (includeProtectedHistory)
        {
            var account = Account.CreateWebsite(Guid.NewGuid(), "catalogue-migration-history", "CATALOGUE-MIGRATION-HISTORY", now);
            protectedEvent = new BingoEvent(Guid.NewGuid(), "Protected catalogue history", "protected-catalogue-history", "UTC", account.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
            protectedEvent.OpenSignups(now);
            protectedEvent.CloseSignups(now.AddMinutes(1));
            protectedEvent.StartEvent(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
            migrate.AddRange(account, protectedEvent);
        }

        await migrate.SaveChangesAsync();
        foreach (var record in snapshot.Items)
            baselineItemVersions[record.NormalizedName] = items.Single(x => x.NormalizedName == record.NormalizedName).Version;
        foreach (var record in snapshot.Bosses)
            baselineBossVersions[record.Slug] = bosses.Single(x => x.Slug == record.Slug).Version;

        var baselineAuditCount = await migrate.AuditEntries.CountAsync();
        var baselineSourceDropFingerprint = await SourceDropFingerprintAsync(migrate);
        if (protectedEvent is not null)
        {
            await using var transaction = await migrate.Database.BeginTransactionAsync();
            migrate.EventItemPrices.Add(EventItemPrice.Capture(
                protectedEvent.Id,
                manualItem,
                new DateTimeOffset(2026, 9, 16, 11, 0, 0, TimeSpan.Zero),
                protectedEvent.ActualStartedAt!.Value,
                null,
                EventPriceFallbackReason.NoMapping));
            protectedEvent.MarkItemPricesCaptured();
            await migrate.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        return new RetainedFixture(
            baselineItemVersions,
            baselineBossVersions,
            baselineAuditCount,
            manualItem.Id,
            manualItem.Name,
            manualItem.ExternalIdentifier!,
            manualAt,
            manualAt,
            rejectedAt,
            newerItem.Name,
            newerAt,
            conflictingBossAt,
            extraItem.Id,
            extraBoss.Id,
            extraDrop.Id,
            extraDrop.DisplayRate,
            extraDrop.NumericProbability,
            extraDrop.DefaultEhbEstimate,
            extraDrop.Version,
            baselineSourceDropFingerprint,
            protectedEvent?.Id ?? Guid.Empty,
            protectedEvent?.ActualStartedAt ?? default,
            preLiveEvent?.Id ?? Guid.Empty,
            preLiveParticipant?.Id ?? Guid.Empty,
            preLiveAccount?.Id ?? Guid.Empty,
            preLiveEvent?.Version ?? 0);
    }

    private static async Task<CatalogueSnapshotService.CatalogueSnapshot> ReadSnapshotAsync()
    {
        return JsonSerializer.Deserialize<CatalogueSnapshotService.CatalogueSnapshot>(
                   await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json")), SnapshotJsonOptions)
               ?? throw new InvalidOperationException("The checked-in catalogue snapshot is empty.");
    }

    private static void AssertSnapshotShape(CatalogueSnapshotService.CatalogueSnapshot snapshot)
    {
        Assert.Equal(2, snapshot.SchemaVersion);
        Assert.Equal(68, snapshot.Bosses.Length);
        Assert.Equal(311, snapshot.Items.Length);
        Assert.Equal(441, snapshot.Drops.Length);
        Assert.Equal(195, snapshot.Items.Count(x => x.PriceSource == CataloguePriceSource.Api));
        Assert.Equal(116, snapshot.Items.Count(x => x.PriceSource == CataloguePriceSource.Untradeable));
        Assert.All(snapshot.Bosses, boss =>
        {
            Assert.NotNull(boss.ExternalIdentifier);
            Assert.Equal(ApiMappingStatus.Verified, boss.MappingStatus);
            Assert.NotNull(boss.MappingCheckedAt);
        });
        Assert.Equal("maggot_king", snapshot.Bosses.Single(x => x.Name == "Maggot King").ExternalIdentifier);
        Assert.Equal("zalcano", snapshot.Bosses.Single(x => x.Name == "Zalcano").ExternalIdentifier);
    }

    private static async Task<bool> HasAppliedMigrationAsync(DbContext db, string migration)
        => await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = {migration}) AS \"Value\"").SingleAsync();

    private static async Task<string> SourceDropFingerprintAsync(DbContext db)
        => JsonSerializer.Serialize(await db.Set<SourceDrop>().AsNoTracking().OrderBy(x => x.Id).Select(x => new
        {
            x.Id,
            x.BossActivityId,
            x.ItemId,
            x.DisplayRate,
            x.NumericProbability,
            x.RateConditionNote,
            x.DefaultEhbEstimate,
            x.ProbabilityScope,
            x.ConditionalOnParent,
            x.ParentProbability,
            x.AssumedParticipants,
            x.RollsPerCompletion,
            x.RollGroup,
            x.DataSource,
            x.DataUpdatedAt,
            x.Active,
            x.Version
        }).ToListAsync());

    private sealed record RetainedFixture(
        Dictionary<string, long> BaselineItemVersions,
        Dictionary<string, long> BaselineBossVersions,
        int BaselineAuditCount,
        Guid ManualItemId,
        string ManualItemName,
        string ManualItemExternalId,
        DateTimeOffset ManualPriceObservedAt,
        DateTimeOffset ManualMappingCheckedAt,
        DateTimeOffset ManualRejectedObservedAt,
        string NewerItemName,
        DateTimeOffset NewerPriceObservedAt,
        DateTimeOffset ConflictingBossMappingCheckedAt,
        Guid ExtraItemId,
        Guid ExtraBossId,
        Guid ExtraDropId,
        string ExtraDropDisplayRate,
        decimal? ExtraDropProbability,
        decimal? ExtraDropEhb,
        long ExtraDropVersion,
        string BaselineSourceDropFingerprint,
        Guid ProtectedEventId,
        DateTimeOffset ProtectedEventStartedAt,
        Guid PreLiveEventId,
        Guid PreLiveParticipantId,
        Guid PreLiveAccountId,
        long PreLiveEventVersion);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class UnavailableCatalogueApi : ICatalogueApiClient
    {
        public Task<CatalogueApiResult<IReadOnlyList<ApiItem>>> GetItemsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogueApiResult<IReadOnlySet<string>>> GetBossMetricsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(CancellationToken ct) =>
            Task.FromResult(new CatalogueApiResult<ApiHourlyPrices>(null, "controlled provider outage"));
        public Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(DateTimeOffset hour, CancellationToken ct) =>
            Task.FromResult(new CatalogueApiResult<ApiHourlyPrices>(null, "controlled provider outage"));
    }

}
