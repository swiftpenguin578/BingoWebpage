using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCoCaptainSignupQuestion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO signup_questions (
                id, signup_form_id, event_id, key, label, help_text, type, required,
                position, options, active, disabled_at, disabled_by_account_id,
                disabled_reason, replaced_by_signup_question_id, system_field,
                account_answer_role, public_on_signup_board, version)
            SELECT md5('co-captain:' || f.id::text)::uuid, f.id, f.event_id,
                   candidate.signup_key, 'Co-captain (optional)', NULL, 'Text', FALSE,
                   COALESCE((SELECT MAX(q.position) + 1 FROM signup_questions q WHERE q.signup_form_id = f.id), 2),
                   NULL, TRUE, NULL, NULL, NULL, NULL, 'CoCaptainName',
                   NULL, TRUE, 0
            FROM signup_forms f
            CROSS JOIN LATERAL (
                SELECT CASE
                    WHEN NOT EXISTS (
                        SELECT 1 FROM signup_questions q
                        WHERE q.signup_form_id = f.id AND q.key = 'co_captain'
                    ) THEN 'co_captain'
                    ELSE 'co_captain_' || (COALESCE(MAX(substring(q.key FROM '^co_captain_([0-9]+)$')::numeric), 1) + 1)::text
                END AS signup_key
                FROM signup_questions q
                WHERE q.signup_form_id = f.id
            ) AS candidate
            WHERE NOT EXISTS (
                SELECT 1 FROM signup_questions q
                WHERE q.signup_form_id = f.id
                  AND q.system_field = 'CoCaptainName'
            );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the additive question and any retained responses if this migration is rolled back.
    }
}
