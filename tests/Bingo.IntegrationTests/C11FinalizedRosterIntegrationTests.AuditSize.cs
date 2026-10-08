using System.Globalization;
using System.Net;
using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

// Post-draft roster corrections at the real event's size (44 players, 4 teams of 11). The
// audit before/after once held the whole published roster, which exceeded the 4,000-character
// audit columns and made Teams AddMember/RemoveMember fail with PostgreSQL 22001.
public sealed partial class C11FinalizedRosterIntegrationTests
{
    private const int AuditColumnLimit = 4_000;

    [Fact]
    public async Task FinalizedRosterAddAndRemoveAtFortyFourPlayersWriteChangeOnlyAuditsWithinColumnLimits()
    {
        var seed = await SeedAsync();
        var (thirdTeamId, fourthTeamId) = await GrowRosterToFortyFourAsync(seed);
        await using (var db = Db())
        {
            var cycle = await db.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
            Assert.Equal(44, await db.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycle.Id));
            foreach (var teamId in new[] { seed.TeamId, seed.SecondTeamId, thirdTeamId, fourthTeamId })
                Assert.Equal(11, await db.TeamMemberships.CountAsync(x => x.TeamId == teamId && x.LeftAt == null));
        }

        await using var factory = Factory();
        using var admin = await LoginAsync(factory, "c11-admin");
        var draftPath = $"/Admin/Events/Draft/{seed.EventId}";

        long membershipVersion;
        await using (var db = Db()) membershipVersion = await db.TeamMemberships.Where(x => x.Id == seed.DepartedMembershipId).Select(x => x.Version).SingleAsync();
        using (var response = await PostAsync(admin, draftPath, "RemoveMember", await admin.GetStringAsync(draftPath), new()
        {
            ["membershipId"] = seed.DepartedMembershipId.ToString(),
            ["confirmed"] = "true",
            ["expectedMembershipVersion"] = membershipVersion.ToString(CultureInfo.InvariantCulture),
            ["rosterTeamId"] = seed.TeamId.ToString()
        })) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        long teamVersion;
        await using (var db = Db()) teamVersion = await db.Teams.Where(x => x.Id == seed.TeamId).Select(x => x.Version).SingleAsync();
        using (var response = await PostAsync(admin, draftPath, "AddMember", await admin.GetStringAsync(draftPath), new()
        {
            ["teamId"] = seed.TeamId.ToString(),
            ["participantId"] = seed.WaitingId.ToString(),
            ["role"] = TeamMembershipRole.Captain.ToString(),
            ["confirmed"] = "true",
            ["expectedTeamVersion"] = teamVersion.ToString(CultureInfo.InvariantCulture),
            ["rosterTeamId"] = seed.TeamId.ToString()
        })) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using (var verify = Db())
        {
            Assert.NotNull((await verify.TeamMemberships.SingleAsync(x => x.Id == seed.DepartedMembershipId)).LeftAt);
            var added = await verify.TeamMemberships.SingleAsync(x => x.EventParticipantId == seed.WaitingId && x.LeftAt == null);
            Assert.Equal(seed.TeamId, added.TeamId);
            Assert.Equal(TeamMembershipRole.Captain, added.Role);
            var cycle = await verify.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
            Assert.Equal(3, cycle.CycleNumber);
            Assert.Equal(44, await verify.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycle.Id));

            var removal = await verify.AuditEntries.SingleAsync(x => x.EventId == seed.EventId && x.Action == "roster.finalized_removed");
            AssertWithinColumns(removal);
            Assert.Equal("membership", removal.TargetType);
            Assert.Equal(seed.DepartedMembershipId.ToString(), removal.TargetId);
            AssertChange(removal.BeforeState!, seed.DepartedId, "Departed C", seed.TeamId, "C11 team one", onRoster: true, role: "Captain", cycle: 1, rosterCount: 44, teamCount: 11);
            AssertChange(removal.AfterState!, seed.DepartedId, "Departed C", seed.TeamId, "C11 team one", onRoster: false, role: null, cycle: 2, rosterCount: 43, teamCount: 10);
            Assert.Contains("removed-membership-retained-participant", removal.Details);

            var addition = await verify.AuditEntries.SingleAsync(x => x.EventId == seed.EventId && x.Action == "roster.finalized_added");
            AssertWithinColumns(addition);
            Assert.Equal(added.Id.ToString(), addition.TargetId);
            AssertChange(addition.BeforeState!, seed.WaitingId, "Waiting C", seed.TeamId, "C11 team one", onRoster: false, role: null, cycle: 2, rosterCount: 43, teamCount: 10);
            AssertChange(addition.AfterState!, seed.WaitingId, "Waiting C", seed.TeamId, "C11 team one", onRoster: true, role: "Captain", cycle: 3, rosterCount: 44, teamCount: 11);
            Assert.Contains("reused-participant", addition.Details);

