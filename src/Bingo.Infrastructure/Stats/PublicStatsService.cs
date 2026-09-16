using System.Data;
using Bingo.Application.Boards;
using Bingo.Application.Catalogue;
using Bingo.Application.Stats;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bingo.Infrastructure.Stats;

public sealed partial class PublicStatsService(ApplicationDbContext db, TimeProvider time) : IPublicStatsService
{
    public async Task<PublicEventStats?> GetAsync(string eventSlug, CancellationToken cancellationToken = default)
    {
        RequireConsistentTransaction(db);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        var data = await ReadDataAsync(eventSlug, cancellationToken);
        if (data is null) return null;
        var luck = await ReadLuckAsync(data, cancellationToken);
        return Project(data, luck);
    }

    private static void RequireConsistentTransaction(ApplicationDbContext context)
    {
        if (context.Database.CurrentTransaction is { } current &&
            current.GetDbTransaction().IsolationLevel is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new InvalidOperationException("Stats requires a consistent database snapshot.");
    }

    private async Task<StatsData?> ReadDataAsync(string slug, CancellationToken ct)
    {
        // Published board is the existing public Board boundary. FirstPublicAt additionally fences
        // inconsistent private fixtures; the one reconstructed import has no actual item history.
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug && x.HiddenAt == null &&
            x.FirstPublicAt != null && x.BoardPublished && x.State != EventState.Cancelled && x.State != EventState.Discarded &&
            x.Slug != "det-store-danske-sommerbingo-2026", ct);
        if (ev is null) return null;
        var board = await new PublicBoardService(db, time).GetEventBoardAsync(slug, 0, ct);
        if (board is null) return null;
        var publication = await db.PublishedObjectivesAsync(ev.Id, ct);
        if (publication is null) return null;
        var definitions = publication.Tiles.Select(tile => new ProgressTileDefinition(tile.Id, tile.RowIndex, tile.ColumnIndex,
            tile.EstimatedEhbSnapshot, publication.Requirements.Where(x => x.BoardTileId == tile.Id)
                .Select(x => new ProgressRequirementDefinition(x.Id, x.Position, x.TargetContribution, x.DuplicatesAllowed)).ToArray())).ToArray();
        var teamIds = board.Teams.Select(x => x.TeamId).ToArray();
        var requirementIds = publication.Requirements.Select(x => x.Id).ToArray();
        var evidence = await (from contribution in db.SubmissionContributions.AsNoTracking()
                              join submission in db.Submissions.AsNoTracking() on contribution.SubmissionId equals submission.Id
                              where submission.EventId == ev.Id && submission.Status == SubmissionStatus.Approved && contribution.ReversedAt == null &&
                                  teamIds.Contains(contribution.TeamId) && requirementIds.Contains(contribution.RequirementId)
                              select new StatsEvidence(submission, contribution)).ToListAsync(ct);
        var prices = await db.EventItemPrices.AsNoTracking().Where(x => x.EventId == ev.Id).ToDictionaryAsync(x => x.ItemId, ct);
        var itemIds = publication.Drops.Select(x => x.ItemIdSnapshot).Distinct().ToArray();
        var artwork = await db.CatalogueItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ImageUrl, ct);
        var byDrop = publication.Drops.ToDictionary(x => x.Id);
        var drops = evidence.DistinctBy(x => x.Submission.Id).Where(x => x.Submission.DropSnapshotId is { } id && byDrop.ContainsKey(id))
            .Select(x =>
            {
                var submission = x.Submission; var drop = byDrop[submission.DropSnapshotId!.Value];
                var price = prices.GetValueOrDefault(drop.ItemIdSnapshot);
                return new StatsItemDrop(submission.Id, submission.TeamId, submission.CreditedParticipantId,
                    submission.CreditedOsrsCharacterId, submission.CreditedCharacterName, submission.BoardTileId,
                    drop.SourceDropId, new(drop.ItemIdSnapshot, drop.ItemName, OsrsWikiImageUrl.Normalize(artwork.GetValueOrDefault(drop.ItemIdSnapshot))),
                    drop.BossName, submission.SubmittedAt, price?.ValueGp, price?.SelectedHour, price?.CapturedAt, price?.Source);
            }).OrderBy(x => x.SubmittedAt).ThenBy(x => x.SubmissionId).ToArray();
        var assignments = await db.EventParticipantCharacters.AsNoTracking()
            .Where(x => x.EventId == ev.Id && x.EventRole == EventCharacterRole.Playing && x.ReleasedAt == null &&
                db.EventParticipants.Any(p => p.Id == x.EventParticipantId && p.SignupStatus == SignupStatus.Confirmed))
            .Select(x => new StatsAssignment(x.EventParticipantId, x.OsrsCharacterId)).ToListAsync(ct);
        var publicRoster = board.RosterPlayers ?? [];
        var memberships = await (from member in db.TeamMemberships.AsNoTracking()
                                 join participant in db.EventParticipants.AsNoTracking() on member.EventParticipantId equals participant.Id
                                 where teamIds.Contains(member.TeamId) && member.LeftAt == null && participant.SignupStatus == SignupStatus.Confirmed
                                 select new { member.TeamId, PlayerId = participant.Id }).ToListAsync(ct);
        // An unavailable primary/activity assignment must not silently remove a required player
        // from a complete team score. Generic public wording reveals no missing account name.
        var roster = memberships.Select(member => publicRoster.FirstOrDefault(x => x.TeamId == member.TeamId && x.PlayerId == member.PlayerId)
            ?? new PublicRosterPlayer(member.TeamId, board.Teams.Single(x => x.TeamId == member.TeamId).TeamName, member.PlayerId,
                evidence.FirstOrDefault(x => x.Submission.CreditedParticipantId == member.PlayerId)?.Submission.CreditedCharacterName ?? "Participant", [])).ToArray();
        var final = ev.ResultsPublished ? await db.EventFinalizations.AsNoTracking()
            .Where(x => x.EventId == ev.Id && x.UnfinalizedAt == null).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct) : null;
        var official = final is null ? [] : await db.OfficialPlacements.AsNoTracking().Where(x => x.FinalizationId == final.Id).ToListAsync(ct);
        return new(ev, board, publication, definitions, evidence, drops, assignments,
            official.ToDictionary(x => x.TeamId, x => new StatsOfficialCompletion(x.BoardComplete, x.BoardCompletedAt, x.CompletedTiles, x.CompletedLines)),
            roster, publication.Drops.GroupBy(x => x.ItemIdSnapshot).ToDictionary(x => x.Key, x => new StatsItemIdentity(x.Key, x.First().ItemName, OsrsWikiImageUrl.Normalize(artwork.GetValueOrDefault(x.Key)))));
    }

    private static PublicEventStats Project(StatsData data, StatsLuck luck)
    {
        var total = Value(data.Drops);
        var teams = data.Board.Teams.Select(team =>
        {
            var drops = data.Drops.Where(x => x.TeamId == team.TeamId).ToArray(); var value = Value(drops);
            var evidence = data.Evidence.Where(x => x.Submission.TeamId == team.TeamId).ToArray();
            var roster = data.Roster.Where(x => x.TeamId == team.TeamId).ToArray();
            // Keep historical credited players as well as the entire current public roster.
            var playerIds = roster.Select(x => x.PlayerId).Concat(evidence.Select(x => x.Submission.CreditedParticipantId)).Distinct();
            var players = playerIds.Select(id =>
            {
                var player = roster.FirstOrDefault(x => x.PlayerId == id);
                var personal = drops.Where(x => x.PlayerId == id).ToArray(); var personalValue = Value(personal);
                return new StatsPlayer(id, player?.PlayerName ?? evidence.First(x => x.Submission.CreditedParticipantId == id).Submission.CreditedCharacterName,
                    player?.PlayingAccountNames ?? [], personalValue, Share(personalValue, value), Share(personalValue, total),
                    ValueHistory(personal), Valuable(personal), evidence.Where(x => x.Submission.CreditedParticipantId == id).Select(x => x.Submission.BoardTileId).Distinct().Count(), Repeated(personal));
            }).OrderByDescending(x => x.Value.KnownValueGp).ThenBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.PlayerId).ToArray();
            var history = ProgressHistory(data, evidence);
            return new StatsTeam(team.TeamId, team.TeamName, team.TeamSlug, value, Share(value, total), ValueHistory(drops), Valuable(drops),
                players, data.Official.GetValueOrDefault(team.TeamId) is { } official ? team.Progress with { BoardComplete = official.BoardComplete, BoardCompletedAt = official.CompletedAt } : team.Progress,
                history, Milestones(data, [team.TeamId], false), data.Official.GetValueOrDefault(team.TeamId), Repeated(drops), Versatile(players, team.TeamId));
        }).ToArray();
        var repeated = Repeated(data.Drops);
        var versatile = teams.SelectMany(team => team.Players.Select(player => new StatsVersatilePlayer(player.PlayerId, team.TeamId, player.Name, player.DistinctTiles)))
            .Where(x => x.DistinctTiles > 0).OrderByDescending(x => x.DistinctTiles).ThenBy(x => x.PlayerId).ThenBy(x => x.TeamId).FirstOrDefault();
        return new(data.Event.Id, data.Event.Name, data.Event.Slug, data.Event.Timezone, data.Event.State, data.Event.ActualStartedAt,
            data.Event.ActualEndedAt, data.Event.StatsEvidenceRevision, data.Publication.Approval.Id, total, teams, data.Drops, luck,
            Milestones(data, teams.Select(x => x.TeamId).ToArray()), repeated, versatile, data.Board.EventResult is { IsOfficial: true } officialResult ? officialResult : null,
            data.Board.Rows, data.Board.Columns, ValueHistory(data.Drops), Valuable(data.Drops), AggregateHistory(data, teams),
            data.Board.Teams.SelectMany(team => team.Tiles).DistinctBy(tile => tile.TileId)
                .Select(tile => new StatsTile(tile.TileId, tile.Name, tile.ImageUrl)).ToArray());
    }

    private static StatsRepeatedItem? Repeated(IEnumerable<StatsItemDrop> drops) => drops.GroupBy(x => x.Item.ItemId).OrderByDescending(x => x.Count()).ThenBy(x => x.Key)
        .Select(x => new StatsRepeatedItem(x.First().Item, x.Count())).FirstOrDefault();
    private static StatsVersatilePlayer? Versatile(IEnumerable<StatsPlayer> players, Guid teamId) => players.Where(x => x.DistinctTiles > 0)
        .OrderByDescending(x => x.DistinctTiles).ThenBy(x => x.PlayerId).Select(x => new StatsVersatilePlayer(x.PlayerId, teamId, x.Name, x.DistinctTiles)).FirstOrDefault();

    private static StatsValueTotal Value(IEnumerable<StatsItemDrop> drops)
    {
        var items = drops.ToArray(); var known = items.Sum(x => (decimal)(x.ValueGp ?? 0)); var missing = items.Count(x => x.ValueGp is null);
        return new(items.Length, known, missing, missing == 0 ? known : null);
    }
    private static decimal? Share(StatsValueTotal part, StatsValueTotal total) =>
        part.ValueGp is { } value && total.ValueGp is > 0 ? value / total.ValueGp.Value * 100 : null;
    private static StatsItemDrop? Valuable(IEnumerable<StatsItemDrop> drops) => drops.Where(x => x.ValueGp is not null)
        .OrderByDescending(x => x.ValueGp).ThenBy(x => x.SubmittedAt).ThenBy(x => x.SubmissionId).FirstOrDefault();
    private static StatsValuePoint[] ValueHistory(IReadOnlyList<StatsItemDrop> drops)
    {
        decimal known = 0; var missing = 0; var count = 0;
        return drops.Select(x =>
        {
            known += x.ValueGp ?? 0; if (x.ValueGp is null) missing++; count++;
            return new StatsValuePoint(x.SubmittedAt, x.SubmissionId, new(count, known, missing, missing == 0 ? known : null));
        }).ToArray();
    }

    private sealed record StatsAssignment(Guid PlayerId, Guid CharacterId);
    private sealed record StatsEvidence(Submission Submission, SubmissionContribution Contribution);
    private sealed record StatsData(BingoEvent Event, PublicEventBoard Board, PublishedBoardData Publication,
        IReadOnlyList<ProgressTileDefinition> Definitions, IReadOnlyList<StatsEvidence> Evidence, IReadOnlyList<StatsItemDrop> Drops,
        IReadOnlyList<StatsAssignment> Assignments, IReadOnlyDictionary<Guid, StatsOfficialCompletion> Official, IReadOnlyList<PublicRosterPlayer> Roster, IReadOnlyDictionary<Guid, StatsItemIdentity> Items);
}
