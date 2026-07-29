using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class NormalizeRetainedLegacyDiscordSystemField : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The retained conversion used field names removed from the active enum.
        // Preserve every retained-only question/answer as disabled custom history instead.
        migrationBuilder.Sql("UPDATE signup_questions SET system_field = 'None', public_on_signup_board = FALSE WHERE system_field IN ('LegacySecondaryAltAccount', 'LegacyDiscordIdentity', 'LegacyComments');");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE signup_questions SET system_field = CASE key WHEN 'legacy_alt_account' THEN 'LegacySecondaryAltAccount' WHEN 'legacy_discord_identity' THEN 'LegacyDiscordIdentity' WHEN 'legacy_comments' THEN 'LegacyComments' ELSE system_field END WHERE key IN ('legacy_alt_account', 'legacy_discord_identity', 'legacy_comments');");
    }
}
