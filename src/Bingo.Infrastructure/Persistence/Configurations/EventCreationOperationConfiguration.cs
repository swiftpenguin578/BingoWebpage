using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventCreationOperationConfiguration : IEntityTypeConfiguration<EventCreationOperation>
{
    public void Configure(EntityTypeBuilder<EventCreationOperation> builder)
    {
        builder.ToTable("event_creation_operations");
        builder.HasKey(x => new { x.ActorAccountId, x.RequestId });
        builder.Property(x => x.ActorAccountId).HasColumnName("actor_account_id");
        builder.Property(x => x.RequestId).HasColumnName("request_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Timezone).HasColumnName("timezone").HasMaxLength(100).IsRequired();
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}
