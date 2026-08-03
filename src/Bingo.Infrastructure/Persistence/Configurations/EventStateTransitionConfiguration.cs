using Bingo.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventStateTransitionConfiguration : IEntityTypeConfiguration<EventStateTransition>
{
    public void Configure(EntityTypeBuilder<EventStateTransition> builder)
    {
        var entity = builder;
        entity.ToTable("event_state_transitions");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.EventId).HasColumnName("event_id");
        entity.Property(item => item.FromState).HasColumnName("from_state").HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.ToState).HasColumnName("to_state").HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.PerformedByAccountId).HasColumnName("performed_by_account_id");
        entity.Property(item => item.PerformedAt).HasColumnName("performed_at");
        entity.Property(item => item.EffectiveAt).HasColumnName("effective_at");
        entity.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(1_000);
        entity.Property(item => item.Scheduled).HasColumnName("scheduled");
        entity.HasAlternateKey(item => new { item.EventId, item.Id });
        entity.HasIndex(item => new { item.EventId, item.PerformedAt });
    }
}
