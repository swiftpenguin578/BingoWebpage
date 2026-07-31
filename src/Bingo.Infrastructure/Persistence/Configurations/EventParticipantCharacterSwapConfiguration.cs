using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventParticipantCharacterSwapConfiguration : IEntityTypeConfiguration<EventParticipantCharacterSwap>
{
    public void Configure(EntityTypeBuilder<EventParticipantCharacterSwap> builder)
    {
        builder.ToTable("event_participant_character_swaps", table =>
        {
            table.HasCheckConstraint("ck_event_participant_character_swaps_distinct_characters", "previous_osrs_character_id IS NULL OR previous_osrs_character_id <> next_osrs_character_id");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.EventParticipantId).HasColumnName("event_participant_id");
        builder.Property(x => x.PreviousOsrsCharacterId).HasColumnName("previous_osrs_character_id");
        builder.Property(x => x.NextOsrsCharacterId).HasColumnName("next_osrs_character_id");
        builder.Property(x => x.EffectiveAtUtc).HasColumnName("effective_at_utc");
        builder.Property(x => x.RecordedAtUtc).HasColumnName("recorded_at_utc");
        builder.Property(x => x.RecordedByAccountId).HasColumnName("recorded_by_account_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2_000);
        builder.HasIndex(x => new { x.EventParticipantId, x.EffectiveAtUtc, x.RecordedAtUtc, x.Id });
        builder.HasIndex(x => x.EventParticipantId).IsUnique().HasFilter("previous_osrs_character_id IS NULL");
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EventParticipant>().WithMany()
            .HasForeignKey(x => new { x.EventId, x.EventParticipantId })
            .HasPrincipalKey(x => new { x.EventId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OsrsCharacter>().WithMany().HasForeignKey(x => x.PreviousOsrsCharacterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OsrsCharacter>().WithMany().HasForeignKey(x => x.NextOsrsCharacterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.RecordedByAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
