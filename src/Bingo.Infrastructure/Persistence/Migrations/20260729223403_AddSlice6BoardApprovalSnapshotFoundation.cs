using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSlice6BoardApprovalSnapshotFoundation : Migration
{
    private static readonly string[] RateVariantIndexColumns = ["approval_requirement_drop_snapshot_id", "rate_variant_id"];
    private static readonly string[] RequirementBossIndexColumns = ["approval_requirement_snapshot_id", "boss_activity_id"];
    private static readonly string[] RequirementDropIndexColumns = ["approval_requirement_snapshot_id", "source_drop_id"];
    private static readonly string[] RequirementPositionIndexColumns = ["approval_tile_snapshot_id", "position"];
    private static readonly string[] RequirementIdentityIndexColumns = ["approval_tile_snapshot_id", "board_requirement_snapshot_id"];
    private static readonly string[] BoardSnapshotVersionIndexColumns = ["board_id", "version"];
    private static readonly string[] TileIdentityIndexColumns = ["approval_snapshot_id", "board_tile_id"];
    private static readonly string[] TilePositionIndexColumns = ["approval_snapshot_id", "row_index", "column_index"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "source_drops",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "source_drop_rate_variants",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "catalogue_items",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "boss_activities",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.AddColumn<Guid>(
            name: "active_approval_snapshot_id",
            table: "boards",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "board_approval_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                board_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                approved_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                supersedes_approval_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                rows = table.Column<int>(type: "integer", nullable: false),
                columns = table.Column<int>(type: "integer", nullable: false),
                total_ehb_estimate = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                calculation_version = table.Column<int>(type: "integer", nullable: false),
                board_version = table.Column<long>(type: "bigint", nullable: false),
                lifecycle_state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_approval_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_board_approval_snapshots_board_approval_snapshots_supersede~",
                    column: x => x.supersedes_approval_snapshot_id,
                    principalTable: "board_approval_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_board_approval_snapshots_boards_board_id",
                    column: x => x.board_id,
                    principalTable: "boards",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "board_approval_tile_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                approval_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                board_tile_id = table.Column<Guid>(type: "uuid", nullable: false),
                tile_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                row_index = table.Column<int>(type: "integer", nullable: false),
                column_index = table.Column<int>(type: "integer", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                evidence_instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                estimated_ehb = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                artwork_reference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_approval_tile_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_board_approval_tile_snapshots_board_approval_snapshots_appr~",
                    column: x => x.approval_snapshot_id,
                    principalTable: "board_approval_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "board_approval_requirement_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                approval_tile_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                board_requirement_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                target_contribution = table.Column<int>(type: "integer", nullable: false),
                duplicates_allowed = table.Column<bool>(type: "boolean", nullable: false),
                allow_higher_weightings = table.Column<bool>(type: "boolean", nullable: false),
                credited_weight = table.Column<int>(type: "integer", nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                manual_objective = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_approval_requirement_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_board_approval_requirement_snapshots_board_approval_tile_sn~",
                    column: x => x.approval_tile_snapshot_id,
                    principalTable: "board_approval_tile_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "board_approval_requirement_boss_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                approval_requirement_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                boss_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                efficient_rate = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                catalogue_version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_approval_requirement_boss_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_board_approval_requirement_boss_snapshots_board_approval_re~",
                    column: x => x.approval_requirement_snapshot_id,
                    principalTable: "board_approval_requirement_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "board_approval_requirement_drop_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                approval_requirement_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_drop_id = table.Column<Guid>(type: "uuid", nullable: false),
                boss_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                display_rate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                numeric_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                probability_scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                conditional_on_parent = table.Column<bool>(type: "boolean", nullable: false),
                parent_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                assumed_participants = table.Column<int>(type: "integer", nullable: false),
                rolls_per_completion = table.Column<int>(type: "integer", nullable: false),
                roll_group = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                rate_condition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                maximum_contribution = table.Column<int>(type: "integer", nullable: true),
                ehb_per_contribution = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                credited_weight = table.Column<int>(type: "integer", nullable: false),
                catalogue_version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_approval_requirement_drop_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_board_approval_requirement_drop_snapshots_board_approval_re~",
                    column: x => x.approval_requirement_snapshot_id,
                    principalTable: "board_approval_requirement_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "board_approval_rate_variant_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                approval_requirement_drop_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                rate_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                display_rate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                numeric_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                condition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                catalogue_version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_approval_rate_variant_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_board_approval_rate_variant_snapshots_board_approval_requir~",
                    column: x => x.approval_requirement_drop_snapshot_id,
                    principalTable: "board_approval_requirement_drop_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_boards_active_approval_snapshot_id",
            table: "boards",
            column: "active_approval_snapshot_id");

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_rate_variant_snapshots_approval_requirement_~",
            table: "board_approval_rate_variant_snapshots",
            columns: RateVariantIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_requirement_boss_snapshots_approval_requirem~",
            table: "board_approval_requirement_boss_snapshots",
            columns: RequirementBossIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_requirement_drop_snapshots_approval_requirem~",
            table: "board_approval_requirement_drop_snapshots",
            columns: RequirementDropIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_requirement_snapshots_approval_tile_snapsho~1",
            table: "board_approval_requirement_snapshots",
            columns: RequirementPositionIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_requirement_snapshots_approval_tile_snapshot~",
            table: "board_approval_requirement_snapshots",
            columns: RequirementIdentityIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_snapshots_board_id_version",
            table: "board_approval_snapshots",
            columns: BoardSnapshotVersionIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_snapshots_supersedes_approval_snapshot_id",
            table: "board_approval_snapshots",
            column: "supersedes_approval_snapshot_id");

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_tile_snapshots_approval_snapshot_id_board_ti~",
            table: "board_approval_tile_snapshots",
            columns: TileIdentityIndexColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_tile_snapshots_approval_snapshot_id_row_inde~",
            table: "board_approval_tile_snapshots",
            columns: TilePositionIndexColumns,
            unique: true);

        migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM boards board
                        WHERE board.state = 'Published'
                          AND (
                              board.published_at IS NULL
                              OR (SELECT count(*) FROM board_tiles tile WHERE tile.board_id = board.id) <> board.rows * board.columns
                              OR EXISTS (
                                  SELECT 1
                                  FROM board_tiles tile
                                  WHERE tile.board_id = board.id
                                    AND (tile.row_index < 0 OR tile.row_index >= board.rows OR tile.column_index < 0 OR tile.column_index >= board.columns)
                              )
                              OR EXISTS (
                                  SELECT 1
                                  FROM board_tiles tile
                                  WHERE tile.board_id = board.id
                                    AND NOT EXISTS (SELECT 1 FROM board_requirement_snapshots requirement WHERE requirement.board_tile_id = tile.id)
                              )
                          )
                    ) THEN
                        RAISE EXCEPTION 'Cannot backfill an approval snapshot from an incomplete or ambiguous retained published board.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM board_requirement_snapshots requirement
                        JOIN board_tiles tile ON tile.id = requirement.board_tile_id
                        JOIN boards board ON board.id = tile.board_id
                        WHERE board.state = 'Published'
                        GROUP BY requirement.board_tile_id, requirement.position
                        HAVING count(*) <> 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot backfill an approval snapshot from retained board requirements with ambiguous positions.';
                    END IF;
                END $$;

                INSERT INTO board_approval_snapshots
                    (id, board_id, version, approved_at, approved_by_account_id, supersedes_approval_snapshot_id,
                     name, rows, columns, total_ehb_estimate, calculation_version, board_version, lifecycle_state)
                SELECT md5(board.id::text || ':approval:1')::uuid, board.id, 1, board.published_at, NULL, NULL,
                       board.name, board.rows, board.columns, board.total_ehb_estimate, board.calculation_version, board.version, board.state
                FROM boards board
                WHERE board.state = 'Published';

                INSERT INTO board_approval_tile_snapshots
                    (id, approval_snapshot_id, board_tile_id, tile_template_id, row_index, column_index,
                     name, description, evidence_instructions, estimated_ehb, artwork_reference)
                SELECT md5(tile.board_id::text || ':approval-tile:' || tile.id::text)::uuid,
                       md5(tile.board_id::text || ':approval:1')::uuid,
                       tile.id, tile.tile_template_id, tile.row_index, tile.column_index,
                       tile.name_snapshot, tile.description_snapshot, tile.evidence_instructions_snapshot,
                       tile.estimated_ehb_snapshot, tile.image_url_snapshot
                FROM board_tiles tile
                JOIN boards board ON board.id = tile.board_id
                WHERE board.state = 'Published';

                INSERT INTO board_approval_requirement_snapshots
                    (id, approval_tile_snapshot_id, board_requirement_snapshot_id, position, target_contribution,
                     duplicates_allowed, allow_higher_weightings, credited_weight, description, manual_objective)
                SELECT md5(tile.board_id::text || ':approval-requirement:' || requirement.id::text)::uuid,
                       md5(tile.board_id::text || ':approval-tile:' || tile.id::text)::uuid,
                       requirement.id, requirement.position, requirement.target_contribution,
                       requirement.duplicates_allowed, requirement.allow_higher_weightings, requirement.credited_weight,
                       requirement.description, requirement.manual_objective
                FROM board_requirement_snapshots requirement
                JOIN board_tiles tile ON tile.id = requirement.board_tile_id
                JOIN boards board ON board.id = tile.board_id
                WHERE board.state = 'Published';

                INSERT INTO board_approval_requirement_boss_snapshots
                    (id, approval_requirement_snapshot_id, boss_activity_id, name, efficient_rate, catalogue_version)
                SELECT md5(tile.board_id::text || ':approval-boss:' || requirement.id::text || ':' || boss.id::text)::uuid,
                       md5(tile.board_id::text || ':approval-requirement:' || requirement.id::text)::uuid,
                       boss.boss_activity_id, boss.boss_name, boss.efficient_rate, COALESCE(activity.version, 1)
                FROM board_requirement_boss_snapshots boss
                JOIN board_requirement_snapshots requirement ON requirement.id = boss.requirement_id
                JOIN board_tiles tile ON tile.id = requirement.board_tile_id
                JOIN boards board ON board.id = tile.board_id
                LEFT JOIN boss_activities activity ON activity.id = boss.boss_activity_id
                WHERE board.state = 'Published';

                INSERT INTO board_approval_requirement_drop_snapshots
                    (id, approval_requirement_snapshot_id, source_drop_id, boss_name, item_name, display_rate,
                     numeric_probability, probability_scope, conditional_on_parent, parent_probability, assumed_participants,
                     rolls_per_completion, roll_group, rate_condition, maximum_contribution, ehb_per_contribution, credited_weight, catalogue_version)
                SELECT md5(tile.board_id::text || ':approval-drop:' || requirement.id::text || ':' || drop_snapshot.id::text)::uuid,
                       md5(tile.board_id::text || ':approval-requirement:' || requirement.id::text)::uuid,
                       drop_snapshot.source_drop_id, drop_snapshot.boss_name, drop_snapshot.item_name, drop_snapshot.display_rate,
                       drop_snapshot.numeric_probability, COALESCE(source_drop.probability_scope, 'Participant'),
                       COALESCE(source_drop.conditional_on_parent, FALSE), source_drop.parent_probability,
                       COALESCE(source_drop.assumed_participants, 1), COALESCE(source_drop.rolls_per_completion, 1),
                       COALESCE(source_drop.roll_group, 'default'), source_drop.rate_condition_note,
                       drop_snapshot.maximum_contribution, drop_snapshot.ehb_per_contribution, drop_snapshot.credited_weight,
                       COALESCE(source_drop.version, 1)
                FROM board_requirement_drop_snapshots drop_snapshot
                JOIN board_requirement_snapshots requirement ON requirement.id = drop_snapshot.requirement_id
                JOIN board_tiles tile ON tile.id = requirement.board_tile_id
                JOIN boards board ON board.id = tile.board_id
                LEFT JOIN source_drops source_drop ON source_drop.id = drop_snapshot.source_drop_id
                WHERE board.state = 'Published';

                INSERT INTO board_approval_rate_variant_snapshots
                    (id, approval_requirement_drop_snapshot_id, rate_variant_id, position, label, display_rate,
                     numeric_probability, condition, catalogue_version)
                SELECT md5(tile.board_id::text || ':approval-rate-variant:' || drop_snapshot.id::text || ':' || variant.id::text)::uuid,
                       md5(tile.board_id::text || ':approval-drop:' || requirement.id::text || ':' || drop_snapshot.id::text)::uuid,
                       variant.id, variant.position, variant.label, variant.display_rate, variant.numeric_probability,
                       variant.condition, variant.version
                FROM board_requirement_drop_snapshots drop_snapshot
                JOIN board_requirement_snapshots requirement ON requirement.id = drop_snapshot.requirement_id
                JOIN board_tiles tile ON tile.id = requirement.board_tile_id
                JOIN boards board ON board.id = tile.board_id
                JOIN source_drop_rate_variants variant ON variant.source_drop_id = drop_snapshot.source_drop_id
                WHERE board.state = 'Published';

                UPDATE boards board
                   SET active_approval_snapshot_id = md5(board.id::text || ':approval:1')::uuid
                 WHERE board.state = 'Published';
                """);

        migrationBuilder.AddForeignKey(
            name: "FK_boards_board_approval_snapshots_active_approval_snapshot_id",
            table: "boards",
            column: "active_approval_snapshot_id",
            principalTable: "board_approval_snapshots",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_boards_board_approval_snapshots_active_approval_snapshot_id",
            table: "boards");

        migrationBuilder.DropTable(
            name: "board_approval_rate_variant_snapshots");

        migrationBuilder.DropTable(
            name: "board_approval_requirement_boss_snapshots");

        migrationBuilder.DropTable(
            name: "board_approval_requirement_drop_snapshots");

        migrationBuilder.DropTable(
            name: "board_approval_requirement_snapshots");

        migrationBuilder.DropTable(
            name: "board_approval_tile_snapshots");

        migrationBuilder.DropTable(
            name: "board_approval_snapshots");

        migrationBuilder.DropIndex(
            name: "IX_boards_active_approval_snapshot_id",
            table: "boards");

        migrationBuilder.DropColumn(
            name: "version",
            table: "source_drops");

        migrationBuilder.DropColumn(
            name: "version",
            table: "source_drop_rate_variants");

        migrationBuilder.DropColumn(
            name: "version",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "version",
            table: "boss_activities");

        migrationBuilder.DropColumn(
            name: "active_approval_snapshot_id",
            table: "boards");
    }
}
