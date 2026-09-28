using System.Data;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.WiseOldMan;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task StatsPass3InitialUnexpectedUnrankedEndIsVisibleWithoutInventingActivity()
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f) { Override = MetricResult(f, new(10, -1, 0)) };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!; Assert.False(cache.Complete);
        var rows = cache.Rows.Where(x => x.Metric == "vorkath").ToArray(); Assert.Equal(2, rows.Length);
        Assert.All(rows, x => { Assert.Equal(MetricActivityCoverage.UnexpectedUnrankedEnd, x.LastIssue); Assert.Null(x.RecordedActivity()); Assert.Null(x.FetchedAt); });
    }

    [Fact]
    public async Task StatsPass3AdditiveMigrationRetainsExistingEhbAndDefersBasisToProvenHistory()
    {
        // Create current-model fixture rows before downgrading; later additive event columns
        // must not be inserted into the intentionally older schema under test.
        var fixture = await MetricFixtureAsync();
        await using var migration = new ApplicationDbContext(options);
        await migration.GetService<IMigrator>().MigrateAsync("20260915170124_GuardSuspiciousPriceCandidates");
        await using (var old = new ApplicationDbContext(options))
        {
            old.EventCompetitionCharacterActivities.Add(new(Guid.NewGuid(), fixture.Event.Id, 1, 42, fixture.Characters[0].Id, 15,
                fixture.Clock.GetUtcNow(), fixture.Clock.GetUtcNow().AddMinutes(-5), "original", 10, 25));
            await old.SaveChangesAsync();
        }
        await RetainedCatalogueMigrationTestSupport.PrepareAsync(migration);
        await migration.Database.MigrateAsync();
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.EventLuckOutcomeBases.ToListAsync()); Assert.Empty(await verify.EventCompetitionCharacterMetricActivities.ToListAsync());
        var existing = Assert.Single(await verify.EventCompetitionCharacterActivities.ToListAsync());
        Assert.Equal(15, existing.GainedEhb); Assert.Equal(10, existing.StartEhb); Assert.Equal(25, existing.EndEhb);
        Assert.Equal("original", existing.AssignmentFingerprint);
        await using var transaction = await verify.Database.BeginTransactionAsync();
        await verify.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {fixture.Event.Id} FOR UPDATE").SingleAsync();
        await verify.RetainLuckOutcomeBasesAsync(fixture.Event.Id, fixture.Clock.GetUtcNow()); await verify.SaveChangesAsync(); await transaction.CommitAsync();
        Assert.Equal(2, await verify.EventLuckOutcomeBases.CountAsync());
        Assert.All(await verify.EventLuckOutcomeBases.ToListAsync(), x => Assert.Equal(LuckBasisStatus.Retained, x.Status));
    }

    [Theory]
    [InlineData("conflicting-copy")]
    [InlineData("tied-approval")]
    [InlineData("missing-identity")]
    public async Task StatsPass3LegacyAmbiguityNeverGuessesTheEarliestBasis(string issue)
    {
        var f = await MetricFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        if (issue == "tied-approval")
        {
            // A separate retained approval with the same earliest time cannot establish ordering.
            f.Clock.Advance(TimeSpan.FromHours(-2)); await AddMetricApprovalAsync(db, f, false);
        }
        else if (issue == "missing-identity")
            await db.BoardApprovalRequirementBossSnapshots.Where(x => x.BossActivityId == f.Bosses[0].Id).ExecuteDeleteAsync();
        else
        {
            var old = await db.BoardApprovalRequirementDropSnapshots.SingleAsync(x => x.SourceDropId == f.Drops[0].Id);
            var tile = await db.BoardApprovalTileSnapshots.SingleAsync();
            var duplicate = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), tile.Id, Guid.NewGuid(), 2, 1, true, false, 1, "Conflict", false);
            db.AddRange(duplicate, new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), duplicate.Id, old.SourceDropId, old.ItemIdSnapshot, old.BossName, old.ItemName, "1/5", .2m, null, 1, 1, 1),
                new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), duplicate.Id, f.Bosses[0].Id, f.Bosses[0].Name, 10, 1));
        }
        await db.SaveChangesAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {f.Event.Id} FOR UPDATE").SingleAsync();
        await db.RetainLuckOutcomeBasesAsync(f.Event.Id, f.Clock.GetUtcNow()); await db.SaveChangesAsync(); await transaction.CommitAsync();
        var basis = await db.EventLuckOutcomeBases.AsNoTracking().SingleAsync(x => x.SourceDropId == f.Drops[0].Id);
        Assert.Equal(issue == "missing-identity" ? LuckBasisStatus.MissingIdentity : LuckBasisStatus.ConflictingEarliestApproval, basis.Status);
        Assert.Null(basis.Metric);
        if (issue != "missing-identity") { Assert.Null(basis.NumericProbability); Assert.Null(basis.FirstApprovalSnapshotId); }
        Assert.False((await db.LuckSourceRequestAsync(f.Event.Id)).SourcesAvailable);
    }

    [Fact]
    public async Task StatsPass3OneBatchIncludesBothRegularAccountsAcrossSwapAndExcludesInformationalAlt()
    {
        var f = await MetricFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        var client = new MetricClient(f); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f);
        Assert.True((await service.RefreshAsync(f.Event.Id, f.Actor)).Succeeded);
        Assert.Equal(["vorkath", "zulrah"], client.Requested);
        var cache = await service.ReadMetricCacheAsync(f.Event.Id);
        Assert.NotNull(cache); Assert.True(cache.Compatible); Assert.True(cache.Complete);
        Assert.Equal(4, cache.Rows.Count); Assert.Equal(60, cache.Rows.Sum(x => x.RecordedActivity()));
        Assert.DoesNotContain(cache.Rows, x => x.OsrsCharacterId == f.Characters[2].Id);
        Assert.All(cache.Rows, x => { Assert.Equal(f.Clock.GetUtcNow(), x.FetchedAt); Assert.Equal(f.Clock.GetUtcNow().AddMinutes(-5), x.UpstreamUpdatedAt); Assert.Equal(cache.ActivityBatchId, x.ActivityBatchId); });
        Assert.Equal(2, await db.EventCompetitionCharacterActivities.CountAsync(x => x.EventId == f.Event.Id));
        Assert.Equal(1, client.MetricCalls); Assert.Equal(1, client.ValidationCalls);
        Assert.Single(await db.EventParticipantCharacterSwaps.Where(x => x.EventId == f.Event.Id).ToListAsync());
    }

    [Fact]
    public async Task BossLeaderboardProjectionAndBoardRoutesUseLinkedMetricSnapshot()
    {
        var f = await MetricFixtureAsync();
        var now = f.Clock.GetUtcNow();
        var team = new Team(Guid.NewGuid(), f.Event.Id, "Metric team", "metric-team", TeamFormationType.Drafted, null, true);
        var zeroTeam = new Team(Guid.NewGuid(), f.Event.Id, "Zero team", "zero-team", TeamFormationType.Drafted, null, true);
        var emptyTeam = new Team(Guid.NewGuid(), f.Event.Id, "Empty team", "empty-team", TeamFormationType.Drafted, null, true);
        var secondParticipant = new EventParticipant(Guid.NewGuid(), f.Event.Id, SignupStatus.Confirmed, 2, now.AddHours(-3), SignupSource.Website);
        var secondCharacters = new List<string> { "Fixture Three", "Fixture Four" }
            .Select(name => new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now)).ToArray();
        var partialParticipant = new EventParticipant(Guid.NewGuid(), f.Event.Id, SignupStatus.Confirmed, 3, now.AddHours(-3), SignupSource.Website);
        var partialCharacters = new List<string> { "Fixture Zero", "Fixture Missing" }
            .Select(name => new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now)).ToArray();
        var signupForm = new SignupForm(Guid.NewGuid(), f.Event.Id, now);
        var primaryQuestion = new SignupQuestion(Guid.NewGuid(), signupForm.Id, f.Event.Id, "primary_regular_account", "Primary account", SignupQuestionType.Account, true, 0, null,
            SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        team.Finalize(now.AddHours(-2));
        zeroTeam.Finalize(now.AddHours(-2));
        emptyTeam.Finalize(now.AddHours(-2));
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(team, zeroTeam, emptyTeam, secondParticipant, partialParticipant, signupForm, primaryQuestion);
            var basePrimaryAssignment = await setup.EventParticipantCharacters
                .Where(value => value.EventId == f.Event.Id)
                .OrderBy(value => value.RegistrationOrder).FirstOrDefaultAsync();
            if (basePrimaryAssignment is not null)
                setup.Entry(basePrimaryAssignment).Property(value => value.SignupQuestionId).CurrentValue = primaryQuestion.Id;
            setup.AddRange(secondCharacters); setup.AddRange(partialCharacters);
            setup.AddRange(secondCharacters.Select((character, index) => new EventParticipantCharacter(Guid.NewGuid(), f.Event.Id, secondParticipant.Id, character.Id, index, now, null, index == 0 ? primaryQuestion.Id : null,
                EventCharacterRole.Playing, 0, EhbSource.Manual, null)));
            setup.AddRange(partialCharacters.Select((character, index) => new EventParticipantCharacter(Guid.NewGuid(), f.Event.Id, partialParticipant.Id, character.Id, index, now, null, index == 0 ? primaryQuestion.Id : null,
                EventCharacterRole.Playing, 0, EhbSource.Manual, null)));
            setup.Add(new TeamMembership(Guid.NewGuid(), team.Id, await setup.EventParticipants.Where(value => value.EventId == f.Event.Id).OrderBy(value => value.SignupSequence).Select(value => value.Id).FirstAsync(),
                TeamMembershipRole.Participant, now.AddHours(-2), null, "Boss leaderboard fixture"));
            setup.Add(new TeamMembership(Guid.NewGuid(), team.Id, secondParticipant.Id,
                TeamMembershipRole.Participant, now.AddHours(-2), null, "Boss leaderboard fixture"));
            setup.Add(new TeamMembership(Guid.NewGuid(), zeroTeam.Id, partialParticipant.Id,
                TeamMembershipRole.Participant, now.AddHours(-2), null, "Boss leaderboard fixture"));
            var firstParticipantId = await setup.EventParticipants.Where(value => value.EventId == f.Event.Id)
                .OrderBy(value => value.SignupSequence).Select(value => value.Id).FirstAsync();
            var draft = new DraftSession(Guid.NewGuid(), f.Event.Id, 1);
            draft.FinalizeDirect(now.AddHours(-2));
            var draftPublication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddHours(-2), f.Admin.Id, DraftPublicationMethod.DirectRoster);
            setup.AddRange(draft, draftPublication,
                new DraftPublicationRoster(Guid.NewGuid(), draftPublication.Id, team.Id, firstParticipantId, TeamMembershipRole.Participant, null, f.Characters[0].DisplayName),
                new DraftPublicationRoster(Guid.NewGuid(), draftPublication.Id, team.Id, secondParticipant.Id, TeamMembershipRole.Participant, null, secondCharacters[0].DisplayName),
                new DraftPublicationRoster(Guid.NewGuid(), draftPublication.Id, zeroTeam.Id, partialParticipant.Id, TeamMembershipRole.Participant, null, partialCharacters[0].DisplayName));
            await setup.SaveChangesAsync();
        }
        f.AdditionalCharacters.AddRange(secondCharacters); f.AdditionalCharacters.AddRange(partialCharacters);

        MetricPublication publication;
        await using (var correction = new ApplicationDbContext(options))
            publication = await AddRepeatedMetricPlacementAsync(correction, f);

        var metricClient = new MetricClient(f) { Override = MetricResult(f, new(10, 25, 15), new(-1, 47, 47)) };
        await using (var syncDb = new ApplicationDbContext(options))
        {
            var service = MetricService(syncDb, metricClient, f);
            await ConfigureMetricAsync(service, f);
            Assert.True((await service.RefreshAsync(f.Event.Id, f.Actor)).Succeeded);
        }

        Guid participantId;
        Guid characterId;
        Guid requirementId;
        Guid tileId;
        Guid dropId;
        await using (var evidence = new ApplicationDbContext(options))
        {
            participantId = await evidence.EventParticipants.Where(value => value.EventId == f.Event.Id).OrderBy(value => value.SignupSequence).Select(value => value.Id).FirstAsync();
            characterId = f.Characters[0].Id;
            requirementId = publication.FirstRequirementId;
            tileId = publication.FirstTileId;
            dropId = publication.FirstDropId;
            AddApprovedEvidence(evidence, f, team, participantId, characterId, tileId, requirementId, dropId, now);
            AddApprovedEvidence(evidence, f, team, participantId, characterId, publication.SecondTileId, publication.SecondRequirementId, publication.SecondDropId, now.AddMinutes(1));

            var secondParticipantId = await evidence.EventParticipants.Where(value => value.EventId == f.Event.Id).OrderBy(value => value.SignupSequence).Select(value => value.Id).Skip(1).FirstAsync();
            AddApprovedEvidence(evidence, f, team, secondParticipantId, secondCharacters[0].Id, tileId, requirementId, dropId, now.AddMinutes(2));
            AddApprovedEvidence(evidence, f, team, secondParticipantId, secondCharacters[0].Id, publication.SecondTileId, publication.SecondRequirementId, publication.SecondDropId, now.AddMinutes(3));
            AddApprovedEvidence(evidence, f, team, secondParticipantId, secondCharacters[0].Id, tileId, publication.AlternateRequirementId, publication.AlternateDropId, now.AddMinutes(3).AddSeconds(1));

            var reversed = AddApprovedEvidence(evidence, f, team, participantId, characterId, tileId, requirementId, dropId, now.AddMinutes(4));
            reversed.Submission.Reverse("Duplicate fixture evidence", now.AddMinutes(5));
            reversed.Contribution.Reverse(now.AddMinutes(5));
            AddApprovedEvidence(evidence, f, team, participantId, characterId, tileId, publication.AlternateRequirementId, publication.AlternateDropId, now.AddMinutes(6));
            await evidence.SaveChangesAsync();
        }

        await using (var projectionDb = new ApplicationDbContext(options))
        {
            var projection = new CachedEventCompetitionActivityProjection(projectionDb, f.Clock);
            var vorkath = await projection.GetMetricLeaderboardAsync(f.Event.Id, "vorkath");
            Assert.True(vorkath.IsBossMode);
            Assert.Equal(["vorkath", "zulrah"], vorkath.Options.Select(value => value.Metric));
            Assert.True(vorkath.Compatible);
            Assert.True(vorkath.Complete);
            Assert.False(vorkath.Stale);
            Assert.Equal(3, vorkath.Teams.Count);
            var projectedTeam = Assert.Single(vorkath.Teams, value => value.TeamId == team.Id);
            Assert.Equal(team.Id, projectedTeam.TeamId);
            Assert.Equal(60m, projectedTeam.TotalGained);
            Assert.Equal(30m, projectedTeam.AverageGained);
            Assert.Equal(2, projectedTeam.ContributingParticipantCount);
            Assert.Equal(2, projectedTeam.MvpNames.Count);
            Assert.Equal(30m, projectedTeam.MvpValue);
            var projectedParticipant = Assert.Single(projectedTeam.Participants, value => value.ParticipantId == participantId);
            Assert.Equal(2, projectedParticipant.DropCount);
            Assert.Equal([15m, 15m], projectedParticipant.Accounts.Select(value => value.RecordedActivity).ToArray());
            Assert.Equal([10m, 10m], projectedParticipant.Accounts.Select(value => value.Start).ToArray());
            Assert.Equal([25m, 25m], projectedParticipant.Accounts.Select(value => value.End).ToArray());
            var sourceRequest = await projectionDb.LuckSourceRequestAsync(f.Event.Id);
            Assert.True(sourceRequest.SourcesAvailable);
            Assert.Equal("vorkath", sourceRequest.Outcomes.Single(value => value.SourceDropId == f.Drops[0].Id).Basis!.Metric);
            Assert.Equal("zulrah", sourceRequest.Outcomes.Single(value => value.SourceDropId == f.Drops[1].Id).Basis!.Metric);
            var zero = Assert.Single(vorkath.Teams, value => value.TeamId == zeroTeam.Id);
            Assert.Equal(0m, zero.TotalGained);
            Assert.Empty(zero.MvpNames);
            Assert.Empty(zero.Participants);
            Assert.Equal(2, zero.Rank);
            var empty = Assert.Single(vorkath.Teams, value => value.TeamId == emptyTeam.Id);
            Assert.Equal(2, empty.Rank);
            var zulrah = await projection.GetMetricLeaderboardAsync(f.Event.Id, "zulrah");
            var alternateTeam = Assert.Single(zulrah.Teams, value => value.TeamId == team.Id);
            Assert.Equal(188m, alternateTeam.TotalGained);
            Assert.Equal([47m, 47m], alternateTeam.Participants.Single(value => value.ParticipantId == participantId).Accounts.Select(value => value.RecordedActivity).ToArray());
            Assert.All(alternateTeam.Participants.SelectMany(value => value.Accounts), value => Assert.Equal(MetricActivityAvailability.Estimated, value.Availability));
            f.Clock.Advance(TimeSpan.FromHours(1));
            var stale = await projection.GetMetricLeaderboardAsync(f.Event.Id, "vorkath");
            Assert.True(stale.Stale);
            Assert.Equal(60m, stale.Teams.Single(value => value.TeamId == team.Id).TotalGained);
            var staleCache = await new EventCompetitionSynchronizationService(projectionDb, metricClient, new FixedStatus(), f.Clock)
                .ReadMetricCacheAsync(f.Event.Id);
            Assert.True(staleCache!.Stale);
            await using (var retain = new ApplicationDbContext(options))
            {
                var row = await retain.EventCompetitionCharacterMetricActivities.SingleAsync(value => value.EventId == f.Event.Id && value.Metric == "vorkath" && value.OsrsCharacterId == f.Characters[0].Id);
                row.RecordFailure(f.Clock.GetUtcNow());
                await retain.SaveChangesAsync();
            }
            var retained = await projection.GetMetricLeaderboardAsync(f.Event.Id, "vorkath");
            Assert.Equal(EventCompetitionActivityState.Partial, retained.State);
            Assert.Equal(60m, retained.Teams.Single(value => value.TeamId == team.Id).TotalGained);
            Assert.Contains(retained.Teams.Single(value => value.TeamId == team.Id).Participants, value => value.Incomplete);
            Assert.Equal(MetricActivityAvailability.RetainedWithIssue, retained.Teams.Single(value => value.TeamId == team.Id).Participants.SelectMany(value => value.Accounts).Single(value => value.CharacterId == f.Characters[0].Id).Availability);
            Assert.Null((await projection.GetMetricLeaderboardAsync(f.Event.Id, "unlinked_metric")).Selected);
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .UseSetting("WiseOldMan:DevelopmentFake:AutomaticSynchronizationEnabled", "false")
            .ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(f.Clock);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var initialDefaultView = await client.GetStringAsync("/Events/metric-event/Board?view=leaderboards&ranking=activity");
        Assert.Contains("data-public-leaderboard-view=\"activity\"", initialDefaultView, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-view=\"drops\"", initialDefaultView, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-view=\"players\"", initialDefaultView, StringComparison.Ordinal);
        Assert.Contains("Multiple MVPs", initialDefaultView, StringComparison.Ordinal);
        Assert.Contains("&#x2B;10.00", initialDefaultView, StringComparison.Ordinal);
        Assert.Contains("&#x2B;2.00", initialDefaultView, StringComparison.Ordinal);
        Assert.DoesNotContain("&#x2B;60.00", initialDefaultView, StringComparison.Ordinal);

        await using (var lifecycleDb = new ApplicationDbContext(options))
        {
            var eventItem = await lifecycleDb.Events.SingleAsync(value => value.Id == f.Event.Id);
            eventItem.EndEvent(f.Clock.GetUtcNow());
            await lifecycleDb.SaveChangesAsync();
            eventItem.FinalizeResults(f.Clock.GetUtcNow().AddMinutes(1));
            await lifecycleDb.SaveChangesAsync();
        }
        using (var finalized = await client.GetAsync("/Events/metric-event/Board?view=leaderboards&metric=vorkath&ranking=teams"))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, finalized.StatusCode);
            var finalizedHtml = await finalized.Content.ReadAsStringAsync();
            Assert.Contains("data-public-leaderboard-panel=\"metric-teams\"", finalizedHtml, StringComparison.Ordinal);
            Assert.Contains("data-sort-total-gained=\"60\"", finalizedHtml, StringComparison.Ordinal);
            Assert.Contains("data-sort-gained=\"30\"", finalizedHtml, StringComparison.Ordinal);
            Assert.Contains("&#x2B;60", finalizedHtml, StringComparison.Ordinal);
        }
        await using (var archiveDb = new ApplicationDbContext(options))
        {
            var eventItem = await archiveDb.Events.SingleAsync(value => value.Id == f.Event.Id);
            eventItem.Archive(f.Clock.GetUtcNow().AddMinutes(2));
            await archiveDb.SaveChangesAsync();
        }
        using (var archived = await client.GetAsync("/Events/metric-event/Board?view=leaderboards&metric=vorkath&ranking=teams"))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, archived.StatusCode);
            var archivedHtml = await archived.Content.ReadAsStringAsync();
            Assert.Contains("data-public-leaderboard-panel=\"metric-teams\"", archivedHtml, StringComparison.Ordinal);
            Assert.Contains("data-sort-total-gained=\"60\"", archivedHtml, StringComparison.Ordinal);
            Assert.Contains("data-sort-gained=\"30\"", archivedHtml, StringComparison.Ordinal);
            Assert.Contains("&#x2B;60", archivedHtml, StringComparison.Ordinal);
        }
        using var danishRequest = new HttpRequestMessage(HttpMethod.Get, "/Events/metric-event/Board?view=leaderboards&metric=vorkath&ranking=teams");
        danishRequest.Headers.Add("Accept-Language", "da");
        using var danishResponse = await client.SendAsync(danishRequest);
        var danishView = await danishResponse.Content.ReadAsStringAsync();
        Assert.Contains(">Rank<", danishView, StringComparison.Ordinal);
        Assert.DoesNotContain(">Placering<", danishView, StringComparison.Ordinal);
        Assert.Contains(">Opn&#xE5;et<", danishView, StringComparison.Ordinal);
        Assert.Contains(">Start<", danishView, StringComparison.Ordinal);
        Assert.Contains(">Slut<", danishView, StringComparison.Ordinal);

        await using (var partial = new ApplicationDbContext(options))
        {
            await partial.EventCompetitionCharacterMetricActivities
                .Where(value => value.EventId == f.Event.Id && value.Metric == "vorkath" &&
                                (value.OsrsCharacterId == f.Characters[1].Id || value.OsrsCharacterId == partialCharacters[1].Id))
                .ExecuteDeleteAsync();
        }
        await using (var partialProjectionDb = new ApplicationDbContext(options))
        {
            var partialProjection = new CachedEventCompetitionActivityProjection(partialProjectionDb, f.Clock);
            var partialLeaderboard = await partialProjection.GetMetricLeaderboardAsync(f.Event.Id, "vorkath");
            Assert.Equal(EventCompetitionActivityState.Partial, partialLeaderboard.State);
            var partialTeam = partialLeaderboard.Teams.Single(value => value.TeamId == team.Id);
            Assert.Equal(45m, partialTeam.TotalGained);
            Assert.Single(partialTeam.MvpNames);
            Assert.Equal("Fixture Three", partialTeam.MvpNames[0]);
            Assert.Equal(30m, partialTeam.MvpValue);
            var partialPlayer = partialTeam.Participants.Single(value => value.ParticipantId == participantId);
            Assert.Equal(15m, partialPlayer.TotalGained);
            Assert.True(partialPlayer.Incomplete);
            Assert.Equal(MetricActivityAvailability.RetainedWithIssue, partialPlayer.Accounts.Single(value => value.CharacterId == f.Characters[0].Id).Availability);
            var missingAccount = partialPlayer.Accounts.Single(value => value.CharacterId == f.Characters[1].Id);
            Assert.Equal(MetricActivityAvailability.WaitingForActivityData, missingAccount.Availability);
            Assert.Null(missingAccount.Gained); Assert.Null(missingAccount.Start); Assert.Null(missingAccount.End);
            var zeroTeamProjection = partialLeaderboard.Teams.Single(value => value.TeamId == zeroTeam.Id);
            Assert.Equal(0m, zeroTeamProjection.TotalGained); Assert.Empty(zeroTeamProjection.Participants); Assert.Empty(zeroTeamProjection.MvpNames);
            var zeroRow = await partialProjectionDb.EventCompetitionCharacterMetricActivities.AsNoTracking()
                .SingleAsync(value => value.EventId == f.Event.Id && value.Metric == "vorkath" && value.OsrsCharacterId == partialCharacters[0].Id);
            Assert.Equal(MetricActivityCoverage.ZeroRecorded, zeroRow.Coverage);
            Assert.Equal(0m, zeroRow.RecordedActivity());
            Assert.False(await partialProjectionDb.EventCompetitionCharacterMetricActivities.AnyAsync(value => value.EventId == f.Event.Id && value.Metric == "vorkath" && value.OsrsCharacterId == partialCharacters[1].Id));
        }

        var selected = await client.GetStringAsync("/Events/metric-event/Board?view=leaderboards&metric=vorkath&ranking=teams");
        Assert.Contains("data-public-leaderboard-mode=\"metric\"", selected, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-view=\"teams\"", selected, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-view=\"players\"", selected, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-panel=\"metric-teams\"", selected, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-panel=\"metric-players\"", selected, StringComparison.Ordinal);
        Assert.Contains("METRIC: Vorkath", selected, StringComparison.Ordinal);
        Assert.Contains("dropSearch=Vorkath", selected, StringComparison.Ordinal);
        Assert.Contains("dropTeam=metric-team", selected, StringComparison.Ordinal);
        Assert.Contains("Metric team", selected, StringComparison.Ordinal);
        var selectedPlayers = await client.GetStringAsync("/Events/metric-event/Board?view=leaderboards&metric=vorkath&ranking=players");
        Assert.Contains("data-public-leaderboard-panel=\"metric-players\"", selectedPlayers, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-sort-key=\"gained\"", selectedPlayers, StringComparison.Ordinal);
        Assert.Contains("data-sort-start=\"\" data-sort-end=\"\"", selectedPlayers, StringComparison.Ordinal);
        Assert.Contains("&#x2B;15", selectedPlayers, StringComparison.Ordinal);
        Assert.DoesNotContain(">Retained<", selectedPlayers, StringComparison.Ordinal);
        Assert.DoesNotContain(">Waiting<", selectedPlayers, StringComparison.Ordinal);
        Assert.Contains("data-sort-player=\"Fixture One\"", selectedPlayers, StringComparison.Ordinal);
        Assert.Contains("data-sort-gained=\"15\"", selectedPlayers, StringComparison.Ordinal);
        var defaultView = await client.GetStringAsync("/Events/metric-event/Board?view=leaderboards&ranking=activity");
        Assert.Contains("data-public-leaderboard-view=\"activity\"", defaultView, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-view=\"drops\"", defaultView, StringComparison.Ordinal);
        Assert.Contains("data-public-leaderboard-view=\"players\"", defaultView, StringComparison.Ordinal);
        Assert.DoesNotContain("data-public-leaderboard-panel=\"metric-teams\"", defaultView, StringComparison.Ordinal);
        var invalid = await client.GetStringAsync("/Events/metric-event/Board?view=leaderboards&metric=unlinked_metric&ranking=teams");
        Assert.Contains("data-public-leaderboard-view=\"activity\"", invalid, StringComparison.Ordinal);
        Assert.DoesNotContain("data-public-leaderboard-panel=\"metric-teams\"", invalid, StringComparison.Ordinal);

        await using (var incompatibleDb = new ApplicationDbContext(options))
        {
            (await incompatibleDb.EventParticipantCharacters.SingleAsync(value => value.OsrsCharacterId == f.Characters[1].Id)).Release(f.Admin.Id, f.Clock.GetUtcNow());
            await incompatibleDb.SaveChangesAsync();
        }
        await using (var projectionDb = new ApplicationDbContext(options))
        {
            var projection = new CachedEventCompetitionActivityProjection(projectionDb, f.Clock);
            var incompatible = await projection.GetMetricLeaderboardAsync(f.Event.Id, "vorkath");
            Assert.False(incompatible.Compatible);
            Assert.Empty(incompatible.Teams.Single(value => value.TeamId == team.Id).Participants);
            Assert.Equal(0m, incompatible.Teams.Single(value => value.TeamId == team.Id).TotalGained);
        }
        await using (var unlinkedDb = new ApplicationDbContext(options))
        {
            var state = await unlinkedDb.EventCompetitionSynchronizations.SingleAsync(value => value.EventId == f.Event.Id);
            state.Reconfigure(null, null, null, null, state.AssignmentFingerprint, f.Clock.GetUtcNow().AddMinutes(3));
            await unlinkedDb.SaveChangesAsync();
        }
        await using (var unlinkedProjectionDb = new ApplicationDbContext(options))
        {
            var unlinkedProjection = new CachedEventCompetitionActivityProjection(unlinkedProjectionDb, f.Clock);
            var unlinkedLeaderboard = await unlinkedProjection.GetMetricLeaderboardAsync(f.Event.Id, "vorkath");
            Assert.False(unlinkedLeaderboard.IsBossMode);
            Assert.Empty(unlinkedLeaderboard.Options);
            Assert.Null(unlinkedLeaderboard.Selected);
        }
        using (var unlinked = await client.GetAsync("/Events/metric-event/Board?view=leaderboards&metric=vorkath&ranking=teams"))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, unlinked.StatusCode);
            var unlinkedHtml = await unlinked.Content.ReadAsStringAsync();
            Assert.Contains("data-public-leaderboards-mode=\"default\"", unlinkedHtml, StringComparison.Ordinal);
            Assert.Contains("data-public-leaderboard-view=\"activity\"", unlinkedHtml, StringComparison.Ordinal);
            Assert.Contains("data-public-leaderboard-view=\"drops\"", unlinkedHtml, StringComparison.Ordinal);
            Assert.Contains("data-public-leaderboard-view=\"players\"", unlinkedHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("data-public-leaderboard-panel=\"metric-teams\"", unlinkedHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("data-public-leaderboard-panel=\"metric-players\"", unlinkedHtml, StringComparison.Ordinal);
        }
        await using (var unpublishedDb = new ApplicationDbContext(options))
        {
            var board = await unpublishedDb.Boards.SingleAsync(value => value.Id == f.BoardId);
            unpublishedDb.Entry(board).Property(value => value.State).CurrentValue = BoardState.Draft;
            unpublishedDb.Entry(board).Property(value => value.ActiveApprovalSnapshotId).CurrentValue = null;
            await unpublishedDb.SaveChangesAsync();
        }
        using (var unpublished = await client.GetAsync("/Events/metric-event/Board?view=leaderboards&ranking=activity"))
            Assert.Equal(System.Net.HttpStatusCode.NotFound, unpublished.StatusCode);
        await using (var hiddenDb = new ApplicationDbContext(options))
        {
            var eventItem = await hiddenDb.Events.SingleAsync(value => value.Id == f.Event.Id);
            eventItem.Hide(f.Admin.Id, f.Clock.GetUtcNow().AddMinutes(4), f.Event.Name, "Boss leaderboard hidden fixture");
            await hiddenDb.SaveChangesAsync();
        }
        using (var hidden = await client.GetAsync("/Events/metric-event/Board?view=leaderboards&ranking=activity"))
            Assert.Equal(System.Net.HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(1, metricClient.MetricCalls);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unranked-end")]
    [InlineData("oversized")]
    [InlineData("failure")]
    public async Task StatsPass3PartialAndFailedBatchesRetainRawOriginWithoutClaimingCompleteness(string scenario)
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f);
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        var first = (await service.ReadMetricCacheAsync(f.Event.Id))!; var before = first.Rows.Single(x => x.OsrsCharacterId == f.Characters[0].Id && x.Metric == "vorkath");
        f.Clock.Advance(TimeSpan.FromHours(2));
        client.Override = scenario == "failure" ? new(WiseOldManCompetitionStatus.Unavailable) : MetricResult(f, scenario == "missing" ? null : scenario == "oversized" ? new(10, 100000000000000000000m, 15) : new(20, -1, 0));
        await service.RefreshAsync(f.Event.Id, f.Actor);
        var after = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.True(after.Compatible); Assert.False(after.Complete);
        var retained = after.Rows.Single(x => x.OsrsCharacterId == before.OsrsCharacterId && x.Metric == before.Metric);
        Assert.Equal(before.Start, retained.Start); Assert.Equal(before.End, retained.End); Assert.Equal(before.Gained, retained.Gained);
        Assert.Equal(before.ActivityBatchId, retained.ActivityBatchId); Assert.Equal(before.FetchedAt, retained.FetchedAt); Assert.Equal(before.UpstreamUpdatedAt, retained.UpstreamUpdatedAt);
        Assert.NotNull(retained.LastIssue); Assert.Equal(f.Clock.GetUtcNow(), retained.LastAttemptAt);
        var state = await db.EventCompetitionSynchronizations.AsNoTracking().SingleAsync(x => x.EventId == f.Event.Id);
        if (scenario != "failure") { Assert.True(state.LatestComplete); Assert.Equal(f.Clock.GetUtcNow(), state.LastSuccessfulAt); }
        else Assert.Equal(before.FetchedAt, state.LastSuccessfulAt);
    }

    [Theory]
    [InlineData(-1, -1, 0, MetricActivityCoverage.ZeroRecorded, MetricActivityAvailability.WaitingForActivityData)]
    [InlineData(-1, 35, 34, MetricActivityCoverage.EstimatedBaseline, MetricActivityAvailability.Estimated)]
    [InlineData(20, 35, 12, MetricActivityCoverage.Ranked, MetricActivityAvailability.Available)]
    [InlineData(20, 20, 0, MetricActivityCoverage.Ranked, MetricActivityAvailability.WaitingForActivityUpdate)]
    public async Task StatsPass3PostgresPreservesEachAgreedCountCase(int start, int end, int gain, MetricActivityCoverage coverage, MetricActivityAvailability withDrop)
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f) { Override = MetricResult(f, new(start, end, gain)) };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.True(cache.Complete);
        var row = cache.Rows.First(x => x.Metric == "vorkath");
        Assert.Equal(coverage, row.Coverage); Assert.Equal(withDrop, row.Availability(true));
        Assert.Equal((decimal)start, row.Start); Assert.Equal((decimal)end, row.End); Assert.Equal((decimal)gain, row.Gained);
        if (start == -1 && end == -1) Assert.Equal(MetricActivityAvailability.NoRecordedActivity, row.Availability(false));
    }

    [Theory]
    [InlineData("lease")]
    [InlineData("expired")]
    [InlineData("generation")]
    [InlineData("competition")]
    [InlineData("assignment")]
    public async Task StatsPass3RejectsAnInFlightResponseAfterItsFenceChanges(string mutation)
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f);
        client.BeforeReturn = async () =>
        {
            await using var change = new ApplicationDbContext(options);
            var state = await change.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == f.Event.Id);
            if (mutation == "lease") state.AcquireLease("other-worker", f.Clock.GetUtcNow().AddMinutes(2));
            else if (mutation == "expired") f.Clock.Advance(TimeSpan.FromMinutes(3));
            else if (mutation == "generation") state.BeginReplacementGeneration(state.AssignmentFingerprint, f.Clock.GetUtcNow());
            else if (mutation == "competition") state.Reconfigure(43, "Replacement", f.Event.EventStartsAt, f.Event.EventEndsAt, state.AssignmentFingerprint, f.Clock.GetUtcNow());
            else
            {
                var replacement = new OsrsCharacter(Guid.NewGuid(), "Replacement", "REPLACEMENT", f.Clock.GetUtcNow()); change.Add(replacement);
                (await change.EventParticipantCharacters.SingleAsync(x => x.OsrsCharacterId == f.Characters[0].Id)).ReplaceCharacter(replacement.Id);
            }
            await change.SaveChangesAsync();
        };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        Assert.Empty(await db.EventCompetitionCharacterMetricActivities.AsNoTracking().ToListAsync());
        Assert.Empty(await db.EventCompetitionCharacterActivities.AsNoTracking().ToListAsync());
        Assert.False((await service.ReadMetricCacheAsync(f.Event.Id))!.Complete);
    }

    [Theory]
    [InlineData("approval")]
    [InlineData("objective")]
    [InlineData("mapping")]
    [InlineData("mapping-binding")]
    public async Task StatsPass3SourceChangeDuringFetchCannotSatisfyNewBoardButKeepsEhb(string mutation)
    {
        var f = await MetricFixtureAsync(mapped: mutation is not ("mapping" or "mapping-binding")); var client = new MetricClient(f);
        client.BeforeReturn = async () =>
        {
            await using var change = new ApplicationDbContext(options);
            await using var transaction = await change.Database.BeginTransactionAsync();
            await change.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {f.Event.Id} FOR UPDATE").SingleAsync();
            if (mutation is "mapping" or "mapping-binding")
            {
                var boss = await change.BossActivities.SingleAsync(x => x.Id == f.Bosses[0].Id);
                boss.ConfigureApi("vorkath"); boss.RecordMapping(ApiMappingStatus.Verified, f.Clock.GetUtcNow()); boss.AdvanceVersion();
            }
            else await AddMetricApprovalAsync(change, f, mutation == "objective");
            await change.SaveChangesAsync();
            if (mutation == "mapping-binding")
            {
                await change.RetainLuckOutcomeBasesAsync(f.Event.Id, f.Clock.GetUtcNow());
                await change.SaveChangesAsync();
            }
            await transaction.CommitAsync();
        };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.False(cache.Complete); Assert.Empty(cache.Rows);
        Assert.Equal(2, await db.EventCompetitionCharacterActivities.CountAsync());
        Assert.True((await db.EventCompetitionSynchronizations.AsNoTracking().SingleAsync()).LatestComplete);
        if (mutation == "mapping") Assert.Equal(2, (await db.EventLuckOutcomeBases.AsNoTracking().SingleAsync(x => x.SourceDropId == f.Drops[0].Id)).SourceRevision);
        client.BeforeReturn = null; f.Clock.Advance(TimeSpan.FromHours(2));
        await service.RefreshAsync(f.Event.Id, f.Actor);
        Assert.True((await service.ReadMetricCacheAsync(f.Event.Id))!.Complete);
    }

    [Theory]
    [InlineData("board")]
    [InlineData("assignment")]
    public async Task StatsPass3ReadRechecksCurrentSourceAndAssignmentFingerprint(string change)
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f);
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        await using (var mutate = new ApplicationDbContext(options))
        {
            if (change == "board") await AddMetricApprovalAsync(mutate, f, false);
            else (await mutate.EventParticipantCharacters.SingleAsync(x => x.OsrsCharacterId == f.Characters[0].Id)).Release(f.Admin.Id, f.Clock.GetUtcNow());
            await mutate.SaveChangesAsync();
        }
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.False(cache.Compatible); Assert.False(cache.Complete); Assert.Empty(cache.Rows);
        Assert.Equal(1, client.MetricCalls);
    }

    [Fact]
    public void StatsPass3MetricContractKeepsMissingAndUnsupportedMappingsUnavailable()
    {
        Assert.Equal("Unmapped", CompetitionMetricContract.UnavailableReason(null));
        Assert.Equal("UnverifiedMetric", CompetitionMetricContract.UnavailableReason("unsupported_fixture_metric"));
    }

    [Theory]
    [InlineData("chambers_of_xeric", "chambers_of_xeric_challenge_mode")]
    [InlineData("theatre_of_blood", "theatre_of_blood_hard_mode")]
    [InlineData("tombs_of_amascut", "tombs_of_amascut_expert")]
    [InlineData("the_gauntlet", "the_corrupted_gauntlet")]
    [InlineData("nightmare", "phosanis_nightmare")]
    public async Task StatsPass3VerifiedModePairsRetainIndependentUsableActivity(string normalMetric, string alternateMetric)
    {
        var f = await MetricFixtureAsync(firstMetric: normalMetric, secondMetric: alternateMetric);
        var client = new MetricClient(f) { Override = MetricResult(f, new(100, 113, 13), new(40, 47, 7)) };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f);
        Assert.True((await service.RefreshAsync(f.Event.Id, f.Actor)).Succeeded);
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.True(cache.Compatible); Assert.True(cache.Complete); Assert.True(cache.SuccessfulBatch); Assert.False(cache.Stale);
        Assert.Equal(new[] { normalMetric, alternateMetric }.Order(StringComparer.Ordinal), client.Requested);
        Assert.True(cache.Sources.SourcesAvailable); Assert.Equal(2, cache.Sources.Outcomes.Count);
        Assert.All(cache.Sources.Outcomes, x => Assert.Null(x.UnavailableReason));
        Assert.Equal(normalMetric, cache.Sources.Outcomes.Single(x => x.SourceDropId == f.Drops[0].Id).Basis!.Metric);
        Assert.Equal(alternateMetric, cache.Sources.Outcomes.Single(x => x.SourceDropId == f.Drops[1].Id).Basis!.Metric);
        Assert.Equal(4, cache.Rows.Count);
        foreach (var character in f.Characters.Take(2))
        {
            var normal = Assert.Single(cache.Rows, x => x.OsrsCharacterId == character.Id && x.Metric == normalMetric);
            Assert.Equal(100m, normal.Start); Assert.Equal(113m, normal.End); Assert.Equal(13m, normal.Gained); Assert.Equal(13m, normal.RecordedActivity());
            var alternate = Assert.Single(cache.Rows, x => x.OsrsCharacterId == character.Id && x.Metric == alternateMetric);
            Assert.Equal(40m, alternate.Start); Assert.Equal(47m, alternate.End); Assert.Equal(7m, alternate.Gained); Assert.Equal(7m, alternate.RecordedActivity());
        }
        Assert.All(cache.Rows, x =>
        {
            Assert.Equal(MetricActivityCoverage.Ranked, x.Coverage);
            Assert.Equal(MetricActivityAvailability.Available, x.Availability(true));
            Assert.Equal(cache.ActivityBatchId, x.ActivityBatchId); Assert.Null(x.LastIssue);
        });
    }

    private async Task<MetricFixture> MetricFixtureAsync(bool mapped = true, string firstMetric = "vorkath", string secondMetric = "zulrah")
    {
        var clock = new TestClock(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)); var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "MetricAdmin", "METRICADMIN", now); admin.SetGlobalRole(GlobalRole.Admin);
        var ev = new BingoEvent(Guid.NewGuid(), "Synthetic metric event", "metric-event", "", "UTC", now.AddHours(-3), now.AddHours(-2), now.AddHours(-1), now.AddHours(6), now.AddHours(6), 10, admin.Id, now);
        ev.OpenSignups(now.AddHours(-3)); ev.CloseSignups(now.AddHours(-2)); ev.SetDraftRosterPublication(true); ev.StartEvent(now.AddHours(-1));
        var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 1, now.AddHours(-3), SignupSource.Website);
        var names = new[] { "Fixture One", "Fixture Two", "Fixture Alt" };
        var characters = names.Select(name => new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now)).ToArray();
        var assignments = characters.Select((character, index) => new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, index, now, null, null,
            index == 2 ? EventCharacterRole.Informational : EventCharacterRole.Playing, index == 2 ? null : 0, index == 2 ? null : EhbSource.Manual, null)).ToArray();
        var bosses = new[] { firstMetric, secondMetric }.Select((metric, i) =>
        {
            var displayName = metric.Length == 0 ? metric : char.ToUpperInvariant(metric[0]) + metric[1..];
            var boss = new BossActivity(Guid.NewGuid(), displayName, "fixture-" + metric.Replace('_', '-'), "Boss", 10, now);
            if (mapped || i != 0) { boss.ConfigureApi(metric); boss.RecordMapping(ApiMappingStatus.Verified, now); }
            return boss;
        }).ToArray();
        var items = new[] { new CatalogueItem(Guid.NewGuid(), "Fixture drop one", "FIXTURE DROP ONE"), new CatalogueItem(Guid.NewGuid(), "Fixture drop two", "FIXTURE DROP TWO") };
        var drops = items.Select((item, index) => new SourceDrop(Guid.NewGuid(), bosses[index].Id, item.Id, "1/10", .1m, 1, now)).ToArray();
        var board = new Board(Guid.NewGuid(), ev.Id, "Metric board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Metric tile", "", "", 1);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 2, true, false, "Synthetic drop objective", false);
        var workingDrops = drops.Select((drop, index) => new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop.Id, items[index].Id, bosses[index].Name, items[index].Name, "1/10", .1m, null, 1)).ToArray();
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, ev, participant, board, tile, requirement); db.AddRange(characters); db.AddRange(assignments); db.AddRange(bosses); db.AddRange(items); db.AddRange(drops); db.AddRange(workingDrops);
        db.Add(new EventParticipantCharacterSwap(Guid.NewGuid(), ev.Id, participant.Id, characters[0].Id, characters[1].Id, now.AddMinutes(-10), now, admin.Id, "Synthetic active account swap"));
        await BoardApprovalFixture.PublishAsync(db, board, now.AddHours(-2), [tile], [requirement], workingDrops);
        var approvedRequirement = await db.BoardApprovalRequirementSnapshots.SingleAsync();
        db.AddRange(bosses.Select(boss => new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), approvedRequirement.Id, boss.Id, boss.Name, 10, boss.Version)));
        await db.SaveChangesAsync();
        return new(ev, admin, characters, bosses, drops, board.Id, clock);
    }

    private static async Task AddMetricApprovalAsync(ApplicationDbContext db, MetricFixture f, bool newObjective)
    {
        var board = await db.Boards.SingleAsync(x => x.Id == f.BoardId);
        var previous = await db.BoardApprovalSnapshots.SingleAsync(x => x.Id == board.ActiveApprovalSnapshotId);
        var priorTile = await db.BoardApprovalTileSnapshots.SingleAsync(x => x.ApprovalSnapshotId == previous.Id);
        var oldRequirements = await db.BoardApprovalRequirementSnapshots.Where(x => x.ApprovalTileSnapshotId == priorTile.Id).ToListAsync();
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, previous.Version + 1, f.Clock.GetUtcNow(), f.Admin.Id, previous.Id, board.Name, 1, 1, 1, 1, board.Version, BoardState.Published);
        var tile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, priorTile.BoardTileId, priorTile.TileTemplateId, 0, 0, "Correction", "", "", 1, null);
        db.AddRange(approval, tile);
        foreach (var old in oldRequirements)
        {
            var requirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), tile.Id, old.BoardRequirementSnapshotId, old.Position, 2, true, false, 1, "Correction", false); db.Add(requirement);
            var drops = await db.BoardApprovalRequirementDropSnapshots.Where(x => x.ApprovalRequirementSnapshotId == old.Id).ToListAsync();
            foreach (var drop in drops) db.Add(new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop.SourceDropId, drop.ItemIdSnapshot, drop.BossName, drop.ItemName, "1/5", .2m, null, 1, 1, 2));
            foreach (var boss in f.Bosses) db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, boss.Id, boss.Name, 10, boss.Version));
        }
        if (newObjective)
        {
            var requirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), tile.Id, Guid.NewGuid(), 2, 1, true, false, 1, "New outcome", false);
            var item = new CatalogueItem(Guid.NewGuid(), "New fixture outcome", "NEW FIXTURE OUTCOME");
            var source = new SourceDrop(Guid.NewGuid(), f.Bosses[0].Id, item.Id, "1/20", .05m, 1, f.Clock.GetUtcNow());
            db.AddRange(requirement, item, source, new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, source.Id, item.Id, f.Bosses[0].Name, item.Name, "1/20", .05m, null, 1, 1, 1),
                new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, f.Bosses[0].Id, f.Bosses[0].Name, 10, 1));
        }
        board.ReplacePublishedApproval(approval.Id);
    }

    private static async Task<MetricPublication> AddRepeatedMetricPlacementAsync(ApplicationDbContext db, MetricFixture f)
    {
        var board = await db.Boards.SingleAsync(value => value.Id == f.BoardId);
        var previous = await db.BoardApprovalSnapshots.SingleAsync(value => value.Id == board.ActiveApprovalSnapshotId);
        var priorTiles = await db.BoardApprovalTileSnapshots
            .Where(value => value.ApprovalSnapshotId == previous.Id)
            .ToListAsync();
        var priorRequirements = await db.BoardApprovalRequirementSnapshots
            .Where(value => priorTiles.Select(tile => tile.Id).Contains(value.ApprovalTileSnapshotId))
            .ToListAsync();
        var priorDrops = await db.BoardApprovalRequirementDropSnapshots
            .Where(value => priorRequirements.Select(requirement => requirement.Id).Contains(value.ApprovalRequirementSnapshotId))
            .ToListAsync();
        var priorBosses = await db.BoardApprovalRequirementBossSnapshots
            .Where(value => priorRequirements.Select(requirement => requirement.Id).Contains(value.ApprovalRequirementSnapshotId))
            .ToListAsync();

        var firstTile = await db.BoardTiles.SingleAsync(value => value.Id == priorTiles.Single().BoardTileId);
        var firstRequirement = await db.BoardRequirementSnapshots.SingleAsync(value => value.Id == priorRequirements.Single().BoardRequirementSnapshotId);
        var repeatedTile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 1, "Repeated metric tile", "", "", 1);
        var repeatedRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), repeatedTile.Id, 1, 1, true, false, "Repeated synthetic drop objective", false);
        var repeatedDrop = new BoardRequirementDropSnapshot(Guid.NewGuid(), repeatedRequirement.Id, f.Drops[0].Id, f.Drops[0].ItemId,
            f.Bosses[0].Name, "Fixture drop one", "1/10", .1m, null, 1);
        db.AddRange(repeatedTile, repeatedRequirement, repeatedDrop);

        board.BeginPublishedCorrection();
        board.Resize(1, 2, 2);
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, previous.Version + 1, f.Clock.GetUtcNow(), f.Admin.Id, previous.Id,
            board.Name, board.Rows, board.Columns, board.TotalEhbEstimate, board.CalculationVersion, board.Version, BoardState.Published);
        db.Add(approval);

        var approvalTiles = new Dictionary<Guid, BoardApprovalTileSnapshot>();
        foreach (var priorTile in priorTiles)
        {
            var copy = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, priorTile.BoardTileId, priorTile.TileTemplateId,
                priorTile.RowIndex, priorTile.ColumnIndex, priorTile.Name, priorTile.Description, priorTile.EvidenceInstructions,
                priorTile.EstimatedEhb, priorTile.ArtworkReference);
            approvalTiles.Add(priorTile.Id, copy);
            db.Add(copy);
        }
        var approvalRequirements = new Dictionary<Guid, BoardApprovalRequirementSnapshot>();
        foreach (var priorRequirement in priorRequirements)
        {
            var copy = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), approvalTiles[priorRequirement.ApprovalTileSnapshotId].Id,
                priorRequirement.BoardRequirementSnapshotId, priorRequirement.Position, priorRequirement.TargetContribution,
                priorRequirement.DuplicatesAllowed, priorRequirement.AllowHigherWeightings, priorRequirement.CreditedWeight,
                priorRequirement.Description, priorRequirement.ManualObjective);
            approvalRequirements.Add(priorRequirement.Id, copy);
            db.Add(copy);
        }
        foreach (var priorDrop in priorDrops)
        {
            db.Add(new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), approvalRequirements[priorDrop.ApprovalRequirementSnapshotId].Id,
                priorDrop.SourceDropId, priorDrop.ItemIdSnapshot, priorDrop.BossName, priorDrop.ItemName, priorDrop.DisplayRate,
                priorDrop.NumericProbability, priorDrop.MaximumContribution, priorDrop.EhbPerContribution, priorDrop.CreditedWeight,
                priorDrop.CatalogueVersion, priorDrop.ProbabilityScope, priorDrop.ConditionalOnParent, priorDrop.ParentProbability,
                priorDrop.AssumedParticipants, priorDrop.RollsPerCompletion, priorDrop.RollGroup, priorDrop.RateCondition));
        }
        foreach (var priorBoss in priorBosses)
        {
            db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), approvalRequirements[priorBoss.ApprovalRequirementSnapshotId].Id,
                priorBoss.BossActivityId, priorBoss.Name, priorBoss.EfficientRate, priorBoss.CatalogueVersion));
        }
        var repeatedApprovalTile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, repeatedTile.Id, repeatedTile.TileTemplateId,
            repeatedTile.RowIndex, repeatedTile.ColumnIndex, repeatedTile.NameSnapshot, repeatedTile.DescriptionSnapshot,
            repeatedTile.EvidenceInstructionsSnapshot, repeatedTile.EstimatedEhbSnapshot, repeatedTile.ImageUrlSnapshot);
        var repeatedApprovalRequirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), repeatedApprovalTile.Id, repeatedRequirement.Id,
            repeatedRequirement.Position, repeatedRequirement.TargetContribution, repeatedRequirement.DuplicatesAllowed,
            repeatedRequirement.AllowHigherWeightings, repeatedRequirement.CreditedWeight, repeatedRequirement.Description,
            repeatedRequirement.ManualObjective);
        db.AddRange(repeatedApprovalTile, repeatedApprovalRequirement,
            new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), repeatedApprovalRequirement.Id, repeatedDrop.SourceDropId,
                repeatedDrop.ItemIdSnapshot, repeatedDrop.BossName, repeatedDrop.ItemName, repeatedDrop.DisplayRate,
                repeatedDrop.NumericProbability, repeatedDrop.MaximumContribution, repeatedDrop.EhbPerContribution,
                repeatedDrop.CreditedWeight, 1));
        foreach (var boss in f.Bosses)
            db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), repeatedApprovalRequirement.Id, boss.Id, boss.Name, 10, boss.Version));

        board.ReplacePublishedApproval(approval.Id);
        await db.SaveChangesAsync();

        return new(
            firstTile.Id, firstRequirement.Id,
            await db.BoardRequirementDropSnapshots.Where(value => value.RequirementId == firstRequirement.Id && value.SourceDropId == f.Drops[0].Id).Select(value => value.Id).SingleAsync(),
            repeatedTile.Id, repeatedRequirement.Id, repeatedDrop.Id,
            await db.BoardRequirementDropSnapshots.Where(value => value.RequirementId == firstRequirement.Id && value.SourceDropId == f.Drops[1].Id).Select(value => value.Id).SingleAsync(),
            firstRequirement.Id);
    }

    private static (Submission Submission, SubmissionContribution Contribution) AddApprovedEvidence(
        ApplicationDbContext db, MetricFixture f, Team team, Guid participantId, Guid characterId,
        Guid tileId, Guid requirementId, Guid dropId, DateTimeOffset submittedAt)
    {
        var character = f.Characters.Concat(f.AdditionalCharacters).Single(value => value.Id == characterId);
        var submission = new Submission(Guid.NewGuid(), f.Event.Id, team.Id, tileId, requirementId, dropId,
            participantId, characterId, character.DisplayName, f.Admin.Id, 1, submittedAt, null, null);
        submission.Approve(1, submittedAt);
        var contribution = new SubmissionContribution(Guid.NewGuid(), submission.Id, team.Id, requirementId, dropId, participantId, 1, submittedAt);
        db.AddRange(submission, contribution);
        return (submission, contribution);
    }

    private static EventCompetitionSynchronizationService MetricService(ApplicationDbContext db, MetricClient client, MetricFixture fixture) => new(db, client, new FixedStatus(), fixture.Clock);
    private static async Task ConfigureMetricAsync(EventCompetitionSynchronizationService service, MetricFixture f) => Assert.True((await service.ConfigureAsync(f.Event.Id, f.Event.Version, 42, false, f.Actor)).Succeeded);
    private static WiseOldManCompetitionResult MetricResult(MetricFixture f, WiseOldManMetricDelta? first, WiseOldManMetricDelta? second = null) => new(WiseOldManCompetitionStatus.Success,
        new(42, "Synthetic competition", f.Event.EventStartsAt!.Value, f.Event.EventEndsAt!.Value, f.Clock.GetUtcNow(), f.Characters.Concat(f.AdditionalCharacters).Select(character =>
        {
            var metric = character.DisplayName is "Fixture Zero" or "Fixture Missing" ? new WiseOldManMetricDelta(-1, -1, 0) : first;
            return new WiseOldManCompetitionParticipant(character.DisplayName, "regular", 5, 10, 15,
                metric is null ? new Dictionary<string, WiseOldManMetricDelta> { [f.Bosses[1].ExternalIdentifier!] = second ?? new(10, 25, 15) }
                    : new Dictionary<string, WiseOldManMetricDelta> { [f.Bosses[0].ExternalIdentifier ?? "vorkath"] = metric, [f.Bosses[1].ExternalIdentifier!] = second ?? new(10, 25, 15) },
                f.Clock.GetUtcNow().AddMinutes(-5));
        }).ToArray()));
    private sealed record MetricPublication(
        Guid FirstTileId, Guid FirstRequirementId, Guid FirstDropId,
        Guid SecondTileId, Guid SecondRequirementId, Guid SecondDropId,
        Guid AlternateDropId, Guid AlternateRequirementId);
    private sealed record MetricFixture(BingoEvent Event, Account Admin, OsrsCharacter[] Characters, BossActivity[] Bosses, SourceDrop[] Drops, Guid BoardId, TestClock Clock)
    {
        public List<OsrsCharacter> AdditionalCharacters { get; } = [];
        public LifecycleActor Actor => new(Admin.Id, Admin.LoginName);
    }
    private sealed class MetricClient(MetricFixture fixture) : IWiseOldManCompetitionClient
    {
        public Func<Task>? BeforeReturn { get; set; }
        public WiseOldManCompetitionResult? Override { get; set; }
        public IReadOnlyCollection<string> Requested { get; private set; } = [];
        public int MetricCalls { get; private set; }
        public int ValidationCalls { get; private set; }
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default)
        { ValidationCalls++; return Task.FromResult(MetricResult(fixture, new(10, 25, 15))); }
        public async Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default)
        {
            MetricCalls++; Requested = metrics; var response = Override ?? MetricResult(fixture, new(10, 25, 15));
            if (BeforeReturn is not null) await BeforeReturn();
            return response;
        }
    }
}
