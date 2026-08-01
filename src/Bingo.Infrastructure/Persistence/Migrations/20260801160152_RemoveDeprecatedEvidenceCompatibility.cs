using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RemoveDeprecatedEvidenceCompatibility : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                DO $$
                DECLARE affected text;
                BEGIN
                    SELECT string_agg(id::text, ',' ORDER BY id) INTO affected
                    FROM submissions
                    WHERE status NOT IN ('Pending', 'ChangesRequested', 'Approved', 'Rejected', 'Withdrawn', 'Reversed');
                    IF affected IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 8.3 retained-data preflight failed; unsupported submission status IDs: %', affected;
                    END IF;
                    SELECT string_agg(id::text, ',' ORDER BY id) INTO affected
                    FROM review_actions
                    WHERE action NOT IN ('Submitted', 'RequestChanges', 'EditMetadata', 'Approve', 'Reject', 'MarkDuplicate', 'HidePublicEvidence', 'ShowPublicEvidence', 'ReverseApproval', 'Withdraw', 'Resubmit', 'ReplaceEvidence', 'RebalanceContribution');
                    IF affected IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 8.3 retained-data preflight failed; unsupported review action IDs: %', affected;
                    END IF;
                    SELECT string_agg(id::text, ',' ORDER BY id) INTO affected
                    FROM evidence_assets asset
                    WHERE NOT EXISTS (SELECT 1 FROM submissions submission WHERE submission.id = asset.submission_id);
                    IF affected IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 8.3 retained-data preflight failed; orphaned evidence asset IDs: %', affected;
                    END IF;
                    SELECT string_agg(id::text, ',' ORDER BY id) INTO affected
                    FROM submission_contributions contribution
                    WHERE NOT EXISTS (SELECT 1 FROM submissions submission WHERE submission.id = contribution.submission_id);
                    IF affected IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 8.3 retained-data preflight failed; orphaned contribution IDs: %', affected;
                    END IF;
                    SELECT string_agg(id::text, ',' ORDER BY id) INTO affected
                    FROM review_actions action
                    WHERE NOT EXISTS (SELECT 1 FROM submissions submission WHERE submission.id = action.submission_id);
                    IF affected IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 8.3 retained-data preflight failed; orphaned review action IDs: %', affected;
                    END IF;
                    SELECT string_agg(id::text, ',' ORDER BY id) INTO affected
                    FROM submissions
                    WHERE status = 'Approved' AND (public_evidence_hidden OR public_player_hidden);
                    IF affected IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 8.3 retained-data preflight failed; approved-hidden submission IDs: %', affected;
                    END IF;
                END $$;
                """);

        migrationBuilder.Sql("UPDATE submissions SET status = 'Rejected' WHERE status = 'ChangesRequested';");

        migrationBuilder.DropColumn(
            name: "duplicate_of_submission_id",
            table: "submissions");

        migrationBuilder.DropColumn(
            name: "public_evidence_hidden",
            table: "submissions");

        migrationBuilder.DropColumn(
            name: "public_player_hidden",
            table: "submissions");

        migrationBuilder.DropColumn(
            name: "public_privacy_requested",
            table: "submissions");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "duplicate_of_submission_id",
            table: "submissions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "public_evidence_hidden",
            table: "submissions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "public_player_hidden",
            table: "submissions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "public_privacy_requested",
            table: "submissions",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }
}
