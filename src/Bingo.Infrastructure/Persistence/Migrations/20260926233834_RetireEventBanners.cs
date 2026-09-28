using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RetireEventBanners : Migration
{
    private const string RetirementLedger = "event_banner_retirement_keys";
    private static readonly string[] EventCleanupIdentityColumns = ["event_id", "storage_key"];
    private static readonly string[] EventBannerReplacementIndexColumns = ["event_id", "replaced_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This first, non-transactional step is deliberately idempotent. If
        // the guarded schema removal below fails, the ledger remains on the
        // database so an operator can retry cleanup without rediscovering
        // keys from a source table that may have changed in the meantime.
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS event_banner_retirement_keys
            (
                event_id uuid NOT NULL,
                storage_key character varying(500) NOT NULL,
                cleanup_status character varying(32) NOT NULL DEFAULT 'pending',
                attempt_count integer NOT NULL DEFAULT 0,
                last_attempted_at timestamp with time zone NULL,
                last_failure character varying(1000) NULL,
                completed_at timestamp with time zone NULL,
                recorded_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT pk_event_banner_retirement_keys PRIMARY KEY (event_id, storage_key),
                CONSTRAINT ck_event_banner_retirement_keys_status CHECK
                (
                    cleanup_status IN ('pending', 'deleted', 'missing', 'shared-retained')
                    AND
                    ((cleanup_status = 'pending' AND completed_at IS NULL)
                     OR (cleanup_status <> 'pending' AND completed_at IS NOT NULL))
                )
            );
            """, suppressTransaction: true);

        // UNION preserves the exact event/key pair from both historical
        // sources, including replaced assets, already-queued outbox records,
        // and empty or whitespace keys. The historical schema only required
        // storage_key to be non-null, so every value must remain recoverable
        // until an operator records a terminal cleanup outcome.
        migrationBuilder.Sql("""
            INSERT INTO event_banner_retirement_keys
                (event_id, storage_key)
            SELECT event_id, storage_key
            FROM
            (
                SELECT event_id, storage_key FROM event_banner_assets
                UNION
                SELECT event_id, storage_key FROM event_banner_cleanups
            ) AS banner_keys
            ON CONFLICT (event_id, storage_key) DO NOTHING;
            """, suppressTransaction: true);

        // No source table, event reference, or ledger row is removed until
        // every exact key has a recorded successful outcome. Operators may
        // mark a key deleted, missing, or shared-retained only after the
        // corresponding object-storage and non-banner reference checks.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM event_banner_retirement_keys
                    WHERE cleanup_status = 'pending'
                       OR completed_at IS NULL
                ) THEN
                    RAISE EXCEPTION
                        'Event-banner retirement is not ready: every exact key must be deleted, missing, or shared-retained before schema removal.'
                        USING ERRCODE = '55000';
                END IF;
            END $$;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_events_event_banner_assets_banner_asset_id",
            table: "events");

        migrationBuilder.DropIndex(
            name: "IX_events_banner_asset_id",
            table: "events");

        migrationBuilder.DropColumn(
            name: "banner_asset_id",
            table: "events");

        migrationBuilder.DropTable(
            name: "event_banner_cleanups");

        migrationBuilder.DropTable(
            name: "event_banner_assets");

        // The ledger is temporary compatibility state. It is only removed
        // after the guard above has proved that cleanup execution recorded an
        // outcome for every recovered key.
        migrationBuilder.DropTable(
            name: RetirementLedger);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Down restores the pre-retirement schema shape for a disposable
        // rehearsal. It does not claim to recover deleted object bytes or
        // the temporary retirement ledger; production rollback requires the
        // separately managed database/object-storage recovery plan.
        migrationBuilder.CreateTable(
            name: "event_banner_assets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
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
            constraints: table => table.PrimaryKey("PK_event_banner_assets", x => x.id));

        migrationBuilder.CreateTable(
            name: "event_banner_cleanups",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_failure = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_banner_cleanups", x => x.id);
                table.ForeignKey(
                    name: "FK_event_banner_cleanups_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "banner_asset_id",
            table: "events",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_events_banner_asset_id",
            table: "events",
            column: "banner_asset_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_banner_assets_event_id_replaced_at",
            table: "event_banner_assets",
            columns: EventBannerReplacementIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_event_banner_cleanups_event_id_storage_key",
            table: "event_banner_cleanups",
            columns: EventCleanupIdentityColumns,
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_events_event_banner_assets_banner_asset_id",
            table: "events",
            column: "banner_asset_id",
            principalTable: "event_banner_assets",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }
}
