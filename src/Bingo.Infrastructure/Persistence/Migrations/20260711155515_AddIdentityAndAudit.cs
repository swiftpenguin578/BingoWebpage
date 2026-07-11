using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddIdentityAndAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    active_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correction_only_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

        migrationBuilder.CreateTable(
            name: "audit_entries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                actor_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_entries", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_accounts_normalized_username",
            table: "accounts",
            column: "normalized_username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_audit_entries_action_target_type",
            table: "audit_entries",
            columns: ["action", "target_type"]);

        migrationBuilder.CreateIndex(
            name: "IX_audit_entries_occurred_at",
            table: "audit_entries",
            column: "occurred_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "accounts");

        migrationBuilder.DropTable(
            name: "audit_entries");
    }
}
