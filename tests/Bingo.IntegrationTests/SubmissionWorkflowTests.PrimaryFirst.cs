using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class SubmissionWorkflowTests
{
    [Fact]
    public async Task PublicBoardShowsThePrimaryPlayingAccountFirstEvenWithAHigherRegistrationOrder()
    {
        var setup = await SeedAsync(target: 2, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var submissions = Service(db);
        var approved = await submissions.CreateAsync(Command(setup));
        await submissions.ApproveCurrentAsync(approved.SubmissionId, setup.AdminId);

        // The primary ("Player One", linked to the primary question) is registered after the second account.
        var primary = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == setup.ParticipantId);
        db.Entry(primary).Property(x => x.RegistrationOrder).CurrentValue = 5;
        var second = new OsrsCharacter(Guid.NewGuid(), "Second account", "SECOND ACCOUNT", now);
        db.AddRange(second, new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, setup.ParticipantId, second.Id, 1, now, setup.AdminId, null,
            EventCharacterRole.Playing, 1, EhbSource.Manual, null));
        await db.SaveChangesAsync();

        var board = await new PublicBoardService(db, new FixedTimeProvider(now)).GetEventBoardAsync($"event-{setup.EventId:N}");

        Assert.Equal(["Player One", "Second account"], Assert.Single(board!.RosterPlayers!).PlayingAccountNames);
        var dropPlayer = Assert.Single(Assert.Single(board.DropEhbTeams!).Players);
        Assert.Equal("Player One", dropPlayer.PlayerName);
        Assert.Equal(["Player One", "Second account"], dropPlayer.PlayingAccountNames);
    }
}
