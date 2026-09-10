using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
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
        const string legacyDiscord = "RETAINED-LEGACY-DISCORD-SENTINEL";
        const string retainedAdminPassword = "retained-admin-password";
        string retainedAdminLogin;
        string retainedSlug;

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
                VALUES ({linkId}, {accountId}, {linkedCharacterId}, TRUE, FALSE, 4, {created}, {created.AddHours(1)});
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
                     second_account_name, discord_identity, ehb_snapshot, captain_volunteer, payment_status,
                     signup_status, signup_sequence, signed_up_at, form_version, source)
                VALUES
                    ({participantId}, {eventId}, {"Existing Main"}, {"EXISTING MAIN"},
                     {"Helper Alt"}, {legacyDiscord}, 123.45, FALSE, {"Unknown"}, {"Confirmed"}, 1, {created}, 1, {"CsvImport"});
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
            Assert.Equal(0, link.SortOrder);
            Assert.Equal(created.AddTicks(-(created.Ticks % TimeSpan.TicksPerMicrosecond)), link.LinkedAt);
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
            var legacyQuestion = await migrated.SignupQuestions.SingleAsync(x => x.EventId == eventId && x.Key == "legacy_discord_identity");
            Assert.False(legacyQuestion.PublicOnSignupBoard);
            Assert.Equal(SignupSystemField.None, legacyQuestion.SystemField);
            Assert.Equal("Discord identity", legacyQuestion.Label);
            Assert.Equal("Retained historical signup value.", legacyQuestion.HelpText);
            Assert.Equal(9991, legacyQuestion.Position);
            var legacyAnswer = await migrated.SignupAnswers.SingleAsync(x => x.EventParticipantId == participantId && x.SignupQuestionId == legacyQuestion.Id);
            Assert.Equal(legacyDiscord, legacyAnswer.Value);
            Assert.Equal(participantId, legacyAnswer.EventParticipantId);
            Assert.Equal(legacyQuestion.Id, legacyAnswer.SignupQuestionId);

            var questions = await migrated.SignupQuestions
                .Where(x => x.EventId == eventId && x.Active)
                .OrderBy(x => x.Position)
                .ToListAsync();
            Assert.Collection(questions,
                primary =>
                {
                    Assert.Equal("primary_regular_account", primary.Key);
                    Assert.Equal(SignupQuestionType.Account, primary.Type);
                    Assert.True(primary.Required);
                    Assert.Equal(SignupSystemField.PrimaryRegularAccount, primary.SystemField);
                    Assert.Equal(EventCharacterRole.Playing, primary.AccountAnswerRole);
                    Assert.Equal(0, primary.Position);
                },
                captain =>
                {
                    Assert.Equal("captain_volunteer", captain.Key);
                    Assert.Equal(SignupQuestionType.YesNo, captain.Type);
                    Assert.False(captain.Required);
                    Assert.Equal(SignupSystemField.CaptainVolunteer, captain.SystemField);
                    Assert.Null(captain.AccountAnswerRole);
                    Assert.Equal(1, captain.Position);
                },
                coCaptain =>
                {
                    Assert.Equal(SignupQuestion.CoCaptainKey, coCaptain.Key);
                    Assert.Equal(SignupQuestion.CoCaptainLabel, coCaptain.Label);
                    Assert.Equal(SignupQuestionType.Text, coCaptain.Type);
                    Assert.False(coCaptain.Required);
                    Assert.Equal(SignupSystemField.CoCaptainName, coCaptain.SystemField);
                    Assert.Null(coCaptain.HelpText);
                    Assert.Null(coCaptain.Options);
                    Assert.Null(coCaptain.AccountAnswerRole);
                    Assert.True(coCaptain.PublicOnSignupBoard);
                    Assert.True(coCaptain.Position > 1);
                    Assert.Equal(legacyQuestion.Position + 1, coCaptain.Position);
                });
            var coCaptainQuestion = Assert.Single(questions, question => question.SystemField == SignupSystemField.CoCaptainName);
            Assert.False(await migrated.SignupAnswers.AnyAsync(answer => answer.EventParticipantId == participantId && answer.SignupQuestionId == coCaptainQuestion.Id));
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DiscordAuthentication:ClientId"] = "test",
                ["DiscordAuthentication:ClientSecret"] = "test"
            }).Build();
            var readiness = await new EventReadinessEvaluator(migrated, configuration)
                .GetSignupReadinessAsync(eventId, SignupOpeningMode.OpenNow, DateTimeOffset.UtcNow);
            Assert.DoesNotContain(readiness!.Blockers, blocker => blocker.Code == "SIGNUP_QUESTIONS_INVALID");

            var retainedAdmin = await migrated.Accounts.SingleAsync(x => x.Id == accountId);
            retainedAdmin.SetGlobalRole(GlobalRole.Admin);
            retainedAdmin.SetPassword(new PasswordHasher<Account>().HashPassword(retainedAdmin, retainedAdminPassword), false, DateTimeOffset.UtcNow, incrementVersion: false);
            var retainedEvent = await migrated.Events.SingleAsync(x => x.Id == eventId);
            retainedEvent.MarkFirstPublic(DateTimeOffset.UtcNow);
            retainedEvent.OpenSignups(DateTimeOffset.UtcNow);
            await migrated.SaveChangesAsync();
            retainedAdminLogin = retainedAdmin.LoginName;
            retainedSlug = retainedEvent.Slug;
        }

        await using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString())))
        {
            using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var login = await admin.GetStringAsync("/Account/Login");
            using (var signedIn = await admin.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = retainedAdminLogin,
                ["Input.Password"] = retainedAdminPassword,
                ["__RequestVerificationToken"] = AntiforgeryToken(login)
            }))) Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
            var detail = await admin.GetAsync($"/Admin/Events/Participant/{eventId}/Participants/{participantId}");
            var detailHtml = await detail.Content.ReadAsStringAsync();
            Assert.True(detail.StatusCode == HttpStatusCode.OK, detailHtml);
            Assert.Contains(legacyDiscord, detailHtml, StringComparison.Ordinal);

            using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var publicTable = await anonymous.GetAsync($"/Events/{retainedSlug}/Signups");
            Assert.Equal(HttpStatusCode.OK, publicTable.StatusCode);
            Assert.DoesNotContain(legacyDiscord, await publicTable.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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

    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    [Fact]
    public async Task RetainedCollisionMigrationReleasesHistoryBeforeCreatingCurrentReservationIndex()
    {
        await using var retained = new ApplicationDbContext(options);
        await retained.Database.EnsureDeletedAsync();
        await retained.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var accountId = Guid.NewGuid();
        var withdrawnReuseEvent = Guid.NewGuid();
        var removedReuseEvent = Guid.NewGuid();
        var primaryWinsEvent = Guid.NewGuid();
        var informationalOnlyEvent = Guid.NewGuid();
        var withdrawn = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var reuse = Guid.NewGuid();
        var primary = Guid.NewGuid();
        var informational = Guid.NewGuid();
        var informationalFirst = Guid.NewGuid();
        var informationalSecond = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.AddDays(-3);

        await retained.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO accounts (id, password_hash, disabled_at, created_at, last_login_at, must_change_password, account_type, active, authorization_version, login_name, normalized_login_name, password_changed_at, onboarding_completed_at, global_role, public_username, normalized_public_username, password_version, version)
            VALUES ({accountId}, {"hash"}, NULL, {now}, NULL, FALSE, {"WebsiteAccount"}, TRUE, 1, {"migration-owner"}, {"MIGRATION-OWNER"}, {now}, {now}, {"User"}, {"migration-owner"}, {"MIGRATION-OWNER"}, 1, 1);
            INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, allow_private_signup_editing, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at)
            VALUES
                ({withdrawnReuseEvent}, {"withdrawn-reuse"}, {"withdrawn-reuse"}, {""}, {"UTC"}, {"Draft"}, {now}, {now.AddHours(1)}, {now.AddDays(1)}, {now.AddDays(2)}, {now.AddDays(2)}, 10, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {accountId}, {now}),
                ({removedReuseEvent}, {"removed-reuse"}, {"removed-reuse"}, {""}, {"UTC"}, {"Draft"}, {now}, {now.AddHours(1)}, {now.AddDays(1)}, {now.AddDays(2)}, {now.AddDays(2)}, 10, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {accountId}, {now}),
                ({primaryWinsEvent}, {"primary-wins"}, {"primary-wins"}, {""}, {"UTC"}, {"Draft"}, {now}, {now.AddHours(1)}, {now.AddDays(1)}, {now.AddDays(2)}, {now.AddDays(2)}, 10, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {accountId}, {now}),
                ({informationalOnlyEvent}, {"informational-only"}, {"informational-only"}, {""}, {"UTC"}, {"Draft"}, {now}, {now.AddHours(1)}, {now.AddDays(1)}, {now.AddDays(2)}, {now.AddDays(2)}, 10, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {accountId}, {now});
            INSERT INTO event_participants (id, event_id, primary_account_name, normalized_primary_account_name, second_account_name, ehb_snapshot, captain_volunteer, payment_status, signup_status, signup_sequence, signed_up_at, withdrawn_at, form_version, source)
            VALUES
                ({withdrawn}, {withdrawnReuseEvent}, {"Reuse"}, {"REUSE"}, NULL, 10, FALSE, {"Unknown"}, {"Withdrawn"}, 1, {now}, {now.AddHours(1)}, 1, {"Website"}),
                ({reuse}, {withdrawnReuseEvent}, {"Reuse"}, {"REUSE"}, NULL, 20, FALSE, {"Unknown"}, {"Confirmed"}, 2, {now.AddMinutes(1)}, NULL, 1, {"Website"}),
                ({removed}, {removedReuseEvent}, {"Removed Reuse"}, {"REMOVED REUSE"}, NULL, 25, FALSE, {"Unknown"}, {"Removed"}, 1, {now}, NULL, 1, {"Website"}),
                ({primary}, {primaryWinsEvent}, {"Shared"}, {"SHARED"}, NULL, 30, FALSE, {"Unknown"}, {"Confirmed"}, 1, {now}, NULL, 1, {"Website"}),
                ({informational}, {primaryWinsEvent}, {"Other"}, {"OTHER"}, {"Shared"}, 40, FALSE, {"Unknown"}, {"Confirmed"}, 2, {now.AddMinutes(1)}, NULL, 1, {"Website"}),
                ({informationalFirst}, {informationalOnlyEvent}, {"First"}, {"FIRST"}, {"Info Shared"}, 50, FALSE, {"Unknown"}, {"Confirmed"}, 1, {now}, NULL, 1, {"Website"}),
                ({informationalSecond}, {informationalOnlyEvent}, {"Second"}, {"SECOND"}, {"Info Shared"}, 60, FALSE, {"Unknown"}, {"Confirmed"}, 2, {now.AddMinutes(1)}, NULL, 1, {"Website"});
            """);

        await retained.GetService<IMigrator>().MigrateAsync();

        var assignments = retained.EventParticipantCharacters.AsNoTracking();
        var reuseRows = await assignments.Where(x => x.EventId == withdrawnReuseEvent).ToListAsync();
        Assert.Single(reuseRows, x => x.EventParticipantId == reuse && x.ReleasedAt == null);
        Assert.Single(reuseRows, x => x.EventParticipantId == withdrawn && x.ReleasedAt != null);
        Assert.Single(await assignments.Where(x => x.EventId == removedReuseEvent).ToListAsync(), x => x.EventParticipantId == removed && x.ReleasedAt != null);
        var primaryRows = await assignments.Where(x => x.EventId == primaryWinsEvent).ToListAsync();
        Assert.Single(primaryRows, x => x.EventParticipantId == primary && x.EventRole == EventCharacterRole.Playing && x.ReleasedAt == null);
        Assert.Single(primaryRows, x => x.EventParticipantId == informational && x.EventRole == EventCharacterRole.Informational && x.ReleasedAt != null);
        var informationalRows = await assignments.Where(x => x.EventId == informationalOnlyEvent && x.EventRole == EventCharacterRole.Informational).OrderBy(x => x.RegistrationOrder).ToListAsync();
        Assert.Single(informationalRows, x => x.EventParticipantId == informationalFirst && x.ReleasedAt == null);
        Assert.Single(informationalRows, x => x.EventParticipantId == informationalSecond && x.ReleasedAt != null);
    }

    [Fact]
    public async Task RetainedMigrationFailsClosedForMultipleCurrentPlayingReservations()
    {
        await using var retained = new ApplicationDbContext(options);
        await retained.Database.EnsureDeletedAsync();
        await retained.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var accountId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var firstParticipantId = Guid.NewGuid();
        var secondParticipantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.AddDays(-3);

        await retained.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO accounts (id, password_hash, disabled_at, created_at, last_login_at, must_change_password, account_type, active, authorization_version, login_name, normalized_login_name, password_changed_at, onboarding_completed_at, global_role, public_username, normalized_public_username, password_version, version)
            VALUES ({accountId}, {"hash"}, NULL, {now}, NULL, FALSE, {"WebsiteAccount"}, TRUE, 1, {"collision-owner"}, {"COLLISION-OWNER"}, {now}, {now}, {"User"}, {"collision-owner"}, {"COLLISION-OWNER"}, 1, 1);
            INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, allow_private_signup_editing, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at)
            VALUES ({eventId}, {"playing-collision"}, {"playing-collision"}, {""}, {"UTC"}, {"Draft"}, {now}, {now.AddHours(1)}, {now.AddDays(1)}, {now.AddDays(2)}, {now.AddDays(2)}, 10, TRUE, TRUE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, {accountId}, {now});
            INSERT INTO event_participants (id, event_id, primary_account_name, normalized_primary_account_name, ehb_snapshot, captain_volunteer, payment_status, signup_status, signup_sequence, signed_up_at, form_version, source)
            VALUES
                ({firstParticipantId}, {eventId}, {"Contended"}, {"CONTENDED"}, 10, FALSE, {"Unknown"}, {"Confirmed"}, 1, {now}, 1, {"Website"}),
                ({secondParticipantId}, {eventId}, {"Contended"}, {"CONTENDED"}, 20, FALSE, {"Unknown"}, {"Confirmed"}, 2, {now.AddMinutes(1)}, 1, {"Website"});
            """);

        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => retained.GetService<IMigrator>().MigrateAsync());
        Assert.Contains("cannot resolve multiple active Playing", exception.MessageText, StringComparison.Ordinal);
    }
}
