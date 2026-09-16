using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Bingo.Infrastructure.Teams;
using Bingo.Web;
using Bingo.Web.Teams;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class ParticipantFlowIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_participant_flow").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Theory]
    [InlineData("encoding", "en")]
    [InlineData("encoding", "da")]
    [InlineData("primary", "en")]
    [InlineData("primary", "da")]
    public async Task InvalidCsvUploadReturnsRecoverablePreviewWithoutApplyOrRecords(string failure, string culture)
    {
        var admin = Website("csv-admin", GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "CSV preview event", "csv-preview", "UTC", admin.Id, now);
        var team = new Team(Guid.NewGuid(), item.Id, "CSV team", "csv-team", TeamFormationType.Preformed, null, false);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item, team, new DraftSession(Guid.NewGuid(), item.Id, 1));
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, admin);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        var route = $"/Admin/Events/Draft/{item.Id}";
        var page = await client.GetStringAsync($"{route}?rosterTeamId={team.Id}");
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent(Token(page)), "__RequestVerificationToken");
        upload.Add(new StringContent(team.Id.ToString()), "teamId");
        var bytes = Encoding.UTF8.GetBytes("Account,EHB,Account\r\nValid Main,1,\r\n");
        bytes = failure == "encoding" ? [.. bytes, 0xC3, 0x28] : Encoding.UTF8.GetBytes("Account,EHB,Account\r\nValid Main,1,\r\n,2,Secondary\r\n");
        upload.Add(new ByteArrayContent(bytes), "csv", "roster.csv");
        using var preview = await client.PostAsync($"{route}?handler=PreviewRosterCsv&culture={culture}&ui-culture={culture}", upload);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var html = WebUtility.HtmlDecode(await preview.Content.ReadAsStringAsync());
        Assert.Contains("draft-csv-callout-error", html);
        Assert.Contains(failure == "encoding" ? culture == "da" ? "Gem den som UTF-8" : "Save it as UTF-8" : culture == "da" ? "Den primære konto er påkrævet." : "Primary Account is required.", html);
        Assert.DoesNotContain("name=\"previewToken\"", html);
        Assert.DoesNotContain("DecoderFallbackException", html);
        Assert.DoesNotContain("Secondary", html);
        using var apply = await client.PostAsync($"{route}?handler=ApplyRosterCsv", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["teamId"] = team.Id.ToString(),
            ["previewToken"] = ""
        }));
        Assert.Equal(HttpStatusCode.Redirect, apply.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.EventParticipants.ToListAsync());
        Assert.Empty(await verify.OsrsCharacters.ToListAsync());
        Assert.Empty(await verify.EventParticipantCharacters.ToListAsync());
        Assert.Empty(await verify.TeamMemberships.ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == item.Id).ToListAsync());
    }

    [Fact]
    public async Task InterleavedPreformedMemberKeepsWaitingRanksAndOrdinaryAdmission()
    {
        var admin = Website("queue-admin", GlobalRole.Admin);
        var item = ClosedEvent(admin, "queue");
        var team = new Team(Guid.NewGuid(), item.Id, "External team", "external-team", TeamFormationType.Preformed, null, false);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var question = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var first = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddMinutes(-1), SignupSource.AdminCreated);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item, team, form, question, first);
            await db.SaveChangesAsync();
        }
        async Task<Guid> AddWaiterAsync(string name)
        {
            await using var db = new ApplicationDbContext(options);
            var result = await new SignupService(db, new SecretHasher(), new FixedClock(now)).CreateAdminParticipantAsync(new(item.Id, null, admin.Id, admin.LoginName, null,
                new Dictionary<Guid, AdminAccountAnswer> { [question.Id] = new(name, 1m) }, new Dictionary<Guid, string>()));
            Assert.True(result.Succeeded, result.Error);
            return result.ParticipantId!.Value;
        }
        var waiterA = await AddWaiterAsync("Waiter A");
        await using (var db = new ApplicationDbContext(options))
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, new FixedClock(now)), cache, new FixedClock(now));
            await using var csv = new MemoryStream(Encoding.UTF8.GetBytes("Account,EHB\r\nExternal Main,1\r\n"));
            var preview = await service.PreviewAsync(admin.Id, item.Id, team.Id, csv, CancellationToken.None);
            Assert.True(preview.IsValid);
            Assert.True((await service.ApplyAsync(admin.Id, admin.LoginName, item.Id, team.Id, preview.Nonce!, CancellationToken.None)).Succeeded);
        }
        var waiterB = await AddWaiterAsync("Waiter B");
        await using var factory = Factory();
        using var anonymous = Client(factory);
        var html = await anonymous.GetStringAsync($"/Events/{item.Slug}/Signups");
        var waitingRows = Regex.Matches(html, "<th scope=\"row\" data-label=\"Position\">(\\d+)</th>").Select(x => x.Groups[1].Value).ToArray();
        Assert.Equal<string>(["01", "02"], waitingRows);
        Assert.True(html.IndexOf("Waiter A", StringComparison.Ordinal) < html.IndexOf("Waiter B", StringComparison.Ordinal));
        Assert.Contains("<h2 id=\"confirmed-heading\">confirmed <span>2</span>", html);
        Assert.DoesNotContain(admin.LoginName, html);
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.Equal(SignupStatus.WaitingList, (await db.EventParticipants.FindAsync(waiterA))!.SignupStatus);
            Assert.Equal(SignupStatus.WaitingList, (await db.EventParticipants.FindAsync(waiterB))!.SignupStatus);
            Assert.Equal(1, (await db.Events.FindAsync(item.Id))!.ParticipantCap);
            Assert.True((await new SignupService(db, new SecretHasher(), new FixedClock(now)).WithdrawAsync(item.Id, first.Id, admin.Id, admin.LoginName, true)).Succeeded);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Confirmed, (await verify.EventParticipants.FindAsync(waiterA))!.SignupStatus);
        Assert.Equal(SignupStatus.WaitingList, (await verify.EventParticipants.FindAsync(waiterB))!.SignupStatus);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, await verify.EventParticipants.OrderBy(x => x.SignupSequence).Select(x => x.SignupSequence).ToArrayAsync());
        html = await anonymous.GetStringAsync($"/Events/{item.Slug}/Signups");
        Assert.Equal<string>(["01"], Regex.Matches(html, "<th scope=\"row\" data-label=\"Position\">(\\d+)</th>").Select(x => x.Groups[1].Value).ToArray());
    }

    [Theory]
    [InlineData(GlobalRole.User, false)]
    [InlineData(GlobalRole.Admin, false)]
    [InlineData(GlobalRole.User, true)]
    public async Task RoleNotificationLinksResolveForRecipientAcrossPublicationAndDemotion(GlobalRole ownerRole, bool publishBoard)
    {
        var admin = Website("role-admin", GlobalRole.Admin);
        var owner = Website("role-owner", ownerRole);
        var outsider = Website("role-outsider");
        var item = new BingoEvent(Guid.NewGuid(), "Role notification event", "role-event", "UTC", admin.Id, now.AddDays(-3));
        var team = new Team(Guid.NewGuid(), item.Id, "Private roster team", "private-team", TeamFormationType.Drafted, null, true);
        var member = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddDays(-2), SignupSource.AdminCreated);
        member.AssignOwner(owner);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, member.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "Fixture");
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, owner, outsider, item, team, member, membership, draft);
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var client = Client(factory);
        using var other = Client(factory);
        await LoginAsync(client, owner);
        await LoginAsync(other, outsider);
        using var adminClient = Client(factory);
        if (publishBoard) await LoginAsync(adminClient, admin);
        var seen = new HashSet<Guid>();
        foreach (var phase in new[] { "private", "closed", "published", "reopened", "live" })
        {
            if (phase == "reopened" && publishBoard)
            {
                var draftRoute = $"/Admin/Events/Draft/{item.Id}";
                var draftHtml = await adminClient.GetStringAsync(draftRoute);
                using var reopened = await adminClient.PostAsync($"{draftRoute}?handler=Reopen", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = Token(draftHtml),
                    ["confirmed"] = "true",
                    ["reason"] = "Fixture reopened roster correction"
                }));
                Assert.Equal(HttpStatusCode.Redirect, reopened.StatusCode);
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var current = await db.Events.SingleAsync();
                if (phase == "closed")
                {
                    current.ConfigureInitialSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(1), now.AddHours(4), 1);
                    current.MarkFirstPublic(now.AddDays(-2));
                    current.OpenSignups(now.AddDays(-2));
                    current.CloseSignups(now.AddDays(-1));
                }
                if (phase == "reopened")
                {
                    if (!publishBoard)
                    {
                        (await db.DraftPublicationCycles.SingleAsync()).Supersede(now, admin.Id, "Fixture reopen");
                        current.SetDraftRosterPublication(false);
                    }
                    else
                    {
                        Assert.True(current.BoardPublished);
                        Assert.False(current.TeamRostersPublished);
                        Assert.False(current.DraftResultsPublished);
                        Assert.Null(current.ActualStartedAt);
                        Assert.True(current.EventEndsAt > now);
                        Assert.False(await db.DraftPublicationCycles.AnyAsync(x => x.SupersededAt == null));
                        Assert.Equal(BoardState.Published, (await db.Boards.SingleAsync()).State);
                        var peer = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 2, now, SignupSource.AdminCreated);
                        var peerCharacter = new OsrsCharacter(Guid.NewGuid(), "Reopened roster peer", "REOPENED ROSTER PEER", now);
                        db.AddRange(peer, peerCharacter,
                            new TeamMembership(Guid.NewGuid(), team.Id, peer.Id, TeamMembershipRole.Participant, now, null, "Fixture private correction"),
                            new EventParticipantCharacter(Guid.NewGuid(), item.Id, peer.Id, peerCharacter.Id, 0, now, admin.Id, null, EventCharacterRole.Playing, 1m, EhbSource.Manual, null));
                    }
                }
                if (phase is "published" or "live")
                {
                    var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, phase == "published" ? 1 : 2, now, admin.Id);
                    db.AddRange(cycle, new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, member.Id, TeamMembershipRole.Participant, null, "Published player"));
                    current.SetDraftRosterPublication(true);
                    if (phase == "published" && publishBoard)
                    {
                        var session = await db.DraftSessions.SingleAsync();
                        session.Start(now);
                        session.Finalize(now);
                        current.SetDraftLocked(true);
                        (await db.Teams.SingleAsync()).Finalize(now);
                        var board = new Board(Guid.NewGuid(), item.Id, "Role notification board", 1, 1);
                        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Published tile", "Fixture objective", "", 1m);
                        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "Fixture requirement", true);
                        db.AddRange(board, tile, requirement);
                        await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement]);
                        current.SetBoardPublication(true, now);
                    }
                }
                if (phase == "live") current.StartEvent(now);
                await db.SaveChangesAsync();
            }
            foreach (var role in new[] { TeamMembershipRole.Captain, TeamMembershipRole.CoCaptain, TeamMembershipRole.Participant })
            {
                PersonalNotification notice;
                await using (var db = new ApplicationDbContext(options))
                {
                    var result = await new TeamCaptainAuthorityService(db, new FixedClock(now)).ChangeRoleAsync(new(item.Id, membership.Id, role, admin.Id, admin.LoginName));
                    Assert.True(result.Succeeded, result.Error);
                    notice = await db.PersonalNotifications.SingleAsync(x => x.RecipientAccountId == owner.Id && !seen.Contains(x.Id));
                    seen.Add(notice.Id);
                }
                var html = await FollowNoticeAsync(client, notice);
                Assert.Contains(item.Name, html);
                Assert.DoesNotContain(outsider.LoginName, html);
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/notifications?read={notice.Id}")).StatusCode);
                if (phase is "published" or "live")
                {
                    Assert.Contains("Published player", html);
                    Assert.Equal($"/Events/{item.Slug}/Teams", notice.Route);
                    if (ownerRole == GlobalRole.User)
                    {
                        using var confirmation = await client.GetAsync($"/Events/{item.Slug}/Signup/Confirmation?participantId={member.Id}");
                        Assert.Equal(HttpStatusCode.Redirect, confirmation.StatusCode);
                        Assert.StartsWith(publishBoard ? $"/Events/{item.Slug}/Board/{team.Slug}" : $"/Events/{item.Slug}/Teams", confirmation.Headers.Location!.OriginalString, StringComparison.Ordinal);
                        using var destination = await client.GetAsync(confirmation.Headers.Location);
                        Assert.Equal(HttpStatusCode.OK, destination.StatusCode);
                    }
                }
                else
                {
                    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Events/{item.Slug}/Teams")).StatusCode);
                    if (ownerRole == GlobalRole.User)
                    {
                        Assert.DoesNotContain(team.Name, html);
                        Assert.DoesNotContain("Published player", html);
                        Assert.DoesNotContain("Reopened roster peer", html);
                        Assert.Contains("data-signup-confirmation", html);
                        using var denied = await other.GetAsync(notice.Route);
                        AssertDenied(denied);
                    }
                    else Assert.StartsWith("/Admin/Events/Draft/", notice.Route, StringComparison.Ordinal);
                }
            }
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(15, await verify.PersonalNotifications.CountAsync());
        Assert.Equal(15, await verify.TeamMembershipRoleTransitions.CountAsync());
        Assert.Equal(15, await verify.AuditEntries.CountAsync(x => x.Action == "team.membership_role_changed"));
        Assert.Equal(ownerRole, (await verify.Accounts.FindAsync(owner.Id))!.GlobalRole);
        Assert.Equal(2, await verify.DraftPublicationRosters.CountAsync());
    }

    [Fact]
    public async Task LiveVacancyLinksKeepAdminFollowUpAndPermitOrdinaryLeadershipWithoutDuplicates()
    {
        var admin = Website("vacancy-admin", GlobalRole.Admin);
        var superAdmin = Website("vacancy-super", GlobalRole.SuperAdmin);
        var leader = Website("vacancy-leader");
        var coCaptain = Website("vacancy-co");
        var item = ClosedEvent(admin, "vacancy-event");
        item.SetDraftLocked(true);
        item.StartEvent(now);
        var team = new Team(Guid.NewGuid(), item.Id, "Vacancy team", "vacancy-team", TeamFormationType.Drafted, null, true);
        var departed = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddDays(-2), SignupSource.AdminCreated);
        var departedMembership = new TeamMembership(Guid.NewGuid(), team.Id, departed.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "Fixture");
        await using (var db = new ApplicationDbContext(options))
        {
            var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
            var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now, admin.Id);
            db.AddRange(admin, superAdmin, leader, coCaptain, item, team, departed, departedMembership, draft, cycle,
                new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, departed.Id, TeamMembershipRole.Participant, null, "Departed player"));
            long sequence = 2;
            foreach (var recipient in new[] { admin, leader, coCaptain })
            {
                var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, sequence++, now.AddDays(-2), SignupSource.AdminCreated);
                participant.AssignOwner(recipient);
                db.AddRange(participant, new TeamMembership(Guid.NewGuid(), team.Id, participant.Id,
                    recipient == coCaptain ? TeamMembershipRole.CoCaptain : TeamMembershipRole.Captain, now.AddDays(-1), null, "Fixture"));
            }
            await db.SaveChangesAsync();
            var failedOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new RejectNotifications()).Options;
            await using (var failedDb = new ApplicationDbContext(failedOptions))
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => new SignupService(failedDb, new SecretHasher(), new FixedClock(now))
                    .WithdrawLiveAsync(new(item.Id, departed.Id, admin.Id, admin.LoginName, departedMembership.Version)));
            }
            await using (var rolledBack = new ApplicationDbContext(options))
            {
                Assert.Equal(SignupStatus.Confirmed, (await rolledBack.EventParticipants.FindAsync(departed.Id))!.SignupStatus);
                Assert.Null((await rolledBack.TeamMemberships.FindAsync(departedMembership.Id))!.LeftAt);
                Assert.Empty(await rolledBack.PersonalNotifications.ToListAsync());
                Assert.Empty(await rolledBack.AuditEntries.Where(x => x.Action == "participant.live_withdrawn").ToListAsync());
            }
            var service = new SignupService(db, new SecretHasher(), new FixedClock(now));
            var result = await service.WithdrawLiveAsync(new(item.Id, departed.Id, admin.Id, admin.LoginName, departedMembership.Version));
            Assert.True(result.Succeeded, result.Error);
            Assert.False((await service.WithdrawLiveAsync(new(item.Id, departed.Id, admin.Id, admin.LoginName, departedMembership.Version))).Changed);
        }
        await using var factory = Factory();
        await using var verify = new ApplicationDbContext(options);
        var notices = await verify.PersonalNotifications.Where(x => x.Title == "participant.live_withdrawn").ToListAsync();
        Assert.Equal(4, notices.Count);
        Assert.Equal(4, notices.Select(x => x.RecipientAccountId).Distinct().Count());
        foreach (var recipient in new[] { admin, superAdmin, leader, coCaptain })
        {
            using var client = Client(factory);
            await LoginAsync(client, recipient);
            var notice = Assert.Single(notices, x => x.RecipientAccountId == recipient.Id);
            var html = await FollowNoticeAsync(client, notice);
            Assert.Contains(item.Name, html);
            if (recipient.GlobalRole is GlobalRole.Admin or GlobalRole.SuperAdmin)
            {
                Assert.Equal($"/Admin/Events/Participant/{item.Id}/Participants/{departed.Id}", notice.Route);
                Assert.Contains("vacancy", html, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.Contains(team.Name, html);
                Assert.DoesNotContain("Admin notes", html);
                using var denied = await client.GetAsync($"/Admin/Events/Participant/{item.Id}/Participants/{departed.Id}");
                AssertDenied(denied);
            }
        }
        Assert.Equal(SignupStatus.Withdrawn, (await verify.EventParticipants.FindAsync(departed.Id))!.SignupStatus);
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "participant.live_withdrawn").ToListAsync());
    }

    private static void AssertDenied(HttpResponseMessage response) => Assert.True(response.StatusCode == HttpStatusCode.Forbidden ||
        response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.OriginalString.Contains("AccessDenied", StringComparison.Ordinal));

    private async Task<string> FollowNoticeAsync(HttpClient client, PersonalNotification notice)
    {
        var inbox = await client.GetStringAsync("/notifications");
        var link = Regex.Match(inbox, $"href=\"(/notifications\\?read={notice.Id})\"");
        Assert.True(link.Success);
        using var opened = await client.GetAsync(WebUtility.HtmlDecode(link.Groups[1].Value));
        Assert.Equal(HttpStatusCode.Redirect, opened.StatusCode);
        Assert.Equal(notice.Route, opened.Headers.Location!.OriginalString);
        using var destination = await client.GetAsync(opened.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, destination.StatusCode);
        await using var db = new ApplicationDbContext(options);
        Assert.NotNull((await db.PersonalNotifications.FindAsync(notice.Id))!.ReadAt);
        return WebUtility.HtmlDecode(await destination.Content.ReadAsStringAsync());
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(now));
            services.ConfigureAll<HttpClientFactoryOptions>(settings => settings.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = new BlockExternalRequests()));
        }));

    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private Account Website(string name, GlobalRole role = GlobalRole.User)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);
        account.SetGlobalRole(role);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, false);
        return account;
    }
    private BingoEvent ClosedEvent(Account admin, string slug)
    {
        var item = new BingoEvent(Guid.NewGuid(), $"Participant flow {slug}", slug, "UTC", admin.Id, now.AddDays(-3));
        item.ConfigureInitialSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(1), now.AddHours(4), 1);
        item.MarkFirstPublic(now.AddDays(-2));
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        return item;
    }
    private static async Task LoginAsync(HttpClient client, Account account)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = account.LoginName,
            ["Input.Password"] = "password",
            ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("Login", response.Headers.Location!.OriginalString);
    }
    private static string Token(string html)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return WebUtility.HtmlDecode(token);
    }
    private sealed class RejectNotifications : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PersonalNotification>().Any(x => x.State == EntityState.Added))
                throw new DbUpdateException("Injected notification failure");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class BlockExternalRequests : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new InvalidOperationException("External providers are disabled in participant-flow tests.");
    }
}
