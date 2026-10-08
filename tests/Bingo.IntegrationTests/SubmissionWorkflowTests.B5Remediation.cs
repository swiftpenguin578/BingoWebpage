using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Review;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class SubmissionWorkflowTests
{
    private static readonly string[] B5RemediationTeamNames = ["Team One", "Team Two"];
    private static readonly int[] B5RemediationRosterCounts = [1, 1];
    private static readonly int[] B5RemediationPlacements = [1, 2];
    [Fact]
    public async Task B5RemediationCrossTeamCreditRendersBoardStatsAndPublishedPlacements()
    {
        var setup = await SeedAsync(2, true, tileEhb: 12);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var first = await service.CreateAsync(Command(setup));
        await service.ApproveCurrentAsync(first.SubmissionId, setup.AdminId);
        var original = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == first.SubmissionId);
        var teamB = new Team(Guid.NewGuid(), setup.EventId, "Team Two", "team-two", TeamFormationType.Drafted, null, true);
        teamB.Finalize(now.AddDays(-1));
        (await db.TeamMemberships.SingleAsync(x => x.TeamId == setup.TeamId && x.EventParticipantId == setup.ParticipantId)).Leave(now.AddSeconds(1), "Retained cross-team history");
        var priorPublication = await db.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
        priorPublication.Supersede(now.AddSeconds(1), setup.AdminId, "Retained roster replacement");
        var captainParticipantId = await db.TeamMemberships.Where(x => x.TeamId == setup.TeamId && x.Role == TeamMembershipRole.Captain).Select(x => x.EventParticipantId).SingleAsync();
        var currentPublication = new DraftPublicationCycle(Guid.NewGuid(), priorPublication.DraftSessionId, 2, now.AddSeconds(1), setup.AdminId, DraftPublicationMethod.DirectRoster);
        db.AddRange(teamB, currentPublication,
            new DraftPublicationRoster(Guid.NewGuid(), currentPublication.Id, setup.TeamId, captainParticipantId, TeamMembershipRole.Captain, null, "Captain One"),
            new TeamMembership(Guid.NewGuid(), teamB.Id, setup.ParticipantId, TeamMembershipRole.Participant, now.AddSeconds(1), null, "Retained cross-team history"),
            new DraftPublicationRoster(Guid.NewGuid(), currentPublication.Id, teamB.Id, setup.ParticipantId, TeamMembershipRole.Participant, null, original.CreditedCharacterName));
        var second = new Submission(Guid.NewGuid(), setup.EventId, teamB.Id, setup.TileId, setup.RequirementId, setup.DropId,
            setup.ParticipantId, original.CreditedOsrsCharacterId, original.CreditedCharacterName, setup.AdminId, 2, now.AddSeconds(2), null, null);
        db.Submissions.Add(second);
        await db.SaveChangesAsync();
        await Service(db, new FixedTimeProvider(now.AddSeconds(3))).ApproveCurrentAsync(second.Id, setup.AdminId);
        var clock = new FixedTimeProvider(now.AddHours(5));
        var ev = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        ev.SetBoardPublication(true, now.AddDays(-1));
        ev.EndEvent(now.AddSeconds(3)); ev.CloseSubmissionsIfDue(clock.GetUtcNow());
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, EventState.Live, EventState.AwaitingFinalReview, setup.AdminId, now.AddSeconds(3), "End", effectiveAt: now.AddSeconds(3)));
        await db.SaveChangesAsync();
        var boards = new PublicBoardService(db, clock);
        var board = (await boards.GetEventBoardAsync(ev.Slug))!;
        Assert.Equal(2, board.PlayerLeaderboard.Count);
        var creditedRows = board.PlayerLeaderboard.Where(x => x.PlayerId == setup.ParticipantId).ToList();
        Assert.Equal(2, creditedRows.Count);
        Assert.All(creditedRows, player => { Assert.Equal(setup.ParticipantId, player.PlayerId); Assert.Equal(12m, player.EstimatedEhb); });
        Assert.Equal(B5RemediationTeamNames, creditedRows.Select(x => x.TeamName).Order().ToArray());
        Assert.Equal(2, board.RosterPlayers!.Count);
        Assert.Equal(teamB.Id, Assert.Single(board.RosterPlayers, x => x.PlayerId == setup.ParticipantId).TeamId);
        Assert.Equal(B5RemediationRosterCounts, board.DropEhbTeams!.OrderBy(x => x.TeamName).Select(x => x.PlayerCount).ToArray());
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await AssertCrossTeamPagesAsync(client, ev.Slug, setup.ParticipantId);
        var finals = new EventFinalizationService(db, boards, clock);
        var readiness = (await finals.GetReadinessAsync(ev.Id))!;
        Assert.Empty(readiness.Blockers);
        Assert.Equal(2, readiness.Placements.Count);
        await finals.FinalizeAsync(ev.Id, new LifecycleActor(setup.AdminId, "admin"), ev.Version);
        var placements = await db.OfficialPlacements.AsNoTracking().OrderBy(x => x.Placement).ToListAsync();
        Assert.Equal(new[] { setup.TeamId, teamB.Id }, placements.Select(x => x.TeamId).ToArray());
        Assert.Equal(B5RemediationPlacements, placements.Select(x => x.Placement).ToArray());
        var published = (await boards.GetEventBoardAsync(ev.Slug))!;
        Assert.True(published.EventResult!.IsOfficial);
        Assert.Equal("Team One", published.EventResult.TeamName);
        Assert.Equal(2, published.PlayerLeaderboard.Count);
        var publishedCreditedRows = published.PlayerLeaderboard.Where(x => x.PlayerId == setup.ParticipantId).ToList();
        Assert.Equal(2, publishedCreditedRows.Count);
        Assert.All(publishedCreditedRows, player => Assert.Equal(12m, player.EstimatedEhb));
        await AssertCrossTeamPagesAsync(client, ev.Slug, setup.ParticipantId);
    }

    [Theory]
    [InlineData(-1, false, false)]
    [InlineData(0, false, true)]
    [InlineData(1, false, true)]
    [InlineData(null, false, true)]
    [InlineData(1, true, false)]
    public async Task B5RemediationCorrectionUsesMembershipAtUploadAndLatestRole(int? leaveSeconds, bool latestInformational, bool eligible)
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var created = await service.CreateAsync(Command(setup));
        var submission = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == created.SubmissionId);
        var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 10, now.AddDays(-5), SignupSource.Website);
        var character = new OsrsCharacter(Guid.NewGuid(), "Retained player", "RETAINED PLAYER", now.AddDays(-5));
        var playing = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 0, now.AddDays(-5), setup.AdminId, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null);
        var membership = new TeamMembership(Guid.NewGuid(), setup.TeamId, participant.Id, TeamMembershipRole.Participant, now.AddDays(-4), null, null);
        if (leaveSeconds is { } offset) membership.Leave(submission.SubmittedAt.AddSeconds(offset), "Retained membership");
        db.AddRange(participant, character, playing, membership);
        if (latestInformational)
        {
            playing.Release(setup.AdminId, now.AddMinutes(-2));
            var informational = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 1, now.AddMinutes(-1), setup.AdminId, null, EventCharacterRole.Informational, null, null, null);
            informational.Release(setup.AdminId, now);
            db.EventParticipantCharacters.Add(informational);
        }
        await db.SaveChangesAsync();
        var baseline = await B5EvidenceStateAsync();
        var choices = await service.GetCorrectionCharactersAsync(submission.Id, setup.AdminId);
        if (!eligible)
        {
            Assert.DoesNotContain(choices, x => x.CharacterId == character.Id);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EditMetadataAsync(new(submission.Id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character.Id, "Correction", submission.Version)));
            Assert.Equal("Choose an unambiguous Playing character assigned in this event to a current or former member of this submission's team.", error.Message);
            Assert.Equal(baseline, await B5EvidenceStateAsync());
            return;
        }
        var choice = Assert.Single(choices, x => x.CharacterId == character.Id);
        Assert.Equal(leaveSeconds is not null, choice.LeftTeam);
        Assert.False(choice.Released);
        Assert.Equal(leaveSeconds is null, choice.Current);
        await service.EditMetadataAsync(new(submission.Id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character.Id, "Correction", submission.Version));
        var corrected = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission.Id);
        Assert.Equal(participant.Id, corrected.CreditedParticipantId);
        Assert.Equal(character.Id, corrected.CreditedOsrsCharacterId);
        Assert.Equal(setup.TeamId, corrected.TeamId);
        Assert.Equal(submission.SubmittedAt, corrected.SubmittedAt);
        Assert.Equal(submission.Version + 1, corrected.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B5RemediationReviewActionVersionsPreserveLegacyUnknown(bool legacy)
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var created = await service.CreateAsync(Command(setup));
        var before = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == created.SubmissionId);
        if (legacy)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE review_actions SET before_snapshot = before_snapshot - 'Version', after_snapshot = after_snapshot - 'Version' WHERE submission_id = {before.Id}");
            db.ReviewActions.Add(new ReviewAction(Guid.NewGuid(), before.Id, ReviewActionType.EditMetadata, setup.AdminId, now.AddSeconds(1), "Legacy correction", "{}", "{}"));
            await db.SaveChangesAsync();
        }
        else await service.ApproveAsync(before.Id, setup.AdminId, expectedVersion: before.Version);
        var state = (await service.GetReviewReadbackAsync(before.Id, setup.AdminId)).State!;
        Assert.NotNull(state.LatestAction);
        Assert.Equal(setup.AdminId, state.LatestAction.ActorId);
        Assert.Equal(legacy ? ReviewActionType.EditMetadata : ReviewActionType.Approve, state.LatestAction.Type);
        Assert.Equal(legacy ? null : (int?)before.Version, state.LatestAction.BeforeVersion);
        Assert.Equal(legacy ? null : (int?)state.Version, state.LatestAction.AfterVersion);
        Assert.Equal(legacy ? before.Version : before.Version + 1, state.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B5RemediationReviewDetailsExposeTeamLeaveTimeOnlyForFormerMember(bool left)
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var created = await Service(db).CreateAsync(Command(setup));
        var leaveAt = now.AddMinutes(1);
        if (left)
        {
            (await db.TeamMemberships.SingleAsync(x => x.TeamId == setup.TeamId && x.EventParticipantId == setup.ParticipantId)).Leave(leaveAt, "Left team");
            await db.SaveChangesAsync();
        }
        var page = new DetailsModel(db, Service(db));
        Assert.IsType<PageResult>(await page.OnGetAsync(created.SubmissionId, CancellationToken.None));
        Assert.Equal(left ? leaveAt : null, page.Details.CreditedParticipantLeftTeamAt);
        Assert.Equal(setup.TeamId, page.Details.TeamId);
        Assert.Equal(setup.ParticipantId, page.Details.PlayerId);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    public async Task B5RemediationContributionRespectsExhaustedAndPartialDropCap(int cap, int expectedAdd)
    {
        var setup = await SeedAsync(8, true, dropMaximum: cap);
        await using var db = new ApplicationDbContext(options);
        var first = await Service(db).CreateAsync(Command(setup));
        var second = await Service(db, new FixedTimeProvider(now.AddSeconds(1))).CreateAsync(Command(setup));
        Assert.Equal(2, (await Service(db).ApproveCurrentAsync(first.SubmissionId, setup.AdminId)).ApprovedContribution);
        var read = (await Service(db).GetReviewReadbackAsync(second.SubmissionId, setup.AdminId)).State!;
        Assert.Null(read.Contribution.BlockingSubmission);
        Assert.Equal(new SubmissionContributionNumbers(expectedAdd, 2, 6, 2, 8, false), read.Contribution.Values);
        if (expectedAdd == 0)
        {
            var baseline = await B5EvidenceStateAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).ApproveCurrentAsync(second.SubmissionId, setup.AdminId));
            Assert.Equal(baseline, await B5EvidenceStateAsync());
        }
        else
        {
            Assert.Equal(expectedAdd, (await Service(db).ApproveCurrentAsync(second.SubmissionId, setup.AdminId)).ApprovedContribution);
            var approved = (await Service(db).GetReviewReadbackAsync(second.SubmissionId, setup.AdminId)).State!;
            Assert.Equal(SubmissionStatus.Approved, approved.Status);
            Assert.Equal(read.Contribution.Values, approved.Contribution.Values);
        }
    }

    [Theory]
    [InlineData("accounts")]
    [InlineData("submissions")]
    [InlineData("review_actions")]
    public async Task B5RemediationReviewReadbackHandlesEveryReadBoundary(string table)
    {
        var setup = await SeedAsync(3, true);
        Guid submissionId;
        await using (var seed = new ApplicationDbContext(options)) submissionId = (await Service(seed).CreateAsync(Command(setup))).SubmissionId;
        var baseline = await B5EvidenceStateAsync();
        var failing = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new B5AnyReviewReadFailure(table)).Options;
        await using var db = new ApplicationDbContext(failing);
        var page = new DetailsModel(db, Service(db)) { PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, setup.AdminId.ToString())], "test")) } } };
        var result = Assert.IsType<SubmissionReviewReadback>(Assert.IsType<JsonResult>(await page.OnGetReadbackAsync(submissionId, CancellationToken.None)).Value);
        Assert.False(result.Known); Assert.Null(result.State);
        Assert.Equal("no-store", page.Response.Headers.CacheControl);
        Assert.Equal(baseline, await B5EvidenceStateAsync());
    }

    [Fact]
    public async Task B5RemediationReviewDisabledAdminIsRefusedByBothHandlers()
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var submission = await Service(db).CreateAsync(Command(setup));
        (await db.Accounts.SingleAsync(x => x.Id == setup.AdminId)).Disable(now);
        await db.SaveChangesAsync();
        var page = new DetailsModel(db, Service(db)) { PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, setup.AdminId.ToString())], "test")) } } };
        Assert.IsType<ForbidResult>(await page.OnGetReadbackAsync(submission.SubmissionId, CancellationToken.None));
        Assert.IsType<ForbidResult>(await page.OnGetCorrectionCharactersAsync(submission.SubmissionId, CancellationToken.None));
        Assert.Equal("no-store", page.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task B5RemediationCorrectionPickerHttpUsesNoStoreAndNotFound()
    {
        var setup = await SeedAsync(3, true);
        Guid submissionId;
        await using (var seed = new ApplicationDbContext(options))
        {
            submissionId = (await Service(seed).CreateAsync(Command(setup))).SubmissionId;
            var admin = await seed.Accounts.SingleAsync(x => x.Id == setup.AdminId);
            admin.SetGlobalRole(GlobalRole.SuperAdmin);
            admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, false);
            await seed.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(now)); }));
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var authenticated = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = "admin", ["Input.Password"] = "password", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, authenticated.StatusCode);
        using var success = await client.GetAsync($"/Admin/Review/Details/{submissionId}?handler=CorrectionCharacters");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.True(success.Headers.CacheControl!.NoStore);
        using var json = JsonDocument.Parse(await success.Content.ReadAsStringAsync());
        Assert.Single(json.RootElement.EnumerateArray());
        using var missing = await client.GetAsync($"/Admin/Review/Details/{Guid.NewGuid()}?handler=CorrectionCharacters");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.True(missing.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("Readback")]
    [InlineData("CorrectionCharacters")]
    public async Task B5RemediationDisabledSessionReviewEndpointsReturnLoginWithoutData(string handler)
    {
        var setup = await SeedAsync(3, true);
        Guid submissionId;
        await using (var seed = new ApplicationDbContext(options))
        {
            submissionId = (await Service(seed).CreateAsync(Command(setup))).SubmissionId;
            var admin = await seed.Accounts.SingleAsync(x => x.Id == setup.AdminId);
            admin.SetGlobalRole(GlobalRole.SuperAdmin);
            admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "password"), false, now, false);
            await seed.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedTimeProvider(now)); }));
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var authenticated = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = "admin", ["Input.Password"] = "password", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, authenticated.StatusCode);
        var path = $"/Admin/Review/Details/{submissionId}?handler={handler}";
        using var available = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);
        Assert.NotEmpty(await available.Content.ReadAsStringAsync());
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.Accounts.SingleAsync(x => x.Id == setup.AdminId)).Disable(now);
            await db.SaveChangesAsync();
        }
        using var refused = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = new Uri(client.BaseAddress!, refused.Headers.Location!);
        Assert.Equal("/Account/Login", location.AbsolutePath);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("true", query["accessChanged"].ToString());
        Assert.Equal(path, query["ReturnUrl"].ToString());
        Assert.Equal(2, query.Count);
        Assert.Empty(await refused.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("moved")]
    [InlineData("other-team")]
    [InlineData("other-event")]
    public async Task B5RemediationCorrectionChecksActualOtherTeamAndEventAssignments(string scenario)
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var created = await service.CreateAsync(Command(setup));
        var submission = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == created.SubmissionId);
        var eventId = setup.EventId;
        if (scenario == "other-event")
        {
            var other = new BingoEvent(Guid.NewGuid(), "Other event", "other-event", "UTC", setup.AdminId, now, PlacementRule.LegacyScoreTimeThenEhb);
            db.Events.Add(other); eventId = other.Id;
        }
        var team = new Team(Guid.NewGuid(), eventId, "Other team", "other-team", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 10, now.AddDays(-5), SignupSource.Website);
        var character = new OsrsCharacter(Guid.NewGuid(), "Other player", "OTHER PLAYER", now.AddDays(-5));
        db.AddRange(team, participant, character,
            new EventParticipantCharacter(Guid.NewGuid(), eventId, participant.Id, character.Id, 0, now.AddDays(-5), setup.AdminId, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null),
            new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now.AddSeconds(2), null, "Current other team"));
        if (scenario == "moved")
        {
            var previous = new TeamMembership(Guid.NewGuid(), setup.TeamId, participant.Id, TeamMembershipRole.Participant, now.AddDays(-4), null, null);
            previous.Leave(now.AddSeconds(1), "Moved after upload"); db.TeamMemberships.Add(previous);
        }
        await db.SaveChangesAsync();
        var baseline = await B5EvidenceStateAsync();
        var choices = await service.GetCorrectionCharactersAsync(submission.Id, setup.AdminId);
        if (scenario == "moved")
        {
            Assert.True(Assert.Single(choices, x => x.CharacterId == character.Id).LeftTeam);
            await service.EditMetadataAsync(new(submission.Id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character.Id, "Correct former team credit", submission.Version));
            var corrected = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission.Id);
            Assert.Equal(setup.TeamId, corrected.TeamId); Assert.Equal(participant.Id, corrected.CreditedParticipantId);
            Assert.Equal(submission.SubmittedAt, corrected.SubmittedAt);
        }
        else
        {
            Assert.DoesNotContain(choices, x => x.CharacterId == character.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.EditMetadataAsync(new(submission.Id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character.Id, "Invalid attribution", submission.Version)));
            Assert.Equal(baseline, await B5EvidenceStateAsync());
        }
    }

    private sealed class B5AnyReviewReadFailure(string table) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM " + table, StringComparison.Ordinal)) throw new TimeoutException("Controlled read failure");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static async Task AssertCrossTeamPagesAsync(HttpClient client, string slug, Guid participantId)
    {
        using var boardResponse = await client.GetAsync($"/Events/{slug}/Board?view=leaderboards&ranking=players");
        Assert.Equal(HttpStatusCode.OK, boardResponse.StatusCode);
        var boardHtml = await boardResponse.Content.ReadAsStringAsync();
        var rows = Regex.Matches(boardHtml, "<tr[^>]*data-sort-player=\"Player One\"[^>]*>").Select(x => x.Value).Where(x => x.Contains("data-sort-team=", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Single(rows, x => x.Contains("data-sort-team=\"Team One\"", StringComparison.Ordinal));
        Assert.Single(rows, x => x.Contains("data-sort-team=\"Team Two\"", StringComparison.Ordinal));
        Assert.All(rows, x => Assert.Equal(12m, decimal.Parse(Regex.Match(x, "data-sort-drop-ehb=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture)));
        using var statsResponse = await client.GetAsync($"/Events/{slug}/Stats");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        var statsHtml = await statsResponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(Regex.Match(statsHtml, "<script id=\"stats-data\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value);
        var teams = json.RootElement.GetProperty("stats").GetProperty("teams").EnumerateArray().ToArray();
        Assert.Equal(2, teams.Length);
        Assert.Equal(B5RemediationTeamNames, teams.Select(x => x.GetProperty("name").GetString()).Order().ToArray());
        Assert.All(teams, team => Assert.Single(team.GetProperty("players").EnumerateArray(), x => x.GetProperty("playerId").GetGuid() == participantId));
    }
}
