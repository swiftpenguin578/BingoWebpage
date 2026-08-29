using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice9Pass92LiveWithdrawalIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice9_pass92")
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
    public async Task LiveWithdrawalAndWaitingReplacementPreserveHistoryAndUseOneVacancyWinner()
    {
        var seed = await SeedAsync();
        var clock = new FixedTimeProvider(seed.Now);

        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Service(db, clock).WithdrawLiveAsync(new(seed.EventId, seed.DepartedParticipantId, seed.AdminId, "admin", seed.DepartedMembershipVersion));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(new DateTimeOffset(2026, 8, 2, 12, 35, 0, TimeSpan.Zero), result.EffectiveAtUtc);
        }

        await using (var afterWithdrawal = new ApplicationDbContext(options))
        {
            var departed = await afterWithdrawal.EventParticipants.SingleAsync(x => x.Id == seed.DepartedParticipantId);
            var formerMembership = await afterWithdrawal.TeamMemberships.SingleAsync(x => x.Id == seed.DepartedMembershipId);
            Assert.Equal(SignupStatus.Withdrawn, departed.SignupStatus);
            Assert.Equal(new DateTimeOffset(2026, 8, 2, 12, 35, 0, TimeSpan.Zero), departed.WithdrawnAt);
            Assert.NotNull(formerMembership.LeftAt);
            Assert.Equal(TeamMembershipRole.Participant, formerMembership.Role);
            Assert.All(await afterWithdrawal.EventParticipantCharacters.Where(x => x.EventParticipantId == seed.DepartedParticipantId).ToListAsync(), x => Assert.Null(x.ReleasedAt));
            Assert.Single(await afterWithdrawal.DraftPicks.Where(x => x.EventParticipantId == seed.DepartedParticipantId).ToListAsync());
            Assert.Single(await afterWithdrawal.TeamMembershipRoleTransitions.Where(x => x.TeamMembershipId == seed.DepartedMembershipId).ToListAsync());
            Assert.Equal(SignupStatus.WaitingList, await afterWithdrawal.EventParticipants.Where(x => x.Id == seed.WaitingParticipantId).Select(x => x.SignupStatus).SingleAsync());
            Assert.Equal(3, await afterWithdrawal.PersonalNotifications.CountAsync(x => x.Title == "participant.live_withdrawn"));
        }

        async Task<LiveParticipantResult> FillAsync()
        {
            await using var db = new ApplicationDbContext(options);
            return await Service(db, clock).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.WaitingParticipantId));
        }

        var fills = await Task.WhenAll(FillAsync(), FillAsync());
        Assert.Single(fills, x => x.Succeeded);
        Assert.Single(fills, x => !x.Succeeded);

        Guid followUpId;
        await using (var verify = new ApplicationDbContext(options))
        {
            var replacement = await verify.EventParticipants.SingleAsync(x => x.Id == seed.WaitingParticipantId);
            var membership = await verify.TeamMemberships.SingleAsync(x => x.EventParticipantId == seed.WaitingParticipantId);
            var followUp = await verify.WaitingListPromotionFollowUps.SingleAsync();
            followUpId = followUp.Id;
            Assert.Equal(SignupStatus.Confirmed, replacement.SignupStatus);
            Assert.Equal(TeamMembershipSource.Replacement, membership.Source);
            Assert.Equal(seed.DepartedMembershipId, membership.ReplacesMembershipId);
            Assert.Null(membership.AssignedByDraftPickId);
            Assert.Empty(await verify.DraftPicks.Where(x => x.EventParticipantId == seed.WaitingParticipantId).ToListAsync());
            Assert.Single(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == seed.WaitingParticipantId && x.PreviousOsrsCharacterId == null).ToListAsync());
            Assert.Equal(new DateTimeOffset(2026, 8, 2, 12, 35, 0, TimeSpan.Zero), await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == seed.WaitingParticipantId).Select(x => x.EffectiveAtUtc).SingleAsync());
            Assert.Equal(4, await verify.PersonalNotifications.CountAsync(x => x.Title == "participant.live_replaced"));
            Assert.False(followUp.CompletedAt.HasValue);
        }

        await using (var routeDb = new ApplicationDbContext(options))
        {
            var routeContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, seed.AdminId.ToString()), new Claim(ClaimTypes.Name, "admin")], "test")) };
            var route = new Bingo.Web.Pages.Admin.Events.ParticipantModel(routeDb, new EventParticipantCharacterService(routeDb, clock), Service(routeDb, clock))
            {
                PageContext = new PageContext(new ActionContext(routeContext, new RouteData(), new PageActionDescriptor()))
            };
            Assert.IsType<PageResult>(await route.OnGetAsync(seed.EventId, seed.WaitingParticipantId, CancellationToken.None));
            Assert.NotNull(route.PromotionFollowUp);
        }

        await using (var complete = new ApplicationDbContext(options))
            Assert.True((await Service(complete, clock).CompletePromotionFollowUpAsync(seed.EventId, followUpId, seed.AdminId, "admin")).Changed);
        await using (var repeat = new ApplicationDbContext(options))
            Assert.False((await Service(repeat, clock).CompletePromotionFollowUpAsync(seed.EventId, followUpId, seed.AdminId, "admin")).Changed);
        await using var final = new ApplicationDbContext(options);
        var completed = await final.WaitingListPromotionFollowUps.SingleAsync();
        Assert.Equal(seed.AdminId, completed.CompletedByAccountId);
        Assert.NotNull(completed.CompletedAt);

        await using (var authorityDb = new ApplicationDbContext(options))
        {
            var authority = new EvidenceAuthority(authorityDb);
            var eligibilityBoundary = new DateTimeOffset(2026, 8, 2, 12, 35, 0, TimeSpan.Zero);
            await authority.AuthorizeAsync(seed.LeaderOwnerId, seed.EventId, seed.TeamId, seed.DepartedParticipantId, eligibilityBoundary, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(seed.LeaderOwnerId, seed.EventId, seed.TeamId, seed.DepartedParticipantId, eligibilityBoundary.AddTicks(10), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => authority.ResolveCreditedCharacterAsync(seed.EventId, seed.WaitingParticipantId, eligibilityBoundary.AddTicks(-10), CancellationToken.None));
            var replacementCharacter = await authority.ResolveCreditedCharacterAsync(seed.EventId, seed.WaitingParticipantId, eligibilityBoundary, CancellationToken.None);
            Assert.NotEqual(Guid.Empty, replacementCharacter.OsrsCharacterId);
        }
    }

    [Fact]
    public async Task InternalReplacementIsValidatedAndStartsWithNoInheritedDraftState()
    {
        var seed = await SeedAsync();
        var clock = new FixedTimeProvider(seed.Now);
        var form = new SignupForm(Guid.NewGuid(), seed.EventId, seed.Now);
        var question = new SignupQuestion(Guid.NewGuid(), form.Id, seed.EventId, "primary_regular_account", "Main account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var character = new OsrsCharacter(Guid.NewGuid(), "Internal replacement", "INTERNAL REPLACEMENT", seed.Now);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(form, question, character);
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            Assert.True((await Service(db, clock).WithdrawLiveAsync(new(seed.EventId, seed.DepartedParticipantId, seed.AdminId, "admin", seed.DepartedMembershipVersion))).Succeeded);
        }

        var internalRequest = new AdminParticipantChangeRequest(
            seed.EventId,
            null,
            seed.AdminId,
            "admin",
            null,
            new Dictionary<Guid, AdminAccountAnswer> { [question.Id] = new("Internal replacement", 12m) },
            new Dictionary<Guid, string>());
        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Service(db, clock).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", null, internalRequest));
            Assert.True(result.Succeeded, result.Error);
        }

        await using var verify = new ApplicationDbContext(options);
        var replacement = await verify.EventParticipants.SingleAsync(x => x.Source == SignupSource.AdminCreated);
        var membership = await verify.TeamMemberships.SingleAsync(x => x.EventParticipantId == replacement.Id && x.LeftAt == null);
        Assert.Equal(TeamMembershipSource.Replacement, membership.Source);
        Assert.Equal(seed.DepartedMembershipId, membership.ReplacesMembershipId);
        Assert.Null(replacement.AccountId);
        Assert.Empty(await verify.DraftPicks.Where(x => x.EventParticipantId == replacement.Id).ToListAsync());
        Assert.Empty(await verify.WaitingListPromotionFollowUps.ToListAsync());
        Assert.Single(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == replacement.Id && x.PreviousOsrsCharacterId == null).ToListAsync());
    }

    [Fact]
    public async Task LiveParticipantRouteWithdrawsAndManageLinksLifecycleActionsWhileAwaitingFinalReviewRemainsRejected()
    {
        var seed = await SeedAsync();
        var clock = new FixedTimeProvider(seed.Now);
        Guid externalParticipantId;
        long externalMembershipVersion;
        Guid unsupportedEventId;
        Guid unsupportedParticipantId;
        long unsupportedMembershipVersion;
        await using (var db = new ApplicationDbContext(options))
        {
            var externalParticipant = new EventParticipant(Guid.NewGuid(), seed.EventId, SignupStatus.Confirmed, 4, seed.Now, SignupSource.AdminCreated);
            var externalMembership = new TeamMembership(Guid.NewGuid(), seed.TeamId, externalParticipant.Id, TeamMembershipRole.Participant, seed.Now, null, "external route test");
            externalParticipantId = externalParticipant.Id;
            externalMembershipVersion = externalMembership.Version;
            db.AddRange(externalParticipant, externalMembership);
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var unsupported = new BingoEvent(Guid.NewGuid(), "Awaiting route event", $"awaiting-route-{Guid.NewGuid():N}", "UTC", seed.AdminId, seed.Now);
            unsupported.ConfigureInitialSchedule(seed.Now.AddDays(-2), seed.Now.AddDays(-1), seed.Now.AddDays(-1), seed.Now.AddHours(-2), seed.Now.AddHours(3), 3);
            unsupported.OpenSignups(seed.Now.AddDays(-2));
            unsupported.CloseSignups(seed.Now.AddDays(-1));
            unsupported.StartEvent(seed.Now.AddHours(-2));
            unsupported.EndEvent(seed.Now.AddHours(-1));
            var team = new Team(Guid.NewGuid(), unsupported.Id, "Awaiting team", "awaiting-team", TeamFormationType.Drafted, null, true, seed.Now);
            var participant = Participant(unsupported.Id, await db.Accounts.SingleAsync(x => x.Id == seed.AdminId), "Awaiting participant", SignupStatus.Confirmed, 1, seed.Now);
            var assignment = Assignment(unsupported.Id, participant, "Awaiting main", seed.AdminId, 10m, seed.Now);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, seed.Now, null, "test");
            unsupportedEventId = unsupported.Id;
            unsupportedParticipantId = participant.Id;
            unsupportedMembershipVersion = membership.Version;
            db.AddRange(unsupported, team, participant, assignment.Character, assignment.Assignment, membership);
            await db.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString())
                .ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(seed.Now));
                }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using var loggedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "pass92-admin",
            ["Input.Password"] = "password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(HttpStatusCode.Redirect, loggedIn.StatusCode);

        var participants = await client.GetStringAsync($"/Admin/Events/Participants/{seed.EventId}");
        Assert.DoesNotContain("Locked for draft", participants, StringComparison.Ordinal);
        Assert.Contains($"/Admin/Events/Participant/{seed.EventId}/Participants/{externalParticipantId}", participants, StringComparison.Ordinal);
        Assert.Contains("External roster member", participants, StringComparison.Ordinal);
        Assert.Contains("Manage live participant", participants, StringComparison.Ordinal);

        var liveParticipant = await client.GetStringAsync($"/Admin/Events/Participant/{seed.EventId}/Participants/{externalParticipantId}");
        Assert.Equal(externalMembershipVersion.ToString(CultureInfo.InvariantCulture), InputValue(liveParticipant, "ExpectedMembershipVersion"));
        using var liveWithdrawal = await client.PostAsync($"/Admin/Events/Participant/{seed.EventId}/Participants/{externalParticipantId}?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ExpectedMembershipVersion"] = InputValue(liveParticipant, "ExpectedMembershipVersion"),
            ["ConfirmLifecycleAction"] = "true",
            ["__RequestVerificationToken"] = AntiforgeryToken(liveParticipant)
        }));
        Assert.Equal(HttpStatusCode.Redirect, liveWithdrawal.StatusCode);

        var notificationsAfterWithdrawal = await client.GetStringAsync("/notifications");
        var withdrawalReadLink = Regex.Match(notificationsAfterWithdrawal, "<a[^>]*href=\"([^\"]+)\"[^>]*><strong>Live participant withdrawn</strong>").Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(withdrawalReadLink));
        using var withdrawalRead = await client.GetAsync(withdrawalReadLink);
        Assert.Equal(HttpStatusCode.Redirect, withdrawalRead.StatusCode);
        using var withdrawalDestination = await client.GetAsync(withdrawalRead.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, withdrawalDestination.StatusCode);

        var vacancyLink = Regex.Match(notificationsAfterWithdrawal, "<a[^>]*href=\"([^\"]+)\"[^>]*>(?:(?!</a>).)*<strong>Open vacancy</strong>", RegexOptions.Singleline).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(vacancyLink));
        var vacancyParticipant = await client.GetStringAsync(vacancyLink);
        Assert.Contains("Fill open vacancy", vacancyParticipant, StringComparison.Ordinal);
        using var fillVacancy = await client.PostAsync($"{vacancyLink}?handler=FillVacancy", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["VacancyMembershipId"] = InputValue(vacancyParticipant, "VacancyMembershipId"),
            ["VacancyMembershipVersion"] = InputValue(vacancyParticipant, "VacancyMembershipVersion"),
            ["ReplacementWaitingParticipantId"] = seed.WaitingParticipantId.ToString(),
            ["__RequestVerificationToken"] = AntiforgeryToken(vacancyParticipant)
        }));
        Assert.Equal(HttpStatusCode.Redirect, fillVacancy.StatusCode);

        var notificationsAfterReplacement = await client.GetStringAsync("/notifications");
        var followUpLink = Regex.Match(notificationsAfterReplacement, "<a[^>]*href=\"([^\"]+)\"[^>]*>(?:(?!</a>).)*<strong>Waiting-list follow-up</strong>", RegexOptions.Singleline).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(followUpLink));
        var promotedParticipant = await client.GetStringAsync(followUpLink);
        Assert.Contains("Mark follow-up complete", promotedParticipant, StringComparison.Ordinal);
        var followUpId = Guid.Parse(InputValue(promotedParticipant, "followUpId"));
        using var completeFollowUp = await client.PostAsync($"{followUpLink}?handler=CompletePromotionFollowUp", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["followUpId"] = followUpId.ToString(),
            ["__RequestVerificationToken"] = AntiforgeryToken(promotedParticipant)
        }));
        Assert.Equal(HttpStatusCode.Redirect, completeFollowUp.StatusCode);
        await using (var repeat = new ApplicationDbContext(options))
            Assert.False((await Service(repeat, clock).CompletePromotionFollowUpAsync(seed.EventId, followUpId, seed.AdminId, "admin")).Changed);

        var unsupportedParticipant = await client.GetStringAsync($"/Admin/Events/Participant/{unsupportedEventId}/Participants/{unsupportedParticipantId}");
        using var unsupportedWithdrawal = await client.PostAsync($"/Admin/Events/Participant/{unsupportedEventId}/Participants/{unsupportedParticipantId}?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ExpectedMembershipVersion"] = unsupportedMembershipVersion.ToString(CultureInfo.InvariantCulture),
            ["ConfirmLifecycleAction"] = "true",
            ["__RequestVerificationToken"] = AntiforgeryToken(unsupportedParticipant)
        }));
        Assert.Equal(HttpStatusCode.Redirect, unsupportedWithdrawal.StatusCode);
        var unsupportedResult = await client.GetStringAsync(unsupportedWithdrawal.Headers.Location!.OriginalString);
        Assert.Contains("Participant lifecycle changes are locked because the draft has started or the event has moved on.", unsupportedResult, StringComparison.Ordinal);

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Withdrawn, await verify.EventParticipants.Where(x => x.Id == externalParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.NotNull(await verify.TeamMemberships.Where(x => x.EventParticipantId == externalParticipantId).Select(x => x.LeftAt).SingleAsync());
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == seed.WaitingParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.NotNull(await verify.WaitingListPromotionFollowUps.Where(x => x.Id == followUpId).Select(x => x.CompletedAt).SingleAsync());
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == unsupportedParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Null(await verify.TeamMemberships.Where(x => x.EventParticipantId == unsupportedParticipantId).Select(x => x.LeftAt).SingleAsync());
    }

    private async Task<Seed> SeedAsync()
    {
        var now = new DateTimeOffset(2026, 8, 2, 12, 34, 27, TimeSpan.Zero);
        await using var db = new ApplicationDbContext(options);
        var admin = Website("pass92-admin", GlobalRole.Admin, now);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, incrementVersion: false);
        var superAdmin = Website("pass92-super", GlobalRole.SuperAdmin, now);
        var leaderOwner = Website("pass92-leader", GlobalRole.User, now);
        var departedOwner = Website("pass92-departed", GlobalRole.User, now);
        var waitingOwner = Website("pass92-waiting", GlobalRole.User, now);
        var item = new BingoEvent(Guid.NewGuid(), "Pass 9.2", $"pass92-{Guid.NewGuid():N}", "UTC", admin.Id, now.AddDays(-1));
        item.ConfigureInitialSchedule(now.AddDays(-2), now.AddDays(-1), now.AddDays(-1), now.AddHours(-1), now.AddHours(4), 3);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.SetDraftLocked(true);
        item.StartEvent(now.AddHours(-1));
        var team = new Team(Guid.NewGuid(), item.Id, "Live team", "live-team", TeamFormationType.Drafted, null, true, now.AddDays(-1));
        var departed = Participant(item.Id, departedOwner, "Departed", SignupStatus.Confirmed, 1, now.AddDays(-2));
        var leader = Participant(item.Id, leaderOwner, "Leader", SignupStatus.Confirmed, 2, now.AddDays(-2).AddMinutes(1));
        var waiting = Participant(item.Id, waitingOwner, "Waiting", SignupStatus.WaitingList, 3, now.AddDays(-2).AddMinutes(2));
        var departedAssignment = Assignment(item.Id, departed, "Departed main", departedOwner.Id, 20m, now.AddDays(-2));
        var leaderAssignment = Assignment(item.Id, leader, "Leader main", leaderOwner.Id, 30m, now.AddDays(-2));
        var waitingAssignment = Assignment(item.Id, waiting, "Waiting main", waitingOwner.Id, 40m, now.AddDays(-2));
        var departedMembership = new TeamMembership(Guid.NewGuid(), team.Id, departed.Id, TeamMembershipRole.Captain, now.AddDays(-1), null, "Draft pick");
        var leaderMembership = new TeamMembership(Guid.NewGuid(), team.Id, leader.Id, TeamMembershipRole.Captain, now.AddDays(-1), null, "Draft pick");
        var session = new DraftSession(Guid.NewGuid(), item.Id, 2);
        var pick = new DraftPick(Guid.NewGuid(), session.Id, team.Id, departed.Id, 1, 1, now.AddDays(-1));
        db.AddRange(admin, superAdmin, leaderOwner, departedOwner, waitingOwner, item, team, departed, leader, waiting,
            departedAssignment.Character, departedAssignment.Assignment, leaderAssignment.Character, leaderAssignment.Assignment, waitingAssignment.Character, waitingAssignment.Assignment,
            departedMembership, leaderMembership, session, pick,
            new EventParticipantCharacterSwap(Guid.NewGuid(), item.Id, departed.Id, null, departedAssignment.Assignment.OsrsCharacterId, now.AddHours(-1), now.AddHours(-1), null, null),
            new EventParticipantCharacterSwap(Guid.NewGuid(), item.Id, leader.Id, null, leaderAssignment.Assignment.OsrsCharacterId, now.AddHours(-1), now.AddHours(-1), null, null));
        await db.SaveChangesAsync();
        return new(item.Id, admin.Id, leaderOwner.Id, team.Id, departed.Id, waiting.Id, departedMembership.Id, departedMembership.Version, now);
    }

    private static SignupService Service(ApplicationDbContext db, TimeProvider clock) => new(db, new SecretHasher(), clock);
    private static EventParticipant Participant(Guid eventId, Account owner, string name, SignupStatus status, long sequence, DateTimeOffset now)
    {
        var participant = new EventParticipant(Guid.NewGuid(), eventId, status, sequence, now, SignupSource.Website);
        participant.AssignOwner(owner);
        return participant;
    }
    private static (OsrsCharacter Character, EventParticipantCharacter Assignment) Assignment(Guid eventId, EventParticipant participant, string name, Guid ownerId, decimal ehb, DateTimeOffset now)
    {
        var character = new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), eventId, participant.Id, character.Id, 0, now, ownerId, null, EventCharacterRole.Playing, ehb, EhbSource.Manual, null);
        return (character, assignment);
    }
    private static Account Website(string name, GlobalRole role, DateTimeOffset now) { var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now); account.SetGlobalRole(role); return account; }
    private sealed record Seed(Guid EventId, Guid AdminId, Guid LeaderOwnerId, Guid TeamId, Guid DepartedParticipantId, Guid WaitingParticipantId, Guid DepartedMembershipId, long DepartedMembershipVersion, DateTimeOffset Now);
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private static string InputValue(string page, string name) => Regex.Match(page, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
}
