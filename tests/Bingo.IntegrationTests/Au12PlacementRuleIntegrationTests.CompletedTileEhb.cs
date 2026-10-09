using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

// H2: AU12 places teams by the expected EHB of complete tiles only. Fixture (2x2, no lines):
// tile 0 = 4 EHB / target 1, tile 1 = 20 EHB / target 4, tile 2 = 6 EHB / target 2, tile 3 = 1 EHB / target 4.
// Team A completes tile 0 and has 3/4 of tile 1 (Drop EHB 4 + 15 = 19, completed-tile EHB 4) and reaches
// its score first. Team B completes tile 2 with two submissions (Drop EHB 6, completed-tile EHB 6).
// Proportional credit would put A first under AU12; completed-tile EHB puts B first.
public sealed partial class Au12PlacementRuleIntegrationTests
{
    private static readonly DateTimeOffset H2SubmittedAt = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime)]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb)]
    public async Task H2RankingFinalReviewAndSnapshotUseCompletedTileEhbWhileDropEhbStaysProportional(PlacementRule rule)
    {
        var fixture = await SeedCompletedTileAsync(rule);
        await using var db = new ApplicationDbContext(options);
        var publicBoards = new PublicBoardService(db, new Clock());
        var board = (await publicBoards.GetEventBoardAsync("au12-h2-event"))!;
        var a = board.Teams.Single(x => x.TeamName == "Team A");
        var b = board.Teams.Single(x => x.TeamName == "Team B");
        var au12 = rule == PlacementRule.CreditedEhbThenScoreTime;

        // Public Drop EHB displays keep proportional credit for both rules.
        Assert.Equal(19m, a.Progress.EhbTiebreak);
        Assert.Equal(6m, b.Progress.EhbTiebreak);
        Assert.Equal(4m, a.Progress.CompletedTileEhb);
        Assert.Equal(6m, b.Progress.CompletedTileEhb);
        Assert.Equal(19m, board.DropEhbTeams!.Single(x => x.TeamName == "Team A").DropEhb);
        Assert.Equal(6m, board.DropEhbTeams!.Single(x => x.TeamName == "Team B").DropEhb);
        Assert.Equal(25m, board.RecentDropSummary!.TotalDropEhb);
        Assert.Equal(19m, board.RecentDropSummary.HighestDropEhbTeamValue);
        Assert.Equal(1, a.Progress.CompletedTiles);
        Assert.Equal(1, b.Progress.CompletedTiles);

        // Public rank: AU12 by completed-tile EHB (B), legacy by earlier score time (A).
        Assert.Equal(au12 ? 2 : 1, a.Rank);
        Assert.Equal(au12 ? 1 : 2, b.Rank);

        // Final review readiness shows the ranking value of the event's rule.
        var service = new EventFinalizationService(db, publicBoards, new Clock());
        var readiness = (await service.GetReadinessAsync(fixture.EventId))!;
        Assert.True(readiness.CanFinalize, string.Join(";", readiness.Blockers.Select(x => x.Description)));
        Assert.Equal(board.Teams.Select(x => (x.TeamId, x.Rank)), readiness.Placements.Select(x => (x.TeamId, x.Placement)));
        var expectedEhb = au12 ? (A: 4m, B: 6m) : (A: 19m, B: 6m);
        Assert.Equal(expectedEhb.A, readiness.Placements.Single(x => x.TeamName == "Team A").EhbTiebreak);
        Assert.Equal(expectedEhb.B, readiness.Placements.Single(x => x.TeamName == "Team B").EhbTiebreak);

        // The official snapshot stores the same ranking value, rows and JSON alike.
        Assert.True((await service.FinalizeAsync(fixture.EventId, new LifecycleActor(fixture.AdminId, "admin"), readiness.EventVersion)).Published);
        db.ChangeTracker.Clear();
        var official = await db.OfficialPlacements.OrderBy(x => x.TeamName).ToListAsync();
        Assert.Equal([("Team A", au12 ? 2 : 1, expectedEhb.A), ("Team B", au12 ? 1 : 2, expectedEhb.B)],
            official.Select(x => (x.TeamName, x.Placement, x.EhbTiebreak)));
        var snapshot = await db.EventFinalizations.SingleAsync();
        Assert.Equal(official.ToDictionary(x => x.TeamName, x => x.EhbTiebreak), SnapshotEhb(snapshot.CalculationInputsJson!, "teams"));
        using (var results = JsonDocument.Parse(snapshot.CalculationResultsJson!))
            Assert.Equal(official.ToDictionary(x => x.TeamName, x => x.EhbTiebreak),
                results.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("TeamName").GetString()!, x => x.GetProperty("EhbTiebreak").GetDecimal()));

        // Recomputing after finalization leaves the snapshot rows and the displayed result unchanged.
        var rowsBefore = official.Select(x => (x.Id, x.Placement, x.EhbTiebreak)).ToList();
        var after = (await new EventFinalizationService(db, new PublicBoardService(db, new Clock()), new Clock()).GetReadinessAsync(fixture.EventId))!;
        Assert.Equal(official.OrderBy(x => x.Placement).Select(x => (x.TeamId, x.Placement, x.EhbTiebreak)),
            after.Placements.OrderBy(x => x.Placement).Select(x => (x.TeamId, x.Placement, x.EhbTiebreak)));
        Assert.Equal(rowsBefore, await db.OfficialPlacements.OrderBy(x => x.TeamName).Select(x => new ValueTuple<Guid, int, decimal>(x.Id, x.Placement, x.EhbTiebreak)).ToListAsync());
    }

    [Fact]
    public async Task H2ExistingAu12SnapshotWithProportionalEhbIsNotRerankedOrRewritten()
    {
        var fixture = await SeedCompletedTileAsync(PlacementRule.CreditedEhbThenScoreTime);
        await using (var finalize = new ApplicationDbContext(options))
        {
            var service = new EventFinalizationService(finalize, new PublicBoardService(finalize, new Clock()), new Clock());
            var readiness = (await service.GetReadinessAsync(fixture.EventId))!;
            Assert.True((await service.FinalizeAsync(fixture.EventId, new LifecycleActor(fixture.AdminId, "admin"), readiness.EventVersion)).Published);
        }
        // Emulate a snapshot written before H2: proportional EHB placed Team A first.
        await using (var legacyWriter = new ApplicationDbContext(options))
        {
            await legacyWriter.Database.ExecuteSqlAsync($"UPDATE official_placements SET placement = 1, ehb_tiebreak = 19 WHERE team_name = 'Team A'");
            await legacyWriter.Database.ExecuteSqlAsync($"UPDATE official_placements SET placement = 2, ehb_tiebreak = 6 WHERE team_name = 'Team B'");
        }
        await using var db = new ApplicationDbContext(options);
        var stored = await db.OfficialPlacements.AsNoTracking().OrderBy(x => x.TeamName).Select(x => new { x.Id, x.Placement, x.EhbTiebreak }).ToListAsync();
        var inputs = (await db.EventFinalizations.AsNoTracking().SingleAsync()).CalculationInputsJson;
        var publicBoards = new PublicBoardService(db, new Clock());
        var board = (await publicBoards.GetEventBoardAsync("au12-h2-event"))!;
        Assert.Equal(1, board.Teams.Single(x => x.TeamName == "Team A").Rank);
        Assert.Equal(2, board.Teams.Single(x => x.TeamName == "Team B").Rank);
        var readinessAfter = (await new EventFinalizationService(db, publicBoards, new Clock()).GetReadinessAsync(fixture.EventId))!;
        Assert.Equal([("Team A", 1, 19m), ("Team B", 2, 6m)], readinessAfter.Placements.OrderBy(x => x.Placement).Select(x => (x.TeamName, x.Placement, x.EhbTiebreak)));
        Assert.Equal(stored, await db.OfficialPlacements.AsNoTracking().OrderBy(x => x.TeamName).Select(x => new { x.Id, x.Placement, x.EhbTiebreak }).ToListAsync());
        Assert.Equal(inputs, (await db.EventFinalizations.AsNoTracking().SingleAsync()).CalculationInputsJson);
    }

    [Fact]
    public async Task H2ReversedApprovalRemovesTileEhbFromRankingButKeepsProportionalDropEhb()
    {
        var fixture = await SeedCompletedTileAsync(PlacementRule.CreditedEhbThenScoreTime);
        await using var db = new ApplicationDbContext(options);
        var publicBoards = new PublicBoardService(db, new Clock());
        Assert.Equal(1, (await publicBoards.GetEventBoardAsync("au12-h2-event"))!.Teams.Single(x => x.TeamName == "Team B").Rank);

        // Reverse Team B's later tile-2 approval through the production review path.
        var later = await db.Submissions.AsNoTracking().Where(x => x.EventId == fixture.EventId && x.TeamId == fixture.TeamBId)
            .OrderByDescending(x => x.SubmittedAt).FirstAsync();
        await new SubmissionService(db, null!, new Clock()).ReverseAsync(later.Id, fixture.AdminId, "H2 reversal fixture", expectedVersion: later.Version);
        db.ChangeTracker.Clear();

        Assert.False((await db.TileCompletionFacts.AsNoTracking().SingleAsync(x => x.TeamId == fixture.TeamBId && x.BoardTileId == fixture.TileIds[2])).IsComplete);
        var board = (await publicBoards.GetEventBoardAsync("au12-h2-event"))!;
        var b = board.Teams.Single(x => x.TeamName == "Team B");
        Assert.Equal(0m, b.Progress.CompletedTileEhb);
        Assert.Equal(3m, b.Progress.EhbTiebreak);
        Assert.Equal(2, b.Rank);
        Assert.Equal(1, board.Teams.Single(x => x.TeamName == "Team A").Rank);
        var readiness = (await new EventFinalizationService(db, publicBoards, new Clock()).GetReadinessAsync(fixture.EventId))!;
        Assert.Equal(0m, readiness.Placements.Single(x => x.TeamName == "Team B").EhbTiebreak);
        Assert.Equal(4m, readiness.Placements.Single(x => x.TeamName == "Team A").EhbTiebreak);
    }

    private static Dictionary<string, decimal> SnapshotEhb(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty(property).EnumerateArray()
            .ToDictionary(x => x.GetProperty("TeamName").GetString()!, x => x.GetProperty("EhbTiebreak").GetDecimal());
    }

    private async Task<(Guid EventId, Guid AdminId, Guid TeamBId, Guid[] TileIds)> SeedCompletedTileAsync(PlacementRule rule)
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "au12-h2-admin", "AU12-H2-ADMIN", Now.AddDays(-10));
        admin.SetGlobalRole(GlobalRole.Admin);
        var ev = new BingoEvent(Guid.NewGuid(), "AU12 H2 event", "au12-h2-event", "UTC", admin.Id, Now.AddDays(-10), rule);
        ev.ConfigureSchedule(Now.AddDays(-8), Now.AddDays(-5), null, Now.AddHours(-4), Now.AddHours(-1), 20);
        ev.OpenSignups(Now.AddDays(-8)); ev.CloseSignups(Now.AddDays(-5)); ev.SetDraftRosterPublication(true);
        ev.StartEvent(Now.AddHours(-4)); ev.MarkFirstPublic(Now.AddDays(-8));
        var board = new Board(Guid.NewGuid(), ev.Id, "AU12 H2 board", 2, 2);
        decimal[] ehb = [4m, 20m, 6m, 1m];
        int[] targets = [1, 4, 2, 4];
        var tiles = Enumerable.Range(0, 4).Select(i => new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), i / 2, i % 2, "Tile " + i, "", "", ehb[i])).ToList();
        var requirements = tiles.Select((tile, i) => new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, targets[i], true, false, "Objective", true)).ToList();
        board.SetTotalEhb(ehb.Sum());
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1); draft.FinalizeDirect(Now.AddDays(-2));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, Now.AddDays(-2), admin.Id, DraftPublicationMethod.DirectRoster);
        db.AddRange(admin, ev, board, draft, publication); db.AddRange(tiles); db.AddRange(requirements);
        // (tile, amount, seconds after H2SubmittedAt); microsecond-aligned, deterministic UTC.
        (int Tile, int Amount, int Seconds)[][] credits =
        [
            [(0, 1, 0), (1, 3, 0)],
            [(2, 1, 1), (2, 1, 2)]
        ];
        var teamIds = new Guid[2];
        for (var index = 0; index < 2; index++)
        {
            var team = new Team(Guid.NewGuid(), ev.Id, index == 0 ? "Team A" : "Team B", "h2-team-" + index, TeamFormationType.Drafted, null, true); team.Finalize(Now.AddDays(-2));
            teamIds[index] = team.Id;
            var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, index + 1, Now.AddDays(-6), SignupSource.AdminCreated);
            var character = new OsrsCharacter(Guid.NewGuid(), "AU12 H2 Player " + index, "AU12 H2 PLAYER " + index, Now.AddDays(-6));
            db.AddRange(team, participant, character,
                new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, Now.AddDays(-6), admin.Id, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null),
                new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, Now.AddDays(-4), null, "AU12 H2 fixture"),
                new DraftPublicationRoster(Guid.NewGuid(), publication.Id, team.Id, participant.Id, TeamMembershipRole.Participant, null, character.DisplayName));
            foreach (var (tile, amount, seconds) in credits[index])
            {
                var submission = new Submission(Guid.NewGuid(), ev.Id, team.Id, tiles[tile].Id, requirements[tile].Id, null, participant.Id, character.Id, character.DisplayName, admin.Id, amount, H2SubmittedAt.AddSeconds(seconds), null, null);
                submission.Approve(amount, Now.AddHours(-2));
                db.AddRange(submission, new SubmissionContribution(Guid.NewGuid(), submission.Id, team.Id, requirements[tile].Id, null, participant.Id, amount, Now.AddHours(-2)));
            }
        }
        await BoardApprovalFixture.PublishAsync(db, board, Now.AddDays(-2), tiles, requirements);
        ev.EndEvent(Now.AddHours(-1));
        db.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, EventState.Live, EventState.AwaitingFinalReview, admin.Id, Now.AddHours(-1), "AU12 H2 fixture", effectiveAt: Now.AddHours(-1)));
        await db.SaveChangesAsync();
        // Persist tile completion facts as the review path does, so ranking reads reconciled facts.
        var published = (await db.PublishedObjectivesAsync(ev.Id))!;
        await TileCompletionFactReconciler.ReconcileAsync(db, ev.Id, published, teamIds, Now.AddHours(-2));
        await db.SaveChangesAsync();
        return (ev.Id, admin.Id, teamIds[1], tiles.Select(x => x.Id).ToArray());
    }
}
