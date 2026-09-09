using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class CaptainScopedNavigationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_captain_scoped_navigation").WithUsername("bingo").WithPassword("bingo_test_password").Build();
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
    public async Task NamedSubmissionGetRedirectsToCanonicalDetailsRoute()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Website("submission-handler-get", now);
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "password"), false, now, false);
        var live = LiveEvent(owner.Id, "Submission handler get", "submission-handler-get", now);
        var team = new Team(Guid.NewGuid(), live.Id, "Submission handler team", "submission-handler-team", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website);
        participant.AssignOwner(owner);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var character = new OsrsCharacter(Guid.NewGuid(), "Submission handler player", "SUBMISSION HANDLER PLAYER", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), live.Id, participant.Id, character.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var board = new Board(Guid.NewGuid(), live.Id, "Submission handler board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Submission handler tile", "Description", "", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "Requirement", true);
        var submission = new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, owner.Id, 1, now, null, null);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(owner, live, team, participant, membership, character, assignment, board, tile, requirement, submission);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement]);
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, owner.LoginName);

        var canonicalRoute = $"/Submissions/{submission.Id}?eventId={live.Id}&teamId={team.Id}";
        using var response = await client.GetAsync($"/Captain/Submissions/{submission.Id}?handler=Correct&eventId={live.Id}&teamId={team.Id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(canonicalRoute, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task SubmissionDetailsCanonicalizeByActorAndKeepNotificationReadState()
    {
        var now = DateTimeOffset.UtcNow;
        var participantOwner = Website("submission-canonical-participant", now);
        var captain = Website("submission-canonical-captain", now);
        var coCaptain = Website("submission-canonical-co-captain", now);
        var former = Website("submission-canonical-former", now);
        var unrelated = Website("submission-canonical-unrelated", now);
        var globalOnly = Website("submission-canonical-global-only", now);
        participantOwner.SetGlobalRole(GlobalRole.Admin);
        captain.SetGlobalRole(GlobalRole.Admin);
        coCaptain.SetGlobalRole(GlobalRole.SuperAdmin);
        globalOnly.SetGlobalRole(GlobalRole.Admin);
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "submission-canonical-emergency", "SUBMISSION-CANONICAL-EMERGENCY", now);
        foreach (var account in new[] { participantOwner, captain, coCaptain, former, unrelated, globalOnly, emergency })
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, false);
        emergency.Enable();
        var live = LiveEvent(participantOwner.Id, "Submission canonical", "submission-canonical", now);
        var team = new Team(Guid.NewGuid(), live.Id, "Submission canonical team", "submission-canonical-team", TeamFormationType.Drafted, null, true);
        team.Finalize(now.AddMinutes(-1));
        var crossTeam = new Team(Guid.NewGuid(), live.Id, "Submission canonical other team", "submission-canonical-other-team", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website);
        participant.AssignOwner(participantOwner);
        var captainParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 2, now.AddDays(-1), SignupSource.AdminCreated);
        captainParticipant.AssignOwner(captain);
        var coCaptainParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 3, now.AddDays(-1), SignupSource.AdminCreated);
        coCaptainParticipant.AssignOwner(coCaptain);
        var formerParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 4, now.AddDays(-1), SignupSource.AdminCreated);
        formerParticipant.AssignOwner(former);
        var crossTeamParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 5, now.AddDays(-1), SignupSource.AdminCreated);
        crossTeamParticipant.AssignOwner(unrelated);
        var participantMembership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var captainMembership = new TeamMembership(Guid.NewGuid(), team.Id, captainParticipant.Id, TeamMembershipRole.Captain, now.AddDays(-1), null, "test");
        var coCaptainMembership = new TeamMembership(Guid.NewGuid(), team.Id, coCaptainParticipant.Id, TeamMembershipRole.CoCaptain, now.AddDays(-1), null, "test");
        var formerMembership = new TeamMembership(Guid.NewGuid(), team.Id, formerParticipant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        formerMembership.Leave(now.AddHours(-1), "Former member");
        var crossTeamMembership = new TeamMembership(Guid.NewGuid(), crossTeam.Id, crossTeamParticipant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var emergencyAccess = new AccountEventAccess(Guid.NewGuid(), emergency.Id, live.Id, team.Id, null, now.AddHours(-1), null, null);
        emergencyAccess.Enable();
        var character = new OsrsCharacter(Guid.NewGuid(), "Submission canonical player", "SUBMISSION CANONICAL PLAYER", now);
        var captainCharacter = new OsrsCharacter(Guid.NewGuid(), "Submission canonical captain", "SUBMISSION CANONICAL CAPTAIN", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), live.Id, participant.Id, character.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var captainAssignment = new EventParticipantCharacter(Guid.NewGuid(), live.Id, captainParticipant.Id, captainCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var board = new Board(Guid.NewGuid(), live.Id, "Submission canonical board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Submission canonical tile", "Description", "", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "Requirement", true);
        var submission = new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, participantOwner.Id, 1, now, null, null);
        var overlappingOwnerSubmission = new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, null, captainParticipant.Id, captainCharacter.Id, captainCharacter.DisplayName, captain.Id, 1, now, null, null);
        var notification = new PersonalNotification(Guid.NewGuid(), participantOwner.Id, "evidence.rejected", "Please review the submission.", $"/Submissions/{submission.Id}", now);
        var coCaptainNotification = new PersonalNotification(Guid.NewGuid(), coCaptain.Id, "evidence.rejected", "Please review the submission.", $"/Submissions/{submission.Id}", now);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(participantOwner, captain, coCaptain, former, unrelated, globalOnly, emergency, live, team, crossTeam,
                participant, captainParticipant, coCaptainParticipant, formerParticipant, crossTeamParticipant,
                participantMembership, captainMembership, coCaptainMembership, formerMembership, crossTeamMembership,
                emergencyAccess, character, captainCharacter, assignment, captainAssignment, board, tile, requirement,
                submission, overlappingOwnerSubmission, notification, coCaptainNotification);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement]);
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var participantClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var captainClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var coCaptainClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var emergencyClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var formerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var unrelatedClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var globalOnlyClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(participantClient, participantOwner.LoginName);
        await LoginAsync(captainClient, captain.LoginName);
        await LoginAsync(coCaptainClient, coCaptain.LoginName);
        await LoginAsync(emergencyClient, emergency.LoginName);
        await LoginAsync(formerClient, former.LoginName);
        await LoginAsync(unrelatedClient, unrelated.LoginName);
        await LoginAsync(globalOnlyClient, globalOnly.LoginName);

        using var participantBoard = await participantClient.GetAsync($"/Events/{live.Slug}/Board/{team.Slug}");
        var participantBoardHtml = await participantBoard.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, participantBoard.StatusCode);
        Assert.Contains(">submissions<", participantBoardHtml, StringComparison.Ordinal);
        Assert.Contains(">Admin<", participantBoardHtml, StringComparison.Ordinal);

        using var captainBoard = await captainClient.GetAsync($"/Events/{live.Slug}/Board/{team.Slug}");
        var captainBoardHtml = await captainBoard.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, captainBoard.StatusCode);
        Assert.Contains(">Captain<", captainBoardHtml, StringComparison.Ordinal);
        Assert.Contains(">Admin<", captainBoardHtml, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Submissions?eventId={live.Id}&amp;teamId={team.Id}\"", captainBoardHtml, StringComparison.Ordinal);

        using var coCaptainBoard = await coCaptainClient.GetAsync($"/Events/{live.Slug}/Board/{team.Slug}");
        var coCaptainBoardHtml = await coCaptainBoard.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, coCaptainBoard.StatusCode);
        Assert.Contains(">Captain<", coCaptainBoardHtml, StringComparison.Ordinal);
        Assert.Contains(">Admin<", coCaptainBoardHtml, StringComparison.Ordinal);

        using var globalBoard = await globalOnlyClient.GetAsync($"/Events/{live.Slug}/Board/{team.Slug}");
        var globalBoardHtml = await globalBoard.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, globalBoard.StatusCode);
        Assert.DoesNotContain(">Captain<", globalBoardHtml, StringComparison.Ordinal);
        Assert.DoesNotContain($"href=\"/Submissions?eventId={live.Id}&amp;teamId={team.Id}\"", globalBoardHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await globalOnlyClient.GetAsync($"/Submissions?eventId={live.Id}&teamId={team.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await captainClient.GetAsync($"/Submissions?eventId={live.Id}&teamId={crossTeam.Id}")).StatusCode);

        var scope = $"?eventId={live.Id}&teamId={team.Id}";
        using var participantRoute = await participantClient.GetAsync($"/Captain/Submissions/{submission.Id}{scope}");
        Assert.Equal(HttpStatusCode.Redirect, participantRoute.StatusCode);
        Assert.Equal($"/Submissions/{submission.Id}{scope}", participantRoute.Headers.Location?.OriginalString);

        using var captainRoute = await captainClient.GetAsync($"/Submissions/{submission.Id}{scope}");
        Assert.Equal(HttpStatusCode.OK, captainRoute.StatusCode);

        using var participantCanonicalRoute = await participantClient.GetAsync($"/Submissions/{submission.Id}{scope}");
        Assert.Equal(HttpStatusCode.OK, participantCanonicalRoute.StatusCode);
        using var teammateRoute = await participantClient.GetAsync($"/Submissions/{overlappingOwnerSubmission.Id}{scope}");
        Assert.Equal(HttpStatusCode.OK, teammateRoute.StatusCode);
        var teammateHtml = await teammateRoute.Content.ReadAsStringAsync();
        Assert.Contains("captain-submission-readonly", teammateHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Save changes", teammateHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Withdraw mistaken submission", teammateHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Submit linked resubmission", teammateHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await coCaptainClient.GetAsync($"/Submissions/{submission.Id}{scope}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await emergencyClient.GetAsync($"/Submissions/{submission.Id}{scope}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await formerClient.GetAsync($"/Submissions/{submission.Id}{scope}")).StatusCode);

        using var overlappingOwnerRoute = await captainClient.GetAsync($"/Captain/Submissions/{overlappingOwnerSubmission.Id}{scope}");
        Assert.Equal(HttpStatusCode.Redirect, overlappingOwnerRoute.StatusCode);
        Assert.Equal($"/Submissions/{overlappingOwnerSubmission.Id}{scope}", overlappingOwnerRoute.Headers.Location?.OriginalString);

        using var unrelatedAlias = await unrelatedClient.GetAsync($"/Captain/Submissions/{submission.Id}{scope}");
        Assert.Equal(HttpStatusCode.Redirect, unrelatedAlias.StatusCode);
        Assert.Equal($"/Submissions/{submission.Id}{scope}", unrelatedAlias.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelatedClient.GetAsync(unrelatedAlias.Headers.Location!.OriginalString!)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelatedClient.GetAsync($"/Submissions/{submission.Id}{scope}")).StatusCode);

        using var notificationRoute = await participantClient.GetAsync($"/notifications?read={notification.Id}");
        Assert.Equal(HttpStatusCode.Redirect, notificationRoute.StatusCode);
        Assert.Equal($"/Submissions/{submission.Id}", notificationRoute.Headers.Location?.OriginalString);
        using var coCaptainNotificationRoute = await coCaptainClient.GetAsync($"/notifications?read={coCaptainNotification.Id}");
        Assert.Equal(HttpStatusCode.Redirect, coCaptainNotificationRoute.StatusCode);
        Assert.Equal($"/Submissions/{submission.Id}", coCaptainNotificationRoute.Headers.Location?.OriginalString);
        await using var verify = new ApplicationDbContext(options);
        Assert.NotNull(await verify.PersonalNotifications.Where(item => item.Id == notification.Id).Select(item => item.ReadAt).SingleAsync());
        Assert.NotNull(await verify.PersonalNotifications.Where(item => item.Id == coCaptainNotification.Id).Select(item => item.ReadAt).SingleAsync());
    }

    [Fact]
    public async Task ParticipantHistoryDetailsAndWithdrawalPreserveSelectedEventAndTeamScope()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Website("scoped-navigation-participant", now);
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "password"), false, now, false);
        var first = LiveEvent(owner.Id, "Scoped first", "scoped-first", now);
        var second = LiveEvent(owner.Id, "Scoped second", "scoped-second", now);
        var firstTeam = new Team(Guid.NewGuid(), first.Id, "First team", "scoped-first-team", TeamFormationType.Drafted, null, true);
        var secondTeam = new Team(Guid.NewGuid(), second.Id, "Second team", "scoped-second-team", TeamFormationType.Drafted, null, true);
        var firstParticipant = new EventParticipant(Guid.NewGuid(), first.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website);
        var secondParticipant = new EventParticipant(Guid.NewGuid(), second.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website);
        firstParticipant.AssignOwner(owner);
        secondParticipant.AssignOwner(owner);
        var firstMembership = new TeamMembership(Guid.NewGuid(), firstTeam.Id, firstParticipant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var secondMembership = new TeamMembership(Guid.NewGuid(), secondTeam.Id, secondParticipant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var firstCharacter = new OsrsCharacter(Guid.NewGuid(), "Scoped first character", "SCOPED FIRST CHARACTER", now);
        var secondCharacter = new OsrsCharacter(Guid.NewGuid(), "Scoped second character", "SCOPED SECOND CHARACTER", now);
        var firstAssignment = new EventParticipantCharacter(Guid.NewGuid(), first.Id, firstParticipant.Id, firstCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var secondAssignment = new EventParticipantCharacter(Guid.NewGuid(), second.Id, secondParticipant.Id, secondCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var firstBoard = new Board(Guid.NewGuid(), first.Id, "First board", 1, 1);
        var secondBoard = new Board(Guid.NewGuid(), second.Id, "Second board", 1, 1);
        var firstTile = new BoardTile(Guid.NewGuid(), firstBoard.Id, Guid.NewGuid(), 0, 0, "First tile", "First description", "", 1m);
        var secondTile = new BoardTile(Guid.NewGuid(), secondBoard.Id, Guid.NewGuid(), 0, 0, "Second tile", "Second description", "", 1m);
        var firstRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), firstTile.Id, 0, 1, true, false, "First requirement", true);
        var secondRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), secondTile.Id, 0, 1, true, false, "Second requirement", true);
        var submission = new Submission(Guid.NewGuid(), first.Id, firstTeam.Id, firstTile.Id, firstRequirement.Id, null, firstParticipant.Id, firstCharacter.Id, firstCharacter.DisplayName, owner.Id, 1, now, null, null);
        var secondSubmission = new Submission(Guid.NewGuid(), second.Id, secondTeam.Id, secondTile.Id, secondRequirement.Id, null, secondParticipant.Id, secondCharacter.Id, secondCharacter.DisplayName, owner.Id, 1, now, null, null);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(owner, first, second, firstTeam, secondTeam, firstParticipant, secondParticipant, firstMembership, secondMembership,
                firstCharacter, secondCharacter, firstAssignment, secondAssignment, firstBoard, secondBoard, firstTile, secondTile,
                firstRequirement, secondRequirement, submission, secondSubmission);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, firstBoard, now, [firstTile], [firstRequirement]);
            await BoardApprovalFixture.PublishAsync(db, secondBoard, now, [secondTile], [secondRequirement]);
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, owner.LoginName);

        using var captainAlias = await client.GetAsync("/Captain");
        Assert.Equal(HttpStatusCode.Redirect, captainAlias.StatusCode);
        Assert.Equal("/Submissions", captainAlias.Headers.Location?.OriginalString);
        using var captainLedgerAlias = await client.GetAsync($"/Captain?handler=Ledger&eventId={first.Id}&teamId={firstTeam.Id}");
        Assert.Equal(HttpStatusCode.Redirect, captainLedgerAlias.StatusCode);
        Assert.Equal($"/Submissions?eventId={first.Id}&teamId={firstTeam.Id}", captainLedgerAlias.Headers.Location?.OriginalString);
        using var crossScopeAlias = await client.GetAsync($"/Captain?eventId={second.Id}&teamId={firstTeam.Id}");
        Assert.Equal(HttpStatusCode.Redirect, crossScopeAlias.StatusCode);
        Assert.Equal($"/Submissions?eventId={second.Id}&teamId={firstTeam.Id}", crossScopeAlias.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(crossScopeAlias.Headers.Location!.OriginalString!)).StatusCode);

        var detailsRoute = $"/Submissions/{submission.Id}?eventId={first.Id}&teamId={firstTeam.Id}";
        using var details = await client.GetAsync(detailsRoute);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailsHtml = await details.Content.ReadAsStringAsync();
        Assert.Contains($"href=\"/Submissions?eventId={first.Id}&amp;teamId={firstTeam.Id}\"", detailsHtml, StringComparison.Ordinal);
        var withdrawAction = Regex.Matches(detailsHtml, $"action=\\\"([^\\\"]*{submission.Id}[^\\\"]*)\\\"").Select(x => x.Value).Single(x => x.Contains("handler=Withdraw", StringComparison.Ordinal));
        Assert.Contains($"eventId={first.Id}", withdrawAction, StringComparison.Ordinal);
        Assert.Contains($"teamId={firstTeam.Id}", withdrawAction, StringComparison.Ordinal);

        var token = Regex.Match(detailsHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        using var withdrawn = await client.PostAsync($"/Submissions/{submission.Id}?handler=Withdraw&eventId={first.Id}&teamId={firstTeam.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ExpectedVersion"] = "1",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, withdrawn.StatusCode);
        Assert.Equal($"/Submissions?eventId={first.Id}&teamId={firstTeam.Id}", withdrawn.Headers.Location?.OriginalString);

        await using var verification = new ApplicationDbContext(options);
        Assert.Equal(SubmissionStatus.Withdrawn, await verification.Submissions.Where(x => x.Id == submission.Id).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Pending, await verification.Submissions.Where(x => x.Id == secondSubmission.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task LiveCaptainRouteRendersLedgerPlayerFilterAgainstPostgreSql()
    {
        var now = DateTimeOffset.UtcNow;
        var captain = Website("captain-ledger-route", now);
        captain.SetPassword(new PasswordHasher<Account>().HashPassword(captain, "password"), false, now, false);
        var live = LiveEvent(captain.Id, "Captain ledger live", "captain-ledger-live", now);
        var team = new Team(Guid.NewGuid(), live.Id, "Captain ledger team", "captain-ledger-team", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website);
        var secondParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 2, now.AddDays(-1), SignupSource.Website);
        participant.AssignOwner(captain);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Captain, now.AddDays(-1), null, "test");
        var secondMembership = new TeamMembership(Guid.NewGuid(), team.Id, secondParticipant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var character = new OsrsCharacter(Guid.NewGuid(), "Captain ledger player", "CAPTAIN LEDGER PLAYER", now);
        var secondCharacter = new OsrsCharacter(Guid.NewGuid(), "Second ledger player", "SECOND LEDGER PLAYER", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), live.Id, participant.Id, character.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var secondAssignment = new EventParticipantCharacter(Guid.NewGuid(), live.Id, secondParticipant.Id, secondCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var board = new Board(Guid.NewGuid(), live.Id, "Captain ledger board", 1, 2);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Captain ledger tile", "Description", "", 1m);
        var secondTile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 1, "Second ledger tile", "Description", "", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "Requirement", true);
        var secondRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), secondTile.Id, 0, 1, true, false, "Requirement", true);
        var drop = new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, Guid.NewGuid(), Guid.NewGuid(), "Captain ledger boss", "Captain ledger drop", "1/10", 0.1m, null, 1m);
        var secondDrop = new BoardRequirementDropSnapshot(Guid.NewGuid(), secondRequirement.Id, Guid.NewGuid(), Guid.NewGuid(), "Second ledger boss", "Second ledger drop", "1/10", 0.1m, null, 1m);
        var submissions = Enumerable.Range(0, 26)
            .Select(index => new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, drop.Id, participant.Id, character.Id,
                "Captain ledger player", captain.Id, 1, now.AddMinutes(-index), null, null))
            .ToList();
        var secondSubmission = new Submission(Guid.NewGuid(), live.Id, team.Id, secondTile.Id, secondRequirement.Id, secondDrop.Id, secondParticipant.Id, secondCharacter.Id,
                "Second ledger player", captain.Id, 1, now.AddMinutes(1), null, null);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(captain, live, team, participant, secondParticipant, membership, secondMembership, character, secondCharacter, assignment, secondAssignment, board, tile, secondTile, requirement, secondRequirement, drop, secondDrop);
            db.Submissions.AddRange(submissions);
            db.Submissions.Add(secondSubmission);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, now, [tile, secondTile], [requirement, secondRequirement], [drop, secondDrop]);
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, captain.LoginName);

        using var response = await client.GetAsync($"/Submissions?eventId={live.Id}&teamId={team.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"search\"", html, StringComparison.Ordinal);
        Assert.Contains("SEARCH SUBMISSIONS", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Events/{live.Slug}/Board/{team.Slug}\"", html, StringComparison.Ordinal);
        Assert.Contains("Second ledger player", html, StringComparison.Ordinal);
        Assert.Contains("Captain ledger drop", html, StringComparison.Ordinal);
        Assert.Contains("Search drops, players or tiles…", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"status\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"tile\"", html, StringComparison.Ordinal);
        Assert.Contains("ledgerPage=2", html, StringComparison.Ordinal);

        using var pageTwo = await client.GetAsync($"/Submissions?eventId={live.Id}&teamId={team.Id}&ledgerPage=2");
        Assert.Equal(HttpStatusCode.OK, pageTwo.StatusCode);
        var pageTwoHtml = await pageTwo.Content.ReadAsStringAsync();
        Assert.Contains("Page 2 of 2", pageTwoHtml, StringComparison.Ordinal);
        Assert.Contains("<td>Captain ledger player</td>", pageTwoHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>Second ledger player</td>", pageTwoHtml, StringComparison.Ordinal);

        using var dropSearch = await client.GetAsync($"/Submissions?eventId={live.Id}&teamId={team.Id}&search={Uri.EscapeDataString("CAPTAIN LEDGER DROP")}");
        Assert.Equal(HttpStatusCode.OK, dropSearch.StatusCode);
        var dropSearchHtml = await dropSearch.Content.ReadAsStringAsync();
        Assert.Contains("Captain ledger drop", dropSearchHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>Second ledger player</td>", dropSearchHtml, StringComparison.Ordinal);

        using var tileSearch = await client.GetAsync($"/Submissions?eventId={live.Id}&teamId={team.Id}&search={Uri.EscapeDataString("CAPTAIN LEDGER TILE")}");
        Assert.Equal(HttpStatusCode.OK, tileSearch.StatusCode);
        var tileSearchHtml = await tileSearch.Content.ReadAsStringAsync();
        Assert.Contains("Captain ledger player", tileSearchHtml, StringComparison.Ordinal);

        using var search = await client.GetAsync($"/Submissions?eventId={live.Id}&teamId={team.Id}&search={Uri.EscapeDataString("SECOND LEDGER PLAYER")}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var searchHtml = await search.Content.ReadAsStringAsync();
        Assert.Contains("Second ledger player", searchHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>Captain ledger player</td>", searchHtml, StringComparison.Ordinal);

        using var combined = await client.GetAsync($"/Submissions?eventId={live.Id}&teamId={team.Id}&search=LEDGER&player={secondParticipant.Id}");
        Assert.Equal(HttpStatusCode.OK, combined.StatusCode);
        var combinedHtml = await combined.Content.ReadAsStringAsync();
        Assert.Contains("Second ledger player", combinedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<td>Captain ledger player</td>", combinedHtml, StringComparison.Ordinal);
        Assert.Contains($"player={secondParticipant.Id}", combinedHtml, StringComparison.Ordinal);

        using var partial = await client.GetAsync($"/Submissions?handler=Ledger&eventId={live.Id}&teamId={team.Id}&search=ledger&player={secondParticipant.Id}&ledgerPage=1");
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var partialHtml = await partial.Content.ReadAsStringAsync();
        Assert.Contains("data-captain-ledger-results", partialHtml, StringComparison.Ordinal);
        Assert.Contains("Second ledger player", partialHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("captain-workspace-heading", partialHtml, StringComparison.Ordinal);

        async Task<string> PartialSearch(string value, Guid? playerId = null)
        {
            var query = $"/Submissions?handler=Ledger&eventId={live.Id}&teamId={team.Id}&search={Uri.EscapeDataString(value)}";
            if (playerId is { } id) query += $"&player={id}";
            using var result = await client.GetAsync(query);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            return await result.Content.ReadAsStringAsync();
        }

        var partialDropSearchHtml = await PartialSearch("SECOND LEDGER DROP");
        Assert.Contains("Second ledger player", partialDropSearchHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Captain ledger player", partialDropSearchHtml, StringComparison.Ordinal);
        var partialTileSearchHtml = await PartialSearch("CAPTAIN LEDGER TILE");
        Assert.Contains("Captain ledger player", partialTileSearchHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Second ledger player", partialTileSearchHtml, StringComparison.Ordinal);
        var partialPlayerSearchHtml = await PartialSearch("SECOND LEDGER PLAYER");
        Assert.Contains("Second ledger player", partialPlayerSearchHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Captain ledger player", partialPlayerSearchHtml, StringComparison.Ordinal);
        var partialNonMatchHtml = await PartialSearch("NO SUCH LEDGER VALUE");
        Assert.DoesNotContain("Captain ledger player", partialNonMatchHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Second ledger player", partialNonMatchHtml, StringComparison.Ordinal);
        var partialCombinedHtml = await PartialSearch("LEDGER", secondParticipant.Id);
        Assert.Contains("Second ledger player", partialCombinedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Captain ledger player", partialCombinedHtml, StringComparison.Ordinal);

        using var filteredPageTwo = await client.GetAsync($"/Submissions?eventId={live.Id}&teamId={team.Id}&search=LEDGER&player={participant.Id}&ledgerPage=2");
        Assert.Equal(HttpStatusCode.OK, filteredPageTwo.StatusCode);
        var filteredPageTwoHtml = await filteredPageTwo.Content.ReadAsStringAsync();
        Assert.Contains("Page 2 of 2", filteredPageTwoHtml, StringComparison.Ordinal);
        Assert.Contains($"search=LEDGER", filteredPageTwoHtml, StringComparison.Ordinal);
        Assert.Contains($"player={participant.Id}", filteredPageTwoHtml, StringComparison.Ordinal);

    }

    [Fact]
    public async Task LiveCaptainHeaderNavigationIsScopedAndSharedByDesktopAndMobile()
    {
        var now = DateTimeOffset.UtcNow;
        var captain = Website("header-captain", now);
        var coCaptain = Website("header-co-captain", now);
        var participant = Website("header-participant", now);
        var admin = Website("header-admin", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "header-emergency", "HEADER-EMERGENCY", now);
        var awaitingCaptain = Website("header-awaiting-captain", now);
        var awaitingParticipant = Website("header-awaiting-participant", now);
        var ambiguousParticipant = Website("header-ambiguous-participant", now);
        var awaitingEmergency = Account.CreateEmergency(Guid.NewGuid(), "header-awaiting-emergency", "HEADER-AWAITING-EMERGENCY", now);
        foreach (var account in new[] { captain, coCaptain, participant, admin, awaitingCaptain, awaitingParticipant, ambiguousParticipant, emergency, awaitingEmergency })
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, false);
        emergency.Enable();
        awaitingEmergency.Enable();

        var live = LiveEvent(captain.Id, "Header live", "header-live", now);
        var liveTeam = new Team(Guid.NewGuid(), live.Id, "Header live team", "header-live-team", TeamFormationType.Drafted, null, true);
        var captainParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website);
        var coCaptainParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 2, now.AddDays(-1), SignupSource.Website);
        var ordinaryParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 3, now.AddDays(-1), SignupSource.Website);
        captainParticipant.AssignOwner(captain);
        coCaptainParticipant.AssignOwner(coCaptain);
        ordinaryParticipant.AssignOwner(participant);
        var liveMemberships = new[]
        {
            new TeamMembership(Guid.NewGuid(), liveTeam.Id, captainParticipant.Id, TeamMembershipRole.Captain, now.AddDays(-1), null, "test"),
            new TeamMembership(Guid.NewGuid(), liveTeam.Id, coCaptainParticipant.Id, TeamMembershipRole.CoCaptain, now.AddDays(-1), null, "test"),
            new TeamMembership(Guid.NewGuid(), liveTeam.Id, ordinaryParticipant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test")
        };
        var emergencyAccess = new AccountEventAccess(Guid.NewGuid(), emergency.Id, live.Id, liveTeam.Id, null, now.AddHours(-1), null, null);
        emergencyAccess.Enable();

        var finalized = LiveEvent(captain.Id, "Header finalized", "header-finalized", now);
        finalized.EndEvent(now.AddMinutes(-30));
        finalized.FinalizeResults(now.AddMinutes(-20));
        var finalizedTeam = new Team(Guid.NewGuid(), finalized.Id, "Header finalized team", "header-finalized-team", TeamFormationType.Drafted, null, true);
        var finalizedCaptainParticipant = new EventParticipant(Guid.NewGuid(), finalized.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        var finalizedOrdinaryParticipant = new EventParticipant(Guid.NewGuid(), finalized.Id, SignupStatus.Confirmed, 2, now, SignupSource.Website);
        finalizedCaptainParticipant.AssignOwner(captain);
        finalizedOrdinaryParticipant.AssignOwner(participant);
        var finalizedCaptainMembership = new TeamMembership(Guid.NewGuid(), finalizedTeam.Id, finalizedCaptainParticipant.Id, TeamMembershipRole.Captain, now, null, "test");
        var finalizedOrdinaryMembership = new TeamMembership(Guid.NewGuid(), finalizedTeam.Id, finalizedOrdinaryParticipant.Id, TeamMembershipRole.Participant, now, null, "test");

        var archived = LiveEvent(captain.Id, "Header archived", "header-archived", now);
        archived.EndEvent(now.AddMinutes(-29));
        archived.FinalizeResults(now.AddMinutes(-19));
        archived.Archive(now.AddMinutes(-18));
        var archivedTeam = new Team(Guid.NewGuid(), archived.Id, "Header archived team", "header-archived-team", TeamFormationType.Drafted, null, true);
        var archivedCaptainParticipant = new EventParticipant(Guid.NewGuid(), archived.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        var archivedOrdinaryParticipant = new EventParticipant(Guid.NewGuid(), archived.Id, SignupStatus.Confirmed, 2, now, SignupSource.Website);
        archivedCaptainParticipant.AssignOwner(captain);
        archivedOrdinaryParticipant.AssignOwner(participant);
        var archivedCaptainMembership = new TeamMembership(Guid.NewGuid(), archivedTeam.Id, archivedCaptainParticipant.Id, TeamMembershipRole.Captain, now, null, "test");
        var archivedOrdinaryMembership = new TeamMembership(Guid.NewGuid(), archivedTeam.Id, archivedOrdinaryParticipant.Id, TeamMembershipRole.Participant, now, null, "test");

        var awaiting = LiveEvent(captain.Id, "Header awaiting", "header-awaiting", now);
        awaiting.EndEvent(now.AddMinutes(-28));
        var awaitingTeam = new Team(Guid.NewGuid(), awaiting.Id, "Header awaiting team", "header-awaiting-team", TeamFormationType.Drafted, null, true);
        var awaitingCaptainParticipant = new EventParticipant(Guid.NewGuid(), awaiting.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        var awaitingOrdinaryParticipant = new EventParticipant(Guid.NewGuid(), awaiting.Id, SignupStatus.Confirmed, 2, now, SignupSource.Website);
        awaitingCaptainParticipant.AssignOwner(awaitingCaptain);
        awaitingOrdinaryParticipant.AssignOwner(awaitingParticipant);
        var awaitingCaptainMembership = new TeamMembership(Guid.NewGuid(), awaitingTeam.Id, awaitingCaptainParticipant.Id, TeamMembershipRole.Captain, now, null, "test");
        var awaitingOrdinaryMembership = new TeamMembership(Guid.NewGuid(), awaitingTeam.Id, awaitingOrdinaryParticipant.Id, TeamMembershipRole.Participant, now, null, "test");
        var awaitingEmergencyAccess = new AccountEventAccess(Guid.NewGuid(), awaitingEmergency.Id, awaiting.Id, awaitingTeam.Id, null, now.AddHours(-1), null, null);
        awaitingEmergencyAccess.Enable();

        var ambiguousLiveOne = LiveEvent(captain.Id, "Header ambiguous one", "header-ambiguous-one", now);
        var ambiguousTeamOne = new Team(Guid.NewGuid(), ambiguousLiveOne.Id, "Header ambiguous team one", "header-ambiguous-team-one", TeamFormationType.Drafted, null, true);
        var ambiguousParticipantOne = new EventParticipant(Guid.NewGuid(), ambiguousLiveOne.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        ambiguousParticipantOne.AssignOwner(ambiguousParticipant);
        var ambiguousMembershipOne = new TeamMembership(Guid.NewGuid(), ambiguousTeamOne.Id, ambiguousParticipantOne.Id, TeamMembershipRole.Participant, now, null, "test");
        var ambiguousLiveTwo = LiveEvent(captain.Id, "Header ambiguous two", "header-ambiguous-two", now);
        var ambiguousTeamTwo = new Team(Guid.NewGuid(), ambiguousLiveTwo.Id, "Header ambiguous team two", "header-ambiguous-team-two", TeamFormationType.Drafted, null, true);
        var ambiguousParticipantTwo = new EventParticipant(Guid.NewGuid(), ambiguousLiveTwo.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        ambiguousParticipantTwo.AssignOwner(ambiguousParticipant);
        var ambiguousMembershipTwo = new TeamMembership(Guid.NewGuid(), ambiguousTeamTwo.Id, ambiguousParticipantTwo.Id, TeamMembershipRole.Participant, now, null, "test");

        var draftCaptain = Website("header-draft-captain", now);
        draftCaptain.SetPassword(new PasswordHasher<Account>().HashPassword(draftCaptain, "password"), false, now, false);
        var draft = new BingoEvent(Guid.NewGuid(), "Header draft", "header-draft", "", "UTC", now.AddDays(-1), now.AddDays(1), null, now.AddDays(2), now.AddDays(7), 20, captain.Id, now);
        var draftTeam = new Team(Guid.NewGuid(), draft.Id, "Header draft team", "header-draft-team", TeamFormationType.Drafted, null, true);
        var draftParticipant = new EventParticipant(Guid.NewGuid(), draft.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website);
        draftParticipant.AssignOwner(draftCaptain);
        var draftMembership = new TeamMembership(Guid.NewGuid(), draftTeam.Id, draftParticipant.Id, TeamMembershipRole.Captain, now, null, "test");

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(captain, coCaptain, participant, admin, emergency, awaitingCaptain, awaitingParticipant, ambiguousParticipant, awaitingEmergency, draftCaptain,
                live, liveTeam, captainParticipant,
                coCaptainParticipant, ordinaryParticipant, liveMemberships[0], liveMemberships[1], liveMemberships[2], emergencyAccess,
                finalized, finalizedTeam, finalizedCaptainParticipant, finalizedOrdinaryParticipant, finalizedCaptainMembership, finalizedOrdinaryMembership,
                archived, archivedTeam, archivedCaptainParticipant, archivedOrdinaryParticipant, archivedCaptainMembership, archivedOrdinaryMembership,
                awaiting, awaitingTeam, awaitingCaptainParticipant, awaitingOrdinaryParticipant, awaitingCaptainMembership, awaitingOrdinaryMembership, awaitingEmergencyAccess,
                ambiguousLiveOne, ambiguousTeamOne, ambiguousParticipantOne, ambiguousMembershipOne, ambiguousLiveTwo, ambiguousTeamTwo, ambiguousParticipantTwo, ambiguousMembershipTwo,
                draft, draftTeam, draftParticipant, draftMembership);
            await db.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        async Task<string> LoggedInHtml(Account account)
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            await LoginAsync(client, account.LoginName);
            return await client.GetStringAsync("/");
        }

        var expectedHref = $"/Submissions?eventId={live.Id}&amp;teamId={liveTeam.Id}";
        foreach (var account in new[] { captain, coCaptain, emergency })
        {
            var html = await LoggedInHtml(account);
            Assert.Equal(2, Regex.Count(html, Regex.Escape($"href=\"{expectedHref}\"")));
            Assert.Equal(2, Regex.Count(html, Regex.Escape(">Captain</a>")));
            Assert.Equal(0, Regex.Count(html, @">Submissions</a>", RegexOptions.IgnoreCase));
        }

        var expectedSubmissionHref = $"/Submissions?eventId={live.Id}&amp;teamId={liveTeam.Id}";
        var participantHtml = await LoggedInHtml(participant);
        Assert.Equal(2, Regex.Count(participantHtml, Regex.Escape($"href=\"{expectedSubmissionHref}\"")));
        Assert.Equal(2, Regex.Count(participantHtml, @">Submissions</a>", RegexOptions.IgnoreCase));
        Assert.Equal(0, Regex.Count(participantHtml, Regex.Escape(">Captain</a>")));

        var awaitingParticipantHtml = await LoggedInHtml(awaitingParticipant);
        var expectedAwaitingHref = $"/Submissions?eventId={awaiting.Id}&amp;teamId={awaitingTeam.Id}";
        Assert.Equal(2, Regex.Count(awaitingParticipantHtml, Regex.Escape($"href=\"{expectedAwaitingHref}\"")));
        Assert.Equal(2, Regex.Count(awaitingParticipantHtml, @">Submissions</a>", RegexOptions.IgnoreCase));
        Assert.Equal(0, Regex.Count(awaitingParticipantHtml, Regex.Escape(">Captain</a>")));

        var awaitingCaptainHtml = await LoggedInHtml(awaitingCaptain);
        Assert.Equal(2, Regex.Count(awaitingCaptainHtml, Regex.Escape($"href=\"{expectedAwaitingHref}\"")));
        Assert.Equal(2, Regex.Count(awaitingCaptainHtml, Regex.Escape(">Captain</a>")));
        Assert.Equal(0, Regex.Count(awaitingCaptainHtml, @">Submissions</a>", RegexOptions.IgnoreCase));

        var awaitingEmergencyHtml = await LoggedInHtml(awaitingEmergency);
        Assert.Equal(2, Regex.Count(awaitingEmergencyHtml, Regex.Escape($"href=\"{expectedAwaitingHref}\"")));
        Assert.Equal(2, Regex.Count(awaitingEmergencyHtml, Regex.Escape(">Captain</a>")));
        Assert.Equal(0, Regex.Count(awaitingEmergencyHtml, @">Submissions</a>", RegexOptions.IgnoreCase));

        var ambiguousParticipantHtml = await LoggedInHtml(ambiguousParticipant);
        Assert.DoesNotContain("href=\"/Submissions?eventId=", ambiguousParticipantHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, Regex.Count(ambiguousParticipantHtml, @">Submissions</a>", RegexOptions.IgnoreCase));

        foreach (var account in new[] { admin })
        {
            var html = await LoggedInHtml(account);
            Assert.DoesNotContain($"href=\"{expectedHref}\"", html, StringComparison.Ordinal);
        }

        var draftHtml = await LoggedInHtml(draftCaptain);
        Assert.DoesNotContain(">Captain</a>", draftHtml, StringComparison.Ordinal);

    }

    [Fact]
    public async Task SubmissionDetailsExposeLinkedHistoryAndOnlyUnlinkedRejectionsCanResubmit()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Website("submission-chain-participant", now);
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "password"), false, now, false);
        var live = LiveEvent(owner.Id, "Submission chain live", "submission-chain-live", now);
        var team = new Team(Guid.NewGuid(), live.Id, "Submission chain team", "submission-chain-team", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website);
        participant.AssignOwner(owner);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var character = new OsrsCharacter(Guid.NewGuid(), "Submission chain player", "SUBMISSION CHAIN PLAYER", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), live.Id, participant.Id, character.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var board = new Board(Guid.NewGuid(), live.Id, "Submission chain board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Submission chain tile", "Description", "", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "Requirement", true);
        var parent = new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, owner.Id, 1, now.AddMinutes(-3), null, null);
        parent.Reject("Use a clearer screenshot.", now.AddMinutes(-2));
        var child = new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, owner.Id, 1, now.AddMinutes(-1), null, null, parent.Id);
        var unlinkedRejected = new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, owner.Id, 1, now, null, null);
        unlinkedRejected.Reject("Use a clearer screenshot.", now);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(owner, live, team, participant, membership, character, assignment, board, tile, requirement, parent, child, unlinkedRejected);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement]);
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, owner.LoginName);
        var query = $"?eventId={live.Id}&teamId={team.Id}";

        var parentHtml = await client.GetStringAsync($"/Submissions/{parent.Id}{query}");
        Assert.Contains("captain-ledger-status--replaced", parentHtml, StringComparison.Ordinal);
        Assert.Contains($"/Submissions/{child.Id}", parentHtml, StringComparison.Ordinal);
        Assert.Contains("View replacement submission", parentHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Resubmit", parentHtml, StringComparison.Ordinal);

        var childHtml = await client.GetStringAsync($"/Submissions/{child.Id}{query}");
        Assert.Contains($"/Submissions/{parent.Id}", childHtml, StringComparison.Ordinal);
        Assert.Contains("View prior submission", childHtml, StringComparison.Ordinal);

        var unlinkedRejectedHtml = await client.GetStringAsync($"/Submissions/{unlinkedRejected.Id}{query}");
        Assert.Contains("captain-ledger-status--rejected", unlinkedRejectedHtml, StringComparison.Ordinal);
        Assert.Contains("handler=Resubmit", unlinkedRejectedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminReviewDetailsJourneyPreservesFiltersAndSubmissionEventBoundaries()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website("admin-review-linked-child", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, false);
        var live = LiveEvent(admin.Id, "Admin review linked child", "admin-review-linked-child", now);
        var team = new Team(Guid.NewGuid(), live.Id, "Admin review team", "admin-review-team", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.AdminCreated);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var character = new OsrsCharacter(Guid.NewGuid(), "Admin review player", "ADMIN REVIEW PLAYER", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), live.Id, participant.Id, character.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var board = new Board(Guid.NewGuid(), live.Id, "Admin review board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Admin review tile", "Description", "", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "Requirement", true);
        var parent = new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, admin.Id, 1, now.AddMinutes(-2), null, null);
        parent.Reject("Use a clearer screenshot.", now.AddMinutes(-1));
        var child = new Submission(Guid.NewGuid(), live.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, admin.Id, 1, now, null, null, parent.Id);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, live, team, participant, membership, character, assignment, board, tile, requirement, parent, child);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement]);
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.LoginName);

        var search = "Admin review player";
        var queueUrl = $"/Admin/Review?eventId={live.Id}&search={Uri.EscapeDataString(search)}&status=Pending";
        var queueHtml = await client.GetStringAsync(queueUrl);
        var detailsUrl = WebUtility.HtmlDecode(Regex.Match(queueHtml, $"<a class=\"admin-review-cell-value admin-review-submission-link\"[^>]*href=\"([^\"]*/Admin/Review/Details/{child.Id}[^\"]*)\"").Groups[1].Value);
        Assert.NotEmpty(detailsUrl);
        var detailsHtml = await client.GetStringAsync(detailsUrl);
        AssertContext(detailsHtml);

        var rejectForm = Regex.Matches(detailsHtml, @"<form\b[\s\S]*?</form>")
            .Select(match => match.Value).Single(form => form.Contains("data-admin-review-reject-form", StringComparison.Ordinal));
        var action = WebUtility.HtmlDecode(Regex.Match(rejectForm, "action=\"([^\"]+)\"").Groups[1].Value);
        var fields = Regex.Matches(rejectForm, @"<input\b[^>]*>").Select(match => match.Value)
            .Where(input => input.Contains("type=\"hidden\"", StringComparison.Ordinal))
            .ToDictionary(input => Regex.Match(input, "name=\"([^\"]+)\"").Groups[1].Value,
                input => WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value));
        fields["Input.Reason"] = "Journey rejection reason.";
        using var decision = await client.PostAsync(action, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, decision.StatusCode);
        var redirect = decision.Headers.Location!.OriginalString;
        Assert.StartsWith($"/Admin/Review/Details/{child.Id}?", redirect, StringComparison.Ordinal);
        var resolvedHtml = await client.GetStringAsync(redirect);
        AssertContext(resolvedHtml);
        Assert.Contains("Journey rejection reason.", resolvedHtml, StringComparison.Ordinal);
        var backUrl = WebUtility.HtmlDecode(Regex.Match(resolvedHtml, "<a[^>]*class=\"[^\"]*admin-review-back-link[^\"]*\"[^>]*href=\"([^\"]+)\"").Groups[1].Value);
        var backQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, backUrl).Query);
        Assert.Equal(live.Id.ToString(), backQuery["eventId"].ToString());
        Assert.Equal(search, backQuery["search"].ToString());
        Assert.Equal("Pending", backQuery["status"].ToString());
        var returnedQueue = await client.GetStringAsync(backUrl);
        Assert.Contains($"data-event-id=\"{live.Id}\"", returnedQueue, StringComparison.Ordinal);
        await using (var db = new ApplicationDbContext(options))
            Assert.Equal(SubmissionStatus.Rejected, (await db.Submissions.SingleAsync(item => item.Id == child.Id)).Status);

        AssertContext(await client.GetStringAsync($"/Admin/Review/Details/{child.Id}"));
        var other = LiveEvent(admin.Id, "Other details context", "other-details-context", now);
        var denied = Website("details-denied", now);
        denied.SetPassword(new PasswordHasher<Account>().HashPassword(denied, "password"), false, now, false);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(other, denied);
            await db.SaveChangesAsync();
        }
        AssertContext(await client.GetStringAsync($"/Admin/Review/Details/{child.Id}?eventId={other.Id}"));
        using var deniedClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(deniedClient, denied.LoginName);
        using var deniedResponse = await deniedClient.GetAsync(detailsUrl);
        Assert.Equal(HttpStatusCode.Redirect, deniedResponse.StatusCode);
        Assert.Contains("AccessDenied", deniedResponse.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin review player", await deniedResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using (var db = new ApplicationDbContext(options))
        {
            var eventItem = await db.Events.SingleAsync(item => item.Id == live.Id);
            eventItem.EndEvent(now);
            eventItem.FinalizeResults(now);
            eventItem.Hide(admin.Id, now, eventItem.Name, "Hidden details boundary fixture.");
            await db.SaveChangesAsync();
        }
        using var hiddenResponse = await client.GetAsync($"/Admin/Review/Details/{child.Id}?eventId={other.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);
        var hiddenHtml = await hiddenResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Admin review player", hiddenHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-admin-event-navigation", hiddenHtml, StringComparison.Ordinal);

        void AssertContext(string html)
        {
            Assert.Contains($"<span class=\"admin-selected-event-name\">{live.Name}</span>", html, StringComparison.Ordinal);
            Assert.Contains($"data-admin-event-section=\"overview\" href=\"/Admin/Events/Manage/{live.Id}\"", html, StringComparison.Ordinal);
            Assert.Contains("data-admin-review-detail", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AdminReviewSelectsExplicitVisibleEventsAndKeepsTheQueueEventScoped()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website("admin-review-event-scope", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, false);
        var emptyDraft = new BingoEvent(Guid.NewGuid(), "Admin review empty draft", "admin-review-empty-draft", "UTC", admin.Id, now);
        var selected = LiveEvent(admin.Id, "Admin review finalized", "admin-review-finalized", now);
        selected.EndEvent(now.AddHours(-1));
        selected.FinalizeResults(now.AddMinutes(-30));
        var other = LiveEvent(admin.Id, "Admin review other", "admin-review-other", now);
        var hidden = LiveEvent(admin.Id, "Admin review hidden", "admin-review-hidden", now);
        hidden.EndEvent(now.AddHours(-1));
        hidden.FinalizeResults(now.AddMinutes(-30));
        hidden.Hide(admin.Id, now, hidden.Name, "Hidden review boundary fixture.");

        var selectedTeam = new Team(Guid.NewGuid(), selected.Id, "Selected review team", "selected-review-team", TeamFormationType.Drafted, null, true);
        var selectedParticipant = new EventParticipant(Guid.NewGuid(), selected.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.AdminCreated);
        var selectedMembership = new TeamMembership(Guid.NewGuid(), selectedTeam.Id, selectedParticipant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var selectedCharacter = new OsrsCharacter(Guid.NewGuid(), "Selected review player", "SELECTED REVIEW PLAYER", now);
        var selectedAssignment = new EventParticipantCharacter(Guid.NewGuid(), selected.Id, selectedParticipant.Id, selectedCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var selectedBoard = new Board(Guid.NewGuid(), selected.Id, "Selected review board", 1, 1);
        var selectedTile = new BoardTile(Guid.NewGuid(), selectedBoard.Id, Guid.NewGuid(), 0, 0, "Selected review tile", "Description", "", 1m);
        var selectedRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), selectedTile.Id, 0, 1, true, false, "Requirement", true);
        var selectedSubmission = new Submission(Guid.NewGuid(), selected.Id, selectedTeam.Id, selectedTile.Id, selectedRequirement.Id, null, selectedParticipant.Id, selectedCharacter.Id, selectedCharacter.DisplayName, admin.Id, 1, now.AddMinutes(-5), null, null);

        var newerPending = new Submission(Guid.NewGuid(), selected.Id, selectedTeam.Id, selectedTile.Id, selectedRequirement.Id, null, selectedParticipant.Id, selectedCharacter.Id, selectedCharacter.DisplayName, admin.Id, 1, now.AddMinutes(-4), null, null);
        var approved = new Submission(Guid.NewGuid(), selected.Id, selectedTeam.Id, selectedTile.Id, selectedRequirement.Id, null, selectedParticipant.Id, selectedCharacter.Id, selectedCharacter.DisplayName, admin.Id, 1, now.AddMinutes(-3), null, null);
        approved.Approve(1, now);
        var rejected = new Submission(Guid.NewGuid(), selected.Id, selectedTeam.Id, selectedTile.Id, selectedRequirement.Id, null, selectedParticipant.Id, selectedCharacter.Id, selectedCharacter.DisplayName, admin.Id, 1, now.AddMinutes(-2), null, null);
        rejected.Reject("Ordering fixture.", now);

        var otherTeam = new Team(Guid.NewGuid(), other.Id, "Other review team", "other-review-team", TeamFormationType.Drafted, null, true);
        var otherParticipant = new EventParticipant(Guid.NewGuid(), other.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.AdminCreated);
        var otherMembership = new TeamMembership(Guid.NewGuid(), otherTeam.Id, otherParticipant.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        var otherCharacter = new OsrsCharacter(Guid.NewGuid(), "Other review player", "OTHER REVIEW PLAYER", now);
        var otherAssignment = new EventParticipantCharacter(Guid.NewGuid(), other.Id, otherParticipant.Id, otherCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
        var otherBoard = new Board(Guid.NewGuid(), other.Id, "Other review board", 1, 1);
        var otherTile = new BoardTile(Guid.NewGuid(), otherBoard.Id, Guid.NewGuid(), 0, 0, "Other review tile", "Description", "", 1m);
        var otherRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), otherTile.Id, 0, 1, true, false, "Requirement", true);
        var otherSubmission = new Submission(Guid.NewGuid(), other.Id, otherTeam.Id, otherTile.Id, otherRequirement.Id, null, otherParticipant.Id, otherCharacter.Id, otherCharacter.DisplayName, admin.Id, 1, now.AddMinutes(-4), null, null);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, emptyDraft, selected, other, hidden,
                selectedTeam, selectedParticipant, selectedMembership, selectedCharacter, selectedAssignment, selectedBoard, selectedTile, selectedRequirement, selectedSubmission, newerPending, approved, rejected,
                otherTeam, otherParticipant, otherMembership, otherCharacter, otherAssignment, otherBoard, otherTile, otherRequirement, otherSubmission);
            await db.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.LoginName);

        var emptyResponse = await client.GetAsync($"/Admin/Review?eventId={emptyDraft.Id}");
        var emptyHtml = await emptyResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        Assert.Contains($"data-event-id=\"{emptyDraft.Id}\"", emptyHtml, StringComparison.Ordinal);
        Assert.Contains("No submissions match these filters", emptyHtml, StringComparison.Ordinal);

        var selectedResponse = await client.GetAsync($"/Admin/Review?eventId={selected.Id}");
        var selectedHtml = await selectedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, selectedResponse.StatusCode);
        Assert.Contains($"data-event-id=\"{selected.Id}\"", selectedHtml, StringComparison.Ordinal);
        Assert.Contains("Selected review team", selectedHtml, StringComparison.Ordinal);
        Assert.Contains("Selected review player", selectedHtml, StringComparison.Ordinal);
        Assert.Contains("Selected review tile", selectedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-review-team=\"Other review team\"", selectedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-review-player=\"Other review player\"", selectedHtml, StringComparison.Ordinal);
        Assert.Contains($"/Admin/Review/Details/{selectedSubmission.Id}", selectedHtml, StringComparison.Ordinal);

        var renderedSubmissionIds = Regex.Matches(selectedHtml, @"<tr data-admin-review-row[^>]*>[\s\S]*?/Admin/Review/Details/([0-9a-f-]+)")
            .Select(match => Guid.Parse(match.Groups[1].Value)).ToArray();
        Assert.Equal(new[] { newerPending.Id, selectedSubmission.Id, rejected.Id, approved.Id }, renderedSubmissionIds);

        var hiddenResponse = await client.GetAsync($"/Admin/Review?eventId={hidden.Id}");
        var hiddenHtml = await hiddenResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, hiddenResponse.StatusCode);
        Assert.Contains("data-event-id=\"\"", hiddenHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Selected review team", hiddenHtml, StringComparison.Ordinal);

        var invalidResponse = await client.GetAsync($"/Admin/Review?eventId={Guid.NewGuid()}");
        var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        Assert.Contains("data-event-id=\"\"", invalidHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Selected review team", invalidHtml, StringComparison.Ordinal);
    }

    private static BingoEvent LiveEvent(Guid ownerId, string name, string slug, DateTimeOffset now)
    {
        var item = new BingoEvent(Guid.NewGuid(), name, slug, "UTC", ownerId, now.AddDays(-2));
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-1), now.AddHours(2), 20);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now.AddHours(-1));
        return item;
    }

    private static Account Website(string name, DateTimeOffset now) => Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);


    private static async Task LoginAsync(HttpClient client, string username)
    {
        var page = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = "password",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
    }
}
