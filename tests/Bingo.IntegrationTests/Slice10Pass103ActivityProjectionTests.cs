using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.WiseOldMan;
using Bingo.Web.Catalogue;
using Bingo.Web.Security;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice10Pass103ActivityProjectionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice10_pass103")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task DevelopmentResetSeedsCaptainOperationsInventory()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var db = new ApplicationDbContext(options);
        const string owner = "captain-fixture-reset-owner";
        await new OperatorRecoveryService(db, clock, new Microsoft.AspNetCore.Identity.PasswordHasher<Account>())
            .BootstrapOwnerAsync(owner, "captain-fixture-reset-password", owner, CancellationToken.None);
        await new CatalogueSnapshotService(db, clock).ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));

        var seeder = new DevelopmentScenarioSeeder(db, new DevelopmentEnvironment(), new Microsoft.AspNetCore.Identity.PasswordHasher<Account>(), new SeedEvidenceStorage(), clock);
        await seeder.ResetAndSeedAsync();
        var live = await db.Events.SingleAsync(value => value.Slug == "test-15-dkl-live");
        var firstTeam = await db.Teams.Where(value => value.EventId == live.Id).OrderBy(value => value.DraftPosition).FirstAsync();
        var focusKinds = await db.TeamFocusMarkers.Where(value => value.EventId == live.Id && value.TeamId == firstTeam.Id && value.Focused).Select(value => value.TargetKind).ToListAsync();
        Assert.Contains(TeamFocusTargetKind.Tile, focusKinds);
        Assert.Contains(TeamFocusTargetKind.Row, focusKinds);
        Assert.Contains(TeamFocusTargetKind.Column, focusKinds);
        var submissions = await db.Submissions.Where(value => value.EventId == live.Id && value.TeamId == firstTeam.Id).ToListAsync();
        Assert.Contains(submissions, value => value.Status == SubmissionStatus.Withdrawn);
        Assert.Contains(submissions, value => value.ResubmissionOfSubmissionId is not null);
        Assert.Contains(submissions, source => source.Status == SubmissionStatus.Rejected && submissions.Any(child => child.ResubmissionOfSubmissionId == source.Id));
    }

    [Fact]
    public async Task CompleteAndPartialProjectionsUseMatchedCurrentRowsWithoutCarryForward()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var setup = await SeedAsync(now);
        var fingerprint = await FingerprintAsync(setup.Event.Id);
        var state = new EventCompetitionSynchronization(
            Guid.NewGuid(), setup.Event.Id, 1, 55, "Projection competition",
            setup.Event.EventStartsAt, setup.Event.EventEndsAt, fingerprint, now);
        state.MarkSuccess(now, now, true, "[]", null);
        await using (var db = new ApplicationDbContext(options))
        {
            db.EventCompetitionSynchronizations.Add(state);
            db.EventCompetitionCharacterActivities.AddRange(
                Activity(setup.Event.Id, 1, 55, setup.FirstCharacter.Id, 5m, fingerprint, now),
                Activity(setup.Event.Id, 1, 55, setup.SecondCharacter.Id, 7m, fingerprint, now),
                Activity(setup.Event.Id, 1, 55, setup.OtherCharacter.Id, 12m, fingerprint, now));
            await db.SaveChangesAsync();
            var projection = new CachedEventCompetitionActivityProjection(db, clock);

            var complete = await projection.GetAsync(setup.Event.Id);
            var team = Assert.Single(complete.Teams);
            Assert.Equal(EventCompetitionActivityState.Complete, complete.State);
            Assert.Equal(24m, team.TotalGainedEhb);
            Assert.Equal(12m, team.AverageGainedEhb);
            Assert.Equal(["Alice", "Bob"], team.MvpNames);
            var alice = Assert.Single(team.Participants, value => value.ParticipantName == "Alice");
            Assert.Equal(["Alice", "Alice second"], alice.PlayingAccountNames);
            Assert.Equal(12m, alice.TotalGainedEhb);
            Assert.Equal([5m, 7m], alice.Accounts.Select(value => value.GainedEhb).ToArray());
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var partial = await db.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == setup.Event.Id);
            partial.BeginReplacementGeneration(fingerprint, now.AddMinutes(1));
            partial.MarkSuccess(now.AddMinutes(1), now.AddMinutes(1), false, "[\"Bob\"]", "The competition response is missing one or more current Playing accounts.");
            db.EventCompetitionCharacterActivities.AddRange(
                Activity(setup.Event.Id, partial.Generation, 55, setup.FirstCharacter.Id, 9m, fingerprint, now.AddMinutes(1)),
                Activity(setup.Event.Id, partial.Generation, 55, setup.SecondCharacter.Id, 11m, fingerprint, now.AddMinutes(1)));
            await db.SaveChangesAsync();
            var projection = new CachedEventCompetitionActivityProjection(db, clock);

            var latest = await projection.GetAsync(setup.Event.Id);
            Assert.Equal(EventCompetitionActivityState.Partial, latest.State);
            Assert.True(latest.HasRankings);
            Assert.Equal(3, latest.ExpectedAccountCount);
            Assert.Equal(2, latest.MatchedAccountCount);
            var partialTeam = Assert.Single(latest.Teams);
            Assert.Equal(20m, partialTeam.TotalGainedEhb);
            Assert.Equal(20m, partialTeam.AverageGainedEhb);
            Assert.Equal(3, partialTeam.ExpectedAccountCount);
            Assert.Equal(2, partialTeam.MatchedAccountCount);
            Assert.Equal(["Alice"], partialTeam.MvpNames);
            Assert.DoesNotContain(partialTeam.Participants, value => value.ParticipantName == "Bob");
            Assert.DoesNotContain(partialTeam.Participants.SelectMany(value => value.Accounts), value => value.CharacterId == setup.OtherCharacter.Id);
            Assert.Equal(2, await db.EventCompetitionCharacterActivities.CountAsync(value => value.EventId == setup.Event.Id && value.Generation == partial.Generation));
            Assert.Equal(5, await db.EventCompetitionCharacterActivities.CountAsync());

            partial.BeginReplacementGeneration(fingerprint, now.AddMinutes(2));
            partial.MarkSuccess(now.AddMinutes(2), now.AddMinutes(2), false, "[\"Alice\",\"Bob\"]", "The competition response is missing one or more current Playing accounts.");
            await db.SaveChangesAsync();
            var noMatches = await projection.GetAsync(setup.Event.Id);
            Assert.Equal(EventCompetitionActivityState.Partial, noMatches.State);
            Assert.False(noMatches.HasRankings);
            Assert.Equal(3, noMatches.ExpectedAccountCount);
            Assert.Equal(0, noMatches.MatchedAccountCount);
            Assert.All(noMatches.Teams, value => Assert.Empty(value.Participants));
        }
    }

    [Fact]
    public async Task ProjectionReportsConfiguredWaitingStaleAndTemporaryStatesWithoutRequests()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var setup = await SeedAsync(now);
        await using (var db = new ApplicationDbContext(options))
        {
            var projection = new CachedEventCompetitionActivityProjection(db, clock);
            Assert.Equal(EventCompetitionActivityState.NotConfigured, (await projection.GetAsync(setup.Event.Id)).State);

            var fingerprint = await FingerprintAsync(setup.Event.Id);
            var waiting = new EventCompetitionSynchronization(
                Guid.NewGuid(), setup.Event.Id, 1, 55, "Projection competition",
                setup.Event.EventStartsAt, setup.Event.EventEndsAt, fingerprint, now);
            db.EventCompetitionSynchronizations.Add(waiting);
            db.EventCompetitionCharacterActivities.AddRange(
                Activity(setup.Event.Id, 1, 55, setup.FirstCharacter.Id, 5m, fingerprint, now.AddHours(-3)),
                Activity(setup.Event.Id, 1, 55, setup.SecondCharacter.Id, 7m, fingerprint, now.AddHours(-3)),
                Activity(setup.Event.Id, 1, 55, setup.OtherCharacter.Id, 12m, fingerprint, now.AddHours(-3)));
            await db.SaveChangesAsync();
            Assert.Equal(EventCompetitionActivityState.WaitingForFirstSync, (await projection.GetAsync(setup.Event.Id)).State);

            waiting.MarkSuccess(now.AddHours(-3), now.AddHours(-3), true, "[]", null);
            await db.SaveChangesAsync();
            Assert.Equal(EventCompetitionActivityState.Stale, (await projection.GetAsync(setup.Event.Id)).State);

            waiting.BeginReplacementGeneration(fingerprint, now.AddMinutes(-1));
            waiting.MarkSuccess(now.AddMinutes(-1), now.AddMinutes(-1), false, "[\"Bob\"]", "The competition response is missing one or more current Playing accounts.");
            db.EventCompetitionCharacterActivities.Add(
                Activity(setup.Event.Id, waiting.Generation, 55, setup.FirstCharacter.Id, 5m, fingerprint, now.AddMinutes(-1)));
            await db.SaveChangesAsync();

            var partial = await projection.GetAsync(setup.Event.Id);
            Assert.Equal(EventCompetitionActivityState.Partial, partial.State);
            Assert.True(partial.HasRankings);
            Assert.Equal(3, partial.ExpectedAccountCount);
            Assert.Equal(1, partial.MatchedAccountCount);
            var partialTeam = Assert.Single(partial.Teams);
            Assert.Equal(5m, partialTeam.TotalGainedEhb);
            Assert.Equal(5m, partialTeam.AverageGainedEhb);
            Assert.Equal(1, partialTeam.MatchedAccountCount);
            var partialRank = partialTeam.Rank;

            waiting.MarkFailure(now, "Unavailable", "temporary", now.AddMinutes(1));
            await db.SaveChangesAsync();
            Assert.Equal(now.AddMinutes(1), waiting.RetryDueAt);
            var temporary = await projection.GetAsync(setup.Event.Id);
            Assert.Equal(EventCompetitionActivityState.TemporarilyUnavailable, temporary.State);
            Assert.True(temporary.HasRankings);
            Assert.Equal(partial.Generation, temporary.Generation);
            Assert.Equal(partial.MatchedAccountCount, temporary.MatchedAccountCount);
            var retainedTeam = Assert.Single(temporary.Teams);
            Assert.Equal(partialRank, retainedTeam.Rank);
            Assert.Equal(partialTeam.TotalGainedEhb, retainedTeam.TotalGainedEhb);
            Assert.Equal(partialTeam.AverageGainedEhb, retainedTeam.AverageGainedEhb);
            Assert.Equal(partialTeam.MatchedAccountCount, retainedTeam.MatchedAccountCount);
            Assert.Equal(["Alice"], retainedTeam.MvpNames);
            Assert.Single(retainedTeam.Participants);
        }
    }

    [Fact]
    public async Task RenderedPublicBoardAndTeamBoardUseOnlyTheCachedProjection()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new TestClock(now);
        await using (var db = new ApplicationDbContext(options))
        {
            const string owner = "slice10-pass103-render-owner";
            await new OperatorRecoveryService(db, clock, new Microsoft.AspNetCore.Identity.PasswordHasher<Account>())
                .BootstrapOwnerAsync(owner, "slice10-pass103-password", owner, CancellationToken.None);
            await new CatalogueSnapshotService(db, clock).ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));
            var admin = await db.Accounts.SingleAsync(value => value.LoginName == owner);
            var seeder = new DevelopmentScenarioSeeder(db, new DevelopmentEnvironment(), new Microsoft.AspNetCore.Identity.PasswordHasher<Account>(), new SeedEvidenceStorage(), clock);
            await seeder.ResetAndSeedAsync();
            Assert.Equal(GlobalRole.SuperAdmin, admin.GlobalRole);
            var lookupEvent = await db.Events.SingleAsync(value => value.Slug == "test-16-signup-lookup");
            Assert.Equal(EventState.SignupOpen, lookupEvent.State);
            Assert.Equal(9, await db.EventParticipants.CountAsync(value => value.EventId == lookupEvent.Id));
            var secondaryAdminId = await db.Accounts.Where(value => value.LoginName == DevelopmentScenarioSeeder.SecondaryAdminUsername).Select(value => value.Id).SingleAsync();
            Assert.Single(await db.AccountOsrsCharacters.Where(value => value.AccountId == secondaryAdminId && value.Active).ToListAsync());
            var seededLiveId = await db.Events.Where(value => value.Slug == "test-15-dkl-live").Select(value => value.Id).SingleAsync();
            var seededSynchronization = await db.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == seededLiveId);
            Assert.True(seededSynchronization.NormalDueAt <= clock.GetUtcNow());
            Assert.True(seededSynchronization.LastSuccessfulAt <= clock.GetUtcNow().AddHours(-2));
        }

        Guid liveId;
        await using (var db = new ApplicationDbContext(options))
        {
            var live = await db.Events.SingleAsync(value => value.Slug == "test-15-dkl-live");
            liveId = live.Id;
            var synchronization = await db.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == live.Id);
            var assignments = await db.EventParticipantCharacters.AsNoTracking()
                .Where(value => value.EventId == live.Id && value.EventRole == EventCharacterRole.Playing && value.ReleasedAt == null &&
                                db.EventParticipants.Any(participant => participant.Id == value.EventParticipantId && participant.SignupStatus == SignupStatus.Confirmed))
                .Join(db.OsrsCharacters.AsNoTracking(), value => value.OsrsCharacterId, character => character.Id,
                    (value, character) => new { value.OsrsCharacterId, character.DisplayName })
                .ToListAsync();
            var fetchedAt = clock.GetUtcNow();
            synchronization.BeginReplacementGeneration(synchronization.AssignmentFingerprint, fetchedAt);
            synchronization.MarkSuccess(fetchedAt, fetchedAt, false, "[\"Dev Activity Secondary\"]", "The competition response is missing one or more current Playing accounts.");
            db.EventCompetitionCharacterActivities.AddRange(assignments
                .Where(value => value.DisplayName != "Dev Activity Secondary")
                .Select(value => new EventCompetitionCharacterActivity(Guid.NewGuid(), live.Id, synchronization.Generation, 1515,
                    value.OsrsCharacterId, 1m, fetchedAt, fetchedAt, synchronization.AssignmentFingerprint)));
            synchronization.MarkFailure(fetchedAt.AddSeconds(1), "Unavailable", "temporary", fetchedAt.AddSeconds(2));
            await db.SaveChangesAsync();
        }

        var fake = new CountingCompetitionClient();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .UseSetting("WiseOldMan:DevelopmentFake:AutomaticSynchronizationEnabled", "false")
            .ConfigureServices(services =>
            {
                services.RemoveAll<IWiseOldManCompetitionClient>();
                services.AddSingleton<IWiseOldManCompetitionClient>(fake);
                services.RemoveAll<IEvidenceStorage>();
                services.AddSingleton<IEvidenceStorage, SeedEvidenceStorage>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var board = await client.GetStringAsync("/Events/test-15-dkl-live/Board?view=leaderboards&ranking=activity");
        Assert.Contains("aria-label=\"Leaderboards\"", board, StringComparison.Ordinal);
        Assert.True(board.IndexOf(">EHB</a>", StringComparison.Ordinal) < board.IndexOf(">Drop EHB</a>", StringComparison.Ordinal));
        Assert.Contains("data-public-leaderboard-switcher", board, StringComparison.Ordinal);
        Assert.Contains("public-ui-view-switcher-item is-current", board, StringComparison.Ordinal);
        Assert.Contains("view=leaderboards&amp;ranking=activity", board, StringComparison.Ordinal);
        Assert.Contains("view=leaderboards&amp;ranking=drops", board, StringComparison.Ordinal);
        Assert.DoesNotContain("public-ui-table-nav", board, StringComparison.Ordinal);
        Assert.DoesNotContain("leaderboards-heading", board, StringComparison.Ordinal);
        Assert.DoesNotContain("Performance", board, StringComparison.Ordinal);
        Assert.DoesNotContain("Official bingo position stays visible while you explore player performance.", board, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"EHB leaderboard\"", board, StringComparison.Ordinal);
        Assert.Contains(">WOM</th>", board, StringComparison.Ordinal);
        Assert.DoesNotContain("Wise Old Man integration", board, StringComparison.Ordinal);
        Assert.DoesNotContain(">EHB</strong>", board, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity EHB", board, StringComparison.Ordinal);
        Assert.Contains("Dev Player 001", board, StringComparison.Ordinal);
        Assert.Contains("Dev Activity Secondary", board, StringComparison.Ordinal);
        Assert.Contains("public-ui-leaderboard-detail-row", board, StringComparison.Ordinal);
        Assert.Contains("public-ui-table--nested", board, StringComparison.Ordinal);
        Assert.Contains("Start EHB", board, StringComparison.Ordinal);
        Assert.Contains("End EHB", board, StringComparison.Ordinal);
        Assert.Contains("—", board, StringComparison.Ordinal);
        Assert.Contains("wiseoldman.net/players/Dev%20Player%20001", board, StringComparison.Ordinal);
        Assert.Contains("wiseoldman.net/players/Dev%20Activity%20Secondary", board, StringComparison.Ordinal);
        Assert.Contains("Avg. gained", board, StringComparison.Ordinal);
        Assert.Contains("MVP", board, StringComparison.Ordinal);
        Assert.Contains("class=\"public-ui-table\"", board, StringComparison.Ordinal);
        Assert.Contains("<td><strong class=\"public-ui-section-heading public-ui-positive-delta--success\">", board, StringComparison.Ordinal);
        Assert.Contains("public-ui-leaderboard-detail-summary", board, StringComparison.Ordinal);
        Assert.Contains("<td><strong class=\"public-ui-section-heading\">", board, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary><strong class=\"public-ui-section-heading\">", board, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-expand-control", board, StringComparison.Ordinal);
        Assert.Contains("<path d=\"m6 9 6 6 6-6\" />", board, StringComparison.Ordinal);
        Assert.Contains("<details ", board, StringComparison.Ordinal);
        Assert.Contains("public-ui-standings", board, StringComparison.Ordinal);
        var dropBoard = await client.GetStringAsync("/Events/test-15-dkl-live/Board?view=leaderboards&ranking=drops");
        Assert.Contains("data-public-leaderboard-switcher", dropBoard, StringComparison.Ordinal);
        Assert.Contains("public-ui-view-switcher-item is-current", dropBoard, StringComparison.Ordinal);
        Assert.DoesNotContain(">Drop EHB</strong>", dropBoard, StringComparison.Ordinal);
        Assert.DoesNotContain("public-ui-table-nav", dropBoard, StringComparison.Ordinal);
        Assert.Contains("class=\"public-ui-table\"", dropBoard, StringComparison.Ordinal);
        Assert.Contains("public-ui-leaderboard-detail-row", dropBoard, StringComparison.Ordinal);
        Assert.Contains("public-ui-table--nested", dropBoard, StringComparison.Ordinal);
        Assert.Contains("Total drops", dropBoard, StringComparison.Ordinal);
        Assert.Contains("Drop EHB", dropBoard, StringComparison.Ordinal);
        Assert.Contains("<td><strong class=\"public-ui-section-heading public-ui-positive-delta--success\">", dropBoard, StringComparison.Ordinal);
        Assert.Contains("<td><strong class=\"public-ui-section-heading\">", dropBoard, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary><strong class=\"public-ui-section-heading\">", dropBoard, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-expand-control", dropBoard, StringComparison.Ordinal);
        Assert.Contains("public-ui-standings", dropBoard, StringComparison.Ordinal);
        var team = await client.GetStringAsync("/Events/test-15-dkl-live/Board/touch-kids-not-grass");
        Assert.Contains("aria-label=\"EHB\"", team, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity EHB", team, StringComparison.Ordinal);
        Assert.Contains("Team total", team, StringComparison.Ordinal);
        Assert.Contains("Dev Player 001", team, StringComparison.Ordinal);
        Assert.Contains("Provisional coverage", team, StringComparison.Ordinal);
        Assert.Contains("Wise Old Man is temporarily unavailable. Showing the last available cache.", team, StringComparison.Ordinal);
        Assert.DoesNotContain("Dev Activity Secondary", team, StringComparison.Ordinal);
        var login = await client.GetStringAsync("/Account/Login");
        using (var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = DevelopmentScenarioSeeder.SecondaryAdminUsername,
            ["Input.Password"] = DevelopmentScenarioSeeder.SecondaryAdminPassword,
            ["__RequestVerificationToken"] = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value
        }))) Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        var manage = await client.GetStringAsync($"/Admin/Events/Manage/{liveId}");
        Assert.Contains("Dev Activity Secondary", manage, StringComparison.Ordinal);
        Assert.Contains("Partial", manage, StringComparison.Ordinal);
        Assert.Equal(0, fake.Calls);

        await using (var db = new ApplicationDbContext(options))
        {
            var synchronization = await db.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == liveId);
            var missingCharacter = await db.OsrsCharacters.SingleAsync(value => value.DisplayName == "Dev Activity Secondary");
            var completedAt = DateTimeOffset.UtcNow;
            synchronization.MarkSuccess(completedAt, completedAt, true, "[]", null);
            db.EventCompetitionCharacterActivities.Add(new EventCompetitionCharacterActivity(
                Guid.NewGuid(), liveId, synchronization.Generation, 1515, missingCharacter.Id, 1m,
                completedAt, completedAt, synchronization.AssignmentFingerprint));
            await db.SaveChangesAsync();
        }

        var completeBoard = await client.GetStringAsync("/Events/test-15-dkl-live/Board?view=leaderboards&ranking=activity");
        var completeTeam = await client.GetStringAsync("/Events/test-15-dkl-live/Board/touch-kids-not-grass");
        Assert.DoesNotContain("Provisional coverage", completeTeam, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DevelopmentTest15DueControlIsIdempotentAndResetRemainsCachedOnly()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        const string owner = "slice10-pass103-control-owner";
        await using (var db = new ApplicationDbContext(options))
        {
            await new OperatorRecoveryService(db, clock, new Microsoft.AspNetCore.Identity.PasswordHasher<Account>())
                .BootstrapOwnerAsync(owner, "slice10-pass103-control-password", owner, CancellationToken.None);
            await new CatalogueSnapshotService(db, clock).ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));
            var seeder = new DevelopmentScenarioSeeder(db, new DevelopmentEnvironment(), new Microsoft.AspNetCore.Identity.PasswordHasher<Account>(), new SeedEvidenceStorage(), clock);
            await seeder.ResetAndSeedAsync();
            var live = await db.Events.SingleAsync(value => value.Slug == "test-15-dkl-live");
            var synchronization = await db.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == live.Id);
            synchronization.MarkSuccess(clock.GetUtcNow(), clock.GetUtcNow(), true, "[]", null);
            await db.SaveChangesAsync();
        }

        List<string> expectedNames;
        await using (var db = new ApplicationDbContext(options))
        {
            var seededLiveId = await db.Events.Where(value => value.Slug == "test-15-dkl-live").Select(value => value.Id).SingleAsync();
            expectedNames = await db.EventParticipantCharacters.AsNoTracking()
                .Where(value => value.EventId == seededLiveId && value.EventRole == EventCharacterRole.Playing && value.ReleasedAt == null &&
                                db.EventParticipants.Any(participant => participant.Id == value.EventParticipantId && participant.SignupStatus == SignupStatus.Confirmed))
                .Join(db.OsrsCharacters.AsNoTracking(), value => value.OsrsCharacterId, character => character.Id, (_, character) => character.DisplayName)
                .ToListAsync();
        }
        var fake = new CountingCompetitionClient(new WiseOldManCompetitionResult(
            WiseOldManCompetitionStatus.Success,
            new WiseOldManCompetition(1515, "TEST 15 development fake competition", clock.GetUtcNow().AddHours(-1), clock.GetUtcNow().AddDays(5), clock.GetUtcNow(),
                expectedNames.Select(name => new WiseOldManCompetitionParticipant(name, "REGULAR", 1m)).ToArray())));

        Guid liveId;
        await using (var ids = new ApplicationDbContext(options))
            liveId = await ids.Events.Where(value => value.Slug == "test-15-dkl-live").Select(value => value.Id).SingleAsync();
        await using (var before = new ApplicationDbContext(options))
        {
            var synchronization = await before.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == liveId);
            Assert.True(synchronization.NormalDueAt > clock.GetUtcNow());
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .UseSetting("WiseOldMan:DevelopmentFake:AutomaticSynchronizationEnabled", "false")
            .ConfigureServices(services =>
            {
                services.RemoveAll<IWiseOldManCompetitionClient>();
                services.AddSingleton<IWiseOldManCompetitionClient>(fake);
                services.RemoveAll<IEvidenceStorage>();
                services.AddSingleton<IEvidenceStorage, SeedEvidenceStorage>();
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using (var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = DevelopmentScenarioSeeder.SecondaryAdminUsername,
            ["Input.Password"] = DevelopmentScenarioSeeder.SecondaryAdminPassword,
            ["__RequestVerificationToken"] = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value
        }))) Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var manageRoute = $"/Admin/Events/Manage/{liveId}";
        var manage = await client.GetStringAsync(manageRoute);
        using (var due = await client.PostAsync($"{manageRoute}?handler=MakeDevelopmentCompetitionDue", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Regex.Match(manage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value
        }))) Assert.Equal(HttpStatusCode.Redirect, due.StatusCode);
        Assert.Equal(0, fake.Calls);

        var refreshPage = await client.GetStringAsync(manageRoute);
        using (var refreshed = await client.PostAsync($"{manageRoute}?handler=RefreshCompetition", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Regex.Match(refreshPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value
        }))) Assert.Equal(HttpStatusCode.Redirect, refreshed.StatusCode);
        Assert.Equal(1, fake.Calls);

        await using (var verify = new ApplicationDbContext(options))
        {
            var state = await verify.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == (verify.Events.Where(value => value.Slug == "test-15-dkl-live").Select(value => value.Id).Single()));
            Assert.True(state.LatestComplete);
            Assert.NotNull(state.LastSuccessfulAt);
            Assert.Equal(state.LastSuccessfulAt!.Value.AddHours(2), state.NormalDueAt);
            Assert.Null(state.RetryDueAt);
            Assert.Equal(expectedNames.Count, await verify.EventCompetitionCharacterActivities.CountAsync(value => value.EventId == state.EventId && value.Generation == state.Generation));
        }

        Guid lookupId;
        await using (var ids = new ApplicationDbContext(options))
            lookupId = await ids.Events.Where(value => value.Slug == "test-16-signup-lookup").Select(value => value.Id).SingleAsync();
        var lookupRoute = $"/Admin/Events/Manage/{lookupId}";
        var lookupPage = await client.GetStringAsync(lookupRoute);
        using (var rejected = await client.PostAsync($"{lookupRoute}?handler=RefreshCompetition", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Regex.Match(lookupPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value
        }))) Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("This event is read-only in its current lifecycle state.", await client.GetStringAsync(lookupRoute), StringComparison.Ordinal);
        Assert.Equal(1, fake.Calls);
        await using (var verifyRejected = new ApplicationDbContext(options))
            Assert.False(await verifyRejected.EventCompetitionSynchronizations.AnyAsync(value => value.EventId == lookupId));

        await using (var reset = new ApplicationDbContext(options))
        {
            var seeder = new DevelopmentScenarioSeeder(reset, new DevelopmentEnvironment(), new Microsoft.AspNetCore.Identity.PasswordHasher<Account>(), new SeedEvidenceStorage(), clock);
            await seeder.ResetAndSeedAsync();
            await seeder.ResetAndSeedAsync();
            var live = await reset.Events.SingleAsync(value => value.Slug == "test-15-dkl-live");
            Assert.True((await reset.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == live.Id)).NormalDueAt <= clock.GetUtcNow());
            var lookup = await reset.Events.SingleAsync(value => value.Slug == "test-16-signup-lookup");
            Assert.Equal(EventState.SignupOpen, lookup.State);
            Assert.Equal(9, await reset.EventParticipants.CountAsync(value => value.EventId == lookup.Id));
            Assert.Equal(22, await reset.Events.CountAsync());
        }
        Assert.Equal(1, fake.Calls);
    }

    private async Task<Seed> SeedAsync(DateTimeOffset now)
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "projection-admin", "PROJECTION-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Projection event", $"projection-{Guid.NewGuid():N}", null, "UTC",
            now.AddHours(-2), now.AddHours(-1), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 10, admin.Id, now);
        bingoEvent.OpenSignups(now.AddHours(-2));
        bingoEvent.CloseSignups(now.AddHours(-1));
        bingoEvent.StartEvent(now);
        var team = new Team(Guid.NewGuid(), bingoEvent.Id, "Projection team", "projection-team", TeamFormationType.Drafted, null, true);
        team.Finalize(now);
        var alice = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated);
        var bob = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 2, now, SignupSource.AdminCreated);
        var first = new OsrsCharacter(Guid.NewGuid(), "Alice", "ALICE", now);
        var second = new OsrsCharacter(Guid.NewGuid(), "Alice second", "ALICE SECOND", now);
        var other = new OsrsCharacter(Guid.NewGuid(), "Bob", "BOB", now);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, bingoEvent, team, alice, bob, first, second, other);
        db.TeamMemberships.AddRange(
            new TeamMembership(Guid.NewGuid(), team.Id, alice.Id, TeamMembershipRole.Participant, now, null, "projection"),
            new TeamMembership(Guid.NewGuid(), team.Id, bob.Id, TeamMembershipRole.Participant, now, null, "projection"));
        db.EventParticipantCharacters.AddRange(
            new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, alice.Id, first.Id, 0, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null),
            new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, alice.Id, second.Id, 1, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null),
            new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, bob.Id, other.Id, 0, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null));
        await db.SaveChangesAsync();
        return new(bingoEvent, team, first, second, other);
    }

    private static EventCompetitionCharacterActivity Activity(
        Guid eventId, int generation, long competitionId, Guid characterId, decimal gained,
        string fingerprint, DateTimeOffset fetchedAt) =>
        new(Guid.NewGuid(), eventId, generation, competitionId, characterId, gained, fetchedAt, fetchedAt, fingerprint);

    private async Task<string> FingerprintAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        var values = await db.EventParticipantCharacters.AsNoTracking()
            .Where(value => value.EventId == eventId && value.EventRole == EventCharacterRole.Playing && value.ReleasedAt == null &&
                            db.EventParticipants.Any(participant => participant.Id == value.EventParticipantId && participant.SignupStatus == SignupStatus.Confirmed))
            .OrderBy(value => value.Id)
            .Select(value => $"{value.Id:N}:{value.EventParticipantId:N}:{value.OsrsCharacterId:N}")
            .ToListAsync();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', values)))).ToLowerInvariant();
    }

    private sealed record Seed(BingoEvent Event, Team Team, OsrsCharacter FirstCharacter, OsrsCharacter SecondCharacter, OsrsCharacter OtherCharacter);

    private sealed class CountingCompetitionClient(WiseOldManCompetitionResult? configuredResult = null) : IWiseOldManCompetitionClient
    {
        public int Calls { get; private set; }
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(configuredResult ?? new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, Message: "The render test must not request Wise Old Man."));
        }
    }

    private sealed class FixedStatus : IWiseOldManStatus
    {
        public WiseOldManRequestStatus GetStatus() => new(20, 17, null, null, null, null, null, null);
    }

    private sealed class SeedEvidenceStorage : IEvidenceStorage
    {
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoredEvidence($"{eventId}/{submissionId}.png", originalFilename, "image/png", 3, 1, 1, new string('a', 64)));
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DevelopmentEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Bingo.IntegrationTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
