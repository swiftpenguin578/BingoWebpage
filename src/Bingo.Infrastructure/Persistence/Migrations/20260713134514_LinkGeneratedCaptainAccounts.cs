using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class LinkGeneratedCaptainAccounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "captain_participant_id",
            table: "accounts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_accounts_captain_participant_id",
            table: "accounts",
            column: "captain_participant_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_accounts_captain_participant_id",
            table: "accounts");

        migrationBuilder.DropColumn(
            name: "captain_participant_id",
            table: "accounts");
    }
}
