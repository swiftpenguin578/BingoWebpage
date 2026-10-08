using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class SignupQuestionCreationOperationConfiguration : IEntityTypeConfiguration<SignupQuestionCreationOperation>
{
    public void Configure(EntityTypeBuilder<SignupQuestionCreationOperation> builder)
    {
        builder.ToTable("signup_question_creation_operations");
        builder.HasKey(x => x.RequestId).HasName("pk_signup_question_creation_operations");
        builder.Property(x => x.RequestId).HasColumnName("request_id");
        builder.Property(x => x.ActorAccountId).HasColumnName("actor_account_id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.InputFingerprint).HasColumnName("input_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(x => x.QuestionId).HasColumnName("question_id");
        builder.HasIndex(x => x.QuestionId).IsUnique();
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        // No question FK: event discard may remove setup definitions, but cannot erase retry identity.
    }
}
