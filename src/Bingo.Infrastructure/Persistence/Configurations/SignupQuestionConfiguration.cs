using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class SignupQuestionConfiguration : IEntityTypeConfiguration<SignupQuestion>
{
    public void Configure(EntityTypeBuilder<SignupQuestion> builder)
    {
        var entity = builder;
        entity.ToTable("signup_questions");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.EventId).HasColumnName("event_id");
        entity.Property(item => item.Key).HasColumnName("key").HasMaxLength(100);
        entity.Property(item => item.Label).HasColumnName("label").HasMaxLength(300);
        entity.Property(item => item.HelpText).HasColumnName("help_text").HasMaxLength(1_000);
        entity.Property(item => item.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.Required).HasColumnName("required");
        entity.Property(item => item.Position).HasColumnName("position");
        entity.Property(item => item.Options).HasColumnName("options").HasMaxLength(4_000);
        entity.Property(item => item.Active).HasColumnName("active");
        entity.HasIndex(item => new { item.EventId, item.Key }).IsUnique();
    }
}
