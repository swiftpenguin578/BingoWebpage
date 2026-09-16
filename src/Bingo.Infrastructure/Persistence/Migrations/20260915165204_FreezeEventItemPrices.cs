using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class FreezeEventItemPrices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "item_prices_captured_at",
            table: "events",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "event_item_prices",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                item_id = table.Column<Guid>(type: "uuid", nullable: false),
                value_gp = table.Column<long>(type: "bigint", nullable: false),
                selected_hour = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                api_item_id = table.Column<int>(type: "integer", nullable: true),
                price_observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                fallback_catalogue_source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                fallback_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_item_prices", x => new { x.event_id, x.item_id });
                table.CheckConstraint("ck_event_item_price_hour", "date_trunc('hour', selected_hour AT TIME ZONE 'UTC') = selected_hour AT TIME ZONE 'UTC' AND selected_hour + interval '1 hour' <= captured_at");
                table.CheckConstraint("ck_event_item_price_provenance", "(source = 'WikiHourly' AND api_item_id IS NOT NULL AND price_observed_at IS NOT NULL AND price_observed_at = selected_hour AND fallback_catalogue_source IS NULL AND fallback_reason IS NULL)\nOR (source IN ('CatalogueFallback', 'CatalogueIntroduction') AND fallback_catalogue_source IS NOT NULL\n    AND fallback_catalogue_source IN ('Api', 'Manual', 'Untradeable') AND (fallback_catalogue_source <> 'Untradeable' OR value_gp = 0)\n    AND ((source = 'CatalogueIntroduction' AND fallback_reason IS NULL)\n        OR (source = 'CatalogueFallback' AND fallback_reason IS NOT NULL AND fallback_reason IN ('NoMapping', 'NoHourlyPrice', 'ProviderUnavailable', 'PreparedHourChanged'))))");
                table.CheckConstraint("ck_event_item_price_value", "value_gp >= 0 AND (api_item_id IS NULL OR api_item_id > 0)");
                table.ForeignKey(
                    name: "FK_event_item_prices_catalogue_items_item_id",
                    column: x => x.item_id,
                    principalTable: "catalogue_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_item_prices_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_item_prices_item_id",
            table: "event_item_prices",
            column: "item_id");
        migrationBuilder.Sql("""
            CREATE FUNCTION reject_event_item_price_update() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Event item prices are immutable' USING ERRCODE = '23514';
            END;
            $$;
            CREATE TRIGGER event_item_prices_immutable BEFORE UPDATE ON event_item_prices
                FOR EACH ROW EXECUTE FUNCTION reject_event_item_price_update();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_item_prices");

        migrationBuilder.Sql("DROP FUNCTION reject_event_item_price_update()");

        migrationBuilder.DropColumn(
            name: "item_prices_captured_at",
            table: "events");
    }
}
