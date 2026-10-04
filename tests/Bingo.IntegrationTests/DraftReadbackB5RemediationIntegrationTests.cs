using System.Text.Json;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class DraftOperationsIntegrationTests
{
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
}
