using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Bingo.Infrastructure.Persistence.Migrations;

public partial class AddSlice8EvidenceFoundation : Migration
{
    private static readonly string[] ActiveAssetColumns = ["submission_id", "active"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_evidence_assets_submission_id_active", "evidence_assets");
        migrationBuilder.AddColumn<string>("credited_character_name", "submissions", type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<Guid>("credited_osrs_character_id", "submissions", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>("resubmission_of_submission_id", "submissions", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<int>("version", "submissions", type: "integer", nullable: true);

        migrationBuilder.Sql("""
            DO $$
            DECLARE
                orphan_assets text;
                orphan_contributions text;
                orphan_reviews text;
                duplicate_assets text;
                invalid_participants text;
                missing_attribution text;
                ambiguous_attribution text;
                future_only_swaps text;
                invalid_swap_targets text;
                unsupported_status text;
                unsupported_review_actions text;
                hidden_approved text;
                missing_characters text;
            BEGIN
                SELECT string_agg(a.id::text, ',' ORDER BY a.id) INTO orphan_assets
                FROM evidence_assets a LEFT JOIN submissions s ON s.id = a.submission_id
                WHERE s.id IS NULL;
                SELECT string_agg(c.id::text, ',' ORDER BY c.id) INTO orphan_contributions
                FROM submission_contributions c LEFT JOIN submissions s ON s.id = c.submission_id
                WHERE s.id IS NULL;
                SELECT string_agg(r.id::text, ',' ORDER BY r.id) INTO orphan_reviews
                FROM review_actions r LEFT JOIN submissions s ON s.id = r.submission_id
                WHERE s.id IS NULL;
                SELECT string_agg(q.submission_id::text, ',' ORDER BY q.submission_id) INTO duplicate_assets
                FROM (SELECT a.submission_id FROM evidence_assets a WHERE a.active GROUP BY a.submission_id HAVING count(*) > 1) q;
                SELECT string_agg(s.id::text, ',' ORDER BY s.id) INTO invalid_participants
                FROM submissions s LEFT JOIN event_participants p ON p.id = s.credited_participant_id
                WHERE p.id IS NULL OR p.event_id <> s.event_id;
                SELECT string_agg(s.id::text, ',' ORDER BY s.id) INTO hidden_approved
                FROM submissions s
                WHERE s.status = 'Approved' AND (s.public_evidence_hidden OR s.public_player_hidden);

                SELECT string_agg(s.id::text, ',' ORDER BY s.id) INTO unsupported_status
                FROM submissions s
                WHERE s.status NOT IN ('Pending', 'ChangesRequested', 'Approved', 'Rejected', 'Withdrawn', 'Reversed');
                SELECT string_agg(r.id::text, ',' ORDER BY r.id) INTO unsupported_review_actions
                FROM review_actions r
                WHERE r.action NOT IN ('Submitted', 'RequestChanges', 'EditMetadata', 'Approve', 'Reject', 'MarkDuplicate', 'HidePublicEvidence', 'ShowPublicEvidence', 'ReverseApproval', 'Withdraw', 'Resubmit', 'ReplaceEvidence', 'RebalanceContribution');
                SELECT string_agg(s.id::text, ',' ORDER BY s.id) INTO future_only_swaps
                FROM submissions s
                WHERE NOT EXISTS (
                    SELECT 1 FROM event_participant_character_swaps sw
                    WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id
                      AND sw.effective_at_utc <= s.submitted_at)
                  AND EXISTS (
                    SELECT 1 FROM event_participant_character_swaps sw
                    WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id
                      AND sw.effective_at_utc > s.submitted_at);
                SELECT string_agg(s.id::text, ',' ORDER BY s.id) INTO invalid_swap_targets
                FROM submissions s
                WHERE EXISTS (
                    SELECT 1 FROM event_participant_character_swaps sw
                    WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id
                      AND sw.effective_at_utc <= s.submitted_at)
                  AND NOT EXISTS (
                    SELECT 1
                    FROM event_participant_character_swaps sw
                    JOIN event_participant_characters a
                      ON a.event_id = sw.event_id AND a.event_participant_id = sw.event_participant_id
                     AND a.osrs_character_id = sw.next_osrs_character_id
                     AND a.event_role = 'Playing'
                     AND a.registered_at <= s.submitted_at
                     AND (a.released_at IS NULL OR a.released_at > s.submitted_at)
                    WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id
                      AND sw.effective_at_utc <= s.submitted_at
                      AND sw.id = (SELECT latest.id FROM event_participant_character_swaps latest
                                   WHERE latest.event_participant_id = s.credited_participant_id AND latest.event_id = s.event_id
                                     AND latest.effective_at_utc <= s.submitted_at
                                   ORDER BY latest.effective_at_utc DESC, latest.recorded_at_utc DESC, latest.id DESC LIMIT 1));

                SELECT string_agg(s.id::text, ',' ORDER BY s.id) INTO missing_attribution
                FROM submissions s
                WHERE NOT EXISTS (
                    SELECT 1 FROM event_participant_character_swaps sw
                    WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id)
                  AND NOT EXISTS (
                    SELECT 1 FROM event_participant_characters a
                    WHERE a.event_participant_id = s.credited_participant_id AND a.event_id = s.event_id
                      AND a.event_role = 'Playing' AND a.registered_at <= s.submitted_at
                      AND (a.released_at IS NULL OR a.released_at > s.submitted_at));
                SELECT string_agg(s.id::text, ',' ORDER BY s.id) INTO ambiguous_attribution
                FROM submissions s
                WHERE NOT EXISTS (
                    SELECT 1 FROM event_participant_character_swaps sw
                    WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id)
                  AND (SELECT count(*) FROM event_participant_characters a
                       WHERE a.event_participant_id = s.credited_participant_id AND a.event_id = s.event_id
                         AND a.event_role = 'Playing' AND a.registered_at <= s.submitted_at
                         AND (a.released_at IS NULL OR a.released_at > s.submitted_at)) > 1;

                IF orphan_assets IS NOT NULL OR orphan_contributions IS NOT NULL OR orphan_reviews IS NOT NULL OR
                   duplicate_assets IS NOT NULL OR invalid_participants IS NOT NULL OR missing_attribution IS NOT NULL OR
                   ambiguous_attribution IS NOT NULL OR future_only_swaps IS NOT NULL OR invalid_swap_targets IS NOT NULL OR
                   unsupported_status IS NOT NULL OR unsupported_review_actions IS NOT NULL OR hidden_approved IS NOT NULL THEN
                    RAISE EXCEPTION 'Slice 8.1 retained-data preflight failed; orphan_assets=%; orphan_contributions=%; orphan_reviews=%; duplicate_active_assets=%; invalid_participants=%; missing_attribution=%; ambiguous_attribution=%; future_only_swaps=%; invalid_swap_targets=%; unsupported_status=%; unsupported_review_actions=%; approved_hidden=%',
                        COALESCE(orphan_assets, '-'), COALESCE(orphan_contributions, '-'), COALESCE(orphan_reviews, '-'),
                        COALESCE(duplicate_assets, '-'), COALESCE(invalid_participants, '-'), COALESCE(missing_attribution, '-'),
                        COALESCE(ambiguous_attribution, '-'), COALESCE(future_only_swaps, '-'), COALESCE(invalid_swap_targets, '-'),
                        COALESCE(unsupported_status, '-'), COALESCE(unsupported_review_actions, '-'), COALESCE(hidden_approved, '-');
                END IF;

                UPDATE submissions s
                SET credited_osrs_character_id = COALESCE(
                        (SELECT sw.next_osrs_character_id FROM event_participant_character_swaps sw
                         WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id
                           AND sw.effective_at_utc <= s.submitted_at
                         ORDER BY sw.effective_at_utc DESC, sw.recorded_at_utc DESC, sw.id DESC LIMIT 1),
                        (SELECT a.osrs_character_id FROM event_participant_characters a
                         WHERE a.event_participant_id = s.credited_participant_id AND a.event_id = s.event_id
                           AND a.event_role = 'Playing' AND a.registered_at <= s.submitted_at
                           AND (a.released_at IS NULL OR a.released_at > s.submitted_at)
                           AND NOT EXISTS (SELECT 1 FROM event_participant_character_swaps sw WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id)
                         ORDER BY a.registration_order, a.id LIMIT 1)),
                    credited_character_name = c."DisplayName",
                    version = 1
                FROM osrs_characters c
                WHERE c."Id" = COALESCE(
                    (SELECT sw.next_osrs_character_id FROM event_participant_character_swaps sw
                     WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id
                     AND sw.effective_at_utc <= s.submitted_at
                     ORDER BY sw.effective_at_utc DESC, sw.recorded_at_utc DESC, sw.id DESC LIMIT 1),
                    (SELECT a.osrs_character_id FROM event_participant_characters a
                     WHERE a.event_participant_id = s.credited_participant_id AND a.event_id = s.event_id
                       AND a.event_role = 'Playing' AND a.registered_at <= s.submitted_at
                       AND (a.released_at IS NULL OR a.released_at > s.submitted_at)
                       AND NOT EXISTS (SELECT 1 FROM event_participant_character_swaps sw WHERE sw.event_participant_id = s.credited_participant_id AND sw.event_id = s.event_id)
                     ORDER BY a.registration_order, a.id LIMIT 1));

                SELECT string_agg(s.id::text, ',' ORDER BY s.id) INTO missing_characters
                FROM submissions s LEFT JOIN osrs_characters c ON c."Id" = s.credited_osrs_character_id
                WHERE c."Id" IS NULL;
                IF missing_characters IS NOT NULL THEN
                    RAISE EXCEPTION 'Slice 8.1 retained-data preflight failed; missing_character_rows=%', missing_characters;
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<string>("credited_character_name", "submissions", type: "character varying(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "character varying(100)", oldMaxLength: 100, oldNullable: true);
        migrationBuilder.AlterColumn<Guid>("credited_osrs_character_id", "submissions", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<int>("version", "submissions", type: "integer", nullable: false, defaultValue: 1, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
        migrationBuilder.CreateIndex("IX_submissions_credited_osrs_character_id", "submissions", "credited_osrs_character_id");
        migrationBuilder.CreateIndex("IX_submissions_credited_participant_id", "submissions", "credited_participant_id");
        migrationBuilder.CreateIndex("IX_submissions_resubmission_of_submission_id", "submissions", "resubmission_of_submission_id", unique: true, filter: "resubmission_of_submission_id IS NOT NULL");
        migrationBuilder.CreateIndex("IX_evidence_assets_submission_id_active", "evidence_assets", ActiveAssetColumns, unique: true, filter: "active");
        migrationBuilder.AddForeignKey(name: "FK_evidence_assets_submissions_submission_id", table: "evidence_assets", column: "submission_id", principalTable: "submissions", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_review_actions_submissions_submission_id", table: "review_actions", column: "submission_id", principalTable: "submissions", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_submission_contributions_submissions_submission_id", table: "submission_contributions", column: "submission_id", principalTable: "submissions", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_submissions_event_participants_credited_participant_id", table: "submissions", column: "credited_participant_id", principalTable: "event_participants", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_submissions_osrs_characters_credited_osrs_character_id", table: "submissions", column: "credited_osrs_character_id", principalTable: "osrs_characters", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_submissions_submissions_resubmission_of_submission_id", table: "submissions", column: "resubmission_of_submission_id", principalTable: "submissions", principalColumn: "id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_evidence_assets_submissions_submission_id", "evidence_assets");
        migrationBuilder.DropForeignKey("FK_review_actions_submissions_submission_id", "review_actions");
        migrationBuilder.DropForeignKey("FK_submission_contributions_submissions_submission_id", "submission_contributions");
        migrationBuilder.DropForeignKey("FK_submissions_event_participants_credited_participant_id", "submissions");
        migrationBuilder.DropForeignKey("FK_submissions_osrs_characters_credited_osrs_character_id", "submissions");
        migrationBuilder.DropForeignKey("FK_submissions_submissions_resubmission_of_submission_id", "submissions");
        migrationBuilder.DropIndex("IX_submissions_credited_osrs_character_id", "submissions");
        migrationBuilder.DropIndex("IX_submissions_credited_participant_id", "submissions");
        migrationBuilder.DropIndex("IX_submissions_resubmission_of_submission_id", "submissions");
        migrationBuilder.DropIndex("IX_evidence_assets_submission_id_active", "evidence_assets");
        migrationBuilder.AlterColumn<int>("version", "submissions", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldDefaultValue: 1);
        migrationBuilder.DropColumn("credited_character_name", "submissions");
        migrationBuilder.DropColumn("credited_osrs_character_id", "submissions");
        migrationBuilder.DropColumn("resubmission_of_submission_id", "submissions");
        migrationBuilder.DropColumn("version", "submissions");
        migrationBuilder.CreateIndex("IX_evidence_assets_submission_id_active", "evidence_assets", ActiveAssetColumns);
    }
}
