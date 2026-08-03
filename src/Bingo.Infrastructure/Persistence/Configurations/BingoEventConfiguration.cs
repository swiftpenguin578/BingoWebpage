using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class BingoEventConfiguration : IEntityTypeConfiguration<BingoEvent>
{
    public void Configure(EntityTypeBuilder<BingoEvent> builder)
    {
        var entity = builder;
        entity.ToTable("events");
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName("id");
        entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        entity.Property(item => item.Slug).HasColumnName("slug").HasMaxLength(120);
        entity.HasIndex(item => item.Slug).IsUnique();
        entity.Property(item => item.Description).HasColumnName("description").HasMaxLength(4_000);
        entity.Property(item => item.Timezone).HasColumnName("timezone").HasMaxLength(100);
        entity.Property(item => item.State).HasColumnName("state").HasConversion<string>().HasMaxLength(40);
        entity.Property(item => item.BannerAssetId).HasColumnName("banner_asset_id");
        entity.HasOne<EventBannerAsset>().WithMany().HasForeignKey(item => item.BannerAssetId).OnDelete(DeleteBehavior.SetNull);
        entity.Property(item => item.FirstPublicAt).HasColumnName("first_public_at");
        entity.Property(item => item.SignupOpensAt).HasColumnName("signup_opens_at");
        entity.Property(item => item.SignupClosesAt).HasColumnName("signup_closes_at");
        entity.Property(item => item.DraftAt).HasColumnName("draft_at");
        entity.Property(item => item.EventStartsAt).HasColumnName("event_starts_at");
        entity.Property(item => item.EventEndsAt).HasColumnName("event_ends_at");
        entity.Property(item => item.SubmissionCutoffAt).HasColumnName("submission_cutoff_at");
        entity.Property(item => item.ActualSignupOpenedAt).HasColumnName("actual_signup_opened_at");
        entity.Property(item => item.ActualSignupClosedAt).HasColumnName("actual_signup_closed_at");
        entity.Property(item => item.ActualStartedAt).HasColumnName("actual_started_at");
        entity.Property(item => item.ActualEndedAt).HasColumnName("actual_ended_at");
        entity.Property(item => item.SubmissionsClosedAt).HasColumnName("submissions_closed_at");
        entity.Property(item => item.ScheduledSignupOpeningEnabled).HasColumnName("scheduled_signup_opening_enabled");
        entity.Property(item => item.ScheduledSignupWarningCodes).HasColumnName("scheduled_signup_warning_codes").HasMaxLength(2_000);
        entity.Property(item => item.ReopenedSubmissionCutoffAt).HasColumnName("reopened_submission_cutoff_at");
        entity.Property(item => item.ParticipantCap).HasColumnName("participant_cap");
        entity.Property(item => item.WaitingListEnabled).HasColumnName("waiting_list_enabled");
        entity.Property(item => item.RequireSignupCode).HasColumnName("require_signup_code");
        entity.Property(item => item.SignupCodeHash).HasColumnName("signup_code_hash").HasMaxLength(500);
        entity.Property(item => item.PublicRules).HasColumnName("public_rules").HasMaxLength(10_000);
        entity.Property(item => item.BuyInDescription).HasColumnName("buy_in_description").HasMaxLength(2_000);
        entity.Property(item => item.PrizeDescription).HasColumnName("prize_description").HasMaxLength(2_000);
        entity.Property(item => item.ExpectedTeamCount).HasColumnName("expected_team_count");
        entity.Property(item => item.ExpectedTeamSize).HasColumnName("expected_team_size");
        entity.Property(item => item.ExpectedBoardRows).HasColumnName("expected_board_rows");
        entity.Property(item => item.ExpectedBoardColumns).HasColumnName("expected_board_columns");
        entity.Property(item => item.ParticipantListPublished).HasColumnName("participant_list_published");
        entity.Property(item => item.DraftResultsPublished).HasColumnName("draft_results_published");
        entity.Property(item => item.TeamRostersPublished).HasColumnName("team_rosters_published");
        entity.Property(item => item.BoardPublished).HasColumnName("board_published");
        entity.Property(item => item.ResultsPublished).HasColumnName("results_published");
        entity.Property(item => item.DraftLocked).HasColumnName("draft_locked");
        entity.Property(item => item.IsDevelopmentFixture).HasColumnName("is_development_fixture");
        entity.Property(item => item.EvidenceCodeEnabled).HasColumnName("evidence_code_enabled");
        entity.Property(item => item.FinalizedAt).HasColumnName("finalized_at");
        entity.Property(item => item.ArchivedAt).HasColumnName("archived_at");
        entity.Property(item => item.CancelledAt).HasColumnName("cancelled_at");
        entity.Property(item => item.CancelledByAccountId).HasColumnName("cancelled_by_account_id");
        entity.Property(item => item.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(2_000);
        entity.Property(item => item.DiscardedAt).HasColumnName("discarded_at");
        entity.Property(item => item.DiscardedByAccountId).HasColumnName("discarded_by_account_id");
        entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
        entity.Property(item => item.CreatedByAccountId).HasColumnName("created_by_account_id");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
    }
}

public sealed class EventBannerAssetConfiguration : IEntityTypeConfiguration<EventBannerAsset>
{
    public void Configure(EntityTypeBuilder<EventBannerAsset> builder)
    {
        builder.ToTable("event_banner_assets");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.EventId).HasColumnName("event_id");
        builder.Property(item => item.StorageKey).HasColumnName("storage_key").HasMaxLength(500);
        builder.Property(item => item.OriginalFilename).HasColumnName("original_filename").HasMaxLength(255);
        builder.Property(item => item.MediaType).HasColumnName("media_type").HasMaxLength(100);
        builder.Property(item => item.ByteSize).HasColumnName("byte_size");
        builder.Property(item => item.Width).HasColumnName("width");
        builder.Property(item => item.Height).HasColumnName("height");
        builder.Property(item => item.Checksum).HasColumnName("checksum").HasMaxLength(64);
        builder.Property(item => item.UploadedByAccountId).HasColumnName("uploaded_by_account_id");
        builder.Property(item => item.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(item => item.ReplacedAt).HasColumnName("replaced_at");
        builder.HasIndex(item => new { item.EventId, item.ReplacedAt });
    }
}

public sealed class EventBannerCleanupConfiguration : IEntityTypeConfiguration<EventBannerCleanup>
{
    public void Configure(EntityTypeBuilder<EventBannerCleanup> builder)
    {
        builder.ToTable("event_banner_cleanups");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.EventId).HasColumnName("event_id");
        builder.Property(item => item.StorageKey).HasColumnName("storage_key").HasMaxLength(500);
        builder.Property(item => item.QueuedAt).HasColumnName("queued_at");
        builder.Property(item => item.LastAttemptedAt).HasColumnName("last_attempted_at");
        builder.Property(item => item.LastFailure).HasColumnName("last_failure").HasMaxLength(1_000);
        builder.Property(item => item.AttemptCount).HasColumnName("attempt_count");
        builder.HasIndex(item => new { item.EventId, item.StorageKey }).IsUnique();
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ScheduledEventStartAttemptConfiguration : IEntityTypeConfiguration<ScheduledEventStartAttempt>
{
    public void Configure(EntityTypeBuilder<ScheduledEventStartAttempt> builder)
    {
        builder.ToTable("scheduled_event_start_attempts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.EventId).HasColumnName("event_id");
        builder.Property(item => item.ScheduledFor).HasColumnName("scheduled_for");
        builder.Property(item => item.AttemptedAt).HasColumnName("attempted_at");
        builder.Property(item => item.Started).HasColumnName("started");
        builder.Property(item => item.BlockerCodes).HasColumnName("blocker_codes").HasMaxLength(2_000);
        builder.Property(item => item.ResolvedAt).HasColumnName("resolved_at");
        builder.HasIndex(item => new { item.EventId, item.ScheduledFor }).IsUnique();
        builder.HasIndex(item => new { item.EventId, item.ResolvedAt });
    }
}

public sealed class ScheduledSignupOpeningAttemptConfiguration : IEntityTypeConfiguration<ScheduledSignupOpeningAttempt>
{
    public void Configure(EntityTypeBuilder<ScheduledSignupOpeningAttempt> builder)
    {
        builder.ToTable("scheduled_signup_opening_attempts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.EventId).HasColumnName("event_id");
        builder.Property(item => item.ScheduledFor).HasColumnName("scheduled_for");
        builder.Property(item => item.AttemptedAt).HasColumnName("attempted_at");
        builder.Property(item => item.Opened).HasColumnName("opened");
        builder.Property(item => item.BlockerCodes).HasColumnName("blocker_codes").HasMaxLength(2_000);
        builder.Property(item => item.BlockerDetails).HasColumnName("blocker_details").HasMaxLength(4_000);
        builder.Property(item => item.ResolvedAt).HasColumnName("resolved_at");
        builder.HasIndex(item => new { item.EventId, item.ScheduledFor }).IsUnique();
        builder.HasIndex(item => new { item.EventId, item.ResolvedAt });
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EventFinalizationSnapshotConfiguration : IEntityTypeConfiguration<EventFinalizationSnapshot>
{
    public void Configure(EntityTypeBuilder<EventFinalizationSnapshot> builder) { builder.ToTable("event_finalizations"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.Version).HasColumnName("version"); builder.HasIndex(x => new { x.EventId, x.Version }).IsUnique(); builder.Property(x => x.FinalizedAt).HasColumnName("finalized_at"); builder.Property(x => x.FinalizedByAccountId).HasColumnName("finalized_by_account_id"); builder.Property(x => x.ReviewCycleId).HasColumnName("review_cycle_id").IsRequired(); builder.HasAlternateKey(x => new { x.EventId, x.Id }); builder.HasOne<EventStateTransition>().WithMany().HasForeignKey(x => new { x.EventId, x.ReviewCycleId }).HasPrincipalKey(x => new { x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); builder.Property(x => x.ConsumedResolutionIdsJson).HasColumnName("consumed_resolution_ids_json").HasMaxLength(12000); builder.Property(x => x.CalculationInputsJson).HasColumnName("calculation_inputs_json").HasMaxLength(30000); builder.Property(x => x.CalculationResultsJson).HasColumnName("calculation_results_json").HasMaxLength(30000); builder.Property(x => x.UnfinalizedAt).HasColumnName("unfinalized_at"); builder.Property(x => x.UnfinalizedByAccountId).HasColumnName("unfinalized_by_account_id"); builder.Property(x => x.UnfinalizeReason).HasColumnName("unfinalize_reason").HasMaxLength(2000); builder.Ignore(x => x.Active); }
}
public sealed class OfficialPlacementSnapshotConfiguration : IEntityTypeConfiguration<OfficialPlacementSnapshot>
{
    public void Configure(EntityTypeBuilder<OfficialPlacementSnapshot> builder) { builder.ToTable("official_placements"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.FinalizationId).HasColumnName("finalization_id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.TeamName).HasColumnName("team_name").HasMaxLength(200); builder.Property(x => x.Placement).HasColumnName("placement"); builder.Property(x => x.BoardComplete).HasColumnName("board_complete"); builder.Property(x => x.BoardCompletedAt).HasColumnName("board_completed_at"); builder.Property(x => x.CompletedLines).HasColumnName("completed_lines"); builder.Property(x => x.CompletedTiles).HasColumnName("completed_tiles"); builder.Property(x => x.EhbTiebreak).HasColumnName("ehb_tiebreak").HasPrecision(14, 4); builder.HasIndex(x => new { x.FinalizationId, x.TeamId }).IsUnique(); builder.HasOne<EventFinalizationSnapshot>().WithMany().HasForeignKey(x => new { x.EventId, x.FinalizationId }).HasPrincipalKey(x => new { x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class FinalReviewResolutionConfiguration : IEntityTypeConfiguration<FinalReviewResolution>
{
    public void Configure(EntityTypeBuilder<FinalReviewResolution> builder) { builder.ToTable("final_review_resolutions"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.ReviewCycleId).HasColumnName("review_cycle_id").IsRequired(); builder.Property(x => x.BlockerKey).HasColumnName("blocker_key").HasMaxLength(200); builder.Property(x => x.BlockerDescription).HasColumnName("blocker_description").HasMaxLength(2000); builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000); builder.Property(x => x.ResolvedByAccountId).HasColumnName("resolved_by_account_id"); builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at"); builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.HasIndex(x => new { x.ReviewCycleId, x.BlockerKey }).IsUnique(); builder.HasOne<EventStateTransition>().WithMany().HasForeignKey(x => new { x.EventId, x.ReviewCycleId }).HasPrincipalKey(x => new { x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); builder.HasOne<Team>().WithMany().HasForeignKey(x => new { x.EventId, x.TeamId }).HasPrincipalKey(x => new { x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class TeamCompletionCorrectionConfiguration : IEntityTypeConfiguration<TeamCompletionCorrection>
{
    public void Configure(EntityTypeBuilder<TeamCompletionCorrection> builder) { builder.ToTable("team_completion_corrections"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.ReviewCycleId).HasColumnName("review_cycle_id").IsRequired(); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.CorrectedCompletedAt).HasColumnName("corrected_completed_at"); builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000); builder.Property(x => x.CorrectedByAccountId).HasColumnName("corrected_by_account_id"); builder.Property(x => x.RecordedAt).HasColumnName("recorded_at"); builder.HasIndex(x => new { x.ReviewCycleId, x.TeamId }).IsUnique(); builder.HasOne<EventStateTransition>().WithMany().HasForeignKey(x => new { x.EventId, x.ReviewCycleId }).HasPrincipalKey(x => new { x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); builder.HasOne<Team>().WithMany().HasForeignKey(x => new { x.EventId, x.TeamId }).HasPrincipalKey(x => new { x.EventId, x.Id }).OnDelete(DeleteBehavior.Restrict); }
}
