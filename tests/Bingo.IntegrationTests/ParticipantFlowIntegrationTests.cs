using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Bingo.Application.Integrations.WiseOldMan;
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
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class ParticipantFlowIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_participant_flow").WithUsername("bingo").WithPassword("bingo_test_password"));
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task DirectInternalParticipantCreationFailsClosedWithoutAnAccountValidator()
    {
        var admin = Website($"validation-admin-{Guid.NewGuid():N}", GlobalRole.Admin);
        var item = ClosedEvent(admin, $"validation-event-{Guid.NewGuid():N}");
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var question = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null,
            SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item, form, question);
            await db.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var service = new SignupService(db, new SecretHasher(), new FixedClock(now));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAdminParticipantAsync(new(item.Id, null, admin.Id, admin.LoginName, admin.Id,
                new Dictionary<Guid, AdminAccountAnswer> { [question.Id] = new("Unverified", 1m) }, new Dictionary<Guid, string>())));
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.EventParticipants.Where(x => x.EventId == item.Id).ToListAsync());
        Assert.False(await verify.OsrsCharacters.AnyAsync(x => x.NormalizedName == "UNVERIFIED INTERNAL"));
    }

    [Fact]
    public async Task AdminParticipantEditRendersAndBindsFreshOutageConfirmationToken()
    {
        var admin = Website($"edit-validation-admin-{Guid.NewGuid():N}", GlobalRole.Admin);
        var item = ClosedEvent(admin, $"edit-validation-{Guid.NewGuid():N}");
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var question = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null,
            SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated);
        var existingCharacter = new OsrsCharacter(Guid.NewGuid(), "Existing Edit Account", "EXISTING EDIT ACCOUNT", now);
        var existingAssignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, existingCharacter.Id, 0, now,
            admin.Id, question.Id, EventCharacterRole.Playing, 1m, EhbSource.Manual, null);
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(admin, item, form, question, participant, existingCharacter, existingAssignment);
            await seed.SaveChangesAsync();
        }

        var validation = new ContextBoundEditConfirmation();
        await using var factory = Factory(validation);
        using var client = Client(factory);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da");
        await LoginAsync(client, admin);
        // A10 (U5-Q1, item 1b): the old detail form and its WOM outage confirmation are retired.
        // The drawer save takes a typed RSN as an event-only account without any WOM request,
        // and a post to the old detail route is refused before any write.
        var oldRoute = $"/Admin/Events/Participant/{item.Id}/Participants/{participant.Id}";
        var route = $"/Admin/Events/Participants/{item.Id}";
        var page = await client.GetStringAsync(route);
        var editVersion = await CurrentResponseVersionAsync(participant.Id);
        using (var retired = await client.PostAsync(oldRoute, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["Input.ExpectedResponseVersion"] = editVersion.ToString(CultureInfo.InvariantCulture),
            [$"Input.AccountAnswers[{question.Id}].CharacterName"] = "First Outage",
            [$"Input.AccountAnswers[{question.Id}].Ehb"] = "5.5"
        }))) Assert.Equal(HttpStatusCode.NotFound, retired.StatusCode);
        Assert.Equal("Existing Edit Account", await CurrentParticipantCharacterAsync(participant.Id));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{route}?handler=SaveParticipant")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["participantId"] = participant.Id.ToString(),
                ["expectedResponseVersion"] = editVersion.ToString(CultureInfo.InvariantCulture),
                ["expectedPaid"] = "false",
                ["expectedNote"] = "",
                ["paid"] = "false",
                ["note"] = "",
                ["accounts"] = $"[{{\"assignmentId\":\"{existingAssignment.Id}\",\"name\":\"Changed Edit\",\"ehb\":5.5,\"role\":\"playing\",\"primary\":true}}]",
                ["answers"] = "{}"
            })
        };
        request.Headers.Add("Accept", "application/json"); request.Headers.Add("RequestVerificationToken", Token(page));
        using (var saved = await client.SendAsync(request))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            using var json = System.Text.Json.JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
            Assert.Equal("saved", json.RootElement.GetProperty("outcome").GetString());
        }
        Assert.Equal("Changed Edit", await CurrentParticipantCharacterAsync(participant.Id));
        await using (var verify = new ApplicationDbContext(options))
            Assert.Equal((decimal?)5.5m, await verify.EventParticipantCharacters.Where(value => value.EventParticipantId == participant.Id && value.ReleasedAt == null).Select(value => value.EhbSnapshot).SingleAsync());
        Assert.Empty(validation.Requests);
    }

    [Fact]
    public async Task RetiredCsvHandlersReturnNotFoundWithoutWrites()
    {
        var admin = Website("csv-admin", GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "CSV preview event", "csv-preview", "UTC", admin.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var team = new Team(Guid.NewGuid(), item.Id, "CSV team", "csv-team", TeamFormationType.Preformed, null, false);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item, team, new DraftSession(Guid.NewGuid(), item.Id, 1));
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, admin);
        var route = $"/Admin/Events/Draft/{item.Id}";
        var page = await client.GetStringAsync($"{route}?rosterTeamId={team.Id}");
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent(Token(page)), "__RequestVerificationToken");
        upload.Add(new StringContent(team.Id.ToString()), "teamId");
        upload.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Account,EHB\r\nLegacy Main,1\r\n")), "csv", "roster.csv");
        using var preview = await client.PostAsync($"{route}?handler=PreviewRosterCsv", upload);
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
        using var apply = await client.PostAsync($"{route}?handler=ApplyRosterCsv", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["teamId"] = team.Id.ToString(),
            ["previewToken"] = ""
        }));
        Assert.Equal(HttpStatusCode.NotFound, apply.StatusCode);
        using var external = await client.PostAsync($"{route}?handler=AddExternalMember", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["teamId"] = team.Id.ToString(),
            ["name"] = "Legacy Main",
            ["ehb"] = "1"
        }));
        Assert.Equal(HttpStatusCode.NotFound, external.StatusCode);
        using var remove = await client.PostAsync($"{route}?handler=RemoveExternalTeam", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["teamId"] = team.Id.ToString()
        }));
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        using var template = await client.GetAsync($"{route}?handler=RosterCsvTemplate&teamId={team.Id}");
        Assert.Equal(HttpStatusCode.NotFound, template.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.EventParticipants.ToListAsync());
        Assert.Empty(await verify.OsrsCharacters.ToListAsync());
        Assert.Empty(await verify.EventParticipantCharacters.ToListAsync());
        Assert.Empty(await verify.TeamMemberships.ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == item.Id).ToListAsync());
    }

    [Fact]
    public async Task SetupTeamConfigurationCanChangeDraftInclusionAndKeepsFormationHistory()
    {
        var admin = Website($"inclusion-admin-{Guid.NewGuid():N}", GlobalRole.Admin);
        var item = ClosedEvent(admin, $"inclusion-event-{Guid.NewGuid():N}");
        var team = new Team(Guid.NewGuid(), item.Id, "Historical manual team", "historical-manual-team", TeamFormationType.Preformed, "Legacy clan", false);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item, team, new DraftSession(Guid.NewGuid(), item.Id, 1));
            await db.SaveChangesAsync();
        }

        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, admin);
        var route = $"/Admin/Events/Draft/{item.Id}";

        async Task AddManualTeamAsync(string name)
        {
            var setupPage = await client.GetStringAsync(route);
            using var add = await client.PostAsync($"{route}?handler=AddTeam", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = Token(setupPage),
                ["name"] = name,
                ["affiliation"] = "Descriptive only",
                ["includedInDraft"] = "false"
            }));
            Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        }

        // Both Add Team surfaces use the same bound handler; an unchecked box
        // posts the explicit false value rather than falling back to inclusion.
        await AddManualTeamAsync("Manual setup team");
        await AddManualTeamAsync("Manual dialog team");
        var page = await client.GetStringAsync($"{route}?rosterTeamId={team.Id}");
        using var response = await client.PostAsync($"{route}?handler=UpdateTeam", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["teamId"] = team.Id.ToString(),
            ["rosterTeamId"] = team.Id.ToString(),
            ["name"] = team.Name,
            ["affiliation"] = team.AffiliationName ?? string.Empty,
            ["version"] = "1",
            ["removeImage"] = "false",
            ["includedInDraft"] = "true"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.Teams.SingleAsync(value => value.Id == team.Id);
        Assert.True(saved.IncludedInDraft);
        Assert.Equal(TeamFormationType.Preformed, saved.FormationType);
        var manualTeams = await verify.Teams.Where(value => value.Name == "Manual setup team" || value.Name == "Manual dialog team").ToListAsync();
        Assert.Equal(2, manualTeams.Count);
        Assert.All(manualTeams, value => Assert.False(value.IncludedInDraft));
        var audit = await verify.AuditEntries.SingleAsync(value => value.EventId == item.Id && value.Action == "team.inclusion_changed");
        Assert.Contains(team.Id.ToString(), audit.Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InterleavedPreformedMemberKeepsWaitingRanksAndOrdinaryAdmission()
    {
        var admin = Website("queue-admin", GlobalRole.Admin);
        var item = ClosedEvent(admin, "queue");
        // Keep the historical value intentionally inconsistent: current signup
        // promotion must follow IncludedInDraft, not FormationType.
        var team = new Team(Guid.NewGuid(), item.Id, "External team", "external-team", TeamFormationType.Drafted, null, false);
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
            var owner = Website($"{name.Replace(' ', '-').ToLowerInvariant()}-{Guid.NewGuid():N}", GlobalRole.User);
            db.Accounts.Add(owner);
            await db.SaveChangesAsync();
            var result = await new SignupService(db, new SecretHasher(), new FixedClock(now), accountValidation: new SuccessfulWiseOldManAccountValidation()).CreateAdminParticipantAsync(new(item.Id, null, admin.Id, admin.LoginName, owner.Id,
                new Dictionary<Guid, AdminAccountAnswer> { [question.Id] = new(name, 1m) }, new Dictionary<Guid, string>()));
            Assert.True(result.Succeeded, result.Error);
            return result.ParticipantId!.Value;
        }
        var waiterA = await AddWaiterAsync("Waiter A");
        await using (var db = new ApplicationDbContext(options))
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = new PreformedRosterCsvImportService(db, new EventParticipantCharacterService(db, new FixedClock(now)), cache, new FixedClock(now));
            await using var csv = new MemoryStream(Encoding.UTF8.GetBytes("Account,EHB\r\nExtern Main,1\r\n" /* U5-Q4: CSV import enforces the 12-character RSN rule (U5 review L1) */));
            var preview = await service.PreviewAsync(admin.Id, item.Id, team.Id, csv, CancellationToken.None);
            Assert.True(preview.IsValid);
            Assert.True((await service.ApplyAsync(admin.Id, admin.LoginName, item.Id, team.Id, preview.Nonce!, CancellationToken.None)).Succeeded);
        }
        var waiterB = await AddWaiterAsync("Waiter B");
        await using var factory = Factory();
        using var adminClient = Client(factory);
        await LoginAsync(adminClient, admin);
        // A10: Signup setup owns capacity counts; preserve both exact queue/count assertions.
        var signupSetupHtml = await adminClient.GetStringAsync($"/Admin/Events/SignupSetup/{item.Id}");
        Assert.Equal("2 of 1", WebUtility.HtmlDecode(Regex.Match(signupSetupHtml, "<dd[^>]*data-confirmed[^>]*>([^<]+)</dd>").Groups[1].Value));
        Assert.Equal("2", WebUtility.HtmlDecode(Regex.Match(signupSetupHtml, "<dd[^>]*data-waiting[^>]*>([^<]+)</dd>").Groups[1].Value));
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
        // The manual-team participant still consumes the only confirmed place;
        // withdrawing the other confirmed participant cannot promote a waiter.
        Assert.Equal(SignupStatus.WaitingList, (await verify.EventParticipants.FindAsync(waiterA))!.SignupStatus);
        Assert.Equal(SignupStatus.WaitingList, (await verify.EventParticipants.FindAsync(waiterB))!.SignupStatus);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, await verify.EventParticipants.OrderBy(x => x.SignupSequence).Select(x => x.SignupSequence).ToArrayAsync());
        html = await anonymous.GetStringAsync($"/Events/{item.Slug}/Signups");
        Assert.Equal<string>(["01", "02"], Regex.Matches(html, "<th scope=\"row\" data-label=\"Position\">(\\d+)</th>").Select(x => x.Groups[1].Value).ToArray());
    }

    [Fact]
    public async Task CapacityDoesNotChangeWhenAIncludedTeamBecomesManualOrIsRestored()
    {
        var admin = Website($"capacity-inclusion-admin-{Guid.NewGuid():N}", GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "Capacity inclusion event", $"capacity-inclusion-{Guid.NewGuid():N}", "UTC", admin.Id, now.AddDays(-3), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureInitialSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(1), now.AddHours(4), 2);
        item.MarkFirstPublic(now.AddDays(-2));
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        var team = new Team(Guid.NewGuid(), item.Id, "Switchable team", "switchable-team", TeamFormationType.Drafted, null, true);
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
        var confirmed = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddMinutes(-3), SignupSource.AdminCreated);
        var secondConfirmed = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 2, now.AddMinutes(-2), SignupSource.AdminCreated);
        var waiter = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 3, now.AddMinutes(-1), SignupSource.AdminCreated);
        waiter.MoveToWaiting(3, now.AddMinutes(-1));
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, confirmed.Id, TeamMembershipRole.Participant, now.AddMinutes(-2), null, "seed");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(admin, item, team, draft, confirmed, secondConfirmed, waiter, membership);
            await seed.SaveChangesAsync();
        }

        await using (var manual = new ApplicationDbContext(options))
        {
            (await manual.Teams.SingleAsync(value => value.Id == team.Id)).SetIncludedInDraft(false);
            await manual.SaveChangesAsync();
            var promoted = await new SignupService(manual, new SecretHasher(), new FixedClock(now)).PromoteAvailablePlacesAsync(item.Id);
            Assert.Equal(0, promoted);
        }
        await using (var afterManual = new ApplicationDbContext(options))
        {
            Assert.Equal(2, await afterManual.EventParticipants.CountAsync(value => value.EventId == item.Id && value.SignupStatus == SignupStatus.Confirmed));
            Assert.Equal(SignupStatus.WaitingList, await afterManual.EventParticipants.Where(value => value.Id == waiter.Id).Select(value => value.SignupStatus).SingleAsync());
            (await afterManual.Teams.SingleAsync(value => value.Id == team.Id)).SetIncludedInDraft(true);
            await afterManual.SaveChangesAsync();
            var promoted = await new SignupService(afterManual, new SecretHasher(), new FixedClock(now)).PromoteAvailablePlacesAsync(item.Id);
            Assert.Equal(0, promoted);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(2, await verify.EventParticipants.CountAsync(value => value.EventId == item.Id && value.SignupStatus == SignupStatus.Confirmed));
        Assert.Equal(SignupStatus.WaitingList, await verify.EventParticipants.Where(value => value.Id == waiter.Id).Select(value => value.SignupStatus).SingleAsync());
        Assert.True(await verify.Teams.Where(value => value.Id == team.Id).Select(value => value.IncludedInDraft).SingleAsync());
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
        var item = new BingoEvent(Guid.NewGuid(), "Role notification event", "role-event", "UTC", admin.Id, now.AddDays(-3), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var team = new Team(Guid.NewGuid(), item.Id, "Private roster team", "private-team", TeamFormationType.Drafted, null, true);
        var member = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddDays(-2), SignupSource.AdminCreated);
        member.AssignOwner(owner);
        var memberCharacter = new OsrsCharacter(Guid.NewGuid(), "Published player", "PUBLISHED PLAYER", now.AddDays(-2));
        var memberPlayingCharacter = new EventParticipantCharacter(Guid.NewGuid(), item.Id, member.Id, memberCharacter.Id, 0, now.AddDays(-2),
            admin.Id, null, EventCharacterRole.Playing, 1m, EhbSource.Manual, null);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, member.Id, TeamMembershipRole.Participant, now.AddDays(-1), null, "Fixture");
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, owner, outsider, item, team, member, memberCharacter, memberPlayingCharacter, membership, draft);
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var client = Client(factory);
        using var other = Client(factory);
        await LoginAsync(client, owner);
        await LoginAsync(other, outsider);
        using var adminClient = Client(factory);
        await LoginAsync(adminClient, admin);
        var seen = new HashSet<Guid>();
        foreach (var phase in new[] { "private", "closed", "published", "reopened", "live" })
        {
            if (phase == "published")
            {
                var draftRoute = $"/Admin/Events/Draft/{item.Id}";
                var draftHtml = await adminClient.GetStringAsync(draftRoute);
                using var finalized = await adminClient.PostAsync($"{draftRoute}?handler=Finalize", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = Token(draftHtml),
                    ["confirmed"] = "true"
                }));
                Assert.Equal(HttpStatusCode.Redirect, finalized.StatusCode);
            }
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
                Assert.Equal(HttpStatusCode.NotFound, reopened.StatusCode);
                await using var unchanged = new ApplicationDbContext(options);
                var unchangedEvent = await unchanged.Events.SingleAsync(x => x.Id == item.Id);
                Assert.True(unchangedEvent.TeamRostersPublished);
                Assert.True(unchangedEvent.DraftResultsPublished);
                Assert.Single(await unchanged.DraftPublicationCycles.Where(x => x.SupersededAt == null).ToListAsync());
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
                    var activeCycle = await db.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
                    activeCycle.Supersede(now, admin.Id, "Fixture reopened roster correction");
                    current.SetDraftRosterPublication(false);
                    if (publishBoard)
                    {
                        await db.SaveChangesAsync();
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
                if (phase == "published")
                {
                    Assert.True(current.TeamRostersPublished);
                    Assert.True(current.DraftResultsPublished);
                    Assert.Single(await db.DraftPublicationCycles.Where(x => x.SupersededAt == null).ToListAsync());
                    Assert.Single(await db.DraftPublicationRosters.ToListAsync());
                    if (publishBoard)
                    {
                        var board = new Board(Guid.NewGuid(), item.Id, "Role notification board", 1, 1);
                        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Published tile", "Fixture objective", "", 1m);
                        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "Fixture requirement", true);
                        db.AddRange(board, tile, requirement);
                        await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement]);
                        current.SetBoardPublication(true, now);
                    }
                }
                if (phase == "live")
                {
                    var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 2, now, admin.Id);
                    db.AddRange(cycle, new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, team.Id, member.Id, TeamMembershipRole.Participant, null, "Published player"));
                    current.SetDraftRosterPublication(true);
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
        }
        var before = await StateHashAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var result = await new SignupService(db, new SecretHasher(), new FixedClock(now))
                .WithdrawLiveAsync(new(item.Id, departed.Id, admin.Id, admin.LoginName, departedMembership.Version));
            Assert.False(result.Succeeded);
            Assert.Contains("Roster membership is fixed after the event first goes Live.", result.Error, StringComparison.Ordinal);
            var replacement = await new SignupService(db, new SecretHasher(), new FixedClock(now))
                .ReplaceVacancyAsync(new(item.Id, departedMembership.Id, admin.Id, admin.LoginName, Guid.NewGuid()));
            Assert.False(replacement.Succeeded);
            Assert.Contains("replacements and vacancies are retired", replacement.Error, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(before, await StateHashAsync());
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(SignupStatus.Confirmed, (await verify.EventParticipants.FindAsync(departed.Id))!.SignupStatus);
            Assert.Null((await verify.TeamMemberships.FindAsync(departedMembership.Id))!.LeftAt);
            Assert.Equal(departedMembership.Version, (await verify.TeamMemberships.FindAsync(departedMembership.Id))!.Version);
            Assert.Empty(await verify.PersonalNotifications.ToListAsync());
            Assert.Empty(await verify.AuditEntries.ToListAsync());
        }
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, admin);
        // A10 (U5 items 0a/1b, S5): the old detail route only redirects; its Withdraw and FillVacancy
        // posts are refused before any write, and the Participants Withdraw is refused from Live on.
        var participantPath = $"/Admin/Events/Participant/{item.Id}/Participants/{departed.Id}";
        var participantsPath = $"/Admin/Events/Participants/{item.Id}";
        var participantPage = await client.GetStringAsync(participantsPath);
        Assert.DoesNotContain("Fill open vacancy", await client.GetStringAsync($"{participantsPath}?handler=Current&participant={departed.Id}"), StringComparison.Ordinal);
        using (var rejected = await client.PostAsync($"{participantsPath}?handler=Withdraw", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["participantId"] = departed.Id.ToString(),
            ["confirmLifecycleAction"] = "true",
            ["__RequestVerificationToken"] = Token(participantPage)
        }))) { Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode); Assert.Equal($"/Admin/Events/Manage/{item.Id}", rejected.Headers.Location?.OriginalString); }
        foreach (var handler in new[] { "Withdraw", "FillVacancy" })
        {
            using var forged = await client.PostAsync($"{participantPath}?handler={handler}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["VacancyMembershipId"] = departedMembership.Id.ToString(),
                ["VacancyMembershipVersion"] = departedMembership.Version.ToString(CultureInfo.InvariantCulture),
                ["ReplacementWaitingParticipantId"] = Guid.NewGuid().ToString(),
                ["ConfirmLifecycleAction"] = "true",
                ["__RequestVerificationToken"] = Token(participantPage)
            }));
            Assert.Equal(HttpStatusCode.NotFound, forged.StatusCode);
        }
        Assert.Equal(before, await StateHashAsync());
    }

    private async Task<string> StateHashAsync()
    {
        var tables = new[]
        {
            "events", "event_participants", "event_participant_characters", "osrs_characters", "signup_answers",
            "team_memberships", "team_membership_role_transitions", "draft_sessions", "draft_picks",
            "draft_publication_cycles", "draft_publication_rosters", "audit_entries", "personal_notifications",
            "waiting_list_promotion_follow_ups", "event_participant_character_swaps", "submissions",
            "submission_contributions", "event_competition_synchronizations"
        };
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        var state = new StringBuilder();
        foreach (var table in tables)
        {
            await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(row) ORDER BY to_jsonb(row)::text), '[]'::jsonb)::text FROM {table} row", connection);
            state.Append(table).Append(':').Append(await command.ExecuteScalarAsync());
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state.ToString())));
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

    private WebApplicationFactory<Program> Factory(IWiseOldManAccountValidation? accountValidation = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(now));
            if (accountValidation is not null)
            {
                services.RemoveAll<IWiseOldManAccountValidation>();
                services.AddSingleton(accountValidation);
            }
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
        var item = new BingoEvent(Guid.NewGuid(), $"Participant flow {slug}", slug, "UTC", admin.Id, now.AddDays(-3), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
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
    private async Task<string> CurrentParticipantCharacterAsync(Guid participantId)
    {
        await using var db = new ApplicationDbContext(options);
        return await (from assignment in db.EventParticipantCharacters
                      join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                      where assignment.EventParticipantId == participantId && assignment.ReleasedAt == null
                      select character.DisplayName).SingleAsync();
    }
    private async Task<int> CurrentResponseVersionAsync(Guid participantId)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.EventParticipants.Where(participant => participant.Id == participantId).Select(participant => participant.ResponseVersion).SingleAsync();
    }
    private static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, "<input\\b(?=[^>]*\\bname=\"" + Regex.Escape(name) + "\")(?=[^>]*\\bvalue=\"([^\"]*)\")[^>]*>");
        Assert.True(match.Success, $"Could not find hidden input '{name}'.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
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
    private sealed class ContextBoundEditConfirmation : IWiseOldManAccountValidation
    {
        private readonly Dictionary<string, string> issued = new(StringComparer.Ordinal);
        private int nextToken;
        public List<WiseOldManAccountValidationRequest> Requests { get; } = [];
        public Task<WiseOldManAccountValidationResult> ValidateAsync(WiseOldManAccountValidationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var binding = string.Join("|", request.ActorAccountId, request.Action, request.EventId, request.ParticipantId,
                request.ExpectedVersion, string.Join(",", request.CharacterNames.Select(name => name.Trim().ToUpperInvariant()).Order(StringComparer.Ordinal)));
            if (request.ConfirmationToken is { } token && issued.TryGetValue(token, out var expected) && expected == binding)
            {
                issued.Remove(token);
                return Task.FromResult(new WiseOldManAccountValidationResult(WiseOldManAccountValidationOutcome.ConfirmedOperationalFailure, []));
            }

            var issuedToken = $"edit-confirmation-{++nextToken}";
            issued[issuedToken] = binding;
            var name = request.CharacterNames.FirstOrDefault() ?? "Edited account";
            var issue = new WiseOldManAccountValidationIssue(name, name.Trim().ToUpperInvariant(), WiseOldManLookupStatus.Unavailable);
            return Task.FromResult(new WiseOldManAccountValidationResult(WiseOldManAccountValidationOutcome.ConfirmationRequired, [issue], issuedToken));
        }
    }
    private sealed class BlockExternalRequests : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new InvalidOperationException("External providers are disabled in participant-flow tests.");
    }
}
