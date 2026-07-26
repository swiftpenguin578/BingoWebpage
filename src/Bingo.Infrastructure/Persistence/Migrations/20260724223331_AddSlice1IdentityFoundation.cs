#pragma warning disable IDE0161, CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlice1IdentityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "after_state", table: "audit_entries", type: "character varying(4000)", maxLength: 4000, nullable: true);
            migrationBuilder.AddColumn<string>(name: "before_state", table: "audit_entries", type: "character varying(4000)", maxLength: 4000, nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "event_id", table: "audit_entries", type: "uuid", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_audit_entries_event_id_occurred_at", table: "audit_entries", columns: new[] { "event_id", "occurred_at" });

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "accounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "account_type",
                table: "accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "active",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "authorization_version",
                table: "accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(name: "login_name", table: "accounts", type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "normalized_login_name", table: "accounts", type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<DateTimeOffset?>(name: "password_changed_at", table: "accounts", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset?>(name: "onboarding_completed_at", table: "accounts", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid?>(name: "profile_osrs_character_id", table: "accounts", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid?>(name: "disabled_by_account_id", table: "accounts", type: "uuid", nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "disabled_reason",
                table: "accounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discord_display_name",
                table: "accounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discord_user_id",
                table: "accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emergency_login_username",
                table: "accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "global_role",
                table: "accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_public_username",
                table: "accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "password_version",
                table: "accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "public_username",
                table: "accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "account_discord_identity_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreviousDiscordUserId = table.Column<string>(type: "text", nullable: true),
                    NextDiscordUserId = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_discord_identity_transitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "account_event_accesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    active_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correction_only_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_event_accesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "account_osrs_characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OsrsCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    preferred = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_osrs_characters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "osrs_characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_osrs_characters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "password_credential_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_credential_tokens", x => x.Id);
                });

            // Preserve legacy IDs and password hashes. The separate preflight command must have
            // rejected ambiguous rows before this migration is allowed on retained data.
            migrationBuilder.Sql("""
                UPDATE accounts
                SET login_name = username,
                    normalized_login_name = normalized_username,
                    password_changed_at = created_at,
                    authorization_version = 1,
                    password_version = 1,
                    version = 1,
                    account_type = CASE WHEN role = 'Admin' THEN 'WebsiteAccount' ELSE 'EmergencyCaptain' END,
                    global_role = CASE WHEN role = 'Admin' THEN 'Admin' ELSE NULL END,
                    public_username = CASE WHEN role = 'Admin' THEN username ELSE NULL END,
                    normalized_public_username = CASE WHEN role = 'Admin' THEN normalized_username ELSE NULL END,
                    emergency_login_username = CASE WHEN role = 'Captain' THEN username ELSE NULL END,
                    onboarding_completed_at = CASE WHEN role = 'Admin' THEN created_at ELSE NULL END,
                    active = CASE WHEN role = 'Admin' AND disabled_at IS NULL THEN TRUE ELSE FALSE END;
                """);
            migrationBuilder.Sql("""
                INSERT INTO account_event_accesses ("Id", "AccountId", "EventId", "TeamId", "ParticipantId", active_from, correction_only_from, expires_at, "Enabled")
                SELECT gen_random_uuid(), a.id, a.event_id, a.team_id, a.captain_participant_id, a.active_from, a.correction_only_from, a.expires_at, FALSE
                FROM accounts a
                JOIN team_memberships m ON m.team_id = a.team_id AND m.event_participant_id = a.captain_participant_id
                    AND m.left_at IS NULL AND m.role IN ('Captain', 'CoCaptain')
                WHERE a.role = 'Captain' AND a.event_id IS NOT NULL AND a.team_id IS NOT NULL;
                """);

            migrationBuilder.DropIndex(name: "IX_accounts_captain_participant_id", table: "accounts");
            migrationBuilder.DropIndex(name: "IX_accounts_normalized_username", table: "accounts");
            migrationBuilder.DropColumn(name: "active_from", table: "accounts");
            migrationBuilder.DropColumn(name: "captain_participant_id", table: "accounts");
            migrationBuilder.DropColumn(name: "correction_only_from", table: "accounts");
            migrationBuilder.DropColumn(name: "event_id", table: "accounts");
            migrationBuilder.DropColumn(name: "expires_at", table: "accounts");
            migrationBuilder.DropColumn(name: "team_id", table: "accounts");
            migrationBuilder.DropColumn(name: "role", table: "accounts");
            migrationBuilder.DropColumn(name: "username", table: "accounts");
            migrationBuilder.DropColumn(name: "normalized_username", table: "accounts");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_discord_user_id",
                table: "accounts",
                column: "discord_user_id",
                unique: true,
                filter: "discord_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_global_role",
                table: "accounts",
                column: "global_role",
                unique: true,
                filter: "global_role = 'SuperAdmin'");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_normalized_login_name",
                table: "accounts",
                column: "normalized_login_name",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_accounts_type_role",
                table: "accounts",
                sql: "(account_type = 'WebsiteAccount' AND global_role IS NOT NULL) OR (account_type = 'EmergencyCaptain' AND global_role IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_account_discord_identity_transitions_AccountId_OccurredAt",
                table: "account_discord_identity_transitions",
                columns: new[] { "AccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_account_event_accesses_AccountId",
                table: "account_event_accesses",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_account_event_accesses_EventId_TeamId",
                table: "account_event_accesses",
                columns: new[] { "EventId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_account_osrs_characters_AccountId",
                table: "account_osrs_characters",
                column: "AccountId",
                unique: true,
                filter: "active AND preferred");

            migrationBuilder.CreateIndex(
                name: "IX_account_osrs_characters_AccountId_OsrsCharacterId",
                table: "account_osrs_characters",
                columns: new[] { "AccountId", "OsrsCharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_osrs_characters_NormalizedName",
                table: "osrs_characters",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_credential_tokens_AccountId_Purpose",
                table: "password_credential_tokens",
                columns: new[] { "AccountId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_password_credential_tokens_TokenHash",
                table: "password_credential_tokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_audit_entries_event_id_occurred_at", table: "audit_entries");
            migrationBuilder.DropColumn(name: "after_state", table: "audit_entries");
            migrationBuilder.DropColumn(name: "before_state", table: "audit_entries");
            migrationBuilder.DropColumn(name: "event_id", table: "audit_entries");

            migrationBuilder.DropTable(
                name: "account_discord_identity_transitions");

            migrationBuilder.DropTable(
                name: "account_event_accesses");

            migrationBuilder.DropTable(
                name: "account_osrs_characters");

            migrationBuilder.DropTable(
                name: "osrs_characters");

            migrationBuilder.DropTable(
                name: "password_credential_tokens");

            migrationBuilder.DropIndex(
                name: "IX_accounts_discord_user_id",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_accounts_global_role",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_accounts_normalized_login_name",
                table: "accounts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_accounts_type_role",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "account_type",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "active",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "authorization_version",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "disabled_reason",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "discord_display_name",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "discord_user_id",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "emergency_login_username",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "global_role",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "normalized_public_username",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "password_version",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "public_username",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "version",
                table: "accounts");

            migrationBuilder.RenameColumn(
                name: "profile_osrs_character_id",
                table: "accounts",
                newName: "team_id");

            migrationBuilder.RenameColumn(
                name: "password_changed_at",
                table: "accounts",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "onboarding_completed_at",
                table: "accounts",
                newName: "correction_only_from");

            migrationBuilder.RenameColumn(
                name: "normalized_login_name",
                table: "accounts",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "login_name",
                table: "accounts",
                newName: "normalized_username");

            migrationBuilder.RenameColumn(
                name: "disabled_by_account_id",
                table: "accounts",
                newName: "event_id");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "accounts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "active_from",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "captain_participant_id",
                table: "accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_captain_participant_id",
                table: "accounts",
                column: "captain_participant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_normalized_username",
                table: "accounts",
                column: "normalized_username",
                unique: true);
        }
    }
}
