using Bingo.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(150); builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(1000); builder.Property(x => x.FormationType).HasColumnName("formation_type").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.AffiliationName).HasColumnName("affiliation_name").HasMaxLength(200); builder.Property(x => x.IncludedInDraft).HasColumnName("included_in_draft"); builder.Property(x => x.DraftPosition).HasColumnName("draft_position"); builder.Property(x => x.Active).HasColumnName("active"); builder.Property(x => x.FinalizedAt).HasColumnName("finalized_at"); builder.HasIndex(x => new { x.EventId, x.Slug }).IsUnique();
    }
}

public sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("team_memberships"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.EventParticipantId).HasColumnName("event_participant_id"); builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.JoinedAt).HasColumnName("joined_at"); builder.Property(x => x.LeftAt).HasColumnName("left_at"); builder.Property(x => x.AssignedByDraftPickId).HasColumnName("assigned_by_draft_pick_id"); builder.Property(x => x.AssignmentReason).HasColumnName("assignment_reason").HasMaxLength(1000); builder.HasIndex(x => new { x.TeamId, x.EventParticipantId }); builder.HasIndex(x => x.EventParticipantId).HasFilter("left_at IS NULL").IsUnique();
    }
}

public sealed class DraftSessionConfiguration : IEntityTypeConfiguration<DraftSession>
{
    public void Configure(EntityTypeBuilder<DraftSession> builder)
    {
        builder.ToTable("draft_sessions"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.TargetTeamSize).HasColumnName("target_team_size"); builder.Property(x => x.State).HasColumnName("state").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.LockedAt).HasColumnName("locked_at"); builder.Property(x => x.FinalizedAt).HasColumnName("finalized_at"); builder.Property(x => x.ControllerAccountId).HasColumnName("controller_account_id"); builder.Property(x => x.ControllerLeaseExpiresAt).HasColumnName("controller_lease_expires_at"); builder.Property(x => x.ControlVersion).HasColumnName("control_version").IsConcurrencyToken(); builder.HasIndex(x => x.EventId).IsUnique();
    }
}

public sealed class DraftPickConfiguration : IEntityTypeConfiguration<DraftPick>
{
    public void Configure(EntityTypeBuilder<DraftPick> builder)
    {
        builder.ToTable("draft_picks"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.DraftSessionId).HasColumnName("draft_session_id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.EventParticipantId).HasColumnName("event_participant_id"); builder.Property(x => x.PickNumber).HasColumnName("pick_number"); builder.Property(x => x.RoundNumber).HasColumnName("round_number"); builder.Property(x => x.PickedAt).HasColumnName("picked_at"); builder.Property(x => x.UndoneAt).HasColumnName("undone_at"); builder.HasIndex(x => new { x.DraftSessionId, x.PickNumber }).HasFilter("undone_at IS NULL").IsUnique(); builder.HasIndex(x => x.EventParticipantId).HasFilter("undone_at IS NULL").IsUnique();
    }
}
