using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3DestructiveLifecycleIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("slice3_destructive").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly DateTimeOffset now = new(2026, 7, 27, 18, 0, 0, TimeSpan.Zero);
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
    public async Task SetupHeavyEmptyEventDiscardsToReservedMinimalTombstone()
    {
        var actor = Account.CreateWebsite(Guid.NewGuid(), "Admin", "ADMIN", now); actor.SetGlobalRole(GlobalRole.Admin);
        var eventId = Guid.NewGuid(); var boardId = Guid.NewGuid(); var tileId = Guid.NewGuid(); var requirementId = Guid.NewGuid(); var bannerId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = Draft(eventId, "reserved-discard-slug", actor.Id); item.ConfigurePlanning("Rules", "Buy-in", null, null, null, 2, 2); item.ConfigureSchedule(now.AddDays(1), now.AddDays(2), null, now.AddDays(3), now.AddDays(4), 20); item.ConfigureScheduledSignupOpening(true, []); item.SetBannerAsset(bannerId);
            var form = new SignupForm(Guid.NewGuid(), eventId, now);
            var question = new SignupQuestion(Guid.NewGuid(), form.Id, eventId, "question", "Question", SignupQuestionType.Text, false, 0, null);
            setup.AddRange(actor, item, new EventBannerAsset(bannerId, eventId, "event/banner", "banner.png", "image/png", 10, 1, 1, "checksum", actor.Id, now), new Board(boardId, eventId, "Setup board", 1, 1), new BoardTile(tileId, boardId, Guid.NewGuid(), 0, 0, "Tile", string.Empty, string.Empty, 1, null), new BoardRequirementSnapshot(requirementId, tileId, 0, 1, false, false, "Requirement", true), form, question, new DraftSession(Guid.NewGuid(), eventId, 1), new EvidenceCode(Guid.NewGuid(), eventId, "ABC123", now, actor.Id, now, null), new ScheduledEventStartAttempt(Guid.NewGuid(), eventId, now.AddDays(3), now, false, ["BOARD_NOT_PUBLISHED"]), new ScheduledSignupOpeningAttempt(Guid.NewGuid(), eventId, now.AddDays(1), now, false, ["DESCRIPTION_REQUIRED"]));
            await setup.SaveChangesAsync();
        }
        await using (var mutation = new ApplicationDbContext(options))
        {
            var item = await mutation.Events.SingleAsync(x => x.Id == eventId);
            var result = await new EventDestructiveLifecycleService(mutation, new FixedClock(now)).DiscardAsync(eventId, item.Version, true, new LifecycleActor(actor.Id, "Admin"));
            Assert.True(result.Succeeded, result.Error);
        }
        await using (var repeated = new ApplicationDbContext(options))
        {
            var item = await repeated.Events.SingleAsync(x => x.Id == eventId);
            Assert.False((await new EventDestructiveLifecycleService(repeated, new FixedClock(now)).DiscardAsync(eventId, item.Version, true, new LifecycleActor(actor.Id, "Admin"))).Succeeded);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            var tombstone = await verify.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.Discarded, tombstone.State); Assert.Equal("reserved-discard-slug", tombstone.Slug); Assert.Equal(actor.Id, tombstone.CreatedByAccountId); Assert.Equal(actor.Id, tombstone.DiscardedByAccountId); Assert.Null(tombstone.Description); Assert.Null(tombstone.EventStartsAt); Assert.Null(tombstone.BannerAssetId);
            Assert.Empty(await verify.Boards.Where(x => x.EventId == eventId).ToListAsync()); Assert.Empty(await verify.SignupQuestions.Where(x => x.EventId == eventId).ToListAsync()); Assert.Empty(await verify.SignupForms.Where(x => x.EventId == eventId).ToListAsync()); Assert.Empty(await verify.DraftSessions.Where(x => x.EventId == eventId).ToListAsync()); Assert.Empty(await verify.EventBannerAssets.Where(x => x.EventId == eventId).ToListAsync());
            Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == eventId && x.ToState == EventState.Discarded).ToListAsync()); Assert.Single(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.discarded").ToListAsync());
            verify.Events.Add(Draft(Guid.NewGuid(), "reserved-discard-slug", actor.Id)); await Assert.ThrowsAsync<DbUpdateException>(() => verify.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task DiscardedManagedBannerCleanupPersistsAcrossStorageFailureAndRetriesSafely()
    {
        var actor = Account.CreateWebsite(Guid.NewGuid(), "Cleanup Admin", "CLEANUP ADMIN", now);
        var discardedId = Guid.NewGuid();
        var unrelatedId = Guid.NewGuid();
        var discardedBanner = new EventBannerAsset(Guid.NewGuid(), discardedId, $"{discardedId:N}/banner.png", "banner.png", "image/png", 10, 1, 1, "cleanup", actor.Id, now);
        var unrelatedBanner = new EventBannerAsset(Guid.NewGuid(), unrelatedId, $"{unrelatedId:N}/banner.png", "other.png", "image/png", 10, 1, 1, "other", actor.Id, now);
        var storage = new FlakyStorage(discardedBanner.StorageKey, unrelatedBanner.StorageKey);
        await using (var setup = new ApplicationDbContext(options))
        {
            var discarded = Draft(discardedId, "discard-cleanup", actor.Id); discarded.SetBannerAsset(discardedBanner.Id);
            var unrelated = Draft(unrelatedId, "unrelated-banner", actor.Id); unrelated.SetBannerAsset(unrelatedBanner.Id);
            setup.AddRange(actor, discarded, unrelated, discardedBanner, unrelatedBanner);
            await setup.SaveChangesAsync();
        }

        await using (var mutation = new ApplicationDbContext(options))
        {
            var cleanup = new EventBannerCleanupService(mutation, storage, new FixedClock(now), NullLogger<EventBannerCleanupService>.Instance);
            var item = await mutation.Events.SingleAsync(x => x.Id == discardedId);
            Assert.True((await new EventDestructiveLifecycleService(mutation, new FixedClock(now), cleanup).DiscardAsync(discardedId, item.Version, true, new LifecycleActor(actor.Id, "Cleanup Admin"))).Succeeded);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.Discarded, (await verify.Events.SingleAsync(x => x.Id == discardedId)).State);
            var pending = Assert.Single(await verify.EventBannerCleanups.Where(x => x.EventId == discardedId).ToListAsync());
            Assert.Equal(discardedBanner.StorageKey, pending.StorageKey);
            Assert.NotNull(pending.LastAttemptedAt);
            Assert.True(pending.AttemptCount > 0);
            Assert.Empty(await verify.EventBannerAssets.Where(x => x.EventId == discardedId).ToListAsync());
            Assert.NotNull((await verify.Events.SingleAsync(x => x.Id == unrelatedId)).BannerAssetId);
        }

        storage.FailDeletes = false;
        async Task RetryInNewContext()
        {
            await using var retry = new ApplicationDbContext(options);
            await new EventBannerCleanupService(retry, storage, new FixedClock(now.AddMinutes(1)), NullLogger<EventBannerCleanupService>.Instance).ProcessPendingAsync();
        }
        await Task.WhenAll(RetryInNewContext(), RetryInNewContext());
        await using (var verify = new ApplicationDbContext(options))
            Assert.Empty(await verify.EventBannerCleanups.Where(x => x.EventId == discardedId).ToListAsync());
        Assert.Contains(discardedBanner.StorageKey, storage.Deleted);
        Assert.DoesNotContain(unrelatedBanner.StorageKey, storage.Deleted);

        var missingId = Guid.NewGuid();
        var missingKey = $"{missingId:N}/already-missing.png";
        storage.Allow(missingKey);
        storage.MissingDeletes = true;
        await using (var setup = new ApplicationDbContext(options))
        {
            var missing = Draft(missingId, "missing-cleanup", actor.Id); missing.Discard(actor.Id, now, false);
            setup.AddRange(missing, new EventBannerCleanup(Guid.NewGuid(), missingId, missingKey, now));
            await setup.SaveChangesAsync();
        }
        await using (var retry = new ApplicationDbContext(options))
            await new EventBannerCleanupService(retry, storage, new FixedClock(now.AddMinutes(2)), NullLogger<EventBannerCleanupService>.Instance).ProcessPendingAsync();
        await using (var verify = new ApplicationDbContext(options))
            Assert.Empty(await verify.EventBannerCleanups.Where(x => x.EventId == missingId).ToListAsync());
    }

    [Fact]
    public async Task DiscardDatabaseFailureLeavesNoTombstoneCleanupRecordOrStorageDeletion()
    {
        var actor = Account.CreateWebsite(Guid.NewGuid(), "Rollback cleanup Admin", "ROLLBACK CLEANUP ADMIN", now);
        var eventId = Guid.NewGuid();
        var banner = new EventBannerAsset(Guid.NewGuid(), eventId, $"{eventId:N}/rollback.png", "rollback.png", "image/png", 10, 1, 1, "rollback", actor.Id, now);
        var storage = new FlakyStorage(banner.StorageKey, "unrelated/key");
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = Draft(eventId, "rollback-cleanup", actor.Id); item.SetBannerAsset(banner.Id);
            setup.AddRange(actor, item, banner);
            await setup.SaveChangesAsync();
        }
        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnCleanupInsert()).Options;
        await using (var mutation = new ApplicationDbContext(failingOptions))
        {
            var cleanup = new EventBannerCleanupService(mutation, storage, new FixedClock(now), NullLogger<EventBannerCleanupService>.Instance);
            var item = await mutation.Events.SingleAsync(value => value.Id == eventId);
            var result = await new EventDestructiveLifecycleService(mutation, new FixedClock(now), cleanup).DiscardAsync(eventId, item.Version, true, new LifecycleActor(actor.Id, "Rollback cleanup Admin"));
            Assert.False(result.Succeeded);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.Draft, (await verify.Events.SingleAsync(value => value.Id == eventId)).State);
            Assert.NotNull((await verify.Events.SingleAsync(value => value.Id == eventId)).BannerAssetId);
            Assert.Single(await verify.EventBannerAssets.Where(value => value.EventId == eventId).ToListAsync());
            Assert.Empty(await verify.EventBannerCleanups.Where(value => value.EventId == eventId).ToListAsync());
        }
        Assert.Empty(storage.Deleted);
    }

    [Theory]
    [InlineData("participant")]
    [InlineData("team")]
    [InlineData("event access")]
    [InlineData("submission")]
    [InlineData("evidence")]
    public async Task EveryProtectedCategoryBlocksDiscardWithoutCleanup(string category)
    {
        var actor = Account.CreateWebsite(Guid.NewGuid(), $"Admin-{Guid.NewGuid():N}", $"ADMIN-{Guid.NewGuid():N}", now); actor.SetGlobalRole(GlobalRole.Admin);
        var eventId = Guid.NewGuid(); var board = new Board(Guid.NewGuid(), eventId, "Preserved setup", 1, 1);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(actor, Draft(eventId, $"blocked-{Guid.NewGuid():N}", actor.Id), board);
            if (category == "participant") setup.EventParticipants.Add(new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated, null));
            if (category == "team") setup.Teams.Add(new Team(Guid.NewGuid(), eventId, "Team", "team", TeamFormationType.Preformed, null, false));
            if (category == "event access") setup.AccountEventAccesses.Add(new AccountEventAccess(Guid.NewGuid(), actor.Id, eventId, Guid.NewGuid(), null, now, null, null));
            if (category is "submission" or "evidence")
            {
                var tileId = Guid.NewGuid(); var requirementId = Guid.NewGuid(); var teamId = Guid.NewGuid(); var participantId = Guid.NewGuid(); var characterId = Guid.NewGuid();
                var team = new Team(teamId, eventId, "Submission team", $"submission-team-{eventId:N}", TeamFormationType.Preformed, null, false);
                var participant = new EventParticipant(participantId, eventId, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated, null);
                var character = new OsrsCharacter(characterId, "Retained player", "RETAINED PLAYER", now);
                var tile = new BoardTile(tileId, board.Id, requirementId, 0, 0, "Submission tile", "Description", "Evidence", 1);
                var requirement = new BoardRequirementSnapshot(requirementId, tileId, 0, 1, true, false, "Requirement", true);
                var submission = new Submission(Guid.NewGuid(), eventId, teamId, tileId, requirementId, null, participantId, characterId, character.DisplayName, actor.Id, 1, now, null, null);
                setup.AddRange(team, participant, character, tile, requirement, submission);
                if (category == "evidence") setup.EvidenceAssets.Add(new EvidenceAsset(Guid.NewGuid(), submission.Id, "evidence/key", "proof.png", "image/png", 10, 1, 1, "sum", now, actor.Id, EvidenceAssetRole.OriginalEvidence));
            }
            await setup.SaveChangesAsync();
        }
        await using (var mutation = new ApplicationDbContext(options))
        {
            var item = await mutation.Events.SingleAsync(x => x.Id == eventId); var result = await new EventDestructiveLifecycleService(mutation, new FixedClock(now)).DiscardAsync(eventId, item.Version, true, new LifecycleActor(actor.Id, actor.PublicUsername!)); Assert.False(result.Succeeded); Assert.Contains("protected", result.Error!, StringComparison.OrdinalIgnoreCase);
        }
        await using var verify = new ApplicationDbContext(options); Assert.Equal(EventState.Draft, (await verify.Events.SingleAsync(x => x.Id == eventId)).State); Assert.True(await verify.Boards.AnyAsync(x => x.EventId == eventId)); Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.discarded").ToListAsync());
    }

    [Fact]
    public async Task CancellationArchiveAndArchivedUnfinalizationPreserveHistoryAndCurrentBoundary()
    {
        var actor = Account.CreateWebsite(Guid.NewGuid(), "Lifecycle Admin", "LIFECYCLE ADMIN", now); actor.SetGlobalRole(GlobalRole.Admin);
        var cancelledId = Guid.NewGuid(); var archivedId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var cancelled = Draft(cancelledId, "cancelled-public", actor.Id); cancelled.ConfigureSchedule(now.AddHours(-2), now.AddHours(-1), null, now.AddMinutes(-1), now.AddHours(2), 20); cancelled.MarkFirstPublic(now.AddHours(-2)); cancelled.OpenSignups(now.AddHours(-2)); cancelled.CloseSignups(now.AddHours(-1));
            var archived = Finalized(archivedId, "archived-history", actor.Id); var cycleId = Guid.NewGuid(); var final = new EventFinalizationSnapshot(Guid.NewGuid(), archivedId, 1, now, actor.Id, cycleId);
            setup.AddRange(actor, cancelled, new EventParticipant(Guid.NewGuid(), cancelledId, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated, null), archived, new EventStateTransition(cycleId, archivedId, EventState.Live, EventState.AwaitingFinalReview, actor.Id, now.AddHours(-2), "Ended", effectiveAt: now.AddHours(-2)), final); await setup.SaveChangesAsync();
        }
        await using (var mutation = new ApplicationDbContext(options))
        {
            var cancelled = await mutation.Events.SingleAsync(x => x.Id == cancelledId); var result = await new EventDestructiveLifecycleService(mutation, new FixedClock(now)).CancelAsync(cancelledId, cancelled.Version, true, "Private operational reason", new LifecycleActor(actor.Id, actor.PublicUsername!)); Assert.True(result.Succeeded, result.Error);
        }
        await using (var mutation = new ApplicationDbContext(options)) await new EventFinalizationService(mutation, null!, new FixedClock(now)).ArchiveAsync(archivedId, true, new LifecycleActor(actor.Id, actor.PublicUsername!));
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.Cancelled, (await verify.Events.SingleAsync(x => x.Id == cancelledId)).State); Assert.Single(await verify.EventParticipants.Where(x => x.EventId == cancelledId).ToListAsync()); Assert.Equal("Private operational reason", (await verify.Events.SingleAsync(x => x.Id == cancelledId)).CancellationReason); Assert.Equal(EventState.Archived, (await verify.Events.SingleAsync(x => x.Id == archivedId)).State); Assert.Single(await verify.EventFinalizations.Where(x => x.EventId == archivedId).ToListAsync());
            var publicSignup = new Bingo.Web.Pages.Events.SignupModel(verify, null!, new FixedClock(now)); Assert.IsType<PageResult>(await publicSignup.GetForTestAsync("cancelled-public", CancellationToken.None)); Assert.True(publicSignup.EventView!.Cancelled); Assert.DoesNotContain("Private operational reason", publicSignup.EventView.ToString(), StringComparison.Ordinal);
        }
        var competingId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options)) { var competing = Draft(competingId, "competing-current", actor.Id); competing.ConfigureSchedule(now.AddHours(-2), now.AddHours(-1), null, now.AddMinutes(-30), now.AddHours(2), 20); competing.OpenSignups(now.AddHours(-2)); competing.CloseSignups(now.AddHours(-1)); competing.StartEvent(now.AddMinutes(-30)); setup.Events.Add(competing); await setup.SaveChangesAsync(); }
        await using (var blocked = new ApplicationDbContext(options)) await Assert.ThrowsAsync<InvalidOperationException>(() => new EventFinalizationService(blocked, null!, new FixedClock(now.AddMinutes(1))).UnfinalizeAsync(archivedId, "Blocked correction", true, new LifecycleActor(actor.Id, actor.PublicUsername!)));
        await using (var verify = new ApplicationDbContext(options)) { Assert.Equal(EventState.Archived, (await verify.Events.SingleAsync(x => x.Id == archivedId)).State); Assert.Null((await verify.EventFinalizations.SingleAsync(x => x.EventId == archivedId)).UnfinalizedAt); await verify.Events.Where(x => x.Id == competingId).ExecuteDeleteAsync(); }
        await using (var mutation = new ApplicationDbContext(options)) await new EventFinalizationService(mutation, null!, new FixedClock(now.AddMinutes(1))).UnfinalizeAsync(archivedId, "Correct official history", true, new LifecycleActor(actor.Id, actor.PublicUsername!));
        await using (var verify = new ApplicationDbContext(options)) { Assert.Equal(EventState.AwaitingFinalReview, (await verify.Events.SingleAsync(x => x.Id == archivedId)).State); Assert.NotNull((await verify.EventFinalizations.SingleAsync(x => x.EventId == archivedId)).UnfinalizedAt); }
    }

    [Fact]
    public async Task DiscardRedirectPreservesOneTimeFeedbackForEnhancedAndNativeJourneys()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Discard feedback Admin", "DISCARD FEEDBACK ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "discard-feedback-password"), false, now, incrementVersion: false);
        var enhancedEvent = Draft(Guid.NewGuid(), "enhanced-discard-feedback", admin.Id);
        var nativeEvent = Draft(Guid.NewGuid(), "native-discard-feedback", admin.Id);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, enhancedEvent, nativeEvent);
            await setup.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using var loggedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = admin.PublicUsername!,
            ["Input.Password"] = "discard-feedback-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, loggedIn.StatusCode);

        var enhancedPath = $"/Admin/Events/Manage/{enhancedEvent.Id}";
        var enhancedPage = await client.GetStringAsync(enhancedPath);
        using var enhancedRequest = new HttpRequestMessage(HttpMethod.Post, $"{enhancedPath}?handler=Discard")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["EventVersion"] = InputValue(enhancedPage, "EventVersion"),
                ["ConfirmDestructiveAction"] = "true",
                ["__RequestVerificationToken"] = AntiforgeryToken(enhancedPage)
            })
        };
        enhancedRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
        enhancedRequest.Headers.Add("X-Bingo-Enhanced-Post", "partial");
        using var enhancedResponse = await client.SendAsync(enhancedRequest);
        Assert.True(enhancedResponse.StatusCode == System.Net.HttpStatusCode.NoContent,
            $"Expected enhanced navigation response, got {enhancedResponse.StatusCode}; headers: {enhancedResponse.Headers}");
        var destination = enhancedResponse.Headers.GetValues("X-Bingo-Post-Navigation").Single();
        Assert.Equal("/Admin/Events", destination);
        var enhancedDestination = await client.GetStringAsync(destination);
        Assert.Contains("Event discarded.", enhancedDestination, StringComparison.Ordinal);
        Assert.DoesNotContain("Event discarded.", await client.GetStringAsync(destination), StringComparison.Ordinal);

        var nativePath = $"/Admin/Events/Manage/{nativeEvent.Id}";
        var nativePage = await client.GetStringAsync(nativePath);
        using var nativeResponse = await client.PostAsync($"{nativePath}?handler=Discard", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = InputValue(nativePage, "EventVersion"),
            ["ConfirmDestructiveAction"] = "true",
            ["__RequestVerificationToken"] = AntiforgeryToken(nativePage)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, nativeResponse.StatusCode);
        var nativeDestination = await client.GetStringAsync(nativeResponse.Headers.Location!.OriginalString);
        Assert.Contains("Event discarded.", nativeDestination, StringComparison.Ordinal);
        Assert.DoesNotContain("Event discarded.", await client.GetStringAsync(nativeResponse.Headers.Location!.OriginalString), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrepareDestructiveConfirmationRendersCancellationUiWithoutMutatingEvent()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Prepare cancellation Admin", "PREPARE CANCELLATION ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "prepare-cancellation-password"), false, now, incrementVersion: false);
        var eventItem = Draft(Guid.NewGuid(), "prepare-cancellation", admin.Id);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, eventItem, Participant(eventItem.Id, SignupStatus.Confirmed, 1));
            await setup.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using var loggedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = admin.PublicUsername!,
            ["Input.Password"] = "prepare-cancellation-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, loggedIn.StatusCode);

        var path = $"/Admin/Events/Manage/{eventItem.Id}";
        var manage = await client.GetStringAsync(path);
        using var prepareResponse = await client.PostAsync($"{path}?handler=PrepareDestructiveConfirmation", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryToken(manage)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, prepareResponse.StatusCode);
        Assert.Equal($"{path}?confirm=destructive", prepareResponse.Headers.Location!.OriginalString);

        var confirmation = await client.GetStringAsync(prepareResponse.Headers.Location!.OriginalString);
        Assert.Contains("Cancellation reason", confirmation, StringComparison.Ordinal);
        Assert.Contains("Confirm cancellation", confirmation, StringComparison.Ordinal);
        Assert.Contains("name=\"ConfirmDestructiveAction\"", confirmation, StringComparison.Ordinal);

        await using var verify = new ApplicationDbContext(options);
        var unchanged = await verify.Events.SingleAsync(item => item.Id == eventItem.Id);
        Assert.Equal(EventState.Draft, unchanged.State);
        Assert.Null(unchanged.CancellationReason);
        Assert.Empty(await verify.EventStateTransitions.Where(item => item.EventId == eventItem.Id).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(item => item.EventId == eventItem.Id).ToListAsync());
    }

    [Fact]
    public async Task TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Terminal route Admin", "TERMINAL ROUTE ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "terminal-route-password"), false, now, incrementVersion: false);
        var cancelledId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var finalizedId = Guid.NewGuid();
        var archivedId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var cancelled = Draft(cancelledId, "terminal-cancelled", admin.Id);
            cancelled.Cancel(admin.Id, now, "Preserved terminal history", true);
            var finalized = Finalized(finalizedId, "terminal-finalized", admin.Id);
            var archived = Finalized(archivedId, "terminal-archived", admin.Id); archived.Archive(now);
            setup.AddRange(admin, cancelled, finalized, archived,
                new EventParticipant(participantId, cancelledId, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated, null),
                new Board(Guid.NewGuid(), cancelledId, "Cancelled board", 1, 1),
                new DraftSession(Guid.NewGuid(), cancelledId, 1));
            await setup.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using var loggedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = admin.PublicUsername!,
            ["Input.Password"] = "terminal-route-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, loggedIn.StatusCode);

        var cancelledRoutes = new[]
        {
            $"/Admin/Events/Manage/{cancelledId}?handler=Capacity",
            $"/Admin/Events/Identity/{cancelledId}",
            $"/Admin/Events/Schedule/{cancelledId}",
            $"/Admin/Events/Questions/{cancelledId}",
            $"/Admin/Events/Board/{cancelledId}?handler=Create",
            $"/Admin/Events/Draft/{cancelledId}?handler=Configure",
            $"/Admin/Events/Finalize/{cancelledId}?handler=Resolve"
        };
        foreach (var route in cancelledRoutes)
            await AssertReadOnlyPostAsync(client, cancelledId, route);
        await AssertReadOnlyPostAsync(client, finalizedId, $"/Admin/Events/Identity/{finalizedId}");
        await AssertReadOnlyPostAsync(client, archivedId, $"/Admin/Events/Schedule/{archivedId}");

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Cancelled, (await verify.Events.SingleAsync(value => value.Id == cancelledId)).State);
        Assert.Equal(EventState.Finalized, (await verify.Events.SingleAsync(value => value.Id == finalizedId)).State);
        Assert.Equal(EventState.Archived, (await verify.Events.SingleAsync(value => value.Id == archivedId)).State);
        Assert.Single(await verify.EventParticipants.Where(value => value.EventId == cancelledId).ToListAsync());
        Assert.Single(await verify.Boards.Where(value => value.EventId == cancelledId).ToListAsync());
        Assert.Single(await verify.DraftSessions.Where(value => value.EventId == cancelledId).ToListAsync());
        Assert.Empty(await verify.EventStateTransitions.Where(value => value.EventId == cancelledId).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == cancelledId).ToListAsync());
        Assert.Empty(await verify.PersonalNotifications.Where(value => value.Route == $"/Admin/Events/Manage/{cancelledId}").ToListAsync());
    }

    [Fact]
    public async Task ResumeEventPostReachesLifecycleBoundaryOnlyFromAwaitingFinalReview()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "Resume route Admin", "RESUME ROUTE ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "resume-route-password"), false, now, incrementVersion: false);
        var awaitingId = Guid.NewGuid();
        var archivedId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var awaiting = Draft(awaitingId, "resume-route-awaiting", admin.Id);
            awaiting.ConfigureSchedule(now.AddDays(-3), now.AddDays(-2), null, now.AddHours(-3), now.AddHours(2), 20);
            awaiting.OpenSignups(now.AddDays(-3));
            awaiting.CloseSignups(now.AddDays(-2));
            awaiting.StartEvent(now.AddHours(-3));
            awaiting.EndEvent(now.AddHours(-1));
            var archived = Finalized(archivedId, "resume-route-archived", admin.Id);
            archived.Archive(now);
            setup.AddRange(admin, awaiting, archived);
            await setup.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using var loggedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = admin.PublicUsername!,
            ["Input.Password"] = "resume-route-password",
            ["__RequestVerificationToken"] = AntiforgeryToken(login)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, loggedIn.StatusCode);

        var awaitingPage = await client.GetStringAsync($"/Admin/Events/Manage/{awaitingId}");
        var replacementEnd = DateTimeOffset.UtcNow.AddHours(3);
        using var resumeResponse = await client.PostAsync($"/Admin/Events/Manage/{awaitingId}?handler=ResumeEvent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = InputValue(awaitingPage, "EventVersion"),
            ["ConfirmResumeEvent"] = "true",
            ["ResumeReason"] = "The event ended prematurely during the route test.",
            ["ReplacementEventEndsAt"] = replacementEnd.ToString("O"),
            ["__RequestVerificationToken"] = AntiforgeryToken(awaitingPage)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, resumeResponse.StatusCode);

        await AssertReadOnlyPostAsync(client, archivedId, $"/Admin/Events/Manage/{archivedId}?handler=ResumeEvent");

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Live, (await verify.Events.SingleAsync(value => value.Id == awaitingId)).State);
        Assert.Single(await verify.EventStateTransitions.Where(value => value.EventId == awaitingId && value.ToState == EventState.Live).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(value => value.EventId == awaitingId && value.Action == "event.resumed").ToListAsync());
        Assert.Equal(EventState.Archived, (await verify.Events.SingleAsync(value => value.Id == archivedId)).State);
        Assert.Empty(await verify.EventStateTransitions.Where(value => value.EventId == archivedId && value.ToState == EventState.Live).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == archivedId && value.Action == "event.resumed").ToListAsync());
    }

    [Fact]
    public async Task CancellationNotifiesOnlyActiveOwnedParticipantsAndRollsBackAtomically()
    {
        var eventId = Guid.NewGuid();
        var actor = Account.CreateWebsite(Guid.NewGuid(), "Cancellation Admin", "CANCELLATION ADMIN", now); actor.SetGlobalRole(GlobalRole.Admin);
        var confirmedOwner = Account.CreateWebsite(Guid.NewGuid(), "Confirmed owner", "CONFIRMED OWNER", now);
        var waitingOwner = Account.CreateWebsite(Guid.NewGuid(), "Waiting owner", "WAITING OWNER", now);
        var withdrawnOwner = Account.CreateWebsite(Guid.NewGuid(), "Withdrawn owner", "WITHDRAWN OWNER", now);
        var removedOwner = Account.CreateWebsite(Guid.NewGuid(), "Removed owner", "REMOVED OWNER", now);
        var unrelated = Account.CreateWebsite(Guid.NewGuid(), "Unrelated", "UNRELATED", now);
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "emergency-cancel", "EMERGENCY-CANCEL", now);
        var item = Draft(eventId, "cancel-notification-event", actor.Id); item.MarkFirstPublic(now.AddMinutes(-1));
        var confirmed = Participant(eventId, SignupStatus.Confirmed, 1); confirmed.AssignOwner(confirmedOwner);
        var waiting = Participant(eventId, SignupStatus.WaitingList, 2); waiting.AssignOwner(waitingOwner);
        var withdrawn = Participant(eventId, SignupStatus.Confirmed, 3); withdrawn.AssignOwner(withdrawnOwner); withdrawn.Withdraw(now, "Withdrawn before cancellation");
        var removed = Participant(eventId, SignupStatus.Confirmed, 4); removed.AssignOwner(removedOwner); removed.Withdraw(now, "Withdrawn before cancellation");
        var unowned = Participant(eventId, SignupStatus.Confirmed, 5);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(actor, confirmedOwner, waitingOwner, withdrawnOwner, removedOwner, unrelated, emergency, item, confirmed, waiting, withdrawn, removed, unowned);
            setup.AccountEventAccesses.Add(new AccountEventAccess(Guid.NewGuid(), emergency.Id, eventId, Guid.NewGuid(), null, now, null, null));
            await setup.SaveChangesAsync();
        }

        var version = item.Version;
        async Task<LifecycleMutationResult> CancelOnce()
        {
            await using var mutation = new ApplicationDbContext(options);
            return await new EventDestructiveLifecycleService(mutation, new FixedClock(now)).CancelAsync(eventId, version, true, "Private cancellation reason", new LifecycleActor(actor.Id, actor.PublicUsername!));
        }
        var concurrent = await Task.WhenAll(CancelOnce(), CancelOnce());
        Assert.Single(concurrent, result => result.Succeeded);
        Assert.False((await CancelOnce()).Succeeded);

        await using (var verify = new ApplicationDbContext(options))
        {
            var notifications = await verify.PersonalNotifications.Where(x => x.Title == "event.cancelled").OrderBy(x => x.RecipientAccountId).ToListAsync();
            Assert.Equal(2, notifications.Count);
            Assert.Single(notifications, x => x.RecipientAccountId == confirmedOwner.Id);
            Assert.Single(notifications, x => x.RecipientAccountId == waitingOwner.Id);
            Assert.DoesNotContain(notifications, x => x.RecipientAccountId == withdrawnOwner.Id || x.RecipientAccountId == removedOwner.Id || x.RecipientAccountId == unrelated.Id || x.RecipientAccountId == emergency.Id);
            Assert.All(notifications, notification =>
            {
                Assert.Equal("cancel-notification-event has been cancelled.", notification.Detail);
                Assert.DoesNotContain("Private cancellation reason", notification.Detail, StringComparison.Ordinal);
                Assert.Equal("/Events/cancel-notification-event/Signup", notification.Route);
            });
            Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == eventId && x.ToState == EventState.Cancelled).ToListAsync());
            Assert.Single(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.cancelled").ToListAsync());
        }

        var rollbackEvent = Draft(Guid.NewGuid(), "cancel-notification-rollback", actor.Id);
        var rollbackOwner = Account.CreateWebsite(Guid.NewGuid(), "Rollback owner", "ROLLBACK OWNER", now);
        var rollbackParticipant = Participant(rollbackEvent.Id, SignupStatus.Confirmed, 1); rollbackParticipant.AssignOwner(rollbackOwner);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(rollbackEvent, rollbackOwner, rollbackParticipant);
            await setup.SaveChangesAsync();
        }
        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnNotificationInsert()).Options;
        await using (var mutation = new ApplicationDbContext(failingOptions))
        {
            var failed = await new EventDestructiveLifecycleService(mutation, new FixedClock(now)).CancelAsync(rollbackEvent.Id, rollbackEvent.Version, true, "Private rollback reason", new LifecycleActor(actor.Id, actor.PublicUsername!));
            Assert.False(failed.Succeeded);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(EventState.Draft, (await verify.Events.SingleAsync(x => x.Id == rollbackEvent.Id)).State);
            Assert.Empty(await verify.PersonalNotifications.Where(x => x.RecipientAccountId == rollbackOwner.Id).ToListAsync());
            Assert.Empty(await verify.EventStateTransitions.Where(x => x.EventId == rollbackEvent.Id).ToListAsync());
            Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == rollbackEvent.Id).ToListAsync());
        }
    }

    private BingoEvent Draft(Guid id, string slug, Guid actorId) => new(id, slug, slug, "UTC", actorId, now);
    private EventParticipant Participant(Guid eventId, SignupStatus status, long sequence) => new(Guid.NewGuid(), eventId, status, sequence, now, SignupSource.Website, null);
    private static async Task AssertReadOnlyPostAsync(HttpClient client, Guid eventId, string route)
    {
        var manage = await client.GetStringAsync($"/Admin/Events/Manage/{eventId}");
        using var response = await client.PostAsync(route, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiforgeryToken(manage)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Admin/Events/Manage/{eventId}", response.Headers.Location!.OriginalString);
        var result = await client.GetStringAsync(response.Headers.Location!.OriginalString);
        Assert.Contains("read-only", result, StringComparison.OrdinalIgnoreCase);
    }
    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private static string InputValue(string page, string name) => Regex.Match(page, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
    private BingoEvent Finalized(Guid id, string slug, Guid actorId) { var item = Draft(id, slug, actorId); item.ConfigureSchedule(now.AddHours(-5), now.AddHours(-4), null, now.AddHours(-3), now.AddHours(-2), 20); item.OpenSignups(now.AddHours(-5)); item.CloseSignups(now.AddHours(-4)); item.StartEvent(now.AddHours(-3)); item.EndEvent(now.AddHours(-2)); item.FinalizeResults(now.AddHours(-1)); return item; }
    private sealed class FixedClock(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed class ThrowOnNotificationInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<PersonalNotification>().Any(entry => entry.State == EntityState.Added)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated notification persistence failure."))
                : ValueTask.FromResult(result);
    }
    private sealed class ThrowOnCleanupInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<EventBannerCleanup>().Any(entry => entry.State == EntityState.Added)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated cleanup outbox persistence failure."))
                : ValueTask.FromResult(result);
    }
    private sealed class FlakyStorage(params string[] knownKeys) : IEvidenceStorage
    {
        private readonly HashSet<string> keys = new(knownKeys, StringComparer.Ordinal);
        public bool FailDeletes { get; set; } = true;
        public bool MissingDeletes { get; set; }
        public List<string> Deleted { get; } = [];
        public void Allow(string storageKey) => keys.Add(storageKey);
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new FileNotFoundException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (!keys.Contains(storageKey)) throw new InvalidOperationException("Unexpected storage key.");
            if (MissingDeletes) throw new FileNotFoundException();
            if (FailDeletes) throw new IOException("Simulated storage failure.");
            Deleted.Add(storageKey);
            return Task.CompletedTask;
        }
    }
}
