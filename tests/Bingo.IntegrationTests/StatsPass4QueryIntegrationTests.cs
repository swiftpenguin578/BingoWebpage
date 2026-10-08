using System.Data;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Stats;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Bingo.Web.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Theory]
    [InlineData(100, 1, 1, false, 55.0897160098096, 0)]
    [InlineData(200, 4, 1, true, 73.1888590400194, 66.6666666666667)]
    [InlineData(100, 2, 2, false, 54.0662189603843, 0)]
    public async Task StatsPass4CompleteQueryCalculatesActualDropsAndFrozenPersonalRolls(int kills, int received, int rolls, bool secondOutcome, double expectedPercent, double expectedKcDifference)
    {
        var f = await FullStatsFixtureAsync(rolls, secondOutcome);
        for (var i = 0; i < received; i++) await ApproveStatsAsync(f, await PendingStatsAsync(f, i % 2, 0, i + 1));
        await SyncStatsAsync(f, kills);
        var result = await ReadStatsAsync(f);
        Assert.Equal(received, result.Value.Drops); Assert.Equal(received * 100m, result.Value.ValueGp);
        Assert.Equal(received, result.Luck.Result.Received);
        Assert.InRange(result.Luck.Result.Percentage!.Value, (decimal)expectedPercent - .00000001m, (decimal)expectedPercent + .00000001m);
        Assert.InRange(result.Luck.Result.KcDifference!.Value, (decimal)expectedKcDifference - .00000001m, (decimal)expectedKcDifference + .00000001m);
        var activity = Assert.Single(result.Luck.Result.Activities!);
        Assert.Equal(kills, activity.Kc);
        Assert.InRange(activity.KcDifference!.Value, (decimal)expectedKcDifference - .00000001m, (decimal)expectedKcDifference + .00000001m);
        Assert.Equal(secondOutcome ? 2 : 1, result.Luck.Sources.Count);
        Assert.Equal(2, result.Rows); Assert.Equal(2, result.Columns);
        Assert.All(result.Drops, x => Assert.Equal(f.Items[0].Id, x.Item.ItemId));
        Assert.All(result.Drops, x => Assert.Equal("https://oldschool.runescape.wiki/images/Dragon_warhammer.png", x.Item.ImageUrl));
        await using var verify = new ApplicationDbContext(options);
        var cp = Assert.Single(await verify.EventStatsLuckCheckpoints.ToListAsync());
        Assert.Equal(result.EvidenceRevision, cp.EvidenceRevision); Assert.Equal(result.Luck.ActivityBatchId, cp.ActivityBatchId);
        Assert.DoesNotContain("MissingAccounts", cp.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("account_id", cp.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatsPass4RealCatalogueDksProjectionDeduplicatesBossActivityAndKeepsTileAttribution()
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, target: 3);
        await using (var catalogue = new ApplicationDbContext(options))
            await new CatalogueSnapshotService(catalogue, f.Clock).ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));

        Guid[] dksDropIds;
        await using (var setup = new ApplicationDbContext(options))
        {
            var bosses = await setup.BossActivities
                .Where(x => x.ExternalIdentifier == "dagannoth_prime" || x.ExternalIdentifier == "dagannoth_rex" || x.ExternalIdentifier == "dagannoth_supreme")
                .ToDictionaryAsync(x => x.ExternalIdentifier!);
            Assert.Equal(3, bosses.Count);
            var itemNames = new[] { "SEERS RING", "BERSERKER RING", "WARRIOR RING", "ARCHERS RING" };
            var catalogueRows = await (from source in setup.SourceDrops
                                       join boss in setup.BossActivities on source.BossActivityId equals boss.Id
                                       join item in setup.CatalogueItems on source.ItemId equals item.Id
                                       where itemNames.Contains(item.NormalizedName)
                                             && (boss.ExternalIdentifier == "dagannoth_prime" ||
                                                 boss.ExternalIdentifier == "dagannoth_rex" ||
                                                 boss.ExternalIdentifier == "dagannoth_supreme")
                                       select new { Source = source, Boss = boss, Item = item }).ToListAsync();
            Assert.Equal(itemNames.Length, catalogueRows.Count);
            var outcomes = itemNames.Select(name => catalogueRows.Single(x => x.Item.NormalizedName == name)).ToArray();
            var prime = bosses["dagannoth_prime"];
            var rex = bosses["dagannoth_rex"];
            var supreme = bosses["dagannoth_supreme"];
            Assert.Equal(prime.Id, outcomes[0].Boss.Id);
            Assert.Equal(rex.Id, outcomes[1].Boss.Id);
            Assert.Equal(rex.Id, outcomes[2].Boss.Id);
            Assert.Equal(supreme.Id, outcomes[3].Boss.Id);
            var requirementIds = f.Requirements.Take(2).Select(x => x.Id).ToArray();
            var approvalRequirements = await setup.BoardApprovalRequirementSnapshots
                .Where(x => requirementIds.Contains(x.BoardRequirementSnapshotId))
                .ToDictionaryAsync(x => x.BoardRequirementSnapshotId);
            var oldSourceIds = await setup.BoardRequirementDropSnapshots
                .Where(x => requirementIds.Contains(x.RequirementId))
                .Select(x => x.SourceDropId)
                .Distinct()
                .ToArrayAsync();

            // Replace the synthetic fixture outcomes with the four real DKS catalogue outcomes.
            await setup.BoardApprovalRequirementDropSnapshots
                .Where(x => oldSourceIds.Contains(x.SourceDropId))
                .ExecuteDeleteAsync();
            await setup.BoardRequirementDropSnapshots
                .Where(x => requirementIds.Contains(x.RequirementId))
                .ExecuteDeleteAsync();
            await setup.BoardApprovalRequirementBossSnapshots
                .Where(x => approvalRequirements.Values.Select(requirement => requirement.Id).Contains(x.ApprovalRequirementSnapshotId))
                .ExecuteDeleteAsync();
            await setup.SourceDrops.Where(x => oldSourceIds.Contains(x.Id)).ExecuteDeleteAsync();

            setup.EventItemPrices.AddRange(outcomes.Select(x =>
                EventItemPrice.Introduce(f.Event.Id, x.Item, f.Clock.GetUtcNow().AddHours(-2), f.Clock.GetUtcNow())));
            for (var tile = 0; tile < 2; tile++)
            {
                var requirement = f.Requirements[tile];
                var approvalRequirement = approvalRequirements[requirement.Id];
                var offset = tile * 2;
                foreach (var boss in outcomes.Skip(offset).Take(2).Select(x => x.Boss).DistinctBy(x => x.Id))
                    setup.BoardApprovalRequirementBossSnapshots.Add(new BoardApprovalRequirementBossSnapshot(
                        Guid.NewGuid(), approvalRequirement.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour, boss.Version));
                for (var outcome = 0; outcome < 2; outcome++)
                {
                    var definition = outcomes[offset + outcome];
                    var source = definition.Source;
                    setup.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(
                        Guid.NewGuid(), requirement.Id, source.Id, definition.Item.Id, definition.Boss.Name,
                        definition.Item.Name, source.DisplayRate, source.NumericProbability, null, source.DefaultEhbEstimate,
                        1, source.ProbabilityScope, source.ConditionalOnParent, source.ParentProbability,
                        source.AssumedParticipants, source.RollsPerCompletion, source.RollGroup, source.RateConditionNote));
                    setup.BoardApprovalRequirementDropSnapshots.Add(new BoardApprovalRequirementDropSnapshot(
                        Guid.NewGuid(), approvalRequirement.Id, source.Id, definition.Item.Id, definition.Boss.Name,
                        definition.Item.Name, source.DisplayRate, source.NumericProbability, null, source.DefaultEhbEstimate,
                        1, definition.Boss.Version, source.ProbabilityScope, source.ConditionalOnParent,
                        source.ParentProbability, source.AssumedParticipants, source.RollsPerCompletion,
                        source.RollGroup, source.RateConditionNote));
                }
            }

            // Deliberately place the real Seers ring (Dagannoth Prime) source on both
            // tiles so tile-retained numerators can be distinguished from event-wide ones.
            var repeated = outcomes[0];
            var repeatedSource = repeated.Source;
            var repeatedRequirement = f.Requirements[1];
            var repeatedApprovalRequirement = approvalRequirements[repeatedRequirement.Id];
            setup.BoardApprovalRequirementBossSnapshots.Add(new BoardApprovalRequirementBossSnapshot(
                Guid.NewGuid(), repeatedApprovalRequirement.Id, repeated.Boss.Id, repeated.Boss.Name,
                repeated.Boss.EfficientCompletionsPerHour, repeated.Boss.Version));
            setup.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(
                Guid.NewGuid(), repeatedRequirement.Id, repeatedSource.Id, repeated.Item.Id, repeated.Boss.Name,
                repeated.Item.Name, repeatedSource.DisplayRate, repeatedSource.NumericProbability, null,
                repeatedSource.DefaultEhbEstimate, 1, repeatedSource.ProbabilityScope, repeatedSource.ConditionalOnParent,
                repeatedSource.ParentProbability, repeatedSource.AssumedParticipants, repeatedSource.RollsPerCompletion,
                repeatedSource.RollGroup, repeatedSource.RateConditionNote));
            setup.BoardApprovalRequirementDropSnapshots.Add(new BoardApprovalRequirementDropSnapshot(
                Guid.NewGuid(), repeatedApprovalRequirement.Id, repeatedSource.Id, repeated.Item.Id, repeated.Boss.Name,
                repeated.Item.Name, repeatedSource.DisplayRate, repeatedSource.NumericProbability, null,
                repeatedSource.DefaultEhbEstimate, 1, repeated.Boss.Version, repeatedSource.ProbabilityScope,
                repeatedSource.ConditionalOnParent, repeatedSource.ParentProbability, repeatedSource.AssumedParticipants,
                repeatedSource.RollsPerCompletion, repeatedSource.RollGroup, repeatedSource.RateConditionNote));
            await setup.SaveChangesAsync();
            dksDropIds = outcomes.Select(x => x.Source.Id).ToArray();
        }

        for (var index = 0; index < dksDropIds.Length; index++)
        {
            var tile = index < 2 ? 0 : 1;
            await ApproveStatsAsync(f, await PendingStatsForDropAsync(f, tile, 0, 10 + index, dksDropIds[index]));
        }
        await ApproveStatsAsync(f, await PendingStatsForDropAsync(f, 1, 0, 15, dksDropIds[0]));

        var metrics = new Dictionary<string, WiseOldManMetricDelta>
        {
            ["dagannoth_prime"] = new(0, 100, 100),
            ["dagannoth_rex"] = new(0, 50, 50),
            ["dagannoth_supreme"] = new(0, 25, 25)
        };
        var competition = new WiseOldManCompetition(42, "Controlled DKS competition", f.Event.EventStartsAt!.Value, f.Event.EventEndsAt!.Value, f.Clock.GetUtcNow(),
            [new(f.Characters[0].Id.ToString(), "regular", 1, 0, 1, metrics, f.Clock.GetUtcNow().AddMinutes(-5))]);
        await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Success, competition));

        var result = await ReadStatsAsync(f);
        Assert.Equal(5, result.Luck.Result.Received);
        var activities = result.Luck.Result.Activities!.ToDictionary(x => x.Name);
        Assert.Equal(3, activities.Count);
        Assert.Equal(2, activities["Dagannoth Prime"].Received);
        Assert.Equal(2, activities["Dagannoth Rex"].Received);
        Assert.Equal(1, activities["Dagannoth Supreme"].Received);
        Assert.Equal(result.Luck.Result.Percentage, Assert.Single(result.Luck.Teams).Result.Percentage);
        Assert.Contains(result.Luck.Sources, x => x.Item.Name == "Seers ring" && x.BossName == "Dagannoth Prime");
        Assert.Contains(result.Luck.Sources, x => x.Item.Name == "Berserker ring" && x.BossName == "Dagannoth Rex");
        Assert.Contains(result.Luck.Sources, x => x.Item.Name == "Warrior ring" && x.BossName == "Dagannoth Rex");
        Assert.Contains(result.Luck.Sources, x => x.Item.Name == "Archers ring" && x.BossName == "Dagannoth Supreme");

        var tile0 = await ReadTileActivityAsync(f, 0);
        var tile1 = await ReadTileActivityAsync(f, 1);
        Assert.Equal(["Dagannoth Prime", "Dagannoth Rex"], tile0.Team.Metrics.Select(x => x.Name));
        Assert.Equal(["Dagannoth Prime", "Dagannoth Rex", "Dagannoth Supreme"], tile1.Team.Metrics.Select(x => x.Name));
        var tile0Player = Assert.Single(tile0.Team.Players);
        var tile1Player = Assert.Single(tile1.Team.Players);
        Assert.Equal(2, tile0.Team.Result.Received);
        Assert.InRange(tile0.Team.Result.Percentage!.Value, 77.9396912567m, 77.9396912568m);
        Assert.Equal(106m, tile0.Team.Result.KcDifference);
        Assert.Equal(3, tile1.Team.Result.Received);
        Assert.InRange(tile1.Team.Result.Percentage!.Value, 89.6282691401m, 89.6282691402m);
        Assert.Equal(209m, tile1.Team.Result.KcDifference);
        Assert.Equal(2, tile0Player.Sources!.Count);
        Assert.Equal(3, tile1Player.Sources!.Count);
        Assert.All(tile0Player.Sources, source => Assert.Equal(1, source.Received));
        Assert.All(tile1Player.Sources, source => Assert.Equal(1, source.Received));
        var tile0Prime = tile0Player.Result.Activities!.Single(x => x.Name == "Dagannoth Prime");
        var tile1Prime = tile1Player.Result.Activities!.Single(x => x.Name == "Dagannoth Prime");
        Assert.Equal(1, tile0Prime.Received);
        Assert.InRange(tile0Prime.Percentage!.Value, 63.6128240483m, 63.6128240484m);
        Assert.Equal(28m, tile0Prime.KcDifference);
        Assert.Equal(1, tile1Prime.Received);
        Assert.InRange(tile1Prime.Percentage!.Value, 63.6128240483m, 63.6128240484m);
        Assert.Equal(28m, tile1Prime.KcDifference);
        var tile0PrimeMetric = tile0.Team.Metrics.Single(x => x.Name == "Dagannoth Prime");
        var tile1PrimeMetric = tile1.Team.Metrics.Single(x => x.Name == "Dagannoth Prime");
        Assert.Equal(100m, tile0PrimeMetric.Count);
        Assert.InRange(tile0PrimeMetric.Percentage!.Value, 63.6128240483m, 63.6128240484m);
        Assert.Equal(28m, tile0PrimeMetric.KcDifference);
        Assert.Equal(100m, tile1PrimeMetric.Count);
        Assert.InRange(tile1PrimeMetric.Percentage!.Value, 63.6128240483m, 63.6128240484m);
        Assert.Equal(28m, tile1PrimeMetric.KcDifference);
        Assert.Equal(50m, tile0.Team.Metrics.Single(x => x.Name == "Dagannoth Rex").Count);
        Assert.Equal(80.8588261729m, tile0.Team.Metrics.Single(x => x.Name == "Dagannoth Rex").Percentage!.Value, 10);
        Assert.Equal(78m, tile0.Team.Metrics.Single(x => x.Name == "Dagannoth Rex").KcDifference);
        Assert.Equal(50m, tile1.Team.Metrics.Single(x => x.Name == "Dagannoth Rex").Count);
        Assert.Equal(80.8588261729m, tile1.Team.Metrics.Single(x => x.Name == "Dagannoth Rex").Percentage!.Value, 10);
        Assert.Equal(78m, tile1.Team.Metrics.Single(x => x.Name == "Dagannoth Rex").KcDifference);
        Assert.Equal(25m, tile1.Team.Metrics.Single(x => x.Name == "Dagannoth Supreme").Count);
        Assert.Equal(90.2847228794m, tile1.Team.Metrics.Single(x => x.Name == "Dagannoth Supreme").Percentage!.Value, 10);
        Assert.Equal(103m, tile1.Team.Metrics.Single(x => x.Name == "Dagannoth Supreme").KcDifference);
        var seersId = result.Luck.Sources.Single(s => s.Item.Name == "Seers ring").Item.ItemId;
        Assert.Contains(tile0Player.Sources, x => x.ItemId == seersId);
        Assert.Contains(tile1Player.Sources, x => x.ItemId == seersId);
    }

    [Fact]
    public async Task StatsPass4LaterThenEarlierApprovalRecomputesHistoryAndWeightedDropsRemainOneItem()
    {
        var f = await FullStatsFixtureAsync(target: 6, weight: 3);
        var later = await PendingStatsAsync(f, 0, 0, 20, 3); var earlier = await PendingStatsAsync(f, 0, 0, 10, 3);
        await SyncStatsAsync(f, 200);
        await ApproveStatsAsync(f, later);
        var first = await ReadStatsAsync(f); Assert.Equal(100, first.Value.ValueGp); Assert.Equal(3, first.Teams[0].ProgressHistory[^1].Approved);
        Assert.Equal(f.Clock.GetUtcNow().AddMinutes(-40), first.Milestones.Single(x => x.Id == "submission").At);
        f.Clock.Advance(TimeSpan.FromMinutes(1)); await ApproveStatsAsync(f, earlier);
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, 200);
        var second = await ReadStatsAsync(f);
        Assert.Equal(2, second.Value.Drops); Assert.Equal(200, second.Value.ValueGp); Assert.Equal(2, second.Luck.Result.Received);
        Assert.Equal([earlier, later], second.Drops.Select(x => x.SubmissionId));
        Assert.Equal([100m, 200m], second.Teams[0].ValueHistory.Select(x => x.Value.ValueGp!.Value));
        Assert.Equal(f.Event.ActualStartedAt!.Value.AddMinutes(10), second.Milestones.Single(x => x.Id == "submission").At);
        Assert.Equal(f.Event.ActualStartedAt!.Value.AddMinutes(20), second.Milestones.Single(x => x.Id == "tile").At);
        Assert.Equal(first.EvidenceRevision + 1, second.EvidenceRevision);
        Assert.Equal(second.EvidenceRevision, second.Luck.EvidenceRevision);
        await ReverseStatsAsync(f, earlier);
        var reversed = await ReadStatsAsync(f); Assert.Single(reversed.Drops); Assert.Equal(0, reversed.Teams[0].Progress.CompletedTiles);
        Assert.Equal(StatsMilestoneState.Pending, reversed.Milestones.Single(x => x.Id == "tile").State);
    }

    [Theory]
    [InlineData("zero", false, StatsLuckStatus.NoEligibleActivity)]
    [InlineData("zero", true, StatsLuckStatus.WaitingForActivityUpdate)]
    [InlineData("unranked", false, StatsLuckStatus.NoEligibleActivity)]
    [InlineData("unranked", true, StatsLuckStatus.WaitingForActivityData)]
    [InlineData("missing", true, StatsLuckStatus.WaitingForActivityData)]
    [InlineData("unexpected-unranked", true, StatsLuckStatus.WaitingForActivityData)]
    [InlineData("estimated", true, StatsLuckStatus.Calculated)]
    [InlineData("mode", true, StatsLuckStatus.Calculated)]
    public async Task StatsPass4CompleteQueryPreservesUnavailableZeroAndEstimatedStates(string scenario, bool drop, StatsLuckStatus status)
    {
        var f = await FullStatsFixtureAsync(metric: scenario == "mode" ? "nightmare" : "vorkath");
        if (drop) await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        WiseOldManMetricDelta? delta = scenario switch { "unranked" => new(-1, -1, 0), "missing" => null, "unexpected-unranked" => new(10, -1, 0), "estimated" => new(-1, 100, 100), "mode" => new(0, 100, 100), _ => new(0, 0, 0) };
        await SyncStatsAsync(f, delta);
        var result = await ReadStatsAsync(f);
        Assert.Equal(status, result.Luck.Result.Status);
        if (status != StatsLuckStatus.Calculated) Assert.Null(result.Luck.Result.Percentage);
        else { Assert.InRange(result.Luck.Result.Percentage!.Value, 55.08971599m, 55.08971603m); Assert.Equal(scenario == "estimated", result.Luck.Result.Estimated); }
        if (scenario == "mode")
        {
            Assert.All(result.Luck.Sources, x => Assert.Null(x.UnavailableReason));
            Assert.Equal(1m, result.Luck.Result.Expected);
            Assert.Equal(100m, Assert.Single(Assert.Single(Assert.Single(result.Luck.Teams).Players).Sources).Activity);
        }
        Assert.Equal(scenario == "unranked", result.Luck.Result.ZeroRecordedApproximation);
        Assert.Equal(scenario == "estimated", result.Luck.Result.Estimated);
        Assert.All(result.Luck.Sources, source => Assert.Null(source.UnavailableReason));
        await using var verify = new ApplicationDbContext(options);
        var row = await verify.EventCompetitionCharacterMetricActivities.AsNoTracking()
            .SingleAsync(x => x.OsrsCharacterId == f.Characters[0].Id);
        if (scenario is "missing" or "unexpected-unranked")
        {
            Assert.Equal(scenario == "missing" ? Bingo.Domain.Integrations.WiseOldMan.MetricActivityCoverage.Missing
                : Bingo.Domain.Integrations.WiseOldMan.MetricActivityCoverage.UnexpectedUnrankedEnd, row.LastIssue);
            Assert.Null(Assert.Single(Assert.Single(Assert.Single(result.Luck.Teams).Players).Sources).Activity);
        }
        else Assert.Equal(scenario switch
        {
            "unranked" => Bingo.Domain.Integrations.WiseOldMan.MetricActivityCoverage.ZeroRecorded,
            "estimated" => Bingo.Domain.Integrations.WiseOldMan.MetricActivityCoverage.EstimatedBaseline,
            _ => Bingo.Domain.Integrations.WiseOldMan.MetricActivityCoverage.Ranked
        }, row.Coverage);
    }

    [Fact]
    public async Task StatsPass4NormalSyncReplacesFormerModeGateCacheAndCheckpoint()
    {
        var f = await FullStatsFixtureAsync(metric: "nightmare");
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        var original = await ReadStatsAsync(f);
        // Represent a prior source contract with its own fingerprint and gated saved payload.
        var formerFingerprint = new string('a', 64);
        var unavailable = new StatsLuckResult(1, null, null, StatsLuckStatus.WaitingForActivityData, false, false);
        var formerLuck = original.Luck with
        {
            Result = unavailable,
            Sources = original.Luck.Sources.Select(x => x with { UnavailableReason = "ModeSemanticsUnverified" }).ToArray(),
            Teams = original.Luck.Teams.Select(team => team with
            {
                Result = unavailable,
                Players = team.Players.Select(player => player with
                {
                    Result = unavailable,
                    Sources = player.Sources.Select(x => x with { Activity = null, Expected = null, Status = StatsLuckStatus.WaitingForActivityData }).ToArray()
                }).ToArray()
            }).ToArray()
        };
        await using (var previous = new ApplicationDbContext(options))
        {
            var state = await previous.EventCompetitionSynchronizations.SingleAsync();
            state.SetSourceRequest(formerFingerprint);
            await previous.SaveChangesAsync();
            await previous.EventCompetitionCharacterMetricActivities.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SourceRequestFingerprint, formerFingerprint));
            var payload = System.Text.Json.JsonSerializer.Serialize(formerLuck);
            await previous.EventStatsLuckCheckpoints.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SourceFingerprint, formerFingerprint).SetProperty(x => x.Payload, payload));
        }
        var awaitingSync = await ReadStatsAsync(f);
        Assert.Equal(StatsLuckStatus.WaitingForActivityData, awaitingSync.Luck.Result.Status);
        Assert.Null(awaitingSync.Luck.CalculatedAt);
        Assert.All(awaitingSync.Luck.Sources, x => Assert.Null(x.UnavailableReason));

        f.Clock.Advance(TimeSpan.FromHours(3));
        await using (var sync = new ApplicationDbContext(options))
        {
            var response = StatsResponse(f, new(0, 200, 200));
            var names = await sync.OsrsCharacters.ToDictionaryAsync(x => x.Id, x => x.DisplayName);
            response = response with { Competition = response.Competition! with { Participants = response.Competition.Participants.Select(p => p with { Username = names[Guid.Parse(p.Username)] }).ToArray() } };
            var service = new EventCompetitionSynchronizationService(sync, new StatsClient(response, response, null), new FixedStatus(), f.Clock);
            await service.ProcessDueAsync();
            var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
            Assert.True(cache.Compatible); Assert.True(cache.Complete);
            Assert.NotEqual(formerFingerprint, cache.Sources.Fingerprint);
            Assert.All(cache.Rows, x => Assert.Equal(cache.Sources.Fingerprint, x.SourceRequestFingerprint));
            var checkpoint = await sync.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            Assert.Equal(cache.Sources.Fingerprint, checkpoint.SourceFingerprint);
            Assert.Equal(cache.ActivityBatchId, checkpoint.ActivityBatchId);
            Assert.NotEqual(original.Luck.ActivityBatchId, checkpoint.ActivityBatchId);
            Assert.DoesNotContain("ModeSemanticsUnverified", checkpoint.Payload, StringComparison.Ordinal);
        }
        var recovered = await ReadStatsAsync(f);
        Assert.Equal(StatsLuckStatus.Calculated, recovered.Luck.Result.Status);
        Assert.Equal(2m, recovered.Luck.Result.Expected); AssertLuckScore(26.9312679764995m, recovered.Luck.Result);
        Assert.Equal(f.Clock.GetUtcNow(), recovered.Luck.CalculatedAt);
        Assert.False(recovered.Luck.Stale);
    }

    [Fact]
    public async Task StatsPass4PoolsRegularAccountsAndPlayersWithoutInformationalAltsOrAveragePercentages()
    {
        var f = await FullStatsFixtureAsync(players: 2, extraRegular: true);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 1, 1, 11));
        await SyncStatsAsync(f, 100);
        var result = await ReadStatsAsync(f);
        var team = Assert.Single(result.Luck.Teams);
        Assert.Equal(2, team.Players.Count); Assert.Equal(3, team.Players.Sum(x => x.Sources.Count));
        Assert.Equal(3, team.Result.Expected); Assert.Equal(2, team.Result.Received);
        AssertLuckScore(30.9856790762009m, team.Result);
        Assert.NotEqual(team.Players.Average(x => x.Result.Percentage), team.Result.Percentage);
        Assert.DoesNotContain(team.Players.SelectMany(x => x.Sources), x => x.CharacterId == f.Alt.Id);
        Assert.Equal(100, result.Teams[0].Players.Sum(x => x.TeamValueShare));
        Assert.Equal(2, result.RepeatedItem!.Count); Assert.Equal(1, result.MostVersatile!.DistinctTiles);
    }

    [Fact]
    public async Task StatsPass4OutageRetainsWholeCheckpointAndReversalRetainsIt()
    {
        var f = await FullStatsFixtureAsync();
        var id = await PendingStatsAsync(f, 0, 0, 10); await ApproveStatsAsync(f, id); await SyncStatsAsync(f, 100);
        var original = await ReadStatsAsync(f);
        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null, Message: "Synthetic outage"));
        var stale = await ReadStatsAsync(f); Assert.True(stale.Luck.Stale); Assert.Equal(original.Luck.Result.Percentage, stale.Luck.Result.Percentage);
        Assert.Equal(original.Luck.CalculatedAt, stale.Luck.CalculatedAt); Assert.Equal(original.Luck.FetchedAt, stale.Luck.FetchedAt);
        await ReverseStatsAsync(f, id);
        var reversed = await ReadStatsAsync(f); Assert.Equal(0, reversed.Value.Drops); Assert.Equal(original.Luck.Result.Percentage, reversed.Luck.Result.Percentage);
        Assert.Equal(original.Luck.CalculatedAt, reversed.Luck.CalculatedAt); Assert.True(reversed.Luck.Stale);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(original.EvidenceRevision, (await verify.EventStatsLuckCheckpoints.SingleAsync()).EvidenceRevision);
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, 100);
        var recovered = await ReadStatsAsync(f); AssertLuckScore(18.3016170636616m, recovered.Luck.Result); Assert.Equal(recovered.EvidenceRevision, recovered.Luck.EvidenceRevision);
    }

    [Fact]
    public async Task StatsPass4EvidenceDuringProviderFetchAndStaleWriterCannotOverwriteNewerRevision()
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        EventStatsLuckCheckpoint old;
        await using (var read = new ApplicationDbContext(options)) old = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        var id = await PendingStatsAsync(f, 0, 0, 10);
        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, StatsResponse(f, new(0, 200, 200)), () => ApproveStatsAsync(f, id));
        var fresh = await ReadStatsAsync(f); Assert.Equal(1, fresh.Luck.Result.Received); Assert.Equal(2, fresh.Luck.Result.Expected);
        await using var stale = new ApplicationDbContext(options);
        await using var tx = await stale.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Assert.False(await PublicStatsService.TryWriteCheckpointAsync(stale, f.Clock, old)); await tx.CommitAsync();
        var current = await ReadStatsAsync(f); Assert.Equal(System.Text.Json.JsonSerializer.Serialize(fresh.Luck), System.Text.Json.JsonSerializer.Serialize(current.Luck));
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("private")]
    [InlineData("unpublished")]
    [InlineData("cancelled")]
    [InlineData("import")]
    [InlineData("unknown")]
    public async Task StatsPass4QueryBoundaryDoesNotLeakUnavailableEventsOrChoosePreferredEvent(string scenario)
    {
        var f = await FullStatsFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        if (scenario == "hidden") { var ev = await db.Events.SingleAsync(); ev.EndEvent(f.Clock.GetUtcNow()); ev.Hide(f.Admin.Id, f.Clock.GetUtcNow(), f.Event.Name, "Fixture hidden"); }
        if (scenario == "private") await db.Database.ExecuteSqlRawAsync("UPDATE events SET first_public_at = NULL");
        if (scenario == "unpublished") await db.Database.ExecuteSqlRawAsync("UPDATE events SET board_published = false");
        if (scenario == "cancelled") await db.Database.ExecuteSqlRawAsync("UPDATE events SET state = 'Cancelled'");
        if (scenario == "import") await db.Database.ExecuteSqlRawAsync("UPDATE events SET slug = 'det-store-danske-sommerbingo-2026'");
        await db.SaveChangesAsync();
        Assert.Null(await new PublicStatsService(db, f.Clock).GetAsync(scenario == "unknown" ? "missing" : scenario == "import" ? "det-store-danske-sommerbingo-2026" : f.Event.Slug));
    }

    [Fact]
    public async Task StatsPass4MissingHistoricalPriceIsExplicitAndCatalogueEditsKeepExactFrozenIdentity()
    {
        var f = await FullStatsFixtureAsync(); var id = await PendingStatsAsync(f, 0, 0, 10); await ApproveStatsAsync(f, id);
        await using (var mutate = new ApplicationDbContext(options))
        {
            var item = await mutate.CatalogueItems.SingleAsync(x => x.Id == f.Items[0].Id); item.SetPrice(99999, CataloguePriceSource.Manual, f.Clock.GetUtcNow());
            await mutate.SaveChangesAsync();
        }
        var frozen = await ReadStatsAsync(f); Assert.Equal(100, frozen.Value.ValueGp); Assert.Equal(f.Items[0].Id, frozen.Drops[0].Item.ItemId);
        await using (var legacy = new ApplicationDbContext(options)) await legacy.EventItemPrices.ExecuteDeleteAsync();
        var missing = await ReadStatsAsync(f); Assert.Equal(1, missing.Value.MissingPrices); Assert.Null(missing.Value.ValueGp);
        Assert.Null(missing.Drops[0].ValueGp); Assert.Null(missing.Drops[0].PriceHour); Assert.Null(missing.Teams[0].EventValueShare);
    }

    [Fact]
    public async Task BoundedLuckStatsAndTileMatchFixedPercentileAcrossReceivedCounts()
    {
        var f = await FullStatsFixtureAsync();
        await using (var setup = new ApplicationDbContext(options))
            await setup.Database.ExecuteSqlRawAsync("UPDATE board_approval_requirement_drop_snapshots SET numeric_probability = 1.0 / 251");
        await SyncStatsAsync(f, 502);
        decimal[] expected = [6.7397870409m, 27.01306645995m, 54.10711820099m, 76.70805047248m, 90.25956946247m, 96.56795614269m, 98.96068245992m];
        for (var received = 0; received < expected.Length; received++)
        {
            if (received > 0)
            {
                await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, received));
                f.Clock.Advance(TimeSpan.FromHours(3));
                await SyncStatsAsync(f, 502);
            }
            var stats = await ReadStatsAsync(f);
            var tile = await ReadTileActivityAsync(f);
            AssertLuckScore(expected[received], stats.Luck.Result, .0000001m); // frozen probability has 12 decimal places
            AssertLuckScore(expected[received], tile.Team.Result, .0000001m);
            Assert.Equal(received, stats.Luck.Result.Received); Assert.Equal(received, tile.Team.Result.Received);
            Assert.Equal(502m, Assert.Single(tile.Team.Metrics).Count);
            AssertLuckScore(expected[0], (await ReadTileActivityAsync(f, 1)).Team.Result, .0000001m);
        }
    }

    [Fact]
    public async Task BoundedLuckProjectionRetainsKcWhenPmfWorkLimitMakesPercentileUnavailable()
    {
        var f = await FullStatsFixtureAsync();
        await using (var setup = new ApplicationDbContext(options))
            await setup.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE board_approval_requirement_drop_snapshots
                SET display_rate = {"1/2"}, numeric_probability = 0.5
                WHERE approval_requirement_snapshot_id IN (
                    SELECT requirement.id
                    FROM board_approval_requirement_snapshots requirement
                    JOIN board_approval_tile_snapshots tile ON tile.id = requirement.approval_tile_snapshot_id
                    JOIN board_approval_snapshots approval ON approval.id = tile.approval_snapshot_id
                    JOIN boards board ON board.id = approval.board_id
                    WHERE board.event_id = {f.Event.Id}
                )
                """);

        await SyncStatsAsync(f, new WiseOldManMetricDelta(0, 1_000_000_000, 1_000_000_000));
        var result = await ReadStatsAsync(f);
        Assert.Equal(StatsLuckStatus.Calculated, result.Luck.Result.Status);
        Assert.NotNull(result.Luck.Result.Expected);
        Assert.Null(result.Luck.Result.Percentage);
        Assert.NotNull(result.Luck.Result.KcDifference);
        Assert.NotNull(result.Luck.Result.Activities);
        Assert.Equal(1_000_000_000m, result.Luck.Result.Activities!.Single().Kc);
        Assert.Equal(-1_000_000_000m, result.Luck.Result.Activities!.Single().KcDifference);
    }

    [Fact]
    public async Task BoundedLuckContradictoryRollMechanicsLeaveBossAndTileValuesUnavailable()
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true);
        var firstSource = f.Drops[0].SourceDropId;
        var secondSource = f.Drops[1].SourceDropId;
        await using (var setup = new ApplicationDbContext(options))
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET rolls_per_completion = 2 WHERE source_drop_id = {secondSource}");
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10, itemIndex: 0));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 11, itemIndex: 1));
        await SyncStatsAsync(f, 10);

        var stats = await ReadStatsAsync(f);
        var activity = Assert.Single(stats.Luck.Result.Activities!);
        Assert.Equal(StatsLuckStatus.Incomplete, activity.Status);
        Assert.Null(activity.Expected);
        Assert.Null(activity.Kc);
        Assert.Null(activity.Percentage);
        Assert.Null(activity.KcDifference);

        var tile = await ReadTileActivityAsync(f, 0);
        Assert.Equal(StatsLuckStatus.Incomplete, tile.Team.Result.Status);
        Assert.Null(tile.Team.Result.Percentage);
        Assert.Null(tile.Team.Result.KcDifference);
        var tileActivity = Assert.Single(tile.Team.Result.Activities!);
        Assert.Equal(StatsLuckStatus.Incomplete, tileActivity.Status);
        Assert.Null(tileActivity.Expected);
        Assert.Null(tileActivity.Kc);
        Assert.Null(tileActivity.Percentage);
        Assert.Null(tileActivity.KcDifference);
        var metric = Assert.Single(tile.Team.Metrics);
        Assert.Equal(StatsLuckStatus.Incomplete, metric.Status);
        Assert.Null(metric.Count);
        Assert.Null(metric.Percentage);
        Assert.Null(metric.KcDifference);
        var player = Assert.Single(tile.Team.Players);
        Assert.Equal(StatsLuckStatus.Incomplete, player.Result.Status);
        Assert.Null(player.Result.Percentage);
        Assert.Null(player.Result.KcDifference);
    }

    [Theory]
    [InlineData(false, 81.25)]
    [InlineData(true, 78.348928)]
    public async Task BoundedLuckGroupsExclusiveOutcomesAndConvolvesIndependentRepeatedPersonalRolls(bool independent, double expected)
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, rolls: 2);
        await using (var setup = new ApplicationDbContext(options))
        {
            var firstSource = f.Drops[0].SourceDropId;
            // Frozen rates are already personal; assumed team size must not divide them again.
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET numeric_probability = CASE WHEN source_drop_id = {firstSource} THEN 0.4 ELSE 0.3 END, conditional_on_parent = (source_drop_id = {firstSource}), parent_probability = CASE WHEN source_drop_id = {firstSource} THEN 0.5 ELSE NULL END, assumed_participants = 5");
            if (independent)
                await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET roll_group = 'independent' WHERE source_drop_id <> {firstSource}");
        }
        for (var i = 0; i < 3; i++) await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, i + 1, itemIndex: i % 2));
        await SyncStatsAsync(f, 2);
        var stats = await ReadStatsAsync(f); var tile = await ReadTileActivityAsync(f);
        Assert.Equal(2m, stats.Luck.Result.Expected); Assert.Equal(2m, tile.Team.Result.Expected);
        AssertLuckScore((decimal)expected, stats.Luck.Result); AssertLuckScore((decimal)expected, tile.Team.Result);
        Assert.Equal(2m, Assert.Single(tile.Team.Metrics).Count);
    }

    [Fact]
    public async Task LuckCheckpointConversionOperationReportsAllOutcomesAndPreservesFailedRows()
    {
        var f = await FullStatsFixtureAsync();
        await SyncStatsAsync(f, 100);

        EventStatsLuckCheckpoint baseline;
        await using (var read = new ApplicationDbContext(options))
            baseline = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();

        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE event_stats_luck_checkpoints
                SET schema_version = {1},
                    algorithm_version = {"legacy-signed-v1"},
                    converted_from_schema_version = NULL,
                    converted_at = NULL
                WHERE event_id = {f.Event.Id}
                """);
        }

        await using (var conversion = new ApplicationDbContext(options))
        {
            var report = await new LuckCheckpointConversionService(conversion, f.Clock).RunAsync();
            var item = Assert.Single(report.Events);
            Assert.True(report.Succeeded);
            Assert.Equal(LuckCheckpointConversionOutcome.Converted, item.Outcome);
            Assert.Null(item.Reason);
        }

        await using (var converted = new ApplicationDbContext(options))
        {
            var row = await converted.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            Assert.Equal(EventStatsLuckCheckpoint.CurrentSchemaVersion, row.SchemaVersion);
            Assert.Equal(1, row.ConvertedFromSchemaVersion);
            Assert.NotNull(row.ConvertedAt);
            Assert.Equal(baseline.CalculatedAt, row.CalculatedAt);
            Assert.Equal(baseline.FetchedAt, row.FetchedAt);
        }
        Assert.Equal(StatsLuckStatus.Calculated, (await ReadStatsAsync(f)).Luck.Result.Status);

        string convertedPayload;
        DateTimeOffset? convertedAt;
        await using (var retry = new ApplicationDbContext(options))
        {
            var before = await retry.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            convertedPayload = before.Payload;
            convertedAt = before.ConvertedAt;
            var report = await new LuckCheckpointConversionService(retry, f.Clock).RunAsync();
            var item = Assert.Single(report.Events);
            Assert.True(report.Succeeded);
            Assert.Equal(LuckCheckpointConversionOutcome.AlreadyConverted, item.Outcome);
            Assert.Null(item.Reason);
        }
        await using (var afterRetry = new ApplicationDbContext(options))
        {
            var row = await afterRetry.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            Assert.Equal(convertedPayload, row.Payload);
            Assert.Equal(convertedAt, row.ConvertedAt);
        }

        await using (var malformed = new ApplicationDbContext(options))
        {
            await malformed.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE event_stats_luck_checkpoints
                SET schema_version = {1},
                    algorithm_version = {"legacy-signed-v1"},
                    converted_from_schema_version = NULL,
                    converted_at = NULL,
                    payload = CAST({"{}"} AS jsonb)
                WHERE event_id = {f.Event.Id}
                """);
        }

        EventStatsLuckCheckpoint failedBefore;
        await using (var read = new ApplicationDbContext(options))
            failedBefore = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        await using (var failedConversion = new ApplicationDbContext(options))
        {
            var report = await new LuckCheckpointConversionService(failedConversion, f.Clock).RunAsync();
            var item = Assert.Single(report.Events);
            Assert.False(report.Succeeded);
            Assert.Equal(LuckCheckpointConversionOutcome.CouldNotConvert, item.Outcome);
            Assert.Contains("unsupported-retained-payload", item.Reason, StringComparison.Ordinal);
        }
        await using (var afterFailure = new ApplicationDbContext(options))
        {
            var row = await afterFailure.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            Assert.Equal(failedBefore.Payload, row.Payload);
            Assert.Equal(failedBefore.SchemaVersion, row.SchemaVersion);
            Assert.Equal(failedBefore.AlgorithmVersion, row.AlgorithmVersion);
            Assert.Equal(failedBefore.ConvertedFromSchemaVersion, row.ConvertedFromSchemaVersion);
            Assert.Equal(failedBefore.ConvertedAt, row.ConvertedAt);
        }
    }

    [Fact]
    public async Task BoundedLuckLegacyConversionIsExplicitAndReadsRetainConvertedSnapshot()
    {
        var f = await FullStatsFixtureAsync(players: 2, extraRegular: true);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 1, 1, 11));
        await SyncStatsAsync(f, 100);

        EventStatsLuckCheckpoint originalCheckpoint;
        StatsLuck stored;
        await using (var read = new ApplicationDbContext(options))
        {
            originalCheckpoint = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            stored = JsonSerializer.Deserialize<StatsLuck>(originalCheckpoint.Payload)!;
        }
        var originalStats = await ReadStatsAsync(f);
        var originalTile = await ReadTileActivityAsync(f);
        var legacyLifecycleFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { f.Event.State, f.Event.ActualStartedAt, f.Event.ActualEndedAt, f.Event.FinalizedAt, f.Event.ArchivedAt, f.Event.ResultsPublished })))).ToLowerInvariant();
        var legacy = stored with
        {
            Result = OldRatio(stored.Result),
            Teams = stored.Teams.Select(team => team with
            {
                Result = OldRatio(team.Result),
                Players = team.Players.Select(player => player with { Result = OldRatio(player.Result) }).ToArray()
            }).ToArray(),
            Tiles = stored.Tiles?.Select(tile => tile with
            {
                Teams = tile.Teams.Select(team => team with
                {
                    Result = OldRatio(team.Result),
                    Players = team.Players.Select(player => player with { Result = OldRatio(player.Result) }).ToArray()
                }).ToArray()
            }).ToArray()
        };
        await using (var setup = new ApplicationDbContext(options))
        {
            var payload = JsonSerializer.Serialize(legacy);
            await setup.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE event_stats_luck_checkpoints
                SET schema_version = 1,
                    algorithm_version = {"legacy-signed-v1"},
                    converted_from_schema_version = NULL,
                    converted_at = NULL,
                    lifecycle_fingerprint = {legacyLifecycleFingerprint},
                    payload = CAST({payload} AS jsonb)
                WHERE event_id = {f.Event.Id}
                """);
        }

        await using (var conversion = new ApplicationDbContext(options))
        {
            await using var tx = await conversion.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            Assert.True(await PublicStatsService.ConvertLegacyCheckpointAsync(conversion, f.Clock, f.Event.Id));
            await tx.CommitAsync();
        }

        await using var verify = new ApplicationDbContext(options);
        var convertedCheckpoint = await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(EventStatsLuckCheckpoint.CurrentSchemaVersion, convertedCheckpoint.SchemaVersion);
        Assert.Equal("luck-percentile-kc-v2-conversion", convertedCheckpoint.AlgorithmVersion);
        Assert.Equal(1, convertedCheckpoint.ConvertedFromSchemaVersion);
        Assert.NotNull(convertedCheckpoint.ConvertedAt);
        Assert.Equal(originalCheckpoint.CalculatedAt, convertedCheckpoint.CalculatedAt);
        Assert.Equal(originalCheckpoint.FetchedAt, convertedCheckpoint.FetchedAt);
        Assert.Equal(originalCheckpoint.UpstreamUpdatedAt, convertedCheckpoint.UpstreamUpdatedAt);

        var convertedStats = await ReadStatsAsync(f);
        var convertedTile = await ReadTileActivityAsync(f);
        Assert.Equal(originalStats.Luck.Result.Percentage, convertedStats.Luck.Result.Percentage);
        Assert.NotEqual(legacy.Result.Percentage, convertedStats.Luck.Result.Percentage);
        Assert.Equal(2, convertedStats.Luck.Result.Received);
        Assert.Equal(1, convertedTile.Team.Result.Received);
        Assert.Equal(convertedStats.Luck.CalculatedAt, convertedTile.CalculatedAt);
        Assert.Null(convertedTile.Team.Result.Percentage);
        Assert.Equal(StatsLuckStatus.WaitingForActivityData, convertedTile.Team.Result.Status);

        await using (var retry = new ApplicationDbContext(options))
        {
            await using var tx = await retry.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            Assert.False(await PublicStatsService.ConvertLegacyCheckpointAsync(retry, f.Clock, f.Event.Id));
            await tx.CommitAsync();
        }
        await using (var afterRetry = new ApplicationDbContext(options))
            Assert.Equal(convertedCheckpoint.Payload, (await afterRetry.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).Payload);

        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 1, 12));
        var stale = await ReadStatsAsync(f);
        var staleTile = await ReadTileActivityAsync(f);
        Assert.True(stale.Luck.Stale); Assert.True(staleTile.Stale);
        Assert.Equal(3, stale.Value.Drops); Assert.Equal(2, stale.Luck.Result.Received);
        Assert.Equal(convertedStats.Luck.Result.Percentage, stale.Luck.Result.Percentage);
        Assert.Equal(convertedStats.Luck.CalculatedAt, stale.Luck.CalculatedAt);
        Assert.Null(staleTile.Team.Result.Percentage);
        Assert.Equal(StatsLuckStatus.WaitingForActivityData, staleTile.Team.Result.Status);

        static StatsLuckResult OldRatio(StatsLuckResult result) => result with
        { Percentage = result.Expected is > 0 ? (result.Received / result.Expected.Value - 1) * 100 : null };
    }

    [Fact]
    public async Task BoundedLuckLegacyConversionRejectsMalformedRetainedPayloadAndSupportsPartialScopes()
    {
        var f = await FullStatsFixtureAsync(players: 2);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        string originalPayload;
        EventStatsLuckCheckpoint originalCheckpoint;
        await using (var read = new ApplicationDbContext(options))
        {
            originalCheckpoint = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            originalPayload = originalCheckpoint.Payload;
        }
        var saved = JsonSerializer.Deserialize<StatsLuck>(originalPayload)!;
        var legacyLifecycleFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { f.Event.State, f.Event.ActualStartedAt, f.Event.ActualEndedAt, f.Event.FinalizedAt, f.Event.ArchivedAt, f.Event.ResultsPublished })))).ToLowerInvariant();
        var invalidMechanics = saved with
        {
            Sources = saved.Sources.Select((source, index) => index == 0 ? source with { Probability = 1.1m } : source).ToArray()
        };
        var negativeCounts = saved with { Result = saved.Result with { Received = -1 } };
        var nullSourceNode = System.Text.Json.Nodes.JsonNode.Parse(originalPayload)!.AsObject();
        nullSourceNode["Sources"]!.AsArray().Insert(0, null);
        var nullSource = nullSourceNode.ToJsonString();
        var nullTeamResultNode = System.Text.Json.Nodes.JsonNode.Parse(originalPayload)!.AsObject();
        nullTeamResultNode["Teams"]!.AsArray()[0]!.AsObject()["Result"] = null;
        var nullTeamResult = nullTeamResultNode.ToJsonString();
        var nullPlayerResultNode = System.Text.Json.Nodes.JsonNode.Parse(originalPayload)!.AsObject();
        nullPlayerResultNode["Teams"]!.AsArray()[0]!.AsObject()["Players"]!.AsArray()[0]!.AsObject()["Result"] = null;
        var nullPlayerResult = nullPlayerResultNode.ToJsonString();
        var malformedTilePayloads = new List<string>();
        foreach (var playerScope in new[] { false, true })
        {
            foreach (var malformedField in new[] { "missing-result", "null-result", "null-metrics", "null-metric" })
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(originalPayload)!.AsObject();
                var scope = node["Tiles"]!.AsArray()[0]!.AsObject()["Teams"]!.AsArray()[0]!.AsObject();
                if (playerScope) scope = scope["Players"]!.AsArray()[0]!.AsObject();
                switch (malformedField)
                {
                    case "missing-result": scope.Remove("Result"); break;
                    case "null-result": scope["Result"] = null; break;
                    case "null-metrics": scope["Metrics"] = null; break;
                    case "null-metric": scope["Metrics"]!.AsArray().Insert(0, null); break;
                }
                malformedTilePayloads.Add(node.ToJsonString());
            }
        }
        foreach (var malformed in new[]
        {
            "{}",
            """{"Result":null,"Teams":null,"Sources":null}""",
            """{"Result":null,"Result":null,"Teams":[],"Sources":[]}""",
            """{"Result":[],"Teams":[],"Sources":[]}""",
            JsonSerializer.Serialize(negativeCounts),
            JsonSerializer.Serialize(invalidMechanics),
            nullSource,
            nullTeamResult,
            nullPlayerResult
        }.Concat(malformedTilePayloads))
        {
            string persistedPayload;
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE event_stats_luck_checkpoints SET schema_version = 1, lifecycle_fingerprint = {legacyLifecycleFingerprint}, payload = CAST({malformed} AS jsonb)
                    WHERE event_id = {f.Event.Id}
                    """);
                persistedPayload = await setup.EventStatsLuckCheckpoints.AsNoTracking()
                    .Where(x => x.EventId == f.Event.Id).Select(x => x.Payload).SingleAsync();
            }
            await using (var conversion = new ApplicationDbContext(options))
            {
                await using var tx = await conversion.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var result = await PublicStatsService.ConvertLegacyCheckpointWithDiagnosticAsync(conversion, f.Clock, f.Event.Id);
                Assert.False(result.Converted, $"malformed payload was converted: {malformed}");
                Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic), $"missing diagnostic for malformed payload: {malformed}");
                Assert.InRange(result.Diagnostic!.Length, 1, 240);
                await tx.CommitAsync();
            }
            await using var verify = new ApplicationDbContext(options);
            var retained = await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            Assert.Equal(1, retained.SchemaVersion);
            Assert.Equal(persistedPayload, retained.Payload);
            Assert.Equal(originalCheckpoint.CalculatedAt, retained.CalculatedAt);
            Assert.Equal(originalCheckpoint.FetchedAt, retained.FetchedAt);
            Assert.Equal(originalCheckpoint.UpstreamUpdatedAt, retained.UpstreamUpdatedAt);
        }


        var partial = saved with
        {
            Teams = saved.Teams.Select(team => team with
            {
                Players = team.Players.Take(1).ToArray()
            }).ToArray()
        };
        var partialPayload = JsonSerializer.Serialize(partial);
        await using (var setup = new ApplicationDbContext(options))
            await setup.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE event_stats_luck_checkpoints SET schema_version = 1, lifecycle_fingerprint = {legacyLifecycleFingerprint}, payload = CAST({partialPayload} AS jsonb)
                WHERE event_id = {f.Event.Id}
                """);
        await using (var conversion = new ApplicationDbContext(options))
        {
            await using var tx = await conversion.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await PublicStatsService.ConvertLegacyCheckpointWithDiagnosticAsync(conversion, f.Clock, f.Event.Id);
            Assert.True(result.Converted, result.Diagnostic);
            await tx.CommitAsync();
        }
        await using var after = new ApplicationDbContext(options);
        var converted = await after.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(EventStatsLuckCheckpoint.CurrentSchemaVersion, converted.SchemaVersion);
        Assert.Equal(originalCheckpoint.CalculatedAt, converted.CalculatedAt);
        Assert.Equal(originalCheckpoint.FetchedAt, converted.FetchedAt);
        Assert.Equal(originalCheckpoint.UpstreamUpdatedAt, converted.UpstreamUpdatedAt);
    }

    private static void AssertLuckScore(decimal expected, StatsLuckResult result, decimal tolerance = .00000001m)
    {
        Assert.Equal(StatsLuckStatus.Calculated, result.Status);
        Assert.NotNull(result.Percentage);
        Assert.InRange(result.Percentage.Value, -100m, 100m);
        Assert.InRange(result.Percentage.Value, expected - tolerance, expected + tolerance);
    }

    private async Task<FullStatsFixture> FullStatsFixtureAsync(int rolls = 1, bool secondOutcome = false, int target = 10, int weight = 1,
        int players = 1, bool extraRegular = false, string metric = "vorkath", int dimensions = 2, int actualStartedHoursAgo = 1, int eventDurationHours = 11, DateTimeOffset? clockNow = null, string? secondMetric = null)
    {
        var clock = new TestClock(clockNow ?? new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)); var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "StatsAdmin", "STATSADMIN", now); admin.SetGlobalRole(GlobalRole.SuperAdmin);
        var actualStart = now.AddHours(-actualStartedHoursAgo);
        var ev = new BingoEvent(Guid.NewGuid(), "Synthetic Stats event", "stats-event", "", "UTC", actualStart.AddHours(-2), actualStart.AddHours(-1), actualStart, actualStart.AddHours(eventDurationHours), actualStart.AddHours(eventDurationHours), 20, admin.Id, now);
        ev.OpenSignups(actualStart.AddHours(-2)); ev.CloseSignups(actualStart.AddHours(-1)); ev.SetDraftRosterPublication(true); ev.SetBoardPublication(true, actualStart.AddHours(-1)); ev.MarkFirstPublic(actualStart.AddHours(-2)); ev.StartEvent(actualStart);
        var team = new Team(Guid.NewGuid(), ev.Id, "Public team", "public-team", TeamFormationType.Drafted, null, true); team.Finalize(actualStart.AddHours(-1));
        var participants = Enumerable.Range(0, players).Select(i => new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, i + 1, actualStart.AddHours(-2), SignupSource.Website)).ToArray();
        var chars = participants.Select((_, i) => new OsrsCharacter(Guid.NewGuid(), "Stats Player " + i, "STATS PLAYER " + i, now)).ToArray();
        var form = new SignupForm(Guid.NewGuid(), ev.Id, now.AddHours(-3));
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "primary", "Primary account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var assignments = participants.Select((p, i) => new EventParticipantCharacter(Guid.NewGuid(), ev.Id, p.Id, chars[i].Id, 0, actualStart.AddHours(-1), null, primary.Id, EventCharacterRole.Playing, 0, EhbSource.Manual, null)).ToList();
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1); draft.FinalizeDirect(actualStart.AddHours(-1));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, actualStart.AddHours(-1), admin.Id, DraftPublicationMethod.DirectRoster);
        var publishedRoster = participants.Select((participant, index) => new DraftPublicationRoster(
            Guid.NewGuid(), publication.Id, team.Id, participant.Id, TeamMembershipRole.Participant, null, chars[index].DisplayName)).ToArray();
        var regular = new OsrsCharacter(Guid.NewGuid(), "Regular two", "REGULAR TWO", now);
        if (extraRegular) assignments.Add(new(Guid.NewGuid(), ev.Id, participants[0].Id, regular.Id, 1, actualStart.AddHours(-1), null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null));
        var alt = new OsrsCharacter(Guid.NewGuid(), "Informational", "INFORMATIONAL", now);
        assignments.Add(new(Guid.NewGuid(), ev.Id, participants[0].Id, alt.Id, 2, actualStart.AddHours(-1), null, null, EventCharacterRole.Informational, null, null, null));
        var boss = new BossActivity(Guid.NewGuid(), "Vorkath fixture", "stats-source", "Boss", 10, now); boss.ConfigureApi(metric); boss.RecordMapping(ApiMappingStatus.Verified, now);
        var secondBoss = secondMetric is null ? boss : new BossActivity(Guid.NewGuid(), "Second boss fixture", "stats-second-source", "Boss", 10, now);
        if (secondMetric is not null) { secondBoss.ConfigureApi(secondMetric); secondBoss.RecordMapping(ApiMappingStatus.Verified, now); }
        var items = Enumerable.Range(0, secondOutcome ? 2 : 1).Select(i => { var item = new CatalogueItem(Guid.NewGuid(), "Exact variant " + i, "EXACT VARIANT " + i); item.SetPrice(100 + i * 100, CataloguePriceSource.Manual, now); item.Update(item.Name, item.NormalizedName, null, null, "https://oldschool.runescape.wiki/images/Dragon_warhammer.png"); return item; }).ToArray();
        var sources = items.Select((item, i) => new SourceDrop(Guid.NewGuid(), i == 0 ? boss.Id : secondBoss.Id, item.Id, i == 0 ? "1/100" : "1/200", i == 0 ? .01m : .005m, 1, now)).ToArray();
        var board = new Board(Guid.NewGuid(), ev.Id, "Stats board", dimensions, dimensions);
        var tiles = Enumerable.Range(0, dimensions * dimensions).Select(i => new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), i / dimensions, i % dimensions, "Tile " + i, "", "", 10)).ToArray();
        var requirements = tiles.Select((tile, i) => new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, target, true, true, "Objective " + i, i >= 2, weight)).ToArray();
        var drops = requirements.Take(2).SelectMany(req => sources.Select((source, i) => new BoardRequirementDropSnapshot(Guid.NewGuid(), req.Id, source.Id, items[i].Id, i == 0 ? boss.Name : secondBoss.Name, items[i].Name, source.DisplayRate, i == 0 ? .01m : .005m, null, 1, weight,
            DropProbabilityScope.Participant, false, null, 1, rolls))).ToArray();
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, ev, team, draft, publication, boss, board, regular, alt, form, primary); db.AddRange(participants); db.AddRange(chars); db.AddRange(assignments); db.AddRange(publishedRoster); db.AddRange(items); db.AddRange(sources); db.AddRange(tiles); db.AddRange(requirements); db.AddRange(drops);
        if (secondMetric is not null) db.Add(secondBoss);
        db.AddRange(participants.Select(p => new TeamMembership(Guid.NewGuid(), team.Id, p.Id, TeamMembershipRole.Participant, now.AddHours(-2), null, "Fixture")));
        await BoardApprovalFixture.PublishAsync(db, board, now.AddHours(-2), tiles, requirements, drops);
        // The common fixture helper predates personal rolls. Set fixture mechanics before first basis capture.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET rolls_per_completion = {rolls}");
        foreach (var req in await db.BoardApprovalRequirementSnapshots.Where(x => !x.ManualObjective).ToListAsync())
        {
            db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), req.Id, boss.Id, boss.Name, 10, 1));
            if (secondMetric is not null) db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), req.Id, secondBoss.Id, secondBoss.Name, 10, 1));
        }
        db.AddRange(items.Select(item => EventItemPrice.Introduce(ev.Id, item, new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddHours(-2), now)));
        await db.SaveChangesAsync();
        return new(ev, admin, team, participants, chars, assignments, alt, boss, items, tiles, requirements, drops, clock, secondMetric is null ? null : secondBoss);
    }

    private async Task<Guid> PendingStatsForDropAsync(FullStatsFixture f, int tile, int player, int minutes, Guid dropSnapshotId)
    {
        await using var db = new ApplicationDbContext(options);
        var teamId = await db.TeamMemberships.Where(x => x.EventParticipantId == f.Players[player].Id && x.LeftAt == null).Select(x => x.TeamId).SingleAsync();
        var workingDropId = await db.BoardRequirementDropSnapshots.Where(x => x.RequirementId == f.Requirements[tile].Id && x.SourceDropId == dropSnapshotId).Select(x => x.Id).SingleAsync();
        var s = new Submission(Guid.NewGuid(), f.Event.Id, teamId, f.Tiles[tile].Id, f.Requirements[tile].Id, workingDropId,
            f.Players[player].Id, f.Characters[player].Id, f.Characters[player].DisplayName, f.Admin.Id, 1,
            f.Event.ActualStartedAt!.Value.AddMinutes(minutes), null, null);
        db.Add(s); await db.SaveChangesAsync(); return s.Id;
    }

    private async Task<Guid> PendingStatsAsync(FullStatsFixture f, int tile, int player, int minutes, int weight = 1, int itemIndex = 0)
    {
        var drop = f.Drops.Where(x => x.RequirementId == f.Requirements[tile].Id).Skip(itemIndex).FirstOrDefault();
        await using var db = new ApplicationDbContext(options);
        var teamId = await db.TeamMemberships.Where(x => x.EventParticipantId == f.Players[player].Id && x.LeftAt == null).Select(x => x.TeamId).SingleAsync();
        var s = new Submission(Guid.NewGuid(), f.Event.Id, teamId, f.Tiles[tile].Id, f.Requirements[tile].Id, drop?.Id,
            f.Players[player].Id, f.Characters[player].Id, f.Characters[player].DisplayName, f.Admin.Id, weight,
            f.Event.ActualStartedAt!.Value.AddMinutes(minutes), null, null);
        db.Add(s); await db.SaveChangesAsync(); return s.Id;
    }
    private async Task ApproveStatsAsync(FullStatsFixture f, Guid submission)
    { await using var db = new ApplicationDbContext(options); await new SubmissionService(db, null!, f.Clock).ApproveCurrentAsync(submission, f.Admin.Id); }
    private async Task ReverseStatsAsync(FullStatsFixture f, Guid submission)
    { await using var db = new ApplicationDbContext(options); await new SubmissionService(db, null!, f.Clock).ReverseCurrentAsync(submission, f.Admin.Id, "Synthetic reversal"); }
    private async Task PublishCurrentRosterAsync(FullStatsFixture f, bool retainPreviousEntries = false)
    {
        await using var db = new ApplicationDbContext(options);
        var draft = await db.DraftSessions.SingleAsync(x => x.EventId == f.Event.Id);
        var current = await db.DraftPublicationCycles.SingleAsync(x => x.DraftSessionId == draft.Id && x.SupersededAt == null);
        var previousEntries = retainPreviousEntries
            ? await db.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == current.Id).ToListAsync()
            : [];
        current.Supersede(f.Clock.GetUtcNow(), f.Admin.Id, "Controlled fixture roster replacement");
        var cycleNumber = await db.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).MaxAsync(x => x.CycleNumber) + 1;
        var replacement = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, cycleNumber, f.Clock.GetUtcNow(), f.Admin.Id, DraftPublicationMethod.DirectRoster);
        db.Add(replacement);
        var names = f.Players.Select((player, index) => (player.Id, Name: f.Characters[index].DisplayName)).ToDictionary(x => x.Id, x => x.Name);
        var memberships = await db.TeamMemberships.Where(x => x.LeftAt == null && names.Keys.Contains(x.EventParticipantId)).ToListAsync();
        foreach (var member in memberships)
            db.Add(new DraftPublicationRoster(Guid.NewGuid(), replacement.Id, member.TeamId, member.EventParticipantId, member.Role, null, names[member.EventParticipantId]));
        foreach (var entry in previousEntries.Where(entry => !memberships.Any(member => member.TeamId == entry.TeamId && member.EventParticipantId == entry.EventParticipantId)))
            db.Add(new DraftPublicationRoster(Guid.NewGuid(), replacement.Id, entry.TeamId, entry.EventParticipantId, entry.Role, entry.EffectivePickNumber, entry.PublicCharacterName));
        await db.SaveChangesAsync();
    }
    private async Task<PublicEventStats> ReadStatsAsync(FullStatsFixture f)
    { await using var db = new ApplicationDbContext(options); return Assert.IsType<PublicEventStats>(await new PublicStatsService(db, f.Clock).GetAsync(f.Event.Slug)); }
    private Task SyncStatsAsync(FullStatsFixture f, int kills) => SyncStatsAsync(f, new WiseOldManMetricDelta(0, kills, kills));
    private Task SyncStatsAsync(FullStatsFixture f, WiseOldManMetricDelta? delta) => SyncStatsAsync(f, StatsResponse(f, delta));
    private static WiseOldManCompetitionResult StatsResponse(FullStatsFixture f, WiseOldManMetricDelta? delta) => new(WiseOldManCompetitionStatus.Success,
        new(42, "Controlled Stats competition", f.Event.EventStartsAt!.Value, f.Event.EventEndsAt!.Value, f.Clock.GetUtcNow(),
            f.Assignments.Select(a => new WiseOldManCompetitionParticipant(a.OsrsCharacterId.ToString(), "regular", 1, 0, 1,
                delta is null ? new Dictionary<string, WiseOldManMetricDelta>() : new Dictionary<string, WiseOldManMetricDelta> { [f.Boss.ExternalIdentifier!] = delta }, f.Clock.GetUtcNow().AddMinutes(-5))).ToArray()));
    private async Task SyncStatsAsync(FullStatsFixture f, WiseOldManCompetitionResult result, Func<Task>? before = null)
    {
        await using var db = new ApplicationDbContext(options);
        // Match the controlled provider payload to public character names only inside this fixture.
        var names = await db.OsrsCharacters.ToDictionaryAsync(x => x.Id, x => x.DisplayName);
        if (result.Competition is { } c) result = result with { Competition = c with { Participants = c.Participants.Select(p => p with { Username = names[Guid.Parse(p.Username)] }).ToArray() } };
        var client = new StatsClient(StatsResponse(f, new(0, 100, 100)), result, before);
        var service = new EventCompetitionSynchronizationService(db, client, new FixedStatus(), f.Clock);
        var actor = new LifecycleActor(f.Admin.Id, f.Admin.LoginName);
        if (!await db.EventCompetitionSynchronizations.AnyAsync()) Assert.True((await service.ConfigureAsync(f.Event.Id, (await db.Events.SingleAsync()).Version, 42, false, actor)).Succeeded);
        await service.RefreshAsync(f.Event.Id, actor);
    }
    private sealed record FullStatsFixture(BingoEvent Event, Account Admin, Team Team, EventParticipant[] Players, OsrsCharacter[] Characters,
        IReadOnlyList<EventParticipantCharacter> Assignments, OsrsCharacter Alt, BossActivity Boss, CatalogueItem[] Items, BoardTile[] Tiles,
        BoardRequirementSnapshot[] Requirements, BoardRequirementDropSnapshot[] Drops, TestClock Clock, BossActivity? SecondBoss);
    private sealed class StatsClient(WiseOldManCompetitionResult validation, WiseOldManCompetitionResult result, Func<Task>? before) : IWiseOldManCompetitionClient
    {
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default) => Task.FromResult(validation);
        public async Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default)
        { if (before is not null) await before(); return result; }
    }
}
