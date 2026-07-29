using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSignupAnswerIntegrityRelations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_signup_answers_signup_question_id",
            table: "signup_answers",
            column: "signup_question_id");

        migrationBuilder.AddForeignKey(
            name: "FK_signup_answers_event_participants_event_participant_id",
            table: "signup_answers",
            column: "event_participant_id",
            principalTable: "event_participants",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_signup_answers_signup_questions_signup_question_id",
            table: "signup_answers",
            column: "signup_question_id",
            principalTable: "signup_questions",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_signup_answers_event_participants_event_participant_id",
            table: "signup_answers");

        migrationBuilder.DropForeignKey(
            name: "FK_signup_answers_signup_questions_signup_question_id",
            table: "signup_answers");

        migrationBuilder.DropIndex(
            name: "IX_signup_answers_signup_question_id",
            table: "signup_answers");
    }
}
