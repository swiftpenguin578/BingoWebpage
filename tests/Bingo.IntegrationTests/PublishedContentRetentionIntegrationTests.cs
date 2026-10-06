using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Announcements;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class PublishedContentRetentionIntegrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260910181345_AddCoCaptainSignupQuestion";
    private const string Password = "C21-C37-controlled-password";
    private const string PrivateReason = "PRIVATE cancellation reason sentinel";
    private static readonly byte[] Png = CreatePng(default);
    private static readonly byte[] ReplacementPng = CreatePng(new Rgba32(255, 0, 0));
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_published_content").WithUsername("bingo").WithPassword("bingo_fixture_password").Build();
    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), $"bingo-c21-c37-{Guid.NewGuid():N}");
    private DbContextOptions<ApplicationDbContext> options = null!;
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient admin = null!;
    private HttpClient anonymous = null!;
    private HttpClient ordinary = null!;
    private Account actor = null!;

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(database);
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        actor = Account.CreateWebsite(Guid.NewGuid(), "published-admin", "PUBLISHED-ADMIN", DateTimeOffset.UtcNow);
        actor.SetGlobalRole(GlobalRole.Admin);
        var participant = Account.CreateWebsite(Guid.NewGuid(), "published-viewer", "PUBLISHED-VIEWER", DateTimeOffset.UtcNow);
        foreach (var account in new[] { actor, participant })
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, Password), false, DateTimeOffset.UtcNow, incrementVersion: false);
        db.AddRange(actor, participant);
        await db.SaveChangesAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.UseSetting("EvidenceStorage:LocalPath", storageRoot);
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
        admin = Client(); anonymous = Client(); ordinary = Client();
        await LoginAsync(admin, actor.LoginName);
        await LoginAsync(ordinary, participant.LoginName);
    }

    public async Task DisposeAsync()
    {
        admin?.Dispose(); anonymous?.Dispose(); ordinary?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
        await database.DisposeAsync();
        if (Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
    }

    [Fact]
    public async Task RetainedMigrationAndPrivateRemovalKeepPublishedArtworkAndBoundCleanup()
    {
        var fixture = await SeedAsync();
        var other = await SeedAsync();
        await PublishAsync(fixture);
        var originalApproval = await ApprovalAsync(fixture);
        var originalUrl = await RenderedImageUrlAsync(fixture);
        await AssertImageAsync(anonymous, originalUrl);
        var unreferenced = await AddImageAsync(fixture);
        // Materialize the fixture with the current model, then rehearse the historical
        // schema upgrade without using current EF entities against missing columns.
        await using (var previous = new ApplicationDbContext(options))
            await previous.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await using (var migration = new ApplicationDbContext(options))
        {
            await RetainedCatalogueMigrationTestSupport.PrepareAsync(migration);
            await migration.Database.MigrateAsync();
        }
        await CorrectAsync(fixture);
        await BoardPostAsync(fixture, "Remove", new() { ["tileId"] = fixture.TileId.ToString() });
        await AssertImageAsync(anonymous, originalUrl);
        await AssertImageAsync(admin, RetainedUrl(fixture, originalApproval));
        await AssertRetainedDeniedAsync(fixture, other, originalApproval);
        await using var verify = new ApplicationDbContext(options);
        Assert.False(await verify.BoardTiles.AnyAsync(tile => tile.Id == fixture.TileId));
        Assert.True(await verify.BoardTileImageAssets.AnyAsync(image => image.Id == fixture.ImageId));
        Assert.False(await verify.BoardTileImageAssets.AnyAsync(image => image.Id == unreferenced.Id));
        Assert.True(File.Exists(Path.Combine(storageRoot, fixture.Key)));
        Assert.False(File.Exists(Path.Combine(storageRoot, unreferenced.StorageKey)));
        Assert.True(File.Exists(Path.Combine(storageRoot, other.Key)));
        Assert.True(await verify.BoardTileImageAssets.AnyAsync(image => image.Id == other.ImageId));
        Assert.Equal(originalApproval, (await verify.Boards.SingleAsync(board => board.Id == fixture.BoardId)).ActiveApprovalSnapshotId);
        Assert.Equal(fixture.Key, (await verify.BoardApprovalTileSnapshots.SingleAsync(tile => tile.ApprovalSnapshotId == originalApproval)).ArtworkReference);
        Assert.Contains(fixture.TileName, await anonymous.GetStringAsync($"/Events/{fixture.Slug}/Board/{fixture.TeamSlug}/Tiles/{fixture.TileId}"));

        // The combined C20/C21 candidate must restore the removed illustrated tile,
        // using the migrated retained metadata and the actual managed storage bytes.
        await BoardPostAsync(fixture, "DiscardCorrection", new() { ["confirmed"] = "true" });
        verify.ChangeTracker.Clear();
        var restoredBoard = await verify.Boards.SingleAsync(board => board.Id == fixture.BoardId);
        Assert.False(restoredBoard.PublishedCorrectionInProgress);
        Assert.Equal(originalApproval, restoredBoard.ActiveApprovalSnapshotId);
        var restored = await verify.BoardTiles.SingleAsync(tile => tile.Id == fixture.TileId);
        var approved = await verify.BoardApprovalTileSnapshots.SingleAsync(tile => tile.ApprovalSnapshotId == originalApproval);
        Assert.Equal(approved.TileTemplateId, restored.TileTemplateId);
        Assert.Equal(approved.Name, restored.NameSnapshot);
        Assert.Equal(approved.RowIndex, restored.RowIndex);
        Assert.Equal(approved.ColumnIndex, restored.ColumnIndex);
        Assert.Equal(approved.EstimatedEhb, restored.EstimatedEhbSnapshot);
        Assert.Equal(fixture.ImageId, restored.ActiveImageAssetId);
        Assert.Null((await verify.BoardTileImageAssets.SingleAsync(image => image.Id == fixture.ImageId)).ReplacedAt);
        var objectiveIds = await verify.BoardApprovalRequirementSnapshots.Where(x => x.ApprovalTileSnapshotId == approved.Id).Select(x => x.BoardRequirementSnapshotId).OrderBy(x => x).ToListAsync();
        Assert.Equal(objectiveIds, await verify.BoardRequirementSnapshots.Where(x => x.BoardTileId == fixture.TileId).Select(x => x.Id).OrderBy(x => x).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == fixture.EventId && x.Action == "board.published_correction_discarded").ToListAsync());
        await AssertImageAsync(admin, $"/Admin/Events/Board/{fixture.EventId}?handler=TileImage&tileId={fixture.TileId}");
        await AssertImageAsync(anonymous, originalUrl);
        await AssertImageAsync(admin, RetainedUrl(fixture, originalApproval));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrivateImageReplacementOrRemovalRetainsCurrentAndSupersededApprovalAccess(bool removeImage)
    {
        var fixture = await SeedAsync();
        var other = await SeedAsync();
        await PublishAsync(fixture);
        var originalApproval = await ApprovalAsync(fixture);
        var originalUrl = await RenderedImageUrlAsync(fixture);
        await CorrectAsync(fixture);
        var fields = new Dictionary<string, string>
        {
            ["TileDraft.TileId"] = fixture.TileId.ToString(),
            ["TileDraft.Name"] = fixture.TileName,
            ["TileDraft.ManualEhb"] = "4",
            ["TileDraft.RemoveImage"] = removeImage.ToString(),
            ["TileDraft.Requirements[0].Kind"] = "challenge",
            ["TileDraft.Requirements[0].Description"] = "Complete the challenge",
            ["TileDraft.Requirements[0].Target"] = "1"
        };
        await BoardPostAsync(fixture, "EditTile", fields, upload: !removeImage);
        await AssertImageAsync(anonymous, originalUrl);
        await using (var verify = new ApplicationDbContext(options))
        {
            var tile = await verify.BoardTiles.SingleAsync(tile => tile.Id == fixture.TileId);
            if (removeImage) Assert.Null(tile.ActiveImageAssetId);
            else Assert.NotEqual(fixture.ImageId, tile.ActiveImageAssetId);
            Assert.NotNull((await verify.BoardTileImageAssets.SingleAsync(image => image.Id == fixture.ImageId)).ReplacedAt);
        }
        await BoardPostAsync(fixture, "Approve", new() { ["confirmed"] = "true" });
        Assert.NotEqual(originalApproval, await ApprovalAsync(fixture));
        await AssertImageAsync(admin, RetainedUrl(fixture, originalApproval));
        await AssertRetainedDeniedAsync(fixture, other, originalApproval);
        if (removeImage) Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(originalUrl)).StatusCode);
        else await AssertImageAsync(anonymous, originalUrl, ReplacementPng);
        await CorrectAsync(fixture);
        await BoardPostAsync(fixture, "Remove", new() { ["tileId"] = fixture.TileId.ToString() });
        await AssertImageAsync(admin, RetainedUrl(fixture, originalApproval));
        if (!removeImage) await AssertImageAsync(anonymous, originalUrl, ReplacementPng);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnnouncementsKeepPublishedWordingAndArtworkUntilCorrectionApproval(bool approveReplacement)
    {
        var fixture = await SeedAsync();
        await PublishAsync(fixture);
        Guid requirementId;
        Guid submissionId;
        await using (var db = new ApplicationDbContext(options))
        {
            var now = DateTimeOffset.UtcNow;
            var ev = await db.Events.SingleAsync(x => x.Id == fixture.EventId);
            ev.StartEvent(now.AddMinutes(-1));
            var participant = await db.EventParticipants.SingleAsync(x => x.EventId == ev.Id);
            participant.AssignOwner(await db.Accounts.SingleAsync(x => x.Id == actor.Id));
            var character = new OsrsCharacter(Guid.NewGuid(), "Announcement player", "ANNOUNCEMENT PLAYER", now);
            db.Add(character);
            db.Add(new TeamMembership(Guid.NewGuid(), fixture.TeamId, participant.Id, TeamMembershipRole.Captain, now.AddMinutes(-1), null, null));
            requirementId = await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == fixture.TileId).Select(x => x.Id).SingleAsync();
            var submission = new Submission(Guid.NewGuid(), ev.Id, fixture.TeamId, fixture.TileId, requirementId, null,
                participant.Id, character.Id, character.DisplayName, actor.Id, 1, now, null, null);
            submission.Approve(1, now, ev.AnnouncementGeneration, true, ev.ReserveAnnouncementOrdinal());
            db.Add(submission);
            db.Add(new SubmissionContribution(Guid.NewGuid(), submission.Id, fixture.TeamId, requirementId, null, participant.Id, 1, now));
            await db.SaveChangesAsync();
            submissionId = submission.Id;
        }
        var baseline = await admin.GetFromJsonAsync<DropAnnouncementSnapshot>($"/api/drop-announcements/{fixture.EventId}");
        Assert.NotNull(baseline);
        var original = Assert.Single(baseline.Queue);
        Assert.Equal(fixture.TileName, original.TileName);
        Assert.Equal(ImageUrl(fixture), original.TileArtworkReference);
        Assert.True(original.CompletedTileAtApproval);
        await AssertImageAsync(anonymous, original.TileArtworkReference!);

        await CorrectAsync(fixture);
        await BoardPostAsync(fixture, "EditTile", new()
        {
            ["TileDraft.TileId"] = fixture.TileId.ToString(),
            ["TileDraft.Name"] = "Private replacement title",
            ["TileDraft.ManualEhb"] = "4",
            ["TileDraft.Requirements[0].RequirementId"] = requirementId.ToString(),
            ["TileDraft.Requirements[0].Kind"] = "challenge",
            ["TileDraft.Requirements[0].Description"] = "Private replacement wording",
            ["TileDraft.Requirements[0].Target"] = "1"
        }, upload: true);
        await using (var working = new ApplicationDbContext(options))
        {
            var tile = await working.BoardTiles.SingleAsync(x => x.Id == fixture.TileId);
            Assert.Equal("Private replacement title", tile.NameSnapshot);
            Assert.NotEqual(fixture.ImageId, tile.ActiveImageAssetId);
        }
        var during = await admin.GetFromJsonAsync<DropAnnouncementSnapshot>($"/api/drop-announcements/{fixture.EventId}");
        Assert.NotNull(during);
        Assert.Equal(original, Assert.Single(during.Queue));
        Assert.Equal(baseline.Generation, during.Generation);
        Assert.Equal(baseline.SnapshotSequence, during.SnapshotSequence);
        await AssertImageAsync(anonymous, original.TileArtworkReference!);

        await BoardPostAsync(fixture, approveReplacement ? "Approve" : "DiscardCorrection", new() { ["confirmed"] = "true" });
        var after = await admin.GetFromJsonAsync<DropAnnouncementSnapshot>($"/api/drop-announcements/{fixture.EventId}");
        Assert.NotNull(after);
        var entry = Assert.Single(after.Queue);
        Assert.Equal(approveReplacement ? "Private replacement title" : fixture.TileName, entry.TileName);
        Assert.Equal(original with { TileName = entry.TileName }, entry);
        Assert.Equal(baseline.Generation, after.Generation);
        Assert.Equal(baseline.SnapshotSequence, after.SnapshotSequence);
        Assert.Contains(submissionId, after.NewSubmissionIds);
        await AssertImageAsync(anonymous, entry.TileArtworkReference!, approveReplacement ? ReplacementPng : Png);
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.DropAnnouncementAcknowledgements.Where(x => x.EventId == fixture.EventId).ToListAsync());
    }

    [Fact]
    public async Task CancelledPublishedEventUsesGenericStateAcrossRealOldUrlsAndReadEntryPoints()
    {
        var fixture = await SeedAsync();
        await PublishAsync(fixture);
        var approval = await ApprovalAsync(fixture);
        var boardUrl = $"/Events/{fixture.Slug}/Board";
        var html = await anonymous.GetStringAsync(boardUrl);
        Assert.Contains("public-ui-header-context-nav", html);
        Assert.Contains("public-ui-has-secondary-nav", html);
        var teamsUrl = Link(html, $"/Events/{fixture.Slug}/Teams");
        var teamUrl = Link(html, $"/Events/{fixture.Slug}/Board/{fixture.TeamSlug}");
        var teamHtml = await anonymous.GetStringAsync(teamUrl);
        Assert.Contains("public-ui-header-context-nav", teamHtml);
        Assert.Contains("public-ui-has-secondary-nav", teamHtml);
        var tileUrl = Link(teamHtml, $"/Events/{fixture.Slug}/Board/{fixture.TeamSlug}/Tiles/{fixture.TileId}");
        var imageUrl = await RenderedImageUrlAsync(fixture);
        await AssertImageAsync(anonymous, imageUrl);
        await AssertImageAsync(anonymous, $"/Events/{fixture.Slug}/Teams/{fixture.TeamId}/Image");
        Assert.Contains("Roster sentinel", await anonymous.GetStringAsync(teamsUrl));
        Assert.Contains(fixture.TileName, await anonymous.GetStringAsync(tileUrl));
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync(ev => ev.Id == fixture.EventId);
            var manageUrl = $"/Admin/Events/Manage/{fixture.EventId}";
            await PostAsync(admin, manageUrl + "?handler=Cancel", await admin.GetStringAsync(manageUrl), new()
            {
                ["EventVersion"] = ev.Version.ToString(CultureInfo.InvariantCulture),
                ["ConfirmDestructiveAction"] = "true",
                ["CancellationReason"] = PrivateReason
            });
        }
        foreach (var client in new[] { anonymous, ordinary, admin })
        {
            foreach (var url in new[] { boardUrl, teamsUrl, teamUrl, tileUrl, boardUrl + "?view=drops&dropCount=50", boardUrl + "?view=leaderboards", tileUrl + "?handler=Sidebar" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                AssertGeneric(await response.Content.ReadAsStringAsync(), fixture);
            }
        }
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(imageUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/Events/{fixture.Slug}/Teams/{fixture.TeamId}/Image")).StatusCode);
        await AssertImageAsync(admin, RetainedUrl(fixture, approval));
        var adminBoardUrl = $"/Admin/Events/Board/{fixture.EventId}";
        Assert.Equal(HttpStatusCode.Redirect, (await admin.GetAsync(adminBoardUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.GetAsync(adminBoardUrl + $"?handler=TileImage&tileId={fixture.TileId}")).StatusCode);
        await PostAsync(admin, adminBoardUrl + "?handler=Remove", await admin.GetStringAsync($"/Admin/Events/Manage/{fixture.EventId}"), new() { ["tileId"] = fixture.TileId.ToString() });
        using var scope = factory.Services.CreateScope();
        var boards = scope.ServiceProvider.GetRequiredService<IPublicBoardService>();
        var projection = await boards.GetEventBoardAsync(fixture.Slug);
        Assert.Equal(EventState.Cancelled, projection!.EventState);
        Assert.Empty(projection.Teams); Assert.Empty(projection.RecentDrops); Assert.Empty(projection.PlayerLeaderboard);
        Assert.False(projection.SubmissionsOpen); Assert.Null(projection.WiseOldManCompetitionId);
        Assert.Null(await boards.GetTileAsync(fixture.Slug, fixture.TeamSlug, fixture.TileId));
        await using var verify = new ApplicationDbContext(options);
        var cancelled = await verify.Events.SingleAsync(ev => ev.Id == fixture.EventId);
        Assert.Equal(PrivateReason, cancelled.CancellationReason);
        Assert.True(cancelled.BoardPublished); Assert.True(cancelled.TeamRostersPublished);
        Assert.True(await verify.BoardApprovalSnapshots.AnyAsync(row => row.Id == approval));
        Assert.True(await verify.BoardTiles.AnyAsync(row => row.Id == fixture.TileId));
        Assert.True(await verify.DraftPublicationRosters.AnyAsync(row => row.TeamId == fixture.TeamId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinalizedAndArchivedPublicHistoryAndImagesRemainVisible(bool archived)
    {
        var fixture = await SeedAsync();
        await PublishAsync(fixture);
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync(ev => ev.Id == fixture.EventId);
            ev.StartEvent(DateTimeOffset.UtcNow.AddHours(-3)); ev.EndEvent(DateTimeOffset.UtcNow.AddHours(-2));
            ev.FinalizeResults(DateTimeOffset.UtcNow);
            if (archived) ev.Archive(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        foreach (var url in new[] { $"/Events/{fixture.Slug}/Board", $"/Events/{fixture.Slug}/Teams", $"/Events/{fixture.Slug}/Board/{fixture.TeamSlug}", $"/Events/{fixture.Slug}/Board/{fixture.TeamSlug}/Tiles/{fixture.TileId}" })
        {
            var html = await anonymous.GetStringAsync(url);
            Assert.DoesNotContain("data-event-cancelled", html);
            Assert.Contains(fixture.TeamName, html);
            Assert.Contains("public-ui-header-context-nav", html);
            Assert.Contains("public-ui-has-secondary-nav", html);
        }
        await AssertImageAsync(anonymous, await RenderedImageUrlAsync(fixture));
        await AssertImageAsync(admin, RetainedUrl(fixture, await ApprovalAsync(fixture)));
        await using var hidden = new ApplicationDbContext(options);
        var evHidden = await hidden.Events.SingleAsync(ev => ev.Id == fixture.EventId);
        evHidden.Hide(actor.Id, DateTimeOffset.UtcNow, evHidden.Name, "fixture quarantine");
        await hidden.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/Events/{fixture.Slug}/Board")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(ImageUrl(fixture))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(RetainedUrl(fixture, await ApprovalAsync(fixture)))).StatusCode);
    }

    [Fact]
    public async Task UnpublishedAndPrivateCancelledArtworkStaysPrivate()
    {
        var fixture = await SeedAsync();
        await BoardPostAsync(fixture, "Approve", new() { ["confirmed"] = "true" });
        var approval = await ApprovalAsync(fixture);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(ImageUrl(fixture))).StatusCode);
        await AssertImageAsync(admin, RetainedUrl(fixture, approval));
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync(ev => ev.Id == fixture.EventId);
            ev.Cancel(actor.Id, DateTimeOffset.UtcNow, PrivateReason, true);
            await db.SaveChangesAsync();
        }
        foreach (var suffix in new[] { "Board", "Teams", $"Board/{fixture.TeamSlug}", $"Board/{fixture.TeamSlug}/Tiles/{fixture.TileId}", $"Board/{fixture.TeamSlug}/Tiles/{fixture.TileId}?handler=Sidebar" })
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/Events/{fixture.Slug}/{suffix}")).StatusCode);
    }

    private async Task<Fixture> SeedAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var ev = new BingoEvent(Guid.NewGuid(), "Public retention fixture", $"retention-{Guid.NewGuid():N}", "UTC", actor.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        ev.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(1), now.AddDays(2), 10);
        ev.OpenSignups(now.AddDays(-2)); ev.CloseSignups(now.AddDays(-1));
        ev.SetDraftRosterPublication(true);
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1); draft.Start(now); draft.Finalize(now);
        var team = new Team(Guid.NewGuid(), ev.Id, "Team sentinel", "team-sentinel", TeamFormationType.Drafted, null, true); team.Finalize(now);
        var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now, actor.Id);
        var player = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated);
        var roster = new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, player.Id, TeamMembershipRole.Captain, 1, "Roster sentinel");
        var board = new Board(Guid.NewGuid(), ev.Id, "Retention board", 1, 1); board.AcquireEditing(actor.Id, now, TimeSpan.FromMinutes(30));
        var template = new TileTemplate(Guid.NewGuid(), "Tile sentinel", "Challenge", ObjectiveType.Manual, "Evidence", 4);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, "Tile sentinel", "Challenge", "Evidence", 4);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "Complete the challenge", true);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(ev, draft, team, cycle, player, roster, board, template, tile, requirement);
        await db.SaveChangesAsync();
        var fixture = new Fixture(ev.Id, ev.Slug, board.Id, tile.Id, tile.NameSnapshot, team.Id, team.Slug, team.Name, Guid.Empty, "");
        var image = await AddImageAsync(fixture);
        tile.SetActiveImageAsset(image.Id);
        var teamImage = new TeamImageAsset(Guid.NewGuid(), ev.Id, team.Id, image.StorageKey, "team.png", "image/png", Png.Length, 1, 1, image.Checksum, actor.Id, now);
        db.Add(teamImage); await db.SaveChangesAsync(); team.SetActiveImage(teamImage.Id); await db.SaveChangesAsync();
        return fixture with { ImageId = image.Id, Key = image.StorageKey };
    }

    private async Task<BoardTileImageAsset> AddImageAsync(Fixture fixture)
    {
        using var scope = factory.Services.CreateScope();
        var imageId = Guid.NewGuid();
        using var stream = new MemoryStream(Png);
        var stored = await scope.ServiceProvider.GetRequiredService<IEvidenceStorage>().StoreAsync(fixture.EventId, imageId, "fixture.png", stream);
        var image = new BoardTileImageAsset(imageId, fixture.EventId, fixture.TileId, stored.StorageKey, stored.OriginalFilename, stored.MediaType, stored.ByteSize, stored.Width, stored.Height, stored.Checksum, actor.Id, DateTimeOffset.UtcNow);
        await using var db = new ApplicationDbContext(options); db.Add(image); await db.SaveChangesAsync(); return image;
    }

    private async Task PublishAsync(Fixture fixture)
    {
        await BoardPostAsync(fixture, "Approve", new() { ["confirmed"] = "true" });
        await BoardPostAsync(fixture, "Publish", new() { ["confirmed"] = "true" });
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(BoardState.Published, (await db.Boards.SingleAsync(board => board.Id == fixture.BoardId)).State);
    }
    private Task CorrectAsync(Fixture fixture) => BoardPostAsync(fixture, "CorrectPublished", new() { ["confirmed"] = "true", ["reason"] = "Private fixture correction" });
    private async Task BoardPostAsync(Fixture fixture, string handler, Dictionary<string, string> fields, bool upload = false)
    {
        var url = $"/Admin/Events/Board/{fixture.EventId}";
        await using var db = new ApplicationDbContext(options);
        fields["BoardVersion"] = (await db.Boards.SingleAsync(board => board.Id == fixture.BoardId)).Version.ToString(CultureInfo.InvariantCulture);
        var page = await admin.GetStringAsync(url);
        if (handler == "Approve") fields["ApprovalCatalogueFingerprint"] = Regex.Match(page, "name=\"ApprovalCatalogueFingerprint\" value=\"([^\"]*)\"").Groups[1].Value;
        if (!upload) { await PostAsync(admin, url + "?handler=" + handler, page, fields); return; }
        using var content = new MultipartFormDataContent();
        fields["__RequestVerificationToken"] = Token(page);
        foreach (var field in fields) content.Add(new StringContent(field.Value), field.Key);
        var image = new ByteArrayContent(ReplacementPng); image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "TileDraft.Image", "replacement.png");
        using var response = await admin.PostAsync(url + "?handler=" + handler, content);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private async Task<Guid> ApprovalAsync(Fixture fixture)
    {
        await using var db = new ApplicationDbContext(options);
        return (await db.Boards.SingleAsync(board => board.Id == fixture.BoardId)).ActiveApprovalSnapshotId!.Value;
    }
    private async Task<string> RenderedImageUrlAsync(Fixture fixture)
    {
        var html = await anonymous.GetStringAsync($"/Events/{fixture.Slug}/Board/{fixture.TeamSlug}");
        var url = Regex.Matches(html, "src=\"([^\"]+)\"").Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).First(value => value.Contains($"/Board/Tiles/{fixture.TileId}/Image", StringComparison.Ordinal));
        return url;
    }
    private async Task AssertRetainedDeniedAsync(Fixture fixture, Fixture other, Guid approval)
    {
        foreach (var client in new[] { anonymous, ordinary })
        {
            var response = await client.GetAsync(RetainedUrl(fixture, approval));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(RetainedUrl(other with { TileId = fixture.TileId }, approval))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(ImageUrl(other with { TileId = fixture.TileId }))).StatusCode);
    }
    private static async Task AssertImageAsync(HttpClient client, string url, byte[]? expected = null)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expected ?? Png, await response.Content.ReadAsByteArrayAsync());
    }
    private static void AssertGeneric(string html, Fixture fixture)
    {
        Assert.Contains("data-event-cancelled", html); Assert.Contains("This event will not take place.", html);
        foreach (var denied in new[] { PrivateReason, fixture.TeamName, fixture.TileName, "Roster sentinel", "data-submission", "data-team-focus", "public-team-workspace", "data-public-recent-drops", "public-ui-header-context-nav", "public-ui-has-secondary-nav", "/Captain/", "/Participant/" })
            Assert.DoesNotContain(denied, html);
    }
    private static string Link(string html, string path) => Regex.Matches(html, "href=\"([^\"]+)\"").Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).First(value => value.Equals(path, StringComparison.OrdinalIgnoreCase));
    private static byte[] CreatePng(Rgba32 color)
    {
        using var image = new Image<Rgba32>(2, 2, color);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
    private static string ImageUrl(Fixture fixture) => $"/Events/{fixture.Slug}/Board/Tiles/{fixture.TileId}/Image";
    private static string RetainedUrl(Fixture fixture, Guid approval) => $"/Admin/Events/Board/{fixture.EventId}?handler=TileImage&tileId={fixture.TileId}&approvalId={approval}";
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    private static async Task LoginAsync(HttpClient client, string username) => await PostAsync(client, "/Account/Login", await client.GetStringAsync("/Account/Login"), new() { ["Input.Username"] = username, ["Input.Password"] = Password });
    private static string Token(string html) => Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private static async Task PostAsync(HttpClient client, string url, string page, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = Token(page); Assert.NotEmpty(fields["__RequestVerificationToken"]);
        using var response = await client.PostAsync(url, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private sealed record Fixture(Guid EventId, string Slug, Guid BoardId, Guid TileId, string TileName, Guid TeamId, string TeamSlug, string TeamName, Guid ImageId, string Key);
}
