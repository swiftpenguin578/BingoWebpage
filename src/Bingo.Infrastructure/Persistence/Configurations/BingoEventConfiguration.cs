using Bingo.Domain.Events;
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
        entity.Property(item => item.SignupOpensAt).HasColumnName("signup_opens_at");
        entity.Property(item => item.SignupClosesAt).HasColumnName("signup_closes_at");
        entity.Property(item => item.EventStartsAt).HasColumnName("event_starts_at");
        entity.Property(item => item.EventEndsAt).HasColumnName("event_ends_at");
        entity.Property(item => item.SubmissionCutoffAt).HasColumnName("submission_cutoff_at");
        entity.Property(item => item.ReopenedSubmissionCutoffAt).HasColumnName("reopened_submission_cutoff_at");
        entity.Property(item => item.ParticipantCap).HasColumnName("participant_cap");
        entity.Property(item => item.WaitingListEnabled).HasColumnName("waiting_list_enabled");
        entity.Property(item => item.AllowPrivateSignupEditing).HasColumnName("allow_private_signup_editing");
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
        entity.Property(item => item.EvidenceCodeEnabled).HasColumnName("evidence_code_enabled");
        entity.Property(item => item.FinalizedAt).HasColumnName("finalized_at");
        entity.Property(item => item.ArchivedAt).HasColumnName("archived_at");
        entity.Property(item => item.CreatedByAccountId).HasColumnName("created_by_account_id");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
    }
}

public sealed class EventFinalizationSnapshotConfiguration : IEntityTypeConfiguration<EventFinalizationSnapshot>
{
    public void Configure(EntityTypeBuilder<EventFinalizationSnapshot> builder) { builder.ToTable("event_finalizations"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.Version).HasColumnName("version"); builder.HasIndex(x => new { x.EventId, x.Version }).IsUnique(); builder.Property(x => x.FinalizedAt).HasColumnName("finalized_at"); builder.Property(x => x.FinalizedByAccountId).HasColumnName("finalized_by_account_id"); builder.Property(x => x.UnfinalizedAt).HasColumnName("unfinalized_at"); builder.Property(x => x.UnfinalizedByAccountId).HasColumnName("unfinalized_by_account_id"); builder.Property(x => x.UnfinalizeReason).HasColumnName("unfinalize_reason").HasMaxLength(2000); builder.Ignore(x => x.Active); }
}
public sealed class OfficialPlacementSnapshotConfiguration : IEntityTypeConfiguration<OfficialPlacementSnapshot>
{
    public void Configure(EntityTypeBuilder<OfficialPlacementSnapshot> builder) { builder.ToTable("official_placements"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.FinalizationId).HasColumnName("finalization_id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.TeamName).HasColumnName("team_name").HasMaxLength(200); builder.Property(x => x.Placement).HasColumnName("placement"); builder.Property(x => x.BoardComplete).HasColumnName("board_complete"); builder.Property(x => x.BoardCompletedAt).HasColumnName("board_completed_at"); builder.Property(x => x.CompletedLines).HasColumnName("completed_lines"); builder.Property(x => x.CompletedTiles).HasColumnName("completed_tiles"); builder.Property(x => x.EhbTiebreak).HasColumnName("ehb_tiebreak").HasPrecision(14, 4); builder.HasIndex(x => new { x.FinalizationId, x.TeamId }).IsUnique(); }
}
public sealed class FinalReviewResolutionConfiguration : IEntityTypeConfiguration<FinalReviewResolution>
{
    public void Configure(EntityTypeBuilder<FinalReviewResolution> builder) { builder.ToTable("final_review_resolutions"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.BlockerKey).HasColumnName("blocker_key").HasMaxLength(200); builder.Property(x => x.BlockerDescription).HasColumnName("blocker_description").HasMaxLength(2000); builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000); builder.Property(x => x.ResolvedByAccountId).HasColumnName("resolved_by_account_id"); builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at"); builder.HasIndex(x => new { x.EventId, x.BlockerKey }); }
}
public sealed class TeamCompletionCorrectionConfiguration : IEntityTypeConfiguration<TeamCompletionCorrection>
{
    public void Configure(EntityTypeBuilder<TeamCompletionCorrection> builder) { builder.ToTable("team_completion_corrections"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.CorrectedCompletedAt).HasColumnName("corrected_completed_at"); builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(2000); builder.Property(x => x.CorrectedByAccountId).HasColumnName("corrected_by_account_id"); builder.Property(x => x.RecordedAt).HasColumnName("recorded_at"); builder.HasIndex(x => new { x.EventId, x.TeamId }).IsUnique(); }
}
