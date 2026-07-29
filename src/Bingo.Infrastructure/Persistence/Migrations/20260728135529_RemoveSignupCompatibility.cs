using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RemoveSignupCompatibility : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_event_participants_private_edit_token_hash",
            table: "event_participants");

        migrationBuilder.DropColumn(
            name: "allow_private_signup_editing",
            table: "events");

        migrationBuilder.DropColumn(
            name: "comments",
            table: "event_participants");

        migrationBuilder.DropColumn(
            name: "discord_identity",
            table: "event_participants");

        migrationBuilder.DropColumn(
            name: "private_edit_token_hash",
            table: "event_participants");

        migrationBuilder.DropColumn(
            name: "removed_at",
            table: "event_participants");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "allow_private_signup_editing",
            table: "events",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "comments",
            table: "event_participants",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "discord_identity",
            table: "event_participants",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "private_edit_token_hash",
            table: "event_participants",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "removed_at",
            table: "event_participants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_event_participants_private_edit_token_hash",
            table: "event_participants",
            column: "private_edit_token_hash",
            unique: true);
    }
}
