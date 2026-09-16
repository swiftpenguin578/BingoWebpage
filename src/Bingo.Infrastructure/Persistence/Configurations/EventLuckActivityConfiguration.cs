using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventLuckOutcomeBasisConfiguration : IEntityTypeConfiguration<EventLuckOutcomeBasis>
{
    public void Configure(EntityTypeBuilder<EventLuckOutcomeBasis> builder)
    {
        builder.ToTable("event_luck_outcome_bases", table =>
        {
            table.HasCheckConstraint("ck_luck_basis_mechanics", "status <> 'Retained' OR (boss_activity_id IS NOT NULL AND first_approval_snapshot_id IS NOT NULL AND first_approval_drop_snapshot_id IS NOT NULL AND first_approved_at IS NOT NULL AND numeric_probability IS NOT NULL AND rolls_per_completion IS NOT NULL AND numeric_probability > 0 AND numeric_probability <= 1 AND rolls_per_completion >= 1)");
            table.HasCheckConstraint("ck_luck_basis_binding", "(metric IS NULL AND metric_bound_at IS NULL AND mapping_validated_at IS NULL AND mapping_catalogue_version IS NULL AND source_revision = 1) OR (status = 'Retained' AND metric IS NOT NULL AND metric_bound_at IS NOT NULL AND mapping_validated_at IS NOT NULL AND mapping_catalogue_version IS NOT NULL AND source_revision = 2)");
        });
        builder.HasKey(x => new { x.EventId, x.SourceDropId, x.ItemIdSnapshot });
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.SourceDropId).HasColumnName("source_drop_id");
        builder.Property(x => x.ItemIdSnapshot).HasColumnName("item_id_snapshot");
        builder.Property(x => x.BossActivityId).HasColumnName("boss_activity_id");
        builder.Property(x => x.FirstApprovalSnapshotId).HasColumnName("first_approval_snapshot_id");
        builder.Property(x => x.FirstApprovalDropSnapshotId).HasColumnName("first_approval_drop_snapshot_id");
        builder.Property(x => x.FirstApprovedAt).HasColumnName("first_approved_at");
        builder.Property(x => x.NumericProbability).HasColumnName("numeric_probability").HasPrecision(18, 12);
        builder.Property(x => x.RollsPerCompletion).HasColumnName("rolls_per_completion");
        builder.Property(x => x.ProbabilityScope).HasColumnName("probability_scope").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ConditionalOnParent).HasColumnName("conditional_on_parent");
        builder.Property(x => x.ParentProbability).HasColumnName("parent_probability").HasPrecision(18, 12);
        builder.Property(x => x.AssumedParticipants).HasColumnName("assumed_participants");
        builder.Property(x => x.RollGroup).HasColumnName("roll_group").HasMaxLength(100);
        builder.Property(x => x.RateCondition).HasColumnName("rate_condition").HasMaxLength(2000);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Metric).HasColumnName("metric").HasMaxLength(100);
        builder.Property(x => x.MetricBoundAt).HasColumnName("metric_bound_at");
        builder.Property(x => x.MappingValidatedAt).HasColumnName("mapping_validated_at");
        builder.Property(x => x.MappingCatalogueVersion).HasColumnName("mapping_catalogue_version");
        builder.Property(x => x.SourceRevision).HasColumnName("source_revision").IsConcurrencyToken();
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BoardApprovalSnapshot>().WithMany().HasForeignKey(x => x.FirstApprovalSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BoardApprovalRequirementDropSnapshot>().WithMany().HasForeignKey(x => x.FirstApprovalDropSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BossActivity>().WithMany().HasForeignKey(x => x.BossActivityId).OnDelete(DeleteBehavior.Restrict);
        var bindingFields = new[] { nameof(EventLuckOutcomeBasis.Metric), nameof(EventLuckOutcomeBasis.MetricBoundAt), nameof(EventLuckOutcomeBasis.MappingValidatedAt), nameof(EventLuckOutcomeBasis.MappingCatalogueVersion), nameof(EventLuckOutcomeBasis.SourceRevision) };
        foreach (var property in builder.Metadata.GetProperties().Where(x => !bindingFields.Contains(x.Name, StringComparer.Ordinal)))
            property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}

public sealed class EventCompetitionCharacterMetricActivityConfiguration : IEntityTypeConfiguration<EventCompetitionCharacterMetricActivity>
{
    public void Configure(EntityTypeBuilder<EventCompetitionCharacterMetricActivity> builder)
    {
        builder.ToTable("event_competition_character_metric_activity", table =>
        {
            table.HasCheckConstraint("ck_metric_activity_origin", "(fetched_at IS NULL AND start IS NULL AND \"end\" IS NULL AND gained IS NULL AND activity_batch_id IS NOT NULL AND source_request_fingerprint IS NOT NULL AND coverage = 'Missing') OR (fetched_at IS NOT NULL AND start IS NOT NULL AND \"end\" IS NOT NULL AND gained IS NOT NULL AND activity_batch_id IS NOT NULL AND source_request_fingerprint IS NOT NULL AND coverage IN ('Ranked','EstimatedBaseline','ZeroRecorded'))");
            table.HasCheckConstraint("ck_metric_activity_values", "start >= -1 AND \"end\" >= -1 AND gained >= 0 AND trunc(start) = start AND trunc(\"end\") = \"end\" AND trunc(gained) = gained");
        });
        builder.HasKey(x => new { x.EventId, x.Generation, x.OsrsCharacterId, x.Metric });
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.Generation).HasColumnName("generation");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.OsrsCharacterId).HasColumnName("osrs_character_id");
        builder.Property(x => x.Metric).HasColumnName("metric").HasMaxLength(100);
        builder.Property(x => x.Start).HasColumnName("start").HasPrecision(20, 0);
        builder.Property(x => x.End).HasColumnName("end").HasPrecision(20, 0);
        builder.Property(x => x.Gained).HasColumnName("gained").HasPrecision(20, 0);
        builder.Property(x => x.Coverage).HasColumnName("coverage").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.LastIssue).HasColumnName("last_issue").HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.FetchedAt).HasColumnName("fetched_at");
        builder.Property(x => x.UpstreamUpdatedAt).HasColumnName("upstream_updated_at");
        builder.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(x => x.ActivityBatchId).HasColumnName("activity_batch_id");
        builder.Property(x => x.SourceRequestFingerprint).HasColumnName("source_request_fingerprint").HasMaxLength(64);
        builder.Property(x => x.AssignmentFingerprint).HasColumnName("assignment_fingerprint").HasMaxLength(64);
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OsrsCharacter>().WithMany().HasForeignKey(x => x.OsrsCharacterId).OnDelete(DeleteBehavior.Restrict);
    }
}
