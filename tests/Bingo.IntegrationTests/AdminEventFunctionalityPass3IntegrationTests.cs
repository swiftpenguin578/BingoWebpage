using System.Security.Claims;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AdminEventFunctionalityPass3IntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_admin_event_functionality_pass3")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
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
    public async Task ManageMutationsRollBackWithAuditFailureAndSuccessfulActionsCreateOneAuditEach()
    {
        var now = new FixedTimeProvider(DateTimeOffset.UtcNow);
        var admin = Bingo.Domain.Access.Account.CreateWebsite(Guid.NewGuid(), "pass3-admin", "PASS3-ADMIN", now.GetUtcNow());
        admin.SetGlobalRole(Bingo.Domain.Access.GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "Pass 3 event", $"pass3-{Guid.NewGuid():N}", "", "UTC",
            now.GetUtcNow().AddHours(-4), now.GetUtcNow().AddHours(-3), now.GetUtcNow().AddHours(-2), now.GetUtcNow().AddHours(2), now.GetUtcNow().AddHours(2), 20, admin.Id, now.GetUtcNow());
        item.OpenSignups(now.GetUtcNow().AddHours(-4));
        item.CloseSignups(now.GetUtcNow().AddHours(-3));
        item.StartEvent(now.GetUtcNow().AddHours(-2));
        item.SetEvidenceCodeEnabled(true, now.GetUtcNow());
        var originalActivation = now.GetUtcNow().AddMinutes(-10);
        var originalCode = new EvidenceCode(Guid.NewGuid(), item.Id, "ORIGINAL", originalActivation, admin.Id, originalActivation, null);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, item, originalCode);
            await setup.SaveChangesAsync();
        }

        long version;
        await using (var read = new ApplicationDbContext(options))
            version = await read.Events.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();

        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnAuditInsert()).Options;
        await using (var failingDb = new ApplicationDbContext(failingOptions))
        {
            var page = Manage(failingDb, now, admin);
            page.EventVersion = version;
            page.ReopenUntil = now.GetUtcNow().AddHours(1);
            page.StateReason = "Audit rollback for reopening.";
            Assert.IsType<RedirectToPageResult>(await page.OnPostReopenSubmissionsAsync(item.Id, CancellationToken.None));
        }
        await using (var failingDb = new ApplicationDbContext(failingOptions))
        {
            var page = Manage(failingDb, now, admin);
            page.EventVersion = version;
            Assert.IsType<RedirectToPageResult>(await page.OnPostDisableEvidenceCodesAsync(item.Id, CancellationToken.None));
        }
        await using (var failingDb = new ApplicationDbContext(failingOptions))
        {
            var page = Manage(failingDb, now, admin);
            page.EventVersion = version;
            page.NewEvidenceCode = "FAILED";
            page.EvidenceCodeActivatesAt = now.GetUtcNow().AddMinutes(10);
            Assert.IsType<RedirectToPageResult>(await page.OnPostCreateEvidenceCodeAsync(item.Id, CancellationToken.None));
        }

        await using (var afterFailures = new ApplicationDbContext(options))
        {
            var persisted = await afterFailures.Events.AsNoTracking().SingleAsync(x => x.Id == item.Id);
            var code = await afterFailures.EvidenceCodes.AsNoTracking().SingleAsync(x => x.Id == originalCode.Id);
            Assert.Equal(EventState.Live, persisted.State);
            Assert.True(persisted.EvidenceCodeEnabled);
            Assert.Null(persisted.ReopenedSubmissionCutoffAt);
            Assert.Equal(version, persisted.Version);
            Assert.Null(code.RetiresAt);
            Assert.Single(await afterFailures.EvidenceCodes.Where(x => x.EventId == item.Id).ToListAsync());
            Assert.Empty(await afterFailures.AuditEntries.Where(x => x.TargetId == item.Id.ToString()).ToListAsync());
        }

        await using (var createDb = new ApplicationDbContext(options))
        {
            var page = Manage(createDb, now, admin);
            page.EventVersion = version;
            page.NewEvidenceCode = "NEXTCODE";
            page.EvidenceCodeActivatesAt = now.GetUtcNow().AddMinutes(10);
            Assert.IsType<RedirectToPageResult>(await page.OnPostCreateEvidenceCodeAsync(item.Id, CancellationToken.None));
        }

        await using (var staleDb = new ApplicationDbContext(options))
        {
            var page = Manage(staleDb, now, admin);
            page.EventVersion = version;
            Assert.IsType<RedirectToPageResult>(await page.OnPostDisableEvidenceCodesAsync(item.Id, CancellationToken.None));
        }

        long afterCreateVersion;
        await using (var afterCreate = new ApplicationDbContext(options))
        {
            afterCreateVersion = await afterCreate.Events.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            Assert.True(await afterCreate.Events.Where(x => x.Id == item.Id).Select(x => x.EvidenceCodeEnabled).SingleAsync());
            var codes = await afterCreate.EvidenceCodes.Where(x => x.EventId == item.Id).OrderBy(x => x.ActivatesAt).ToListAsync();
            Assert.Equal(2, codes.Count);
            Assert.Equal(now.GetUtcNow().AddMinutes(10), codes[0].RetiresAt);
            Assert.Null(codes[1].RetiresAt);
            Assert.Equal(1, await afterCreate.AuditEntries.CountAsync(x => x.TargetId == item.Id.ToString() && x.Action == "evidence_code.created"));
        }

        await using (var disableDb = new ApplicationDbContext(options))
        {
            var page = Manage(disableDb, now, admin);
            page.EventVersion = afterCreateVersion;
            Assert.IsType<RedirectToPageResult>(await page.OnPostDisableEvidenceCodesAsync(item.Id, CancellationToken.None));
        }

        long afterDisableVersion;
        await using (var afterDisable = new ApplicationDbContext(options))
            afterDisableVersion = await afterDisable.Events.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();

        await using (var reopenDb = new ApplicationDbContext(options))
        {
            var page = Manage(reopenDb, now, admin);
            page.EventVersion = afterDisableVersion;
            page.ReopenUntil = now.GetUtcNow().AddHours(3);
            page.StateReason = "Audit-backed reopening.";
            Assert.IsType<RedirectToPageResult>(await page.OnPostReopenSubmissionsAsync(item.Id, CancellationToken.None));
        }

        await using var verify = new ApplicationDbContext(options);
        var final = await verify.Events.AsNoTracking().SingleAsync(x => x.Id == item.Id);
        Assert.False(final.EvidenceCodeEnabled);
        Assert.Equal(now.GetUtcNow().AddHours(3), final.ReopenedSubmissionCutoffAt);
        Assert.Equal(1, await verify.AuditEntries.CountAsync(x => x.TargetId == item.Id.ToString() && x.Action == "evidence_code.created"));
        Assert.Equal(1, await verify.AuditEntries.CountAsync(x => x.TargetId == item.Id.ToString() && x.Action == "event.evidence_code_mode"));
        Assert.Equal(1, await verify.AuditEntries.CountAsync(x => x.TargetId == item.Id.ToString() && x.Action == "event.submissions_reopened"));
        var successfulAudits = await verify.AuditEntries.Where(x => x.TargetId == item.Id.ToString()).ToListAsync();
        Assert.Equal(3, successfulAudits.Count);
        Assert.All(successfulAudits, audit => Assert.Equal(item.Id, audit.EventId));
    }

    private static ManageModel Manage(ApplicationDbContext db, TimeProvider clock, Bingo.Domain.Access.Account admin)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Name, admin.LoginName)
            ], "test"))
        };
        return new ManageModel(db, null!, null!, new AuditWriter(db, clock), null!, null!, null!, null!, clock)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new EmptyTempDataProvider())
        };
    }

    private sealed class ThrowOnAuditInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated audit persistence failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
