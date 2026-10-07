using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AuditHistoryIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_audit_history")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        );

    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
        await using var db = new ApplicationDbContext(options);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task AuditFiltersIncludeHiddenHistoryUseExactActionsAndRespectDstCalendarBounds()
    {
        var actorId = Guid.NewGuid();
        var hiddenEventId = Guid.NewGuid();
        var hiddenEvent = BingoEvent.CreateArchivedHistorical(
            hiddenEventId,
            "Hidden audit history",
            "hidden-audit-history",
            null,
            "Europe/Copenhagen",
            null,
            null,
            new DateTimeOffset(2027, 3, 28, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 3, 28, 13, 0, 0, TimeSpan.Zero),
            actorId,
            new DateTimeOffset(2027, 3, 27, 12, 0, 0, TimeSpan.Zero),
            null,
            1,
            1,
            1,
            1);
        hiddenEvent.Hide(actorId, new DateTimeOffset(2027, 3, 29, 12, 0, 0, TimeSpan.Zero), hiddenEvent.Name, "Controlled audit fixture");

        var exactActionId = Guid.NewGuid();
        var areaActionId = Guid.NewGuid();
        var nextCalendarDayId = Guid.NewGuid();
        var accountActionId = Guid.NewGuid();
        var redactedHiddenActionId = Guid.NewGuid();
        var insideDayExact = new DateTimeOffset(2027, 3, 28, 21, 30, 0, TimeSpan.Zero);
        var insideDayArea = insideDayExact.AddMinutes(1);
        var nextCalendarDay = new DateTimeOffset(2027, 3, 28, 22, 30, 0, TimeSpan.Zero);
        var accountAction = insideDayExact.AddMinutes(2);

        await using (var setup = new ApplicationDbContext(options))
        {
            var actor = Account.CreateWebsite(actorId, "audit-admin", "AUDIT-ADMIN", insideDayExact);
            actor.SetGlobalRole(GlobalRole.Admin);
            setup.AddRange(actor, hiddenEvent,
                new AuditEntry(exactActionId, insideDayExact, actorId, actor.LoginName, "event.started", "event", hiddenEventId.ToString("D"), "Hidden event started.", hiddenEventId),
                new AuditEntry(areaActionId, insideDayArea, actorId, actor.LoginName, "event.started_automatically", "event", hiddenEventId.ToString("D"), "Hidden event started automatically.", hiddenEventId),
                new AuditEntry(nextCalendarDayId, nextCalendarDay, actorId, actor.LoginName, "event.ended", "event", hiddenEventId.ToString("D"), "Outside the selected local calendar day.", hiddenEventId),
                new AuditEntry(accountActionId, accountAction, actorId, actor.LoginName, "account.changed", "account", actorId.ToString("D"), "An account change.", hiddenEventId),
                new AuditEntry(redactedHiddenActionId, insideDayExact.AddMinutes(3), actorId, actor.LoginName, "account.password_reset", "account", actorId.ToString("D"), "{\"password\":\"hidden-secret\"}", hiddenEventId, "{\"password\":\"hidden-secret\"}", "{\"password\":\"hidden-secret\"}"));
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        // A10 (T1): reference query names (action, type, from, to); rules unchanged.
        var exact = new Bingo.Web.Pages.Admin.Audit.IndexModel(db)
        {
            ActionQuery = "event.started",
            TypeQuery = "event",
            FromQuery = "2027-03-28",
            ToQuery = "2027-03-28"
        };

        await exact.OnGetAsync(CancellationToken.None);

        var exactEntry = Assert.Single(exact.Entries);
        Assert.Equal(exactActionId, exactEntry.Id);
        Assert.Equal(hiddenEventId, exactEntry.EventId);
        Assert.True(await db.Events.Where(item => item.Id == hiddenEventId).Select(item => item.HiddenAt).SingleAsync() is not null);

        var area = new Bingo.Web.Pages.Admin.Audit.IndexModel(db)
        {
            ActionQuery = "event.",
            FromQuery = "2027-03-28",
            ToQuery = "2027-03-28"
        };

        await area.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, area.Entries.Count);
        Assert.Contains(area.Entries, entry => entry.Id == exactActionId);
        Assert.Contains(area.Entries, entry => entry.Id == areaActionId);
        Assert.DoesNotContain(area.Entries, entry => entry.Id == nextCalendarDayId);
        Assert.DoesNotContain(area.Entries, entry => entry.Id == accountActionId);

        var selected = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { EntryQuery = redactedHiddenActionId.ToString() };
        await selected.OnGetAsync(CancellationToken.None);
        Assert.False(selected.EntryUnavailable);
        Assert.Equal(redactedHiddenActionId, selected.SelectedEntry?.Id);
        Assert.Contains(selected.EventOptions, option => option.Id == hiddenEventId && option.IsHidden);
        var presented = AuditPresenter.Present(selected.SelectedEntry!, new AuditPassthroughLocalizer());
        Assert.Contains("Sensitive details withheld", presented.Details);
        Assert.DoesNotContain("hidden-secret", presented.Details);
        Assert.DoesNotContain("hidden-secret", presented.BeforeState);

        var missing = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { EntryQuery = Guid.NewGuid().ToString() };
        await missing.OnGetAsync(CancellationToken.None);
        Assert.True(missing.EntryUnavailable);
        Assert.Null(missing.SelectedEntry);

        var filteredOut = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { EntryQuery = redactedHiddenActionId.ToString(), ActionQuery = "event.started" };
        await filteredOut.OnGetAsync(CancellationToken.None);
        Assert.True(filteredOut.EntryUnavailable);
        Assert.Null(filteredOut.SelectedEntry);

        // C-AUD-4 (08-decisions "Reference sweep" C-AUD-4): an out-of-range date is dropped with a
        // notice and the rest still applies (was: model error and an empty list).
        var invalidDate = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { ToQuery = "9999-12-31", ActionQuery = "event.started" };
        await invalidDate.OnGetAsync(CancellationToken.None);
        Assert.True(invalidDate.DroppedLinkParts);
        Assert.Null(invalidDate.To);
        Assert.Equal(exactActionId, Assert.Single(invalidDate.Entries).Id);
    }

    [Fact]
    public async Task AuditFiltersRespectAutumnDstCalendarBounds()
    {
        var actorId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var eventAt = new DateTimeOffset(2027, 10, 31, 12, 0, 0, TimeSpan.Zero);
        var hiddenEvent = BingoEvent.CreateArchivedHistorical(
            eventId,
            "Autumn audit history",
            "autumn-audit-history",
            null,
            "Europe/Copenhagen",
            null,
            null,
            eventAt,
            eventAt.AddHours(1),
            actorId,
            eventAt.AddDays(-1),
            null,
            1,
            1,
            1,
            1);
        hiddenEvent.Hide(actorId, eventAt.AddDays(1), hiddenEvent.Name, "Controlled autumn fixture");
        var startInside = new DateTimeOffset(2027, 10, 30, 22, 30, 0, TimeSpan.Zero);
        var endInside = new DateTimeOffset(2027, 10, 31, 22, 30, 0, TimeSpan.Zero);
        var beforeStart = new DateTimeOffset(2027, 10, 30, 21, 59, 0, TimeSpan.Zero);
        var atEnd = new DateTimeOffset(2027, 10, 31, 23, 0, 0, TimeSpan.Zero);
        await using (var setup = new ApplicationDbContext(options))
        {
            var actor = Account.CreateWebsite(actorId, "autumn-admin", "AUTUMN-ADMIN", eventAt);
            setup.AddRange(actor, hiddenEvent,
                new AuditEntry(Guid.NewGuid(), startInside, actorId, "autumn-admin", "event.started", "event", eventId.ToString("D"), "Inside lower bound.", eventId),
                new AuditEntry(Guid.NewGuid(), endInside, actorId, "autumn-admin", "event.ended", "event", eventId.ToString("D"), "Inside upper bound.", eventId),
                new AuditEntry(Guid.NewGuid(), beforeStart, actorId, "autumn-admin", "event.cancelled", "event", eventId.ToString("D"), "Before selected local day.", eventId),
                new AuditEntry(Guid.NewGuid(), atEnd, actorId, "autumn-admin", "event.cancelled", "event", eventId.ToString("D"), "At exclusive upper bound.", eventId));
            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var page = new Bingo.Web.Pages.Admin.Audit.IndexModel(db)
        {
            EventQuery = eventId.ToString(),
            FromQuery = "2027-10-31",
            ToQuery = "2027-10-31"
        };
        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, page.Entries.Count);
        Assert.DoesNotContain(page.Entries, entry => entry.OccurredAt == beforeStart);
        Assert.DoesNotContain(page.Entries, entry => entry.OccurredAt == atEnd);
        Assert.Contains(page.Entries, entry => entry.OccurredAt == startInside);
        Assert.Contains(page.Entries, entry => entry.OccurredAt == endInside);
    }

    [Fact]
    public async Task AuditPageRefusesAuthenticatedNonAdmin()
    {
        var createdAt = new DateTimeOffset(2027, 10, 31, 12, 0, 0, TimeSpan.Zero);
        await using (var setup = new ApplicationDbContext(options))
        {
            var user = Account.CreateWebsite(Guid.NewGuid(), "audit-ordinary-user", "AUDIT-ORDINARY-USER", createdAt);
            user.SetPassword(new PasswordHasher<Account>().HashPassword(user, "audit-test-password"), false, createdAt, incrementVersion: false);
            setup.Add(user);
            await setup.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        var loginToken = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "audit-ordinary-user",
            ["Input.Password"] = "audit-test-password",
            ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        using var denied = await client.GetAsync("/Admin/Audit?entry=00000000-0000-0000-0000-000000000001");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditHttpOutOfRangeEndDateReturnsValidationPage()
    {
        var createdAt = new DateTimeOffset(2027, 10, 31, 12, 0, 0, TimeSpan.Zero);
        await using (var setup = new ApplicationDbContext(options))
        {
            var admin = Account.CreateWebsite(Guid.NewGuid(), "audit-date-admin", "AUDIT-DATE-ADMIN", createdAt);
            admin.SetGlobalRole(GlobalRole.Admin);
            admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "audit-test-password"), false, createdAt, incrementVersion: false);
            setup.Add(admin);
            await setup.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        var loginToken = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "audit-date-admin",
            ["Input.Password"] = "audit-test-password",
            ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        // C-AUD-4: invalid link values are dropped with a notice instead of a validation error.
        using var response = await client.GetAsync("/Admin/Audit?to=9999-12-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Some filters in the link weren’t recognised.", html, StringComparison.Ordinal);

        using var invalidFrom = await client.GetAsync("/Admin/Audit?from=abc");
        Assert.Equal(HttpStatusCode.OK, invalidFrom.StatusCode);
        Assert.Contains("Some filters in the link weren’t recognised.", WebUtility.HtmlDecode(await invalidFrom.Content.ReadAsStringAsync()), StringComparison.Ordinal);

        using var invalidEntry = await client.GetAsync("/Admin/Audit?entry=abc");
        Assert.Equal(HttpStatusCode.OK, invalidEntry.StatusCode);
        var invalidEntryHtml = await invalidEntry.Content.ReadAsStringAsync();
        Assert.Contains("data-audit-requested=\"missing\"", invalidEntryHtml, StringComparison.Ordinal);
        Assert.Contains("This entry isn’t available", WebUtility.HtmlDecode(invalidEntryHtml), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditPagingUsesStablePagesAndRejectsNonIntegerPageValues()
    {
        var occurredAt = new DateTimeOffset(2027, 3, 28, 10, 0, 0, TimeSpan.Zero);
        await using (var setup = new ApplicationDbContext(options))
        {
            for (var index = 0; index < 30; index++)
            {
                var id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}");
                setup.AuditEntries.Add(new AuditEntry(id, occurredAt, null, "audit-admin", "account.changed", "account", index.ToString(CultureInfo.InvariantCulture), "Paging fixture."));
            }

            await setup.SaveChangesAsync();
        }

        await using var db = new ApplicationDbContext(options);
        var pageOne = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { ActionQuery = "account.changed", PageQuery = "1" };
        await pageOne.OnGetAsync(CancellationToken.None);
        var pageTwo = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { ActionQuery = "account.changed", PageQuery = "2" };
        await pageTwo.OnGetAsync(CancellationToken.None);

        Assert.Equal(25, pageOne.Entries.Count);
        Assert.True(pageOne.HasNextPage);
        Assert.Equal(5, pageTwo.Entries.Count);
        Assert.False(pageTwo.HasNextPage);
        Assert.Empty(pageOne.Entries.Select(entry => entry.Id).Intersect(pageTwo.Entries.Select(entry => entry.Id)));
        Assert.Equal(30, pageOne.Entries.Concat(pageTwo.Entries).Select(entry => entry.Id).Distinct().Count());
        Assert.Equal("29", pageOne.Entries[0].TargetId);
        Assert.Equal("5", pageOne.Entries[^1].TargetId);
        Assert.Equal("4", pageTwo.Entries[0].TargetId);
        Assert.Equal("0", pageTwo.Entries[^1].TargetId);

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?page=2.5");
        var malformed = new Bingo.Web.Pages.Admin.Audit.IndexModel(db)
        {
            ActionQuery = "account.changed",
            PageQuery = "2.5",
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor()))
        };

        await malformed.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, malformed.PageNumber);
        Assert.Equal(25, malformed.Entries.Count);
    }

    [Fact]
    public async Task AuditAreasActorMatchingAndDroppedLinkPartsFollowTheT1Decisions()
    {
        var actorId = Guid.NewGuid();
        var at = new DateTimeOffset(2027, 5, 2, 10, 0, 0, TimeSpan.Zero);
        var live = new BingoEvent(Guid.NewGuid(), "Live audit event", "live-audit-event", "UTC", actorId, at.AddDays(-30), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var draft = new BingoEvent(Guid.NewGuid(), "Draft audit event", "draft-audit-event", "UTC", actorId, at.AddDays(-30), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var discarded = new BingoEvent(Guid.NewGuid(), "Discarded audit event", "discarded-audit-event", "UTC", actorId, at.AddDays(-30), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        discarded.Discard(actorId, at.AddDays(-1), protectedHistoryExists: false);
        var keys = new[] { "team.member_added", "team.created", "event.signup_opened", "event.started", "participant.admin_created", "roster.finalized_added", "signup_question.created" };
        var ids = keys.ToDictionary(key => key, _ => Guid.NewGuid());
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(live, draft, discarded);
            var index = 0;
            foreach (var key in keys)
                setup.AuditEntries.Add(new AuditEntry(ids[key], at.AddMinutes(index++), actorId, "Mixed_Case-Admin", key, "event", live.Id.ToString("D"), null, key == "event.started" ? draft.Id : live.Id));
            setup.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), at, null, "System", "event.started_automatically", "event", discarded.Id.ToString("D"), null, discarded.Id));
            await setup.SaveChangesAsync();
        }
        await using var db = new ApplicationDbContext(options);
        async Task<HashSet<string>> Area(string token)
        {
            var page = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { ActionQuery = token, FromQuery = "2027-05-02", ToQuery = "2027-05-02" };
            await page.OnGetAsync(CancellationToken.None);
            Assert.False(page.DroppedLinkParts);
            return page.Entries.Select(entry => entry.Action).ToHashSet();
        }
        // S11 / Q5 (a): moved keys belong to exactly one area.
        Assert.Equal(new HashSet<string> { "team.member_added", "participant.admin_created", "roster.finalized_added" }, await Area("participant."));
        Assert.Equal(new HashSet<string> { "team.created" }, await Area("team."));
        Assert.Equal(new HashSet<string> { "event.signup_opened", "signup_question.created" }, await Area("signup."));
        Assert.Equal(new HashSet<string> { "event.started", "event.started_automatically" }, await Area("event."));

        // C-AUD-3: case is ignored and a leading "@" is dropped.
        var actor = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { ActorQuery = " @mixed_case " };
        await actor.OnGetAsync(CancellationToken.None);
        Assert.Equal(7, actor.Entries.Count);
        Assert.Equal("mixed_case", actor.Actor);
        var wildcard = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { ActorQuery = "mixed%case" };
        await wildcard.OnGetAsync(CancellationToken.None);
        Assert.Empty(wildcard.Entries);

        // C-AUD-4: invalid link parts are dropped and the rest still applies.
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?event={Guid.NewGuid()}&action=team.&type=spaceship&from=2027-13-40&page=0&EventId=x");
        var dropped = new Bingo.Web.Pages.Admin.Audit.IndexModel(db)
        {
            EventQuery = Guid.NewGuid().ToString(), ActionQuery = "team.", TypeQuery = "spaceship", FromQuery = "2027-13-40", PageQuery = "0",
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor()))
        };
        await dropped.OnGetAsync(CancellationToken.None);
        Assert.True(dropped.DroppedLinkParts);
        Assert.Null(dropped.EventId);
        Assert.Equal(string.Empty, dropped.Type);
        Assert.Null(dropped.From);
        Assert.Equal(1, dropped.PageNumber);
        Assert.Equal("team.created", Assert.Single(dropped.Entries).Action);
        Assert.Equal("/Admin/Audit?action=team.", dropped.AuditUrl());
        var reversed = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { FromQuery = "2027-05-03", ToQuery = "2027-05-01" };
        await reversed.OnGetAsync(CancellationToken.None);
        Assert.True(reversed.DroppedLinkParts);
        Assert.Equal(new DateOnly(2027, 5, 3), reversed.From);
        Assert.Null(reversed.To);

        // Event menu (Q7, AU16): ordered as on Events, with state; Discarded omitted, its entries kept.
        var all = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { EventQuery = discarded.Id.ToString() };
        await all.OnGetAsync(CancellationToken.None);
        Assert.DoesNotContain(all.EventOptions, option => option.Id == discarded.Id);
        Assert.Contains(all.EventOptions, option => option.Id == draft.Id && option.State == EventState.Draft);
        Assert.Equal("event.started_automatically", Assert.Single(all.Entries).Action);
        Assert.Null(Assert.Single(all.Entries).ActorAccountId);
    }

    private sealed class AuditPassthroughLocalizer : IStringLocalizer<AuditResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
