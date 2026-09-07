using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task DeleteQuestionMigrationRepairsOnlyPriorRemovalsInPreDraftEvents()
    {
        await using var db = new ApplicationDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20260905221344_AddImmutableCatalogueItemIdentity");
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"delete-migration-{Guid.NewGuid():N}", now);
        db.Add(admin);
        var cases = new List<(SignupQuestion Question, EventParticipant Participant, EventParticipantCharacter Assignment, AccountOsrsCharacter Link, bool Delete)>();
        foreach (var kind in new[] { "draft", "open", "closed", "locked", "cancelled", "replacement", "conversion", "legacy", "system", "active" })
        {
            var bingoEvent = new BingoEvent(Guid.NewGuid(), "Migration deletion", $"migration-delete-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 10, admin.Id, now);
            if (kind != "draft") { bingoEvent.MarkFirstPublic(now); bingoEvent.OpenSignups(now); }
            if (kind is "closed" or "locked") bingoEvent.CloseSignups(now);
            if (kind == "locked") bingoEvent.SetDraftLocked(true);
            if (kind == "cancelled") bingoEvent.Cancel(admin.Id, now, "preserve history", true);
            var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
            var question = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, kind == "legacy" ? "legacy_alt_account" : kind, kind, SignupQuestionType.Account, kind == "system", 0, null,
                kind == "system" ? SignupSystemField.PrimaryRegularAccount : SignupSystemField.None, EventCharacterRole.Playing);
            if (kind is not ("system" or "active")) question.Deactivate(admin.Id, now, kind == "conversion" ? "Retained conversion" : null);
            if (kind == "replacement")
            {
                var replacement = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "new_question", "new question", SignupQuestionType.Text, false, 1, null);
                question.ReplaceWith(replacement.Id);
                db.Add(replacement);
            }
            var character = new OsrsCharacter(Guid.NewGuid(), "Migration account", $"MIGRATION ACCOUNT {Guid.NewGuid():N}", now);
            var link = new AccountOsrsCharacter(Guid.NewGuid(), admin.Id, character.Id, admin.Id, false, cases.Count, null, 4m, now);
            var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
            participant.AssignOwner(admin);
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, participant.Id, character.Id, 0, now, admin.Id, question.Id, EventCharacterRole.Playing, 4m, EhbSource.Manual, null);
            db.AddRange(bingoEvent, form, question, character, link, participant, assignment, new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, character.Id));
            cases.Add((question, participant, assignment, link, kind is "draft" or "open" or "closed"));
        }
        await db.SaveChangesAsync();
        var systemId = cases.Single(x => x.Question.SystemField == SignupSystemField.PrimaryRegularAccount).Question.Id;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE signup_questions SET active = false, disabled_at = {now} WHERE id = {systemId}");
        db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync();
        foreach (var item in cases)
        {
            var question = await db.SignupQuestions.AsNoTracking().SingleAsync(x => x.Id == item.Question.Id);
            var assignment = await db.EventParticipantCharacters.AsNoTracking().SingleAsync(x => x.Id == item.Assignment.Id);
            Assert.Equal(item.Delete, question.DisabledReason == SignupQuestion.DeletedReason);
            Assert.Equal(!item.Delete, await db.SignupAnswers.AnyAsync(x => x.SignupQuestionId == question.Id));
            Assert.Equal(item.Delete, assignment.ReleasedAt is not null);
            Assert.Equal(4m, assignment.EhbSnapshot);
            Assert.True(await db.AccountOsrsCharacters.Where(x => x.Id == item.Link.Id).Select(x => x.Active).SingleAsync());
            var participant = await db.EventParticipants.AsNoTracking().SingleAsync(x => x.Id == item.Participant.Id);
            Assert.Equal(item.Delete ? 2 : 1, participant.ResponseVersion);
            Assert.Equal(SignupStatus.Confirmed, participant.SignupStatus);
            Assert.Equal(1, participant.SignupSequence);
            Assert.Equal(item.Delete ? 1 : 0, await db.SignupForms.Where(x => x.Id == question.SignupFormId).Select(x => x.Version).SingleAsync());
        }
        Assert.Equal(3, await db.AuditEntries.CountAsync(x => x.Action == "signup_question.deleted"));
    }

    [Theory]
    [InlineData(EventCharacterRole.Playing)]
    [InlineData(EventCharacterRole.Informational)]
    public async Task DeleteQuestionRemovesAnswersAndReservationsWithoutResurrectingAccounts(EventCharacterRole role)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var admin = Website($"delete-admin-{Guid.NewGuid():N}", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "delete-password"), false, now, incrementVersion: false);
        var borrower = Website($"delete-borrower-{Guid.NewGuid():N}", now);
        var bingoEvent = Event(admin.Id, now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var optional = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "optional", "Remove optional account", SignupQuestionType.Account, false, 1, null, accountAnswerRole: role);
        var answered = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "answered", "Remove answered question", SignupQuestionType.Text, false, 2, null);
        var unanswered = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "unanswered", "Remove unanswered question", SignupQuestionType.Text, false, 3, null);
        var original = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "original", "Retained original question", SignupQuestionType.Text, false, 4, null);
        var replacement = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "replacement", "Replacement question", SignupQuestionType.Text, false, 5, null);
        var main = new OsrsCharacter(Guid.NewGuid(), "Deletion Main", $"DELETION MAIN {Guid.NewGuid():N}", now);
        var alt = new OsrsCharacter(Guid.NewGuid(), "Deletion Optional", $"DELETION OPTIONAL {Guid.NewGuid():N}", now);
        var altLink = new AccountOsrsCharacter(Guid.NewGuid(), admin.Id, alt.Id, admin.Id, false, 1, "private label", 7m, now);
        db.AddRange(admin, borrower, bingoEvent, form, primary, optional, answered, unanswered, original, main, alt, altLink,
            new AccountOsrsCharacter(Guid.NewGuid(), admin.Id, main.Id, admin.Id, true, 0, null, 12m, now),
            new AccountOsrsCharacter(Guid.NewGuid(), borrower.Id, alt.Id, borrower.Id, true, 0, null, 7m, now));
        await db.SaveChangesAsync();
        var service = new SignupService(db, new SecretHasher(), TimeProvider.System);
        var signedUp = await service.SignUpAuthenticatedAsync(new(bingoEvent.Id, admin.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [primary.Id] = new(main.Id, 12m), [optional.Id] = new(alt.Id, role == EventCharacterRole.Playing ? 7m : null) },
            new Dictionary<Guid, string> { [answered.Id] = "erase this answer", [original.Id] = "keep this history" }, null));
        Assert.True(signedUp.Succeeded, signedUp.Error);
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == signedUp.ParticipantId);
        var sequence = participant.SignupSequence;
        var signedUpAt = participant.SignedUpAt;
        Assert.False((await service.DeleteQuestionAsync(bingoEvent.Id, answered.Id, admin.Id, admin.LoginName)).Succeeded);
        bingoEvent.CloseSignups(now);
        original.Deactivate(admin.Id, now, "structural replacement");
        original.ReplaceWith(replacement.Id);
        db.Add(replacement);
        await db.SaveChangesAsync();
        Assert.False((await service.DeleteQuestionAsync(bingoEvent.Id, answered.Id, borrower.Id, borrower.LoginName)).Succeeded);
        Assert.False((await service.DeleteQuestionAsync(bingoEvent.Id, primary.Id, admin.Id, admin.LoginName)).Succeeded);
        Assert.False((await service.DeleteQuestionAsync(bingoEvent.Id, original.Id, admin.Id, admin.LoginName)).Succeeded);
        var version = participant.ResponseVersion;
        var formVersion = form.Version;
        await using (var failing = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnParticipantAudit("signup_question.deleted")).Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new SignupService(failing, new SecretHasher(), TimeProvider.System).DeleteQuestionAsync(bingoEvent.Id, optional.Id, admin.Id, admin.LoginName));
        Assert.True(await db.SignupQuestions.AsNoTracking().Where(x => x.Id == optional.Id).Select(x => x.Active).SingleAsync());
        Assert.True(await db.EventParticipantCharacters.AnyAsync(x => x.SignupQuestionId == optional.Id && x.ReleasedAt == null));

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetStringAsync("/Account/Login");
        using (var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = admin.LoginName, ["Input.Password"] = "delete-password", ["__RequestVerificationToken"] = AntiforgeryToken(page) }))) Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var route = $"/Admin/Events/Questions/{bingoEvent.Id}";
        foreach (var question in new[] { optional, answered, unanswered })
        {
            page = await client.GetStringAsync(route);
            var removalForm = Regex.Match(page, $"<form(?=[^>]*action=\"[^\"]*handler=Deactivate[^\"]*\")[^>]*>(?:(?!</form>)[\\s\\S])*?value=\"{question.Id}\"(?:(?!</form>)[\\s\\S])*?</form>").Value;
            Assert.NotEmpty(removalForm);
            var action = WebUtility.HtmlDecode(Regex.Match(removalForm, "action=\"([^\"]*)\"").Groups[1].Value);
            using var removed = await client.PostAsync(action, new FormUrlEncodedContent(new Dictionary<string, string> { ["questionId"] = question.Id.ToString(), ["__RequestVerificationToken"] = AntiforgeryToken(removalForm) }));
            Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        }
        db.ChangeTracker.Clear();
        var removedIds = new[] { optional.Id, answered.Id, unanswered.Id };
        Assert.False(await db.SignupAnswers.AnyAsync(x => removedIds.Contains(x.SignupQuestionId)));
        Assert.All(await db.SignupQuestions.Where(x => removedIds.Contains(x.Id)).ToListAsync(), x => Assert.Equal(SignupQuestion.DeletedReason, x.DisabledReason));
        var history = await db.EventParticipantCharacters.SingleAsync(x => x.SignupQuestionId == optional.Id);
        Assert.NotNull(history.ReleasedAt);
        Assert.Equal(admin.Id, history.ReleasedByAccountId);
        var retainedLink = await db.AccountOsrsCharacters.SingleAsync(x => x.Id == altLink.Id);
        Assert.True(retainedLink.Active);
        Assert.Equal(7m, retainedLink.SavedEhb);
        Assert.Equal("private label", retainedLink.PersonalLabel);
        participant = await db.EventParticipants.SingleAsync(x => x.Id == signedUp.ParticipantId);
        Assert.Equal(sequence, participant.SignupSequence);
        Assert.Equal(signedUpAt.AddTicks(-(signedUpAt.Ticks % TimeSpan.TicksPerMicrosecond)), participant.SignedUpAt);
        Assert.Equal(SignupStatus.Confirmed, participant.SignupStatus);
        Assert.Equal(version + 2, participant.ResponseVersion);
        Assert.Equal(formVersion + 3, await db.SignupForms.Where(x => x.Id == form.Id).Select(x => x.Version).SingleAsync());
        Assert.Equal(3, await db.AuditEntries.CountAsync(x => x.EventId == bingoEvent.Id && x.Action == "signup_question.deleted"));
        Assert.True((await service.DeleteQuestionAsync(bingoEvent.Id, optional.Id, admin.Id, admin.LoginName)).Succeeded);
        Assert.Equal(3, await db.AuditEntries.CountAsync(x => x.EventId == bingoEvent.Id && x.Action == "signup_question.deleted"));
        foreach (var view in new[] { route, $"/Events/{bingoEvent.Slug}/Signups", $"/Events/{bingoEvent.Slug}/Signup/Confirmation?participantId={participant.Id}", $"/Admin/Events/Participant/{bingoEvent.Id}/Participants/{participant.Id}" })
        {
            page = await client.GetStringAsync(view);
            foreach (var question in new[] { optional, answered, unanswered }) Assert.DoesNotContain(question.Label, page);
            Assert.DoesNotContain("erase this answer", page);
            if (view.Contains("Confirmation", StringComparison.Ordinal) || view.EndsWith("Signups", StringComparison.Ordinal)) Assert.Contains("keep this history", page);
        }
        bingoEvent = await db.Events.SingleAsync(x => x.Id == bingoEvent.Id);
        bingoEvent.OpenSignups(now);
        await db.SaveChangesAsync();
        page = await client.GetStringAsync($"/Events/{bingoEvent.Slug}/Signup?edit=true");
        foreach (var question in new[] { optional, answered, unanswered }) Assert.DoesNotContain(question.Label, page);
        var borrowed = await service.SignUpAuthenticatedAsync(new(bingoEvent.Id, borrower.Id, new Dictionary<Guid, AuthenticatedAccountAnswer> { [primary.Id] = new(alt.Id, 7m) }, new Dictionary<Guid, string>(), null));
        Assert.True(borrowed.Succeeded, borrowed.Error);
        Assert.True((await service.WithdrawAsync(bingoEvent.Id, participant.Id, admin.Id, admin.LoginName, false)).Succeeded);
        if (role == EventCharacterRole.Playing)
        {
            bingoEvent.CloseSignups(now);
            await db.SaveChangesAsync();
            Assert.True((await service.RestoreAsync(bingoEvent.Id, participant.Id, admin.Id, admin.LoginName)).Succeeded);
        }
        else Assert.True((await service.RejoinAsync(bingoEvent.Id, participant.Id, admin.Id, admin.LoginName)).Succeeded);
        Assert.Equal(new[] { main.Id }, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).Select(x => x.OsrsCharacterId).ToListAsync());
        if (bingoEvent.State == EventState.SignupOpen) bingoEvent.CloseSignups(now);
        bingoEvent.SetDraftLocked(true);
        await db.SaveChangesAsync();
        Assert.False((await service.DeleteQuestionAsync(bingoEvent.Id, replacement.Id, admin.Id, admin.LoginName)).Succeeded);
        Assert.True(await db.SignupQuestions.AsNoTracking().Where(x => x.Id == replacement.Id).Select(x => x.Active).SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictingAccountMarksItsRenderedAnswerAndRetainsAtomicRetry(bool edit)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"conflict-owner-{Guid.NewGuid():N}", now);
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "conflict-password"), false, now, incrementVersion: false);
        var other = Website($"private-owner-{Guid.NewGuid():N}", now);
        var bingoEvent = Event(owner.Id, now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var alt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "alt", "Alt account", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Informational);
        var custom = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "answer", "Your answer", SignupQuestionType.Text, false, 2, null);
        var main = new OsrsCharacter(Guid.NewGuid(), "Conflict Main", $"CONFLICT MAIN {Guid.NewGuid():N}", now);
        var reserved = new OsrsCharacter(Guid.NewGuid(), "Conflict Alt", $"CONFLICT ALT {Guid.NewGuid():N}", now);
        var available = new OsrsCharacter(Guid.NewGuid(), "Available Alt", $"AVAILABLE ALT {Guid.NewGuid():N}", now);
        var mainLink = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, main.Id, owner.Id, true, 0, null, 18m, now);
        db.AddRange(owner, other, bingoEvent, form, primary, alt, custom, main, reserved, available, mainLink,
            new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, reserved.Id, owner.Id, false, 1, null, null, now),
            new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, available.Id, owner.Id, false, 2, null, null, now));
        var otherParticipant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        otherParticipant.AssignOwner(other);
        db.AddRange(otherParticipant, new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, otherParticipant.Id, reserved.Id, 0, now, other.Id, primary.Id, EventCharacterRole.Playing, 8m, EhbSource.Manual, null));
        await db.SaveChangesAsync();
        var service = new SignupService(db, new SecretHasher(), TimeProvider.System);
        Guid? existingId = null;
        if (edit)
        {
            var created = await service.SignUpAuthenticatedAsync(new(bingoEvent.Id, owner.Id,
                new Dictionary<Guid, AuthenticatedAccountAnswer> { [primary.Id] = new(main.Id, 18m), [alt.Id] = new(available.Id, null) },
                new Dictionary<Guid, string> { [custom.Id] = "original answer" }, null));
            Assert.True(created.Succeeded, created.Error);
            existingId = created.ParticipantId;
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetStringAsync("/Account/Login");
        using (var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = owner.LoginName, ["Input.Password"] = "conflict-password", ["__RequestVerificationToken"] = AntiforgeryToken(page) }))) Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var route = $"/Events/{bingoEvent.Slug}/Signup";
        if (edit)
        {
            using var entry = await client.GetAsync(route);
            page = await client.GetStringAsync(entry.Headers.Location!);
            route = WebUtility.HtmlDecode(Regex.Match(page, "href=\"([^\"]*edit=true)\"").Groups[1].Value);
            Assert.NotEmpty(route);
        }
        page = await client.GetStringAsync(route);
        var version = HiddenValue(page, "Input_ExpectedResponseVersion");
        foreach (var conflictId in new[] { main.Id, reserved.Id })
        {
            using var rejected = await client.PostAsync(route, Post(page, conflictId));
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            page = await rejected.Content.ReadAsStringAsync();
            var fieldError = Regex.Match(page, $"<span[^>]*data-valmsg-for=\"Input.AccountAnswers\\[{alt.Id}\\].OsrsCharacterId\"[^>]*>[\\s\\S]*?</span>").Value;
            Assert.Contains("field-validation-error", fieldError);
            Assert.Contains(conflictId == main.Id ? "Choose each account only once." : "Choose another account.", fieldError);
            Assert.Contains("checked=\"checked\"", InputTag(page, $"question-{primary.Id}-{main.Id}"));
            Assert.Contains("checked=\"checked\"", InputTag(page, $"question-{alt.Id}-{conflictId}"));
            Assert.Equal("27.5", HiddenValue(page, $"ehb-{primary.Id}"));
            Assert.Contains("retained custom answer", page);
            Assert.Equal(version, HiddenValue(page, "Input_ExpectedResponseVersion"));
            Assert.DoesNotContain(other.LoginName, page);
            db.ChangeTracker.Clear();
            Assert.Equal(18m, await db.AccountOsrsCharacters.Where(x => x.Id == mainLink.Id).Select(x => x.SavedEhb).SingleAsync());
            if (edit)
            {
                Assert.Equal(1, await db.EventParticipants.Where(x => x.Id == existingId).Select(x => x.ResponseVersion).SingleAsync());
                Assert.Equal("original answer", await db.SignupAnswers.Where(x => x.EventParticipantId == existingId && x.SignupQuestionId == custom.Id).Select(x => x.Value).SingleAsync());
                Assert.Equal(new[] { main.Id, available.Id }.Order(), (await db.EventParticipantCharacters.Where(x => x.EventParticipantId == existingId && x.ReleasedAt == null).Select(x => x.OsrsCharacterId).ToListAsync()).Order());
            }
            else Assert.False(await db.EventParticipants.AnyAsync(x => x.EventId == bingoEvent.Id && x.AccountId == owner.Id));
        }
        using var corrected = await client.PostAsync(route, Post(page, available.Id));
        Assert.Equal(HttpStatusCode.Redirect, corrected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(corrected.Headers.Location!)).StatusCode);
        db.ChangeTracker.Clear();
        var savedId = await db.EventParticipants.Where(x => x.EventId == bingoEvent.Id && x.AccountId == owner.Id).Select(x => x.Id).SingleAsync();
        Assert.Equal(27.5m, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == savedId && x.SignupQuestionId == primary.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
        Assert.Equal(available.Id, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == savedId && x.SignupQuestionId == alt.Id && x.ReleasedAt == null).Select(x => x.OsrsCharacterId).SingleAsync());
        Assert.Equal("retained custom answer", await db.SignupAnswers.Where(x => x.EventParticipantId == savedId && x.SignupQuestionId == custom.Id).Select(x => x.Value).SingleAsync());

        FormUrlEncodedContent Post(string html, Guid altId) => new(new Dictionary<string, string>
        {
            [$"Input.AccountAnswers[{primary.Id}].OsrsCharacterId"] = main.Id.ToString(),
            [$"Input.AccountAnswers[{primary.Id}].Ehb"] = "27.5",
            [$"Input.AccountAnswers[{alt.Id}].OsrsCharacterId"] = altId.ToString(),
            [$"Input.Answers[{custom.Id}]"] = "retained custom answer",
            ["Input.ExpectedResponseVersion"] = HiddenValue(html, "Input_ExpectedResponseVersion"),
            ["__RequestVerificationToken"] = AntiforgeryToken(html)
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestorationUsesOnlyAssignmentsReleasedAtWithdrawal(bool adminRestore)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"restore-owner-{Guid.NewGuid():N}", now);
        var borrower = Website($"restore-borrower-{Guid.NewGuid():N}", now);
        var bingoEvent = Event(owner.Id, now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var alt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "alt", "Alt", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Informational);
        var main = new OsrsCharacter(Guid.NewGuid(), "Restore main", $"RESTORE MAIN {Guid.NewGuid():N}", now);
        var alternate = new OsrsCharacter(Guid.NewGuid(), "Removed alt", $"REMOVED ALT {Guid.NewGuid():N}", now);
        db.AddRange(owner, borrower, bingoEvent, form, primary, alt, main, alternate,
            new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, main.Id, owner.Id, true, 0, null, 18m, now),
            new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, alternate.Id, owner.Id, false, 1, null, null, now),
            new AccountOsrsCharacter(Guid.NewGuid(), borrower.Id, alternate.Id, borrower.Id, true, 0, null, 9m, now));
        await db.SaveChangesAsync();
        var service = new SignupService(db, new SecretHasher(), new FixedSignupTimeProvider(now));
        var selected = new Dictionary<Guid, AuthenticatedAccountAnswer> { [primary.Id] = new(main.Id, 18m), [alt.Id] = new(alternate.Id, null) };
        var created = await service.SignUpAuthenticatedAsync(new(bingoEvent.Id, owner.Id, selected, new Dictionary<Guid, string>(), null));
        Assert.True(created.Succeeded, created.Error);
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == created.ParticipantId);
        selected.Remove(alt.Id);
        var edited = await service.SignUpAuthenticatedAsync(new(bingoEvent.Id, owner.Id, selected, new Dictionary<Guid, string>(), null, participant.ResponseVersion));
        Assert.True(edited.Succeeded, edited.Error);
        var removed = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == alt.Id);
        var removedAt = removed.ReleasedAt;
        // Legacy/imported assignments have no question; restoration must retain each of them.
        var retainedIds = new List<Guid> { main.Id };
        if (adminRestore)
        {
            // The retained migration populated the primary assignment without an account answer.
            db.SignupAnswers.Remove(await db.SignupAnswers.SingleAsync(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == primary.Id));
            for (var order = 2; order < 4; order++)
            {
                var retained = new OsrsCharacter(Guid.NewGuid(), $"Retained {order}", $"RETAINED {Guid.NewGuid():N}", now);
                retainedIds.Add(retained.Id);
                db.AddRange(retained, new EventParticipantCharacter(Guid.NewGuid(), bingoEvent.Id, participant.Id, retained.Id, order, now, owner.Id, null, EventCharacterRole.Playing, 12m, EhbSource.Manual, null));
            }
            await db.SaveChangesAsync();
        }
        Assert.True((await service.WithdrawAsync(bingoEvent.Id, participant.Id, owner.Id, owner.LoginName, false)).Succeeded);
        var borrowed = await service.SignUpAuthenticatedAsync(new(bingoEvent.Id, borrower.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [primary.Id] = new(alternate.Id, 9m) }, new Dictionary<Guid, string>(), null));
        Assert.True(borrowed.Succeeded, borrowed.Error);
        var restored = adminRestore
            ? await service.RestoreAsync(bingoEvent.Id, participant.Id, owner.Id, owner.LoginName)
            : await service.RejoinAsync(bingoEvent.Id, participant.Id, owner.Id, owner.LoginName);
        Assert.True(restored.Succeeded, restored.Error);
        Assert.Equal(retainedIds.Order(), (await db.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).Select(x => x.OsrsCharacterId).ToListAsync()).Order());
        Assert.Equal(removedAt, removed.ReleasedAt);
        Assert.Equal(borrowed.ParticipantId, await db.EventParticipantCharacters.Where(x => x.OsrsCharacterId == alternate.Id && x.ReleasedAt == null).Select(x => x.EventParticipantId).SingleAsync());
        Assert.Equal(3, participant.SignupSequence);
    }

    [Theory]
    [InlineData("-1", "kept answer")]
    [InlineData("27.5", "")]
    public async Task UnlinkedRegisteredAccountSurvivesInvalidRenderedEdit(string invalidEhb, string invalidAnswer)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"retry-owner-{Guid.NewGuid():N}", now);
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "retry-password"), false, now, incrementVersion: false);
        var bingoEvent = Event(owner.Id, now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var answer = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "answer", "Required answer", SignupQuestionType.Text, true, 1, null);
        var main = new OsrsCharacter(Guid.NewGuid(), "Retained main", $"RETAINED MAIN {Guid.NewGuid():N}", now);
        var link = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, main.Id, owner.Id, true, 0, null, 18m, now);
        db.AddRange(owner, bingoEvent, form, primary, answer, main, link);
        await db.SaveChangesAsync();
        Assert.True((await new SignupService(db, new SecretHasher(), TimeProvider.System).SignUpAuthenticatedAsync(new(bingoEvent.Id, owner.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [primary.Id] = new(main.Id, 18m) }, new Dictionary<Guid, string> { [answer.Id] = "original" }, null))).Succeeded);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetStringAsync("/Account/Login");
        using (var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = owner.LoginName, ["Input.Password"] = "retry-password", ["__RequestVerificationToken"] = AntiforgeryToken(page) }))) Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var entry = await client.GetAsync($"/Events/{bingoEvent.Slug}/Signup");
        page = await client.GetStringAsync(entry.Headers.Location!);
        var editRoute = WebUtility.HtmlDecode(Regex.Match(page, "href=\"([^\"]*edit=true)\"").Groups[1].Value);
        Assert.NotEmpty(editRoute);
        page = await client.GetStringAsync(editRoute);
        var accountsRoute = WebUtility.HtmlDecode(Regex.Match(page, "href=\"([^\"]*MyAccounts[^\"]*)\"").Groups[1].Value);
        Assert.NotEmpty(accountsRoute);
        page = await client.GetStringAsync(accountsRoute);
        var unlinkForm = Regex.Match(page, "<form[^>]*class=\"public-pass2-unlink-form\"[\\s\\S]*?</form>").Value;
        Assert.NotEmpty(unlinkForm);
        var unlinkRoute = WebUtility.HtmlDecode(Regex.Match(unlinkForm, "action=\"([^\"]+)\"").Groups[1].Value);
        using (var unlink = await client.PostAsync(unlinkRoute, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Unlink.LinkId"] = link.Id.ToString(),
            ["Unlink.ConfirmRegistrationWarning"] = "true",
            ["ReturnUrl"] = HiddenValue(unlinkForm, "ReturnUrl"),
            ["__RequestVerificationToken"] = AntiforgeryToken(unlinkForm)
        }))) Assert.Equal(HttpStatusCode.Redirect, unlink.StatusCode);
        page = await client.GetStringAsync(editRoute);
        Assert.Contains("checked=\"checked\"", InputTag(page, $"question-{primary.Id}-{main.Id}"));
        var version = HiddenValue(page, "Input_ExpectedResponseVersion");
        using var invalid = await client.PostAsync(editRoute, Post(page, invalidEhb, invalidAnswer));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        page = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("checked=\"checked\"", InputTag(page, $"question-{primary.Id}-{main.Id}"));
        Assert.Contains("validation-summary-errors", page);
        Assert.Equal(version, HiddenValue(page, "Input_ExpectedResponseVersion"));
        Assert.Contains($"value=\"{invalidEhb}\"", page);
        if (invalidAnswer.Length > 0) Assert.Contains(invalidAnswer, page);
        using var corrected = await client.PostAsync(editRoute, Post(page, "27.5", "corrected answer"));
        Assert.Equal(HttpStatusCode.Redirect, corrected.StatusCode);
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.AccountOsrsCharacters.SingleAsync(x => x.Id == link.Id)).UnlinkedAt);
        var saved = await db.EventParticipantCharacters.SingleAsync(x => x.EventId == bingoEvent.Id && x.ReleasedAt == null);
        Assert.Equal(main.Id, saved.OsrsCharacterId);
        Assert.Equal(27.5m, saved.EhbSnapshot);
        Assert.Equal("corrected answer", await db.SignupAnswers.Where(x => x.SignupQuestionId == answer.Id).Select(x => x.Value).SingleAsync());

        FormUrlEncodedContent Post(string html, string ehb, string value) => new(new Dictionary<string, string>
        {
            [$"Input.AccountAnswers[{primary.Id}].OsrsCharacterId"] = main.Id.ToString(),
            [$"Input.AccountAnswers[{primary.Id}].Ehb"] = ehb,
            [$"Input.Answers[{answer.Id}]"] = value,
            ["Input.ExpectedResponseVersion"] = HiddenValue(html, "Input_ExpectedResponseVersion"),
            ["__RequestVerificationToken"] = AntiforgeryToken(html)
        });
    }

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
        Assert.Equal(originalSignedUpAt.AddTicks(-(originalSignedUpAt.Ticks % TimeSpan.TicksPerMicrosecond)), saved.SignedUpAt);
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
    public async Task FreshWiseOldManSignupTokenIsTheOnlyTrustedSignupProvenance()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var owner = Website($"wom-provenance-{Guid.NewGuid():N}", now);
        var bingoEvent = Event(owner.Id, now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var character = new OsrsCharacter(Guid.NewGuid(), "Trusted Main", $"TRUSTED MAIN {Guid.NewGuid():N}", now);
        var link = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 10m, now);
        db.AddRange(owner, bingoEvent, form, regular, character, link);
        await db.SaveChangesAsync();

        var tokens = new SignupLookupTokenService(new EphemeralDataProtectionProvider());
        var service = new SignupService(db, new SecretHasher(), TimeProvider.System, tokens);
        var validToken = tokens.Create(character.NormalizedName, 22m, now.AddSeconds(-1), now, now.AddMinutes(5));
        var created = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(
            bingoEvent.Id, owner.Id,
            new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(character.Id, 22m, validToken) },
            new Dictionary<Guid, string>(), null));
        Assert.True(created.Succeeded);
        Assert.Equal(EhbSource.WiseOldMan, await db.EventParticipantCharacters.Where(x => x.EventParticipantId == created.ParticipantId).Select(x => x.EhbSource).SingleAsync());
        Assert.NotNull(await db.EventParticipantCharacters.Where(x => x.EventParticipantId == created.ParticipantId).Select(x => x.EhbFetchedAt).SingleAsync());

        async Task AssertManualAsync(decimal ehb, string? token)
        {
            db.ChangeTracker.Clear();
            var version = await db.EventParticipants.Where(x => x.Id == created.ParticipantId).Select(x => x.ResponseVersion).SingleAsync();
            var result = await service.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(
                bingoEvent.Id, owner.Id,
                new Dictionary<Guid, AuthenticatedAccountAnswer> { [regular.Id] = new(character.Id, ehb, token) },
                new Dictionary<Guid, string>(), null, version));
            Assert.True(result.Succeeded);
            db.ChangeTracker.Clear();
            var assignment = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == created.ParticipantId && x.ReleasedAt == null);
            Assert.Equal(EhbSource.Manual, assignment.EhbSource);
            Assert.Null(assignment.EhbFetchedAt);
        }

        await AssertManualAsync(23m, validToken);
        await AssertManualAsync(24m, null);
        await AssertManualAsync(25m, tokens.Create(character.NormalizedName, 25m, now.AddMinutes(-6), now.AddMinutes(-6), now.AddMinutes(-1)));
        await AssertManualAsync(26m, tokens.Create("OTHER NAME", 26m, now.AddSeconds(-1), now, now.AddMinutes(5)));
    }

    [Theory]
    [InlineData("en", "Create your website account", "17.50")]
    [InlineData("da", "Opret din webkonto", "17,50")]
    public async Task OnboardingFetchRenderAndPostbackRemainCultureConsistent(string culture, string heading, string displayedEhb)
    {
        var fake = new FakeWiseOldManPlayerLookup();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services =>
            {
                services.RemoveAll<IWiseOldManPlayerLookup>();
                services.AddSingleton<IWiseOldManPlayerLookup>(fake);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);

        var discordUserId = $"onboarding-culture-{culture}-{Guid.NewGuid():N}";
        var characterName = $"Bilingual Character {culture}";
        var username = $"onboarding-{culture}-{Guid.NewGuid():N}";
        var onboardingState = factory.Services.GetRequiredService<DiscordOnboardingStateService>();
        var issue = new DefaultHttpContext();
        onboardingState.Issue(issue.Response, discordUserId, "Bilingual onboarding");
        client.DefaultRequestHeaders.Add("Cookie", issue.Response.Headers.SetCookie.Single()!.Split(';')[0]);

        var onboarding = await client.GetStringAsync("/Account/Onboarding");
        Assert.Contains(heading, onboarding, StringComparison.Ordinal);
        using var fetched = await client.PostAsync("/Account/Onboarding?handler=Fetch", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.OsrsCharacterName"] = characterName,
            ["__RequestVerificationToken"] = AntiforgeryToken(onboarding)
        }));
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var fetchedPage = await fetched.Content.ReadAsStringAsync();
        Assert.Equal(1, fake.Calls);
        Assert.Contains(heading, fetchedPage, StringComparison.Ordinal);
        var onboardingEhbInput = Regex.Match(fetchedPage, "<input(?=[^>]*data-onboarding-ehb)[^>]*>");
        Assert.True(onboardingEhbInput.Success);
        Assert.Contains("type=\"text\"", onboardingEhbInput.Value, StringComparison.Ordinal);
        Assert.Contains("inputmode=\"decimal\"", onboardingEhbInput.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"number\"", onboardingEhbInput.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("data-val-number", onboardingEhbInput.Value, StringComparison.Ordinal);
        var renderedEhb = Regex.Match(onboardingEhbInput.Value, "value=\"([^\"]*)\"").Groups[1].Value;
        Assert.Equal(displayedEhb, renderedEhb);

        using var completed = await client.PostAsync("/Account/Onboarding", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.OsrsCharacterName"] = characterName,
            ["Input.SavedEhb"] = displayedEhb,
            ["Input.Password"] = "long-bilingual-password",
            ["Input.ConfirmPassword"] = "long-bilingual-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(fetchedPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);

        await using var db = new ApplicationDbContext(options);
        var account = await db.Accounts.SingleAsync(item => item.DiscordUserId == discordUserId);
        Assert.Equal(17.5m, await db.AccountOsrsCharacters.Where(item => item.AccountId == account.Id).Select(item => item.SavedEhb).SingleAsync());
    }

    [Theory]
    [InlineData("en", "17.50", "17.5", "3000.10", "17.5", "18.75")]
    [InlineData("da", "17,50", "17.5", "3000,10", "17,5", "18,75")]
    public async Task RenderedMyAccountsAndSignupFetchesUseLocalizedEditingWithInvariantMachineTransport(string culture, string myAccountsFetchedDisplay, string machineEhb, string addFetchedDisplay, string signupFetchedDisplay, string editSavedDisplay)
    {
        var now = DateTimeOffset.UtcNow;
        Guid eventId;
        Guid regularId;
        Guid characterId;
        Guid linkId;
        Guid accountId;
        string loginName;
        await using (var db = new ApplicationDbContext(options))
        {
            var owner = Website($"wom-route-{Guid.NewGuid():N}", now);
            owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "wom-route-password"), false, now, incrementVersion: false);
            var bingoEvent = Event(owner.Id, now);
            var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
            var regular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var character = new OsrsCharacter(Guid.NewGuid(), "Route WoM Main", $"ROUTE WOM MAIN {Guid.NewGuid():N}", now);
            var link = new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 10m, now);
            db.AddRange(owner, bingoEvent, form, regular, character, link);
            await db.SaveChangesAsync();
            eventId = bingoEvent.Id; regularId = regular.Id; characterId = character.Id; linkId = link.Id; accountId = owner.Id; loginName = owner.LoginName;
        }

        var fake = new FakeWiseOldManPlayerLookup();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services =>
            {
                services.RemoveAll<IWiseOldManPlayerLookup>();
                services.AddSingleton<IWiseOldManPlayerLookup>(fake);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        var login = await client.GetStringAsync("/Account/Login");
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = loginName,
            ["Input.Password"] = "wom-route-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var myAccounts = await client.GetStringAsync("/Account/MyAccounts");
        Assert.Equal(0, fake.Calls);
        var addEhbInput = Regex.Match(myAccounts, "<input(?=[^>]*\\bid=\"Add_SavedEhb\")[^>]*>");
        Assert.True(addEhbInput.Success);
        Assert.Contains("inputmode=\"decimal\"", addEhbInput.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"number\"", addEhbInput.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("data-val-number", addEhbInput.Value, StringComparison.Ordinal);
        using var fetchedDefault = await client.PostAsync("/Account/MyAccounts?handler=Fetch", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Fetch.LinkId"] = linkId.ToString(),
            ["Edit.LinkId"] = linkId.ToString(),
            ["Edit.CharacterName"] = "Route WoM Main",
            ["Edit.PersonalLabel"] = string.Empty,
            ["Edit.SavedEhb"] = "10",
            ["__RequestVerificationToken"] = AntiforgeryToken(myAccounts)
        }));
        Assert.Equal(HttpStatusCode.OK, fetchedDefault.StatusCode);
        Assert.Equal(1, fake.Calls);
        var fetchedDefaultPage = await fetchedDefault.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{myAccountsFetchedDisplay}\"", fetchedDefaultPage, StringComparison.Ordinal);
        await using (var verify = new ApplicationDbContext(options)) Assert.Equal(10m, await verify.AccountOsrsCharacters.Where(x => x.Id == linkId).Select(x => x.SavedEhb).SingleAsync());
        using var updatedMyAccount = await client.PostAsync("/Account/MyAccounts?handler=Update", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Edit.LinkId"] = linkId.ToString(),
            ["Edit.CharacterName"] = "Route WoM Main",
            ["Edit.PersonalLabel"] = string.Empty,
            ["Edit.SavedEhb"] = editSavedDisplay,
            ["__RequestVerificationToken"] = AntiforgeryToken(fetchedDefaultPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, updatedMyAccount.StatusCode);
        await using (var verify = new ApplicationDbContext(options))
        {
            var currentLink = await verify.AccountOsrsCharacters
                .Where(x => x.Id == linkId)
                .Select(x => new { x.OsrsCharacterId, x.SavedEhb })
                .SingleAsync();
            Assert.Equal(18.75m, currentLink.SavedEhb);
            characterId = currentLink.OsrsCharacterId;
        }

        var slug = await EventSlugAsync(eventId);
        var signup = await client.GetStringAsync($"/Events/{slug}/Signup");
        Assert.Equal(1, fake.Calls);
        var fetchPost = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [$"Input.AccountAnswers[{regularId}].OsrsCharacterId"] = characterId.ToString(),
            [$"Input.AccountAnswers[{regularId}].Ehb"] = machineEhb,
            ["Input.FetchQuestionId"] = regularId.ToString(),
            ["__RequestVerificationToken"] = AntiforgeryToken(signup)
        });
        using var fetchedSignup = await client.PostAsync($"/Events/{slug}/Signup", fetchPost);
        Assert.Equal(HttpStatusCode.OK, fetchedSignup.StatusCode);
        var fetchedPage = await fetchedSignup.Content.ReadAsStringAsync();
        Assert.Equal(2, fake.Calls);
        Assert.Contains($"value=\"{machineEhb}\"", fetchedPage, StringComparison.Ordinal);
        Assert.Contains(signupFetchedDisplay, fetchedPage, StringComparison.Ordinal);
        var tokenMatch = Regex.Match(fetchedPage, $"name=\"Input.AccountAnswers\\[{Regex.Escape(regularId.ToString())}\\]\\.WiseOldManLookupToken\" value=\"([^\"]*)\"");
        Assert.True(tokenMatch.Success);
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(17.5m, await verify.AccountOsrsCharacters.Where(x => x.Id == linkId).Select(x => x.SavedEhb).SingleAsync());
            Assert.Empty(await verify.EventParticipants.Where(x => x.EventId == eventId).ToListAsync());
            Assert.Empty(await verify.EventParticipantCharacters.Where(x => x.EventId == eventId).ToListAsync());
        }
        var normalSave = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [$"Input.AccountAnswers[{regularId}].OsrsCharacterId"] = characterId.ToString(),
            [$"Input.AccountAnswers[{regularId}].Ehb"] = machineEhb,
            [$"Input.AccountAnswers[{regularId}].WiseOldManLookupToken"] = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value),
            ["__RequestVerificationToken"] = AntiforgeryToken(fetchedPage)
        });
        using var saved = await client.PostAsync($"/Events/{slug}/Signup", normalSave);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(2, fake.Calls);
        await using (var verify = new ApplicationDbContext(options)) Assert.Equal(EhbSource.WiseOldMan, await verify.EventParticipantCharacters.Where(x => x.EventId == eventId && x.ReleasedAt == null).Select(x => x.EhbSource).SingleAsync());

        var addPage = await client.GetStringAsync("/Account/MyAccounts");
        var beforeAddLinkCount = await CountActiveLinksAsync(accountId);
        using var fetchedAdd = await client.PostAsync("/Account/MyAccounts?handler=FetchAdd", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Add.CharacterName"] = "Route WoM Add",
            ["Add.SavedEhb"] = culture == "da" ? "12,50" : "12.50",
            ["__RequestVerificationToken"] = AntiforgeryToken(addPage)
        }));
        Assert.Equal(HttpStatusCode.OK, fetchedAdd.StatusCode);
        var fetchedAddPage = await fetchedAdd.Content.ReadAsStringAsync();
        Assert.Equal(3, fake.Calls);
        Assert.Contains("Route WoM Add", fetchedAddPage, StringComparison.Ordinal);
        Assert.Contains($"value=\"{addFetchedDisplay}\"", fetchedAddPage, StringComparison.Ordinal);
        Assert.Equal(beforeAddLinkCount, await CountActiveLinksAsync(accountId));
        using var savedFetchedAdd = await client.PostAsync("/Account/MyAccounts?handler=Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Add.CharacterName"] = "Route WoM Add",
            ["Add.SavedEhb"] = addFetchedDisplay,
            ["__RequestVerificationToken"] = AntiforgeryToken(fetchedAddPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, savedFetchedAdd.StatusCode);
        await using (var verify = new ApplicationDbContext(options)) Assert.Equal(3000.10m, await verify.AccountOsrsCharacters.Where(x => x.AccountId == accountId && x.Active && x.SavedEhb == 3000.10m).Select(x => x.SavedEhb).SingleAsync());

        async Task<string> EventSlugAsync(Guid id)
        {
            await using var lookup = new ApplicationDbContext(options);
            return await lookup.Events.Where(x => x.Id == id).Select(x => x.Slug).SingleAsync();
        }

        async Task<int> CountActiveLinksAsync(Guid ownerId)
        {
            await using var lookup = new ApplicationDbContext(options);
            return await lookup.AccountOsrsCharacters.CountAsync(x => x.AccountId == ownerId && x.Active);
        }
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
        Assert.Contains("data-saved-ehb", AccountControl(createPage, preferredCharacterId), StringComparison.Ordinal);
        Assert.Contains("checked", AccountControl(createPage, preferredCharacterId), StringComparison.Ordinal);
        Assert.Contains("data-saved-ehb=\"\"", AccountControl(createPage, alternateCharacterId), StringComparison.Ordinal);
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
        Assert.Contains("checked", AccountControl(invalidPage, alternateCharacterId), StringComparison.Ordinal);
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
        Guid firstAlternateCharacterId;
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
            var primary = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Main account", SignupQuestionType.Account, true, 2, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
            var optionalRegular = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "optional_regular", "Optional regular account", SignupQuestionType.Account, false, 0, null, SignupSystemField.None, EventCharacterRole.Playing);
            var optionalAlt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "optional_alt", "Optional alt account", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Informational);
            var text = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "comment", "Comment", SignupQuestionType.Text, true, 3, null, SignupSystemField.None);
            var firstCharacter = new OsrsCharacter(Guid.NewGuid(), "First rendered main", $"FIRST RENDERED MAIN {Guid.NewGuid():N}", now);
            var firstAlternateCharacter = new OsrsCharacter(Guid.NewGuid(), "First rendered secondary", $"FIRST RENDERED SECONDARY {Guid.NewGuid():N}", now);
            var secondCharacter = new OsrsCharacter(Guid.NewGuid(), "Second rendered main", $"SECOND RENDERED MAIN {Guid.NewGuid():N}", now);
            var firstLink = new AccountOsrsCharacter(Guid.NewGuid(), first.Id, firstCharacter.Id, first.Id, true, 0, null, 18m, now);
            var firstAlternateLink = new AccountOsrsCharacter(Guid.NewGuid(), first.Id, firstAlternateCharacter.Id, first.Id, false, 1, null, 7m, now);
            var secondLink = new AccountOsrsCharacter(Guid.NewGuid(), second.Id, secondCharacter.Id, second.Id, true, 0, null, 21m, now);
            db.AddRange(admin, first, second, bingoEvent, form, primary, optionalRegular, optionalAlt, text, firstCharacter, firstAlternateCharacter, secondCharacter, firstLink, firstAlternateLink, secondLink);
            await db.SaveChangesAsync();
            eventId = bingoEvent.Id;
            formId = form.Id;
            primaryQuestionId = primary.Id;
            optionalRegularQuestionId = optionalRegular.Id;
            optionalAltQuestionId = optionalAlt.Id;
            textQuestionId = text.Id;
            firstCharacterId = firstCharacter.Id;
            firstAlternateCharacterId = firstAlternateCharacter.Id;
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
        Assert.True(signupPage.IndexOf("<legend>Main account", StringComparison.Ordinal) < signupPage.IndexOf("<legend>Optional regular account", StringComparison.Ordinal));
        Assert.True(signupPage.IndexOf("<legend>Optional regular account", StringComparison.Ordinal) < signupPage.IndexOf("<legend>Optional alt account", StringComparison.Ordinal));
        Assert.Contains($"id=\"question-{optionalRegularQuestionId}-none\"", signupPage, StringComparison.Ordinal);
        Assert.Contains($"id=\"question-{optionalAltQuestionId}-none\"", signupPage, StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", InputTag(signupPage, $"question-{primaryQuestionId}-{firstCharacterId}"), StringComparison.Ordinal);
        Assert.DoesNotContain("checked=\"checked\"", InputTag(signupPage, $"question-{primaryQuestionId}-{firstAlternateCharacterId}"), StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", InputTag(signupPage, $"question-{optionalRegularQuestionId}-none"), StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", InputTag(signupPage, $"question-{optionalAltQuestionId}-none"), StringComparison.Ordinal);

        using (var missingCode = await firstClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, firstCharacterId, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, null)))
        {
            Assert.Equal(HttpStatusCode.OK, missingCode.StatusCode);
            var html = await missingCode.Content.ReadAsStringAsync();
            Assert.Contains("The event code is incorrect.", html, StringComparison.Ordinal);
            Assert.Contains($"value=\"{firstCharacterId}\"", html, StringComparison.Ordinal);
            Assert.Contains("A retained answer", html, StringComparison.Ordinal);
            Assert.Contains("checked=\"checked\"", InputTag(html, $"question-{optionalRegularQuestionId}-none"), StringComparison.Ordinal);
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
        using (var missingLaterRequired = await firstClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, firstCharacterId, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, signupCode, textAnswer: "")))
        {
            Assert.Equal(HttpStatusCode.OK, missingLaterRequired.StatusCode);
            var html = await missingLaterRequired.Content.ReadAsStringAsync();
            Assert.Contains("Comment", html, StringComparison.Ordinal);
            Assert.Contains("required", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"value=\"{firstCharacterId}\"", html, StringComparison.Ordinal);
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
        using (var duplicate = await firstClient.PostAsync($"/Events/{slug}/Signup", RenderedSignupPost(signupPage, primaryQuestionId, firstCharacterId, optionalRegularQuestionId, optionalAltQuestionId, textQuestionId, signupCode, firstCharacterId, "18")))
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

    [Theory]
    [InlineData("en")]
    [InlineData("da")]
    public async Task SelectedAltAndReleasedCharacterJourneyPreserveRoleAndWithdrawalBoundaries(string culture)
    {
        var now = DateTimeOffset.UtcNow;
        await using var seed = new ApplicationDbContext(options);
        var owner = Website($"journey-a-{Guid.NewGuid():N}", now);
        var borrower = Website($"journey-b-{Guid.NewGuid():N}", now);
        foreach (var account in new[] { owner, borrower })
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "journey-password"), false, now, incrementVersion: false);
        var bingoEvent = Event(owner.Id, now);
        var form = new SignupForm(Guid.NewGuid(), bingoEvent.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var alt = new SignupQuestion(Guid.NewGuid(), form.Id, bingoEvent.Id, "alt", "Support alt", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Informational);
        var character = new OsrsCharacter(Guid.NewGuid(), "Shared journey", $"SHARED JOURNEY {Guid.NewGuid():N}", now);
        var alternate = new OsrsCharacter(Guid.NewGuid(), "Journey alt", $"JOURNEY ALT {Guid.NewGuid():N}", now);
        seed.AddRange(owner, borrower, bingoEvent, form, primary, alt, character, alternate,
            new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 18.5m, now),
            new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, alternate.Id, owner.Id, false, 1, null, 73.25m, now),
            new AccountOsrsCharacter(Guid.NewGuid(), borrower.Id, character.Id, borrower.Id, true, 0, null, 9m, now));
        await seed.SaveChangesAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var a = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var b = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(a, owner.LoginName);
        await Login(b, borrower.LoginName);
        var route = $"/Events/{bingoEvent.Slug}/Signup";
        var page = await a.GetStringAsync(route);
        // A selected Alt with saved EHB must work, including obsolete metadata from a stale form.
        using (var created = await a.PostAsync(route, Post(page, true, "73.25")))
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        page = await a.GetStringAsync(route + "?edit=true");
        Assert.Contains("checked=\"checked\"", InputTag(page, $"question-{alt.Id}-{alternate.Id}"));
        var altFieldset = Regex.Match(page, "<fieldset[^>]*>\\s*<legend>Support alt[\\s\\S]*?</fieldset>").Value;
        Assert.NotEmpty(altFieldset);
        Assert.DoesNotContain("EHB", altFieldset);
        Assert.DoesNotContain("data-ehb", altFieldset);
        Assert.DoesNotContain("WiseOldMan", altFieldset);
        Assert.DoesNotContain("Input.FetchQuestionId", altFieldset);
        using (var edited = await a.PostAsync(route, Post(page, true, "obsolete-invalid-ehb")))
            Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        var participant = await verify.EventParticipants.AsNoTracking().SingleAsync(x => x.AccountId == owner.Id);
        var assignment = await verify.EventParticipantCharacters.AsNoTracking().SingleAsync(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == alt.Id && x.ReleasedAt == null);
        Assert.Equal(EventCharacterRole.Informational, assignment.EventRole);
        Assert.Null(assignment.EhbSnapshot); Assert.Null(assignment.EhbSource); Assert.Null(assignment.EhbFetchedAt);
        Assert.Equal(73.25m, await verify.AccountOsrsCharacters.Where(x => x.AccountId == owner.Id && x.OsrsCharacterId == alternate.Id).Select(x => x.SavedEhb).SingleAsync());
        // Save None before a separate re-add so the released order must remain reserved.
        page = await a.GetStringAsync(route + "?edit=true");
        using (var cleared = await a.PostAsync(route, Post(page, false, "")))
            Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        var releasedAlt = await verify.EventParticipantCharacters.AsNoTracking().SingleAsync(x => x.Id == assignment.Id);
        Assert.NotNull(releasedAlt.ReleasedAt);
        Assert.Equal(1, releasedAlt.RegistrationOrder);
        Assert.False(await verify.EventParticipantCharacters.AnyAsync(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == alt.Id && x.ReleasedAt == null));
        page = await a.GetStringAsync(route + "?edit=true");
        using (var readded = await a.PostAsync(route, Post(page, true, "")))
            Assert.Equal(HttpStatusCode.Redirect, readded.StatusCode);
        var assignments = await verify.EventParticipantCharacters.AsNoTracking().Where(x => x.EventParticipantId == participant.Id).OrderBy(x => x.RegistrationOrder).ToListAsync();
        Assert.Equal([0, 1, 2], assignments.Select(x => x.RegistrationOrder));
        Assert.Equal(character.Id, assignments[0].OsrsCharacterId);
        Assert.Equal(EventCharacterRole.Playing, assignments[0].EventRole);
        Assert.Equal(18.5m, assignments[0].EhbSnapshot);
        Assert.Equal(EhbSource.Manual, assignments[0].EhbSource);
        Assert.Null(assignments[0].ReleasedAt);
        Assert.Equal(assignment.Id, assignments[1].Id);
        Assert.Equal(releasedAlt.ReleasedAt, assignments[1].ReleasedAt);
        Assert.NotEqual(assignment.Id, assignments[2].Id);
        Assert.Equal(alternate.Id, assignments[2].OsrsCharacterId);
        Assert.Null(assignments[2].ReleasedAt);
        Assert.All(assignments.Skip(1), item =>
        {
            Assert.Equal(EventCharacterRole.Informational, item.EventRole);
            Assert.Null(item.EhbSnapshot); Assert.Null(item.EhbSource); Assert.Null(item.EhbFetchedAt);
        });
        var updatedParticipant = await verify.EventParticipants.AsNoTracking().SingleAsync(x => x.Id == participant.Id);
        Assert.Equal(participant.ResponseVersion + 2, updatedParticipant.ResponseVersion);
        participant = updatedParticipant;
        var stalePage = await a.GetStringAsync(route + "?edit=true");
        var confirmationRoute = route + $"/Confirmation?participantId={participant.Id}";
        page = await a.GetStringAsync(confirmationRoute);
        using (var withdrawal = await Lifecycle(a, "Withdraw", page)) Assert.Equal(HttpStatusCode.Redirect, withdrawal.StatusCode);
        page = await a.GetStringAsync(confirmationRoute);
        Assert.DoesNotContain("edit=true", page);
        using (var directGet = await a.GetAsync(route + "?edit=true")) Assert.Equal(HttpStatusCode.Redirect, directGet.StatusCode);
        using (var stalePost = await a.PostAsync(route, Post(stalePage, true, "18"))) Assert.Equal(HttpStatusCode.Redirect, stalePost.StatusCode);
        await using (var serviceDb = new ApplicationDbContext(options))
        {
            var withdrawn = await serviceDb.EventParticipants.SingleAsync(x => x.Id == participant.Id);
            var result = await new SignupService(serviceDb, new SecretHasher(), TimeProvider.System).SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(bingoEvent.Id, owner.Id,
                new Dictionary<Guid, AuthenticatedAccountAnswer> { [primary.Id] = new(character.Id, 99m), [alt.Id] = new(alternate.Id, 999m, "stale-token") },
                new Dictionary<Guid, string>(), null, withdrawn.ResponseVersion));
            Assert.False(result.Succeeded);
            Assert.Equal("Withdrawn signups cannot be edited. Rejoin while signup is open.", result.Error);
        }
        var before = await verify.EventParticipants.AsNoTracking().SingleAsync(x => x.Id == participant.Id);
        Assert.Equal(participant.ResponseVersion, before.ResponseVersion);
        Assert.Equal(participant.SignedUpAt, before.SignedUpAt);
        Assert.Equal(participant.SignupSequence, before.SignupSequence);
        Assert.False(await verify.EventParticipantCharacters.AnyAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null));
        // Real borrower admission proves availability, rather than inferring it from ReleasedAt.
        var borrowerPage = await b.GetStringAsync(route);
        using (var borrowed = await b.PostAsync(route, Post(borrowerPage, false, ""))) Assert.Equal(HttpStatusCode.Redirect, borrowed.StatusCode);
        var borrowerParticipant = await verify.EventParticipants.AsNoTracking().SingleAsync(x => x.AccountId == borrower.Id);
        using (var conflict = await Lifecycle(a, "Rejoin", page))
        {
            Assert.Equal(HttpStatusCode.Redirect, conflict.StatusCode);
            var conflictPage = WebUtility.HtmlDecode(await a.GetStringAsync(conflict.Headers.Location!));
            Assert.Contains(culture == "da" ? "En af dine konti" : "One of your accounts", conflictPage);
        }
        var after = await verify.EventParticipants.AsNoTracking().SingleAsync(x => x.Id == participant.Id);
        Assert.Equal(SignupStatus.Withdrawn, after.SignupStatus);
        Assert.Equal(before.ResponseVersion, after.ResponseVersion);
        Assert.Equal(before.SignupSequence, after.SignupSequence);
        Assert.Equal(before.SignedUpAt, after.SignedUpAt);
        Assert.Equal(borrowerParticipant.Id, await verify.EventParticipantCharacters.Where(x => x.OsrsCharacterId == character.Id && x.ReleasedAt == null).Select(x => x.EventParticipantId).SingleAsync());
        Assert.False(await verify.EventParticipantCharacters.AnyAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null));

        async Task Login(HttpClient client, string name)
        {
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
            var login = await client.GetStringAsync("/Account/Login");
            using var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = name, ["Input.Password"] = "journey-password", ["__RequestVerificationToken"] = AntiforgeryToken(login) }));
            Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        }
        FormUrlEncodedContent Post(string html, bool includeAlt, string altEhb) => new(new Dictionary<string, string>
        {
            [$"Input.AccountAnswers[{primary.Id}].OsrsCharacterId"] = character.Id.ToString(),
            [$"Input.AccountAnswers[{primary.Id}].Ehb"] = "18.5",
            [$"Input.AccountAnswers[{alt.Id}].OsrsCharacterId"] = includeAlt ? alternate.Id.ToString() : "",
            [$"Input.AccountAnswers[{alt.Id}].Ehb"] = altEhb,
            [$"Input.AccountAnswers[{alt.Id}].WiseOldManLookupToken"] = "stale-token",
            ["Input.ExpectedResponseVersion"] = HiddenValue(html, "Input_ExpectedResponseVersion"),
            ["__RequestVerificationToken"] = AntiforgeryToken(html)
        });
        Task<HttpResponseMessage> Lifecycle(HttpClient client, string handler, string html) => client.PostAsync(route + "/Confirmation?handler=" + handler,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(html) }));
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
        Assert.DoesNotContain("breadcrumb-bar", confirmation, StringComparison.Ordinal);
        Assert.Contains("data-confirm-message=\"Withdraw from this event? This will release your place.\"", confirmation, StringComparison.Ordinal);
        Assert.Contains("name=\"ConfirmLifecycleAction\" value=\"false\"", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("I understand that", confirmation, StringComparison.Ordinal);
        using var withdrawn = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["participantId"] = waitingId.ToString(),
            ["__RequestVerificationToken"] = AntiforgeryToken(confirmation)
        }));
        Assert.Equal(HttpStatusCode.Redirect, withdrawn.StatusCode);
        var missingConfirmationPage = await ownerClient.GetStringAsync(withdrawn.Headers.Location!);
        Assert.Contains("Confirm that you want to withdraw before continuing.", missingConfirmationPage, StringComparison.Ordinal);
        using var falseConfirmation = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ConfirmLifecycleAction"] = "false",
            ["__RequestVerificationToken"] = AntiforgeryToken(missingConfirmationPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, falseConfirmation.StatusCode);
        var rejectedWithdrawPage = await ownerClient.GetStringAsync(falseConfirmation.Headers.Location!);
        Assert.Contains("Confirm that you want to withdraw before continuing.", rejectedWithdrawPage, StringComparison.Ordinal);
        using var confirmedWithdraw = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ConfirmLifecycleAction"] = "true",
            ["__RequestVerificationToken"] = AntiforgeryToken(rejectedWithdrawPage)
        }));
        Assert.Equal(HttpStatusCode.Redirect, confirmedWithdraw.StatusCode);
        var resultPage = await ownerClient.GetStringAsync(confirmedWithdraw.Headers.Location!);
        Assert.Contains("Your signup has been withdrawn.", resultPage, StringComparison.Ordinal);
        Assert.Contains("Withdrawn", resultPage, StringComparison.Ordinal);
        Assert.DoesNotContain("edit=true", resultPage, StringComparison.Ordinal);
        Assert.Contains("You can rejoin using the button below. Your previous place is not reserved.", resultPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Your place is confirmed.", resultPage, StringComparison.Ordinal);
        Assert.Contains("Rejoin signup", resultPage, StringComparison.Ordinal);
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.Id == participantId).Select(x => x.SignupStatus).SingleAsync());
            Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == waitingId).Select(x => x.SignupStatus).SingleAsync());
            Assert.All(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId).ToListAsync(), x => Assert.NotNull(x.ReleasedAt));
        }
        using (var rejectedRejoin = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Rejoin", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "false", ["__RequestVerificationToken"] = AntiforgeryToken(resultPage) })))
        {
            Assert.Equal(HttpStatusCode.Redirect, rejectedRejoin.StatusCode);
            resultPage = await ownerClient.GetStringAsync(rejectedRejoin.Headers.Location!);
            Assert.Contains("Confirm that you want to rejoin before continuing.", resultPage, StringComparison.Ordinal);
        }
        using (var rejoined = await ownerClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Rejoin", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(resultPage) })))
        {
            Assert.Equal(HttpStatusCode.Redirect, rejoined.StatusCode);
            resultPage = await ownerClient.GetStringAsync(rejoined.Headers.Location!);
            Assert.Contains("Your signup has been restored.", resultPage, StringComparison.Ordinal);
            Assert.Contains("<strong>#1</strong>", resultPage, StringComparison.Ordinal);
            Assert.Contains("Waiting list", resultPage, StringComparison.Ordinal);
            Assert.Contains("Withdraw from event", resultPage, StringComparison.Ordinal);
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
        Assert.Contains("Signup is closed. Contact an Admin to ask about rejoining.", closedPage, StringComparison.Ordinal);
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
        var otherPage = await otherClient.GetStringAsync("/");
        using var denied = await otherClient.PostAsync($"/Events/{slug}/Signup/Confirmation?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string> { ["participantId"] = participantId.ToString(), ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(otherPage) }));
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
            var historyBoard = new Bingo.Domain.Boards.Board(Guid.NewGuid(), history.Id, "Archived history board", 1, 1);
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
            await BoardApprovalFixture.PublishAsync(db, historyBoard, now);
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
        Assert.Contains("Allowed Main", table, StringComparison.Ordinal); Assert.Contains("123.45", table, StringComparison.Ordinal); Assert.Contains("EHB", table, StringComparison.Ordinal); Assert.Contains("Allowed answer", table, StringComparison.Ordinal); Assert.Contains("Historical answer", table, StringComparison.Ordinal); Assert.Contains("Not answered", table, StringComparison.Ordinal); Assert.Contains("Waiting Main", table, StringComparison.Ordinal); Assert.Contains("01", table, StringComparison.Ordinal);
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
            var board = new Board(Guid.NewGuid(), item.Id, "Pre-live board", 1, 1);
            var template = new TileTemplate(Guid.NewGuid(), "Pre-live tile", "Public description", ObjectiveType.Manual, string.Empty, 1m);
            var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, "Pre-live tile", "Public description", string.Empty, 1m);
            var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Complete the challenge", true);
            db.AddRange(board, template, tile, requirement);
            await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement]);
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
        var route = $"/Admin/Events/Participants/{bingoEvent.Id}";
        var page = await client.GetStringAsync(route);
        Assert.Contains("WORKSPACE-USERNAME-", page, StringComparison.Ordinal); Assert.Contains("Discord linked", page, StringComparison.Ordinal); Assert.Contains("Unlinked", page, StringComparison.Ordinal); Assert.Contains("Workspace team", page, StringComparison.Ordinal);
        foreach (var query in new[] { "ParticipantSearch=WORKSPACE", "ParticipantStatus=WaitingList", "ParticipantPayment=paid", "ParticipantDiscord=linked", "ParticipantCaptain=true", "ParticipantSource=Website", $"ParticipantTeamId={team.Id}" })
        { var filtered = await client.GetStringAsync($"{route}?{query}"); Assert.Contains(query.Contains("Waiting") ? "Waiting Workspace" : "Workspace Main", filtered, StringComparison.Ordinal); }
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
        var participantsRoute = $"/Admin/Events/Participants/{bingoEvent.Id}";
        var participantsPage = await client.GetStringAsync(participantsRoute);
        var detailRoute = RenderedParticipantDetailRoute(participantsPage, participant.Id); Assert.False(string.IsNullOrWhiteSpace(detailRoute));
        var detail = await client.GetStringAsync(detailRoute);
        Assert.Contains($"action=\"{detailRoute}?handler=Payment\"", detail, StringComparison.Ordinal);
        using (var paid = await client.PostAsync($"{detailRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["payment"] = "Paid", ["__RequestVerificationToken"] = AntiforgeryToken(detail) }))) { Assert.Equal(HttpStatusCode.Redirect, paid.StatusCode); Assert.Equal($"{detailRoute}#payment", paid.Headers.Location?.OriginalString); }
        var afterPaid = await client.GetStringAsync(detailRoute);
        using (var unpaid = await client.PostAsync($"{detailRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["payment"] = "Unpaid", ["__RequestVerificationToken"] = AntiforgeryToken(afterPaid) }))) Assert.Equal(HttpStatusCode.Redirect, unpaid.StatusCode);
        var afterUnpaid = await client.GetStringAsync(detailRoute);
        using (var paidAgain = await client.PostAsync($"{detailRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["payment"] = "Paid", ["__RequestVerificationToken"] = AntiforgeryToken(afterUnpaid) }))) Assert.Equal(HttpStatusCode.Redirect, paidAgain.StatusCode);
        var afterPaidAgain = await client.GetStringAsync(detailRoute);
        using (var unpaidAgain = await client.PostAsync($"{detailRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["payment"] = "Unpaid", ["__RequestVerificationToken"] = AntiforgeryToken(afterPaidAgain) }))) Assert.Equal(HttpStatusCode.Redirect, unpaidAgain.StatusCode);
        using (var note = await client.PostAsync($"{detailRoute}?handler=AdminNote", new FormUrlEncodedContent(new Dictionary<string, string> { ["AdminNote"] = "private note", ["ExpectedAdminNote"] = "", ["__RequestVerificationToken"] = AntiforgeryToken(detail) }))) Assert.Equal(HttpStatusCode.Redirect, note.StatusCode);
        var stale = await client.GetStringAsync(detailRoute);
        using (var rejected = await client.PostAsync($"{detailRoute}?handler=AdminNote", new FormUrlEncodedContent(new Dictionary<string, string> { ["AdminNote"] = "stale overwrite", ["ExpectedAdminNote"] = "", ["__RequestVerificationToken"] = AntiforgeryToken(stale) }))) Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        await using (var verify = new ApplicationDbContext(options)) { var saved = await verify.EventParticipants.SingleAsync(x => x.Id == participant.Id); Assert.False(saved.PaymentReceived); Assert.Equal("private note", saved.AdminNotes); var audits = await verify.AuditEntries.Where(x => x.TargetId == participant.Id.ToString()).ToListAsync(); Assert.Equal(4, audits.Count(x => x.Action == "participant.payment_updated")); Assert.Contains(audits, x => x.Action == "participant.admin_note_updated" && x.BeforeState!.Contains("false") && x.AfterState!.Contains("true")); Assert.Equal(1, audits.Count(x => x.Action == "participant.admin_note_updated")); }
        var post = await client.GetStringAsync(detailRoute); Assert.Contains("Save note", post, StringComparison.Ordinal); Assert.Contains("handler=Payment", post, StringComparison.Ordinal); Assert.DoesNotContain("data-save-state", post, StringComparison.Ordinal); Assert.DoesNotContain("data-admin-note-form", post, StringComparison.Ordinal);

        await using (var lifecycle = new ApplicationDbContext(options))
        {
            var item = await lifecycle.Events.SingleAsync(x => x.Id == bingoEvent.Id);
            item.CloseSignups(now); item.StartEvent(now); item.EndEvent(now); item.FinalizeResults(now); item.Archive(now);
            await lifecycle.SaveChangesAsync();
        }
        var terminalParticipantsPage = await client.GetStringAsync(participantsRoute);
        var terminalDetailRoute = RenderedParticipantDetailRoute(terminalParticipantsPage, participant.Id); Assert.False(string.IsNullOrWhiteSpace(terminalDetailRoute));
        var terminal = await client.GetStringAsync(terminalDetailRoute);
        Assert.Contains("handler=Payment", terminal, StringComparison.Ordinal); Assert.Contains("handler=AdminNote", terminal, StringComparison.Ordinal); Assert.DoesNotContain("handler=TransferOwnership", terminal, StringComparison.Ordinal); Assert.DoesNotContain("handler=Withdraw", terminal, StringComparison.Ordinal); Assert.DoesNotContain("handler=Restore", terminal, StringComparison.Ordinal); Assert.DoesNotContain("Save changes", terminal, StringComparison.Ordinal);
        using (var terminalPayment = await client.PostAsync($"{terminalDetailRoute}?handler=Payment", new FormUrlEncodedContent(new Dictionary<string, string> { ["payment"] = "Paid", ["__RequestVerificationToken"] = AntiforgeryToken(terminal) }))) Assert.Equal(HttpStatusCode.Redirect, terminalPayment.StatusCode);
        var terminalAfterPayment = await client.GetStringAsync(terminalDetailRoute);
        using (var terminalNote = await client.PostAsync($"{terminalDetailRoute}?handler=AdminNote", new FormUrlEncodedContent(new Dictionary<string, string> { ["AdminNote"] = "archived note", ["ExpectedAdminNote"] = "private note", ["__RequestVerificationToken"] = AntiforgeryToken(terminalAfterPayment) }))) Assert.Equal(HttpStatusCode.Redirect, terminalNote.StatusCode);
        using (var terminalCorrection = await client.PostAsync(terminalDetailRoute, new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.ExpectedResponseVersion"] = "1", ["__RequestVerificationToken"] = AntiforgeryToken(terminalAfterPayment) }))) { Assert.Equal(HttpStatusCode.Redirect, terminalCorrection.StatusCode); Assert.Equal($"/Admin/Events/Manage/{bingoEvent.Id}", terminalCorrection.Headers.Location?.OriginalString); }
        using (var terminalWithdraw = await client.PostAsync($"{terminalDetailRoute}?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(terminalAfterPayment) }))) Assert.Equal(HttpStatusCode.Redirect, terminalWithdraw.StatusCode);
        using (var terminalRestore = await client.PostAsync($"{terminalDetailRoute}?handler=Restore", new FormUrlEncodedContent(new Dictionary<string, string> { ["ConfirmLifecycleAction"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(terminalAfterPayment) }))) { Assert.Equal(HttpStatusCode.Redirect, terminalRestore.StatusCode); Assert.Equal($"/Admin/Events/Manage/{bingoEvent.Id}", terminalRestore.Headers.Location?.OriginalString); }
        await using (var verify = new ApplicationDbContext(options)) { var saved = await verify.EventParticipants.SingleAsync(x => x.Id == participant.Id); Assert.True(saved.PaymentReceived); Assert.Equal("archived note", saved.AdminNotes); Assert.Equal(SignupStatus.Confirmed, saved.SignupStatus); }
    }

    [Fact]
    public async Task AdminPaymentAndNotesRollBackParticipantAndAuditStateWhenAuditPersistenceFails()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"rollback-metadata-admin-{Guid.NewGuid():N}", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var bingoEvent = Event(admin.Id, now);
        var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, bingoEvent, participant);
            await setup.SaveChangesAsync();
        }

        var paymentOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options)
            .AddInterceptors(new ThrowOnParticipantAudit("participant.payment_updated"))
            .Options;
        await using (var failingPayment = new ApplicationDbContext(paymentOptions))
        {
            var result = await new SignupService(failingPayment, new SecretHasher(), TimeProvider.System)
                .SetPaymentAsync(bingoEvent.Id, participant.Id, admin.Id, admin.LoginName, PaymentStatus.Paid);
            Assert.False(result.Succeeded);
            Assert.Contains("try again", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        var noteOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options)
            .AddInterceptors(new ThrowOnParticipantAudit("participant.admin_note_updated"))
            .Options;
        await using (var failingNote = new ApplicationDbContext(noteOptions))
        {
            var result = await new SignupService(failingNote, new SecretHasher(), TimeProvider.System)
                .SetAdminNotesAsync(bingoEvent.Id, participant.Id, admin.Id, admin.LoginName, "private note", string.Empty);
            Assert.False(result.Succeeded);
            Assert.Contains("try again", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.EventParticipants.AsNoTracking().SingleAsync(x => x.Id == participant.Id);
        Assert.False(saved.PaymentReceived);
        Assert.Null(saved.AdminNotes);
        Assert.Equal(1, saved.ResponseVersion);
        Assert.Empty(await verify.AuditEntries.Where(x => x.TargetId == participant.Id.ToString()).ToListAsync());
    }

    [Fact]
    public async Task AdminPrivateMetadataAndOwnershipFollowTheRetainedLifecycleMatrix()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"matrix-admin-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin);
        var transferTarget = Website($"matrix-target-{Guid.NewGuid():N}", now);
        var cases = new (string Key, EventState State, DraftState? DraftState)[]
        {
            ("private-setup", EventState.Draft, DraftState.Setup),
            ("signup-open", EventState.SignupOpen, null),
            ("signup-closed", EventState.SignupClosed, null),
            ("draft-running", EventState.SignupClosed, DraftState.Running),
            ("draft-paused", EventState.SignupClosed, DraftState.Paused),
            ("draft-finalized", EventState.SignupClosed, DraftState.Finalized),
            ("live", EventState.Live, null),
            ("final-review", EventState.AwaitingFinalReview, null),
            ("finalized", EventState.Finalized, null),
            ("archived", EventState.Archived, null),
            ("cancelled", EventState.Cancelled, null)
        };
        var seeded = new List<(string Key, BingoEvent Event, EventParticipant Participant)>();
        var hiddenParticipant = new EventParticipant(Guid.NewGuid(), Guid.Empty, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        BingoEvent hidden;
        BingoEvent discarded;
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, transferTarget);
            foreach (var (key, state, draftState) in cases)
            {
                var item = new BingoEvent(Guid.NewGuid(), $"Metadata {key}", $"metadata-{key}-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 10, admin.Id, now);
                DraftSession? draft = null;
                if (state != EventState.Draft)
                {
                    item.MarkFirstPublic(now);
                    item.OpenSignups(now);
                }
                if (state is EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived)
                {
                    item.CloseSignups(now);
                    if (draftState is { } selectedDraft)
                    {
                        draft = new DraftSession(Guid.NewGuid(), item.Id, 2);
                        if (selectedDraft is DraftState.Running or DraftState.Paused or DraftState.Finalized) draft.Start(now);
                        if (selectedDraft == DraftState.Paused) draft.Pause();
                        if (selectedDraft == DraftState.Finalized) draft.Finalize(now);
                        if (selectedDraft is DraftState.Running or DraftState.Paused or DraftState.Finalized) item.SetDraftLocked(true);
                    }
                }
                if (state is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived)
                {
                    item.StartEvent(now);
                    if (state is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived) item.EndEvent(now);
                    if (state is EventState.Finalized or EventState.Archived) item.FinalizeResults(now);
                    if (state == EventState.Archived) item.Archive(now);
                }
                if (state == EventState.Cancelled)
                {
                    item.Cancel(admin.Id, now, "Cancelled for lifecycle coverage", true);
                }
                var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
                seeded.Add((key, item, participant));
                db.Add(item); db.Add(participant);
                if (draft is not null) db.Add(draft);
            }

            hidden = new BingoEvent(Guid.NewGuid(), "Hidden metadata event", $"hidden-metadata-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 10, admin.Id, now);
            hidden.MarkFirstPublic(now); hidden.OpenSignups(now); hidden.CloseSignups(now); hidden.StartEvent(now); hidden.EndEvent(now); hidden.FinalizeResults(now); hidden.Archive(now); hidden.Hide(admin.Id, now, hidden.Name, "Hidden for lifecycle coverage");
            hiddenParticipant = new EventParticipant(Guid.NewGuid(), hidden.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
            db.AddRange(hidden, hiddenParticipant);

            discarded = new BingoEvent(Guid.NewGuid(), "Discarded metadata event", $"discarded-metadata-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 10, admin.Id, now);
            discarded.Discard(admin.Id, now, false);
            db.Add(discarded);
            await db.SaveChangesAsync();
        }

        foreach (var row in seeded.Where(row => row.Event.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview))
        {
            await using var db = new ApplicationDbContext(options);
            var result = await new SignupService(db, new SecretHasher(), TimeProvider.System).TransferParticipantOwnershipAsync(new ParticipantOwnershipTransferRequest(row.Event.Id, row.Participant.Id, admin.Id, admin.LoginName, transferTarget.Id, null, true));
            Assert.True(result.Succeeded, $"Ownership transfer failed for {row.Key}: {result.Error}");
        }
        foreach (var row in seeded)
        {
            await using var db = new ApplicationDbContext(options);
            var service = new SignupService(db, new SecretHasher(), TimeProvider.System);
            var payment = await service.SetPaymentAsync(row.Event.Id, row.Participant.Id, admin.Id, admin.LoginName, PaymentStatus.Paid);
            Assert.True(payment.Succeeded, $"Payment failed for {row.Key}: {payment.Error}");
            var note = await service.SetAdminNotesAsync(row.Event.Id, row.Participant.Id, admin.Id, admin.LoginName, $"note-{row.Key}", string.Empty);
            Assert.True(note.Succeeded, $"Notes failed for {row.Key}: {note.Error}");
        }

        await using (var verify = new ApplicationDbContext(options))
        {
            foreach (var row in seeded)
            {
                var participant = await verify.EventParticipants.SingleAsync(x => x.Id == row.Participant.Id);
                Assert.True(participant.PaymentReceived);
                Assert.Equal($"note-{row.Key}", participant.AdminNotes);
                if (row.Event.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview) Assert.Equal(transferTarget.Id, participant.AccountId);
            }
        }

        await using (var denied = new ApplicationDbContext(options))
        {
            var service = new SignupService(denied, new SecretHasher(), TimeProvider.System);
            foreach (var row in seeded.Where(row => row.Event.State is EventState.Finalized or EventState.Archived or EventState.Cancelled))
            {
                var result = await service.TransferParticipantOwnershipAsync(new ParticipantOwnershipTransferRequest(row.Event.Id, row.Participant.Id, admin.Id, admin.LoginName, transferTarget.Id, null, true));
                Assert.False(result.Succeeded, $"Ownership transfer unexpectedly succeeded for {row.Key}.");
            }
            Assert.False((await service.TransferParticipantOwnershipAsync(new ParticipantOwnershipTransferRequest(hidden.Id, hiddenParticipant.Id, admin.Id, admin.LoginName, transferTarget.Id, null, true))).Succeeded);
            Assert.False((await service.TransferParticipantOwnershipAsync(new ParticipantOwnershipTransferRequest(discarded.Id, Guid.NewGuid(), admin.Id, admin.LoginName, transferTarget.Id, null, true))).Succeeded);
            Assert.False((await service.SetPaymentAsync(hidden.Id, hiddenParticipant.Id, admin.Id, admin.LoginName, PaymentStatus.Paid)).Succeeded);
            Assert.False((await service.SetAdminNotesAsync(hidden.Id, hiddenParticipant.Id, admin.Id, admin.LoginName, "must-not-save", string.Empty)).Succeeded);
            Assert.False((await service.SetPaymentAsync(discarded.Id, Guid.NewGuid(), admin.Id, admin.LoginName, PaymentStatus.Paid)).Succeeded);
            Assert.False((await service.SetAdminNotesAsync(discarded.Id, Guid.NewGuid(), admin.Id, admin.LoginName, "must-not-save", string.Empty)).Succeeded);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Null(await verify.EventParticipants.Where(x => x.Id == hiddenParticipant.Id).Select(x => x.AdminNotes).SingleAsync());
            Assert.False(await verify.EventParticipants.Where(x => x.Id == hiddenParticipant.Id).Select(x => x.PaymentReceived).SingleAsync());
            Assert.Equal(EventState.Discarded, await verify.Events.Where(x => x.Id == discarded.Id).Select(x => x.State).SingleAsync());
        }
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
            var saved = await verify.EventParticipants.SingleAsync(x => x.Id == first.Id); Assert.Equal(SignupSource.Website, saved.Source); Assert.Equal(now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)), saved.SignedUpAt); Assert.Equal(1, saved.SignupSequence); Assert.True(saved.PaymentReceived); Assert.True(saved.CaptainVolunteer); Assert.Equal("private-note", saved.AdminNotes);
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
        await using (var verify = new ApplicationDbContext(options)) { Assert.Equal(correctionBeforeStale, await ParticipantStateAsync(verify, first.Id)); var saved = await verify.EventParticipants.SingleAsync(x => x.Id == first.Id); Assert.False(saved.CaptainVolunteer); Assert.Equal(SignupStatus.Confirmed, saved.SignupStatus); Assert.Equal(SignupSource.Website, saved.Source); Assert.Equal(now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)), saved.SignedUpAt); Assert.Equal(1, saved.SignupSequence); Assert.Equal(3, saved.ResponseVersion); Assert.True(saved.PaymentReceived); Assert.Equal("private-note", saved.AdminNotes); Assert.Equal("after", await verify.SignupAnswers.Where(x => x.EventParticipantId == first.Id && x.SignupQuestionId == answer.Id).Select(x => x.Value).SingleAsync()); Assert.Equal(19m, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == first.Id && x.ReleasedAt == null && x.SignupQuestionId == regular.Id).Select(x => x.EhbSnapshot).SingleAsync()); Assert.Equal(2, await verify.AuditEntries.CountAsync(x => x.TargetId == first.Id.ToString() && x.Action == "participant.corrected")); }
        var participantsRoute = $"/Admin/Events/Participants/{bingoEvent.Id}"; var participants = await client.GetStringAsync(participantsRoute);
        using (var created = await client.PostAsync($"{participantsRoute}?handler=CreateInternalParticipant", new FormUrlEncodedContent(new Dictionary<string, string> { [$"InternalParticipant.AccountAnswers[{regular.Id}].CharacterName"] = $"Internal {Guid.NewGuid():N}", [$"InternalParticipant.AccountAnswers[{regular.Id}].Ehb"] = "8", [$"InternalParticipant.Answers[{answer.Id}]"] = "created", ["__RequestVerificationToken"] = AntiforgeryToken(participants) }))) Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
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
        using (var unconfirmed = await adminClient.PostAsync($"{detailRoute}?handler=TransferOwnership", new FormUrlEncodedContent(new Dictionary<string, string> { ["ExpectedOwnerAccountId"] = oldOwner.Id.ToString(), ["DestinationOwnerAccountId"] = newOwner.Id.ToString(), ["__RequestVerificationToken"] = AntiforgeryToken(detail) }))) Assert.Equal(HttpStatusCode.Redirect, unconfirmed.StatusCode);
        await using (var unchanged = new ApplicationDbContext(options)) { Assert.Equal(oldOwner.Id, await unchanged.EventParticipants.Where(x => x.Id == participant.Id).Select(x => x.AccountId).SingleAsync()); Assert.Empty(await unchanged.PersonalNotifications.Where(x => x.Title == "participant.ownership_transferred").ToListAsync()); }
        using (var invalid = await adminClient.PostAsync($"{detailRoute}?handler=TransferOwnership", new FormUrlEncodedContent(new Dictionary<string, string> { ["ExpectedOwnerAccountId"] = oldOwner.Id.ToString(), ["DestinationOwnerAccountId"] = Guid.NewGuid().ToString(), ["ConfirmOwnershipTransfer"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(detail) }))) Assert.Equal(HttpStatusCode.Redirect, invalid.StatusCode);
        var transferPage = await adminClient.GetStringAsync(detailRoute);
        using (var transferred = await adminClient.PostAsync($"{detailRoute}?handler=TransferOwnership", new FormUrlEncodedContent(new Dictionary<string, string> { ["ExpectedOwnerAccountId"] = oldOwner.Id.ToString(), ["DestinationOwnerAccountId"] = newOwner.Id.ToString(), ["ConfirmOwnershipTransfer"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(transferPage) }))) Assert.Equal(HttpStatusCode.Redirect, transferred.StatusCode);
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.EventParticipants.SingleAsync(x => x.Id == participant.Id); Assert.Equal(newOwner.Id, saved.AccountId); Assert.Equal(now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)), saved.SignedUpAt); Assert.Equal(1, saved.SignupSequence); Assert.True(saved.PaymentReceived); Assert.Equal("private", saved.AdminNotes);
            Assert.Equal(12m, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
            var notifications = await verify.PersonalNotifications.Where(x => x.Title == "participant.ownership_transferred").ToListAsync();
            Assert.Equal(2, notifications.Count); Assert.Equal("/Account/MyEvents", notifications.Single(x => x.RecipientAccountId == oldOwner.Id).Route); Assert.Equal($"/Events/{bingoEvent.Slug}/Signup/Confirmation?participantId={participant.Id}", notifications.Single(x => x.RecipientAccountId == newOwner.Id).Route);
            Assert.Single(await verify.AuditEntries.Where(x => x.TargetId == participant.Id.ToString() && x.Action == "participant.ownership_transferred").ToListAsync());
        }
        using var oldClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); await LoginAsync(oldClient, oldOwner.LoginName, "password"); Assert.NotEqual(HttpStatusCode.OK, (await oldClient.GetAsync($"/Events/{bingoEvent.Slug}/Signup/Confirmation?participantId={participant.Id}")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await oldClient.GetAsync("/Account/MyEvents")).StatusCode);
        using var newClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); await LoginAsync(newClient, newOwner.LoginName, "password"); Assert.Equal(HttpStatusCode.OK, (await newClient.GetAsync($"/Events/{bingoEvent.Slug}/Signup/Confirmation?participantId={participant.Id}")).StatusCode);
        var duplicatePage = await adminClient.GetStringAsync(detailRoute);
        using (var duplicateTransfer = await adminClient.PostAsync($"{detailRoute}?handler=TransferOwnership", new FormUrlEncodedContent(new Dictionary<string, string> { ["ExpectedOwnerAccountId"] = newOwner.Id.ToString(), ["DestinationOwnerAccountId"] = duplicateOwner.Id.ToString(), ["ConfirmOwnershipTransfer"] = "true", ["__RequestVerificationToken"] = AntiforgeryToken(duplicatePage) }))) Assert.Equal(HttpStatusCode.Redirect, duplicateTransfer.StatusCode);
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
        var first = TransferAsync(firstDestination.Id); var second = TransferAsync(secondDestination.Id); start.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, results.Count(x => x.Succeeded)); Assert.Equal(1, results.Count(x => !x.Succeeded && x.Error!.Contains("changed elsewhere", StringComparison.Ordinal)));
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.EventParticipants.SingleAsync(x => x.Id == participant.Id); Assert.True(saved.AccountId is { } winner && (winner == firstDestination.Id || winner == secondDestination.Id)); Assert.Equal(SignupSource.Website, saved.Source); Assert.Equal(now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond)), saved.SignedUpAt); Assert.Equal(1, saved.SignupSequence); Assert.True(saved.PaymentReceived); Assert.Equal("race-private-note", saved.AdminNotes);
            Assert.Equal("retained", await verify.SignupAnswers.Where(x => x.EventParticipantId == participant.Id).Select(x => x.Value).SingleAsync()); Assert.Equal(13m, await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).Select(x => x.EhbSnapshot).SingleAsync());
            Assert.Equal(original.Id, await verify.AccountOsrsCharacters.Where(x => x.OsrsCharacterId == character.Id).Select(x => x.AccountId).SingleAsync()); Assert.Equal(team.Id, await verify.TeamMemberships.Where(x => x.EventParticipantId == participant.Id && x.LeftAt == null).Select(x => x.TeamId).SingleAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.TargetId == participant.Id.ToString() && x.Action == "participant.ownership_transferred").ToListAsync()); Assert.Equal(2, await verify.PersonalNotifications.CountAsync(x => x.Title == "participant.ownership_transferred"));
        }

        async Task<ParticipantOwnershipTransferResult> TransferAsync(Guid destination)
        {
            await start.Task;
            await using var context = new ApplicationDbContext(options);
            var service = new SignupService(context, new SecretHasher(), TimeProvider.System);
            return await service.TransferParticipantOwnershipAsync(new ParticipantOwnershipTransferRequest(bingoEvent.Id, participant.Id, admin.Id, admin.LoginName, destination, original.Id, true));
        }
    }

    [Fact]
    public async Task OwnershipTransferRollsBackParticipantAuditAndNotificationsTogether()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"rollback-transfer-admin-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin);
        var original = Website($"rollback-transfer-original-{Guid.NewGuid():N}", now);
        var destination = Website($"rollback-transfer-destination-{Guid.NewGuid():N}", now);
        var bingoEvent = Event(admin.Id, now);
        var participant = new EventParticipant(Guid.NewGuid(), bingoEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website); participant.AssignOwner(original);
        await using (var setup = new ApplicationDbContext(options)) { setup.AddRange(admin, original, destination, bingoEvent, participant); await setup.SaveChangesAsync(); }

        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).AddInterceptors(new ThrowOnOwnershipAudit()).Options;
        await using (var failing = new ApplicationDbContext(failingOptions))
        {
            var service = new SignupService(failing, new SecretHasher(), TimeProvider.System);
            var result = await service.TransferParticipantOwnershipAsync(new ParticipantOwnershipTransferRequest(bingoEvent.Id, participant.Id, admin.Id, admin.LoginName, destination.Id, original.Id, true));
            Assert.False(result.Succeeded);
            Assert.Contains("try again", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(original.Id, await verify.EventParticipants.Where(x => x.Id == participant.Id).Select(x => x.AccountId).SingleAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.TargetId == participant.Id.ToString() && x.Action == "participant.ownership_transferred").ToListAsync());
        Assert.Empty(await verify.PersonalNotifications.Where(x => x.EventId == bingoEvent.Id && x.Title == "participant.ownership_transferred").ToListAsync());
    }

    private static async Task<ParticipantState> ParticipantStateAsync(ApplicationDbContext db, Guid participantId)
    {
        var participant = await db.EventParticipants.SingleAsync(item => item.Id == participantId);
        var assignments = await db.EventParticipantCharacters.Where(item => item.EventParticipantId == participantId).OrderBy(item => item.RegistrationOrder).Select(item => $"{item.OsrsCharacterId}:{item.EventRole}:{item.EhbSnapshot}:{item.ReleasedAt}:{item.SignupQuestionId}").ToListAsync();
        var answers = await db.SignupAnswers.Where(item => item.EventParticipantId == participantId).OrderBy(item => item.SignupQuestionId).Select(item => $"{item.SignupQuestionId}:{item.Value}").ToListAsync();
        return new(participant.AccountId, participant.SignedUpAt, participant.SignupSequence, participant.SignupStatus, participant.Source, participant.ResponseVersion, participant.CaptainVolunteer, participant.PaymentReceived, participant.AdminNotes, string.Join('|', assignments), string.Join('|', answers), await db.AuditEntries.CountAsync(item => item.TargetId == participantId.ToString()), await db.PersonalNotifications.CountAsync());
    }

    private sealed record ParticipantState(Guid? OwnerId, DateTimeOffset SignedUpAt, long SignupSequence, SignupStatus Status, SignupSource Source, int ResponseVersion, bool CaptainVolunteer, bool PaymentReceived, string? AdminNote, string Assignments, string Answers, int AuditCount, int NotificationCount);

    private sealed class ThrowOnOwnershipAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == "participant.ownership_transferred")
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated ownership audit persistence failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class ThrowOnParticipantAudit(string action) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == action)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException($"Simulated {action} audit persistence failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class FixedSignupTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static Account Website(string name, DateTimeOffset now) => Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);

    private static FormUrlEncodedContent SignupPost(string page, Guid questionId, Guid characterId, string ehb) => new(new Dictionary<string, string>
    {
        [$"Input.AccountAnswers[{questionId}].OsrsCharacterId"] = characterId.ToString(),
        [$"Input.AccountAnswers[{questionId}].Ehb"] = ehb,
        ["Input.ExpectedResponseVersion"] = HiddenValue(page, "Input_ExpectedResponseVersion"),
        ["__RequestVerificationToken"] = AntiforgeryToken(page)
    });

    private static FormUrlEncodedContent RenderedSignupPost(string page, Guid primaryQuestionId, Guid? primaryCharacterId, Guid optionalRegularQuestionId, Guid optionalAltQuestionId, Guid textQuestionId, string? signupCode, Guid? optionalRegularCharacterId = null, string optionalRegularEhb = "", string textAnswer = "A retained answer") => new(new Dictionary<string, string>
    {
        [$"Input.AccountAnswers[{primaryQuestionId}].OsrsCharacterId"] = primaryCharacterId?.ToString() ?? string.Empty,
        [$"Input.AccountAnswers[{primaryQuestionId}].Ehb"] = "18",
        [$"Input.AccountAnswers[{optionalRegularQuestionId}].OsrsCharacterId"] = optionalRegularCharacterId?.ToString() ?? string.Empty,
        [$"Input.AccountAnswers[{optionalRegularQuestionId}].Ehb"] = optionalRegularEhb,
        [$"Input.AccountAnswers[{optionalAltQuestionId}].OsrsCharacterId"] = string.Empty,
        [$"Input.Answers[{textQuestionId}]"] = textAnswer,
        ["Input.SignupCode"] = signupCode ?? string.Empty,
        ["__RequestVerificationToken"] = AntiforgeryToken(page)
    });

    private static string InputTag(string page, string id) => Regex.Match(page, $"<input(?=[^>]*\\bid=\"{Regex.Escape(id)}\")[^>]*>").Value;

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
    private static string HiddenValue(string page, string id) => Regex.Match(InputTag(page, id), "value=\"([^\"]*)\"").Groups[1].Value;

    private static string RenderedParticipantDetailRoute(string page, Guid participantId) =>
        Regex.Match(page, $"<tr[^>]*data-participant-id=\"{Regex.Escape(participantId.ToString())}\"[\\s\\S]*?<a[^>]+href=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value;

    private static decimal InputDecimal(string page, string id) => decimal.Parse(InputValue(page, id), CultureInfo.InvariantCulture);

    private static string AccountControl(string page, Guid id) => Regex.Match(page, $"<input(?=[^>]*type=\"radio\")(?=[^>]*value=\"{Regex.Escape(id.ToString())}\")[^>]*>").Value;

    private static BingoEvent Event(Guid ownerId, DateTimeOffset now)
    {
        var item = new BingoEvent(Guid.NewGuid(), "Authenticated signup", $"authenticated-signup-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 10, ownerId, now);
        item.MarkFirstPublic(now);
        item.OpenSignups(now);
        return item;
    }

    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private sealed class FakeWiseOldManPlayerLookup : IWiseOldManPlayerLookup
    {
        public int Calls { get; private set; }
        public Task<WiseOldManPlayerLookupResult> LookupPlayerAsync(string characterName, CancellationToken cancellationToken = default)
        {
            Calls++;
            var ehb = characterName == "Route WoM Add" ? 3000.09582m : 17.5m;
            return Task.FromResult(new WiseOldManPlayerLookupResult(WiseOldManLookupStatus.Success, ehb, DateTimeOffset.UtcNow));
        }
    }
}
