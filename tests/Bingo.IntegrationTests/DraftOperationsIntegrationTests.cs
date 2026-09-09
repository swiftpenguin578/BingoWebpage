using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
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
    public async Task CancelPrivateDraftReturnsToEditableSetupAndRetainsPickHistory()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostCancelAsync(setup.EventId, CancellationToken.None));

        await using var verify = new ApplicationDbContext(options);
        var draft = await verify.DraftSessions.SingleAsync(value => value.EventId == setup.EventId);
        Assert.Equal(DraftState.Setup, draft.State);
        Assert.Null(draft.FirstPickRecordedAt);
        Assert.Null(draft.ControllerAccountId);
        Assert.Null(draft.ControllerLeaseExpiresAt);
        Assert.False(await verify.Events.Where(value => value.Id == setup.EventId).Select(value => value.DraftLocked).SingleAsync());
        Assert.Equal(2, await verify.Teams.CountAsync(value => value.EventId == setup.EventId && value.Active && value.FormationType == TeamFormationType.Drafted));
        Assert.All(await verify.Teams.Where(value => value.EventId == setup.EventId && value.IncludedInDraft).ToListAsync(), value => Assert.Null(value.DraftPosition));
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
        Assert.Single(activePicks);
        Assert.Single(activeMemberships);
        Assert.Equal(activePicks[0].Id, activeMemberships[0].AssignedByDraftPickId);
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
        var externalTeamName = $"External rollback {Guid.NewGuid():N}";
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
        var externalTeamName = $"External malformed {Guid.NewGuid():N}";
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, externalTeamName, TeamFormationType.Preformed, null, CancellationToken.None));
        var externalTeamId = await TeamIdAsync(setup.EventId, externalTeamName);

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString())
                .ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
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

        var beforeExternal = await ExternalRosterCountsAsync(setup.EventId);
        var externalResponse = await client.PostAsync($"{draftPath}?handler=AddExternalMember", Form(token, new Dictionary<string, string>
        {
            ["teamId"] = externalTeamId.ToString(),
            ["name"] = "Malformed external",
            ["ehb"] = "7.5",
            ["additionalAccounts"] = "",
            ["role"] = "NotARole"
        }));
        Assert.Equal(HttpStatusCode.Redirect, externalResponse.StatusCode);
        Assert.Contains("Choose Participant, Captain, or Co-captain.", await client.GetStringAsync(draftPath));
        Assert.Equal(beforeExternal, await ExternalRosterCountsAsync(setup.EventId));
    }

    [Fact]
    public async Task DraftPageRendersAttentionForUnownedCurrentCaptainWithoutEmergencyAccess()
    {
        var setup = await SeedAsync();
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
        var readiness = Regex.Matches(html, "<p class=\"team-readiness-status\">.*?</p>", RegexOptions.Singleline)
            .Select(match => match.Value)
            .First(block => block.Contains("A Captain is assigned, but website access is not ready."));
        Assert.Contains("admin-status-pill is-danger\">Attention</span>", readiness);
        Assert.DoesNotContain("admin-status-pill is-danger\">Enabled</span>", readiness);
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
    public async Task FinalizationIsAtomicAndReopenCreatesAReplacementPublicationWithoutRewritingHistory()
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
            Assert.Equal(4, await verified.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycle.Id));
            Assert.Equal(2, await verified.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == cycle.Id && x.EffectivePickNumber != null));
            Assert.True(await verified.Events.Where(x => x.Id == setup.EventId).Select(x => x.TeamRostersPublished && x.DraftResultsPublished).SingleAsync());
            Assert.Single(await verified.AuditEntries.Where(x => x.Action == "draft.finalized").ToListAsync());
        }

        var beforeDraftedTeamRejection = await RosterMutationCountsAsync(setup.EventId);
        var draftedTeamMessage = await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId,
            page => page.OnPostAddTeamAsync(setup.EventId, "Late drafted team", TeamFormationType.Drafted, null, CancellationToken.None));
        Assert.Equal("Drafted teams cannot be added after the draft has been completed.", draftedTeamMessage);
        Assert.Equal(beforeDraftedTeamRejection, await RosterMutationCountsAsync(setup.EventId));

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostReopenAsync(setup.EventId, true, "Correct the final pick", CancellationToken.None));
        await using (var reopened = new ApplicationDbContext(options))
        {
            Assert.Equal(DraftState.Running, await reopened.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.State).SingleAsync());
            Assert.False(await reopened.Events.Where(x => x.Id == setup.EventId).Select(x => x.TeamRostersPublished || x.DraftResultsPublished).SingleAsync());
            Assert.NotNull(await reopened.DraftPublicationCycles.Where(x => x.Id == firstCycleId).Select(x => x.SupersededAt).SingleAsync());
            Assert.Equal(4, await reopened.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == firstCycleId));
        }
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAcquireControlAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostUndoAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        await using var final = new ApplicationDbContext(options);
        var draftId = await final.DraftSessions.Where(x => x.EventId == setup.EventId).Select(x => x.Id).SingleAsync();
        Assert.Equal(2, await final.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == draftId));
        Assert.Single(await final.DraftPublicationCycles.Where(x => x.DraftSessionId == draftId && x.SupersededAt == null).ToListAsync());
        Assert.Equal(2, await final.AuditEntries.CountAsync(x => x.Action == "draft.finalized"));
        Assert.Single(await final.AuditEntries.Where(x => x.Action == "draft.reopened").ToListAsync());
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

    [Fact]
    public async Task ConfirmedPreformedCorrectionSupersedesPublicationAndMoveKeepsReplacementHistory()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "External A", TeamFormationType.Preformed, null, CancellationToken.None, true));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "External B", TeamFormationType.Preformed, null, CancellationToken.None, true));

        Guid firstExternalTeam;
        Guid secondExternalTeam;
        await using (var lookup = new ApplicationDbContext(options))
        {
            firstExternalTeam = await lookup.Teams.Where(x => x.EventId == setup.EventId && x.Name == "External A").Select(x => x.Id).SingleAsync();
            secondExternalTeam = await lookup.Teams.Where(x => x.EventId == setup.EventId && x.Name == "External B").Select(x => x.Id).SingleAsync();
        }
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddExternalMemberAsync(setup.EventId, firstExternalTeam, "Frozen external", 10, null, CancellationToken.None, true, role: TeamMembershipRole.Captain));

        Guid membershipId;
        await using (var afterAdd = new ApplicationDbContext(options))
        {
            membershipId = await afterAdd.TeamMemberships.Where(x => x.TeamId == firstExternalTeam && x.LeftAt == null).Select(x => x.Id).SingleAsync();
            var externalParticipantId = await afterAdd.TeamMemberships.Where(x => x.Id == membershipId).Select(x => x.EventParticipantId).SingleAsync();
            Assert.Equal(TeamMembershipRole.Captain, await afterAdd.TeamMemberships.Where(x => x.Id == membershipId).Select(x => x.Role).SingleAsync());
            var draft = await afterAdd.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
            Assert.Equal(4, await afterAdd.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == draft.Id));
            var addedCycleId = await afterAdd.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id && x.SupersededAt == null).Select(x => x.Id).SingleAsync();
            Assert.Equal(TeamMembershipRole.Captain, await afterAdd.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == addedCycleId && x.EventParticipantId == externalParticipantId).Select(x => x.Role).SingleAsync());
            Assert.Equal(2, await afterAdd.DraftPicks.CountAsync(x => x.DraftSessionId == draft.Id));
        }

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostMoveMemberAsync(setup.EventId, membershipId, secondExternalTeam, CancellationToken.None, true));
        await using var verify = new ApplicationDbContext(options);
        var ended = await verify.TeamMemberships.SingleAsync(x => x.Id == membershipId);
        var replacement = await verify.TeamMemberships.SingleAsync(x => x.ReplacesMembershipId == membershipId);
        Assert.NotNull(ended.LeftAt);
        Assert.Equal(TeamMembershipSource.Replacement, replacement.Source);
        Assert.Equal(secondExternalTeam, replacement.TeamId);
        var finalDraft = await verify.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
        var activeCycleId = await verify.DraftPublicationCycles.Where(x => x.DraftSessionId == finalDraft.Id && x.SupersededAt == null).Select(x => x.Id).SingleAsync();
        Assert.Equal(5, await verify.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == finalDraft.Id));
        Assert.Equal(5, await verify.DraftPublicationRosters.CountAsync(x => x.DraftPublicationCycleId == activeCycleId));
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "team.member_moved").ToListAsync());
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
            ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "Race external A", TeamFormationType.Preformed, null, CancellationToken.None, true)),
            ExecuteAsync(setup.EventId, setup.SecondAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "Race external B", TeamFormationType.Preformed, null, CancellationToken.None, true)));
        var raceTeamCount = 0;
        await using (var afterRace = new ApplicationDbContext(options))
        {
            var draft = await afterRace.DraftSessions.SingleAsync(x => x.EventId == setup.EventId);
            Assert.Single(await afterRace.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id && x.SupersededAt == null).ToListAsync());
            raceTeamCount = await afterRace.Teams.CountAsync(x => x.EventId == setup.EventId && (x.Name == "Race external A" || x.Name == "Race external B"));
            Assert.InRange(raceTeamCount, 1, 2);
            Assert.Equal(raceTeamCount + 1, await afterRace.DraftPublicationCycles.CountAsync(x => x.DraftSessionId == draft.Id));
        }

        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostAddTeamAsync(setup.EventId, "Audit failure external", TeamFormationType.Preformed, null, CancellationToken.None, true), new ThrowingAuditWriter());
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
            await db.TeamMemberships.CountAsync(membership => teamIds.Contains(membership.TeamId)));
    }

    private async Task<IActionResult> ExecuteAsync(Guid eventId, Guid accountId, Func<DraftModel, Task<IActionResult>> action, Bingo.Application.Auditing.IAuditWriter? auditWriter = null, IAdminCollaborationNotifier? collaboration = null, DateTimeOffset? at = null, ITeamCaptainAuthorityService? captainAuthority = null)
    {
        await using var db = new ApplicationDbContext(options);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accountId.ToString()), new Claim(ClaimTypes.Name, $"admin-{accountId:N}")], "test"))
        };
        var clock = new FixedTimeProvider(at ?? now);
        var page = new DraftModel(db, clock, auditWriter ?? new AuditWriter(db, clock), collaboration ?? new NullAdminCollaborationNotifier(), null!, new Bingo.Infrastructure.Signups.EventParticipantCharacterService(db, clock), captainAuthority: captainAuthority ?? new TeamCaptainAuthorityService(db, clock))
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new EmptyTempDataProvider())
        };
        return await action(page);
    }

    private async Task<string?> ExecuteAndReadStatusAsync(Guid eventId, Guid accountId, Func<DraftModel, Task<IActionResult>> action)
    {
        await using var db = new ApplicationDbContext(options);
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

    private async Task<Setup> SeedAsync()
    {
        var firstLogin = $"draft-a-{Guid.NewGuid():N}";
        var secondLogin = $"draft-b-{Guid.NewGuid():N}";
        var firstAdmin = Account.CreateWebsite(Guid.NewGuid(), firstLogin, firstLogin.ToUpperInvariant(), now);
        var secondAdmin = Account.CreateWebsite(Guid.NewGuid(), secondLogin, secondLogin.ToUpperInvariant(), now);
        firstAdmin.SetGlobalRole(GlobalRole.Admin);
        secondAdmin.SetGlobalRole(GlobalRole.Admin);
        var passwords = new PasswordHasher<Account>();
        firstAdmin.SetPassword(passwords.HashPassword(firstAdmin, "password"), false, now, false);
        secondAdmin.SetPassword(passwords.HashPassword(secondAdmin, "password"), false, now, false);
        var item = new BingoEvent(Guid.NewGuid(), "Draft test", $"draft-{Guid.NewGuid():N}", "UTC", firstAdmin.Id, now.AddDays(-1));
        item.ConfigureSchedule(now.AddDays(-1), now.AddHours(-1), null, now.AddHours(1), now.AddDays(1), 10);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(now.AddDays(-1));
        item.CloseSignups(now.AddHours(-1));
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var primaryQuestion = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var firstTeam = new Team(Guid.NewGuid(), item.Id, "First", "first", TeamFormationType.Drafted, null, true);
        var secondTeam = new Team(Guid.NewGuid(), item.Id, "Second", "second", TeamFormationType.Drafted, null, true);
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
        var players = Enumerable.Range(1, 4).Select(index => new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, index, now, SignupSource.Website)).ToList();
        var characters = players.Select((player, index) => new OsrsCharacter(Guid.NewGuid(), $"Draft {index}", $"DRAFT {item.Id:N} {index}", now)).ToList();
        var assignments = players.Select((player, index) => new EventParticipantCharacter(Guid.NewGuid(), item.Id, player.Id, characters[index].Id, 0, now, null, primaryQuestion.Id, EventCharacterRole.Playing, index + 1, EhbSource.Manual, null)).ToList();
        await using var db = new ApplicationDbContext(options);
        db.AddRange(firstAdmin, secondAdmin, item, form, primaryQuestion, firstTeam, secondTeam, draft);
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
    private sealed record MutationSnapshot(string Memberships, string Assignments, string Cycles, string Roster, int AuditCount, int NotificationCount);
    private sealed record ExternalRosterCounts(int Participants, int CharacterReservations, int Memberships);
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed class EmptyTempDataProvider : ITempDataProvider { public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(); public void SaveTempData(HttpContext context, IDictionary<string, object> values) { } }
    private sealed class ThrowingAuditWriter : Bingo.Application.Auditing.IAuditWriter { public Task WriteAsync(Guid? actorAccountId, string actorUsername, string action, string targetType, string? targetId = null, string? details = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected audit failure"); public Task WriteAsync(Guid? actorAccountId, string actorUsername, string action, string targetType, string? targetId, string? details, Guid? eventId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected audit failure"); }
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
