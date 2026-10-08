using System.Net;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bingo.IntegrationTests;

// U6 item 0a (brief 93): team structure server rules on PostgreSQL.
public sealed partial class DraftOperationsIntegrationTests
{
    // B6-c1: the Edit-team dialog sends no affiliation or image; rename and inclusion
    // changes keep the stored affiliation and the active image, and the public team
    // page still shows the affiliation. Sending affiliation empty still clears it.
    [Fact]
    public async Task U6RenameAndInclusionToggleKeepAffiliationAndActiveImage()
    {
        var setup = await SeedAsync();
        Guid teamId, imageId;
        string slug;
        await using (var seed = new ApplicationDbContext(options))
        {
            var team = await seed.Teams.SingleAsync(value => value.EventId == setup.EventId && value.Name == "Second");
            team.Update(team.Name, team.Slug, "Clan Kept", null);
            imageId = Guid.NewGuid();
            seed.TeamImageAssets.Add(new TeamImageAsset(imageId, setup.EventId, team.Id, $"teams/{imageId:N}.png", "crest.png", "image/png", 10, 1, 1, "checksum", setup.FirstAdminId, now));
            team.SetActiveImage(imageId);
            await seed.SaveChangesAsync();
            teamId = team.Id;
            slug = await seed.Events.Where(value => value.Id == setup.EventId).Select(value => value.Slug).SingleAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var route = $"/Admin/Events/Draft/{setup.EventId}";
        var token = AntiforgeryToken(await client.GetStringAsync(route));

        async Task<long> VersionAsync()
        {
            await using var read = new ApplicationDbContext(options);
            return await read.Teams.Where(value => value.Id == teamId).Select(value => value.Version).SingleAsync();
        }
        async Task AssertKeptAsync(string expectedName)
        {
            await using var read = new ApplicationDbContext(options);
            var team = await read.Teams.SingleAsync(value => value.Id == teamId);
            Assert.Equal(expectedName, team.Name);
            Assert.Equal("Clan Kept", team.AffiliationName);
            Assert.Equal(imageId, team.ActiveImageAssetId);
            Assert.Null((await read.TeamImageAssets.SingleAsync(value => value.Id == imageId)).ReplacedAt);
        }

        // The public team projection (Teams page model; the team board reads the same
        // stored AffiliationName) still carries the affiliation for the renamed team.
        async Task AssertPublicAffiliationAsync(string name)
        {
            using (var page = await client.GetAsync($"/Events/{slug}/Teams")) Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            await using var publicDb = new ApplicationDbContext(options);
            var model = new Bingo.Web.Pages.Events.TeamsModel(publicDb, new FixedTimeProvider(now));
            model.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext(new Microsoft.AspNetCore.Mvc.ActionContext(new Microsoft.AspNetCore.Http.DefaultHttpContext(), new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.RazorPages.PageActionDescriptor()))
            { ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()) };
            await model.OnGetAsync(slug, null, CancellationToken.None);
            Assert.Equal("Clan Kept", Assert.Single(model.Teams, team => team.Name == name).Affiliation);
        }

        using (var rename = await client.PostAsync($"{route}?handler=UpdateTeam", Form(token, new() { ["teamId"] = teamId.ToString(), ["name"] = "Second renamed", ["version"] = (await VersionAsync()).ToString(System.Globalization.CultureInfo.InvariantCulture) })))
            Assert.Equal(HttpStatusCode.Redirect, rename.StatusCode);
        await AssertKeptAsync("Second renamed");

        using (var toggle = await client.PostAsync($"{route}?handler=UpdateTeam", Form(token, new() { ["teamId"] = teamId.ToString(), ["name"] = "Second renamed", ["version"] = (await VersionAsync()).ToString(System.Globalization.CultureInfo.InvariantCulture), ["includedInDraft"] = "false" })))
            Assert.Equal(HttpStatusCode.Redirect, toggle.StatusCode);
        await AssertKeptAsync("Second renamed");
        await using (var read = new ApplicationDbContext(options))
            Assert.False((await read.Teams.SingleAsync(value => value.Id == teamId)).IncludedInDraft);

        // One drafted team and one manual team finalize directly from setup; the public
        // team page then shows the kept affiliation.
        using (var finalize = await client.PostAsync($"{route}?handler=Finalize", Form(token, new() { ["confirmed"] = "true" })))
            Assert.Equal(HttpStatusCode.Redirect, finalize.StatusCode);
        await AssertPublicAffiliationAsync("Second renamed");

        // A finalized rename before the event starts keeps them too.
        using (var finalRename = await client.PostAsync($"{route}?handler=UpdateTeam", Form(token, new() { ["teamId"] = teamId.ToString(), ["name"] = "Second final", ["version"] = (await VersionAsync()).ToString(System.Globalization.CultureInfo.InvariantCulture) })))
            Assert.Equal(HttpStatusCode.Redirect, finalRename.StatusCode);
        await AssertKeptAsync("Second final");
        await AssertPublicAffiliationAsync("Second final");

        // "Sent empty" is different from "not sent": an explicit empty value clears it.
        using (var cleared = await client.PostAsync($"{route}?handler=UpdateTeam", Form(token, new() { ["teamId"] = teamId.ToString(), ["name"] = "Second final", ["version"] = (await VersionAsync()).ToString(System.Globalization.CultureInfo.InvariantCulture), ["affiliation"] = "" })))
            Assert.Equal(HttpStatusCode.Redirect, cleared.StatusCode);
        await using (var read = new ApplicationDbContext(options))
        {
            var team = await read.Teams.SingleAsync(value => value.Id == teamId);
            Assert.Null(team.AffiliationName);
            Assert.Equal(imageId, team.ActiveImageAssetId);
        }
    }

