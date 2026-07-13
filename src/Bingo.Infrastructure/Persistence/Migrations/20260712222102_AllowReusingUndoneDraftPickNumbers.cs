using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Bingo.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AllowReusingUndoneDraftPickNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_draft_picks_draft_session_id_pick_number",
                table: "draft_picks");

            migrationBuilder.CreateIndex(
                name: "IX_draft_picks_draft_session_id_pick_number",
                table: "draft_picks",
                columns: new[] { "draft_session_id", "pick_number" },
                unique: true,
                filter: "undone_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_draft_picks_draft_session_id_pick_number",
                table: "draft_picks");

            migrationBuilder.CreateIndex(
                name: "IX_draft_picks_draft_session_id_pick_number",
                table: "draft_picks",
                columns: new[] { "draft_session_id", "pick_number" },
                unique: true);
        }
}
