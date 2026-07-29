using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class SignupFormConfiguration : IEntityTypeConfiguration<SignupForm>
{
    public void Configure(EntityTypeBuilder<SignupForm> builder)
    {
        builder.ToTable("signup_forms", table =>
        {
            table.HasCheckConstraint("ck_signup_forms_code", "(require_signup_code AND signup_code_hash IS NOT NULL) OR (NOT require_signup_code AND signup_code_hash IS NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.EventId).HasColumnName("event_id");
        builder.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.PublishedAt).HasColumnName("published_at");
        builder.Property(item => item.ClosedAt).HasColumnName("closed_at");
        builder.Property(item => item.FirstResponseAt).HasColumnName("first_response_at");
        builder.Property(item => item.RequireSignupCode).HasColumnName("require_signup_code");
        builder.Property(item => item.SignupCodeHash).HasColumnName("signup_code_hash").HasMaxLength(200);
        builder.HasIndex(item => item.EventId).IsUnique();
        builder.HasAlternateKey(item => new { item.EventId, item.Id });
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}
