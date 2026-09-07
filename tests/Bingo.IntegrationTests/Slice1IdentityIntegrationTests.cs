using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Auditing;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.Events;
using Bingo.Web.Navigation;
using Bingo.Web.Pages.Admin.Events;
using Bingo.Web.Security;
using Bingo.Web.TestData;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscordFailureRetryAndRequiredPasswordChangeRetainSignupDestination(bool failUserInfo)
    {
        var now = time.GetUtcNow();
        await using var db = new ApplicationDbContext(options);
        var account = Website("oauth-recovery");
        account.SetDiscordIdentity("recovery-discord", "Recovery");
        account.SetPassword(passwords.HashPassword(account, "long-test-password"), true, now, incrementVersion: false);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Recovery event", $"recovery-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 10, account.Id, now);
        bingoEvent.MarkFirstPublic(now);
        bingoEvent.OpenSignups(now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        db.AddRange(account, bingoEvent, form);
        await db.SaveChangesAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .UseSetting("DiscordAuthentication:ClientId", "test-client")
            .UseSetting("DiscordAuthentication:ClientSecret", "test-secret")
            .ConfigureServices(services => services.PostConfigure<Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions>("Discord", oauth => oauth.Backchannel = new HttpClient(new RecoveryDiscordBackchannel(failUserInfo)))));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var destination = $"/Events/{bingoEvent.Slug}/Signup?edit=True";
        using var entry = await client.GetAsync(destination);
        var page = await client.GetStringAsync(entry.Headers.Location!);
        var discordRoute = DiscordLink(page);
        using var challenge = await client.GetAsync(discordRoute);
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["state"].ToString();
        Assert.NotEmpty(state);
        using var cancelled = await client.GetAsync($"/Account/DiscordCallback?{(failUserInfo ? "code=test-code" : "error=access_denied")}&state={Uri.EscapeDataString(state)}");
        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        Assert.Equal(destination, Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, cancelled.Headers.Location!).Query)["ReturnUrl"]);
        page = await client.GetStringAsync(cancelled.Headers.Location!);
        Assert.Contains("Discord sign-in was cancelled or failed.", page);
        using var retry = await client.GetAsync(DiscordLink(page));
        state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(retry.Headers.Location!.Query)["state"].ToString();
        using var callback = await client.GetAsync($"/Account/DiscordCallback?code=test-code&state={Uri.EscapeDataString(state)}");
        using var completed = await client.GetAsync(callback.Headers.Location!);
        Assert.Equal(destination, completed.Headers.Location?.OriginalString);
        using var passwordRequired = await client.GetAsync(completed.Headers.Location!);
        var passwordRoute = passwordRequired.Headers.Location!;
        Assert.Equal("/Account/ChangePassword", new Uri(client.BaseAddress!, passwordRoute).AbsolutePath);
        page = await client.GetStringAsync(passwordRoute);
        Assert.Equal(destination, FormValue(page, "ReturnUrl"));
        using var invalid = await client.PostAsync(passwordRoute, PasswordPost(page, "wrong-password"));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        page = await invalid.Content.ReadAsStringAsync();
        Assert.Equal(destination, FormValue(page, "ReturnUrl"));
        using var saved = await client.PostAsync(passwordRoute, PasswordPost(page, "long-test-password"));
        Assert.Equal(destination, saved.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(saved.Headers.Location!)).StatusCode);
        db.ChangeTracker.Clear();
        Assert.False((await db.Accounts.SingleAsync(x => x.Id == account.Id)).MustChangePassword);

        static string DiscordLink(string html) => WebUtility.HtmlDecode(Regex.Match(html, "href=\"([^\"]*/Account/DiscordLogin[^\"]*)\"").Groups[1].Value);
        static string FormValue(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, $"<input(?=[^>]*name=\"{Regex.Escape(name)}\")[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        static FormUrlEncodedContent PasswordPost(string html, string current) => new(new Dictionary<string, string>
        {
            ["Input.CurrentPassword"] = current,
            ["Input.NewPassword"] = "updated-test-password",
            ["Input.ConfirmPassword"] = "updated-test-password",
            ["ReturnUrl"] = FormValue(html, "ReturnUrl"),
            ["__RequestVerificationToken"] = FormValue(html, "__RequestVerificationToken")
        });
    }

    [Fact]
    public async Task DiscordFailureIgnoresInvalidDestinationsAndUnprotectedCallbackState()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .UseSetting("DiscordAuthentication:ClientId", "test-client")
            .UseSetting("DiscordAuthentication:ClientSecret", "test-secret"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var challenge = await client.GetAsync("/Account/DiscordLogin?returnUrl=https%3A%2F%2Fexample.org%2Felsewhere");
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["state"].ToString();
        using var invalidDestination = await client.GetAsync($"/Account/DiscordCallback?error=access_denied&state={Uri.EscapeDataString(state)}");
        Assert.Equal("/Account/Login", invalidDestination.Headers.Location?.OriginalString);
        using var corrupt = await client.GetAsync("/Account/DiscordCallback?error=access_denied&state=corrupt&returnUrl=%2FEvents%2Funtrusted%2FSignup");
        Assert.Equal("/Account/Login", corrupt.Headers.Location?.OriginalString);
        using var missingTicket = await client.GetAsync("/Account/DiscordComplete?returnUrl=%2FEvents%2Funtrusted%2FSignup");
        Assert.Equal("/Account/Login", missingTicket.Headers.Location?.OriginalString);
    }

    private sealed class RecoveryDiscordBackchannel(bool failUserInfo) : HttpMessageHandler
    {
        private bool pendingFailure = failUserInfo;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var isToken = request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal);
            if (!isToken && pendingFailure)
            {
                pendingFailure = false;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(isToken
                    ? "{\"access_token\":\"test-access-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"
                    : "{\"id\":\"recovery-discord\",\"global_name\":\"Recovery\"}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

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
        stateService.Issue(issue.Response, "discord-onboarding", "Display", "/Events/test-16-signup-lookup/Signup");
        var setCookie = issue.Response.Headers.SetCookie.Single();
        var request = new DefaultHttpContext(); request.Request.Headers.Cookie = setCookie!.Split(';')[0];
        Assert.True(stateService.TryRead(request.Request, out var state)); // GET and invalid POST both retain the proof.
        Assert.Equal("discord-onboarding", state.DiscordUserId);
        Assert.Equal("/Events/test-16-signup-lookup/Signup", state.ReturnUrl);
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
        var callbackContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(external)
                .AddSingleton<IUrlHelperFactory, UrlHelperFactory>()
                .BuildServiceProvider()
        };
        var callback = new Bingo.Web.Pages.Account.DiscordCallbackModel(db, new AccountAuthenticationService(db, passwords, clock), new AccountIdentityService(db, passwords, clock), new DiscordLinkStateService(cache, clock), onboarding, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.DiscordCallbackModel>.Instance)
        {
            PageContext = new PageContext(new ActionContext(callbackContext, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(callbackContext, new DictionaryTempDataProvider())
        };
        Assert.IsType<RedirectToPageResult>(await callback.OnGetAsync(null, CancellationToken.None));
        var request = new DefaultHttpContext { RequestServices = callbackContext.RequestServices };
        request.Request.Headers.Cookie = callbackContext.Response.Headers.SetCookie.Single()!.Split(';')[0];
        var lookup = new FixedWiseOldManPlayerLookup();
        var page = new Bingo.Web.Pages.Account.OnboardingModel(new AccountIdentityService(db, passwords, clock), new AccountAuthenticationService(db, passwords, clock), onboarding, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.OnboardingModel>.Instance, lookup)
        {
            PageContext = new PageContext(new ActionContext(request, new RouteData(), new PageActionDescriptor()))
        };
        Assert.IsType<PageResult>(page.OnGet());
        page.Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { OsrsCharacterName = "Slice One" };
        Assert.IsType<PageResult>(await page.OnPostFetchAsync(CancellationToken.None));
        Assert.Equal(1, lookup.Calls);
        Assert.Equal("Slice One", lookup.LastCharacterName);
        Assert.Equal(17.5m, page.Input.SavedEhb);
        Assert.True(onboarding.TryRead(request.Request, out _));
        Assert.Empty(await db.Accounts.Where(account => account.DiscordUserId == "slice1-onboarding-discord").ToListAsync());
        page.ModelState.Clear(); page.Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { Username = "slice1-onboarding", OsrsCharacterName = "Slice One", SavedEhb = page.Input.SavedEhb, Password = "short", ConfirmPassword = "short" };
        Assert.IsType<PageResult>(await page.OnPostAsync(CancellationToken.None));
        Assert.True(onboarding.TryRead(request.Request, out _));
        page.ModelState.Clear(); page.Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { Username = "slice1-onboarding-taken", OsrsCharacterName = "Slice One", SavedEhb = 17.5m, Password = "long-onboarding-password", ConfirmPassword = "long-onboarding-password" };
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
        var retry = new Bingo.Web.Pages.Account.OnboardingModel(new AccountIdentityService(retryDb, passwords, clock), new AccountAuthenticationService(retryDb, passwords, clock), onboarding, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.OnboardingModel>.Instance, new FixedWiseOldManPlayerLookup())
        {
            PageContext = new PageContext(new ActionContext(request, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(request, new DictionaryTempDataProvider()),
            Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { Username = "slice1-onboarding", OsrsCharacterName = "Slice One", SavedEhb = 17.5m, Password = "long-onboarding-password", ConfirmPassword = "long-onboarding-password" }
        };
        var retryResult = Assert.IsType<LocalRedirectResult>(await retry.OnPostAsync(CancellationToken.None));
        Assert.Equal("/", retryResult.Url);
        Assert.Single(await db.Accounts.Where(account => account.DiscordUserId == "slice1-onboarding-discord").ToListAsync());
        await using var onboardingVerification = new ApplicationDbContext(options);
        var completedAccount = await onboardingVerification.Accounts.AsNoTracking().SingleAsync(account => account.DiscordUserId == "slice1-onboarding-discord");
        Assert.Equal(17.5m, await onboardingVerification.AccountOsrsCharacters.AsNoTracking().Where(link => link.AccountId == completedAccount.Id).Select(link => link.SavedEhb).SingleAsync());
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
        var shell = new SharedShellService(db, new PassthroughLocalizer(), null!, null!, null!, time);
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

        var otherNotification = new PersonalNotification(Guid.NewGuid(), other.Id, "account.admin_granted", string.Empty, "/Account/Settings", time.GetUtcNow());
        db.PersonalNotifications.Add(otherNotification); await db.SaveChangesAsync();
        var markAllResult = await page.OnPostMarkAllAsReadAsync(CancellationToken.None);
        Assert.Equal("/notifications", Assert.IsType<RedirectResult>(markAllResult).Url);
        Assert.All(await db.PersonalNotifications.Where(item => item.RecipientAccountId == recipient.Id).ToListAsync(), item => Assert.NotNull(item.ReadAt));
        Assert.Null((await db.PersonalNotifications.SingleAsync(item => item.Id == otherNotification.Id)).ReadAt);
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
        var shell = new SharedShellService(db, new PassthroughLocalizer(), null!, null!, null!, time);

        var adminInbox = await shell.GetNotificationsAsync(authentication.CreatePrincipal(admin), CancellationToken.None);
        var ownerInbox = await shell.GetNotificationsAsync(authentication.CreatePrincipal(owner), CancellationToken.None);
        var userInbox = await shell.GetNotificationsAsync(authentication.CreatePrincipal(user), CancellationToken.None);
        var emergencyInbox = await shell.GetNotificationsAsync(authentication.CreatePrincipal(emergency), CancellationToken.None);

        Assert.Equal(adminInbox.OverviewUrl, ownerInbox.OverviewUrl);
        Assert.Equal("/notifications", ownerInbox.OverviewUrl);
        Assert.Equal(0, userInbox.Count);
        Assert.Equal(0, emergencyInbox.Count);
    }

    [Fact]
    public async Task CaptainMoveRevokesThePreviousEmergencyScopeInsideTheMoveTransaction()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-move-admin", GlobalRole.Admin); var ev = Event(time.GetUtcNow().AddHours(2), time.GetUtcNow().AddDays(1));
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

        Assert.IsType<RedirectToPageResult>(await page.OnPostMoveMemberAsync(ev.Id, membership.Id, target.Id, CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.False((await db.AccountEventAccesses.SingleAsync(item => item.Id == access.Id)).Enabled);
        var retired = await db.TeamMemberships.SingleAsync(item => item.Id == membership.Id);
        Assert.NotNull(retired.LeftAt);
        var replacement = await db.TeamMemberships.SingleAsync(item => item.EventParticipantId == participant.Id && item.LeftAt == null);
        Assert.Equal(target.Id, replacement.TeamId);
        Assert.Equal(Bingo.Domain.Teams.TeamMembershipSource.Replacement, replacement.Source);
        Assert.Equal(membership.Id, replacement.ReplacesMembershipId);
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
        var page = new Bingo.Web.Pages.Admin.Events.DraftModel(db, time, new AuditWriter(db, time), new NoopCollaborationNotifier(), new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, time), captainAuthority: new Bingo.Infrastructure.Teams.TeamCaptainAuthorityService(db, time)) { PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())), TempData = new TempDataDictionary(context, new DictionaryTempDataProvider()) };

        Assert.IsType<RedirectToPageResult>(await page.OnPostFinalizeAsync(ev.Id, CancellationToken.None));
        Assert.Equal(baseline, (await db.Accounts.CountAsync(), await db.AccountEventAccesses.CountAsync(), await db.PasswordCredentialTokens.CountAsync()));
        Assert.IsType<RedirectToPageResult>(await page.OnPostChangeRoleAsync(ev.Id, membership.Id, Bingo.Domain.Teams.TeamMembershipRole.Captain, CancellationToken.None, membership.Version));
        Assert.Equal(baseline, (await db.Accounts.CountAsync(), await db.AccountEventAccesses.CountAsync(), await db.PasswordCredentialTokens.CountAsync()));
        Assert.IsType<PageResult>(await page.OnGetAsync(ev.Id, "ehb", CancellationToken.None));
        var projectedMember = Assert.Single(Assert.Single(page.Teams).Members);
        Assert.Equal("Draft captain", projectedMember.Name);
        Assert.Equal(1m, projectedMember.Ehb);
    }

    [Fact]
    public async Task ConcurrentExternalMembersAcrossEventsShareOneNewCharacterWithoutPartialPersistence()
    {
        var now = time.GetUtcNow();
        Guid adminId;
        Guid firstEventId;
        Guid secondEventId;
        Guid firstTeamId;
        Guid secondTeamId;
        await using (var setup = new ApplicationDbContext(options))
        {
            var admin = Website("slice2-external-member-admin", GlobalRole.Admin);
            var firstEvent = new Bingo.Domain.Events.BingoEvent(Guid.NewGuid(), "External one", "slice2-external-one", "", "UTC", now, now.AddHours(1), now.AddDays(1), now.AddDays(2), now.AddDays(2), 10, admin.Id, now);
            var secondEvent = new Bingo.Domain.Events.BingoEvent(Guid.NewGuid(), "External two", "slice2-external-two", "", "UTC", now, now.AddHours(1), now.AddDays(1), now.AddDays(2), now.AddDays(2), 10, admin.Id, now);
            var firstTeam = new Bingo.Domain.Teams.Team(Guid.NewGuid(), firstEvent.Id, "First external", "first-external", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
            var secondTeam = new Bingo.Domain.Teams.Team(Guid.NewGuid(), secondEvent.Id, "Second external", "second-external", Bingo.Domain.Teams.TeamFormationType.Preformed, null, false);
            setup.AddRange(admin, firstEvent, secondEvent, firstTeam, secondTeam);
            await setup.SaveChangesAsync();
            adminId = admin.Id;
            firstEventId = firstEvent.Id;
            secondEventId = secondEvent.Id;
            firstTeamId = firstTeam.Id;
            secondTeamId = secondTeam.Id;
        }

        async Task<IActionResult> AddAsync(Guid eventId, Guid teamId, string reason)
        {
            await using var db = new ApplicationDbContext(options);
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Name, "slice2-external-member-admin")], "test"))
            };
            var page = new Bingo.Web.Pages.Admin.Events.DraftModel(
                db, time, new AuditWriter(db, time), new NoopCollaborationNotifier(), new NoopSignupService(),
                new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, time))
            {
                PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
                TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
            };
            return await page.OnPostAddExternalMemberAsync(eventId, teamId, "Concurrent External", 1m, null, CancellationToken.None);
        }

        var results = await Task.WhenAll(
            AddAsync(firstEventId, firstTeamId, "first external member"),
            AddAsync(secondEventId, secondTeamId, "second external member"));
        Assert.All(results, result => Assert.IsType<RedirectToPageResult>(result));

        await using var verification = new ApplicationDbContext(options);
        var character = await verification.OsrsCharacters.SingleAsync(item => item.NormalizedName == "CONCURRENT EXTERNAL");
        var participants = await verification.EventParticipants
            .Where(item => item.EventId == firstEventId || item.EventId == secondEventId)
            .OrderBy(item => item.EventId)
            .ToListAsync();
        Assert.Equal(2, participants.Count);
        Assert.All(participants, participant =>
        {
            Assert.Equal(Bingo.Domain.Signups.SignupSource.AdminCreated, participant.Source);
            Assert.Equal(Bingo.Domain.Signups.SignupStatus.Confirmed, participant.SignupStatus);
        });
        var firstParticipantId = participants.Single(participant => participant.EventId == firstEventId).Id;
        var secondParticipantId = participants.Single(participant => participant.EventId == secondEventId).Id;
        Assert.Equal(2, await verification.EventParticipantCharacters.CountAsync(item =>
            (item.EventId == firstEventId || item.EventId == secondEventId) && item.OsrsCharacterId == character.Id && item.ReleasedAt == null));
        Assert.Equal(firstTeamId, await verification.TeamMemberships.Where(item => item.EventParticipantId == firstParticipantId && item.LeftAt == null).Select(item => item.TeamId).SingleAsync());
        Assert.Equal(secondTeamId, await verification.TeamMemberships.Where(item => item.EventParticipantId == secondParticipantId && item.LeftAt == null).Select(item => item.TeamId).SingleAsync());
        Assert.Equal(2, await verification.AuditEntries.CountAsync(item =>
            item.Action == "team.member_added" && item.ActorAccountId == adminId &&
            (item.TargetId == firstTeamId.ToString() || item.TargetId == secondTeamId.ToString())));
    }

    [Fact]
    public async Task DraftTeamFeedbackAndManagedImageProjectionAreSafeAndPersistent()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice5-draft-feedback-admin", GlobalRole.Admin);
        var ev = new BingoEvent(Guid.NewGuid(), "Draft feedback", "slice5-draft-feedback", "UTC", admin.Id, time.GetUtcNow());
        ev.ConfigureSchedule(null, null, null, time.GetUtcNow().AddHours(1), time.GetUtcNow().AddDays(1), 10);
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1);
        var team = new Team(Guid.NewGuid(), ev.Id, "External", "external", TeamFormationType.Preformed, null, false);
        var reservedParticipant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 1, time.GetUtcNow(), SignupSource.AdminCreated);
        var reservedCharacter = new OsrsCharacter(Guid.NewGuid(), "Reserved", "RESERVED", time.GetUtcNow());
        var reservedAssignment = new EventParticipantCharacter(Guid.NewGuid(), ev.Id, reservedParticipant.Id, reservedCharacter.Id, 0, time.GetUtcNow(), admin.Id, null, EventCharacterRole.Playing, 1m, EhbSource.AdminCorrection, null);
        db.AddRange(admin, ev, draft, team, reservedParticipant, reservedCharacter, reservedAssignment);
        await db.SaveChangesAsync();

        Bingo.Web.Pages.Admin.Events.DraftModel Page(IEvidenceStorage? storage = null)
        {
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Name, admin.LoginName)], "test")) };
            return new Bingo.Web.Pages.Admin.Events.DraftModel(db, time, new AuditWriter(db, time), new NoopCollaborationNotifier(), new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, time), storage)
            {
                PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
                TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
            };
        }

        var duplicateTeam = Page();
        Assert.IsType<RedirectToPageResult>(await duplicateTeam.OnPostAddTeamAsync(ev.Id, team.Name, TeamFormationType.Preformed, null, CancellationToken.None));
        Assert.Equal("A team with that name already exists for this event.", duplicateTeam.TempData["StatusMessage"]);
        Assert.Equal(1, await db.Teams.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());

        var duplicateMember = Page();
        Assert.IsType<RedirectToPageResult>(await duplicateMember.OnPostAddExternalMemberAsync(ev.Id, team.Id, "Reserved", 5m, null, CancellationToken.None));
        Assert.Contains("could not be added", duplicateMember.TempData["StatusMessage"]?.ToString());
        Assert.Equal(1, await db.EventParticipants.CountAsync());
        Assert.Equal(1, await db.EventParticipantCharacters.CountAsync());
        Assert.Equal(0, await db.TeamMemberships.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
        db.ChangeTracker.Clear();

        var storage = new SeedEvidenceStorage();
        var upload = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "image", "team.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        var update = Page(storage);
        Assert.IsType<RedirectToPageResult>(await update.OnPostUpdateTeamAsync(ev.Id, team.Id, team.Name, null, upload, false, team.Version, CancellationToken.None));
        db.ChangeTracker.Clear();
        var persisted = await db.Teams.SingleAsync(item => item.Id == team.Id);
        Assert.NotNull(persisted.ActiveImageAssetId);
        Assert.Equal(persisted.ActiveImageAssetId, await db.TeamImageAssets.Where(item => item.TeamId == team.Id && item.ReplacedAt == null).Select(item => (Guid?)item.Id).SingleAsync());

        var projection = Page(storage);
        Assert.IsType<PageResult>(await projection.OnGetAsync(ev.Id, null, CancellationToken.None));
        Assert.Contains("handler=TeamImage", projection.Teams.Single().ImageUrl);
        Assert.IsType<FileStreamResult>(await projection.OnGetTeamImageAsync(ev.Id, team.Id, CancellationToken.None));
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
        emergencyEvent.OpenSignups();
        emergencyEvent.CloseSignups();
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
        Assert.Contains("Opsætningen af nødkontoen er fuldført. En administrator skal stadig aktivere den, før den kan bruges.", loginContent, StringComparison.Ordinal);
        Assert.Contains("app-toast-success", loginContent, StringComparison.Ordinal);

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
        ev.OpenSignups();
        ev.CloseSignups();
        ev.StartEvent(now);
        ev.EndEvent();
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
        var reopen = new Bingo.Web.Pages.Admin.Events.ManageModel(db, new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, clock), new AuditWriter(db, clock), null!, null!, null!, null!, clock)
        {
            PageContext = new PageContext(new ActionContext(reopenContext, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(reopenContext, new DictionaryTempDataProvider()),
            EventVersion = ev.Version,
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
    public async Task ManageReopenParsesEventLocalWallTimeAsUtc()
    {
        var now = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero).AddMicroseconds(123456);
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-event-local-admin", GlobalRole.Admin);
        var ev = new BingoEvent(Guid.NewGuid(), "Event-local parsing", $"event-local-{Guid.NewGuid():N}", "Europe/Copenhagen", admin.Id, now.AddDays(-3));
        ev.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-2), now.AddHours(-1), 10);
        ev.OpenSignups();
        ev.CloseSignups();
        ev.StartEvent(now.AddHours(-2));
        ev.EndEvent();
        db.AddRange(admin, ev);
        await db.SaveChangesAsync();

        var context = new DefaultHttpContext { User = new AccountAuthenticationService(db, passwords, clock).CreatePrincipal(admin) };
        var model = new Bingo.Web.Pages.Admin.Events.ManageModel(db, new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, clock), new AuditWriter(db, clock), null!, null!, null!, null!, clock)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new DictionaryTempDataProvider()),
            EventVersion = ev.Version,
            ReopenUntilLocal = "2026-08-30T15:00",
            StateReason = "Event-local parsing coverage."
        };

        Assert.IsType<RedirectToPageResult>(await model.OnPostReopenSubmissionsAsync(ev.Id, CancellationToken.None));
        Assert.Equal(new DateTimeOffset(2026, 8, 30, 13, 0, 0, TimeSpan.Zero), await db.Events.Where(x => x.Id == ev.Id).Select(x => x.ReopenedSubmissionCutoffAt).SingleAsync());
    }

    [Theory]
    [InlineData("2026-03-29T02:30")]
    [InlineData("2026-10-25T02:30")]
    public async Task ManageRejectsInvalidOrAmbiguousEventLocalWallTimeWithoutChangingCutoffOrAuditing(string localTime)
    {
        var now = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        var admin = Website("slice1-event-local-dst-admin", GlobalRole.Admin);
        var ev = new BingoEvent(Guid.NewGuid(), "Event-local DST parsing", $"event-local-dst-{Guid.NewGuid():N}", "Europe/Copenhagen", admin.Id, now.AddDays(-3));
        ev.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-2), now.AddHours(-1), 10);
        ev.OpenSignups();
        ev.CloseSignups();
        ev.StartEvent(now.AddHours(-2));
        ev.EndEvent();
        var existingCutoff = now.AddHours(1);
        ev.ReopenSubmissions(existingCutoff, now);
        db.AddRange(admin, ev);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var auditCount = await db.AuditEntries.CountAsync();

        var context = new DefaultHttpContext { User = new AccountAuthenticationService(db, passwords, clock).CreatePrincipal(admin) };
        var model = new Bingo.Web.Pages.Admin.Events.ManageModel(db, new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, clock), new AuditWriter(db, clock), null!, null!, null!, null!, clock)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new DictionaryTempDataProvider()),
            EventVersion = ev.Version,
            ReopenUntilLocal = localTime,
            StateReason = "Event-local DST parsing coverage."
        };

        Assert.IsType<RedirectToPageResult>(await model.OnPostReopenSubmissionsAsync(ev.Id, CancellationToken.None));
        Assert.False(model.ModelState.IsValid);
        Assert.Equal(existingCutoff, await db.Events.Where(x => x.Id == ev.Id).Select(x => x.ReopenedSubmissionCutoffAt).SingleAsync());
        Assert.Equal(auditCount, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task DevelopmentDklManualObjectiveHasNoEventOrApprovalDropSnapshots()
    {
        var seededAt = DateTimeOffset.UtcNow;
        var clock = new MutableTimeProvider(seededAt);
        await using var db = new ApplicationDbContext(options);
        const string ownerUsername = "slice1-manual-objective-owner";
        await new OperatorRecoveryService(db, clock, passwords).BootstrapOwnerAsync(ownerUsername, "long-test-password", ownerUsername, CancellationToken.None);
        await new CatalogueSnapshotService(db, clock).ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));
        await new DevelopmentScenarioSeeder(db, new DevelopmentEnvironment(), passwords, new SeedEvidenceStorage(), clock).ResetAndSeedAsync();

        var live = await db.Events.SingleAsync(item => item.Slug == "test-15-dkl-live");
        var boardId = await db.Boards.Where(item => item.EventId == live.Id).Select(item => item.Id).SingleAsync();
        var manual = await (from requirement in db.BoardRequirementSnapshots
                            join tile in db.BoardTiles on requirement.BoardTileId equals tile.Id
                            where tile.BoardId == boardId && tile.NameSnapshot == "Superior Slayer" && requirement.ManualObjective
                            select new { requirement.Id, requirement.Description, requirement.TargetContribution, tile.EstimatedEhbSnapshot }).SingleAsync();
        Assert.Equal(4, manual.TargetContribution);
        Assert.Equal(21m, manual.EstimatedEhbSnapshot);
        Assert.Contains("Imbued heart", manual.Description, StringComparison.Ordinal);
        Assert.Empty(await db.BoardRequirementDropSnapshots.Where(item => item.RequirementId == manual.Id).ToListAsync());

        var approvalRequirementIds = await (from requirement in db.BoardApprovalRequirementSnapshots
                                            join tile in db.BoardApprovalTileSnapshots on requirement.ApprovalTileSnapshotId equals tile.Id
                                            join approval in db.BoardApprovalSnapshots on tile.ApprovalSnapshotId equals approval.Id
                                            where approval.BoardId == boardId && requirement.ManualObjective
                                            select requirement.Id).ToListAsync();
        Assert.Single(approvalRequirementIds);
        Assert.Empty(await db.BoardApprovalRequirementDropSnapshots.Where(item => approvalRequirementIds.Contains(item.ApprovalRequirementSnapshotId)).ToListAsync());
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
        var retainedBoardCharacter = new OsrsCharacter(Guid.NewGuid(), "03 Captain Alpha", "03 CAPTAIN ALPHA", seededAt);
        var retainedLiveCharacter = new OsrsCharacter(Guid.NewGuid(), "Dev Player 001", "DEV PLAYER 001", seededAt);
        var retainedLink = new AccountOsrsCharacter(Guid.NewGuid(), admin.Id, retainedLiveCharacter.Id, true, 0, seededAt);
        db.AddRange(retainedBoardCharacter, retainedLiveCharacter, retainedLink);
        var obsolete = new Bingo.Domain.Events.BingoEvent(Guid.NewGuid(), "TEST 00 — Obsolete", "test-00-obsolete", "UTC", admin.Id, seededAt);
        var manual = new Bingo.Domain.Events.BingoEvent(Guid.NewGuid(), "S4 Manual Test", "s4-manual-test", "UTC", admin.Id, seededAt);
        var manualForm = new SignupForm(Guid.NewGuid(), manual.Id, seededAt);
        var cancelled = new Bingo.Domain.Events.BingoEvent(Guid.NewGuid(), "Cancelled tombstone", "cancelled-tombstone", "UTC", admin.Id, seededAt);
        cancelled.Cancel(admin.Id, seededAt, "Reset regression", protectedHistoryExists: true);
        var discarded = new Bingo.Domain.Events.BingoEvent(Guid.NewGuid(), "Discarded tombstone", "discarded-tombstone", "UTC", admin.Id, seededAt);
        discarded.Discard(admin.Id, seededAt, protectedHistoryExists: false);
        db.AddRange(
            obsolete, manual, manualForm, cancelled, discarded,
            new SignupQuestion(Guid.NewGuid(), manualForm.Id, manual.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing),
            new SignupQuestion(Guid.NewGuid(), manualForm.Id, manual.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer));
        await db.SaveChangesAsync();
        var catalogueCount = await db.CatalogueItems.CountAsync();
        var seeder = new DevelopmentScenarioSeeder(db, new DevelopmentEnvironment(), passwords, new SeedEvidenceStorage(), clock);
        var result = await seeder.ResetAndSeedAsync();
        await AssertSlice6FixtureInvariantsAsync(db, clock.GetUtcNow());

        var boundaryNow = clock.GetUtcNow();
        var currentBoundaryEvent = new BingoEvent(Guid.NewGuid(), "Test-owned current event", "test-owned-current-event", "UTC", admin.Id, boundaryNow);
        currentBoundaryEvent.ConfigureSchedule(boundaryNow.AddDays(-3), boundaryNow.AddDays(-2), null, boundaryNow.AddDays(-1), boundaryNow.AddDays(3), 20);
        currentBoundaryEvent.OpenSignups(boundaryNow.AddDays(-3));
        currentBoundaryEvent.CloseSignups(boundaryNow.AddDays(-2));
        currentBoundaryEvent.StartEvent(boundaryNow.AddDays(-1));
        currentBoundaryEvent.MarkFirstPublic(boundaryNow.AddDays(-2));
        db.Events.Add(currentBoundaryEvent);
        await db.SaveChangesAsync();
        var initialScheduledEvent = await db.Events.SingleAsync(item => item.Slug == "test-05-scheduled-lifecycle-blockers");
        var initialEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        try
        {
            var readiness = await new EventLifecycleService(db, null!, clock).GetStartReadinessAsync(initialScheduledEvent.Id);
            Assert.Contains(readiness!.Blockers, blocker => blocker.Code == "CURRENT_EVENT_EXISTS" && blocker.Description.Contains("Test-owned current event", StringComparison.Ordinal));
            var signupLifecycle = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, new ConfigurationBuilder().Build()), clock);
            await signupLifecycle.ProcessDueSignupAsync();
            var overlapEvent = await db.Events.SingleAsync(item => item.Slug == "test-91-overlapping-scheduled-opening");
            var overlapAttempt = await db.ScheduledSignupOpeningAttempts.SingleAsync(item => item.EventId == overlapEvent.Id);
            Assert.Contains("EVENT_WINDOW_OVERLAP", overlapAttempt.Blockers);
            var readinessEvaluator = new EventReadinessEvaluator(db, new ConfigurationBuilder().Build());
            var missingFormReadiness = await readinessEvaluator.GetSignupReadinessAsync(
                (await db.Events.SingleAsync(item => item.Slug == "test-92-missing-signup-form")).Id,
                SignupOpeningMode.OpenNow, clock.GetUtcNow());
            Assert.Contains(missingFormReadiness!.Blockers, blocker => blocker.Code == "SIGNUP_FORM_MISSING");
            var malformedQuestionsReadiness = await readinessEvaluator.GetSignupReadinessAsync(
                (await db.Events.SingleAsync(item => item.Slug == "test-93-malformed-signup-questions")).Id,
                SignupOpeningMode.OpenNow, clock.GetUtcNow());
            Assert.Contains(malformedQuestionsReadiness!.Blockers, blocker => blocker.Code == "SIGNUP_QUESTIONS_INVALID");
            var unusableCodeReadiness = await readinessEvaluator.GetSignupReadinessAsync(
                (await db.Events.SingleAsync(item => item.Slug == "test-94-unusable-signup-code")).Id,
                SignupOpeningMode.OpenNow, clock.GetUtcNow());
            Assert.Contains(unusableCodeReadiness!.Blockers, blocker => blocker.Code == "SIGNUP_CODE_UNUSABLE");
            var missingCloseReadiness = await readinessEvaluator.GetSignupReadinessAsync(
                (await db.Events.SingleAsync(item => item.Slug == "test-95-missing-scheduled-window")).Id,
                SignupOpeningMode.ScheduleOpening, clock.GetUtcNow());
            Assert.Contains(missingCloseReadiness!.Blockers, blocker => blocker.Code == "SIGNUP_CLOSE_REQUIRED");
            Assert.Contains(missingCloseReadiness.Blockers, blocker => blocker.Code == "SCHEDULED_OPENING_INVALID");
            var invalidWindowReadiness = await readinessEvaluator.GetSignupReadinessAsync(
                (await db.Events.SingleAsync(item => item.Slug == "test-96-invalid-scheduled-window")).Id,
                SignupOpeningMode.ScheduleOpening, clock.GetUtcNow());
            Assert.Contains(invalidWindowReadiness!.Blockers, blocker => blocker.Code == "SIGNUP_CLOSE_NOT_FUTURE");
            Assert.Contains(invalidWindowReadiness.Blockers, blocker => blocker.Code == "SCHEDULED_WINDOW_INVALID");
            var afterStartReadiness = await readinessEvaluator.GetSignupReadinessAsync(
                (await db.Events.SingleAsync(item => item.Slug == "test-97-signup-closes-after-event")).Id,
                SignupOpeningMode.ScheduleOpening, clock.GetUtcNow());
            Assert.Contains(afterStartReadiness!.Blockers, blocker => blocker.Code == "SIGNUP_CLOSE_AFTER_EVENT_START");
            var startLifecycle = new EventLifecycleService(db, null!, clock);
            var missingPlayingReadiness = await startLifecycle.GetStartReadinessAsync(
                (await db.Events.SingleAsync(item => item.Slug == "test-98-missing-playing-assignment")).Id);
            Assert.Contains(missingPlayingReadiness!.Blockers, blocker => blocker.Code == "PARTICIPANT_PLAYING_ASSIGNMENT_INVALID");
            var discardEventId = (await db.Events.SingleAsync(item => item.Slug == "test-03-draft-discard-candidate")).Id;
            var discardModel = new Bingo.Web.Pages.Admin.Events.ManageModel(
                db, new NoopSignupService(), new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, clock),
                new AuditWriter(db, clock), readinessEvaluator,
                new EventSignupLifecycleService(db, readinessEvaluator, clock), startLifecycle, null!, clock);
            Assert.IsType<PageResult>(await discardModel.OnGetAsync(discardEventId, CancellationToken.None));
            Assert.True(discardModel.CanDiscard);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", initialEnvironment);
        }
        currentBoundaryEvent.EndEvent(clock.GetUtcNow());
        currentBoundaryEvent.FinalizeResults(clock.GetUtcNow());
        currentBoundaryEvent.Archive(clock.GetUtcNow());
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var test84ForReopen = await db.Events.SingleAsync(item => item.Slug == "test-84-evidence-history");
        var versionOneMetrics = await db.OfficialPlacements.AsNoTracking()
            .Where(item => item.EventId == test84ForReopen.Id)
            .OrderBy(item => item.TeamName)
            .Select(item => new { item.TeamId, item.Placement, item.BoardComplete, item.BoardCompletedAt, item.CompletedLines, item.CompletedTiles, item.EhbTiebreak })
            .ToListAsync();
        var finalization = new EventFinalizationService(db, new PublicBoardService(db, clock), clock);
        var actor = new LifecycleActor(admin.Id, admin.LoginName);
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        try
        {
            await finalization.UnfinalizeAsync(test84ForReopen.Id, "Focused re-finalization parity check.", true, actor);
            var readiness = await finalization.GetReadinessAsync(test84ForReopen.Id) ?? throw new InvalidOperationException("TEST 84 readiness was not available after unfinalizing.");
            Assert.Equal(EventState.AwaitingFinalReview, readiness.State);
            foreach (var teamId in readiness.Placements.Where(item => item.BoardComplete).Select(item => item.TeamId).ToList())
            {
                await finalization.AcknowledgeCompletionTimeAsync(test84ForReopen.Id, teamId, admin.Id, readiness.EventVersion, readiness.ReviewCycleId);
                readiness = await finalization.GetReadinessAsync(test84ForReopen.Id) ?? throw new InvalidOperationException("TEST 84 readiness was not available after acknowledging completion.");
            }
            foreach (var tie in readiness.Blockers.Where(item => item.CanOverride && !item.IsCompletionTimeAcknowledgement).ToList())
            {
                await finalization.ResolveBlockerAsync(test84ForReopen.Id, tie.Key, "Focused parity fixture tie acknowledgement.", true, admin.Id, readiness.EventVersion, readiness.ReviewCycleId);
                readiness = await finalization.GetReadinessAsync(test84ForReopen.Id) ?? throw new InvalidOperationException("TEST 84 readiness was not available after resolving the tie.");
            }
            Assert.True(readiness.CanFinalize);
            await finalization.FinalizeAsync(test84ForReopen.Id, actor, readiness.EventVersion);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
        }

        db.ChangeTracker.Clear();
        var versionTwoMetrics = await db.OfficialPlacements.AsNoTracking()
            .Where(item => item.EventId == test84ForReopen.Id && item.FinalizationId == db.EventFinalizations.Where(final => final.EventId == test84ForReopen.Id && final.UnfinalizedAt == null).Select(final => final.Id).Single())
            .OrderBy(item => item.TeamName)
            .Select(item => new { item.TeamId, item.Placement, item.BoardComplete, item.BoardCompletedAt, item.CompletedLines, item.CompletedTiles, item.EhbTiebreak })
            .ToListAsync();
        Assert.Equal(versionOneMetrics, versionTwoMetrics);
        Assert.Equal(2, await db.EventFinalizations.CountAsync(item => item.EventId == test84ForReopen.Id));
        Assert.NotNull(await db.EventFinalizations.Where(item => item.EventId == test84ForReopen.Id && item.Version == 1).Select(item => item.UnfinalizedAt).SingleAsync());
        Assert.Single(await db.EventFinalizations.Where(item => item.EventId == test84ForReopen.Id && item.Version == 2 && item.UnfinalizedAt == null).ToListAsync());
        await finalization.ArchiveAsync(test84ForReopen.Id, true, actor);

        var historyAssetId = await (from asset in db.EvidenceAssets
                                    join submission in db.Submissions on asset.SubmissionId equals submission.Id
                                    where submission.EventId == test84ForReopen.Id && submission.Status == Bingo.Domain.Evidence.SubmissionStatus.Rejected
                                    select asset.Id).SingleAsync();
        var workerClock = new MutableTimeProvider(Assert.IsType<DateTimeOffset>(test84ForReopen.EventEndsAt).AddTicks(-1));
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString())
                .ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(workerClock);
                    services.RemoveAll<IEvidenceStorage>();
                    services.AddSingleton<IEvidenceStorage, SeedEvidenceStorage>();
                }));
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var unrelatedClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        async Task SignInAsync(HttpClient client, string username, string password)
        {
            var login = await client.GetStringAsync("/Account/Login");
            var token = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
            using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = username,
                ["Input.Password"] = password,
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await SignInAsync(ownerClient, DevelopmentScenarioSeeder.EvidenceParticipantUsername, DevelopmentScenarioSeeder.EvidenceParticipantPassword);
        var myEvents = await ownerClient.GetStringAsync("/Account/MyEvents");
        var evidenceLink = $"/Evidence/{historyAssetId}";
        Assert.Contains($"href=\"{evidenceLink}\"", myEvents, StringComparison.Ordinal);
        using var ownerEvidence = await ownerClient.GetAsync(evidenceLink);
        Assert.Equal(HttpStatusCode.OK, ownerEvidence.StatusCode);

        await SignInAsync(unrelatedClient, DevelopmentScenarioSeeder.ReplacementUsername, DevelopmentScenarioSeeder.ReplacementPassword);
        var unrelatedEvents = await unrelatedClient.GetStringAsync("/Account/MyEvents");
        Assert.DoesNotContain("Påskebingo 2026", unrelatedEvents, StringComparison.Ordinal);
        using var unrelatedEvidence = await unrelatedClient.GetAsync(evidenceLink);
        Assert.Equal(HttpStatusCode.Redirect, unrelatedEvidence.StatusCode);
        Assert.Equal("/Account/AccessDenied", unrelatedEvidence.Headers.Location?.ToString());
        using var anonymousEvidence = await anonymousClient.GetAsync(evidenceLink);
        Assert.Equal(HttpStatusCode.NotFound, anonymousEvidence.StatusCode);

        async Task AssertEvidenceFixtureOwnersAsync()
        {
            var live = await db.Events.SingleAsync(item => item.Slug == "test-15-dkl-live");
            var firstTeam = await db.Teams.Where(item => item.EventId == live.Id).OrderBy(item => item.DraftPosition).FirstAsync();
            var ownedRows = await (from membership in db.TeamMemberships
                                   join participant in db.EventParticipants on membership.EventParticipantId equals participant.Id
                                   join account in db.Accounts on participant.AccountId equals account.Id
                                   where membership.TeamId == firstTeam.Id && membership.LeftAt == null
                                   select new { membership.Role, account.LoginName, participant.Id }).ToListAsync();
            Assert.Equal(ownedRows.Count, ownedRows.Select(row => row.LoginName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains(ownedRows, row => row.LoginName == DevelopmentScenarioSeeder.EvidenceCaptainUsername && row.Role == TeamMembershipRole.Captain);
            Assert.Contains(ownedRows, row => row.LoginName == DevelopmentScenarioSeeder.EvidenceCoCaptainUsername && row.Role == TeamMembershipRole.CoCaptain);
            Assert.Contains(ownedRows, row => row.LoginName == DevelopmentScenarioSeeder.EvidenceParticipantUsername && row.Role == TeamMembershipRole.Participant);
        }

        await AssertEvidenceFixtureOwnersAsync();

        db.ChangeTracker.Clear();
        var test62BeforeReopen = await db.Events.SingleAsync(item => item.Slug == "test-62-board-publication-setup");
        var draftBeforeReopen = await db.DraftSessions.SingleAsync(item => item.EventId == test62BeforeReopen.Id);
        var activeCycleBeforeReopen = await db.DraftPublicationCycles.SingleAsync(item => item.DraftSessionId == draftBeforeReopen.Id && item.SupersededAt == null);
        var frozenRosterCount = await db.DraftPublicationRosters.CountAsync(item => item.DraftPublicationCycleId == activeCycleBeforeReopen.Id);
        var reopenContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Name, admin.LoginName)], "test"))
        };
        var reopenPage = new DraftModel(
            db, clock, new AuditWriter(db, clock), new NoopCollaborationNotifier(), null!,
            new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, clock))
        {
            PageContext = new PageContext(new ActionContext(reopenContext, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(reopenContext, new DictionaryTempDataProvider())
        };
        Assert.IsType<RedirectToPageResult>(await reopenPage.OnPostReopenAsync(
            test62BeforeReopen.Id, true, "Seed publication correction check.", CancellationToken.None));
        db.ChangeTracker.Clear();
        var test62AfterReopen = await db.Events.SingleAsync(item => item.Id == test62BeforeReopen.Id);
        var draftAfterReopen = await db.DraftSessions.SingleAsync(item => item.Id == draftBeforeReopen.Id);
        Assert.False(test62AfterReopen.TeamRostersPublished);
        Assert.Equal(DraftState.Running, draftAfterReopen.State);
        Assert.NotNull(await db.DraftPublicationCycles.Where(item => item.Id == activeCycleBeforeReopen.Id).Select(item => item.SupersededAt).SingleAsync());
        Assert.Empty(await db.DraftPublicationCycles.Where(item => item.DraftSessionId == draftBeforeReopen.Id && item.SupersededAt == null).ToListAsync());
        Assert.Equal(frozenRosterCount, await db.DraftPublicationRosters.CountAsync(item => item.DraftPublicationCycleId == activeCycleBeforeReopen.Id));
        Assert.Single(await db.AuditEntries.Where(item => item.TargetId == draftBeforeReopen.Id.ToString() && item.Action == "draft.reopened").ToListAsync());

        db.ChangeTracker.Clear();
        var liveFixtureBeforeRepeat = await db.Events.SingleAsync(item => item.Slug == "test-15-dkl-live");
        var teamBeforeRepeat = await db.Teams.FirstAsync(item => item.EventId == liveFixtureBeforeRepeat.Id);
        var membershipBeforeRepeat = await db.TeamMemberships.FirstAsync(item => item.TeamId == teamBeforeRepeat.Id);
        var sessionBeforeRepeat = await db.DraftSessions.SingleAsync(item => item.EventId == liveFixtureBeforeRepeat.Id);
        var seededPublication = await db.DraftPublicationCycles.SingleAsync(
            item => item.DraftSessionId == sessionBeforeRepeat.Id && item.SupersededAt == null);
        seededPublication.Supersede(clock.GetUtcNow(), admin.Id, "Reset regression");
        var publication = new DraftPublicationCycle(Guid.NewGuid(), sessionBeforeRepeat.Id, 99, clock.GetUtcNow(), admin.Id);
        db.AddRange(
            new TeamImageAsset(Guid.NewGuid(), liveFixtureBeforeRepeat.Id, teamBeforeRepeat.Id, "reset-regression.png", "reset-regression.png", "image/png", 1, 1, 1, new string('a', 64), admin.Id, clock.GetUtcNow()),
            new TeamMembershipRoleTransition(Guid.NewGuid(), membershipBeforeRepeat.Id, TeamMembershipRole.Participant, TeamMembershipRole.Captain, admin.Id, clock.GetUtcNow()),
            publication,
            new DraftPublicationRoster(Guid.NewGuid(), publication.Id, teamBeforeRepeat.Id, membershipBeforeRepeat.EventParticipantId, TeamMembershipRole.Participant, null, "Reset regression"));
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO team_legacy_image_references (team_id, retired_url, retired_at) VALUES ({teamBeforeRepeat.Id}, {"https://retired.example/reset-regression.png"}, {clock.GetUtcNow()})");

        var repeated = await seeder.ResetAndSeedAsync();
        await AssertSlice6FixtureInvariantsAsync(db, clock.GetUtcNow());
        await AssertEvidenceFixtureOwnersAsync();

        Assert.Equal(ownerUsername, result.AdminUsername);
        Assert.Equal(ownerUsername, repeated.AdminUsername);
        Assert.Equal(DevelopmentScenarioSeeder.SecondaryAdminUsername, result.SecondaryAdminUsername);
        Assert.Equal(21, result.Scenarios.Count);
        var expectedEventNames = new[] { "Børnebingo 2026", "Det Store Danske Efterårsbingo 2026", "Det Store Danske Forårsbingo 2026", "Det Store Danske Forårsbingo 2026", "Det Store Danske Sommerbingo 2027", "Det Store Danske Vinterbingo 2027", "Efterårsbingo 2026", "Efterårsbingo 2027", "Familiebingo 2026", "Forårsbingo 2027", "Februarbingo 2026", "Januarbingo 2026", "Julebingo 2025", "Martsbingo 2026", "Påskebingo 2026", "Sommerbingo 2026", "Sommerferiebingo 2026", "Søndagsbingo 2026", "Vinterbingo 2026", "Vinterbingo 2027", "Weekendbingo 2026" };
        Assert.Equal(expectedEventNames.OrderBy(name => name), result.Scenarios.Select(scenario => scenario.EventName).OrderBy(name => name));
        db.ChangeTracker.Clear();
        var owner = await db.Accounts.SingleAsync(account => account.Id == admin.Id);
        Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
        Assert.True(owner.Active);
        Assert.Single(await db.Accounts.Where(account => account.GlobalRole == GlobalRole.SuperAdmin).ToListAsync());
        var secondaryAdmin = await db.Accounts.SingleAsync(account => account.LoginName == DevelopmentScenarioSeeder.SecondaryAdminUsername);
        Assert.Equal(GlobalRole.Admin, secondaryAdmin.GlobalRole);
        Assert.True(secondaryAdmin.Active);
        var seededEvents = await db.Events.OrderBy(item => item.Slug).ToListAsync();
        Assert.Equal(["test-03-draft-discard-candidate", "test-04-readiness-blockers", "test-05-scheduled-lifecycle-blockers", "test-13-dkl-board", "test-15-dkl-live", "test-16-signup-lookup", "test-21-final-review", "test-22-secondary-playing-finalization", "test-62-board-publication-setup", "test-84-evidence-history", "test-85-archived-results", "test-86-cancelled-event", "test-87-discarded-empty-draft", "test-91-overlapping-scheduled-opening", "test-92-missing-signup-form", "test-93-malformed-signup-questions", "test-94-unusable-signup-code", "test-95-missing-scheduled-window", "test-96-invalid-scheduled-window", "test-97-signup-closes-after-event", "test-98-missing-playing-assignment"], seededEvents.Select(item => item.Slug).OrderBy(slug => slug).ToArray());
        Assert.All(seededEvents, item => Assert.True(item.IsDevelopmentFixture));
        Assert.Single(seededEvents, item => item.State == EventState.Live && item.Slug == "test-15-dkl-live");
        Assert.Equal(
            new Dictionary<string, EventState>
            {
                ["test-03-draft-discard-candidate"] = EventState.Draft,
                ["test-04-readiness-blockers"] = EventState.Draft,
                ["test-05-scheduled-lifecycle-blockers"] = EventState.SignupClosed,
                ["test-13-dkl-board"] = EventState.SignupClosed,
                ["test-15-dkl-live"] = EventState.Live,
                ["test-16-signup-lookup"] = EventState.SignupOpen,
                ["test-21-final-review"] = EventState.AwaitingFinalReview,
                ["test-22-secondary-playing-finalization"] = EventState.SignupClosed,
                ["test-62-board-publication-setup"] = EventState.SignupClosed,
                ["test-84-evidence-history"] = EventState.Finalized,
                ["test-85-archived-results"] = EventState.Archived,
                ["test-86-cancelled-event"] = EventState.Cancelled,
                ["test-87-discarded-empty-draft"] = EventState.Discarded,
                ["test-91-overlapping-scheduled-opening"] = EventState.Draft,
                ["test-92-missing-signup-form"] = EventState.Draft,
                ["test-93-malformed-signup-questions"] = EventState.Draft,
                ["test-94-unusable-signup-code"] = EventState.Draft,
                ["test-95-missing-scheduled-window"] = EventState.Draft,
                ["test-96-invalid-scheduled-window"] = EventState.Draft,
                ["test-97-signup-closes-after-event"] = EventState.Draft,
                ["test-98-missing-playing-assignment"] = EventState.SignupClosed
            },
            seededEvents.ToDictionary(item => item.Slug, item => item.State));
        var draftCandidateEventId = seededEvents.Single(value => value.Slug == "test-03-draft-discard-candidate").Id;
        Assert.Empty(await db.EventParticipants.Where(item => item.EventId == draftCandidateEventId).ToListAsync());
        var scheduledEventId = seededEvents.Single(value => value.Slug == "test-05-scheduled-lifecycle-blockers").Id;
        var scheduledStart = await db.ScheduledEventStartAttempts.SingleAsync(item => item.EventId == scheduledEventId);
        Assert.Equal(["BOARD_NOT_PUBLISHED", "DRAFT_NOT_FINALIZED"], scheduledStart.Blockers);
        var scheduledOpening = await db.ScheduledSignupOpeningAttempts.SingleAsync(item => item.EventId == scheduledEventId);
        Assert.Equal(["LIFECYCLE_STATE_INVALID"], scheduledOpening.Blockers);
        var signupLookupEvent = Assert.Single(seededEvents, item => item.Slug == "test-16-signup-lookup");
        Assert.Equal(EventState.SignupOpen, signupLookupEvent.State);
        Assert.Equal(9, await db.EventParticipants.CountAsync(item => item.EventId == signupLookupEvent.Id));
        Assert.Empty(await db.SignupForms.Where(form => form.EventId == manual.Id).ToListAsync());
        Assert.Equal(catalogueCount, await db.CatalogueItems.CountAsync());
        Assert.Equal(2, await db.OsrsCharacters.CountAsync(character => character.Id == retainedBoardCharacter.Id || character.Id == retainedLiveCharacter.Id));
        Assert.Equal(await db.OsrsCharacters.CountAsync(), await db.OsrsCharacters.Select(character => character.NormalizedName).Distinct().CountAsync());
        var preservedLink = await db.AccountOsrsCharacters.SingleAsync(link => link.Id == retainedLink.Id);
        Assert.Equal(admin.Id, preservedLink.AccountId);
        Assert.Equal(retainedLiveCharacter.Id, preservedLink.OsrsCharacterId);
        var assignments = await (from assignment in db.EventParticipantCharacters
                                 join bingoEvent in db.Events on assignment.EventId equals bingoEvent.Id
                                 where assignment.OsrsCharacterId == retainedBoardCharacter.Id || assignment.OsrsCharacterId == retainedLiveCharacter.Id
                                 select new { bingoEvent.Slug, assignment.OsrsCharacterId }).ToListAsync();
        Assert.Contains(assignments, assignment => assignment.Slug == "test-13-dkl-board" && assignment.OsrsCharacterId == retainedBoardCharacter.Id);
        Assert.Contains(assignments, assignment => assignment.Slug == "test-15-dkl-live" && assignment.OsrsCharacterId == retainedLiveCharacter.Id);
        var fixtureForms = await db.SignupForms.Where(form => seededEvents.Select(item => item.Id).Contains(form.EventId)).ToListAsync();
        var fixtureQuestions = await db.SignupQuestions.Where(question => fixtureForms.Select(form => form.Id).Contains(question.SignupFormId)).ToListAsync();
        Assert.Equal([SignupQuestionType.Account, SignupQuestionType.Number, SignupQuestionType.SingleChoice, SignupQuestionType.Text, SignupQuestionType.YesNo], fixtureQuestions.Select(question => question.Type).Distinct().OrderBy(type => type.ToString()).ToArray());
        Assert.Contains(fixtureQuestions, question => question.AccountAnswerRole == EventCharacterRole.Playing);
        Assert.Contains(fixtureQuestions, question => question.AccountAnswerRole == EventCharacterRole.Informational);
        var fixtureParticipants = await db.EventParticipants.Where(participant => seededEvents.Select(item => item.Id).Contains(participant.EventId) && participant.EventId != signupLookupEvent.Id).ToListAsync();
        Assert.Contains(fixtureParticipants, participant => participant.AccountId is not null);
        Assert.Contains(fixtureParticipants, participant => participant.AccountId is null);
        var fixtureParticipantIds = fixtureParticipants.Select(participant => participant.Id).ToArray();
        Assert.All(await db.EventParticipantCharacters.Where(item => fixtureParticipantIds.Contains(item.EventParticipantId) && item.SignupQuestionId != null).ToListAsync(), assignment =>
        {
            var participant = Assert.Single(fixtureParticipants, item => item.Id == assignment.EventParticipantId);
            var question = Assert.Single(fixtureQuestions, item => item.Id == assignment.SignupQuestionId);
            var form = Assert.Single(fixtureForms, item => item.Id == question.SignupFormId);
            Assert.Equal(participant.EventId, assignment.EventId); Assert.Equal(participant.EventId, question.EventId); Assert.Equal(participant.EventId, form.EventId); Assert.Equal(SignupQuestionType.Account, question.Type);
        });
        Assert.All(await db.SignupAnswers.Where(answer => fixtureParticipantIds.Contains(answer.EventParticipantId)).ToListAsync(), answer =>
        {
            var participant = Assert.Single(fixtureParticipants, item => item.Id == answer.EventParticipantId);
            var question = Assert.Single(fixtureQuestions, item => item.Id == answer.SignupQuestionId);
            var form = Assert.Single(fixtureForms, item => item.Id == question.SignupFormId);
            Assert.Equal(participant.EventId, question.EventId); Assert.Equal(participant.EventId, form.EventId);
        });
        var liveFixture = Assert.Single(seededEvents, item => item.Slug == "test-15-dkl-live");
        var liveBoard = Assert.Single(await db.Boards.Where(item => item.EventId == liveFixture.Id).ToListAsync());
        var liveTiles = await db.BoardTiles.Where(item => item.BoardId == liveBoard.Id).ToListAsync();
        Assert.NotEmpty(liveTiles);
        Assert.NotEmpty(await db.BoardRequirementSnapshots.Where(item => liveTiles.Select(tile => tile.Id).Contains(item.BoardTileId)).ToListAsync());
        var participantProperties = db.Model.FindEntityType(typeof(EventParticipant))!.GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain(participantProperties, property => property.Contains("PrivateEdit", StringComparison.Ordinal) || property is "PrimaryAccountName" or "SecondAccountName" or "DiscordIdentity" or "Comments");
        Assert.DoesNotContain(Enum.GetNames<SignupStatus>(), status => status == "Removed");
        Assert.DoesNotContain(db.Model.GetEntityTypes().Select(type => type.ClrType.Name), name => name.Contains("Csv", StringComparison.Ordinal) || name.Contains("PrivateEdit", StringComparison.Ordinal));

        var access = await db.AccountEventAccesses.FirstAsync(item => item.Enabled);
        var ev = await db.Events.SingleAsync(item => item.Id == access.EventId);
        var cutoff = Assert.IsType<DateTimeOffset>(ev.SubmissionCutoffAt);
        var lifecycle = new EmergencyCredentialLifecycleService(db, clock);
        clock.Set(cutoff.AddMicroseconds(-1)); await lifecycle.ApplyAsync(CancellationToken.None);
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
            var request = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection()
                    .AddSingleton<IAuthenticationService>(auth)
                    .AddSingleton<IUrlHelperFactory, UrlHelperFactory>()
                    .BuildServiceProvider()
            };
            request.Request.Headers.Cookie = issue.Response.Headers.SetCookie.Single()!.Split(';')[0];
            var page = new Bingo.Web.Pages.Account.OnboardingModel(new AccountIdentityService(db, passwords, time), new AccountAuthenticationService(db, passwords, time), state, new PassthroughLocalizer(), Microsoft.Extensions.Logging.Abstractions.NullLogger<Bingo.Web.Pages.Account.OnboardingModel>.Instance, new FixedWiseOldManPlayerLookup())
            {
                PageContext = new PageContext(new ActionContext(request, new RouteData(), new PageActionDescriptor())),
                TempData = new TempDataDictionary(request, new DictionaryTempDataProvider()),
                Input = new Bingo.Web.Pages.Account.OnboardingModel.InputModel { Username = "race-onboarding", OsrsCharacterName = discordId.EndsWith("-a", StringComparison.Ordinal) ? "Race Character A" : "Race Character B", Password = "long-race-password", ConfirmPassword = "long-race-password" }
            };
            var result = await page.OnPostAsync(CancellationToken.None);
            await db.DisposeAsync();
            return (result, page);
        }

        var onboarding = await Task.WhenAll(Onboard("race-onboarding-discord-a"), Onboard("race-onboarding-discord-b"));
        Assert.Equal(1, onboarding.Count(outcome => outcome.Result is LocalRedirectResult));
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
        db.Events.AddRange(
            new BingoEvent(eventId, "Audit event", $"audit-event-{eventId:N}", "UTC", Guid.NewGuid(), now),
            new BingoEvent(otherEventId, "Other audit event", $"other-audit-event-{otherEventId:N}", "UTC", Guid.NewGuid(), now));
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

    private static async Task AssertSlice6FixtureInvariantsAsync(ApplicationDbContext db, DateTimeOffset seededAt)
    {
        db.ChangeTracker.Clear();
        var events = await db.Events.OrderBy(item => item.Slug).ToListAsync();
        Assert.Equal(["test-03-draft-discard-candidate", "test-04-readiness-blockers", "test-05-scheduled-lifecycle-blockers", "test-13-dkl-board", "test-15-dkl-live", "test-16-signup-lookup", "test-21-final-review", "test-22-secondary-playing-finalization", "test-62-board-publication-setup", "test-84-evidence-history", "test-85-archived-results", "test-86-cancelled-event", "test-87-discarded-empty-draft", "test-91-overlapping-scheduled-opening", "test-92-missing-signup-form", "test-93-malformed-signup-questions", "test-94-unusable-signup-code", "test-95-missing-scheduled-window", "test-96-invalid-scheduled-window", "test-97-signup-closes-after-event", "test-98-missing-playing-assignment"], events.Select(item => item.Slug).ToArray());
        var test13 = Assert.Single(events, item => item.Slug == "test-13-dkl-board");
        var test15 = Assert.Single(events, item => item.Slug == "test-15-dkl-live");
        var test84 = Assert.Single(events, item => item.Slug == "test-84-evidence-history");
        var test62 = Assert.Single(events, item => item.Slug == "test-62-board-publication-setup");
        var test16 = Assert.Single(events, item => item.Slug == "test-16-signup-lookup");
        await AssertDklEventInventoryAsync(db, test13, test15, seededAt);
        await AssertSeededDraftPublicationFixturesAsync(db, events);
        Assert.Equal(EventState.SignupOpen, test16.State);
        Assert.True(test16.IsDevelopmentFixture);
        Assert.Equal(6, test16.ParticipantCap);
        Assert.Equal(9, await db.EventParticipants.CountAsync(item => item.EventId == test16.Id));

        var test13Board = Assert.Single(await db.Boards.Where(item => item.EventId == test13.Id).ToListAsync());
        Assert.Equal(BoardState.Draft, test13Board.State);
        Assert.Equal("DKL comparison board", test13Board.Name);
        Assert.Equal(5, test13Board.Rows);
        Assert.Equal(5, test13Board.Columns);
        Assert.Null(test13Board.ActiveApprovalSnapshotId);
        var test13TileIds = await db.BoardTiles.Where(item => item.BoardId == test13Board.Id).Select(item => item.Id).ToListAsync();
        Assert.Equal(25, test13TileIds.Count);
        var test13RequirementIds = await db.BoardRequirementSnapshots.Where(item => test13TileIds.Contains(item.BoardTileId)).Select(item => item.Id).ToListAsync();
        Assert.NotEmpty(test13RequirementIds);
        Assert.NotEmpty(await db.BoardRequirementDropSnapshots.Where(item => test13RequirementIds.Contains(item.RequirementId)).ToListAsync());

        Assert.Equal(EventState.SignupClosed, test62.State);
        Assert.True(test62.IsDevelopmentFixture);
        Assert.True(test62.EventStartsAt > seededAt.AddDays(30));
        Assert.True(test62.TeamRostersPublished);
        Assert.False(test62.BoardPublished);
        var publicationBoard = Assert.Single(await db.Boards.Where(item => item.EventId == test62.Id).ToListAsync());
        Assert.Equal(BoardState.Validated, publicationBoard.State);
        Assert.NotNull(publicationBoard.ActiveApprovalSnapshotId);
        var activeApprovalId = publicationBoard.ActiveApprovalSnapshotId.Value;
        Assert.Single(await db.BoardApprovalSnapshots.Where(item => item.Id == activeApprovalId && item.LifecycleState == BoardState.Validated).ToListAsync());
        var publicationDraft = Assert.Single(await db.DraftSessions.Where(item => item.EventId == test62.Id).ToListAsync());
        Assert.Equal(DraftState.Finalized, publicationDraft.State);
        var publicationCycle = Assert.Single(await db.DraftPublicationCycles
            .Where(item => item.DraftSessionId == publicationDraft.Id && item.SupersededAt == null)
            .ToListAsync());
        var publicationRoster = await db.DraftPublicationRosters
            .Where(item => item.DraftPublicationCycleId == publicationCycle.Id)
            .ToListAsync();
        Assert.NotEmpty(publicationRoster);
        Assert.All(publicationRoster, item => Assert.False(string.IsNullOrWhiteSpace(item.PublicCharacterName)));

        Assert.Equal(EventState.Finalized, test84.State);
        Assert.True(test84.IsDevelopmentFixture);
        Assert.True(test84.ActualEndedAt < seededAt);
        Assert.True(test84.SubmissionCutoffAt < seededAt);
        Assert.True(test84.ResultsPublished);
        var historyCycle = Assert.Single(await db.EventStateTransitions
            .Where(item => item.EventId == test84.Id && item.FromState == EventState.Live && item.ToState == EventState.AwaitingFinalReview)
            .ToListAsync());
        var historyFinalization = Assert.Single(await db.EventFinalizations
            .Where(item => item.EventId == test84.Id && item.Version == 1)
            .ToListAsync());
        Assert.Equal(historyCycle.Id, historyFinalization.ReviewCycleId);
        var historyPlacements = await db.OfficialPlacements
            .Where(item => item.EventId == test84.Id)
            .ToListAsync();
        Assert.NotEmpty(historyPlacements);
        Assert.All(historyPlacements, item => Assert.Equal(historyFinalization.Id, item.FinalizationId));
        var historyParticipant = await (from participant in db.EventParticipants
                                        join account in db.Accounts on participant.AccountId equals account.Id
                                        join membership in db.TeamMemberships on participant.Id equals membership.EventParticipantId
                                        where participant.EventId == test84.Id && membership.LeftAt == null && membership.Role == TeamMembershipRole.Participant
                                        select new { participant.Id, membership.TeamId, account.LoginName }).SingleAsync(item => item.LoginName == DevelopmentScenarioSeeder.EvidenceParticipantUsername);
        Assert.NotEqual(Guid.Empty, historyParticipant.Id);
        var historyRejected = Assert.Single(await db.Submissions.Where(item => item.EventId == test84.Id && item.Status == Bingo.Domain.Evidence.SubmissionStatus.Rejected).ToListAsync());
        Assert.Equal(historyParticipant.TeamId, historyRejected.TeamId);
        Assert.Single(await db.Submissions.Where(item => item.TeamId == historyParticipant.TeamId && item.CreditedParticipantId == historyParticipant.Id && item.Status == Bingo.Domain.Evidence.SubmissionStatus.Rejected).ToListAsync());
        Assert.NotEmpty(await db.Teams.Where(item => item.EventId == test62.Id && item.Active).ToListAsync());
        Assert.NotEmpty(await db.TeamMemberships.Where(item => db.Teams.Where(team => team.EventId == test62.Id).Select(team => team.Id).Contains(item.TeamId) && item.LeftAt == null).ToListAsync());
        await AssertSeedBoardRateSelectionsAsync(db, test62.Id, activeApprovalId);

        var liveBoard = Assert.Single(await db.Boards.Where(item => item.EventId == test15.Id).ToListAsync());
        Assert.Equal(BoardState.Published, liveBoard.State);
        Assert.Equal("DKL comparison board", liveBoard.Name);
        Assert.Equal(5, liveBoard.Rows);
        Assert.Equal(5, liveBoard.Columns);
        Assert.Equal(25, await db.BoardTiles.CountAsync(item => item.BoardId == liveBoard.Id));
        Assert.NotNull(liveBoard.ActiveApprovalSnapshotId);
        Assert.True(test15.BoardPublished);
        Assert.NotEmpty(await db.Submissions.Where(item => item.EventId == test15.Id).ToListAsync());
        await AssertSeedBoardRateSelectionsAsync(db, test15.Id, liveBoard.ActiveApprovalSnapshotId.Value);
    }

    private static async Task AssertSeededDraftPublicationFixturesAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<BingoEvent> events)
    {
        var publishedSlugs = new[]
        {
            "test-15-dkl-live",
            "test-21-final-review",
            "test-62-board-publication-setup",
            "test-84-evidence-history",
            "test-85-archived-results"
        };

        foreach (var slug in publishedSlugs)
        {
            var bingoEvent = Assert.Single(events, item => item.Slug == slug);
            Assert.True(bingoEvent.DraftResultsPublished);
            Assert.True(bingoEvent.TeamRostersPublished);
            var draft = await db.DraftSessions.SingleAsync(item => item.EventId == bingoEvent.Id);
            Assert.Equal(DraftState.Finalized, draft.State);
            var cycle = Assert.Single(await db.DraftPublicationCycles
                .Where(item => item.DraftSessionId == draft.Id && item.SupersededAt == null)
                .ToListAsync());

            var picks = await db.DraftPicks
                .Where(item => item.DraftSessionId == draft.Id && item.UndoneAt == null)
                .ToDictionaryAsync(item => item.Id);
            var memberships = await db.TeamMemberships
                .Where(item => db.Teams.Any(team => team.Id == item.TeamId && team.EventId == bingoEvent.Id && team.Active))
                .ToListAsync();
            var expected = memberships
                .Select(item => (item.TeamId, item.EventParticipantId, item.Role,
                    EffectivePickNumber: item.AssignedByDraftPickId is { } pickId && picks.TryGetValue(pickId, out var pick) ? pick.PickNumber : (int?)null))
                .OrderBy(item => item.TeamId)
                .ThenBy(item => item.EventParticipantId)
                .ThenBy(item => item.Role)
                .ToArray();
            var actual = (await db.DraftPublicationRosters
                    .Where(item => item.DraftPublicationCycleId == cycle.Id)
                    .ToListAsync())
                .Select(item => (item.TeamId, item.EventParticipantId, item.Role, item.EffectivePickNumber))
                .OrderBy(item => item.TeamId)
                .ThenBy(item => item.EventParticipantId)
                .ThenBy(item => item.Role)
                .ToArray();

            Assert.Equal(expected, actual);
            Assert.All(await db.DraftPublicationRosters.Where(item => item.DraftPublicationCycleId == cycle.Id).ToListAsync(),
                item => Assert.False(string.IsNullOrWhiteSpace(item.PublicCharacterName)));
        }

        var intentionallyIncomplete = Assert.Single(events, item => item.Slug == "test-98-missing-playing-assignment");
        Assert.False(intentionallyIncomplete.DraftResultsPublished);
        Assert.False(intentionallyIncomplete.TeamRostersPublished);
        var incompleteDraft = await db.DraftSessions.SingleAsync(item => item.EventId == intentionallyIncomplete.Id);
        Assert.Empty(await db.DraftPublicationCycles
            .Where(item => item.DraftSessionId == incompleteDraft.Id && item.SupersededAt == null)
            .ToListAsync());
    }

    private static async Task AssertDklEventInventoryAsync(
        ApplicationDbContext db,
        BingoEvent test13,
        BingoEvent test15,
        DateTimeOffset seededAt)
    {
        Assert.Equal("Sommerbingo 2026", test13.Name);
        Assert.Equal("test-13-dkl-board", test13.Slug);
        Assert.Equal("Europe/Copenhagen", test13.Timezone);
        Assert.Equal(EventState.SignupClosed, test13.State);
        Assert.True(test13.IsDevelopmentFixture);
        Assert.Equal(20, test13.ParticipantCap);
        Assert.Equal(5, test13.ExpectedBoardRows);
        Assert.Equal(5, test13.ExpectedBoardColumns);
        Assert.True(test13.EventStartsAt > seededAt.AddDays(6));
        var test13Ends = Assert.IsType<DateTimeOffset>(test13.EventEndsAt);
        Assert.Equal(test13Ends.AddMinutes(30), test13.SubmissionCutoffAt);
        Assert.Null(test13.ActualStartedAt);
        Assert.Null(test13.ActualEndedAt);
        Assert.Null(test13.SubmissionsClosedAt);
        Assert.False(test13.DraftLocked);
        Assert.False(test13.BoardPublished);
        Assert.Empty(await db.Teams.Where(item => item.EventId == test13.Id).ToListAsync());
        Assert.Empty(await db.EventParticipants.Where(item => item.EventId == test13.Id && item.SignupStatus == SignupStatus.WaitingList).ToListAsync());

        Assert.Equal("Vinterbingo 2026", test15.Name);
        Assert.Equal("test-15-dkl-live", test15.Slug);
        Assert.Equal("Europe/Copenhagen", test15.Timezone);
        Assert.Equal(EventState.Live, test15.State);
        Assert.True(test15.IsDevelopmentFixture);
        Assert.Equal(60, test15.ParticipantCap);
        Assert.Equal(6, test15.ExpectedTeamCount);
        Assert.Equal(10, test15.ExpectedTeamSize);
        Assert.Equal(5, test15.ExpectedBoardRows);
        Assert.Equal(5, test15.ExpectedBoardColumns);
        var fixtureNow = new DateTimeOffset(seededAt.Year, seededAt.Month, seededAt.Day, seededAt.Hour, seededAt.Minute < 30 ? 0 : 30, 0, TimeSpan.Zero);
        var test15Starts = Assert.IsType<DateTimeOffset>(test15.EventStartsAt);
        Assert.Equal(fixtureNow.AddHours(-99), test15Starts);
        var test15Ends = Assert.IsType<DateTimeOffset>(test15.EventEndsAt);
        Assert.Equal(fixtureNow.AddDays(14), test15Ends);
        Assert.Equal(test15Ends.AddMinutes(30), test15.SubmissionCutoffAt);
        Assert.NotNull(test15.ActualStartedAt);
        Assert.Null(test15.ActualEndedAt);
        Assert.Null(test15.SubmissionsClosedAt);
        Assert.True(test15.DraftLocked);
        Assert.True(test15.BoardPublished);

        var expectedTeams = new[]
        {
            (Name: "Touch kids, not grass", Slug: "touch-kids-not-grass"),
            (Name: "Såeh cs?", Slug: "saeh-cs"),
            (Name: "Morytania Monkeys", Slug: "morytania-monkeys"),
            (Name: "The Agency", Slug: "the-agency"),
            (Name: "Xen0%_d_rops", Slug: "xen0-d-rops"),
            (Name: "Zalamalikum", Slug: "zalamalikum")
        };
        var teams = await db.Teams.Where(item => item.EventId == test15.Id).OrderBy(item => item.DraftPosition).ToListAsync();
        Assert.Equal(expectedTeams.Length, teams.Count);
        Assert.Equal(expectedTeams.Select(item => item.Name), teams.Select(item => item.Name));
        Assert.Equal(expectedTeams.Select(item => item.Slug), teams.Select(item => item.Slug));
        Assert.All(teams, team => Assert.True(team.Active));
        Assert.All(teams, team => Assert.NotNull(team.FinalizedAt));

        var activeMemberships = await db.TeamMemberships
            .Where(item => teams.Select(team => team.Id).Contains(item.TeamId) && item.LeftAt == null)
            .ToListAsync();
        Assert.Equal(60, activeMemberships.Count);
        Assert.Equal(6, activeMemberships.Count(item => item.Role == TeamMembershipRole.Captain));
        Assert.Equal(6, activeMemberships.Count(item => item.Role == TeamMembershipRole.CoCaptain));
        Assert.Equal(48, activeMemberships.Count(item => item.Role == TeamMembershipRole.Participant));
        Assert.Equal(60, await db.EventParticipants.CountAsync(item => item.EventId == test15.Id && item.SignupStatus == SignupStatus.Confirmed));

        var waiting = await db.EventParticipants
            .Where(item => item.EventId == test15.Id && item.SignupStatus == SignupStatus.WaitingList)
            .SingleAsync();
        Assert.Equal(DevelopmentScenarioSeeder.ReplacementUsername, await db.Accounts
            .Where(account => account.Id == waiting.AccountId)
            .Select(account => account.LoginName)
            .SingleAsync());
        Assert.NotEmpty(await db.EventParticipantCharacters.Where(item => item.EventParticipantId == waiting.Id && item.EventRole == EventCharacterRole.Playing && item.ReleasedAt == null).ToListAsync());

        var firstTeam = teams[0];
        var firstTeamOwners = await (from membership in db.TeamMemberships
                                     join participant in db.EventParticipants on membership.EventParticipantId equals participant.Id
                                     join account in db.Accounts on participant.AccountId equals account.Id
                                     where membership.TeamId == firstTeam.Id && membership.LeftAt == null
                                     select new { membership.Role, account.LoginName }).ToListAsync();
        Assert.Contains(firstTeamOwners, item => item.Role == TeamMembershipRole.Captain && item.LoginName == DevelopmentScenarioSeeder.EvidenceCaptainUsername);
        Assert.Contains(firstTeamOwners, item => item.Role == TeamMembershipRole.CoCaptain && item.LoginName == DevelopmentScenarioSeeder.EvidenceCoCaptainUsername);
        Assert.Contains(firstTeamOwners, item => item.Role == TeamMembershipRole.Participant && item.LoginName == DevelopmentScenarioSeeder.EvidenceParticipantUsername);

        var liveAccesses = await db.AccountEventAccesses.Where(item => item.EventId == test15.Id).ToListAsync();
        Assert.Equal(7, liveAccesses.Count);
        Assert.Equal(6, liveAccesses.Count(item => item.Enabled));
        Assert.All(liveAccesses, item => Assert.NotEqual(Guid.Empty, item.TeamId));
        Assert.Equal(1, await db.Accounts.CountAsync(item => item.LoginName == DevelopmentScenarioSeeder.EvidenceDisabledEmergencyUsername));

        var draft = await db.DraftSessions.Where(item => item.EventId == test15.Id).SingleAsync();
        Assert.Equal(DraftState.Finalized, draft.State);
        Assert.Equal(48, await db.DraftPicks.CountAsync(item => item.DraftSessionId == draft.Id && item.UndoneAt == null));
        Assert.Equal(1, await db.BoardApprovalSnapshots.CountAsync(item => item.BoardId == (db.Boards.Where(board => board.EventId == test15.Id).Select(board => board.Id).Single())));
        Assert.Equal(2, await db.Submissions.CountAsync(item => item.EventId == test15.Id && item.Status == Bingo.Domain.Evidence.SubmissionStatus.Pending));
        Assert.Equal(1, await db.Submissions.CountAsync(item => item.EventId == test15.Id && item.Status == Bingo.Domain.Evidence.SubmissionStatus.Rejected));
        Assert.True(await db.Submissions.AnyAsync(item => item.EventId == test15.Id && item.Status == Bingo.Domain.Evidence.SubmissionStatus.Approved));
        var approvedSubmissions = await db.Submissions
            .Where(item => item.EventId == test15.Id && item.Status == Bingo.Domain.Evidence.SubmissionStatus.Approved)
            .ToListAsync();
        Assert.Equal(166, approvedSubmissions.Count);
        var newestSubmission = approvedSubmissions.Max(item => item.SubmittedAt);
        var oldestSubmission = approvedSubmissions.Min(item => item.SubmittedAt);
        Assert.True(newestSubmission - oldestSubmission > TimeSpan.FromHours(90));
        Assert.All(approvedSubmissions, submission =>
        {
            Assert.InRange(submission.SubmittedAt, test15Starts, seededAt);
            var reviewedAt = Assert.IsType<DateTimeOffset>(submission.ReviewedAt);
            Assert.InRange(reviewedAt, submission.SubmittedAt, seededAt);
        });
        Assert.Equal(2, await db.Submissions.CountAsync(submission => submission.EventId == test15.Id && submission.Status == Bingo.Domain.Evidence.SubmissionStatus.Pending && db.EvidenceAssets.Any(asset => asset.SubmissionId == submission.Id)));
    }

    private static async Task AssertSeedBoardRateSelectionsAsync(ApplicationDbContext db, Guid eventId, Guid approvalId)
    {
        var currentDrops = await (from drop in db.BoardRequirementDropSnapshots
                                  join requirement in db.BoardRequirementSnapshots on drop.RequirementId equals requirement.Id
                                  join tile in db.BoardTiles on requirement.BoardTileId equals tile.Id
                                  join board in db.Boards on tile.BoardId equals board.Id
                                  where board.EventId == eventId
                                  select drop).ToListAsync();
        Assert.NotEmpty(currentDrops);

        var frozenDrops = await (from drop in db.BoardApprovalRequirementDropSnapshots
                                 join requirement in db.BoardApprovalRequirementSnapshots on drop.ApprovalRequirementSnapshotId equals requirement.Id
                                 join tile in db.BoardApprovalTileSnapshots on requirement.ApprovalTileSnapshotId equals tile.Id
                                 where tile.ApprovalSnapshotId == approvalId
                                 select drop).ToListAsync();
        Assert.NotEmpty(frozenDrops);
        Assert.All(frozenDrops, drop => Assert.True(drop.NumericProbability is > 0 || (drop.DisplayRate == "Historical item pool" && drop.EhbPerContribution is null)));
    }

    private Account Website(string username, GlobalRole role = GlobalRole.User)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), username, AccountAuthenticationService.NormalizeUsername(username), time.GetUtcNow());
        account.SetPassword(passwords.HashPassword(account, "long-test-password"), false, time.GetUtcNow(), incrementVersion: false);
        account.SetGlobalRole(role);
        return account;
    }

    private static Bingo.Domain.Events.BingoEvent Event(DateTimeOffset now, DateTimeOffset cutoff) =>
        new(Guid.NewGuid(), "Slice 1 cutoff event", $"slice1-cutoff-{Guid.NewGuid():N}", "", "UTC", now.AddDays(-2), now.AddDays(-1), now.AddHours(-1), cutoff.AddMinutes(-30), cutoff, 10, Guid.NewGuid(), now.AddDays(-3));

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
    private sealed class NoopCollaborationNotifier : IAdminCollaborationNotifier { public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask; public Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask; public Task NotifyEventsControlChangedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class NoopSignupService : ISignupService { public Task<int> IncreaseCapacityAndPromoteAsync(Guid eventId, int newCap, CancellationToken cancellationToken = default) => Task.FromResult(0); public Task<int> PromoteAvailablePlacesAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.FromResult(0); }
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

    private sealed class FixedWiseOldManPlayerLookup : IWiseOldManPlayerLookup
    {
        public int Calls { get; private set; }
        public string? LastCharacterName { get; private set; }

        public Task<WiseOldManPlayerLookupResult> LookupPlayerAsync(string characterName, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastCharacterName = characterName;
            return Task.FromResult(new WiseOldManPlayerLookupResult(WiseOldManLookupStatus.Success, 17.5m, DateTimeOffset.UtcNow));
        }
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
