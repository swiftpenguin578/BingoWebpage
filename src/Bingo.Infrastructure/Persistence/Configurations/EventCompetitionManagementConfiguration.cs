using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventCompetitionManagementConfiguration : IEntityTypeConfiguration<EventCompetitionManagement>
{
    public void Configure(EntityTypeBuilder<EventCompetitionManagement> builder)
    {
        builder.ToTable("event_competition_management");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.SynchronizationId).HasColumnName("synchronization_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.CompetitionTitle).HasColumnName("competition_title").HasMaxLength(300);
        builder.Property(x => x.CompetitionStartsAt).HasColumnName("competition_starts_at");
        builder.Property(x => x.CompetitionEndsAt).HasColumnName("competition_ends_at");
        builder.Property(x => x.ProtectedVerificationCode).HasColumnName("protected_verification_code").HasMaxLength(4000);
        builder.Property(x => x.ManagedFieldScope).HasColumnName("managed_field_scope").HasMaxLength(200);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.LastAppliedLocalFingerprint).HasColumnName("last_applied_local_fingerprint").HasMaxLength(64);
        builder.Property(x => x.LastAppliedRemoteFingerprint).HasColumnName("last_applied_remote_fingerprint").HasMaxLength(64);
        builder.Property(x => x.LastAcknowledgedRosterJson).HasColumnName("last_acknowledged_roster_json").HasMaxLength(100_000);
        builder.Property(x => x.ManagementVersion).HasColumnName("management_version");
        builder.Property(x => x.LastAppliedAt).HasColumnName("last_applied_at");
        builder.Property(x => x.LastErrorAt).HasColumnName("last_error_at");
        builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(60);
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2_000);
        builder.Property(x => x.LastOperationId).HasColumnName("last_operation_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.ActualStartedAt).HasColumnName("actual_started_at");
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasIndex(x => new { x.CompetitionId, x.Status });
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EventCompetitionSynchronization>().WithMany().HasForeignKey(x => x.SynchronizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EventCompetitionManagementOperationConfiguration : IEntityTypeConfiguration<EventCompetitionManagementOperation>
{
    public void Configure(EntityTypeBuilder<EventCompetitionManagementOperation> builder)
    {
        builder.ToTable("event_competition_management_operations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.ManagementId).HasColumnName("management_id");
        builder.Property(x => x.Type).HasColumnName("operation_type").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DesiredPayloadJson).HasColumnName("desired_payload_json").HasMaxLength(200_000);
        builder.Property(x => x.DesiredFingerprint).HasColumnName("desired_fingerprint").HasMaxLength(64);
        builder.Property(x => x.Phase).HasColumnName("phase").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.EventVersion).HasColumnName("event_version");
        builder.Property(x => x.ActorAccountId).HasColumnName("actor_account_id");
        builder.Property(x => x.ActorUsername).HasColumnName("actor_username").HasMaxLength(100);
        builder.Property(x => x.RemoteCompetitionId).HasColumnName("remote_competition_id");
        builder.Property(x => x.RemoteReceiptReference).HasColumnName("remote_receipt_reference").HasMaxLength(200);
        builder.Property(x => x.SafeErrorCode).HasColumnName("safe_error_code").HasMaxLength(60);
        builder.Property(x => x.SafeError).HasColumnName("safe_error").HasMaxLength(2_000);
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.ClaimedAt).HasColumnName("claimed_at");
        builder.Property(x => x.SendingAt).HasColumnName("sending_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.EventId, x.Type, x.Phase, x.NextAttemptAt });
        builder.HasIndex(x => new { x.EventId, x.DesiredFingerprint, x.Phase });
        builder.HasIndex(x => new { x.EventId, x.Type })
            .IsUnique()
            .HasFilter("phase IN ('Pending', 'Claimed', 'Sending', 'Retry', 'Unknown')");
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EventCompetitionManagement>().WithMany().HasForeignKey(x => x.ManagementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bingo.Domain.Access.Account>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