    // S7: a populated team needs the confirmation intent; without it nothing changes.
    // With it the memberships end, the players stay signed up with no team, and a
    // Captain's role ends with a recorded transition. An empty team needs no intent.
    [Fact]
    public async Task U6RemovingAPopulatedTeamNeedsTheIntentAndKeepsThePlayersSignedUp()
    {
        var setup = await SeedAsync();
        var secondId = await TeamIdAsync(setup.EventId, "Second");
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddMemberAsync(setup.EventId, secondId, setup.PlayerIds[2], "Preassigned", CancellationToken.None));
        var before = await RosterStateAsync();

        var refused = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostRemoveDraftTeamAsync(setup.EventId, secondId, CancellationToken.None));
        Assert.Equal("Second now has members. Nothing was removed; check the team before trying again.", refused);
        Assert.Equal(before, await RosterStateAsync());

        var removed = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostRemoveDraftTeamAsync(setup.EventId, secondId, CancellationToken.None, confirmRemoveMembers: true));
        Assert.Equal("Second removed. 2 players are signed up with no team.", removed);
        await using (var verify = new ApplicationDbContext(options))
        {
            var team = await verify.Teams.SingleAsync(value => value.Id == secondId);
            Assert.False(team.Active);
            Assert.Empty(await verify.TeamMemberships.Where(value => value.TeamId == secondId && value.LeftAt == null).ToListAsync());
            Assert.All(await verify.EventParticipants.Where(value => value.Id == setup.PlayerIds[1] || value.Id == setup.PlayerIds[2]).ToListAsync(),
                participant => Assert.Equal(SignupStatus.Confirmed, participant.SignupStatus));
            var captainMembership = await verify.TeamMemberships.SingleAsync(value => value.TeamId == secondId && value.EventParticipantId == setup.PlayerIds[1]);
            Assert.Single(await verify.TeamMembershipRoleTransitions.Where(value => value.TeamMembershipId == captainMembership.Id && value.ToRole == TeamMembershipRole.Participant).ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(value => value.EventId == setup.EventId && value.Action == "draft.team_removed").ToListAsync());
        }

        // A manual team with no members is removed without the intent.
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "Manual empty", null, null, CancellationToken.None, false, false));
        var manualId = await TeamIdAsync(setup.EventId, "Manual empty");
        Assert.Equal("Manual empty removed.", await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostRemoveDraftTeamAsync(setup.EventId, manualId, CancellationToken.None)));
    }

    // S7 + TD-8: RemoveDraftTeam and setup AddMember lock the event row, then the draft
    // row. Add first: the removal (with intent) ends the new membership too. Remove
    // first: the addition is refused with a clear message and writes nothing.
    [Fact]
    public async Task U6RemoveDraftTeamAndAddMemberSerializeInBothOrders()
    {
        async Task RunAsync(bool addFirst)
        {
            var setup = await SeedAsync();
            var teamId = await TeamIdAsync(setup.EventId, "Second");
            var boundary = new DraftRowLockBoundary();
            var boundaryOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).AddInterceptors(boundary).Options;
            var observer = new DraftEventLockObserver();
            var observerOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).AddInterceptors(observer).Options;
            Func<DraftModel, Task<Microsoft.AspNetCore.Mvc.IActionResult>> add = page => page.OnPostAddMemberAsync(setup.EventId, teamId, setup.PlayerIds[2], "Race", CancellationToken.None);
            Func<DraftModel, Task<Microsoft.AspNetCore.Mvc.IActionResult>> remove = page => page.OnPostRemoveDraftTeamAsync(setup.EventId, teamId, CancellationToken.None, confirmRemoveMembers: true);
            var firstTask = ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, addFirst ? add : remove, boundaryOptions);
            await boundary.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var secondTask = ExecuteAndReadStatusAsync(setup.EventId, setup.SecondAdminId, addFirst ? remove : add, observerOptions);
            await observer.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var blocked = await WaitForDatabaseBlockAsync(secondTask, observer.BackendPid.Task.Result, boundary.BackendPid.Task.Result);
            boundary.Release.TrySetResult();
            await Task.WhenAll(firstTask, secondTask);
            Assert.True(blocked, "The second operation must wait for the first one's event-row lock.");

            await using var verify = new ApplicationDbContext(options);
            Assert.False((await verify.Teams.SingleAsync(value => value.Id == teamId)).Active);
            Assert.Empty(await verify.TeamMemberships.Where(value => value.TeamId == teamId && value.LeftAt == null).ToListAsync());
            var playerMemberships = await verify.TeamMemberships.Where(value => value.EventParticipantId == setup.PlayerIds[2]).ToListAsync();
            Assert.Equal(SignupStatus.Confirmed, (await verify.EventParticipants.SingleAsync(value => value.Id == setup.PlayerIds[2])).SignupStatus);
            if (addFirst)
            {
                Assert.Contains("added to Second", firstTask.Result, StringComparison.Ordinal);
                Assert.Equal("Second removed. 2 players are signed up with no team.", secondTask.Result);
                var membership = Assert.Single(playerMemberships);
                Assert.NotNull(membership.LeftAt);
            }
            else
            {
                Assert.Equal("Second removed. 1 player is signed up with no team.", firstTask.Result);
                Assert.Equal("Second was removed. Nothing was added.", secondTask.Result);
                Assert.Empty(playerMemberships);
                Assert.Empty(await verify.AuditEntries.Where(value => value.Action == "team.member_added" && value.TargetId == teamId.ToString()).ToListAsync());
            }
        }

        await RunAsync(addFirst: true);
        await RunAsync(addFirst: false);
    }

    // B-Teams-4: adding a team after finalization is retired; nothing is written.
    [Fact]
    public async Task U6AddingATeamAfterFinalizationIsRefused()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        var before = await RosterStateAsync();
        foreach (var included in new[] { false, true })
        {
            var status = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "Late team", null, null, CancellationToken.None, true, included));
            Assert.Equal("Teams can’t be added after the rosters are finalized. Correct the rosters by adding or removing members.", status);
        }
        Assert.Equal(before, await RosterStateAsync());
    }

    // B-Teams-5: unique ignoring case on add and rename; the team's own unchanged name
    // still saves beside older case-variant duplicates (no data rewrite, no index).
    [Fact]
    public async Task U6TeamNamesAreUniqueIgnoringCaseOnAddAndRename()
    {
        var setup = await SeedAsync();
        Assert.Equal("Another team already has this name.", await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostAddTeamAsync(setup.EventId, "  first ", null, null, CancellationToken.None)));
        var secondId = await TeamIdAsync(setup.EventId, "Second");
        long Version(ApplicationDbContext db) => db.Teams.Where(value => value.Id == secondId).Select(value => value.Version).Single();
        long version;
        await using (var read = new ApplicationDbContext(options)) version = Version(read);
        Assert.Equal("Another team already has this name.", await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostUpdateTeamAsync(setup.EventId, secondId, "FIRST", null, null, false, version, CancellationToken.None)));
        await using (var read = new ApplicationDbContext(options)) Assert.Equal("Second", (await read.Teams.SingleAsync(value => value.Id == secondId)).Name);

        // Older data may already hold a case-variant pair; saving under the unchanged name works.
        await using (var legacy = new ApplicationDbContext(options))
        {
            legacy.Teams.Add(new Team(Guid.NewGuid(), setup.EventId, "SECOND", "second-legacy", null, false));
            await legacy.SaveChangesAsync();
        }
        Assert.Equal("Second updated.", await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostUpdateTeamAsync(setup.EventId, secondId, "Second", null, null, false, version, CancellationToken.None)));
        // A case-only change of its own name is checked against the other teams.
        await using (var read = new ApplicationDbContext(options)) version = Version(read);
        Assert.Equal("Another team already has this name.", await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostUpdateTeamAsync(setup.EventId, secondId, "second", null, null, false, version, CancellationToken.None)));
    }
}
