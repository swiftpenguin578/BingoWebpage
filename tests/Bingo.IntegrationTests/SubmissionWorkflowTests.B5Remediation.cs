using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Evidence;
using Bingo.Domain.Teams;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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
        await service.ApproveAsync(first.SubmissionId, setup.AdminId);
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
        await Service(db, new FixedTimeProvider(now.AddSeconds(3))).ApproveAsync(second.Id, setup.AdminId);
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
