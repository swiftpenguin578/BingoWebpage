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

public sealed class AuditHistoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_audit_history")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
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
        var exact = new Bingo.Web.Pages.Admin.Audit.IndexModel(db)
        {
            Action = "event.started",
            Entity = "event",
            From = new DateOnly(2027, 3, 28),
            To = new DateOnly(2027, 3, 28)
        };

        await exact.OnGetAsync(CancellationToken.None);

        var exactEntry = Assert.Single(exact.Entries);
        Assert.Equal(exactActionId, exactEntry.Id);
        Assert.Equal(hiddenEventId, exactEntry.EventId);
        Assert.True(await db.Events.Where(item => item.Id == hiddenEventId).Select(item => item.HiddenAt).SingleAsync() is not null);

        var area = new Bingo.Web.Pages.Admin.Audit.IndexModel(db)
        {
            Action = "event",
            From = new DateOnly(2027, 3, 28),
            To = new DateOnly(2027, 3, 28)
        };

        await area.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, area.Entries.Count);
        Assert.Contains(area.Entries, entry => entry.Id == exactActionId);
        Assert.Contains(area.Entries, entry => entry.Id == areaActionId);
        Assert.DoesNotContain(area.Entries, entry => entry.Id == nextCalendarDayId);
        Assert.DoesNotContain(area.Entries, entry => entry.Id == accountActionId);

        var selected = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { EntryId = redactedHiddenActionId };
        await selected.OnGetAsync(CancellationToken.None);
        Assert.False(selected.EntryUnavailable);
        Assert.Equal(redactedHiddenActionId, selected.SelectedEntry?.Id);
        Assert.Contains(selected.EventOptions, option => option.Id == hiddenEventId && option.IsHidden);
        var presented = AuditPresenter.Present(selected.SelectedEntry!, new AuditPassthroughLocalizer());
        Assert.Contains("Sensitive details withheld", presented.Details);
        Assert.DoesNotContain("hidden-secret", presented.Details);
        Assert.DoesNotContain("hidden-secret", presented.BeforeState);

        var missing = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { EntryId = Guid.NewGuid() };
        await missing.OnGetAsync(CancellationToken.None);
        Assert.True(missing.EntryUnavailable);
        Assert.Null(missing.SelectedEntry);

        var filteredOut = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { EntryId = redactedHiddenActionId, Action = "event.started" };
        await filteredOut.OnGetAsync(CancellationToken.None);
        Assert.True(filteredOut.EntryUnavailable);
        Assert.Null(filteredOut.SelectedEntry);

        var invalidDate = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { To = DateOnly.MaxValue };
        await invalidDate.OnGetAsync(CancellationToken.None);
        Assert.False(invalidDate.ModelState.IsValid);
        Assert.Contains(invalidDate.ModelState[nameof(invalidDate.To)]!.Errors, error => error.ErrorMessage?.Contains("out of range", StringComparison.OrdinalIgnoreCase) == true);
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
            EventId = eventId,
            From = new DateOnly(2027, 10, 31),
            To = new DateOnly(2027, 10, 31)
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

        using var response = await client.GetAsync("/Admin/Audit?To=9999-12-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The end date is out of range.", html, StringComparison.Ordinal);

        using var invalidFrom = await client.GetAsync("/Admin/Audit?From=abc");
        Assert.Equal(HttpStatusCode.OK, invalidFrom.StatusCode);
        var invalidFromHtml = await invalidFrom.Content.ReadAsStringAsync();
        Assert.Contains("data-valmsg-for=\"From\"", invalidFromHtml, StringComparison.Ordinal);
        Assert.Contains("field-validation-error", invalidFromHtml, StringComparison.Ordinal);

        using var invalidEntry = await client.GetAsync("/Admin/Audit?entry=abc");
        Assert.Equal(HttpStatusCode.OK, invalidEntry.StatusCode);
        var invalidEntryHtml = await invalidEntry.Content.ReadAsStringAsync();
        Assert.Contains("This entry isn't available", WebUtility.HtmlDecode(invalidEntryHtml), StringComparison.Ordinal);
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
        var pageOne = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { Action = "account.changed", PageNumber = 1 };
        await pageOne.OnGetAsync(CancellationToken.None);
        var pageTwo = new Bingo.Web.Pages.Admin.Audit.IndexModel(db) { Action = "account.changed", PageNumber = 2 };
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
        context.Request.QueryString = new QueryString("?pageNumber=2.5");
        var malformed = new Bingo.Web.Pages.Admin.Audit.IndexModel(db)
        {
            Action = "account.changed",
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor()))
        };

        await malformed.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, malformed.PageNumber);
        Assert.Equal(25, malformed.Entries.Count);
    }

    private sealed class AuditPassthroughLocalizer : IStringLocalizer<AuditResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
