using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class EventCompetitionSynchronizationConfiguration : IEntityTypeConfiguration<EventCompetitionSynchronization>
{
    public void Configure(EntityTypeBuilder<EventCompetitionSynchronization> builder)
    {
        builder.ToTable("event_competition_synchronizations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.Generation).HasColumnName("generation");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.CompetitionTitle).HasColumnName("competition_title").HasMaxLength(300);
        builder.Property(x => x.CompetitionStartsAt).HasColumnName("competition_starts_at");
        builder.Property(x => x.CompetitionEndsAt).HasColumnName("competition_ends_at");
        builder.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(x => x.LastSuccessfulAt).HasColumnName("last_successful_at");
        builder.Property(x => x.LastUpstreamUpdatedAt).HasColumnName("last_upstream_updated_at");
        builder.Property(x => x.AssignmentFingerprint).HasColumnName("assignment_fingerprint").HasMaxLength(64);
        builder.Property(x => x.LatestComplete).HasColumnName("latest_complete");
        builder.Property(x => x.MissingAccountsJson).HasColumnName("missing_accounts_json").HasMaxLength(8_000);
        builder.Property(x => x.LastErrorKind).HasColumnName("last_error_kind").HasMaxLength(40);
        builder.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(2_000);
        builder.Property(x => x.CycleStartedAt).HasColumnName("cycle_started_at");
        builder.Property(x => x.NormalDueAt).HasColumnName("normal_due_at");
        builder.Property(x => x.RetryDueAt).HasColumnName("retry_due_at");
        builder.Property(x => x.RetryCount).HasColumnName("retry_count");
        builder.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(64);
        builder.Property(x => x.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.HasIndex(x => new { x.EventId, x.CompetitionId, x.NormalDueAt, x.RetryDueAt });
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EventCompetitionCharacterActivityConfiguration : IEntityTypeConfiguration<EventCompetitionCharacterActivity>
{
    public void Configure(EntityTypeBuilder<EventCompetitionCharacterActivity> builder)
    {
        builder.ToTable("event_competition_character_activity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.Generation).HasColumnName("generation");
        builder.Property(x => x.CompetitionId).HasColumnName("competition_id");
        builder.Property(x => x.OsrsCharacterId).HasColumnName("osrs_character_id");
        builder.Property(x => x.GainedEhb).HasColumnName("gained_ehb").HasPrecision(14, 4);
        builder.Property(x => x.FetchedAt).HasColumnName("fetched_at");
        builder.Property(x => x.UpstreamUpdatedAt).HasColumnName("upstream_updated_at");
        builder.Property(x => x.AssignmentFingerprint).HasColumnName("assignment_fingerprint").HasMaxLength(64);
        builder.HasIndex(x => new { x.EventId, x.Generation, x.OsrsCharacterId }).IsUnique();
        builder.HasIndex(x => new { x.EventId, x.Generation });
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OsrsCharacter>().WithMany().HasForeignKey(x => x.OsrsCharacterId).OnDelete(DeleteBehavior.Restrict);
    }
}
