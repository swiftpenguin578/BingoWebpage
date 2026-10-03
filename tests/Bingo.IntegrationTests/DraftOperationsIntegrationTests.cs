using System.Data;
using System.Data.Common;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Bingo.Infrastructure.Teams;
using Bingo.Web;
using Bingo.Web.Events;
using Bingo.Web.Pages.Admin.Events;
using Bingo.Web.Teams;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class DraftOperationsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_draft_operations")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private readonly DateTimeOffset now = new(2026, 7, 29, 18, 0, 0, TimeSpan.Zero);
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task EmptyDraftWorkspaceRendersWithoutWritesAndCreatesFirstTeamThroughItsModalPost()
    {
        var setup = await SeedAsync(initialPrivate: true);
        await using (var clear = new ApplicationDbContext(options))
        {
            var teamIds = await clear.Teams.Where(team => team.EventId == setup.EventId).Select(team => team.Id).ToListAsync();
            clear.TeamMemberships.RemoveRange(clear.TeamMemberships.Where(membership => teamIds.Contains(membership.TeamId)));
            clear.Teams.RemoveRange(clear.Teams.Where(team => team.EventId == setup.EventId));
            clear.DraftSessions.RemoveRange(clear.DraftSessions.Where(draft => draft.EventId == setup.EventId));
            await clear.SaveChangesAsync();
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

        var emptyHtml = await client.GetStringAsync(route);
        Assert.Contains("data-draft-dialog-open=\"add-team\"", emptyHtml, StringComparison.Ordinal);
        Assert.Contains("data-draft-add-team-dialog", emptyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"formationType\"", emptyHtml, StringComparison.Ordinal);
        await using (var afterGet = new ApplicationDbContext(options))
        {
            Assert.Empty(await afterGet.DraftSessions.Where(draft => draft.EventId == setup.EventId).ToListAsync());
            Assert.Empty(await afterGet.Teams.Where(team => team.EventId == setup.EventId).ToListAsync());
        }

        using var add = await client.PostAsync($"{route}?handler=AddTeam", Form(AntiforgeryToken(emptyHtml), new()
        {
            ["name"] = "First setup team",
            ["includedInDraft"] = "false",
            ["affiliation"] = "Setup clan"
        }));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);

        var savedHtml = await client.GetStringAsync(route);
        Assert.Contains("data-team-name=\"First setup team\"", savedHtml, StringComparison.Ordinal);
        Assert.Contains("First setup team", savedHtml, StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        Assert.Single(await verify.DraftSessions.Where(draft => draft.EventId == setup.EventId).ToListAsync());
        var team = await verify.Teams.SingleAsync(value => value.EventId == setup.EventId);
        Assert.False(team.IncludedInDraft);
        Assert.Equal("Setup clan", team.AffiliationName);
    }

    [Fact]
    public async Task AdminCreatedInternalParticipantsReachPoolPreassignmentPicksAndFinalRoster()
    {
        var setup = await SeedAsync();
        var ownerIds = await SeedParticipantOwnersAsync(3);
        Guid questionId;
        Guid draftedTeamId;
        await using (var seed = new ApplicationDbContext(options))
        {
            questionId = await seed.SignupQuestions.Where(x => x.EventId == setup.EventId).Select(x => x.Id).SingleAsync();
            draftedTeamId = await seed.Teams.Where(x => x.EventId == setup.EventId && x.Name == "First").Select(x => x.Id).SingleAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var participantsPath = $"/Admin/Events/Participants/{setup.EventId}";
        var draftPath = $"/Admin/Events/Draft/{setup.EventId}";
        async Task Post(string path, string handler, Dictionary<string, string> fields)
        {
            var html = await client.GetStringAsync(path);
            Assert.Contains($"handler={handler}", html, StringComparison.OrdinalIgnoreCase);
            var response = await client.PostAsync($"{path}{(path.Contains('?') ? '&' : '?')}handler={handler}", Form(AntiforgeryToken(html), fields));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        var internalNames = new[] { "Internal Pre", "Internal Pick", "Internal Gone" };
        for (var index = 0; index < internalNames.Length; index++)
        {
            var name = internalNames[index];
            await Post(participantsPath + "?addParticipant=1", "CreateInternalParticipant", new()
            {
                ["InternalParticipant.OwnerAccountId"] = ownerIds[index].ToString(),
                [$"InternalParticipant.AccountAnswers[{questionId}].CharacterName"] = name,
                [$"InternalParticipant.AccountAnswers[{questionId}].Ehb"] = "8"
            });
        }
        await Post(draftPath, "AddTeam", new() { ["name"] = "Genuine external", ["formationType"] = "Preformed", ["includedInDraft"] = "false" });
        Guid externalTeamId;
        Guid[] internalIds;
        await using (var db = new ApplicationDbContext(options))
        {
            internalIds = await db.EventParticipants.Where(x => x.EventId == setup.EventId && x.Source == SignupSource.AdminCreated).OrderBy(x => x.SignupSequence).Select(x => x.Id).ToArrayAsync();
            Assert.Equal(3, internalIds.Length);
            externalTeamId = await db.Teams.Where(x => x.EventId == setup.EventId && x.FormationType == TeamFormationType.Preformed).Select(x => x.Id).SingleAsync();
        }
        var retainedManualParticipantId = await SeedAccountlessParticipantAsync(setup, "Retained Manual", 90);
        await Post(draftPath + $"?rosterTeamId={externalTeamId}", "AddMember", new()
        {
            ["teamId"] = externalTeamId.ToString(),
            ["participantId"] = retainedManualParticipantId.ToString()
        });
        // The retired external-member route is covered by the canonical fail-closed
        // proof below; this journey retains a current manual-roster member.
        await Post(draftPath, "WithdrawParticipant", new() { ["participantId"] = internalIds[2].ToString() });
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal(SignupStatus.Withdrawn, await db.EventParticipants.Where(x => x.Id == internalIds[2]).Select(x => x.SignupStatus).SingleAsync());
            Assert.False(await db.EventParticipantCharacters.AnyAsync(x => x.EventParticipantId == internalIds[2] && x.ReleasedAt == null));
        }
        var pool = await client.GetStringAsync(draftPath);
        var poolSectionStart = pool.IndexOf("data-draft-participant-section", StringComparison.Ordinal);
        var poolSection = pool[poolSectionStart..pool.IndexOf("</table>", poolSectionStart, StringComparison.Ordinal)];
        Assert.Contains("Internal Pre", poolSection);
        Assert.Contains("Internal Pick", poolSection);
        Assert.DoesNotContain("Genuine external", poolSection);
        Assert.DoesNotContain("Retained Manual", poolSection);
        DraftModel? loaded = null;
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, async page => { loaded = page; return await page.OnGetAsync(setup.EventId, null, CancellationToken.None); });
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.Distribution);
        Assert.Equal(6, loaded.ConfirmedCount);
        Assert.Equal(6, loaded.Distribution.IncludedParticipants);
        Assert.Equal(2, loaded.AssignedCount);
        Assert.Equal(4, loaded.AvailableCount);
        var editor = draftPath + $"?rosterTeamId={draftedTeamId}";
        await Post(editor, "AddMember", new() { ["teamId"] = draftedTeamId.ToString(), ["participantId"] = internalIds[0].ToString() });
        Guid internalMembershipId;
        await using (var db = new ApplicationDbContext(options))
            internalMembershipId = await db.TeamMemberships.Where(x => x.EventParticipantId == internalIds[0] && x.LeftAt == null).Select(x => x.Id).SingleAsync();
        await Post(editor, "RemoveMember", new() { ["membershipId"] = internalMembershipId.ToString(), ["reason"] = "Return to pool" });
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal(SignupStatus.Confirmed, await db.EventParticipants.Where(x => x.Id == internalIds[0]).Select(x => x.SignupStatus).SingleAsync());
            Assert.True(await db.EventParticipantCharacters.AnyAsync(x => x.EventParticipantId == internalIds[0] && x.ReleasedAt == null));
            Assert.NotNull(await db.TeamMemberships.Where(x => x.Id == internalMembershipId).Select(x => x.LeftAt).SingleAsync());
        }
        await Post(editor, "AddMember", new() { ["teamId"] = draftedTeamId.ToString(), ["participantId"] = internalIds[0].ToString() });
        await Post(draftPath, "Start", new());
        await Post(draftPath, "Scramble", new());
        foreach (var playerId in setup.PlayerIds.Skip(2))
            await Post(draftPath, "Pick", new() { ["participantId"] = playerId.ToString() });
        // Forging finalization before the remaining internal pick must leave publication untouched.
        var token = AntiforgeryToken(await client.GetStringAsync(draftPath));
        await client.PostAsync(draftPath + "?handler=Finalize", Form(token, new() { ["confirmed"] = "true" }));
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal(DraftState.Running, await db.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.State).SingleAsync());
            Assert.Empty(await db.DraftPublicationCycles.ToListAsync());
        }
        await Post(draftPath, "Pick", new() { ["participantId"] = internalIds[1].ToString() });
        await using (var beforeFinalization = new ApplicationDbContext(options))
        {
            var draftId = await beforeFinalization.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.Id).SingleAsync();
            Assert.Equal(3, await beforeFinalization.DraftPicks.CountAsync(x => x.DraftSessionId == draftId && x.UndoneAt == null));
            Assert.Equal(6, await beforeFinalization.TeamMemberships.CountAsync(x => x.LeftAt == null && beforeFinalization.Teams.Any(team => team.Id == x.TeamId && team.EventId == setup.EventId && team.Active && team.IncludedInDraft)));
        }
        await Post(draftPath, "Finalize", new() { ["confirmed"] = "true" });
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(DraftState.Finalized, await verify.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.State).SingleAsync());
        var roster = await verify.DraftPublicationRosters.ToListAsync();
        Assert.Equal(7, roster.Count);
        Assert.Contains(roster, x => x.EventParticipantId == internalIds[0] && x.TeamId == draftedTeamId && x.EffectivePickNumber == null);
        Assert.Contains(roster, x => x.EventParticipantId == internalIds[1] && x.EffectivePickNumber != null);
        Assert.Single(roster, x => x.TeamId == externalTeamId);
        Assert.All(await verify.Teams.Where(x => x.EventId == setup.EventId && x.FormationType == TeamFormationType.Drafted).ToListAsync(),
            team => Assert.Equal(3, roster.Count(x => x.TeamId == team.Id)));
        var publicationCount = roster.Count;
        await client.PostAsync(draftPath + "?handler=Pick", Form(token, new() { ["participantId"] = internalIds[1].ToString() }));
        Assert.Equal(publicationCount, await verify.DraftPublicationRosters.CountAsync());
    }

    [Fact]
    public async Task FinalizedPreformedEditorPreservesSourceBasedRemovalControls()
    {
        var setup = await SeedAsync();
        var preformedTeamName = "Mixed preformed";
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(
            setup.EventId, preformedTeamName, TeamFormationType.Preformed, null, CancellationToken.None, false, false));
        var preformedTeamId = await TeamIdAsync(setup.EventId, preformedTeamName);
        var retained = await SeedRetainedManualParticipantAsync(setup, preformedTeamId);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var draftPath = $"/Admin/Events/Draft/{setup.EventId}";
        async Task Post(string path, string handler, Dictionary<string, string> fields)
        {
            var html = await client.GetStringAsync(path);
            Assert.Contains($"handler={handler}", html, StringComparison.OrdinalIgnoreCase);
            using var response = await client.PostAsync($"{path}{(path.Contains('?') ? '&' : '?')}handler={handler}", Form(AntiforgeryToken(html), fields));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        var editor = draftPath + $"?rosterTeamId={preformedTeamId}";
        // The current AddMember flow can assign a website signup to a manual team.
        await Post(editor, "AddMember", new() { ["teamId"] = preformedTeamId.ToString(), ["participantId"] = setup.PlayerIds[2].ToString() });
        await Post(draftPath, "Start", new());
        await Post(draftPath, "Scramble", new());
        await Post(draftPath, "Pick", new() { ["participantId"] = setup.PlayerIds[3].ToString() });
        await Post(draftPath, "Finalize", new() { ["confirmed"] = "true" });
        Guid websiteMembershipId;
        Guid retainedMembershipId;
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal(DraftState.Finalized, await db.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.State).SingleAsync());
            var members = await db.TeamMemberships.Where(x => x.TeamId == preformedTeamId && x.LeftAt == null).ToListAsync();
            websiteMembershipId = Assert.Single(members, x => x.EventParticipantId == setup.PlayerIds[2]).Id;
            retainedMembershipId = Assert.Single(members, x => x.EventParticipantId == retained.ParticipantId).Id;
            Assert.Equal(SignupSource.Website, await db.EventParticipants.Where(x => x.Id == setup.PlayerIds[2]).Select(x => x.Source).SingleAsync());
            Assert.Equal(TeamMembershipSource.RetainedConversion, await db.TeamMemberships.Where(x => x.Id == websiteMembershipId).Select(x => x.Source).SingleAsync());
            Assert.Equal(TeamMembershipSource.PreformedManual, await db.TeamMemberships.Where(x => x.Id == retainedMembershipId).Select(x => x.Source).SingleAsync());
            Assert.Equal(5, await db.DraftPublicationRosters.CountAsync());
            Assert.True(await db.DraftPublicationRosters.AnyAsync(x => x.EventParticipantId == setup.PlayerIds[2] && x.TeamId == preformedTeamId));
            Assert.True(await db.DraftPublicationRosters.AnyAsync(x => x.EventParticipantId == retained.ParticipantId && x.TeamId == preformedTeamId));
        }
        var finalizedEditor = await client.GetStringAsync(editor);
        Assert.Contains($"name=\"membershipId\" value=\"{websiteMembershipId}\"", finalizedEditor, StringComparison.Ordinal);
        Assert.Contains("Retained Manual", finalizedEditor, StringComparison.Ordinal);
        var removalForms = Regex.Matches(finalizedEditor, """<form\b[^>]*action="[^"]*handler=RemoveMember[^"]*"[^>]*>.*?</form>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)
            .Select(match => match.Value).ToArray();
        Assert.Contains(removalForms, form => form.Contains(websiteMembershipId.ToString(), StringComparison.Ordinal) && form.Contains("name=\"confirmed\" value=\"true\"", StringComparison.Ordinal));
        Assert.Contains(removalForms, form => form.Contains(retainedMembershipId.ToString(), StringComparison.Ordinal) && form.Contains("name=\"confirmed\" value=\"true\"", StringComparison.Ordinal));

        long websiteMembershipVersion;
        await using (var db = new ApplicationDbContext(options))
            websiteMembershipVersion = await db.TeamMemberships.Where(x => x.Id == websiteMembershipId).Select(x => x.Version).SingleAsync();
        await Post(editor, "RemoveMember", new()
        {
            ["membershipId"] = websiteMembershipId.ToString(),
            ["confirmed"] = "true",
            ["expectedMembershipVersion"] = websiteMembershipVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        await using var verify = new ApplicationDbContext(options);
        var cycles = await verify.DraftPublicationCycles.Where(x => verify.DraftSessions.Any(draft => draft.Id == x.DraftSessionId && draft.EventId == setup.EventId)).OrderBy(x => x.CycleNumber).ToListAsync();
        Assert.Equal(2, cycles.Count);
        Assert.Single(cycles, x => x.SupersededAt is not null);
        var activeCycle = Assert.Single(cycles, x => x.SupersededAt is null);
        Assert.Equal(5, await verify.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycles[0].Id));
        Assert.Equal(4, await verify.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == activeCycle.Id));
        var removedMembership = await verify.TeamMemberships.SingleAsync(x => x.Id == websiteMembershipId);
        Assert.NotNull(removedMembership.LeftAt);
        Assert.Equal(TeamMembershipSource.RetainedConversion, removedMembership.Source);
        Assert.Null(removedMembership.ReplacesMembershipId);
        Assert.True(await verify.TeamMemberships.AnyAsync(x => x.Id == retainedMembershipId && x.LeftAt == null && x.Source == TeamMembershipSource.PreformedManual));
        Assert.Contains("roster.finalized_removed", await verify.AuditEntries.Where(x => x.EventId == setup.EventId).Select(x => x.Action).ToListAsync());
    }

    [Fact]
    public async Task ExternalPreformedMemberRechecksEventStartAfterWiseOldManLookup()
    {
        var setup = await SeedAsync();
        var teamId = Guid.NewGuid();
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Teams.Add(new Team(teamId, setup.EventId, "Preformed", "preformed", TeamFormationType.Preformed, null, false));
            await seed.SaveChangesAsync();
        }
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        var before = await ExternalRosterCountsAsync(setup.EventId);
        var validation = new CountingWiseOldManAccountValidation();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.RemoveAll<IWiseOldManAccountValidation>();
                services.AddSingleton<IWiseOldManAccountValidation>(validation);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var draftPath = $"/Admin/Events/Draft/{setup.EventId}";
        var page = await client.GetStringAsync($"{draftPath}?rosterTeamId={teamId}");
        var response = await client.PostAsync($"{draftPath}?handler=AddExternalMember", Form(AntiforgeryToken(page), new()
        {
            ["teamId"] = teamId.ToString(),
            ["name"] = "External after event start",
            ["ehb"] = "5",
            ["role"] = "Participant"
        }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, validation.Calls);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(before, await ExternalRosterCountsAsync(setup.EventId));
        Assert.False(await verify.OsrsCharacters.AnyAsync(value => value.NormalizedName == "EXTERNAL AFTER EVENT START"));
        Assert.False(await verify.AuditEntries.AnyAsync(value => value.EventId == setup.EventId && value.Action == "team.member_added" && value.TargetId == teamId.ToString()));
    }

    [Fact]
    public async Task ControllerLeaseSupportsAcquireConfirmedTakeoverReleaseAndRecovery()
    {
        var setup = await SeedAsync();
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAcquireControlAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.SecondAdminId, page => page.OnPostAcquireControlAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.SecondAdminId, page => page.OnPostTakeControlAsync(setup.EventId, true, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.SecondAdminId, page => page.OnPostReleaseControlAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAcquireControlAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.SecondAdminId, page => page.OnPostAcquireControlAsync(setup.EventId, CancellationToken.None), at: now.Add(DraftControlLease.Duration).Add(TimeSpan.FromSeconds(1)));

        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
        Assert.Equal(setup.SecondAdminId, draft.ControllerAccountId);
        Assert.True(draft.HasActiveController(now.Add(DraftControlLease.Duration).Add(TimeSpan.FromSeconds(1))));
        Assert.Equal(3, await verify.AuditEntries.CountAsync(value => value.Action == "draft.control_acquired"));
        Assert.Single(await verify.AuditEntries.Where(value => value.Action == "draft.control_taken_over").ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(value => value.Action == "draft.control_released").ToListAsync());
    }

    [Fact]
    public async Task SetupStartAutoAcquiresWhenUncontestedAndBlocksAnActiveOtherController()
    {
        var uncontested = await SeedAsync();
        DraftModel? firstSetup = null;
        await ExecuteAsync(uncontested.EventId, uncontested.FirstAdminId, async page =>
        {
            firstSetup = page;
            return await page.OnGetAsync(uncontested.EventId, null, CancellationToken.None);
        });
        Assert.NotNull(firstSetup);
        Assert.False(firstSetup.CanControlDraft);
        Assert.Null(firstSetup.DraftControllerAccountId);


        await ExecuteAsync(uncontested.EventId, uncontested.FirstAdminId, page => page.OnPostStartAsync(uncontested.EventId, CancellationToken.None));
        await using (var started = new ApplicationDbContext(options))
        {
            var draft = await started.DraftSessions.SingleAsync(value => value.EventId == uncontested.EventId);
            Assert.Equal(DraftState.Running, draft.State);
            Assert.Equal(uncontested.FirstAdminId, draft.ControllerAccountId);
            Assert.Single(await started.AuditEntries.Where(value => value.Action == "draft.started").ToListAsync());
            Assert.Empty(await started.AuditEntries.Where(value => value.Action == "draft.control_acquired").ToListAsync());
        }

        var contested = await SeedAsync();
        await ExecuteAsync(contested.EventId, contested.FirstAdminId, page => page.OnPostAcquireControlAsync(contested.EventId, CancellationToken.None));
        DraftModel? observerSetup = null;
        await ExecuteAsync(contested.EventId, contested.SecondAdminId, async page =>
        {
            observerSetup = page;
            return await page.OnGetAsync(contested.EventId, null, CancellationToken.None);
        });
        Assert.NotNull(observerSetup);
        Assert.False(observerSetup.CanControlDraft);
        Assert.Equal(contested.FirstAdminId, observerSetup.DraftControllerAccountId);
        Assert.Equal(await LoginNameAsync(contested.FirstAdminId), observerSetup.DraftControllerName);

        await ExecuteAsync(contested.EventId, contested.SecondAdminId, page => page.OnPostStartAsync(contested.EventId, CancellationToken.None));

        await using (var rejected = new ApplicationDbContext(options))
        {
            var draft = await rejected.DraftSessions.SingleAsync(value => value.EventId == contested.EventId);
            Assert.Equal(DraftState.Setup, draft.State);
            Assert.Equal(contested.FirstAdminId, draft.ControllerAccountId);
            Assert.Single(await rejected.AuditEntries.Where(value => value.Action == "draft.started").ToListAsync());
            Assert.Single(await rejected.AuditEntries.Where(value => value.Action == "draft.control_acquired").ToListAsync());
        }

        await ExecuteAsync(contested.EventId, contested.SecondAdminId, page => page.OnPostTakeControlAsync(contested.EventId, true, CancellationToken.None));
        await ExecuteAsync(contested.EventId, contested.FirstAdminId, page => page.OnPostStartAsync(contested.EventId, CancellationToken.None));
        await ExecuteAsync(contested.EventId, contested.SecondAdminId, page => page.OnPostStartAsync(contested.EventId, CancellationToken.None));

        await using var completed = new ApplicationDbContext(options);
        Assert.Equal(DraftState.Running, await completed.DraftSessions.Where(value => value.EventId == contested.EventId).Select(value => value.State).SingleAsync());
        Assert.Equal(contested.SecondAdminId, await completed.DraftSessions.Where(value => value.EventId == contested.EventId).Select(value => value.ControllerAccountId).SingleAsync());
        Assert.Single(await completed.AuditEntries.Where(value => value.Action == "draft.control_acquired").ToListAsync());
        Assert.Single(await completed.AuditEntries.Where(value => value.Action == "draft.control_taken_over").ToListAsync());
        Assert.Equal(2, await completed.AuditEntries.CountAsync(value => value.Action == "draft.started"));
    }

    [Fact]
    public async Task DraftStartInterleavesWithSelectedCapacityAndAccountMutationAtTheRealBoundary()
    {
        var setup = await SeedAsync();
        Guid secondaryQuestionId;
        Guid addedCharacterId;
        await using (var seed = new ApplicationDbContext(options))
        {
            var item = await seed.Events.SingleAsync(value => value.Id == setup.EventId);
            item.SetParticipantCap(3);
            item.AdvanceVersion();
            var form = await seed.SignupForms.SingleAsync(value => value.EventId == setup.EventId);
            var secondary = new SignupQuestion(Guid.NewGuid(), form.Id, setup.EventId, "playing_second", "Second Playing", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Playing);
            var character = new OsrsCharacter(Guid.NewGuid(), "Interleaved account", $"INTERLEAVED {setup.EventId:N}", now);
            var waiting = await seed.EventParticipants.SingleAsync(value => value.Id == setup.PlayerIds[3]);
            waiting.MoveToWaiting(5, now);
            seed.AddRange(secondary, character);
            await seed.SaveChangesAsync();
            secondaryQuestionId = secondary.Id;
            addedCharacterId = character.Id;
        }

        var startBoundary = new DraftEventReadBoundary();
        var startOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .AddInterceptors(startBoundary)
            .Options;

        async Task<string?> StartAsync() => await ExecuteAndReadStatusAsync(
            setup.EventId,
            setup.FirstAdminId,
            page => page.OnPostStartAsync(setup.EventId, CancellationToken.None),
            startOptions);

        async Task<ParticipantQueueMutationResult> ConfirmAsync()
        {
            await using var db = new ApplicationDbContext(options);
            var eventVersion = await db.Events.Where(value => value.Id == setup.EventId).Select(value => value.Version).SingleAsync();
            var responseVersion = await db.EventParticipants.Where(value => value.Id == setup.PlayerIds[3]).Select(value => value.ResponseVersion).SingleAsync();
            return await new SignupService(db, new SecretHasher(), new FixedTimeProvider(now), accountValidation: new SuccessfulWiseOldManAccountValidation())
                .ConfirmWaitingParticipantAsync(new(
                    setup.EventId, setup.PlayerIds[3], setup.FirstAdminId, "admin", ExpandCapacityWhenFull: true,
                    ExpectedEventVersion: eventVersion, ExpectedResponseVersion: responseVersion));
        }

        async Task<EventAccountMutationResult> AddAccountAsync()
        {
            await using var db = new ApplicationDbContext(options);
            var responseVersion = await db.EventParticipants.Where(value => value.Id == setup.PlayerIds[2]).Select(value => value.ResponseVersion).SingleAsync();
            return await new SignupService(db, new SecretHasher(), new FixedTimeProvider(now), accountValidation: new SuccessfulWiseOldManAccountValidation())
                .AddEventParticipantAccountAsync(new(
                    setup.EventId, setup.PlayerIds[2], addedCharacterId, EventCharacterRole.Playing, 12.345678m, setup.FirstAdminId, "admin", secondaryQuestionId,
                    ExpectedResponseVersion: responseVersion));
        }

        Task<ParticipantQueueMutationResult>? confirmTask = null;
        Task<EventAccountMutationResult>? accountTask = null;
        var startTask = StartAsync();
        try
        {
            await startBoundary.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            confirmTask = ConfirmAsync();
            await confirmTask;
            accountTask = AddAccountAsync();
            await accountTask;
            startBoundary.Release.TrySetResult();
        }
        finally
        {
            startBoundary.Release.TrySetResult();
        }

        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
        var itemAfter = await verify.Events.SingleAsync(value => value.Id == setup.EventId);
        var confirmedCount = await verify.EventParticipants.CountAsync(value => value.EventId == setup.EventId && value.SignupStatus == SignupStatus.Confirmed);
        var activeAddedAccount = await verify.EventParticipantCharacters.AnyAsync(value => value.EventParticipantId == setup.PlayerIds[2] && value.OsrsCharacterId == addedCharacterId && value.ReleasedAt == null);
        var startStatus = await startTask;
        var confirmResult = await confirmTask!;
        var accountResult = await accountTask!;

        Assert.NotNull(startStatus);
        Assert.Equal("An exception has been raised that is likely due to a transient failure.", startStatus);
        Assert.Equal(DraftState.Setup, draft.State);
        Assert.False(itemAfter.DraftLocked);
        Assert.Equal(0, await verify.AuditEntries.CountAsync(value => value.EventId == setup.EventId && value.Action == "draft.started"));
        Assert.True(itemAfter.ParticipantCap == 4, $"confirm succeeded={confirmResult.Succeeded}, changed={confirmResult.Changed}, error={confirmResult.Error}, status={confirmResult.Status}");
        Assert.Equal(4, confirmedCount);
        Assert.True(accountResult.Succeeded);
        Assert.True(accountResult.Changed);
        Assert.True(activeAddedAccount);
        var selectedStatus = await verify.EventParticipants.Where(value => value.Id == setup.PlayerIds[3]).Select(value => value.SignupStatus).SingleAsync();
        Assert.True(confirmResult.Succeeded);
        Assert.Equal(SignupStatus.Confirmed, selectedStatus);

        var recoveryStatus = await ExecuteAndReadStatusAsync(
            setup.EventId,
            setup.FirstAdminId,
            page => page.OnPostStartAsync(setup.EventId, CancellationToken.None));
        Assert.Contains("started", recoveryStatus, StringComparison.OrdinalIgnoreCase);
        await using var recovered = new ApplicationDbContext(options);
        Assert.Equal(DraftState.Running, await recovered.DraftSessions.Where(value => value.EventId == setup.EventId).Select(value => value.State).SingleAsync());
        Assert.True(await recovered.Events.Where(value => value.Id == setup.EventId).Select(value => value.DraftLocked).SingleAsync());
    }

    [Fact]
    public async Task DraftLoadKeepsParticipantWithMissingStrictAuthorityVisibleAndReadinessBlocked()
    {
        var setup = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            var participant = await seed.EventParticipants.SingleAsync(value => value.Id == setup.PlayerIds[0]);
            var questionId = await seed.SignupQuestions.Where(value => value.EventId == setup.EventId).Select(value => value.Id).SingleAsync();
            var firstTeamId = await seed.Teams.Where(value => value.EventId == setup.EventId && value.Name == "First").Select(value => value.Id).SingleAsync();
            var draftId = await seed.DraftSessions.Where(value => value.EventId == setup.EventId).Select(value => value.Id).SingleAsync();
            var character = new OsrsCharacter(Guid.NewGuid(), "Draft 0 duplicate", $"DRAFT DUPLICATE {setup.EventId:N}", now);
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 10, now, null, questionId, EventCharacterRole.Playing, 99, EhbSource.Manual, null);
            seed.AddRange(character, assignment, new DraftPick(Guid.NewGuid(), draftId, firstTeamId, participant.Id, 1, 1, now));
            await seed.SaveChangesAsync();
        }

        foreach (var sort in new[] { "ehb", "name", "signup", "status" })
        {
            DraftModel? loaded = null;
            var result = await ExecuteAsync(setup.EventId, setup.FirstAdminId, async page =>
            {
                loaded = page;
                return await page.OnGetAsync(setup.EventId, sort, CancellationToken.None);
            });

            Assert.IsType<PageResult>(result);
            Assert.NotNull(loaded);
            var participant = Assert.Single(loaded!.Participants, value => value.Id == setup.PlayerIds[0]);
            Assert.Equal("Draft 0", participant.Name);
            Assert.Equal(1m, participant.Ehb);
            Assert.Equal("Draft 0", loaded.LatestPick!.PlayerName);
            Assert.Equal(1m, Assert.Single(loaded.Teams.Single(value => value.Name == "First").Members).Ehb);
        }

        var status = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostStartAsync(setup.EventId, CancellationToken.None));
        Assert.Contains("Every included confirmed participant must retain one valid primary-account reservation.", status);
    }

    [Fact]
    public async Task MissingPlayingAuthorityBlocksStartPickAndFinalizationWithoutMutation()
    {
        var setup = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            var assignment = await seed.EventParticipantCharacters
                .SingleAsync(value => value.EventParticipantId == setup.PlayerIds[2] && value.EventRole == EventCharacterRole.Playing && value.ReleasedAt == null);
            assignment.Release(setup.FirstAdminId, now);
            await seed.SaveChangesAsync();
        }

        var startBefore = await RosterStateAsync();
        var startStatus = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostStartAsync(setup.EventId, CancellationToken.None));
        Assert.Contains("Every included confirmed participant must retain one valid primary-account reservation.", startStatus);
        Assert.Equal(startBefore, await RosterStateAsync());

        // Exercise the same invariant after a historical/fixture state has already
        // entered Running: neither a pick nor finalization may write around it.
        await using (var seed = new ApplicationDbContext(options))
        {
            var draft = await seed.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
            var teams = await seed.Teams.Where(value => value.EventId == setup.EventId && value.Active && value.IncludedInDraft).OrderBy(value => value.Name).ToListAsync();
            draft.AcquireControl(setup.FirstAdminId, now, DraftControlLease.Duration);
            teams[0].SetDraftPosition(1);
            teams[1].SetDraftPosition(2);
            draft.Start(now);
            await seed.SaveChangesAsync();
        }

        var pickBefore = await RosterStateAsync();
        var pickStatus = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        Assert.Contains("Every included confirmed participant must retain one valid primary-account reservation.", pickStatus);
        Assert.Equal(pickBefore, await RosterStateAsync());

        var finalizeBefore = await RosterStateAsync();
        var finalizeStatus = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        Assert.Contains("Every included confirmed participant must retain one valid primary-account reservation.", finalizeStatus);
        Assert.Equal(finalizeBefore, await RosterStateAsync());
    }

    [Fact]
    public async Task CancelPrivateDraftReturnsToEditableSetupAndRetainsPickHistory()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostUndoAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostUndoAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostCancelAsync(setup.EventId, CancellationToken.None));

        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
        Assert.Equal(DraftState.Setup, draft.State);
        Assert.NotNull(draft.FirstPickRecordedAt);
        Assert.Null(draft.ControllerAccountId);
        Assert.Null(draft.ControllerLeaseExpiresAt);
        Assert.False(await verify.Events.Where(value => value.Id == setup.EventId).Select(value => value.DraftLocked).SingleAsync());
        Assert.Equal(2, await verify.Teams.CountAsync(value => value.EventId == setup.EventId && value.Active && value.FormationType == TeamFormationType.Drafted));
        Assert.All(await verify.Teams.Where(value => value.EventId == setup.EventId && value.IncludedInDraft).ToListAsync(), value => Assert.NotNull(value.DraftPosition));
        Assert.Equal(2, await verify.DraftPicks.CountAsync(value => value.DraftSessionId == draft.Id && value.UndoneAt != null));
        Assert.Empty(await verify.TeamMemberships.Where(value => value.AssignedByDraftPickId != null && value.LeftAt == null).ToListAsync());
        Assert.Equal(2, await verify.TeamMemberships.CountAsync(value => value.AssignedByDraftPickId != null && value.LeftAt != null));
        Assert.Equal(2, await verify.TeamMemberships.CountAsync(value => value.Role == TeamMembershipRole.Captain && value.LeftAt == null));
        Assert.Single(await verify.AuditEntries.Where(value => value.Action == "draft.cancelled").ToListAsync());

        var published = await SeedAsync();
        await StartAndScrambleAsync(published);
        await ExecuteAsync(published.EventId, published.FirstAdminId, page => page.OnPostPickAsync(published.EventId, published.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(published.EventId, published.FirstAdminId, page => page.OnPostPickAsync(published.EventId, published.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(published.EventId, published.FirstAdminId, page => page.OnPostFinalizeAsync(published.EventId, CancellationToken.None, true));
        Assert.IsType<BadRequestResult>(await ExecuteAsync(published.EventId, published.FirstAdminId, page => page.OnPostCancelAsync(published.EventId, CancellationToken.None)));
    }

    [Fact]
    public async Task ZeroActivePickCancellationAuditFailureRollsBackTheEntireDraftMutation()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostUndoAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostUndoAsync(setup.EventId, CancellationToken.None));

        var before = await RosterStateAsync();
        await ExecuteAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostCancelAsync(setup.EventId, CancellationToken.None), new ThrowingAuditWriter());

        Assert.Equal(before, await RosterStateAsync());
        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
        Assert.Equal(DraftState.Running, draft.State);
        Assert.Equal(setup.FirstAdminId, draft.ControllerAccountId);
        Assert.Equal(2, await verify.DraftPicks.CountAsync(value => value.DraftSessionId == draft.Id && value.UndoneAt != null));
        Assert.Empty(await verify.DraftPublicationCycles.Where(value => value.DraftSessionId == draft.Id).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.Action == "draft.cancelled").ToListAsync());
    }

    [Fact]
    public async Task SamePickAndPickUndoRacesLeaveOneConsistentLedgerAndNoPartialMemberships()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await Task.WhenAll(
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None)),
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None)));

        await using (var afterPick = new ApplicationDbContext(options))
        {
            var draft = await afterPick.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
            Assert.Single(await afterPick.DraftPicks.Where(value => value.DraftSessionId == draft.Id && value.UndoneAt == null).ToListAsync());
            Assert.Single(await afterPick.TeamMemberships.Where(value => value.AssignedByDraftPickId != null && value.LeftAt == null).ToListAsync());
            Assert.Single(await afterPick.AuditEntries.Where(value => value.Action == "draft.pick_recorded").ToListAsync());
        }

        await Task.WhenAll(
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None)),
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostUndoAsync(setup.EventId, CancellationToken.None)));

        await using var verify = new ApplicationDbContext(options);
        var finalDraft = await verify.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
        var activePicks = await verify.DraftPicks.Where(value => value.DraftSessionId == finalDraft.Id && value.UndoneAt == null).ToListAsync();
        var activeMemberships = await verify.TeamMemberships.Where(value => value.AssignedByDraftPickId != null && value.LeftAt == null).ToListAsync();
        Assert.InRange(activePicks.Count, 0, 2);
        Assert.Equal(activePicks.Count, activeMemberships.Count);
        Assert.Equal(activePicks.Select(value => value.Id).Order(), activeMemberships.Select(value => value.AssignedByDraftPickId!.Value).Order());
        Assert.Equal(await verify.DraftPicks.CountAsync(value => value.DraftSessionId == finalDraft.Id), await verify.TeamMemberships.CountAsync(value => value.AssignedByDraftPickId != null));
    }

    [Fact]
    public async Task DraftedSetupAssignmentCanChooseCaptainAndUnlockDraftStartInOneStep()
    {
        var setup = await SeedAsync();
        Guid secondTeamId; Guid secondMembershipId;
        await using (var seed = new ApplicationDbContext(options))
        {
            secondTeamId = await seed.Teams.Where(team => team.EventId == setup.EventId && team.Name == "Second").Select(team => team.Id).SingleAsync();
            secondMembershipId = await seed.TeamMemberships.Where(membership => membership.TeamId == secondTeamId && membership.LeftAt == null).Select(membership => membership.Id).SingleAsync();
            var captainLogin = $"preseed-captain-{Guid.NewGuid():N}";
            var captainAccount = Account.CreateWebsite(Guid.NewGuid(), captainLogin, captainLogin.ToUpperInvariant(), now);
            (await seed.EventParticipants.SingleAsync(participant => participant.Id == setup.PlayerIds[2])).AssignOwner(captainAccount);
            seed.Accounts.Add(captainAccount);
            await seed.SaveChangesAsync();
            var authority = new TeamCaptainAuthorityService(seed, new FixedTimeProvider(now));
            Assert.True((await authority.ChangeRoleAsync(new(setup.EventId, secondMembershipId, TeamMembershipRole.Participant, setup.FirstAdminId, "draft-admin"))).Succeeded);
        }

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddMemberAsync(setup.EventId, secondTeamId, setup.PlayerIds[2], "Preassign Captain", CancellationToken.None, role: TeamMembershipRole.Captain));
        Guid addedMembershipId;
        await using (var verify = new ApplicationDbContext(options))
        {
            var membership = await verify.TeamMemberships.SingleAsync(item => item.TeamId == secondTeamId && item.EventParticipantId == setup.PlayerIds[2] && item.LeftAt == null);
            addedMembershipId = membership.Id;
            Assert.Equal(TeamMembershipRole.Captain, membership.Role);
            Assert.Single(await verify.TeamMembershipRoleTransitions.Where(item => item.TeamMembershipId == addedMembershipId && item.FromRole == TeamMembershipRole.Participant && item.ToRole == TeamMembershipRole.Captain).ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(item => item.EventId == setup.EventId && item.Action == "team.membership_role_changed" && item.TargetId == addedMembershipId.ToString()).ToListAsync());
        }
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAcquireControlAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostStartAsync(setup.EventId, CancellationToken.None));

        await using var completed = new ApplicationDbContext(options);
        Assert.Equal(DraftState.Running, await completed.DraftSessions.Where(draft => draft.EventId == setup.EventId).Select(draft => draft.State).SingleAsync());
        Assert.Single(await completed.TeamMembershipRoleTransitions.Where(item => item.TeamMembershipId == addedMembershipId && item.ToRole == TeamMembershipRole.Captain).ToListAsync());
    }

    [Fact]
    public async Task InvalidOrFailedSelectedRoleLeavesManualRosterAdditionWithoutResidue()
    {
        var setup = await SeedAsync();
        var teamId = await TeamIdAsync(setup.EventId, "Second");
        var before = await RosterMutationCountsAsync(setup.EventId);

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddMemberAsync(
            setup.EventId, teamId, setup.PlayerIds[2], "Invalid role", CancellationToken.None,
            role: (TeamMembershipRole)999));
        Assert.Equal(before, await RosterMutationCountsAsync(setup.EventId));

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddMemberAsync(
            setup.EventId, teamId, setup.PlayerIds[2], "Rejected role", CancellationToken.None,
            role: TeamMembershipRole.Captain), captainAuthority: new RejectingCaptainAuthority());
        Assert.Equal(before, await RosterMutationCountsAsync(setup.EventId));
        var externalTeamName = $"External rollback {Guid.NewGuid():N}"[..30];
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, externalTeamName, TeamFormationType.Preformed, null, CancellationToken.None));
        var externalTeamId = await TeamIdAsync(setup.EventId, externalTeamName);
        var externalBefore = await ExternalRosterCountsAsync(setup.EventId);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddExternalMemberAsync(
            setup.EventId, externalTeamId, "Rejected external", 12, "Rejected account", CancellationToken.None,
            role: TeamMembershipRole.Captain), captainAuthority: new RejectingCaptainAuthority());
        Assert.Equal(externalBefore, await ExternalRosterCountsAsync(setup.EventId));
        await using var verify = new ApplicationDbContext(options);
        Assert.False(await verify.TeamMemberships.AnyAsync(item => item.EventParticipantId == setup.PlayerIds[2] && item.LeftAt == null));
        Assert.False(await verify.EventParticipants.AnyAsync(item => item.EventId == setup.EventId && item.Source == SignupSource.AdminCreated));
    }

    [Fact]
    public async Task MalformedPostedRoleIsRejectedBeforeMemberOrExternalWrites()
    {
        var setup = await SeedAsync();
        var draftedTeamId = await TeamIdAsync(setup.EventId, "Second");
        var externalTeamName = $"External malformed {Guid.NewGuid():N}"[..30];
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, externalTeamName, TeamFormationType.Preformed, null, CancellationToken.None, false, false));
        var externalTeamId = await TeamIdAsync(setup.EventId, externalTeamName);

        var validation = new CountingWiseOldManAccountValidation();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString())
                .ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                    services.RemoveAll<IWiseOldManAccountValidation>();
                    services.AddSingleton<IWiseOldManAccountValidation>(validation);
                }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var draftPath = $"/Admin/Events/Draft/{setup.EventId}";
        var token = AntiforgeryToken(await client.GetStringAsync(draftPath));

        var beforeMember = await RosterMutationCountsAsync(setup.EventId);
        var memberResponse = await client.PostAsync($"{draftPath}?handler=AddMember", Form(token, new Dictionary<string, string>
        {
            ["teamId"] = draftedTeamId.ToString(),
            ["participantId"] = setup.PlayerIds[2].ToString(),
            ["reason"] = "Malformed role",
            ["role"] = "NotARole"
        }));
        Assert.Equal(HttpStatusCode.Redirect, memberResponse.StatusCode);
        Assert.Contains("Choose Participant, Captain, or Co-captain.", await client.GetStringAsync(draftPath));
        Assert.Equal(beforeMember, await RosterMutationCountsAsync(setup.EventId));
        Assert.Equal(0, validation.Calls);

        var beforeExternal = await ExternalRosterCountsAsync(setup.EventId);
        var externalResponse = await client.PostAsync($"{draftPath}?handler=AddExternalMember", Form(token, new Dictionary<string, string>
        {
            ["teamId"] = externalTeamId.ToString(),
            ["name"] = "Malformed external",
            ["ehb"] = "7.5",
            ["additionalAccounts"] = "",
            ["role"] = "NotARole"
        }));
        Assert.Equal(HttpStatusCode.NotFound, externalResponse.StatusCode);
        Assert.Equal(beforeExternal, await ExternalRosterCountsAsync(setup.EventId));
        Assert.Equal(0, validation.Calls);
    }

    [Fact]
    public async Task DraftPageRendersAttentionForUnownedCurrentCaptainWithoutEmergencyAccess()
    {
        var setup = await SeedAsync();
        var unowned = await SeedUnownedCaptainAsync(setup);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString())
                .ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));

        var response = await client.GetAsync($"/Admin/Events/Draft/{setup.EventId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Null(await verify.EventParticipants.Where(value => value.Id == unowned.ParticipantId).Select(value => value.AccountId).SingleAsync());
            Assert.Equal(TeamMembershipRole.Captain, await verify.TeamMemberships.Where(value => value.Id == unowned.MembershipId).Select(value => value.Role).SingleAsync());
        }
        var readiness = Regex.Matches(html, "<p class=\"team-readiness-status\">.*?</p>", RegexOptions.Singleline)
            .Select(match => match.Value)
            .First(block => block.Contains("A Captain is assigned, but website access is not ready."));
        Assert.Contains("admin-status-pill is-danger\">Attention</span>", readiness);
        Assert.DoesNotContain("admin-status-pill is-danger\">Enabled</span>", readiness);
        Assert.DoesNotContain("emergency", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DraftedSetupAssignmentRejectsAfterFirstPickAndAfterEventStartWithoutResidue()
    {
        var afterPick = await SeedAsync();
        await StartAndScrambleAsync(afterPick);
        await ExecuteAsync(afterPick.EventId, afterPick.FirstAdminId, page => page.OnPostPickAsync(afterPick.EventId, afterPick.PlayerIds[2], CancellationToken.None));
        var beforeFirstPickRejection = await RosterMutationCountsAsync(afterPick.EventId);
        var draftedTeamId = await TeamIdAsync(afterPick.EventId, "First");
        await ExecuteAsync(afterPick.EventId, afterPick.FirstAdminId, page => page.OnPostAddMemberAsync(afterPick.EventId, draftedTeamId, afterPick.PlayerIds[3], "Too late", CancellationToken.None));
        Assert.Equal(beforeFirstPickRejection, await RosterMutationCountsAsync(afterPick.EventId));

        var afterStart = await SeedAsync();
        await using (var start = new ApplicationDbContext(options))
        {
            var item = await start.Events.SingleAsync(value => value.Id == afterStart.EventId);
            item.StartEvent(now);
            await start.SaveChangesAsync();
        }
        var beforeStartRejection = await RosterMutationCountsAsync(afterStart.EventId);
        var startDraftedTeamId = await TeamIdAsync(afterStart.EventId, "First");
        await ExecuteAsync(afterStart.EventId, afterStart.FirstAdminId, page => page.OnPostAddMemberAsync(afterStart.EventId, startDraftedTeamId, afterStart.PlayerIds[2], "Too late", CancellationToken.None));
        Assert.Equal(beforeStartRejection, await RosterMutationCountsAsync(afterStart.EventId));
    }

    [Fact]
    public async Task FinalizationFreezesPublicationAndRetiredReopenDoesNotMutateHistory()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        Guid firstCycleId;
        await using (var verified = new ApplicationDbContext(options))
        {
            var draft = await verified.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
            Assert.Equal(DraftState.Finalized, draft.State);
            var cycle = Assert.Single(await verified.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id && x.SupersededAt == null).ToListAsync());
            firstCycleId = cycle.Id;
            Assert.Equal(DraftPublicationMethod.WebsiteDraft, cycle.PublicationMethod);
            Assert.Equal(4, await verified.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycle.Id));
            Assert.Equal(2, await verified.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycle.Id && x.EffectivePickNumber != null));
            Assert.True(await verified.Events.Where(x => x.Id == setup.EventId).Select(x => x.TeamRostersPublished && x.DraftResultsPublished).SingleAsync());
            Assert.Single(await verified.AuditEntries.Where(x => x.Action == "draft.finalized").ToListAsync());
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var draftPath = $"/Admin/Events/Draft/{setup.EventId}";
        var html = await client.GetStringAsync(draftPath);
        Assert.DoesNotContain("asp-page-handler=\"Reopen\"", html, StringComparison.Ordinal);
        var retiredResponse = await client.PostAsync($"{draftPath}?handler=Reopen", Form(AntiforgeryToken(html), new Dictionary<string, string>
        {
            ["confirmed"] = "true",
            ["reason"] = "Retired action proof"
        }));
        Assert.Equal(HttpStatusCode.NotFound, retiredResponse.StatusCode);

        await using var final = new ApplicationDbContext(options);
        var draftId = await final.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.Id).SingleAsync();
        Assert.Equal(DraftState.Finalized, await final.DraftSessions.Where(x => x.Id == draftId).Select(x => x.State).SingleAsync());
        Assert.True(await final.Events.Where(x => x.Id == setup.EventId).Select(x => x.TeamRostersPublished && x.DraftResultsPublished).SingleAsync());
        Assert.Single(await final.DraftPublicationCycles.Where(x => x.DraftSessionId == draftId && x.SupersededAt == null).ToListAsync());
        Assert.Equal(4, await final.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == firstCycleId));
        Assert.Empty(await final.AuditEntries.Where(x => x.Action == "draft.reopened").ToListAsync());
    }

    [Fact]
    public async Task RetiredDraftActionsAndHistoricalPausedRowsAreReadOnlyWithoutMutation()
    {
        var setup = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            var draft = await seed.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
            var bingoEvent = await seed.Events.SingleAsync(x => x.Id == setup.EventId);
            var teams = await seed.Teams.Where(x => x.EventId == setup.EventId).OrderBy(x => x.Name).ToListAsync();
            draft.Start(now);
            draft.RecordFirstPick(now.AddMinutes(1));
            draft.Pause();
            bingoEvent.SetDraftLocked(true, now);
            teams[0].SetDraftPosition(1);
            teams[1].SetDraftPosition(2);
            var pick = new DraftPick(Guid.NewGuid(), draft.Id, teams[0].Id, setup.PlayerIds[2], 1, 1, now.AddMinutes(1));
            var membership = new TeamMembership(Guid.NewGuid(), teams[0].Id, setup.PlayerIds[2], TeamMembershipRole.Participant, now.AddMinutes(1), pick.Id, "Historical paused fixture");
            membership.SetSource(TeamMembershipSource.DraftPick);
            seed.AddRange(pick, membership);
            await seed.SaveChangesAsync();
        }

        var before = await RosterStateAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var draftPath = $"/Admin/Events/Draft/{setup.EventId}";
        var html = await client.GetStringAsync(draftPath);
        Assert.Contains("Historical paused draft", html, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-page-handler=\"Pause\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-page-handler=\"Resume\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-page-handler=\"Reopen\"", html, StringComparison.Ordinal);
        foreach (var handler in new[] { "Pause", "Resume", "Reopen" })
        {
            var response = await client.PostAsync($"{draftPath}?handler={handler}", Form(AntiforgeryToken(html), new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.Equal(before, await RosterStateAsync());
    }

    [Fact]
    public async Task DirectRosterFinalizationPublishesSetupRosterWithoutSyntheticDraftHistory()
    {
        var setup = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            foreach (var team in await seed.Teams.Where(x => x.EventId == setup.EventId).ToListAsync()) team.SetIncludedInDraft(false);
            await seed.SaveChangesAsync();
        }

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));

        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
        Assert.Equal(DraftState.Finalized, draft.State);
        var cycle = Assert.Single(await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).ToListAsync());
        Assert.Equal(DraftPublicationMethod.DirectRoster, cycle.PublicationMethod);
        Assert.Equal(2, await verify.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycle.Id));
        Assert.Empty(await verify.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == cycle.Id && x.EffectivePickNumber != null).ToListAsync());
        Assert.Empty(await verify.DraftPicks.Where(x => x.DraftSessionId == draft.Id).ToListAsync());
        Assert.True(await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.TeamRostersPublished && x.DraftResultsPublished).SingleAsync());
    }

    [Fact]
    public async Task DirectRosterFinalizationRejectsIneligibleParticipantWithoutResidue()
    {
        var setup = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            foreach (var team in await seed.Teams.Where(x => x.EventId == setup.EventId).ToListAsync()) team.SetIncludedInDraft(false);
            var duplicate = await seed.EventParticipants.SingleAsync(x => x.Id == setup.PlayerIds[1]);
            duplicate.Withdraw(now, "Invalid direct-roster fixture");
            await seed.SaveChangesAsync();
        }

        var status = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));

        Assert.Contains("currently confirmed", status, StringComparison.OrdinalIgnoreCase);
        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
        Assert.Equal(DraftState.Setup, draft.State);
        Assert.Empty(await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).ToListAsync());
        Assert.False(await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.TeamRostersPublished || x.DraftResultsPublished).SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyActiveTeamBlocksDirectAndWebsitePublicationWithoutResidue(bool websiteDraft)
    {
        var setup = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Teams.Add(new Team(Guid.NewGuid(), setup.EventId, "Empty active team", "empty-active-team", TeamFormationType.Preformed, null, !websiteDraft, now));
            if (!websiteDraft)
                foreach (var team in await seed.Teams.Where(value => value.EventId == setup.EventId).ToListAsync()) team.SetIncludedInDraft(false);
            await seed.SaveChangesAsync();
        }

        if (websiteDraft)
        {
            await StartAndScrambleAsync(setup);
            await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
            await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        }

        var status = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        Assert.Contains("Every active team must have at least one roster member", status);
        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
        Assert.Equal(websiteDraft ? DraftState.Running : DraftState.Setup, draft.State);
        Assert.Empty(await verify.DraftPublicationCycles.Where(value => value.DraftSessionId == draft.Id).ToListAsync());
        Assert.Empty(await verify.DraftPublicationRosters.ToListAsync());
        Assert.False(await verify.Events.Where(value => value.Id == setup.EventId).Select(value => value.TeamRostersPublished || value.DraftResultsPublished).SingleAsync());
        Assert.All(await verify.Teams.Where(value => value.EventId == setup.EventId).ToListAsync(), value => Assert.Null(value.FinalizedAt));
    }

    [Fact]
    public async Task DirectFinalizationAuditFailureRollsBackEveryPublicationWrite()
    {
        var setup = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            foreach (var team in await seed.Teams.Where(value => value.EventId == setup.EventId).ToListAsync()) team.SetIncludedInDraft(false);
            await seed.SaveChangesAsync();
        }

        var before = await RosterStateAsync();
        await ExecuteAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true), new ThrowingAuditWriter());

        Assert.Equal(before, await RosterStateAsync());
        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
        Assert.Equal(DraftState.Setup, draft.State);
        Assert.Null(draft.ControllerAccountId);
        Assert.Empty(await verify.DraftPublicationCycles.Where(value => value.DraftSessionId == draft.Id).ToListAsync());
        Assert.Empty(await verify.DraftPublicationRosters.ToListAsync());
        Assert.False(await verify.Events.Where(value => value.Id == setup.EventId).Select(value => value.TeamRostersPublished || value.DraftResultsPublished).SingleAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.Action == "draft.finalized").ToListAsync());
    }

    [Fact]
    public async Task ConcurrentDirectFinalizationHasOnePublicationWinner()
    {
        var setup = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            foreach (var team in await seed.Teams.Where(x => x.EventId == setup.EventId).ToListAsync()) team.SetIncludedInDraft(false);
            await seed.SaveChangesAsync();
        }

        await Task.WhenAll(
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true)),
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true)));

        await using var verify = new ApplicationDbContext(options);
        var draftId = await verify.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.Id).SingleAsync();
        Assert.Single(await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == draftId && x.SupersededAt == null).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "draft.finalized").ToListAsync());
    }

    [Fact]
    public async Task FinalizationAuditFailureRollsBackEveryPublicationWrite()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true), new ThrowingAuditWriter());
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(DraftState.Running, await verify.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.State).SingleAsync());
        Assert.Empty(await verify.DraftPublicationCycles.ToListAsync());
        Assert.Empty(await verify.DraftPublicationRosters.ToListAsync());
        Assert.False(await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.TeamRostersPublished || x.DraftResultsPublished).SingleAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.Action == "draft.finalized").ToListAsync());
    }

    [Fact]
    public async Task FinalizationUsesTheDerivedDraftedParticipantSetAndRejectsIncompleteDraftedRostersWithoutResidue()
    {
        var valid = await SeedAsync();
        await using (var seed = new ApplicationDbContext(options))
        {
            var participant = new EventParticipant(Guid.NewGuid(), valid.EventId, SignupStatus.Confirmed, 5, now, SignupSource.Website);
            var primaryQuestionId = await seed.SignupQuestions.Where(x => x.EventId == valid.EventId && x.SystemField == SignupSystemField.PrimaryRegularAccount).Select(x => x.Id).SingleAsync();
            var character = new OsrsCharacter(Guid.NewGuid(), "Preformed retained", "PREFORMED RETAINED", now);
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), valid.EventId, participant.Id, character.Id, 0, now, null, primaryQuestionId, EventCharacterRole.Playing, 5, EhbSource.Manual, null);
            var secondary = new OsrsCharacter(Guid.NewGuid(), "Secondary playing", "SECONDARY PLAYING", now);
            var secondaryAssignment = new EventParticipantCharacter(Guid.NewGuid(), valid.EventId, participant.Id, secondary.Id, 1, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null);
            var team = new Team(Guid.NewGuid(), valid.EventId, "Preformed", "preformed", TeamFormationType.Preformed, null, false);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, "seed"); membership.SetSource(TeamMembershipSource.PreformedManual);
            seed.AddRange(participant, character, assignment, secondary, secondaryAssignment, team, membership); await seed.SaveChangesAsync();
        }
        await StartAndScrambleAsync(valid);
        await ExecuteAsync(valid.EventId, valid.FirstAdminId, page => page.OnPostPickAsync(valid.EventId, valid.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(valid.EventId, valid.FirstAdminId, page => page.OnPostPickAsync(valid.EventId, valid.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(valid.EventId, valid.FirstAdminId, page => page.OnPostFinalizeAsync(valid.EventId, CancellationToken.None, true));
        await using (var verified = new ApplicationDbContext(options))
        {
            var draft = await verified.DraftSessions.SingleAsync(x => x.EventId == valid.EventId);
            Assert.Equal(DraftState.Finalized, draft.State);
            Assert.Equal(5, await verified.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == verified.DraftPublicationCycles.Where(c => c.DraftSessionId == draft.Id && c.SupersededAt == null).Select(c => c.Id).Single()));
            Assert.Equal(2, await verified.DraftPicks.CountAsync(x => x.DraftSessionId == draft.Id && x.UndoneAt == null));
        }

        var incomplete = await SeedAsync();
        await StartAndScrambleAsync(incomplete);
        await ExecuteAsync(incomplete.EventId, incomplete.FirstAdminId, page => page.OnPostPickAsync(incomplete.EventId, incomplete.PlayerIds[2], CancellationToken.None));
        var notifier = new RecordingCollaborationNotifier();
        await ExecuteAsync(incomplete.EventId, incomplete.FirstAdminId, page => page.OnPostFinalizeAsync(incomplete.EventId, CancellationToken.None, true), collaboration: notifier);
        await using var rejected = new ApplicationDbContext(options);
        Assert.Equal(DraftState.Running, await rejected.DraftSessions.Where(x => x.EventId == incomplete.EventId).Select(x => x.State).SingleAsync());
        Assert.Empty(await rejected.DraftPublicationCycles.Where(x => x.DraftSessionId == rejected.DraftSessions.Where(d => d.EventId == incomplete.EventId).Select(d => d.Id).Single()).ToListAsync());
        var rejectedDraftId = await rejected.DraftSessions.Where(x => x.EventId == incomplete.EventId).Select(x => x.Id).SingleAsync();
        Assert.Empty(await rejected.AuditEntries.Where(x => x.Action == "draft.finalized" && x.TargetId == rejectedDraftId.ToString()).ToListAsync());
        Assert.Empty(await rejected.PersonalNotifications.ToListAsync());
        Assert.Equal(3, await rejected.TeamMemberships.CountAsync(x => rejected.Teams.Any(t => t.EventId == incomplete.EventId && t.Id == x.TeamId) && x.LeftAt == null));
        Assert.Empty(notifier.DraftEvents);
    }

    [Fact]
    public async Task ConcurrentFinalizationHasOneWinnerAndNoDuplicatePublicationResidue()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));

        await Task.WhenAll(
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true)),
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true)));

        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
        var cycle = Assert.Single(await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id && x.SupersededAt == null).ToListAsync());
        Assert.Equal(4, await verify.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycle.Id));
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "draft.finalized").ToListAsync());
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task CrossEventMoveRejectsSourceParticipantOrTargetWithoutMutation(bool foreignSource, bool foreignParticipant, bool foreignTarget)
    {
        var route = await SeedAsync();
        var other = await SeedAsync();
        var source = new Team(Guid.NewGuid(), foreignSource ? other.EventId : route.EventId, "Source", "source", TeamFormationType.Preformed, null, false);
        var target = new Team(Guid.NewGuid(), foreignTarget ? other.EventId : route.EventId, "Target", "target", TeamFormationType.Preformed, null, false);
        var participant = new EventParticipant(Guid.NewGuid(), foreignParticipant ? other.EventId : route.EventId, SignupStatus.Confirmed, 99, now, SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), "External member", "EXTERNAL MEMBER", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), participant.EventId, participant.Id, character.Id, 0, now, route.FirstAdminId, null, EventCharacterRole.Playing, 1, EhbSource.AdminCorrection, null);
        var membership = new TeamMembership(Guid.NewGuid(), source.Id, participant.Id, TeamMembershipRole.Captain, now, null, "seed");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(source, target, participant, character, assignment, membership);
            await seed.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(route.FirstAdminId));
        var draftPath = $"/Admin/Events/Draft/{route.EventId}";
        var token = AntiforgeryToken(await client.GetStringAsync("/Admin/Events"));
        var before = await RosterStateAsync();
        var response = await client.PostAsync($"{draftPath}?handler=MoveMember", Form(token, new Dictionary<string, string>
        {
            ["membershipId"] = membership.Id.ToString(),
            ["targetTeamId"] = target.Id.ToString(),
            ["confirmed"] = "true"
        }));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await RosterStateAsync());
    }

    [Fact]
    public async Task FinalizationRechecksCaptainAndReturnsToRoleCorrectionWithoutPublicationResidue()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        Guid membershipId;
        Guid teamId;
        await using (var lookup = new ApplicationDbContext(options))
        {
            var member = await lookup.TeamMemberships.SingleAsync(x => x.EventParticipantId == setup.PlayerIds[0] && x.LeftAt == null);
            membershipId = member.Id;
            teamId = member.TeamId;
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var draftPath = $"/Admin/Events/Draft/{setup.EventId}";
        var token = AntiforgeryToken(await client.GetStringAsync(draftPath));
        async Task ChangeRoleAsync(TeamMembershipRole role)
        {
            await using var lookup = new ApplicationDbContext(options);
            var version = await lookup.TeamMemberships.Where(x => x.Id == membershipId).Select(x => x.Version).SingleAsync();
            var response = await client.PostAsync($"{draftPath}?handler=ChangeRole", Form(token, new Dictionary<string, string>
            {
                ["membershipId"] = membershipId.ToString(),
                ["membershipVersion"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["role"] = role.ToString()
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(role, await lookup.TeamMemberships.Where(x => x.Id == membershipId).Select(x => x.Role).SingleAsync());
        }
        await ChangeRoleAsync(TeamMembershipRole.CoCaptain);
        var before = await RosterStateAsync();
        var rejected = await client.PostAsync($"{draftPath}?handler=Finalize", Form(token, new Dictionary<string, string> { ["confirmed"] = "true" }));
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Equal($"{draftPath}?rosterTeamId={teamId}", rejected.Headers.Location?.OriginalString);
        Assert.Equal(before, await RosterStateAsync());
        var recovery = await client.GetStringAsync(rejected.Headers.Location);
        Assert.Contains("Assign a current Captain to every drafted team before finalizing: First.", recovery);
        Assert.Contains("handler=ChangeRole", recovery);
        Assert.Contains($"value=\"{membershipId}\"", recovery);
        await ChangeRoleAsync(TeamMembershipRole.Captain);
        var finalized = await client.PostAsync($"{draftPath}?handler=Finalize", Form(token, new Dictionary<string, string> { ["confirmed"] = "true" }));
        Assert.Equal(HttpStatusCode.Redirect, finalized.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(DraftState.Finalized, await verify.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.State).SingleAsync());
        Assert.True(await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.TeamRostersPublished && x.DraftResultsPublished).SingleAsync());
        Assert.Single(await verify.DraftPublicationCycles.ToListAsync());
        Assert.Equal(4, await verify.DraftPublicationRosters.CountAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "draft.finalized").ToListAsync());
        Assert.Equal(2, await verify.EventParticipants.CountAsync(x => x.AccountId != null));
    }

    [Fact]
    public async Task FinalizedMoveIsRetiredAndLeavesMembershipAndPublicationUnchanged()
    {
        var setup = await SeedAsync();
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "External A", TeamFormationType.Preformed, null, CancellationToken.None, true, false));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "External B", TeamFormationType.Preformed, null, CancellationToken.None, true, false));
        var firstExternalTeam = await TeamIdAsync(setup.EventId, "External A");
        var secondExternalTeam = await TeamIdAsync(setup.EventId, "External B");
        Guid membershipId;
        await using (var seedExternal = new ApplicationDbContext(options))
        {
            var sequence = (await seedExternal.EventParticipants.Where(x => x.EventId == setup.EventId).MaxAsync(x => (long?)x.SignupSequence) ?? 0) + 1;
            var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, sequence, now, SignupSource.AdminCreated);
            var character = new OsrsCharacter(Guid.NewGuid(), "Frozen external", $"FROZEN EXTERNAL {Guid.NewGuid():N}", now);
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 0, now, setup.FirstAdminId, null, EventCharacterRole.Playing, 10, EhbSource.AdminCorrection, null);
            var membership = new TeamMembership(Guid.NewGuid(), firstExternalTeam, participant.Id, TeamMembershipRole.Captain, now, null, "Finalized roster fixture");
            membership.SetSource(TeamMembershipSource.RetainedConversion);
            var secondParticipant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, sequence + 1, now, SignupSource.AdminCreated);
            var secondCharacter = new OsrsCharacter(Guid.NewGuid(), "Frozen external two", $"FROZEN EXTERNAL TWO {Guid.NewGuid():N}", now);
            var secondAssignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, secondParticipant.Id, secondCharacter.Id, 0, now, setup.FirstAdminId, null, EventCharacterRole.Playing, 11, EhbSource.AdminCorrection, null);
            var secondMembership = new TeamMembership(Guid.NewGuid(), secondExternalTeam, secondParticipant.Id, TeamMembershipRole.Captain, now, null, "Finalized roster fixture");
            secondMembership.SetSource(TeamMembershipSource.RetainedConversion);
            seedExternal.AddRange(participant, character, assignment, membership, secondParticipant, secondCharacter, secondAssignment, secondMembership);
            await seedExternal.SaveChangesAsync();
            membershipId = membership.Id;
        }
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        var finalizedStatus = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        Assert.True(finalizedStatus?.Contains("published", StringComparison.OrdinalIgnoreCase) == true, $"Unexpected finalization status: {finalizedStatus}");

        await using (var afterAdd = new ApplicationDbContext(options))
        {
            var draft = await afterAdd.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
            Assert.Equal(DraftState.Finalized, draft.State);
            Assert.Equal(2, await afterAdd.DraftPicks.CountAsync(x => x.DraftSessionId == draft.Id));
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var draftPath = $"/Admin/Events/Draft/{setup.EventId}";
        var token = AntiforgeryToken(await client.GetStringAsync(draftPath));
        int beforeCycleCount;
        int beforePublishedCount;
        await using (var beforeMove = new ApplicationDbContext(options))
        {
            var beforeDraft = await beforeMove.DraftSessions.SingleAsync(d => d.EventId == setup.EventId);
            beforeCycleCount = await beforeMove.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == beforeDraft.Id);
            beforePublishedCount = await beforeMove.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId != Guid.Empty && beforeMove.DraftPublicationCycles.Any(c => c.Id == x.DraftPublicationCycleId && c.DraftSessionId == beforeDraft.Id));
        }
        var moved = await client.PostAsync($"{draftPath}?handler=MoveMember", Form(token, new Dictionary<string, string>
        {
            ["membershipId"] = membershipId.ToString(),
            ["targetTeamId"] = secondExternalTeam.ToString(),
            ["confirmed"] = "true"
        }));
        Assert.Equal(HttpStatusCode.Redirect, moved.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        var ended = await verify.TeamMemberships.SingleAsync(x => x.Id == membershipId);
        Assert.Null(ended.LeftAt);
        Assert.Equal(firstExternalTeam, ended.TeamId);
        Assert.Empty(await verify.TeamMemberships.Where(x => x.ReplacesMembershipId == membershipId).ToListAsync());
        var finalDraft = await verify.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
        var activeCycleId = await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == finalDraft.Id && x.SupersededAt == null).Select(x => x.Id).SingleAsync();
        Assert.Equal(beforeCycleCount, await verify.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == finalDraft.Id));
        Assert.Equal(beforePublishedCount, await verify.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == activeCycleId));
        Assert.Empty(await verify.AuditEntries.Where(x => x.Action == "team.member_moved").ToListAsync());
    }

    [Fact]
    public async Task MoveMemberRaceWithFinalizationRejectsConfirmedPostWithoutMutation()
    {
        var setup = await SeedAsync();
        Guid sourceTeamId;
        Guid targetTeamId;
        Guid membershipId;
        await using (var prepare = new ApplicationDbContext(options))
        {
            var teams = await prepare.Teams.Where(x => x.EventId == setup.EventId && x.Active).OrderBy(x => x.Name).ToListAsync();
            Assert.Equal(2, teams.Count);
            foreach (var team in teams) team.SetIncludedInDraft(false);
            sourceTeamId = teams[0].Id;
            targetTeamId = teams[1].Id;
            membershipId = await prepare.TeamMemberships
                .Where(x => x.TeamId == sourceTeamId && x.LeftAt == null)
                .Select(x => x.Id)
                .SingleAsync();
            await prepare.SaveChangesAsync();
        }

        var barrier = new MoveTransactionStartBarrier();
        var moveOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .AddInterceptors(barrier)
            .Options;
        await using var moveDb = new ApplicationDbContext(moveOptions);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, setup.FirstAdminId.ToString()), new Claim(ClaimTypes.Name, $"admin-{setup.FirstAdminId:N}")], "test"))
        };
        var movePage = new DraftModel(
            moveDb,
            new FixedTimeProvider(now),
            new AuditWriter(moveDb, new FixedTimeProvider(now)),
            new NullAdminCollaborationNotifier(),
            null!,
            new Bingo.Infrastructure.Signups.EventParticipantCharacterService(moveDb, new FixedTimeProvider(now)),
            captainAuthority: new TeamCaptainAuthorityService(moveDb, new FixedTimeProvider(now)))
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new EmptyTempDataProvider())
        };

        var moveTask = movePage.OnPostMoveMemberAsync(setup.EventId, membershipId, targetTeamId, CancellationToken.None, true);
        await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var finalizationStatus = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        Assert.Contains("finalized", finalizationStatus, StringComparison.OrdinalIgnoreCase);

        barrier.Release();
        var result = await moveTask;
        Assert.IsType<RedirectToPageResult>(result);

        await using var verify = new ApplicationDbContext(options);
        var original = await verify.TeamMemberships.SingleAsync(x => x.Id == membershipId);
        Assert.Equal(sourceTeamId, original.TeamId);
        Assert.Null(original.LeftAt);
        Assert.Empty(await verify.TeamMemberships.Where(x => x.ReplacesMembershipId == membershipId).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == setup.EventId && x.Action == "team.member_moved").ToListAsync());
        var draftId = await verify.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.Id).SingleAsync();
        Assert.Equal(DraftState.Finalized, await verify.DraftSessions.Where(x => x.Id == draftId).Select(x => x.State).SingleAsync());
        Assert.Single(await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == draftId).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentOrFailedPublishedCorrectionsLeaveNoPartialTeamOrPublicationResidue()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));

        await Task.WhenAll(
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "Race external A", TeamFormationType.Preformed, null, CancellationToken.None, true, false)),
            ExecuteAsync(setup.EventId, setup.SecondAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "Race external B", TeamFormationType.Preformed, null, CancellationToken.None, true, false)));
        var raceTeamCount = 0;
        await using (var afterRace = new ApplicationDbContext(options))
        {
            var draft = await afterRace.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
            Assert.Single(await afterRace.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id && x.SupersededAt == null).ToListAsync());
            raceTeamCount = await afterRace.Teams.CountAsync(x => x.EventId == setup.EventId && (x.Name == "Race external A" || x.Name == "Race external B"));
            Assert.InRange(raceTeamCount, 1, 2);
            Assert.Equal(raceTeamCount + 1, await afterRace.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == draft.Id));
        }

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "Audit failure external", TeamFormationType.Preformed, null, CancellationToken.None, true, false), new ThrowingAuditWriter());
        await using var verify = new ApplicationDbContext(options);
        Assert.False(await verify.Teams.AnyAsync(x => x.EventId == setup.EventId && x.Name == "Audit failure external"));
        var finalDraft = await verify.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
        Assert.Equal(raceTeamCount + 1, await verify.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == finalDraft.Id));
        Assert.Empty(await verify.AuditEntries.Where(x => x.Details != null && x.Details.Contains("Injected rollback")).ToListAsync());
    }

    [Theory]
    [InlineData("Live")]
    [InlineData("AwaitingFinalReview")]
    [InlineData("Finalized")]
    [InlineData("Archived")]
    [InlineData("Cancelled")]
    [InlineData("Discarded")]
    public async Task PostLiveRemoveAndMoveRoutesAreRejectedByThePageFilterWithoutAnyResidue(string state)
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        Guid firstMembership;
        Guid target;
        string draftPath;
        await using (var seed = new ApplicationDbContext(options))
        {
            var ev = await seed.Events.SingleAsync(x => x.Id == setup.EventId);
            var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 99, now, SignupSource.AdminCreated);
            var source = new Team(Guid.NewGuid(), ev.Id, "Live external A", "live-external-a", TeamFormationType.Preformed, null, false);
            var destination = new Team(Guid.NewGuid(), ev.Id, "Live external B", "live-external-b", TeamFormationType.Preformed, null, false);
            var character = new OsrsCharacter(Guid.NewGuid(), "Live external", "LIVE EXTERNAL", now);
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, now, setup.FirstAdminId, null, EventCharacterRole.Playing, 1, EhbSource.AdminCorrection, null);
            var membership = new TeamMembership(Guid.NewGuid(), source.Id, participant.Id, TeamMembershipRole.Participant, now, null, "seed"); membership.SetSource(TeamMembershipSource.PreformedManual);
            seed.AddRange(participant, source, destination, character, assignment, membership); await seed.SaveChangesAsync(); firstMembership = membership.Id; target = destination.Id; draftPath = $"/Admin/Events/Draft/{ev.Id}";
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var token = AntiforgeryToken(await client.GetStringAsync(draftPath));
        await using (var transition = new ApplicationDbContext(options))
        {
            await transition.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET state = {state} WHERE id = {setup.EventId}");
        }
        var before = await MutationSnapshotAsync(firstMembership);
        var remove = await client.PostAsync($"{draftPath}?handler=RemoveMember", Form(token, new Dictionary<string, string> { ["membershipId"] = firstMembership.ToString(), ["reason"] = "blocked" }));
        var move = await client.PostAsync($"{draftPath}?handler=MoveMember", Form(token, new Dictionary<string, string> { ["membershipId"] = firstMembership.ToString(), ["targetTeamId"] = target.ToString(), ["reason"] = "blocked" }));
        var expectedStatus = state == "Discarded" ? HttpStatusCode.NotFound : HttpStatusCode.Redirect;
        Assert.Equal(expectedStatus, remove.StatusCode); Assert.Equal(expectedStatus, move.StatusCode);
        if (expectedStatus == HttpStatusCode.Redirect)
        {
            Assert.Equal($"/Admin/Events/Manage/{setup.EventId}", remove.Headers.Location?.OriginalString);
            Assert.Equal($"/Admin/Events/Manage/{setup.EventId}", move.Headers.Location?.OriginalString);
        }
        Assert.Equal(before, await MutationSnapshotAsync(firstMembership));
        await using var verify = new ApplicationDbContext(options);
        Assert.Null(await verify.TeamMemberships.Where(x => x.Id == firstMembership).Select(x => x.LeftAt).SingleAsync());
        Assert.Equal(Enum.Parse<EventState>(state), await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.State).SingleAsync());
    }

    [Fact]
    public async Task PublicTeamImageRouteRejectsMismatchedOrRetiredAssetsAndThePageHasNoImageHandler()
    {
        string slug; Guid crossTeam; Guid retired; Guid crossEvent; Guid unpointed;
        await using (var seed = new ApplicationDbContext(options))
        {
            var admin = Account.CreateWebsite(Guid.NewGuid(), "image-admin", "IMAGE-ADMIN", now);
            var first = new BingoEvent(Guid.NewGuid(), "public-images", "Public images", "UTC", admin.Id, now);
            var second = new BingoEvent(Guid.NewGuid(), "other-images", "Other images", "UTC", admin.Id, now);
            var current = new Team(Guid.NewGuid(), first.Id, "Current", "current", TeamFormationType.Preformed, null, false);
            var other = new Team(Guid.NewGuid(), first.Id, "Other", "other", TeamFormationType.Preformed, null, false);
            var retiredTeam = new Team(Guid.NewGuid(), first.Id, "Retired", "retired", TeamFormationType.Preformed, null, false);
            var foreign = new Team(Guid.NewGuid(), second.Id, "Foreign", "foreign", TeamFormationType.Preformed, null, false);
            var orphanTeam = new Team(Guid.NewGuid(), first.Id, "Orphan", "orphan", TeamFormationType.Preformed, null, false);
            var otherAsset = new TeamImageAsset(Guid.NewGuid(), first.Id, other.Id, "other.png", "other.png", "image/png", 1, 1, 1, new string('a', 64), admin.Id, now);
            var retiredAsset = new TeamImageAsset(Guid.NewGuid(), first.Id, retiredTeam.Id, "retired.png", "retired.png", "image/png", 1, 1, 1, new string('b', 64), admin.Id, now); retiredAsset.Replace(now);
            var foreignAsset = new TeamImageAsset(Guid.NewGuid(), second.Id, foreign.Id, "foreign.png", "foreign.png", "image/png", 1, 1, 1, new string('c', 64), admin.Id, now);
            var orphanAsset = new TeamImageAsset(Guid.NewGuid(), first.Id, orphanTeam.Id, "orphan.png", "orphan.png", "image/png", 1, 1, 1, new string('d', 64), admin.Id, now);
            seed.AddRange(admin, first, second, current, other, retiredTeam, foreign, orphanTeam, otherAsset, retiredAsset, foreignAsset, orphanAsset); await seed.SaveChangesAsync();
            current.SetActiveImage(otherAsset.Id); retiredTeam.SetActiveImage(retiredAsset.Id); other.SetActiveImage(foreignAsset.Id); await seed.SaveChangesAsync();
            Assert.Empty(await new PublicTeamImageService(seed, null!).CurrentTeamIdsAsync(first.Id, [current.Id, retiredTeam.Id, other.Id, orphanTeam.Id], CancellationToken.None));
            slug = first.Slug; crossTeam = current.Id; retired = retiredTeam.Id; crossEvent = other.Id; unpointed = orphanTeam.Id;
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var teamId in new[] { crossTeam, retired, crossEvent, unpointed })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Events/{slug}/Teams/{teamId}/Image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Events/{slug}/Teams?handler=Image&teamId={crossTeam}")).StatusCode);
    }

    [Fact]
    public async Task PublishedRosterUsesFrozenNamesAfterCurrentAssignmentsAreReleased()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        string slug;
        string[] frozenNames;
        await using (var mutate = new ApplicationDbContext(options))
        {
            slug = await mutate.Events.Where(x => x.Id == setup.EventId).Select(x => x.Slug).SingleAsync();
            frozenNames = await mutate.DraftPublicationRosters.Where(x => mutate.DraftPublicationCycles.Any(c => c.Id == x.DraftPublicationCycleId && c.SupersededAt == null)).Select(x => x.PublicCharacterName).ToArrayAsync();
            foreach (var assignment in await mutate.EventParticipantCharacters.Where(x => x.EventId == setup.EventId && x.ReleasedAt == null).ToListAsync()) assignment.Release(setup.FirstAdminId, now.AddMinutes(1));
            await mutate.SaveChangesAsync();
        }
        await using var read = new ApplicationDbContext(options);
        var persistedFrozenNames = await read.DraftPublicationRosters.Where(x => read.DraftPublicationCycles.Any(c => c.Id == x.DraftPublicationCycleId && c.SupersededAt == null)).Select(x => x.PublicCharacterName).ToArrayAsync();
        Assert.Equal(frozenNames.Order(), persistedFrozenNames.Order());
        var page = new Bingo.Web.Pages.Events.TeamsModel(read, new FixedTimeProvider(now))
        {
            PageContext = new PageContext(new ActionContext(new DefaultHttpContext(), new RouteData(), new PageActionDescriptor()))
            {
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            }
        };
        Assert.IsType<PageResult>(await page.OnGetAsync(slug, CancellationToken.None));
        Assert.Equal(frozenNames.Order(), page.Teams.SelectMany(x => x.Members).Select(x => x.Name).Order());
    }

    [Fact]
    public async Task ActiveNonEmptyPublicationRemainsAuthoritativeWhenLegacyFlagsAreFalse()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));

        string slug;
        await using (var mutate = new ApplicationDbContext(options))
        {
            var item = await mutate.Events.SingleAsync(value => value.Id == setup.EventId);
            slug = item.Slug;
            item.SetDraftRosterPublication(false);
            await mutate.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync($"/Events/{slug}/Signups");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Events/{slug}/Teams", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StaleLegacyRosterFlagsWithoutPublicationDoNotExposeTeamsToHttpOrMyEvents()
    {
        var setup = await SeedAsync();
        string slug;
        await using (var mutate = new ApplicationDbContext(options))
        {
            var item = await mutate.Events.SingleAsync(value => value.Id == setup.EventId);
            item.MarkFirstPublic(now);
            item.SetDraftRosterPublication(true);
            slug = item.Slug;
            await mutate.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var signups = await anonymous.GetAsync($"/Events/{slug}/Signups");
        Assert.Equal(HttpStatusCode.OK, signups.StatusCode);
        Assert.DoesNotContain("/Events/" + slug + "/Teams", (await signups.Content.ReadAsStringAsync()), StringComparison.Ordinal);

        var projection = new Bingo.Web.Pages.Account.MyEventsModel.EventRow(
            setup.EventId, "Stale flags", slug, EventState.SignupClosed, now, now, true,
            TeamRostersPublished: true, DraftResultsPublished: true, BoardPublished: false, ResultsPublished: false,
            RosterExists: false, PublishedBoardExists: false, setup.PlayerIds[0], SignupStatus.Confirmed, now, null);
        Assert.Equal("/Events/Confirmation", projection.DestinationPage);
        Assert.Equal(EventDisplayPhase.SignupsClosed, projection.DisplayPhase);
    }

    [Theory]
    [InlineData(DraftPublicationMethod.WebsiteDraft)]
    [InlineData(DraftPublicationMethod.DirectRoster)]
    public async Task PublicBoardUsesTheFinalizedRosterForEveryApprovedPublicationMethod(DraftPublicationMethod method)
    {
        var setup = await SeedAsync();
        if (method == DraftPublicationMethod.DirectRoster)
        {
            await using var direct = new ApplicationDbContext(options);
            foreach (var team in await direct.Teams.Where(x => x.EventId == setup.EventId).ToListAsync()) team.SetIncludedInDraft(false);
            await direct.SaveChangesAsync();
            await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        }
        else
        {
            await StartAndScrambleAsync(setup);
            await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
            await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
            await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        }

        string slug;
        await using (var seed = new ApplicationDbContext(options))
        {
            slug = await seed.Events.Where(x => x.Id == setup.EventId).Select(x => x.Slug).SingleAsync();
            var template = new TileTemplate(Guid.NewGuid(), "Direct/website tile", "Frozen objective", ObjectiveType.Manual, string.Empty, 1m);
            var board = new Board(Guid.NewGuid(), setup.EventId, "Finalized roster board", 1, 1);
            var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, "Frozen tile", "Frozen description", "Proof", 1m);
            var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Complete", true);
            seed.AddRange(template, board, tile, requirement);
            await BoardApprovalFixture.PublishAsync(seed, board, now, [tile], [requirement]);
        }

        await using var verify = new ApplicationDbContext(options);
        var cycle = await verify.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
        Assert.Equal(method, cycle.PublicationMethod);
        var publicBoard = await new PublicBoardService(verify, new FixedTimeProvider(now)).GetEventBoardAsync(slug);
        Assert.NotNull(publicBoard);
        Assert.Equal(2, publicBoard!.Teams.Count);
        Assert.Equal(method == DraftPublicationMethod.DirectRoster ? 2 : 4, publicBoard.RosterPlayers!.Count);
        Assert.Contains("Draft 0", publicBoard.RosterPlayers.Select(x => x.PlayerName));
    }

    private async Task<Guid[]> SeedParticipantOwnersAsync(int count)
    {
        var owners = Enumerable.Range(0, count)
            .Select(index =>
            {
                var token = Guid.NewGuid().ToString("N");
                var login = $"draft-owner-{index}-{token}";
                return Account.CreateWebsite(Guid.NewGuid(), login, login.ToUpperInvariant(), now);
            })
            .ToArray();
        await using var db = new ApplicationDbContext(options);
        db.AddRange(owners);
        await db.SaveChangesAsync();
        return owners.Select(owner => owner.Id).ToArray();
    }

    private async Task<UnownedCaptainFixture> SeedUnownedCaptainAsync(Setup setup)
    {
        await using var db = new ApplicationDbContext(options);
        var questionId = await db.SignupQuestions.Where(x => x.EventId == setup.EventId && x.SystemField == SignupSystemField.PrimaryRegularAccount).Select(x => x.Id).SingleAsync();
        var team = new Team(Guid.NewGuid(), setup.EventId, "Unowned Captain", "unowned-captain", TeamFormationType.Preformed, null, false);
        var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 90, now.AddMinutes(1), SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), "Unowned Captain", $"UNOWNED CAPTAIN {setup.EventId:N}", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 0, now, setup.FirstAdminId, questionId, EventCharacterRole.Playing, 8m, EhbSource.Manual, null);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Captain, now, null, "retained unowned captain fixture");
        membership.SetSource(TeamMembershipSource.PreformedManual);
        db.AddRange(team, participant, character, assignment, membership);
        await db.SaveChangesAsync();
        return new(participant.Id, membership.Id);
    }

    private async Task<RetainedManualParticipantFixture> SeedRetainedManualParticipantAsync(Setup setup, Guid teamId)
    {
        await using var db = new ApplicationDbContext(options);
        var questionId = await db.SignupQuestions.Where(x => x.EventId == setup.EventId && x.SystemField == SignupSystemField.PrimaryRegularAccount).Select(x => x.Id).SingleAsync();
        var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 5, now.AddMinutes(1), SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), "Retained Manual", $"RETAINED MANUAL {setup.EventId:N}", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 0, now, setup.FirstAdminId, questionId, EventCharacterRole.Playing, 5m, EhbSource.Manual, null);
        var membership = new TeamMembership(Guid.NewGuid(), teamId, participant.Id, TeamMembershipRole.Participant, now, null, "retained manual fixture");
        membership.SetSource(TeamMembershipSource.PreformedManual);
        db.AddRange(participant, character, assignment, membership);
        await db.SaveChangesAsync();
        return new(participant.Id, membership.Id);
    }

    private async Task<Guid> SeedAccountlessParticipantAsync(Setup setup, string name, long signupSequence)
    {
        await using var db = new ApplicationDbContext(options);
        var questionId = await db.SignupQuestions.Where(x => x.EventId == setup.EventId && x.SystemField == SignupSystemField.PrimaryRegularAccount).Select(x => x.Id).SingleAsync();
        var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, signupSequence, now.AddMinutes(1), SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), name, $"{name.ToUpperInvariant()} {setup.EventId:N}", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 0, now, setup.FirstAdminId, questionId, EventCharacterRole.Playing, 5m, EhbSource.Manual, null);
        db.AddRange(participant, character, assignment);
        await db.SaveChangesAsync();
        return participant.Id;
    }

    private async Task StartAndScrambleAsync(Setup setup)
    {
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAcquireControlAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostStartAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostScrambleAsync(setup.EventId, CancellationToken.None));
    }

    private async Task<Guid> TeamIdAsync(Guid eventId, string name)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.Teams.Where(team => team.EventId == eventId && team.Name == name).Select(team => team.Id).SingleAsync();
    }

    private async Task<(int Memberships, int RoleTransitions, int Audits, int Notifications)> RosterMutationCountsAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        var teamIds = db.Teams.Where(team => team.EventId == eventId).Select(team => team.Id);
        var membershipIds = db.TeamMemberships.Where(membership => teamIds.Contains(membership.TeamId)).Select(membership => membership.Id);
        return (
            await db.TeamMemberships.CountAsync(membership => teamIds.Contains(membership.TeamId)),
            await db.TeamMembershipRoleTransitions.CountAsync(transition => membershipIds.Contains(transition.TeamMembershipId)),
            await db.AuditEntries.CountAsync(audit => audit.EventId == eventId),
            await db.PersonalNotifications.CountAsync());
    }

    private async Task<ExternalRosterCounts> ExternalRosterCountsAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        var teamIds = db.Teams.Where(team => team.EventId == eventId).Select(team => team.Id);
        return new(
            await db.EventParticipants.CountAsync(participant => participant.EventId == eventId && participant.Source == SignupSource.AdminCreated),
            await db.EventParticipantCharacters.CountAsync(assignment => assignment.EventId == eventId),
            await db.TeamMemberships.CountAsync(membership => teamIds.Contains(membership.TeamId)),
            await db.AuditEntries.CountAsync(entry => entry.EventId == eventId),
            await db.DraftPublicationCycles.CountAsync(cycle => db.DraftSessions.Any(draft => draft.Id == cycle.DraftSessionId && draft.EventId == eventId)),
            await db.DraftPublicationRosters.CountAsync(row => db.DraftPublicationCycles.Any(cycle => cycle.Id == row.DraftPublicationCycleId && db.DraftSessions.Any(draft => draft.Id == cycle.DraftSessionId && draft.EventId == eventId))));
    }

    private async Task StartEventAtLifecycleBoundaryAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleAsync();
        item.StartEvent(now);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<IActionResult> ExecuteAsync(Guid eventId, Guid accountId, Func<DraftModel, Task<IActionResult>> action, Bingo.Application.Auditing.IAuditWriter? auditWriter = null, IAdminCollaborationNotifier? collaboration = null, DateTimeOffset? at = null, ITeamCaptainAuthorityService? captainAuthority = null, IWiseOldManAccountValidation? accountValidation = null)
    {
        await using var db = new ApplicationDbContext(options);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accountId.ToString()), new Claim(ClaimTypes.Name, $"admin-{accountId:N}")], "test"))
        };
        var clock = new FixedTimeProvider(at ?? now);
        var page = new DraftModel(db, clock, auditWriter ?? new AuditWriter(db, clock), collaboration ?? new NullAdminCollaborationNotifier(), null!, new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, clock), captainAuthority: captainAuthority ?? new TeamCaptainAuthorityService(db, clock), accountValidation: accountValidation)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new EmptyTempDataProvider())
        };
        return await action(page);
    }

    private async Task<string?> ExecuteAndReadStatusAsync(Guid eventId, Guid accountId, Func<DraftModel, Task<IActionResult>> action, DbContextOptions<ApplicationDbContext>? contextOptions = null)
    {
        await using var db = new ApplicationDbContext(contextOptions ?? options);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accountId.ToString()), new Claim(ClaimTypes.Name, $"admin-{accountId:N}")], "test"))
        };
        var page = new DraftModel(db, new FixedTimeProvider(now), new AuditWriter(db, new FixedTimeProvider(now)), new NullAdminCollaborationNotifier(), null!, new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, new FixedTimeProvider(now)), captainAuthority: new TeamCaptainAuthorityService(db, new FixedTimeProvider(now)))
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new EmptyTempDataProvider())
        };
        await action(page);
        return page.TempData["StatusMessage"]?.ToString();
    }

    private sealed class DraftEventReadBoundary : DbCommandInterceptor
    {
        private int entered;

        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM events AS e", StringComparison.Ordinal)
                && command.CommandText.Contains("WHERE e.id =", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref entered, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return result;
        }
    }

    private async Task<Setup> SeedAsync(bool initialPrivate = false)
    {
        var firstLogin = $"draft-a-{Guid.NewGuid():N}";
        var secondLogin = $"draft-b-{Guid.NewGuid():N}";
        var firstAdmin = Account.CreateWebsite(Guid.NewGuid(), firstLogin, firstLogin.ToUpperInvariant(), now);
        var secondAdmin = Account.CreateWebsite(Guid.NewGuid(), secondLogin, secondLogin.ToUpperInvariant(), now);
        var firstCaptainLogin = $"draft-captain-a-{Guid.NewGuid():N}";
        var secondCaptainLogin = $"draft-captain-b-{Guid.NewGuid():N}";
        var firstCaptainAccount = Account.CreateWebsite(Guid.NewGuid(), firstCaptainLogin, firstCaptainLogin.ToUpperInvariant(), now);
        var secondCaptainAccount = Account.CreateWebsite(Guid.NewGuid(), secondCaptainLogin, secondCaptainLogin.ToUpperInvariant(), now);
        firstAdmin.SetGlobalRole(GlobalRole.Admin);
        secondAdmin.SetGlobalRole(GlobalRole.Admin);
        var passwords = new PasswordHasher<Account>();
        firstAdmin.SetPassword(passwords.HashPassword(firstAdmin, "password"), false, now, false);
        secondAdmin.SetPassword(passwords.HashPassword(secondAdmin, "password"), false, now, false);
        var item = new BingoEvent(Guid.NewGuid(), "Draft test", $"draft-{Guid.NewGuid():N}", "UTC", firstAdmin.Id, now.AddDays(-1));
        if (initialPrivate)
            item.ConfigureSchedule(null, null, null, null, null, null);
        else
            item.ConfigureSchedule(now.AddDays(-1), now.AddHours(-1), null, now.AddHours(1), now.AddDays(1), 10);
        item.ConfigureSignup(true, false, null);
        if (!initialPrivate)
        {
            item.OpenSignups(now.AddDays(-1));
            item.CloseSignups(now.AddHours(-1));
        }
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var primaryQuestion = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var firstTeam = new Team(Guid.NewGuid(), item.Id, "First", "first", TeamFormationType.Drafted, null, true);
        var secondTeam = new Team(Guid.NewGuid(), item.Id, "Second", "second", TeamFormationType.Drafted, null, true);
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
        var players = Enumerable.Range(1, 4).Select(index => new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, index, now, SignupSource.Website)).ToList();
        players[0].AssignOwner(firstCaptainAccount);
        players[1].AssignOwner(secondCaptainAccount);
        var characters = players.Select((player, index) => new OsrsCharacter(Guid.NewGuid(), $"Draft {index}", $"DRAFT {item.Id:N} {index}", now)).ToList();
        var assignments = players.Select((player, index) => new EventParticipantCharacter(Guid.NewGuid(), item.Id, player.Id, characters[index].Id, 0, now, null, primaryQuestion.Id, EventCharacterRole.Playing, index + 1, EhbSource.Manual, null)).ToList();
        await using var db = new ApplicationDbContext(options);
        db.AddRange(firstAdmin, secondAdmin, firstCaptainAccount, secondCaptainAccount, item, form, primaryQuestion, firstTeam, secondTeam, draft);
        db.AddRange(players); db.AddRange(characters); db.AddRange(assignments);
        db.AddRange(new TeamMembership(Guid.NewGuid(), firstTeam.Id, players[0].Id, TeamMembershipRole.Captain, now, null, "seed"), new TeamMembership(Guid.NewGuid(), secondTeam.Id, players[1].Id, TeamMembershipRole.Captain, now, null, "seed"));
        await db.SaveChangesAsync();
        return new(item.Id, firstAdmin.Id, secondAdmin.Id, players.Select(player => player.Id).ToArray());
    }

    private async Task<string> LoginNameAsync(Guid accountId)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.Accounts.Where(account => account.Id == accountId).Select(account => account.LoginName).SingleAsync();
    }

    private static async Task LoginAsync(HttpClient client, string username)
    {
        var token = AntiforgeryToken(await client.GetStringAsync("/Account/Login"));
        var response = await client.PostAsync("/Account/Login", Form(token, new Dictionary<string, string> { ["Input.Username"] = username, ["Input.Password"] = "password" }));
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());
    }

    private async Task<string> RosterStateAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new
        {
            Events = await db.Events.OrderBy(x => x.Id).ToListAsync(),
            Teams = await db.Teams.OrderBy(x => x.Id).ToListAsync(),
            Participants = await db.EventParticipants.OrderBy(x => x.Id).ToListAsync(),
            Memberships = await db.TeamMemberships.OrderBy(x => x.Id).ToListAsync(),
            Roles = await db.TeamMembershipRoleTransitions.OrderBy(x => x.Id).ToListAsync(),
            Access = await db.AccountEventAccesses.OrderBy(x => x.Id).ToListAsync(),
            Characters = await db.EventParticipantCharacters.OrderBy(x => x.Id).ToListAsync(),
            Drafts = await db.DraftSessions.OrderBy(x => x.Id).ToListAsync(),
            Picks = await db.DraftPicks.OrderBy(x => x.Id).ToListAsync(),
            Cycles = await db.DraftPublicationCycles.OrderBy(x => x.Id).ToListAsync(),
            Rosters = await db.DraftPublicationRosters.OrderBy(x => x.Id).ToListAsync(),
            Audit = await db.AuditEntries.OrderBy(x => x.Id).ToListAsync(),
            Notifications = await db.PersonalNotifications.OrderBy(x => x.Id).ToListAsync()
        });
    }

    private async Task<MutationSnapshot> MutationSnapshotAsync(Guid membershipId)
    {
        await using var db = new ApplicationDbContext(options);
        var member = await db.TeamMemberships.SingleAsync(membership => membership.Id == membershipId);
        var eventId = await db.Teams.Where(team => team.Id == member.TeamId).Select(team => team.EventId).SingleAsync();
        var draftId = await db.DraftSessions.Where(draft => draft.EventId == eventId).Select(draft => draft.Id).SingleAsync();
        var memberships = await db.TeamMemberships.Where(item => item.EventParticipantId == member.EventParticipantId).OrderBy(item => item.Id).ToListAsync();
        var assignments = await db.EventParticipantCharacters.Where(item => item.EventId == eventId && item.EventParticipantId == member.EventParticipantId).OrderBy(item => item.Id).ToListAsync();
        var cycles = await db.DraftPublicationCycles.Where(item => item.DraftSessionId == draftId).OrderBy(item => item.CycleNumber).ToListAsync();
        var roster = await db.DraftPublicationRosters.Where(item => db.DraftPublicationCycles.Any(cycle => cycle.DraftSessionId == draftId && cycle.Id == item.DraftPublicationCycleId)).OrderBy(item => item.Id).ToListAsync();
        return new(
            string.Join(';', memberships.Select(item => $"{item.Id}:{item.TeamId}:{item.LeftAt:O}:{item.Source}:{item.ReplacesMembershipId}")),
            string.Join(';', assignments.Select(item => $"{item.Id}:{item.OsrsCharacterId}:{item.ReleasedAt:O}")),
            string.Join(';', cycles.Select(item => $"{item.Id}:{item.CycleNumber}:{item.SupersededAt:O}")),
            string.Join(';', roster.Select(item => $"{item.Id}:{item.TeamId}:{item.EventParticipantId}:{item.EffectivePickNumber}:{item.PublicCharacterName}")),
            await db.AuditEntries.CountAsync(),
            await db.PersonalNotifications.CountAsync());
    }

    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(fields);
    }

    private sealed record Setup(Guid EventId, Guid FirstAdminId, Guid SecondAdminId, Guid[] PlayerIds);
    private sealed record UnownedCaptainFixture(Guid ParticipantId, Guid MembershipId);
    private sealed record RetainedManualParticipantFixture(Guid ParticipantId, Guid MembershipId);
    private sealed record MutationSnapshot(string Memberships, string Assignments, string Cycles, string Roster, int AuditCount, int NotificationCount);
    private sealed record ExternalRosterCounts(int Participants, int CharacterReservations, int Memberships, int AuditEntries, int PublicationCycles, int PublishedRosterRows);
    private sealed class CountingWiseOldManAccountValidation : IWiseOldManAccountValidation
    {
        public int Calls { get; private set; }

        public Task<WiseOldManAccountValidationResult> ValidateAsync(
            WiseOldManAccountValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new WiseOldManAccountValidationResult(WiseOldManAccountValidationOutcome.Success, []));
        }
    }
    private sealed class BarrierWiseOldManAccountValidation : IWiseOldManAccountValidation
    {
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> LookupStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release() => release.TrySetResult(true);
        public async Task<WiseOldManAccountValidationResult> ValidateAsync(WiseOldManAccountValidationRequest request, CancellationToken cancellationToken = default)
        {
            LookupStarted.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return new(WiseOldManAccountValidationOutcome.Success, []);
        }
    }
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed class MoveTransactionStartBarrier : DbTransactionInterceptor
    {
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release() => release.TrySetResult(true);

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
    private sealed class EmptyTempDataProvider : ITempDataProvider { public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(); public void SaveTempData(HttpContext context, IDictionary<string, object> values) { } }
    private sealed class ThrowingAuditWriter : Bingo.Application.Auditing.IAuditWriter
    {
        public void Stage(Guid? actorAccountId, string actorUsername, string action, string targetType, string? targetId = null, string? details = null, Guid? eventId = null, string? beforeState = null, string? afterState = null) => throw new InvalidOperationException("Injected audit failure");
        public Task WriteAndSaveAsync(Guid? actorAccountId, string actorUsername, string action, string targetType, string? targetId = null, string? details = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected audit failure");
        public Task WriteAndSaveAsync(Guid? actorAccountId, string actorUsername, string action, string targetType, string? targetId, string? details, Guid? eventId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected audit failure");
        public Task WriteAsync(Guid? actorAccountId, string actorUsername, string action, string targetType, string? targetId = null, string? details = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected audit failure");
        public Task WriteAsync(Guid? actorAccountId, string actorUsername, string action, string targetType, string? targetId, string? details, Guid? eventId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected audit failure");
    }
    private sealed class RejectingCaptainAuthority : ITeamCaptainAuthorityService
    {
        public Task<TeamCaptainRoleChangeResult> ChangeRoleAsync(TeamCaptainRoleChange change, CancellationToken ct = default) => Task.FromResult(new TeamCaptainRoleChangeResult(false, "Injected role failure."));
        public Task<bool> HasDraftSignupTableAccessAsync(Guid accountId, Guid eventId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasCurrentCaptainAuthorityAsync(Guid accountId, Guid eventId, Guid? teamId = null, CancellationToken ct = default) => Task.FromResult(false);
    }
    private sealed class RecordingCollaborationNotifier : IAdminCollaborationNotifier
    {
        public List<Guid> DraftEvents { get; } = [];
        public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default) { DraftEvents.Add(eventId); return Task.CompletedTask; }
        public Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyEventsControlChangedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
