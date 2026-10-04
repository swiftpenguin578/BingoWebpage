using System.Net;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.IntegrationTests;

public sealed partial class EventQuarantineIntegrationTests
{
    [Theory]
    [InlineData(EventState.AwaitingFinalReview, EventState.Live)]
    [InlineData(EventState.Finalized, EventState.AwaitingFinalReview)]
    [InlineData(EventState.AwaitingFinalReview, EventState.Finalized)]
    public async Task RestoreRejectsAnotherCurrentEventWithoutChangingEitherEvent(EventState hiddenState, EventState currentState)
    {
        var (admin, hidden, other) = await ExclusivityFixtureAsync(hiddenState, resume: false);
        await using (var startDb = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(startDb, null!, new FixedClock(now));
            var started = await lifecycle.StartNowAsync(other.Id, other.Version, true, null, new(admin.Id, admin.LoginName));
            Assert.True(started.Succeeded, started.Error);
            if (currentState != EventState.Live)
            {
                var item = await startDb.Events.SingleAsync(x => x.Id == other.Id);
                item.EndEvent(now);
                if (currentState == EventState.Finalized) item.FinalizeResults(now);
                await startDb.SaveChangesAsync();
            }
        }
        var before = await EventSnapshotsAsync();
        await using (var restoreDb = new ApplicationDbContext(options))
        {
            var notifier = new RecordingAdminCollaborationNotifier();
            var result = await new EventQuarantineService(restoreDb, new FixedClock(now), notifier)
                .RestoreAsync(hidden.Id, hidden.Version, hidden.Name, null, new(admin.Id, admin.LoginName));
            Assert.False(result.Succeeded);
            Assert.Equal(EventQuarantineOutcome.InvalidState, result.Outcome);
            Assert.Contains("Archive it before restoring", result.Error);
            Assert.Equal(0, notifier.EventsControlChanges);
        }
        Assert.Equal(before, await EventSnapshotsAsync());
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.AuditEntries.Where(x => x.Action == "event.restored").ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "event.hidden").ToListAsync());
        Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == other.Id).ToListAsync());
    }

    [Fact]
    public async Task ArchivedRestoreDoesNotClaimCurrentSlot()
    {
        var (admin, hidden, other) = await ExclusivityFixtureAsync(EventState.Archived, resume: true);
        await using var db = new ApplicationDbContext(options);
        var result = await new EventQuarantineService(db, new FixedClock(now)).RestoreAsync(
            hidden.Id, hidden.Version, hidden.Name, null, new(admin.Id, admin.LoginName));
        Assert.True(result.Succeeded, result.Error);
        var restored = await db.Events.AsNoTracking().SingleAsync(x => x.Id == hidden.Id);
        Assert.Equal(EventState.Archived, restored.State);
        Assert.Null(restored.HiddenAt);
        Assert.Null(restored.HiddenByAccountId);
        Assert.Null(restored.HiddenReason);
        Assert.Equal(hidden.ActualEndedAt, restored.ActualEndedAt);
        Assert.Equal(hidden.FinalizedAt, restored.FinalizedAt);
        Assert.Equal(hidden.ArchivedAt, restored.ArchivedAt);
        Assert.Equal(EventState.AwaitingFinalReview, (await db.Events.AsNoTracking().SingleAsync(x => x.Id == other.Id)).State);
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "event.restored").ToListAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RestoreAndStartOrResumeShareCurrentLock(bool resume, bool restoreFirst)
    {
        var (admin, hidden, other) = await ExclusivityFixtureAsync(EventState.AwaitingFinalReview, resume);
        var hiddenBefore = JsonSerializer.Serialize(hidden);
        await using var blocker = new ApplicationDbContext(options);
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)");
        await using var restoreDb = new ApplicationDbContext(options);
        await using var lifecycleDb = new ApplicationDbContext(options);
        await restoreDb.Database.OpenConnectionAsync();
        await lifecycleDb.Database.OpenConnectionAsync();
        var actor = new LifecycleActor(admin.Id, admin.LoginName);
        Task<EventQuarantineResult> Restore() => new EventQuarantineService(restoreDb, new FixedClock(now))
            .RestoreAsync(hidden.Id, hidden.Version, hidden.Name, null, actor);
        Task<EventStartResult> Transition() => resume
            ? new EventLifecycleService(lifecycleDb, null!, new FixedClock(now)).ResumePrematureEndAsync(other.Id, other.Version, true, "Controlled resume", null, actor)
            : new EventLifecycleService(lifecycleDb, null!, new FixedClock(now)).StartNowAsync(other.Id, other.Version, true, null, actor);
        Task<EventQuarantineResult> restore;
        Task<EventStartResult> transition;
        if (restoreFirst)
        {
            restore = Restore();
            await AssertWaitingForCurrentLockAsync(restoreDb);
            transition = Transition();
            await AssertWaitingForCurrentLockAsync(lifecycleDb);
        }
        else
        {
            transition = Transition();
            await AssertWaitingForCurrentLockAsync(lifecycleDb);
            restore = Restore();
            await AssertWaitingForCurrentLockAsync(restoreDb);
        }
        await transaction.CommitAsync();
        var restored = await restore;
        var transitioned = await transition;
        Assert.True(restored.Succeeded || transitioned.Succeeded, $"Restore: {restored.Error}; transition: {transitioned.Error}");
        Assert.False(restored.Succeeded && transitioned.Succeeded);
        if (resume)
        {
            Assert.False(restored.Succeeded);
            Assert.True(transitioned.Succeeded, transitioned.Error);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(1, await verify.Events.CountAsync(x => x.HiddenAt == null &&
            (x.State == EventState.Live || x.State == EventState.AwaitingFinalReview || x.State == EventState.Finalized)));
        Assert.Equal(restored.Succeeded ? 1 : 0, await verify.AuditEntries.CountAsync(x => x.Action == "event.restored"));
        Assert.Equal(transitioned.Succeeded ? 1 : 0, await verify.EventStateTransitions.CountAsync(x => x.EventId == other.Id));
        if (!restored.Succeeded)
            Assert.Equal(hiddenBefore, JsonSerializer.Serialize(await verify.Events.AsNoTracking().SingleAsync(x => x.Id == hidden.Id)));
    }

    [Fact]
    public async Task RestoreConflictReturnsToHiddenManageAndCanRecoverAfterOtherEventIsArchived()
    {
        var (admin, hidden, other) = await ExclusivityFixtureAsync(EventState.Finalized, resume: true);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.PublicUsername!, "quarantine-exclusivity-password");
        var path = $"/Admin/Events/Manage/{hidden.Id}?hidden=true";
        async Task<HttpResponseMessage> RestoreFromPageAsync(string page) => await client.PostAsync($"{path}&handler=RestoreHidden", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = InputValue(page, "EventVersion"),
            ["ConfirmDestructiveAction"] = "true",
            ["EventNameConfirmation"] = hidden.Name,
            ["__RequestVerificationToken"] = AntiforgeryToken(page)
        }));
        var before = await EventSnapshotsAsync();
        using (var rejected = await RestoreFromPageAsync(await client.GetStringAsync(path)))
        {
            Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
            Assert.Equal($"/Admin/Events/Manage/{hidden.Id}?hidden=True", rejected.Headers.Location!.OriginalString);
        }
        var recoveryPage = await client.GetStringAsync(path);
        Assert.Contains("Archive it before restoring this event.", recoveryPage);
        Assert.Contains("handler=RestoreHidden", recoveryPage);
        Assert.Equal(before, await EventSnapshotsAsync());
        await using (var archiveDb = new ApplicationDbContext(options))
        {
            var item = await archiveDb.Events.SingleAsync(x => x.Id == other.Id);
            item.FinalizeResults(now);
            item.Archive(now);
            await archiveDb.SaveChangesAsync();
        }
        using var recovered = await RestoreFromPageAsync(await client.GetStringAsync(path));
        Assert.Equal(HttpStatusCode.Redirect, recovered.StatusCode);
        Assert.Equal("/Admin/Events", recovered.Headers.Location!.OriginalString);
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await verify.Events.SingleAsync(x => x.Id == hidden.Id)).HiddenAt);
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "event.restored").ToListAsync());
    }

    private async Task<(Account Admin, BingoEvent Hidden, BingoEvent Other)> ExclusivityFixtureAsync(EventState hiddenState, bool resume)
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "quarantine-exclusivity", "QUARANTINE-EXCLUSIVITY", now);
        admin.SetGlobalRole(GlobalRole.SuperAdmin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "quarantine-exclusivity-password"), false, now, incrementVersion: false);
        var hidden = ReadyForFinalReview(admin.Id);
        if (hiddenState is EventState.Finalized or EventState.Archived) hidden.FinalizeResults(now);
        if (hiddenState == EventState.Archived) hidden.Archive(now);
        var other = new BingoEvent(Guid.NewGuid(), "Other current event", $"other-{Guid.NewGuid():N}", "UTC", admin.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        other.ConfigureInitialSchedule(now.AddHours(-5), now.AddHours(-4), null, now.AddHours(-3), now.AddHours(1), 10);
        other.OpenSignups(now.AddHours(-4));
        other.CloseSignups(now.AddHours(-3));
        if (resume) { other.StartEvent(now.AddHours(-2)); other.EndEvent(now.AddHours(-1)); }
        await using var setup = new ApplicationDbContext(options);
        setup.AddRange(admin, hidden, other);
        await setup.SaveChangesAsync();
        var hide = await new EventQuarantineService(setup, new FixedClock(now)).HideAsync(
            hidden.Id, hidden.Version, hidden.Name, "Controlled quarantine", new(admin.Id, admin.LoginName));
        Assert.True(hide.Succeeded, hide.Error);
        if (!resume) await AddExclusivityStartReadinessAsync(setup, other, admin);
        return (admin, hidden, other);
    }

    private async Task AddExclusivityStartReadinessAsync(ApplicationDbContext db, BingoEvent item, Account admin)
    {
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var question = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var board = new Board(Guid.NewGuid(), item.Id, "Published board", 1, 1);
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
        draft.Start(now.AddHours(-2));
        draft.Finalize(now.AddHours(-1));
        var team = new Team(Guid.NewGuid(), item.Id, "Ready team", $"ready-{item.Id:N}", TeamFormationType.Drafted, null, true, now);
        team.Finalize(now);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddHours(-2), SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), "Ready player", "READY PLAYER", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, 0, now.AddHours(-2), admin.Id, question.Id, EventCharacterRole.Playing, 1, EhbSource.Manual, null);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now.AddHours(-2), null, "Controlled fixture");
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddHours(-1), admin.Id);
        var roster = new DraftPublicationRoster(Guid.NewGuid(), publication.Id, team.Id, participant.Id, TeamMembershipRole.Participant, 1, character.DisplayName);
        item.SetDraftRosterPublication(true);
        db.AddRange(form, question, board, draft, team, participant, character, assignment, membership, publication, roster);
        await BoardApprovalFixture.PublishAsync(db, board, now);
    }

    private async Task<string> EventSnapshotsAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(await db.Events.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
    }

    private async Task AssertWaitingForCurrentLockAsync(ApplicationDbContext context)
    {
        var pid = ((NpgsqlConnection)context.Database.GetDbConnection()).ProcessID;
        await using var probe = new ApplicationDbContext(options);
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await probe.Database.SqlQueryRaw<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = {0} AND locktype = 'advisory' AND objid = 7303004 AND NOT granted) AS \"Value\"", pid).SingleAsync()) return;
            await Task.Delay(10);
        }
        Assert.Fail("The operation did not wait for the shared current-event advisory lock.");
    }
}
