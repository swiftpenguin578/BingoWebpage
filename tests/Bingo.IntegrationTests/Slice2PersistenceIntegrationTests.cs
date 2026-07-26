using System.Globalization;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice2PersistenceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice2_persistence")
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
    public async Task CharacterAndLinkUniquenessIsEnforcedWithoutExclusiveCharacterOwnership()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var first = Website("link-first", now);
        var second = Website("link-second", now);
        var shared = new OsrsCharacter(Guid.NewGuid(), "Shared Main", "SHARED MAIN", now);
        db.AddRange(first, second, shared);
        db.AccountOsrsCharacters.AddRange(
            new AccountOsrsCharacter(Guid.NewGuid(), first.Id, shared.Id, first.Id, true, 0, "Main", 10m, now),
            new AccountOsrsCharacter(Guid.NewGuid(), second.Id, shared.Id, second.Id, true, 0, "Borrowed", 20m, now));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.AccountOsrsCharacters.CountAsync(x => x.OsrsCharacterId == shared.Id));
        Assert.Collection(
            await db.AccountOsrsCharacters.OrderBy(x => x.SavedEhb).Select(x => x.SavedEhb!.Value).ToListAsync(),
            value => Assert.Equal(10m, value),
            value => Assert.Equal(20m, value));

        db.AccountOsrsCharacters.Add(new AccountOsrsCharacter(Guid.NewGuid(), first.Id, shared.Id, first.Id, false, 1, null, null, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.OsrsCharacters.Add(new OsrsCharacter(Guid.NewGuid(), "shared main", "shared main", now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task OnlyOneActivePreferredLinkPerAccountIsEnforced()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var account = Website("preferred-owner", now);
        var first = new OsrsCharacter(Guid.NewGuid(), "Preferred One", "PREFERRED ONE", now);
        var second = new OsrsCharacter(Guid.NewGuid(), "Preferred Two", "PREFERRED TWO", now);
        db.AddRange(account, first, second);
        db.AccountOsrsCharacters.AddRange(
            new AccountOsrsCharacter(Guid.NewGuid(), account.Id, first.Id, account.Id, true, 0, null, null, now),
            new AccountOsrsCharacter(Guid.NewGuid(), account.Id, second.Id, account.Id, true, 1, null, null, now));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ParticipantOwnershipAllowsExternalRowsButOnlyOneOwnerPerEvent()
    {
        var seed = await SeedEventAsync("ownership");
        await using var db = new ApplicationDbContext(options);
        var owner = Website("participant-owner", seed.Now);
        var owned = Participant(seed.EventId, "Owned", 1, seed.Now);
        var duplicate = Participant(seed.EventId, "Duplicate owner", 2, seed.Now);
        var externalOne = Participant(seed.EventId, "External one", 3, seed.Now);
        var externalTwo = Participant(seed.EventId, "External two", 4, seed.Now);
        owned.AssignOwner(owner);
        duplicate.AssignOwner(owner);
        db.AddRange(owner, owned, externalOne, externalTwo);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.EventParticipants.CountAsync(x => x.AccountId == null));

        db.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CurrentAssignmentEventConsistencyUniquenessAndConcurrencyAreEnforced()
    {
        var seed = await SeedEventAsync("assignment");
        Guid firstAssignmentId;
        Guid actorId;
        await using (var db = new ApplicationDbContext(options))
        {
            var actor = Website("assignment-actor", seed.Now);
            actorId = actor.Id;
            var firstParticipant = Participant(seed.EventId, "First assignment", 1, seed.Now);
            var secondParticipant = Participant(seed.EventId, "Second assignment", 2, seed.Now);
            var character = new OsrsCharacter(Guid.NewGuid(), "Contended", "CONTENDED", seed.Now);
            var firstAssignment = Playing(seed.EventId, firstParticipant.Id, character.Id, actor.Id, seed.Now);
            firstAssignmentId = firstAssignment.Id;
            db.AddRange(actor, firstParticipant, secondParticipant, character, firstAssignment);
            await db.SaveChangesAsync();

            db.EventParticipantCharacters.Add(Playing(seed.EventId, secondParticipant.Id, character.Id, actor.Id, seed.Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            var other = await SeedEventAsync("assignment-other");
            db.EventParticipantCharacters.Add(Playing(other.EventId, firstParticipant.Id, character.Id, actor.Id, seed.Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var first = new ApplicationDbContext(options);
        await using var second = new ApplicationDbContext(options);
        var firstCopy = await first.EventParticipantCharacters.SingleAsync(x => x.Id == firstAssignmentId);
        var secondCopy = await second.EventParticipantCharacters.SingleAsync(x => x.Id == firstAssignmentId);
        firstCopy.Release(actorId, seed.Now.AddMinutes(1));
        secondCopy.Release(actorId, seed.Now.AddMinutes(2));
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task MyAccountsAddsReactivatesOrdersAndKeepsSavedEhbPerWebsiteLink()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var first = Website($"my-accounts-first-{Guid.NewGuid():N}", now);
        var second = Website($"my-accounts-second-{Guid.NewGuid():N}", now);
        db.AddRange(first, second);
        await db.SaveChangesAsync();
        var service = new MyAccountsService(db, TimeProvider.System);

        await service.AddOrReactivateAsync(first.Id, "Shared Account", "Main", 11m, CancellationToken.None);
        var original = await db.AccountOsrsCharacters.SingleAsync(item => item.AccountId == first.Id);
        await service.UnlinkAsync(first.Id, original.Id, true, CancellationToken.None);
        await service.AddOrReactivateAsync(first.Id, " shared account ", "Relinked", 12m, CancellationToken.None);
        await service.AddOrReactivateAsync(first.Id, "Second Account", null, null, CancellationToken.None);
        await service.SetPreferredAsync(first.Id, (await db.AccountOsrsCharacters.SingleAsync(item => item.AccountId == first.Id && item.Position == 1)).Id, CancellationToken.None);
        await service.MoveAsync(first.Id, original.Id, 1, CancellationToken.None);
        await service.AddOrReactivateAsync(second.Id, "SHARED ACCOUNT", "Borrowed", 99m, CancellationToken.None);

        var firstLinks = await db.AccountOsrsCharacters.Where(item => item.AccountId == first.Id).OrderBy(item => item.Position).ToListAsync();
        Assert.Equal(2, firstLinks.Count);
        Assert.Equal(original.Id, firstLinks.Single(item => item.OsrsCharacterId == original.OsrsCharacterId).Id);
        Assert.Equal("Relinked", firstLinks.Single(item => item.Id == original.Id).PersonalLabel);
        Assert.Equal(12m, firstLinks.Single(item => item.Id == original.Id).SavedEhb);
        Assert.Single(firstLinks, item => item.Preferred);
        Assert.Equal(99m, await db.AccountOsrsCharacters.Where(item => item.AccountId == second.Id).Select(item => item.SavedEhb).SingleAsync());
    }

    [Fact]
    public async Task MyAccountsMutationsAreScopedToTheAuthenticatedWebsiteAccount()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"my-accounts-owner-{Guid.NewGuid():N}", now);
        var other = Website($"my-accounts-other-{Guid.NewGuid():N}", now);
        db.AddRange(owner, other);
        await db.SaveChangesAsync();
        var service = new MyAccountsService(db, TimeProvider.System);
        await service.AddOrReactivateAsync(owner.Id, "Scoped Character", "Owner", 10m, CancellationToken.None);
        var link = await db.AccountOsrsCharacters.SingleAsync(item => item.AccountId == owner.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(other.Id, link.Id, "Attempted", 20m, CancellationToken.None));

        var unchanged = await db.AccountOsrsCharacters.AsNoTracking().SingleAsync(item => item.Id == link.Id);
        Assert.Equal("Owner", unchanged.PersonalLabel);
        Assert.Equal(10m, unchanged.SavedEhb);
    }

    [Fact]
    public async Task MyAccountsCorrectionPropagatesOnlyToEditableAssignmentsAndPreservesLinkMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"my-accounts-correct-{Guid.NewGuid():N}", now);
        var oldCharacter = new OsrsCharacter(Guid.NewGuid(), "Mispeled", "MISPELED", now);
        var link = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, oldCharacter.Id, owner.Id, true, 4, "Borrowed", 66m, now);
        var open = Event(now, "open-correction", signupOpen: true);
        var closed = Event(now, "closed-correction", signupOpen: false);
        var openParticipant = Participant(open.Id, "Open", 1, now); openParticipant.AssignOwner(owner);
        var closedParticipant = Participant(closed.Id, "Closed", 1, now); closedParticipant.AssignOwner(owner);
        var openAssignment = Playing(open.Id, openParticipant.Id, oldCharacter.Id, owner.Id, now);
        var closedAssignment = Playing(closed.Id, closedParticipant.Id, oldCharacter.Id, owner.Id, now);
        db.AddRange(owner, oldCharacter, link, open, closed, openParticipant, closedParticipant, openAssignment, closedAssignment);
        await db.SaveChangesAsync();

        await new MyAccountsService(db, TimeProvider.System).CorrectAsync(owner.Id, link.Id, "Misspelled", CancellationToken.None);

        var corrected = await db.OsrsCharacters.SingleAsync(item => item.NormalizedName == "MISSPELLED");
        var persistedLink = await db.AccountOsrsCharacters.SingleAsync(item => item.Id == link.Id);
        Assert.Equal(corrected.Id, persistedLink.OsrsCharacterId);
        Assert.Equal("Borrowed", persistedLink.PersonalLabel);
        Assert.Equal(4, persistedLink.Position);
        Assert.True(persistedLink.Preferred);
        Assert.Equal(66m, persistedLink.SavedEhb);
        Assert.Equal(corrected.Id, await db.EventParticipantCharacters.Where(item => item.Id == openAssignment.Id).Select(item => item.OsrsCharacterId).SingleAsync());
        Assert.Equal(oldCharacter.Id, await db.EventParticipantCharacters.Where(item => item.Id == closedAssignment.Id).Select(item => item.OsrsCharacterId).SingleAsync());
    }

    [Fact]
    public async Task MyAccountsCorrectionConflictRollsBackTheEntireMutation()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"my-accounts-conflict-owner-{Guid.NewGuid():N}", now);
        var other = Website($"my-accounts-conflict-other-{Guid.NewGuid():N}", now);
        var oldCharacter = new OsrsCharacter(Guid.NewGuid(), "Tyop", "TYOP", now);
        var corrected = new OsrsCharacter(Guid.NewGuid(), "Typo", "TYPO", now);
        var link = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, oldCharacter.Id, owner.Id, true, 0, null, 5m, now);
        var bingoEvent = Event(now, "conflict-correction", signupOpen: true);
        var ownerParticipant = Participant(bingoEvent.Id, "Owner", 1, now); ownerParticipant.AssignOwner(owner);
        var otherParticipant = Participant(bingoEvent.Id, "Other", 2, now); otherParticipant.AssignOwner(other);
        var oldAssignment = Playing(bingoEvent.Id, ownerParticipant.Id, oldCharacter.Id, owner.Id, now);
        var correctedAssignment = Playing(bingoEvent.Id, otherParticipant.Id, corrected.Id, other.Id, now);
        db.AddRange(owner, other, oldCharacter, corrected, link, bingoEvent, ownerParticipant, otherParticipant, oldAssignment, correctedAssignment);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<MyAccountsCorrectionConflictException>(() => new MyAccountsService(db, TimeProvider.System).CorrectAsync(owner.Id, link.Id, "Typo", CancellationToken.None));

        Assert.Equal(oldCharacter.Id, await db.AccountOsrsCharacters.Where(item => item.Id == link.Id).Select(item => item.OsrsCharacterId).SingleAsync());
        Assert.Equal(oldCharacter.Id, await db.EventParticipantCharacters.Where(item => item.Id == oldAssignment.Id).Select(item => item.OsrsCharacterId).SingleAsync());
    }

    [Fact]
    public async Task WebsiteUsernameRenameIsIndependentOfCharactersAuditedAndPreservesAccountOwnedData()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website("Calm Chris", now);
        var character = new OsrsCharacter(Guid.NewGuid(), "Different OSRS Name", "DIFFERENT OSRS NAME", now);
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "correct-current-password"), false, now, incrementVersion: false);
        owner.SetDiscordIdentity("rename-discord", "Discord display");
        owner.CompleteOnboarding(character.Id, now);
        var bingoEvent = Event(now, "rename-preservation", true);
        var participant = Participant(bingoEvent.Id, "Existing participant", 1, now);
        participant.AssignOwner(owner);
        var assignment = Playing(bingoEvent.Id, participant.Id, character.Id, owner.Id, now);
        var link = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, "Main", 42m, now);
        db.AddRange(owner, character, bingoEvent, participant, assignment, link);
        await db.SaveChangesAsync();

        var originalAuthorizationVersion = owner.AuthorizationVersion;
        var originalPasswordVersion = owner.PasswordVersion;
        var result = await new AccountIdentityService(db, new PasswordHasher<Account>(), TimeProvider.System)
            .RenameUsernameAsync(owner.Id, "  calm CHRIS  ", "correct-current-password", CancellationToken.None);

        Assert.Equal(UsernameRenameResult.Success, result);
        var renamed = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == owner.Id);
        Assert.Equal("calm CHRIS", renamed.LoginName);
        Assert.Equal("calm CHRIS", renamed.PublicUsername);
        Assert.Equal("CALM CHRIS", renamed.NormalizedLoginName);
        Assert.Equal("CALM CHRIS", renamed.NormalizedPublicUsername);
        Assert.Equal(originalAuthorizationVersion, renamed.AuthorizationVersion);
        Assert.Equal(originalPasswordVersion, renamed.PasswordVersion);
        Assert.Equal("rename-discord", renamed.DiscordUserId);
        Assert.Equal(character.Id, renamed.ProfileOsrsCharacterId);
        Assert.Equal(link.Id, await db.AccountOsrsCharacters.Where(x => x.AccountId == owner.Id).Select(x => x.Id).SingleAsync());
        Assert.Equal(42m, await db.AccountOsrsCharacters.Where(x => x.Id == link.Id).Select(x => x.SavedEhb).SingleAsync());
        Assert.Equal(owner.Id, await db.EventParticipants.Where(x => x.Id == participant.Id).Select(x => x.AccountId).SingleAsync());
        Assert.Equal(character.Id, await db.EventParticipantCharacters.Where(x => x.Id == assignment.Id).Select(x => x.OsrsCharacterId).SingleAsync());
        var audit = await db.AuditEntries.SingleAsync(x => x.Action == "account.username_changed");
        Assert.Equal(owner.Id, audit.ActorAccountId);
        Assert.Equal(owner.Id.ToString(), audit.TargetId);
        Assert.Contains("Calm Chris", audit.BeforeState, StringComparison.Ordinal);
        Assert.Contains("calm CHRIS", audit.AfterState, StringComparison.Ordinal);
        Assert.DoesNotContain("correct-current-password", audit.BeforeState + audit.AfterState + audit.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebsiteUsernameRenameRejectsWrongPasswordAndNormalizedCollisionsWithoutMutationOrAudit()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website("rename-owner", now);
        var occupied = Website("Taken Name", now);
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "correct-current-password"), false, now, incrementVersion: false);
        db.AddRange(owner, occupied);
        await db.SaveChangesAsync();
        var identities = new AccountIdentityService(db, new PasswordHasher<Account>(), TimeProvider.System);

        Assert.Equal(UsernameRenameResult.WrongPassword, await identities.RenameUsernameAsync(owner.Id, "new name", "wrong-password", CancellationToken.None));
        Assert.Equal(UsernameRenameResult.UsernameTaken, await identities.RenameUsernameAsync(owner.Id, " taken name ", "correct-current-password", CancellationToken.None));
        var unchanged = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == owner.Id);
        Assert.Equal("rename-owner", unchanged.LoginName);
        Assert.Empty(await db.AuditEntries.Where(x => x.Action == "account.username_changed").ToListAsync());
    }

    [Fact]
    public async Task WebsiteUsernameDatabaseRaceAllowsExactlyOneWinnerAndEmergencyNamesRemainReserved()
    {
        var now = DateTimeOffset.UtcNow;
        await using (var seed = new ApplicationDbContext(options))
        {
            var first = Website("race-first", now);
            var second = Website("race-second", now);
            var emergency = Account.CreateEmergency(Guid.NewGuid(), "reserved-emergency", "RESERVED-EMERGENCY", now);
            first.SetPassword(new PasswordHasher<Account>().HashPassword(first, "correct-current-password"), false, now, incrementVersion: false);
            second.SetPassword(new PasswordHasher<Account>().HashPassword(second, "correct-current-password"), false, now, incrementVersion: false);
            seed.AddRange(first, second, emergency);
            await seed.SaveChangesAsync();
            var identities = new AccountIdentityService(seed, new PasswordHasher<Account>(), TimeProvider.System);
            Assert.Equal(UsernameRenameResult.UsernameTaken, await identities.RenameUsernameAsync(first.Id, "reserved-emergency", "correct-current-password", CancellationToken.None));
            Assert.Equal(UsernameRenameResult.NotAvailable, await identities.RenameUsernameAsync(emergency.Id, "not-permitted", "correct-current-password", CancellationToken.None));
        }

        await using var firstContext = new ApplicationDbContext(options);
        await using var secondContext = new ApplicationDbContext(options);
        var firstAccount = await firstContext.Accounts.SingleAsync(x => x.LoginName == "race-first");
        var secondAccount = await secondContext.Accounts.SingleAsync(x => x.LoginName == "race-second");
        firstAccount.RenameWebsiteUsername("Contended", "CONTENDED");
        secondAccount.RenameWebsiteUsername("Contended", "CONTENDED");
        var saves = await Task.WhenAll(
            CaptureAsync(firstContext.SaveChangesAsync()),
            CaptureAsync(secondContext.SaveChangesAsync()));

        Assert.Single(saves, result => result is null);
        Assert.Single(saves, result => result is DbUpdateException);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(1, await verify.Accounts.CountAsync(x => x.NormalizedLoginName == "CONTENDED"));
    }

    [Fact]
    public async Task WebsiteUsernameRenameRefreshesTheNewPrincipalWithoutInvalidatingExistingSessions()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var account = Website("session-before", now);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "correct-current-password"), false, now, incrementVersion: false);
        db.Add(account);
        await db.SaveChangesAsync();
        var authentication = new AccountAuthenticationService(db, new PasswordHasher<Account>(), TimeProvider.System);
        var existingPasswordSession = authentication.CreatePrincipal(account, "password");
        var existingDiscordSession = authentication.CreatePrincipal(account, "discord");
        var authorizationVersion = account.AuthorizationVersion;
        var passwordVersion = account.PasswordVersion;

        Assert.Equal(UsernameRenameResult.Success, await new AccountIdentityService(db, new PasswordHasher<Account>(), TimeProvider.System)
            .RenameUsernameAsync(account.Id, "session after", "correct-current-password", CancellationToken.None));
        var refreshedPasswordSession = authentication.CreatePrincipal(account, "password");
        var refreshedDiscordSession = authentication.CreatePrincipal(account, "discord");

        Assert.Equal("session after", refreshedPasswordSession.Identity?.Name);
        Assert.Equal("password", refreshedPasswordSession.FindFirst(Bingo.Application.Access.AccountClaims.AuthenticationMethod)?.Value);
        Assert.Equal("session after", refreshedDiscordSession.Identity?.Name);
        Assert.Equal("discord", refreshedDiscordSession.FindFirst(Bingo.Application.Access.AccountClaims.AuthenticationMethod)?.Value);
        Assert.Equal(authorizationVersion.ToString(CultureInfo.InvariantCulture), refreshedPasswordSession.FindFirst(Bingo.Application.Access.AccountClaims.AuthorizationVersion)?.Value);
        Assert.Equal(passwordVersion.ToString(CultureInfo.InvariantCulture), refreshedPasswordSession.FindFirst(Bingo.Application.Access.AccountClaims.PasswordVersion)?.Value);
        Assert.NotNull((await ValidateSessionAsync(db, existingPasswordSession)).Principal);
        Assert.NotNull((await ValidateSessionAsync(db, existingDiscordSession)).Principal);
    }

    [Fact]
    public async Task BrowserLevelOnboardingAndMyAccountsJourneyUsesOrdinaryForms()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var onboardingState = factory.Services.GetRequiredService<DiscordOnboardingStateService>();
        var issue = new DefaultHttpContext();
        onboardingState.Issue(issue.Response, $"discord-my-accounts-{Guid.NewGuid():N}", "Browser journey");
        client.DefaultRequestHeaders.Add("Cookie", issue.Response.Headers.SetCookie.Single()!.Split(';')[0]);

        var onboarding = await client.GetStringAsync("/Account/Onboarding");
        Assert.Contains("Input.Username", onboarding, StringComparison.Ordinal);
        Assert.Contains("Input.OsrsCharacterName", onboarding, StringComparison.Ordinal);
        var onboardingToken = AntiforgeryToken(onboarding);
        using var completed = await client.PostAsync("/Account/Onboarding", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "browser-public-username",
            ["Input.OsrsCharacterName"] = "Browser exact character",
            ["Input.Password"] = "long-browser-password",
            ["Input.ConfirmPassword"] = "long-browser-password",
            ["__RequestVerificationToken"] = onboardingToken
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, completed.StatusCode);

        var accounts = await client.GetStringAsync("/Account/MyAccounts");
        Assert.Contains("Browser exact character", accounts, StringComparison.Ordinal);
        var addToken = AntiforgeryToken(accounts);
        using var added = await client.PostAsync("/Account/MyAccounts?handler=Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Add.CharacterName"] = "Browser second character",
            ["Add.PersonalLabel"] = "Alt",
            ["Add.SavedEhb"] = "44.5",
            ["__RequestVerificationToken"] = addToken
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, added.StatusCode);
        var updated = await client.GetStringAsync("/Account/MyAccounts");
        Assert.Contains("Browser second character", updated, StringComparison.Ordinal);
        Assert.Contains("Alt", updated, StringComparison.Ordinal);
        Assert.Contains("44.5", updated, StringComparison.Ordinal);

        var settings = await client.GetStringAsync("/Account/Settings");
        Assert.Contains("Change website username", settings, StringComparison.Ordinal);
        using var renamed = await client.PostAsync("/Account/Settings?handler=RenameUsername", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Rename.Username"] = "browser renamed username",
            ["Rename.CurrentPassword"] = "long-browser-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(settings)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, renamed.StatusCode);
        var renamedSettings = await client.GetStringAsync("/Account/Settings");
        Assert.Contains("Your website username was changed.", renamedSettings, StringComparison.Ordinal);
        using var passwordLogin = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await passwordLogin.GetStringAsync("/Account/Login");
        using var loggedIn = await passwordLogin.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "browser renamed username",
            ["Input.Password"] = "long-browser-password",
            ["Input.RememberMe"] = "true",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, loggedIn.StatusCode);
        var originalExpiry = CookieExpiry(loggedIn);
        var passwordSettings = await passwordLogin.GetStringAsync("/Account/Settings");
        using var renamedAgain = await passwordLogin.PostAsync("/Account/Settings?handler=RenameUsername", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Rename.Username"] = "browser expiry username",
            ["Rename.CurrentPassword"] = "long-browser-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(passwordSettings)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, renamedAgain.StatusCode);
        Assert.Equal(originalExpiry, CookieExpiry(renamedAgain));
    }

    [Fact]
    public async Task BrowserLevelEnhancedSamePageMutationsReplaceTheMyAccountsNavigation()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var onboardingState = factory.Services.GetRequiredService<DiscordOnboardingStateService>();
        var issue = new DefaultHttpContext();
        onboardingState.Issue(issue.Response, $"discord-enhanced-my-accounts-{Guid.NewGuid():N}", "Enhanced browser journey");
        client.DefaultRequestHeaders.Add("Cookie", issue.Response.Headers.SetCookie.Single()!.Split(';')[0]);

        var onboarding = await client.GetStringAsync("/Account/Onboarding");
        using var completed = await EnhancedPostAsync(client, "/Account/Onboarding", new Dictionary<string, string>
        {
            ["Input.Username"] = "enhanced-browser-public-username",
            ["Input.OsrsCharacterName"] = "Enhanced browser first character",
            ["Input.Password"] = "long-enhanced-browser-password",
            ["Input.ConfirmPassword"] = "long-enhanced-browser-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(onboarding)
        });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, completed.StatusCode);
        Assert.Equal("/", completed.Headers.GetValues("X-Bingo-Post-Navigation").Single());

        using var routeBeforeMutationPage = await client.GetAsync("/");
        routeBeforeMutationPage.EnsureSuccessStatusCode();

        var accounts = await client.GetStringAsync("/Account/MyAccounts");
        using var firstMutation = await EnhancedPostAsync(client, "/Account/MyAccounts?handler=Add", new Dictionary<string, string>
        {
            ["Add.CharacterName"] = "Enhanced browser second character",
            ["Add.PersonalLabel"] = "Alt",
            ["Add.SavedEhb"] = "44.5",
            ["__RequestVerificationToken"] = AntiforgeryToken(accounts)
        });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, firstMutation.StatusCode);
        Assert.Equal("/Account/MyAccounts", firstMutation.Headers.GetValues("X-Bingo-Post-Navigation").Single());

        var afterFirstMutation = await client.GetStringAsync("/Account/MyAccounts");
        Assert.Contains("Character added to My Accounts.", afterFirstMutation, StringComparison.Ordinal);
        Assert.Contains("Enhanced browser second character", afterFirstMutation, StringComparison.Ordinal);

        using var secondMutation = await EnhancedPostAsync(client, "/Account/MyAccounts?handler=Add", new Dictionary<string, string>
        {
            ["Add.CharacterName"] = "Enhanced browser third character",
            ["Add.PersonalLabel"] = "Third",
            ["Add.SavedEhb"] = "45.5",
            ["__RequestVerificationToken"] = AntiforgeryToken(afterFirstMutation)
        });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, secondMutation.StatusCode);
        Assert.Equal("/Account/MyAccounts", secondMutation.Headers.GetValues("X-Bingo-Post-Navigation").Single());

        var afterSecondMutation = await client.GetStringAsync("/Account/MyAccounts");
        Assert.Contains("Character added to My Accounts.", afterSecondMutation, StringComparison.Ordinal);
        Assert.Contains("Enhanced browser second character", afterSecondMutation, StringComparison.Ordinal);
        Assert.Contains("Enhanced browser third character", afterSecondMutation, StringComparison.Ordinal);

        var sharedNavigation = await client.GetStringAsync("/js/site.js");
        Assert.Contains("window.location.replace(destination);", sharedNavigation, StringComparison.Ordinal);
        Assert.Contains("window.location.assign(destination);", sharedNavigation, StringComparison.Ordinal);
        Assert.Contains("replaceSamePageHistory(window.location.href, state.destination || window.location.href);", sharedNavigation, StringComparison.Ordinal);
    }

    private async Task<(Guid EventId, DateTimeOffset Now)> SeedEventAsync(string slug)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var creator = Website($"creator-{slug}-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), slug, $"{slug}-{Guid.NewGuid():N}", "", "UTC",
            now, now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 20, creator.Id, now);
        db.AddRange(creator, bingoEvent);
        await db.SaveChangesAsync();
        return (bingoEvent.Id, now);
    }

    private static Account Website(string name, DateTimeOffset now)
        => Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);

    private static EventParticipant Participant(Guid eventId, string name, long sequence, DateTimeOffset now)
        => new(Guid.NewGuid(), eventId, SignupStatus.Confirmed, sequence, now, SignupSource.AdminCreated, null);

    private static EventParticipantCharacter Playing(Guid eventId, Guid participantId, Guid characterId, Guid actorId, DateTimeOffset now)
        => new(Guid.NewGuid(), eventId, participantId, characterId, 0, now, actorId, null, EventCharacterRole.Playing, 1m, EhbSource.Manual, null);

    private static async Task<Exception?> CaptureAsync(Task save)
    {
        try { await save; return null; }
        catch (Exception exception) { return exception; }
    }

    private static async Task<CookieValidatePrincipalContext> ValidateSessionAsync(ApplicationDbContext db, System.Security.Claims.ClaimsPrincipal principal)
    {
        var context = new DefaultHttpContext();
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), CookieAuthenticationDefaults.AuthenticationScheme);
        var validation = new CookieValidatePrincipalContext(
            context,
            new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            ticket);
        await new AccountCookieEvents(db, TimeProvider.System).ValidatePrincipal(validation);
        return validation;
    }

    private static BingoEvent Event(DateTimeOffset now, string name, bool signupOpen)
    {
        var bingoEvent = new BingoEvent(Guid.NewGuid(), name, $"{name}-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(4), 20, Guid.NewGuid(), now);
        if (signupOpen) bingoEvent.OpenSignups(); else bingoEvent.CloseSignups();
        return bingoEvent;
    }

    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private static string CookieExpiry(HttpResponseMessage response) => Regex.Match(
        response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("Bingo.Auth=", StringComparison.Ordinal)),
        "expires=([^;]+)",
        RegexOptions.IgnoreCase).Groups[1].Value;

    private static Task<HttpResponseMessage> EnhancedPostAsync(HttpClient client, string path, Dictionary<string, string> values)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(values)
        };
        request.Headers.Add("X-Bingo-Enhanced-Post", "true");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return client.SendAsync(request);
    }
}
