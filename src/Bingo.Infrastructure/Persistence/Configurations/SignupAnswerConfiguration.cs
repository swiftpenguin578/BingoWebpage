using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class SignupAnswerConfiguration : IEntityTypeConfiguration<SignupAnswer>
{
    public void Configure(EntityTypeBuilder<SignupAnswer> builder)
    {
        var entity = builder;
        entity.ToTable("signup_answers", table => table.HasCheckConstraint("ck_signup_answers_account_value", "osrs_character_id IS NOT NULL OR value IS NOT NULL"));
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.EventParticipantId).HasColumnName("event_participant_id");
        entity.Property(item => item.SignupQuestionId).HasColumnName("signup_question_id");
        entity.Property(item => item.QuestionLabelSnapshot).HasColumnName("question_label_snapshot").HasMaxLength(300);
        entity.Property(item => item.Value).HasColumnName("value").HasMaxLength(4_000);
        entity.Property(item => item.OsrsCharacterId).HasColumnName("osrs_character_id");
        entity.HasIndex(item => new { item.EventParticipantId, item.SignupQuestionId }).IsUnique();
        entity.HasOne<EventParticipant>().WithMany().HasForeignKey(item => item.EventParticipantId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<SignupQuestion>().WithMany().HasForeignKey(item => item.SignupQuestionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Bingo.Domain.Access.OsrsCharacter>().WithMany().HasForeignKey(item => item.OsrsCharacterId).OnDelete(DeleteBehavior.Restrict);
    }
}
