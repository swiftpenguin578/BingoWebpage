using Bingo.Domain.Events;

namespace Bingo.Application.Announcements;

public interface IDropAnnouncementService
{
    Task<DropAnnouncementSnapshot?> GetAsync(Guid accountId, Guid eventId, int queueLimit = 25, CancellationToken cancellationToken = default);
    Task<DropAnnouncementSnapshot?> GetAsync(Guid accountId, Guid eventId, int queueLimit, int queueOffset, CancellationToken cancellationToken = default);
    Task<DropAnnouncementSnapshot?> GetAsync(Guid accountId, Guid eventId, int queueLimit, int queueOffset, long? snapshotSequence, CancellationToken cancellationToken = default);
    Task<DropAnnouncementSnapshot?> GetCurrentAsync(Guid accountId, int queueLimit = 25, CancellationToken cancellationToken = default);
    Task<DropAnnouncementSnapshot?> GetCurrentAsync(Guid accountId, int queueLimit, int queueOffset, CancellationToken cancellationToken = default);
    Task<DropAnnouncementSnapshot?> GetCurrentAsync(Guid accountId, int queueLimit, int queueOffset, long? snapshotSequence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetNewSubmissionIdsAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default);
    Task<bool> ClaimAutomaticExpansionAsync(Guid accountId, Guid eventId, long? snapshotSequence = null, CancellationToken cancellationToken = default);
    Task DismissAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default);
    Task DismissSnapshotAsync(Guid accountId, Guid eventId, int generation, long snapshotSequence, CancellationToken cancellationToken = default);
    Task AcknowledgeBannerAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default);
    Task AcknowledgeDropsAsync(Guid accountId, Guid eventId, IReadOnlyCollection<Guid> submissionIds, CancellationToken cancellationToken = default);
    Task AcknowledgeBothAsync(Guid accountId, Guid eventId, Guid submissionId, CancellationToken cancellationToken = default);
    Task ClearAllNewAsync(Guid accountId, Guid eventId, CancellationToken cancellationToken = default);
}

public sealed record DropAnnouncementSnapshot(
    Guid EventId,
    string EventSlug,
    int Generation,
    long SnapshotSequence,
    DateTimeOffset? ExpansionCooldownUntil,
    IReadOnlyList<DropAnnouncementEntry> Queue,
    IReadOnlyList<Guid> NewSubmissionIds,
    int QueueTotalCount,
    int NewCount)
{
    public bool HasQueue => Queue.Count > 0;
}

public sealed record DropAnnouncementEntry(
    Guid SubmissionId,
    Guid TileId,
    string TileName,
    string? TileArtworkReference,
    Guid? ItemIdSnapshot,
    string? ItemArtworkReference,
    string TeamName,
    string TeamSlug,
    string? PlayerName,
    string? BossName,
    string? DropName,
    int Contribution,
    DateTimeOffset ApprovedAt,
    Guid? EvidenceAssetId,
    bool CompletedTileAtApproval,
    int ProgressAfter,
    int Target);
