using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDropAnnouncements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "announcement_generation",
            table: "submissions",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "announcement_ordinal",
            table: "submissions",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "completed_tile_at_approval",
            table: "submissions",
            type: "boolean",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "announcement_generation",
            table: "events",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "announcement_sequence",
            table: "events",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "announcements_tracking_started_at",
            table: "events",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.CreateTable(
            name: "drop_announcement_account_states",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                expansion_cooldown_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_drop_announcement_account_states", x => x.id);
                table.ForeignKey(
                    name: "FK_drop_announcement_account_states_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_drop_announcement_account_states_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "drop_announcement_acknowledgements",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                banner_acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                drops_acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_drop_announcement_acknowledgements", x => x.id);
                table.ForeignKey(
                    name: "FK_drop_announcement_acknowledgements_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_drop_announcement_acknowledgements_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_drop_announcement_acknowledgements_submissions_submission_id",
                    column: x => x.submission_id,
                    principalTable: "submissions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_submissions_event_id_announcement_generation_announcement_o~",
            table: "submissions",
            columns: new[] { "event_id", "announcement_generation", "announcement_ordinal", "reviewed_at", "id" });

        migrationBuilder.CreateIndex(
            name: "IX_drop_announcement_account_states_account_id_event_id",
            table: "drop_announcement_account_states",
            columns: new[] { "account_id", "event_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_drop_announcement_account_states_event_id",
            table: "drop_announcement_account_states",
            column: "event_id");

        migrationBuilder.CreateIndex(
            name: "IX_drop_announcement_acknowledgements_account_id_event_id_bann~",
            table: "drop_announcement_acknowledgements",
            columns: new[] { "account_id", "event_id", "banner_acknowledged_at" });

        migrationBuilder.CreateIndex(
            name: "IX_drop_announcement_acknowledgements_account_id_event_id_drop~",
            table: "drop_announcement_acknowledgements",
            columns: new[] { "account_id", "event_id", "drops_acknowledged_at" });

        migrationBuilder.CreateIndex(
            name: "IX_drop_announcement_acknowledgements_account_id_event_id_subm~",
            table: "drop_announcement_acknowledgements",
            columns: new[] { "account_id", "event_id", "submission_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_drop_announcement_acknowledgements_event_id",
            table: "drop_announcement_acknowledgements",
            column: "event_id");

        migrationBuilder.CreateIndex(
            name: "IX_drop_announcement_acknowledgements_submission_id",
            table: "drop_announcement_acknowledgements",
            column: "submission_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "drop_announcement_account_states");

        migrationBuilder.DropTable(
            name: "drop_announcement_acknowledgements");

        migrationBuilder.DropIndex(
            name: "IX_submissions_event_id_announcement_generation_announcement_o~",
            table: "submissions");

        migrationBuilder.DropColumn(
            name: "announcement_generation",
            table: "submissions");

        migrationBuilder.DropColumn(
            name: "announcement_ordinal",
            table: "submissions");

        migrationBuilder.DropColumn(
            name: "completed_tile_at_approval",
            table: "submissions");

        migrationBuilder.DropColumn(
            name: "announcement_generation",
            table: "events");

        migrationBuilder.DropColumn(
            name: "announcement_sequence",
            table: "events");

        migrationBuilder.DropColumn(
            name: "announcements_tracking_started_at",
            table: "events");
    }
}
