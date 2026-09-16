using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class GuardSuspiciousPriceCandidates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_event_item_price_provenance",
            table: "event_item_prices");

        migrationBuilder.AddColumn<long>(
            name: "rejected_price_gp",
            table: "catalogue_items",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "rejected_price_observed_at",
            table: "catalogue_items",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_event_item_price_provenance",
            table: "event_item_prices",
            sql: "(source = 'WikiHourly' AND api_item_id IS NOT NULL AND price_observed_at IS NOT NULL AND price_observed_at = selected_hour AND fallback_catalogue_source IS NULL AND fallback_reason IS NULL)\nOR (source IN ('CatalogueFallback', 'CatalogueIntroduction') AND fallback_catalogue_source IS NOT NULL\n    AND fallback_catalogue_source IN ('Api', 'Manual', 'Untradeable') AND (fallback_catalogue_source <> 'Untradeable' OR value_gp = 0)\n    AND ((source = 'CatalogueIntroduction' AND fallback_reason IS NULL)\n        OR (source = 'CatalogueFallback' AND fallback_reason IS NOT NULL AND fallback_reason IN ('NoMapping', 'NoHourlyPrice', 'ProviderUnavailable', 'PreparedHourChanged', 'PriceMoveRejected'))))");

        migrationBuilder.AddCheckConstraint(
            name: "ck_catalogue_price_rejection",
            table: "catalogue_items",
            sql: "(rejected_price_gp IS NULL AND rejected_price_observed_at IS NULL) OR (rejected_price_gp IS NOT NULL AND rejected_price_gp >= 0 AND rejected_price_observed_at IS NOT NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_event_item_price_provenance",
            table: "event_item_prices");

        migrationBuilder.DropCheckConstraint(
            name: "ck_catalogue_price_rejection",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "rejected_price_gp",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "rejected_price_observed_at",
            table: "catalogue_items");

        migrationBuilder.AddCheckConstraint(
            name: "ck_event_item_price_provenance",
            table: "event_item_prices",
            sql: "(source = 'WikiHourly' AND api_item_id IS NOT NULL AND price_observed_at IS NOT NULL AND price_observed_at = selected_hour AND fallback_catalogue_source IS NULL AND fallback_reason IS NULL)\nOR (source IN ('CatalogueFallback', 'CatalogueIntroduction') AND fallback_catalogue_source IS NOT NULL\n    AND fallback_catalogue_source IN ('Api', 'Manual', 'Untradeable') AND (fallback_catalogue_source <> 'Untradeable' OR value_gp = 0)\n    AND ((source = 'CatalogueIntroduction' AND fallback_reason IS NULL)\n        OR (source = 'CatalogueFallback' AND fallback_reason IS NOT NULL AND fallback_reason IN ('NoMapping', 'NoHourlyPrice', 'ProviderUnavailable', 'PreparedHourChanged'))))");
    }
}
