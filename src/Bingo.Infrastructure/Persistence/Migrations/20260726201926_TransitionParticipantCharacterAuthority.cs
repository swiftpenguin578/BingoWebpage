#pragma warning disable IDE0161, CA1861
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransitionParticipantCharacterAuthority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_participants_event_id_normalized_primary_account_name",
                table: "event_participants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_event_participant_characters_release",
                table: "event_participant_characters");

            // Pass 2.1 retained every legacy assignment. Translate already-inactive
            // participant rows into released assignment history before the legacy
            // participant timestamps remain the only truthful release evidence.
            migrationBuilder.Sql("""
                UPDATE event_participant_characters assignment
                SET released_at = COALESCE(participant.withdrawn_at, participant.removed_at, participant.signed_up_at),
                    released_by_account_id = NULL
                FROM event_participants participant
                WHERE assignment.event_participant_id = participant.id
                  AND assignment.released_at IS NULL
                  AND participant.signup_status IN ('Withdrawn', 'Removed');
                """);

            migrationBuilder.DropColumn(
                name: "ehb_snapshot",
                table: "event_participants");

            migrationBuilder.DropColumn(
                name: "normalized_primary_account_name",
                table: "event_participants");

            migrationBuilder.DropColumn(
                name: "primary_account_name",
                table: "event_participants");

            migrationBuilder.DropColumn(
                name: "second_account_name",
                table: "event_participants");

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_participant_characters_release",
                table: "event_participant_characters",
                sql: "released_at IS NOT NULL OR released_by_account_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_event_participant_characters_release",
                table: "event_participant_characters");

            migrationBuilder.AddColumn<decimal>(
                name: "ehb_snapshot",
                table: "event_participants",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "normalized_primary_account_name",
                table: "event_participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "primary_account_name",
                table: "event_participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "second_account_name",
                table: "event_participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_event_id_normalized_primary_account_name",
                table: "event_participants",
                columns: new[] { "event_id", "normalized_primary_account_name" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_event_participant_characters_release",
                table: "event_participant_characters",
                sql: "(released_at IS NULL AND released_by_account_id IS NULL) OR (released_at IS NOT NULL AND released_by_account_id IS NOT NULL)");
        }
    }
}
