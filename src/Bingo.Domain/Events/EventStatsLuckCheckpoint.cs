namespace Bingo.Domain.Events;

/// <summary>One complete public calculation. Never a denominator cache.</summary>
public sealed class EventStatsLuckCheckpoint
{
    private EventStatsLuckCheckpoint() { }
    public const int CurrentSchemaVersion = 2;
    public const int MaximumPayloadBytes = 8 * 1024 * 1024;
    public EventStatsLuckCheckpoint(Guid eventId, long evidenceRevision, long competitionId, int generation,
        Guid activityBatchId, string assignmentFingerprint, string sourceFingerprint, string lifecycleFingerprint,
        DateTimeOffset calculatedAt, DateTimeOffset? fetchedAt, DateTimeOffset? upstreamUpdatedAt, string payload,
        string algorithmVersion = "luck-percentile-kc-v2", int? convertedFromSchemaVersion = null,
        DateTimeOffset? convertedAt = null)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > MaximumPayloadBytes) throw new ArgumentException("Stats checkpoint exceeds its bounded payload.", nameof(payload));
        EventId = eventId; EvidenceRevision = evidenceRevision; CompetitionId = competitionId; Generation = generation;
        ActivityBatchId = activityBatchId; AssignmentFingerprint = assignmentFingerprint; SourceFingerprint = sourceFingerprint;
        LifecycleFingerprint = lifecycleFingerprint; CalculatedAt = calculatedAt; FetchedAt = fetchedAt;
        UpstreamUpdatedAt = upstreamUpdatedAt; Payload = payload;
        AlgorithmVersion = algorithmVersion; ConvertedFromSchemaVersion = convertedFromSchemaVersion; ConvertedAt = convertedAt;
    }
    public Guid EventId { get; private set; }
    public int SchemaVersion { get; private set; } = CurrentSchemaVersion;
    public long EvidenceRevision { get; private set; }
    public long CompetitionId { get; private set; }
    public int Generation { get; private set; }
    public Guid ActivityBatchId { get; private set; }
    public string AssignmentFingerprint { get; private set; } = string.Empty;
    public string SourceFingerprint { get; private set; } = string.Empty;
    public string LifecycleFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CalculatedAt { get; private set; }
    public DateTimeOffset? FetchedAt { get; private set; }
    public DateTimeOffset? UpstreamUpdatedAt { get; private set; }
    public string AlgorithmVersion { get; private set; } = "luck-percentile-kc-v2";
    public int? ConvertedFromSchemaVersion { get; private set; }
    public DateTimeOffset? ConvertedAt { get; private set; }
    public string Payload { get; private set; } = string.Empty;
}
