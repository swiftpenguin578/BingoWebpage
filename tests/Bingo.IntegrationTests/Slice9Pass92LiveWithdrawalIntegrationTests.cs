using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
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
    public async Task LiveWithdrawalAndReplacementAreRetiredAndPreserveHistory()
    {
        var seed = await SeedAsync();
        var clock = new FixedTimeProvider(seed.Now);
        var before = await StateHashAsync();

        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Service(db, clock).WithdrawLiveAsync(new(seed.EventId, seed.DepartedParticipantId, seed.AdminId, "admin", seed.DepartedMembershipVersion));
            Assert.False(result.Succeeded);
            Assert.Contains("Roster membership is fixed after the event first goes Live.", result.Error, StringComparison.Ordinal);
        }

        async Task<LiveParticipantResult> FillAsync()
        {
            await using var db = new ApplicationDbContext(options);
            return await Service(db, clock).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.WaitingParticipantId));
        }

        var fills = await Task.WhenAll(FillAsync(), FillAsync());
        Assert.All(fills, result =>
        {
            Assert.False(result.Succeeded);
            Assert.Contains("replacements and vacancies are retired", result.Error, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Equal(before, await StateHashAsync());
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == seed.DepartedParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Null(await verify.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.LeftAt).SingleAsync());
        Assert.Equal(seed.DepartedMembershipVersion, await verify.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync());
        Assert.Empty(await verify.PersonalNotifications.ToListAsync());
        Assert.Empty(await verify.AuditEntries.ToListAsync());
        Assert.Empty(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == seed.WaitingParticipantId).ToListAsync());
    }

    [Fact]
    public async Task LiveReplacementActivatesEditedSystemPrimaryAndPreservesHistoricalCreditAndSecondaryRoles()
    {
        var seed = await SeedAsync(startLive: false);
        var signupAt = seed.Now.AddDays(-1).AddHours(-1);
        Guid editedPrimaryId;
        Guid historicalCreditedCharacterId;
        string historicalCreditedCharacterName;
        string assignmentsBefore;
        string historyBefore;
        await using (var db = new ApplicationDbContext(options))
        {
            var participant = await db.EventParticipants.SingleAsync(x => x.Id == seed.WaitingParticipantId);
            var primary = await db.SignupQuestions.SingleAsync(x => x.EventId == seed.EventId && x.SystemField == SignupSystemField.PrimaryRegularAccount);
            var secondary = new SignupQuestion(Guid.NewGuid(), primary.SignupFormId, seed.EventId, "secondary", "Secondary", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Playing);
            var informational = new SignupQuestion(Guid.NewGuid(), primary.SignupFormId, seed.EventId, "informational", "Informational", SignupQuestionType.Account, false, 2, null, SignupSystemField.None, EventCharacterRole.Informational);
            var originalPrimary = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == participant.Id);
            var secondaryCharacter = new OsrsCharacter(Guid.NewGuid(), "Secondary B", "SECONDARY B", signupAt);
            var editedPrimary = new OsrsCharacter(Guid.NewGuid(), "Edited primary C", "EDITED PRIMARY C", signupAt);
            var informationalCharacter = new OsrsCharacter(Guid.NewGuid(), "Informational D", "INFORMATIONAL D", signupAt);
            editedPrimaryId = editedPrimary.Id;
            db.AddRange(secondary, informational, secondaryCharacter, editedPrimary, informationalCharacter);
            var ownerId = participant.AccountId!.Value;
            var characterIds = new[] { originalPrimary.OsrsCharacterId, secondaryCharacter.Id, editedPrimary.Id, informationalCharacter.Id };
            db.AddRange(characterIds.Select((id, index) => new AccountOsrsCharacter(Guid.NewGuid(), ownerId, id, ownerId, index == 0, index, null, null, signupAt)));
            await db.SaveChangesAsync();
            var answers = new Dictionary<Guid, AuthenticatedAccountAnswer>
            {
                [primary.Id] = new(originalPrimary.OsrsCharacterId, 40m),
                [secondary.Id] = new(secondaryCharacter.Id, 20m),
                [informational.Id] = new(informationalCharacter.Id, null)
            };
            var initial = await Service(db, new FixedTimeProvider(signupAt)).SignUpAuthenticatedAsync(new(seed.EventId, ownerId, answers, new Dictionary<Guid, string>(), null, participant.ResponseVersion));
            Assert.True(initial.Succeeded, initial.Error);
            answers[primary.Id] = new(editedPrimary.Id, 50m);
            var edited = await Service(db, new FixedTimeProvider(signupAt.AddMinutes(1))).SignUpAuthenticatedAsync(new(seed.EventId, ownerId, answers, new Dictionary<Guid, string>(), null, participant.ResponseVersion));
            Assert.True(edited.Succeeded, edited.Error);
            db.ChangeTracker.Clear();
            Assert.Equal(editedPrimary.Id, await db.SignupAnswers.Where(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == primary.Id).Select(x => x.OsrsCharacterId).SingleAsync());
            var assignments = await db.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id).OrderBy(x => x.RegistrationOrder).ToListAsync();
            Assert.NotNull(assignments.Single(x => x.OsrsCharacterId == originalPrimary.OsrsCharacterId).ReleasedAt);
            Assert.Equal(secondaryCharacter.Id, assignments.First(x => x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing).OsrsCharacterId);
            Assert.Equal(EventCharacterRole.Informational, assignments.Single(x => x.OsrsCharacterId == informationalCharacter.Id).EventRole);
            assignmentsBefore = JsonSerializer.Serialize(assignments);

            var item = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            item.CloseSignups(seed.Now.AddDays(-1));
            item.SetDraftLocked(true);
            item.StartEvent(seed.Now.AddHours(-1));
            var credited = await new EvidenceAuthority(db).ResolveCreditedCharacterAsync(seed.EventId, seed.DepartedParticipantId, seed.Now.AddMinutes(-10));
            historicalCreditedCharacterId = credited.OsrsCharacterId;
            historicalCreditedCharacterName = credited.Name;
            var submission = new Submission(Guid.NewGuid(), seed.EventId, seed.TeamId, Guid.NewGuid(), Guid.NewGuid(), null,
                seed.DepartedParticipantId, credited.OsrsCharacterId, credited.Name, seed.LeaderOwnerId, 1, seed.Now.AddMinutes(-10), null, null);
            submission.Approve(1, seed.Now.AddMinutes(-9));
            db.AddRange(submission, new SubmissionContribution(Guid.NewGuid(), submission.Id, seed.TeamId, submission.RequirementId, null, seed.DepartedParticipantId, 1, seed.Now.AddMinutes(-9)));
            await db.SaveChangesAsync();
        }
        async Task<string> CreditHistoryAsync()
        {
            await using var db = new ApplicationDbContext(options);
            return JsonSerializer.Serialize(new
            {
                Submissions = await db.Submissions.OrderBy(x => x.Id).ToListAsync(),
                Contributions = await db.SubmissionContributions.OrderBy(x => x.Id).ToListAsync(),
                Activations = await db.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId != seed.WaitingParticipantId).OrderBy(x => x.Id).ToListAsync(),
                Picks = await db.DraftPicks.OrderBy(x => x.Id).ToListAsync()
            });
        }
        historyBefore = await CreditHistoryAsync();
        var before = await StateHashAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var withdrawn = await Service(db, new FixedTimeProvider(seed.Now)).WithdrawLiveAsync(new(seed.EventId, seed.DepartedParticipantId, seed.AdminId, "admin", seed.DepartedMembershipVersion));
            Assert.False(withdrawn.Succeeded);
            Assert.Contains("Roster membership is fixed after the event first goes Live.", withdrawn.Error, StringComparison.Ordinal);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var replaced = await Service(db, new FixedTimeProvider(seed.Now.AddMinutes(2))).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", seed.WaitingParticipantId));
            Assert.False(replaced.Succeeded);
            Assert.Contains("replacements and vacancies are retired", replaced.Error, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(before, await StateHashAsync());
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == seed.WaitingParticipantId).ToListAsync());
        Assert.Equal(assignmentsBefore, JsonSerializer.Serialize(await verify.EventParticipantCharacters.Where(x => x.EventParticipantId == seed.WaitingParticipantId).OrderBy(x => x.RegistrationOrder).ToListAsync()));
        Assert.Equal(historyBefore, await CreditHistoryAsync());
        Assert.Contains(editedPrimaryId, await verify.EventParticipantCharacters
            .Where(x => x.EventParticipantId == seed.WaitingParticipantId && x.ReleasedAt == null)
            .Select(x => x.OsrsCharacterId)
            .ToListAsync());
        var retainedSubmission = await verify.Submissions.SingleAsync(x => x.EventId == seed.EventId && x.CreditedParticipantId == seed.DepartedParticipantId);
        Assert.Equal(historicalCreditedCharacterId, retainedSubmission.CreditedOsrsCharacterId);
        Assert.Equal(historicalCreditedCharacterName, retainedSubmission.CreditedCharacterName);
        Assert.Equal(SubmissionStatus.Approved, retainedSubmission.Status);
    }

    [Fact]
    public async Task InternalReplacementIsValidatedAndStartsWithNoInheritedDraftState()
    {
        var seed = await SeedAsync();
        var clock = new FixedTimeProvider(seed.Now);
        Guid questionId;
        var character = new OsrsCharacter(Guid.NewGuid(), "Internal replacement", "INTERNAL REPLACEMENT", seed.Now);
        await using (var db = new ApplicationDbContext(options))
        {
            questionId = await db.SignupQuestions.Where(x => x.EventId == seed.EventId && x.SystemField == SignupSystemField.PrimaryRegularAccount).Select(x => x.Id).SingleAsync();
            db.Add(character);
            await db.SaveChangesAsync();
        }
        var before = await StateHashAsync();
        List<Guid> draftPickIdsBefore;
        await using (var db = new ApplicationDbContext(options))
        {
            draftPickIdsBefore = await db.DraftPicks.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var withdrawn = await Service(db, clock).WithdrawLiveAsync(new(seed.EventId, seed.DepartedParticipantId, seed.AdminId, "admin", seed.DepartedMembershipVersion));
            Assert.False(withdrawn.Succeeded);
            Assert.Contains("Roster membership is fixed after the event first goes Live.", withdrawn.Error, StringComparison.Ordinal);
        }

        var internalRequest = new AdminParticipantChangeRequest(
            seed.EventId,
            null,
            seed.AdminId,
            "admin",
            null,
            new Dictionary<Guid, AdminAccountAnswer> { [questionId] = new("Internal replacement", 12m) },
            new Dictionary<Guid, string>());
        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Service(db, clock).ReplaceVacancyAsync(new(seed.EventId, seed.DepartedMembershipId, seed.AdminId, "admin", null, internalRequest));
            Assert.False(result.Succeeded);
            Assert.Contains("replacements and vacancies are retired", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(before, await StateHashAsync());
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.EventParticipants.Where(x => x.Source == SignupSource.AdminCreated).ToListAsync());
        Assert.Null(await verify.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.LeftAt).SingleAsync());
        Assert.Equal(seed.DepartedMembershipVersion, await verify.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync());
        var draftPickIdsAfter = await verify.DraftPicks.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        Assert.Equal(draftPickIdsBefore, draftPickIdsAfter);
        Assert.Empty(await verify.WaitingListPromotionFollowUps.ToListAsync());
        Assert.Empty(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == seed.WaitingParticipantId).ToListAsync());
        Assert.Empty(await verify.PersonalNotifications.ToListAsync());
        Assert.Empty(await verify.AuditEntries.ToListAsync());
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
            var unsupported = new BingoEvent(Guid.NewGuid(), "Awaiting route event", $"awaiting-route-{Guid.NewGuid():N}", "UTC", seed.AdminId, seed.Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
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
        Assert.DoesNotContain("Manage live participant", participants, StringComparison.Ordinal);

        var liveBefore = await StateHashAsync();
        var liveParticipant = await client.GetStringAsync($"/Admin/Events/Participant/{seed.EventId}/Participants/{externalParticipantId}");
        Assert.Equal(string.Empty, InputValue(liveParticipant, "ExpectedMembershipVersion"));
        Assert.DoesNotContain("Remove participant", liveParticipant, StringComparison.Ordinal);
        using var liveWithdrawal = await client.PostAsync($"/Admin/Events/Participant/{seed.EventId}/Participants/{externalParticipantId}?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ExpectedMembershipVersion"] = externalMembershipVersion.ToString(CultureInfo.InvariantCulture),
            ["ConfirmLifecycleAction"] = "true",
            ["__RequestVerificationToken"] = AntiforgeryToken(liveParticipant)
        }));
        Assert.Equal(HttpStatusCode.Redirect, liveWithdrawal.StatusCode);
        var liveDestination = await client.GetStringAsync(liveWithdrawal.Headers.Location!.OriginalString);
        Assert.Contains("Roster membership is fixed after the event first goes Live.", liveDestination, StringComparison.Ordinal);
        Assert.Equal(liveBefore, await StateHashAsync());

        var vacancyParticipant = await client.GetStringAsync($"/Admin/Events/Participant/{seed.EventId}/Participants/{seed.DepartedParticipantId}");
        Assert.DoesNotContain("Fill open vacancy", vacancyParticipant, StringComparison.Ordinal);

        var awaitingBefore = await StateHashAsync();
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
        Assert.Equal(awaitingBefore, await StateHashAsync());

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == externalParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Null(await verify.TeamMemberships.Where(x => x.EventParticipantId == externalParticipantId).Select(x => x.LeftAt).SingleAsync());
        Assert.Equal(externalMembershipVersion, await verify.TeamMemberships.Where(x => x.EventParticipantId == externalParticipantId).Select(x => x.Version).SingleAsync());
        Assert.Equal(SignupStatus.Confirmed, await verify.EventParticipants.Where(x => x.Id == unsupportedParticipantId).Select(x => x.SignupStatus).SingleAsync());
        Assert.Null(await verify.TeamMemberships.Where(x => x.EventParticipantId == unsupportedParticipantId).Select(x => x.LeftAt).SingleAsync());
        Assert.Empty(await verify.PersonalNotifications.ToListAsync());
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    private async Task<Seed> SeedAsync(bool startLive = true)
    {
        var now = new DateTimeOffset(2026, 8, 2, 12, 34, 27, TimeSpan.Zero);
        await using var db = new ApplicationDbContext(options);
        var admin = Website("pass92-admin", GlobalRole.Admin, now);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, incrementVersion: false);
        var superAdmin = Website("pass92-super", GlobalRole.SuperAdmin, now);
        var leaderOwner = Website("pass92-leader", GlobalRole.User, now);
        var departedOwner = Website("pass92-departed", GlobalRole.User, now);
        var waitingOwner = Website("pass92-waiting", GlobalRole.User, now);
        var item = new BingoEvent(Guid.NewGuid(), "Pass 9.2", $"pass92-{Guid.NewGuid():N}", "UTC", admin.Id, now.AddDays(-1), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureInitialSchedule(now.AddDays(-2), now.AddDays(-1), now.AddDays(-1), now.AddHours(-1), now.AddHours(4), 3);
        item.OpenSignups(now.AddDays(-2));
        if (startLive)
        {
            item.CloseSignups(now.AddDays(-1));
            item.SetDraftLocked(true);
            item.StartEvent(now.AddHours(-1));
        }
        var form = new SignupForm(Guid.NewGuid(), item.Id, now.AddDays(-2));
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Main account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var team = new Team(Guid.NewGuid(), item.Id, "Live team", "live-team", TeamFormationType.Drafted, null, true, now.AddDays(-1));
        var departed = Participant(item.Id, departedOwner, "Departed", SignupStatus.Confirmed, 1, now.AddDays(-2));
        var leader = Participant(item.Id, leaderOwner, "Leader", SignupStatus.Confirmed, 2, now.AddDays(-2).AddMinutes(1));
        var waiting = Participant(item.Id, waitingOwner, "Waiting", SignupStatus.WaitingList, 3, now.AddDays(-2).AddMinutes(2));
        var departedAssignment = Assignment(item.Id, departed, "Departed main", departedOwner.Id, 20m, now.AddDays(-2), primary.Id);
        var leaderAssignment = Assignment(item.Id, leader, "Leader main", leaderOwner.Id, 30m, now.AddDays(-2), primary.Id);
        var waitingAssignment = Assignment(item.Id, waiting, "Waiting main", waitingOwner.Id, 40m, now.AddDays(-2), primary.Id);
        var departedMembership = new TeamMembership(Guid.NewGuid(), team.Id, departed.Id, TeamMembershipRole.Captain, now.AddDays(-1), null, "Draft pick");
        var leaderMembership = new TeamMembership(Guid.NewGuid(), team.Id, leader.Id, TeamMembershipRole.Captain, now.AddDays(-1), null, "Draft pick");
        var session = new DraftSession(Guid.NewGuid(), item.Id, 2);
        var pick = new DraftPick(Guid.NewGuid(), session.Id, team.Id, departed.Id, 1, 1, now.AddDays(-1));
        db.AddRange(admin, superAdmin, leaderOwner, departedOwner, waitingOwner, item, form, primary, team, departed, leader, waiting,
            departedAssignment.Character, departedAssignment.Assignment, leaderAssignment.Character, leaderAssignment.Assignment, waitingAssignment.Character, waitingAssignment.Assignment,
            departedMembership, leaderMembership, session, pick,
            new EventParticipantCharacterSwap(Guid.NewGuid(), item.Id, departed.Id, null, departedAssignment.Assignment.OsrsCharacterId, now.AddHours(-1), now.AddHours(-1), null, null),
            new EventParticipantCharacterSwap(Guid.NewGuid(), item.Id, leader.Id, null, leaderAssignment.Assignment.OsrsCharacterId, now.AddHours(-1), now.AddHours(-1), null, null));
        await db.SaveChangesAsync();
        return new(item.Id, admin.Id, leaderOwner.Id, team.Id, departed.Id, waiting.Id, departedMembership.Id, departedMembership.Version, now);
    }

    private static SignupService Service(ApplicationDbContext db, TimeProvider clock) =>
        new(db, new SecretHasher(), clock, accountValidation: new SuccessfulWiseOldManAccountValidation());
    private static EventParticipant Participant(Guid eventId, Account owner, string name, SignupStatus status, long sequence, DateTimeOffset now)
    {
        var participant = new EventParticipant(Guid.NewGuid(), eventId, status, sequence, now, SignupSource.Website);
        participant.AssignOwner(owner);
        return participant;
    }
    private static (OsrsCharacter Character, EventParticipantCharacter Assignment) Assignment(Guid eventId, EventParticipant participant, string name, Guid ownerId, decimal ehb, DateTimeOffset now, Guid? primaryQuestionId = null)
    {
        var character = new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), eventId, participant.Id, character.Id, 0, now, ownerId, primaryQuestionId, EventCharacterRole.Playing, ehb, EhbSource.Manual, null);
        return (character, assignment);
    }
    private static Account Website(string name, GlobalRole role, DateTimeOffset now) { var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now); account.SetGlobalRole(role); return account; }
    private sealed record Seed(Guid EventId, Guid AdminId, Guid LeaderOwnerId, Guid TeamId, Guid DepartedParticipantId, Guid WaitingParticipantId, Guid DepartedMembershipId, long DepartedMembershipVersion, DateTimeOffset Now);
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private async Task<string> StateHashAsync()
    {
        var tables = new[]
        {
            "events", "event_participants", "event_participant_characters", "osrs_characters", "signup_answers",
            "team_memberships", "team_membership_role_transitions", "draft_sessions", "draft_picks",
            "draft_publication_cycles", "draft_publication_rosters", "audit_entries", "personal_notifications",
            "waiting_list_promotion_follow_ups", "event_participant_character_swaps", "submissions",
            "submission_contributions", "event_competition_synchronizations"
        };
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        var state = new StringBuilder();
        foreach (var table in tables)
        {
            await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(row) ORDER BY to_jsonb(row)::text), '[]'::jsonb)::text FROM {table} row", connection);
            state.Append(table).Append(':').Append(await command.ExecuteScalarAsync());
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state.ToString())));
    }
    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private static string InputValue(string page, string name) => Regex.Match(page, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
}
