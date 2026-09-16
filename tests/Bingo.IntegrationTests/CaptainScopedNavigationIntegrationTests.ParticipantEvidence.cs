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

namespace Bingo.IntegrationTests;

public sealed partial class CaptainScopedNavigationIntegrationTests
{
    [Fact]
    public async Task StatusLedgerFiltersFullHistoryAndPreservesHttpRoleScope()
    {
        var now = DateTimeOffset.UtcNow;
        var captain = Website("status-captain", now);
        var coCaptain = Website("status-co-captain", now);
        var member = Website("status-member", now);
        var outsider = Website("status-outsider", now);
        var admin = Website("status-admin", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "status-emergency", "STATUS-EMERGENCY", now);
        emergency.Enable();
        var accounts = new[] { captain, coCaptain, member, outsider, admin, emergency };
        foreach (var account in accounts)
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, false);
        var live = LiveEvent(captain.Id, "Status ledger", "status-ledger", now);
        var otherEvent = LiveEvent(outsider.Id, "Other ledger", "other-ledger", now);
        var team = new Team(Guid.NewGuid(), live.Id, "Status team", "status-team", TeamFormationType.Drafted, null, true);
        var opponent = new Team(Guid.NewGuid(), live.Id, "Opponent", "opponent", TeamFormationType.Drafted, null, true);
        var otherTeam = new Team(Guid.NewGuid(), otherEvent.Id, "Other event team", "other-event-team", TeamFormationType.Drafted, null, true);
        var board = new Board(Guid.NewGuid(), live.Id, "Status board", 1, 2);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Status drop tile", "", "", 1m);
        var manualTile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 1, "Status manual tile", "", "", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 100, true, false, "Drop", false);
        var manualRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), manualTile.Id, 0, 100, true, false, "Manual", true);
        var drop = new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, Guid.NewGuid(), Guid.NewGuid(), "Status boss", "Status drop", "1/10", 0.1m, null, 1m);
        var participants = accounts.Take(4).Select((account, index) =>
        {
            var participant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, index + 1, now.AddDays(-1), SignupSource.Website);
            participant.AssignOwner(account);
            return participant;
        }).ToArray();
        var roles = new[] { TeamMembershipRole.Captain, TeamMembershipRole.CoCaptain, TeamMembershipRole.Participant, TeamMembershipRole.Participant };
        var memberships = participants.Select((participant, index) => new TeamMembership(Guid.NewGuid(), index == 3 ? opponent.Id : team.Id, participant.Id, roles[index], now.AddDays(-1), null, "test")).ToArray();
        // Retained evidence credited to a departed teammate remains part of the ledger.
        var departed = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 5, now.AddDays(-1), SignupSource.Website);
        var departedMembership = new TeamMembership(Guid.NewGuid(), team.Id, departed.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "test");
        departedMembership.Leave(now, "fixture departure");
        var character = new OsrsCharacter(Guid.NewGuid(), "Status credit", "STATUS CREDIT", now);
        var access = new AccountEventAccess(Guid.NewGuid(), emergency.Id, live.Id, team.Id, null, now.AddHours(-1), null, null);
        access.Enable();
        var rows = new List<Submission>();
        Submission Add(SubmissionStatus status, Guid? player = null, Guid? teamId = null, bool manual = false, Guid? parent = null)
        {
            var row = new Submission(Guid.NewGuid(), live.Id, teamId ?? team.Id, manual ? manualTile.Id : tile.Id,
                manual ? manualRequirement.Id : requirement.Id, manual ? null : drop.Id, player ?? participants[0].Id,
                character.Id, "Status credit", captain.Id, 1, now.AddMinutes(-rows.Count), null, null, parent);
            if (status == SubmissionStatus.Approved || status == SubmissionStatus.Reversed) row.Approve(1, now);
            if (status == SubmissionStatus.Rejected) row.Reject("fixture feedback", now);
            if (status == SubmissionStatus.Withdrawn) row.Withdraw(now);
            if (status == SubmissionStatus.Reversed) row.Reverse("fixture reversal", now);
            rows.Add(row);
            return row;
        }
        for (var index = 0; index < 26; index++)
        {
            Add(SubmissionStatus.Pending);
            Add(SubmissionStatus.Approved);
        }
        Add(SubmissionStatus.Rejected);
        var replaced = Add(SubmissionStatus.Rejected);
        Add(SubmissionStatus.Pending, parent: replaced.Id);
        Add(SubmissionStatus.Withdrawn, manual: true);
        Add(SubmissionStatus.Reversed);
        Add(SubmissionStatus.Approved, participants[2].Id);
        Add(SubmissionStatus.Rejected, departed.Id);
        Add(SubmissionStatus.Approved, participants[3].Id, opponent.Id);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(accounts);
            db.AddRange(live, otherEvent, team, opponent, otherTeam, board, tile, manualTile, requirement, manualRequirement, drop, character, access, departed, departedMembership);
            db.AddRange(participants);
            db.AddRange(memberships);
            db.AddRange(rows);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, now, [tile, manualTile], [requirement, manualRequirement], [drop]);
        }
        var authorized = rows.Where(row => row.TeamId == team.Id).OrderByDescending(row => row.SubmittedAt).ToArray();
        string DisplayStatus(Submission row) => row.Id == replaced.Id ? "Replaced" : row.Status.ToString();
        static Guid[] DetailIds(string html) => Regex.Matches(html, "href=\"/Submissions/([0-9a-f-]{36})").Select(match => Guid.Parse(match.Groups[1].Value)).ToArray();
        var route = $"/Submissions?eventId={live.Id}&teamId={team.Id}";
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        foreach (var actor in new[] { captain, coCaptain, member, emergency })
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            await LoginAsync(client, actor.LoginName);
            foreach (var status in new[] { "Pending", "Approved", "Rejected", "Withdrawn", "Reversed", "Replaced" })
            {
                var matching = authorized.Where(row => DisplayStatus(row) == status).ToArray();
                var html = await client.GetStringAsync($"{route}&status={status}");
                Assert.Equal(matching.Take(25).Select(row => row.Id), DetailIds(html));
                Assert.Contains($"Showing {Math.Min(25, matching.Length)} of {matching.Length} retained submissions", html);
                Assert.Contains($"value=\"{status}\" selected=\"selected\"", html);
                Assert.Equal(actor != member, html.Contains("id=\"captain-focus-heading\"", StringComparison.Ordinal));
                Assert.Contains("form=\"submissions-ledger-filters\"", html);
                var partial = await client.GetStringAsync($"{route}&handler=Ledger&status={status}&ledgerPage=2");
                Assert.Equal((matching.Length > 25 ? matching.Skip(25) : matching).Select(row => row.Id), DetailIds(partial));
                if (matching.Length > 25)
                {
                    var next = WebUtility.HtmlDecode(Regex.Match(html, "data-captain-ledger-page href=\"([^\"]+)\"").Groups[1].Value);
                    Assert.Contains($"status={status}", next);
                    Assert.Equal(matching.Skip(25).Select(row => row.Id), DetailIds(await client.GetStringAsync(next)));
                }
            }
            var combined = await client.GetStringAsync($"{route}&status=Approved&player={participants[0].Id}&search=STATUS%20DROP&ledgerPage=2");
            var combinedRows = authorized.Where(row => row.Status == SubmissionStatus.Approved && row.CreditedParticipantId == participants[0].Id).ToArray();
            Assert.Equal(combinedRows.Skip(25).Select(row => row.Id), DetailIds(combined));
            Assert.Contains("Showing 1 of 26 retained submissions", combined);
            Assert.Contains("status=Approved", combined);
            Assert.Contains($"player={participants[0].Id}", combined);
            Assert.Contains("search=STATUS%20DROP", combined);
            Assert.Empty(DetailIds(await client.GetStringAsync($"{route}&handler=Ledger&status=Approved&player={participants[3].Id}")));
            Assert.Equal(authorized.Take(25).Select(row => row.Id), DetailIds(await client.GetStringAsync($"{route}&status=unsupported")));
            Assert.Equal(authorized.Where(row => row.Status == SubmissionStatus.Approved).Take(25).Select(row => row.Id), DetailIds(await client.GetStringAsync($"{route}&handler=Ledger&status=approved")));
            foreach (var suffix in new[] { "", "&handler=Ledger" })
            {
                using var wrongTeam = await client.GetAsync($"/Submissions?eventId={live.Id}&teamId={opponent.Id}&status=Approved{suffix}");
                Assert.Equal(HttpStatusCode.NotFound, wrongTeam.StatusCode);
                using var wrongEvent = await client.GetAsync($"/Submissions?eventId={otherEvent.Id}&teamId={otherTeam.Id}&status=Approved{suffix}");
                Assert.Equal(HttpStatusCode.NotFound, wrongEvent.StatusCode);
            }
            using var alias = await client.GetAsync($"/Captain?eventId={live.Id}&teamId={team.Id}&status=Approved&ledgerPage=2");
            Assert.Equal(HttpStatusCode.Redirect, alias.StatusCode);
            Assert.Contains("status=Approved", alias.Headers.Location!.OriginalString);
        }
        foreach (var actor in new[] { outsider, admin })
        {
            using var denied = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            await LoginAsync(denied, actor.LoginName);
            foreach (var suffix in new[] { "", "&handler=Ledger" })
            {
                using var response = await denied.GetAsync($"{route}&status=Rejected{suffix}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymousResponse = await anonymous.GetAsync($"{route}&handler=Ledger&status=Rejected");
        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.StartsWith("http://localhost/Account/Login", anonymousResponse.Headers.Location!.OriginalString);
    }
}
