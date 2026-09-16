using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PopulateRetainedCatalogue : Migration
{
    // The payload is compiled into the migration so retained deployments never read
    // mutable Web JSON at migration runtime.
    private const string FrozenPayload = CataloguePopulationPayload.Json;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            CREATE TEMP TABLE _retained_catalogue_migration_guard (
                should_run boolean NOT NULL
            ) ON COMMIT DROP;

            -- Clean bootstrap applies the full snapshot after --migrate. Keep the
            -- retained population empty on that path; all later statements then become
            -- no-ops while the migration is still recorded normally.
            INSERT INTO _retained_catalogue_migration_guard (should_run)
            SELECT NOT (
                ((SELECT COUNT(*) FROM catalogue_items) < 311 OR
                 (SELECT COUNT(*) FROM boss_activities) < 68)
                AND NOT EXISTS (SELECT 1 FROM accounts)
                AND NOT EXISTS (SELECT 1 FROM events)
            );

            CREATE TEMP TABLE _retained_catalogue_context (
                occurred_at timestamp with time zone NOT NULL,
                actor_username text NOT NULL,
                payload_version text NOT NULL
            ) ON COMMIT DROP;

            INSERT INTO _retained_catalogue_context (occurred_at, actor_username, payload_version)
            VALUES (statement_timestamp(), 'system/catalogue-migration', 'step11-2026-09-16');

            CREATE TEMP TABLE _retained_catalogue_item_payload (
                snapshot_id uuid NOT NULL,
                name text NOT NULL,
                normalized_name text NOT NULL,
                external_identifier text,
                catalogue_value_gp bigint,
                price_source text NOT NULL,
                price_observed_at timestamp with time zone,
                mapping_status text NOT NULL,
                mapping_checked_at timestamp with time zone,
                matched_api_name text,
                matched_api_icon text,
                PRIMARY KEY (snapshot_id),
                UNIQUE (normalized_name)
            ) ON COMMIT DROP;

            INSERT INTO _retained_catalogue_item_payload (
                snapshot_id, name, normalized_name, external_identifier,
                catalogue_value_gp, price_source, price_observed_at,
                mapping_status, mapping_checked_at, matched_api_name, matched_api_icon)
            SELECT p.snapshot_id, p.name, p.normalized_name, p.external_identifier,
                   p.catalogue_value_gp, p.price_source, p.price_observed_at,
                   p.mapping_status, p.mapping_checked_at, p.matched_api_name, p.matched_api_icon
            FROM jsonb_to_recordset(
                ($catalogue${FrozenPayload}$catalogue$::jsonb)->'items'
            ) AS p(
                snapshot_id uuid,
                name text,
                normalized_name text,
                external_identifier text,
                catalogue_value_gp bigint,
                price_source text,
                price_observed_at timestamp with time zone,
                mapping_status text,
                mapping_checked_at timestamp with time zone,
                matched_api_name text,
                matched_api_icon text
            )
            WHERE (SELECT should_run FROM _retained_catalogue_migration_guard);

            CREATE TEMP TABLE _retained_catalogue_boss_payload (
                snapshot_id uuid NOT NULL,
                name text NOT NULL,
                slug text NOT NULL,
                external_identifier text NOT NULL,
                mapping_status text NOT NULL,
                mapping_checked_at timestamp with time zone NOT NULL,
                PRIMARY KEY (snapshot_id),
                UNIQUE (slug)
            ) ON COMMIT DROP;

            INSERT INTO _retained_catalogue_boss_payload (
                snapshot_id, name, slug, external_identifier, mapping_status, mapping_checked_at)
            SELECT p.snapshot_id, p.name, p.slug, p.external_identifier,
                   p.mapping_status, p.mapping_checked_at
            FROM jsonb_to_recordset(
                ($catalogue${FrozenPayload}$catalogue$::jsonb)->'bosses'
            ) AS p(
                snapshot_id uuid,
                name text,
                slug text,
                external_identifier text,
                mapping_status text,
                mapping_checked_at timestamp with time zone
            )
            WHERE (SELECT should_run FROM _retained_catalogue_migration_guard);

            DO $catalogue_payload_validation$
            BEGIN
                IF NOT (SELECT should_run FROM _retained_catalogue_migration_guard) THEN
                    RETURN;
                END IF;

                IF (SELECT COUNT(*) FROM _retained_catalogue_item_payload) <> 311
                   OR (SELECT COUNT(*) FROM _retained_catalogue_item_payload WHERE price_source = 'Api') <> 195
                   OR (SELECT COUNT(*) FROM _retained_catalogue_item_payload WHERE price_source = 'Untradeable') <> 116
                THEN
                    RAISE EXCEPTION 'Retained catalogue item payload coverage is invalid.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM _retained_catalogue_item_payload
                    WHERE NOT (
                        (price_source = 'Api'
                         AND external_identifier IS NOT NULL
                         AND catalogue_value_gp IS NOT NULL
                         AND catalogue_value_gp >= 0
                         AND price_observed_at IS NOT NULL
                         AND mapping_status = 'Verified'
                         AND mapping_checked_at IS NOT NULL
                         AND matched_api_name IS NOT NULL
                         AND matched_api_icon IS NOT NULL)
                        OR
                        (price_source = 'Untradeable'
                         AND external_identifier IS NULL
                         AND catalogue_value_gp = 0
                         AND price_observed_at IS NULL
                         AND mapping_status = 'NotConfigured'
                         AND mapping_checked_at IS NULL
                         AND matched_api_name IS NULL
                         AND matched_api_icon IS NULL)
                    )
                )
                THEN
                    RAISE EXCEPTION 'Retained catalogue item payload contains an invalid price or mapping row.';
                END IF;

                IF (SELECT COUNT(*) FROM _retained_catalogue_boss_payload) <> 68
                   OR EXISTS (
                       SELECT 1
                       FROM _retained_catalogue_boss_payload
                       WHERE external_identifier IS NULL
                          OR mapping_status <> 'Verified'
                          OR mapping_checked_at IS NULL
                   )
                THEN
                    RAISE EXCEPTION 'Retained catalogue boss mapping payload coverage is invalid.';
                END IF;
            END
            $catalogue_payload_validation$;

            DO $catalogue_identity_validation$
            BEGIN
                IF NOT (SELECT should_run FROM _retained_catalogue_migration_guard) THEN
                    RETURN;
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM _retained_catalogue_item_payload p
                    LEFT JOIN catalogue_items i
                      ON i.normalized_name = p.normalized_name
                     AND i.name = p.name
                    GROUP BY p.snapshot_id
                    HAVING COUNT(i.id) <> 1
                )
                THEN
                    RAISE EXCEPTION 'Retained catalogue migration requires one exact item normalized-name and name match per payload row.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM _retained_catalogue_boss_payload p
                    LEFT JOIN boss_activities b
                      ON b.slug = p.slug
                     AND b.name = p.name
                    GROUP BY p.snapshot_id
                    HAVING COUNT(b.id) <> 1
                )
                THEN
                    RAISE EXCEPTION 'Retained catalogue migration requires one exact boss slug and name match per payload row.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM _retained_catalogue_item_payload p
                    JOIN catalogue_items i
                      ON i.normalized_name = p.normalized_name
                     AND i.name = p.name
                    GROUP BY i.id
                    HAVING COUNT(*) <> 1
                )
                THEN
                    RAISE EXCEPTION 'Retained catalogue migration requires one-to-one item identity resolution.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM _retained_catalogue_boss_payload p
                    JOIN boss_activities b
                      ON b.slug = p.slug
                     AND b.name = p.name
                    GROUP BY b.id
                    HAVING COUNT(*) <> 1
                )
                THEN
                    RAISE EXCEPTION 'Retained catalogue migration requires one-to-one boss identity resolution.';
                END IF;
            END
            $catalogue_identity_validation$;

            -- Lock every resolved source row before capturing before-state. The migration
            -- transaction then cannot race a newer operator edit into an audit row.
            SELECT i.id
            FROM catalogue_items i
            JOIN _retained_catalogue_item_payload p
              ON i.normalized_name = p.normalized_name
             AND i.name = p.name
            ORDER BY i.id
            FOR UPDATE;

            SELECT b.id
            FROM boss_activities b
            JOIN _retained_catalogue_boss_payload p
              ON b.slug = p.slug
             AND b.name = p.name
            ORDER BY b.id
            FOR UPDATE;

            CREATE TEMP TABLE _retained_catalogue_item_target ON COMMIT DROP AS
            SELECT p.*, i.id AS actual_id
            FROM _retained_catalogue_item_payload p
            JOIN catalogue_items i
              ON i.normalized_name = p.normalized_name
             AND i.name = p.name;

            ALTER TABLE _retained_catalogue_item_target ADD PRIMARY KEY (actual_id);

            CREATE TEMP TABLE _retained_catalogue_boss_target ON COMMIT DROP AS
            SELECT p.*, b.id AS actual_id
            FROM _retained_catalogue_boss_payload p
            JOIN boss_activities b
              ON b.slug = p.slug
             AND b.name = p.name;

            ALTER TABLE _retained_catalogue_boss_target ADD PRIMARY KEY (actual_id);

            DO $catalogue_locked_identity_validation$
            BEGIN
                IF (SELECT should_run FROM _retained_catalogue_migration_guard)
                   AND ((SELECT COUNT(*) FROM _retained_catalogue_item_target) <> 311
                        OR (SELECT COUNT(*) FROM _retained_catalogue_boss_target) <> 68)
                THEN
                    RAISE EXCEPTION 'Retained catalogue identity changed while rows were being locked.';
                END IF;
            END
            $catalogue_locked_identity_validation$;

            CREATE TEMP TABLE _retained_catalogue_item_before ON COMMIT DROP AS
            SELECT i.id, i.name, i.normalized_name, i.external_identifier,
                   i.catalogue_value_gp, i.price_source, i.price_observed_at,
                   i.mapping_status, i.mapping_checked_at, i.matched_api_name,
                   i.matched_api_icon, i.rejected_price_gp,
                   i.rejected_price_observed_at, i.version
            FROM catalogue_items i
            JOIN _retained_catalogue_item_target t ON t.actual_id = i.id;

            CREATE TEMP TABLE _retained_catalogue_boss_before ON COMMIT DROP AS
            SELECT b.id, b.name, b.slug, b.external_identifier,
                   b.mapping_status, b.mapping_checked_at, b.version
            FROM boss_activities b
            JOIN _retained_catalogue_boss_target t ON t.actual_id = b.id;

            -- Existing seed rows can carry a legacy WOM/API identifier while their
            -- newer mapping metadata is still NotConfigured. Fill that metadata only
            -- when the identifier agrees exactly with this payload; any other configured
            -- or conflicting row remains untouched.
            UPDATE catalogue_items i
            SET external_identifier = CASE
                    WHEN i.external_identifier IS NULL AND t.external_identifier IS NOT NULL
                    THEN t.external_identifier ELSE i.external_identifier END,
                mapping_status = CASE
                    WHEN (i.external_identifier IS NULL AND t.external_identifier IS NOT NULL
                          AND i.mapping_status = 'NotConfigured'
                          AND i.mapping_checked_at IS NULL
                          AND i.matched_api_name IS NULL
                          AND i.matched_api_icon IS NULL)
                      OR (i.external_identifier = t.external_identifier
                          AND i.mapping_status = 'NotConfigured'
                          AND i.mapping_checked_at IS NULL
                          AND i.matched_api_name IS NULL
                          AND i.matched_api_icon IS NULL)
                    THEN t.mapping_status ELSE i.mapping_status END,
                mapping_checked_at = CASE
                    WHEN (i.external_identifier IS NULL AND t.external_identifier IS NOT NULL
                          AND i.mapping_status = 'NotConfigured'
                          AND i.mapping_checked_at IS NULL
                          AND i.matched_api_name IS NULL
                          AND i.matched_api_icon IS NULL)
                      OR (i.external_identifier = t.external_identifier
                          AND i.mapping_status = 'NotConfigured'
                          AND i.mapping_checked_at IS NULL
                          AND i.matched_api_name IS NULL
                          AND i.matched_api_icon IS NULL)
                    THEN t.mapping_checked_at ELSE i.mapping_checked_at END,
                matched_api_name = CASE
                    WHEN (i.external_identifier IS NULL AND t.external_identifier IS NOT NULL
                          AND i.mapping_status = 'NotConfigured'
                          AND i.mapping_checked_at IS NULL
                          AND i.matched_api_name IS NULL
                          AND i.matched_api_icon IS NULL)
                      OR (i.external_identifier = t.external_identifier
                          AND i.mapping_status = 'NotConfigured'
                          AND i.mapping_checked_at IS NULL
                          AND i.matched_api_name IS NULL
                          AND i.matched_api_icon IS NULL)
                    THEN t.matched_api_name ELSE i.matched_api_name END,
                matched_api_icon = CASE
                    WHEN (i.external_identifier IS NULL AND t.external_identifier IS NOT NULL
                          AND i.mapping_status = 'NotConfigured'
                          AND i.mapping_checked_at IS NULL
                          AND i.matched_api_name IS NULL
                          AND i.matched_api_icon IS NULL)
                      OR (i.external_identifier = t.external_identifier
                          AND i.mapping_status = 'NotConfigured'
                          AND i.mapping_checked_at IS NULL
                          AND i.matched_api_name IS NULL
                          AND i.matched_api_icon IS NULL)
                    THEN t.matched_api_icon ELSE i.matched_api_icon END,
                catalogue_value_gp = CASE
                    WHEN i.catalogue_value_gp IS NULL AND i.price_source = 'Missing'
                    THEN t.catalogue_value_gp ELSE i.catalogue_value_gp END,
                price_source = CASE
                    WHEN i.catalogue_value_gp IS NULL AND i.price_source = 'Missing'
                    THEN t.price_source ELSE i.price_source END,
                price_observed_at = CASE
                    WHEN i.catalogue_value_gp IS NULL AND i.price_source = 'Missing'
                    THEN t.price_observed_at ELSE i.price_observed_at END,
                version = i.version + 1
            FROM _retained_catalogue_item_target t
            WHERE i.id = t.actual_id
              AND (
                  (i.external_identifier IS NULL AND t.external_identifier IS NOT NULL
                   AND i.mapping_status = 'NotConfigured'
                   AND i.mapping_checked_at IS NULL
                   AND i.matched_api_name IS NULL
                   AND i.matched_api_icon IS NULL)
                  OR (i.external_identifier = t.external_identifier
                      AND i.mapping_status = 'NotConfigured'
                      AND i.mapping_checked_at IS NULL
                      AND i.matched_api_name IS NULL
                      AND i.matched_api_icon IS NULL)
                  OR (i.catalogue_value_gp IS NULL AND i.price_source = 'Missing')
              );

            UPDATE boss_activities b
            SET external_identifier = CASE
                    WHEN b.external_identifier IS NULL THEN t.external_identifier
                    ELSE b.external_identifier END,
                mapping_status = CASE
                    WHEN (b.external_identifier IS NULL
                          AND b.mapping_status = 'NotConfigured'
                          AND b.mapping_checked_at IS NULL)
                      OR (b.external_identifier = t.external_identifier
                          AND b.mapping_status = 'NotConfigured'
                          AND b.mapping_checked_at IS NULL)
                    THEN t.mapping_status
                    ELSE b.mapping_status END,
                mapping_checked_at = CASE
                    WHEN (b.external_identifier IS NULL
                          AND b.mapping_status = 'NotConfigured'
                          AND b.mapping_checked_at IS NULL)
                      OR (b.external_identifier = t.external_identifier
                          AND b.mapping_status = 'NotConfigured'
                          AND b.mapping_checked_at IS NULL)
                    THEN t.mapping_checked_at
                    ELSE b.mapping_checked_at END,
                version = b.version + 1
            FROM _retained_catalogue_boss_target t
            WHERE b.id = t.actual_id
              AND (
                  (b.external_identifier IS NULL
                   AND b.mapping_status = 'NotConfigured'
                   AND b.mapping_checked_at IS NULL)
                  OR (b.external_identifier = t.external_identifier
                      AND b.mapping_status = 'NotConfigured'
                      AND b.mapping_checked_at IS NULL)
              );

            CREATE TEMP TABLE _retained_catalogue_item_after ON COMMIT DROP AS
            SELECT i.id, i.name, i.normalized_name, i.external_identifier,
                   i.catalogue_value_gp, i.price_source, i.price_observed_at,
                   i.mapping_status, i.mapping_checked_at, i.matched_api_name,
                   i.matched_api_icon, i.rejected_price_gp,
                   i.rejected_price_observed_at, i.version
            FROM catalogue_items i
            JOIN _retained_catalogue_item_target t ON t.actual_id = i.id;

            CREATE TEMP TABLE _retained_catalogue_boss_after ON COMMIT DROP AS
            SELECT b.id, b.name, b.slug, b.external_identifier,
                   b.mapping_status, b.mapping_checked_at, b.version
            FROM boss_activities b
            JOIN _retained_catalogue_boss_target t ON t.actual_id = b.id;

            INSERT INTO audit_entries (
                id, occurred_at, actor_account_id, actor_username, action,
                target_type, target_id, details, event_id, before_state, after_state)
            SELECT gen_random_uuid(), c.occurred_at, NULL, c.actor_username,
                   'catalogue.retained_item_filled', 'catalogue_item', after.id::text,
                   format('Step 11 approved retained catalogue population (%s) for %s.',
                          c.payload_version, after.name),
                   NULL,
                   jsonb_build_object(
                       'Id', before.id, 'Name', before.name,
                       'NormalizedName', before.normalized_name,
                       'ExternalIdentifier', before.external_identifier,
                       'CatalogueValueGp', before.catalogue_value_gp,
                       'PriceSource', before.price_source,
                       'PriceObservedAt', before.price_observed_at,
                       'MappingStatus', before.mapping_status,
                       'MappingCheckedAt', before.mapping_checked_at,
                       'MatchedApiName', before.matched_api_name,
                       'MatchedApiIcon', before.matched_api_icon,
                       'RejectedPriceGp', before.rejected_price_gp,
                       'RejectedPriceObservedAt', before.rejected_price_observed_at,
                       'Version', before.version
                   )::text,
                   jsonb_build_object(
                       'Id', after.id, 'Name', after.name,
                       'NormalizedName', after.normalized_name,
                       'ExternalIdentifier', after.external_identifier,
                       'CatalogueValueGp', after.catalogue_value_gp,
                       'PriceSource', after.price_source,
                       'PriceObservedAt', after.price_observed_at,
                       'MappingStatus', after.mapping_status,
                       'MappingCheckedAt', after.mapping_checked_at,
                       'MatchedApiName', after.matched_api_name,
                       'MatchedApiIcon', after.matched_api_icon,
                       'RejectedPriceGp', after.rejected_price_gp,
                       'RejectedPriceObservedAt', after.rejected_price_observed_at,
                       'Version', after.version
                   )::text
            FROM _retained_catalogue_item_before before
            JOIN _retained_catalogue_item_after after ON after.id = before.id
            CROSS JOIN _retained_catalogue_context c
            WHERE before.external_identifier IS DISTINCT FROM after.external_identifier
               OR before.catalogue_value_gp IS DISTINCT FROM after.catalogue_value_gp
               OR before.price_source IS DISTINCT FROM after.price_source
               OR before.price_observed_at IS DISTINCT FROM after.price_observed_at
               OR before.mapping_status IS DISTINCT FROM after.mapping_status
               OR before.mapping_checked_at IS DISTINCT FROM after.mapping_checked_at
               OR before.matched_api_name IS DISTINCT FROM after.matched_api_name
               OR before.matched_api_icon IS DISTINCT FROM after.matched_api_icon
               OR before.version IS DISTINCT FROM after.version;

            INSERT INTO audit_entries (
                id, occurred_at, actor_account_id, actor_username, action,
                target_type, target_id, details, event_id, before_state, after_state)
            SELECT gen_random_uuid(), c.occurred_at, NULL, c.actor_username,
                   'catalogue.retained_boss_filled', 'boss_activity', after.id::text,
                   format('Step 11 approved retained WOM mapping population (%s) for %s.',
                          c.payload_version, after.name),
                   NULL,
                   jsonb_build_object(
                       'Id', before.id, 'Name', before.name, 'Slug', before.slug,
                       'ExternalIdentifier', before.external_identifier,
                       'MappingStatus', before.mapping_status,
                       'MappingCheckedAt', before.mapping_checked_at,
                       'Version', before.version
                   )::text,
                   jsonb_build_object(
                       'Id', after.id, 'Name', after.name, 'Slug', after.slug,
                       'ExternalIdentifier', after.external_identifier,
                       'MappingStatus', after.mapping_status,
                       'MappingCheckedAt', after.mapping_checked_at,
                       'Version', after.version
                   )::text
            FROM _retained_catalogue_boss_before before
            JOIN _retained_catalogue_boss_after after ON after.id = before.id
            CROSS JOIN _retained_catalogue_context c
            WHERE before.external_identifier IS DISTINCT FROM after.external_identifier
               OR before.mapping_status IS DISTINCT FROM after.mapping_status
               OR before.mapping_checked_at IS DISTINCT FROM after.mapping_checked_at
               OR before.version IS DISTINCT FROM after.version;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The additive population cannot be safely reversed after operators can edit these fields.
    }
}
