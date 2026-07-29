using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{
    public void Configure(EntityTypeBuilder<EventParticipant> builder)
    {
        var entity = builder;
        entity.ToTable("event_participants");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.EventId).HasColumnName("event_id");
        entity.Property(item => item.AccountId).HasColumnName("account_id");
        entity.Property(item => item.AdminNotes).HasColumnName("admin_notes").HasMaxLength(4_000);
        entity.Property(item => item.CaptainVolunteer).HasColumnName("captain_volunteer");
        entity.Property(item => item.PaymentReceived).HasColumnName("payment_received");
        entity.Property(item => item.SignupStatus).HasColumnName("signup_status").HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.SignupSequence).HasColumnName("signup_sequence");
        entity.Property(item => item.SignedUpAt).HasColumnName("signed_up_at");
        entity.Property(item => item.ConfirmedAt).HasColumnName("confirmed_at");
        entity.Property(item => item.WaitingListedAt).HasColumnName("waiting_listed_at");
        entity.Property(item => item.WithdrawnAt).HasColumnName("withdrawn_at");
        entity.Property(item => item.WithdrawnByAccountId).HasColumnName("withdrawn_by_account_id");
        entity.Property(item => item.StatusReason).HasColumnName("status_reason").HasMaxLength(1_000);
        entity.Property(item => item.FormVersion).HasColumnName("form_version");
        entity.Property(item => item.ResponseVersion).HasColumnName("response_version").HasDefaultValue(1);
        entity.Property(item => item.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(30);
        entity.HasIndex(item => new { item.EventId, item.SignupSequence }).IsUnique();
        entity.HasAlternateKey(item => new { item.EventId, item.Id });
        entity.HasIndex(item => new { item.EventId, item.AccountId }).IsUnique().HasFilter("account_id IS NOT NULL");
        entity.HasOne<Bingo.Domain.Access.Account>().WithMany().HasForeignKey(item => item.AccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Bingo.Domain.Access.Account>().WithMany().HasForeignKey(item => item.WithdrawnByAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
