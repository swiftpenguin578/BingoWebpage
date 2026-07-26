using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice2MigrationRehearsalTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260725170951_AddEmergencyLifecycleAndPersonalNotifications";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice2_migration_rehearsal")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task RepresentativeRetainedAndCleanMigrationsSucceedDeterministically()
    {
        var accountId = Guid.NewGuid();
        var linkedCharacterId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow.AddDays(-2);

        await using (var retained = new ApplicationDbContext(options))
        {
            await retained.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await retained.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO accounts
                    (id, password_hash, disabled_at, created_at, last_login_at, must_change_password,
                     account_type, active, authorization_version, login_name, normalized_login_name,
                     password_changed_at, onboarding_completed_at, global_role, public_username,
                     normalized_public_username, password_version, version)
                VALUES
                    ({accountId}, {"hash"}, NULL, {created}, NULL, FALSE,
                     {"WebsiteAccount"}, TRUE, 1, {"retained-user"}, {"RETAINED-USER"},
                     {created}, {created}, {"User"}, {"retained-user"}, {"RETAINED-USER"}, 1, 1);
                INSERT INTO osrs_characters ("Id", "DisplayName", "NormalizedName", "CreatedAt", "UpdatedAt")
                VALUES ({linkedCharacterId}, {"Existing Main"}, {"EXISTING MAIN"}, {created}, {created});
                INSERT INTO account_osrs_characters
                    ("Id", "AccountId", "OsrsCharacterId", active, preferred, "Position", "CreatedAt", "UpdatedAt")
                VALUES ({linkId}, {accountId}, {linkedCharacterId}, TRUE, TRUE, 4, {created}, {created.AddHours(1)});
                INSERT INTO events
                    (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at,
                     event_starts_at, event_ends_at, submission_cutoff_at, participant_cap,
                     waiting_list_enabled, allow_private_signup_editing, require_signup_code,
                     participant_list_published, draft_results_published, team_rosters_published,
                     board_published, results_published, draft_locked, created_by_account_id, created_at)
                VALUES
                    ({eventId}, {"Retained event"}, {"retained-event"}, {""}, {"UTC"}, {"Draft"},
                     {created}, {created.AddHours(1)}, {created.AddDays(1)}, {created.AddDays(2)},
                     {created.AddDays(2).AddHours(1)}, 10, TRUE, TRUE, FALSE, FALSE, FALSE,
                     FALSE, FALSE, FALSE, FALSE, {accountId}, {created});
                INSERT INTO event_participants
                    (id, event_id, primary_account_name, normalized_primary_account_name,
                     second_account_name, ehb_snapshot, captain_volunteer, payment_status,
                     signup_status, signup_sequence, signed_up_at, form_version, source)
                VALUES
                    ({participantId}, {eventId}, {"Existing Main"}, {"EXISTING MAIN"},
                     {"Helper Alt"}, 123.45, FALSE, {"Unknown"}, {"Confirmed"}, 1, {created}, 1, {"CsvImport"});
                """);

            await retained.GetService<IMigrator>().MigrateAsync();
        }

        await using (var migrated = new ApplicationDbContext(options))
        {
            var link = await migrated.AccountOsrsCharacters.SingleAsync();
            Assert.Equal(linkId, link.Id);
            Assert.Equal(accountId, link.AccountId);
            Assert.Equal(linkedCharacterId, link.OsrsCharacterId);
            Assert.True(link.Active);
            Assert.True(link.Preferred);
            Assert.Equal(4, link.SortOrder);
            Assert.Equal(created, link.LinkedAt);
            Assert.Equal(accountId, link.LinkedByAccountId);
            Assert.Null(link.UnlinkedAt);
            Assert.Null(link.SavedEhb);

            var participant = await migrated.EventParticipants.SingleAsync();
            Assert.Equal(participantId, participant.Id);
            Assert.Null(participant.AccountId);
            var authority = await migrated.PrimaryCharacters().SingleAsync();
            Assert.Equal("Existing Main", authority.Name);
            Assert.Equal(123.45m, authority.Ehb);

            var assignments = await migrated.EventParticipantCharacters
                .OrderBy(x => x.RegistrationOrder)
                .ToListAsync();
            Assert.Collection(
                assignments,
                primary =>
                {
                    Assert.Equal(EventCharacterRole.Playing, primary.EventRole);
                    Assert.Equal(linkedCharacterId, primary.OsrsCharacterId);
                    Assert.Equal(123.45m, primary.EhbSnapshot);
                    Assert.Equal(EhbSource.Import, primary.EhbSource);
                    Assert.Null(primary.RegisteredByAccountId);
                },
                secondary =>
                {
                    Assert.Equal(EventCharacterRole.Informational, secondary.EventRole);
                    Assert.Null(secondary.EhbSnapshot);
                    Assert.Null(secondary.EhbSource);
                    Assert.Equal("HELPER ALT", migrated.OsrsCharacters.Single(x => x.Id == secondary.OsrsCharacterId).NormalizedName);
                });
            Assert.Equal(2, await migrated.OsrsCharacters.CountAsync());
            Assert.Empty(await migrated.AccountOsrsCharacters.Where(x => x.OsrsCharacterId != linkedCharacterId).ToListAsync());
        }

        await using (var clean = new ApplicationDbContext(options))
        {
            await clean.Database.EnsureDeletedAsync();
            await clean.Database.MigrateAsync();
            Assert.Empty(await clean.EventParticipantCharacters.ToListAsync());
            Assert.Empty(await clean.EventParticipants.ToListAsync());
            Assert.Empty(await clean.AccountOsrsCharacters.ToListAsync());
        }
    }
}
