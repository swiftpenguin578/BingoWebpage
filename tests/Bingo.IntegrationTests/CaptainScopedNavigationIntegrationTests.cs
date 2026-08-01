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

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Captain")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Captain?eventId={second.Id}&teamId={firstTeam.Id}")).StatusCode);

        var scopedIndexRoute = $"/Captain?eventId={first.Id}&teamId={firstTeam.Id}";
        using var index = await client.GetAsync(scopedIndexRoute);
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        var indexHtml = await index.Content.ReadAsStringAsync();
        Assert.Contains($"/Captain/Submissions/{submission.Id}?eventId={first.Id}&amp;teamId={firstTeam.Id}", indexHtml, StringComparison.Ordinal);
        Assert.Contains($"/Captain/Submit/{firstTile.Id}?eventId={first.Id}&amp;teamId={firstTeam.Id}", indexHtml, StringComparison.Ordinal);

        var detailsRoute = $"/Captain/Submissions/{submission.Id}?eventId={first.Id}&teamId={firstTeam.Id}";
        using var details = await client.GetAsync(detailsRoute);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var detailsHtml = await details.Content.ReadAsStringAsync();
        Assert.Contains($"href=\"/Captain?eventId={first.Id}&amp;teamId={firstTeam.Id}\"", detailsHtml, StringComparison.Ordinal);
        var withdrawAction = Regex.Matches(detailsHtml, $"action=\\\"([^\\\"]*{submission.Id}[^\\\"]*)\\\"").Select(x => x.Value).Single(x => x.Contains("handler=Withdraw", StringComparison.Ordinal));
        Assert.Contains($"eventId={first.Id}", withdrawAction, StringComparison.Ordinal);
        Assert.Contains($"teamId={firstTeam.Id}", withdrawAction, StringComparison.Ordinal);

        var token = Regex.Match(detailsHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        using var withdrawn = await client.PostAsync($"/Captain/Submissions/{submission.Id}?handler=Withdraw&eventId={first.Id}&teamId={firstTeam.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ExpectedVersion"] = "1",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, withdrawn.StatusCode);
        Assert.Equal(scopedIndexRoute, withdrawn.Headers.Location?.OriginalString);

        await using var verification = new ApplicationDbContext(options);
        Assert.Equal(SubmissionStatus.Withdrawn, await verification.Submissions.Where(x => x.Id == submission.Id).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Pending, await verification.Submissions.Where(x => x.Id == secondSubmission.Id).Select(x => x.Status).SingleAsync());
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
