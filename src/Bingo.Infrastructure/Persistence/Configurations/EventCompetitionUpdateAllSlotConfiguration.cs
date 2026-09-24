using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventCompetitionUpdateAllSlotConfiguration : IEntityTypeConfiguration<EventCompetitionUpdateAllSlot>
{
    public void Configure(EntityTypeBuilder<EventCompetitionUpdateAllSlot> builder)
    {
        builder.ToTable("event_competition_update_all_slots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.ManagementId).HasColumnName("management_id");
        builder.Property(x => x.SynchronizationId).HasColumnName("synchronization_id");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.ActualStartedAt).HasColumnName("actual_started_at");
        builder.Property(x => x.PairedFetchAt).HasColumnName("paired_fetch_at");
        builder.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(x => x.ManagementVersion).HasColumnName("management_version");
        builder.Property(x => x.SynchronizationGeneration).HasColumnName("synchronization_generation");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.ClaimedAt).HasColumnName("claimed_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.OutcomeCode).HasColumnName("outcome_code").HasMaxLength(80);
        builder.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(2_000);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.CompetitionId, x.PairedFetchAt }).IsUnique();
        builder.HasIndex(x => new { x.EventId, x.ScheduledAt, x.Status });
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EventCompetitionManagement>().WithMany().HasForeignKey(x => x.ManagementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EventCompetitionSynchronization>().WithMany().HasForeignKey(x => x.SynchronizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
