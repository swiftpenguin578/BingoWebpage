using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddBoardEditingLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "edit_control_version",
                table: "boards",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<Guid>(
                name: "editor_account_id",
                table: "boards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "editor_lease_expires_at",
                table: "boards",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "edit_control_version",
                table: "boards");

            migrationBuilder.DropColumn(
                name: "editor_account_id",
                table: "boards");

            migrationBuilder.DropColumn(
                name: "editor_lease_expires_at",
                table: "boards");
        }
}
