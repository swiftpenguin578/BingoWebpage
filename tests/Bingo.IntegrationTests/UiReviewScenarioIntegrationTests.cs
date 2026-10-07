using System.Net;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Dashboard;
using Bingo.Application.Evidence;
using Bingo.Infrastructure.Events;
using Bingo.Web.HistoricalImport;
using Bingo.Web.Navigation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Routing;
using Xunit.Abstractions;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Evidence;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class UiReviewScenarioIntegrationTests(ITestOutputHelper output, PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 6, 12, 34, 56, TimeSpan.Zero).AddTicks(1234567);
    private static readonly string[] SharedWinnerNames = ["Amber Owls", "Silver Foxes"];
    private static readonly string[] ImportHashFields = ["manifestHash", "inputHash", "importHash"];
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_ur_tests").WithUsername("bingo").WithPassword("bingo_test_password"));
    private readonly string evidence = Path.Combine(Path.GetTempPath(), "bingo-ur-tests-" + Guid.NewGuid());
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync()
    {
        await database.DisposeAsync();
        if (Directory.Exists(evidence)) Directory.Delete(evidence, recursive: true);
    }

    [Theory]
    [InlineData("live", EventState.Live)]
    [InlineData("final-review", EventState.AwaitingFinalReview)]
    public async Task ScenariosRespectRealLifecycleAndPublishedEvidenceBoundaries(string profile, EventState currentState)
    {
        await using var factory = Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        await scope.ServiceProvider.GetRequiredService<CatalogueSnapshotService>()
            .ApplyAsync(Path.Combine(environment.ContentRootPath, CatalogueSnapshotService.DefaultRelativePath));
        var result = await scope.ServiceProvider.GetRequiredService<UiReviewScenarioSeeder>().SeedAsync(profile);
        db.ChangeTracker.Clear();
        var events = await db.Events.AsNoTracking().ToListAsync();
        var current = Assert.Single(events, value => !value.IsHidden && value.State is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized);
        Assert.Equal(currentState, current.State);
        Assert.Equal(result.CurrentEventId, current.Id);
        Assert.All(events, value => Assert.False(value.IsDevelopmentFixture));
        Assert.All(events.Where(value => value.ActualSignupOpenedAt is not null), value => Assert.Equal(value.ActualSignupOpenedAt, value.FirstPublicAt));
        Assert.All(events.Where(value => value.IsHidden), value => Assert.Contains(value.State, new[] { EventState.AwaitingFinalReview, EventState.Finalized, EventState.Archived }));
        Assert.Equal(3, events.Count(value => value.IsHidden));
        Assert.Contains(events, value => value.IsHidden && value.State == EventState.Finalized);
        Assert.All(await db.Accounts.ToListAsync(), value => Assert.False(value.MustChangePassword));
        Assert.Equal(11, await db.Accounts.CountAsync());
        var plainWebsite = await db.Accounts.SingleAsync(value => value.LoginName == "ReviewWebsite");
        Assert.False(await db.EventParticipants.AnyAsync(value => value.AccountId == plainWebsite.Id));
        Assert.True(events.Count(value => !value.IsHidden && value.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed) > 8);
        var expected = new DateTimeOffset(Now.Ticks - Now.Ticks % 10, TimeSpan.Zero);
        Assert.Equal(expected, result.BuiltAt);
        Assert.Equal(expected.AddDays(-90), current.CreatedAt);
        Assert.Equal(expected, await db.Accounts.Where(value => value.LoginName == "ReviewOwner").Select(value => value.PasswordChangedAt).SingleAsync());
        var closed = events.Single(value => value.Slug == "ur-signups-closed");
        var readiness = await scope.ServiceProvider.GetRequiredService<IEventLifecycleService>().GetStartReadinessAsync(closed.Id);
        Assert.Contains(readiness!.Blockers, value => value.Code == "CURRENT_EVENT_EXISTS");
        var owner = await db.Accounts.SingleAsync(value => value.LoginName == "ReviewOwner");
        var refusal = await scope.ServiceProvider.GetRequiredService<ISubmissionService>().ApproveAsync(result.BlockedSubmissionId, owner.Id);
        Assert.NotNull(refusal.BlockingSubmission);
        Assert.Equal(0, refusal.ApprovedContribution);
        var lifecycle = new EventLifecycleService(db, scope.ServiceProvider.GetRequiredService<IEventSignupLifecycleService>(), new ReviewClock());
        var deniedStart = await lifecycle.StartNowAsync(closed.Id, closed.Version, true, "Synthetic boundary check", new LifecycleActor(owner.Id, owner.LoginName));
        Assert.False(deniedStart.Succeeded);
        Assert.Contains(deniedStart.Blockers!, value => value.Code == "CURRENT_EVENT_EXISTS");
        var frozenImportId = events.Single(value => value.Slug == "ur-imported").Id;
        Assert.False(await db.SubmissionContributions.AnyAsync(value => !db.Teams.Any(team => team.Id == value.TeamId && team.EventId == frozenImportId)));
        Assert.True((await db.Boards.SingleAsync(value => value.EventId == current.Id)).PublishedCorrectionInProgress);
        Assert.Equal(2, await db.DraftPublicationRosters.Where(value => db.Teams.Any(team => team.Id == value.TeamId && team.EventId == current.Id && team.AffiliationName != null)).Select(value => value.TeamId).Distinct().CountAsync());
        Assert.True(await db.TeamMemberships.AnyAsync(value => value.LeftAt != null));
        Assert.Equal(3, await db.AuditEntries.CountAsync(value => value.Action == "event.hidden"));
        var outcomes = await db.EventCompetitionSynchronizations.Select(value => value.EndUpdateStatus).ToListAsync();
        Assert.Contains(EventCompetitionEndUpdateStatus.Pending, outcomes);
        Assert.Contains(EventCompetitionEndUpdateStatus.Rejected, outcomes);
        Assert.Contains(EventCompetitionEndUpdateStatus.CouldNotUpdate, outcomes);
        var shell = scope.ServiceProvider.GetRequiredService<SharedShellService>();
        foreach (var username in new[] { "ReviewOwner", "ReviewAdmin" })
        {
            var account = await db.Accounts.SingleAsync(value => value.LoginName == username);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Role, account.GlobalRole!.Value.ToString()) }, "review-proof"));
            var navigation = await shell.GetAsync(user, new RouteValueDictionary { ["page"] = "/Admin/Index" }, CancellationToken.None);
            Assert.Equal(current.Id, navigation.CurrentEvent!.EventId);
            var dashboard = await scope.ServiceProvider.GetRequiredService<IAdminDashboardService>().GetAsync(account.Id);
            // Existing Dashboard card selects Live, otherwise the next preparation event (PR Dashboard contract).
            var dashboardId = currentState == EventState.Live ? current.Id : events.Single(value => value.Slug == "ur-draft").Id;
            Assert.Equal(dashboardId, dashboard.CurrentEvent!.EventId);
            Assert.Equal(4L, dashboard.Statistics.EventsHeld.Value); // One current, two platform archives and one frozen import.
            Assert.DoesNotContain(dashboard.History, value => value.EventId == result.DiscardedEventId);
            Assert.DoesNotContain(dashboard.ParticipationChart, value => value.EventId == result.DiscardedEventId);
            var populations = await scope.ServiceProvider.GetRequiredService<IAdminDashboardService>()
                .GetEventParticipationAsync(account.Id, events.Select(value => value.Id).ToArray());
            Assert.False(populations.ContainsKey(result.DiscardedEventId));
            Assert.DoesNotContain(events.Where(value => value.IsHidden).Select(value => value.Id), value => value == dashboard.CurrentEvent.EventId);
            var design = await shell.GetAdminDesignAsync(user, new RouteValueDictionary { ["page"] = "/Admin/Index" }, CancellationToken.None);
            Assert.DoesNotContain(design.Events, value => value.Id == result.DiscardedEventId);
            if (username == "ReviewAdmin") Assert.DoesNotContain(design.Events, value => value.Hidden);
        }
        var attention = await shell.GetAdminActionsAsync(CancellationToken.None);
        Assert.DoesNotContain(result.DiscardedEventId, attention.EventIds);
        Assert.False(attention.ActionsByEvent.ContainsKey(result.DiscardedEventId));
        Assert.DoesNotContain(attention.Items, value => value.Url.Contains(result.DiscardedEventId.ToString(), StringComparison.Ordinal));
        var discardAudit = await db.AuditEntries.SingleAsync(value => value.EventId == result.DiscardedEventId && value.Action == "event.discarded");
        foreach (var username in new[] { "ReviewAdmin", "ReviewOwner" })
        {
            var actor = await db.Accounts.SingleAsync(value => value.LoginName == username);
            var dashboard = await scope.ServiceProvider.GetRequiredService<IAdminDashboardService>().GetAsync(actor.Id);
            Assert.DoesNotContain(dashboard.History, value => value.EventId == result.DiscardedEventId);
            Assert.DoesNotContain(dashboard.ParticipationChart, value => value.EventId == result.DiscardedEventId);
            Assert.NotEqual(result.DiscardedEventId, dashboard.LatestEndedRecap?.EventId);
            Assert.NotEqual(result.DiscardedEventId, dashboard.CurrentEvent?.EventId);
            Assert.NotEqual(result.DiscardedEventId, dashboard.Statistics.LatestContributionEventId);
            Assert.Equal(4L, dashboard.Statistics.EventsHeld.Value);
            Assert.Equal(dashboard.ParticipationChart.Sum(value => value.Participants.Value), dashboard.Statistics.EventParticipations.Value);
            var login = await LoginAsync(factory, username, disabled: false);
            using var client = login.Client;
            var rendered = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin"));
            Assert.DoesNotContain(events.Single(value => value.Id == result.DiscardedEventId).Name, rendered, StringComparison.Ordinal);
            Assert.DoesNotContain(result.DiscardedEventId.ToString(), rendered, StringComparison.Ordinal);
            var audit = await client.GetStringAsync($"/Admin/Audit?eventId={result.DiscardedEventId}");
            Assert.Contains($"data-audit-entry=\"{discardAudit.Id}\"", audit, StringComparison.Ordinal);
        }
        using var localWom = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("WiseOldMan");
        using var localResponse = await localWom.GetAsync("players/Ur%20Participant");
        Assert.Equal(HttpStatusCode.OK, localResponse.StatusCode); // 127.0.0.1:1 is unserved: only the local handler can answer.
        await AssertPrintedUrlsAsync(factory, result, discardAudit.Id);
        await AssertSeededHistoryAsync(db, events);
        await AssertU2ScenariosAsync(factory, scope.ServiceProvider, db, events, result);

    }

    private static async Task AssertU2ScenariosAsync(WebApplicationFactory<Program> factory, IServiceProvider services,
        ApplicationDbContext db, IReadOnlyList<BingoEvent> events, UiReviewScenarios scenarios)
    {
        var currentRows = events.Where(value => !value.IsHidden && value.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live).ToArray();
        Assert.True(currentRows.Length > 25);
        foreach (var name in new[] { "alpha", "Alpha", "Ægir", "Ørn", "År" }) Assert.Contains(currentRows, value => value.Name == name);
        var noCapacity = events.Single(value => value.Slug == "ur-upcoming-15");
        Assert.Null(noCapacity.ParticipantCap);
        var noDates = events.Single(value => value.Slug == "ur-upcoming-16");
        Assert.Null(noDates.SignupOpensAt); Assert.Null(noDates.SignupClosesAt);
        Assert.Null(noDates.EventStartsAt); Assert.Null(noDates.EventEndsAt); Assert.Null(noDates.SubmissionCutoffAt);
        var cancelled = events.Single(value => value.Slug == "ur-cancelled");
        Assert.Equal(3, await db.EventParticipants.CountAsync(value => value.EventId == cancelled.Id && value.SignupStatus == SignupStatus.Confirmed));
        var postponed = events.Single(value => value.Slug == "ur-draft");
        var failed = events.Single(value => value.Slug == "ur-opening-failed");
        var actions = await services.GetRequiredService<SharedShellService>().GetAdminActionsAsync(CancellationToken.None);
        Assert.True(actions.ActionsByEvent[postponed.Id].ScheduledStartPostponed);
        Assert.True(actions.ActionsByEvent[failed.Id].ScheduledOpeningFailed);
        var attempt = await db.ScheduledEventStartAttempts.SingleAsync(value => value.EventId == postponed.Id);
        Assert.Equal(postponed.EventStartsAt, attempt.ScheduledFor);
        Assert.Equal(scenarios.BuiltAt.AddHours(-1), attempt.AttemptedAt);
        var opening = await db.ScheduledSignupOpeningAttempts.SingleAsync(value => value.EventId == failed.Id);
        Assert.Equal(failed.SignupOpensAt, opening.ScheduledFor);
        Assert.Equal(scenarios.BuiltAt.AddHours(-1), opening.AttemptedAt);

        Assert.False(failed.ScheduledSignupOpeningEnabled);
        Assert.False(opening.Opened); Assert.Null(opening.ResolvedAt);
        Assert.False(attempt.Started); Assert.Null(attempt.ResolvedAt);
        var openingReadiness = (await services.GetRequiredService<IEventReadinessEvaluator>()
            .GetSignupReadinessAsync(failed.Id, SignupOpeningMode.ScheduledExecution, opening.AttemptedAt))!;
        var startReadiness = (await services.GetRequiredService<IEventLifecycleService>().GetStartReadinessAsync(postponed.Id))!;
        var openingOverlap = await EventSignupLifecycleService.CurrentEventBoundaryConflictAsync(db, failed, CancellationToken.None);
        Assert.Null(openingOverlap); // This fixture's configured window does not overlap a public event.
        var openingBlockers = openingReadiness.Blockers.Concat(openingOverlap is null ? [] : [openingOverlap]).ToArray();
        Assert.Equal(openingBlockers.Select(value => value.Code).Order(StringComparer.Ordinal), opening.Blockers);
        Assert.Equal(openingBlockers.Select(value => value.Description), opening.Details);
        var publicWindow = events.Where(value => !value.IsHidden && value.State is EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized)
            .OrderBy(value => value.EventStartsAt).ThenBy(value => value.Name).First();
        var overlapProbe = new BingoEvent(Guid.NewGuid(), "Unpersisted overlap probe", "ur-overlap-probe", null, publicWindow.Timezone,
            publicWindow.EventStartsAt!.Value.AddDays(-2), publicWindow.EventStartsAt.Value.AddDays(-1),
            publicWindow.EventStartsAt, publicWindow.EventEndsAt, publicWindow.EventEndsAt!.Value.AddHours(1), null,
            failed.CreatedByAccountId, scenarios.BuiltAt);
        var conflict = Assert.IsType<ReadinessItem>(await EventSignupLifecycleService.CurrentEventBoundaryConflictAsync(db, overlapProbe, CancellationToken.None));
        Assert.Equal("EVENT_WINDOW_OVERLAP", conflict.Code);
        Assert.Contains(publicWindow.Name, conflict.Description, StringComparison.Ordinal);
        Assert.Equal(startReadiness.Blockers.Select(value => value.Code).Order(StringComparer.Ordinal), attempt.Blockers);
        foreach (var (item, action, title, scheduledFor, descriptions) in new[] {
            (failed, "event.signup_opening_failed", "Scheduled signup opening failed", opening.ScheduledFor, openingBlockers.Select(value => value.Description)),
            (postponed, "event.start_postponed", "Automatic start postponed", attempt.ScheduledFor, startReadiness.Blockers.Select(value => value.Description)) })
        {
            var audit = await db.AuditEntries.SingleAsync(value => value.EventId == item.Id && value.Action == action);
            Assert.Null(audit.ActorAccountId); Assert.Equal("System", audit.ActorUsername);
            Assert.Equal("event", audit.TargetType); Assert.Equal(item.Id.ToString(), audit.TargetId);
            Assert.Equal(scenarios.BuiltAt.AddHours(-1), audit.OccurredAt);
            Assert.Null(audit.BeforeState); Assert.Null(audit.AfterState);
            using var details = JsonDocument.Parse(audit.Details!);
            AssertFields(details.RootElement, "scheduledFor", "blockerCodes");
            Assert.Equal(scheduledFor, details.RootElement.GetProperty("scheduledFor").GetDateTimeOffset());
            var expectedCodes = item == failed ? opening.Blockers : attempt.Blockers;
            Assert.Equal(expectedCodes, details.RootElement.GetProperty("blockerCodes").EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal));
            var notifications = await db.PersonalNotifications.Where(value => value.EventId == item.Id).ToListAsync();
            Assert.Equal(await db.Accounts.Where(value => value.Active && value.AccountType == AccountType.WebsiteAccount
                && (value.GlobalRole == GlobalRole.Admin || value.GlobalRole == GlobalRole.SuperAdmin)).Select(value => value.Id).OrderBy(value => value).ToListAsync(),
                notifications.Select(value => value.RecipientAccountId).Order());
            Assert.All(notifications, value => {
                Assert.Equal(title, value.Title);
                Assert.Equal($"{item.Name}: {string.Join(" ", descriptions)}", value.Detail);
                Assert.Equal($"/Admin/Events/Manage/{item.Id}", value.Route);
                Assert.Equal(audit.OccurredAt, value.CreatedAt); Assert.Null(value.ReadAt);
            });
        }
        var importedEvent = events.Single(value => value.Slug == "ur-imported");
        var importTransition = await db.EventStateTransitions.SingleAsync(value => value.EventId == importedEvent.Id);
        Assert.Equal(EventState.Draft, importTransition.FromState); Assert.Equal(EventState.Archived, importTransition.ToState);
        Assert.Equal("Frozen historical import; no live lifecycle transition.", importTransition.Reason);
        Assert.Equal(importedEvent.ActualEndedAt, importTransition.PerformedAt); Assert.Equal(importTransition.PerformedAt, importTransition.EffectiveAt);
        Assert.False(importTransition.Scheduled);
        Assert.Null(importedEvent.ActualSignupOpenedAt); Assert.Null(importedEvent.ActualSignupClosedAt);
        Assert.Equal(importedEvent.ActualEndedAt, importedEvent.FinalizedAt); Assert.Equal(importedEvent.FinalizedAt, importedEvent.ArchivedAt);
        var importParticipants = await db.EventParticipants.Where(value => value.EventId == importedEvent.Id).ToListAsync();
        Assert.Equal(6, importParticipants.Count);
        var reconstructed = await db.Submissions.SingleAsync(value => value.EventId == importedEvent.Id);
        Assert.Equal(importedEvent.ActualStartedAt, reconstructed.SubmittedAt); // First importer sequence is zero.
        Assert.Equal(reconstructed.SubmittedAt.AddSeconds(1), reconstructed.ReviewedAt);
        Assert.Equal(HistoricalEventImporter.Disclosure, reconstructed.CaptainNote);
        var importActions = await db.ReviewActions.Where(value => value.SubmissionId == reconstructed.Id).OrderBy(value => value.PerformedAt).ToListAsync();
        Assert.Equal(2, importActions.Count);
        Assert.Equal(ReviewActionType.Submitted, importActions[0].Action);
        Assert.Equal(reconstructed.SubmittedAt, importActions[0].PerformedAt);
        Assert.Equal(ReviewActionType.Approve, importActions[1].Action);
        Assert.Equal(reconstructed.ReviewedAt, importActions[1].PerformedAt);
        var importedBoard = await db.Boards.SingleAsync(value => value.EventId == importedEvent.Id);
        var importTiles = await db.BoardTiles.Where(value => value.BoardId == importedBoard.Id).ToListAsync();
        Assert.Equal(4, importTiles.Count);
        Assert.All(importTiles, value => Assert.Equal(HistoricalEventImporter.Disclosure, value.EvidenceInstructionsSnapshot));
        var importedTileTemplates = await db.TileTemplates.Where(value => importTiles.Select(tile => tile.TileTemplateId).Contains(value.Id)).ToListAsync();
        Assert.Equal(4, importedTileTemplates.Count);
        Assert.All(importedTileTemplates, value => Assert.Equal(HistoricalEventImporter.Disclosure, value.EvidenceInstructions));
        var importRequirements = await db.BoardRequirementSnapshots.Where(value => db.BoardTiles.Any(tile => tile.Id == value.BoardTileId && tile.BoardId == importedBoard.Id)).ToListAsync();
        Assert.Equal(4, importRequirements.Count); Assert.All(importRequirements, value => Assert.Equal(1, value.Position));
        var importTemplates = await db.TileTemplateRequirements.Where(value => db.BoardTiles.Any(tile => tile.TileTemplateId == value.TileTemplateId && tile.BoardId == importedBoard.Id)).ToListAsync();
        Assert.Equal(4, importTemplates.Count); Assert.All(importTemplates, value => Assert.Equal(1, value.Position));
        var frozenTiles = await db.BoardApprovalTileSnapshots.Where(value => value.ApprovalSnapshotId == importedBoard.ActiveApprovalSnapshotId).ToListAsync();
        Assert.Equal(4, frozenTiles.Count); Assert.All(frozenTiles, value => Assert.Equal(HistoricalEventImporter.Disclosure, value.EvidenceInstructions));
        var frozenRequirements = await db.BoardApprovalRequirementSnapshots.Where(value => db.BoardApprovalTileSnapshots.Any(tile => tile.Id == value.ApprovalTileSnapshotId && tile.ApprovalSnapshotId == importedBoard.ActiveApprovalSnapshotId)).ToListAsync();
        Assert.Equal(4, frozenRequirements.Count); Assert.All(frozenRequirements, value => Assert.Equal(1, value.Position));
        var importActivities = await db.EventCompetitionCharacterActivities.Where(value => value.EventId == importedEvent.Id).ToListAsync();
        Assert.Equal(6, importActivities.Count);
        Assert.All(importActivities, value =>
        {
            Assert.Equal(importedEvent.ActualEndedAt, value.UpstreamUpdatedAt);
            Assert.Equal(importedEvent.ActualEndedAt, value.FetchedAt);
        });
        Assert.All(importParticipants, value => { Assert.Equal(SignupSource.CsvImport, value.Source); Assert.Null(value.AccountId); });
        var importAudit = await db.AuditEntries.SingleAsync(value => value.EventId == importedEvent.Id);
        Assert.Equal("historical_import.applied", importAudit.Action); Assert.Equal("event", importAudit.TargetType);
        Assert.Equal(importedEvent.CreatedByAccountId, importAudit.ActorAccountId);
        Assert.Equal("ReviewOwner", importAudit.ActorUsername); Assert.Equal(importedEvent.Id.ToString("D"), importAudit.TargetId);
        Assert.Equal(scenarios.BuiltAt, importAudit.OccurredAt); Assert.Null(importAudit.BeforeState); Assert.Null(importAudit.AfterState);
        var importedFinalization = await db.EventFinalizations.SingleAsync(value => value.EventId == importedEvent.Id);
        Assert.Equal(importTransition.Id, importedFinalization.ReviewCycleId);
        Assert.Equal(importedEvent.ActualEndedAt, importedFinalization.FinalizedAt);
        Assert.Equal("[]", importedFinalization.ConsumedResolutionIdsJson);
        using var frozenResults = JsonDocument.Parse(importedFinalization.CalculationResultsJson!);
        AssertFields(frozenResults.RootElement, "counters", "placements");
        Assert.Equal(2, frozenResults.RootElement.GetProperty("counters").GetArrayLength());
        Assert.All(frozenResults.RootElement.GetProperty("placements").EnumerateArray(), value => AssertFields(value, "Slug", "Placement"));
        Assert.Equal(1, await db.SubmissionContributions.CountAsync(value => db.Teams.Any(team => team.Id == value.TeamId && team.EventId == importedEvent.Id)));
        using var provenance = JsonDocument.Parse(importedFinalization.CalculationInputsJson!);
        AssertFields(provenance.RootElement, "sourceEventId", "manifestHash", "inputHash", "importHash");
        using var importDetails = JsonDocument.Parse(importAudit.Details!);
        AssertFields(importDetails.RootElement, "manifestHash", "inputHash", "importHash", "sourceEventId", "counts");
        Assert.Equal(importedEvent.Slug, provenance.RootElement.GetProperty("sourceEventId").GetString());
        var hashes = ImportHashFields.Select(key => provenance.RootElement.GetProperty(key).GetString()!).ToArray();
        Assert.Equal(3, hashes.Distinct(StringComparer.Ordinal).Count()); Assert.All(hashes, hash => Assert.Matches("^[a-f0-9]{64}$", hash));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{hashes[0]}:{hashes[1]}"))), hashes[2]);
        foreach (var key in new[] { "sourceEventId", "manifestHash", "inputHash", "importHash" })
            Assert.Equal(provenance.RootElement.GetProperty(key).GetString(), importDetails.RootElement.GetProperty(key).GetString());
        Assert.Equal(2, importDetails.RootElement.GetProperty("counts").GetProperty("teams").GetInt32());
        Assert.Equal(6, importDetails.RootElement.GetProperty("counts").GetProperty("participants").GetInt32());
        Assert.False(await db.AuditEntries.AnyAsync(value => value.EventId == importedEvent.Id && value.Action.StartsWith("event.")));



        foreach (var username in new[] { "ReviewAdmin", "ReviewOwner" })
        {
            var actor = await db.Accounts.SingleAsync(value => value.LoginName == username);
            var dashboard = await services.GetRequiredService<IAdminDashboardService>().GetAsync(actor.Id);
            var phase = Assert.Single(dashboard.History, value => value.EventId == scenarios.CurrentEventId);
            Assert.Equal(scenarios.Profile == "live" ? EventState.Live : EventState.AwaitingFinalReview, phase.State);
            Assert.True(phase.Provisional);
            var imported = Assert.Single(dashboard.History, value => value.EventId == events.Single(item => item.Slug == "ur-imported").Id);
            Assert.True(imported.IsHistoricalImport);
            Assert.False(imported.ApprovedSubmissions.IsAvailable);
            Assert.True(dashboard.Statistics.ApprovedSubmissions.IsAvailable); // Mixed platform/import history.
            var shared = Assert.Single(dashboard.History, value => value.EventId == events.Single(item => item.Slug == "ur-archived").Id);
            Assert.Equal(SharedWinnerNames, shared.Winners.Select(value => value.TeamName).Order(StringComparer.Ordinal));
            Assert.All(shared.Winners, value => Assert.Equal(1, value.Placement));

            var login = await LoginAsync(factory, username, disabled: false);
            using var client = login.Client;
            Guid[] Rows(string html) => Regex.Matches(html, "<div class=\"tr row[^\"]*\"[^>]*data-event-id=\"([^\"]+)\"").Select(value => Guid.Parse(value.Groups[1].Value)).ToArray();
            var page1 = await client.GetStringAsync("/Admin/Events?view=current");
            var page2 = await client.GetStringAsync("/Admin/Events?view=current&page=2");
            Assert.Equal(25, Rows(page1).Length); Assert.Equal(currentRows.Length - 25, Rows(page2).Length);
            Assert.Equal(currentRows.Select(value => value.Id).Order(), Rows(page1).Concat(Rows(page2)).Order());
            Assert.Contains("aria-label=\"Page 2\"", page1, StringComparison.Ordinal);
            var cancelledHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Events?view=past&phase=cancelled"));
            Assert.Equal(cancelled.Id, Assert.Single(Rows(cancelledHtml)));
            Assert.Contains("3 confirmed", cancelledHtml, StringComparison.Ordinal);
            Assert.Contains("when it was cancelled", cancelledHtml, StringComparison.Ordinal);
            var attentionHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Events?view=current&attention=1"));
            Assert.Contains(postponed.Id, Rows(attentionHtml)); Assert.Contains(failed.Id, Rows(attentionHtml));
            Assert.Contains("Start postponed", attentionHtml, StringComparison.Ordinal);
            Assert.Contains("Signup opening failed", attentionHtml, StringComparison.Ordinal);
            var emptyDetails = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Events?view=current&search=No"));
            Assert.Contains(noCapacity.Id, Rows(emptyDetails)); Assert.Contains(noDates.Id, Rows(emptyDetails));
            Assert.Contains("No capacity set", emptyDetails, StringComparison.Ordinal);
            Assert.Contains("Not scheduled", emptyDetails, StringComparison.Ordinal);
            foreach (var seededAudit in await db.AuditEntries.Where(value => value.Action == "event.signup_opening_failed"
                || value.Action == "event.start_postponed" || value.Action == "historical_import.applied").ToListAsync())
            {
                var auditHtml = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Audit?eventId={seededAudit.EventId}"));
                Assert.Contains($"data-audit-entry=\"{seededAudit.Id}\"", auditHtml, StringComparison.Ordinal);
                Assert.Contains(seededAudit.Action, auditHtml, StringComparison.Ordinal);
                Assert.Contains(seededAudit.ActorUsername!, auditHtml, StringComparison.Ordinal);
            }
            var notificationHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/notifications"));
            foreach (var notification in await db.PersonalNotifications.Where(value => value.RecipientAccountId == actor.Id
                && (value.EventId == failed.Id || value.EventId == postponed.Id)).ToListAsync())
                Assert.Contains(notification.Detail, notificationHtml, StringComparison.Ordinal);
            var historyHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin"));
            Assert.Contains("Imported", historyHtml, StringComparison.Ordinal);
            Assert.Contains(string.Join(" · ", shared.Winners.Select(value => value.TeamName)), historyHtml, StringComparison.Ordinal);

            foreach (var culture in new[] { "en", "da" })
            {
                var token = WebUtility.HtmlDecode(Regex.Match(page1, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
                using var language = await client.PostAsync("/Language", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["culture"] = culture, ["returnUrl"] = "/Admin/Events", ["__RequestVerificationToken"] = token
                }));
                Assert.Equal(HttpStatusCode.Redirect, language.StatusCode);
                Assert.Contains(language.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Culture=", StringComparison.Ordinal));
                var query = "/Admin/Events?view=current&sort=identity&direction=asc";
                var ordered = Rows(await client.GetStringAsync(query)).Concat(Rows(await client.GetStringAsync(query + "&page=2")));
                var expected = currentRows.OrderBy(value => value.Name, StringComparer.Create(CultureInfo.GetCultureInfo(culture), true)).ThenBy(value => value.Id).Select(value => value.Id);
                Assert.Equal(expected, ordered);
            }
        }
    }

    private static async Task AssertSeededHistoryAsync(ApplicationDbContext db, IReadOnlyList<BingoEvent> events)
    {
        var actions = ProductionAuditActions();
        var audits = await db.AuditEntries.AsNoTracking().ToListAsync();
        var transitions = await db.EventStateTransitions.AsNoTracking().ToListAsync();
        var boards = await db.Boards.AsNoTracking().ToListAsync();
        var approvals = await db.BoardApprovalSnapshots.AsNoTracking().ToListAsync();
        foreach (var entry in audits.Where(value => value.Action.StartsWith("event.", StringComparison.Ordinal) || value.Action.StartsWith("board.", StringComparison.Ordinal)))
        {
            Assert.Contains(entry.Action, actions); // Derived from compiled production writers, never the seed or a copied action list.
            var item = events.Single(value => value.Id == entry.EventId);
            var boardEntry = entry.Action.StartsWith("board.", StringComparison.Ordinal);
            Assert.Equal(boardEntry ? "board" : "event", entry.TargetType);
            var board = boards.Single(value => value.EventId == item.Id);
            Assert.Equal((boardEntry ? board.Id : item.Id).ToString(), entry.TargetId);
            if (entry.Action is "event.start_postponed" or "event.signup_opening_failed")
            {
                Assert.Null(entry.BeforeState); Assert.Null(entry.AfterState);
                Assert.Null(entry.ActorAccountId); Assert.Equal("System", entry.ActorUsername);
                using var scheduledDetails = JsonDocument.Parse(entry.Details!);
                AssertFields(scheduledDetails.RootElement, "scheduledFor", "blockerCodes");
                continue; // Dedicated U2 proof above checks exact persisted attempts/recipients.
            }
            using var after = JsonDocument.Parse(entry.AfterState!);
            using var before = entry.BeforeState is null ? null : JsonDocument.Parse(entry.BeforeState);
            var afterJson = after.RootElement;
            if (boardEntry)
            {
                var approval = Assert.Single(approvals, value => value.BoardId == board.Id);
                Assert.Equal(approval.Id, afterJson.GetProperty("activeApprovalSnapshotId").GetGuid());
                if (entry.Action == "board.approved")
                {
                    AssertFields(before!.RootElement, "state", "activeApprovalSnapshotId");
                    AssertFields(afterJson, "state", "activeApprovalSnapshotId", "approvalVersion");
                    Assert.Equal("Draft", before.RootElement.GetProperty("state").GetString());
                    Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("activeApprovalSnapshotId").ValueKind);
                    Assert.Equal("Validated", afterJson.GetProperty("state").GetString());
                    Assert.Equal(approval.Version, afterJson.GetProperty("approvalVersion").GetInt32());
                    Assert.Equal(approval.ApprovedAt, entry.OccurredAt);
                    Assert.Equal($"Approved snapshot {approval.Version}", entry.Details);
                }
                else if (entry.Action == "board.published")
                {
                    AssertFields(before!.RootElement, "state", "activeApprovalSnapshotId");
                    AssertFields(afterJson, "state", "activeApprovalSnapshotId");
                    Assert.Equal("Validated", before.RootElement.GetProperty("state").GetString());
                    Assert.Equal("Published", afterJson.GetProperty("state").GetString());
                    Assert.Equal(approval.Id, before.RootElement.GetProperty("activeApprovalSnapshotId").GetGuid());
                    Assert.Equal(board.PublishedAt, entry.OccurredAt);
                    Assert.Equal($"Published approval snapshot {approval.Id}", entry.Details);
                }
                else
                {
                    Assert.Equal("board.published_correction_started", entry.Action);
                    AssertFields(before!.RootElement, "activeApprovalSnapshotId");
                    AssertFields(afterJson, "activeApprovalSnapshotId", "workingCopy", "reason");
                    Assert.Equal(approval.Id, before.RootElement.GetProperty("activeApprovalSnapshotId").GetGuid());
                    Assert.True(afterJson.GetProperty("workingCopy").GetBoolean());
                    Assert.Equal(entry.Details, afterJson.GetProperty("reason").GetString());
                    Assert.True(board.PublishedCorrectionInProgress);
                }
            }
            else if (entry.Action == "event.created")
            {
                Assert.Null(before);
                AssertFields(afterJson, "Name", "Slug", "Description", "Timezone", "State");
                Assert.Equal(item.Name, afterJson.GetProperty("Name").GetString());
                Assert.Equal(item.Slug, afterJson.GetProperty("Slug").GetString());
                Assert.Equal(item.Description, afterJson.GetProperty("Description").GetString());
                Assert.Equal(item.Timezone, afterJson.GetProperty("Timezone").GetString());
                Assert.Equal((int)EventState.Draft, afterJson.GetProperty("State").GetInt32());
                Assert.Equal(item.CreatedAt, entry.OccurredAt);
                Assert.Equal("Created as a private draft.", entry.Details);
            }
            else if (entry.Action == "event.hidden")
            {
                AssertFields(before!.RootElement, "State", "Version", "HiddenAt", "HiddenByAccountId", "HiddenReason");
                AssertFields(afterJson, "State", "Version", "HiddenAt", "HiddenByAccountId", "HiddenReason");
                Assert.Equal((int)item.State, before.RootElement.GetProperty("State").GetInt32());
                Assert.Equal((int)item.State, afterJson.GetProperty("State").GetInt32());
                Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("HiddenAt").ValueKind);
                Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("HiddenByAccountId").ValueKind);
                Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("HiddenReason").ValueKind);
                Assert.Equal(item.HiddenAt, afterJson.GetProperty("HiddenAt").GetDateTimeOffset());
                Assert.Equal(item.HiddenByAccountId, afterJson.GetProperty("HiddenByAccountId").GetGuid());
                Assert.Equal(item.HiddenReason, afterJson.GetProperty("HiddenReason").GetString());
                Assert.Equal(item.Version, afterJson.GetProperty("Version").GetInt64());
                Assert.Equal(before.RootElement.GetProperty("Version").GetInt64() + 1, afterJson.GetProperty("Version").GetInt64());
                Assert.Equal(item.HiddenAt, entry.OccurredAt);
                Assert.Equal(item.HiddenReason, entry.Details);
            }
            else
            {
                AssertFields(before!.RootElement, "state");
                var from = (EventState)before.RootElement.GetProperty("state").GetInt32();
                var to = (EventState)afterJson.GetProperty("state").GetInt32();
                var row = Assert.Single(transitions, value => value.EventId == item.Id && value.FromState == from && value.ToState == to && value.PerformedAt == entry.OccurredAt);
                Assert.False(row.Scheduled);
                Assert.Equal(row.PerformedAt, row.EffectiveAt);
                Assert.Equal(entry.ActorAccountId, row.PerformedByAccountId);
                Assert.Equal(entry.Details, row.Reason);
                if (entry.Action is "event.signup_opened" or "event.signup_closed")
                {
                    AssertFields(afterJson, "state", "ActualSignupOpenedAt", "ActualSignupClosedAt");
                    Assert.Equal(item.ActualSignupOpenedAt, afterJson.GetProperty("ActualSignupOpenedAt").GetDateTimeOffset());
                    if (entry.Action == "event.signup_opened") Assert.Equal(JsonValueKind.Null, afterJson.GetProperty("ActualSignupClosedAt").ValueKind);
                    else Assert.Equal(item.ActualSignupClosedAt, afterJson.GetProperty("ActualSignupClosedAt").GetDateTimeOffset());
                    Assert.Equal(entry.Action == "event.signup_opened" ? item.ActualSignupOpenedAt : item.ActualSignupClosedAt, row.EffectiveAt);
                }
                else if (entry.Action is "event.started" or "event.ended")
                {
                    AssertFields(afterJson, "state", "ActualStartedAt", "ActualEndedAt");
                    Assert.Equal(item.ActualStartedAt, afterJson.GetProperty("ActualStartedAt").GetDateTimeOffset());
                    if (entry.Action == "event.started") Assert.Equal(JsonValueKind.Null, afterJson.GetProperty("ActualEndedAt").ValueKind);
                    else Assert.Equal(item.ActualEndedAt, afterJson.GetProperty("ActualEndedAt").GetDateTimeOffset());
                    Assert.Equal(entry.Action == "event.started" ? item.ActualStartedAt : item.ActualEndedAt, row.EffectiveAt);
                }
                else AssertFields(afterJson, "state");
            }
        }
        foreach (var board in boards)
        {
            var boardAudits = audits.Where(value => value.TargetType == "board" && value.TargetId == board.Id.ToString()).ToArray();
            if (audits.Any(value => value.EventId == board.EventId && value.Action == "historical_import.applied"))
                Assert.Empty(boardAudits); // Importer publishes its frozen board without platform board audits.
            else if (board.ActiveApprovalSnapshotId is null) Assert.Empty(boardAudits);
            else
            {
                Assert.Single(boardAudits, value => value.Action == "board.approved");
                Assert.Single(boardAudits, value => value.Action == "board.published");
                Assert.Equal(board.PublishedCorrectionInProgress ? 1 : 0, boardAudits.Count(value => value.Action == "board.published_correction_started"));
            }
        }
        foreach (var item in events)
        {
            var chain = transitions.Where(value => value.EventId == item.Id).OrderBy(value => value.PerformedAt).ToArray();
            if (audits.Any(value => value.EventId == item.Id && value.Action == "historical_import.applied"))
            {
                var importedTransition = Assert.Single(chain);
                Assert.Equal(EventState.Draft, importedTransition.FromState); Assert.Equal(EventState.Archived, importedTransition.ToState);
                Assert.Equal(item.State, importedTransition.ToState);
                Assert.Equal("Frozen historical import; no live lifecycle transition.", importedTransition.Reason);
                continue; // Exact import audit/provenance/transition proof above; platform chain below unchanged.
            }
            var state = EventState.Draft;
            foreach (var row in chain)
            {
                Assert.Equal(state, row.FromState);
                state = row.ToState;
                Assert.Single(audits, value => value.EventId == item.Id && value.TargetType == "event" && value.OccurredAt == row.PerformedAt && value.BeforeState != null
                    && JsonDocument.Parse(value.BeforeState).RootElement.TryGetProperty("state", out var from) && from.GetInt32() == (int)row.FromState);
            }
            Assert.Equal(item.State, state); // Missing open/close/start/end/publication/cancel/discard cannot leave a complete chain.
        }
    }

    private static void AssertFields(JsonElement value, params string[] names) =>
        Assert.Equal(names.Order(StringComparer.Ordinal), value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

    private static HashSet<string> ProductionAuditActions()
    {
        // Read ldstr operands in the actual compiled production writers, including async state machines.
        Type[] writers = [typeof(EventCreationService), typeof(EventSignupLifecycleService), typeof(EventLifecycleService),
            typeof(EventFinalizationService), typeof(EventDestructiveLifecycleService), typeof(EventQuarantineService),
            typeof(Bingo.Web.Pages.Admin.Events.BoardModel)];
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(value => value.FieldType == typeof(OpCode))
            .Select(value => (OpCode)value.GetValue(null)!).ToDictionary(value => unchecked((ushort)value.Value));
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var writer in writers) ReadType(writer);
        return result;

        void ReadType(Type type)
        {
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) ReadType(nested);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var il = method.GetMethodBody()?.GetILAsByteArray();
                if (il is null) continue;
                for (var at = 0; at < il.Length;)
                {
                    var code = il[at++] == 0xfe ? codes[(ushort)(0xfe00 | il[at++])] : codes[il[at - 1]];
                    if (code == OpCodes.Ldstr)
                    {
                        var value = method.Module.ResolveString(BitConverter.ToInt32(il, at));
                        if (Regex.IsMatch(value, "^(event|board)\\.[a-z_]+$")) result.Add(value);
                    }
                    at += code.OperandType switch
                    {
                        OperandType.InlineNone => 0,
                        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                        OperandType.InlineVar => 2,
                        OperandType.InlineI8 or OperandType.InlineR => 8,
                        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, at),
                        _ => 4
                    };
                }
            }
        }
    }

    private async Task AssertPrintedUrlsAsync(WebApplicationFactory<Program> factory, UiReviewScenarios scenarios, Guid discardAuditId)
    {
        var links = UiReviewScenarioCatalogue.Links(scenarios, new Uri("http://127.0.0.1:5310"));
        var clients = new Dictionary<string, HttpClient>(StringComparer.Ordinal);
        var authCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var account in scenarios.Accounts)
            {
                var login = await LoginAsync(factory, account.Username, account.Disabled);
                clients.Add(account.Username, login.Client);
                authCookies.Add(account.Username, login.AuthCookie);
            }
            foreach (var link in links)
            {
                using var response = await clients[link.Username].GetAsync(new Uri(link.Url).PathAndQuery);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{scenarios.Profile}: {link.Username} GET {link.Url} returned {(int)response.StatusCode}");
                if (link.Hidden)
                {
                    using var refused = await clients["ReviewAdmin"].GetAsync(new Uri(link.Url).PathAndQuery);
                    Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
                }
            }
            var discardedName = scenarios.Events.Single(value => value.Id == scenarios.DiscardedEventId).Name;
            var hidden = scenarios.Events.Where(value => value.Hidden).ToArray();
            foreach (var username in new[] { "ReviewAdmin", "ReviewOwner", "ReviewParticipant", "ReviewWebsite" })
            {
                var routes = username is "ReviewAdmin" or "ReviewOwner"
                    ? new[] { "/Admin/Events/Index", "/Admin/Index", "/Account/MyEvents" }
                    : new[] { "/", "/Account/MyEvents" };
                foreach (var route in routes)
                {
                    var html = WebUtility.HtmlDecode(await clients[username].GetStringAsync(route));
                    Assert.DoesNotContain(discardedName, html, StringComparison.Ordinal);
                    if (username != "ReviewOwner")
                        Assert.All(hidden, value => Assert.DoesNotContain(value.Name, html, StringComparison.Ordinal));
                }
                if (username is "ReviewAdmin" or "ReviewOwner")
                {
                    // Bypass the fixture cookie jar so its earlier valid selection cannot replace this stale cookie.
                    using var staleClient = factory.CreateClient(new WebApplicationFactoryClientOptions
                    { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("https://localhost") });
                    staleClient.DefaultRequestHeaders.Add("Cookie", $"{authCookies[username]}; {AdminEventSession.CookieName}={scenarios.DiscardedEventId:D}");
                    using var remembered = await staleClient.GetAsync("/Admin/Index");
                    Assert.Equal(HttpStatusCode.OK, remembered.StatusCode);
                    Assert.Contains(remembered.Headers.GetValues("Set-Cookie"), value => value.StartsWith(AdminEventSession.CookieName + "=;", StringComparison.Ordinal));
                    Assert.DoesNotContain($"data-selected-event-id=\"{scenarios.DiscardedEventId}\"", await remembered.Content.ReadAsStringAsync(), StringComparison.Ordinal);
                    var auditRoute = $"/Admin/Audit/Index?eventId={scenarios.DiscardedEventId}";
                    var auditHtml = await clients[username].GetStringAsync(auditRoute);
                    Assert.Contains($"data-audit-entry=\"{discardAuditId}\"", auditHtml, StringComparison.Ordinal);
                    var auditLink = WebUtility.HtmlDecode(Regex.Match(auditHtml, "class=\"admin-audit-entry-link\"[^>]*href=\"([^\"]+)\"").Groups[1].Value);
                    Assert.NotEmpty(auditLink);
                    using var auditEntryResponse = await clients[username].GetAsync(auditLink);
                    Assert.Equal(HttpStatusCode.OK, auditEntryResponse.StatusCode);
                    output.WriteLine($"{scenarios.Profile} {username}: retained discard audit link {auditLink} => {(int)auditEntryResponse.StatusCode}; discarded event absent from Dashboard.");
                    using var discarded = await clients[username].GetAsync($"/Admin/Events/Identity/{scenarios.DiscardedEventId}");
                    Assert.Equal(HttpStatusCode.NotFound, discarded.StatusCode);
                }
            }
            var discardedScenario = scenarios.Events.Single(value => value.Id == scenarios.DiscardedEventId);
            foreach (var route in new[] { $"/Events/{discardedScenario.Slug}/Board", $"/Events/{discardedScenario.Slug}/Teams" })
            {
                using var refused = await clients["ReviewWebsite"].GetAsync(route);
                Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            }
            foreach (var item in hidden)
            {
                foreach (var route in new[] { $"/Events/{item.Slug}/Board", $"/Events/{item.Slug}/Teams", $"/Admin/Events/Identity/{item.Id}" })
                {
                    using var refused = await clients["ReviewAdmin"].GetAsync(route);
                    Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
                }
            }
        }
        finally { foreach (var client in clients.Values) client.Dispose(); }
    }

    private static async Task<(HttpClient Client, string AuthCookie)> LoginAsync(WebApplicationFactory<Program> factory, string username, bool disabled)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var html = await client.GetStringAsync("/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Input.Username"] = username, ["Input.Password"] = UiReviewScenarioSeeder.Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(disabled ? HttpStatusCode.OK : HttpStatusCode.Redirect, login.StatusCode);
        Assert.DoesNotContain("ChangePassword", login.Headers.Location?.ToString() ?? string.Empty, StringComparison.Ordinal);
        if (disabled)
        {
            using var protectedPage = await client.GetAsync("/Account/MyEvents");
            Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
            Assert.Contains("/Account/Login", protectedPage.Headers.Location!.ToString(), StringComparison.Ordinal);
        }
        var cookieName = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name;
        var authCookie = disabled ? string.Empty : Assert.Single(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(cookieName + "=", StringComparison.Ordinal)).Split(';')[0];
        return (client, authCookie);
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Development")
        .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .UseSetting("DevelopmentAdminBootstrap:Enabled", "false")
        .UseSetting("EvidenceStorage:Provider", "Local")
        .UseSetting("EvidenceStorage:LocalPath", evidence)
        .UseSetting("WiseOldMan:DevelopmentFake:Enabled", "true")
        .UseSetting("WiseOldMan:BaseUrl", "http://127.0.0.1:1/")
        .ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new ReviewClock());
            services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, settings => settings.TimeProvider = TimeProvider.System);
        }));
    private sealed class ReviewClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
