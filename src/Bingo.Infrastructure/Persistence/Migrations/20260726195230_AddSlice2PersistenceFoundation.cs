#pragma warning disable IDE0161, CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlice2PersistenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "account_osrs_characters",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "Position",
                table: "account_osrs_characters",
                newName: "sort_order");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "account_osrs_characters",
                newName: "created_at");

            migrationBuilder.AddColumn<Guid>(
                name: "account_id",
                table: "event_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "linked_at",
                table: "account_osrs_characters",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "linked_by_account_id",
                table: "account_osrs_characters",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "personal_label",
                table: "account_osrs_characters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "saved_ehb",
                table: "account_osrs_characters",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "unlinked_at",
                table: "account_osrs_characters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "account_osrs_characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Slice 1 already established one retained row per account/character pair.
            // Preserve that identity and its preferred state while translating Active
            // into explicit link/unlink history. The existing account is the only
            // truthful linking actor available for retained Slice 1 rows.
            migrationBuilder.Sql("""
                UPDATE account_osrs_characters
                SET linked_at = created_at,
                    linked_by_account_id = "AccountId",
                    unlinked_at = CASE WHEN active THEN NULL ELSE updated_at END,
                    version = 1;
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_event_participants_event_id_id",
                table: "event_participants",
                columns: new[] { "event_id", "id" });

            migrationBuilder.CreateTable(
                name: "event_participant_characters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    osrs_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_order = table.Column<int>(type: "integer", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registered_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signup_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ehb_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ehb_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ehb_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_participant_characters", x => x.id);
                    table.CheckConstraint("ck_event_participant_characters_ehb", "(event_role = 'Playing' AND ehb_snapshot IS NOT NULL AND ehb_snapshot >= 0 AND ehb_source IS NOT NULL) OR (event_role = 'Informational' AND ehb_snapshot IS NULL AND ehb_source IS NULL AND ehb_fetched_at IS NULL)");
                    table.CheckConstraint("ck_event_participant_characters_fetch", "(ehb_source = 'WiseOldMan' AND ehb_fetched_at IS NOT NULL) OR (ehb_source IS DISTINCT FROM 'WiseOldMan' AND ehb_fetched_at IS NULL)");
                    table.CheckConstraint("ck_event_participant_characters_registration_order", "registration_order >= 0");
                    table.CheckConstraint("ck_event_participant_characters_release", "(released_at IS NULL AND released_by_account_id IS NULL) OR (released_at IS NOT NULL AND released_by_account_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_event_participant_characters_accounts_registered_by_account~",
                        column: x => x.registered_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_participant_characters_accounts_released_by_account_id",
                        column: x => x.released_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_participant_characters_event_participants_event_id_ev~",
                        columns: x => new { x.event_id, x.event_participant_id },
                        principalTable: "event_participants",
                        principalColumns: new[] { "event_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_participant_characters_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_participant_characters_osrs_characters_osrs_character~",
                        column: x => x.osrs_character_id,
                        principalTable: "osrs_characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_participant_characters_signup_questions_signup_questi~",
                        column: x => x.signup_question_id,
                        principalTable: "signup_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Reuse any Slice 1 character identity with the same normalized OSRS name.
            // New UUIDs are derived from the normalized name so the rehearsal is stable
            // and a repeated retained-data fixture produces the same mapping.
            migrationBuilder.Sql("""
                INSERT INTO osrs_characters ("Id", "DisplayName", "NormalizedName", "CreatedAt", "UpdatedAt")
                SELECT md5('slice2-character:' || source.normalized_name)::uuid,
                       source.display_name,
                       source.normalized_name,
                       source.created_at,
                       source.created_at
                FROM (
                    SELECT DISTINCT ON (normalized_name)
                           normalized_name,
                           display_name,
                           created_at
                    FROM (
                        SELECT upper(btrim(primary_account_name)) AS normalized_name,
                               btrim(primary_account_name) AS display_name,
                               signed_up_at AS created_at,
                               0 AS role_order
                        FROM event_participants
                        WHERE btrim(primary_account_name) <> ''
                        UNION ALL
                        SELECT upper(btrim(second_account_name)),
                               btrim(second_account_name),
                               signed_up_at,
                               1
                        FROM event_participants
                        WHERE second_account_name IS NOT NULL
                          AND btrim(second_account_name) <> ''
                    ) retained_names
                    ORDER BY normalized_name, created_at, role_order, display_name
                ) source
                ON CONFLICT ("NormalizedName") DO NOTHING;
                """);

            // The legacy primary account is deterministically retained as registration
            // order zero and Playing. Existing EHB is copied as an event snapshot.
            // No participant ownership, My Accounts link, signup question, or actor is
            // guessed from names.
            migrationBuilder.Sql("""
                INSERT INTO event_participant_characters
                    (id, event_id, event_participant_id, osrs_character_id,
                     registration_order, registered_at, registered_by_account_id,
                     signup_question_id, event_role, ehb_snapshot, ehb_source,
                     ehb_fetched_at, released_at, released_by_account_id, version)
                SELECT md5('slice2-assignment:playing:' || p.id::text)::uuid,
                       p.event_id,
                       p.id,
                       c."Id",
                       0,
                       p.signed_up_at,
                       NULL,
                       NULL,
                       'Playing',
                       p.ehb_snapshot,
                       CASE WHEN p.source = 'CsvImport' THEN 'Import' ELSE 'Manual' END,
                       NULL,
                       NULL,
                       NULL,
                       1
                FROM event_participants p
                JOIN osrs_characters c
                  ON c."NormalizedName" = upper(btrim(p.primary_account_name));
                """);

            // The optional legacy second account is informational only. A duplicate of
            // the primary name is ignored rather than manufacturing two assignments for
            // the same participant and character.
            migrationBuilder.Sql("""
                INSERT INTO event_participant_characters
                    (id, event_id, event_participant_id, osrs_character_id,
                     registration_order, registered_at, registered_by_account_id,
                     signup_question_id, event_role, ehb_snapshot, ehb_source,
                     ehb_fetched_at, released_at, released_by_account_id, version)
                SELECT md5('slice2-assignment:informational:' || p.id::text)::uuid,
                       p.event_id,
                       p.id,
                       c."Id",
                       1,
                       p.signed_up_at,
                       NULL,
                       NULL,
                       'Informational',
                       NULL,
                       NULL,
                       NULL,
                       NULL,
                       NULL,
                       1
                FROM event_participants p
                JOIN osrs_characters c
                  ON c."NormalizedName" = upper(btrim(p.second_account_name))
                WHERE p.second_account_name IS NOT NULL
                  AND btrim(p.second_account_name) <> ''
                  AND upper(btrim(p.second_account_name)) <> upper(btrim(p.primary_account_name));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_account_id",
                table: "event_participants",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_event_id_account_id",
                table: "event_participants",
                columns: new[] { "event_id", "account_id" },
                unique: true,
                filter: "account_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_account_osrs_characters_linked_by_account_id",
                table: "account_osrs_characters",
                column: "linked_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_osrs_characters_OsrsCharacterId",
                table: "account_osrs_characters",
                column: "OsrsCharacterId");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_osrs_characters_normalized_name_ci
                ON osrs_characters (lower(btrim("NormalizedName")));
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_account_osrs_characters_active_history",
                table: "account_osrs_characters",
                sql: "(active AND unlinked_at IS NULL) OR (NOT active AND unlinked_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_account_osrs_characters_saved_ehb",
                table: "account_osrs_characters",
                sql: "saved_ehb IS NULL OR saved_ehb >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_account_osrs_characters_sort_order",
                table: "account_osrs_characters",
                sql: "sort_order >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_event_participant_characters_event_id_event_participant_id",
                table: "event_participant_characters",
                columns: new[] { "event_id", "event_participant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_event_participant_characters_event_id_osrs_character_id",
                table: "event_participant_characters",
                columns: new[] { "event_id", "osrs_character_id" },
                unique: true,
                filter: "released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_event_participant_characters_event_participant_id_registrat~",
                table: "event_participant_characters",
                columns: new[] { "event_participant_id", "registration_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_participant_characters_osrs_character_id",
                table: "event_participant_characters",
                column: "osrs_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_participant_characters_registered_by_account_id",
                table: "event_participant_characters",
                column: "registered_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_participant_characters_released_by_account_id",
                table: "event_participant_characters",
                column: "released_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_participant_characters_signup_question_id",
                table: "event_participant_characters",
                column: "signup_question_id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_osrs_characters_accounts_AccountId",
                table: "account_osrs_characters",
                column: "AccountId",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_account_osrs_characters_accounts_linked_by_account_id",
                table: "account_osrs_characters",
                column: "linked_by_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_account_osrs_characters_osrs_characters_OsrsCharacterId",
                table: "account_osrs_characters",
                column: "OsrsCharacterId",
                principalTable: "osrs_characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_event_participants_accounts_account_id",
                table: "event_participants",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_osrs_characters_accounts_AccountId",
                table: "account_osrs_characters");

            migrationBuilder.DropForeignKey(
                name: "FK_account_osrs_characters_accounts_linked_by_account_id",
                table: "account_osrs_characters");

            migrationBuilder.DropForeignKey(
                name: "FK_account_osrs_characters_osrs_characters_OsrsCharacterId",
                table: "account_osrs_characters");

            migrationBuilder.DropForeignKey(
                name: "FK_event_participants_accounts_account_id",
                table: "event_participants");

            migrationBuilder.DropTable(
                name: "event_participant_characters");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_event_participants_event_id_id",
                table: "event_participants");

            migrationBuilder.DropIndex(
                name: "IX_event_participants_account_id",
                table: "event_participants");

            migrationBuilder.DropIndex(
                name: "IX_event_participants_event_id_account_id",
                table: "event_participants");

            migrationBuilder.DropIndex(
                name: "IX_account_osrs_characters_linked_by_account_id",
                table: "account_osrs_characters");

            migrationBuilder.DropIndex(
                name: "IX_account_osrs_characters_OsrsCharacterId",
                table: "account_osrs_characters");

            migrationBuilder.Sql("DROP INDEX ux_osrs_characters_normalized_name_ci;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_account_osrs_characters_active_history",
                table: "account_osrs_characters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_account_osrs_characters_saved_ehb",
                table: "account_osrs_characters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_account_osrs_characters_sort_order",
                table: "account_osrs_characters");

            migrationBuilder.DropColumn(
                name: "account_id",
                table: "event_participants");

            migrationBuilder.DropColumn(
                name: "linked_at",
                table: "account_osrs_characters");

            migrationBuilder.DropColumn(
                name: "linked_by_account_id",
                table: "account_osrs_characters");

            migrationBuilder.DropColumn(
                name: "personal_label",
                table: "account_osrs_characters");

            migrationBuilder.DropColumn(
                name: "saved_ehb",
                table: "account_osrs_characters");

            migrationBuilder.DropColumn(
                name: "unlinked_at",
                table: "account_osrs_characters");

            migrationBuilder.DropColumn(
                name: "version",
                table: "account_osrs_characters");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "account_osrs_characters",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "sort_order",
                table: "account_osrs_characters",
                newName: "Position");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "account_osrs_characters",
                newName: "CreatedAt");
        }
    }
}
