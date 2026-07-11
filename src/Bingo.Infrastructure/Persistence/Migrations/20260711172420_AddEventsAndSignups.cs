using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEventsAndSignups : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "event_participants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                primary_account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                normalized_primary_account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                second_account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                discord_identity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ehb_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                comments = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                admin_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                captain_volunteer = table.Column<bool>(type: "boolean", nullable: false),
                payment_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                signup_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                signup_sequence = table.Column<long>(type: "bigint", nullable: false),
                signed_up_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                waiting_listed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                status_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                private_edit_token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                form_version = table.Column<int>(type: "integer", nullable: false),
                source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_participants", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "event_state_transitions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                from_state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                to_state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                performed_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_state_transitions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                signup_opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                signup_closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                event_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                event_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                submission_cutoff_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                participant_cap = table.Column<int>(type: "integer", nullable: false),
                waiting_list_enabled = table.Column<bool>(type: "boolean", nullable: false),
                allow_private_signup_editing = table.Column<bool>(type: "boolean", nullable: false),
                require_signup_code = table.Column<bool>(type: "boolean", nullable: false),
                signup_code_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                public_rules = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                buy_in_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                prize_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                expected_team_count = table.Column<int>(type: "integer", nullable: true),
                expected_team_size = table.Column<int>(type: "integer", nullable: true),
                expected_board_rows = table.Column<int>(type: "integer", nullable: true),
                expected_board_columns = table.Column<int>(type: "integer", nullable: true),
                participant_list_published = table.Column<bool>(type: "boolean", nullable: false),
                draft_results_published = table.Column<bool>(type: "boolean", nullable: false),
                team_rosters_published = table.Column<bool>(type: "boolean", nullable: false),
                board_published = table.Column<bool>(type: "boolean", nullable: false),
                results_published = table.Column<bool>(type: "boolean", nullable: false),
                draft_locked = table.Column<bool>(type: "boolean", nullable: false),
                created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_events", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "signup_answers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                signup_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                question_label_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_signup_answers", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "signup_questions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                help_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                required = table.Column<bool>(type: "boolean", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                options = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_signup_questions", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_participants_event_id_normalized_primary_account_name",
            table: "event_participants",
            columns: ["event_id", "normalized_primary_account_name"]);

        migrationBuilder.CreateIndex(
            name: "IX_event_participants_event_id_signup_sequence",
            table: "event_participants",
            columns: ["event_id", "signup_sequence"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_event_participants_private_edit_token_hash",
            table: "event_participants",
            column: "private_edit_token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_event_state_transitions_event_id_performed_at",
            table: "event_state_transitions",
            columns: ["event_id", "performed_at"]);

        migrationBuilder.CreateIndex(
            name: "IX_events_slug",
            table: "events",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_signup_answers_event_participant_id_signup_question_id",
            table: "signup_answers",
            columns: ["event_participant_id", "signup_question_id"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_signup_questions_event_id_key",
            table: "signup_questions",
            columns: ["event_id", "key"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_participants");

        migrationBuilder.DropTable(
            name: "event_state_transitions");

        migrationBuilder.DropTable(
            name: "events");

        migrationBuilder.DropTable(
            name: "signup_answers");

        migrationBuilder.DropTable(
            name: "signup_questions");
    }
}
