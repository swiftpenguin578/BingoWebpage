using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCatalogueApiMappingAndPrices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "catalogue_value_gp",
            table: "catalogue_items",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "mapping_checked_at",
            table: "catalogue_items",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "mapping_status",
            table: "catalogue_items",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "NotConfigured");

        migrationBuilder.AddColumn<string>(
            name: "matched_api_icon",
            table: "catalogue_items",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "matched_api_name",
            table: "catalogue_items",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "price_observed_at",
            table: "catalogue_items",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "price_source",
            table: "catalogue_items",
            type: "character varying(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "Missing");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "mapping_checked_at",
            table: "boss_activities",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "mapping_status",
            table: "boss_activities",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "NotConfigured");

        migrationBuilder.AddCheckConstraint(
            name: "ck_catalogue_item_price",
            table: "catalogue_items",
            sql: "(catalogue_value_gp IS NULL AND price_source = 'Missing') OR (catalogue_value_gp IS NOT NULL AND catalogue_value_gp >= 0 AND price_source IN ('Api', 'Manual', 'Untradeable') AND (price_source <> 'Untradeable' OR catalogue_value_gp = 0))");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_catalogue_item_price",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "catalogue_value_gp",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "mapping_checked_at",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "mapping_status",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "matched_api_icon",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "matched_api_name",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "price_observed_at",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "price_source",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "mapping_checked_at",
            table: "boss_activities");

        migrationBuilder.DropColumn(
            name: "mapping_status",
            table: "boss_activities");
    }
}
