using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Auditing;
using Bingo.Application.Evidence;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.Events;
using Bingo.Web.Navigation;
using Bingo.Web.Security;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice1IdentityIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice1_identity_tests")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly PasswordHasher<Account> passwords = new();
    private readonly TimeProvider time = TimeProvider.System;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public void LoginThrottlesIdentifierAndNetworkIndependently()
    {
        var throttles = new LoginThrottleService(time);
        for (var index = 0; index < 5; index++) throttles.RecordFailure("ALICE", "127.0.0.1");
        Assert.True(throttles.IsBlocked("ALICE", "10.0.0.2"));
        Assert.True(throttles.IsBlocked("BOB", "127.0.0.1"));
        throttles.Clear("ALICE");
        Assert.False(throttles.IsBlocked("ALICE", "10.0.0.2"));
        Assert.True(throttles.IsBlocked("BOB", "127.0.0.1"));
    }

    [Fact]
    public void DiscordLinkIntentRejectsDirectBypassMismatchesAndReplay()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var intents = new DiscordLinkStateService(cache, time);
        var accountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var state = intents.Create(accountId, "link");

        Assert.False(intents.TryConsumeForChallenge(null, accountId, "link"));
        Assert.False(intents.TryConsumeForChallenge(state, otherAccountId, "link"));
        Assert.False(intents.TryConsumeForChallenge(state, accountId, "replace"));
        Assert.True(intents.TryConsumeForChallenge(state, accountId, "link"));
        Assert.False(intents.TryConsumeForChallenge(state, accountId, "link"));
        Assert.False(intents.TryConsumeForCallback(state, otherAccountId, "link"));
        Assert.True(intents.TryConsumeForCallback(state, accountId, "link"));
        Assert.False(intents.TryConsumeForCallback(state, accountId, "link"));
    }

    [Fact]
    public async Task DiscordLinkAndReplaceRejectCollisionsAndInvalidateExistingPrincipals()
    {
        await using var db = new ApplicationDbContext(options);
        var first = Website("slice1-discord-first");
        var second = Website("slice1-discord-second");
        db.Accounts.AddRange(first, second);
        await db.SaveChangesAsync();
        var identities = new AccountIdentityService(db, passwords, time);
        var authentication = new AccountAuthenticationService(db, passwords, time);
        var oldPrincipal = authentication.CreatePrincipal(first, "discord");

        await identities.SetDiscordAsync(first, "discord-1", "First", "linked", CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => identities.SetDiscordAsync(second, "discord-1", "Second", "linked", CancellationToken.None));
        await identities.SetDiscordAsync(first, "discord-2", "First replacement", "replaced", CancellationToken.None);

        Assert.Equal("discord-2", first.DiscordUserId);
        Assert.NotEqual(oldPrincipal.FindFirstValue(Bingo.Application.Access.AccountClaims.AuthorizationVersion), first.AuthorizationVersion.ToString(CultureInfo.InvariantCulture));
        var transitions = await db.AccountDiscordIdentityTransitions.Where(x => x.AccountId == first.Id).OrderBy(x => x.OccurredAt).ToListAsync();
        Assert.Collection(transitions,
            item => Assert.Equal("linked", item.Action),
            item => Assert.Equal("replaced", item.Action));
        Assert.Equal("discord-1", transitions[1].PreviousDiscordUserId);
        Assert.Equal("discord-2", transitions[1].NextDiscordUserId);
    }

    [Fact]
    public async Task DiscordCallbackConsumesReplacePurposeAndDoesNotMutateOnCollision()
    {
        await using var db = new ApplicationDbContext(options);
        var account = Website("slice1-callback-account");
        var occupied = Website("slice1-callback-occupied");
        db.Accounts.AddRange(account, occupied);
        await db.SaveChangesAsync();
        await new AccountIdentityService(db, passwords, time).SetDiscordAsync(account, "discord-old", "Old", "linked", CancellationToken.None);
        await new AccountIdentityService(db, passwords, time).SetDiscordAsync(occupied, "discord-occupied", "Occupied", "linked", CancellationToken.None);
        var authentication = new AccountAuthenticationService(db, passwords, time);
        var identities = new AccountIdentityService(db, passwords, time);

        var success = CallbackContext(account.Id, "discord-new", "replace");
        var successModel = CallbackModel(db, authentication, identities, success.Context, success.LinkState);
        var successResult = await successModel.OnGetAsync(null, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(successResult);
        Assert.Equal("discord-new", await db.Accounts.AsNoTracking().Where(item => item.Id == account.Id).Select(item => item.DiscordUserId).SingleAsync());
        Assert.NotNull(success.Authentication.LastSignedInPrincipal);
        Assert.Equal("replaced", await db.AccountDiscordIdentityTransitions.OrderByDescending(item => item.OccurredAt).Select(item => item.Action).FirstAsync());

        var collision = CallbackContext(account.Id, "discord-occupied", "replace");
        var collisionModel = CallbackModel(db, authentication, identities, collision.Context, collision.LinkState);
        var collisionResult = await collisionModel.OnGetAsync(null, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(collisionResult);
        Assert.Equal("discord-new", await db.Accounts.AsNoTracking().Where(item => item.Id == account.Id).Select(item => item.DiscordUserId).SingleAsync());
        Assert.Equal("That Discord account is already linked.", collisionModel.TempData["StatusMessage"]?.ToString());
    }

    [Fact]
    public void ProtectedOnboardingStateSurvivesValidationAndRejectsExpiryAndReplay()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var stateService = new DiscordOnboardingStateService(new EphemeralDataProtectionProvider(), cache, clock);
        var issue = new DefaultHttpContext();
        stateService.Issue(issue.Response, "discord-onboarding", "Display");
        var setCookie = issue.Response.Headers.SetCookie.Single();
        var request = new DefaultHttpContext(); request.Request.Headers.Cookie = setCookie!.Split(';')[0];
        Assert.True(stateService.TryRead(request.Request, out var state)); // GET and invalid POST both retain the proof.
        Assert.Equal("discord-onboarding", state.DiscordUserId);
        Assert.True(stateService.TryRead(request.Request, out _));
        stateService.Consume(new DefaultHttpContext().Response, state);
        Assert.False(stateService.TryRead(request.Request, out _));

        var expiredIssue = new DefaultHttpContext(); stateService.Issue(expiredIssue.Response, "discord-expired", null);
        var expiredRequest = new DefaultHttpContext(); expiredRequest.Request.Headers.Cookie = expiredIssue.Response.Headers.SetCookie.Single()!.Split(';')[0];
        clock.Set(clock.GetUtcNow().AddMinutes(15));
        Assert.False(stateService.TryRead(expiredRequest.Request, out _));
    }

    [Fact]
    public async Task DiscordCallbackOnboardingJourneyRetainsValidationStateThenConsumesIt()
    {
        await using var db = new ApplicationDbContext(options);
        var occupied = Website("slice1-onboarding-taken");
        db.Accounts.Add(occupied);
        await db.SaveChangesAsync();
        var occupiedId = occupied.Id;
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var onboarding = new DiscordOnboardingStateService(new EphemeralDataProtectionProvider(), cache, clock);
        var external = new RecordingAuthenticationService("slice1-onboarding-discord", null, Guid.Empty, null);
        var callbackContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(external).BuildServiceProvider() };
        var callback = new Bingo.Web.Pages.Account.DiscordCallbackModel(db, new AccountAuthenticationService(db, passwords, clock), new AccountIdentityService(db, passwords, clock), new DiscordLinkStateService(cache, clock), onboarding, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.DiscordCallbackModel>.Instance)
        {
            PageContext = new PageContext(new ActionContext(callbackContext, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(callbackContext, new DictionaryTempDataProvider())
        };
        Assert.IsType<RedirectToPageResult>(await callback.OnGetAsync(null, CancellationToken.None));
        var request = new DefaultHttpContext { RequestServices = callbackContext.RequestServices };
        request.Request.Headers.Cookie = callbackContext.Response.Headers.SetCookie.Single()!.Split(';')[0];
        var page = new Bingo.Web.Pages.Account.OnboardingModel(new AccountIdentityService(db, passwords, clock), new AccountAuthenticationService(db, passwords, clock), onboarding, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.OnboardingModel>.Instance)
        {
            PageContext = new PageContext(new ActionContext(request, new RouteData(), new PageActionDescriptor()))
        };
        Assert.IsType<PageResult>(page.OnGet());
        page.Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { Username = "slice1-onboarding", OsrsCharacterName = "Slice One", Password = "short", ConfirmPassword = "short" };
        Assert.IsType<PageResult>(await page.OnPostAsync(CancellationToken.None));
        Assert.True(onboarding.TryRead(request.Request, out _));
        page.ModelState.Clear(); page.Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { Username = "slice1-onboarding-taken", OsrsCharacterName = "Slice One", Password = "long-onboarding-password", ConfirmPassword = "long-onboarding-password" };
        Assert.IsType<PageResult>(await page.OnPostAsync(CancellationToken.None));
        Assert.Equal("slice1-onboarding-taken", page.Input.Username);
        Assert.Equal("long-onboarding-password", page.Input.Password);
        Assert.Equal("long-onboarding-password", page.Input.ConfirmPassword);
        Assert.True(onboarding.TryRead(request.Request, out _));
        var unchanged = await db.Accounts.AsNoTracking().SingleAsync(account => account.Id == occupiedId);
        Assert.Equal("slice1-onboarding-taken", unchanged.LoginName);
        Assert.Null(unchanged.DiscordUserId);
        Assert.Single(await db.Accounts.Where(account => account.NormalizedLoginName == "SLICE1-ONBOARDING-TAKEN").ToListAsync());
        Assert.Empty(await db.Accounts.Where(account => account.DiscordUserId == "slice1-onboarding-discord").ToListAsync());

        await using var retryDb = new ApplicationDbContext(options);
        var retry = new Bingo.Web.Pages.Account.OnboardingModel(new AccountIdentityService(retryDb, passwords, clock), new AccountAuthenticationService(retryDb, passwords, clock), onboarding, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.OnboardingModel>.Instance)
        {
            PageContext = new PageContext(new ActionContext(request, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(request, new DictionaryTempDataProvider()),
            Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { Username = "slice1-onboarding", OsrsCharacterName = "Slice One", Password = "long-onboarding-password", ConfirmPassword = "long-onboarding-password" }
        };
        Assert.IsType<RedirectToPageResult>(await retry.OnPostAsync(CancellationToken.None));
        Assert.Single(await db.Accounts.Where(account => account.DiscordUserId == "slice1-onboarding-discord").ToListAsync());
        Assert.Equal(occupiedId, await db.Accounts.Where(account => account.NormalizedLoginName == "SLICE1-ONBOARDING-TAKEN").Select(account => account.Id).SingleAsync());
        Assert.False(onboarding.TryRead(request.Request, out _));
        Assert.IsType<RedirectToPageResult>(await page.OnPostAsync(CancellationToken.None));
        Assert.Single(await db.Accounts.Where(account => account.DiscordUserId == "slice1-onboarding-discord").ToListAsync());
    }

    [Fact]
    public async Task SameAccountSecurityMutationHasOneWinnerAndRejectsTheStaleSession()
    {
        Guid accountId;
        ClaimsPrincipal stale;
        await using (var seed = new ApplicationDbContext(options))
        {
            var account = Website("slice1-version-race"); accountId = account.Id;
            seed.Add(account); await seed.SaveChangesAsync();
            stale = new AccountAuthenticationService(seed, passwords, time).CreatePrincipal(account);
        }
        await using var first = new ApplicationDbContext(options); await using var second = new ApplicationDbContext(options);
        var firstAccount = await first.Accounts.SingleAsync(item => item.Id == accountId);
        var secondAccount = await second.Accounts.SingleAsync(item => item.Id == accountId);
        async Task<bool> Mutate(Func<AccountIdentityService, Account, Task> mutation, ApplicationDbContext context, Account account)
        {
            try { await mutation(new AccountIdentityService(context, new PasswordHasher<Account>(), time), account); return true; }
            catch (DbUpdateConcurrencyException) { return false; }
        }
        var results = await Task.WhenAll(
            Mutate((service, account) => service.SetDiscordAsync(account, "slice1-version-race-discord", "Race", "linked", CancellationToken.None), first, firstAccount),
            Mutate((service, account) => service.ChangePasswordAsync(account, "replacement-race-password", CancellationToken.None), second, secondAccount));
        Assert.Equal(1, results.Count(item => item));
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await ValidateCookieAsync(verify, stale)).Principal);
    }

    [Fact]
    public async Task ConcurrentResetGenerationLeavesExactlyOneUsableCurrentToken()
    {
        Guid ownerId; Guid targetId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var owner = Website("slice1-reset-generation-owner", GlobalRole.SuperAdmin); var target = Website("slice1-reset-generation-target"); ownerId = owner.Id; targetId = target.Id;
            seed.AddRange(owner, target); await seed.SaveChangesAsync();
        }
        async Task<string> Generate()
        {
            await using var attempt = new ApplicationDbContext(options);
            return await new AccountIdentityService(attempt, new PasswordHasher<Account>(), time).GenerateResetLinkAsync(ownerId, targetId, CancellationToken.None);
        }
        var links = await Task.WhenAll(Generate(), Generate());
        await using var verify = new ApplicationDbContext(options);
        var usable = await verify.PasswordCredentialTokens.Where(token => token.AccountId == targetId && token.Purpose == PasswordCredentialTokenPurpose.Reset && token.UsedAt == null && token.SupersededAt == null).ToListAsync();
        Assert.Single(usable);
        Assert.Contains(links, link => AccountIdentityService.Hash(link) == usable[0].TokenHash);
    }

    [Fact]
    public async Task EmergencyCreationIsAuthorizedCutoffBoundAndAtomic()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-emergency-create-admin", GlobalRole.Admin);
        var ev = Event(time.GetUtcNow(), time.GetUtcNow().AddHours(2));
        var team = new Bingo.Domain.Teams.Team(Guid.NewGuid(), ev.Id, "Emergency team", "emergency-team", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
        db.AddRange(admin, ev, team); await db.SaveChangesAsync();
        var service = new EmergencyCredentialService(db, time);
        var account = await service.CreateAsync(admin.Id, "slice1-created-emergency", ev.Id, team.Id, CancellationToken.None);
        var access = await db.AccountEventAccesses.SingleAsync(item => item.AccountId == account.Id);
        Assert.Equal(ev.EventStartsAt, access.ActiveFrom);
        Assert.Equal(ev.SubmissionCutoffAt, access.CorrectionOnlyFrom);
        Assert.Single(await db.AuditEntries.Where(item => item.Action == "account.emergency_created" && item.TargetId == account.Id.ToString()).ToListAsync());
        var expired = Event(time.GetUtcNow(), time.GetUtcNow()); var expiredTeam = new Bingo.Domain.Teams.Team(Guid.NewGuid(), expired.Id, "Expired emergency team", "expired-emergency-team", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
        db.AddRange(expired, expiredTeam); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(admin.Id, "slice1-cutoff-emergency", expired.Id, expiredTeam.Id, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Guid.NewGuid(), "slice1-unauthorized-emergency", ev.Id, team.Id, CancellationToken.None));
    }

    [Fact]
    public async Task EmergencyCreationRollsBackWhenItsAuditCannotPersist()
    {
        Guid adminId; Guid eventId; Guid teamId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var admin = Website("slice1-emergency-rollback-admin", GlobalRole.Admin); var ev = Event(time.GetUtcNow(), time.GetUtcNow().AddHours(2));
            var team = new Bingo.Domain.Teams.Team(Guid.NewGuid(), ev.Id, "Rollback team", "rollback-team", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
            adminId = admin.Id; eventId = ev.Id; teamId = team.Id; seed.AddRange(admin, ev, team); await seed.SaveChangesAsync();
        }
        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnAuditInsert()).Options;
        await using (var failing = new ApplicationDbContext(failingOptions))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new EmergencyCredentialService(failing, time).CreateAsync(adminId, "slice1-emergency-rollback", eventId, teamId, CancellationToken.None));
        await using var verify = new ApplicationDbContext(options);
        Assert.False(await verify.Accounts.AnyAsync(account => account.NormalizedLoginName == "SLICE1-EMERGENCY-ROLLBACK"));
        Assert.Empty(await verify.AccountEventAccesses.Where(access => access.EventId == eventId && access.TeamId == teamId).ToListAsync());
    }

    [Fact]
    public async Task PersonalNotificationsAreRecipientScopedUnreadAndDirectlyReadable()
    {
        await using var db = new ApplicationDbContext(options);
        var recipient = Website("slice1-notification-recipient"); var other = Website("slice1-notification-other");
        var notifications = Enumerable.Range(0, 7).Select(index => new PersonalNotification(Guid.NewGuid(), recipient.Id, "account.admin_granted", string.Empty, "/Account/Settings", time.GetUtcNow().AddMinutes(index))).ToList();
        var notification = notifications[^1];
        db.AddRange(recipient, other); db.PersonalNotifications.AddRange(notifications); await db.SaveChangesAsync();
        var recipientPrincipal = new AccountAuthenticationService(db, passwords, time).CreatePrincipal(recipient);
        var otherPrincipal = new AccountAuthenticationService(db, passwords, time).CreatePrincipal(other);
        var shell = new SharedShellService(db, new PassthroughLocalizer());
        var recipientInbox = await shell.GetNotificationsAsync(recipientPrincipal, CancellationToken.None);
        var otherInbox = await shell.GetNotificationsAsync(otherPrincipal, CancellationToken.None);
        Assert.Equal(7, recipientInbox.Count); Assert.Equal(6, recipientInbox.Items.Count); Assert.Contains($"read={notification.Id}", recipientInbox.Items[0].Url, StringComparison.Ordinal);
        Assert.Equal(0, otherInbox.Count); Assert.Equal("No notifications.", otherInbox.EmptyText); Assert.Equal("/notifications", otherInbox.OverviewUrl);

        var context = new DefaultHttpContext(); context.User = recipientPrincipal;
        var page = new Bingo.Web.Pages.NotificationsModel(db, time, new PassthroughLocalizer()) { PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())) };
        var result = await page.OnGetAsync(notification.Id, CancellationToken.None);
        Assert.IsType<RedirectResult>(result);
        Assert.NotNull((await db.PersonalNotifications.SingleAsync(item => item.Id == notification.Id)).ReadAt);
        var afterRead = await shell.GetNotificationsAsync(recipientPrincipal, CancellationToken.None);
        Assert.Equal(6, afterRead.Count); Assert.Equal(6, afterRead.Items.Count);
    }

    [Fact]
    public async Task SuperAdminUsesTheNormalAdminNotificationViewWithoutGrantingItToOtherAccounts()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-shell-admin", GlobalRole.Admin);
        var owner = Website("slice1-shell-owner", GlobalRole.SuperAdmin);
        var user = Website("slice1-shell-user");
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "slice1-shell-emergency", "SLICE1-SHELL-EMERGENCY", time.GetUtcNow());
        db.AddRange(admin, owner, user, emergency);
        await db.SaveChangesAsync();
        var authentication = new AccountAuthenticationService(db, passwords, time);
        var shell = new SharedShellService(db, new PassthroughLocalizer());

        var adminInbox = await shell.GetNotificationsAsync(authentication.CreatePrincipal(admin), CancellationToken.None);
        var ownerInbox = await shell.GetNotificationsAsync(authentication.CreatePrincipal(owner), CancellationToken.None);
        var userInbox = await shell.GetNotificationsAsync(authentication.CreatePrincipal(user), CancellationToken.None);
        var emergencyInbox = await shell.GetNotificationsAsync(authentication.CreatePrincipal(emergency), CancellationToken.None);

        Assert.Equal(adminInbox.OverviewUrl, ownerInbox.OverviewUrl);
        Assert.Equal("/Admin/Review", ownerInbox.OverviewUrl);
        Assert.Equal(0, userInbox.Count);
        Assert.Equal(0, emergencyInbox.Count);
    }

    [Fact]
    public async Task CaptainMoveRevokesThePreviousEmergencyScopeInsideTheMoveTransaction()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-move-admin", GlobalRole.Admin); var ev = Event(time.GetUtcNow(), time.GetUtcNow().AddDays(1));
        var source = new Bingo.Domain.Teams.Team(Guid.NewGuid(), ev.Id, "Source", "slice1-source", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
        var target = new Bingo.Domain.Teams.Team(Guid.NewGuid(), ev.Id, "Target", "slice1-target", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
        var participant = new Bingo.Domain.Signups.EventParticipant(Guid.NewGuid(), ev.Id, Bingo.Domain.Signups.SignupStatus.Confirmed, 1, time.GetUtcNow(), Bingo.Domain.Signups.SignupSource.AdminCreated, null);
        var character = new OsrsCharacter(Guid.NewGuid(), "Move captain", "MOVE CAPTAIN", time.GetUtcNow());
        var assignment = new Bingo.Domain.Signups.EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, time.GetUtcNow(), admin.Id, null, Bingo.Domain.Signups.EventCharacterRole.Playing, 1m, Bingo.Domain.Signups.EhbSource.AdminCorrection, null);
        var membership = new Bingo.Domain.Teams.TeamMembership(Guid.NewGuid(), source.Id, participant.Id, Bingo.Domain.Teams.TeamMembershipRole.Captain, time.GetUtcNow(), null, "seed");
        var credential = Account.CreateEmergency(Guid.NewGuid(), "slice1-move-credential", "SLICE1-MOVE-CREDENTIAL", time.GetUtcNow()); credential.SetPassword(passwords.HashPassword(credential, "long-test-password"), false, time.GetUtcNow()); credential.Enable();
        var access = new AccountEventAccess(Guid.NewGuid(), credential.Id, ev.Id, source.Id, participant.Id, null, null, null); access.Enable();
        db.AddRange(admin, ev, source, target, participant, character, assignment, membership, credential, access); await db.SaveChangesAsync();
        var context = new DefaultHttpContext { User = new AccountAuthenticationService(db, passwords, time).CreatePrincipal(admin) };
        var page = new Bingo.Web.Pages.Admin.Events.DraftModel(db, time, new AuditWriter(db, time), new NoopCollaborationNotifier(), new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, time)) { PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())), TempData = new TempDataDictionary(context, new DictionaryTempDataProvider()) };

        Assert.IsType<RedirectToPageResult>(await page.OnPostMoveMemberAsync(ev.Id, membership.Id, target.Id, "Move captain", CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.False((await db.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
        Assert.Equal(target.Id, await db.TeamMemberships.Where(item => item.EventParticipantId == participant.Id && item.LeftAt == null).Select(item => item.TeamId).SingleAsync());
    }

    [Fact]
    public async Task DraftFinalizationAndPostFinalizationCaptainPromotionDoNotCreateCredentials()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-draft-no-credentials", GlobalRole.Admin); var ev = Event(time.GetUtcNow(), time.GetUtcNow().AddDays(1));
        var draft = new Bingo.Domain.Teams.DraftSession(Guid.NewGuid(), ev.Id, 1); draft.AcquireControl(admin.Id, time.GetUtcNow(), Bingo.Application.Teams.DraftControlLease.Duration); draft.Start(time.GetUtcNow());
        var team = new Bingo.Domain.Teams.Team(Guid.NewGuid(), ev.Id, "Draft team", "slice1-draft-team", Bingo.Domain.Teams.TeamFormationType.Drafted, null, true);
        var participant = new Bingo.Domain.Signups.EventParticipant(Guid.NewGuid(), ev.Id, Bingo.Domain.Signups.SignupStatus.Confirmed, 1, time.GetUtcNow(), Bingo.Domain.Signups.SignupSource.AdminCreated, null);
        var character = new OsrsCharacter(Guid.NewGuid(), "Draft captain", "DRAFT CAPTAIN", time.GetUtcNow());
        var assignment = new Bingo.Domain.Signups.EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, time.GetUtcNow(), admin.Id, null, Bingo.Domain.Signups.EventCharacterRole.Playing, 1m, Bingo.Domain.Signups.EhbSource.AdminCorrection, null);
        var membership = new Bingo.Domain.Teams.TeamMembership(Guid.NewGuid(), team.Id, participant.Id, Bingo.Domain.Teams.TeamMembershipRole.Participant, time.GetUtcNow(), null, "seed");
        db.AddRange(admin, ev, draft, team, participant, character, assignment, membership); await db.SaveChangesAsync();
        var baseline = (await db.Accounts.CountAsync(), await db.AccountEventAccesses.CountAsync(), await db.PasswordCredentialTokens.CountAsync());
        var context = new DefaultHttpContext { User = new AccountAuthenticationService(db, passwords, time).CreatePrincipal(admin) };
        var page = new Bingo.Web.Pages.Admin.Events.DraftModel(db, time, new AuditWriter(db, time), new NoopCollaborationNotifier(), new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, time)) { PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())), TempData = new TempDataDictionary(context, new DictionaryTempDataProvider()) };

        Assert.IsType<RedirectToPageResult>(await page.OnPostFinalizeAsync(ev.Id, CancellationToken.None));
        Assert.Equal(baseline, (await db.Accounts.CountAsync(), await db.AccountEventAccesses.CountAsync(), await db.PasswordCredentialTokens.CountAsync()));
        Assert.IsType<RedirectToPageResult>(await page.OnPostChangeRoleAsync(ev.Id, membership.Id, Bingo.Domain.Teams.TeamMembershipRole.Captain, CancellationToken.None));
        Assert.Equal(baseline, (await db.Accounts.CountAsync(), await db.AccountEventAccesses.CountAsync(), await db.PasswordCredentialTokens.CountAsync()));
        Assert.IsType<PageResult>(await page.OnGetAsync(ev.Id, "ehb", CancellationToken.None));
        var projectedMember = Assert.Single(Assert.Single(page.Teams).Members);
        Assert.Equal("Draft captain", projectedMember.Name);
        Assert.Equal(1m, projectedMember.Ehb);
    }

    [Fact]
    public async Task OwnershipTransferAndOperatorRecoveryKeepExactlyOneOwner()
    {
        await using var db = new ApplicationDbContext(options);
        var owner = Website("slice1-owner", GlobalRole.SuperAdmin);
        var destination = Website("slice1-destination", GlobalRole.User);
        db.Accounts.AddRange(owner, destination);
        await db.SaveChangesAsync();
        var administration = new AccountAdministrationService(db, passwords, time);

        await administration.TransferOwnershipAsync(owner.Id, "long-test-password", destination.LoginName, CancellationToken.None);
        Assert.Equal(GlobalRole.Admin, owner.GlobalRole);
        Assert.Equal(GlobalRole.SuperAdmin, destination.GlobalRole);
        Assert.Single(await db.Accounts.Where(x => x.GlobalRole == GlobalRole.SuperAdmin).ToListAsync());

        var recovery = new OperatorRecoveryService(db, time, passwords);
        await recovery.RecoverOwnerAsync(owner.LoginName, owner.LoginName, CancellationToken.None);
        Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
        Assert.Equal(GlobalRole.Admin, destination.GlobalRole);
        Assert.Single(await db.Accounts.Where(x => x.GlobalRole == GlobalRole.SuperAdmin).ToListAsync());
        Assert.Contains(await db.AuditEntries.ToListAsync(), entry => entry.Action == "account.owner_recovered");
    }

    [Fact]
    public async Task EmergencySetupResetEnableAndClaimsFollowTheRequiredOrder()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-emergency-admin", GlobalRole.Admin);
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "slice1-emergency", "SLICE1-EMERGENCY", time.GetUtcNow());
        var emergencyEvent = Event(time.GetUtcNow(), time.GetUtcNow().AddHours(1));
        emergencyEvent.StartEvent(time.GetUtcNow());
        var eventId = emergencyEvent.Id;
        var teamId = Guid.NewGuid();
        var access = new AccountEventAccess(Guid.NewGuid(), emergency.Id, eventId, teamId, null, null, null, time.GetUtcNow().AddDays(1));
        db.AddRange(admin, emergency, emergencyEvent);
        db.AccountEventAccesses.Add(access);
        await db.SaveChangesAsync();
        var identities = new AccountIdentityService(db, passwords, time);
        var administration = new AccountAdministrationService(db, passwords, time);

        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.SetEmergencyEnabledAsync(admin.Id, emergency.Id, true, CancellationToken.None));
        var setup = await identities.GenerateEmergencyCredentialLinkAsync(admin.Id, emergency.Id, CancellationToken.None);
        await identities.ConsumeResetAsync(setup, "emergency-password", CancellationToken.None);
        await administration.SetEmergencyEnabledAsync(admin.Id, emergency.Id, true, CancellationToken.None);

        var authentication = new AccountAuthenticationService(db, passwords, time);
        var authenticated = await authentication.ValidateCredentialsAsync(emergency.LoginName, "emergency-password", CancellationToken.None);
        var scopedAccess = await authentication.GetEmergencyAccessAsync(emergency.Id, CancellationToken.None);
        var principal = authentication.CreatePrincipal(authenticated!, emergencyAccess: scopedAccess);
        Assert.True(emergency.Active);
        Assert.True(access.Enabled);
        Assert.Equal(eventId.ToString(), principal.FindFirstValue(Bingo.Application.Access.AccountClaims.EventId));
        Assert.Equal(teamId.ToString(), principal.FindFirstValue(Bingo.Application.Access.AccountClaims.TeamId));

        var reset = await identities.GenerateEmergencyCredentialLinkAsync(admin.Id, emergency.Id, CancellationToken.None);
        await identities.ConsumeResetAsync(reset, "replacement-password", CancellationToken.None);
        Assert.Null(await authentication.ValidateCredentialsAsync(emergency.LoginName, "emergency-password", CancellationToken.None));
        Assert.NotNull(await authentication.ValidateCredentialsAsync(emergency.LoginName, "replacement-password", CancellationToken.None));
    }

    [Fact]
    public async Task EmergencySetupLinkHttpSubmissionShowsLocalizedOutcomeAndConsumesOnlyOnSuccess()
    {
        string setupToken;
        Guid emergencyId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var admin = Website("slice1-http-setup-admin", GlobalRole.Admin);
            var emergency = Account.CreateEmergency(Guid.NewGuid(), "slice1-http-emergency", "SLICE1-HTTP-EMERGENCY", time.GetUtcNow());
            var ev = Event(time.GetUtcNow(), time.GetUtcNow().AddHours(2));
            var team = new Bingo.Domain.Teams.Team(Guid.NewGuid(), ev.Id, "HTTP emergency team", "http-emergency-team", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
            seed.AddRange(admin, emergency, ev, team);
            seed.AccountEventAccesses.Add(new AccountEventAccess(Guid.NewGuid(), emergency.Id, ev.Id, team.Id, null, ev.EventStartsAt, ev.SubmissionCutoffAt, null));
            await seed.SaveChangesAsync();
            emergencyId = emergency.Id;
            setupToken = await new AccountIdentityService(seed, passwords, time).GenerateEmergencyCredentialLinkAsync(admin.Id, emergency.Id, CancellationToken.None);
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da");

        var invalidForm = await client.GetStringAsync("/Account/ResetPassword/not-a-token");
        var invalidRequestToken = Regex.Match(invalidForm, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var invalid = await client.PostAsync("/Account/ResetPassword/not-a-token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Password"] = "long-enough-password",
            ["Input.ConfirmPassword"] = "long-enough-password",
            ["__RequestVerificationToken"] = invalidRequestToken
        }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidContent = WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync());
        Assert.Contains("Dette link er ikke længere gyldigt.", invalidContent, StringComparison.Ordinal);

        var setupForm = await client.GetStringAsync($"/Account/ResetPassword/{setupToken}");
        var setupRequestToken = Regex.Match(setupForm, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var completed = await client.PostAsync($"/Account/ResetPassword/{setupToken}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Password"] = "long-enough-password",
            ["Input.ConfirmPassword"] = "long-enough-password",
            ["__RequestVerificationToken"] = setupRequestToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        Assert.Equal("/Account/Login", completed.Headers.Location?.ToString());
        using var login = await client.GetAsync("/Account/Login");
        var loginContent = WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync());
        Assert.Contains("Opsætningen af nødkontoen er fuldført. En admin skal stadig aktivere den, før den kan bruges.", loginContent, StringComparison.Ordinal);
        Assert.Contains("notice-success", loginContent, StringComparison.Ordinal);

        await using var verify = new ApplicationDbContext(options);
        Assert.NotNull(await verify.Accounts.Where(account => account.Id == emergencyId).Select(account => account.PasswordHash).SingleAsync());
        Assert.NotNull(await verify.PasswordCredentialTokens.Where(token => token.AccountId == emergencyId && token.Purpose == PasswordCredentialTokenPurpose.EmergencySetup).Select(token => token.UsedAt).SingleAsync());
    }

    [Fact]
    public async Task PasswordChangeHttpReissuesCurrentPasswordSessionAndRejectsOnlyPriorPasswordSessions()
    {
        Guid accountId;
        ClaimsPrincipal discordSession;
        await using (var seed = new ApplicationDbContext(options))
        {
            var account = Website("slice1-http-password-change");
            seed.Accounts.Add(account);
            await seed.SaveChangesAsync();
            accountId = account.Id;
            discordSession = new AccountAuthenticationService(seed, passwords, time).CreatePrincipal(account, "discord");
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var current = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var prior = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        async Task SignIn(HttpClient client)
        {
            var login = await client.GetStringAsync("/Account/Login");
            var token = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = "slice1-http-password-change", ["Input.Password"] = "long-test-password", ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await SignIn(current);
        await SignIn(prior);
        var form = await current.GetStringAsync("/Account/ChangePassword");
        var formToken = Regex.Match(form, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var changed = await current.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = "long-test-password",
            ["Input.NewPassword"] = "replacement-password",
            ["Input.ConfirmPassword"] = "replacement-password",
            ["__RequestVerificationToken"] = formToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Equal("/Account/Settings", changed.Headers.Location?.ToString());
        using var settings = await current.GetAsync("/Account/Settings");
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        Assert.Contains("Your password was changed.", await settings.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var stale = await prior.GetAsync("/Account/Settings");
        Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);
        Assert.Equal("/Account/Login", stale.Headers.Location?.AbsolutePath);

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(accountId, await verify.Accounts.Where(account => account.Id == accountId).Select(account => account.Id).SingleAsync());
        Assert.NotNull((await ValidateCookieAsync(verify, discordSession)).Principal);
    }

    [Fact]
    public async Task WebsiteResetLinkIsVisibleAfterAuthorizedGenerationAndCanBeConsumed()
    {
        Guid targetId;
        Guid ownerId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var owner = Website("slice1-http-reset-owner", GlobalRole.SuperAdmin);
            var target = Website("slice1-http-reset-admin", GlobalRole.Admin);
            targetId = target.Id;
            ownerId = owner.Id;
            seed.AddRange(owner, target);
            await seed.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        var loginToken = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = "slice1-http-reset-owner", ["Input.Password"] = "long-test-password", ["__RequestVerificationToken"] = loginToken }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var manage = await client.GetStringAsync($"/Admin/Accounts/Manage/{targetId}");
        var manageToken = Regex.Match(manage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var generated = await client.PostAsync($"/Admin/Accounts/Manage/{targetId}?handler=GenerateResetLink", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = manageToken }));
        Assert.Equal(HttpStatusCode.Redirect, generated.StatusCode);
        var revealed = await client.GetStringAsync($"/Admin/Accounts/Manage/{targetId}");
        var resetPath = Regex.Match(revealed, "https?://[^<]+(/Account/ResetPassword/[A-F0-9]+)").Groups[1].Value;
        Assert.NotEmpty(resetPath);
        var mismatched = await client.GetStringAsync($"/Admin/Accounts/Manage/{ownerId}");
        Assert.DoesNotContain(resetPath, mismatched, StringComparison.Ordinal);

        var resetForm = await client.GetStringAsync(resetPath);
        var resetToken = Regex.Match(resetForm, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var completed = await client.PostAsync(resetPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Password"] = "replacement-password",
            ["Input.ConfirmPassword"] = "replacement-password",
            ["__RequestVerificationToken"] = resetToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        Assert.Equal("/Account/Login", completed.Headers.Location?.ToString());
    }

    [Fact]
    public async Task EmergencyCredentialLifecycleRequiresReopeningBeforeAuditedReenableAndDisablesAgainWhenItCloses()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-cutoff-admin", GlobalRole.Admin);
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "slice1-cutoff-emergency", "SLICE1-CUTOFF-EMERGENCY", now);
        emergency.SetPassword(passwords.HashPassword(emergency, "emergency-password"), false, now, incrementVersion: false);
        emergency.Enable();
        var ev = Event(now, now.AddMinutes(10));
        ev.StartEvent(now);
        var access = new AccountEventAccess(Guid.NewGuid(), emergency.Id, ev.Id, Guid.NewGuid(), null, ev.EventStartsAt, ev.SubmissionCutoffAt, null);
        access.Enable();
        db.AddRange(admin, emergency, ev, access);
        await db.SaveChangesAsync();

        var lifecycle = new EmergencyCredentialLifecycleService(db, clock);
        await lifecycle.ApplyAsync(CancellationToken.None);
        Assert.True((await db.AccountEventAccesses.SingleAsync(x => x.Id == access.Id)).Enabled);

        clock.Set(now.AddMinutes(10).AddTicks(1));
        await lifecycle.ApplyAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
        var disabled = await db.AccountEventAccesses.SingleAsync(x => x.Id == access.Id);
        Assert.False(disabled.Enabled);
        Assert.True(disabled.CutoffDisabled);
        Assert.False((await db.Accounts.SingleAsync(x => x.Id == emergency.Id)).Active);
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "account.emergency_cutoff_disabled" && x.TargetId == emergency.Id.ToString()).ToListAsync());

        await lifecycle.ApplyAsync(CancellationToken.None);
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "account.emergency_cutoff_disabled" && x.TargetId == emergency.Id.ToString()).ToListAsync());

        var administration = new AccountAdministrationService(db, passwords, clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.SetEmergencyEnabledAsync(admin.Id, emergency.Id, true, CancellationToken.None));
        var reopenContext = new DefaultHttpContext { User = new AccountAuthenticationService(db, passwords, clock).CreatePrincipal(admin) };
        var reopen = new Bingo.Web.Pages.Admin.Events.ManageModel(db, new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, clock), new AuditWriter(db, clock), clock)
        {
            PageContext = new PageContext(new ActionContext(reopenContext, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(reopenContext, new DictionaryTempDataProvider()),
            ReopenUntil = clock.GetUtcNow().AddMinutes(5),
            StateReason = "Focused lifecycle reopening test."
        };
        Assert.IsType<RedirectToPageResult>(await reopen.OnPostReopenSubmissionsAsync(ev.Id, CancellationToken.None));
        await administration.SetEmergencyEnabledAsync(admin.Id, emergency.Id, true, CancellationToken.None);
        db.ChangeTracker.Clear();
        Assert.True((await db.Accounts.SingleAsync(x => x.Id == emergency.Id)).Active);
        Assert.True((await db.AccountEventAccesses.SingleAsync(x => x.Id == access.Id)).Enabled);
        Assert.Equal(AccountAccessMode.Full, (await db.AccountEventAccesses.SingleAsync(x => x.Id == access.Id)).GetAccessMode(clock.GetUtcNow()));
        clock.Set(clock.GetUtcNow().AddMinutes(5));
        await lifecycle.ApplyAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
        Assert.False((await db.Accounts.SingleAsync(x => x.Id == emergency.Id)).Active);
        Assert.False((await db.AccountEventAccesses.SingleAsync(x => x.Id == access.Id)).Enabled);
        Assert.Contains(await db.AuditEntries.ToListAsync(), x => x.Action == "account.emergency_enabled");
        Assert.Contains(await db.AuditEntries.ToListAsync(), x => x.Action == "event.submissions_reopened");
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.Action == "account.emergency_cutoff_disabled" && x.TargetId == emergency.Id.ToString()));
    }

    [Fact]
    public async Task DevelopmentSeededEmergencyCredentialFollowsCutoffLifecycle()
    {
        var seededAt = DateTimeOffset.UtcNow;
        var clock = new MutableTimeProvider(seededAt);
        await using var db = new ApplicationDbContext(options);
        const string ownerUsername = "slice1-seed-lifecycle-owner";
        await new OperatorRecoveryService(db, clock, passwords).BootstrapOwnerAsync(ownerUsername, "long-test-password", ownerUsername, CancellationToken.None);
        var admin = await db.Accounts.SingleAsync(account => account.LoginName == ownerUsername);
        await new CatalogueSnapshotService(db, clock).ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));
        var result = await new DevelopmentScenarioSeeder(db, new DevelopmentEnvironment(), passwords, new SeedEvidenceStorage(), clock).ResetAndSeedAsync();

        Assert.Equal(ownerUsername, result.AdminUsername);
        Assert.Equal(DevelopmentScenarioSeeder.SecondaryAdminUsername, result.SecondaryAdminUsername);
        Assert.Equal(16, result.Scenarios.Count);
        db.ChangeTracker.Clear();
        var owner = await db.Accounts.SingleAsync(account => account.Id == admin.Id);
        Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
        Assert.True(owner.Active);
        Assert.Single(await db.Accounts.Where(account => account.GlobalRole == GlobalRole.SuperAdmin).ToListAsync());
        var secondaryAdmin = await db.Accounts.SingleAsync(account => account.LoginName == DevelopmentScenarioSeeder.SecondaryAdminUsername);
        Assert.Equal(GlobalRole.Admin, secondaryAdmin.GlobalRole);
        Assert.True(secondaryAdmin.Active);

        var access = await db.AccountEventAccesses.FirstAsync(item => item.Enabled);
        var ev = await db.Events.SingleAsync(item => item.Id == access.EventId);
        var cutoff = ev.SubmissionCutoffAt;
        var lifecycle = new EmergencyCredentialLifecycleService(db, clock);
        clock.Set(cutoff.AddTicks(-1)); await lifecycle.ApplyAsync(CancellationToken.None);
        Assert.True((await db.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
        clock.Set(cutoff); await lifecycle.ApplyAsync(CancellationToken.None); db.ChangeTracker.Clear();
        Assert.False((await db.Accounts.SingleAsync(item => item.Id == access.AccountId)).Active);
        Assert.False((await db.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
        Assert.Single(await db.AuditEntries.Where(item => item.Action == "account.emergency_cutoff_disabled" && item.TargetId == access.AccountId.ToString()).ToListAsync());
        await lifecycle.ApplyAsync(CancellationToken.None);
        Assert.Single(await db.AuditEntries.Where(item => item.Action == "account.emergency_cutoff_disabled" && item.TargetId == access.AccountId.ToString()).ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AccountAdministrationService(db, passwords, clock).SetEmergencyEnabledAsync(admin.Id, access.AccountId, true, CancellationToken.None));
        await lifecycle.ApplyAsync(CancellationToken.None); db.ChangeTracker.Clear();
        Assert.False((await db.Accounts.SingleAsync(item => item.Id == access.AccountId)).Active);
        Assert.False((await db.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
    }

    [Fact]
    public async Task AdminRoleAndRestoreNotificationsCommitWithTheirMutations()
    {
        await using var db = new ApplicationDbContext(options);
        var owner = Website("slice1-notify-owner", GlobalRole.SuperAdmin);
        var target = Website("slice1-notify-target");
        db.AddRange(owner, target);
        await db.SaveChangesAsync();
        var administration = new AccountAdministrationService(db, passwords, time);

        await administration.GrantAdminAsync(owner.Id, target.Id, CancellationToken.None);
        await administration.RevokeAdminAsync(owner.Id, target.Id, CancellationToken.None);
        await administration.DisableAsync(owner.Id, target.Id, "test disable", CancellationToken.None);
        await administration.RestoreAsync(owner.Id, target.Id, CancellationToken.None);

        var notifications = await db.PersonalNotifications.Where(x => x.RecipientAccountId == target.Id).OrderBy(x => x.CreatedAt).ToListAsync();
        Assert.Equal(3, notifications.Count);
        Assert.Equal(GlobalRole.User, (await db.Accounts.SingleAsync(x => x.Id == target.Id)).GlobalRole);
        Assert.True((await db.Accounts.SingleAsync(x => x.Id == target.Id)).Active);
    }

    [Fact]
    public async Task ResetConsumptionAllowsExactlyOneConcurrentSuccess()
    {
        Guid targetId;
        string token;
        await using (var seed = new ApplicationDbContext(options))
        {
            var owner = Website("slice1-reset-owner", GlobalRole.SuperAdmin);
            var target = Website("slice1-reset-target");
            seed.AddRange(owner, target);
            await seed.SaveChangesAsync();
            targetId = target.Id;
            token = await new AccountIdentityService(seed, passwords, time).GenerateResetLinkAsync(owner.Id, target.Id, CancellationToken.None);
        }

        async Task<bool> Consume(string password)
        {
            await using var attempt = new ApplicationDbContext(options);
            try { await new AccountIdentityService(attempt, new PasswordHasher<Account>(), time).ConsumeResetAsync(token, password, CancellationToken.None); return true; }
            catch (InvalidOperationException) { return false; }
        }

        var results = await Task.WhenAll(Consume("concurrent-password-one"), Consume("concurrent-password-two"));
        Assert.Equal(1, results.Count(value => value));
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(1, await verify.PasswordCredentialTokens.Where(x => x.AccountId == targetId && x.UsedAt != null).CountAsync());
    }

    [Fact]
    public async Task OperatorOwnerResetLinksRequireTheActiveOwnerAndFollowNormalSingleUsePasswordRules()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        await using var db = new ApplicationDbContext(options);
        var owner = Website("slice1-owner-reset", GlobalRole.SuperAdmin);
        var user = Website("slice1-owner-reset-user");
        db.AddRange(owner, user);
        await db.SaveChangesAsync();
        var recovery = new OperatorRecoveryService(db, clock, passwords);

        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.CreateOwnerRecoveryResetLinkAsync(owner.LoginName, "different-owner", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.CreateOwnerRecoveryResetLinkAsync(owner.LoginName, owner.LoginName.ToUpperInvariant(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.CreateOwnerRecoveryResetLinkAsync(owner.LoginName.ToUpperInvariant(), owner.LoginName.ToUpperInvariant(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.CreateOwnerRecoveryResetLinkAsync(user.LoginName, user.LoginName, CancellationToken.None));

        var first = await recovery.CreateOwnerRecoveryResetLinkAsync(owner.LoginName, owner.LoginName, CancellationToken.None);
        var second = await recovery.CreateOwnerRecoveryResetLinkAsync(owner.LoginName, owner.LoginName, CancellationToken.None);
        var identities = new AccountIdentityService(db, passwords, clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => identities.ConsumeResetAsync(first, "replacement-owner-password", CancellationToken.None));
        clock.Set(clock.GetUtcNow().AddMinutes(61));
        await Assert.ThrowsAsync<InvalidOperationException>(() => identities.ConsumeResetAsync(second, "replacement-owner-password", CancellationToken.None));

        var stale = new AccountAuthenticationService(db, passwords, clock).CreatePrincipal(owner);
        var usable = await recovery.CreateOwnerRecoveryResetLinkAsync(owner.LoginName, owner.LoginName, CancellationToken.None);
        await identities.ConsumeResetAsync(usable, "replacement-owner-password", CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => identities.ConsumeResetAsync(usable, "another-owner-password", CancellationToken.None));
        Assert.Null((await ValidateCookieAsync(db, stale)).Principal);
        Assert.NotNull(await new AccountAuthenticationService(db, passwords, clock).ValidateCredentialsAsync(owner.LoginName, "replacement-owner-password", CancellationToken.None));

        var audit = await db.AuditEntries.Where(entry => entry.Action == "account.owner_recovery_reset_link_created").ToListAsync();
        Assert.Equal(3, audit.Count);
        Assert.All(audit, entry =>
        {
            Assert.Null(entry.ActorAccountId);
            Assert.Equal("System", entry.ActorUsername);
            Assert.DoesNotContain(first, entry.Details, StringComparison.Ordinal);
            Assert.DoesNotContain(usable, entry.Details, StringComparison.Ordinal);
            Assert.DoesNotContain("replacement-owner-password", entry.Details, StringComparison.Ordinal);
        });
        var completion = Assert.Single(await db.AuditEntries.Where(entry => entry.Action == "account.owner_recovery_password_reset").ToListAsync());
        Assert.Null(completion.ActorAccountId);
        Assert.Equal("System", completion.ActorUsername);
        Assert.DoesNotContain(usable, completion.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("replacement-owner-password", completion.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscordLinkRaceLeavesExactlyOneSuccessfulWinner()
    {
        Guid firstId;
        Guid secondId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var first = Website("slice1-discord-race-first");
            var second = Website("slice1-discord-race-second");
            firstId = first.Id; secondId = second.Id;
            seed.AddRange(first, second);
            await seed.SaveChangesAsync();
        }
        async Task<bool> Link(Guid id)
        {
            await using var attempt = new ApplicationDbContext(options);
            var account = await attempt.Accounts.SingleAsync(x => x.Id == id);
            try { await new AccountIdentityService(attempt, new PasswordHasher<Account>(), time).SetDiscordAsync(account, "slice1-discord-race", "Race", "linked", CancellationToken.None); return true; }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(Link(firstId), Link(secondId));
        Assert.Equal(1, results.Count(value => value));
        await using var verify = new ApplicationDbContext(options);
        Assert.Single(await verify.Accounts.Where(x => x.DiscordUserId == "slice1-discord-race").ToListAsync());
    }

    [Fact]
    public async Task DiscordIdentityEndpointsTranslateRelationalUniquenessRacesIntoSafeConflicts()
    {
        var onboardingBarrier = new IdentityRaceBarrier("RACE-ONBOARDING");
        var onboardingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(onboardingBarrier).Options;
        async Task<(IActionResult Result, Bingo.Web.Pages.Account.OnboardingModel Page)> Onboard(string discordId)
        {
            var db = new ApplicationDbContext(onboardingOptions);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var state = new DiscordOnboardingStateService(new EphemeralDataProtectionProvider(), cache, time);
            var issue = new DefaultHttpContext();
            state.Issue(issue.Response, discordId, "Race user");
            var auth = new RecordingAuthenticationService("unused", null, Guid.Empty, null);
            var request = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider() };
            request.Request.Headers.Cookie = issue.Response.Headers.SetCookie.Single()!.Split(';')[0];
            var page = new Bingo.Web.Pages.Account.OnboardingModel(new AccountIdentityService(db, passwords, time), new AccountAuthenticationService(db, passwords, time), state, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.OnboardingModel>.Instance)
            {
                PageContext = new PageContext(new ActionContext(request, new RouteData(), new PageActionDescriptor())),
                TempData = new TempDataDictionary(request, new DictionaryTempDataProvider()),
                Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { Username = "race-onboarding", OsrsCharacterName = "Race Character", Password = "long-race-password", ConfirmPassword = "long-race-password" }
            };
            var result = await page.OnPostAsync(CancellationToken.None);
            await db.DisposeAsync();
            return (result, page);
        }

        var onboarding = await Task.WhenAll(Onboard("race-onboarding-discord-a"), Onboard("race-onboarding-discord-b"));
        Assert.Equal(1, onboarding.Count(outcome => outcome.Result is RedirectToPageResult));
        var onboardingLoser = Assert.Single(onboarding, outcome => outcome.Result is PageResult);
        Assert.Equal("race-onboarding", onboardingLoser.Page.Input.Username);
        Assert.Equal("long-race-password", onboardingLoser.Page.Input.Password);
        Assert.Contains(onboardingLoser.Page.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage == "That username or Discord account is already in use.");
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Single(await verify.Accounts.Where(account => account.NormalizedLoginName == "RACE-ONBOARDING").ToListAsync());
            Assert.Equal(1, await verify.Accounts.CountAsync(account => account.DiscordUserId == "race-onboarding-discord-a" || account.DiscordUserId == "race-onboarding-discord-b"));
        }

        Guid firstId;
        Guid secondId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var first = Website("slice1-endpoint-race-first");
            var second = Website("slice1-endpoint-race-second");
            firstId = first.Id;
            secondId = second.Id;
            seed.AddRange(first, second);
            await seed.SaveChangesAsync();
        }
        var linkBarrier = new IdentityRaceBarrier("endpoint-race-discord");
        var linkOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(linkBarrier).Options;
        async Task<(IActionResult Result, Bingo.Web.Pages.Account.DiscordCallbackModel Page)> Link(Guid accountId)
        {
            await using var db = new ApplicationDbContext(linkOptions);
            var callback = CallbackContext(accountId, "endpoint-race-discord", "link");
            var page = CallbackModel(db, new AccountAuthenticationService(db, passwords, time), new AccountIdentityService(db, passwords, time), callback.Context, callback.LinkState);
            return (await page.OnGetAsync(null, CancellationToken.None), page);
        }

        var links = await Task.WhenAll(Link(firstId), Link(secondId));
        Assert.All(links, outcome => Assert.IsType<RedirectToPageResult>(outcome.Result));
        Assert.Contains(links, outcome => outcome.Page.TempData["StatusMessage"]?.ToString() == "That Discord account is already linked.");
        await using var linked = new ApplicationDbContext(options);
        Assert.Single(await linked.Accounts.Where(account => account.DiscordUserId == "endpoint-race-discord").ToListAsync());
        Assert.Equal(2, await linked.Accounts.CountAsync(account => account.Id == firstId || account.Id == secondId));
        Assert.Equal(1, await linked.Accounts.CountAsync(account => (account.Id == firstId || account.Id == secondId) && account.DiscordUserId == null));
    }

    [Fact]
    public async Task CookieValidationRejectsStaleAuthorizationAndPasswordVersions()
    {
        await using var db = new ApplicationDbContext(options);
        var account = Website("slice1-cookie-stale");
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var authentication = new AccountAuthenticationService(db, passwords, time);
        var principal = authentication.CreatePrincipal(account);

        account.SetGlobalRole(GlobalRole.Admin);
        await db.SaveChangesAsync();
        Assert.Null((await ValidateCookieAsync(db, principal)).Principal);

        var fresh = authentication.CreatePrincipal(account);
        account.SetPassword(passwords.HashPassword(account, "replacement-password"), false, time.GetUtcNow());
        await db.SaveChangesAsync();
        Assert.Null((await ValidateCookieAsync(db, fresh)).Principal);
    }

    [Fact]
    public async Task RoleChangesAndOwnershipTransferInvalidateSessionsAndDirectStaleSessionsToSignInAgain()
    {
        await using var db = new ApplicationDbContext(options);
        var owner = Website("slice1-session-owner", GlobalRole.SuperAdmin);
        var target = Website("slice1-session-target");
        var destination = Website("slice1-session-destination");
        db.Accounts.AddRange(owner, target, destination);
        await db.SaveChangesAsync();
        var authentication = new AccountAuthenticationService(db, passwords, time);
        var administration = new AccountAdministrationService(db, passwords, time);

        async Task AssertStaleSessionGetsAccessChangedOutcome(ClaimsPrincipal principal)
        {
            var validation = await ValidateCookieAsync(db, principal);
            Assert.Null(validation.Principal);
            var redirect = new RedirectContext<CookieAuthenticationOptions>(
                validation.HttpContext,
                new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)),
                new CookieAuthenticationOptions(),
                new AuthenticationProperties(),
                "/Account/Login?ReturnUrl=%2FAccount%2FSettings");
            await new AccountCookieEvents(db, time).RedirectToLogin(redirect);
            Assert.Contains("accessChanged=true", validation.HttpContext.Response.Headers.Location.ToString(), StringComparison.Ordinal);
        }

        var beforeGrant = authentication.CreatePrincipal(target);
        await administration.GrantAdminAsync(owner.Id, target.Id, CancellationToken.None);
        await AssertStaleSessionGetsAccessChangedOutcome(beforeGrant);

        var beforeRevoke = authentication.CreatePrincipal(target);
        await administration.RevokeAdminAsync(owner.Id, target.Id, CancellationToken.None);
        await AssertStaleSessionGetsAccessChangedOutcome(beforeRevoke);

        var ownerSession = authentication.CreatePrincipal(owner);
        var destinationSession = authentication.CreatePrincipal(destination);
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.TransferOwnershipAsync(owner.Id, "wrong-password", destination.LoginName, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.TransferOwnershipAsync(owner.Id, "long-test-password", "not-the-destination", CancellationToken.None));
        Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
        Assert.Equal(GlobalRole.User, destination.GlobalRole);

        await administration.TransferOwnershipAsync(owner.Id, "long-test-password", destination.LoginName, CancellationToken.None);
        await AssertStaleSessionGetsAccessChangedOutcome(ownerSession);
        await AssertStaleSessionGetsAccessChangedOutcome(destinationSession);
    }

    [Fact]
    public async Task FailedNotificationPersistenceRollsBackAdminMutation()
    {
        async Task AssertRollback(GlobalRole initialRole, bool active, Func<AccountAdministrationService, Guid, Guid, Task> mutate)
        {
            var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnNotificationInsert()).Options;
            await using var db = new ApplicationDbContext(failingOptions);
            var owner = await db.Accounts.SingleOrDefaultAsync(account => account.GlobalRole == GlobalRole.SuperAdmin);
            if (owner is null)
            {
                owner = Website($"slice1-rollback-owner-{Guid.NewGuid():N}", GlobalRole.SuperAdmin);
                db.Add(owner);
                await db.SaveChangesAsync();
            }
            var target = Website($"slice1-rollback-target-{Guid.NewGuid():N}", initialRole);
            if (!active) target.Disable(time.GetUtcNow());
            db.Add(target); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => mutate(new AccountAdministrationService(db, passwords, time), owner.Id, target.Id));
            db.ChangeTracker.Clear();
            var unchanged = await db.Accounts.SingleAsync(x => x.Id == target.Id);
            Assert.Equal(initialRole, unchanged.GlobalRole); Assert.Equal(active, unchanged.Active);
            Assert.Empty(await db.PersonalNotifications.Where(x => x.RecipientAccountId == target.Id).ToListAsync());
        }
        await AssertRollback(GlobalRole.User, true, (service, actor, target) => service.GrantAdminAsync(actor, target, CancellationToken.None));
        await AssertRollback(GlobalRole.Admin, true, (service, actor, target) => service.RevokeAdminAsync(actor, target, CancellationToken.None));
        await AssertRollback(GlobalRole.User, false, (service, actor, target) => service.RestoreAsync(actor, target, CancellationToken.None));
    }

    [Fact]
    public async Task OwnershipTransferRaceRetainsExactlyOneOwner()
    {
        Guid ownerId;
        string firstDestination;
        string secondDestination;
        await using (var seed = new ApplicationDbContext(options))
        {
            var owner = Website("slice1-owner-race", GlobalRole.SuperAdmin);
            var first = Website("slice1-owner-race-first");
            var second = Website("slice1-owner-race-second");
            ownerId = owner.Id; firstDestination = first.LoginName; secondDestination = second.LoginName;
            seed.AddRange(owner, first, second);
            await seed.SaveChangesAsync();
        }

        async Task<bool> Transfer(string destination)
        {
            await using var attempt = new ApplicationDbContext(options);
            try { await new AccountAdministrationService(attempt, new PasswordHasher<Account>(), time).TransferOwnershipAsync(ownerId, "long-test-password", destination, CancellationToken.None); return true; }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException) { return false; }
        }

        var outcomes = await Task.WhenAll(Transfer(firstDestination), Transfer(secondDestination));
        Assert.Equal(1, outcomes.Count(value => value));
        await using var verify = new ApplicationDbContext(options);
        Assert.Single(await verify.Accounts.Where(x => x.GlobalRole == GlobalRole.SuperAdmin).ToListAsync());
    }


    [Fact]
    public async Task AuditFiltersAndPaginatesStructuredRowsStably()
    {
        await using var db = new ApplicationDbContext(options);
        var eventId = Guid.NewGuid();
        var otherEventId = Guid.NewGuid();
        var now = time.GetUtcNow();
        for (var index = 0; index < 30; index++)
        {
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now.AddMinutes(index), null, "admin", "account.changed", "account", index.ToString(CultureInfo.InvariantCulture), "Changed account.", eventId, "{\"role\":\"User\"}", "{\"role\":\"Admin\"}"));
        }
        for (var index = 0; index < 5; index++)
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now.AddMinutes(index), null, "admin", "other.action", "account", $"other-{index}", "Other event.", otherEventId));
        await db.SaveChangesAsync();
        var model = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { Action = "account.changed", EventId = eventId, PageNumber = 1 };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(25, model.Entries.Count);
        Assert.True(model.HasNextPage);
        Assert.All(model.Entries, entry => Assert.Equal(eventId, entry.EventId));
        Assert.All(model.Entries, entry => Assert.NotNull(entry.BeforeState));
        Assert.Equal("29", model.Entries[0].TargetId);
        Assert.Equal("5", model.Entries[^1].TargetId);

        model.PageNumber = 2;
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(5, model.Entries.Count);
        Assert.False(model.HasNextPage);
        Assert.All(model.Entries, entry => Assert.Equal(eventId, entry.EventId));
        Assert.Equal("4", model.Entries[0].TargetId);
        Assert.Equal("0", model.Entries[^1].TargetId);
    }

    private Account Website(string username, GlobalRole role = GlobalRole.User)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), username, AccountAuthenticationService.NormalizeUsername(username), time.GetUtcNow());
        account.SetPassword(passwords.HashPassword(account, "long-test-password"), false, time.GetUtcNow(), incrementVersion: false);
        account.SetGlobalRole(role);
        return account;
    }

    private static Bingo.Domain.Events.BingoEvent Event(DateTimeOffset now, DateTimeOffset cutoff) =>
        new(Guid.NewGuid(), "Slice 1 cutoff event", $"slice1-cutoff-{Guid.NewGuid():N}", "", "UTC", now.AddDays(-2), now.AddDays(-1), now.AddHours(-1), now.AddDays(1), cutoff, 10, Guid.NewGuid(), now.AddDays(-3));

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }

    private static (DefaultHttpContext Context, RecordingAuthenticationService Authentication, DiscordLinkStateService LinkState) CallbackContext(Guid accountId, string discordId, string purpose)
    {
        var cache = new MemoryCache(new MemoryCacheOptions()); var linkState = new DiscordLinkStateService(cache, TimeProvider.System); var state = linkState.Create(accountId, purpose); Assert.True(linkState.TryConsumeForChallenge(state, accountId, purpose));
        var authentication = new RecordingAuthenticationService(discordId, purpose, accountId, state);
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider() };
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "test"));
        return (context, authentication, linkState);
    }

    private static Bingo.Web.Pages.Account.DiscordCallbackModel CallbackModel(ApplicationDbContext db, AccountAuthenticationService authentication, AccountIdentityService identities, HttpContext context, DiscordLinkStateService linkState)
    {
        var onboarding = new DiscordOnboardingStateService(new EphemeralDataProtectionProvider(), new MemoryCache(new MemoryCacheOptions()), TimeProvider.System);
        var model = new Bingo.Web.Pages.Account.DiscordCallbackModel(db, authentication, identities, linkState, onboarding, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.DiscordCallbackModel>.Instance)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
        };
        return model;
    }

    private async Task<CookieValidatePrincipalContext> ValidateCookieAsync(ApplicationDbContext db, ClaimsPrincipal principal)
    {
        var authentication = new RecordingAuthenticationService("unused", "unused", Guid.NewGuid(), "unused");
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider() };
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), CookieAuthenticationDefaults.AuthenticationScheme);
        var validation = new CookieValidatePrincipalContext(context, new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)), new CookieAuthenticationOptions(), ticket);
        await new AccountCookieEvents(db, time).ValidatePrincipal(validation);
        return validation;
    }

    private sealed class ThrowOnNotificationInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<PersonalNotification>().Any(entry => entry.State == EntityState.Added)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated notification persistence failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class ThrowOnAuditInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated audit persistence failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class IdentityRaceBarrier(string expectedIdentity) : SaveChangesInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var races = eventData.Context!.ChangeTracker.Entries<Account>().Any(entry =>
                (entry.State is EntityState.Added or EntityState.Modified) &&
                (entry.Entity.NormalizedLoginName == expectedIdentity || entry.Entity.DiscordUserId == expectedIdentity));
            if (!races) return result;
            if (Interlocked.Increment(ref arrivals) == 2) release.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class SeedEvidenceStorage : IEvidenceStorage
    {
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default) => Task.FromResult(new StoredEvidence($"{eventId}/{submissionId}.png", originalFilename, "image/png", 3, 1, 1, new string('a', 64)));
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class NoopCollaborationNotifier : IAdminCollaborationNotifier { public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask; public Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class NoopSignupService : ISignupService { public Task<SignupResult> SignUpAsync(SignupRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<int> IncreaseCapacityAndPromoteAsync(Guid eventId, int newCap, CancellationToken cancellationToken = default) => Task.FromResult(0); public Task<int> PromoteAvailablePlacesAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.FromResult(0); }
    private sealed class DevelopmentEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Bingo.IntegrationTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class PassthroughLocalizer : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        private readonly AuthenticateResult result;

        public RecordingAuthenticationService(string discordId, string? purpose, Guid accountId, string? state)
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, discordId), new Claim(ClaimTypes.Name, "Discord display")], "Discord.External");
            var properties = new AuthenticationProperties();
            if (purpose is not null) { properties.Items["discord-purpose"] = purpose; properties.Items["discord-account-id"] = accountId.ToString(); properties.Items["discord-link-state"] = state!; }
            result = AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), properties, "Discord.External"));
        }

        public ClaimsPrincipal? LastSignedInPrincipal { get; private set; }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(result);
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) { LastSignedInPrincipal = principal; return Task.CompletedTask; }
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
