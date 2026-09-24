using Bingo.Application.Boards;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Events;
using Bingo.Web.UI;

namespace Bingo.BrowserTests;

public sealed class EventMastheadModelTests
{
    [Theory]
    [InlineData(EventState.Archived)]
    [InlineData(EventState.Live)]
    [InlineData(EventState.SignupClosed)]
    public void SharedMastheadPreservesEventFactsAndTheExistingPlayerSelections(EventState state)
    {
        var teamId = Guid.NewGuid();
        var anna = Guid.NewGuid();
        var bert = Guid.NewGuid();
        var missing = Guid.NewGuid();
        PublicRosterPlayer Roster(Guid id, string name) => new(teamId, "Team", id, name, [name]);
        var board = new PublicEventBoard(Guid.NewGuid(), "Event", "event", state, 5, 5, 100m, [], [], [],
            EventStartsAt: new DateTimeOffset(2026, 7, 14, 16, 0, 0, TimeSpan.Zero),
            EventEndsAt: new DateTimeOffset(2026, 7, 19, 16, 0, 0, TimeSpan.Zero),
            EventResult: state == EventState.Archived ? new("Team", "team", true) : null,
            SubmissionsOpen: state == EventState.Live,
            DropEhbTeams: [new(1, teamId, "Team", 3, 2, 9, 30m, [],
                [new(anna, "Anna", 10m, 1, 2), new(bert, "Bert", 20m, 1, 7)])],
            RosterPlayers: [Roster(bert, "Bert"), Roster(missing, "Missing"), Roster(anna, "Anna")],
            WiseOldManCompetitionId: 145197);
        var activity = new EventCompetitionActivityProjection(EventCompetitionActivityState.Partial, 1, null, null,
            [new(teamId, "Team", 10m, 5m,
                [new(bert, "Bert", 5m, []), new(anna, "Anna", 5m, [])], [])]);

        var masthead = new EventMastheadModel(board, activity, "team");

        Assert.Same(board, masthead.Board);
        Assert.Same(activity, masthead.Activity);
        Assert.Equal("team", masthead.SubmissionTeamSlug);
        Assert.Equal(["Anna", "Bert", "Missing"], masthead.Players.Select(player => player.Participant.ParticipantName));
        Assert.Equal([1, 1, 0], masthead.Players.Select(player => player.Rank));
        Assert.Equal(bert, masthead.MostSpooned?.Participant.ParticipantId);
        Assert.Equal(bert, masthead.HighestDropEhb?.Participant.ParticipantId);
        Assert.Equal(anna, masthead.HighestEhb?.Participant.ParticipantId);
        Assert.False(masthead.Players[^1].HasActivity);
    }

    [Fact]
    public void UnavailableActivityKeepsDropRankingButDoesNotInventHighestEhb()
    {
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var board = new PublicEventBoard(Guid.NewGuid(), "Event", "event", EventState.Live, 5, 5, 100m, [], [], [],
            DropEhbTeams: [new(1, teamId, "Team", 1, 1, 2, 10m, [], [new(playerId, "Player", 10m, 1, 2)])],
            RosterPlayers: [new(teamId, "Team", playerId, "Player", ["Player"])]);
        var activity = new EventCompetitionActivityProjection(EventCompetitionActivityState.NotConfigured, 0, null, null, []);

        var masthead = new EventMastheadModel(board, activity, null);

        Assert.Null(masthead.HighestEhb);
        Assert.Equal(playerId, masthead.MostSpooned?.Participant.ParticipantId);
        Assert.Equal(playerId, masthead.HighestDropEhb?.Participant.ParticipantId);
        Assert.False(masthead.Players.Single().HasActivity);
        Assert.Null(masthead.SubmissionTeamSlug);
    }
}
