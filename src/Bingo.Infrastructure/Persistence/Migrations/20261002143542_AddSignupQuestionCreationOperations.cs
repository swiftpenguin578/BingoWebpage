using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSignupQuestionCreationOperations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "signup_question_creation_operations",
            columns: table => new
            {
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                question_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_signup_question_creation_operations", x => x.request_id);
                table.ForeignKey(
                    name: "FK_signup_question_creation_operations_accounts_actor_account_~",
                    column: x => x.actor_account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_signup_question_creation_operations_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_signup_question_creation_operations_actor_account_id",
            table: "signup_question_creation_operations",
            column: "actor_account_id");

        migrationBuilder.CreateIndex(
            name: "IX_signup_question_creation_operations_event_id",
            table: "signup_question_creation_operations",
            column: "event_id");

        migrationBuilder.CreateIndex(
            name: "IX_signup_question_creation_operations_question_id",
            table: "signup_question_creation_operations",
            column: "question_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "signup_question_creation_operations");
    }
}
