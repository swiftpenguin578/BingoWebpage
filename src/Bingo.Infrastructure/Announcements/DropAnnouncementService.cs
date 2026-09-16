using System.Data;
using Bingo.Application.Announcements;
using Bingo.Domain.Access;
using Bingo.Domain.Announcements;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Announcements;

public sealed class DropAnnouncementService(ApplicationDbContext db, TimeProvider time) : IDropAnnouncementService
{
    private static readonly TimeSpan ExpansionCooldown = TimeSpan.FromMinutes(2);

    public Task<DropAnnouncementSnapshot?> GetCurrentAsync(Guid accountId, int queueLimit = 25, CancellationToken cancellationToken = default)
        => GetCurrentAsync(accountId, queueLimit, 0, null, cancellationToken);

    public Task<DropAnnouncementSnapshot?> GetCurrentAsync(Guid accountId, int queueLimit, int queueOffset, CancellationToken cancellationToken = default)
        => GetCurrentAsync(accountId, queueLimit, queueOffset, null, cancellationToken);

    public async Task<DropAnnouncementSnapshot?> GetCurrentAsync(Guid accountId, int queueLimit, int queueOffset, long? snapshotSequence, CancellationToken cancellationToken = default)
    {
        var eventId = await (from ev in db.Events.AsNoTracking()
                             join participant in db.EventParticipants.AsNoTracking() on ev.Id equals participant.EventId
                             join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                             join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                             join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
                             where ev.HiddenAt == null && (ev.State == EventState.Live || ev.State == EventState.AwaitingFinalReview) && account.Id == accountId
                                   && account.AccountType == AccountType.WebsiteAccount && account.Active
                                   && participant.SignupStatus == SignupStatus.Confirmed && membership.LeftAt == null
                                   && team.EventId == ev.Id && team.Active
                             orderby ev.EventStartsAt descending
                             select (Guid?)ev.Id).FirstOrDefaultAsync(cancellationToken);
        return eventId is Guid id ? await GetAsync(accountId, id, queueLimit, queueOffset, snapshotSequence, cancellationToken) : null;
    }

    public Task<DropAnnouncementSnapshot?> GetAsync(Guid accountId, Guid eventId, int queueLimit = 25, CancellationToken cancellationToken = default)
        => GetAsync(accountId, eventId, queueLimit, 0, null, cancellationToken);

    public Task<DropAnnouncementSnapshot?> GetAsync(Guid accountId, Guid eventId, int queueLimit, int queueOffset, CancellationToken cancellationToken = default)
        => GetAsync(accountId, eventId, queueLimit, queueOffset, null, cancellationToken);

