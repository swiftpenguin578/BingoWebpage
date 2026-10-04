using System.Net;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Data.Common;
using Bingo.Domain.Auditing;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class DraftOperationsIntegrationTests
{
    [Fact]
    public async Task B5RemediationDisabledSessionTeamsReadbackReturnsLoginWithoutData()
    {
        var setup = await SeedAsync();
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var path = $"/Admin/Events/Draft/{setup.EventId}?handler=Readback";
        using var available = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);
        Assert.NotEmpty(await available.Content.ReadAsStringAsync());
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.Accounts.SingleAsync(x => x.Id == setup.FirstAdminId)).Disable(now);
            await db.SaveChangesAsync();
        }
        using var refused = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        var location = new Uri(client.BaseAddress!, refused.Headers.Location!);
        Assert.Equal("/Account/Login", location.AbsolutePath);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("true", query["accessChanged"].ToString());
        Assert.Equal(path, query["ReturnUrl"].ToString());
        Assert.Equal(2, query.Count);
        Assert.Empty(await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task B5RemediationDraftReadbackSeparatesOldWomSuccessFromNewRosterPublication()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, p => p.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, p => p.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, p => p.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        var createdAt = now.AddSeconds(1);
        var succeededAt = now.AddSeconds(2);
        var republishedAt = now.AddSeconds(3);
        Guid operationId;
        Guid publicationId;
        await using (var db = new ApplicationDbContext(options))
        {
            var prior = await db.DraftPublicationCycles.SingleAsync(x => x.SupersededAt == null);
            prior.Supersede(republishedAt, setup.FirstAdminId, "Controlled local republish");
            var replacement = new DraftPublicationCycle(Guid.NewGuid(), prior.DraftSessionId, prior.CycleNumber + 1, republishedAt, setup.FirstAdminId, prior.PublicationMethod);
            publicationId = replacement.Id;
            db.DraftPublicationCycles.Add(replacement);
            var roster = await db.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == prior.Id).ToListAsync();
            db.DraftPublicationRosters.AddRange(roster.Select(x => new DraftPublicationRoster(Guid.NewGuid(), replacement.Id, x.TeamId, x.EventParticipantId, x.Role, x.EffectivePickNumber, x.PublicCharacterName)));
            var sync = new EventCompetitionSynchronization(Guid.NewGuid(), setup.EventId, 1, 123, "Controlled", now, now.AddDays(1), "fixture", now);
            var management = new EventCompetitionManagement(Guid.NewGuid(), setup.EventId, sync.Id, 123, "Controlled", now, now.AddDays(1), "fixture-only", "fixture", now);
            var operation = new EventCompetitionManagementOperation(Guid.NewGuid(), setup.EventId, management.Id, EventCompetitionManagementOperationType.Update, "{}", "fixture", 1, createdAt);
            operationId = operation.Id;
            operation.Succeed(123, "fixture", succeededAt);
            management.MarkApplied(operation.Id, "old-roster", "old-roster", "[]", "Controlled", now, now.AddDays(1), succeededAt);
            db.AddRange(sync, management, operation);
            await db.SaveChangesAsync();
        }
        var baseline = await RosterStateAsync();
        var read = await B5DraftReadAsync(setup);
        Assert.Equal(publicationId, read.RosterPublicationId);
        Assert.Equal(2, read.RosterPublicationCycle);
        Assert.Equal(republishedAt, read.RosterPublishedAt);
        var operationRead = read.Synchronization.LastOperation!;
        Assert.Equal(operationId, operationRead.OperationId);
        Assert.Equal(EventCompetitionManagementOperationPhase.Succeeded, operationRead.Phase);
        Assert.Equal(createdAt, operationRead.CreatedAt);
        Assert.Equal(succeededAt, operationRead.UpdatedAt);
        Assert.True(operationRead.CreatedAt < read.RosterPublishedAt);
        Assert.Equal("Active", read.Synchronization.ManagementStatus);
        Assert.Null(read.Synchronization.LastLocalQueueStatus);
        Assert.Equal(baseline, await RosterStateAsync());
        Assert.DoesNotContain("fixture-only", JsonSerializer.Serialize(read), StringComparison.Ordinal);
    }
    [Theory]
    [InlineData("accounts")]
    [InlineData("audit_entries")]
    public async Task B5RemediationDraftReadFailuresIncludeAuthorizationAndLocalAudit(string table)
    {
        var setup = await SeedAsync();
        var baseline = await RosterStateAsync();
        var failing = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new B5AnyDraftReadFailure(table)).Options;
        await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, async page =>
        {
            var result = await page.OnGetReadbackAsync(setup.EventId, CancellationToken.None);
            var read = Assert.IsType<DraftModel.DraftReadback>(Assert.IsType<JsonResult>(result).Value);
            Assert.False(read.Known); Assert.Null(read.State);
            return result;
        }, failing);
        Assert.Equal(baseline, await RosterStateAsync());
    }

    [Theory]
    [InlineData("{ invalid")]
    [InlineData("[]")]
    public async Task B5RemediationMalformedWomAuditDoesNotHideDraftState(string malformed)
    {
        var setup = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, setup.FirstAdminId, "admin", "roster.finalized_added.wom_sync", "membership", Guid.NewGuid().ToString(), null, setup.EventId, null, malformed));
            await db.SaveChangesAsync();
        }
        var before = await RosterStateAsync();
        var read = await B5DraftReadAsync(setup);
        Assert.Equal(setup.EventId, read.EventId); Assert.Equal(2, read.Teams.Count);
        Assert.Null(read.Synchronization.LastLocalQueueStatus);
        Assert.Equal(now, read.Synchronization.LastLocalQueueAt);
        Assert.Equal(before, await RosterStateAsync());
    }

    private sealed class B5AnyDraftReadFailure(string table) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM " + table, StringComparison.Ordinal)) throw new TimeoutException("Controlled read failure");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

}
