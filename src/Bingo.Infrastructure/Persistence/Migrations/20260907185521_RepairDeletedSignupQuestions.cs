using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RepairDeletedSignupQuestions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SELECT e.id FROM events e
            WHERE NOT e.draft_locked AND e.state IN ('Draft', 'SignupOpen', 'SignupClosed')
            AND EXISTS (SELECT 1 FROM signup_questions q WHERE q.event_id = e.id
                AND NOT q.active AND q.disabled_reason IS NULL AND q.replaced_by_signup_question_id IS NULL
                AND q.system_field = 'None' AND q.key NOT IN ('legacy_alt_account', 'legacy_discord_identity', 'legacy_comments'))
            FOR UPDATE;

            WITH deleted_questions AS (
                UPDATE signup_questions q SET disabled_reason = 'deleted', version = q.version + 1
                FROM events e WHERE q.event_id = e.id
                AND NOT e.draft_locked AND e.state IN ('Draft', 'SignupOpen', 'SignupClosed')
                AND NOT q.active AND q.disabled_reason IS NULL AND q.replaced_by_signup_question_id IS NULL
                AND q.system_field = 'None' AND q.key NOT IN ('legacy_alt_account', 'legacy_discord_identity', 'legacy_comments')
                RETURNING q.id, q.event_id, q.signup_form_id
            ), deleted_answers AS (
                DELETE FROM signup_answers a USING deleted_questions q WHERE a.signup_question_id = q.id
                RETURNING a.event_participant_id
            ), released_assignments AS (
                UPDATE event_participant_characters a SET released_at = CURRENT_TIMESTAMP, released_by_account_id = NULL, version = a.version + 1
                FROM deleted_questions q WHERE a.signup_question_id = q.id AND a.released_at IS NULL
                RETURNING a.event_participant_id
            ), updated_participants AS (
                UPDATE event_participants p SET response_version = p.response_version + 1
                WHERE p.id IN (SELECT event_participant_id FROM deleted_answers UNION SELECT event_participant_id FROM released_assignments)
            ), updated_forms AS (
                UPDATE signup_forms f SET version = f.version + 1 WHERE f.id IN (SELECT signup_form_id FROM deleted_questions)
            )
            INSERT INTO audit_entries (id, occurred_at, actor_username, action, target_type, target_id, details, event_id)
            SELECT gen_random_uuid(), CURRENT_TIMESTAMP, 'migration', 'signup_question.deleted', 'signup_question', q.id::text,
                'Repaired a previously removed custom question: answers deleted and current event reservations released.', q.event_id
            FROM deleted_questions q;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deleted answers and released reservations cannot safely be reconstructed.
    }
}
