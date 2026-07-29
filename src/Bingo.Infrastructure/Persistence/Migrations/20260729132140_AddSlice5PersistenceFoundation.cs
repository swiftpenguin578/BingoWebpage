using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSlice5PersistenceFoundation : Migration
{
    private static readonly string[] TeamNameColumns = ["event_id", "name"];
    private static readonly string[] PublicationCycleColumns = ["draft_session_id", "cycle_number"];
    private static readonly string[] PublicationPickColumns = ["draft_publication_cycle_id", "effective_pick_number"];
    private static readonly string[] PublicationRosterColumns = ["draft_publication_cycle_id", "team_id", "event_participant_id"];
    private static readonly string[] TeamAssetColumns = ["team_id", "replaced_at"];
    private static readonly string[] RoleTransitionColumns = ["team_membership_id", "changed_at"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Retained arbitrary URLs are never fetched or served again. Preserve their
        // provenance as explicit non-serving legacy metadata before removing authority.
        migrationBuilder.CreateTable(
            name: "team_legacy_image_references",
            columns: table => new
            {
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                retired_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_team_legacy_image_references", x => x.team_id));
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM team_memberships m JOIN teams t ON t.id = m.team_id JOIN event_participants p ON p.id = m.event_participant_id WHERE t.event_id <> p.event_id) THEN RAISE EXCEPTION 'Slice 5 conversion blocked: cross-event team membership.'; END IF;
              IF EXISTS (SELECT 1 FROM draft_picks p JOIN draft_sessions d ON d.id = p.draft_session_id JOIN teams t ON t.id = p.team_id JOIN event_participants ep ON ep.id = p.event_participant_id WHERE d.event_id <> t.event_id OR d.event_id <> ep.event_id) THEN RAISE EXCEPTION 'Slice 5 conversion blocked: cross-event draft pick.'; END IF;
              IF EXISTS (SELECT 1 FROM draft_sessions d WHERE d.state = 'Finalized' AND EXISTS (SELECT 1 FROM draft_picks p WHERE p.draft_session_id = d.id AND p.undone_at IS NULL AND NOT EXISTS (SELECT 1 FROM team_memberships m WHERE m.assigned_by_draft_pick_id = p.id AND m.left_at IS NULL))) THEN RAISE EXCEPTION 'Slice 5 conversion blocked: finalized draft has an ambiguous effective pick projection.'; END IF;
              IF EXISTS (SELECT 1 FROM draft_picks p LEFT JOIN team_memberships m ON m.assigned_by_draft_pick_id = p.id WHERE p.undone_at IS NULL AND (m.id IS NULL OR m.left_at IS NOT NULL OR m.team_id <> p.team_id OR m.event_participant_id <> p.event_participant_id)) THEN RAISE EXCEPTION 'Slice 5 conversion blocked: active pick lacks its current membership.'; END IF;
            END $$;
            INSERT INTO team_legacy_image_references (team_id, retired_url, retired_at)
            SELECT id, image_url, CURRENT_TIMESTAMP FROM teams WHERE image_url IS NOT NULL AND btrim(image_url) <> '';
            """);
        migrationBuilder.DropColumn(
            name: "image_url",
            table: "teams");

        migrationBuilder.AddColumn<Guid>(
            name: "active_image_asset_id",
            table: "teams",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "created_at",
            table: "teams",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "metadata_locked_at",
            table: "teams",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "teams",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.Sql("UPDATE teams t SET created_at = e.created_at, version = 1 FROM events e WHERE e.id = t.event_id;");

        migrationBuilder.AddColumn<Guid>(
            name: "replaces_membership_id",
            table: "team_memberships",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "source",
            table: "team_memberships",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "team_memberships",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.Sql("""
            UPDATE team_memberships m SET source = CASE WHEN assigned_by_draft_pick_id IS NOT NULL THEN 'DraftPick' WHEN EXISTS (SELECT 1 FROM teams t WHERE t.id = m.team_id AND t.formation_type = 'Preformed') THEN 'PreformedManual' ELSE 'RetainedConversion' END, version = 1;
            """);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "first_pick_recorded_at",
            table: "draft_sessions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "draft_sessions",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.Sql("UPDATE draft_sessions d SET first_pick_recorded_at = p.first_picked_at, version = 1 FROM (SELECT draft_session_id, MIN(picked_at) AS first_picked_at FROM draft_picks GROUP BY draft_session_id) p WHERE p.draft_session_id = d.id;");

        migrationBuilder.CreateTable(
            name: "draft_publication_cycles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                draft_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                cycle_number = table.Column<int>(type: "integer", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                published_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                superseded_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                reopen_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_draft_publication_cycles", x => x.id);
                table.ForeignKey(
                    name: "FK_draft_publication_cycles_draft_sessions_draft_session_id",
                    column: x => x.draft_session_id,
                    principalTable: "draft_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "team_image_assets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                media_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                byte_size = table.Column<long>(type: "bigint", nullable: false),
                width = table.Column<int>(type: "integer", nullable: false),
                height = table.Column<int>(type: "integer", nullable: false),
                checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                uploaded_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                replaced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_team_image_assets", x => x.id);
                table.ForeignKey(
                    name: "FK_team_image_assets_teams_team_id",
                    column: x => x.team_id,
                    principalTable: "teams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "team_membership_role_transitions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                team_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                from_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                to_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                changed_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_team_membership_role_transitions", x => x.id);
                table.ForeignKey(
                    name: "FK_team_membership_role_transitions_team_memberships_team_memb~",
                    column: x => x.team_membership_id,
                    principalTable: "team_memberships",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "draft_publication_rosters",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                draft_publication_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                effective_pick_number = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_draft_publication_rosters", x => x.id);
                table.ForeignKey(
                    name: "FK_draft_publication_rosters_draft_publication_cycles_draft_pu~",
                    column: x => x.draft_publication_cycle_id,
                    principalTable: "draft_publication_cycles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql("""
            INSERT INTO team_membership_role_transitions (id, team_membership_id, from_role, to_role, changed_by_account_id, changed_at)
            SELECT md5('slice5-role-' || id::text)::uuid, id, role, role, NULL, joined_at FROM team_memberships;
            INSERT INTO draft_publication_cycles (id, draft_session_id, cycle_number, published_at, published_by_account_id)
            SELECT md5('slice5-cycle-' || d.id::text)::uuid, d.id, 1, COALESCE(d.finalized_at, CURRENT_TIMESTAMP), e.created_by_account_id
            FROM draft_sessions d JOIN events e ON e.id = d.event_id WHERE d.state = 'Finalized';
            INSERT INTO draft_publication_rosters (id, draft_publication_cycle_id, team_id, event_participant_id, role, effective_pick_number)
            SELECT md5('slice5-roster-' || c.id::text || '-' || m.id::text)::uuid, c.id, m.team_id, m.event_participant_id, m.role, p.pick_number
            FROM draft_publication_cycles c JOIN draft_sessions d ON d.id = c.draft_session_id JOIN teams t ON t.event_id = d.event_id JOIN team_memberships m ON m.team_id = t.id AND m.left_at IS NULL
            LEFT JOIN draft_picks p ON p.id = m.assigned_by_draft_pick_id AND p.draft_session_id = d.id AND p.team_id = m.team_id AND p.event_participant_id = m.event_participant_id AND p.undone_at IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_teams_active_image_asset_id",
            table: "teams",
            column: "active_image_asset_id");

        migrationBuilder.CreateIndex(
            name: "IX_teams_event_id_name",
            table: "teams",
            columns: TeamNameColumns,
            unique: true,
            filter: "active");

        migrationBuilder.AddCheckConstraint(
            name: "ck_teams_formation_draft",
            table: "teams",
            sql: "(formation_type = 'Drafted' AND included_in_draft) OR (formation_type = 'Preformed' AND NOT included_in_draft)");

        migrationBuilder.CreateIndex(
            name: "IX_team_memberships_replaces_membership_id",
            table: "team_memberships",
            column: "replaces_membership_id");

        migrationBuilder.CreateIndex(
            name: "IX_draft_publication_cycles_draft_session_id",
            table: "draft_publication_cycles",
            column: "draft_session_id",
            unique: true,
            filter: "superseded_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_draft_publication_cycles_draft_session_id_cycle_number",
            table: "draft_publication_cycles",
            columns: PublicationCycleColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_draft_publication_rosters_draft_publication_cycle_id_effect~",
            table: "draft_publication_rosters",
            columns: PublicationPickColumns,
            unique: true,
            filter: "effective_pick_number IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_draft_publication_rosters_draft_publication_cycle_id_team_i~",
            table: "draft_publication_rosters",
            columns: PublicationRosterColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_team_image_assets_team_id_replaced_at",
            table: "team_image_assets",
            columns: TeamAssetColumns);

        migrationBuilder.CreateIndex(
            name: "IX_team_membership_role_transitions_team_membership_id_changed~",
            table: "team_membership_role_transitions",
            columns: RoleTransitionColumns);

        migrationBuilder.AddForeignKey(
            name: "FK_team_memberships_team_memberships_replaces_membership_id",
            table: "team_memberships",
            column: "replaces_membership_id",
            principalTable: "team_memberships",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_teams_team_image_assets_active_image_asset_id",
            table: "teams",
            column: "active_image_asset_id",
            principalTable: "team_image_assets",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_team_memberships_team_memberships_replaces_membership_id",
            table: "team_memberships");

        migrationBuilder.DropForeignKey(
            name: "FK_teams_team_image_assets_active_image_asset_id",
            table: "teams");

        migrationBuilder.DropTable(
            name: "draft_publication_rosters");

        migrationBuilder.DropTable(
            name: "team_legacy_image_references");

        migrationBuilder.DropTable(
            name: "team_image_assets");

        migrationBuilder.DropTable(
            name: "team_membership_role_transitions");

        migrationBuilder.DropTable(
            name: "draft_publication_cycles");

        migrationBuilder.DropIndex(
            name: "IX_teams_active_image_asset_id",
            table: "teams");

        migrationBuilder.DropIndex(
            name: "IX_teams_event_id_name",
            table: "teams");

        migrationBuilder.DropCheckConstraint(
            name: "ck_teams_formation_draft",
            table: "teams");

        migrationBuilder.DropIndex(
            name: "IX_team_memberships_replaces_membership_id",
            table: "team_memberships");

        migrationBuilder.DropColumn(
            name: "active_image_asset_id",
            table: "teams");

        migrationBuilder.DropColumn(
            name: "created_at",
            table: "teams");

        migrationBuilder.DropColumn(
            name: "metadata_locked_at",
            table: "teams");

        migrationBuilder.DropColumn(
            name: "version",
            table: "teams");

        migrationBuilder.DropColumn(
            name: "replaces_membership_id",
            table: "team_memberships");

        migrationBuilder.DropColumn(
            name: "source",
            table: "team_memberships");

        migrationBuilder.DropColumn(
            name: "version",
            table: "team_memberships");

        migrationBuilder.DropColumn(
            name: "first_pick_recorded_at",
            table: "draft_sessions");

        migrationBuilder.DropColumn(
            name: "version",
            table: "draft_sessions");

        migrationBuilder.AddColumn<string>(
            name: "image_url",
            table: "teams",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);
    }
}
