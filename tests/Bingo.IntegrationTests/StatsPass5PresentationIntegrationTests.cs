using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Bingo.Infrastructure.WiseOldMan;
using Bingo.Web;
using Bingo.Web.Pages.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task StatsPass5HttpRouteNavigationEscapingRefreshOwnerPreferenceAndArtwork()
    {
        var f = await FullStatsFixtureAsync();
        var drop = await PendingStatsAsync(f, 0, 0, 10); await ApproveStatsAsync(f, drop);
        var member = Account.CreateWebsite(Guid.NewGuid(), "StatsMember", "STATSMEMBER", f.Clock.GetUtcNow());
        await using (var setup = new ApplicationDbContext(options))
        {
            var actor = await setup.Accounts.SingleAsync(x => x.Id == f.Admin.Id);
            foreach (var account in new[] { actor, member })
            {
                account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, f.Clock.GetUtcNow(), false);
                account.CompleteOnboarding(f.Characters[0].Id, f.Clock.GetUtcNow());
            }
            setup.Add(member);
            (await setup.EventParticipants.SingleAsync(x => x.Id == f.Players[0].Id)).AssignOwner(member);
            await setup.Database.ExecuteSqlRawAsync("UPDATE teams SET name = '<script>window.leak=1</script>'");
            await setup.Database.ExecuteSqlRawAsync("UPDATE board_tiles SET name_snapshot = 'Unapproved draft tile name'");
            await setup.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(f.Clock); });
        });
        using var anon = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var owner = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var ordinary = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var route = $"/Events/{f.Event.Slug}/Stats";
        var publicHtml = await anon.GetStringAsync(route);
        Assert.Contains("stats-page.js", publicHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Unapproved draft tile name", publicHtml, StringComparison.Ordinal);
        Assert.Contains("\\u003Cscript\\u003Ewindow.leak=1\\u003C/script\\u003E", publicHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>window.leak", publicHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"adjust-artwork\"", publicHtml, StringComparison.Ordinal);
        foreach (var id in new[] { "event-size", "timeline-stage", "drop-preview", "header-theme", "theme-toggle", "sample-size-status" })
            Assert.DoesNotContain($"id=\"{id}\"", publicHtml, StringComparison.Ordinal);
        Assert.Contains(route, await anon.GetStringAsync($"/Events/{f.Event.Slug}/Board"), StringComparison.Ordinal);
        await LoginStatsAsync(owner, f.Admin.LoginName); await LoginStatsAsync(ordinary, member.LoginName);
        var ownerHtml = await owner.GetStringAsync(route); var memberHtml = await ordinary.GetStringAsync(route);
        Assert.Contains("id=\"artwork-editor\"", ownerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"artwork-editor\"", memberHtml, StringComparison.Ordinal);
        Assert.Contains($"/Submissions?eventId={f.Event.Id}&amp;teamId={f.Team.Id}", memberHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"shell\"", publicHtml, StringComparison.Ordinal);
        foreach (var role in new[] { Bingo.Domain.Teams.TeamMembershipRole.Captain, Bingo.Domain.Teams.TeamMembershipRole.CoCaptain })
        {
            await using (var roleContext = new ApplicationDbContext(options))
            {
                (await roleContext.TeamMemberships.SingleAsync(x => x.EventParticipantId == f.Players[0].Id)).ChangeRole(role);
                await roleContext.SaveChangesAsync();
            }
            var scopedHtml = await ordinary.GetStringAsync(route);
            Assert.Contains($"/Submissions?eventId={f.Event.Id}&amp;teamId={f.Team.Id}", scopedHtml, StringComparison.Ordinal);
            Assert.Contains(">Submissions</a>", scopedHtml, StringComparison.Ordinal);
        }

        using (var noToken = await owner.PostAsync(route + "?handler=Artwork", new FormUrlEncodedContent([]))) Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using (var noToken = await ordinary.PostAsync(route + "?handler=Guidance", new FormUrlEncodedContent([]))) Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        var anonTokenPage = await anon.GetStringAsync("/Account/Login");
        using (var forbidden = await PostStatsAsync(anon, route + "?handler=Guidance", anonTokenPage, new() { ["hidden"] = "true", ["expectedVersion"] = "1" })) Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
        var settings = new Dictionary<string, string> { ["itemId"] = f.Items[0].Id.ToString(), ["expectedVersion"] = "1", ["x"] = "25", ["y"] = "50", ["width"] = "40", ["height"] = "60", ["scale"] = "1.25", ["rotation"] = "-25" };
        using (var forbidden = await PostStatsAsync(ordinary, route + "?handler=Artwork", memberHtml, settings)) Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var memberBefore = JsonDocument.Parse(await ordinary.GetStringAsync(route + "?handler=Data"));
        var memberVersion = memberBefore.RootElement.GetProperty("preference").GetProperty("version").GetUInt32();
        using (var saved = await PostStatsAsync(ordinary, route + "?handler=Guidance", memberHtml, new() { ["hidden"] = "true", ["accountId"] = f.Admin.Id.ToString(), ["expectedVersion"] = memberVersion.ToString(CultureInfo.InvariantCulture) })) Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using (var stale = await PostStatsAsync(ordinary, route + "?handler=Guidance", memberHtml, new() { ["hidden"] = "false", ["expectedVersion"] = memberVersion.ToString(CultureInfo.InvariantCulture) })) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var freshSession = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginStatsAsync(freshSession, member.LoginName);
        using var reloaded = JsonDocument.Parse(await freshSession.GetStringAsync(route + "?handler=Data"));
        Assert.True(reloaded.RootElement.GetProperty("preference").GetProperty("hidden").GetBoolean());
        using var ownerBefore = JsonDocument.Parse(await owner.GetStringAsync(route + "?handler=Data"));
        Assert.False(ownerBefore.RootElement.GetProperty("preference").GetProperty("hidden").GetBoolean());
        settings["expectedVersion"] = ownerBefore.RootElement.GetProperty("artwork")[0].GetProperty("version").GetInt64().ToString(CultureInfo.InvariantCulture);
        using (var saved = await PostStatsAsync(owner, route + "?handler=Artwork", ownerHtml, settings)) Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using (var stale = await PostStatsAsync(owner, route + "?handler=Artwork", ownerHtml, settings)) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var artworkRead = JsonDocument.Parse(await anon.GetStringAsync(route + "?handler=Data"));
        Assert.Equal(-25, artworkRead.RootElement.GetProperty("artwork")[0].GetProperty("fit").GetProperty("rotation").GetDecimal());
        var savedVersion = artworkRead.RootElement.GetProperty("artwork")[0].GetProperty("version").GetInt64();
        settings["expectedVersion"] = savedVersion.ToString(CultureInfo.InvariantCulture); settings["rotation"] = "181";
        using (var invalid = await PostStatsAsync(owner, route + "?handler=Artwork", ownerHtml, settings)) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        settings["rotation"] = "NaN";
        using (var invalid = await PostStatsAsync(owner, route + "?handler=Artwork", ownerHtml, settings)) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        settings["reset"] = "true"; settings.Remove("rotation");
        using (var reset = await PostStatsAsync(owner, route + "?handler=Artwork", ownerHtml, settings)) Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        await ReverseStatsAsync(f, drop);
        using var reversed = JsonDocument.Parse(await anon.GetStringAsync(route + "?handler=Data"));
        Assert.Equal(0, reversed.RootElement.GetProperty("stats").GetProperty("value").GetProperty("drops").GetInt32());
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await verify.CatalogueItems.SingleAsync(x => x.Id == f.Items[0].Id)).ArtworkX);
        var audits = await verify.AuditEntries.Where(x => x.Action.StartsWith("catalogue.artwork_")).ToArrayAsync();
        Assert.Equal(2, audits.Length); Assert.All(audits, entry => Assert.Equal(f.Admin.Id, entry.ActorAccountId));
        using var afterAudit = JsonDocument.Parse(audits.Single(x => x.Action == "catalogue.artwork_updated").AfterState!);
        Assert.Equal(savedVersion, afterAudit.RootElement.GetProperty("version").GetInt64());
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("private")]
    [InlineData("unpublished")]
    [InlineData("unknown")]
    [InlineData("excluded")]
    [InlineData("cancelled")]
    public async Task StatsPass5HttpExactAccessWithoutFallback(string scenario)
    {
        var f = await FullStatsFixtureAsync();
        var slug = f.Event.Slug;
        await using (var db = new ApplicationDbContext(options))
        {
            if (scenario == "hidden") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET hidden_at = now(), hidden_by_account_id = {f.Admin.Id}, hidden_reason = 'Controlled hidden fixture'");
            if (scenario == "private") await db.Database.ExecuteSqlRawAsync("UPDATE events SET first_public_at = NULL");
            if (scenario == "unpublished") await db.Database.ExecuteSqlRawAsync("UPDATE events SET board_published = false");
            if (scenario == "excluded") { slug = "det-store-danske-sommerbingo-2026"; await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET slug = {slug}"); }
            if (scenario == "cancelled") await db.Database.ExecuteSqlRawAsync("UPDATE events SET state = 'Cancelled'");
            if (scenario == "unknown") slug = "unknown";
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var page = await client.GetAsync($"/Events/{slug}/Stats");
        Assert.Equal(scenario == "cancelled" ? HttpStatusCode.OK : HttpStatusCode.NotFound, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(); Assert.DoesNotContain("id=\"stats-data\"", html, StringComparison.Ordinal);
        using var data = await client.GetAsync($"/Events/{slug}/Stats?handler=Data"); Assert.Equal(HttpStatusCode.NotFound, data.StatusCode);
        await using var direct = new ApplicationDbContext(options);
        var handler = StatsPage(direct, f);
        Assert.IsType<NotFoundResult>(await handler.OnPostArtworkAsync(slug, f.Items[0].Id, 1, true, null, null, null, null, null, null, default));
    }

    [Theory]
    [InlineData(0, 0, 5, 5, .5, -180, true)]
    [InlineData(100, 100, 150, 200, 2.5, 180, true)]
    [InlineData(-.1, 50, 20, 20, 1, 0, false)]
    [InlineData(50, 100.1, 20, 20, 1, 0, false)]
    [InlineData(50, 50, 4.9, 20, 1, 0, false)]
    [InlineData(50, 50, 20, 200.1, 1, 0, false)]
    [InlineData(50, 50, 20, 20, .49, 0, false)]
    [InlineData(50, 50, 20, 20, 2.51, 0, false)]
    [InlineData(50, 50, 20, 20, 1, 180.1, false)]
    public async Task StatsPass5ArtworkBoundsThroughPostAndPostgreSql(double x, double y, double width, double height, double scale, double rotation, bool valid)
    {
        var f = await FullStatsFixtureAsync(); await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await using var db = new ApplicationDbContext(options);
        var result = await StatsPage(db, f).OnPostArtworkAsync(f.Event.Slug, f.Items[0].Id, f.Items[0].Version, false, (decimal)x, (decimal)y, (decimal)width, (decimal)height, (decimal)scale, (decimal)rotation, default);
        if (valid) Assert.IsType<JsonResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(valid ? (decimal?)x : null, (await verify.CatalogueItems.SingleAsync(x => x.Id == f.Items[0].Id)).ArtworkX);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => verify.Database.ExecuteSqlRawAsync("UPDATE catalogue_items SET artwork_x = -1"));
    }

    [Fact]
    public async Task StatsPass5ArtworkAuditFailureRollsBackSettingsAndVersion()
    {
        var f = await FullStatsFixtureAsync(); await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await using (var setup = new ApplicationDbContext(options))
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_artwork_audit_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN IF NEW.action LIKE 'catalogue.artwork_%' THEN RAISE EXCEPTION 'Controlled audit failure' USING ERRCODE = '23514'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER reject_artwork_audit_fixture BEFORE INSERT ON audit_entries FOR EACH ROW EXECUTE FUNCTION reject_artwork_audit_fixture();
                """);
        await using (var db = new ApplicationDbContext(options))
            await Assert.ThrowsAsync<DbUpdateException>(() => StatsPage(db, f).OnPostArtworkAsync(f.Event.Slug, f.Items[0].Id, f.Items[0].Version, false, 20, 30, 40, 50, 1, 0, default));
        await using var verify = new ApplicationDbContext(options);
        var item = await verify.CatalogueItems.SingleAsync(x => x.Id == f.Items[0].Id); Assert.Null(item.ArtworkX); Assert.Equal(f.Items[0].Version, item.Version);
        Assert.False(await verify.AuditEntries.AnyAsync(x => x.Action.StartsWith("catalogue.artwork_")));
    }

    [Fact]
    public async Task StatsPass5MigrationPreservesExistingOwnersAndDefaults()
    {
        var f = await FullStatsFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20260915190625_RetainLuckAfterAdditiveApproval");
        await RetainedCatalogueMigrationTestSupport.PrepareAsync(db);
        await db.Database.MigrateAsync();
        var account = await db.Accounts.SingleAsync(x => x.Id == f.Admin.Id);
        Assert.False(account.StatsGuidanceHidden);
        var item = await db.CatalogueItems.SingleAsync(x => x.Id == f.Items[0].Id); Assert.Null(item.ArtworkX); Assert.Equal(100, item.CatalogueValueGp);
        Assert.Equal(f.Event.Id, (await db.Events.SingleAsync(x => x.Id == f.Event.Id)).Id);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass5ConcurrentCatalogueEditOrRoleChangeRejectsArtwork(bool roleChange)
    {
        var f = await FullStatsFixtureAsync(); await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        var interceptor = new AfterStatsEventRead(async () =>
        {
            await using var other = new ApplicationDbContext(options);
            if (roleChange) (await other.Accounts.SingleAsync(x => x.Id == f.Admin.Id)).SetGlobalRole(GlobalRole.Admin);
            else (await other.CatalogueItems.SingleAsync(x => x.Id == f.Items[0].Id)).SetArtwork(75, 50, 40, 60, 1, 0);
            await other.SaveChangesAsync();
        });
        await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(interceptor).Options))
        {
            var result = await StatsPage(db, f).OnPostArtworkAsync(f.Event.Slug, f.Items[0].Id, f.Items[0].Version, false, 20, 30, 40, 50, 1, 0, default);
            Assert.Equal(409, Assert.IsType<ObjectResult>(result).StatusCode);
        }
        Assert.True(interceptor.Ran);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(roleChange ? null : (decimal?)75, (await verify.CatalogueItems.SingleAsync(x => x.Id == f.Items[0].Id)).ArtworkX);
        Assert.False(await verify.AuditEntries.AnyAsync(x => x.Action.StartsWith("catalogue.artwork_")));
    }

    [Fact]
    public async Task StatsPass5CorrectionMovedParticipantUsesCurrentMembershipWithRetainedTeamEvidence()
    {
        var f = await FullStatsFixtureAsync();
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        var current = new Team(Guid.NewGuid(), f.Event.Id, "Current team", "current-team", TeamFormationType.Drafted, null, true);
        current.Finalize(f.Clock.GetUtcNow());
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.EventParticipants.SingleAsync(x => x.Id == f.Players[0].Id)).AssignOwner(f.Admin);
            var oldMembership = await db.TeamMemberships.SingleAsync();
            oldMembership.Leave(f.Clock.GetUtcNow(), "Controlled move");
            db.Add(current);
            db.Add(new TeamMembership(Guid.NewGuid(), current.Id, f.Players[0].Id, TeamMembershipRole.Participant, f.Clock.GetUtcNow(), null, "Controlled move"));
            var oldPublication = await db.DraftPublicationCycles.SingleAsync();
            oldPublication.Supersede(f.Clock.GetUtcNow(), f.Admin.Id, "Controlled move");
            var currentPublication = new DraftPublicationCycle(Guid.NewGuid(), (await db.DraftSessions.SingleAsync()).Id,
                oldPublication.CycleNumber + 1, f.Clock.GetUtcNow(), f.Admin.Id, DraftPublicationMethod.DirectRoster);
            var retainedRoster = await db.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == oldPublication.Id).ToListAsync();
            db.Add(currentPublication);
            db.AddRange(retainedRoster.Select(row => new DraftPublicationRoster(Guid.NewGuid(), currentPublication.Id,
                row.TeamId, row.EventParticipantId, row.Role, row.EffectivePickNumber, row.PublicCharacterName)));
            db.Add(new DraftPublicationRoster(Guid.NewGuid(), currentPublication.Id, current.Id, f.Players[0].Id,
                TeamMembershipRole.Participant, null, f.Characters[0].DisplayName));
            await db.SaveChangesAsync();
        }
        await SyncStatsAsync(f, 100);
        var payload = await StatsCorrectionPayloadAsync(f, "moved-player");
        Assert.Equal(current.Id, payload.GetProperty("currentMembership").GetProperty("teamId").GetGuid());
        Assert.Equal(f.Players[0].Id, payload.GetProperty("currentMembership").GetProperty("playerId").GetGuid());
        var stats = payload.GetProperty("stats");
        Assert.Equal(2, stats.GetProperty("teams").EnumerateArray().Count(team => team.GetProperty("players").EnumerateArray().Any(p => p.GetProperty("playerId").GetGuid() == f.Players[0].Id)));
        Assert.Equal(2, stats.GetProperty("luck").GetProperty("teams").EnumerateArray().Count(team => team.GetProperty("players").EnumerateArray().Any(p => p.GetProperty("playerId").GetGuid() == f.Players[0].Id)));
        Assert.Equal(f.Team.Id, Assert.Single(stats.GetProperty("drops").EnumerateArray()).GetProperty("teamId").GetGuid());
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.TeamMemberships.SingleAsync(x => x.LeftAt == null)).Leave(f.Clock.GetUtcNow(), "Controlled departure");
            await db.SaveChangesAsync();
        }
        Assert.Equal(JsonValueKind.Null, (await StatsCorrectionPayloadAsync(f, "departed-player")).GetProperty("currentMembership").ValueKind);
    }

    [Fact]
    public async Task StatsPass5CalculatedCompletionDtosRetainRawHistoryAcrossFinalizationArchiveAndReopening()
    {
        var f = await FullStatsFixtureAsync(target: 1, dimensions: 5);
        for (var i = 0; i < 25; i++) await ApproveStatsAsync(f, await PendingStatsAsync(f, i, 0, i + 1));
        var rawCompleted = f.Event.ActualStartedAt!.Value.AddMinutes(25);
        var corrected = rawCompleted.AddMinutes(1);
        f.Clock.Advance(TimeSpan.FromHours(12));
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(db, null!, f.Clock).EndNowAsync(ev.Id, ev.Version, true, "Controlled end", new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        }
        f.Clock.Advance(TimeSpan.FromHours(1));
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new EventFinalizationService(db, new PublicBoardService(db, f.Clock), f.Clock);
            var ready = (await service.GetReadinessAsync(f.Event.Id))!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectCompletionAsync(
                f.Event.Id, f.Team.Id, corrected, "Retired Stats presentation correction", f.Admin.Id,
                ready.EventVersion, ready.ReviewCycleId));
            Assert.True(ready.CanFinalize, string.Join("; ", ready.Blockers.Select(x => x.Description)));
            await service.FinalizeAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName), ready.EventVersion);
        }
        foreach (var state in new[] { "finalized", "archived", "reopened" })
        {
            if (state != "finalized")
            {
                await using var db = new ApplicationDbContext(options);
                var service = new EventFinalizationService(db, new PublicBoardService(db, f.Clock), f.Clock);
                if (state == "reopened") await service.UnfinalizeAsync(f.Event.Id, "Controlled Stats reopening", true, new(f.Admin.Id, f.Admin.LoginName), (await db.Events.SingleAsync(x => x.Id == f.Event.Id)).Version); // B-Final-2: valid reopen supplies current version.
            }
            var payload = await StatsCorrectionPayloadAsync(f, state);
            var stats = payload.GetProperty("stats"); var team = Assert.Single(stats.GetProperty("teams").EnumerateArray());
            Assert.Equal(rawCompleted, team.GetProperty("progressHistory").EnumerateArray().Last().GetProperty("at").GetDateTimeOffset());
            var completion = stats.GetProperty("milestones").EnumerateArray().Single(x => x.GetProperty("id").GetString() == "board").GetProperty("at").GetDateTimeOffset();
            Assert.Equal(rawCompleted, completion);
            if (state == "reopened") Assert.Equal(JsonValueKind.Null, team.GetProperty("officialCompletion").ValueKind);
            else Assert.Equal(rawCompleted, team.GetProperty("officialCompletion").GetProperty("completedAt").GetDateTimeOffset());
        }
    }

    // Optional controlled-fixture export lets the Node harness execute the actual renderer
    // against these PostgreSQL DTOs. No participant data or fixture output is committed.
    private async Task<JsonElement> StatsCorrectionPayloadAsync(FullStatsFixture f, string name)
    {
        await using var db = new ApplicationDbContext(options);
        var result = Assert.IsType<JsonResult>(await StatsPage(db, f).OnGetDataAsync(f.Event.Slug, default));
        var payload = JsonSerializer.SerializeToElement(result.Value, Assert.IsType<JsonSerializerOptions>(result.SerializerSettings));
        var directory = Environment.GetEnvironmentVariable("STATS_PASS5_FIXTURE_DIRECTORY");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".json"), payload.GetRawText());
        }
        return payload;
    }

    private static StatsModel StatsPage(ApplicationDbContext db, FullStatsFixture f, Guid? actorId = null) => new(new PublicStatsService(db, f.Clock), new PublicBoardService(db, f.Clock), db, f.Clock,
        new CachedEventCompetitionActivityProjection(db, f.Clock), new EvidenceAuthority(db))
    {
        PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, (actorId ?? f.Admin.Id).ToString())], "Test")) } }
    };
    private static async Task LoginStatsAsync(HttpClient client, string username)
    {
        var html = await client.GetStringAsync("/Account/Login");
        using var result = await PostStatsAsync(client, "/Account/Login", html, new() { ["Input.Username"] = username, ["Input.Password"] = "password" });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
    }
    private static Task<HttpResponseMessage> PostStatsAsync(HttpClient client, string route, string html, Dictionary<string, string> values)
    {
        values = new(values) { ["__RequestVerificationToken"] = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value };
        return client.PostAsync(route, new FormUrlEncodedContent(values));
    }
}
