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
        entity.Property(item => item.CreatedByAccountId).HasColumnName("created_by_account_id");
        entity.Property(item => item.CreatedAt).HasColumnName("created_at");
    }
}
