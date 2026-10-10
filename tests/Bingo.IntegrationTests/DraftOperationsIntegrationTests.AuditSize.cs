using System.Globalization;
using System.Text.Json;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.IntegrationTests;

public sealed partial class DraftOperationsIntegrationTests
{
    // draft.team_removed records the memberships the removal ended once (after state). Brief 147 B2
    // (A8): it stores the count and at most 25 ids, so removing a 120-member team (which overflowed
    // the 4,000-character audit column at ~90 members) stays small. The display reads the count.
    [Fact]
    public async Task RemovingALargeTeamStoresTheCountAndAtMostTwentyFiveEndedMembershipIds()
    {
        var setup = await SeedAsync();
        var secondId = await TeamIdAsync(setup.EventId, "Second");
        await using (var seed = new ApplicationDbContext(options))
        {
            for (var index = 0; index < 120; index++)
            {
                var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 1_000 + index, now.AddDays(-1).AddMinutes(index), SignupSource.Website);
                seed.AddRange(participant, new TeamMembership(Guid.NewGuid(), secondId, participant.Id, TeamMembershipRole.Participant, now.AddHours(-1), null, "Fixture preassignment"));
            }
            await seed.SaveChangesAsync();
        }
        List<Guid> memberships;
        await using (var read = new ApplicationDbContext(options))
            memberships = await read.TeamMemberships.Where(value => value.TeamId == secondId && value.LeftAt == null).Select(value => value.Id).ToListAsync();
        Assert.True(memberships.Count >= 120);

        var removed = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostRemoveDraftTeamAsync(setup.EventId, secondId, CancellationToken.None, confirmRemoveMembers: true));
        Assert.Equal($"Second removed. {memberships.Count} players are signed up with no team.", removed);

        await using var verify = new ApplicationDbContext(options);
        Assert.False((await verify.Teams.SingleAsync(value => value.Id == secondId)).Active);
        var audit = await verify.AuditEntries.SingleAsync(value => value.EventId == setup.EventId && value.Action == "draft.team_removed");
        Assert.True(audit.Details!.Length <= 2_500, $"draft.team_removed details: {audit.Details.Length}");
        using var details = JsonDocument.Parse(audit.Details);
        var before = details.RootElement.GetProperty("before");
        var after = details.RootElement.GetProperty("after");
        Assert.True(before.GetProperty("Active").GetBoolean());
        Assert.False(before.TryGetProperty("EndedMembershipIds", out _));
        Assert.False(after.GetProperty("Active").GetBoolean());
        Assert.Equal(memberships.Count, after.GetProperty("EndedMembershipCount").GetInt32());
        var stored = after.GetProperty("EndedMembershipIds").EnumerateArray().Select(value => value.GetGuid()).ToList();
        Assert.Equal(Bingo.Web.Pages.Admin.Events.DraftModel.TeamRemovalAuditIdLimit, stored.Count);
        Assert.All(stored, id => Assert.Contains(id, memberships));
        Assert.Equal(memberships.Count - 25, after.GetProperty("EndedMembershipIdsOmitted").GetInt32());

        // Display: the sentence uses the stored count; the drawer lists 10 accounts, then "and N more".
        var names = await AuditNameResolver.ResolveAsync(verify, [audit], CancellationToken.None);
        var shown = AuditPresenter.Present(audit, new Passthrough(), names);
        Assert.EndsWith($"removed the team Second; {memberships.Count} members left the team.", shown.Summary);
        Assert.DoesNotContain(shown.Changes, change => change.Field.Contains("omitted", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(memberships.Count - shown.Affected!.Count, shown.AffectedMore);
        Assert.Equal(AuditPresenter.AffectedShown, shown.Affected.Count);
    }

    private sealed class Passthrough : IStringLocalizer<AuditResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
