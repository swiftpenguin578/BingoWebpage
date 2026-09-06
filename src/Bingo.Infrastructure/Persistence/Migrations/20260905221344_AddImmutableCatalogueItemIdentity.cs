using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddImmutableCatalogueItemIdentity : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "item_id_snapshot",
                table: "board_requirement_drop_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "item_id_snapshot",
                table: "board_approval_requirement_drop_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                CREATE TEMP TABLE IF NOT EXISTS bingo_item_snapshot_mapping
                (
                    snapshot_family text NOT NULL,
                    snapshot_id uuid NOT NULL,
                    item_id uuid NOT NULL,
                    PRIMARY KEY (snapshot_family, snapshot_id)
                );

                DO $$
                DECLARE
                    problems text;
                BEGIN
                    SELECT string_agg(problem, E'\n' ORDER BY family, snapshot_id)
                    INTO problems
                    FROM
                    (
                        SELECT 'event'::text AS family,
                               snapshot.id AS snapshot_id,
                               format('event=%s/"%s" requirement=%s snapshot=%s sourceDrop=%s name="%s"', event_item.id, event_item.name, requirement.id, snapshot.id, snapshot.source_drop_id, snapshot.item_name) AS problem
                        FROM board_requirement_drop_snapshots snapshot
                        JOIN board_requirement_snapshots requirement ON requirement.id = snapshot.requirement_id
                        JOIN board_tiles tile ON tile.id = requirement.board_tile_id
                        JOIN boards board ON board.id = tile.board_id
                        JOIN events event_item ON event_item.id = board.event_id
                        WHERE requirement.manual_objective
                        UNION ALL
                        SELECT 'approval'::text AS family,
                               snapshot.id AS snapshot_id,
                               format('event=%s/"%s" requirement=%s snapshot=%s sourceDrop=%s name="%s"', event_item.id, event_item.name, requirement.board_requirement_snapshot_id, snapshot.id, snapshot.source_drop_id, snapshot.item_name) AS problem
                        FROM board_approval_requirement_drop_snapshots snapshot
                        JOIN board_approval_requirement_snapshots requirement ON requirement.id = snapshot.approval_requirement_snapshot_id
                        JOIN board_approval_tile_snapshots tile ON tile.id = requirement.approval_tile_snapshot_id
                        JOIN board_approval_snapshots approval ON approval.id = tile.approval_snapshot_id
                        JOIN boards board ON board.id = approval.board_id
                        JOIN events event_item ON event_item.id = board.event_id
                        WHERE requirement.manual_objective
                    ) invalid;
                    IF problems IS NOT NULL THEN
                        RAISE EXCEPTION 'Immutable catalogue item migration found drop snapshots attached to manual objectives. Correct or remove these rows before retrying:%', E'\n' || problems;
                    END IF;
                END $$;

                UPDATE board_requirement_drop_snapshots snapshot
                SET item_id_snapshot = source_drop.item_id
                FROM source_drops source_drop
                JOIN catalogue_items current_item ON current_item.id = source_drop.item_id
                WHERE snapshot.source_drop_id = source_drop.id
                  AND snapshot.item_id_snapshot IS NULL
                  AND current_item.name = snapshot.item_name
                  AND (SELECT count(*) FROM catalogue_items candidate WHERE candidate.name = snapshot.item_name) = 1;

                UPDATE board_approval_requirement_drop_snapshots snapshot
                SET item_id_snapshot = source_drop.item_id
                FROM source_drops source_drop
                JOIN catalogue_items current_item ON current_item.id = source_drop.item_id
                WHERE snapshot.source_drop_id = source_drop.id
                  AND snapshot.item_id_snapshot IS NULL
                  AND current_item.name = snapshot.item_name
                  AND (SELECT count(*) FROM catalogue_items candidate WHERE candidate.name = snapshot.item_name) = 1;

                UPDATE board_requirement_drop_snapshots snapshot
                SET item_id_snapshot = mapping.item_id
                FROM bingo_item_snapshot_mapping mapping
                WHERE mapping.snapshot_family = 'event'
                  AND mapping.snapshot_id = snapshot.id
                  AND snapshot.item_id_snapshot IS NULL;

                UPDATE board_approval_requirement_drop_snapshots snapshot
                SET item_id_snapshot = mapping.item_id
                FROM bingo_item_snapshot_mapping mapping
                WHERE mapping.snapshot_family = 'approval'
                  AND mapping.snapshot_id = snapshot.id
                  AND snapshot.item_id_snapshot IS NULL;

                DO $$
                DECLARE
                    problems text;
                BEGIN
                    SELECT string_agg(problem, E'\n' ORDER BY family, snapshot_id)
                    INTO problems
                    FROM
                    (
                        SELECT 'event'::text AS family,
                               snapshot.id AS snapshot_id,
                               format('event=%s requirement=%s sourceDrop=%s frozenItem=%s currentMapping=%s', event_item.id, requirement.id, snapshot.source_drop_id, snapshot.item_name, coalesce(current_item.id::text || '/' || current_item.name, '-')) AS problem
                        FROM board_requirement_drop_snapshots snapshot
                        JOIN board_requirement_snapshots requirement ON requirement.id = snapshot.requirement_id
                        JOIN board_tiles tile ON tile.id = requirement.board_tile_id
                        JOIN boards board ON board.id = tile.board_id
                        JOIN events event_item ON event_item.id = board.event_id
                        LEFT JOIN source_drops source_drop ON source_drop.id = snapshot.source_drop_id
                        LEFT JOIN catalogue_items current_item ON current_item.id = source_drop.item_id
                        WHERE snapshot.item_id_snapshot IS NULL
                        UNION ALL
                        SELECT 'approval'::text AS family,
                               snapshot.id AS snapshot_id,
                               format('event=%s requirement=%s sourceDrop=%s frozenItem=%s currentMapping=%s', event_item.id, requirement.board_requirement_snapshot_id, snapshot.source_drop_id, snapshot.item_name, coalesce(current_item.id::text || '/' || current_item.name, '-')) AS problem
                        FROM board_approval_requirement_drop_snapshots snapshot
                        JOIN board_approval_requirement_snapshots requirement ON requirement.id = snapshot.approval_requirement_snapshot_id
                        JOIN board_approval_tile_snapshots tile ON tile.id = requirement.approval_tile_snapshot_id
                        JOIN board_approval_snapshots approval ON approval.id = tile.approval_snapshot_id
                        JOIN boards board ON board.id = approval.board_id
                        JOIN events event_item ON event_item.id = board.event_id
                        LEFT JOIN source_drops source_drop ON source_drop.id = snapshot.source_drop_id
                        LEFT JOIN catalogue_items current_item ON current_item.id = source_drop.item_id
                        WHERE snapshot.item_id_snapshot IS NULL
                    ) flagged;
                    IF problems IS NOT NULL THEN
                        RAISE EXCEPTION 'Immutable catalogue item snapshot backfill incomplete:%', E'\n' || problems;
                    END IF;
                END $$;

                DROP TABLE IF EXISTS bingo_item_snapshot_mapping;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "item_id_snapshot",
                table: "board_requirement_drop_snapshots",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "item_id_snapshot",
                table: "board_approval_requirement_drop_snapshots",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "item_id_snapshot",
                table: "board_requirement_drop_snapshots");

            migrationBuilder.DropColumn(
                name: "item_id_snapshot",
                table: "board_approval_requirement_drop_snapshots");
        }
}