    public async Task<DropAnnouncementSnapshot?> GetAsync(Guid accountId, Guid eventId, int queueLimit, int queueOffset, long? snapshotSequence, CancellationToken cancellationToken = default)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, cancellationToken);
        if (ev is null || ev.State is not (EventState.Live or EventState.AwaitingFinalReview) || !await IsEligibleAsync(accountId, ev.Id, cancellationToken)) return null;
        var state = await db.DropAnnouncementAccountStates.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId && x.EventId == eventId, cancellationToken);
        queueLimit = Math.Clamp(queueLimit, 1, 100);
        queueOffset = Math.Max(queueOffset, 0);
        var sequence = Math.Clamp(snapshotSequence ?? ev.AnnouncementSequence, 0, ev.AnnouncementSequence);
        var eligibleIds = EligibleSubmissionIds(accountId, ev, sequence);
        var unackBannerIds = eligibleIds.Where(id => !db.DropAnnouncementAcknowledgements.Any(a => a.AccountId == accountId && a.EventId == eventId && a.SubmissionId == id && a.BannerAcknowledgedAt != null));
        var unackDropsIds = eligibleIds.Where(id => !db.DropAnnouncementAcknowledgements.Any(a => a.AccountId == accountId && a.EventId == eventId && a.SubmissionId == id && a.DropsAcknowledgedAt != null));
        var queueTotal = await unackBannerIds.CountAsync(cancellationToken);
        var newTotal = await unackDropsIds.CountAsync(cancellationToken);
        var queueIds = await OrderSubmissionIds(unackBannerIds, queueLimit, queueOffset, cancellationToken);
        var newIds = await OrderSubmissionIds(unackDropsIds, queueLimit, 0, cancellationToken);
        var queue = await EligibleEntriesAsync(accountId, ev, queueIds, sequence, cancellationToken);
        queue = queue.OrderByDescending(x => x.ApprovedAt).ThenByDescending(x => x.SubmissionId).ToList();
        return new DropAnnouncementSnapshot(ev.Id, ev.Slug, ev.AnnouncementGeneration, sequence, state?.ExpansionCooldownUntil, queue, newIds, queueTotal, newTotal);
    }

    public async Task<IReadOnlyList<Guid>> GetNewSubmissionIdsAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default)
    {
        var requested = submissionIds.Where(id => id != Guid.Empty).Distinct().Take(100).ToArray();
        if (requested.Length == 0) return [];
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, cancellationToken);
        if (ev is null || ev.State is not (EventState.Live or EventState.AwaitingFinalReview) || !await IsEligibleAsync(accountId, eventId, cancellationToken)) return [];
        return await EligibleSubmissionIds(accountId, ev, ev.AnnouncementSequence)
            .Where(id => requested.Contains(id) && !db.DropAnnouncementAcknowledgements.Any(a => a.AccountId == accountId && a.EventId == eventId && a.SubmissionId == id && a.DropsAcknowledgedAt != null))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ClaimAutomaticExpansionAsync(Guid accountId, Guid eventId, long? snapshotSequence = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var ev = await LockedEventAsync(eventId, cancellationToken);
            if (ev.State is not (EventState.Live or EventState.AwaitingFinalReview) || !await IsEligibleAsync(accountId, ev.Id, cancellationToken)) { await tx.CommitAsync(cancellationToken); return false; }
            var state = await GetOrCreateStateAsync(accountId, eventId, cancellationToken);
            var now = time.GetUtcNow().ToUniversalTime();
            if (state.ExpansionCooldownUntil > now) { await tx.CommitAsync(cancellationToken); return false; }
            var maximumOrdinal = Math.Clamp(snapshotSequence ?? ev.AnnouncementSequence, 0, ev.AnnouncementSequence);
            var boundary = await (from submission in db.Submissions.AsNoTracking()
                                  join team in db.Teams.AsNoTracking() on submission.TeamId equals team.Id
                                  where submission.EventId == ev.Id && submission.Status == SubmissionStatus.Approved && submission.ReviewedAt != null
                                        && submission.ReviewedAt >= ev.AnnouncementsTrackingStartedAt && submission.AnnouncementGeneration == ev.AnnouncementGeneration
                                        && submission.AnnouncementOrdinal > state.LastAutomaticExpansionOrdinal && submission.AnnouncementOrdinal <= maximumOrdinal
                                        && team.EventId == ev.Id && team.Active
                                        && db.EventParticipants.Any(recipient => recipient.EventId == ev.Id && recipient.AccountId == accountId && recipient.SignupStatus == SignupStatus.Confirmed
                                            && db.TeamMemberships.Any(membership => membership.EventParticipantId == recipient.Id && membership.LeftAt == null
                                                && membership.JoinedAt <= submission.ReviewedAt
                                                && db.Teams.Any(recipientTeam => recipientTeam.Id == membership.TeamId && recipientTeam.EventId == ev.Id && recipientTeam.Active)))
                                        && !db.DropAnnouncementAcknowledgements.Any(a => a.AccountId == accountId && a.EventId == eventId && a.SubmissionId == submission.Id && a.BannerAcknowledgedAt != null)
                                  select submission.AnnouncementOrdinal).MaxAsync(cancellationToken);
            if (boundary is null) { await tx.CommitAsync(cancellationToken); return false; }
            state.ClaimAutomaticExpansion(boundary.Value, now.Add(ExpansionCooldown));
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(CancellationToken.None);
            return true;
        }
        catch (Exception ex) when (IsSerializationConflict(ex))
        {
            return false;
        }
    }

    public Task DismissAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default)
        => AcknowledgeAndCooldownAsync(accountId, eventId, submissionIds, cancellationToken);

    public async Task DismissSnapshotAsync(Guid accountId, Guid eventId, int generation, long snapshotSequence, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ev = await LockedEventAsync(eventId, cancellationToken);
        if (ev.State is not (EventState.Live or EventState.AwaitingFinalReview) || ev.AnnouncementGeneration != generation || snapshotSequence < 0 || !await IsEligibleAsync(accountId, eventId, cancellationToken)) { await tx.CommitAsync(cancellationToken); return; }
        var ids = await EligibleSubmissionIds(accountId, ev, Math.Min(snapshotSequence, ev.AnnouncementSequence))
            .Where(id => !db.DropAnnouncementAcknowledgements.Any(a => a.AccountId == accountId && a.EventId == eventId && a.SubmissionId == id && a.BannerAcknowledgedAt != null))
            .ToListAsync(cancellationToken);
        await AcknowledgeRowsAsync(accountId, eventId, ids, banner: true, drops: false, cancellationToken);
        var state = await GetOrCreateStateAsync(accountId, eventId, cancellationToken);
        state.SetExpansionCooldown(time.GetUtcNow().ToUniversalTime().Add(ExpansionCooldown));
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(CancellationToken.None);
    }

    public Task AcknowledgeBannerAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default)
        => AcknowledgeAsync(accountId, eventId, submissionIds, banner: true, drops: false, cancellationToken);

    public Task AcknowledgeDropsAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default)
        => AcknowledgeAsync(accountId, eventId, submissionIds, banner: false, drops: true, cancellationToken);

    public Task AcknowledgeBothAsync(Guid accountId, Guid eventId, Guid submissionId, CancellationToken cancellationToken = default)
        => AcknowledgeAsync(accountId, eventId, [submissionId], banner: true, drops: true, cancellationToken);

    public async Task ClearAllNewAsync(Guid accountId, Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ev = await LockedEventAsync(eventId, cancellationToken);
        if (ev.State is not (EventState.Live or EventState.AwaitingFinalReview) || !await IsEligibleAsync(accountId, eventId, cancellationToken)) { await tx.CommitAsync(cancellationToken); return; }
        var ids = await EligibleSubmissionIds(accountId, ev, ev.AnnouncementSequence)
            .Where(id => !db.DropAnnouncementAcknowledgements.Any(a => a.AccountId == accountId && a.EventId == eventId && a.SubmissionId == id && a.DropsAcknowledgedAt != null))
            .ToListAsync(cancellationToken);
        await AcknowledgeRowsAsync(accountId, eventId, ids, banner: true, drops: true, cancellationToken);
        await tx.CommitAsync(CancellationToken.None);
    }

    private async Task AcknowledgeAndCooldownAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ev = await LockedEventAsync(eventId, cancellationToken);
        if (ev.State is not (EventState.Live or EventState.AwaitingFinalReview) || !await IsEligibleAsync(accountId, eventId, cancellationToken)) { await tx.CommitAsync(cancellationToken); return; }
        await AcknowledgeRowsAsync(accountId, eventId, submissionIds, banner: true, drops: false, cancellationToken);
        var state = await GetOrCreateStateAsync(accountId, eventId, cancellationToken);
        state.SetExpansionCooldown(time.GetUtcNow().ToUniversalTime().Add(ExpansionCooldown));
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(CancellationToken.None);
    }

    private async Task AcknowledgeAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, bool banner, bool drops, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ev = await LockedEventAsync(eventId, cancellationToken);
        if (ev.State is not (EventState.Live or EventState.AwaitingFinalReview) || !await IsEligibleAsync(accountId, eventId, cancellationToken)) { await tx.CommitAsync(cancellationToken); return; }
        await AcknowledgeRowsAsync(accountId, eventId, submissionIds, banner, drops, cancellationToken);
        await tx.CommitAsync(CancellationToken.None);
    }

    private async Task AcknowledgeRowsAsync(Guid accountId, Guid eventId, IEnumerable<Guid> requestedIds, bool banner, bool drops, CancellationToken cancellationToken)
    {
        var ids = requestedIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return;
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId, cancellationToken);
        var eligible = await EligibleSubmissionIds(accountId, ev, ev.AnnouncementSequence).Where(id => ids.Contains(id)).ToListAsync(cancellationToken);
        var rows = await db.DropAnnouncementAcknowledgements.Where(x => x.AccountId == accountId && x.EventId == eventId && eligible.Contains(x.SubmissionId)).ToDictionaryAsync(x => x.SubmissionId, cancellationToken);
        var now = time.GetUtcNow().ToUniversalTime();
        foreach (var id in eligible)
        {
            if (!rows.TryGetValue(id, out var row))
            {
                row = new DropAnnouncementAcknowledgement(Guid.NewGuid(), accountId, eventId, id);
                db.DropAnnouncementAcknowledgements.Add(row);
                rows[id] = row;
            }
            if (banner) row.AcknowledgeBanner(now);
            if (drops) row.AcknowledgeDrops(now);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Guid> EligibleSubmissionIds(Guid accountId, BingoEvent ev, long maximumOrdinal)
        => from submission in db.Submissions.AsNoTracking()
           join team in db.Teams.AsNoTracking() on submission.TeamId equals team.Id
           where submission.EventId == ev.Id && submission.Status == SubmissionStatus.Approved && submission.ReviewedAt != null
                 && submission.ReviewedAt >= ev.AnnouncementsTrackingStartedAt && submission.AnnouncementGeneration == ev.AnnouncementGeneration
                 && submission.AnnouncementOrdinal != null && submission.AnnouncementOrdinal <= maximumOrdinal
                 && team.EventId == ev.Id && team.Active
                 && db.EventParticipants.Any(recipient => recipient.EventId == ev.Id && recipient.AccountId == accountId && recipient.SignupStatus == SignupStatus.Confirmed
                     && db.TeamMemberships.Any(membership => membership.EventParticipantId == recipient.Id && membership.LeftAt == null && membership.JoinedAt <= submission.ReviewedAt
                         && db.Teams.Any(recipientTeam => recipientTeam.Id == membership.TeamId && recipientTeam.EventId == ev.Id && recipientTeam.Active)))
           select submission.Id;

    private async Task<List<Guid>> OrderSubmissionIds(IQueryable<Guid> ids, int limit, int offset, CancellationToken cancellationToken)
        => await (from submission in db.Submissions.AsNoTracking()
                  join id in ids on submission.Id equals id
                  orderby submission.ReviewedAt descending, submission.Id descending
                  select submission.Id).Skip(offset).Take(limit).ToListAsync(cancellationToken);

    private async Task<List<DropAnnouncementEntry>> EligibleEntriesAsync(Guid accountId, BingoEvent ev, List<Guid> submissionIds, long maximumOrdinal, CancellationToken cancellationToken)
    {
        if (submissionIds.Count == 0) return [];
        var publication = await db.PublishedObjectivesAsync(ev.Id, cancellationToken);
        if (publication is null) return [];
        var tiles = publication.Tiles.ToDictionary(x => x.Id);
        var drops = publication.Drops.ToDictionary(x => x.Id);
        var targets = publication.Requirements.GroupBy(x => x.BoardTileId).ToDictionary(x => x.Key, x => x.Sum(requirement => requirement.TargetContribution));
        var artworkTiles = await (from tile in db.BoardApprovalTileSnapshots.AsNoTracking()
                                  join image in db.BoardTileImageAssets.AsNoTracking() on tile.BoardTileId equals image.BoardTileId
                                  where tile.ApprovalSnapshotId == publication.Approval.Id && tile.ArtworkReference != null
                                      && image.EventId == ev.Id && image.StorageKey == tile.ArtworkReference
                                  select tile.BoardTileId).ToHashSetAsync(cancellationToken);
        var itemIds = publication.Drops.Select(x => x.ItemIdSnapshot).Distinct().ToArray();
        var itemArtwork = await db.CatalogueItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ImageUrl, cancellationToken);
        var entries = await (from submission in db.Submissions.AsNoTracking()
                             join team in db.Teams.AsNoTracking() on submission.TeamId equals team.Id
                             join character in db.OsrsCharacters.AsNoTracking() on submission.CreditedOsrsCharacterId equals character.Id
                             join asset in db.EvidenceAssets.AsNoTracking().Where(x => x.Active) on submission.Id equals asset.SubmissionId into assets
                             where submission.EventId == ev.Id && submission.Status == SubmissionStatus.Approved && submission.ReviewedAt != null
                                   && submission.ReviewedAt >= ev.AnnouncementsTrackingStartedAt && submission.AnnouncementGeneration == ev.AnnouncementGeneration
                                   && submission.AnnouncementOrdinal != null && submission.AnnouncementOrdinal <= maximumOrdinal
                                   && team.EventId == ev.Id && team.Active
                                   && db.EventParticipants.Any(recipient => recipient.EventId == ev.Id && recipient.AccountId == accountId && recipient.SignupStatus == SignupStatus.Confirmed
                                       && db.TeamMemberships.Any(membership => membership.EventParticipantId == recipient.Id && membership.LeftAt == null && membership.JoinedAt <= submission.ReviewedAt
                                           && db.Teams.Any(recipientTeam => recipientTeam.Id == membership.TeamId && recipientTeam.EventId == ev.Id && recipientTeam.Active)))
                                   && submissionIds.Contains(submission.Id)
                             let progressAfter = (db.SubmissionContributions.AsNoTracking()
                                 .Where(contribution => contribution.TeamId == submission.TeamId && contribution.ReversedAt == null
                                     && db.Submissions.Any(previous => previous.Id == contribution.SubmissionId
                                         && previous.BoardTileId == submission.BoardTileId && previous.Status == SubmissionStatus.Approved
                                         && previous.ReviewedAt != null
                                         && (previous.ReviewedAt < submission.ReviewedAt
                                             || (previous.ReviewedAt == submission.ReviewedAt && previous.Id.CompareTo(submission.Id) <= 0))))
                                 .Sum(x => (int?)x.Amount) ?? 0)
                             select new
                             {
                                 Submission = submission,
                                 TeamName = team.Name,
                                 TeamSlug = team.Slug,
                                 PlayerName = character.DisplayName,
                                 EvidenceAssetId = assets.Select(x => (Guid?)x.Id).FirstOrDefault(),
                                 ProgressAfter = progressAfter
                             }).ToListAsync(cancellationToken);
        return entries.Where(x => tiles.ContainsKey(x.Submission.BoardTileId)).Select(entry =>
        {
            var submission = entry.Submission;
            var tile = tiles[submission.BoardTileId];
            var drop = submission.DropSnapshotId is { } dropId ? drops.GetValueOrDefault(dropId) : null;
            var target = targets.GetValueOrDefault(tile.Id);
            return new DropAnnouncementEntry(submission.Id, tile.Id, tile.NameSnapshot,
                artworkTiles.Contains(tile.Id) ? $"/Events/{Uri.EscapeDataString(ev.Slug)}/Board/Tiles/{tile.Id}/Image" : null,
                drop?.ItemIdSnapshot, drop is null ? null : itemArtwork.GetValueOrDefault(drop.ItemIdSnapshot), entry.TeamName, entry.TeamSlug, entry.PlayerName,
                drop?.BossName, drop?.ItemName, submission.ApprovedContribution, submission.ReviewedAt!.Value, entry.EvidenceAssetId,
                submission.CompletedTileAtApproval == true, Math.Min(target, entry.ProgressAfter), target);
        }).ToList();
    }

    private async Task<BingoEvent> LockedEventAsync(Guid eventId, CancellationToken cancellationToken)
        => await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Event not found.");

    private async Task<DropAnnouncementAccountState> GetOrCreateStateAsync(Guid accountId, Guid eventId, CancellationToken cancellationToken)
    {
        var state = await db.DropAnnouncementAccountStates.SingleOrDefaultAsync(x => x.AccountId == accountId && x.EventId == eventId, cancellationToken);
        if (state is not null) return state;
        state = new DropAnnouncementAccountState(Guid.NewGuid(), accountId, eventId);
        db.DropAnnouncementAccountStates.Add(state);
        return state;
    }

    private Task<bool> IsEligibleAsync(Guid accountId, Guid eventId, CancellationToken cancellationToken)
        => (from account in db.Accounts.AsNoTracking()
            join participant in db.EventParticipants.AsNoTracking() on account.Id equals participant.AccountId
            join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
            join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
            where account.Id == accountId && account.AccountType == AccountType.WebsiteAccount && account.Active
                  && participant.EventId == eventId && participant.SignupStatus == SignupStatus.Confirmed && membership.LeftAt == null
                  && team.EventId == eventId && team.Active
            select account.Id).AnyAsync(cancellationToken);

    private static bool IsSerializationConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) return true;
        return false;
    }
}
