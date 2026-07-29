using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice4AuthenticatedSignupIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice4_authenticated_signup")
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
    public async Task AuthenticatedSignupEditsAtomicallyRetainsHistoricalAssignmentAndSnapshotsEhb()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website("signup-owner", now);
        var bingoEvent = Event(owner.Id, now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
        var character = new OsrsCharacter(Guid.NewGuid(), "Historical Main", "HISTORICAL MAIN", now);
        var link = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 12m, now);
        db.AddRange(owner, bingoEvent, form, regular, captain, character, link);
        await db.SaveChangesAsync();
        var service = new SignupService(db, new SecretHasher(), TimeProvider.System);

        var created = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(
            bingoEvent.Id, owner.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(character.Id, 22m) },
            new Dictionary<Guid, string> { [captain.Id] = "false" }, null));
        Assert.True(created.Succeeded);
        var original = await db.EventParticipants.SingleAsync(x => x.Id == created.ParticipantId);
        var originalSignedUpAt = original.SignedUpAt;
        var originalSequence = original.SignupSequence;
        Assert.Equal(22m, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == original.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
        Assert.Equal(22m, await db.AccountOsrsCharacters.Where(x => x.Id == link.Id).Select(x => x.SavedEhb).SingleAsync());

        link.Unlink(now.AddMinutes(1));
        await db.SaveChangesAsync();
        var edited = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(
            bingoEvent.Id, owner.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(character.Id, 31m) },
            new Dictionary<Guid, string> { [captain.Id] = "true" }, null, ExpectedResponseVersion: original.ResponseVersion));
        Assert.True(edited.Succeeded);
        db.ChangeTracker.Clear();
        var saved = await db.EventParticipants.SingleAsync(x => x.Id == original.Id);
        Assert.Equal(originalSignedUpAt, saved.SignedUpAt);
        Assert.Equal(originalSequence, saved.SignupSequence);
        Assert.True(saved.CaptainVolunteer);
        Assert.Equal(31m, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == saved.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
        Assert.Equal(22m, await db.AccountOsrsCharacters.Where(x => x.Id == link.Id).Select(x => x.SavedEhb).SingleAsync());

        var unavailable = Guid.NewGuid();
        var rejected = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(
            bingoEvent.Id, owner.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(unavailable, 40m) },
            new Dictionary<Guid, string> { [captain.Id] = "false" }, null));
        Assert.False(rejected.Succeeded);
        db.ChangeTracker.Clear();
        Assert.Equal(character.Id, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == saved.Id && x.ReleasedAt == null).Select(x => x.OsrsCharacterId).SingleAsync());
        Assert.Equal(31m, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == saved.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());

        var stale = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(
            bingoEvent.Id, owner.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(character.Id, 99m) },
            new Dictionary<Guid, string> { [captain.Id] = "false" }, null, ExpectedResponseVersion: 1));
        Assert.False(stale.Succeeded);
        Assert.Contains("reload", stale.Error!, StringComparison.OrdinalIgnoreCase);
        db.ChangeTracker.Clear();
        var unchanged = await db.EventParticipants.SingleAsync(x => x.Id == saved.Id);
        Assert.Equal(2, unchanged.ResponseVersion);
        Assert.True(unchanged.CaptainVolunteer);
        Assert.Equal(31m, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == saved.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
    }

    [Fact]
    public async Task ChangingRegularAccountUpdatesOnlyTheNewAccountDefaultAndEventSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"ehb-owner-{Guid.NewGuid():N}", now);
        var bingoEvent = Event(owner.Id, now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var firstCharacter = new OsrsCharacter(Guid.NewGuid(), "First EHB", $"FIRST EHB {Guid.NewGuid():N}", now);
        var secondCharacter = new OsrsCharacter(Guid.NewGuid(), "Second EHB", $"SECOND EHB {Guid.NewGuid():N}", now);
        var firstLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, firstCharacter.Id, owner.Id, true, 0, null, 12m, now);
        var secondLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, secondCharacter.Id, owner.Id, false, 1, null, 44m, now);
        db.AddRange(owner, bingoEvent, form, regular, firstCharacter, secondCharacter, firstLink, secondLink);
        await db.SaveChangesAsync();
        var service = new SignupService(db, new SecretHasher(), TimeProvider.System);

        var created = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(
            bingoEvent.Id, owner.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(firstCharacter.Id, 22m) }, new Dictionary<Guid, string>(), null));
        Assert.True(created.Succeeded);
        var edited = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(
            bingoEvent.Id, owner.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(secondCharacter.Id, 55m) }, new Dictionary<Guid, string>(), null, ExpectedResponseVersion: 1));
        Assert.True(edited.Succeeded);

        db.ChangeTracker.Clear();
        var assignment = await db.EventParticipantCharacters.SingleAsync(item => item.EventParticipantId == created.ParticipantId && item.ReleasedAt == null);
        Assert.Equal(secondCharacter.Id, assignment.OsrsCharacterId);
        Assert.Equal(55m, assignment.EhbSnapshot);
        Assert.Equal(22m, await db.AccountOsrsCharacters.Where(item => item.Id == firstLink.Id).Select(item => item.SavedEhb).SingleAsync());
        Assert.Equal(55m, await db.AccountOsrsCharacters.Where(item => item.Id == secondLink.Id).Select(item => item.SavedEhb).SingleAsync());
    }

    [Fact]
    public async Task SignupPageUsesPreferredSavedEhbKeepsSnapshotsAndPreservesInvalidNativePosts()
    {
        var now = DateTimeOffset.UtcNow;
        Guid eventId;
        Guid regularId;
        Guid preferredCharacterId;
        Guid alternateCharacterId;
        Guid preferredLinkId;
        string loginName;
        await using (var db = new ApplicationDbContext(options))
        {
            var owner = Website($"ehb-route-owner-{Guid.NewGuid():N}", now);
            owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "ehb-route-password"), false, now, incrementVersion: false);
            var bingoEvent = Event(owner.Id, now);
            var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
            var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var preferred = new OsrsCharacter(Guid.NewGuid(), "Preferred EHB", $"PREFERRED EHB {Guid.NewGuid():N}", now);
            var alternate = new OsrsCharacter(Guid.NewGuid(), "Alternate EHB", $"ALTERNATE EHB {Guid.NewGuid():N}", now);
            var preferredLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, preferred.Id, owner.Id, true, 0, null, 12m, now);
            var alternateLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, alternate.Id, owner.Id, false, 1, null, null, now);
            db.AddRange(owner, bingoEvent, form, regular, preferred, alternate, preferredLink, alternateLink);
            await db.SaveChangesAsync();
            eventId = bingoEvent.Id;
            regularId = regular.Id;
            preferredCharacterId = preferred.Id;
            alternateCharacterId = alternate.Id;
            preferredLinkId = preferredLink.Id;
            loginName = owner.LoginName;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var slug = await EventSlugAsync(eventId);
        var login = await client.GetStringAsync("/Account/Login");
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = loginName,
            ["Input.Password"] = "ehb-route-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var createPage = await client.GetStringAsync($"/Events/{slug}/Signup");
        Assert.Contains($"value=\"{preferredCharacterId}\"", createPage, StringComparison.Ordinal);
        Assert.Contains("data-saved-ehb", Option(createPage, preferredCharacterId), StringComparison.Ordinal);
        Assert.Contains("selected", Option(createPage, preferredCharacterId), StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{alternateCharacterId}\" data-saved-ehb=\"\"", createPage, StringComparison.Ordinal);
        Assert.Equal(12m, InputDecimal(createPage, $"ehb-{regularId}"));

        using var created = await client.PostAsync($"/Events/{slug}/Signup", SignupPost(createPage, regularId, preferredCharacterId, "27"));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        await using (var verification = new ApplicationDbContext(options))
        {
            Assert.Equal(27m, await verification.EventParticipantCharacters.Where(item => item.EventId == eventId && item.ReleasedAt == null).Select(item => item.EhbSnapshot).SingleAsync());
            Assert.Equal(27m, await verification.AccountOsrsCharacters.Where(item => item.Id == preferredLinkId).Select(item => item.SavedEhb).SingleAsync());
            var link = await verification.AccountOsrsCharacters.SingleAsync(item => item.Id == preferredLinkId);
            link.UpdatePreferences(link.PersonalLabel, link.Position, link.Preferred, 88m, now.AddMinutes(1));
            await verification.SaveChangesAsync();
        }

        var editPage = await client.GetStringAsync($"/Events/{slug}/Signup?edit=true");
        Assert.Equal(27m, InputDecimal(editPage, $"ehb-{regularId}"));
        using var invalid = await client.PostAsync($"/Events/{slug}/Signup", SignupPost(editPage, regularId, alternateCharacterId, "-1"));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidPage = await invalid.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{alternateCharacterId}\"", invalidPage, StringComparison.Ordinal);
        Assert.Contains("selected", Option(invalidPage, alternateCharacterId), StringComparison.Ordinal);
        Assert.Equal(-1m, InputDecimal(invalidPage, $"ehb-{regularId}"));

        ParticipantState signupBeforeStale;
        await using (var snapshot = new ApplicationDbContext(options)) signupBeforeStale = await ParticipantStateAsync(snapshot, await snapshot.EventParticipants.Where(item => item.EventId == eventId).Select(item => item.Id).SingleAsync());
        using var stale = await client.PostAsync($"/Events/{slug}/Signup", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [$"Input.AccountAnswers[{regularId}].OsrsCharacterId"] = alternateCharacterId.ToString(),
            [$"Input.AccountAnswers[{regularId}].Ehb"] = "99",
            ["__RequestVerificationToken"] = AntiforgeryToken(editPage)
        }));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Contains("reload", await stale.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await using (var verification = new ApplicationDbContext(options))
        {
            Assert.Equal(signupBeforeStale, await ParticipantStateAsync(verification, await verification.EventParticipants.Where(item => item.EventId == eventId).Select(item => item.Id).SingleAsync()));
            var participant = await verification.EventParticipants.SingleAsync(item => item.EventId == eventId);
            Assert.Equal(SignupStatus.Confirmed, participant.SignupStatus); Assert.Equal(SignupSource.Website, participant.Source); Assert.Equal(1, participant.SignupSequence); Assert.Equal(1, participant.ResponseVersion);
            Assert.Equal(27m, await verification.EventParticipantCharacters.Where(item => item.EventId == eventId && item.ReleasedAt == null).Select(item => item.EhbSnapshot).SingleAsync());
            Assert.Empty(await verification.AuditEntries.Where(item => item.TargetId == participant.Id.ToString()).ToListAsync());
        }

        async Task<string> EventSlugAsync(Guid id)
        {
            await using var lookup = new ApplicationDbContext(options);
            return await lookup.Events.Where(item => item.Id == id).Select(item => item.Slug).SingleAsync();
        }
    }

    [Fact]
    public async Task SignupRouteUsesOneGetHandlerAndPreservesCreateConfirmationEditAndLoginJourneys()
    {
        var now = DateTimeOffset.UtcNow;
        Guid eventId;
        Guid ownerId;
        Guid characterId;
        Guid regularId;
        await using (var db = new ApplicationDbContext(options))
        {
            var owner = Website($"route-owner-{Guid.NewGuid():N}", now);
            owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "route-password"), false, now, incrementVersion: false);
            var bingoEvent = Event(owner.Id, now);
            var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
            var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var captain = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
            var character = new OsrsCharacter(Guid.NewGuid(), "Route Main", $"ROUTE MAIN {Guid.NewGuid():N}", now);
            var link = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 12m, now);
            db.AddRange(owner, bingoEvent, form, regular, captain, character, link);
            await db.SaveChangesAsync();
            eventId = bingoEvent.Id;
            ownerId = owner.Id;
            characterId = character.Id;
            regularId = regular.Id;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var slug = await EventSlugAsync(eventId);
        using var anonymous = await client.GetAsync($"/Events/{slug}/Signup");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal($"/Account/Login?ReturnUrl=%2FEvents%2F{slug}%2FSignup", anonymous.Headers.Location?.OriginalString);

        var login = await client.GetStringAsync("/Account/Login");
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = await AccountLoginAsync(ownerId),
            ["Input.Password"] = "route-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        using var create = await client.GetAsync($"/Events/{slug}/Signup");
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.Contains("Sign up", await create.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using (var db = new ApplicationDbContext(options))
        {
            var service = new SignupService(db, new SecretHasher(), TimeProvider.System);
            var result = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(eventId, ownerId, new Dictionary<Guid, AuthenticatedAccountAnswer> { [regularId] = new(characterId, 15m) }, new Dictionary<Guid, string>(), null));
            Assert.True(result.Succeeded);
        }
        using var confirmation = await client.GetAsync($"/Events/{slug}/Signup");
        Assert.Equal(HttpStatusCode.Redirect, confirmation.StatusCode);
        Assert.Contains($"/Events/{slug}/Signup/Confirmation", confirmation.Headers.Location?.OriginalString, StringComparison.Ordinal);
        using var edit = await client.GetAsync($"/Events/{slug}/Signup?edit=true");
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Contains("Edit signup", await edit.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        async Task<string> EventSlugAsync(Guid id)
        {
            await using var db = new ApplicationDbContext(options);
            return await db.Events.Where(item => item.Id == id).Select(item => item.Slug).SingleAsync();
        }
        async Task<string> AccountLoginAsync(Guid id)
        {
            await using var db = new ApplicationDbContext(options);
            return await db.Accounts.Where(account => account.Id == id).Select(account => account.LoginName).SingleAsync();
        }
    }

    [Fact]
    public async Task RenderedSignupAllowsEmptyOptionalAccountsAndEnforcesCodeBeforePersisting()
    {
        var now = DateTimeOffset.UtcNow;
        const string signupCode = "slice4-rendered-code";
        Guid eventId;
        Guid formId;
        Guid primaryQuestionId;
        Guid optionalRegularQuestionId;
        Guid optionalAltQuestionId;
        Guid textQuestionId;
        Guid firstCharacterId;
        Guid secondCharacterId;
        string adminLogin;
        string firstLogin;
        string secondLogin;
        await using (var db = new ApplicationDbContext(options))
        {
            var admin = Website($"signup-code-admin-{Guid.NewGuid():N}", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "admin-password"), false, now, incrementVersion: false);
            var first = Website($"signup-code-first-{Guid.NewGuid():N}", now);
            first.SetPassword(new PasswordHasher<Account>().HashPassword(first, "first-password"), false, now, incrementVersion: false);
            var second = Website($"signup-code-second-{Guid.NewGuid():N}", now);
            second.SetPassword(new PasswordHasher<Account>().HashPassword(second, "second-password"), false, now, incrementVersion: false);
            var bingoEvent = Event(admin.Id, now);
            var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
            var primary = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Main account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var optionalRegular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "optional_regular", "Optional regular account", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Playing);
            var optionalAlt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "optional_alt", "Optional alt account", SignupQuestionType.Account, false, 2, null, SignupSystemField.None, EventCharacterRole.Informational);
            var text = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "comment", "Comment", SignupQuestionType.Text, false, 3, null, SignupSystemField.None);
            var firstCharacter = new OsrsCharacter(Guid.NewGuid(), "First rendered main", $"FIRST RENDERED MAIN {Guid.NewGuid():N}", now);
            var secondCharacter = new OsrsCharacter(Guid.NewGuid(), "Second rendered main", $"SECOND RENDERED MAIN {Guid.NewGuid():N}", now);
            var firstLink = new AccountOsrsCharacter(Guid.NewGuid(), first.Id, firstCharacter.Id, first.Id, true, 0, null, 18m, now);
            var secondLink = new AccountOsrsCharacter(Guid.NewGuid(), second.Id, secondCharacter.Id, second.Id, true, 0, null, 21m, now);
            db.AddRange(admin, first, second, bingoEvent, form, primary, optionalRegular, optionalAlt, text, firstCharacter, secondCharacter, firstLink, secondLink);
            await db.SaveChangesAsync();
            eventId = bingoEvent.Id;
            formId = form.Id;
            primaryQuestionId = primary.Id;
            optionalRegularQuestionId = optionalRegular.Id;
            optionalAltQuestionId = optionalAlt.Id;
            textQuestionId = text.Id;
            firstCharacterId = firstCharacter.Id;
            secondCharacterId = secondCharacter.Id;
            adminLogin = admin.LoginName;
            firstLogin = first.LoginName;
            secondLogin = second.LoginName;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(adminClient, adminLogin, "admin-password");
        var questionsUrl = $"/Admin/Events/Questions/{eventId}";
        var adminPage = await adminClient.GetStringAsync(questionsUrl);
        using (var enable = await adminClient.PostAsync($"{questionsUrl}?handler=SignupCode", SignupCodePost(adminPage, true, signupCode)))
            Assert.Equal(HttpStatusCode.Redirect, enable.StatusCode);
        string codeHash;
        await using (var verify = new ApplicationDbContext(options))
        {
            var configuredEvent = await verify.Events.SingleAsync(item => item.Id == eventId);
            var configuredForm = await verify.SignupForms.SingleAsync(item => item.Id == formId);
            Assert.True(configuredEvent.RequireSignupCode);
            Assert.True(configuredForm.RequireSignupCode);
            codeHash = configuredForm.SignupCodeHash!;
            Assert.False(string.IsNullOrWhiteSpace(codeHash));
        }
        adminPage = await adminClient.GetStringAsync(questionsUrl);
        Assert.DoesNotContain(signupCode, adminPage, StringComparison.Ordinal);
        Assert.DoesNotContain(codeHash, adminPage, StringComparison.Ordinal);

        using var firstClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(firstClient, firstLogin, "first-password");
        var slug = await EventSlugAsync(eventId);
        var signupPage = await firstClient.GetStringAsync($"/Events/{slug}/Signup");
        Assert.Contains($"Input.AccountAnswers[{optionalRegularQuestionId}].OsrsCharacterId", signupPage, StringComparison.Ordinal);
        Assert.Contains($"Input.AccountAnswers[{optionalAltQuestionId}].OsrsCharacterId", signupPage, StringComparison.Ordinal);

        using (var missingCode = await firstClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, firstCharacterId, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, null)))
        {
            Assert.Equal(HttpStatusCode.OK, missingCode.StatusCode);
            var html = await missingCode.Content.ReadAsStringAsync();
            Assert.Contains("The event code is incorrect.", html, StringComparison.Ordinal);
            Assert.Contains($"value=\"{firstCharacterId}\"", html, StringComparison.Ordinal);
            Assert.Contains("A retained answer", html, StringComparison.Ordinal);
        }
        await AssertNoFirstResponseAsync();

        signupPage = await firstClient.GetStringAsync($"/Events/{slug}/Signup");
        using (var wrongCode = await firstClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, firstCharacterId, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, "not-the-code")))
        {
            Assert.Equal(HttpStatusCode.OK, wrongCode.StatusCode);
            Assert.Contains("The event code is incorrect.", await wrongCode.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        await AssertNoFirstResponseAsync();

        signupPage = await firstClient.GetStringAsync($"/Events/{slug}/Signup");
        using (var missingRequired = await firstClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, null, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, signupCode)))
        {
            Assert.Equal(HttpStatusCode.OK, missingRequired.StatusCode);
            var html = await missingRequired.Content.ReadAsStringAsync();
            Assert.Contains("Main account", html, StringComparison.Ordinal);
            Assert.Contains("is required.", html, StringComparison.Ordinal);
        }
        await AssertNoFirstResponseAsync();

        signupPage = await firstClient.GetStringAsync($"/Events/{slug}/Signup");
        using (var duplicate = await firstClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, firstCharacterId, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, signupCode, firstCharacterId)))
        {
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            Assert.Contains("Choose each account only once.", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        await AssertNoFirstResponseAsync();

        signupPage = await firstClient.GetStringAsync($"/Events/{slug}/Signup");
        using (var accepted = await firstClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, firstCharacterId, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, signupCode)))
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        await using (var verify = new ApplicationDbContext(options))
        {
            var participant = await verify.EventParticipants.SingleAsync(item => item.EventId == eventId && item.AccountId != null);
            Assert.Equal(firstCharacterId, await verify.EventParticipantCharacters.Where(item => item.EventParticipantId == participant.Id && item.ReleasedAt == null).Select(item => item.OsrsCharacterId).SingleAsync());
            Assert.Empty(await verify.SignupAnswers.Where(item => item.EventParticipantId == participant.Id && item.SignupQuestionId != primaryQuestionId && item.OsrsCharacterId != null).ToListAsync());
            Assert.NotNull(await verify.SignupForms.Where(item => item.Id == formId).Select(item => item.FirstResponseAt).SingleAsync());
        }

        adminPage = await adminClient.GetStringAsync(questionsUrl);
        using (var disable = await adminClient.PostAsync($"{questionsUrl}?handler=SignupCode", SignupCodePost(adminPage, false, null)))
            Assert.Equal(HttpStatusCode.Redirect, disable.StatusCode);
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.False(await verify.Events.Where(item => item.Id == eventId).Select(item => item.RequireSignupCode).SingleAsync());
            Assert.False(await verify.SignupForms.Where(item => item.Id == formId).Select(item => item.RequireSignupCode).SingleAsync());
            Assert.Null(await verify.SignupForms.Where(item => item.Id == formId).Select(item => item.SignupCodeHash).SingleAsync());
        }

        using var secondClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(secondClient, secondLogin, "second-password");
        signupPage = await secondClient.GetStringAsync($"/Events/{slug}/Signup");
        using (var acceptedWithoutCode = await secondClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, secondCharacterId, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, null)))
            Assert.Equal(HttpStatusCode.Redirect, acceptedWithoutCode.StatusCode);

        async Task LoginAsync(HttpClient client, string loginName, string password)
        {
            var login = await client.GetStringAsync("/Account/Login");
            using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = loginName,
                ["Input.Password"] = password,
                ["__RequestVerificationToken"] = AntiforgeryToken(login)
            }));
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        }

        async Task<string> EventSlugAsync(Guid id)
        {
            await using var lookup = new ApplicationDbContext(options);
            return await lookup.Events.Where(item => item.Id == id).Select(item => item.Slug).SingleAsync();
        }

        async Task AssertNoFirstResponseAsync()
        {
            await using var verify = new ApplicationDbContext(options);
            Assert.Empty(await verify.EventParticipants.Where(item => item.EventId == eventId).ToListAsync());
            Assert.Null(await verify.SignupForms.Where(item => item.Id == formId).Select(item => item.FirstResponseAt).SingleAsync());
        }
    }

    [Fact]
    public async Task QuestionsEditAfterFirstResponseChangesOnlyPresentationAndRejectsCraftedStructure()
    {
        var now = DateTimeOffset.UtcNow;
        Guid eventId;
        Guid formId;
        Guid customQuestionId;
        Guid participantId;
        string adminLogin;
        await using (var db = new ApplicationDbContext(options))
        {
            var admin = Website($"edit-admin-{Guid.NewGuid():N}", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "edit-password"), false, now, incrementVersion: false);
            var player = Website($"edit-player-{Guid.NewGuid():N}", now);
            var bingoEvent = Event(admin.Id, now);
            var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
            var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var captain = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
            var custom = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "experience", "Experience", SignupQuestionType.SingleChoice, true, 2, "Low\nHigh", SignupSystemField.None, null, "Choose one");
            var character = new OsrsCharacter(Guid.NewGuid(), "Edit participant", $"EDIT PARTICIPANT {Guid.NewGuid():N}", now);
            var link = new AccountOsrsCharacter(Guid.NewGuid(), player.Id, character.Id, player.Id, true, 0, null, 10m, now);
            db.AddRange(admin, player, bingoEvent, form, regular, captain, custom, character, link);
            await db.SaveChangesAsync();
            var service = new SignupService(db, new SecretHasher(), TimeProvider.System);
            var signedUp = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(bingoEvent.Id, player.Id, new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(character.Id, 10m) }, new Dictionary<Guid, string> { [custom.Id] = "High" }, null));
            Assert.True(signedUp.Succeeded);
            db.ChangeTracker.Clear();
            var closed = await db.Events.SingleAsync(item => item.Id == bingoEvent.Id);
            closed.CloseSignups(now.AddMinutes(1));
            await db.SaveChangesAsync();
            eventId = bingoEvent.Id;
            formId = form.Id;
            customQuestionId = custom.Id;
            participantId = signedUp.ParticipantId!.Value;
            adminLogin = admin.LoginName;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using (var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = adminLogin,
            ["Input.Password"] = "edit-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        })))
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var questionsUrl = $"/Admin/Events/Questions/{eventId}";
        var page = await client.GetStringAsync(questionsUrl);
        Assert.DoesNotContain("name=\"Edit.Type\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Edit.Options\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Edit.AccountRole\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Edit.Required\"", page, StringComparison.Ordinal);
        var before = await QuestionStateAsync();
        using (var edited = await client.PostAsync($"{questionsUrl}?handler=Edit", EditPost(page, customQuestionId, "Experience level", "Shown after signup")))
            Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        var refreshed = await client.GetStringAsync(questionsUrl);
        Assert.Contains("Question saved.", refreshed, StringComparison.Ordinal);
        var afterPresentation = await QuestionStateAsync();
        Assert.Equal("Experience level", afterPresentation.Label);
        Assert.Equal("Shown after signup", afterPresentation.HelpText);
        Assert.Equal(before.Type, afterPresentation.Type);
        Assert.Equal(before.Required, afterPresentation.Required);
        Assert.Equal(before.Options, afterPresentation.Options);
        Assert.Equal(before.AccountRole, afterPresentation.AccountRole);
        Assert.Equal(before.AnswerCount, afterPresentation.AnswerCount);
        Assert.Equal(before.AssignmentCount, afterPresentation.AssignmentCount);
        Assert.Equal(before.FormVersion + 1, afterPresentation.FormVersion);
        Assert.Equal(before.AuditCount + 1, afterPresentation.AuditCount);

        page = await client.GetStringAsync(questionsUrl);
        using (var crafted = await client.PostAsync($"{questionsUrl}?handler=Edit", EditPost(page, customQuestionId, "Experience level", "Shown after signup", new Dictionary<string, string>
        {
            ["Edit.Type"] = "Text",
            ["Edit.Required"] = "false",
            ["Edit.Options"] = string.Empty,
            ["Edit.AccountRole"] = string.Empty
        })))
            Assert.Equal(HttpStatusCode.Redirect, crafted.StatusCode);
        refreshed = await client.GetStringAsync(questionsUrl);
        Assert.Contains("Answer format is locked after the first response. Use replacement for a new optional question.", refreshed, StringComparison.Ordinal);
        var afterCrafted = await QuestionStateAsync();
        Assert.Equal(afterPresentation, afterCrafted);

        page = await client.GetStringAsync(questionsUrl);
        using (var blank = await client.PostAsync($"{questionsUrl}?handler=Edit", EditPost(page, customQuestionId, string.Empty, "Shown after signup")))
            Assert.Equal(HttpStatusCode.Redirect, blank.StatusCode);
        refreshed = await client.GetStringAsync(questionsUrl);
        Assert.Contains("Enter a question label.", refreshed, StringComparison.Ordinal);
        Assert.Equal(afterPresentation, await QuestionStateAsync());

        Guid preResponseEventId;
        await using (var db = new ApplicationDbContext(options))
        {
            var eventForMarkup = Event(await db.Accounts.Select(item => item.Id).FirstAsync(), now);
            var formForMarkup = new SignupForm(Guid.NewGuid(), eventForMarkup.Id, now);
            var regular = new SignupQuestion(Guid.NewGuid(), formForMarkup.Id, eventForMarkup.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var captain = new SignupQuestion(Guid.NewGuid(), formForMarkup.Id, eventForMarkup.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
            var custom = new SignupQuestion(Guid.NewGuid(), formForMarkup.Id, eventForMarkup.Id, "optional_number", "Optional number", SignupQuestionType.Number, false, 2, null);
            eventForMarkup.CloseSignups(now);
            db.AddRange(eventForMarkup, formForMarkup, regular, captain, custom);
            await db.SaveChangesAsync();
            preResponseEventId = eventForMarkup.Id;
        }
        var preResponsePage = await client.GetStringAsync($"/Admin/Events/Questions/{preResponseEventId}");
        Assert.DoesNotContain("selected=\"False\"", preResponsePage, StringComparison.Ordinal);
        Assert.DoesNotContain("checked=\"False\"", preResponsePage, StringComparison.Ordinal);

        async Task<QuestionState> QuestionStateAsync()
        {
            await using var verify = new ApplicationDbContext(options);
            var question = await verify.SignupQuestions.SingleAsync(item => item.Id == customQuestionId);
            return new QuestionState(question.Label, question.HelpText, question.Type, question.Required, question.Options, question.AccountAnswerRole,
                await verify.SignupAnswers.CountAsync(item => item.EventParticipantId == participantId && item.SignupQuestionId == customQuestionId),
                await verify.EventParticipantCharacters.CountAsync(item => item.EventParticipantId == participantId && item.ReleasedAt == null),
                await verify.SignupForms.Where(item => item.Id == formId).Select(item => item.Version).SingleAsync(),
                await verify.AuditEntries.CountAsync(item => item.EventId == eventId && item.Action == "signup_question.edited"));
        }
    }

    [Fact]
    public async Task AdminQuestionsRouteAddsOneCustomQuestionAndPreservesInvalidInput()
    {
        var now = DateTimeOffset.UtcNow;
        Guid eventId;
        Guid formId;
        string loginName;
        await using (var db = new ApplicationDbContext(options))
        {
            var admin = Website($"questions-admin-{Guid.NewGuid():N}", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "questions-password"), false, now, incrementVersion: false);
            var bingoEvent = new BingoEvent(Guid.NewGuid(), "Question builder", $"question-builder-{Guid.NewGuid():N}", "Europe/Copenhagen", admin.Id, now);
            var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
            var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var captain = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
            db.AddRange(admin, bingoEvent, form, regular, captain);
            await db.SaveChangesAsync();
            eventId = bingoEvent.Id;
            formId = form.Id;
            loginName = admin.LoginName;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = loginName,
            ["Input.Password"] = "questions-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var questionsUrl = $"/Admin/Events/Questions/{eventId}";
        var page = await client.GetStringAsync(questionsUrl);
        var versionBefore = await FormVersionAsync();
        using var invalid = await client.PostAsync(questionsUrl, QuestionPost(page, "Invalid choices", "SingleChoice"));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidHtml = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Add at least one choice.", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"Invalid choices\"", invalidHtml, StringComparison.Ordinal);
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(2, await verify.SignupQuestions.CountAsync(item => item.EventId == eventId));
            Assert.Equal(versionBefore, await verify.SignupForms.Where(item => item.Id == formId).Select(item => item.Version).SingleAsync());
            Assert.Empty(await verify.AuditEntries.Where(item => item.EventId == eventId && item.Action == "signup_question.created").ToListAsync());
        }

        page = await client.GetStringAsync(questionsUrl);
        using var created = await client.PostAsync(questionsUrl, QuestionPost(page, "Favourite boss"));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var refreshed = await client.GetStringAsync(questionsUrl);
        Assert.Contains("Question added.", refreshed, StringComparison.Ordinal);
        Assert.Contains("Favourite boss", refreshed, StringComparison.Ordinal);
        await using (var verify = new ApplicationDbContext(options))
        {
            var question = await verify.SignupQuestions.SingleAsync(item => item.EventId == eventId && item.Key == "favourite_boss");
            Assert.True(question.Active);
            Assert.Equal(SignupQuestionType.Text, question.Type);
            Assert.Equal(versionBefore + 1, await verify.SignupForms.Where(item => item.Id == formId).Select(item => item.Version).SingleAsync());
            var audit = await verify.AuditEntries.Where(item => item.EventId == eventId && item.Action == "signup_question.created").ToListAsync();
            Assert.Single(audit);
            Assert.Contains("Favourite boss", audit[0].AfterState, StringComparison.Ordinal);
        }

        async Task<int> FormVersionAsync()
        {
            await using var verify = new ApplicationDbContext(options);
            return await verify.SignupForms.Where(item => item.Id == formId).Select(item => item.Version).SingleAsync();
        }
    }

    [Fact]
    public async Task RenderedConfirmationWithdrawalUsesAuthenticatedOwnershipNotClientParticipantId()
    {
        var now = DateTimeOffset.UtcNow;
        Guid eventId;
        Guid participantId;
        Guid waitingId;
        string slug;
        string ownerLogin;
        string otherLogin;
        await using (var db = new ApplicationDbContext(options))
        {
            var owner = Website($"withdraw-owner-{Guid.NewGuid():N}", now);
            owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "owner-password"), false, now, incrementVersion: false);
            var other = Website($"withdraw-other-{Guid.NewGuid():N}", now);
            other.SetPassword(new PasswordHasher<Account>().HashPassword(other, "other-password"), false, now, incrementVersion: false);
            var bingoEvent = new BingoEvent(Guid.NewGuid(), "Withdrawal", $"withdrawal-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), 1, owner.Id, now);
            bingoEvent.OpenSignups(now);
            var ownerParticipant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website, null); ownerParticipant.AssignOwner(owner);
            var waitingOwner = Website($"withdraw-waiting-{Guid.NewGuid():N}", now);
            var waitingParticipant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.WaitingList, 2, now.AddMinutes(1), SignupSource.Website, null); waitingParticipant.AssignOwner(waitingOwner);
            var ownerCharacter = new OsrsCharacter(Guid.NewGuid(), "Withdraw owner", $"WITHDRAW OWNER {Guid.NewGuid():N}", now);
            var waitingCharacter = new OsrsCharacter(Guid.NewGuid(), "Withdraw waiting", $"WITHDRAW WAITING {Guid.NewGuid():N}", now);
            db.AddRange(owner, other, bingoEvent, ownerParticipant, waitingParticipant, ownerCharacter, waitingCharacter,
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, ownerParticipant.Id, ownerCharacter.Id, 0, now, owner.Id, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null),
                waitingOwner, new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, waitingParticipant.Id, waitingCharacter.Id, 0, now, waitingOwner.Id, null, EventCharacterRole.Playing, 20m, EhbSource.Manual, null));
            await db.SaveChangesAsync();
            eventId = bingoEvent.Id; participantId = ownerParticipant.Id; waitingId = waitingParticipant.Id; slug = bingoEvent.Slug; ownerLogin = owner.LoginName; otherLogin = other.LoginName;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await ownerClient.GetStringAsync("/Account/Login");
        using (var signedIn = await ownerClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = ownerLogin, ["Input.Password"] = "owner-password", ["__RequestVerificationToken"] = AntiforgeryToken(login) }))) Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        var confirmationUrl = $"/Events/{slug}/Signup/Confirmation?participantId={participantId}";
        var confirmation = await ownerClient.GetStringAsync(confirmationUrl);
        Assert.Contains("Withdraw from event", confirmation, StringComparison.Ordinal);
        using var withdrawn = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ConfirmLifecycleAction"] = "true",
            ["participantId"] = waitingId.ToString(),
            ["__RequestVerificationToken"] = AntiforgeryToken(confirmation)
        }));
        Assert.Equal(HttpStatusCode.Redirect, withdrawn.StatusCode);
        var resultPage = await ownerClient.GetStringAsync(withdrawn.Headers.Location!);
        Assert.Contains("Your signup has been withdrawn.", resultPage, StringComparison.Ordinal);
        Assert.Contains("Withdrawn", resultPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Your place is confirmed.", resultPage, StringComparison.Ordinal);
        Assert.Contains("Rejoin signup", resultPage, StringComparison.Ordinal);
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.Id == participantId).Select(x => x.SignupStatus).SingleAsync());
            Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == waitingId).Select(x => x.SignupStatus).SingleAsync());
            Assert.All(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId).ToListAsync(), x => Assert.NotNull(x.ReleasedAt));
        }
        using (var rejoined = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Rejoin", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(resultPage) })))
        {
            Assert.Equal(HttpStatusCode.Redirect, rejoined.StatusCode);
            resultPage = await ownerClient.GetStringAsync(rejoined.Headers.Location!);
            Assert.Contains("You rejoined at waiting-list position 1.", resultPage, StringComparison.Ordinal);
            Assert.Contains("WaitingList", resultPage, StringComparison.Ordinal);
        }
        using (var withdrawnAgain = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(resultPage) })))
        {
            Assert.Equal(HttpStatusCode.Redirect, withdrawnAgain.StatusCode);
            resultPage = await ownerClient.GetStringAsync(withdrawnAgain.Headers.Location!);
        }
        await using (var close = new ApplicationDbContext(options))
        {
            var bingoEvent = await close.Events.SingleAsync(x => x.Id == eventId);
            bingoEvent.CloseSignups(DateTimeOffset.UtcNow);
            await close.SaveChangesAsync();
        }
        var closedPage = await ownerClient.GetStringAsync(confirmationUrl);
        Assert.Contains("Signup is closed. Contact an Admin if you need to be restored.", closedPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Rejoin signup", closedPage, StringComparison.Ordinal);
        using (var stale = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Rejoin", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(closedPage) })))
        {
            Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);
            var stalePage = await ownerClient.GetStringAsync(stale.Headers.Location!);
            Assert.Contains("Signup is closed. Contact an Admin if you need to be restored.", stalePage, StringComparison.Ordinal);
        }
        await using (var withdrawnVerify = new ApplicationDbContext(options))
            Assert.Equal(SignupStatus.Withdrawn, await withdrawnVerify.EventParticipants.Where(x => x.Id == participantId).Select(x => x.SignupStatus).SingleAsync());

        using var otherClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        login = await otherClient.GetStringAsync("/Account/Login");
        using (var signedIn = await otherClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = otherLogin, ["Input.Password"] = "other-password", ["__RequestVerificationToken"] = AntiforgeryToken(login) }))) Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        var otherPage = await otherClient.GetStringAsync($"/Events/{slug}/Signup");
        using var denied = await otherClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true", ["participantId"] = participantId.ToString(), ["__RequestVerificationToken"] = AntiforgeryToken(otherPage) }));
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location?.OriginalString, StringComparison.Ordinal);
        await using var final = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Confirmed, await final.EventParticipants.Where(x => x.Id == waitingId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Equal(eventId, await final.EventParticipants.Where(x => x.Id == participantId).Select(x => x.EventId).SingleAsync());
    }

    [Fact]
    public async Task PublicSignupTableAndMyEventsUseOnlyPublicProjectionAndStateAwareRoutes()
    {
        var now = DateTimeOffset.UtcNow;
        string slug;
        string privateSlug;
        string ownerLogin;
        string historySlug;
        string adminLogin;
        string superAdminLogin;
        string formerAdminLogin;
        Guid formerAdminId;
        await using (var db = new ApplicationDbContext(options))
        {
            var owner = Website($"PUBLIC-USERNAME-{Guid.NewGuid():N}", now);
            owner.SetDiscordIdentity("DISCORD-ID-SENTINEL", "DISCORD-NAME-SENTINEL");
            owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "owner-password"), false, now, incrementVersion: false);
            var waitingOwner = Website($"waiting-{Guid.NewGuid():N}", now);
            var withdrawnOwner = Website($"withdrawn-{Guid.NewGuid():N}", now);
            var admin = Website($"admin-{Guid.NewGuid():N}", now);
            admin.SetGlobalRole(GlobalRole.Admin);
            admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "admin-password"), false, now, incrementVersion: false);
            var superAdmin = Website($"super-admin-{Guid.NewGuid():N}", now);
            superAdmin.SetGlobalRole(GlobalRole.SuperAdmin);
            superAdmin.SetPassword(new PasswordHasher<Account>().HashPassword(superAdmin, "super-admin-password"), false, now, incrementVersion: false);
            var formerAdmin = Website($"former-admin-{Guid.NewGuid():N}", now);
            formerAdmin.SetGlobalRole(GlobalRole.Admin);
            formerAdmin.SetPassword(new PasswordHasher<Account>().HashPassword(formerAdmin, "former-admin-password"), false, now, incrementVersion: false);
            var emergency = Account.CreateEmergency(Guid.NewGuid(), "emergency-sentinel", "EMERGENCY-SENTINEL", now);

            var privateEvent = new BingoEvent(Guid.NewGuid(), "Private sentinel", $"private-{Guid.NewGuid():N}", "UTC", owner.Id, now);
            privateSlug = privateEvent.Slug;
            var bingoEvent = Event(owner.Id, now);
            bingoEvent.MarkFirstPublic(now);
            slug = bingoEvent.Slug;
            var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
            var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var secondRegular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "second_regular", "Anything", SignupQuestionType.Account, false, 2, null, SignupSystemField.None, EventCharacterRole.Playing);
            var alt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "alt_account", "Anything else", SignupQuestionType.Account, false, 3, null, SignupSystemField.None, EventCharacterRole.Informational);
            var captain = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
            var answer = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "favourite_boss", "Favourite boss", SignupQuestionType.Text, false, 4, null);
            var optional = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "later_optional", "Later optional", SignupQuestionType.Text, false, 5, null);
            var historical = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "retired_question", "Retired question", SignupQuestionType.Text, false, 6, null); historical.Deactivate(owner.Id, now, "replaced");

            var confirmed = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website); confirmed.AssignOwner(owner); confirmed.SetCaptainVolunteer(true); confirmed.SetAdminNotes("ADMIN-NOTES-SENTINEL"); confirmed.SetPaymentReceived(true);
            var waiting = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.WaitingList, 2, now.AddMinutes(1), SignupSource.Website, null); waiting.AssignOwner(waitingOwner);
            var withdrawn = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Withdrawn, 3, now.AddMinutes(2), SignupSource.Website, null); withdrawn.AssignOwner(withdrawnOwner);
            var main = new OsrsCharacter(Guid.NewGuid(), "Allowed Main", $"ALLOWED MAIN {Guid.NewGuid():N}", now);
            var second = new OsrsCharacter(Guid.NewGuid(), "Allowed Second", $"ALLOWED SECOND {Guid.NewGuid():N}", now);
            var altCharacter = new OsrsCharacter(Guid.NewGuid(), "Allowed Alt", $"ALLOWED ALT {Guid.NewGuid():N}", now);
            var waitingCharacter = new OsrsCharacter(Guid.NewGuid(), "Waiting Main", $"WAITING MAIN {Guid.NewGuid():N}", now);
            var withdrawnCharacter = new OsrsCharacter(Guid.NewGuid(), "Withdrawn Main", $"WITHDRAWN MAIN {Guid.NewGuid():N}", now);
            var history = Event(owner.Id, now.AddDays(-10)); history.MarkFirstPublic(now.AddDays(-10)); history.CloseSignups(now); history.StartEvent(now); history.EndEvent(now); history.FinalizeResults(now); history.Archive(now);
            historySlug = history.Slug;
            var historyBoard = new Bingo.Domain.Boards.Board(Guid.NewGuid(), history.Id, "Archived history board", 1, 1); historyBoard.Publish(now);
            var historyParticipant = new EventParticipant(Guid.NewGuid(), history.Id, SignupStatus.Confirmed, 1, now.AddDays(-10), SignupSource.Website, null); historyParticipant.AssignOwner(owner);
            var historyCharacter = new OsrsCharacter(Guid.NewGuid(), "History Main", $"HISTORY MAIN {Guid.NewGuid():N}", now);

            db.AddRange(owner, waitingOwner, withdrawnOwner, admin, superAdmin, formerAdmin, emergency, privateEvent, bingoEvent, form, regular, secondRegular, alt, captain, answer, optional, historical, confirmed, waiting, withdrawn, main, second, altCharacter, waitingCharacter, withdrawnCharacter, history, historyBoard, historyParticipant, historyCharacter,
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, confirmed.Id, main.Id, 0, now, owner.Id, regular.Id, EventCharacterRole.Playing, 123.45m, EhbSource.Manual, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, confirmed.Id, second.Id, 1, now, owner.Id, secondRegular.Id, EventCharacterRole.Playing, 67m, EhbSource.Manual, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, confirmed.Id, altCharacter.Id, 2, now, owner.Id, alt.Id, EventCharacterRole.Informational, null, null, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, waiting.Id, waitingCharacter.Id, 0, now, waitingOwner.Id, regular.Id, EventCharacterRole.Playing, 10m, EhbSource.Manual, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, withdrawn.Id, withdrawnCharacter.Id, 0, now, withdrawnOwner.Id, regular.Id, EventCharacterRole.Playing, 99m, EhbSource.Manual, null),
                new EventParticipantCharacter(Guid.NewGuid(), history.Id, historyParticipant.Id, historyCharacter.Id, 0, now, owner.Id, null, EventCharacterRole.Playing, 1m, EhbSource.Manual, null));
            await db.SaveChangesAsync();
            db.AddRange(new SignupAnswer(Guid.NewGuid(), confirmed.Id, answer.Id, "Favourite boss", "Allowed answer"), new SignupAnswer(Guid.NewGuid(), confirmed.Id, historical.Id, "Retired question", "Historical answer"));
            await db.SaveChangesAsync();
            ownerLogin = owner.LoginName;
            adminLogin = admin.LoginName;
            superAdminLogin = superAdmin.LoginName;
            formerAdminLogin = formerAdmin.LoginName;
            formerAdminId = formerAdmin.Id;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var superAdminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var formerAdminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/Events/{privateSlug}/Signups")).StatusCode);
        var table = await anonymous.GetStringAsync($"/Events/{slug}/Signups");
        Assert.Contains("Confirmed", table, StringComparison.Ordinal); Assert.Contains("Waiting list", table, StringComparison.Ordinal);
        Assert.Contains("Account 1", table, StringComparison.Ordinal); Assert.Contains("Account 2", table, StringComparison.Ordinal); Assert.Contains("Alt account", table, StringComparison.Ordinal);
        Assert.Contains("Allowed Main", table, StringComparison.Ordinal); Assert.Contains("123.45", table, StringComparison.Ordinal); Assert.Contains("EHB", table, StringComparison.Ordinal); Assert.Contains("Allowed answer", table, StringComparison.Ordinal); Assert.Contains("Historical answer", table, StringComparison.Ordinal); Assert.Contains("Not answered", table, StringComparison.Ordinal); Assert.Contains("<td>1</td>", table, StringComparison.Ordinal);
        Assert.DoesNotContain("Withdrawn Main", table, StringComparison.Ordinal);
        foreach (var secret in new[] { "PUBLIC-USERNAME-", "DISCORD-ID-SENTINEL", "DISCORD-NAME-SENTINEL", "DISCORD-PARTICIPANT-SENTINEL", "PRIVATE-COMMENTS-SENTINEL", "ADMIN-NOTES-SENTINEL", "EDIT-TOKEN-SENTINEL", "EMERGENCY-SENTINEL" }) Assert.DoesNotContain(secret, table, StringComparison.Ordinal);

        await LoginAsync(ownerClient, ownerLogin, "owner-password");
        await LoginAsync(formerAdminClient, formerAdminLogin, "former-admin-password");
        var myEvents = await ownerClient.GetStringAsync("/Account/MyEvents");
        Assert.Contains("Authenticated signup", myEvents, StringComparison.Ordinal); Assert.Contains("History", myEvents, StringComparison.Ordinal); Assert.Contains("Confirmed", myEvents, StringComparison.Ordinal); Assert.DoesNotContain("emergency-sentinel", myEvents, StringComparison.Ordinal);
        Assert.Contains($"/Events/{slug}/Signup/Confirmation", myEvents, StringComparison.Ordinal);
        Assert.Contains($"/Events/{historySlug}/Board", myEvents, StringComparison.Ordinal);
        Assert.DoesNotContain("Private sentinel", await anonymous.GetStringAsync("/"), StringComparison.Ordinal);

        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Slug == slug);
            item.CloseSignups(now);
            var team = new Team(Guid.NewGuid(), item.Id, "Published team", "published-team", TeamFormationType.Drafted, null, true); team.Finalize(now);
            var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
            db.Add(team);
            db.Add(draft);
            db.Add(new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now, Guid.NewGuid()));
            await db.SaveChangesAsync();
        }
        var rosterOverview = await anonymous.GetStringAsync("/");
        Assert.Contains("Roster available", rosterOverview, StringComparison.Ordinal);
        Assert.Contains($"/Events/{slug}/Teams", rosterOverview, StringComparison.Ordinal);
        var redirectedTable = await anonymous.GetAsync($"/Events/{slug}/Signups");
        var redirectedSignup = await ownerClient.GetAsync($"/Events/{slug}/Signup");
        Assert.Equal(HttpStatusCode.Redirect, redirectedTable.StatusCode); Assert.Equal($"/Events/{slug}/Teams", redirectedTable.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, redirectedSignup.StatusCode); Assert.Equal($"/Events/{slug}/Teams", redirectedSignup.Headers.Location?.OriginalString);
        Assert.Contains($"/Events/{slug}/Teams", await ownerClient.GetStringAsync("/Account/MyEvents"), StringComparison.Ordinal);
        await LoginAsync(adminClient, adminLogin, "admin-password");
        await LoginAsync(superAdminClient, superAdminLogin, "super-admin-password");
        var adminTable = await adminClient.GetStringAsync($"/Events/{slug}/Signups");
        Assert.Contains("Historical answer", adminTable, StringComparison.Ordinal);
        Assert.DoesNotContain("ADMIN-NOTES-SENTINEL", adminTable, StringComparison.Ordinal);
        Assert.Contains("Historical answer", await superAdminClient.GetStringAsync($"/Events/{slug}/Signups"), StringComparison.Ordinal);
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.Accounts.SingleAsync(account => account.Id == formerAdminId)).Disable(now, null, "former admin");
            await db.SaveChangesAsync();
        }
        using var formerRedirect = await formerAdminClient.GetAsync($"/Events/{slug}/Signups");
        Assert.Equal(HttpStatusCode.Redirect, formerRedirect.StatusCode);
        Assert.Equal($"/Events/{slug}/Teams", formerRedirect.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/Events/{slug}/Board")).StatusCode);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(item => item.Slug == slug);
            var board = new Bingo.Domain.Boards.Board(Guid.NewGuid(), item.Id, "Pre-live board", 1, 1);
            board.Publish(now);
            db.Boards.Add(board);
            await db.SaveChangesAsync();
        }
        var boardOverview = await anonymous.GetStringAsync("/");
        Assert.Contains("View board", boardOverview, StringComparison.Ordinal);
        Assert.Contains($"/Events/{slug}/Board", boardOverview, StringComparison.Ordinal);
        Assert.Contains($"/Events/{slug}/Board", await ownerClient.GetStringAsync("/Account/MyEvents"), StringComparison.Ordinal);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(item => item.Slug == slug);
            db.Entry(item).Property(nameof(BingoEvent.FirstPublicAt)).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/Events/{slug}/Board")).StatusCode);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(item => item.Slug == slug);
            item.StartEvent(now);
            item.EndEvent(now);
            item.FinalizeResults(now);
            db.Entry(item).Property(nameof(BingoEvent.FirstPublicAt)).CurrentValue = now;
            await db.SaveChangesAsync();
        }
        Assert.Contains($"/Events/{slug}/Board", await ownerClient.GetStringAsync("/Account/MyEvents"), StringComparison.Ordinal);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(item => item.Slug == slug);
            item.Archive(now);
            await db.SaveChangesAsync();
        }
        Assert.Contains($"/Events/{slug}/Board", await ownerClient.GetStringAsync("/Account/MyEvents"), StringComparison.Ordinal);
        await using (var verify = new ApplicationDbContext(options))
        {
            var item = await verify.Events.SingleAsync(item => item.Slug == slug);
            Assert.Equal(EventState.Archived, item.State);
            Assert.NotNull(item.ActualStartedAt);
        }

        async Task LoginAsync(HttpClient client, string username, string password)
        {
            var login = await client.GetStringAsync("/Account/Login");
            using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = AntiforgeryToken(login) }));
            Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        }
    }

    [Fact]
    public async Task AdminParticipantWorkspaceFiltersPrivateProjectionAndPublicPrivacyRemainBounded()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"workspace-admin-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin); admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, incrementVersion: false);
        var owner = Website($"WORKSPACE-USERNAME-{Guid.NewGuid():N}", now); owner.SetDiscordIdentity("WORKSPACE-DISCORD-ID", "WORKSPACE-DISCORD-DISPLAY");
        var bingoEvent = Event(admin.Id, now); bingoEvent.MarkFirstPublic(now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Regular account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var alt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "alt", "Alt account", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Informational);
        var answer = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "answer", "Custom answer", SignupQuestionType.Text, false, 2, null);
        var historical = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "old", "Historical answer", SignupQuestionType.Text, false, 3, null); historical.Deactivate(admin.Id, now, "replaced");
        var confirmed = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website); confirmed.AssignOwner(owner); confirmed.SetCaptainVolunteer(true); confirmed.SetPaymentReceived(true); confirmed.SetAdminNotes("WORKSPACE-PRIVATE-NOTE");
        var waiting = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.WaitingList, 2, now.AddMinutes(1), SignupSource.AdminCreated, null);
        var withdrawn = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Withdrawn, 3, now.AddMinutes(2), SignupSource.CsvImport, null);
        var main = new OsrsCharacter(Guid.NewGuid(), "Workspace Main", $"WORKSPACE MAIN {Guid.NewGuid():N}", now); var alternate = new OsrsCharacter(Guid.NewGuid(), "Workspace Alt", $"WORKSPACE ALT {Guid.NewGuid():N}", now); var waitingMain = new OsrsCharacter(Guid.NewGuid(), "Waiting Workspace", $"WAITING WORKSPACE {Guid.NewGuid():N}", now); var withdrawnMain = new OsrsCharacter(Guid.NewGuid(), "Withdrawn Workspace", $"WITHDRAWN WORKSPACE {Guid.NewGuid():N}", now);
        var team = new Team(Guid.NewGuid(), bingoEvent.Id, "Workspace team", "workspace-team", TeamFormationType.Drafted, null, true);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, owner, bingoEvent, form, regular, alt, answer, historical, confirmed, waiting, withdrawn, main, alternate, waitingMain, withdrawnMain, team,
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, confirmed.Id, main.Id, 0, now, owner.Id, regular.Id, EventCharacterRole.Playing, 42.5m, EhbSource.Manual, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, confirmed.Id, alternate.Id, 1, now, owner.Id, alt.Id, EventCharacterRole.Informational, null, null, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, waiting.Id, waitingMain.Id, 0, now, admin.Id, regular.Id, EventCharacterRole.Playing, 2m, EhbSource.Manual, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, withdrawn.Id, withdrawnMain.Id, 0, now, admin.Id, regular.Id, EventCharacterRole.Playing, 3m, EhbSource.Manual, null),
                new TeamMembership(Guid.NewGuid(), team.Id, confirmed.Id, TeamMembershipRole.Participant, now, null, null));
            await db.SaveChangesAsync();
            db.AddRange(new SignupAnswer(Guid.NewGuid(), confirmed.Id, answer.Id, "Custom answer", "WORKSPACE-ANSWER"), new SignupAnswer(Guid.NewGuid(), confirmed.Id, historical.Id, "Historical answer", "WORKSPACE-HISTORICAL"));
            await db.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.LoginName, "password");
        var route = $"/Admin/Events/Manage/{bingoEvent.Id}";
        var page = await client.GetStringAsync(route);
        Assert.Contains("3 total · 1 confirmed · 1 waiting · 1 withdrawn", page, StringComparison.Ordinal); Assert.Contains("WORKSPACE-USERNAME-", page, StringComparison.Ordinal); Assert.Contains("Discord linked", page, StringComparison.Ordinal); Assert.Contains("Unowned", page, StringComparison.Ordinal); Assert.Contains("Workspace team", page, StringComparison.Ordinal);
        foreach (var query in new[] { "ParticipantSearch=WORKSPACE", "ParticipantStatus=WaitingList", "ParticipantPayment=paid", "ParticipantDiscord=linked", "ParticipantCaptain=true", "ParticipantSource=Website", $"ParticipantTeamId={team.Id}" })
        { var filtered = await client.GetStringAsync($"{route}?{query}"); Assert.Contains("3 total", filtered, StringComparison.Ordinal); Assert.Contains(query.Contains("Waiting") ? "Waiting Workspace" : "Workspace Main", filtered, StringComparison.Ordinal); }
        var detail = await client.GetStringAsync($"/Admin/Events/Participant/{bingoEvent.Id}/Participants/{confirmed.Id}");
        foreach (var expected in new[] { "Workspace Main", "42.5", "Workspace Alt", "WORKSPACE-ANSWER", "WORKSPACE-HISTORICAL", "WORKSPACE-PRIVATE-NOTE", "Website signup" }) Assert.Contains(expected, detail, StringComparison.Ordinal);
        foreach (var secret in new[] { "WORKSPACE-DISCORD-ID", "WORKSPACE-DISCORD-DISPLAY", "WORKSPACE-TOKEN" }) Assert.DoesNotContain(secret, detail, StringComparison.Ordinal);
        var publicTable = await factory.CreateClient().GetStringAsync($"/Events/{bingoEvent.Slug}/Signups");
        foreach (var secret in new[] { "WORKSPACE-USERNAME-", "WORKSPACE-DISCORD", "WORKSPACE-PRIVATE-NOTE", "WORKSPACE-TOKEN", "AdminCreated", "CsvImport" }) Assert.DoesNotContain(secret, publicTable, StringComparison.Ordinal);

        async Task LoginAsync(HttpClient http, string username, string password)
        { var login = await http.GetStringAsync("/Account/Login"); using var response = await http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = AntiforgeryToken(login) })); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); }
    }

    [Fact]
    public async Task AdminPaymentAndNotesUseNativePostsAuditsAndStaleProtection()
    {
        var now = DateTimeOffset.UtcNow; var admin = Website($"mutation-admin-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin); admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, incrementVersion: false);
        var bingoEvent = Event(admin.Id, now); var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website, null); var character = new OsrsCharacter(Guid.NewGuid(), "Mutation Main", $"MUTATION {Guid.NewGuid():N}", now); var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now); var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, bingoEvent, participant, character, form, regular, new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, participant.Id, character.Id, 0, now, admin.Id, regular.Id, EventCharacterRole.Playing, 1m, EhbSource.Manual, null)); await db.SaveChangesAsync(); }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString())); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login"); using (var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = admin.LoginName, ["Input.Password"] = "password", ["__RequestVerificationToken"] = AntiforgeryToken(login) }))) Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        var manageRoute = $"/Admin/Events/Manage/{bingoEvent.Id}"; var manage = await client.GetStringAsync(manageRoute);
        using (var paid = await client.PostAsync($"{manageRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["participantId"] = participant.Id.ToString(), ["payment"] = "Paid", ["__RequestVerificationToken"] = AntiforgeryToken(manage) }))) Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode);
        var detailRoute = $"/Admin/Events/Participant/{bingoEvent.Id}/Participants/{participant.Id}"; var detail = await client.GetStringAsync(detailRoute);
        Assert.Contains($"action=\"{detailRoute}?handler=Payment\"", detail, StringComparison.Ordinal);
        using (var unpaid = await client.PostAsync($"{detailRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["payment"] = "Unpaid", ["__RequestVerificationToken"] = AntiforgeryToken(detail) }))) { Assert.Equal(HttpStatusCode.Redirect, unpaid.StatusCode); Assert.Equal($"{detailRoute}#payment", unpaid.Headers.Location?.OriginalString); }
        var afterUnpaid = await client.GetStringAsync(detailRoute);
        using (var paidAgain = await client.PostAsync($"{detailRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["payment"] = "Paid", ["__RequestVerificationToken"] = AntiforgeryToken(afterUnpaid) }))) Assert.Equal(HttpStatusCode.Redirect, paidAgain.StatusCode);
        var afterPaid = await client.GetStringAsync(detailRoute);
        using (var unpaidAgain = await client.PostAsync($"{detailRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["payment"] = "Unpaid", ["__RequestVerificationToken"] = AntiforgeryToken(afterPaid) }))) Assert.Equal(HttpStatusCode.Redirect, unpaidAgain.StatusCode);
        using (var note = await client.PostAsync($"{detailRoute}?handler=AdminNote", new FormUrlEncodedContent(new Dictionary<string, string> { ["AdminNote"] = "private note", ["ExpectedAdminNote"] = "", ["__RequestVerificationToken"] = AntiforgeryToken(detail) }))) Assert.Equal(HttpStatusCode.Redirect, note.StatusCode);
        var stale = await client.GetStringAsync(detailRoute);
        using (var rejected = await client.PostAsync($"{detailRoute}?handler=AdminNote", new FormUrlEncodedContent(new Dictionary<string, string> { ["AdminNote"] = "stale overwrite", ["ExpectedAdminNote"] = "", ["__RequestVerificationToken"] = AntiforgeryToken(stale) }))) Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        await using (var verify = new ApplicationDbContext(options)) { var saved = await verify.EventParticipants.SingleAsync(x => x.Id == participant.Id); Assert.False(saved.PaymentReceived); Assert.Equal("private note", saved.AdminNotes); var audits = await verify.AuditEntries.Where(x => x.TargetId == participant.Id.ToString()).ToListAsync(); Assert.Equal(4, audits.Count(x => x.Action == "participant.payment_updated")); Assert.Contains(audits, x => x.Action == "participant.admin_note_updated" && x.BeforeState!.Contains("false") && x.AfterState!.Contains("true")); Assert.Equal(1, audits.Count(x => x.Action == "participant.admin_note_updated")); }
        var post = await client.GetStringAsync(detailRoute); Assert.Contains("Save note", post, StringComparison.Ordinal); Assert.Contains("handler=Payment", post, StringComparison.Ordinal); Assert.Contains("data-save-state", post, StringComparison.Ordinal); Assert.DoesNotContain("data-admin-note-form", post, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminCorrectionAndInternalCreationUseTheActiveFormAtomically()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"admin-46b-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin); admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, incrementVersion: false);
        var owner = Website($"owner-46b-{Guid.NewGuid():N}", now); owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "password"), false, now, incrementVersion: false);
        var bingoEvent = Event(admin.Id, now); bingoEvent.CloseSignups(now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "regular", "Regular", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var alt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "alt", "Alt", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Informational);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 2, null, SignupSystemField.CaptainVolunteer);
        var answer = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "answer", "Answer", SignupQuestionType.Text, true, 3, null);
        var first = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website, null); first.AssignOwner(owner); first.SetPaymentReceived(true); first.SetAdminNotes("private-note");
        var other = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 2, now.AddMinutes(1), SignupSource.Website, null);
        var oldRegular = new OsrsCharacter(Guid.NewGuid(), "Old Regular", "OLD REGULAR", now); var newRegular = new OsrsCharacter(Guid.NewGuid(), "New Regular", "NEW REGULAR", now); var oldAlt = new OsrsCharacter(Guid.NewGuid(), "Old Alt", "OLD ALT", now); var newAlt = new OsrsCharacter(Guid.NewGuid(), "New Alt", "NEW ALT", now); var reserved = new OsrsCharacter(Guid.NewGuid(), "Reserved", "RESERVED", now);
        await using (var db = new ApplicationDbContext(options))
        {
            first.SetCaptainVolunteer(true);
            db.AddRange(admin, owner, bingoEvent, form, regular, alt, captain, answer, first, other, oldRegular, newRegular, oldAlt, newAlt, reserved,
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, first.Id, oldRegular.Id, 0, now, admin.Id, regular.Id, EventCharacterRole.Playing, 5m, EhbSource.Manual, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, first.Id, oldAlt.Id, 1, now, admin.Id, alt.Id, EventCharacterRole.Informational, null, null, null),
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, other.Id, reserved.Id, 0, now, admin.Id, regular.Id, EventCharacterRole.Playing, 7m, EhbSource.Manual, null));
            await db.SaveChangesAsync(); db.Add(new SignupAnswer(Guid.NewGuid(), first.Id, answer.Id, "Answer", "before")); await db.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.LoginName, "password");
        var detailRoute = $"/Admin/Events/Participant/{bingoEvent.Id}/Participants/{first.Id}"; var detail = await client.GetStringAsync(detailRoute);
        Assert.Contains($"name=\"Input.CustomAnswers[{captain.Id}]\"", detail, StringComparison.Ordinal);
        Assert.Contains("value=\"true\" selected", detail, StringComparison.Ordinal);
        using (var corrected = await client.PostAsync(detailRoute, new FormUrlEncodedContent(new Dictionary<string, string> { [$"Input.AccountAnswers[{regular.Id}].CharacterName"] = newRegular.DisplayName, [$"Input.AccountAnswers[{regular.Id}].Ehb"] = "19", [$"Input.AccountAnswers[{alt.Id}].CharacterName"] = newAlt.DisplayName, [$"Input.CustomAnswers[{captain.Id}]"] = "true", [$"Input.CustomAnswers[{answer.Id}]"] = "after", ["Input.ExpectedResponseVersion"] = "1", ["__RequestVerificationToken"] = AntiforgeryToken(detail) }))) Assert.Equal(HttpStatusCode.Redirect, corrected.StatusCode);
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.EventParticipants.SingleAsync(x => x.Id == first.Id); Assert.Equal(SignupSource.Website, saved.Source); Assert.Equal(now, saved.SignedUpAt); Assert.Equal(1, saved.SignupSequence); Assert.True(saved.PaymentReceived); Assert.True(saved.CaptainVolunteer); Assert.Equal("private-note", saved.AdminNotes);
            Assert.Equal("after", await verify.SignupAnswers.Where(x => x.EventParticipantId == first.Id && x.SignupQuestionId == answer.Id).Select(x => x.Value).SingleAsync());
            Assert.Equal(2, await verify.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == first.Id && x.ReleasedAt == null)); Assert.Contains(await verify.AuditEntries.Where(x => x.TargetId == first.Id.ToString()).ToListAsync(), x => x.Action == "participant.corrected" && x.BeforeState != x.AfterState);
        }
        var secondDetail = await client.GetStringAsync(detailRoute);
        using (var rejected = await client.PostAsync(detailRoute, new FormUrlEncodedContent(new Dictionary<string, string> { [$"Input.AccountAnswers[{regular.Id}].CharacterName"] = reserved.DisplayName, [$"Input.AccountAnswers[{regular.Id}].Ehb"] = "23", [$"Input.AccountAnswers[{alt.Id}].CharacterName"] = newAlt.DisplayName, [$"Input.CustomAnswers[{captain.Id}]"] = "true", [$"Input.CustomAnswers[{answer.Id}]"] = "must-not-save", ["Input.ExpectedResponseVersion"] = "2", ["__RequestVerificationToken"] = AntiforgeryToken(secondDetail) }))) Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        await using (var verify = new ApplicationDbContext(options)) { Assert.Equal("after", await verify.SignupAnswers.Where(x => x.EventParticipantId == first.Id && x.SignupQuestionId == answer.Id).Select(x => x.Value).SingleAsync()); Assert.Equal(1, await verify.AuditEntries.CountAsync(x => x.TargetId == first.Id.ToString() && x.Action == "participant.corrected")); }
        var captainDetail = await client.GetStringAsync(detailRoute);
        using (var captainChanged = await client.PostAsync(detailRoute, new FormUrlEncodedContent(new Dictionary<string, string> { [$"Input.AccountAnswers[{regular.Id}].CharacterName"] = newRegular.DisplayName, [$"Input.AccountAnswers[{regular.Id}].Ehb"] = "19", [$"Input.AccountAnswers[{alt.Id}].CharacterName"] = newAlt.DisplayName, [$"Input.CustomAnswers[{captain.Id}]"] = "false", [$"Input.CustomAnswers[{answer.Id}]"] = "after", ["Input.ExpectedResponseVersion"] = "2", ["__RequestVerificationToken"] = AntiforgeryToken(captainDetail) }))) Assert.Equal(HttpStatusCode.Redirect, captainChanged.StatusCode);
        await using (var verify = new ApplicationDbContext(options)) Assert.False(await verify.EventParticipants.Where(x => x.Id == first.Id).Select(x => x.CaptainVolunteer).SingleAsync());
        var missingVersion = await client.GetStringAsync(detailRoute);
        ParticipantState correctionBeforeStale;
        await using (var snapshot = new ApplicationDbContext(options)) correctionBeforeStale = await ParticipantStateAsync(snapshot, first.Id);
        using (var stale = await client.PostAsync(detailRoute, new FormUrlEncodedContent(new Dictionary<string, string> { [$"Input.AccountAnswers[{regular.Id}].CharacterName"] = oldRegular.DisplayName, [$"Input.AccountAnswers[{regular.Id}].Ehb"] = "999", [$"Input.AccountAnswers[{alt.Id}].CharacterName"] = oldAlt.DisplayName, [$"Input.CustomAnswers[{captain.Id}]"] = "true", [$"Input.CustomAnswers[{answer.Id}]"] = "must-not-save", ["__RequestVerificationToken"] = AntiforgeryToken(missingVersion) }))) { Assert.Equal(HttpStatusCode.OK, stale.StatusCode); Assert.Contains("reload", await stale.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase); }
        await using (var verify = new ApplicationDbContext(options)) { Assert.Equal(correctionBeforeStale, await ParticipantStateAsync(verify, first.Id)); var saved = await verify.EventParticipants.SingleAsync(x => x.Id == first.Id); Assert.False(saved.CaptainVolunteer); Assert.Equal(SignupStatus.Confirmed, saved.SignupStatus); Assert.Equal(SignupSource.Website, saved.Source); Assert.Equal(now, saved.SignedUpAt); Assert.Equal(1, saved.SignupSequence); Assert.Equal(3, saved.ResponseVersion); Assert.True(saved.PaymentReceived); Assert.Equal("private-note", saved.AdminNotes); Assert.Equal("after", await verify.SignupAnswers.Where(x => x.EventParticipantId == first.Id && x.SignupQuestionId == answer.Id).Select(x => x.Value).SingleAsync()); Assert.Equal(19m, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == first.Id && x.ReleasedAt == null && x.SignupQuestionId == regular.Id).Select(x => x.EhbSnapshot).SingleAsync()); Assert.Equal(2, await verify.AuditEntries.CountAsync(x => x.TargetId == first.Id.ToString() && x.Action == "participant.corrected")); }
        var manageRoute = $"/Admin/Events/Manage/{bingoEvent.Id}"; var manage = await client.GetStringAsync(manageRoute);
        using (var created = await client.PostAsync($"{manageRoute}?handler=CreateInternalParticipant", new FormUrlEncodedContent(new Dictionary<string, string> { [$"InternalParticipant.AccountAnswers[{regular.Id}].CharacterName"] = $"Internal {Guid.NewGuid():N}", [$"InternalParticipant.AccountAnswers[{regular.Id}].Ehb"] = "8", [$"InternalParticipant.Answers[{answer.Id}]"] = "created", ["__RequestVerificationToken"] = AntiforgeryToken(manage) }))) Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        await using (var verify = new ApplicationDbContext(options)) { var created = await verify.EventParticipants.SingleAsync(x => x.Source == SignupSource.AdminCreated); Assert.Null(created.AccountId); Assert.Equal(SignupStatus.Confirmed, created.SignupStatus); Assert.NotNull(await verify.SignupForms.Where(x => x.Id == form.Id).Select(x => x.FirstResponseAt).SingleAsync()); }

        async Task LoginAsync(HttpClient http, string username, string password)
        { var login = await http.GetStringAsync("/Account/Login"); using var signedIn = await http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = AntiforgeryToken(login) })); Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode); }
    }

    [Fact]
    public async Task AdminOwnershipTransferChangesOnlyParticipantAccessAndRejectsDuplicateDestination()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"transfer-admin-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin); admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, incrementVersion: false);
        var oldOwner = Website($"old-owner-{Guid.NewGuid():N}", now); oldOwner.SetPassword(new PasswordHasher<Account>().HashPassword(oldOwner, "password"), false, now, incrementVersion: false);
        var newOwner = Website($"new-owner-{Guid.NewGuid():N}", now); newOwner.SetPassword(new PasswordHasher<Account>().HashPassword(newOwner, "password"), false, now, incrementVersion: false);
        var duplicateOwner = Website($"duplicate-owner-{Guid.NewGuid():N}", now);
        var bingoEvent = Event(admin.Id, now); bingoEvent.MarkFirstPublic(now); var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now); var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "regular", "Regular", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website, null); participant.AssignOwner(oldOwner); participant.SetPaymentReceived(true); participant.SetAdminNotes("private");
        var duplicate = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 2, now.AddMinutes(1), SignupSource.Website, null); duplicate.AssignOwner(duplicateOwner);
        var character = new OsrsCharacter(Guid.NewGuid(), "Transfer Main", $"TRANSFER {Guid.NewGuid():N}", now);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, oldOwner, newOwner, duplicateOwner, bingoEvent, form, regular, participant, duplicate, character, new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, participant.Id, character.Id, 0, now, admin.Id, regular.Id, EventCharacterRole.Playing, 12m, EhbSource.Manual, null)); await db.SaveChangesAsync(); }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); await LoginAsync(adminClient, admin.LoginName, "password");
        var detailRoute = $"/Admin/Events/Participant/{bingoEvent.Id}/Participants/{participant.Id}"; var detail = await adminClient.GetStringAsync(detailRoute);
        using (var invalid = await adminClient.PostAsync($"{detailRoute}?handler=TransferOwnership", new FormUrlEncodedContent(new Dictionary<string, string> { ["ExpectedOwnerAccountId"] = oldOwner.Id.ToString(), ["DestinationUsername"] = newOwner.LoginName, ["DestinationUsernameConfirmation"] = "wrong", ["__RequestVerificationToken"] = AntiforgeryToken(detail) }))) Assert.Equal(HttpStatusCode.Redirect, invalid.StatusCode);
        var transferPage = await adminClient.GetStringAsync(detailRoute);
        using (var transferred = await adminClient.PostAsync($"{detailRoute}?handler=TransferOwnership", new FormUrlEncodedContent(new Dictionary<string, string> { ["ExpectedOwnerAccountId"] = oldOwner.Id.ToString(), ["DestinationUsername"] = newOwner.LoginName, ["DestinationUsernameConfirmation"] = newOwner.LoginName, ["__RequestVerificationToken"] = AntiforgeryToken(transferPage) }))) Assert.Equal(HttpStatusCode.Redirect, transferred.StatusCode);
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.EventParticipants.SingleAsync(x => x.Id == participant.Id); Assert.Equal(newOwner.Id, saved.AccountId); Assert.Equal(now, saved.SignedUpAt); Assert.Equal(1, saved.SignupSequence); Assert.True(saved.PaymentReceived); Assert.Equal("private", saved.AdminNotes);
            Assert.Equal(12m, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
            Assert.Equal(2, await verify.PersonalNotifications.CountAsync(x => x.Title == "participant.ownership_transferred")); Assert.Single(await verify.AuditEntries.Where(x => x.TargetId == participant.Id.ToString() && x.Action == "participant.ownership_transferred").ToListAsync());
        }
        using var oldClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); await LoginAsync(oldClient, oldOwner.LoginName, "password"); Assert.NotEqual(HttpStatusCode.OK, (await oldClient.GetAsync($"/Events/{bingoEvent.Slug}/Signup/Confirmation?participantId={participant.Id}")).StatusCode);
        using var newClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); await LoginAsync(newClient, newOwner.LoginName, "password"); Assert.Equal(HttpStatusCode.OK, (await newClient.GetAsync($"/Events/{bingoEvent.Slug}/Signup/Confirmation?participantId={participant.Id}")).StatusCode);
        var duplicatePage = await adminClient.GetStringAsync(detailRoute);
        using (var duplicateTransfer = await adminClient.PostAsync($"{detailRoute}?handler=TransferOwnership", new FormUrlEncodedContent(new Dictionary<string, string> { ["ExpectedOwnerAccountId"] = newOwner.Id.ToString(), ["DestinationUsername"] = duplicateOwner.LoginName, ["DestinationUsernameConfirmation"] = duplicateOwner.LoginName, ["__RequestVerificationToken"] = AntiforgeryToken(duplicatePage) }))) Assert.Equal(HttpStatusCode.Redirect, duplicateTransfer.StatusCode);
        await using (var verify = new ApplicationDbContext(options)) { Assert.Equal(newOwner.Id, await verify.EventParticipants.Where(x => x.Id == participant.Id).Select(x => x.AccountId).SingleAsync()); Assert.Equal(2, await verify.PersonalNotifications.CountAsync(x => x.Title == "participant.ownership_transferred")); }

        async Task LoginAsync(HttpClient http, string username, string password)
        { var login = await http.GetStringAsync("/Account/Login"); using var signedIn = await http.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = AntiforgeryToken(login) })); Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode); }
    }

    [Fact]
    public async Task ConcurrentStaleOwnershipTransfersHaveOneWinnerAndNoLoserResidue()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"race-admin-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin);
        var original = Website($"race-original-{Guid.NewGuid():N}", now); var firstDestination = Website($"race-first-{Guid.NewGuid():N}", now); var secondDestination = Website($"race-second-{Guid.NewGuid():N}", now);
        var bingoEvent = Event(admin.Id, now); bingoEvent.MarkFirstPublic(now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now); var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "regular", "Regular", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing); var answer = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "answer", "Answer", SignupQuestionType.Text, false, 1, null);
        var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website, null); participant.AssignOwner(original); participant.SetPaymentReceived(true); participant.SetAdminNotes("race-private-note");
        var character = new OsrsCharacter(Guid.NewGuid(), "Race Main", "RACE MAIN", now); var team = new Team(Guid.NewGuid(), bingoEvent.Id, "Race team", $"race-team-{Guid.NewGuid():N}", TeamFormationType.Drafted, null, true);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, original, firstDestination, secondDestination, bingoEvent, form, regular, answer, participant, character, team,
                new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, participant.Id, character.Id, 0, now, admin.Id, regular.Id, EventCharacterRole.Playing, 13m, EhbSource.Manual, null),
                new AccountOsrsCharacter(Guid.NewGuid(), original.Id, character.Id, original.Id, true, 0, null, 13m, now),
                new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, null));
            await setup.SaveChangesAsync(); setup.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, answer.Id, "Answer", "retained")); await setup.SaveChangesAsync();
        }
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = TransferAsync(firstDestination.LoginName); var second = TransferAsync(secondDestination.LoginName); start.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, results.Count(x => x.Succeeded)); Assert.Equal(1, results.Count(x => !x.Succeeded && x.Error!.Contains("changed elsewhere", StringComparison.Ordinal)));
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.EventParticipants.SingleAsync(x => x.Id == participant.Id); Assert.True(saved.AccountId is { } winner && (winner == firstDestination.Id || winner == secondDestination.Id)); Assert.Equal(SignupSource.Website, saved.Source); Assert.Equal(now, saved.SignedUpAt); Assert.Equal(1, saved.SignupSequence); Assert.True(saved.PaymentReceived); Assert.Equal("race-private-note", saved.AdminNotes);
            Assert.Equal("retained", await verify.SignupAnswers.Where(x => x.EventParticipantId == participant.Id).Select(x => x.Value).SingleAsync()); Assert.Equal(13m, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
            Assert.Equal(original.Id, await verify.AccountOsrsCharacters.Where(x => x.OsrsCharacterId == character.Id).Select(x => x.AccountId).SingleAsync()); Assert.Equal(team.Id, await verify.TeamMemberships.Where(x => x.EventParticipantId == participant.Id && x.LeftAt == null).Select(x => x.TeamId).SingleAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.TargetId == participant.Id.ToString() && x.Action == "participant.ownership_transferred").ToListAsync()); Assert.Equal(2, await verify.PersonalNotifications.CountAsync(x => x.Title == "participant.ownership_transferred"));
        }

        async Task<ParticipantOwnershipTransferResult> TransferAsync(string destination)
        {
            await start.Task;
            await using var context = new ApplicationDbContext(options);
            var service = new SignupService(context, new SecretHasher(), TimeProvider.System);
            return await service.TransferParticipantOwnershipAsync(new ParticipantOwnershipTransferRequest(bingoEvent.Id, participant.Id, admin.Id, admin.LoginName, destination, destination, original.Id));
        }
    }

    private static async Task<ParticipantState> ParticipantStateAsync(ApplicationDbContext db, Guid participantId)
    {
        var participant = await db.EventParticipants.SingleAsync(item => item.Id == participantId);
        var assignments = await db.EventParticipantCharacters.Where(item => item.EventParticipantId == participantId).OrderBy(item => item.RegistrationOrder).Select(item => $"{item.OsrsCharacterId}:{item.EventRole}:{item.EhbSnapshot}:{item.ReleasedAt}:{item.SignupQuestionId}").ToListAsync();
        var answers = await db.SignupAnswers.Where(item => item.EventParticipantId == participantId).OrderBy(item => item.SignupQuestionId).Select(item => $"{item.SignupQuestionId}:{item.Value}").ToListAsync();
        return new(participant.AccountId, participant.SignedUpAt, participant.SignupSequence, participant.SignupStatus, participant.Source, participant.ResponseVersion, participant.CaptainVolunteer, participant.PaymentReceived, participant.AdminNotes, string.Join('|', assignments), string.Join('|', answers), await db.AuditEntries.CountAsync(item => item.TargetId == participantId.ToString()), await db.PersonalNotifications.CountAsync());
    }

    private sealed record ParticipantState(Guid? OwnerId, DateTimeOffset SignedUpAt, long SignupSequence, SignupStatus Status, SignupSource Source, int ResponseVersion, bool CaptainVolunteer, bool PaymentReceived, string? AdminNote, string Assignments, string Answers, int AuditCount, int NotificationCount);

    private static Account Website(string name, DateTimeOffset now) => Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);

    private static FormUrlEncodedContent SignupPost(string page, Guid questionId, Guid characterId, string ehb) => new(new Dictionary<string, string>
    {
        [$"Input.AccountAnswers[{questionId}].OsrsCharacterId"] = characterId.ToString(),
        [$"Input.AccountAnswers[{questionId}].Ehb"] = ehb,
        ["Input.ExpectedResponseVersion"] = HiddenValue(page, "Input_ExpectedResponseVersion"),
        ["__RequestVerificationToken"] = AntiforgeryToken(page)
    });

    private static FormUrlEncodedContent RenderedSignupPost(string page, Guid primaryQuestionId, Guid? primaryCharacterId, Guid optionalRegularQuestionId, Guid optionalAltQuestionId, Guid textQuestionId, string? signupCode, Guid? optionalRegularCharacterId = null) => new(new Dictionary<string, string>
    {
        [$"Input.AccountAnswers[{primaryQuestionId}].OsrsCharacterId"] = primaryCharacterId?.ToString() ?? string.Empty,
        [$"Input.AccountAnswers[{primaryQuestionId}].Ehb"] = "18",
        [$"Input.AccountAnswers[{optionalRegularQuestionId}].OsrsCharacterId"] = optionalRegularCharacterId?.ToString() ?? string.Empty,
        [$"Input.AccountAnswers[{optionalRegularQuestionId}].Ehb"] = string.Empty,
        [$"Input.AccountAnswers[{optionalAltQuestionId}].OsrsCharacterId"] = string.Empty,
        [$"Input.Answers[{textQuestionId}]"] = "A retained answer",
        ["Input.SignupCode"] = signupCode ?? string.Empty,
        ["__RequestVerificationToken"] = AntiforgeryToken(page)
    });

    private static FormUrlEncodedContent SignupCodePost(string page, bool required, string? code) => new(new Dictionary<string, string>
    {
        ["Settings.RequireSignupCode"] = required ? "true" : "false",
        ["Settings.NewSignupCode"] = code ?? string.Empty,
        ["__RequestVerificationToken"] = AntiforgeryToken(page)
    });

    private static FormUrlEncodedContent EditPost(string page, Guid questionId, string label, string? helpText, IReadOnlyDictionary<string, string>? structural = null)
    {
        var values = new Dictionary<string, string>
        {
            ["questionId"] = questionId.ToString(),
            ["Edit.Label"] = label,
            ["Edit.HelpText"] = helpText ?? string.Empty,
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        };
        if (structural is not null)
            foreach (var item in structural) values[item.Key] = item.Value;
        return new FormUrlEncodedContent(values);
    }

    private sealed record QuestionState(string Label, string? HelpText, SignupQuestionType Type, bool Required, string? Options, EventCharacterRole? AccountRole, int AnswerCount, int AssignmentCount, int FormVersion, int AuditCount);

    private static FormUrlEncodedContent QuestionPost(string page, string label, string type = "Text") => new(new Dictionary<string, string>
    {
        ["Input.Label"] = label,
        ["Input.Type"] = type,
        ["Input.HelpText"] = string.Empty,
        ["Input.Options"] = string.Empty,
        ["Input.AccountRole"] = string.Empty,
        ["__RequestVerificationToken"] = AntiforgeryToken(page)
    });

    private static string InputValue(string page, string id) => Regex.Match(page, $"<input id=\"{id}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
    private static string HiddenValue(string page, string id) => InputValue(page, id);

    private static decimal InputDecimal(string page, string id) => decimal.Parse(InputValue(page, id), CultureInfo.InvariantCulture);

    private static string Option(string page, Guid id) => Regex.Match(page, $"<option[^>]*value=\"{id}\"[^>]*>").Value;

    private static BingoEvent Event(Guid ownerId, DateTimeOffset now)
    {
        var item = new BingoEvent(Guid.NewGuid(), "Authenticated signup", $"authenticated-signup-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 10, ownerId, now);
        item.OpenSignups(now);
        return item;
    }

    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
}
