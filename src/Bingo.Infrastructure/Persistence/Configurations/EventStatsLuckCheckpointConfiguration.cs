using Bingo.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventStatsLuckCheckpointConfiguration : IEntityTypeConfiguration<EventStatsLuckCheckpoint>
{
    public void Configure(EntityTypeBuilder<EventStatsLuckCheckpoint> builder)
    {
        builder.ToTable("event_stats_luck_checkpoints", table =>
        {
            table.HasCheckConstraint("ck_stats_checkpoint_payload", "schema_version IN (1, 2) AND evidence_revision >= 0 AND octet_length(payload::text) <= 8388608 AND jsonb_typeof(payload) = 'object'");
        });
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        builder.Property(x => x.EvidenceRevision).HasColumnName("evidence_revision").IsConcurrencyToken();
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.Generation).HasColumnName("generation");
        builder.Property(x => x.ActivityBatchId).HasColumnName("activity_batch_id").IsConcurrencyToken();
        builder.Property(x => x.AssignmentFingerprint).HasColumnName("assignment_fingerprint").HasMaxLength(64);
        builder.Property(x => x.SourceFingerprint).HasColumnName("source_fingerprint").HasMaxLength(64);
        builder.Property(x => x.LifecycleFingerprint).HasColumnName("lifecycle_fingerprint").HasMaxLength(64);
        builder.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
        builder.Property(x => x.FetchedAt).HasColumnName("fetched_at");
        builder.Property(x => x.UpstreamUpdatedAt).HasColumnName("upstream_updated_at");
        builder.Property(x => x.AlgorithmVersion).HasColumnName("algorithm_version").HasMaxLength(80);
        builder.Property(x => x.ConvertedFromSchemaVersion).HasColumnName("converted_from_schema_version");
        builder.Property(x => x.ConvertedAt).HasColumnName("converted_at");
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}
