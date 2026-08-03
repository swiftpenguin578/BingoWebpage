using Bingo.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams", table => table.HasCheckConstraint("ck_teams_formation_draft", "(formation_type = 'Drafted' AND included_in_draft) OR (formation_type = 'Preformed' AND NOT included_in_draft)")); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); builder.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(150); builder.Ignore(x => x.ImageUrl); builder.Property(x => x.ActiveImageAssetId).HasColumnName("active_image_asset_id"); builder.Property(x => x.FormationType).HasColumnName("formation_type").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.AffiliationName).HasColumnName("affiliation_name").HasMaxLength(200); builder.Property(x => x.IncludedInDraft).HasColumnName("included_in_draft"); builder.Property(x => x.DraftPosition).HasColumnName("draft_position"); builder.Property(x => x.Active).HasColumnName("active"); builder.Property(x => x.FinalizedAt).HasColumnName("finalized_at"); builder.Property(x => x.CreatedAt).HasColumnName("created_at"); builder.Property(x => x.MetadataLockedAt).HasColumnName("metadata_locked_at"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.HasIndex(x => new { x.EventId, x.Slug }).IsUnique(); builder.HasIndex(x => new { x.EventId, x.Name }).IsUnique().HasFilter("active"); builder.HasAlternateKey(x => new { x.EventId, x.Id }); builder.HasOne<TeamImageAsset>().WithMany().HasForeignKey(x => x.ActiveImageAssetId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class TeamImageAssetConfiguration : IEntityTypeConfiguration<TeamImageAsset>
{
    public void Configure(EntityTypeBuilder<TeamImageAsset> builder)
    { builder.ToTable("team_image_assets"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(500); builder.Property(x => x.OriginalFilename).HasColumnName("original_filename").HasMaxLength(255); builder.Property(x => x.MediaType).HasColumnName("media_type").HasMaxLength(100); builder.Property(x => x.ByteSize).HasColumnName("byte_size"); builder.Property(x => x.Width).HasColumnName("width"); builder.Property(x => x.Height).HasColumnName("height"); builder.Property(x => x.Checksum).HasColumnName("checksum").HasMaxLength(64); builder.Property(x => x.UploadedByAccountId).HasColumnName("uploaded_by_account_id"); builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at"); builder.Property(x => x.ReplacedAt).HasColumnName("replaced_at"); builder.HasIndex(x => new { x.TeamId, x.ReplacedAt }); builder.HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("team_memberships"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.EventParticipantId).HasColumnName("event_participant_id"); builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.JoinedAt).HasColumnName("joined_at"); builder.Property(x => x.LeftAt).HasColumnName("left_at"); builder.Property(x => x.AssignedByDraftPickId).HasColumnName("assigned_by_draft_pick_id"); builder.Property(x => x.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(30); builder.Property(x => x.ReplacesMembershipId).HasColumnName("replaces_membership_id"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.Property(x => x.AssignmentReason).HasColumnName("assignment_reason").HasMaxLength(1000); builder.HasIndex(x => new { x.TeamId, x.EventParticipantId }); builder.HasIndex(x => x.EventParticipantId).HasFilter("left_at IS NULL").IsUnique(); builder.HasIndex(x => x.ReplacesMembershipId).IsUnique().HasFilter("replaces_membership_id IS NOT NULL"); builder.HasOne<TeamMembership>().WithMany().HasForeignKey(x => x.ReplacesMembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TeamMembershipRoleTransitionConfiguration : IEntityTypeConfiguration<TeamMembershipRoleTransition>
{ public void Configure(EntityTypeBuilder<TeamMembershipRoleTransition> builder) { builder.ToTable("team_membership_role_transitions"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.TeamMembershipId).HasColumnName("team_membership_id"); builder.Property(x => x.FromRole).HasColumnName("from_role").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.ToRole).HasColumnName("to_role").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.ChangedByAccountId).HasColumnName("changed_by_account_id"); builder.Property(x => x.ChangedAt).HasColumnName("changed_at"); builder.HasIndex(x => new { x.TeamMembershipId, x.ChangedAt }); builder.HasOne<TeamMembership>().WithMany().HasForeignKey(x => x.TeamMembershipId).OnDelete(DeleteBehavior.Restrict); } }

public sealed class DraftSessionConfiguration : IEntityTypeConfiguration<DraftSession>
{
    public void Configure(EntityTypeBuilder<DraftSession> builder)
    {
        builder.ToTable("draft_sessions"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.TargetTeamSize).HasColumnName("target_team_size"); builder.Property(x => x.State).HasColumnName("state").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.LockedAt).HasColumnName("locked_at"); builder.Property(x => x.FinalizedAt).HasColumnName("finalized_at"); builder.Property(x => x.FirstPickRecordedAt).HasColumnName("first_pick_recorded_at"); builder.Property(x => x.ControllerAccountId).HasColumnName("controller_account_id"); builder.Property(x => x.ControllerLeaseExpiresAt).HasColumnName("controller_lease_expires_at"); builder.Property(x => x.ControlVersion).HasColumnName("control_version").IsConcurrencyToken(); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.HasIndex(x => x.EventId).IsUnique();
    }
}

