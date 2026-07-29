using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class SignupQuestionConfiguration : IEntityTypeConfiguration<SignupQuestion>
{
    public void Configure(EntityTypeBuilder<SignupQuestion> builder)
    {
        var entity = builder;
        entity.ToTable("signup_questions", table =>
        {
            table.HasCheckConstraint("ck_signup_questions_key", "length(btrim(key)) > 0");
            table.HasCheckConstraint("ck_signup_questions_account_shape", "(type = 'Account' AND account_answer_role IS NOT NULL) OR (type <> 'Account' AND account_answer_role IS NULL)");
            table.HasCheckConstraint("ck_signup_questions_choice_shape", "(type = 'SingleChoice' AND options IS NOT NULL AND length(btrim(options)) > 0) OR (type <> 'SingleChoice' AND options IS NULL)");
            table.HasCheckConstraint("ck_signup_questions_disabled_history", "(active AND disabled_at IS NULL AND disabled_by_account_id IS NULL AND disabled_reason IS NULL) OR (NOT active AND disabled_at IS NOT NULL)");
        });
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.EventId).HasColumnName("event_id");
        entity.Property(item => item.SignupFormId).HasColumnName("signup_form_id");
        entity.Property(item => item.Key).HasColumnName("key").HasMaxLength(100);
        entity.Property(item => item.Label).HasColumnName("label").HasMaxLength(300);
        entity.Property(item => item.HelpText).HasColumnName("help_text").HasMaxLength(1_000);
        entity.Property(item => item.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.Required).HasColumnName("required");
        entity.Property(item => item.Position).HasColumnName("position");
        entity.Property(item => item.Options).HasColumnName("options").HasMaxLength(4_000);
        entity.Property(item => item.SystemField).HasColumnName("system_field").HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.AccountAnswerRole).HasColumnName("account_answer_role").HasConversion<string>().HasMaxLength(30);
        entity.Property(item => item.PublicOnSignupBoard).HasColumnName("public_on_signup_board");
        entity.Property(item => item.Active).HasColumnName("active");
        entity.Property(item => item.DisabledAt).HasColumnName("disabled_at");
        entity.Property(item => item.DisabledByAccountId).HasColumnName("disabled_by_account_id");
        entity.Property(item => item.DisabledReason).HasColumnName("disabled_reason").HasMaxLength(1_000);
        entity.Property(item => item.ReplacedBySignupQuestionId).HasColumnName("replaced_by_signup_question_id");
        entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
        entity.HasIndex(item => new { item.SignupFormId, item.Key }).IsUnique();
        entity.HasIndex(item => new { item.SignupFormId, item.SystemField }).IsUnique().HasFilter("system_field <> 'None'");
        entity.HasAlternateKey(item => new { item.EventId, item.Id });
        entity.HasOne<SignupForm>().WithMany().HasForeignKey(item => item.SignupFormId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Bingo.Domain.Access.Account>().WithMany().HasForeignKey(item => item.DisabledByAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<SignupQuestion>().WithMany().HasForeignKey(item => item.ReplacedBySignupQuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}
