using System.Security.Cryptography;
using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
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
    private const string PreviousSlice9Migration = "20260802002639_AddLiveWithdrawalReplacementPersistence";
    private const string FinalReviewMigration = "20260802005536_AddFinalReviewCyclesAndSnapshotInputs";
    private const string PreviousImmutableItemMigration = "20260831142836_AddEventQuarantine";
    private const string ImmutableItemMigration = "20260905221344_AddImmutableCatalogueItemIdentity";
    private static readonly JsonSerializerOptions MappingJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
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
    public async Task ManualObjectiveDropSnapshotsAreStructuralAndCannotBeMapped()
    {
        var seed = await SeedImmutableSnapshotRowsAsync(manual: true);
        await using var legacy = new ApplicationDbContext(options);
        var report = await new Slice1MigrationPreflight(legacy).RunImmutableItemPreflightAsync(CancellationToken.None);

        Assert.Contains("Structural errors: 2", report, StringComparison.Ordinal);
        Assert.Contains("correction/removal required before retry", report, StringComparison.Ordinal);
        Assert.Contains(seed.EventDropId.ToString(), report, StringComparison.Ordinal);
        Assert.Contains(seed.ApprovalDropId.ToString(), report, StringComparison.Ordinal);
        var template = report[report.IndexOf("Mapping template", StringComparison.Ordinal)..];
        Assert.DoesNotContain(seed.EventDropId.ToString(), template, StringComparison.Ordinal);
        Assert.DoesNotContain(seed.ApprovalDropId.ToString(), template, StringComparison.Ordinal);

        await legacy.Database.OpenConnectionAsync();
        try
        {
            var stagingException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Slice1MigrationPreflight.StageImmutableItemMappingsAsync(
                    legacy.Database.GetDbConnection(), "supplied-mapping.json", "known-hash", CancellationToken.None));
            Assert.Contains("manual objectives are forbidden", stagingException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(await TempMappingExistsAsync(legacy));

            await legacy.Database.ExecuteSqlRawAsync("CREATE TEMP TABLE bingo_item_snapshot_mapping (snapshot_family text NOT NULL, snapshot_id uuid NOT NULL, item_id uuid NOT NULL, PRIMARY KEY (snapshot_family, snapshot_id));");
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO bingo_item_snapshot_mapping (snapshot_family, snapshot_id, item_id)
                VALUES ('event', {seed.EventDropId}, {seed.ItemId}), ('approval', {seed.ApprovalDropId}, {seed.ItemId});
                """);
            var migrationException = await Record.ExceptionAsync(() => legacy.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync());
            Assert.NotNull(migrationException);
            Assert.Contains("manual objectives", migrationException.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await legacy.Database.SqlQuery<int>($"SELECT count(*) AS \"Value\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = {ImmutableItemMigration}").SingleAsync());
            Assert.False(await ColumnExistsAsync(legacy, "board_requirement_drop_snapshots", "item_id_snapshot"));
        }
        finally
        {
            await legacy.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task LegitimateAmbiguousCatalogueRowsStillUseTheAdjudicatedMappingWorkflow()
    {
        var seed = await SeedImmutableSnapshotRowsAsync(manual: false);
        await using var legacy = new ApplicationDbContext(options);
        var report = await new Slice1MigrationPreflight(legacy).RunImmutableItemPreflightAsync(CancellationToken.None);
        Assert.Contains("Structural errors: none.", report, StringComparison.Ordinal);
        Assert.Contains("flagged: 2", report, StringComparison.Ordinal);
        Assert.Contains(seed.AlternateItemId.ToString(), report, StringComparison.Ordinal);

        var mapping = new Slice1MigrationPreflight.ImmutableItemMappingFile
        {
            DatabaseFingerprint = report[..report.IndexOf(Environment.NewLine, StringComparison.Ordinal)]["Database fingerprint: ".Length..],
            Rows =
            [
                new() { SnapshotFamily = "event", SnapshotId = seed.EventDropId, ItemId = seed.ItemId },
                new() { SnapshotFamily = "approval", SnapshotId = seed.ApprovalDropId, ItemId = seed.ItemId }
            ]
        };
        var mappingBytes = JsonSerializer.SerializeToUtf8Bytes(mapping, MappingJsonOptions);
        var mappingPath = Path.Combine(Path.GetTempPath(), $"immutable-item-mapping-{Guid.NewGuid():N}.json");
        await File.WriteAllBytesAsync(mappingPath, mappingBytes);
        try
        {
            await legacy.Database.OpenConnectionAsync();
            await Slice1MigrationPreflight.StageImmutableItemMappingsAsync(
                legacy.Database.GetDbConnection(), mappingPath,
                Convert.ToHexString(SHA256.HashData(mappingBytes)).ToLowerInvariant(), CancellationToken.None);
            Assert.True(await TempMappingExistsAsync(legacy));
            await legacy.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync();

            Assert.Equal(seed.ItemId, await legacy.Database.SqlQuery<Guid>($"SELECT item_id_snapshot AS \"Value\" FROM board_requirement_drop_snapshots WHERE id = {seed.EventDropId}").SingleAsync());
            Assert.Equal(seed.ItemId, await legacy.Database.SqlQuery<Guid>($"SELECT item_id_snapshot AS \"Value\" FROM board_approval_requirement_drop_snapshots WHERE id = {seed.ApprovalDropId}").SingleAsync());
            Assert.False(await TempMappingExistsAsync(legacy));
        }
        finally
        {
            await legacy.Database.CloseConnectionAsync();
            File.Delete(mappingPath);
        }
    }

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
            var persistedCutoff = cutoff.AddTicks(-(cutoff.Ticks % TimeSpan.TicksPerMicrosecond));
            captain.Enable(); access.Enable();
            await migrated.SaveChangesAsync();
            var clock = new FixedTimeProvider(persistedCutoff.AddMicroseconds(-1));
            var lifecycle = new EmergencyCredentialLifecycleService(migrated, clock);
            await lifecycle.ApplyAsync(CancellationToken.None);
            Assert.True((await migrated.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
            clock.Set(persistedCutoff);
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

    [Fact]
    public async Task RetainedOfficialSnapshotMapsToItsOnlyAwaitingFinalReviewTransition()
    {
        var seed = await SeedRetainedOfficialSnapshotAsync(awaitingTransitionCount: 1);

        await using (var migrated = new ApplicationDbContext(options))
        {
            await migrated.GetService<IMigrator>().MigrateAsync(FinalReviewMigration);
        }

        await using var verified = new ApplicationDbContext(options);
        var snapshot = await verified.EventFinalizations.SingleAsync(item => item.Id == seed.SnapshotId);
        Assert.Equal(seed.TransitionIds.Single(), snapshot.ReviewCycleId);
        Assert.Equal(1, await verified.EventStateTransitions.CountAsync(item => item.EventId == seed.EventId && item.ToState == EventState.AwaitingFinalReview));
        Assert.Contains(FinalReviewMigration, await verified.Database.SqlQueryRaw<string>("SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\"").ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task AmbiguousRetainedOfficialSnapshotFailsClosedWithoutPartialUpgrade(int awaitingTransitionCount)
    {
        var seed = await SeedRetainedOfficialSnapshotAsync(awaitingTransitionCount);

        await using var migrated = new ApplicationDbContext(options);
        var exception = await Record.ExceptionAsync(() => migrated.GetService<IMigrator>().MigrateAsync(FinalReviewMigration));

        Assert.NotNull(exception);
        var message = exception.ToString();
        Assert.Contains(seed.EventId.ToString(), message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(seed.SnapshotId.ToString(), message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await migrated.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260802005536_AddFinalReviewCyclesAndSnapshotInputs'").SingleAsync());
        Assert.Equal(0, await migrated.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'event_finalizations' AND column_name = 'review_cycle_id'").SingleAsync());
        Assert.Equal(1, await migrated.Database.SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM event_finalizations WHERE id = {seed.SnapshotId}").SingleAsync());
    }

    private async Task<RetainedOfficialSnapshotSeed> SeedRetainedOfficialSnapshotAsync(int awaitingTransitionCount)
    {
        await using var legacy = new ApplicationDbContext(options);
        await legacy.Database.EnsureDeletedAsync();
        var migrator = legacy.GetService<IMigrator>();
        await migrator.MigrateAsync(LegacyMigration);

        var now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        await legacy.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO accounts (id, username, normalized_username, password_hash, role, event_id, team_id, captain_participant_id, active_from, correction_only_from, expires_at, disabled_at, created_at, last_login_at, must_change_password)
            VALUES ({adminId}, {"retained-final-review-admin"}, {"RETAINED-FINAL-REVIEW-ADMIN"}, {"hash"}, {"Admin"}, NULL, NULL, NULL, NULL, NULL, NULL, NULL, {now}, NULL, FALSE);
            INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, allow_private_signup_editing, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at)
            VALUES ({eventId}, {"Retained final-review event"}, {"retained-final-review-{eventId:N}"}, {""}, {"UTC"}, {"AwaitingFinalReview"}, {now.AddDays(-3)}, {now.AddDays(-2)}, {now.AddDays(-1)}, {now.AddHours(-1)}, {now.AddMinutes(-30)}, {10}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {true}, {adminId}, {now.AddDays(-4)});
            INSERT INTO event_finalizations (id, event_id, version, finalized_at, finalized_by_account_id, unfinalized_at, unfinalized_by_account_id, unfinalize_reason)
            VALUES ({snapshotId}, {eventId}, {1}, {now.AddHours(-2)}, {adminId}, NULL, NULL, NULL);
            """);

        var transitionIds = new List<Guid>(awaitingTransitionCount);
        for (var index = 0; index < awaitingTransitionCount; index++)
        {
            var transitionId = Guid.NewGuid();
            transitionIds.Add(transitionId);
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO event_state_transitions (id, event_id, from_state, to_state, performed_by_account_id, performed_at, reason) VALUES ({transitionId}, {eventId}, {"Live"}, {"AwaitingFinalReview"}, {adminId}, {now.AddHours(-3).AddMinutes(index)}, {"retained transition"})");
        }

        await migrator.MigrateAsync(PreviousSlice9Migration);
        return new RetainedOfficialSnapshotSeed(eventId, snapshotId, transitionIds);
    }

    private async Task<ImmutableSnapshotSeed> SeedImmutableSnapshotRowsAsync(bool manual)
    {
        await using var databaseContext = new ApplicationDbContext(options);
        await databaseContext.Database.EnsureDeletedAsync();
        await databaseContext.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(PreviousImmutableItemMigration);

        var now = DateTimeOffset.UtcNow.AddDays(-1);
        var accountId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var tileId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var approvalTileId = Guid.NewGuid();
        var approvalRequirementId = Guid.NewGuid();
        var eventDropId = Guid.NewGuid();
        var approvalDropId = Guid.NewGuid();
        var sourceDropId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var alternateItemId = Guid.NewGuid();
        var itemName = manual ? "Manual item" : "Ambiguous item";

        await databaseContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO accounts (id, password_hash, must_change_password, account_type, active, authorization_version, login_name, normalized_login_name, global_role, public_username, normalized_public_username, password_version, version, created_at)
            VALUES ({accountId}, {"hash"}, FALSE, {"WebsiteAccount"}, TRUE, 1, {$"snapshot-owner-{eventId:N}"}, {$"SNAPSHOT-OWNER-{eventId:N}"}, {"Admin"}, {$"snapshot-owner-{eventId:N}"}, {$"SNAPSHOT-OWNER-{eventId:N}"}, 1, 1, {now});
            INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at)
            VALUES ({eventId}, {"Snapshot mapping event"}, {$"snapshot-mapping-{eventId:N}"}, {""}, {"UTC"}, {"Draft"}, {now}, {now}, {now}, {now.AddDays(1)}, {now.AddDays(1)}, 10, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {accountId}, {now});
            INSERT INTO boards (id, event_id, name, rows, columns, state, total_ehb_estimate, calculation_version, version)
            VALUES ({boardId}, {eventId}, {"Snapshot mapping board"}, 1, 1, {"Published"}, 1, 1, 1);
            INSERT INTO board_tiles (id, board_id, tile_template_id, row_index, column_index, name_snapshot, description_snapshot, evidence_instructions_snapshot, estimated_ehb_snapshot)
            VALUES ({tileId}, {boardId}, {Guid.NewGuid()}, 0, 0, {"Snapshot tile"}, {"Snapshot description"}, {"Snapshot evidence"}, 1);
            INSERT INTO board_requirement_snapshots (id, board_tile_id, position, target_contribution, duplicates_allowed, allow_higher_weightings, credited_weight, description, manual_objective)
            VALUES ({requirementId}, {tileId}, 0, 1, FALSE, FALSE, 1, {"Snapshot requirement"}, {manual});
            INSERT INTO board_requirement_drop_snapshots (id, requirement_id, source_drop_id, boss_name, item_name, display_rate, numeric_probability, maximum_contribution, ehb_per_contribution, credited_weight)
            VALUES ({eventDropId}, {requirementId}, {sourceDropId}, {"Snapshot boss"}, {itemName}, {"1/10"}, 0.1, 1, 1, 1);
            INSERT INTO board_approval_snapshots (id, board_id, version, approved_at, name, rows, columns, total_ehb_estimate, calculation_version, board_version, lifecycle_state)
            VALUES ({approvalId}, {boardId}, 1, {now}, {"Snapshot mapping board"}, 1, 1, 1, 1, 1, {"Published"});
            INSERT INTO board_approval_tile_snapshots (id, approval_snapshot_id, board_tile_id, tile_template_id, row_index, column_index, name, description, evidence_instructions, estimated_ehb)
            VALUES ({approvalTileId}, {approvalId}, {tileId}, {Guid.NewGuid()}, 0, 0, {"Snapshot tile"}, {"Snapshot description"}, {"Snapshot evidence"}, 1);
            INSERT INTO board_approval_requirement_snapshots (id, approval_tile_snapshot_id, board_requirement_snapshot_id, position, target_contribution, duplicates_allowed, allow_higher_weightings, credited_weight, description, manual_objective)
            VALUES ({approvalRequirementId}, {approvalTileId}, {requirementId}, 0, 1, FALSE, FALSE, 1, {"Snapshot requirement"}, {manual});
            INSERT INTO board_approval_requirement_drop_snapshots
                (id, approval_requirement_snapshot_id, source_drop_id, boss_name, item_name, display_rate, numeric_probability, probability_scope, conditional_on_parent, assumed_participants, rolls_per_completion, roll_group, maximum_contribution, credited_weight, catalogue_version)
            VALUES ({approvalDropId}, {approvalRequirementId}, {sourceDropId}, {"Snapshot boss"}, {itemName}, {"1/10"}, 0.1, {"Participant"}, FALSE, 1, 1, {"default"}, 1, 1, 1);
            INSERT INTO catalogue_items (id, name, normalized_name, active)
            VALUES ({itemId}, {itemName}, {$"{itemName.ToUpperInvariant()}-A"}, TRUE),
                   ({alternateItemId}, {itemName}, {$"{itemName.ToUpperInvariant()}-B"}, TRUE);
            """);

        return new ImmutableSnapshotSeed(eventId, eventDropId, approvalDropId, itemId, alternateItemId);
    }

    private static async Task<bool> TempMappingExistsAsync(ApplicationDbContext db) =>
        await db.Database.SqlQuery<bool>($"SELECT to_regclass('pg_temp.bingo_item_snapshot_mapping') IS NOT NULL AS \"Value\"").SingleAsync();

    private static async Task<bool> ColumnExistsAsync(ApplicationDbContext db, string table, string column) =>
        await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = {table} AND column_name = {column}) AS \"Value\"").SingleAsync();

    private sealed record RetainedOfficialSnapshotSeed(Guid EventId, Guid SnapshotId, IReadOnlyList<Guid> TransitionIds);
    private sealed record ImmutableSnapshotSeed(Guid EventId, Guid EventDropId, Guid ApprovalDropId, Guid ItemId, Guid AlternateItemId);
}

file sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    private DateTimeOffset current = value;
    public override DateTimeOffset GetUtcNow() => current;
    public void Set(DateTimeOffset value) => current = value;
}
