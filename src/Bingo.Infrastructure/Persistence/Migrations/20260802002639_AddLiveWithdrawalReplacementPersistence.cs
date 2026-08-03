#pragma warning disable IDE0161, CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveWithdrawalReplacementPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_team_memberships_replaces_membership_id",
                table: "team_memberships");

            migrationBuilder.CreateTable(
                name: "waiting_list_promotion_follow_ups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ended_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    replacement_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    promoted_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waiting_list_promotion_follow_ups", x => x.id);
                    table.ForeignKey(
                        name: "FK_waiting_list_promotion_follow_ups_accounts_completed_by_acc~",
                        column: x => x.completed_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_waiting_list_promotion_follow_ups_event_participants_promot~",
                        column: x => x.promoted_participant_id,
                        principalTable: "event_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_waiting_list_promotion_follow_ups_team_memberships_ended_me~",
                        column: x => x.ended_membership_id,
                        principalTable: "team_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_waiting_list_promotion_follow_ups_team_memberships_replacem~",
                        column: x => x.replacement_membership_id,
                        principalTable: "team_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_replaces_membership_id",
                table: "team_memberships",
                column: "replaces_membership_id",
                unique: true,
                filter: "replaces_membership_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_promotion_follow_ups_completed_by_account_id",
                table: "waiting_list_promotion_follow_ups",
                column: "completed_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_promotion_follow_ups_ended_membership_id",
                table: "waiting_list_promotion_follow_ups",
                column: "ended_membership_id");

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_promotion_follow_ups_event_id_completed_at",
                table: "waiting_list_promotion_follow_ups",
                columns: new[] { "event_id", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_promotion_follow_ups_event_id_ended_membership~",
                table: "waiting_list_promotion_follow_ups",
                columns: new[] { "event_id", "ended_membership_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_promotion_follow_ups_promoted_participant_id",
                table: "waiting_list_promotion_follow_ups",
                column: "promoted_participant_id");

            migrationBuilder.CreateIndex(
                name: "IX_waiting_list_promotion_follow_ups_replacement_membership_id",
                table: "waiting_list_promotion_follow_ups",
                column: "replacement_membership_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waiting_list_promotion_follow_ups");

            migrationBuilder.DropIndex(
                name: "IX_team_memberships_replaces_membership_id",
                table: "team_memberships");

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_replaces_membership_id",
                table: "team_memberships",
                column: "replaces_membership_id");
        }
    }
}
