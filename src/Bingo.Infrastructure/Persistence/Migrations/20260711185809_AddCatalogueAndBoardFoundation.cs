using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCatalogueAndBoardFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "boards",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                rows = table.Column<int>(type: "integer", nullable: false),
                columns = table.Column<int>(type: "integer", nullable: false),
                state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                total_ehb_estimate = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                calculation_version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_boards", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "boss_activities",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                slug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                efficient_completions_per_hour = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                external_identifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                data_source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                data_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                active = table.Column<bool>(type: "boolean", nullable: false),
                notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_boss_activities", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "catalogue_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                external_identifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                active = table.Column<bool>(type: "boolean", nullable: false),
                notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalogue_items", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "source_drops",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                boss_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                item_id = table.Column<Guid>(type: "uuid", nullable: false),
                display_rate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                numeric_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                rate_condition_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                default_ehb_estimate = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                data_source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                data_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_source_drops", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "template_requirement_bosses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                boss_activity_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_template_requirement_bosses", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "template_requirement_drops",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_drop_id = table.Column<Guid>(type: "uuid", nullable: false),
                maximum_contribution = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_template_requirement_drops", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tile_template_requirements",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tile_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                target_contribution = table.Column<int>(type: "integer", nullable: false),
                duplicates_allowed = table.Column<bool>(type: "boolean", nullable: false),
                allow_higher_weightings = table.Column<bool>(type: "boolean", nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                manual_objective = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tile_template_requirements", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tile_templates",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                objective_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                evidence_instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                manual_ehb_override = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tile_templates", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_boards_event_id",
            table: "boards",
            column: "event_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_boss_activities_slug",
            table: "boss_activities",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_catalogue_items_normalized_name",
            table: "catalogue_items",
            column: "normalized_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_source_drops_boss_activity_id_item_id",
            table: "source_drops",
            columns: ["boss_activity_id", "item_id"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_template_requirement_bosses_requirement_id_boss_activity_id",
            table: "template_requirement_bosses",
            columns: ["requirement_id", "boss_activity_id"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_template_requirement_drops_requirement_id_source_drop_id",
            table: "template_requirement_drops",
            columns: ["requirement_id", "source_drop_id"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_tile_template_requirements_tile_template_id_position",
            table: "tile_template_requirements",
            columns: ["tile_template_id", "position"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "boards");

        migrationBuilder.DropTable(
            name: "boss_activities");

        migrationBuilder.DropTable(
            name: "catalogue_items");

        migrationBuilder.DropTable(
            name: "source_drops");

        migrationBuilder.DropTable(
            name: "template_requirement_bosses");

        migrationBuilder.DropTable(
            name: "template_requirement_drops");

        migrationBuilder.DropTable(
            name: "tile_template_requirements");

        migrationBuilder.DropTable(
            name: "tile_templates");
    }
}
