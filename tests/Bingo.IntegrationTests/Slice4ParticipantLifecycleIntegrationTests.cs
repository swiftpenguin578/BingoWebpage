using System.Security.Claims;
using Bingo.Application.Access;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
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
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice4ParticipantLifecycleIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice4_participant_lifecycle")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
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
        var confirmation = new Bingo.Web.Pages.Events.ConfirmationModel(verify, TimeProvider.System, Service(verify), null!)
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
    public async Task PreformedRosterMembersStayOutOfSignupCountsAndPromotionWithoutExcludingAdminSignups()
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
            var result = await Service(db).UpdateSignupAdministrationAsync(setup.EventId, version, 2, true, setup.EnabledAdminId, "admin");
            Assert.True(result.Succeeded);
            Assert.Equal(0, result.PromotedParticipants);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var version = await db.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync();
            var result = await Service(db).UpdateSignupAdministrationAsync(setup.EventId, version, 3, true, setup.EnabledAdminId, "admin");
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
        Assert.Equal(3, model.Event!.Confirmed);
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

    private static SignupService Service(ApplicationDbContext db) => new(db, new SecretHasher(), TimeProvider.System);
    private static DefaultHttpContext AdminContext(Guid accountId) => new() { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, accountId.ToString()), new Claim(ClaimTypes.Name, "admin")], "test")) };
    private static Account Website(string name, DateTimeOffset now, GlobalRole role = GlobalRole.User) { var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now); account.SetGlobalRole(role); return account; }
    private sealed record Setup(Guid EventId, Guid ConfirmedParticipantId, Guid ConfirmedOwnerId, IReadOnlyList<Guid> WaitingOwnerIds, Guid EnabledAdminId, Guid EnabledSuperAdminId, Guid DisabledAdminId, Guid UnrelatedUserId);
    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
