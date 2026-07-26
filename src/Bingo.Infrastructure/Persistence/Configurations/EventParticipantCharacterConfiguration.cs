using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventParticipantCharacterConfiguration : IEntityTypeConfiguration<EventParticipantCharacter>
{
    public void Configure(EntityTypeBuilder<EventParticipantCharacter> builder)
    {
        builder.ToTable("event_participant_characters", table =>
        {
            table.HasCheckConstraint("ck_event_participant_characters_registration_order", "registration_order >= 0");
            table.HasCheckConstraint(
                "ck_event_participant_characters_ehb",
                "(event_role = 'Playing' AND ehb_snapshot IS NOT NULL AND ehb_snapshot >= 0 AND ehb_source IS NOT NULL) OR " +
                "(event_role = 'Informational' AND ehb_snapshot IS NULL AND ehb_source IS NULL AND ehb_fetched_at IS NULL)");
            table.HasCheckConstraint(
                "ck_event_participant_characters_fetch",
                "(ehb_source = 'WiseOldMan' AND ehb_fetched_at IS NOT NULL) OR (ehb_source IS DISTINCT FROM 'WiseOldMan' AND ehb_fetched_at IS NULL)");
            table.HasCheckConstraint(
                "ck_event_participant_characters_release",
                "(released_at IS NULL AND released_by_account_id IS NULL) OR (released_at IS NOT NULL AND released_by_account_id IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.EventParticipantId).HasColumnName("event_participant_id");
        builder.Property(x => x.OsrsCharacterId).HasColumnName("osrs_character_id");
        builder.Property(x => x.RegistrationOrder).HasColumnName("registration_order");
        builder.Property(x => x.RegisteredAt).HasColumnName("registered_at");
        builder.Property(x => x.RegisteredByAccountId).HasColumnName("registered_by_account_id");
        builder.Property(x => x.SignupQuestionId).HasColumnName("signup_question_id");
        builder.Property(x => x.EventRole).HasColumnName("event_role").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.EhbSnapshot).HasColumnName("ehb_snapshot").HasPrecision(12, 2);
        builder.Property(x => x.EhbSource).HasColumnName("ehb_source").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.EhbFetchedAt).HasColumnName("ehb_fetched_at");
        builder.Property(x => x.ReleasedAt).HasColumnName("released_at");
        builder.Property(x => x.ReleasedByAccountId).HasColumnName("released_by_account_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.EventParticipantId, x.RegistrationOrder }).IsUnique();
        builder.HasIndex(x => new { x.EventId, x.OsrsCharacterId }).IsUnique().HasFilter("released_at IS NULL");
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EventParticipant>().WithMany()
            .HasForeignKey(x => new { x.EventId, x.EventParticipantId })
            .HasPrincipalKey(x => new { x.EventId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OsrsCharacter>().WithMany().HasForeignKey(x => x.OsrsCharacterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.RegisteredByAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.ReleasedByAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SignupQuestion>().WithMany().HasForeignKey(x => x.SignupQuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}
