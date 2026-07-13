using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Bingo.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddEvidenceSubmissionAndReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "evidence_code_enabled",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reopened_submission_cutoff_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evidence_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    media_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    pixel_width = table.Column<int>(type: "integer", nullable: false),
                    pixel_height = table.Column<int>(type: "integer", nullable: false),
                    checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evidence_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    activates_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "review_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    performed_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    before_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    after_snapshot = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_actions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "submission_contributions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    drop_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credited_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reversed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_contributions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board_tile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    drop_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credited_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claimed_weight = table.Column<int>(type: "integer", nullable: false),
                    approved_contribution = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    captain_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    public_evidence_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    public_player_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    expected_evidence_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    current_reviewer_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    duplicate_of_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_assets_checksum",
                table: "evidence_assets",
                column: "checksum");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_assets_submission_id_active",
                table: "evidence_assets",
                columns: new[] { "submission_id", "active" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_codes_event_id_activates_at",
                table: "evidence_codes",
                columns: new[] { "event_id", "activates_at" });

            migrationBuilder.CreateIndex(
                name: "IX_review_actions_submission_id_performed_at",
                table: "review_actions",
                columns: new[] { "submission_id", "performed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_submission_contributions_submission_id",
                table: "submission_contributions",
                column: "submission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_contributions_team_id_requirement_id_reversed_at",
                table: "submission_contributions",
                columns: new[] { "team_id", "requirement_id", "reversed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_submissions_event_id_status_submitted_at",
                table: "submissions",
                columns: new[] { "event_id", "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_submissions_team_id_submitted_at",
                table: "submissions",
                columns: new[] { "team_id", "submitted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_assets");

            migrationBuilder.DropTable(
                name: "evidence_codes");

            migrationBuilder.DropTable(
                name: "review_actions");

            migrationBuilder.DropTable(
                name: "submission_contributions");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.DropColumn(
                name: "evidence_code_enabled",
                table: "events");

            migrationBuilder.DropColumn(
                name: "reopened_submission_cutoff_at",
                table: "events");
        }
}
