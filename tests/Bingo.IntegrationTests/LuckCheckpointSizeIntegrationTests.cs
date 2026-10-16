using System.Data;
using System.Text;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Stats;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bingo.IntegrationTests;

// Brief 178: the Luck checkpoint must fit its 8 MiB bound at production size (49+ characters x 173 outcomes
// on a 5x5 board) without truncation, and reads must reproduce the full calculation exactly.
public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    // The checkpoint serializer options before brief 178.
    private static readonly JsonSerializerOptions FormerCheckpointJsonOptions = new() { WriteIndented = true };
    private static readonly string[] LuckSizeMetrics =
    [
        "abyssal_sire", "alchemical_hydra", "artio", "barrows_chests", "bryophyta", "callisto", "calvarion", "cerberus",
        "chambers_of_xeric", "chaos_elemental", "chaos_fanatic", "commander_zilyana", "corporeal_beast", "crazy_archaeologist",
        "dagannoth_prime", "dagannoth_rex", "dagannoth_supreme", "deranged_archaeologist", "duke_sucellus", "general_graardor",
        "giant_mole", "grotesque_guardians", "hespori", "kalphite_queen", "king_black_dragon", "kraken", "kreearra",
        "kril_tsutsaroth", "nex", "nightmare", "obor", "phantom_muspah", "sarachnis", "scorpia", "skotizo", "spindel",
        "the_leviathan", "the_whisperer", "vardorvis", "vorkath", "zulrah"
    ];

    [Fact]
    public async Task LuckCheckpointAtProductionSizeIsWrittenWellUnderTheBoundAndReadsBackTheFullCalculation()
    {
        var f = await LuckSizeFixtureAsync(teams: 4, playersPerTeam: 13, bosses: 41, outcomes: 173, dimension: 5);
        await ApproveLuckSizeDropsAsync(f, 12);
        var logger = new CapturingLogger<EventCompetitionSynchronizationService>();
        await SyncLuckSizeAsync(f, logger);

        await using var db = new ApplicationDbContext(options);
        var checkpoint = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleOrDefaultAsync();
        Assert.True(checkpoint is not null, string.Join(" | ", logger.Entries.Select(x => x.Message)));
        var storedBytes = await db.Database.SqlQuery<int>($"SELECT octet_length(payload::text) AS \"Value\" FROM event_stats_luck_checkpoints").SingleAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var full = Assert.IsType<StatsLuck>(await PublicStatsService.CalculateAcceptedFetchLuckAsync(db, f.Clock, f.Event.Id));
        await tx.CommitAsync();
        var oldIndentedBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(full, FormerCheckpointJsonOptions));
        var newPayload = PublicStatsService.SerializeCheckpointPayload(full);
        var newBytes = PublicStatsService.CheckpointPayloadBytes(newPayload);
        Console.WriteLine($"brief178 production-size payload: old indented+duplicated={oldIndentedBytes} new compact={Encoding.UTF8.GetByteCount(newPayload)} new bound={newBytes} stored jsonb text={storedBytes}");

        Assert.Equal(52, full.Teams.Sum(x => x.Players.Count));
        Assert.Equal(173, full.Sources.Count);
        Assert.Equal(25, full.Tiles!.Count);
        Assert.Equal(52 * 173, full.Teams.Sum(x => x.Players.Sum(p => p.Sources.Count)));
        Assert.True(full.Tiles.Sum(t => t.Teams.Sum(x => x.Players.Sum(p => p.Sources!.Count(row => row.Received > 0)))) > 0);
        Assert.True(oldIndentedBytes > EventStatsLuckCheckpoint.MaximumPayloadBytes, $"old format {oldIndentedBytes} bytes");
        Assert.True(newBytes < 7 * 1024 * 1024, $"new format {newBytes} bytes");
        Assert.InRange(storedBytes, Encoding.UTF8.GetByteCount(newPayload), newBytes);
        // Headroom below 25% is reported on every accepted fetch, never silently.
        Assert.Equal(newBytes > EventStatsLuckCheckpoint.MaximumPayloadBytes * 0.75 ? 1 : 0, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Contains("above 75% of the 8388608 byte bound", entry.Message, StringComparison.Ordinal));

        // The saved checkpoint alone reproduces the full calculation, including every tile observation row.
        Assert.Equal(JsonSerializer.Serialize(full), JsonSerializer.Serialize(PublicStatsService.DeserializeCheckpointPayload(checkpoint.Payload, includeTiles: true)));
        var stats = await ReadStatsAsync(f.Stats);
        Assert.Equal(StatsLuckStatus.Calculated, stats.Luck.Result.Status);
        Assert.Equal(JsonSerializer.Serialize(full with { Tiles = null }), JsonSerializer.Serialize(stats.Luck));
        var tile = await ReadLuckSizeTileAsync(f, 0, 0);
        Assert.Equal(JsonSerializer.Serialize(full.Tiles.Single(t => t.TileId == f.Stats.Tiles[0].Id).Teams.Single(x => x.TeamId == f.Teams[0].Id)), JsonSerializer.Serialize(tile.Team));
    }

    [Fact]
    public async Task LuckCheckpointCompactFormReadsTheSameAsTheFullIndentedFormerFormat()
    {
        var f = await LuckSizeFixtureAsync(teams: 2, playersPerTeam: 3, bosses: 4, outcomes: 10, dimension: 3);
        await ApproveLuckSizeDropsAsync(f, 6);
        await SyncLuckSizeAsync(f);

        string compact;
        StatsLuck full;
        await using (var db = new ApplicationDbContext(options))
        {
            compact = (await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).Payload;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
            full = Assert.IsType<StatsLuck>(await PublicStatsService.CalculateAcceptedFetchLuckAsync(db, f.Clock, f.Event.Id));
            await tx.CommitAsync();
        }
        using (var document = JsonDocument.Parse(compact))
        {
            Assert.True(document.RootElement.TryGetProperty("TileObservations", out _));
            Assert.All(document.RootElement.GetProperty("Tiles").EnumerateArray().SelectMany(t => t.GetProperty("Teams").EnumerateArray())
                .SelectMany(t => t.GetProperty("Players").EnumerateArray()), player => Assert.Equal(JsonValueKind.Null, player.GetProperty("Sources").ValueKind));
        }
        var compactReads = await ReadAllLuckViewsAsync(f);

        // The former format: indented JSON with every tile observation row stored in full.
        var former = JsonSerializer.Serialize(full, FormerCheckpointJsonOptions);
        await using (var db = new ApplicationDbContext(options))
            Assert.Equal(1, await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE event_stats_luck_checkpoints SET payload = CAST({former} AS jsonb)"));
        var formerReads = await ReadAllLuckViewsAsync(f);

        Assert.Equal(formerReads.Count, compactReads.Count);
        Assert.Equal(formerReads, compactReads);
        Assert.Contains(compactReads, view => view.Contains("\"Received\":1", StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(full with { Tiles = null }), compactReads[0]);
    }

    [Fact]
    public async Task LuckCheckpointDiscardIsLoggedAsAWarningWithItsReasonAndSize()
    {
        var f = await LuckSizeFixtureAsync(teams: 2, playersPerTeam: 2, bosses: 2, outcomes: 4, dimension: 2);
        var logger = new CapturingLogger<EventCompetitionSynchronizationService>();
        await SyncLuckSizeAsync(f, logger, payloadByteLimit: 1000);

        await using var db = new ApplicationDbContext(options);
        Assert.Equal(0, await db.EventStatsLuckCheckpoints.CountAsync());
        Assert.True((await db.EventCompetitionSynchronizations.AsNoTracking().SingleAsync()).LastSuccessfulAt is not null);
        var warning = Assert.Single(logger.Entries, x => x.Level == LogLevel.Warning);
        Assert.Contains(f.Event.Id.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Matches(@"checkpoint-discarded: payload \d+ bytes exceeds 1000 byte bound", warning.Message);
        Assert.All(f.Stats.Characters, character => Assert.DoesNotContain(character.DisplayName, warning.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LuckCheckpointAboveThreeQuartersOfTheBoundIsWrittenAndLogged()
    {
        var f = await LuckSizeFixtureAsync(teams: 2, playersPerTeam: 2, bosses: 2, outcomes: 4, dimension: 2);
        var syncLogger = new CapturingLogger<EventCompetitionSynchronizationService>();
        await SyncLuckSizeAsync(f, syncLogger);
        Assert.Empty(syncLogger.Entries);

        var logger = new CapturingLogger<EventCompetitionSynchronizationService>();
        await using (var db = new ApplicationDbContext(options))
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var bytes = PublicStatsService.CheckpointPayloadBytes(PublicStatsService.SerializeCheckpointPayload(
                (await PublicStatsService.CalculateAcceptedFetchLuckAsync(db, f.Clock, f.Event.Id))!));
            Assert.Null(await PublicStatsService.PublishCheckpointAfterAcceptedFetchAsync(db, f.Clock, f.Event.Id, logger, payloadByteLimit: bytes));
            await tx.CommitAsync();
            var warning = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, warning.Level);
            Assert.Contains($"{bytes} bytes, above 75% of the {bytes} byte bound", warning.Message, StringComparison.Ordinal);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(1, await verify.EventStatsLuckCheckpoints.CountAsync());
    }

    private async Task<List<string>> ReadAllLuckViewsAsync(LuckSizeFixture f)
    {
        var views = new List<string> { JsonSerializer.Serialize((await ReadStatsAsync(f.Stats)).Luck) };
        for (var tile = 0; tile < f.Stats.Tiles.Length; tile++)
            for (var team = 0; team < f.Teams.Length; team++)
                views.Add(JsonSerializer.Serialize(await ReadLuckSizeTileAsync(f, tile, team)));
        return views;
    }

    private async Task<StatsTileActivity> ReadLuckSizeTileAsync(LuckSizeFixture f, int tile, int team)
    {
        await using var db = new ApplicationDbContext(options);
        return Assert.IsType<StatsTileActivity>(await new PublicStatsService(db, f.Clock).GetTileAsync(f.Event.Slug, f.Teams[team].Id, f.Stats.Tiles[tile].Id));
    }

    private async Task ApproveLuckSizeDropsAsync(LuckSizeFixture f, int count)
    {
        // Spread approved drops over tiles, players and teams so tile numerators differ from event numerators.
        for (var i = 0; i < count; i++)
            await ApproveStatsAsync(f.Stats, await PendingStatsAsync(f.Stats, i % f.Stats.Tiles.Length, (i * 5) % f.Stats.Players.Length, 10 + i, itemIndex: i % 2));
    }

    private async Task SyncLuckSizeAsync(LuckSizeFixture f, ILogger<EventCompetitionSynchronizationService>? logger = null, int? payloadByteLimit = null,
        Func<int, int, int>? kills = null)
    {
        await using var db = new ApplicationDbContext(options);
        var participants = f.Stats.Assignments.Where(a => a.EventRole == EventCharacterRole.Playing).Select((a, index) =>
            new WiseOldManCompetitionParticipant(f.Stats.Characters.Single(c => c.Id == a.OsrsCharacterId).DisplayName, "regular", 1, 0, 1,
                f.Bosses.Select((boss, b) => (boss.ExternalIdentifier!, Kills: kills?.Invoke(index, b) ?? 20 + (index * 37 + b * 11) % 400))
                    .ToDictionary(x => x.Item1, x => new WiseOldManMetricDelta(0, x.Kills, x.Kills)),
                f.Clock.GetUtcNow().AddMinutes(-5))).ToArray();
        var result = new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Success, new(42, "Controlled Stats competition",
            f.Event.EventStartsAt!.Value, f.Event.EventEndsAt!.Value, f.Clock.GetUtcNow(), participants));
        var service = new EventCompetitionSynchronizationService(db, new StatsClient(result, result, null), new FixedStatus(), f.Clock, logger)
        {
            CheckpointPayloadByteLimit = payloadByteLimit
        };
        var actor = new LifecycleActor(f.Stats.Admin.Id, f.Stats.Admin.LoginName);
        Assert.True((await service.ConfigureAsync(f.Event.Id, (await db.Events.SingleAsync()).Version, 42, false, actor)).Succeeded);
        var refresh = await service.RefreshAsync(f.Event.Id, actor);
        Assert.True(refresh.Succeeded, refresh.Message);
    }

    private async Task<LuckSizeFixture> LuckSizeFixtureAsync(int teams, int playersPerTeam, int bosses, int outcomes, int dimension, bool interleavedBosses = false)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)); var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "StatsAdmin", "STATSADMIN", now); admin.SetGlobalRole(GlobalRole.SuperAdmin);
        var actualStart = now.AddHours(-1);
        var ev = new BingoEvent(Guid.NewGuid(), "Synthetic Luck size event", "luck-size-event", "", "UTC", actualStart.AddHours(-2), actualStart.AddHours(-1), actualStart, actualStart.AddHours(11), actualStart.AddHours(11), 200, admin.Id, now);
        ev.OpenSignups(actualStart.AddHours(-2)); ev.CloseSignups(actualStart.AddHours(-1)); ev.SetDraftRosterPublication(true); ev.SetBoardPublication(true, actualStart.AddHours(-1)); ev.MarkFirstPublic(actualStart.AddHours(-2)); ev.StartEvent(actualStart);
        var teamRows = Enumerable.Range(0, teams).Select(t =>
        {
            var team = new Team(Guid.NewGuid(), ev.Id, "Public team " + t, "public-team-" + t, TeamFormationType.Drafted, null, true);
            team.Finalize(actualStart.AddHours(-1));
            return team;
        }).ToArray();
        var players = teams * playersPerTeam;
        var participants = Enumerable.Range(0, players).Select(i => new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, i + 1, actualStart.AddHours(-2), SignupSource.Website)).ToArray();
        var chars = participants.Select((_, i) => new OsrsCharacter(Guid.NewGuid(), "Luck Player " + i, "LUCK PLAYER " + i, now)).ToArray();
        var form = new SignupForm(Guid.NewGuid(), ev.Id, now.AddHours(-3));
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "primary", "Primary account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var assignments = participants.Select((p, i) => new EventParticipantCharacter(Guid.NewGuid(), ev.Id, p.Id, chars[i].Id, 0, actualStart.AddHours(-1), null, primary.Id, EventCharacterRole.Playing, 0, EhbSource.Manual, null)).ToList();
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1); draft.FinalizeDirect(actualStart.AddHours(-1));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, actualStart.AddHours(-1), admin.Id, DraftPublicationMethod.DirectRoster);
        Team TeamOf(int player) => teamRows[player % teams];
        var publishedRoster = participants.Select((participant, index) => new DraftPublicationRoster(
            Guid.NewGuid(), publication.Id, TeamOf(index).Id, participant.Id, TeamMembershipRole.Participant, null, chars[index].DisplayName)).ToArray();
        var bossRows = Enumerable.Range(0, bosses).Select(b =>
        {
            var boss = new BossActivity(Guid.NewGuid(), "Luck boss " + b, "luck-boss-" + b, "Boss", 10, now);
            boss.ConfigureApi(LuckSizeMetrics[b]); boss.RecordMapping(ApiMappingStatus.Verified, now);
            return boss;
        }).ToArray();
        var items = Enumerable.Range(0, outcomes).Select(i =>
        {
            var item = new CatalogueItem(Guid.NewGuid(), "Luck item " + i, "LUCK ITEM " + i);
            item.SetPrice(100 + i, CataloguePriceSource.Manual, now);
            item.Update(item.Name, item.NormalizedName, null, null, "https://oldschool.runescape.wiki/images/Dragon_warhammer.png");
            return item;
        }).ToArray();
        // A boss's outcomes are adjacent, so a tile usually spans two or three bosses as on a real board.
        int BossOf(int outcome) => interleavedBosses ? outcome % bosses : outcome * bosses / outcomes;
        var sources = items.Select((item, i) => new SourceDrop(Guid.NewGuid(), bossRows[BossOf(i)].Id, item.Id, "1/1000", .001m, 1, now)).ToArray();
        var board = new Board(Guid.NewGuid(), ev.Id, "Luck board", dimension, dimension);
        var tiles = Enumerable.Range(0, dimension * dimension).Select(i => new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), i / dimension, i % dimension, "Tile " + i, "", "", 10)).ToArray();
        var requirements = tiles.Select((tile, i) => new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 10, true, true, "Objective " + i, false)).ToArray();
        // Every outcome is on at least one tile; neighbouring tiles share some outcomes, as on a real board.
        var perTile = (outcomes + tiles.Length - 1) / tiles.Length + 1;
        var drops = requirements.SelectMany((req, t) => Enumerable.Range(0, perTile).Select(k => (t * (perTile - 1) + k) % outcomes).Distinct().Select(i =>
            new BoardRequirementDropSnapshot(Guid.NewGuid(), req.Id, sources[i].Id, items[i].Id, bossRows[BossOf(i)].Name, items[i].Name, "1/1000", .001m, null, 1, 1,
                DropProbabilityScope.Participant, false, null, 1, 1))).ToArray();
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, ev, draft, publication, board, form, primary); db.AddRange(teamRows); db.AddRange(bossRows); db.AddRange(participants); db.AddRange(chars);
        db.AddRange(assignments); db.AddRange(publishedRoster); db.AddRange(items); db.AddRange(sources); db.AddRange(tiles); db.AddRange(requirements); db.AddRange(drops);
        db.AddRange(participants.Select((p, i) => new TeamMembership(Guid.NewGuid(), TeamOf(i).Id, p.Id, TeamMembershipRole.Participant, now.AddHours(-2), null, "Fixture")));
        await BoardApprovalFixture.PublishAsync(db, board, now.AddHours(-2), tiles, requirements, drops);
        var approvedDrops = await db.BoardApprovalRequirementDropSnapshots.AsNoTracking().ToListAsync();
        foreach (var req in await db.BoardApprovalRequirementSnapshots.Where(x => !x.ManualObjective).ToListAsync())
        {
            var bossIds = approvedDrops.Where(x => x.ApprovalRequirementSnapshotId == req.Id)
                .Select(x => sources.Single(s => s.Id == x.SourceDropId).BossActivityId).Distinct();
            foreach (var boss in bossRows.Where(b => bossIds.Contains(b.Id)))
                db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), req.Id, boss.Id, boss.Name, 10, 1));
        }
        db.AddRange(items.Select(item => EventItemPrice.Introduce(ev.Id, item, new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddHours(-2), now)));
        await db.SaveChangesAsync();
        var stats = new FullStatsFixture(ev, admin, teamRows[0], participants, chars, assignments, chars[0], bossRows[0], items, tiles, requirements, drops, clock, null);
        return new(stats, teamRows, bossRows);
    }

    private sealed record LuckSizeFixture(FullStatsFixture Stats, Team[] Teams, BossActivity[] Bosses)
    {
        public BingoEvent Event => Stats.Event;
        public TestClock Clock => Stats.Clock;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
