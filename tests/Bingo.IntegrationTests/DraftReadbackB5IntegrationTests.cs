using System.Data.Common;
using System.Text.Json;
using Bingo.Domain.Auditing;
using Bingo.Domain.Access;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bingo.IntegrationTests;

public sealed partial class DraftOperationsIntegrationTests
{
    [Fact]
    public async Task B5DraftReadbackDistinguishesReusedPickNumberAndMembership()
    {
        var setup = await SeedAsync(); await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, p => p.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        var before = await B5DraftReadAsync(setup); var old = Assert.Single(before.Picks);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, p => p.OnPostUndoAsync(setup.EventId, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, p => p.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        var after = await B5DraftReadAsync(setup); var current = Assert.Single(after.Picks, x => x.UndoneAt == null);
        Assert.Equal(old.PickNumber, current.PickNumber); Assert.NotEqual(old.PickId, current.PickId);
        Assert.NotNull(Assert.Single(after.Picks, x => x.PickId == old.PickId).UndoneAt);
        Assert.Contains(after.Memberships, x => x.PickId == old.PickId && x.LeftAt != null);
        Assert.Contains(after.Memberships, x => x.PickId == current.PickId && x.LeftAt == null && x.TeamId == current.TeamId);
        // Session version can remain unchanged when lease renewal has the same
        // timestamp; immutable pick and membership identities still distinguish it.
        Assert.Equal(before.Version, after.Version);
        var state = await RosterStateAsync(); await B5DraftReadAsync(setup); Assert.Equal(state, await RosterStateAsync());
    }

    [Fact]
    public async Task B5DraftReadbackKeepsCompetingAdminsActualTeamIdentity()
    {
        var setup = await SeedAsync(); await StartAndScrambleAsync(setup);
        var baseline = await B5DraftReadAsync(setup); var intendedTeam = baseline.CurrentTurn!.TeamId;
        await ExecuteAsync(setup.EventId, setup.SecondAdminId, p => p.OnPostTakeControlAsync(setup.EventId, true, CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.SecondAdminId, p => p.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.SecondAdminId, p => p.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        var read = await B5DraftReadAsync(setup); var actual = Assert.Single(read.Picks, x => x.ParticipantId == setup.PlayerIds[2] && x.UndoneAt == null);
        Assert.NotEqual(intendedTeam, actual.TeamId); Assert.Equal(setup.SecondAdminId, read.ControllerId);
        Assert.Contains(read.Memberships, x => x.PickId == actual.PickId && x.TeamId == actual.TeamId && x.LeftAt == null);
    }

    [Fact]
    public async Task B5DraftReadbackDoesNotInferRedrawFromChangedOrder()
    {
        var setup = await SeedAsync(); await StartAndScrambleAsync(setup);
        var before = await B5DraftReadAsync(setup);
        // Controlled valid random outcome: the redraw keeps exactly the same order.
        // Persist the existing command's domain operations; no RNG/algorithm change.
        await using (var db = new ApplicationDbContext(options))
        {
            foreach (var team in await db.Teams.Where(x => x.EventId == setup.EventId).ToListAsync())
                team.SetDraftPosition(before.Teams.Single(x => x.TeamId == team.Id).DraftPosition);
            (await db.DraftSessions.SingleAsync()).RenewControl(setup.FirstAdminId, now.AddTicks(13), TimeSpan.FromMinutes(5));
            await db.SaveChangesAsync();
        }
        var after = await B5DraftReadAsync(setup);
        Assert.Equal(before.Teams.Select(x => (x.TeamId, x.DraftPosition)), after.Teams.Select(x => (x.TeamId, x.DraftPosition)));
        Assert.True(after.Version > before.Version);
        await using var persisted = new ApplicationDbContext(options);
        Assert.Equal((await persisted.DraftSessions.AsNoTracking().SingleAsync()).ControllerLeaseExpiresAt, after.ControlExpiresAt);
        Assert.Equal(0, after.ControlExpiresAt!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [Fact]
    public async Task B5DraftReadbackIncludesInclusionOnlyTeamEditsAndConfirmedCaptainEligibility()
    {
        var setup = await SeedAsync(); var before = await B5DraftReadAsync(setup); var team = before.Teams[0];
        await ExecuteAsync(setup.EventId, setup.SecondAdminId, p => p.OnPostUpdateTeamAsync(setup.EventId, team.TeamId, team.Name, team.Affiliation, null, false, team.Version, CancellationToken.None, includedInDraft: false));
        var after = await B5DraftReadAsync(setup); var updated = Assert.Single(after.Teams, x => x.TeamId == team.TeamId);
        Assert.Equal(team.Name, updated.Name); Assert.False(updated.IncludedInDraft); Assert.True(updated.Version > team.Version);
        await using (var db = new ApplicationDbContext(options))
        {
            var captainId = await db.TeamMemberships.Where(x => x.TeamId == team.TeamId && x.Role == TeamMembershipRole.Captain).Select(x => x.EventParticipantId).SingleAsync();
            db.Entry(await db.EventParticipants.SingleAsync(x => x.Id == captainId)).Property(x => x.SignupStatus).CurrentValue = SignupStatus.WaitingList;
            await db.SaveChangesAsync();
        }
        Assert.DoesNotContain(team.TeamId, (await B5DraftReadAsync(setup)).UsableCaptainTeamIds);
        await using (var elevate = new ApplicationDbContext(options))
        {
            (await elevate.Accounts.SingleAsync(x => x.Id == setup.FirstAdminId)).SetGlobalRole(GlobalRole.SuperAdmin); await elevate.SaveChangesAsync();
        }
        Assert.True((await B5DraftReadAsync(setup)).Teams.Count > 0);
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.Accounts.SingleAsync(x => x.Id == setup.FirstAdminId)).Disable(now); await db.SaveChangesAsync();
        }
        Assert.IsType<ForbidResult>(await ExecuteAsync(setup.EventId, setup.FirstAdminId, p => p.OnGetReadbackAsync(setup.EventId, CancellationToken.None)));
    }

    [Fact]
    public async Task B5DraftFailedReadbackIsUnknownAndNeverWrites()
    {
        var setup = await SeedAsync(); var baseline = await RosterStateAsync();
        var failing = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).AddInterceptors(new B5DraftReadFailure()).Options;
        DraftModel.DraftReadback? result = null;
        await ExecuteAndReadStatusAsync(setup.EventId, setup.FirstAdminId, async p =>
        {
            var response = await p.OnGetReadbackAsync(setup.EventId, CancellationToken.None);
            result = Assert.IsType<DraftModel.DraftReadback>(Assert.IsType<JsonResult>(response).Value); return response;
        }, failing);
        Assert.False(result!.Known); Assert.Null(result.State); Assert.Equal(baseline, await RosterStateAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B5DraftReadbackExposesPendingAndFailedWomWithoutClaimingCompletion(bool failed)
    {
        var setup = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var sync = new EventCompetitionSynchronization(Guid.NewGuid(), setup.EventId, 1, 123, "Controlled", now, now.AddDays(1), "fixture", now);
            var management = new EventCompetitionManagement(Guid.NewGuid(), setup.EventId, sync.Id, 123, "Controlled", now, now.AddDays(1), "controlled-fixture-only", "fixture", now);
            var operation = new EventCompetitionManagementOperation(Guid.NewGuid(), setup.EventId, management.Id, EventCompetitionManagementOperationType.Update, "{}", "fixture", 1, now);
            management.MarkPending(operation.Id, now);
            if (failed) { operation.Fail("FixtureFailure", "Controlled failure", now); management.MarkFailure(operation.Id, EventCompetitionManagementStatus.Failed, "FixtureFailure", "Controlled failure", now); }
            db.AddRange(sync, management, operation);
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, setup.FirstAdminId, "admin", "roster.finalized_added.wom_sync", "membership", Guid.NewGuid().ToString(), null, setup.EventId, null, JsonSerializer.Serialize(new { localCommitted = true, womStatus = failed ? "Failed" : "Pending" })));
            await db.SaveChangesAsync();
        }
        var read = await B5DraftReadAsync(setup);
        Assert.Equal(failed ? "Failed" : "Pending", read.Synchronization.ManagementStatus);
        Assert.Equal(failed ? EventCompetitionManagementOperationPhase.Failed : EventCompetitionManagementOperationPhase.Pending, read.Synchronization.LastOperation!.Phase);
        Assert.Equal(failed ? "Failed" : "Pending", read.Synchronization.LastLocalQueueStatus);
        Assert.DoesNotContain("controlled-fixture-only", JsonSerializer.Serialize(read), StringComparison.Ordinal);
    }

    private async Task<DraftModel.DraftCurrentState> B5DraftReadAsync(Setup setup)
    {
        var result = Assert.IsType<DraftModel.DraftReadback>(Assert.IsType<JsonResult>(await ExecuteAsync(setup.EventId, setup.FirstAdminId,
            p => p.OnGetReadbackAsync(setup.EventId, CancellationToken.None))).Value);
        Assert.True(result.Known); return result.State!;
    }
    private sealed class B5DraftReadFailure : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM draft_sessions", StringComparison.Ordinal)) throw new TimeoutException("Controlled read failure");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