            // The whole published roster is no longer copied into the audit entry.
            foreach (var entry in new[] { removal, addition })
                Assert.DoesNotContain("Rosterplay", entry.BeforeState + entry.AfterState + entry.Details, StringComparison.Ordinal);
            Assert.All(await verify.AuditEntries.Where(x => x.EventId == seed.EventId).ToListAsync(), AssertWithinColumns);
        }

        // The Audit page still presents both entries with the member and the changed fields.
        var removedPage = WebUtility.HtmlDecode(await admin.GetStringAsync("/Admin/Audit?action=roster.finalized_removed"));
        Assert.Contains("Removed from a finalized roster", removedPage);
        Assert.Contains("Membership · Departed C", removedPage);
        Assert.Contains("Published roster count", removedPage);
        Assert.Contains("Publication cycle number", removedPage);
        var addedPage = WebUtility.HtmlDecode(await admin.GetStringAsync("/Admin/Audit?action=roster.finalized_added"));
        Assert.Contains("Added to a finalized roster", addedPage);
        Assert.Contains("Membership · Waiting C", addedPage);
        Assert.Contains("Captain", addedPage);
        Assert.Contains("Published team member count", addedPage);
    }

    private static void AssertWithinColumns(AuditEntry entry)
    {
        Assert.True((entry.Details?.Length ?? 0) <= AuditColumnLimit, $"{entry.Action} details: {entry.Details?.Length}");
        Assert.True((entry.BeforeState?.Length ?? 0) <= AuditColumnLimit, $"{entry.Action} before: {entry.BeforeState?.Length}");
        Assert.True((entry.AfterState?.Length ?? 0) <= AuditColumnLimit, $"{entry.Action} after: {entry.AfterState?.Length}");
    }

    private static void AssertChange(string json, Guid participantId, string name, Guid teamId, string teamName, bool onRoster, string? role, int cycle, int rosterCount, int teamCount)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(participantId, root.GetProperty("participant").GetProperty("id").GetGuid());
        Assert.Equal(name, root.GetProperty("participant").GetProperty("name").GetString());
        Assert.Equal(teamId, root.GetProperty("teamId").GetGuid());
        Assert.Equal(teamName, root.GetProperty("teamName").GetString());
        Assert.Equal(onRoster, root.GetProperty("onPublishedRoster").GetBoolean());
        Assert.Equal(role, root.GetProperty("role").GetString());
        Assert.Equal(cycle, root.GetProperty("publicationCycleNumber").GetInt32());
        Assert.Equal(rosterCount, root.GetProperty("publishedRosterCount").GetInt32());
        Assert.Equal(teamCount, root.GetProperty("publishedTeamMemberCount").GetInt32());
    }

    // Adds two teams and 41 confirmed, published participants so the finalized roster is the
    // real event's 44 players in four teams of 11 (the base seed has 2 + 1 published members).
    private async Task<(Guid Third, Guid Fourth)> GrowRosterToFortyFourAsync(Seed seed)
    {
        await using var db = Db();
        var now = clock.Now;
        var third = new Team(Guid.NewGuid(), seed.EventId, "C11 team three", "c11-three", TeamFormationType.Drafted, null, true, now.AddDays(-1));
        var fourth = new Team(Guid.NewGuid(), seed.EventId, "C11 team four", "c11-four", TeamFormationType.Drafted, null, true, now.AddDays(-1));
        third.SetDraftPosition(3); fourth.SetDraftPosition(4); third.Finalize(now.AddHours(-1)); fourth.Finalize(now.AddHours(-1));
        db.AddRange(third, fourth);
        var additions = Enumerable.Repeat(seed.TeamId, 9).Concat(Enumerable.Repeat(seed.SecondTeamId, 10))
            .Concat(Enumerable.Repeat(third.Id, 11)).Concat(Enumerable.Repeat(fourth.Id, 11)).ToList();
        for (var index = 0; index < additions.Count; index++)
        {
            var name = $"Rosterplay{index + 1:00}";
            var owner = Account.CreateWebsite(Guid.NewGuid(), $"c11-roster-{index + 1:00}", $"C11-ROSTER-{index + 1:00}", now.AddDays(-3));
            var participant = new EventParticipant(Guid.NewGuid(), seed.EventId, SignupStatus.Confirmed, 100 + index, now.AddDays(-2).AddMinutes(100 + index), SignupSource.Website);
            participant.AssignOwner(owner);
            var character = new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now.AddDays(-2));
            var membership = new TeamMembership(Guid.NewGuid(), additions[index], participant.Id, TeamMembershipRole.Participant, now.AddHours(-2), null, "Fixture preassignment");
            db.AddRange(owner, participant, character,
                new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 25m, now),
                new EventParticipantCharacter(Guid.NewGuid(), seed.EventId, participant.Id, character.Id, 0, now.AddHours(-4), owner.Id, seed.PrimaryQuestionId, EventCharacterRole.Playing, 25, EhbSource.Manual, null),
                new SignupAnswer(Guid.NewGuid(), participant.Id, seed.PrimaryQuestionId, "Primary", "", character.Id),
                membership,
                new DraftPublicationRoster(Guid.NewGuid(), seed.InitialCycleId, additions[index], participant.Id, TeamMembershipRole.Participant, null, name));
        }
        await db.SaveChangesAsync();
        return (third.Id, fourth.Id);
    }
}
