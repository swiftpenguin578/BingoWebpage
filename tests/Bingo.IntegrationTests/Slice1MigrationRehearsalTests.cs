using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice1MigrationRehearsalTests : IAsyncLifetime
{
    private const string LegacyMigration = "20260721232916_AddBoardEditorAndCatalogueRateMechanics";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice1_migration_rehearsal")
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
    public async Task RetainedLegacyAndCleanProductionRehearsalsSucceed()
    {
        var adminId = Guid.NewGuid();
        var captainId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow.AddDays(-1);

        await using (var legacy = new ApplicationDbContext(options))
        {
            var migrator = legacy.GetService<IMigrator>();
            await migrator.MigrateAsync(LegacyMigration);
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO accounts (id, username, normalized_username, password_hash, role, event_id, team_id, captain_participant_id, active_from, correction_only_from, expires_at, disabled_at, created_at, last_login_at, must_change_password)
                VALUES ({adminId}, {"legacy-admin"}, {"LEGACY-ADMIN"}, {"legacy-admin-hash"}, {"Admin"}, NULL, NULL, NULL, NULL, NULL, NULL, NULL, {created}, NULL, FALSE),
                       ({captainId}, {"legacy-captain"}, {"LEGACY-CAPTAIN"}, {"legacy-captain-hash"}, {"Captain"}, {eventId}, {teamId}, {participantId}, NULL, NULL, {created.AddDays(2)}, NULL, {created}, NULL, FALSE);
                """);
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO audit_entries (id, occurred_at, actor_account_id, actor_username, action, target_type, target_id, details) VALUES ({Guid.NewGuid()}, {created}, {adminId}, {"legacy-admin"}, {"legacy.action"}, {"account"}, {captainId.ToString()}, {"Legacy history"});");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, allow_private_signup_editing, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at) VALUES ({eventId}, {"Legacy event"}, {"legacy-event"}, {""}, {"UTC"}, {"Draft"}, {created}, {created}, {created}, {created.AddDays(1)}, {created.AddHours(2)}, {10}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {adminId}, {created});");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO teams (id, event_id, name, slug, formation_type, included_in_draft, active) VALUES ({teamId}, {eventId}, {"Legacy team"}, {"legacy-team"}, {"Preformed"}, {false}, {true});");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO event_participants (id, event_id, primary_account_name, normalized_primary_account_name, ehb_snapshot, captain_volunteer, payment_status, signup_status, signup_sequence, signed_up_at, form_version, source) VALUES ({participantId}, {eventId}, {"Legacy captain"}, {"LEGACY CAPTAIN"}, {1m}, {true}, {"NotRequired"}, {"Confirmed"}, {1L}, {created}, {0}, {"AdminCreated"});");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO team_memberships (id, team_id, event_participant_id, role, joined_at, left_at, assigned_by_draft_pick_id, assignment_reason) VALUES ({Guid.NewGuid()}, {teamId}, {participantId}, {"Captain"}, {created}, NULL, NULL, NULL);");

            var preflight = new Slice1MigrationPreflight(legacy);
            var report = await preflight.RunAsync("legacy-admin", CancellationToken.None);
            Assert.Contains(adminId.ToString(), report, StringComparison.Ordinal);
            Assert.Contains(captainId.ToString(), report, StringComparison.Ordinal);
            await migrator.MigrateAsync();
        }

        await using (var migrated = new ApplicationDbContext(options))
        {
            var admin = await migrated.Accounts.SingleAsync(account => account.Id == adminId);
            var captain = await migrated.Accounts.SingleAsync(account => account.Id == captainId);
            var access = await migrated.AccountEventAccesses.SingleAsync(item => item.AccountId == captainId);
            Assert.Equal("legacy-admin-hash", admin.PasswordHash);
            Assert.Equal(GlobalRole.Admin, admin.GlobalRole);
            Assert.Equal(AccountType.EmergencyCaptain, captain.AccountType);
            Assert.False(captain.Active);
            Assert.Equal(eventId, access.EventId);
            Assert.Equal(teamId, access.TeamId);
            Assert.False(access.Enabled);
            Assert.Equal(adminId, await migrated.AuditEntries.Select(entry => entry.ActorAccountId).SingleAsync());

            var cutoff = created.AddHours(2);
            captain.Enable(); access.Enable();
            await migrated.SaveChangesAsync();
            var clock = new FixedTimeProvider(cutoff.AddTicks(-1));
            var lifecycle = new EmergencyCredentialLifecycleService(migrated, clock);
            await lifecycle.ApplyAsync(CancellationToken.None);
            Assert.True((await migrated.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
            clock.Set(cutoff);
            await lifecycle.ApplyAsync(CancellationToken.None);
            migrated.ChangeTracker.Clear();
            Assert.False((await migrated.Accounts.SingleAsync(item => item.Id == captainId)).Active);
            Assert.False((await migrated.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
            Assert.Single(await migrated.AuditEntries.Where(item => item.Action == "account.emergency_cutoff_disabled" && item.TargetId == captainId.ToString()).ToListAsync());
            await lifecycle.ApplyAsync(CancellationToken.None);
            Assert.Single(await migrated.AuditEntries.Where(item => item.Action == "account.emergency_cutoff_disabled" && item.TargetId == captainId.ToString()).ToListAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => new AccountAdministrationService(migrated, new PasswordHasher<Account>(), clock).SetEmergencyEnabledAsync(adminId, captainId, true, CancellationToken.None));
            await lifecycle.ApplyAsync(CancellationToken.None);
            migrated.ChangeTracker.Clear();
            Assert.False((await migrated.Accounts.SingleAsync(item => item.Id == captainId)).Active);
            Assert.False((await migrated.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
        }

        await using (var clean = new ApplicationDbContext(options))
        {
            await clean.Database.EnsureDeletedAsync();
            await clean.Database.MigrateAsync();
            var catalogue = new CatalogueSnapshotService(clean, TimeProvider.System);
            var snapshot = await catalogue.ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));
            var recovery = new OperatorRecoveryService(clean, TimeProvider.System, new PasswordHasher<Account>());
            await recovery.BootstrapOwnerAsync("production-owner", "long-production-password", "production-owner", CancellationToken.None);

            var owner = await clean.Accounts.SingleAsync();
            Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
            Assert.Equal(68, snapshot.Bosses);
            Assert.Equal(1, await clean.Accounts.CountAsync());
            Assert.Empty(await clean.Events.ToListAsync());
            Assert.Contains(await clean.AuditEntries.ToListAsync(), entry => entry.Action == "account.owner_bootstrapped");
        }
    }

    [Theory]
    [InlineData(false, "authoritative active captain/co-captain membership")]
    [InlineData(true, "selected retained owner must be enabled")]
    public async Task RetainedPreflightRejectsInconsistentScopeOrDisabledSelectedOwner(bool disabledOwner, string expectedMessage)
    {
        await using var legacy = new ApplicationDbContext(options);
        await legacy.Database.EnsureDeletedAsync();
        await legacy.GetService<IMigrator>().MigrateAsync(LegacyMigration);
        var created = DateTimeOffset.UtcNow;
        var ownerId = Guid.NewGuid();
        await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO accounts (id, username, normalized_username, password_hash, role, event_id, team_id, captain_participant_id, active_from, correction_only_from, expires_at, disabled_at, created_at, last_login_at, must_change_password) VALUES ({ownerId}, {"retained-owner"}, {"RETAINED-OWNER"}, {"hash"}, {"Admin"}, NULL, NULL, NULL, NULL, NULL, NULL, {(disabledOwner ? created : (DateTimeOffset?)null)}, {created}, NULL, FALSE)");
        if (!disabledOwner)
        {
            var accountEventId = Guid.NewGuid(); var membershipEventId = Guid.NewGuid(); var teamId = Guid.NewGuid(); var participantId = Guid.NewGuid();
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, allow_private_signup_editing, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at) VALUES ({accountEventId}, {"Account event"}, {"account-event"}, {""}, {"UTC"}, {"Draft"}, {created}, {created}, {created}, {created.AddDays(1)}, {created.AddHours(2)}, {10}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {ownerId}, {created}), ({membershipEventId}, {"Membership event"}, {"membership-event"}, {""}, {"UTC"}, {"Draft"}, {created}, {created}, {created}, {created.AddDays(1)}, {created.AddHours(2)}, {10}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {ownerId}, {created})");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO teams (id, event_id, name, slug, formation_type, included_in_draft, active) VALUES ({teamId}, {membershipEventId}, {"Membership team"}, {"membership-team"}, {"Preformed"}, {false}, {true})");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO event_participants (id, event_id, primary_account_name, normalized_primary_account_name, ehb_snapshot, captain_volunteer, payment_status, signup_status, signup_sequence, signed_up_at, form_version, source) VALUES ({participantId}, {membershipEventId}, {"Cross event captain"}, {"CROSS EVENT CAPTAIN"}, {1m}, {true}, {"NotRequired"}, {"Confirmed"}, {1L}, {created}, {0}, {"AdminCreated"})");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO team_memberships (id, team_id, event_participant_id, role, joined_at, left_at, assigned_by_draft_pick_id, assignment_reason) VALUES ({Guid.NewGuid()}, {teamId}, {participantId}, {"Captain"}, {created}, NULL, NULL, NULL)");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO accounts (id, username, normalized_username, password_hash, role, event_id, team_id, captain_participant_id, active_from, correction_only_from, expires_at, disabled_at, created_at, last_login_at, must_change_password) VALUES ({Guid.NewGuid()}, {"inconsistent-captain"}, {"INCONSISTENT-CAPTAIN"}, {"hash"}, {"Captain"}, {accountEventId}, {teamId}, {participantId}, NULL, NULL, NULL, NULL, {created}, NULL, FALSE)");
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new Slice1MigrationPreflight(legacy).RunAsync("retained-owner", CancellationToken.None));
        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

file sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    private DateTimeOffset current = value;
    public override DateTimeOffset GetUtcNow() => current;
    public void Set(DateTimeOffset value) => current = value;
}
