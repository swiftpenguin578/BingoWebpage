using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddAdminConcurrencyHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "control_version",
                table: "draft_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<Guid>(
                name: "controller_account_id",
                table: "draft_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "controller_lease_expires_at",
                table: "draft_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "boards",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "control_version",
                table: "draft_sessions");

            migrationBuilder.DropColumn(
                name: "controller_account_id",
                table: "draft_sessions");

            migrationBuilder.DropColumn(
                name: "controller_lease_expires_at",
                table: "draft_sessions");

            migrationBuilder.DropColumn(
                name: "version",
                table: "boards");
        }
}
