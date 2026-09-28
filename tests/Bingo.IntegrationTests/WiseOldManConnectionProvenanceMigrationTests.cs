using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class WiseOldManConnectionProvenanceMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_wom_provenance")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task BackfillUsesCreateReceiptAndNeverInfersOwnershipFromCredentialPresence()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
        const string migrationBeforeWomProvenance = "20260926135604_AddDraftPublicationMethod";

        await using (var before = new ApplicationDbContext(options))
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(migrationBeforeWomProvenance);
            var now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
            var websiteEventId = Guid.NewGuid();
            var externalEventId = Guid.NewGuid();
            var unknownEventId = Guid.NewGuid();
            var websiteSyncId = Guid.NewGuid();
            var externalSyncId = Guid.NewGuid();
            var unknownSyncId = Guid.NewGuid();
            var websiteManagementId = Guid.NewGuid();
            var unknownManagementId = Guid.NewGuid();
            var websiteCreateId = Guid.NewGuid();
            var unknownUpdateId = Guid.NewGuid();
            const string emptyPayload = "{}";

            // Seed through SQL because this context has the post-migration model
            // while the database is intentionally at the pre-WOM migration.
            // Constraint triggers are disabled only for these disposable fixture
            // rows; the migration itself still runs against the real schema.
            await using var seedTransaction = await before.Database.BeginTransactionAsync();
            await before.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
            try
            {
                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO event_competition_synchronizations
                        (id, event_id, generation, competition_id, competition_title,
                         competition_starts_at, competition_ends_at, assignment_fingerprint, retry_count)
                    VALUES
                        ({websiteSyncId}, {websiteEventId}, 1, 1001, {"website-created"}, {now.AddHours(1)}, {now.AddHours(2)}, {"website-assignment"}, 0),
                        ({externalSyncId}, {externalEventId}, 1, 1002, {"external-id-only"}, {now.AddHours(1)}, {now.AddHours(2)}, {"external-assignment"}, 0),
                        ({unknownSyncId}, {unknownEventId}, 1, 1003, {"unknown-management"}, {now.AddHours(1)}, {now.AddHours(2)}, {"unknown-assignment"}, 0);

                    INSERT INTO event_competition_management
                        (id, event_id, synchronization_id, competition_id, competition_title,
                         competition_starts_at, competition_ends_at, protected_verification_code,
                         managed_field_scope, status, last_applied_local_fingerprint,
                         management_version, created_at, updated_at)
                    VALUES
                        ({websiteManagementId}, {websiteEventId}, {websiteSyncId}, 1001, {"website-created"}, {now.AddHours(1)}, {now.AddHours(2)}, {"protected-website-code"}, {"title,schedule,teams,participants"}, {"Active"}, {"website-fingerprint"}, 1, {now}, {now}),
                        ({unknownManagementId}, {unknownEventId}, {unknownSyncId}, 1003, {"unknown-management"}, {now.AddHours(1)}, {now.AddHours(2)}, {"protected-but-unproven-code"}, {"title,schedule,teams,participants"}, {"Active"}, {"unknown-fingerprint"}, 1, {now}, {now});

                    -- Production Create operations carry the event actor but
                    -- have no management row yet; the receipt itself is the
                    -- durable evidence used by the provenance backfill.
                    INSERT INTO event_competition_management_operations
                        (id, event_id, management_id, operation_type, desired_payload_json,
                         desired_fingerprint, phase, event_version, remote_competition_id,
                         remote_receipt_reference, attempt_count, created_at, updated_at, version)
                    VALUES
                        ({websiteCreateId}, {websiteEventId}, NULL, {"Create"}, {emptyPayload}, {"website-fingerprint"}, {"Succeeded"}, 1, 1001, {"create-receipt"}, 1, {now}, {now}, 2),
                        ({unknownUpdateId}, {unknownEventId}, {unknownManagementId}, {"Update"}, {emptyPayload}, {"unknown-fingerprint"}, {"Succeeded"}, 1, 1003, {"update-receipt"}, 1, {now}, {now}, 2);
                    """);
                await seedTransaction.CommitAsync();
            }
            finally
            {
                // SET LOCAL restores the normal trigger role when the
                // transaction commits or rolls back.
            }
        }

        await using (var after = new ApplicationDbContext(options))
        {
            await after.Database.MigrateAsync();

            var websiteSync = await after.EventCompetitionSynchronizations.SingleAsync(x => x.CompetitionId == 1001);
            var websiteManagement = await after.EventCompetitionManagements.SingleAsync(x => x.CompetitionId == 1001);
            Assert.Equal(EventCompetitionProvenance.WebsiteCreated, websiteSync.Provenance);
            Assert.Equal(EventCompetitionProvenance.WebsiteCreated, websiteManagement.Provenance);
            Assert.Equal(EventCompetitionWriteCapability.Writable, websiteManagement.WriteCapability);
            Assert.Equal(EventCompetitionCredentialStatus.Valid, websiteManagement.CredentialStatus);
            Assert.True(websiteManagement.CanDelete);

            var externalSync = await after.EventCompetitionSynchronizations.SingleAsync(x => x.CompetitionId == 1002);
            Assert.Equal(EventCompetitionProvenance.External, externalSync.Provenance);
            Assert.False(await after.EventCompetitionManagements.AnyAsync(x => x.CompetitionId == 1002));

            var unknownSync = await after.EventCompetitionSynchronizations.SingleAsync(x => x.CompetitionId == 1003);
            var unknownManagement = await after.EventCompetitionManagements.SingleAsync(x => x.CompetitionId == 1003);
            Assert.Equal(EventCompetitionProvenance.Unknown, unknownSync.Provenance);
            Assert.Equal(EventCompetitionProvenance.Unknown, unknownManagement.Provenance);
            Assert.Equal(EventCompetitionWriteCapability.ReadOnly, unknownManagement.WriteCapability);
            Assert.Equal(EventCompetitionCredentialStatus.Unavailable, unknownManagement.CredentialStatus);
            Assert.False(unknownManagement.CanDelete);
        }
    }

}
