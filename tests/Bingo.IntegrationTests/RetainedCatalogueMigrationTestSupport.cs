using System.Text.Json;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bingo.IntegrationTests;

internal static class RetainedCatalogueMigrationTestSupport
{
    public const string CatalogueApiMappingMigration = "20260915142649_AddCatalogueApiMappingAndPrices";

    private static readonly DateTimeOffset FixtureUpdatedAt = new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);

    public static async Task PrepareAsync(ApplicationDbContext db)
    {
        // Older rehearsal fixtures intentionally contain only the rows needed by the
        // migration under test. The retained catalogue migration is a real upgrade
        // boundary and requires every approved identity to already exist. Bring the
        // fixture to the API-mapping boundary, then seed only the approved item/boss
        // rows needed for that boundary. Source drops and future Web JSON are not
        // imported here.
        if (!(await db.Database.GetAppliedMigrationsAsync()).Contains(CatalogueApiMappingMigration))
            await db.GetService<IMigrator>().MigrateAsync(CatalogueApiMappingMigration);

        // The historical WOM seed used "Nightmare" while the reviewed catalogue
        // baseline uses "The Nightmare". Mirror the existing bootstrap preparation
        // for this one known row, retaining its ID and every association. Any other
        // name mismatch remains a validation failure below.
        var repairedNightmare = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE boss_activities
            SET name = {"The Nightmare"}
            WHERE slug = {"nightmare"}
              AND name = {"Nightmare"}
              AND external_identifier = {"nightmare"};
            """);
        if (repairedNightmare > 1)
            throw new InvalidOperationException("Historical fixture contains multiple legacy nightmare rows.");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json")));
        var items = document.RootElement.GetProperty("items").GetRawText();
        var bosses = document.RootElement.GetProperty("bosses").GetRawText();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO catalogue_items (
                id, name, normalized_name, external_identifier, active, notes, image_url,
                catalogue_value_gp, mapping_checked_at, mapping_status, matched_api_icon,
                matched_api_name, price_observed_at, price_source, version)
            SELECT gen_random_uuid(), p.name, p."normalizedName", p."externalIdentifier", TRUE,
                   NULL, NULL, p."catalogueValueGp", p."mappingCheckedAt", p."mappingStatus",
                   p."matchedApiIcon", p."matchedApiName", p."priceObservedAt", p."priceSource", 1
            FROM jsonb_to_recordset({items}::jsonb) AS p(
                name text, "normalizedName" text, "externalIdentifier" text,
                "catalogueValueGp" bigint, "mappingCheckedAt" timestamptz,
                "mappingStatus" text, "matchedApiIcon" text, "matchedApiName" text,
                "priceObservedAt" timestamptz, "priceSource" text)
            WHERE NOT EXISTS (
                SELECT 1 FROM catalogue_items existing
                WHERE existing.normalized_name = p."normalizedName");
            """);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO boss_activities (
                id, name, slug, category, efficient_completions_per_hour, external_identifier,
                data_source, data_updated_at, active, notes, image_url, mapping_checked_at,
                mapping_status, version)
            SELECT gen_random_uuid(), p.name, p.slug, p.category, p."efficientCompletionsPerHour",
                   p."externalIdentifier", NULL, {FixtureUpdatedAt}, TRUE, NULL, NULL,
                   p."mappingCheckedAt", p."mappingStatus", 1
            FROM jsonb_to_recordset({bosses}::jsonb) AS p(
                name text, slug text, category text, "efficientCompletionsPerHour" numeric,
                "externalIdentifier" text, "mappingCheckedAt" timestamptz,
                "mappingStatus" text)
            WHERE NOT EXISTS (
                SELECT 1 FROM boss_activities existing
                WHERE existing.slug = p.slug);
            """);

        // Historical seed rows have the API columns' defaults after the boundary.
        // Set only those defaults to the approved values so the migration under test
        // can exercise its later upgrade without manufacturing setup-time audits.
        // Configured or conflicting fixture metadata stays available to the migration
        // and is preserved by its own eligibility rules.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE catalogue_items AS i
            SET external_identifier = p."externalIdentifier",
                catalogue_value_gp = p."catalogueValueGp",
                mapping_checked_at = p."mappingCheckedAt",
                mapping_status = p."mappingStatus",
                matched_api_icon = p."matchedApiIcon",
                matched_api_name = p."matchedApiName",
                price_observed_at = p."priceObservedAt",
                price_source = p."priceSource"
            FROM jsonb_to_recordset({items}::jsonb) AS p(
                name text, "normalizedName" text, "externalIdentifier" text,
                "catalogueValueGp" bigint, "mappingCheckedAt" timestamptz,
                "mappingStatus" text, "matchedApiIcon" text, "matchedApiName" text,
                "priceObservedAt" timestamptz, "priceSource" text)
            WHERE i.normalized_name = p."normalizedName"
              AND i.name = p.name
              AND i.mapping_status = 'NotConfigured'
              AND i.mapping_checked_at IS NULL
              AND i.matched_api_name IS NULL
              AND i.matched_api_icon IS NULL
              AND i.catalogue_value_gp IS NULL
              AND i.price_source = 'Missing'
              AND ((i.external_identifier IS NULL AND p."externalIdentifier" IS NOT NULL)
                   OR i.external_identifier = p."externalIdentifier"
                   OR (i.external_identifier IS NULL AND p."externalIdentifier" IS NULL));
            """);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE boss_activities AS b
            SET external_identifier = p."externalIdentifier",
                mapping_checked_at = p."mappingCheckedAt",
                mapping_status = p."mappingStatus"
            FROM jsonb_to_recordset({bosses}::jsonb) AS p(
                name text, slug text, "externalIdentifier" text,
                "mappingCheckedAt" timestamptz, "mappingStatus" text)
            WHERE b.slug = p.slug
              AND b.name = p.name
              AND b.mapping_status = 'NotConfigured'
              AND b.mapping_checked_at IS NULL
              AND (b.external_identifier IS NULL OR b.external_identifier = p."externalIdentifier");
            """);

        var missingItems = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value"
            FROM jsonb_to_recordset({items}::jsonb) AS p(name text, "normalizedName" text)
            LEFT JOIN catalogue_items i
              ON i.normalized_name = p."normalizedName" AND i.name = p.name
            WHERE i.id IS NULL
            """).SingleAsync();
        var missingBosses = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value"
            FROM jsonb_to_recordset({bosses}::jsonb) AS p(name text, slug text)
            LEFT JOIN boss_activities b
              ON b.slug = p.slug AND b.name = p.name
            WHERE b.id IS NULL
            """).SingleAsync();
        if (missingItems != 0 || missingBosses != 0)
            throw new InvalidOperationException($"Historical retained catalogue fixture is incomplete ({missingItems} items, {missingBosses} bosses).");
    }
}
