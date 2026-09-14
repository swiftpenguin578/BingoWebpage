using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class C33FinalizationFreshnessTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("c33_freshness").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly DateTimeOffset now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private DbContextOptions<ApplicationDbContext> options = null!;
    private Setup fixture = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        fixture = await SeedAsync(db);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ActualFormsRejectChangedAndReturnedInputsThenReinspectAndFinalizeWithoutReusingHistory()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        await ReviewHttpAsync(client, fixture.First, "Approve");
        var first = await ReadinessAsync();
        var initialInspection = Form(await client.GetStringAsync(FinalizeUrl), "AcknowledgeCompletion", fixture.TeamA);
        var teamBInspection = Form(await client.GetStringAsync(FinalizeUrl), "AcknowledgeCompletion", fixture.TeamB);
        Assert.Equal(Key(first, fixture.TeamA), initialInspection.Fields["ExpectedInspectionKey"]);
        await PostAsync(client, initialInspection);
        // The other displayed form is now version-stale, but its team's inspection identity is unchanged.
        await PostAsync(client, teamBInspection);
        Assert.False(Blocker(await ReadinessAsync(), fixture.TeamB).Resolved);
        await AcknowledgeHttpAsync(client, fixture.TeamB);
        var inspected = await ReadinessAsync();
        Assert.True(Blocker(inspected, fixture.TeamA).Resolved);
        var staleFinalize = Form(await client.GetStringAsync(FinalizeUrl), "Finalize");
        await ReviewHttpAsync(client, fixture.First, "Reverse");
        var incomplete = await ReadinessAsync();
        Assert.False(incomplete.Placements.Single(x => x.TeamId == fixture.TeamA).BoardComplete);
        Assert.True(Blocker(incomplete, fixture.TeamB).Resolved);
        await PostAsync(client, staleFinalize, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await AssertNotFinalizedAsync();
        await PostAsync(client, initialInspection);
        Assert.Equal(2, await ResolutionCountAsync());
        await ReviewHttpAsync(client, fixture.Replacement, "Approve");
        var returned = await ReadinessAsync();
        Assert.Equal(first.ReviewCycleId, returned.ReviewCycleId);
        Assert.Equal(first.Placements.Single(x => x.TeamId == fixture.TeamA), returned.Placements.Single(x => x.TeamId == fixture.TeamA));
        Assert.NotEqual(Key(first, fixture.TeamA), Key(returned, fixture.TeamA));
        Assert.False(Blocker(returned, fixture.TeamA).Resolved);
        Assert.True(Blocker(returned, fixture.TeamB).Resolved);
        await PostAsync(client, initialInspection);
        // Even a forged current version cannot turn an old inspection identity into a new acknowledgment.
        await PostAsync(client, initialInspection, ("ExpectedVersion", returned.EventVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(2, await ResolutionCountAsync());
        var freshInspection = Form(await client.GetStringAsync(FinalizeUrl), "AcknowledgeCompletion", fixture.TeamA);
        await PostAsync(client, freshInspection);
        var afterAck = await ReadinessAsync();
        await PostAsync(client, freshInspection); // exact authorized replay
        Assert.Equal(afterAck.EventVersion, (await ReadinessAsync()).EventVersion);
        Assert.Equal(3, await ResolutionCountAsync());
        await ResolveAllAsync();
        var finalForm = Form(await client.GetStringAsync(FinalizeUrl), "Finalize");
        await PostAsync(client, finalForm, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Finalized, await verify.Events.Select(x => x.State).SingleAsync());
        var snapshot = await verify.EventFinalizations.SingleAsync();
        Assert.Equal(2, await verify.OfficialPlacements.CountAsync());
        var historical = await verify.FinalReviewResolutions.SingleAsync(x => x.BlockerKey == Key(first, fixture.TeamA));
        Assert.DoesNotContain(historical.Id.ToString(), snapshot.ConsumedResolutionIdsJson);
        Assert.Equal(3, await verify.FinalReviewResolutions.CountAsync(x => x.Kind == FinalReviewResolutionKind.CompletionTimeAcknowledgement));
        var published = await client.GetStringAsync($"/Events/{fixture.Slug}/Board");
        Assert.Contains("Team A", published);
        Assert.Contains("Team B", published);
        await PostAsync(client, finalForm, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        Assert.Single(await verify.EventFinalizations.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("Reject")]
    [InlineData("Edit")]
    [InlineData("Approve")]
    public async Task PendingReviewChangesAdvanceEventFreshnessAndRejectOldFinalizeHttp(string handler)
    {
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        var before = await ReadinessAsync();
        var finalForm = Form(await client.GetStringAsync(FinalizeUrl), "Finalize");
        await ReviewHttpAsync(client, fixture.First, handler);
        var after = await ReadinessAsync();
        Assert.Equal(before.EventVersion + 1, after.EventVersion);
        Assert.Equal(Key(before, fixture.TeamB), Key(after, fixture.TeamB));
        await PostAsync(client, finalForm, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await AssertNotFinalizedAsync();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(FinalizeUrl));
        Assert.Contains("This event changed in another session", html);
        Assert.Contains("app-toast-error", html);
        Assert.DoesNotContain("app-toast-success", html);
    }

    [Fact]
    public async Task CompletionCorrectionsAtIdenticalClockRetainOldInspectionsAndRejectChangeAndReturnReplay()
    {
        await ApproveAsync(fixture.First);
        await AcknowledgeAsync(fixture.TeamA);
        await AcknowledgeAsync(fixture.TeamB);
        var first = await ReadinessAsync();
        var correctedAt = now.AddHours(-2).AddMinutes(-30);
        await CorrectAsync(correctedAt, "first correction");
        await AcknowledgeAsync(fixture.TeamA);
        var inspectedCorrection = await ReadinessAsync();
        await CorrectAsync(correctedAt.AddMinutes(1), "next correction");
        await CorrectAsync(correctedAt, "first correction");
        var returned = await ReadinessAsync();
        Assert.NotEqual(Key(inspectedCorrection, fixture.TeamA), Key(returned, fixture.TeamA));
        Assert.False(Blocker(returned, fixture.TeamA).Resolved);
        Assert.True(Blocker(returned, fixture.TeamB).Resolved);
        await using var db = new ApplicationDbContext(options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).AcknowledgeCompletionTimeAsync(fixture.EventId, fixture.TeamA, fixture.Admin, returned.EventVersion, returned.ReviewCycleId, expectedInspectionKey: Key(inspectedCorrection, fixture.TeamA)));
        Assert.Equal(3, await db.FinalReviewResolutions.CountAsync());
        Assert.Equal(3, await db.AuditEntries.CountAsync(x => x.Action == "event.completion_time_corrected"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).CorrectCompletionAsync(fixture.EventId, fixture.TeamA, correctedAt, "first correction", fixture.Admin, first.EventVersion, first.ReviewCycleId));
        await AcknowledgeAsync(fixture.TeamA);
        await ResolveAllAsync();
        await FinalizeAsync();
    }

    [Fact]
    public async Task MissingForgedAndUnauthorizedHttpTokensCannotWriteInspectionOrOfficialState()
    {
        await ApproveAsync(fixture.First);
        await using var factory = Factory();
        using var admin = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(admin, "c33-admin");
        var form = Form(await admin.GetStringAsync(FinalizeUrl), "AcknowledgeCompletion", fixture.TeamA);
        await PostAsync(admin, form, ("ExpectedInspectionKey", ""));
        await PostAsync(admin, form, ("ExpectedReviewCycleId", Guid.NewGuid().ToString()));
        await PostAsync(admin, form, ("teamId", fixture.TeamB.ToString()));
        Assert.Equal(0, await ResolutionCountAsync());
        var noCsrf = new Dictionary<string, string>(form.Fields);
        noCsrf.Remove("__RequestVerificationToken");
        using var csrfDenied = await admin.PostAsync(form.Action, new FormUrlEncodedContent(noCsrf));
        Assert.Equal(HttpStatusCode.BadRequest, csrfDenied.StatusCode);
        using var ordinary = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(ordinary, "c33-player");
        using var forbidden = await ordinary.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Contains(forbidden.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Redirect });
        var finalForm = Form(await admin.GetStringAsync(FinalizeUrl), "Finalize");
        await PostAsync(admin, finalForm, ("ExpectedVersion", ""), ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await AssertNotFinalizedAsync();
        Assert.Equal(0, await ResolutionCountAsync());
    }

    [Fact]
    public async Task UnfinalizeRetainsOfficialAndInspectionHistoryAndOldCycleCannotReplayIntoNewResults()
    {
        await ApproveAsync(fixture.First);
        await AcknowledgeAsync(fixture.TeamA);
        await AcknowledgeAsync(fixture.TeamB);
        await ResolveAllAsync();
        var first = await ReadinessAsync();
        await FinalizeAsync();
        string originalResults;
        Guid firstFinalization;
        await using (var db = new ApplicationDbContext(options))
        {
            var snapshot = await db.EventFinalizations.SingleAsync();
            firstFinalization = snapshot.Id;
            originalResults = snapshot.CalculationResultsJson!;
            var r = (await Finalization(db).GetReadinessAsync(fixture.EventId))!;
            await Finalization(db).UnfinalizeAsync(fixture.EventId, "C33 explicit new review cycle", true, Actor, r.EventVersion);
        }
        var reopened = await ReadinessAsync();
        Assert.NotEqual(first.ReviewCycleId, reopened.ReviewCycleId);
        Assert.NotEqual(Key(first, fixture.TeamA), Key(reopened, fixture.TeamA));
        Assert.False(Blocker(reopened, fixture.TeamA).Resolved);
        await using (var db = new ApplicationDbContext(options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).AcknowledgeCompletionTimeAsync(fixture.EventId, fixture.TeamA, fixture.Admin, reopened.EventVersion, first.ReviewCycleId, Key(first, fixture.TeamA)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).AcknowledgeCompletionTimeAsync(fixture.EventId, fixture.TeamA, fixture.Admin, reopened.EventVersion, reopened.ReviewCycleId, Key(first, fixture.TeamA)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).FinalizeAsync(fixture.EventId, Actor, first.EventVersion));
        }
        await AcknowledgeAsync(fixture.TeamA);
        await AcknowledgeAsync(fixture.TeamB);
        await ResolveAllAsync();
        await FinalizeAsync();
        await using var verify = new ApplicationDbContext(options);
        var old = await verify.EventFinalizations.SingleAsync(x => x.Id == firstFinalization);
        Assert.NotNull(old.UnfinalizedAt);
        Assert.Equal(originalResults, old.CalculationResultsJson);
        Assert.Equal(2, await verify.EventFinalizations.CountAsync());
        Assert.Equal(4, await verify.FinalReviewResolutions.CountAsync(x => x.Kind == FinalReviewResolutionKind.CompletionTimeAcknowledgement));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(verify).FinalizeAsync(fixture.EventId, Actor, first.EventVersion));
        Assert.Equal(2, await verify.EventFinalizations.CountAsync());
    }

    [Theory]
    [InlineData("submission.approved")]
    [InlineData("submission.reversed")]
    [InlineData("event.completion_time_inspected")]
    [InlineData("event.completion_time_corrected")]
    [InlineData("event.finalized")]
    public async Task FaultAfterDatabaseSaveRollsBackFreshnessAndAllRelatedWrites(string action)
    {
        if (action != "submission.approved") await ApproveAsync(fixture.First);
        if (action == "event.finalized") { await AcknowledgeAsync(fixture.TeamA); await AcknowledgeAsync(fixture.TeamB); await ResolveAllAsync(); }
        var before = await DatabaseStateAsync();
        var readiness = await ReadinessAsync();
        var fault = new AfterSaveBoundary(action, throwFailure: true);
        await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(fault).Options))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                switch (action)
                {
                    case "submission.approved": await Submissions(db).ApproveAsync(fixture.First, fixture.Admin); break;
                    case "submission.reversed": await Submissions(db).ReverseAsync(fixture.First, fixture.Admin, "fault reversal"); break;
                    case "event.completion_time_inspected": await Finalization(db).AcknowledgeCompletionTimeAsync(fixture.EventId, fixture.TeamA, fixture.Admin, readiness.EventVersion, readiness.ReviewCycleId, expectedInspectionKey: Key(readiness, fixture.TeamA)); break;
                    case "event.completion_time_corrected": await Finalization(db).CorrectCompletionAsync(fixture.EventId, fixture.TeamA, now.AddHours(-2).AddMinutes(-30), "fault correction", fixture.Admin, readiness.EventVersion, readiness.ReviewCycleId); break;
                    default: await Finalization(db).FinalizeAsync(fixture.EventId, Actor, readiness.EventVersion); break;
                }
            });
            Assert.Equal("C33 injected failure after SQL save", exception.Message);
        }
        Assert.True(fault.Reached.Task.IsCompletedSuccessfully);
        Assert.Equal(before, await DatabaseStateAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReviewAndFinalizationSerializeWithoutPublishingStaleCompetitiveInputs(bool reviewWins)
    {
        await ApproveAsync(fixture.First);
        await AcknowledgeAsync(fixture.TeamA);
        await AcknowledgeAsync(fixture.TeamB);
        await ResolveAllAsync();
        var ready = await ReadinessAsync();
        var boundary = new AfterSaveBoundary(reviewWins ? "submission.reversed" : "event.finalized");
        var waiting = new EventReadBoundary();
        async Task<Exception?> RunAsync(bool review, bool winner)
        {
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>(options);
            if (winner) builder.AddInterceptors(boundary); else builder.AddInterceptors(waiting);
            await using var db = new ApplicationDbContext(builder.Options);
            try
            {
                if (review) await Submissions(db).ReverseAsync(fixture.First, fixture.Admin, "concurrent reversal");
                else await Finalization(db).FinalizeAsync(fixture.EventId, Actor, ready.EventVersion);
                return null;
            }
            catch (Exception ex) { return ex; }
        }
        var winner = RunAsync(reviewWins, true);
        await boundary.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
        var loser = RunAsync(!reviewWins, false);
        await waiting.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
        boundary.Release.TrySetResult();
        Assert.Null(await winner);
        Assert.NotNull(await loser);
        await using var verify = new ApplicationDbContext(options);
        if (reviewWins)
        {
            await AssertNotFinalizedAsync();
            Assert.Equal(SubmissionStatus.Reversed, (await verify.Submissions.SingleAsync(x => x.Id == fixture.First)).Status);
            Assert.False((await ReadinessAsync()).Placements.Single(x => x.TeamId == fixture.TeamA).BoardComplete);
        }
        else
        {
            Assert.Single(await verify.EventFinalizations.ToListAsync());
            Assert.Equal(SubmissionStatus.Approved, (await verify.Submissions.SingleAsync(x => x.Id == fixture.First)).Status);
            Assert.True((await verify.OfficialPlacements.SingleAsync(x => x.TeamId == fixture.TeamA)).BoardComplete);
            Assert.Empty(await verify.ReviewActions.Where(x => x.Action == ReviewActionType.ReverseApproval).ToListAsync());
        }
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Edit")]
    [InlineData("Reverse")]
    public async Task ConcurrentReviewHttpLoserShowsActionableErrorWithoutWritesThenFreshRetrySucceeds(string handler)
    {
        if (handler == "Reverse") await ApproveAsync(fixture.Replacement);
        var waiting = new EventReadBoundary();
        await using var factory = Factory(waiting);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        var detailUrl = $"/Admin/Review/Details/{fixture.Replacement}?eventId={fixture.EventId}&search=C33&status=Pending";
        var form = Form(await client.GetStringAsync(detailUrl), handler);
        var fields = ReviewFields(form);
        var before = await SubmissionStateAsync(fixture.Replacement);
        var version = (await ReadinessAsync()).EventVersion;
        var boundary = new AfterSaveBoundary("submission.rejected");
        async Task RejectWinnerAsync()
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(boundary).Options);
            await Submissions(db).RejectAsync(fixture.First, fixture.Admin, "C33 concurrent winning decision");
        }
        var winner = RejectWinnerAsync();
        await boundary.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Task<HttpResponseMessage> loser;
        try
        {
            loser = client.PostAsync(form.Action, new FormUrlEncodedContent(fields));
            await waiting.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally { boundary.Release.TrySetResult(); }
        await winner;
        using var result = await loser;
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        var location = result.Headers.Location!.OriginalString;
        Assert.StartsWith($"/Admin/Review/Details/{fixture.Replacement}?", location, StringComparison.Ordinal);
        Assert.Contains($"eventId={fixture.EventId}", location);
        Assert.Contains("search=C33", location);
        Assert.Contains("status=Pending", location);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(location));
        AssertReviewConflictFeedback(html);
        Assert.Equal(before, await SubmissionStateAsync(fixture.Replacement));
        Assert.Equal(version + 1, (await ReadinessAsync()).EventVersion);
        await using (var verify = new ApplicationDbContext(options))
            Assert.Equal(SubmissionStatus.Rejected, await verify.Submissions.Where(x => x.Id == fixture.First).Select(x => x.Status).SingleAsync());

        // Explicitly reload and submit the rendered current form, with no automatic retry.
        var fresh = Form(await client.GetStringAsync(location), handler);
        Assert.Equal(form.Fields["Input.ExpectedVersion"], fresh.Fields["Input.ExpectedVersion"]);
        using var retry = await client.PostAsync(fresh.Action, new FormUrlEncodedContent(ReviewFields(fresh)));
        Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        var successHtml = WebUtility.HtmlDecode(await client.GetStringAsync(retry.Headers.Location!));
        Assert.DoesNotContain("This review was not saved", successHtml);
        Assert.DoesNotContain("app-toast-error", successHtml);
        Assert.Contains(handler switch
        {
            "Approve" => "Approved with 1 contribution.",
            "Reject" => "Submission rejected.",
            "Reverse" => "Approval reversed and later contributions recalculated.",
            _ => "Metadata corrected."
        }, successHtml);
        Assert.NotEqual(before, await SubmissionStateAsync(fixture.Replacement));
        Assert.Equal(version + 2, (await ReadinessAsync()).EventVersion);
        await using var after = new ApplicationDbContext(options);
        var expectedAction = handler switch
        {
            "Approve" => ReviewActionType.Approve,
            "Reject" => ReviewActionType.Reject,
            "Reverse" => ReviewActionType.ReverseApproval,
            _ => ReviewActionType.EditMetadata
        };
        Assert.Single(await after.ReviewActions.Where(x => x.SubmissionId == fixture.Replacement && x.Action == expectedAction).ToListAsync());
    }

    [Theory]
    [InlineData("direct-serialization")]
    [InlineData("wrapped-deadlock")]
    [InlineData("ef-concurrency")]
    public async Task ReviewHttpRecognizesDirectAndWrappedExpectedConflictsWithoutRetryOrWrites(string shape)
    {
        var fault = new InjectReviewConflict(shape);
        await using var factory = Factory(fault);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        var form = Form(await client.GetStringAsync($"/Admin/Review/Details/{fixture.Replacement}"), "Reject");
        var before = await DatabaseStateAsync();
        using var result = await client.PostAsync(form.Action, new FormUrlEncodedContent(ReviewFields(form)));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        AssertReviewConflictFeedback(WebUtility.HtmlDecode(await client.GetStringAsync(result.Headers.Location!)));
        Assert.Equal(1, fault.Attempts);
        Assert.Equal(before, await DatabaseStateAsync());
    }

    private static void AssertReviewConflictFeedback(string html)
    {
        Assert.Contains("This review was not saved", html);
        Assert.Contains("Reload the submission, review the latest state, and try again.", html);
        Assert.Contains("app-toast-error", html);
        Assert.DoesNotContain("app-toast-information", html);
        Assert.DoesNotContain("app-toast-success", html);
        Assert.DoesNotContain("likely due to a transient failure", html);
        Assert.DoesNotContain("40001", html);
    }

    private Dictionary<string, string> ReviewFields(RenderedForm form) => new(form.Fields)
    {
        ["Input.Reason"] = "C33 explicit review decision",
        ["Input.BoardTileId"] = fixture.Tile.ToString(),
        ["Input.RequirementId"] = fixture.Requirement.ToString(),
        ["Input.CreditedOsrsCharacterId"] = fixture.CharacterA.ToString()
    };

    private async Task<string> SubmissionStateAsync(Guid submissionId)
    {
        await using var db = new ApplicationDbContext(options);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            submission = await db.Submissions.SingleAsync(x => x.Id == submissionId),
            contributions = await db.SubmissionContributions.Where(x => x.SubmissionId == submissionId).OrderBy(x => x.Id).ToListAsync(),
            reviews = await db.ReviewActions.Where(x => x.SubmissionId == submissionId).OrderBy(x => x.Id).ToListAsync(),
            audits = await db.AuditEntries.Where(x => x.TargetType == "submission" && x.TargetId == submissionId.ToString()).OrderBy(x => x.Id).ToListAsync()
        });
    }

    private string FinalizeUrl => $"/Admin/Events/Finalize/{fixture.EventId}";
    private LifecycleActor Actor => new(fixture.Admin, "c33-admin");
    private EventFinalizationService Finalization(ApplicationDbContext db) => new(db, new PublicBoardService(db, new FixedClock(now)), new FixedClock(now));
    private SubmissionService Submissions(ApplicationDbContext db) => new(db, new UnusedStorage(), new FixedClock(now));
    private static FinalReviewBlocker Blocker(FinalReviewReadiness r, Guid team) => r.Blockers.Single(x => x.TeamId == team && x.IsCompletionTimeAcknowledgement);
    private static string Key(FinalReviewReadiness r, Guid team) => Blocker(r, team).Key;
    private async Task<FinalReviewReadiness> ReadinessAsync() { await using var db = new ApplicationDbContext(options); return (await Finalization(db).GetReadinessAsync(fixture.EventId))!; }
    private async Task<int> ResolutionCountAsync() { await using var db = new ApplicationDbContext(options); return await db.FinalReviewResolutions.CountAsync(); }
    private async Task ApproveAsync(Guid id) { await using var db = new ApplicationDbContext(options); await Submissions(db).ApproveAsync(id, fixture.Admin); }
    private async Task AcknowledgeAsync(Guid team)
    {
        var r = await ReadinessAsync();
        await using var db = new ApplicationDbContext(options);
        await Finalization(db).AcknowledgeCompletionTimeAsync(fixture.EventId, team, fixture.Admin, r.EventVersion, r.ReviewCycleId, expectedInspectionKey: Key(r, team));
    }
    private async Task CorrectAsync(DateTimeOffset at, string reason)
    {
        var r = await ReadinessAsync();
        await using var db = new ApplicationDbContext(options);
        await Finalization(db).CorrectCompletionAsync(fixture.EventId, fixture.TeamA, at, reason, fixture.Admin, r.EventVersion, r.ReviewCycleId);
    }
    private async Task ResolveAllAsync()
    {
        var r = await ReadinessAsync();
        foreach (var blocker in r.Blockers.Where(x => x.CanOverride && !x.Resolved))
        {
            r = await ReadinessAsync();
            await using var db = new ApplicationDbContext(options);
            await Finalization(db).ResolveBlockerAsync(fixture.EventId, blocker.Key, "Controlled C33 fixture override", true, fixture.Admin, r.EventVersion, r.ReviewCycleId);
        }
    }
    private async Task FinalizeAsync()
    {
        var r = await ReadinessAsync();
        Assert.True(r.CanFinalize);
        await using var db = new ApplicationDbContext(options);
        await Finalization(db).FinalizeAsync(fixture.EventId, Actor, r.EventVersion);
    }
    private async Task AssertNotFinalizedAsync()
    {
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(EventState.AwaitingFinalReview, await db.Events.Select(x => x.State).SingleAsync());
        Assert.Empty(await db.EventFinalizations.ToListAsync());
        Assert.Empty(await db.OfficialPlacements.ToListAsync());
        Assert.Empty(await db.AuditEntries.Where(x => x.Action == "event.finalized").ToListAsync());
        Assert.Empty(await db.EventStateTransitions.Where(x => x.ToState == EventState.Finalized).ToListAsync());
    }
    private async Task<string> DatabaseStateAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            events = await db.Events.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.State }).ToListAsync(),
            submissions = await db.Submissions.OrderBy(x => x.Id).Select(x => new { x.Id, x.Status, x.Version, x.ApprovedContribution }).ToListAsync(),
            contributions = await db.SubmissionContributions.OrderBy(x => x.Id).ToListAsync(),
            reviews = await db.ReviewActions.OrderBy(x => x.Id).ToListAsync(),
            audits = await db.AuditEntries.OrderBy(x => x.Id).ToListAsync(),
            resolutions = await db.FinalReviewResolutions.OrderBy(x => x.Id).ToListAsync(),
            corrections = await db.TeamCompletionCorrections.OrderBy(x => x.Id).ToListAsync(),
            finals = await db.EventFinalizations.OrderBy(x => x.Id).ToListAsync(),
            placements = await db.OfficialPlacements.OrderBy(x => x.Id).ToListAsync(),
            transitions = await db.EventStateTransitions.OrderBy(x => x.Id).ToListAsync(),
            notifications = await db.PersonalNotifications.OrderBy(x => x.Id).ToListAsync()
        });
    }

    private WebApplicationFactory<Program> Factory(IInterceptor? interceptor = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(now));
            if (interceptor is not null) services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(interceptor));
        });
    });
    private static async Task LoginAsync(HttpClient client, string user)
    {
        var html = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Input.Username"] = user, ["Input.Password"] = "password", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private sealed record RenderedForm(string Action, Dictionary<string, string> Fields);
    private static RenderedForm Form(string html, string handler, Guid? team = null)
    {
        var form = Regex.Matches(html, @"<form\b[\s\S]*?</form>").Select(x => x.Value).Single(value =>
            Regex.IsMatch(value, "action=\"[^\"]*[?&](?:amp;)?handler=" + handler + "(?:&[^\"]*)?\"") &&
            (team is null || value.Contains($"value=\"{team}\"", StringComparison.Ordinal)));
        var action = WebUtility.HtmlDecode(Regex.Match(form, "action=\"([^\"]+)\"").Groups[1].Value);
        var fields = Regex.Matches(form, @"<input\b[^>]*>").Select(x => x.Value).Where(x => x.Contains("type=\"hidden\"", StringComparison.Ordinal))
            .ToDictionary(x => Regex.Match(x, "name=\"([^\"]+)\"").Groups[1].Value, x => WebUtility.HtmlDecode(Regex.Match(x, "value=\"([^\"]*)\"").Groups[1].Value));
        Assert.NotEmpty(action);
        Assert.NotEmpty(fields["__RequestVerificationToken"]);
        return new(action, fields);
    }
    private static async Task PostAsync(HttpClient client, RenderedForm form, params (string Name, string Value)[] extras)
    {
        var fields = new Dictionary<string, string>(form.Fields);
        foreach (var (name, value) in extras) fields[name] = value;
        using var response = await client.PostAsync(form.Action, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private async Task ReviewHttpAsync(HttpClient client, Guid submission, string handler)
    {
        var form = Form(await client.GetStringAsync($"/Admin/Review/Details/{submission}"), handler);
        await PostAsync(client, form, ("Input.Reason", "C33 review reason"), ("Input.BoardTileId", fixture.Tile.ToString()), ("Input.RequirementId", fixture.Requirement.ToString()), ("Input.CreditedOsrsCharacterId", fixture.CharacterA.ToString()));
        await using var db = new ApplicationDbContext(options);
        Assert.Contains(await db.ReviewActions.Where(x => x.SubmissionId == submission).ToListAsync(), x => x.Action == handler switch
        { "Approve" => ReviewActionType.Approve, "Reverse" => ReviewActionType.ReverseApproval, "Reject" => ReviewActionType.Reject, _ => ReviewActionType.EditMetadata });
    }
    private async Task AcknowledgeHttpAsync(HttpClient client, Guid team) => await PostAsync(client, Form(await client.GetStringAsync(FinalizeUrl), "AcknowledgeCompletion", team));

    private async Task<Setup> SeedAsync(ApplicationDbContext db)
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "c33-admin", "C33-ADMIN", now.AddDays(-10));
        admin.SetGlobalRole(GlobalRole.Admin);
        var player = Account.CreateWebsite(Guid.NewGuid(), "c33-player", "C33-PLAYER", now.AddDays(-10));
        foreach (var account in new[] { admin, player }) account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, false);
        var ev = new BingoEvent(Guid.NewGuid(), "C33 final review", "c33-final-review", "UTC", admin.Id, now.AddDays(-10));
        ev.ConfigureSchedule(now.AddDays(-8), now.AddDays(-5), null, now.AddHours(-4), now.AddHours(-1), 20);
        ev.OpenSignups(now.AddDays(-8)); ev.CloseSignups(now.AddDays(-5)); ev.StartEvent(now.AddHours(-4)); ev.MarkFirstPublic(now.AddDays(-8));
        var board = new Board(Guid.NewGuid(), ev.Id, "C33 board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "C33 objective", "Finish the run", "Show completion", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "One run", true);
        db.AddRange(admin, player, ev, board, tile, requirement);
        var teams = new List<Team>();
        var characters = new List<OsrsCharacter>();
        var submissions = new List<Submission>();
        for (var index = 0; index < 2; index++)
        {
            var team = new Team(Guid.NewGuid(), ev.Id, index == 0 ? "Team A" : "Team B", index == 0 ? "team-a" : "team-b", TeamFormationType.Drafted, null, true);
            team.Finalize(now.AddDays(-2));
            var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, index + 1, now.AddDays(-6), SignupSource.AdminCreated);
            var character = new OsrsCharacter(Guid.NewGuid(), $"C33 Player {index}", $"C33 PLAYER {index}", now.AddDays(-6));
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, now.AddDays(-6), admin.Id, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
            db.AddRange(team, participant, character, assignment, new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now.AddDays(-4), null, "C33 fixture"));
            for (var count = 0; count < (index == 0 ? 2 : 1); count++)
            {
                var submission = new Submission(Guid.NewGuid(), ev.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, player.Id, 1, now.AddHours(index == 0 ? -3 : -2), null, null);
                db.Submissions.Add(submission);
                submissions.Add(submission);
            }
            teams.Add(team); characters.Add(character);
        }
        await BoardApprovalFixture.PublishAsync(db, board, now.AddDays(-2), [tile], [requirement]);
        await Submissions(db).ApproveAsync(submissions[2].Id, admin.Id);
        ev.EndEvent(now.AddHours(-1));
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, EventState.Live, EventState.AwaitingFinalReview, admin.Id, now.AddHours(-1), "C33 fixture ended", effectiveAt: now.AddHours(-1)));
        await db.SaveChangesAsync();
        return new(ev.Id, ev.Slug, admin.Id, teams[0].Id, teams[1].Id, submissions[0].Id, submissions[1].Id, tile.Id, requirement.Id, characters[0].Id);
    }
    private sealed record Setup(Guid EventId, string Slug, Guid Admin, Guid TeamA, Guid TeamB, Guid First, Guid Replacement, Guid Tile, Guid Requirement, Guid CharacterA);
    private sealed class FixedClock(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
    private sealed class UnusedStorage : IEvidenceStorage
    {
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class AfterSaveBoundary(string action, bool throwFailure = false) : SaveChangesInterceptor
    {
        private bool matched;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            matched = data.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added && x.Entity.Action == action);
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default)
        {
            if (!matched) return result;
            Reached.TrySetResult();
            if (throwFailure) throw new InvalidOperationException("C33 injected failure after SQL save");
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }
    private sealed class InjectReviewConflict(string shape) : DbCommandInterceptor
    {
        public int Attempts { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("FROM events", StringComparison.Ordinal) || !command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)) return ValueTask.FromResult(result);
            Attempts++;
            throw shape switch
            {
                "direct-serialization" => new PostgresException("C33 serialization conflict", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure),
                "wrapped-deadlock" => new InvalidOperationException("C33 wrapped provider conflict", new DbUpdateException("C33 save conflict", new PostgresException("C33 deadlock", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected))),
                _ => new DbUpdateConcurrencyException("C33 optimistic concurrency conflict")
            };
        }
    }

    private sealed class EventReadBoundary : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM events", StringComparison.Ordinal) && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)) Reached.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}
