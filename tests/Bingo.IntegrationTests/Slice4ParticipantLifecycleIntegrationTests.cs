using System.Security.Claims;
using Bingo.Application.Access;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice4ParticipantLifecycleIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice4_participant_lifecycle")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        );
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ConcurrentCapacityIncreasePromotesInOrderAndNotifiesOnlyEnabledAdminsOnce()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 2);
        async Task<int> IncreaseAsync()
        {
            await using var db = new ApplicationDbContext(options);
            return await Service(db).IncreaseCapacityAndPromoteAsync(setup.EventId, 2);
        }

        var results = await Task.WhenAll(IncreaseAsync(), IncreaseAsync());
        Assert.Equal(1, results.Sum());
        await using var verify = new ApplicationDbContext(options);
        var participants = await verify.EventParticipants.Where(x => x.EventId == setup.EventId).OrderBy(x => x.SignupSequence).ToListAsync();
        Assert.Equal(new[] { SignupStatus.Confirmed, SignupStatus.Confirmed, SignupStatus.WaitingList }, participants.Select(x => x.SignupStatus));
        Assert.Equal(setup.WaitingOwnerIds[0], participants[1].AccountId);
        Assert.Equal(11m, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participants[1].Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.promoted").ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "event.capacity_increased").ToListAsync());
        var notifications = await verify.PersonalNotifications.Where(x => x.Title == "participant.promoted").ToListAsync();
        Assert.Equal(3, notifications.Count);
        var promotion = Assert.Single(notifications, x => x.RecipientAccountId == setup.WaitingOwnerIds[0]);
        var route = new Uri($"https://test.invalid{promotion.Route}");
        var segments = route.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, segments.Length);
        Assert.Equal("Events", segments[0]);
        Assert.Equal("Signup", segments[2]);
        Assert.Equal("Confirmation", segments[3]);
        var routeParticipantId = Guid.Parse(QueryHelpers.ParseQuery(route.Query)["participantId"].ToString());
        var confirmation = new Bingo.Web.Pages.Events.ConfirmationModel(verify, TimeProvider.System, Service(verify), new PassthroughLocalizer())
        {
            PageContext = new PageContext(new ActionContext(
                new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.NameIdentifier, promotion.RecipientAccountId.ToString()),
                        new Claim(AccountClaims.AccountType, AccountType.WebsiteAccount.ToString())
                    ], "test"))
                }, new RouteData(), new PageActionDescriptor()))
        };
        var destination = await confirmation.OnGetAsync(Uri.UnescapeDataString(segments[1]), routeParticipantId, CancellationToken.None);
        Assert.IsType<PageResult>(destination);
        Assert.Equal("Lifecycle", confirmation.EventName);
        Assert.Equal(nameof(SignupStatus.Confirmed), confirmation.Status);
        Assert.Single(notifications, x => x.RecipientAccountId == setup.EnabledAdminId);
        Assert.Single(notifications, x => x.RecipientAccountId == setup.EnabledSuperAdminId);
        Assert.DoesNotContain(notifications, x => x.RecipientAccountId == setup.DisabledAdminId || x.RecipientAccountId == setup.UnrelatedUserId);
    }

    [Fact]
    public async Task SignupAdministrationOwnsCapacityAndWaitingListWithVersionedPromotionFeedback()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 1);
        long version;
        await using (var read = new ApplicationDbContext(options))
            version = await read.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync();

        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Service(db).UpdateSignupAdministrationAsync(setup.EventId, version, 2, true, setup.EnabledAdminId, "admin");
            Assert.True(result.Succeeded);
            Assert.Equal(1, result.PromotedParticipants);
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(2, await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.ParticipantCap).SingleAsync());
            Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.AccountId == setup.WaitingOwnerIds[0]).Select(x => x.SignupStatus).SingleAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "event.signup_administration_updated" && x.ActorAccountId == setup.EnabledAdminId).ToListAsync());
            var stale = await Service(verify).UpdateSignupAdministrationAsync(setup.EventId, version, 3, true, setup.EnabledAdminId, "admin");
            Assert.False(stale.Succeeded);
            Assert.Contains("changed", stale.Error, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task SignupAdministrationRollsBackWhenAuditCannotPersist()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 1);
        long version;
        await using (var read = new ApplicationDbContext(options))
            version = await read.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync();

        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .AddInterceptors(new ThrowOnCapacityAudit())
            .Options;
        await using (var failing = new ApplicationDbContext(failingOptions))
        {
            var result = await Service(failing).UpdateSignupAdministrationAsync(setup.EventId, version, 2, true, setup.EnabledAdminId, "admin");
            Assert.False(result.Succeeded);
            Assert.Contains("try again", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        var savedEvent = await verify.Events.SingleAsync(x => x.Id == setup.EventId);
        Assert.Equal(version, savedEvent.Version);
        Assert.Equal(1, savedEvent.ParticipantCap);
        Assert.Equal(new[] { SignupStatus.Confirmed, SignupStatus.WaitingList },
            await verify.EventParticipants.Where(x => x.EventId == setup.EventId).OrderBy(x => x.SignupSequence).Select(x => x.SignupStatus).ToArrayAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "event.signup_administration_updated").ToListAsync());
        Assert.Empty(await verify.PersonalNotifications.Where(x => x.EventId == setup.EventId && x.Title == "participant.promoted").ToListAsync());
    }

    [Fact]
    public async Task OwnerSearchIsBoundedAndDisabledOwnerIdsAreRejectedWithoutCreation()
    {
        var setup = await SeedAsync(capacity: 20, confirmed: 12, waiting: 0);
        await using (var db = new ApplicationDbContext(options))
        {
            db.SignupForms.Add(new SignupForm(Guid.NewGuid(), setup.EventId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();

            var model = new Bingo.Web.Pages.Admin.Events.ParticipantsModel(db, Service(db));
            var response = Assert.IsType<JsonResult>(await model.OnGetSearchOwnerAccountsAsync("owner", CancellationToken.None));
            var matches = Assert.IsType<List<Bingo.Web.Pages.Admin.Events.ParticipantsModel.OwnerAccountOption>>(response.Value);
            Assert.Equal(10, matches.Count);
            Assert.DoesNotContain(matches, item => item.Username.StartsWith("disabled", StringComparison.OrdinalIgnoreCase));
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var before = await db.EventParticipants.CountAsync(item => item.EventId == setup.EventId);
            var result = await Service(db).CreateAdminParticipantAsync(new AdminParticipantChangeRequest(
                setup.EventId, null, setup.EnabledAdminId, "admin", setup.DisabledAdminId, new Dictionary<Guid, AdminAccountAnswer>(), new Dictionary<Guid, string>()));
            Assert.False(result.Succeeded);
            Assert.Contains("active website account", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, await db.EventParticipants.CountAsync(item => item.EventId == setup.EventId));
        }
    }

    [Fact]
    public async Task PreformedRosterMembersCountTowardCapacityAndPromotionAlongsideAdminSignups()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 1);
        var now = DateTimeOffset.UtcNow;
        Guid externalId;
        Guid adminCreatedId;
        await using (var db = new ApplicationDbContext(options))
        {
            var team = new Team(Guid.NewGuid(), setup.EventId, "External", "external", TeamFormationType.Preformed, null, false, now);
            var external = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 10, now, SignupSource.AdminCreated, null);
            var externalCharacter = new OsrsCharacter(Guid.NewGuid(), "External roster", $"EXTERNAL ROSTER {Guid.NewGuid():N}", now);
            var externalAssignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, external.Id, externalCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 1, EhbSource.AdminCorrection, null);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, external.Id, TeamMembershipRole.Participant, now, null, "Pre-formed roster");
            membership.SetSource(TeamMembershipSource.PreformedManual);
            var adminCreated = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 11, now, SignupSource.AdminCreated, null);
            var adminCharacter = new OsrsCharacter(Guid.NewGuid(), "Admin signup", $"ADMIN SIGNUP {Guid.NewGuid():N}", now);
            var adminAssignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, adminCreated.Id, adminCharacter.Id, 0, now, setup.EnabledAdminId, null, EventCharacterRole.Playing, 2, EhbSource.AdminCorrection, null);
            db.AddRange(team, external, externalCharacter, externalAssignment, membership, adminCreated, adminCharacter, adminAssignment);
            await db.SaveChangesAsync();
            externalId = external.Id;
            adminCreatedId = adminCreated.Id;
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var version = await db.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync();
            var result = await Service(db).UpdateSignupAdministrationAsync(setup.EventId, version, 3, true, setup.EnabledAdminId, "admin");
            Assert.True(result.Succeeded);
            Assert.Equal(0, result.PromotedParticipants);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var version = await db.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync();
            var result = await Service(db).UpdateSignupAdministrationAsync(setup.EventId, version, 4, true, setup.EnabledAdminId, "admin");
            Assert.True(result.Succeeded);
            Assert.Equal(1, result.PromotedParticipants);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.AccountId == setup.WaitingOwnerIds[0]).Select(x => x.SignupStatus).SingleAsync());
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == externalId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == adminCreatedId).Select(x => x.SignupStatus).SingleAsync());
        var promotedId = await verify.EventParticipants.Where(p => p.AccountId == setup.WaitingOwnerIds[0]).Select(p => p.Id).SingleAsync();
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.promoted" && x.TargetId == promotedId.ToString()).ToListAsync());

        var model = new Bingo.Web.Pages.Admin.Events.ParticipantsModel(verify, Service(verify))
        {
            PageContext = new PageContext(new ActionContext(AdminContext(setup.EnabledAdminId), new RouteData(), new PageActionDescriptor()))
        };
        Assert.True((await model.OnGetAsync(setup.EventId, CancellationToken.None)) is PageResult);
        Assert.DoesNotContain(model.Participants, row => row.Id == externalId);
        Assert.Equal(4, model.Event!.Confirmed);
    }

    [Fact]
    public async Task ConfirmedAndWaitingWithdrawalsAreIdempotentAndPreserveAssignmentHistory()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 2);
        async Task<ParticipantLifecycleResult> WithdrawAsync(Guid participantId, Guid ownerId)
        {
            await using var db = new ApplicationDbContext(options);
            return await Service(db).WithdrawAsync(setup.EventId, participantId, ownerId, "owner", false);
        }

        var confirmedResults = await Task.WhenAll(WithdrawAsync(setup.ConfirmedParticipantId, setup.ConfirmedOwnerId), WithdrawAsync(setup.ConfirmedParticipantId, setup.ConfirmedOwnerId));
        Assert.Single(confirmedResults, x => x.Changed);
        await using (var verify = new ApplicationDbContext(options))
        {
            var participants = await verify.EventParticipants.Where(x => x.EventId == setup.EventId).OrderBy(x => x.SignupSequence).ToListAsync();
            Assert.Equal(SignupStatus.Withdrawn, participants[0].SignupStatus);
            Assert.Equal(SignupStatus.Confirmed, participants[1].SignupStatus);
            Assert.Equal(SignupStatus.WaitingList, participants[2].SignupStatus);
            Assert.All(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == setup.ConfirmedParticipantId).ToListAsync(), x => Assert.NotNull(x.ReleasedAt));
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.withdrawn").ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.promoted").ToListAsync());
        }

        var waitingParticipant = (await new ApplicationDbContext(options).EventParticipants.Where(x => x.EventId == setup.EventId && x.SignupStatus == SignupStatus.WaitingList).SingleAsync()).Id;
        var waitingResults = await Task.WhenAll(WithdrawAsync(waitingParticipant, setup.WaitingOwnerIds[1]), WithdrawAsync(waitingParticipant, setup.WaitingOwnerIds[1]));
        Assert.Single(waitingResults, x => x.Changed);
        await using var final = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Withdrawn, await final.EventParticipants.Where(x => x.Id == waitingParticipant).Select(x => x.SignupStatus).SingleAsync());
        Assert.Equal(1, await final.EventParticipants.CountAsync(x => x.EventId == setup.EventId && x.SignupStatus == SignupStatus.Confirmed));
        Assert.Single(await final.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.withdrawn" && x.TargetId == waitingParticipant.ToString()).ToListAsync());
    }

    [Fact]
    public async Task RejoinAndAdminRestoreRollbackWhenReleasedCharacterHasBeenClaimed()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 0);
        await using (var db = new ApplicationDbContext(options))
            Assert.True((await Service(db).WithdrawAsync(setup.EventId, setup.ConfirmedParticipantId, setup.ConfirmedOwnerId, "owner", false)).Succeeded);
        var releasedCharacter = await CharacterIdAsync(setup.ConfirmedParticipantId);
        await ClaimCharacterAsync(setup.EventId, releasedCharacter, "claimant");

        await using (var rejoinDb = new ApplicationDbContext(options))
        {
            var rejected = await Service(rejoinDb).RejoinAsync(setup.EventId, setup.ConfirmedParticipantId, setup.ConfirmedOwnerId, "owner");
            Assert.False(rejected.Succeeded);
        }
        await using (var restoreDb = new ApplicationDbContext(options))
        {
            var rejected = await Service(restoreDb).RestoreAsync(setup.EventId, setup.ConfirmedParticipantId, setup.EnabledAdminId, "admin");
            Assert.False(rejected.Succeeded);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Empty(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == setup.ConfirmedParticipantId && x.ReleasedAt == null).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && (x.Action == "participant.rejoined" || x.Action == "participant.admin_restored")).ToListAsync());
        Assert.Empty(await verify.PersonalNotifications.Where(x => x.RecipientAccountId == setup.ConfirmedOwnerId && x.Title == "participant.restored").ToListAsync());
    }

    [Fact]
    public async Task AdminRestoreCapacityOverrideWaitsForReacquisitionAndCanRetryInTheSameContext()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 0);
        await using (var db = new ApplicationDbContext(options))
            Assert.True((await Service(db).WithdrawAsync(setup.EventId, setup.ConfirmedParticipantId, setup.ConfirmedOwnerId, "owner", false)).Succeeded);
        var releasedCharacter = await CharacterIdAsync(setup.ConfirmedParticipantId);
        await ClaimCharacterAsync(setup.EventId, releasedCharacter, "override-claimant");

        await using var restoreDb = new ApplicationDbContext(options);
        var beforeEvent = await restoreDb.Events.AsNoTracking().SingleAsync(x => x.Id == setup.EventId);
        var beforeRestoreResponseVersion = await restoreDb.EventParticipants.AsNoTracking()
            .Where(x => x.Id == setup.ConfirmedParticipantId)
            .Select(x => x.ResponseVersion)
            .SingleAsync();
        var failed = await Service(restoreDb).RestoreAdminParticipantAsync(new(
            setup.EventId, setup.ConfirmedParticipantId, setup.EnabledAdminId, "admin", ExpandCapacityWhenFull: true,
            ExpectedEventVersion: beforeEvent.Version, ExpectedResponseVersion: beforeRestoreResponseVersion));
        Assert.False(failed.Succeeded);
        Assert.Contains("assigned", failed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeEvent.ParticipantCap, await restoreDb.Events.Where(x => x.Id == setup.EventId).Select(x => x.ParticipantCap).SingleAsync());
        Assert.Equal(beforeEvent.Version, await restoreDb.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync());

        var note = await Service(restoreDb).SetAdminNotesAsync(setup.EventId, setup.ConfirmedParticipantId, setup.EnabledAdminId, "admin", "after failed restore", null);
        Assert.True(note.Succeeded, note.Error);

        var claimant = await restoreDb.EventParticipantCharacters
            .Where(x => x.EventId == setup.EventId && x.OsrsCharacterId == releasedCharacter && x.ReleasedAt == null)
            .Select(x => x.EventParticipantId)
            .SingleAsync();
        var claimantOwner = await restoreDb.EventParticipants.Where(x => x.Id == claimant).Select(x => x.AccountId).SingleAsync();
        Assert.True(claimantOwner.HasValue);
        var withdrawn = await Service(restoreDb).WithdrawAsync(setup.EventId, claimant, claimantOwner, "claimant", false);
        Assert.True(withdrawn.Succeeded, withdrawn.Error);

        // Keep the event genuinely full for the retry.  The released claimant
        // has been withdrawn, so a separate confirmed participant supplies the
        // capacity boundary while the original character remains available.
        var fillerNow = new DateTimeOffset(2026, 9, 30, 20, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);
        var fillerOwner = Website($"override-filler-{Guid.NewGuid():N}", fillerNow);
        var fillerParticipant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 2, fillerNow, SignupSource.AdminCreated);
        fillerParticipant.AssignOwner(fillerOwner);
        var fillerCharacter = new OsrsCharacter(Guid.NewGuid(), "Override filler", $"OVERRIDE FILLER {Guid.NewGuid():N}", fillerNow);
        var fillerAssignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, fillerParticipant.Id, fillerCharacter.Id, 0, fillerNow, setup.EnabledAdminId, null, EventCharacterRole.Playing, 18m, EhbSource.AdminCorrection, null);
        restoreDb.AddRange(fillerOwner, fillerParticipant, fillerCharacter, fillerAssignment);
        await restoreDb.SaveChangesAsync();

        var requestEventVersion = await restoreDb.Events.AsNoTracking().Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync();
        var requestResponseVersion = await restoreDb.EventParticipants.AsNoTracking()
            .Where(x => x.Id == setup.ConfirmedParticipantId)
            .Select(x => x.ResponseVersion)
            .SingleAsync();
        var request = new AdminParticipantRestoreRequest(
            setup.EventId, setup.ConfirmedParticipantId, setup.EnabledAdminId, "admin", ExpandCapacityWhenFull: true,
            ExpectedEventVersion: requestEventVersion, ExpectedResponseVersion: requestResponseVersion);
        var restored = await Service(restoreDb).RestoreAdminParticipantAsync(request);
        Assert.True(restored.Succeeded, restored.Error);
        Assert.True(restored.Changed);
        var retry = await Service(restoreDb).RestoreAdminParticipantAsync(request with
        {
            ExpectedEventVersion = await restoreDb.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync(),
            ExpectedResponseVersion = await restoreDb.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId).Select(x => x.ResponseVersion).SingleAsync()
        });
        Assert.True(retry.Succeeded, retry.Error);
        Assert.False(retry.Changed);

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(2, await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.ParticipantCap).SingleAsync());
        Assert.Equal(beforeEvent.Version + 1, await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync());
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Equal("after failed restore", await verify.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId).Select(x => x.AdminNotes).SingleAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.admin_restored" && x.TargetId == setup.ConfirmedParticipantId.ToString()).ToListAsync());
        Assert.Single(await verify.PersonalNotifications.Where(x => x.EventId == setup.EventId && x.RecipientAccountId == setup.ConfirmedOwnerId && x.Title == "participant.restored").ToListAsync());
    }

    [Fact]
    public async Task ClosedSignupAllowsWithdrawalButRejectsSelfRejoinAndAdminRestoreNotifiesGenerically()
    {
        var setup = await SeedAsync(capacity: 2, confirmed: 1, waiting: 0);
        await using (var close = new ApplicationDbContext(options))
        {
            var bingoEvent = await close.Events.SingleAsync(x => x.Id == setup.EventId);
            bingoEvent.CloseSignups(DateTimeOffset.UtcNow);
            await close.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
            Assert.True((await Service(db).WithdrawAsync(setup.EventId, setup.ConfirmedParticipantId, setup.EnabledAdminId, "admin", true, "private detail")).Succeeded);
        await using (var rejoin = new ApplicationDbContext(options))
            Assert.False((await Service(rejoin).RejoinAsync(setup.EventId, setup.ConfirmedParticipantId, setup.ConfirmedOwnerId, "owner")).Succeeded);
        await using (var restore = new ApplicationDbContext(options))
            Assert.True((await Service(restore).RestoreAsync(setup.EventId, setup.ConfirmedParticipantId, setup.EnabledAdminId, "admin")).Succeeded);
        await using var verify = new ApplicationDbContext(options);
        var notices = await verify.PersonalNotifications.Where(x => x.RecipientAccountId == setup.ConfirmedOwnerId && (x.Title == "participant.withdrawn" || x.Title == "participant.restored")).ToListAsync();
        Assert.Equal(2, notices.Count);
        Assert.All(notices, x => Assert.DoesNotContain("private detail", x.Detail, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Equal("private detail", await verify.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId).Select(x => x.AdminNotes).SingleAsync());
    }

    [Fact]
    public async Task ManageAndDraftWithdrawalHandlersUseTheSharedAtomicOperationWithoutAReason()
    {
        var manageSetup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 1);
        Guid promotedParticipantId;
        await using (var db = new ApplicationDbContext(options))
        {
            var context = AdminContext(manageSetup.EnabledAdminId);
            var participants = new Bingo.Web.Pages.Admin.Events.ParticipantsModel(db, Service(db))
            {
                PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
                TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
            };
            Assert.IsType<RedirectToPageResult>(await participants.OnPostWithdrawAsync(manageSetup.EventId, manageSetup.ConfirmedParticipantId, CancellationToken.None));
            Assert.Equal(SignupStatus.Confirmed, await db.EventParticipants.Where(x => x.Id == manageSetup.ConfirmedParticipantId).Select(x => x.SignupStatus).SingleAsync());
            Assert.IsType<RedirectToPageResult>(await participants.OnPostWithdrawAsync(manageSetup.EventId, manageSetup.ConfirmedParticipantId, CancellationToken.None, true));
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.Id == manageSetup.ConfirmedParticipantId).Select(x => x.SignupStatus).SingleAsync());
            Assert.Empty(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == manageSetup.ConfirmedParticipantId && x.ReleasedAt == null).ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.TargetId == manageSetup.ConfirmedParticipantId.ToString() && x.Action == "participant.admin_withdrawn" && x.ActorAccountId == manageSetup.EnabledAdminId).ToListAsync());
            Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.AccountId == manageSetup.WaitingOwnerIds[0]).Select(x => x.SignupStatus).SingleAsync());
            Assert.Contains(await verify.PersonalNotifications.ToListAsync(), x => x.RecipientAccountId == manageSetup.EnabledAdminId && x.Title == "participant.promoted");
        }

        await using (var db = new ApplicationDbContext(options))
        {
            promotedParticipantId = await db.EventParticipants.Where(x => x.EventId == manageSetup.EventId && x.AccountId == manageSetup.WaitingOwnerIds[0]).Select(x => x.Id).SingleAsync();
            db.DraftSessions.Add(new DraftSession(Guid.NewGuid(), manageSetup.EventId, 2));
            await db.SaveChangesAsync();
            var context = AdminContext(manageSetup.EnabledAdminId);
            var draft = new Bingo.Web.Pages.Admin.Events.DraftModel(db, TimeProvider.System, null!, null!, Service(db), new EventParticipantCharacterService(db, TimeProvider.System))
            {
                PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
                TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
            };
            Assert.IsType<RedirectToPageResult>(await draft.OnPostWithdrawParticipantAsync(manageSetup.EventId, promotedParticipantId, CancellationToken.None));
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.AccountId == manageSetup.WaitingOwnerIds[0]).Select(x => x.SignupStatus).SingleAsync());
            Assert.Empty(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == promotedParticipantId && x.ReleasedAt == null).ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.TargetId == promotedParticipantId.ToString() && x.Action == "participant.admin_withdrawn" && x.ActorAccountId == manageSetup.EnabledAdminId).ToListAsync());
            Assert.Contains(await verify.PersonalNotifications.ToListAsync(), x => x.RecipientAccountId == manageSetup.WaitingOwnerIds[0] && x.Title == "participant.withdrawn");
            Assert.Empty(await verify.EventParticipants.Where(x => x.EventId == manageSetup.EventId && x.SignupStatus == SignupStatus.WaitingList).ToListAsync());
        }
    }

    [Fact]
    public async Task DraftAdminWithdrawalPromotesWaitingListAndRestoreUsesCurrentCapacityWithPrivateNote()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var admin = Website($"draft-admin-{Guid.NewGuid():N}", now, GlobalRole.Admin);
        var firstOwner = Website($"draft-first-{Guid.NewGuid():N}", now);
        var waitingOwner = Website($"draft-waiting-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Draft lifecycle", $"draft-lifecycle-{Guid.NewGuid():N}", "", "UTC", now.AddHours(1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), 1, admin.Id, now);
        var first = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website, null); first.AssignOwner(firstOwner);
        var waiting = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.WaitingList, 2, now.AddMinutes(1), SignupSource.Website, null); waiting.AssignOwner(waitingOwner);
        var team = new Team(Guid.NewGuid(), bingoEvent.Id, "Included team", "included-team", TeamFormationType.Drafted, null, true, now);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, first.Id, TeamMembershipRole.Captain, now, null, "Draft setup");
        var firstCharacter = new OsrsCharacter(Guid.NewGuid(), "Draft first", $"DRAFT FIRST {Guid.NewGuid():N}", now);
        var waitingCharacter = new OsrsCharacter(Guid.NewGuid(), "Draft waiting", $"DRAFT WAITING {Guid.NewGuid():N}", now);
        db.AddRange(admin, firstOwner, waitingOwner, bingoEvent, first, waiting, team, membership, firstCharacter, waitingCharacter,
            new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, first.Id, firstCharacter.Id, 0, now, firstOwner.Id, null, EventCharacterRole.Playing, 10, EhbSource.Manual, null),
            new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, waiting.Id, waitingCharacter.Id, 0, now.AddMinutes(1), waitingOwner.Id, null, EventCharacterRole.Playing, 11, EhbSource.Manual, null));
        await db.SaveChangesAsync();

        var withdrawn = await Service(db).WithdrawAsync(bingoEvent.Id, first.Id, admin.Id, admin.LoginName, true, "Draft departure note");
        Assert.True(withdrawn.Succeeded, withdrawn.Error);
        Assert.Equal(SignupStatus.Confirmed, await db.EventParticipants.Where(x => x.Id == waiting.Id).Select(x => x.SignupStatus).SingleAsync());
        Assert.Empty(await db.EventParticipantCharacters.Where(x => x.EventParticipantId == first.Id && x.ReleasedAt == null).ToListAsync());
        Assert.Equal("Draft departure note", await db.EventParticipants.Where(x => x.Id == first.Id).Select(x => x.AdminNotes).SingleAsync());
        Assert.NotNull(await db.TeamMemberships.Where(x => x.Id == membership.Id).Select(x => x.LeftAt).SingleAsync());
        Assert.Equal(TeamMembershipRole.Participant, await db.TeamMemberships.Where(x => x.Id == membership.Id).Select(x => x.Role).SingleAsync());

        var restored = await Service(db).RestoreAsync(bingoEvent.Id, first.Id, admin.Id, admin.LoginName);
        Assert.True(restored.Succeeded, restored.Error);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.WaitingList, await verify.EventParticipants.Where(x => x.Id == first.Id).Select(x => x.SignupStatus).SingleAsync());
        Assert.Single(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == first.Id && x.ReleasedAt == null).ToListAsync());
        Assert.Equal("Draft departure note", await verify.EventParticipants.Where(x => x.Id == first.Id).Select(x => x.AdminNotes).SingleAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == bingoEvent.Id && x.Action == "participant.admin_withdrawn").ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == bingoEvent.Id && x.Action == "participant.admin_restored").ToListAsync());
        var ownerNotifications = await verify.PersonalNotifications.Where(x => x.RecipientAccountId == firstOwner.Id).ToListAsync();
        Assert.Contains(ownerNotifications, x => x.Title == "participant.withdrawn");
        Assert.Contains(ownerNotifications, x => x.Title == "participant.restored");
    }

    [Fact]
    public async Task AdminAccountCorrectionNotifiesLinkedOwnerAndPreservesPrivateMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var admin = Website($"correction-admin-{Guid.NewGuid():N}", now, GlobalRole.Admin);
        var owner = Website($"correction-owner-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Correction", $"correction-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), 2, admin.Id, now);
        bingoEvent.OpenSignups(now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "regular", "Regular", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var oldCharacter = new OsrsCharacter(Guid.NewGuid(), "Correction old", $"CORRECTION OLD {Guid.NewGuid():N}", now);
        var newCharacter = new OsrsCharacter(Guid.NewGuid(), "Correction new", $"CORRECTION NEW {Guid.NewGuid():N}", now);
        var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        participant.AssignOwner(owner); participant.SetPaymentReceived(true); participant.SetAdminNotes("private correction note");
        db.AddRange(admin, owner, bingoEvent, form, regular, oldCharacter, newCharacter, participant,
            new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, participant.Id, oldCharacter.Id, 0, now, owner.Id, regular.Id, EventCharacterRole.Playing, 5, EhbSource.Manual, null));
        await db.SaveChangesAsync();

        var result = await Service(db).CorrectAdminParticipantAsync(new(
            bingoEvent.Id, participant.Id, admin.Id, admin.LoginName, null,
            new Dictionary<Guid, AdminAccountAnswer> { [regular.Id] = new(newCharacter.DisplayName, 7) },
            new Dictionary<Guid, string>(), participant.ResponseVersion));
        Assert.True(result.Succeeded, result.Error);

        await using var verify = new ApplicationDbContext(options);
        var notifications = await verify.PersonalNotifications.Where(x => x.EventId == bingoEvent.Id && x.RecipientAccountId == owner.Id).ToListAsync();
        var notification = Assert.Single(notifications, x => x.Title == "participant.accounts_changed");
        Assert.DoesNotContain(oldCharacter.DisplayName, notification.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(newCharacter.DisplayName, notification.Detail, StringComparison.Ordinal);
        var saved = await verify.EventParticipants.SingleAsync(x => x.Id == participant.Id);
        Assert.True(saved.PaymentReceived);
        Assert.Equal("private correction note", saved.AdminNotes);
        Assert.Equal(7, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
    }

    [Fact]
    public async Task SelfWithdrawalTerminatesActiveExternalRosterMembership()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 0);
        var now = DateTimeOffset.UtcNow;
        Guid membershipId;
        await using (var db = new ApplicationDbContext(options))
        {
            var team = new Team(Guid.NewGuid(), setup.EventId, "External roster", $"external-{Guid.NewGuid():N}", TeamFormationType.Preformed, null, false, now);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, setup.ConfirmedParticipantId, TeamMembershipRole.Captain, now, null, "External roster setup");
            membership.SetSource(TeamMembershipSource.PreformedManual);
            db.AddRange(team, membership);
            await db.SaveChangesAsync();
            membershipId = membership.Id;
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Service(db).WithdrawAsync(setup.EventId, setup.ConfirmedParticipantId, setup.ConfirmedOwnerId, "owner", false);
            Assert.True(result.Succeeded, result.Error);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId).Select(x => x.SignupStatus).SingleAsync());
        var membershipState = await verify.TeamMemberships.Where(x => x.Id == membershipId).Select(x => new { x.LeftAt, x.Role }).SingleAsync();
        Assert.NotNull(membershipState.LeftAt);
        Assert.Equal(TeamMembershipRole.Participant, membershipState.Role);
        Assert.Single(await verify.TeamMembershipRoleTransitions.Where(x => x.TeamMembershipId == membershipId && x.ChangedByAccountId == setup.ConfirmedOwnerId).ToListAsync());
        Assert.Empty(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == setup.ConfirmedParticipantId && x.ReleasedAt == null).ToListAsync());
    }

    [Fact]
    public async Task AdminWithdrawalTerminatesActiveExternalRosterMembership()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 0);
        var now = DateTimeOffset.UtcNow;
        Guid membershipId;
        await using (var db = new ApplicationDbContext(options))
        {
            var team = new Team(Guid.NewGuid(), setup.EventId, "External admin roster", $"external-admin-{Guid.NewGuid():N}", TeamFormationType.Preformed, null, false, now);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, setup.ConfirmedParticipantId, TeamMembershipRole.CoCaptain, now, null, "External admin roster setup");
            membership.SetSource(TeamMembershipSource.PreformedManual);
            db.AddRange(team, membership);
            await db.SaveChangesAsync();
            membershipId = membership.Id;
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Service(db).WithdrawAsync(setup.EventId, setup.ConfirmedParticipantId, setup.EnabledAdminId, "admin", true);
            Assert.True(result.Succeeded, result.Error);
        }

        await using var verify = new ApplicationDbContext(options);
        var membershipState = await verify.TeamMemberships.Where(x => x.Id == membershipId).Select(x => new { x.LeftAt, x.Role }).SingleAsync();
        Assert.NotNull(membershipState.LeftAt);
        Assert.Equal(TeamMembershipRole.Participant, membershipState.Role);
        Assert.Single(await verify.TeamMembershipRoleTransitions.Where(x => x.TeamMembershipId == membershipId && x.ChangedByAccountId == setup.EnabledAdminId).ToListAsync());
    }

    [Fact]
    public async Task WithdrawnParticipantCorrectionCannotRecreateActiveReservation()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"withdrawn-correction-admin-{Guid.NewGuid():N}", now, GlobalRole.Admin);
        var owner = Website($"withdrawn-correction-owner-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Withdrawn correction", $"withdrawn-correction-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), 2, admin.Id, now);
        bingoEvent.OpenSignups(now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "regular", "Regular", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var oldCharacter = new OsrsCharacter(Guid.NewGuid(), "Withdrawn old", $"WITHDRAWN OLD {Guid.NewGuid():N}", now);
        var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        participant.AssignOwner(owner);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, participant.Id, oldCharacter.Id, 0, now, owner.Id, regular.Id, EventCharacterRole.Playing, 5, EhbSource.Manual, null);
        var answer = new SignupAnswer(Guid.NewGuid(), participant.Id, regular.Id, regular.Label, string.Empty, oldCharacter.Id);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, owner, bingoEvent, form, regular, oldCharacter, participant, assignment, answer);
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var withdrawn = await Service(db).WithdrawAsync(bingoEvent.Id, participant.Id, owner.Id, owner.LoginName, false);
            Assert.True(withdrawn.Succeeded, withdrawn.Error);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Service(db).CorrectAdminParticipantAsync(new(
                bingoEvent.Id, participant.Id, admin.Id, admin.LoginName, null,
                new Dictionary<Guid, AdminAccountAnswer> { [regular.Id] = new(oldCharacter.DisplayName, 6) },
                new Dictionary<Guid, string>(), participant.ResponseVersion));
            Assert.False(result.Succeeded);
            Assert.Contains("restored", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.Id == participant.Id).Select(x => x.SignupStatus).SingleAsync());
        Assert.Empty(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).ToListAsync());
        Assert.Equal(oldCharacter.Id, await verify.SignupAnswers.Where(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == regular.Id).Select(x => x.OsrsCharacterId).SingleAsync());
    }

    [Fact]
    public async Task NewParticipantOperationsRejectMissingExpectedVersionsWithoutWrites()
    {
        await using var db = new ApplicationDbContext(options);
        var beforeAudits = await db.AuditEntries.CountAsync();
        var eventId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var results = new object[]
        {
            await Service(db).AddSavedParticipantAsync(new(
                eventId, Guid.NewGuid(), actorId, "admin", [characterId], characterId)),
            await Service(db).SwitchAdminPrimaryAsync(new(
                eventId, participantId, characterId, actorId, "admin", characterId)),
            await Service(db).AddEventParticipantAccountAsync(new(
                eventId, participantId, characterId, EventCharacterRole.Playing, 10m, actorId, "admin")),
            await Service(db).RemoveEventParticipantAccountAsync(new(
                eventId, participantId, assignmentId, actorId, "admin")),
            await Service(db).CorrectEventParticipantAccountAsync(new(
                eventId, participantId, assignmentId, characterId, 10m, actorId, "admin")),
            await Service(db).ConfirmWaitingParticipantAsync(new(
                eventId, participantId, actorId, "admin")),
            await Service(db).MoveConfirmedParticipantToWaitingAsync(new(
                eventId, participantId, actorId, "admin")),
            await Service(db).RestoreAdminParticipantAsync(new(
                eventId, participantId, actorId, "admin"))
        };

        Assert.All(results, result =>
        {
            var succeeded = result switch
            {
                AdminParticipantResult value => value.Succeeded,
                EventAccountMutationResult value => value.Succeeded,
                ParticipantQueueMutationResult value => value.Succeeded,
                ParticipantLifecycleResult value => value.Succeeded,
                _ => true
            };
            var error = result switch
            {
                AdminParticipantResult value => value.Error,
                EventAccountMutationResult value => value.Error,
                ParticipantQueueMutationResult value => value.Error,
                ParticipantLifecycleResult value => value.Error,
                _ => null
            };
            Assert.False(succeeded);
            Assert.Contains("version", error, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(beforeAudits, await db.AuditEntries.CountAsync());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ParticipantPageRestoreRejectsStaleObservedVersionsWithoutMutation()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 0);
        await using var pageDb = new ApplicationDbContext(options);
        var http = AdminContext(setup.EnabledAdminId);
        var model = new Bingo.Web.Pages.Admin.Events.ParticipantModel(
            pageDb,
            new EventParticipantCharacterService(pageDb, TimeProvider.System),
            Service(pageDb),
            new PassthroughLocalizer(),
            TimeProvider.System)
        {
            PageContext = new PageContext(new ActionContext(http, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(http, new DictionaryTempDataProvider())
        };
        Assert.IsType<PageResult>(await model.OnGetAsync(setup.EventId, setup.ConfirmedParticipantId, CancellationToken.None));
        var staleEventVersion = model.ExpectedEventVersion;
        var staleResponseVersion = model.Input.ExpectedResponseVersion;
        Assert.NotNull(staleEventVersion);
        Assert.NotNull(staleResponseVersion);

        await using (var withdraw = new ApplicationDbContext(options))
        {
            var result = await Service(withdraw).WithdrawAsync(
                setup.EventId, setup.ConfirmedParticipantId, setup.ConfirmedOwnerId, "owner", false);
            Assert.True(result.Succeeded, result.Error);
        }
        await using (var concurrent = new ApplicationDbContext(options))
        {
            var participant = await concurrent.EventParticipants.SingleAsync(x => x.Id == setup.ConfirmedParticipantId);
            participant.AdvanceResponseVersion();
            await concurrent.SaveChangesAsync();
        }

        model.ConfirmLifecycleAction = true;
        var response = await model.OnPostRestoreAsync(setup.EventId, setup.ConfirmedParticipantId, false, CancellationToken.None);
        Assert.IsType<RedirectToPageResult>(response);
        Assert.Contains("changed", model.TempData["StatusMessage"]?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.admin_restored").ToListAsync());
    }

    [Fact]
    public async Task SelectedWaitingConfirmationUsesOnlyOneExplicitCapacityPlaceAndIsIdempotent()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 2);
        Guid selectedId;
        await using (var lookup = new ApplicationDbContext(options))
            selectedId = await lookup.EventParticipants.Where(x => x.AccountId == setup.WaitingOwnerIds[0]).Select(x => x.Id).SingleAsync();

        await using (var normal = new ApplicationDbContext(options))
        {
            var observed = await normal.EventParticipants.Where(x => x.Id == selectedId)
                .Select(x => new { ResponseVersion = x.ResponseVersion, EventVersion = normal.Events.Where(e => e.Id == setup.EventId).Select(e => e.Version).Single() })
                .SingleAsync();
            var rejected = await Service(normal).ConfirmWaitingParticipantAsync(new(
                setup.EventId, selectedId, setup.EnabledAdminId, "admin",
                ExpectedEventVersion: observed.EventVersion, ExpectedResponseVersion: observed.ResponseVersion));
            Assert.False(rejected.Succeeded);
            Assert.Contains("add-one-place", rejected.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using (var overrideDb = new ApplicationDbContext(options))
        {
            var observed = await overrideDb.EventParticipants.Where(x => x.Id == selectedId)
                .Select(x => new { ResponseVersion = x.ResponseVersion, EventVersion = overrideDb.Events.Where(e => e.Id == setup.EventId).Select(e => e.Version).Single() })
                .SingleAsync();
            var confirmed = await Service(overrideDb).ConfirmWaitingParticipantAsync(new(
                setup.EventId, selectedId, setup.EnabledAdminId, "admin", ExpandCapacityWhenFull: true,
                ExpectedEventVersion: observed.EventVersion, ExpectedResponseVersion: observed.ResponseVersion));
            Assert.True(confirmed.Succeeded, confirmed.Error);
            Assert.True(confirmed.Changed);
            Assert.Equal(SignupStatus.Confirmed, confirmed.Status);
            Assert.Equal(2, confirmed.EffectiveParticipantCap);
        }

        await using (var retryDb = new ApplicationDbContext(options))
        {
            var observed = await retryDb.EventParticipants.Where(x => x.Id == selectedId)
                .Select(x => new { ResponseVersion = x.ResponseVersion, EventVersion = retryDb.Events.Where(e => e.Id == setup.EventId).Select(e => e.Version).Single() })
                .SingleAsync();
            var retry = await Service(retryDb).ConfirmWaitingParticipantAsync(new(
                setup.EventId, selectedId, setup.EnabledAdminId, "admin", ExpandCapacityWhenFull: true,
                ExpectedEventVersion: observed.EventVersion, ExpectedResponseVersion: observed.ResponseVersion));
            Assert.True(retry.Succeeded, retry.Error);
            Assert.False(retry.Changed);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(2, await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.ParticipantCap).SingleAsync());
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == selectedId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Equal(1, await verify.EventParticipants.CountAsync(x => x.EventId == setup.EventId && x.SignupStatus == SignupStatus.WaitingList));
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.TargetId == selectedId.ToString() && x.Action == "participant.admin_confirmed").ToListAsync());
        var promotionNotifications = await verify.PersonalNotifications.Where(x => x.EventId == setup.EventId && x.Title == "participant.promoted").ToListAsync();
        Assert.Equal(3, promotionNotifications.Count);
        Assert.Single(promotionNotifications, x => x.RecipientAccountId == setup.WaitingOwnerIds[0]);
        Assert.Contains(promotionNotifications, x => x.RecipientAccountId == setup.EnabledAdminId);
        Assert.Contains(promotionNotifications, x => x.RecipientAccountId == setup.EnabledSuperAdminId);
        Assert.DoesNotContain(promotionNotifications, x => x.RecipientAccountId == setup.DisabledAdminId || x.RecipientAccountId == setup.UnrelatedUserId);
    }

    [Fact]
    public async Task ConcurrentSelectedConfirmationHasOneWinnerAndOneAudit()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 1);
        Guid selectedId;
        long eventVersion;
        int responseVersion;
        await using (var lookup = new ApplicationDbContext(options))
        {
            selectedId = await lookup.EventParticipants.Where(x => x.AccountId == setup.WaitingOwnerIds[0]).Select(x => x.Id).SingleAsync();
            eventVersion = await lookup.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync();
            responseVersion = await lookup.EventParticipants.Where(x => x.Id == selectedId).Select(x => x.ResponseVersion).SingleAsync();
        }

        async Task<ParticipantQueueMutationResult> ConfirmAsync()
        {
            await using var db = new ApplicationDbContext(options);
            return await Service(db).ConfirmWaitingParticipantAsync(new(
                setup.EventId, selectedId, setup.EnabledAdminId, "admin", ExpandCapacityWhenFull: true,
                ExpectedEventVersion: eventVersion, ExpectedResponseVersion: responseVersion));
        }

        var results = await Task.WhenAll(ConfirmAsync(), ConfirmAsync());
        Assert.Single(results, result => result.Succeeded && result.Changed);
        Assert.Single(results, result => !result.Succeeded || !result.Changed);

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(2, await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.ParticipantCap).SingleAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.admin_confirmed").ToListAsync());
        var promotionNotifications = await verify.PersonalNotifications.Where(x => x.EventId == setup.EventId && x.Title == "participant.promoted").ToListAsync();
        Assert.Equal(3, promotionNotifications.Count);
        Assert.Contains(promotionNotifications, x => x.RecipientAccountId == setup.WaitingOwnerIds[0]);
        Assert.Contains(promotionNotifications, x => x.RecipientAccountId == setup.EnabledAdminId);
        Assert.Contains(promotionNotifications, x => x.RecipientAccountId == setup.EnabledSuperAdminId);
        Assert.DoesNotContain(promotionNotifications, x => x.RecipientAccountId == setup.DisabledAdminId || x.RecipientAccountId == setup.UnrelatedUserId);
    }

    [Fact]
    public async Task MovingConfirmedParticipantAppendsItAndPromotesThePreExistingWaiterWithMembershipHistory()
    {
        var setup = await SeedAsync(capacity: 1, confirmed: 1, waiting: 1);
        var now = DateTimeOffset.UtcNow;
        var team = new Team(Guid.NewGuid(), setup.EventId, "Draft team", "draft-team", TeamFormationType.Drafted, null, true, now);
        TeamMembership membership;
        await using (var seed = new ApplicationDbContext(options))
        {
            membership = new TeamMembership(Guid.NewGuid(), team.Id, setup.ConfirmedParticipantId, TeamMembershipRole.Captain, now, null, "fixture");
            seed.AddRange(team, membership);
            await seed.SaveChangesAsync();
        }

        Guid waiterId;
        await using (var lookup = new ApplicationDbContext(options))
            waiterId = await lookup.EventParticipants.Where(x => x.AccountId == setup.WaitingOwnerIds[0]).Select(x => x.Id).SingleAsync();

        await using (var db = new ApplicationDbContext(options))
        {
            var observed = await db.EventParticipants.Where(x => x.Id == setup.ConfirmedParticipantId)
                .Select(x => new { ResponseVersion = x.ResponseVersion, EventVersion = db.Events.Where(e => e.Id == setup.EventId).Select(e => e.Version).Single() })
                .SingleAsync();
            var result = await Service(db).MoveConfirmedParticipantToWaitingAsync(new(
                setup.EventId, setup.ConfirmedParticipantId, setup.EnabledAdminId, "admin",
                ExpectedEventVersion: observed.EventVersion, ExpectedResponseVersion: observed.ResponseVersion));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(waiterId, result.PromotedParticipantId);
            Assert.Equal(1, result.WaitingPosition);
        }

        await using var verify = new ApplicationDbContext(options);
        var moved = await verify.EventParticipants.SingleAsync(x => x.Id == setup.ConfirmedParticipantId);
        var promoted = await verify.EventParticipants.SingleAsync(x => x.Id == waiterId);
        Assert.Equal(SignupStatus.WaitingList, moved.SignupStatus);
        Assert.Equal(SignupStatus.Confirmed, promoted.SignupStatus);
        Assert.NotNull(moved.WaitingListedAt);
        Assert.All(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == moved.Id).ToListAsync(), x => Assert.Null(x.ReleasedAt));
        var ended = await verify.TeamMemberships.SingleAsync(x => x.Id == membership.Id);
        Assert.NotNull(ended.LeftAt);
        Assert.Equal(TeamMembershipRole.Participant, ended.Role);
        Assert.Single(await verify.TeamMembershipRoleTransitions.Where(x => x.TeamMembershipId == membership.Id).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.admin_moved_to_waiting" && x.TargetId == moved.Id.ToString()).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "participant.promoted" && x.TargetId == promoted.Id.ToString()).ToListAsync());
        var promotionNotifications = await verify.PersonalNotifications.Where(x => x.EventId == setup.EventId && x.Title == "participant.promoted").ToListAsync();
        Assert.Equal(3, promotionNotifications.Count);
        Assert.Single(promotionNotifications, x => x.RecipientAccountId == setup.WaitingOwnerIds[0]);
        Assert.Contains(promotionNotifications, x => x.RecipientAccountId == setup.EnabledAdminId);
        Assert.Contains(promotionNotifications, x => x.RecipientAccountId == setup.EnabledSuperAdminId);
        Assert.DoesNotContain(promotionNotifications, x => x.RecipientAccountId == setup.DisabledAdminId || x.RecipientAccountId == setup.UnrelatedUserId);
    }

    [Fact]
    public async Task SavedAddAndEventOnlyAccountMutationsPreserveGlobalLinksAndPrimaryMapping()
    {
        var now = new DateTimeOffset(2026, 9, 30, 20, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);
        var admin = Website($"saved-add-admin-{Guid.NewGuid():N}", now, GlobalRole.Admin);
        var owner = Website($"saved-add-owner-{Guid.NewGuid():N}", now);
        var item = new BingoEvent(Guid.NewGuid(), "Saved add", $"saved-add-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), 2, admin.Id, now);
        item.OpenSignups(now);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Primary", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var second = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "playing_second", "Second Playing", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Playing);
        var custom = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "custom", "Custom", SignupQuestionType.Text, true, 2, null);
        var third = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "playing_third", "Third Playing", SignupQuestionType.Account, false, 3, null, SignupSystemField.None, EventCharacterRole.Playing);
        var firstCharacter = new OsrsCharacter(Guid.NewGuid(), "Saved first", $"SAVED FIRST {Guid.NewGuid():N}", now);
        var secondCharacter = new OsrsCharacter(Guid.NewGuid(), "Saved second", $"SAVED SECOND {Guid.NewGuid():N}", now);
        var correctedCharacter = new OsrsCharacter(Guid.NewGuid(), "Corrected event account", $"CORRECTED EVENT {Guid.NewGuid():N}", now);
        var thirdCharacter = new OsrsCharacter(Guid.NewGuid(), "Saved third", $"SAVED THIRD {Guid.NewGuid():N}", now);
        var firstLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, firstCharacter.Id, owner.Id, true, 0, "first", 11.25m, now);
        var secondLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, secondCharacter.Id, owner.Id, false, 1, "second", 22.5m, now);
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(admin, owner, item, form, primary, second, custom, third, firstCharacter, secondCharacter, correctedCharacter, thirdCharacter, firstLink, secondLink);
            await seed.SaveChangesAsync();
        }

        Guid participantId;
        await using (var db = new ApplicationDbContext(options))
        {
            var eventVersion = await db.Events.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            var result = await Service(db).AddSavedParticipantAsync(new(
                item.Id, owner.Id, admin.Id, admin.LoginName, [firstCharacter.Id], firstCharacter.Id, PaymentStatus.Paid,
                ExpectedEventVersion: eventVersion));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(SignupStatus.Confirmed, result.Status);
            participantId = result.ParticipantId!.Value;
        }

        Guid secondAssignmentId;
        Guid correctedAssignmentId;
        Guid thirdAssignmentId;
        int responseVersion;
        await using (var verify = new ApplicationDbContext(options))
        {
            var participant = await verify.EventParticipants.SingleAsync(x => x.Id == participantId);
            Assert.Equal(SignupSource.AdminCreated, participant.Source);
            Assert.True(participant.PaymentReceived);
            Assert.False(participant.CaptainVolunteer);
            Assert.Equal(1, await verify.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participantId && x.ReleasedAt == null));
            Assert.Equal(11.25m, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId).Select(x => x.EhbSnapshot).SingleAsync());
            Assert.Single(await verify.SignupAnswers.Where(x => x.EventParticipantId == participantId).ToListAsync());
            Assert.Empty(await verify.SignupAnswers.Where(x => x.EventParticipantId == participantId && x.SignupQuestionId == custom.Id).ToListAsync());
            Assert.Equal(11.25m, await verify.AccountOsrsCharacters.Where(x => x.Id == firstLink.Id).Select(x => x.SavedEhb).SingleAsync());
            responseVersion = participant.ResponseVersion;
        }

        await using (var teamDb = new ApplicationDbContext(options))
        {
            var team = new Team(Guid.NewGuid(), item.Id, "Primary agreement team", $"primary-agreement-{Guid.NewGuid():N}", TeamFormationType.Drafted, null, true, now);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, participantId, TeamMembershipRole.Participant, now, null, "primary agreement");
            teamDb.AddRange(team, membership);
            await teamDb.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var added = await Service(db).AddEventParticipantAccountAsync(new(
                item.Id, participantId, secondCharacter.Id, EventCharacterRole.Playing, 22.5m, admin.Id, admin.LoginName, second.Id, responseVersion));
            Assert.True(added.Succeeded, added.Error);
            secondAssignmentId = await db.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId && x.OsrsCharacterId == secondCharacter.Id && x.ReleasedAt == null).Select(x => x.Id).SingleAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == participantId);
            var added = await Service(db).AddEventParticipantAccountAsync(new(
                item.Id, participantId, thirdCharacter.Id, EventCharacterRole.Playing, 18m, admin.Id, admin.LoginName, third.Id, participant.ResponseVersion));
            Assert.True(added.Succeeded, added.Error);
            thirdAssignmentId = await db.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId && x.OsrsCharacterId == thirdCharacter.Id && x.ReleasedAt == null).Select(x => x.Id).SingleAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var question = await db.SignupQuestions.SingleAsync(x => x.Id == third.Id);
            question.Deactivate(admin.Id, now, "Current assignment validation fixture");
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == participantId);
            var switched = await Service(db).SwitchAdminPrimaryAsync(new(item.Id, participantId, secondCharacter.Id, admin.Id, admin.LoginName, firstCharacter.Id, participant.ResponseVersion));
            Assert.True(switched.Succeeded, switched.Error);
            Assert.Equal(secondCharacter.Id, switched.PrimaryCharacterId);
        }

        await using (var agreement = new ApplicationDbContext(options))
        {
            var primaryProjection = await agreement.PrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => new { x.OsrsCharacterId, x.Ehb }).SingleAsync();
            var adminProjection = await agreement.AdminPrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => new { x.OsrsCharacterId, x.Ehb }).SingleAsync();
            Assert.Equal(secondCharacter.Id, primaryProjection.OsrsCharacterId);
            Assert.Equal(22.5m, primaryProjection.Ehb);
            Assert.Equal(secondCharacter.Id, adminProjection.OsrsCharacterId);
            Assert.Equal(22.5m, adminProjection.Ehb);
            var context = await new ParticipantLiveService(agreement, TimeProvider.System).GetContextAsync(item.Id, participantId, owner.Id);
            Assert.NotNull(context);
            Assert.Equal(secondCharacter.DisplayName, context!.PlannedCharacterName);
            Assert.Equal(secondCharacter.DisplayName, context.ActiveCharacterName);
            Assert.Contains(context.PlayingCharacters, character => character.CharacterId == secondCharacter.Id && character.IsActive);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == participantId);
            var accountAuditCount = await db.AuditEntries.CountAsync(x => x.EventId == item.Id && x.Action == "participant.primary_switched");
            var ownerNotificationCount = await db.PersonalNotifications.CountAsync(x => x.EventId == item.Id && x.RecipientAccountId == owner.Id && x.Title == "participant.accounts_changed");
            var mappingsBefore = await db.EventParticipantCharacters
                .Where(x => x.EventParticipantId == participantId && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
                .OrderBy(x => x.RegistrationOrder)
                .Select(x => new { x.OsrsCharacterId, x.SignupQuestionId, x.EhbSnapshot })
                .ToListAsync();
            var answersBefore = await db.SignupAnswers
                .Where(x => x.EventParticipantId == participantId)
                .OrderBy(x => x.SignupQuestionId)
                .Select(x => new { x.SignupQuestionId, x.OsrsCharacterId, x.Value })
                .ToListAsync();
            var primaryBefore = await db.PrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => x.OsrsCharacterId).SingleAsync();
            var responseVersionBefore = participant.ResponseVersion;
            var invalidSlot = await Service(db).SwitchAdminPrimaryAsync(new(item.Id, participantId, thirdCharacter.Id, admin.Id, admin.LoginName, secondCharacter.Id, responseVersionBefore));
            Assert.False(invalidSlot.Succeeded);
            Assert.Contains("active Playing slot", invalidSlot.Error, StringComparison.OrdinalIgnoreCase);
            var unchanged = await Service(db).SwitchAdminPrimaryAsync(new(item.Id, participantId, secondCharacter.Id, admin.Id, admin.LoginName, secondCharacter.Id, participant.ResponseVersion));
            Assert.True(unchanged.Succeeded, unchanged.Error);
            Assert.False(unchanged.Changed);
            var stale = await Service(db).SwitchAdminPrimaryAsync(new(item.Id, participantId, firstCharacter.Id, admin.Id, admin.LoginName, secondCharacter.Id, participant.ResponseVersion - 1));
            Assert.False(stale.Succeeded);
            Assert.Contains("changed", stale.Error, StringComparison.OrdinalIgnoreCase);
            var note = await Service(db).SetAdminNotesAsync(item.Id, participantId, admin.Id, admin.LoginName, "same-context validation recovery", null);
            Assert.True(note.Succeeded, note.Error);
            var mappingsAfter = await db.EventParticipantCharacters
                .Where(x => x.EventParticipantId == participantId && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
                .OrderBy(x => x.RegistrationOrder)
                .Select(x => new { x.OsrsCharacterId, x.SignupQuestionId, x.EhbSnapshot })
                .ToListAsync();
            var answersAfter = await db.SignupAnswers
                .Where(x => x.EventParticipantId == participantId)
                .OrderBy(x => x.SignupQuestionId)
                .Select(x => new { x.SignupQuestionId, x.OsrsCharacterId, x.Value })
                .ToListAsync();
            Assert.Equal(mappingsBefore, mappingsAfter);
            Assert.Equal(answersBefore, answersAfter);
            Assert.Equal(primaryBefore, await db.PrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => x.OsrsCharacterId).SingleAsync());
            Assert.Equal(participant.ResponseVersion, await db.EventParticipants.Where(x => x.Id == participantId).Select(x => x.ResponseVersion).SingleAsync());
            Assert.Equal(responseVersionBefore, participant.ResponseVersion);
            Assert.Equal(accountAuditCount, await db.AuditEntries.CountAsync(x => x.EventId == item.Id && x.Action == "participant.primary_switched"));
            Assert.Equal(ownerNotificationCount, await db.PersonalNotifications.CountAsync(x => x.EventId == item.Id && x.RecipientAccountId == owner.Id && x.Title == "participant.accounts_changed"));
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == participantId);
            var corrected = await Service(db).CorrectEventParticipantAccountAsync(new(item.Id, participantId, secondAssignmentId, correctedCharacter.Id, 33.75m, admin.Id, admin.LoginName, participant.ResponseVersion));
            Assert.True(corrected.Succeeded, corrected.Error);
            correctedAssignmentId = await db.EventParticipantCharacters
                .Where(x => x.EventParticipantId == participantId && x.OsrsCharacterId == correctedCharacter.Id && x.ReleasedAt == null)
                .Select(x => x.Id)
                .SingleAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == participantId);
            var switchedBack = await Service(db).SwitchAdminPrimaryAsync(new(item.Id, participantId, firstCharacter.Id, admin.Id, admin.LoginName, correctedCharacter.Id, participant.ResponseVersion));
            Assert.True(switchedBack.Succeeded, switchedBack.Error);
        }

        await using (var agreement = new ApplicationDbContext(options))
        {
            Assert.Equal(firstCharacter.Id, await agreement.PrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => x.OsrsCharacterId).SingleAsync());
            Assert.Equal(firstCharacter.Id, await agreement.AdminPrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => x.OsrsCharacterId).SingleAsync());
            var context = await new ParticipantLiveService(agreement, TimeProvider.System).GetContextAsync(item.Id, participantId, owner.Id);
            Assert.NotNull(context);
            Assert.Equal(firstCharacter.DisplayName, context!.PlannedCharacterName);
            Assert.Equal(firstCharacter.DisplayName, context.ActiveCharacterName);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == participantId);
            var removed = await Service(db).RemoveEventParticipantAccountAsync(new(item.Id, participantId, correctedAssignmentId, admin.Id, admin.LoginName, participant.ResponseVersion));
            Assert.True(removed.Succeeded, removed.Error);
        }

        await using var final = new ApplicationDbContext(options);
        var savedParticipant = await final.EventParticipants.SingleAsync(x => x.Id == participantId);
        Assert.Equal(SignupStatus.Confirmed, savedParticipant.SignupStatus);
        Assert.Equal(1, savedParticipant.SignupSequence);
        Assert.Equal(firstCharacter.Id, await final.PrimaryCharacters().Where(x => x.ParticipantId == participantId).Select(x => x.OsrsCharacterId).SingleAsync());
        var activeCharacters = await final.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId && x.ReleasedAt == null).Select(x => x.OsrsCharacterId).ToListAsync();
        Assert.Contains(firstCharacter.Id, activeCharacters);
        Assert.Contains(thirdCharacter.Id, activeCharacters);
        var retainedThird = await final.EventParticipantCharacters.SingleAsync(x => x.Id == thirdAssignmentId);
        Assert.Equal(third.Id, retainedThird.SignupQuestionId);
        Assert.Null(retainedThird.ReleasedAt);
        var oldAssignment = await final.EventParticipantCharacters.SingleAsync(x => x.Id == secondAssignmentId);
        var correctedAssignment = await final.EventParticipantCharacters.SingleAsync(x => x.Id == correctedAssignmentId);
        Assert.Equal(secondCharacter.Id, oldAssignment.OsrsCharacterId);
        Assert.Equal(22.5m, oldAssignment.EhbSnapshot);
        Assert.Equal(EhbSource.AdminCorrection, oldAssignment.EhbSource);
        Assert.Equal(correctedCharacter.Id, correctedAssignment.OsrsCharacterId);
        Assert.Equal(33.75m, correctedAssignment.EhbSnapshot);
        Assert.Equal(EhbSource.AdminCorrection, correctedAssignment.EhbSource);
        Assert.NotEqual(oldAssignment.Id, correctedAssignment.Id);
        Assert.NotNull(oldAssignment.ReleasedAt);
        Assert.NotNull(correctedAssignment.ReleasedAt);
        Assert.Equal(6, await final.PersonalNotifications.CountAsync(x => x.EventId == item.Id && x.RecipientAccountId == owner.Id && x.Title == "participant.accounts_changed"));
        Assert.Equal(11.25m, await final.AccountOsrsCharacters.Where(x => x.Id == firstLink.Id).Select(x => x.SavedEhb).SingleAsync());
        Assert.Equal(22.5m, await final.AccountOsrsCharacters.Where(x => x.Id == secondLink.Id).Select(x => x.SavedEhb).SingleAsync());
        Assert.Empty(await final.AccountOsrsCharacters.Where(x => x.AccountId == owner.Id && x.OsrsCharacterId == correctedCharacter.Id).ToListAsync());
        Assert.Empty(await final.SignupAnswers.Where(x => x.EventParticipantId == participantId && x.SignupQuestionId == custom.Id).ToListAsync());
    }

    [Fact]
    public async Task SavedAddRejectsInvalidLinksEhbSlotsPrimaryReservationAndStaleEventWithoutPartialWrites()
    {
        var now = new DateTimeOffset(2026, 9, 30, 20, 0, 0, TimeSpan.Zero);
        var admin = Website($"saved-negative-admin-{Guid.NewGuid():N}", now, GlobalRole.Admin);
        var owner = Website($"saved-negative-owner-{Guid.NewGuid():N}", now);
        var foreignOwner = Website($"saved-negative-foreign-{Guid.NewGuid():N}", now);
        var item = new BingoEvent(Guid.NewGuid(), "Saved negative", $"saved-negative-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), 2, admin.Id, now);
        item.OpenSignups(now);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Primary", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var second = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "playing_second", "Second Playing", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Playing);
        var requiredCustom = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "required_custom", "Required Custom", SignupQuestionType.Text, true, 2, null);
        var firstCharacter = new OsrsCharacter(Guid.NewGuid(), "Negative first", $"NEGATIVE FIRST {Guid.NewGuid():N}", now);
        var missingEhbCharacter = new OsrsCharacter(Guid.NewGuid(), "Negative missing EHB", $"NEGATIVE MISSING {Guid.NewGuid():N}", now);
        var inactiveCharacter = new OsrsCharacter(Guid.NewGuid(), "Negative inactive", $"NEGATIVE INACTIVE {Guid.NewGuid():N}", now);
        var foreignCharacter = new OsrsCharacter(Guid.NewGuid(), "Negative foreign", $"NEGATIVE FOREIGN {Guid.NewGuid():N}", now);
        var reservedCharacter = new OsrsCharacter(Guid.NewGuid(), "Negative reserved", $"NEGATIVE RESERVED {Guid.NewGuid():N}", now);
        var firstLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, firstCharacter.Id, owner.Id, true, 0, "first", 11.25m, now);
        var missingEhbLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, missingEhbCharacter.Id, owner.Id, false, 1, "missing", null, now);
        var inactiveLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, inactiveCharacter.Id, owner.Id, false, 2, "inactive", 13.5m, now);
        inactiveLink.Unlink(now);
        var foreignLink = new AccountOsrsCharacter(Guid.NewGuid(), foreignOwner.Id, foreignCharacter.Id, foreignOwner.Id, true, 0, "foreign", 14m, now);
        var reservedLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, reservedCharacter.Id, owner.Id, false, 3, "reserved", 15m, now);
        var reservedOwner = Website($"saved-negative-reserver-{Guid.NewGuid():N}", now);
        var reservedParticipant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        reservedParticipant.AssignOwner(reservedOwner);
        var reservedAssignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, reservedParticipant.Id, reservedCharacter.Id, 0, now, reservedOwner.Id, primary.Id, EventCharacterRole.Playing, 15m, EhbSource.Manual, null);
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(admin, owner, foreignOwner, reservedOwner, item, form, primary, second, requiredCustom,
                firstCharacter, missingEhbCharacter, inactiveCharacter, foreignCharacter, reservedCharacter,
                firstLink, missingEhbLink, inactiveLink, foreignLink, reservedLink, reservedParticipant, reservedAssignment);
            await seed.SaveChangesAsync();
        }

        async Task<AdminParticipantResult> TryAdd(params Guid[] ids)
        {
            await using var db = new ApplicationDbContext(options);
            var eventVersion = await db.Events.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            return await Service(db).AddSavedParticipantAsync(new(
                item.Id, owner.Id, admin.Id, admin.LoginName, ids, ids[0], ExpectedEventVersion: eventVersion));
        }

        var foreign = await TryAdd(foreignCharacter.Id);
        Assert.False(foreign.Succeeded);
        Assert.Contains("saved link", foreign.Error, StringComparison.OrdinalIgnoreCase);
        var inactive = await TryAdd(inactiveCharacter.Id);
        Assert.False(inactive.Succeeded);
        Assert.Contains("saved link", inactive.Error, StringComparison.OrdinalIgnoreCase);
        var missingEhb = await TryAdd(missingEhbCharacter.Id);
        Assert.False(missingEhb.Succeeded);
        Assert.Contains("EHB", missingEhb.Error, StringComparison.OrdinalIgnoreCase);
        await using var duplicateDb = new ApplicationDbContext(options);
        var duplicateEventVersion = await duplicateDb.Events.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
        var duplicate = await Service(duplicateDb).AddSavedParticipantAsync(new(
            item.Id, owner.Id, admin.Id, admin.LoginName, [firstCharacter.Id, firstCharacter.Id], firstCharacter.Id,
            ExpectedEventVersion: duplicateEventVersion));
        Assert.False(duplicate.Succeeded);
        Assert.Contains("once", duplicate.Error, StringComparison.OrdinalIgnoreCase);
        var overSlot = await TryAdd(firstCharacter.Id, missingEhbCharacter.Id, inactiveCharacter.Id);
        Assert.False(overSlot.Succeeded);
        Assert.Contains("slots", overSlot.Error, StringComparison.OrdinalIgnoreCase);

        // The schema enforces one active PrimaryRegularAccount question per
        // form, so exercise the same actionable validation with a separate
        // form that has no primary question instead of manufacturing an
        // impossible duplicate row.
        var noPrimaryEvent = new BingoEvent(Guid.NewGuid(), "Saved no primary", $"saved-no-primary-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), 1, admin.Id, now);
        noPrimaryEvent.OpenSignups(now);
        var noPrimaryForm = new SignupForm(Guid.NewGuid(), noPrimaryEvent.Id, now);
        var noPrimaryText = new SignupQuestion(Guid.NewGuid(), noPrimaryForm.Id, noPrimaryEvent.Id, "required_text", "Required text", SignupQuestionType.Text, true, 0, null);
        await using (var noPrimarySeed = new ApplicationDbContext(options))
        {
            noPrimarySeed.AddRange(noPrimaryEvent, noPrimaryForm, noPrimaryText);
            await noPrimarySeed.SaveChangesAsync();
        }
        await using (var noPrimaryDb = new ApplicationDbContext(options))
        {
            var noPrimaryEventVersion = await noPrimaryDb.Events.Where(x => x.Id == noPrimaryEvent.Id).Select(x => x.Version).SingleAsync();
            var noPrimaryResult = await Service(noPrimaryDb).AddSavedParticipantAsync(new(
                noPrimaryEvent.Id, owner.Id, admin.Id, admin.LoginName, [firstCharacter.Id], firstCharacter.Id,
                ExpectedEventVersion: noPrimaryEventVersion));
            Assert.False(noPrimaryResult.Succeeded);
            Assert.Contains("unambiguous", noPrimaryResult.Error, StringComparison.OrdinalIgnoreCase);
        }

        var reservation = await TryAdd(reservedCharacter.Id);
        Assert.False(reservation.Succeeded);
        Assert.Contains("already registered", reservation.Error, StringComparison.OrdinalIgnoreCase);
        long staleVersion;
        await using (var mutate = new ApplicationDbContext(options))
        {
            staleVersion = await mutate.Events.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            var current = await mutate.Events.SingleAsync(x => x.Id == item.Id);
            current.SetParticipantCap(3);
            current.AdvanceVersion();
            await mutate.SaveChangesAsync();
        }
        var stale = await Service(new ApplicationDbContext(options)).AddSavedParticipantAsync(new(
            item.Id, owner.Id, admin.Id, admin.LoginName, [firstCharacter.Id], firstCharacter.Id,
            ExpectedEventVersion: staleVersion));
        Assert.False(stale.Succeeded);
        Assert.Contains("changed", stale.Error, StringComparison.OrdinalIgnoreCase);

        var successful = await TryAdd(firstCharacter.Id);
        Assert.True(successful.Succeeded, successful.Error);
        await using var verify = new ApplicationDbContext(options);
        Assert.Single(await verify.EventParticipants.Where(x => x.EventId == item.Id && x.AccountId == owner.Id).ToListAsync());
        Assert.Empty(await verify.SignupAnswers.Where(x => x.EventParticipantId == successful.ParticipantId && x.SignupQuestionId == requiredCustom.Id).ToListAsync());
        Assert.Equal(11.25m, await verify.AccountOsrsCharacters.Where(x => x.Id == firstLink.Id).Select(x => x.SavedEhb).SingleAsync());
    }

    [Fact]
    public async Task SameCharacterEhbCorrectionAppendsHistoryAndRepeatIsNoOp()
    {
        var now = new DateTimeOffset(2026, 9, 30, 20, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);
        var fetchedAt = now.AddHours(-2);
        var admin = Website($"same-character-admin-{Guid.NewGuid():N}", now, GlobalRole.Admin);
        var owner = Website($"same-character-owner-{Guid.NewGuid():N}", now);
        var item = new BingoEvent(Guid.NewGuid(), "Same character correction", $"same-character-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), 1, admin.Id, now);
        item.OpenSignups(now);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Primary", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var character = new OsrsCharacter(Guid.NewGuid(), "Same character", $"SAME CHARACTER {Guid.NewGuid():N}", now);
        var savedLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, "saved", 17.25m, now);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        participant.AssignOwner(owner);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, 0, now.AddMinutes(-1), owner.Id, primary.Id, EventCharacterRole.Playing, 20.13m, EhbSource.WiseOldMan, fetchedAt);
        var answer = new SignupAnswer(Guid.NewGuid(), participant.Id, primary.Id, primary.Label, string.Empty, character.Id);
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(admin, owner, item, form, primary, character, savedLink, participant, assignment, answer);
            await seed.SaveChangesAsync();
        }

        Guid replacementId;
        int responseVersion;
        await using (var db = new ApplicationDbContext(options))
        {
            var result = await new SignupService(db, new SecretHasher(), new FixedTimeProvider(now), accountValidation: new SuccessfulWiseOldManAccountValidation())
                .CorrectEventParticipantAccountAsync(new(item.Id, participant.Id, assignment.Id, character.Id, 21.5m, admin.Id, admin.LoginName, participant.ResponseVersion));
            Assert.True(result.Succeeded, result.Error);
            Assert.True(result.Changed);
            replacementId = await db.EventParticipantCharacters
                .Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null)
                .Select(x => x.Id)
                .SingleAsync();
            responseVersion = await db.EventParticipants.Where(x => x.Id == participant.Id).Select(x => x.ResponseVersion).SingleAsync();
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            var assignments = await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id).OrderBy(x => x.RegistrationOrder).ToListAsync();
            Assert.Equal(2, assignments.Count);
            var old = Assert.Single(assignments, x => x.Id == assignment.Id);
            var replacement = Assert.Single(assignments, x => x.Id == replacementId);
            Assert.Equal(character.Id, old.OsrsCharacterId);
            Assert.Equal(20.13m, old.EhbSnapshot);
            Assert.Equal(EhbSource.WiseOldMan, old.EhbSource);
            Assert.Equal(fetchedAt, old.EhbFetchedAt);
            Assert.NotNull(old.ReleasedAt);
            Assert.Equal(character.Id, replacement.OsrsCharacterId);
            Assert.Equal(21.5m, replacement.EhbSnapshot);
            Assert.Equal(EhbSource.AdminCorrection, replacement.EhbSource);
            Assert.Null(replacement.EhbFetchedAt);
            Assert.Null(replacement.ReleasedAt);
            var audit = await verify.AuditEntries.SingleAsync(x => x.EventId == item.Id && x.Action == "participant.event_account_corrected");
            Assert.Contains("20.13", audit.BeforeState, StringComparison.Ordinal);
            Assert.Contains("WiseOldMan", audit.BeforeState, StringComparison.Ordinal);
            using var beforeState = System.Text.Json.JsonDocument.Parse(audit.BeforeState!);
            Assert.Equal(fetchedAt, beforeState.RootElement.GetProperty("ehbFetchedAt").GetDateTimeOffset());
            Assert.Contains("21.5", audit.AfterState, StringComparison.Ordinal);
            Assert.Contains("AdminCorrection", audit.AfterState, StringComparison.Ordinal);
            Assert.Contains(replacementId.ToString(), audit.AfterState, StringComparison.Ordinal);
            Assert.Equal(17.25m, await verify.AccountOsrsCharacters.Where(x => x.Id == savedLink.Id).Select(x => x.SavedEhb).SingleAsync());
            Assert.True(await verify.AccountOsrsCharacters.Where(x => x.Id == savedLink.Id).Select(x => x.Active).SingleAsync());
            Assert.Equal(character.Id, await verify.SignupAnswers.Where(x => x.Id == answer.Id).Select(x => x.OsrsCharacterId).SingleAsync());
            Assert.Single(await verify.PersonalNotifications.Where(x => x.EventId == item.Id && x.RecipientAccountId == owner.Id && x.Title == "participant.accounts_changed").ToListAsync());
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var repeat = await new SignupService(db, new SecretHasher(), new FixedTimeProvider(now), accountValidation: new SuccessfulWiseOldManAccountValidation())
                .CorrectEventParticipantAccountAsync(new(item.Id, participant.Id, replacementId, character.Id, 21.5m, admin.Id, admin.LoginName, responseVersion));
            Assert.True(repeat.Succeeded, repeat.Error);
            Assert.False(repeat.Changed);
        }

        await using var final = new ApplicationDbContext(options);
        Assert.Equal(2, await final.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id));
        Assert.Single(await final.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).ToListAsync());
        Assert.Single(await final.AuditEntries.Where(x => x.EventId == item.Id && x.Action == "participant.event_account_corrected").ToListAsync());
        Assert.Single(await final.PersonalNotifications.Where(x => x.EventId == item.Id && x.RecipientAccountId == owner.Id && x.Title == "participant.accounts_changed").ToListAsync());
        Assert.Equal(17.25m, await final.AccountOsrsCharacters.Where(x => x.Id == savedLink.Id).Select(x => x.SavedEhb).SingleAsync());
    }

    private async Task<Setup> SeedAsync(int capacity, int confirmed, int waiting)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var suffix = Guid.NewGuid().ToString("N");
        var enabledAdmin = Website($"enabled-admin-{suffix}", now, GlobalRole.Admin);
        var enabledSuperAdmin = Website($"enabled-super-admin-{suffix}", now, GlobalRole.SuperAdmin);
        var disabledAdmin = Website($"disabled-admin-{suffix}", now, GlobalRole.Admin); disabledAdmin.Disable(now);
        var unrelated = Website($"unrelated-user-{suffix}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Lifecycle", $"lifecycle-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), capacity, enabledAdmin.Id, now);
        bingoEvent.OpenSignups(now);
        db.AddRange(enabledAdmin, enabledSuperAdmin, disabledAdmin, unrelated, bingoEvent);
        var owners = new List<Guid>(); var participantIds = new List<Guid>();
        for (var index = 0; index < confirmed + waiting; index++)
        {
            var owner = Website($"owner-{Guid.NewGuid():N}", now); var status = index < confirmed ? SignupStatus.Confirmed : SignupStatus.WaitingList;
            var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, status, index + 1, now.AddMinutes(index), SignupSource.Website, null); participant.AssignOwner(owner);
            var character = new OsrsCharacter(Guid.NewGuid(), $"Lifecycle {index}", $"LIFECYCLE {Guid.NewGuid():N}", now);
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, participant.Id, character.Id, 0, now, owner.Id, null, EventCharacterRole.Playing, 10 + index, EhbSource.Manual, null);
            db.AddRange(owner, participant, character, assignment); owners.Add(owner.Id); participantIds.Add(participant.Id);
        }
        await db.SaveChangesAsync();
        return new Setup(bingoEvent.Id, participantIds[0], owners[0], owners.Skip(confirmed).ToList(), enabledAdmin.Id, enabledSuperAdmin.Id, disabledAdmin.Id, unrelated.Id);
    }

    private async Task<Guid> CharacterIdAsync(Guid participantId)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId).Select(x => x.OsrsCharacterId).SingleAsync();
    }

    private async Task ClaimCharacterAsync(Guid eventId, Guid characterId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"{name}-{Guid.NewGuid():N}", now);
        var participant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 99, now, SignupSource.Website, null); participant.AssignOwner(owner);
        db.AddRange(owner, participant, new EventParticipantCharacter(Guid.NewGuid(), eventId, participant.Id, characterId, 0, now, owner.Id, null, EventCharacterRole.Playing, 10, EhbSource.Manual, null));
        await db.SaveChangesAsync();
    }

    private static SignupService Service(ApplicationDbContext db) =>
        new(db, new SecretHasher(), TimeProvider.System, accountValidation: new SuccessfulWiseOldManAccountValidation());
    private static DefaultHttpContext AdminContext(Guid accountId) => new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, accountId.ToString()), new Claim(ClaimTypes.Name, "admin")], "test")) };
    private static Account Website(string name, DateTimeOffset now, GlobalRole role = GlobalRole.User) { var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now); account.SetGlobalRole(role); return account; }
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed record Setup(Guid EventId, Guid ConfirmedParticipantId, Guid ConfirmedOwnerId, IReadOnlyList<Guid> WaitingOwnerIds, Guid EnabledAdminId, Guid EnabledSuperAdminId, Guid DisabledAdminId, Guid UnrelatedUserId);
    private sealed class ThrowOnCapacityAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == "event.signup_administration_updated")
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated capacity audit persistence failure."))
                : ValueTask.FromResult(result);
    }
    private sealed class PassthroughLocalizer : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
