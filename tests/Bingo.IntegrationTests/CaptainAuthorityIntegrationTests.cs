using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Teams;
using Bingo.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class CaptainAuthorityIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_captain_authority").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    public async Task InitializeAsync() { await database.StartAsync(); options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options; await using var db = new ApplicationDbContext(options); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task OwnedCurrentMembershipRoleChangeIsAtomicAuditedAndImmediatelyAuthoritative()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var admin = Website("captain-admin", now); admin.SetGlobalRole(GlobalRole.Admin);
        var owner = Website("captain-owner", now);
        var item = new BingoEvent(Guid.NewGuid(), "Captain authority", "captain-authority", "UTC", admin.Id, now);
        var team = new Team(Guid.NewGuid(), item.Id, "Drafted", "drafted", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated); participant.AssignOwner(owner);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, "seed");
        db.AddRange(admin, owner, item, team, participant, membership);
        await db.SaveChangesAsync();

        var service = new TeamCaptainAuthorityService(db, TimeProvider.System);
        var promoted = await service.ChangeRoleAsync(new(item.Id, membership.Id, TeamMembershipRole.Captain, admin.Id, admin.LoginName), CancellationToken.None);

        Assert.True(promoted.Succeeded);
        Assert.True(await service.HasCurrentCaptainAuthorityAsync(owner.Id, item.Id, team.Id));
        Assert.Single(await db.TeamMembershipRoleTransitions.Where(x => x.TeamMembershipId == membership.Id).ToListAsync());
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "team.membership_role_changed").ToListAsync());
        Assert.Single(await db.PersonalNotifications.Where(x => x.RecipientAccountId == owner.Id).ToListAsync());

        owner.Disable(now, admin.Id, "test");
        await db.SaveChangesAsync();
        Assert.False(await service.HasCurrentCaptainAuthorityAsync(owner.Id, item.Id, team.Id));
    }

    [Fact]
    public async Task SharedReadinessSeparatesDraftCaptainFromEventCaptainAndEmergencyFallback()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var admin = Website("readiness-admin", now); admin.SetGlobalRole(GlobalRole.Admin);
        var item = ClosedEvent(admin.Id, "readiness", now);
        var drafted = new Team(Guid.NewGuid(), item.Id, "Drafted", "drafted", TeamFormationType.Drafted, null, true);
        var draftedCoOnly = new Team(Guid.NewGuid(), item.Id, "Drafted Co", "drafted-co", TeamFormationType.Drafted, null, true);
        var preformed = new Team(Guid.NewGuid(), item.Id, "Preformed", "preformed", TeamFormationType.Preformed, null, false);
        var ownedCaptain = Website("readiness-owner", now);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated); participant.AssignOwner(ownedCaptain);
        var coParticipant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 2, now, SignupSource.AdminCreated);
        var draftCoParticipant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 3, now, SignupSource.AdminCreated);
        var captain = new TeamMembership(Guid.NewGuid(), drafted.Id, participant.Id, TeamMembershipRole.Captain, now, null, "seed");
        var coCaptain = new TeamMembership(Guid.NewGuid(), preformed.Id, coParticipant.Id, TeamMembershipRole.CoCaptain, now, null, "seed");
        var draftCoCaptain = new TeamMembership(Guid.NewGuid(), draftedCoOnly.Id, draftCoParticipant.Id, TeamMembershipRole.CoCaptain, now, null, "seed");
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 1); draft.Start(now); draft.Finalize(now);
        var board = new Bingo.Domain.Boards.Board(Guid.NewGuid(), item.Id, "Board", 1, 1);
        var draftGateItem = ClosedEvent(admin.Id, "draft-gate", now);
        var gateCaptainParticipant = new EventParticipant(Guid.NewGuid(), draftGateItem.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated);
        var gateCoParticipant = new EventParticipant(Guid.NewGuid(), draftGateItem.Id, SignupStatus.Confirmed, 2, now, SignupSource.AdminCreated);
        var gateCaptainTeam = new Team(Guid.NewGuid(), draftGateItem.Id, "Gate Captain", "gate-captain", TeamFormationType.Drafted, null, true);
        var gateCoTeam = new Team(Guid.NewGuid(), draftGateItem.Id, "Gate Co", "gate-co", TeamFormationType.Drafted, null, true);
        var gateExternal = new Team(Guid.NewGuid(), draftGateItem.Id, "Gate External", "gate-external", TeamFormationType.Preformed, null, false);
        db.AddRange(admin, ownedCaptain, item, drafted, draftedCoOnly, preformed, participant, coParticipant, draftCoParticipant, captain, coCaptain, draftCoCaptain, draft, board,
            draftGateItem, gateCaptainParticipant, gateCoParticipant, gateCaptainTeam, gateCoTeam, gateExternal, new DraftSession(Guid.NewGuid(), draftGateItem.Id, 1),
            new TeamMembership(Guid.NewGuid(), gateCaptainTeam.Id, gateCaptainParticipant.Id, TeamMembershipRole.Captain, now, null, "seed"), new TeamMembership(Guid.NewGuid(), gateCoTeam.Id, gateCoParticipant.Id, TeamMembershipRole.CoCaptain, now, null, "seed"));
        await BoardApprovalFixture.PublishAsync(db, board, now);

        var lifecycle = new EventLifecycleService(db, null!, TimeProvider.System);
        var blocked = await lifecycle.GetStartReadinessAsync(item.Id);
        Assert.Contains(blocked!.Blockers, x => x.Code == "TEAM_ACCESS_MISSING" && x.Description.Contains("Preformed", StringComparison.Ordinal));

        var emergency = Account.CreateEmergency(Guid.NewGuid(), "readiness-emergency", "READINESS-EMERGENCY", now); emergency.SetPassword(new PasswordHasher<Account>().HashPassword(emergency, "password"), false, now, false); emergency.Enable();
        var scoped = new AccountEventAccess(Guid.NewGuid(), emergency.Id, item.Id, preformed.Id, null, null, null, null); scoped.Enable();
        db.AddRange(emergency, scoped);
        await db.SaveChangesAsync();
        Assert.DoesNotContain((await lifecycle.GetStartReadinessAsync(item.Id))!.Blockers, x => x.Code == "TEAM_ACCESS_MISSING" && x.Description.Contains("Preformed", StringComparison.Ordinal));

        item.StartEvent(now);
        await db.SaveChangesAsync();
        var roles = new TeamCaptainAuthorityService(db, TimeProvider.System);
        Assert.True((await roles.ChangeRoleAsync(new(item.Id, captain.Id, TeamMembershipRole.Participant, admin.Id, admin.LoginName))).Succeeded);
        Assert.Equal(EventState.Live, await db.Events.Where(x => x.Id == item.Id).Select(x => x.State).SingleAsync());
        Assert.Single((await lifecycle.GetStartReadinessAsync(item.Id))!.Blockers, x => x.Code == "TEAM_ACCESS_MISSING" && x.Description.StartsWith("Drafted needs", StringComparison.Ordinal));
        Assert.True((await roles.ChangeRoleAsync(new(item.Id, captain.Id, TeamMembershipRole.Captain, admin.Id, admin.LoginName))).Succeeded);
        Assert.DoesNotContain((await lifecycle.GetStartReadinessAsync(item.Id))!.Blockers, x => x.Code == "TEAM_ACCESS_MISSING" && x.Description.StartsWith("Drafted needs", StringComparison.Ordinal));

        var draftPage = await DraftStartResultAsync(draftGateItem.Id, admin.Id, now);
        Assert.Contains("Captain", draftPage.Message, StringComparison.Ordinal); // Co-captain-only drafted team fails; Preformed is excluded.
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(draftPage.Result);
    }

    [Fact]
    public async Task ConcurrentRoleChangesHaveOneWinnerAndNoLoserResidue()
    {
        var now = DateTimeOffset.UtcNow; Guid eventId; Guid membershipId; Guid adminId; Guid ownerId;
        await using (var setup = new ApplicationDbContext(options))
        {
            var admin = Website("race-admin", now); admin.SetGlobalRole(GlobalRole.Admin); var owner = Website("race-owner", now);
            var item = new BingoEvent(Guid.NewGuid(), "Race", "race", "UTC", admin.Id, now); var team = new Team(Guid.NewGuid(), item.Id, "Team", "team", TeamFormationType.Drafted, null, true);
            var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated); participant.AssignOwner(owner);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, "seed");
            setup.AddRange(admin, owner, item, team, participant, membership); await setup.SaveChangesAsync(); eventId = item.Id; membershipId = membership.Id; adminId = admin.Id; ownerId = owner.Id;
        }
        var barrier = new RoleChangeRaceBarrier();
        var raceOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(barrier).Options;
        async Task<(TeamMembershipRole Role, TeamCaptainRoleChangeResult Result)> Change(TeamMembershipRole role)
        { await using var context = new ApplicationDbContext(raceOptions); return (role, await new TeamCaptainAuthorityService(context, TimeProvider.System).ChangeRoleAsync(new(eventId, membershipId, role, adminId, "race-admin"))); }
        var results = await Task.WhenAll(Change(TeamMembershipRole.Captain), Change(TeamMembershipRole.CoCaptain));
        var winner = Assert.Single(results, x => x.Result.Succeeded);
        var loser = Assert.Single(results, x => !x.Result.Succeeded);
        Assert.Contains("changed this membership first", loser.Result.Error, StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        var finalMembership = await verify.TeamMemberships.SingleAsync(x => x.Id == membershipId);
        Assert.Equal(winner.Role, finalMembership.Role);
        var transition = Assert.Single(await verify.TeamMembershipRoleTransitions.Where(x => x.TeamMembershipId == membershipId).ToListAsync());
        Assert.Equal(TeamMembershipRole.Participant, transition.FromRole);
        Assert.Equal(winner.Role, transition.ToRole);
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "team.membership_role_changed").ToListAsync());
        Assert.Single(await verify.PersonalNotifications.Where(x => x.RecipientAccountId == ownerId).ToListAsync());
        Assert.Empty(await verify.AccountEventAccesses.ToListAsync());
    }

    private sealed class RoleChangeRaceBarrier : SaveChangesInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!eventData.Context!.ChangeTracker.Entries<TeamMembershipRoleTransition>().Any(entry => entry.State == EntityState.Added))
                return result;

            if (Interlocked.Increment(ref arrivals) == 2)
                release.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    [Fact]
    public async Task AuthenticatedDraftCaptainsReceiveOnlyTheApprovedTableAndThenRosterRedirect()
    {
        var now = DateTimeOffset.UtcNow; string slug; string captainLogin; string coLogin; string adminLogin;
        await using (var db = new ApplicationDbContext(options))
        {
            var admin = PasswordWebsite("table-admin", "password", now); admin.SetGlobalRole(GlobalRole.Admin);
            var captainAccount = PasswordWebsite("table-captain", "password", now); var coAccount = PasswordWebsite("table-co", "password", now);
            var item = ClosedEvent(admin.Id, "captain-table", now); slug = item.Slug;
            var form = new SignupForm(Guid.NewGuid(), item.Id, now); var question = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "selection", "Selection answer", SignupQuestionType.Text, false, 0, null);
            var drafted = new Team(Guid.NewGuid(), item.Id, "Drafted", "drafted-table", TeamFormationType.Drafted, null, true); var external = new Team(Guid.NewGuid(), item.Id, "External", "external-table", TeamFormationType.Preformed, null, false);
            var captain = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.Website); captain.AssignOwner(captainAccount);
            var co = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 2, now, SignupSource.Website); co.AssignOwner(coAccount);
            var waiting = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.WaitingList, 3, now, SignupSource.Website); var withdrawn = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Withdrawn, 4, now, SignupSource.Website);
            var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
            var captainCharacter = new OsrsCharacter(Guid.NewGuid(), "Frozen captain", "FROZEN CAPTAIN", now); var coCharacter = new OsrsCharacter(Guid.NewGuid(), "Frozen co", "FROZEN CO", now);
            var captainAssignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, captain.Id, captainCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 99m, EhbSource.Manual, null);
            var coAssignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, co.Id, coCharacter.Id, 0, now, null, null, EventCharacterRole.Playing, 88m, EhbSource.Manual, null);
            db.AddRange(admin, captainAccount, coAccount, item, form, question, drafted, external, captain, co, waiting, withdrawn, draft,
                captainCharacter, coCharacter, captainAssignment, coAssignment,
                new TeamMembership(Guid.NewGuid(), drafted.Id, captain.Id, TeamMembershipRole.Captain, now, null, "seed"), new TeamMembership(Guid.NewGuid(), drafted.Id, co.Id, TeamMembershipRole.CoCaptain, now, null, "seed"),
                new SignupAnswer(Guid.NewGuid(), captain.Id, question.Id, "Selection answer", "DRAFT-ONLY-ANSWER"), new SignupAnswer(Guid.NewGuid(), waiting.Id, question.Id, "Selection answer", "WAITING-SECRET"));
            await db.SaveChangesAsync(); captainLogin = captainAccount.LoginName; coLogin = coAccount.LoginName; adminLogin = admin.LoginName;
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var captainClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); using var coClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(captainClient, captainLogin); await LoginAsync(coClient, coLogin); await LoginAsync(adminClient, adminLogin);
        foreach (var client in new[] { captainClient, coClient }) { var page = await client.GetStringAsync($"/Events/{slug}/Signups"); Assert.Contains("DRAFT-ONLY-ANSWER", page, StringComparison.Ordinal); Assert.DoesNotContain("WAITING-SECRET", page, StringComparison.Ordinal); Assert.DoesNotContain("Payment", page, StringComparison.Ordinal); Assert.DoesNotContain("Admin notes", page, StringComparison.Ordinal); }
        await using (var db = new ApplicationDbContext(options)) { var item = await db.Events.SingleAsync(x => x.Slug == slug); var draft = await db.DraftSessions.SingleAsync(x => x.EventId == item.Id); draft.Start(now); draft.Finalize(now); var team = await db.Teams.SingleAsync(x => x.Slug == "drafted-table"); team.Finalize(now); var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now, item.CreatedByAccountId); db.DraftPublicationCycles.Add(cycle); foreach (var member in await db.TeamMemberships.Where(x => x.TeamId == team.Id && x.LeftAt == null).ToListAsync()) db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, member.EventParticipantId, member.Role, null, member.Role == TeamMembershipRole.Captain ? "Frozen captain" : "Frozen co")); item.SetDraftRosterPublication(true); await db.SaveChangesAsync(); }
        var redirect = await captainClient.GetAsync($"/Events/{slug}/Signups"); Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode); Assert.Equal($"/Events/{slug}/Teams", redirect.Headers.Location?.OriginalString); Assert.Contains("DRAFT-ONLY-ANSWER", await adminClient.GetStringAsync($"/Events/{slug}/Signups"), StringComparison.Ordinal);
        using var publicClient = factory.CreateClient(); var publicRoster = await publicClient.GetStringAsync($"/Events/{slug}/Teams");
        Assert.Contains("Frozen captain", publicRoster, StringComparison.Ordinal); Assert.Contains("Frozen co", publicRoster, StringComparison.Ordinal);
        Assert.DoesNotContain("DRAFT-ONLY-ANSWER", publicRoster, StringComparison.Ordinal); Assert.DoesNotContain("WAITING-SECRET", publicRoster, StringComparison.Ordinal);
        Assert.DoesNotContain("Payment", publicRoster, StringComparison.Ordinal); Assert.DoesNotContain("Admin notes", publicRoster, StringComparison.Ordinal); Assert.DoesNotContain("EHB", publicRoster, StringComparison.Ordinal);
    }

    private async Task<(Microsoft.AspNetCore.Mvc.IActionResult Result, string Message)> DraftStartResultAsync(Guid eventId, Guid adminId, DateTimeOffset now)
    {
        await using var db = new ApplicationDbContext(options); var context = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.ToString()), new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin")], "test")) };
        var page = new Bingo.Web.Pages.Admin.Events.DraftModel(db, TimeProvider.System, new Bingo.Infrastructure.Auditing.AuditWriter(db, TimeProvider.System), new Bingo.Infrastructure.Teams.NullAdminCollaborationNotifier(), null!, null!) { PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext(new Microsoft.AspNetCore.Mvc.ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.RazorPages.PageActionDescriptor())), TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(context, new EmptyTempDataProvider()) };
        await page.OnPostAcquireControlAsync(eventId, CancellationToken.None);
        var result = await page.OnPostStartAsync(eventId, CancellationToken.None); return (result, page.TempData["StatusMessage"]?.ToString() ?? string.Empty);
    }

    private static BingoEvent ClosedEvent(Guid adminId, string slug, DateTimeOffset now) { var item = new BingoEvent(Guid.NewGuid(), slug, slug, "UTC", adminId, now.AddDays(-1)); item.ConfigureSchedule(now.AddDays(-1), now.AddHours(-1), null, now.AddHours(1), now.AddDays(1), 20); item.ConfigureSignup(true, false, null); item.OpenSignups(now.AddDays(-1)); item.CloseSignups(now); return item; }
    private static Account PasswordWebsite(string name, string password, DateTimeOffset now) { var account = Website(name, now); account.SetPassword(new PasswordHasher<Account>().HashPassword(account, password), false, now, false); return account; }
    private static async Task LoginAsync(HttpClient client, string username) { var page = await client.GetStringAsync("/Account/Login"); var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value; var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = username, ["Input.Password"] = "password", ["__RequestVerificationToken"] = token })); Assert.Equal(HttpStatusCode.Redirect, result.StatusCode); }

    private static Account Website(string name, DateTimeOffset now) => Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);
    private sealed class EmptyTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider { public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>(); public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { } }
}
