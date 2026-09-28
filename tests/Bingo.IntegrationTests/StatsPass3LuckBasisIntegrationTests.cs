using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task StatsPass3ApprovalRetainsFirstMechanicsDeduplicatesAndPropagatesFrozenRolls()
    {
        var (actor, _, boss, drops, eventId) = await PriceBoardFixtureAsync();
        await using (var configure = new ApplicationDbContext(options))
        {
            var source = await configure.BossActivities.SingleAsync(x => x.Id == boss.Id);
            source.ConfigureApi("vorkath"); source.RecordMapping(ApiMappingStatus.Verified, DateTimeOffset.UtcNow);
            var drop = await configure.SourceDrops.SingleAsync(x => x.Id == drops[0]);
            drop.SetRateMechanics(DropProbabilityScope.Team, true, .5m, 5, 3, "personal-rewards");
            await configure.SaveChangesAsync();
        }
        await using (var create = new ApplicationDbContext(options))
        {
            var page = Page(create, actor.Id); page.BoardVersion = 1; page.TileDraft = PriceTileInput(boss.Id, drops);
            page.TileDraft.Requirements.Add(PriceTileInput(boss.Id, [drops[0]]).Requirements.Single());
            await page.OnPostCreateTileAsync(eventId, default); Assert.Equal("committed", page.TempData["BoardTileOutcome"]);
        }
        await ApprovePriceBoardAsync(eventId, actor.Id);
        EventLuckOutcomeBasis first;
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(2, await verify.EventLuckOutcomeBases.CountAsync());
            first = await verify.EventLuckOutcomeBases.SingleAsync(x => x.SourceDropId == drops[0]);
            Assert.Equal(.1m, first.NumericProbability); Assert.Equal(3, first.RollsPerCompletion);
            Assert.Equal(DropProbabilityScope.Team, first.ProbabilityScope); Assert.Equal(5, first.AssumedParticipants);
            Assert.Equal(.5m, first.ParentProbability); Assert.Equal("personal-rewards", first.RollGroup);
            Assert.Equal("vorkath", first.Metric); Assert.Equal(2, first.SourceRevision);
            var board = await verify.Boards.SingleAsync();
            var publication = (await verify.ApprovalObjectivesAsync(board.Id, board.ActiveApprovalSnapshotId!.Value))!;
            Assert.Equal(2, publication.Drops.Count(x => x.SourceDropId == drops[0]));
            Assert.All(publication.Drops.Where(x => x.SourceDropId == drops[0]), x =>
            {
                Assert.Equal(3, x.RollsPerCompletion); Assert.Equal(5, x.AssumedParticipants); Assert.Equal(.1m, x.NumericProbability);
                Assert.True(x.ConditionalOnParent); Assert.Equal(.5m, x.ParentProbability);
            });
        }
        await PublishPriceBoardAsync(eventId, actor.Id); await StartPriceBoardAsync(eventId, actor.Id);
        await using (var dtoCheck = new ApplicationDbContext(options))
        {
            var slug = await dtoCheck.Events.Where(x => x.Id == eventId).Select(x => x.Slug).SingleAsync();
            var teamSlug = await (from roster in dtoCheck.DraftPublicationRosters
                                  join cycle in dtoCheck.DraftPublicationCycles on roster.DraftPublicationCycleId equals cycle.Id
                                  join draft in dtoCheck.DraftSessions on cycle.DraftSessionId equals draft.Id
                                  join team in dtoCheck.Teams on roster.TeamId equals team.Id
                                  where draft.EventId == eventId && cycle.SupersededAt == null
                                  select team.Slug).SingleAsync();
            var tileId = await dtoCheck.BoardTiles.Select(x => x.Id).SingleAsync();
            var dto = await new PublicBoardService(dtoCheck, TimeProvider.System).GetTileAsync(slug, teamSlug, tileId);
            Assert.NotNull(dto);
            Assert.All(dto.Requirements.SelectMany(x => x.EligibleDrops).Where(x => x.RollGroup == "personal-rewards"), x =>
            {
                Assert.Equal(3, x.RollsPerCompletion); Assert.Equal(5, x.AssumedParticipants); Assert.Equal(.1m, x.NumericProbability);
                Assert.True(x.ConditionalOnParent); Assert.Equal(.5m, x.ParentProbability);
            });
            Assert.Equal(2, dto.Requirements.SelectMany(x => x.EligibleDrops).Count(x => x.RollGroup == "personal-rewards"));
        }
        await using (var edit = new ApplicationDbContext(options))
        {
            var source = await edit.BossActivities.SingleAsync(x => x.Id == boss.Id);
            source.ConfigureApi("zulrah"); source.RecordMapping(ApiMappingStatus.Verified, DateTimeOffset.UtcNow); source.AdvanceVersion();
            var drop = await edit.SourceDrops.SingleAsync(x => x.Id == drops[0]);
            drop.Update("1/5", .2m, "Later change", 1, "Synthetic", DateTimeOffset.UtcNow);
            drop.SetRateMechanics(DropProbabilityScope.Participant, false, null, 1, 5, "changed"); drop.AdvanceVersion();
            await edit.SaveChangesAsync();
        }
        await PreparePriceCorrectionAsync(eventId, actor.Id, boss.Id, drops); await ApprovePriceCorrectionAsync(eventId, actor.Id);
        await using var final = new ApplicationDbContext(options);
        var retained = await final.EventLuckOutcomeBases.SingleAsync(x => x.SourceDropId == drops[0]);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first), System.Text.Json.JsonSerializer.Serialize(retained));
        Assert.Equal(2, await final.EventLuckOutcomeBases.CountAsync());
        var sources = await final.LuckSourceRequestAsync(eventId);
        Assert.Single(sources.Metrics); Assert.Equal("vorkath", sources.Metrics.Single());
        // The database, not only entity setters, protects the first basis.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => final.Database.ExecuteSqlInterpolatedAsync($"UPDATE event_luck_outcome_bases SET numeric_probability = 0.9 WHERE event_id = {eventId}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => final.Database.ExecuteSqlInterpolatedAsync($"UPDATE event_luck_outcome_bases SET metric = 'zulrah' WHERE event_id = {eventId}"));
    }

    [Fact]
    public async Task StatsPass3BasisFailureRollsBackTheRealApprovalAndItsAudit()
    {
        var (actor, _, boss, drops, eventId) = await PriceBoardFixtureAsync();
        await CreatePriceBoardTileAsync(eventId, actor.Id, boss.Id, drops);
        var displayed = await LoadBoardAsync(eventId, actor.Id);
        var failureOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new RejectLuckBasisSave()).Options;
        await using (var failure = new ApplicationDbContext(failureOptions))
        {
            var page = Page(failure, actor.Id); page.BoardVersion = displayed.BoardView!.Version; page.ApprovalCatalogueFingerprint = displayed.ApprovalCatalogueFingerprint;
            await page.OnPostApproveAsync(eventId, false, default);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Empty(await verify.BoardApprovalSnapshots.ToListAsync()); Assert.Empty(await verify.EventLuckOutcomeBases.ToListAsync());
            Assert.Empty(await verify.AuditEntries.Where(x => x.Action == "board.approved").ToListAsync());
            Assert.Equal(BoardState.Draft, (await verify.Boards.SingleAsync()).State);
        }
        await ApprovePriceBoardAsync(eventId, actor.Id);
        await using var retry = new ApplicationDbContext(options); Assert.Equal(2, await retry.EventLuckOutcomeBases.CountAsync());
    }

    private sealed class RejectLuckBasisSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<EventLuckOutcomeBasis>().Any(x => x.State == EntityState.Added)) throw new DbUpdateException("Controlled Luck basis failure");
            return ValueTask.FromResult(result);
        }
    }
}
