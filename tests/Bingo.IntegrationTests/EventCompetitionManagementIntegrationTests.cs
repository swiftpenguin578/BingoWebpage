using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using Bingo.Web;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class EventCompetitionManagementIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_wom_management")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private string connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        connectionString = database.GetConnectionString();
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task UnknownCreateRemainsPersistedAndCannotTriggerASecondPost()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: false);
        var managementClient = new RecordingManagementClient
        {
            CreateHandler = (_, _) => Task.FromResult(new WiseOldManCompetitionWriteResult(
                WiseOldManCompetitionWriteStatus.Unknown,
                ErrorCode: "Timeout",
                Message: "The provider outcome is unknown."))
        };

        await using (var db = CreateDb())
        {
            var service = CreateService(db, managementClient, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Unavailable)), clock);
            var first = await service.CreateAsync(fixture.EventId, fixture.EventVersion, fixture.Actor);
            Assert.False(first.Succeeded);
            Assert.Equal("Unknown", first.Status);
            Assert.Equal(1, managementClient.CreateCalls);
        }

        clock.Advance(TimeSpan.FromMinutes(2));
        await using (var reconcileDb = CreateDb())
        {
            await CreateService(reconcileDb, managementClient, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Unavailable)), clock)
                .ProcessDueAsync();
        }

        await using (var retryDb = CreateDb())
        {
            var second = await CreateService(retryDb, managementClient, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Unavailable)), clock)
                .CreateAsync(fixture.EventId, fixture.EventVersion, fixture.Actor);
            Assert.True(second.Succeeded);
            Assert.Equal("Unknown", second.Status);
        }

        Assert.Equal(1, managementClient.CreateCalls);
        await using var verify = CreateDb();
        var operation = await verify.EventCompetitionManagementOperations.SingleAsync(x => x.EventId == fixture.EventId);
        Assert.Equal(EventCompetitionManagementOperationPhase.Unknown, operation.Phase);
        Assert.Null(operation.NextAttemptAt);
    }

    [Fact]
    public async Task DefinitivelyFailedCreateCanBeRetriedAndSucceed()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: false);
        var createAttempts = 0;
        var managementClient = new RecordingManagementClient
        {
            CreateHandler = (payload, _) =>
            {
                if (Interlocked.Increment(ref createAttempts) == 1)
                    return Task.FromResult(new WiseOldManCompetitionWriteResult(
                        WiseOldManCompetitionWriteStatus.NotFound,
                        ErrorCode: "NotFound",
                        Message: "The controlled fake provider did not find the competition."));

                var competition = new WiseOldManCompetition(
                    7701,
                    payload.Title,
                    payload.StartsAt,
                    payload.EndsAt,
                    clock.GetUtcNow(),
                    payload.Teams.SelectMany(team => team.Participants)
                        .Select(name => new WiseOldManCompetitionParticipant(name, "REGULAR", null))
                        .ToArray());
                return Task.FromResult(new WiseOldManCompetitionWriteResult(
                    WiseOldManCompetitionWriteStatus.Success,
                    competition,
                    ProtectedVerificationCode: "controlled-protected-code"));
            }
        };

        await using (var failedDb = CreateDb())
        {
            var failed = await CreateService(failedDb, managementClient,
                new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Unavailable)), clock)
                .CreateAsync(fixture.EventId, fixture.EventVersion, fixture.Actor);
            Assert.False(failed.Succeeded);
            Assert.Equal("Failed", failed.Status);
            Assert.Equal("NotFound", failed.ErrorCode);
        }

        await using (var retryDb = CreateDb())
        {
            var retried = await CreateService(retryDb, managementClient,
                new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Unavailable)), clock)
                .CreateAsync(fixture.EventId, fixture.EventVersion, fixture.Actor);
            Assert.True(retried.Succeeded, retried.Error);
            Assert.Equal("Succeeded", retried.Status);
        }

        Assert.Equal(2, managementClient.CreateCalls);
        await using var verify = CreateDb();
        var operations = await verify.EventCompetitionManagementOperations
            .Where(x => x.EventId == fixture.EventId)
            .ToListAsync();
        Assert.Equal(2, operations.Count);
        var failedOperation = Assert.Single(operations, x => x.Phase == EventCompetitionManagementOperationPhase.Failed);
        Assert.Equal("NotFound", failedOperation.SafeErrorCode);
        var succeededOperation = Assert.Single(operations, x => x.Phase == EventCompetitionManagementOperationPhase.Succeeded);
        Assert.Equal(7701L, succeededOperation.RemoteCompetitionId);
        Assert.Equal(7701L, (await verify.EventCompetitionManagements.SingleAsync(x => x.EventId == fixture.EventId)).CompetitionId);
    }

    [Fact]
    public async Task ManagePageShowsCreateOnlyForDefinitiveFailedOrCancelledCreateWithoutLinks()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        await using var factory = CreateAdminFactory(clock);
        var scenarios = new (string Name, EventCompetitionManagementOperationType? Type,
            EventCompetitionManagementOperationPhase? Phase, string Link, bool Expected)[]
        {
            ("unmanaged", null, null, "none", true),
            ("failed Create", EventCompetitionManagementOperationType.Create, EventCompetitionManagementOperationPhase.Failed, "none", true),
            ("cancelled Create", EventCompetitionManagementOperationType.Create, EventCompetitionManagementOperationPhase.Cancelled, "none", true),
            ("pending Create", EventCompetitionManagementOperationType.Create, EventCompetitionManagementOperationPhase.Pending, "none", false),
            ("claimed Create", EventCompetitionManagementOperationType.Create, EventCompetitionManagementOperationPhase.Claimed, "none", false),
            ("sending Create", EventCompetitionManagementOperationType.Create, EventCompetitionManagementOperationPhase.Sending, "none", false),
            ("retrying Create", EventCompetitionManagementOperationType.Create, EventCompetitionManagementOperationPhase.Retry, "none", false),
            ("unknown Create", EventCompetitionManagementOperationType.Create, EventCompetitionManagementOperationPhase.Unknown, "none", false),
            ("failed Update", EventCompetitionManagementOperationType.Update, EventCompetitionManagementOperationPhase.Failed, "none", false),
            ("failed Delete", EventCompetitionManagementOperationType.Delete, EventCompetitionManagementOperationPhase.Failed, "none", false),
            ("manual link", null, null, "manual", false),
            ("active managed link", null, null, "managed", false),
            ("conflicted managed link", null, null, "conflict", false),
            ("failed Create with managed link", EventCompetitionManagementOperationType.Create, EventCompetitionManagementOperationPhase.Failed, "managed", false)
        };

        var scenarioIndex = 0;
        foreach (var scenario in scenarios)
        {
            var linked = scenario.Link is "managed" or "conflict";
            var fixture = await SeedEventAsync(clock, live: false, competitionId: linked ? 7800 + scenarioIndex : null);
            await SetAdminPasswordAsync(fixture.Actor.Id, clock.GetUtcNow());

            if (scenario.Link == "manual")
                await AddManualCompetitionLinkAsync(fixture.EventId, 7900 + scenarioIndex, clock.GetUtcNow());
            else if (scenario.Link == "conflict")
                await MarkManagementConflictAsync(fixture.EventId, clock.GetUtcNow());

            if (scenario.Type is { } type && scenario.Phase is { } phase)
                await AddOperationAsync(fixture, type, phase, clock);

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
            await LoginAsync(client, fixture.Actor.Username);
            var page = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Events/Manage/{fixture.EventId}"));
            var showsCreate = page.Contains("Create managed WOM competition", StringComparison.Ordinal);
            Assert.True(scenario.Expected == showsCreate,
                $"Scenario '{scenario.Name}' expected Create visible={scenario.Expected}, actual={showsCreate}.");
            scenarioIndex++;
        }
    }

    [Fact]
    public async Task StaleRetryRequeuesCurrentLiveDatesAndRecordsCurrentFingerprint()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: true, competitionId: 4101);
        var remote = fixture.RemoteCompetition!;
        var managementClient = new RecordingManagementClient();
        var oldPayload = new WiseOldManCompetitionWritePayload(
            "Old queued title",
            remote.StartsAt.AddDays(1),
            remote.EndsAt.AddDays(1),
            [new("Alpha", ["Alice"])],
            IncludeTeams: true);

        await using (var setup = CreateDb())
        {
            var operation = new EventCompetitionManagementOperation(
                Guid.NewGuid(), fixture.EventId, fixture.ManagementId, EventCompetitionManagementOperationType.Update,
                JsonSerializer.Serialize(oldPayload), "stale-fingerprint", fixture.EventVersion, clock.GetUtcNow());
            setup.EventCompetitionManagementOperations.Add(operation);
            var management = await setup.EventCompetitionManagements.SingleAsync(x => x.Id == fixture.ManagementId);
            management.MarkPending(operation.Id, clock.GetUtcNow());
            await setup.SaveChangesAsync();
        }

        await using (var first = CreateDb())
        {
            await CreateService(first, managementClient, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote)), clock)
                .ProcessDueAsync();
        }
        await using (var second = CreateDb())
        {
            await CreateService(second, managementClient, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote)), clock)
                .ProcessDueAsync();
        }

        var update = Assert.Single(managementClient.Updates);
        Assert.False(update.IncludeTeams);
        Assert.Equal("Managed event", update.Title);
        Assert.Equal(remote.StartsAt, update.StartsAt);
        Assert.Equal(remote.EndsAt, update.EndsAt);

        await using var verify = CreateDb();
        var savedManagement = await verify.EventCompetitionManagements.SingleAsync(x => x.Id == fixture.ManagementId);
        var expectedFingerprint = WiseOldManCompetitionRules.Fingerprint(new
        {
            Title = "Managed event",
            StartsAt = remote.StartsAt,
            EndsAt = remote.EndsAt,
            RosterLocked = true
        });
        Assert.Equal(expectedFingerprint, savedManagement.LastAppliedLocalFingerprint);
        Assert.Equal(EventCompetitionManagementOperationPhase.Succeeded,
            await verify.EventCompetitionManagementOperations.Where(x => x.EventId == fixture.EventId).Select(x => x.Phase).SingleAsync());
    }

    [Fact]
    public async Task RateLimitedUpdateCoalescesAnEditedPreLiveProjectionBeforePut()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: false, competitionId: 4151);
        var remote = fixture.RemoteCompetition!;
        var managementClient = new RecordingManagementClient();
        var attempts = 0;
        managementClient.UpdateHandler = (_, payload, _, _) =>
            Task.FromResult(Interlocked.Increment(ref attempts) == 1
                ? new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.RateLimited, RetryAt: clock.GetUtcNow())
                : Success(remote, payload));
        var competitionClient = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));

        await using (var first = CreateDb())
        {
            var initial = await CreateService(first, managementClient, competitionClient, clock).QueueUpdateAsync(fixture.EventId);
            Assert.False(initial.Succeeded);
            Assert.Equal("Retry", initial.Status);
        }

        var editedStart = remote.StartsAt.AddMinutes(30);
        var editedEnd = remote.EndsAt.AddMinutes(30);
        await using (var edit = CreateDb())
        {
            var item = await edit.Events.SingleAsync(x => x.Id == fixture.EventId);
            item.ConfigureSchedule(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, editedStart, editedEnd, item.ParticipantCap);
            await edit.SaveChangesAsync();
        }

        await using (var refresh = CreateDb())
            await CreateService(refresh, managementClient, competitionClient, clock).ProcessDueAsync();
        await using (var dispatch = CreateDb())
            await CreateService(dispatch, managementClient, competitionClient, clock).ProcessDueAsync();

        Assert.Equal(2, managementClient.Updates.Count);
        var finalPayload = managementClient.Updates[^1];
        Assert.True(finalPayload.IncludeTeams);
        Assert.Equal(editedStart, finalPayload.StartsAt);
        Assert.Equal(editedEnd, finalPayload.EndsAt);

        await using var verify = CreateDb();
        var expectedPreview = await CreateService(verify, managementClient, competitionClient, clock).PreviewAsync(fixture.EventId);
        var savedManagement = await verify.EventCompetitionManagements.SingleAsync(x => x.Id == fixture.ManagementId);
        Assert.Equal(expectedPreview!.Fingerprint, savedManagement.LastAppliedLocalFingerprint);
        Assert.Equal(EventCompetitionManagementOperationPhase.Succeeded,
            await verify.EventCompetitionManagementOperations.Where(x => x.EventId == fixture.EventId).Select(x => x.Phase).SingleAsync());
    }

    [Fact]
    public async Task ManualLinkWaitsForManagedProviderWriteOnTheSameCompetition()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var managed = await SeedEventAsync(clock, live: true, competitionId: 4201);
        var manual = await SeedEventAsync(clock, live: false, startsOverride: managed.RemoteCompetition!.StartsAt, endsOverride: managed.RemoteCompetition.EndsAt);
        var remote = managed.RemoteCompetition!;
        var providerCurrent = remote;
        var managementClient = new RecordingManagementClient();
        var managedCompetitionClient = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote));
        var manualProviderRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manualCompetitionClient = new RecordingCompetitionClient(_ =>
        {
            manualProviderRead.TrySetResult();
            return new(WiseOldManCompetitionStatus.Success, providerCurrent);
        });
        var updateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manualStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        managementClient.UpdateHandler = async (_, payload, _, cancellationToken) =>
        {
            updateEntered.SetResult();
            await releaseUpdate.Task.WaitAsync(cancellationToken);
            return new(WiseOldManCompetitionWriteStatus.Success, providerCurrent);
        };

        var managedUpdate = Task.Run(async () =>
        {
            await using var db = CreateDb();
            return await CreateService(db, managementClient, managedCompetitionClient, clock).QueueUpdateAsync(managed.EventId);
        });
        await updateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var manualLink = Task.Run(async () =>
        {
            await using var db = CreateDb();
            manualStarted.SetResult();
            return await new EventCompetitionSynchronizationService(db, manualCompetitionClient, new FixedStatus(), clock)
                .ConfigureAsync(manual.EventId, manual.EventVersion, remote.Id, false, manual.Actor);
        });
        await manualStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var completed = await Task.WhenAny(manualProviderRead.Task, Task.Delay(TimeSpan.FromMilliseconds(300)));
        Assert.NotSame(manualProviderRead.Task, completed);

        providerCurrent = new WiseOldManCompetition(remote.Id, "Managed event changed",
            remote.StartsAt.AddMinutes(15), remote.EndsAt.AddMinutes(15), remote.LastUpdatedAt, remote.Participants);
        releaseUpdate.SetResult();
        var updateResult = await managedUpdate;
        await manualProviderRead.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var linkResult = await manualLink;
        Assert.True(updateResult.Succeeded, updateResult.Error);
        Assert.False(linkResult.Succeeded);
        Assert.Contains("within five minutes", linkResult.Error, StringComparison.OrdinalIgnoreCase);

        await using var verify = CreateDb();
        Assert.False(await verify.EventCompetitionSynchronizations.AnyAsync(x => x.EventId == manual.EventId));
    }

    [Fact]
    public async Task ReceiptSaveRetryReloadsManagementAndCompletesTheOperation()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: true, competitionId: 4301);
        var remote = fixture.RemoteCompetition!;
        var interceptor = new ThrowOnceAfterArmingInterceptor();
        var managementClient = new RecordingManagementClient();
        managementClient.UpdateHandler = (_, payload, _, _) =>
        {
            interceptor.Arm();
            return Task.FromResult(Success(remote, payload));
        };

        await using (var setup = CreateDb())
        {
            var currentFingerprint = WiseOldManCompetitionRules.Fingerprint(new
            {
                Title = "Managed event",
                StartsAt = remote.StartsAt,
                EndsAt = remote.EndsAt,
                RosterLocked = true
            });
            var operation = new EventCompetitionManagementOperation(
                Guid.NewGuid(), fixture.EventId, fixture.ManagementId, EventCompetitionManagementOperationType.Update,
                JsonSerializer.Serialize(new WiseOldManCompetitionWritePayload("Managed event", remote.StartsAt, remote.EndsAt, [], false)),
                currentFingerprint, fixture.EventVersion, clock.GetUtcNow());
            setup.EventCompetitionManagementOperations.Add(operation);
            var management = await setup.EventCompetitionManagements.SingleAsync(x => x.Id == fixture.ManagementId);
            management.MarkPending(operation.Id, clock.GetUtcNow());
            await setup.SaveChangesAsync();
        }

        await using (var db = CreateDb(interceptor))
            await CreateService(db, managementClient, new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, remote)), clock)
                .ProcessDueAsync();

        await using var verify = CreateDb();
        Assert.Equal(EventCompetitionManagementOperationPhase.Succeeded,
            await verify.EventCompetitionManagementOperations.Where(x => x.EventId == fixture.EventId).Select(x => x.Phase).SingleAsync());
        Assert.Equal(EventCompetitionManagementStatus.Active,
            await verify.EventCompetitionManagements.Where(x => x.Id == fixture.ManagementId).Select(x => x.Status).SingleAsync());
        Assert.True(interceptor.Thrown);
    }

    [Fact]
    public async Task UpdateAllSlotIsClaimedOnceAcrossTwoContextsAndReceiptSurvivesRestart()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: true, competitionId: 4401,
            startsOverride: clock.GetUtcNow().AddMinutes(-30), endsOverride: clock.GetUtcNow().AddHours(10));
        var managementClient = new RecordingManagementClient();
        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(15));

        var first = Task.Run(async () =>
        {
            await using var db = CreateDb();
            await new EventCompetitionUpdateAllService(db, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();
        });
        var second = Task.Run(async () =>
        {
            await using var db = CreateDb();
            await new EventCompetitionUpdateAllService(db, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();
        });
        await Task.WhenAll(first, second);

        Assert.Single(managementClient.UpdateAllCalls);
        await using (var verify = CreateDb())
        {
            var slot = await verify.EventCompetitionUpdateAllSlots.OrderBy(x => x.PairedFetchAt).FirstAsync();
            Assert.Equal(EventCompetitionUpdateAllSlotStatus.Acknowledged, slot.Status);
            Assert.Equal(fixture.RemoteCompetition!.StartsAt.AddHours(4), slot.PairedFetchAt);
            Assert.Equal(fixture.RemoteCompetition.StartsAt.AddHours(3).AddMinutes(45), slot.ScheduledAt);
            Assert.Equal(1, slot.AttemptCount);
            Assert.Equal("Acknowledged", slot.OutcomeCode);
        }

        // A second worker/restart sees the durable terminal receipt and does not
        // post the same remote slot again.
        await using (var restarted = CreateDb())
            await new EventCompetitionUpdateAllService(restarted, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();
        Assert.Single(managementClient.UpdateAllCalls);
    }

    [Fact]
    public async Task UpdateAllRechecksEventEligibilityAfterAdmissionWaitAndSkipsWithoutPosting()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: true, competitionId: 4409,
            startsOverride: clock.GetUtcNow().AddMinutes(-30), endsOverride: clock.GetUtcNow().AddHours(10));
        var admissionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAdmission = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var managementClient = new RecordingManagementClient
        {
            BeforeUpdateAllRecheck = async _ =>
            {
                admissionStarted.TrySetResult();
                await releaseAdmission.Task;
            }
        };
        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(15));

        var processing = Task.Run(async () =>
        {
            await using var db = CreateDb();
            await new EventCompetitionUpdateAllService(db, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();
        });
        await admissionStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var endEvent = CreateDb())
        {
            var item = await endEvent.Events.SingleAsync(value => value.Id == fixture.EventId);
            item.EndEvent(clock.GetUtcNow());
            await endEvent.SaveChangesAsync();
        }
        releaseAdmission.TrySetResult();
        await processing;

        Assert.Empty(managementClient.UpdateAllCalls);
        await using var verify = CreateDb();
        var slot = await verify.EventCompetitionUpdateAllSlots.Where(value => value.EventId == fixture.EventId).OrderBy(value => value.PairedFetchAt).FirstAsync();
        Assert.Equal(EventCompetitionUpdateAllSlotStatus.Skipped, slot.Status);
        Assert.Equal("DispatchEligibilityChanged", slot.OutcomeCode);
    }

    [Fact]
    public async Task PendingFutureSlotRebindsSameManagedLineageAfterManagementAndSourceRecovery()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: true, competitionId: 4405,
            startsOverride: clock.GetUtcNow().AddMinutes(-30), endsOverride: clock.GetUtcNow().AddHours(10));
        var managementClient = new RecordingManagementClient();

        // The normal scan creates the current and next durable receipts before
        // either dispatch window is due.
        await using (var schedule = CreateDb())
            await new EventCompetitionUpdateAllService(schedule, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();

        await using (var mutate = CreateDb())
        {
            var management = await mutate.EventCompetitionManagements.SingleAsync(x => x.Id == fixture.ManagementId);
            var synchronization = await mutate.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == fixture.EventId);
            management.MarkApplied(Guid.NewGuid(), "edited-fingerprint", RemoteFingerprint(fixture.RemoteCompetition!), "[]",
                "Edited managed event", fixture.RemoteCompetition!.StartsAt, fixture.RemoteCompetition.EndsAt, clock.GetUtcNow());
            synchronization.BeginReplacementGeneration("recovered-assignments", clock.GetUtcNow());
            await mutate.SaveChangesAsync();
        }

        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(15));
        await using (var dispatch = CreateDb())
            await new EventCompetitionUpdateAllService(dispatch, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();

        Assert.Single(managementClient.UpdateAllCalls);
        await using var verify = CreateDb();
        var slot = await verify.EventCompetitionUpdateAllSlots
            .Where(x => x.CompetitionId == fixture.RemoteCompetition!.Id)
            .OrderBy(x => x.PairedFetchAt)
            .FirstAsync();
        var managementAfter = await verify.EventCompetitionManagements.SingleAsync(x => x.Id == fixture.ManagementId);
        var synchronizationAfter = await verify.EventCompetitionSynchronizations.SingleAsync(x => x.Id == managementAfter.SynchronizationId);
        Assert.Equal(EventCompetitionUpdateAllSlotStatus.Acknowledged, slot.Status);
        Assert.Equal(managementAfter.ManagementVersion, slot.ManagementVersion);
        Assert.Equal(synchronizationAfter.Generation, slot.SynchronizationGeneration);
        Assert.Equal(1, slot.AttemptCount);
    }

    [Fact]
    public async Task PendingFutureSlotSkipsDeletedOrReplacedManagedLink()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var deleted = await SeedEventAsync(clock, live: true, competitionId: 4406,
            startsOverride: clock.GetUtcNow().AddMinutes(-30), endsOverride: clock.GetUtcNow().AddHours(10));
        var replaced = await SeedEventAsync(clock, live: true, competitionId: 4407,
            startsOverride: clock.GetUtcNow().AddMinutes(-30), endsOverride: clock.GetUtcNow().AddHours(10));
        var managementClient = new RecordingManagementClient();

        await using (var schedule = CreateDb())
            await new EventCompetitionUpdateAllService(schedule, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();

        clock.Advance(TimeSpan.FromSeconds(1));
        await using (var mutate = CreateDb())
        {
            var deletedManagement = await mutate.EventCompetitionManagements.SingleAsync(x => x.Id == deleted.ManagementId);
            var deletedSynchronization = await mutate.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == deleted.EventId);
            var deletionOperation = new EventCompetitionManagementOperation(
                Guid.NewGuid(), deleted.EventId, deleted.ManagementId, EventCompetitionManagementOperationType.Delete,
                "{}", "deleted", deleted.EventVersion, clock.GetUtcNow());
            deletionOperation.Succeed(deleted.RemoteCompetition!.Id, "deleted", clock.GetUtcNow());
            mutate.EventCompetitionManagementOperations.Add(deletionOperation);
            deletedManagement.MarkDeleted(deletionOperation.Id, clock.GetUtcNow());
            deletedSynchronization.Reconfigure(null, null, null, null, deletedSynchronization.AssignmentFingerprint, clock.GetUtcNow());
            deletedManagement.MarkApplied(Guid.NewGuid(), "recreated", RemoteFingerprint(deleted.RemoteCompetition), "[]",
                deleted.RemoteCompetition.Title, deleted.RemoteCompetition.StartsAt, deleted.RemoteCompetition.EndsAt, clock.GetUtcNow());
            deletedSynchronization.Reconfigure(deleted.RemoteCompetition.Id, deleted.RemoteCompetition.Title,
                deleted.RemoteCompetition.StartsAt, deleted.RemoteCompetition.EndsAt, "recreated-assignments", clock.GetUtcNow());

            var replacedSynchronization = await mutate.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == replaced.EventId);
            replacedSynchronization.Reconfigure(4499, "Replacement", replaced.RemoteCompetition!.StartsAt, replaced.RemoteCompetition.EndsAt,
                "replacement-assignments", clock.GetUtcNow());
            await mutate.SaveChangesAsync();
        }

        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(15));
        await using (var dispatch = CreateDb())
            await new EventCompetitionUpdateAllService(dispatch, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();

        Assert.Empty(managementClient.UpdateAllCalls);
        await using var verify = CreateDb();
        var deletedSlot = await verify.EventCompetitionUpdateAllSlots
            .Where(x => x.CompetitionId == deleted.RemoteCompetition!.Id)
            .OrderBy(x => x.PairedFetchAt)
            .FirstAsync();
        var replacedSlot = await verify.EventCompetitionUpdateAllSlots
            .Where(x => x.CompetitionId == replaced.RemoteCompetition!.Id)
            .OrderBy(x => x.PairedFetchAt)
            .FirstAsync();
        Assert.Equal(EventCompetitionUpdateAllSlotStatus.Skipped, deletedSlot.Status);
        Assert.Equal(EventCompetitionUpdateAllSlotStatus.Skipped, replacedSlot.Status);
        Assert.Equal("ManagementChanged", deletedSlot.OutcomeCode);
        Assert.Equal("SourceMismatch", replacedSlot.OutcomeCode);
    }

    [Fact]
    public async Task ExpiredClaimBecomesAmbiguousAndIsNeverBlindlyRetried()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: true, competitionId: 4402,
            startsOverride: clock.GetUtcNow().AddMinutes(-30), endsOverride: clock.GetUtcNow().AddHours(10));
        var managementClient = new RecordingManagementClient();
        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(15));

        await using (var setup = CreateDb())
        {
            var slot = new EventCompetitionUpdateAllSlot(
                Guid.NewGuid(), fixture.EventId, fixture.ManagementId!.Value,
                (await setup.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == fixture.EventId)).Id,
                fixture.RemoteCompetition!.Id, fixture.RemoteCompetition.StartsAt,
                fixture.RemoteCompetition.StartsAt.AddHours(4), fixture.RemoteCompetition.StartsAt.AddHours(3).AddMinutes(45),
                2, 1, clock.GetUtcNow());
            slot.Claim(clock.GetUtcNow());
            setup.EventCompetitionUpdateAllSlots.Add(slot);
            await setup.SaveChangesAsync();
        }

        clock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        await using (var restarted = CreateDb())
            await new EventCompetitionUpdateAllService(restarted, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();

        await using var verify = CreateDb();
        var saved = await verify.EventCompetitionUpdateAllSlots.OrderBy(x => x.PairedFetchAt).FirstAsync();
        Assert.Equal(EventCompetitionUpdateAllSlotStatus.Unknown, saved.Status);
        Assert.Equal("ClaimExpired", saved.OutcomeCode);
        Assert.Empty(managementClient.UpdateAllCalls);
    }

    [Fact]
    public async Task UpdateAllFailureDoesNotGateThePairedHourlyFetch()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var fixture = await SeedEventAsync(clock, live: true, competitionId: 4403,
            startsOverride: clock.GetUtcNow().AddMinutes(-30), endsOverride: clock.GetUtcNow().AddHours(10));
        var managementClient = new RecordingManagementClient
        {
            UpdateAllHandler = (_, _, _) => Task.FromResult(new WiseOldManUpdateAllResult(
                WiseOldManUpdateAllStatus.Unknown, ErrorCode: "UnknownOutcome", Message: "controlled ambiguous outcome"))
        };
        var competitionClient = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, fixture.RemoteCompetition));

        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(15));
        await using (var updateDb = CreateDb())
            await new EventCompetitionUpdateAllService(updateDb, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();

        await using (var verifyUpdate = CreateDb())
            Assert.Contains(EventCompetitionUpdateAllSlotStatus.Unknown,
                await verifyUpdate.EventCompetitionUpdateAllSlots.Select(x => x.Status).ToListAsync());

        clock.Advance(TimeSpan.FromMinutes(15));
        await using (var fetchDb = CreateDb())
            await new EventCompetitionSynchronizationService(fetchDb, competitionClient, new FixedStatus(), clock).ProcessDueAsync();

        Assert.Equal(1, competitionClient.Calls);
        await using var verifyFetch = CreateDb();
        Assert.NotNull((await verifyFetch.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == fixture.EventId)).LastSuccessfulAt);
    }

    [Fact]
    public async Task UpdateAllHoldsTheCompetitionReferenceLockAgainstManualLinking()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var managed = await SeedEventAsync(clock, live: true, competitionId: 4404,
            startsOverride: clock.GetUtcNow().AddMinutes(-30), endsOverride: clock.GetUtcNow().AddHours(10));
        var manual = await SeedEventAsync(clock, live: false,
            startsOverride: managed.RemoteCompetition!.StartsAt, endsOverride: managed.RemoteCompetition.EndsAt);
        var enteredProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var managementClient = new RecordingManagementClient
        {
            UpdateAllHandler = async (_, _, cancellationToken) =>
            {
                enteredProvider.SetResult();
                await releaseProvider.Task.WaitAsync(cancellationToken);
                return new(WiseOldManUpdateAllStatus.Acknowledged, Message: "controlled acknowledgement");
            }
        };
        var manualClient = new RecordingCompetitionClient(_ => new(WiseOldManCompetitionStatus.Success, managed.RemoteCompetition));
        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(15));

        var update = Task.Run(async () =>
        {
            await using var db = CreateDb();
            await new EventCompetitionUpdateAllService(db, managementClient, new PassthroughCredentialProtector(), clock).ProcessDueAsync();
        });
        await enteredProvider.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var manualLink = Task.Run(async () =>
        {
            await using var db = CreateDb();
            return await new EventCompetitionSynchronizationService(db, manualClient, new FixedStatus(), clock)
                .ConfigureAsync(manual.EventId, manual.EventVersion, managed.RemoteCompetition.Id, false, manual.Actor);
        });
        var completedBeforeRelease = await Task.WhenAny(manualLink, Task.Delay(TimeSpan.FromMilliseconds(300)));
        Assert.NotSame(manualLink, completedBeforeRelease);

        releaseProvider.SetResult();
        await update;
        var linkResult = await manualLink;
        Assert.True(linkResult.Succeeded, linkResult.Error);
    }

    private WebApplicationFactory<Program> CreateAdminFactory(TestClock clock) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:Database", connectionString)
            .UseSetting("DiscordAuthentication:ClientId", "fixture-client")
            .UseSetting("DiscordAuthentication:ClientSecret", "fixture-secret");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    });

    private async Task SetAdminPasswordAsync(Guid accountId, DateTimeOffset now)
    {
        await using var db = CreateDb();
        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "managed-test-password"),
            false, now, incrementVersion: false);
        await db.SaveChangesAsync();
    }

    private static async Task LoginAsync(HttpClient client, string username)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = "managed-test-password",
            ["__RequestVerificationToken"] = InputValue(page, "__RequestVerificationToken")
        }));
        var responseBody = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var validationError = Regex.Match(responseBody,
            "The username or password is incorrect|A public username is required|A password is required").Value;
        Assert.True(response.StatusCode == HttpStatusCode.Redirect,
            $"Admin login failed with {response.StatusCode}: {validationError}");
    }

    private static string InputValue(string page, string name) =>
        WebUtility.HtmlDecode(Regex.Match(page, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"[^>]*>").Groups[1].Value);

    private async Task AddManualCompetitionLinkAsync(Guid eventId, long competitionId, DateTimeOffset now)
    {
        await using var db = CreateDb();
        var item = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId);
        db.EventCompetitionSynchronizations.Add(new EventCompetitionSynchronization(
            Guid.NewGuid(), eventId, 1, competitionId, item.Name,
            item.EventStartsAt!.Value, item.EventEndsAt!.Value, "manual-link-fixture", now));
        await db.SaveChangesAsync();
    }

    private async Task MarkManagementConflictAsync(Guid eventId, DateTimeOffset now)
    {
        await using var db = CreateDb();
        var management = await db.EventCompetitionManagements.SingleAsync(x => x.EventId == eventId);
        management.MarkFailure(Guid.NewGuid(), EventCompetitionManagementStatus.Conflict,
            "SharedSource", "Controlled conflict fixture.", now);
        await db.SaveChangesAsync();
    }

    private async Task AddOperationAsync(
        Fixture fixture,
        EventCompetitionManagementOperationType type,
        EventCompetitionManagementOperationPhase phase,
        TestClock clock)
    {
        await using var db = CreateDb();
        var now = clock.GetUtcNow();
        var operation = new EventCompetitionManagementOperation(
            Guid.NewGuid(), fixture.EventId, null, type, "{}", "managed-ui-fixture", fixture.EventVersion, now);
        operation.SetActor(fixture.Actor.Id, fixture.Actor.Username);
        switch (phase)
        {
            case EventCompetitionManagementOperationPhase.Claimed:
            case EventCompetitionManagementOperationPhase.Sending:
                operation.Claim(now);
                break;
            case EventCompetitionManagementOperationPhase.Retry:
                operation.Retry(now.AddMinutes(1), "Unavailable", "Controlled retry fixture.", now);
                break;
            case EventCompetitionManagementOperationPhase.Unknown:
                operation.MarkUnknown("UnknownOutcome", "Controlled unknown fixture.", now);
                break;
            case EventCompetitionManagementOperationPhase.Failed:
                operation.Fail("NotFound", "Controlled failed fixture.", now);
                break;
            case EventCompetitionManagementOperationPhase.Cancelled:
                operation.Cancel(now);
                break;
            case EventCompetitionManagementOperationPhase.Pending:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unsupported UI fixture phase.");
        }

        db.EventCompetitionManagementOperations.Add(operation);
        await db.SaveChangesAsync();
        if (phase == EventCompetitionManagementOperationPhase.Claimed)
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE event_competition_management_operations SET phase = 'Claimed' WHERE id = {operation.Id}");
    }

    private static EventCompetitionManagementService CreateService(
        ApplicationDbContext db,
        RecordingManagementClient managementClient,
        RecordingCompetitionClient competitionClient,
        TimeProvider clock) =>
        new(db, managementClient, competitionClient, new PassthroughCredentialProtector(), clock);

    private ApplicationDbContext CreateDb(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString);
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new ApplicationDbContext(builder.Options);
    }

    private async Task<Fixture> SeedEventAsync(
        TestClock clock,
        bool live,
        long? competitionId = null,
        DateTimeOffset? startsOverride = null,
        DateTimeOffset? endsOverride = null)
    {
        var now = clock.GetUtcNow();
        var adminLoginName = $"admin-{Guid.NewGuid():N}";
        var admin = Account.CreateWebsite(Guid.NewGuid(), adminLoginName, adminLoginName.ToUpperInvariant(), now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var startsAt = startsOverride ?? (live ? now.AddMinutes(-30) : now.AddHours(1));
        var endsAt = endsOverride ?? (live ? now.AddHours(2) : now.AddHours(3));
        var eventItem = new BingoEvent(Guid.NewGuid(), "Managed event", $"managed-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-3), now.AddHours(-2), startsAt, endsAt, endsAt, 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-3));
        eventItem.CloseSignups(now.AddHours(-2));
        if (live) eventItem.StartEvent(now.AddMinutes(-30));

        var team = new Team(Guid.NewGuid(), eventItem.Id, "Alpha", $"alpha-{Guid.NewGuid():N}", TeamFormationType.Preformed, null, false, now);
        var participant = new EventParticipant(Guid.NewGuid(), eventItem.Id, SignupStatus.Confirmed, 1, now.AddHours(-3), SignupSource.Website);
        var characterName = $"A{Guid.NewGuid():N}"[..12];
        var character = new OsrsCharacter(Guid.NewGuid(), characterName, characterName.ToUpperInvariant(), now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), eventItem.Id, participant.Id, character.Id, 0, now, null, null,
            EventCharacterRole.Playing, 1m, EhbSource.Manual, null);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now, null, "fixture");
        var draft = new DraftSession(Guid.NewGuid(), eventItem.Id, 1);
        draft.Start(now.AddHours(-2));
        draft.Finalize(now.AddHours(-1));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddHours(-1), admin.Id);

        EventCompetitionSynchronization? state = null;
        EventCompetitionManagement? management = null;
        WiseOldManCompetition? remote = null;
        if (competitionId is { } id)
        {
            remote = new WiseOldManCompetition(id, eventItem.Name, startsAt, endsAt, now, []);
            state = new EventCompetitionSynchronization(Guid.NewGuid(), eventItem.Id, 1, id, remote.Title, startsAt, endsAt, "fixture-assignments", now);
            management = new EventCompetitionManagement(Guid.NewGuid(), eventItem.Id, state.Id, id, remote.Title, startsAt, endsAt,
                "protected:secret", "stale-local-fingerprint", now);
            management.MarkApplied(Guid.NewGuid(), "stale-local-fingerprint", RemoteFingerprint(remote), "[]", remote.Title, startsAt, endsAt, now);
            management.ObserveActualStart(eventItem.ActualStartedAt, now);
        }

        await using var db = CreateDb();
        db.AddRange(admin, eventItem, team, participant, character, assignment, membership, draft, publication);
        if (state is not null) db.Add(state);
        if (management is not null) db.Add(management);
        await db.SaveChangesAsync();
        return new(eventItem.Id, eventItem.Version, new(admin.Id, admin.LoginName), management?.Id, remote);
    }

    private static string RemoteFingerprint(WiseOldManCompetition competition) =>
        WiseOldManCompetitionRules.Fingerprint(new { competition.Id, competition.Title, competition.StartsAt, competition.EndsAt });

    private static WiseOldManCompetitionWriteResult Success(WiseOldManCompetition remote, WiseOldManCompetitionWritePayload payload) =>
        new(WiseOldManCompetitionWriteStatus.Success,
            new WiseOldManCompetition(remote.Id, payload.Title, payload.StartsAt, payload.EndsAt, remote.LastUpdatedAt,
                payload.IncludeTeams
                    ? payload.Teams.SelectMany(team => team.Participants).Select(name => new WiseOldManCompetitionParticipant(name, "REGULAR", null)).ToArray()
                    : remote.Participants));

    private sealed record Fixture(Guid EventId, long EventVersion, LifecycleActor Actor, Guid? ManagementId, WiseOldManCompetition? RemoteCompetition);

    private sealed class RecordingManagementClient : IWiseOldManCompetitionManagementClient
    {
        public int CreateCalls { get; private set; }
        public List<WiseOldManCompetitionWritePayload> Updates { get; } = [];
        public ConcurrentQueue<(long CompetitionId, string VerificationCode)> UpdateAllCalls { get; } = [];
        public Func<WiseOldManCompetitionWritePayload, CancellationToken, Task<WiseOldManCompetitionWriteResult>>? CreateHandler { get; init; }
        public Func<long, WiseOldManCompetitionWritePayload, string, CancellationToken, Task<WiseOldManCompetitionWriteResult>>? UpdateHandler { get; set; }
        public Func<long, string, CancellationToken, Task<WiseOldManUpdateAllResult>>? UpdateAllHandler { get; set; }
        public Func<CancellationToken, Task>? BeforeUpdateAllRecheck { get; init; }

        public Task<WiseOldManCompetitionWriteResult> CreateAsync(WiseOldManCompetitionWritePayload payload, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return CreateHandler?.Invoke(payload, cancellationToken)
                ?? Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Unknown));
        }

        public async Task<WiseOldManCompetitionWriteResult> UpdateAsync(long competitionId, WiseOldManCompetitionWritePayload payload, string verificationCode, CancellationToken cancellationToken = default)
        {
            Updates.Add(payload);
            if (UpdateHandler is not null) return await UpdateHandler(competitionId, payload, verificationCode, cancellationToken);
            return new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Success,
                new WiseOldManCompetition(competitionId, payload.Title, payload.StartsAt, payload.EndsAt, DateTimeOffset.UtcNow, []));
        }

        public Task<WiseOldManCompetitionWriteResult> DeleteAsync(long competitionId, string verificationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WiseOldManCompetitionWriteResult(WiseOldManCompetitionWriteStatus.Success,
                new WiseOldManCompetition(competitionId, "Deleted", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, [])));

        public async Task<WiseOldManUpdateAllResult> UpdateAllAsync(long competitionId, string verificationCode, DateTimeOffset dispatchDeadline, Func<CancellationToken, Task<bool>> recheckEligibility, CancellationToken cancellationToken = default)
        {
            if (BeforeUpdateAllRecheck is not null) await BeforeUpdateAllRecheck(cancellationToken);
            if (!await recheckEligibility(cancellationToken))
                return new(WiseOldManUpdateAllStatus.Validation, ErrorCode: "DispatchEligibilityChanged", Message: "Current event eligibility changed before dispatch.");
            return await UpdateAllCoreAsync(competitionId, verificationCode, cancellationToken);
        }

        private async Task<WiseOldManUpdateAllResult> UpdateAllCoreAsync(long competitionId, string verificationCode, CancellationToken cancellationToken)
        {
            UpdateAllCalls.Enqueue((competitionId, verificationCode));
            if (UpdateAllHandler is not null) return await UpdateAllHandler(competitionId, verificationCode, cancellationToken);
            return new(WiseOldManUpdateAllStatus.Acknowledged, Message: "Controlled update-all acknowledgement.");
        }
    }

    private sealed class RecordingCompetitionClient(Func<long, WiseOldManCompetitionResult> resultFactory) : IWiseOldManCompetitionClient
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);

        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Record(competitionId));

        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default) =>
            Task.FromResult(Record(competitionId));

        private WiseOldManCompetitionResult Record(long competitionId)
        {
            Interlocked.Increment(ref calls);
            return resultFactory(competitionId);
        }
    }

    private sealed class PassthroughCredentialProtector : ICompetitionCredentialProtector
    {
        public string Protect(string verificationCode) => $"protected:{verificationCode}";
        public string Unprotect(string protectedCode) => protectedCode.StartsWith("protected:", StringComparison.Ordinal)
            ? protectedCode["protected:".Length..]
            : protectedCode;
    }

    private sealed class FixedStatus : IWiseOldManStatus
    {
        public WiseOldManRequestStatus GetStatus() => new(20, 19, null, null, null, null, null, null);
    }

    private sealed class ThrowOnceAfterArmingInterceptor : SaveChangesInterceptor
    {
        private int armed;
        private int thrown;
        public bool Thrown => Volatile.Read(ref thrown) == 1;
        public void Arm() => Volatile.Write(ref armed, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 1 && Interlocked.Exchange(ref thrown, 1) == 0)
                throw new DbUpdateException("Transient receipt persistence failure.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset value = now;
        public override DateTimeOffset GetUtcNow() => value;
        public void Advance(TimeSpan amount) => value += amount;
    }
}
