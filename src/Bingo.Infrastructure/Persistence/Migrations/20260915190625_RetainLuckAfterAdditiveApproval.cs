using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class RetainLuckAfterAdditiveApproval : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "stats_luck_invalidated_at_revision",
            table: "events",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        // Existing revisions do not identify which past changes were additive. Start at the
        // known current boundary; do not infer compatibility for an older checkpoint.
        migrationBuilder.Sql("UPDATE events SET stats_luck_invalidated_at_revision = stats_evidence_revision");

        migrationBuilder.AddCheckConstraint(
            name: "ck_events_stats_luck_invalidation",
            table: "events",
            sql: "stats_luck_invalidated_at_revision >= 0 AND stats_luck_invalidated_at_revision <= stats_evidence_revision");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_events_stats_luck_invalidation",
            table: "events");

        migrationBuilder.DropColumn(
            name: "stats_luck_invalidated_at_revision",
            table: "events");
    }
}