public sealed class DraftPublicationCycleConfiguration : IEntityTypeConfiguration<DraftPublicationCycle>
{ public void Configure(EntityTypeBuilder<DraftPublicationCycle> builder) { builder.ToTable("draft_publication_cycles"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.DraftSessionId).HasColumnName("draft_session_id"); builder.Property(x => x.CycleNumber).HasColumnName("cycle_number"); builder.Property(x => x.PublishedAt).HasColumnName("published_at"); builder.Property(x => x.PublishedByAccountId).HasColumnName("published_by_account_id"); builder.Property(x => x.SupersededAt).HasColumnName("superseded_at"); builder.Property(x => x.SupersededByAccountId).HasColumnName("superseded_by_account_id"); builder.Property(x => x.ReopenReason).HasColumnName("reopen_reason").HasMaxLength(2000); builder.HasIndex(x => new { x.DraftSessionId, x.CycleNumber }).IsUnique(); builder.HasIndex(x => x.DraftSessionId).IsUnique().HasFilter("superseded_at IS NULL"); builder.HasOne<DraftSession>().WithMany().HasForeignKey(x => x.DraftSessionId).OnDelete(DeleteBehavior.Restrict); } }
public sealed class DraftPublicationRosterConfiguration : IEntityTypeConfiguration<DraftPublicationRoster>
{ public void Configure(EntityTypeBuilder<DraftPublicationRoster> builder) { builder.ToTable("draft_publication_rosters"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.DraftPublicationCycleId).HasColumnName("draft_publication_cycle_id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.EventParticipantId).HasColumnName("event_participant_id"); builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.EffectivePickNumber).HasColumnName("effective_pick_number"); builder.Property(x => x.PublicCharacterName).HasColumnName("public_character_name").HasMaxLength(100).IsRequired(); builder.HasIndex(x => new { x.DraftPublicationCycleId, x.TeamId, x.EventParticipantId }).IsUnique(); builder.HasIndex(x => new { x.DraftPublicationCycleId, x.EffectivePickNumber }).IsUnique().HasFilter("effective_pick_number IS NOT NULL"); builder.HasOne<DraftPublicationCycle>().WithMany().HasForeignKey(x => x.DraftPublicationCycleId).OnDelete(DeleteBehavior.Restrict); } }

public sealed class DraftPickConfiguration : IEntityTypeConfiguration<DraftPick>
{
    public void Configure(EntityTypeBuilder<DraftPick> builder)
    {
        builder.ToTable("draft_picks"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.DraftSessionId).HasColumnName("draft_session_id"); builder.Property(x => x.TeamId).HasColumnName("team_id"); builder.Property(x => x.EventParticipantId).HasColumnName("event_participant_id"); builder.Property(x => x.PickNumber).HasColumnName("pick_number"); builder.Property(x => x.RoundNumber).HasColumnName("round_number"); builder.Property(x => x.PickedAt).HasColumnName("picked_at"); builder.Property(x => x.UndoneAt).HasColumnName("undone_at"); builder.HasIndex(x => new { x.DraftSessionId, x.PickNumber }).HasFilter("undone_at IS NULL").IsUnique(); builder.HasIndex(x => x.EventParticipantId).HasFilter("undone_at IS NULL").IsUnique();
    }
}
