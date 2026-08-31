using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class EventQuarantineIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_event_quarantine")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly DateTimeOffset now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task OnlyActiveSuperAdminCanQuarantineAndStaleRestoreIsRejectedWithoutChangingHistory()
    {
        var superAdmin = Account.CreateWebsite(Guid.NewGuid(), "quarantine-super", "QUARANTINE-SUPER", now);
        superAdmin.SetGlobalRole(GlobalRole.SuperAdmin);
        var ordinaryAdmin = Account.CreateWebsite(Guid.NewGuid(), "quarantine-admin", "QUARANTINE-ADMIN", now);
        ordinaryAdmin.SetGlobalRole(GlobalRole.Admin);
        var item = ReadyForFinalReview(superAdmin.Id);
        var linkedNotificationId = Guid.NewGuid();
        var globalNotificationId = Guid.NewGuid();
        var linkedNotification = new PersonalNotification(linkedNotificationId, superAdmin.Id, "event-linked", "retained", $"/Admin/Events/Manage/{item.Id}", now, item.Id);
        var globalNotification = new PersonalNotification(globalNotificationId, superAdmin.Id, "global", "retained", "/Account", now);

        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(superAdmin, ordinaryAdmin, item, linkedNotification, globalNotification);
            await setup.SaveChangesAsync();
        }

        await using (var unauthorizedDb = new ApplicationDbContext(options))
        {
            var unauthorized = new EventQuarantineService(unauthorizedDb, new FixedClock(now));
            var result = await unauthorized.HideAsync(item.Id, item.Version, item.Name, "unauthorized", new LifecycleActor(ordinaryAdmin.Id, ordinaryAdmin.LoginName));
            Assert.False(result.Succeeded);
        }

        long hiddenVersion;
        await using (var mutationDb = new ApplicationDbContext(options))
        {
            var notifier = new RecordingAdminCollaborationNotifier();
            var mutation = new EventQuarantineService(mutationDb, new FixedClock(now), notifier);
            var result = await mutation.HideAsync(item.Id, item.Version, item.Name, "post-live retention", new LifecycleActor(superAdmin.Id, superAdmin.LoginName));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(1, notifier.EventsControlChanges);

            var hidden = await mutationDb.Events.SingleAsync(x => x.Id == item.Id);
            hiddenVersion = hidden.Version;
            Assert.Equal(EventState.AwaitingFinalReview, hidden.State);
            Assert.Equal(item.ActualEndedAt, hidden.ActualEndedAt);
            Assert.True(hidden.IsHidden);
            Assert.Single(await mutationDb.AuditEntries.Where(x => x.EventId == item.Id && x.Action == "event.hidden").ToListAsync());
            var inbox = await mutationDb.PersonalNotifications
                .Where(x => x.RecipientAccountId == superAdmin.Id && (x.EventId == null || mutationDb.Events.Any(eventItem => eventItem.Id == x.EventId && eventItem.HiddenAt == null)))
                .Select(x => x.Id)
                .ToListAsync();
            Assert.DoesNotContain(linkedNotificationId, inbox);
            Assert.Contains(globalNotificationId, inbox);
            Assert.Null(await mutationDb.PersonalNotifications.Where(x => x.Id == linkedNotificationId).Select(x => x.ReadAt).SingleAsync());
        }

        await using (var staleDb = new ApplicationDbContext(options))
        {
            var stale = new EventQuarantineService(staleDb, new FixedClock(now.AddMinutes(1)));
            var result = await stale.RestoreAsync(item.Id, item.Version, item.Name, "stale restore", new LifecycleActor(superAdmin.Id, superAdmin.LoginName));
            Assert.False(result.Succeeded);
            Assert.True(await staleDb.Events.Where(x => x.Id == item.Id).Select(x => x.IsHidden).SingleAsync());
        }

        await using (var restoreDb = new ApplicationDbContext(options))
        {
            var restore = new EventQuarantineService(restoreDb, new FixedClock(now.AddMinutes(2)));
            var result = await restore.RestoreAsync(item.Id, hiddenVersion, item.Name, "approved restore", new LifecycleActor(superAdmin.Id, superAdmin.LoginName));
            Assert.True(result.Succeeded, result.Error);

            var restored = await restoreDb.Events.SingleAsync(x => x.Id == item.Id);
            Assert.False(restored.IsHidden);
            Assert.Equal(EventState.AwaitingFinalReview, restored.State);
            Assert.Equal(item.ActualEndedAt, restored.ActualEndedAt);
            Assert.Equal(1, await restoreDb.AuditEntries.CountAsync(x => x.EventId == item.Id && x.Action == "event.hidden"));
            Assert.Equal(1, await restoreDb.AuditEntries.CountAsync(x => x.EventId == item.Id && x.Action == "event.restored"));
        }
    }

    [Fact]
    public void ModelAddsQuarantineFieldsAssociationAndDatabaseConstraint()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=unused").Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var eventType = model.FindEntityType(typeof(BingoEvent))!;
        var notificationType = model.FindEntityType(typeof(PersonalNotification))!;

        Assert.NotNull(eventType.FindProperty(nameof(BingoEvent.HiddenAt)));
        Assert.NotNull(eventType.FindProperty(nameof(BingoEvent.HiddenByAccountId)));
        Assert.NotNull(eventType.FindProperty(nameof(BingoEvent.HiddenReason)));
        Assert.Contains(eventType.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(BingoEvent.HiddenAt)]));
        Assert.Contains(eventType.GetCheckConstraints(), constraint => constraint.Name == "ck_events_hidden_metadata");
        Assert.NotNull(notificationType.FindProperty(nameof(PersonalNotification.EventId)));
        Assert.Contains(notificationType.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([
            nameof(PersonalNotification.EventId),
            nameof(PersonalNotification.RecipientAccountId),
            nameof(PersonalNotification.CreatedAt)]));
    }

    [Fact]
    public async Task FinalizedManageHideUsesExactHandlerBoundaryAndValidatesBeforeMutation()
    {
        var superAdmin = Account.CreateWebsite(Guid.NewGuid(), "filter-quarantine-super", "FILTER-QUARANTINE-SUPER", now);
        superAdmin.SetGlobalRole(GlobalRole.SuperAdmin);
        superAdmin.SetPassword(new PasswordHasher<Account>().HashPassword(superAdmin, "filter-quarantine-password"), false, now, incrementVersion: false);
        var ordinaryAdmin = Account.CreateWebsite(Guid.NewGuid(), "filter-quarantine-admin", "FILTER-QUARANTINE-ADMIN", now);
        ordinaryAdmin.SetGlobalRole(GlobalRole.Admin);
        ordinaryAdmin.SetPassword(new PasswordHasher<Account>().HashPassword(ordinaryAdmin, "filter-quarantine-password"), false, now, incrementVersion: false);
        var item = ReadyForFinalReview(superAdmin.Id);
        item.FinalizeResults(now);
        var retainedActualEndedAt = item.ActualEndedAt;
        var retainedFinalizedAt = item.FinalizedAt;

        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(superAdmin, ordinaryAdmin, item);
            await setup.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var superClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var ordinaryClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(superClient, superAdmin.PublicUsername!, "filter-quarantine-password");
        await LoginAsync(ordinaryClient, ordinaryAdmin.PublicUsername!, "filter-quarantine-password");

        var path = $"/Admin/Events/Manage/{item.Id}";
        var manage = await superClient.GetStringAsync(path);
        Assert.Contains("?handler=Hide", manage, StringComparison.Ordinal);
        var eventVersion = InputValue(manage, "EventVersion");
        var confirmationToken = AntiforgeryToken(manage);

        using (var wrongHandlerResponse = await superClient.PostAsync($"{path}?handler=HideTypo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = eventVersion,
            ["EventNameConfirmation"] = item.Name,
            ["QuarantineReason"] = "Should not reach a handler",
            ["__RequestVerificationToken"] = confirmationToken
        })))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, wrongHandlerResponse.StatusCode);
        }
        await AssertUnchangedAsync(item.Id, EventState.Finalized);

        using (var blankReasonResponse = await superClient.PostAsync($"{path}?handler=Hide", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = eventVersion,
            ["EventNameConfirmation"] = item.Name,
            ["QuarantineReason"] = "",
            ["__RequestVerificationToken"] = confirmationToken
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, blankReasonResponse.StatusCode);
            Assert.Equal(path, blankReasonResponse.Headers.Location!.OriginalString);
        }
        var blankReasonPage = await superClient.GetStringAsync(path);
        Assert.Contains("An exact event-name confirmation and reason are required.", blankReasonPage, StringComparison.Ordinal);
        Assert.DoesNotContain("This event is read-only in its current lifecycle state.", blankReasonPage, StringComparison.Ordinal);
        await AssertUnchangedAsync(item.Id, EventState.Finalized);

        var ordinaryManage = await ordinaryClient.GetStringAsync(path);
        Assert.DoesNotContain("?handler=Hide", ordinaryManage, StringComparison.Ordinal);
        using (var ordinaryResponse = await ordinaryClient.PostAsync($"{path}?handler=Hide", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = eventVersion,
            ["EventNameConfirmation"] = item.Name,
            ["QuarantineReason"] = "Ordinary admin attempt",
            ["__RequestVerificationToken"] = AntiforgeryToken(ordinaryManage)
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, ordinaryResponse.StatusCode);
            Assert.Equal(path, ordinaryResponse.Headers.Location!.OriginalString);
        }
        var ordinaryResultPage = await ordinaryClient.GetStringAsync(path);
        Assert.Contains("Only a SuperAdmin can hide or restore an event.", ordinaryResultPage, StringComparison.Ordinal);
        await AssertUnchangedAsync(item.Id, EventState.Finalized);

        using (var validResponse = await superClient.PostAsync($"{path}?handler=Hide", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = eventVersion,
            ["EventNameConfirmation"] = item.Name,
            ["QuarantineReason"] = "Finalized event quarantine",
            ["__RequestVerificationToken"] = AntiforgeryToken(await superClient.GetStringAsync(path))
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, validResponse.StatusCode);
            Assert.Equal("/Admin/Events?filter=hidden", validResponse.Headers.Location!.OriginalString);
        }
        await using var verify = new ApplicationDbContext(options);
        var hidden = await verify.Events.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(EventState.Finalized, hidden.State);
        Assert.True(hidden.IsHidden);

        var superHiddenListing = await superClient.GetStringAsync("/Admin/Events?filter=hidden");
        Assert.Contains($"data-event-id=\"{item.Id}\"", superHiddenListing, StringComparison.Ordinal);
        Assert.Contains("data-event-hidden=\"true\"", superHiddenListing, StringComparison.Ordinal);
        var ordinaryHiddenListing = await ordinaryClient.GetStringAsync("/Admin/Events?filter=hidden");
        Assert.DoesNotContain($"data-event-id=\"{item.Id}\"", ordinaryHiddenListing, StringComparison.Ordinal);

        var hiddenPath = $"{path}?hidden=true";
        var hiddenManage = await superClient.GetStringAsync(hiddenPath);
        Assert.Contains("handler=RestoreHidden", hiddenManage, StringComparison.Ordinal);
        var hiddenVersion = InputValue(hiddenManage, "EventVersion");
        var hiddenToken = AntiforgeryToken(hiddenManage);

        using (var wrongRestoreResponse = await superClient.PostAsync($"{hiddenPath}&handler=RestoreTypo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = hiddenVersion,
            ["EventNameConfirmation"] = item.Name,
            ["QuarantineReason"] = "Should not reach a handler",
            ["__RequestVerificationToken"] = hiddenToken
        })))
        {
            Assert.Equal(HttpStatusCode.NotFound, wrongRestoreResponse.StatusCode);
        }
        await AssertHiddenAsync(item.Id);

        var hiddenRedirectPath = $"{path}?hidden=True";
        using (var blankRestoreResponse = await superClient.PostAsync($"{hiddenPath}&handler=RestoreHidden", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = hiddenVersion,
            ["EventNameConfirmation"] = item.Name,
            ["QuarantineReason"] = "",
            ["__RequestVerificationToken"] = hiddenToken
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, blankRestoreResponse.StatusCode);
            Assert.Equal(hiddenRedirectPath, blankRestoreResponse.Headers.Location!.OriginalString);
        }
        var blankRestorePage = await superClient.GetStringAsync(hiddenPath);
        Assert.Contains("An exact event-name confirmation and reason are required.", blankRestorePage, StringComparison.Ordinal);
        await AssertHiddenAsync(item.Id);

        using (var ordinaryRestoreResponse = await ordinaryClient.GetAsync(hiddenPath))
        {
            Assert.Equal(HttpStatusCode.NotFound, ordinaryRestoreResponse.StatusCode);
        }

        var restoreManage = await superClient.GetStringAsync(hiddenPath);
        using (var validRestoreResponse = await superClient.PostAsync($"{hiddenPath}&handler=RestoreHidden", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = InputValue(restoreManage, "EventVersion"),
            ["EventNameConfirmation"] = item.Name,
            ["QuarantineReason"] = "Restore finalized event",
            ["__RequestVerificationToken"] = AntiforgeryToken(restoreManage)
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, validRestoreResponse.StatusCode);
            Assert.Equal("/Admin/Events", validRestoreResponse.Headers.Location!.OriginalString);
        }
        await using var restoredDb = new ApplicationDbContext(options);
        var restored = await restoredDb.Events.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(EventState.Finalized, restored.State);
        Assert.False(restored.IsHidden);
        Assert.Equal(item.Name, restored.Name);
        Assert.Equal(retainedActualEndedAt, restored.ActualEndedAt);
        Assert.Equal(retainedFinalizedAt, restored.FinalizedAt);
        Assert.True(restored.ResultsPublished);
    }

    private async Task AssertHiddenAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        Assert.Equal(EventState.Finalized, item.State);
        Assert.True(item.IsHidden);
    }

    private async Task AssertUnchangedAsync(Guid eventId, EventState expectedState)
    {
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        Assert.Equal(expectedState, item.State);
        Assert.False(item.IsHidden);
    }

    private static async Task LoginAsync(HttpClient client, string username, string password)
    {
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = AntiforgeryToken(loginPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static string InputValue(string page, string name) => Regex.Match(page, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

    [Fact]
    public async Task HiddenEmergencyCredentialsCannotBeInspectedOrMutatedAndEventAuditsAreFiltered()
    {
        var superAdmin = Account.CreateWebsite(Guid.NewGuid(), "hidden-emergency-super", "HIDDEN-EMERGENCY-SUPER", now);
        superAdmin.SetGlobalRole(GlobalRole.SuperAdmin);
        var ordinaryAdmin = Account.CreateWebsite(Guid.NewGuid(), "hidden-emergency-admin", "HIDDEN-EMERGENCY-ADMIN", now);
        ordinaryAdmin.SetGlobalRole(GlobalRole.Admin);
        var item = ReadyForFinalReview(superAdmin.Id);
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "hidden-emergency-captain", "HIDDEN-EMERGENCY-CAPTAIN", now);
        var access = new AccountEventAccess(Guid.NewGuid(), emergency.Id, item.Id, Guid.NewGuid(), null, item.EventStartsAt, item.SubmissionCutoffAt, null);

        Guid linkAuditId;
        string link;
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(superAdmin, ordinaryAdmin, item, emergency, access);
            await setup.SaveChangesAsync();
            link = await new AccountIdentityService(setup, new PasswordHasher<Account>(), new FixedClock(now))
                .GenerateEmergencyCredentialLinkAsync(ordinaryAdmin.Id, emergency.Id, CancellationToken.None);
            linkAuditId = await setup.AuditEntries.Where(entry => entry.Action == "account.emergency_credential_link_created")
                .Select(entry => entry.Id).SingleAsync();
            Assert.Equal(item.Id, await setup.AuditEntries.Where(entry => entry.Id == linkAuditId).Select(entry => entry.EventId).SingleAsync());
        }

        await using (var hideDb = new ApplicationDbContext(options))
        {
            var result = await new EventQuarantineService(hideDb, new FixedClock(now)).HideAsync(
                item.Id, item.Version, item.Name, "hide emergency scope", new LifecycleActor(superAdmin.Id, superAdmin.LoginName));
            Assert.True(result.Succeeded, result.Error);
        }

        await using (var hiddenDb = new ApplicationDbContext(options))
        {
            var passwords = new PasswordHasher<Account>();
            var clock = new FixedClock(now);
            var administration = new AccountAdministrationService(hiddenDb, passwords, clock);
            var identities = new AccountIdentityService(hiddenDb, passwords, clock);

            var page = new Bingo.Web.Pages.Admin.Accounts.ManageModel(hiddenDb, administration, identities)
            {
                PageContext = new PageContext(new ActionContext(new DefaultHttpContext(), new RouteData(), new PageActionDescriptor()))
            };
            Assert.IsType<NotFoundResult>(await page.OnGetAsync(emergency.Id, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => administration.SetEmergencyEnabledAsync(ordinaryAdmin.Id, emergency.Id, true, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => administration.SetEmergencyEnabledAsync(ordinaryAdmin.Id, emergency.Id, false, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => identities.GenerateEmergencyCredentialLinkAsync(ordinaryAdmin.Id, emergency.Id, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => identities.ConsumeResetAsync(link, "hidden-emergency-password", CancellationToken.None));

            Assert.False(await hiddenDb.Accounts.Where(account => account.Id == emergency.Id).Select(account => account.Active).SingleAsync());
            Assert.False(await hiddenDb.AccountEventAccesses.Where(candidate => candidate.Id == access.Id).Select(candidate => candidate.Enabled).SingleAsync());
            var auditPage = new Bingo.Web.Pages.Admin.Audit.IndexModel(hiddenDb);
            await auditPage.OnGetAsync(CancellationToken.None);
            Assert.DoesNotContain(auditPage.Entries, entry => entry.Id == linkAuditId);
        }
    }

    [Fact]
    public async Task HiddenEventsFailClosedForTeamFocusAccess()
    {
        var superAdmin = Account.CreateWebsite(Guid.NewGuid(), "focus-quarantine-super", "FOCUS-QUARANTINE-SUPER", now);
        superAdmin.SetGlobalRole(GlobalRole.SuperAdmin);
        var captain = Account.CreateWebsite(Guid.NewGuid(), "focus-quarantine-captain", "FOCUS-QUARANTINE-CAPTAIN", now);
        var item = ReadyForFinalReview(superAdmin.Id);
        var participant = new Bingo.Domain.Signups.EventParticipant(Guid.NewGuid(), item.Id, Bingo.Domain.Signups.SignupStatus.Confirmed, 1, now, Bingo.Domain.Signups.SignupSource.AdminCreated);
        participant.AssignOwner(captain);
        var team = new Bingo.Domain.Teams.Team(Guid.NewGuid(), item.Id, "Focus quarantine team", $"focus-quarantine-{item.Id:N}", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
        var membership = new Bingo.Domain.Teams.TeamMembership(Guid.NewGuid(), team.Id, participant.Id, Bingo.Domain.Teams.TeamMembershipRole.Captain, now, null, "test");

        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(superAdmin, captain, item, participant, team, membership);
            await setup.SaveChangesAsync();
        }

        await using (var visibleDb = new ApplicationDbContext(options))
        {
            var context = await new Bingo.Infrastructure.Teams.TeamFocusService(visibleDb, new FixedClock(now))
                .GetContextAsync(item.Id, team.Id, captain.Id, false);
            Assert.NotNull(context);
            Assert.True(context!.IsVisible);
        }

        await using (var hideDb = new ApplicationDbContext(options))
        {
            var result = await new EventQuarantineService(hideDb, new FixedClock(now)).HideAsync(
                item.Id, item.Version, item.Name, "hide focus scope", new LifecycleActor(superAdmin.Id, superAdmin.LoginName));
            Assert.True(result.Succeeded, result.Error);
        }

        await using var hiddenDb = new ApplicationDbContext(options);
        Assert.Null(await new Bingo.Infrastructure.Teams.TeamFocusService(hiddenDb, new FixedClock(now))
            .GetContextAsync(item.Id, team.Id, captain.Id, false));
    }

    [Fact]
    public async Task QuarantineMigrationBackfillsLegacyCaptainRoutesAndRejectsUnassociatedEventRoutes()
    {
        const string previousMigration = "20260823214522_NormalizeMyAccountsPreferredOrder";
        await using var migrationDb = new ApplicationDbContext(options);
        await migrationDb.GetService<IMigrator>().MigrateAsync(previousMigration);

        var eventId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var emergencyAccountId = Guid.NewGuid();
        var emergencyAccessId = Guid.NewGuid();
        var emergencyAuditId = Guid.NewGuid();
        var websiteAccountId = Guid.NewGuid();
        var websiteAuditId = Guid.NewGuid();
        await migrationDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, finalized_at, archived_at, created_by_account_id, created_at)
            VALUES ({eventId}, {"Legacy Captain route event"}, {$"legacy-captain-route-{eventId:N}"}, {""}, {"UTC"}, {"Draft"}, {now}, {now.AddHours(1)}, {now.AddDays(1)}, {now.AddDays(2)}, {now.AddDays(2)}, 10, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, NULL, NULL, {Guid.NewGuid()}, {now});
            INSERT INTO event_participants (id, event_id, captain_volunteer, payment_received, signup_status, signup_sequence, signed_up_at, form_version, response_version, source)
            VALUES ({participantId}, {eventId}, TRUE, FALSE, {"Confirmed"}, 1, {now}, 1, 1, {"AdminCreated"});
            INSERT INTO osrs_characters ("Id", "DisplayName", "NormalizedName", "CreatedAt", "UpdatedAt")
            VALUES ({characterId}, {"Legacy captain"}, {"LEGACY CAPTAIN"}, {now}, {now});
            INSERT INTO submissions (id, event_id, team_id, board_tile_id, requirement_id, drop_snapshot_id, credited_participant_id, submitted_by_account_id, claimed_weight, approved_contribution, submitted_at, captain_note, status, expected_evidence_code, current_reviewer_note, reviewed_at, credited_character_name, credited_osrs_character_id, resubmission_of_submission_id, version)
            VALUES ({submissionId}, {eventId}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, NULL, {participantId}, {Guid.NewGuid()}, 1, 0, {now}, NULL, {"Pending"}, NULL, NULL, NULL, {"Legacy captain"}, {characterId}, NULL, 1);
            INSERT INTO accounts (id, password_hash, disabled_at, created_at, last_login_at, must_change_password, account_type, active, authorization_version, login_name, normalized_login_name, password_changed_at, onboarding_completed_at, global_role, public_username, normalized_public_username, password_version, version)
            VALUES ({emergencyAccountId}, NULL, NULL, {now}, NULL, FALSE, {"EmergencyCaptain"}, FALSE, 1, {"legacy-emergency"}, {"LEGACY-EMERGENCY"}, NULL, NULL, NULL, NULL, NULL, 1, 1);
            INSERT INTO account_event_accesses ("Id", "AccountId", "EventId", "TeamId", "ParticipantId", active_from, correction_only_from, expires_at, "Enabled")
            VALUES ({emergencyAccessId}, {emergencyAccountId}, {eventId}, {Guid.NewGuid()}, NULL, {now}, NULL, NULL, FALSE);
            INSERT INTO audit_entries (id, occurred_at, actor_account_id, actor_username, action, target_type, target_id, details, event_id)
            VALUES ({emergencyAuditId}, {now}, NULL, {"legacy-system"}, {"account.emergency_login"}, {"account"}, {emergencyAccountId.ToString()}, {"Legacy emergency login"}, NULL);
            INSERT INTO accounts (id, password_hash, disabled_at, created_at, last_login_at, must_change_password, account_type, active, authorization_version, login_name, normalized_login_name, password_changed_at, onboarding_completed_at, global_role, public_username, normalized_public_username, password_version, version)
            VALUES ({websiteAccountId}, {"legacy-website-hash"}, NULL, {now}, NULL, FALSE, {"WebsiteAccount"}, TRUE, 1, {"legacy-website"}, {"LEGACY-WEBSITE"}, {now}, {now}, {"Admin"}, {"legacy-website"}, {"LEGACY-WEBSITE"}, 1, 1);
            INSERT INTO audit_entries (id, occurred_at, actor_account_id, actor_username, action, target_type, target_id, details, event_id)
            VALUES ({websiteAuditId}, {now}, NULL, {"legacy-system"}, {"account.password_reset"}, {"account"}, {websiteAccountId.ToString()}, {"Legacy website password reset"}, NULL);
            INSERT INTO personal_notifications ("Id", "RecipientAccountId", "Title", "Detail", "Route", "CreatedAt")
            VALUES ({notificationId}, {Guid.NewGuid()}, {"Legacy submission"}, {"Retained"}, {$"/Captain/Submissions/{submissionId}"}, {now});
            """);

        await migrationDb.GetService<IMigrator>().MigrateAsync();
        var associatedEventId = await migrationDb.Database.SqlQuery<Guid>($"SELECT event_id AS \"Value\" FROM personal_notifications WHERE \"Id\" = {notificationId}").SingleAsync();
        Assert.Equal(eventId, associatedEventId);
        var associatedAuditEventId = await migrationDb.Database.SqlQuery<Guid>($"SELECT event_id AS \"Value\" FROM audit_entries WHERE id = {emergencyAuditId}").SingleAsync();
        Assert.Equal(eventId, associatedAuditEventId);
        var websitePasswordResetEventId = await migrationDb.Database.SqlQuery<Guid?>($"SELECT event_id AS \"Value\" FROM audit_entries WHERE id = {websiteAuditId}").SingleAsync();
        Assert.Null(websitePasswordResetEventId);

        await migrationDb.GetService<IMigrator>().MigrateAsync(previousMigration);
        var unresolvedAuditId = Guid.NewGuid();
        var unresolvedEmergencyAccountId = Guid.NewGuid();
        await migrationDb.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE audit_entries SET event_id = NULL WHERE id = {emergencyAuditId};
            INSERT INTO audit_entries (id, occurred_at, actor_account_id, actor_username, action, target_type, target_id, details, event_id)
            VALUES ({unresolvedAuditId}, {now}, NULL, {"legacy-system"}, {"account.emergency_login"}, {"account"}, {unresolvedEmergencyAccountId.ToString()}, {"Missing emergency account"}, NULL);
            """);

        var auditException = await Assert.ThrowsAsync<PostgresException>(() => migrationDb.GetService<IMigrator>().MigrateAsync());
        Assert.Contains(unresolvedAuditId.ToString(), auditException.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("account.emergency_login", auditException.MessageText, StringComparison.Ordinal);
        Assert.Contains(unresolvedEmergencyAccountId.ToString(), auditException.MessageText, StringComparison.OrdinalIgnoreCase);

        await migrationDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM audit_entries WHERE id = {unresolvedAuditId};");
        await migrationDb.GetService<IMigrator>().MigrateAsync();
        await migrationDb.GetService<IMigrator>().MigrateAsync(previousMigration);

        var unresolvedNotificationId = Guid.NewGuid();
        var unresolvedSubmissionId = Guid.NewGuid();
        await migrationDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO personal_notifications ("Id", "RecipientAccountId", "Title", "Detail", "Route", "CreatedAt")
            VALUES ({unresolvedNotificationId}, {Guid.NewGuid()}, {"Unknown submission"}, {"Correct before deploy"}, {$"/Captain/Submissions/{unresolvedSubmissionId}"}, {now});
            """);

        var notificationException = await Assert.ThrowsAsync<PostgresException>(() => migrationDb.GetService<IMigrator>().MigrateAsync());
        Assert.Contains(unresolvedNotificationId.ToString(), notificationException.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/Captain/Submissions/{unresolvedSubmissionId}", notificationException.MessageText, StringComparison.Ordinal);
    }

    private BingoEvent ReadyForFinalReview(Guid actorId)
    {
        var item = new BingoEvent(Guid.NewGuid(), "Quarantine integration", $"quarantine-{Guid.NewGuid():N}", "UTC", actorId, now);
        item.ConfigureInitialSchedule(now.AddHours(-5), now.AddHours(-4), null, now.AddHours(-3), now.AddHours(1), 10);
        item.OpenSignups(now.AddHours(-4));
        item.CloseSignups(now.AddHours(-3));
        item.StartEvent(now.AddHours(-2));
        item.EndEvent(now.AddHours(-1));
        return item;
    }

    private sealed class FixedClock(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingAdminCollaborationNotifier : IAdminCollaborationNotifier
    {
        public int EventsControlChanges { get; private set; }
        public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyEventsControlChangedAsync(CancellationToken cancellationToken = default)
        {
            EventsControlChanges++;
            return Task.CompletedTask;
        }
    }
}
