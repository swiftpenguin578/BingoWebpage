using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

// Brief 87 item 0c: the Participants list read on PostgreSQL.
public sealed partial class U5ParticipantsServerIntegrationTests
{
    // G3b-3 / TD-2 B: manual-team members are listed, counted and hold queue positions.
    [Fact]
    public async Task ListIncludesManualTeamMembersInRowsCountsAndWaitingPositions()
    {
        var world = await SeedAsync(capacity: 2, confirmed: 2, waiting: 2);
        var manualWaiter = world.Participants[2];
        await using (var db = Db())
        {
            var team = new Team(Guid.NewGuid(), world.EventId, "Manual team", $"manual-{Guid.NewGuid():N}", TeamFormationType.Preformed, null, false, DateTimeOffset.UtcNow);
            db.AddRange(team, new TeamMembership(Guid.NewGuid(), team.Id, manualWaiter, TeamMembershipRole.Participant, DateTimeOffset.UtcNow, null, "manual"));
            await db.SaveChangesAsync();
        }
        await using var read = Db();
        var item = await read.Events.AsNoTracking().SingleAsync(x => x.Id == world.EventId);
        var view = await new ParticipantsListReader(read).ReadAsync(item, ParticipantsQuery.From("waiting", null, null, null, null, null, null), CancellationToken.None);
        Assert.Equal(new ParticipantsCounts(2, 2, 0, 4, 4, 2), view.Counts);
        Assert.Equal([manualWaiter, world.Participants[3]], view.Rows.Select(x => x.Id));
        Assert.Equal([1, 2], view.Rows.Select(x => x.WaitingPosition));
        Assert.Equal("Manual team", view.Rows[0].Team);
    }

    // B-Participants-5: list search matches every account RSN, the website username and
    // the Discord name; the Add search matches username, Discord name or saved RSN only
    // when typed (no "Recently joined" list). B-Participants-6: Paid/Unpaid stays.
    [Fact]
    public async Task ListAndAddSearchMatchAccountsUsernamesAndDiscordNames()
    {
        var world = await SeedAsync(capacity: 3, confirmed: 3, waiting: 0);
        var target = world.Participants[1];
        await using (var db = Db())
        {
            var owner = await db.Accounts.SingleAsync(x => x.Id == world.Owners[1]);
            owner.SetDiscordIdentity($"discord-{Guid.NewGuid():N}", "Kiwi Crab Fan");
            var second = new OsrsCharacter(Guid.NewGuid(), "Iron Kiwi", $"IRON KIWI {Guid.NewGuid():N}", DateTimeOffset.UtcNow);
            db.AddRange(second, new EventParticipantCharacter(Guid.NewGuid(), world.EventId, target, second.Id, 5, DateTimeOffset.UtcNow, world.AdminId, world.SecondQuestionId, EventCharacterRole.Playing, 3, EhbSource.Manual, null));
            (await db.EventParticipants.SingleAsync(x => x.Id == world.Participants[0])).SetPaymentStatus(PaymentStatus.Paid);
            await db.SaveChangesAsync();
        }
        await using var read = Db();
        var item = await read.Events.AsNoTracking().SingleAsync(x => x.Id == world.EventId);
        var reader = new ParticipantsListReader(read);
        async Task<IReadOnlyList<Guid>> Search(string? q, string? pay = null) =>
            (await reader.ReadAsync(item, ParticipantsQuery.From("all", pay, q, null, null, null, null), CancellationToken.None)).Rows.Select(x => x.Id).ToList();
        var username = await read.Accounts.Where(x => x.Id == world.Owners[1]).Select(x => x.PublicUsername).SingleAsync();
        Assert.Equal([target], await Search("iron kiwi"));
        Assert.Equal([target], await Search("crab fan"));
        Assert.Equal([target], await Search(username![^10..]));
        Assert.Equal([world.Participants[0]], await Search(null, "paid"));
        Assert.Equal([world.Participants[1], world.Participants[2]], await Search(null, "unpaid"));
        var row = (await reader.ReadAsync(item, ParticipantsQuery.From("all", null, "iron kiwi", null, null, null, null), CancellationToken.None)).Rows.Single();
        Assert.Equal(1, row.ExtraPlaying);

        Assert.Empty(await reader.SearchOwnersAsync(world.EventId, "", CancellationToken.None));
        var byDiscord = await reader.SearchOwnersAsync(world.EventId, "crab fan", CancellationToken.None);
        Assert.Equal(world.Owners[1], Assert.Single(byDiscord).Id);
        Assert.Equal("active", byDiscord[0].InEvent);
        var byRsn = await reader.SearchOwnersAsync(world.EventId, "U5 Seed 2", CancellationToken.None);
        Assert.Equal(world.Owners[2], Assert.Single(byRsn).Id);
        Assert.Equal("U5 Seed 2", byRsn[0].MatchedAccount);
        Assert.Equal(world.Owners[0], Assert.Single(await reader.SearchOwnersAsync(world.EventId, (await read.Accounts.Where(x => x.Id == world.Owners[0]).Select(x => x.PublicUsername).SingleAsync())!, CancellationToken.None)).Id);
    }

    [Fact]
    public async Task ListSortsAndClampsPagesFromTheQuery()
    {
        var world = await SeedAsync(capacity: 5, confirmed: 5, waiting: 0);
        await using var read = Db();
        var item = await read.Events.AsNoTracking().SingleAsync(x => x.Id == world.EventId);
        var reader = new ParticipantsListReader(read);
        var byEhb = await reader.ReadAsync(item, ParticipantsQuery.From("confirmed", null, null, "ehb", null, null, "10"), CancellationToken.None);
        Assert.Equal(world.Participants.AsEnumerable().Reverse(), byEhb.Rows.Select(x => x.Id));
        var clamped = await reader.ReadAsync(item, ParticipantsQuery.From("confirmed", null, null, "signup", "asc", "9", "10"), CancellationToken.None);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(world.Participants, clamped.Rows.Select(x => x.Id));
        var fallback = ParticipantsQuery.From("bogus", "bogus", null, "bogus", "sideways", "-3", "7");
        Assert.Equal(("confirmed", "any", "signup", false, 1, 25), (fallback.Tab, fallback.Pay, fallback.EffectiveSort, fallback.Descending, fallback.Page, fallback.PerPage));
    }
}
