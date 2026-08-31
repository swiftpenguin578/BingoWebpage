using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddEventQuarantine : Migration
    {
        private static readonly string[] NotificationEventIndexColumns = ["event_id", "RecipientAccountId", "CreatedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                table: "personal_notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hidden_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "hidden_by_account_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hidden_reason",
                table: "events",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            // Existing event-scoped notifications must become suppressible when
            // an event is quarantined. Global account notifications remain null.
            migrationBuilder.Sql("""
                UPDATE personal_notifications AS n
                SET event_id = e.id
                FROM events AS e
                WHERE n.event_id IS NULL
                  AND (
                    n."Route" = '/Admin/Events/Manage/' || e.id::text
                    OR n."Route" = '/Events/' || e.slug || '/Board'
                    OR n."Route" = '/Events/' || e.slug || '/Signup'
                    OR n."Route" = '/Events/' || e.slug || '/Teams'
                    OR n."Route" LIKE '/Events/' || e.slug || '/Signup/Confirmation%'
                  );

                UPDATE personal_notifications AS n
                SET event_id = s.event_id
                FROM submissions AS s
                WHERE n.event_id IS NULL
                  AND n."Route" = '/Submissions/' || s.id::text;

                UPDATE personal_notifications AS n
                SET event_id = s.event_id
                FROM submissions AS s
                WHERE n.event_id IS NULL
                  AND n."Route" = '/Captain/Submissions/' || s.id::text;

                DO $$
                DECLARE
                    unresolved text;
                BEGIN
                    SELECT string_agg(format('id=%s route=%s', n."Id", n."Route"), '; ' ORDER BY n."Id")
                    INTO unresolved
                    FROM personal_notifications AS n
                    WHERE n.event_id IS NULL
                      AND (
                        n."Route" LIKE '/Admin/Events/Manage/%'
                        OR n."Route" LIKE '/Events/%/Board'
                        OR n."Route" LIKE '/Events/%/Signup'
                        OR n."Route" LIKE '/Events/%/Teams'
                        OR n."Route" LIKE '/Events/%/Signup/Confirmation%'
                        OR n."Route" LIKE '/Submissions/%'
                        OR n."Route" LIKE '/Captain/Submissions/%'
                      );
                    IF unresolved IS NOT NULL THEN
                        RAISE EXCEPTION 'Event quarantine notification backfill failed; unassociated event-scoped rows: %', unresolved;
                    END IF;
                END $$;
                """);

            // Emergency-account audit history is event-scoped even though the
            // original rows predate the quarantine association.
            migrationBuilder.Sql("""
                UPDATE audit_entries AS a
                SET event_id = candidate.event_id
                FROM (
                    SELECT a.id, (array_agg(access."EventId" ORDER BY access."EventId"))[1] AS event_id
                    FROM audit_entries AS a
                    JOIN accounts AS account
                      ON a.target_type = 'account'
                     AND a.target_id = account.id::text
                     AND account.account_type = 'EmergencyCaptain'
                    JOIN account_event_accesses AS access ON access."AccountId" = account.id
                    WHERE a.event_id IS NULL
                      AND (
                        a.action IN (
                          'account.emergency_created',
                          'account.emergency_cutoff_disabled',
                          'account.emergency_enabled',
                          'account.emergency_disabled',
                          'account.emergency_login',
                          'account.emergency_credential_link_created',
                          'account.captain_auto_created',
                          'account.captain_auto_disabled'
                        )
                        OR (a.action = 'account.password_reset' AND account.account_type = 'EmergencyCaptain')
                      )
                    GROUP BY a.id
                    HAVING count(DISTINCT access."EventId") = 1
                ) AS candidate
                WHERE a.id = candidate.id;

                DO $$
                DECLARE
                    unresolved text;
                BEGIN
                    SELECT string_agg(
                        format('id=%s action=%s target_type=%s target_id=%s', a.id, a.action, a.target_type, coalesce(a.target_id, '<null>')),
                        '; ' ORDER BY a.id)
                    INTO unresolved
                    FROM audit_entries AS a
                    LEFT JOIN accounts AS account
                      ON a.target_type = 'account'
                     AND a.target_id = account.id::text
                     AND account.account_type = 'EmergencyCaptain'
                    LEFT JOIN account_event_accesses AS access ON access."AccountId" = account.id
                    WHERE a.event_id IS NULL
                      AND (
                        a.action IN (
                          'account.emergency_created',
                          'account.emergency_cutoff_disabled',
                          'account.emergency_enabled',
                          'account.emergency_disabled',
                          'account.emergency_login',
                          'account.emergency_credential_link_created',
                          'account.captain_auto_created',
                          'account.captain_auto_disabled'
                        )
                        OR (a.action = 'account.password_reset' AND account.id IS NOT NULL)
                      )
                    GROUP BY a.id, a.action, a.target_type, a.target_id, account.id
                    HAVING account.id IS NULL OR count(DISTINCT access."EventId") <> 1;
                    IF unresolved IS NOT NULL THEN
                        RAISE EXCEPTION 'Event quarantine audit backfill failed; unassociated emergency rows: %', unresolved;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_personal_notifications_event_id_RecipientAccountId_CreatedAt",
                table: "personal_notifications",
                columns: NotificationEventIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_events_hidden_at",
                table: "events",
                column: "hidden_at");

            migrationBuilder.CreateIndex(
                name: "IX_events_hidden_by_account_id",
                table: "events",
                column: "hidden_by_account_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_events_hidden_metadata",
                table: "events",
                sql: "(hidden_at IS NULL AND hidden_by_account_id IS NULL AND hidden_reason IS NULL) OR (hidden_at IS NOT NULL AND hidden_by_account_id IS NOT NULL AND hidden_reason IS NOT NULL AND btrim(hidden_reason) <> '')");

            migrationBuilder.AddForeignKey(
                name: "FK_events_accounts_hidden_by_account_id",
                table: "events",
                column: "hidden_by_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_personal_notifications_events_event_id",
                table: "personal_notifications",
                column: "event_id",
                principalTable: "events",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_accounts_hidden_by_account_id",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_personal_notifications_events_event_id",
                table: "personal_notifications");

            migrationBuilder.DropIndex(
                name: "IX_personal_notifications_event_id_RecipientAccountId_CreatedAt",
                table: "personal_notifications");

            migrationBuilder.DropIndex(
                name: "IX_events_hidden_at",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_hidden_by_account_id",
                table: "events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_events_hidden_metadata",
                table: "events");

            migrationBuilder.DropColumn(
                name: "event_id",
                table: "personal_notifications");

            migrationBuilder.DropColumn(
                name: "hidden_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "hidden_by_account_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "hidden_reason",
                table: "events");
        }
}
