using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3EvidenceReviewLifecycleIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("slice3_evidence_review_lifecycle").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly DateTimeOffset now = new(2026, 7, 27, 21, 0, 0, TimeSpan.Zero);
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
    public async Task AdminReviewDetailsRejectsTerminalEvidenceMutationsAndKeepsLiveAndFinalReviewAvailable()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Evidence lifecycle Admin", "EVIDENCE LIFECYCLE ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "evidence-lifecycle-password"), false, now, incrementVersion: false);
        var cancelled = SeedApproved(EventState.Cancelled, admin.Id, "review-cancelled");
        var finalized = SeedApproved(EventState.Finalized, admin.Id, "review-finalized");
        var archived = SeedApproved(EventState.Archived, admin.Id, "review-archived");
        var live = SeedApproved(EventState.Live, admin.Id, "review-live");
        var finalReview = SeedApproved(EventState.AwaitingFinalReview, admin.Id, "review-final-review");
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Add(admin);
            await AddSetupAsync(setup, cancelled); await AddSetupAsync(setup, finalized); await AddSetupAsync(setup, archived); await AddSetupAsync(setup, live); await AddSetupAsync(setup, finalReview);
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using var loggedIn = await client.PostAsync("/Account/Login", Form(("Input.Username", admin.PublicUsername!), ("Input.Password", "evidence-lifecycle-password"), ("__RequestVerificationToken", Token(login))));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, loggedIn.StatusCode);

        foreach (var terminal in new[] { cancelled, finalized, archived })
        {
            await AssertRejectedAsync(client, terminal.SubmissionId, "Reverse", ("Input.Note", "Attempted terminal reversal"));
            await AssertRejectedAsync(client, terminal.SubmissionId, "HideVisibility");
            await AssertUnchangedAsync(terminal);
        }

        using (var liveResponse = await PostDetailsAsync(client, live.SubmissionId, "HideVisibility"))
            Assert.Equal(System.Net.HttpStatusCode.Redirect, liveResponse.StatusCode);
        using (var reviewResponse = await PostDetailsAsync(client, finalReview.SubmissionId, "Reverse", ("Input.Note", "Valid final-review reversal")))
            Assert.Equal(System.Net.HttpStatusCode.Redirect, reviewResponse.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.True((await verify.Submissions.SingleAsync(value => value.Id == live.SubmissionId)).PublicEvidenceHidden);
        var reversed = await verify.Submissions.SingleAsync(value => value.Id == finalReview.SubmissionId);
        Assert.Equal(SubmissionStatus.Reversed, reversed.Status);
        Assert.NotNull((await verify.SubmissionContributions.SingleAsync(value => value.SubmissionId == finalReview.SubmissionId)).ReversedAt);
    }

    private static async Task AssertRejectedAsync(HttpClient client, Guid submissionId, string handler, params (string Key, string Value)[] values)
    {
        using var response = await PostDetailsAsync(client, submissionId, handler, values);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var page = await client.GetStringAsync(response.Headers.Location!.OriginalString);
        Assert.Contains("Evidence can only be reviewed while the event is Live or awaiting final review.", page, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PostDetailsAsync(HttpClient client, Guid submissionId, string handler, params (string Key, string Value)[] values)
    {
        var path = $"/Admin/Review/Details/{submissionId}";
        var page = await client.GetStringAsync(path);
        var fields = values.Append(("__RequestVerificationToken", Token(page))).ToArray();
        return await client.PostAsync($"{path}?handler={handler}", Form(fields));
    }

    private async Task AssertUnchangedAsync(ApprovedSetup setup)
    {
        await using var verify = new ApplicationDbContext(options);
        var submission = await verify.Submissions.SingleAsync(value => value.Id == setup.SubmissionId);
        Assert.Equal(SubmissionStatus.Approved, submission.Status);
        Assert.False(submission.PublicEvidenceHidden);
        Assert.False(submission.PublicPlayerHidden);
        var contribution = await verify.SubmissionContributions.SingleAsync(value => value.SubmissionId == setup.SubmissionId);
        Assert.Null(contribution.ReversedAt);
        Assert.Single(await verify.ReviewActions.Where(value => value.SubmissionId == setup.SubmissionId).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == setup.EventId).ToListAsync());
        Assert.Empty(await verify.PersonalNotifications.Where(value => value.Route == $"/Admin/Review/Details/{setup.SubmissionId}").ToListAsync());
    }

    private ApprovedSetup SeedApproved(EventState state, Guid adminId, string slug)
    {
        var eventId = Guid.NewGuid(); var teamId = Guid.NewGuid(); var participantId = Guid.NewGuid(); var boardId = Guid.NewGuid(); var tileId = Guid.NewGuid(); var requirementId = Guid.NewGuid(); var submissionId = Guid.NewGuid();
        var item = new BingoEvent(eventId, slug, slug, "UTC", adminId, now.AddDays(-5));
        if (state == EventState.Cancelled) item.Cancel(adminId, now, "Terminal test", true);
        else
        {
            item.ConfigureSchedule(now.AddDays(-4), now.AddDays(-3), null, now.AddDays(-2), now.AddDays(-1), 20);
            item.OpenSignups(now.AddDays(-4)); item.CloseSignups(now.AddDays(-3)); item.StartEvent(now.AddDays(-2));
            if (state is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived) item.EndEvent(now.AddDays(-1));
            if (state is EventState.Finalized or EventState.Archived) item.FinalizeResults(now.AddHours(-12));
            if (state == EventState.Archived) item.Archive(now.AddHours(-11));
        }
        var board = new Board(boardId, eventId, "Board", 1, 1);
        var submission = new Submission(submissionId, eventId, teamId, tileId, requirementId, null, participantId, adminId, 1, now.AddDays(-2), null, null);
        submission.Approve(1, now.AddDays(-2));
        return new(eventId, teamId, participantId, boardId, tileId, requirementId, submissionId, item, board, submission, adminId);
    }

    private static async Task AddSetupAsync(ApplicationDbContext db, ApprovedSetup setup)
    {
        var character = new OsrsCharacter(Guid.NewGuid(), $"Player {setup.EventId:N}", $"PLAYER {setup.EventId:N}", DateTimeOffset.UtcNow);
        var tile = new BoardTile(setup.TileId, setup.BoardId, Guid.NewGuid(), 0, 0, "Tile", "Description", "Evidence", 1);
        var requirement = new BoardRequirementSnapshot(setup.RequirementId, setup.TileId, 0, 1, true, false, "Requirement", true);
        db.AddRange(setup.Event, new Team(setup.TeamId, setup.EventId, "Team", $"team-{setup.EventId:N}", TeamFormationType.Drafted, null, true),
            new EventParticipant(setup.ParticipantId, setup.EventId, SignupStatus.Confirmed, 1, DateTimeOffset.UtcNow, SignupSource.Website), character,
            new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, setup.ParticipantId, character.Id, 0, DateTimeOffset.UtcNow, setup.AdminId, null, EventCharacterRole.Playing, 100, EhbSource.Manual, null),
            setup.Board, tile, requirement,
            new TeamMembership(Guid.NewGuid(), setup.TeamId, setup.ParticipantId, TeamMembershipRole.Participant, DateTimeOffset.UtcNow, null, null),
            setup.Submission, new SubmissionContribution(Guid.NewGuid(), setup.SubmissionId, setup.TeamId, setup.RequirementId, null, setup.ParticipantId, 1, DateTimeOffset.UtcNow),
            new ReviewAction(Guid.NewGuid(), setup.SubmissionId, ReviewActionType.Approve, setup.AdminId, DateTimeOffset.UtcNow, "Seeded", null, null));
        await BoardApprovalFixture.PublishAsync(db, setup.Board, DateTimeOffset.UtcNow, [tile], [requirement]);
    }

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] values) => new(values.ToDictionary(value => value.Key, value => value.Value));
    private static string Token(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private sealed record ApprovedSetup(Guid EventId, Guid TeamId, Guid ParticipantId, Guid BoardId, Guid TileId, Guid RequirementId, Guid SubmissionId, BingoEvent Event, Board Board, Submission Submission, Guid AdminId);
}
