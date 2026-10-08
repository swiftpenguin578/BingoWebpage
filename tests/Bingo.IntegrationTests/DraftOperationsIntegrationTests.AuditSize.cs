using System.Text.Json;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class DraftOperationsIntegrationTests
{
    // draft.team_removed lists the memberships the removal ended. They are recorded once (after
    // state), so removing a team that holds the whole 50-player event stays inside the
    // 4,000-character audit columns; listing them in both states exceeded the limit.
    [Fact]
    public async Task RemovingAFiftyMemberTeamWritesTheEndedMembershipsOnceWithinTheAuditColumns()
    {
        var setup = await SeedAsync();
        var secondId = await TeamIdAsync(setup.EventId, "Second");
        await using (var seed = new ApplicationDbContext(options))
        {
            for (var index = 0; index < 50; index++)
            {
                var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 1_000 + index, now.AddDays(-1).AddMinutes(index), SignupSource.Website);
                seed.AddRange(participant, new TeamMembership(Guid.NewGuid(), secondId, participant.Id, TeamMembershipRole.Participant, now.AddHours(-1), null, "Fixture preassignment"));
            }
            await seed.SaveChangesAsync();
        }
        List<Guid> memberships;
        await using (var read = new ApplicationDbContext(options))
            memberships = await read.TeamMemberships.Where(value => value.TeamId == secondId && value.LeftAt == null).Select(value => value.Id).ToListAsync();
        Assert.True(memberships.Count >= 50);

        var removed = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostRemoveDraftTeamAsync(setup.EventId, secondId, CancellationToken.None, confirmRemoveMembers: true));
        Assert.Equal($"Second removed. {memberships.Count} players are signed up with no team.", removed);

        await using var verify = new ApplicationDbContext(options);
        Assert.False((await verify.Teams.SingleAsync(value => value.Id == secondId)).Active);
        var audit = await verify.AuditEntries.SingleAsync(value => value.EventId == setup.EventId && value.Action == "draft.team_removed");
        Assert.True(audit.Details!.Length <= 4_000, $"draft.team_removed details: {audit.Details.Length}");
        using var details = JsonDocument.Parse(audit.Details);
        var before = details.RootElement.GetProperty("before");
        var after = details.RootElement.GetProperty("after");
        Assert.True(before.GetProperty("Active").GetBoolean());
        Assert.False(before.TryGetProperty("EndedMembershipIds", out _));
        Assert.False(after.GetProperty("Active").GetBoolean());
        Assert.Equal(memberships.Order(), after.GetProperty("EndedMembershipIds").EnumerateArray().Select(value => value.GetGuid()).Order());
    }
}
