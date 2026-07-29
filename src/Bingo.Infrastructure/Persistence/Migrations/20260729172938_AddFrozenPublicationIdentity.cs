using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFrozenPublicationIdentity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "public_character_name",
            table: "draft_publication_rosters",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.Sql("""
                WITH candidates AS (
                    SELECT roster.id, MIN(osrs."DisplayName") AS public_character_name, COUNT(*) AS candidate_count
                    FROM draft_publication_rosters roster
                    JOIN draft_publication_cycles cycle ON cycle.id = roster.draft_publication_cycle_id
                    JOIN draft_sessions draft ON draft.id = cycle.draft_session_id
                    JOIN event_participant_characters assignment
                      ON assignment.event_participant_id = roster.event_participant_id
                     AND assignment.event_id = draft.event_id
                     AND assignment.event_role = 'Playing'
                     AND assignment.registered_at <= cycle.published_at
                     AND (assignment.released_at IS NULL OR assignment.released_at > cycle.published_at)
                    JOIN osrs_characters osrs ON osrs."Id" = assignment.osrs_character_id
                    GROUP BY roster.id
                )
                UPDATE draft_publication_rosters roster
                   SET public_character_name = candidates.public_character_name
                  FROM candidates
                 WHERE roster.id = candidates.id
                   AND candidates.candidate_count = 1
                   AND candidates.public_character_name IS NOT NULL
                   AND btrim(candidates.public_character_name) <> '';

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM draft_publication_rosters WHERE public_character_name IS NULL OR btrim(public_character_name) = '') THEN
                        RAISE EXCEPTION 'Cannot backfill a unique publication-time public character identity for every retained roster entry.';
                    END IF;
                END $$;
                """);

        migrationBuilder.AlterColumn<string>(
            name: "public_character_name",
            table: "draft_publication_rosters",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "public_character_name",
            table: "draft_publication_rosters");
    }
}
