using System.Globalization;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
                new AuditEntry(accountActionId, accountAction, actorId, actor.LoginName, "account.changed", "account", actorId.ToString("D"), "An account change.", hiddenEventId));
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
}
