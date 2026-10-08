using System.Data.Common;
using System.Net;
using System.Text.Json;
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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class C33FinalizationFreshnessTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithLoopbackPort()
        .WithDatabase("c33_freshness").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly DateTimeOffset now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private DbContextOptions<ApplicationDbContext> options = null!;
    private Setup fixture = null!;

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(database);
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetOwnedConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        fixture = await SeedAsync(db);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ActualPublishFormRejectsStaleResultsThenPublishesCalculatedHistory()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        await ReviewHttpAsync(client, fixture.First, "Approve");
        var first = await ReadinessAsync();
        var staleFinalize = Form(await client.GetStringAsync(FinalizeUrl), "Finalize");
        await ReviewHttpAsync(client, fixture.First, "Reverse");
        var changed = await ReadinessAsync();
        Assert.False(changed.Placements.Single(x => x.TeamId == fixture.TeamA).BoardComplete);
        await ReviewHttpAsync(client, fixture.Replacement, "Approve");
        var returned = await ReadinessAsync();
        Assert.True(returned.EventVersion > first.EventVersion);
        Assert.Equal(first.ReviewCycleId, returned.ReviewCycleId);
        foreach (var placement in first.Placements.Where(x => x.TeamId != fixture.TeamA))
            Assert.Equal(placement, returned.Placements.Single(x => x.TeamId == placement.TeamId));
        var firstTeamA = first.Placements.Single(x => x.TeamId == fixture.TeamA);
        var returnedTeamA = returned.Placements.Single(x => x.TeamId == fixture.TeamA);
        Assert.Equal(firstTeamA.Placement, returnedTeamA.Placement);
        Assert.Equal(firstTeamA.BoardComplete, returnedTeamA.BoardComplete);
        Assert.Equal(now.AddHours(-3).AddMinutes(1), returnedTeamA.CurrentScoreReachedAt);
        Assert.NotEqual(firstTeamA.CurrentScoreReachedAt, returnedTeamA.CurrentScoreReachedAt);
        await PostAsync(client, staleFinalize, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await AssertNotFinalizedAsync();
        var staleHtml = WebUtility.HtmlDecode(await client.GetStringAsync(FinalizeUrl));
        Assert.Contains("This event changed in another session", staleHtml, StringComparison.Ordinal);
        Assert.Contains("app-toast-error", staleHtml, StringComparison.Ordinal);
        var finalReadiness = await ReadinessAsync();
        var finalForm = Form(await client.GetStringAsync(FinalizeUrl), "Finalize");
        await PostAsync(client, finalForm, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Archived, await verify.Events.Select(x => x.State).SingleAsync());
        var snapshot = await verify.EventFinalizations.SingleAsync();
        Assert.Equal(2, await verify.OfficialPlacements.CountAsync());
        Assert.Contains("CurrentScoreReachedAt", snapshot.CalculationInputsJson, StringComparison.Ordinal);
        foreach (var placement in finalReadiness.Placements)
            Assert.Equal(placement.CurrentScoreReachedAt, await verify.OfficialPlacements.Where(value => value.TeamId == placement.TeamId)
                .Select(value => value.CurrentScoreReachedAt).SingleAsync());
        var published = await client.GetStringAsync($"/Events/{fixture.Slug}/Board");
        Assert.Contains("Team A", published);
        Assert.Contains("Team B", published);
        await PostAsync(client, finalForm, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        Assert.Single(await verify.EventFinalizations.AsNoTracking().ToListAsync());
        Assert.Empty(await verify.FinalReviewResolutions.ToListAsync());
        Assert.Empty(await verify.TeamCompletionCorrections.ToListAsync());
    }

    [Fact]
    public async Task EvidenceDerivedEqualAndUnequalScoreTimesDriveRankAndPublishExactTie()
    {
        Guid originalB;
        Guid earlyB;
        Guid laterA;
        Guid laterB;
        await using (var setup = new ApplicationDbContext(options))
        {
            // Publish a second, unfinished tile so ranking is driven by evidence-derived
            // score time even when the boards are incomplete.
            var board = await setup.Boards.SingleAsync(x => x.EventId == fixture.EventId);
            var tile = await setup.BoardTiles.SingleAsync(x => x.Id == fixture.Tile);
            var requirement = await setup.BoardRequirementSnapshots.SingleAsync(x => x.Id == fixture.Requirement);
            var oldApproval = await setup.BoardApprovalSnapshots.SingleAsync(x => x.Id == board.ActiveApprovalSnapshotId);
            board.BeginPublishedCorrection();
            board.Resize(1, 2, 1);
            var unfinished = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 1, "Unfinished objective", "Another run", "Show completion", 1m);
            var unfinishedRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), unfinished.Id, 0, 1, true, false, "Another run", true);
            var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, oldApproval.Version + 1, now, fixture.Admin,
                oldApproval.Id, board.Name, 1, 2, 2m, board.CalculationVersion, board.Version, BoardState.Published);
            setup.AddRange(unfinished, unfinishedRequirement, approval);
            foreach (var (sourceTile, sourceRequirement) in new[] { (tile, requirement), (unfinished, unfinishedRequirement) })
            {
                var frozenTile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, sourceTile.Id, sourceTile.TileTemplateId,
                    sourceTile.RowIndex, sourceTile.ColumnIndex, sourceTile.NameSnapshot, sourceTile.DescriptionSnapshot,
                    sourceTile.EvidenceInstructionsSnapshot, sourceTile.EstimatedEhbSnapshot, null);
                setup.AddRange(frozenTile, new BoardApprovalRequirementSnapshot(Guid.NewGuid(), frozenTile.Id, sourceRequirement.Id,
                    sourceRequirement.Position, sourceRequirement.TargetContribution, sourceRequirement.DuplicatesAllowed,
                    sourceRequirement.AllowHigherWeightings, sourceRequirement.CreditedWeight, sourceRequirement.Description, sourceRequirement.ManualObjective));
            }
            await setup.SaveChangesAsync();
            board.ReplacePublishedApproval(approval.Id);

            var sourceA = await setup.Submissions.SingleAsync(x => x.Id == fixture.First);
            var sourceB = await setup.Submissions.SingleAsync(x => x.TeamId == fixture.TeamB);
            originalB = sourceB.Id;
            Submission NewEvidence(Submission source, DateTimeOffset submittedAt) => new(Guid.NewGuid(), fixture.EventId,
                source.TeamId, fixture.Tile, fixture.Requirement, null, source.CreditedParticipantId, source.CreditedOsrsCharacterId,
                source.CreditedCharacterName, fixture.Admin, 1, submittedAt, null, null);
            var earlyEvidenceB = NewEvidence(sourceB, now.AddHours(-3));
            var laterEvidenceA = NewEvidence(sourceA, now.AddHours(-2));
            var laterEvidenceB = NewEvidence(sourceB, now.AddHours(-2));
            setup.AddRange(earlyEvidenceB, laterEvidenceA, laterEvidenceB);
            earlyB = earlyEvidenceB.Id;
            laterA = laterEvidenceA.Id;
            laterB = laterEvidenceB.Id;
            await setup.SaveChangesAsync();
        }

        async Task ReverseAsync(Guid submissionId)
        {
            await using var db = new ApplicationDbContext(options);
            await Submissions(db).ReverseCurrentAsync(submissionId, fixture.Admin, "Changed tie evidence");
        }

        await ReverseAsync(originalB);
        await ApproveAsync(fixture.First);
        await ApproveAsync(earlyB);
        await RejectAsync(fixture.Replacement);
        var initial = await ReadinessAsync();
        Assert.All(initial.Placements, x =>
        {
            Assert.False(x.BoardComplete);
            Assert.Equal(1, x.CompletedTiles);
            Assert.Equal(now.AddHours(-3), x.CurrentScoreReachedAt);
        });
        Assert.Equal(initial.Placements[0].Placement, initial.Placements[1].Placement);
        Assert.DoesNotContain(initial.Blockers, x => x.IsCompletionTimeAcknowledgement);

        await ReverseAsync(fixture.First);
        await ApproveAsync(laterA);
        var broken = await ReadinessAsync();
        Assert.NotEqual(broken.Placements[0].CurrentScoreReachedAt, broken.Placements[1].CurrentScoreReachedAt);
        Assert.NotEqual(broken.Placements[0].Placement, broken.Placements[1].Placement);
        await ReverseAsync(earlyB);
        await ApproveAsync(laterB);
        var returned = await ReadinessAsync();
        Assert.Equal(initial.ReviewCycleId, returned.ReviewCycleId);
        Assert.True(returned.CanFinalize);
        Assert.Equal(returned.Placements[0].Placement, returned.Placements[1].Placement);
        Assert.Equal(returned.Placements[0].CurrentScoreReachedAt, returned.Placements[1].CurrentScoreReachedAt);
        Assert.Equal(now.AddHours(-2), returned.Placements[0].CurrentScoreReachedAt);
        await FinalizeAsync();
        await using var history = new ApplicationDbContext(options);
        Assert.Equal(EventState.Archived, await history.Events.Select(x => x.State).SingleAsync());
        var snapshot = await history.EventFinalizations.SingleAsync();
        Assert.Contains("exactTieExplanations", snapshot.CalculationInputsJson, StringComparison.Ordinal);
        Assert.Equal(returned.Placements[0].Placement, await history.OfficialPlacements
            .Where(x => x.FinalizationId == snapshot.Id && x.TeamId == returned.Placements[0].TeamId)
            .Select(x => x.Placement).SingleAsync());
        Assert.Equal(returned.Placements[0].CurrentScoreReachedAt, await history.OfficialPlacements
            .Where(x => x.FinalizationId == snapshot.Id && x.TeamId == returned.Placements[0].TeamId)
            .Select(x => x.CurrentScoreReachedAt).SingleAsync());
        Assert.Empty(await history.FinalReviewResolutions.ToListAsync());
        Assert.Empty(await history.TeamCompletionCorrections.ToListAsync());
    }

    [Fact]
    public async Task CompletionFactMigrationBackfillsDerivableCurrentFactsAndLeavesLegacyOfficialTimeUnknown()
    {
        var readiness = await ReadinessAsync();
        var teamBPlacement = readiness.Placements.Single(value => value.TeamId == fixture.TeamB);
        var submittedAt = await SubmissionSubmittedAtAsync(fixture.TeamB);
        Assert.Equal(submittedAt, teamBPlacement.CurrentScoreReachedAt);

        var secondObjectiveId = Guid.NewGuid();
        var secondSubmissionId = Guid.NewGuid();
        var secondSubmittedAt = now.AddHours(-1);
        await using (var addObjective = new ApplicationDbContext(options))
        {
            var boardForBackfill = await addObjective.Boards.SingleAsync(value => value.EventId == fixture.EventId);
            var approvalTile = await addObjective.BoardApprovalTileSnapshots.SingleAsync(value => value.ApprovalSnapshotId == boardForBackfill.ActiveApprovalSnapshotId);
            var existing = await addObjective.Submissions.SingleAsync(value => value.TeamId == fixture.TeamB && value.Status == SubmissionStatus.Approved);
            var objective = new BoardRequirementSnapshot(secondObjectiveId, fixture.Tile, 1, 1, true, false, "Second C33 objective", true);
            var approvedObjective = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), approvalTile.Id, objective.Id, 1, 1, true, false, 1, "Second C33 objective", true);
            var secondSubmission = new Submission(secondSubmissionId, fixture.EventId, fixture.TeamB, fixture.Tile, objective.Id, null,
                existing.CreditedParticipantId, existing.CreditedOsrsCharacterId, existing.CreditedCharacterName, fixture.Admin,
                1, secondSubmittedAt, null, null);
            secondSubmission.Approve(1, now);
            addObjective.AddRange(objective, approvedObjective, secondSubmission,
                new SubmissionContribution(Guid.NewGuid(), secondSubmission.Id, fixture.TeamB, objective.Id, null, existing.CreditedParticipantId, 1, now));
            addObjective.TileCompletionFacts.RemoveRange(await addObjective.TileCompletionFacts.Where(value => value.EventId == fixture.EventId).ToListAsync());
            await addObjective.SaveChangesAsync();
        }

        var finalizationId = Guid.NewGuid();
        const string legacyInputs = "legacy-finalization-inputs";
        const string legacyResults = "legacy-finalization-results";
        await using (var legacyHistory = new ApplicationDbContext(options))
        {
            legacyHistory.EventFinalizations.Add(new EventFinalizationSnapshot(finalizationId, fixture.EventId, 1, now, fixture.Admin, readiness.ReviewCycleId,
                "[]", legacyInputs, legacyResults));
            legacyHistory.OfficialPlacements.Add(new OfficialPlacementSnapshot(Guid.NewGuid(), finalizationId, fixture.EventId,
                fixture.TeamB, teamBPlacement.TeamName, teamBPlacement.Placement, teamBPlacement.BoardComplete,
                teamBPlacement.CalculatedCompletedAt, teamBPlacement.CompletedLines, teamBPlacement.CompletedTiles, teamBPlacement.EhbTiebreak));
            await legacyHistory.SaveChangesAsync();
        }

        const string preCompletionFactsMigration = "20260922204859_AddDerivedTileDescriptions";
        await using (var downgrade = new ApplicationDbContext(options))
            await downgrade.GetService<IMigrator>().MigrateAsync(preCompletionFactsMigration);
        await using (var upgrade = new ApplicationDbContext(options))
            await upgrade.GetService<IMigrator>().MigrateAsync();

        await using var verify = new ApplicationDbContext(options);
        var board = await verify.Boards.SingleAsync(value => value.EventId == fixture.EventId);
        var teamAFact = await verify.TileCompletionFacts.SingleAsync(value => value.TeamId == fixture.TeamA && value.ApprovalSnapshotId == board.ActiveApprovalSnapshotId);
        var teamBFact = await verify.TileCompletionFacts.SingleAsync(value => value.TeamId == fixture.TeamB && value.ApprovalSnapshotId == board.ActiveApprovalSnapshotId);
        Assert.False(teamAFact.IsComplete);
        Assert.Null(teamAFact.CompletedAt);
        Assert.Equal("[]", teamAFact.QualifyingContributionsJson);
        Assert.True(teamBFact.IsComplete);
        Assert.Equal(secondSubmittedAt, teamBFact.CompletedAt);
        Assert.Contains(fixture.Requirement.ToString("D"), teamBFact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(secondObjectiveId.ToString("D"), teamBFact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(secondSubmissionId.ToString("D"), teamBFact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);

        var oldOfficial = await verify.OfficialPlacements.SingleAsync(value => value.FinalizationId == finalizationId);
        Assert.Null(oldOfficial.CurrentScoreReachedAt);
        var oldFinalization = await verify.EventFinalizations.SingleAsync(value => value.Id == finalizationId);
        Assert.Equal(legacyInputs, oldFinalization.CalculationInputsJson);
        Assert.Equal(legacyResults, oldFinalization.CalculationResultsJson);
        Assert.Equal(secondSubmittedAt, (await ReadinessAsync()).Placements.Single(value => value.TeamId == fixture.TeamB).CurrentScoreReachedAt);
    }

    [Theory]
    [InlineData("Reject")]
    [InlineData("Edit")]
    [InlineData("Approve")]
    public async Task PendingReviewChangesAdvanceEventFreshnessAndRejectOldFinalizeHttp(string handler)
    {
        await RejectAsync(fixture.Replacement);
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        var before = await ReadinessAsync();
        var finalForm = Form(await client.GetStringAsync(FinalizeUrl), "Finalize");
        await ReviewHttpAsync(client, fixture.First, handler);
        if (handler == "Edit") await ReviewHttpAsync(client, fixture.First, "Reject");
        var after = await ReadinessAsync();
        Assert.Equal(before.EventVersion + (handler == "Edit" ? 2 : 1), after.EventVersion);
        Assert.True(after.CanFinalize);
        await PostAsync(client, finalForm, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await AssertNotFinalizedAsync();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(FinalizeUrl));
        Assert.Contains("This event changed in another session", html);
        Assert.Contains("app-toast-error", html);
        Assert.DoesNotContain("app-toast-success", html);
    }

    [Fact]
    public async Task CalculatedScoreTimeIgnoresRetiredManualCompletionCorrectionAndFreezesOnPublish()
    {
        await ApproveAsync(fixture.First);
        await RejectAsync(fixture.Replacement);
        var readiness = await ReadinessAsync();
        Assert.True(readiness.CanFinalize);
        var expectedTeamATime = await SubmissionSubmittedAtAsync(fixture.TeamA);
        Assert.Equal(expectedTeamATime, readiness.Placements.Single(x => x.TeamId == fixture.TeamA).CurrentScoreReachedAt);
        var correctionCount = await CompletionCorrectionCountAsync();
        await using var db = new ApplicationDbContext(options);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).CorrectCompletionAsync(
            fixture.EventId, fixture.TeamA, now.AddHours(-2).AddMinutes(-30), "retired correction", fixture.Admin,
            readiness.EventVersion, readiness.ReviewCycleId));
        Assert.Contains("retired", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(correctionCount, await CompletionCorrectionCountAsync());
        await FinalizeAsync();
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Archived, await verify.Events.Select(x => x.State).SingleAsync());
        Assert.Equal(expectedTeamATime, await verify.OfficialPlacements.Where(x => x.TeamId == fixture.TeamA)
            .Select(x => x.CurrentScoreReachedAt).SingleAsync());
        Assert.Empty(await verify.TeamCompletionCorrections.ToListAsync());
    }

    [Fact]
    public async Task EvidenceDerivedScoreTimeDifferenceDeterminesRankAndFreezesOfficialSnapshot()
    {
        await ApproveAsync(fixture.First);
        await RejectAsync(fixture.Replacement);
        var before = await ReadinessAsync();
        var teamA = before.Placements.Single(value => value.TeamId == fixture.TeamA);
        var teamB = before.Placements.Single(value => value.TeamId == fixture.TeamB);
        Assert.NotNull(teamA.CalculatedCompletedAt);
        Assert.NotNull(teamB.CalculatedCompletedAt);
        Assert.True(teamA.CalculatedCompletedAt.Value < teamB.CalculatedCompletedAt.Value);
        Assert.True(teamA.Placement < teamB.Placement);
        Assert.NotEqual(teamA.CurrentScoreReachedAt, teamB.CurrentScoreReachedAt);
        await FinalizeAsync();

        await using var verify = new ApplicationDbContext(options);
        var snapshot = await verify.EventFinalizations.SingleAsync();
        using var inputs = JsonDocument.Parse(snapshot.CalculationInputsJson!);
        var teamBInput = inputs.RootElement.GetProperty("teams").EnumerateArray()
            .Single(value => value.GetProperty("TeamId").GetGuid() == fixture.TeamB);
        Assert.Equal(teamB.CurrentScoreReachedAt, teamBInput.GetProperty("CurrentScoreReachedAt").GetDateTimeOffset());
        Assert.Equal(teamB.CurrentScoreReachedAt, await verify.OfficialPlacements
            .Where(value => value.FinalizationId == snapshot.Id && value.TeamId == fixture.TeamB)
            .Select(value => value.CurrentScoreReachedAt).SingleAsync());
        Assert.Empty(await verify.TeamCompletionCorrections.ToListAsync());
        Assert.Empty(await verify.FinalReviewResolutions.ToListAsync());
    }

    [Fact]
    public async Task FinalizationPageExplainsAndDisplaysScoreTimeInEnglishAndDanish()
    {
        await using var factory = Factory();
        using var english = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(english, "c33-admin");
        var englishHtml = WebUtility.HtmlDecode(await english.GetStringAsync(FinalizeUrl));
        Assert.Contains("Rank order: completed boards by effective finish time", englishHtml, StringComparison.Ordinal);
        Assert.Contains(">Score time</th>", englishHtml, StringComparison.Ordinal);
        Assert.Contains(Regex.Matches(englishHtml, "<td data-label=\\\"Score time\\\"><span>(.*?)</span></td>")
            .Cast<Match>(), match => match.Groups[1].Value != "—");

        using var danish = factory.CreateClient(new() { AllowAutoRedirect = false });
        danish.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da");
        await LoginAsync(danish, "c33-admin");
        var danishHtml = WebUtility.HtmlDecode(await danish.GetStringAsync(FinalizeUrl));
        Assert.Contains("Rangorden: fuldførte boards efter gældende sluttid", danishHtml, StringComparison.Ordinal);
        Assert.Contains(">Scoretid</th>", danishHtml, StringComparison.Ordinal);
        Assert.Contains(Regex.Matches(danishHtml, "<td data-label=\\\"Scoretid\\\"><span>(.*?)</span></td>")
            .Cast<Match>(), match => match.Groups[1].Value != "—");
    }

    [Fact]
    public async Task FinalizationHttpBoundaryRejectsForgedInputsAndRetiredReviewMutationsWithoutWrites()
    {
        await ApproveAsync(fixture.First);
        await using var factory = Factory();
        using var admin = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(admin, "c33-admin");
        var form = Form(await admin.GetStringAsync(FinalizeUrl), "Finalize");
        await PostAsync(admin, form);
        await AssertNotFinalizedAsync();
        var noVersion = new Dictionary<string, string>(form.Fields)
        {
            ["ExpectedVersion"] = string.Empty,
            ["FinalizeConfirmation"] = "PUBLISH_OFFICIAL_RESULTS"
        };
        using var missingVersion = await admin.PostAsync(form.Action, new FormUrlEncodedContent(noVersion));
        Assert.Equal(HttpStatusCode.Redirect, missingVersion.StatusCode);
        await AssertNotFinalizedAsync();

        await ReviewHttpAsync(admin, fixture.First, "Reverse");
        await ReviewHttpAsync(admin, fixture.Replacement, "Approve");
        await PostAsync(admin, form, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await AssertNotFinalizedAsync();
        var staleHtml = WebUtility.HtmlDecode(await admin.GetStringAsync(FinalizeUrl));
        Assert.Contains("This event changed in another session", staleHtml, StringComparison.Ordinal);

        var fresh = Form(await admin.GetStringAsync(FinalizeUrl), "Finalize");
        var noCsrf = new Dictionary<string, string>(fresh.Fields)
        {
            ["FinalizeConfirmation"] = "PUBLISH_OFFICIAL_RESULTS"
        };
        noCsrf.Remove("__RequestVerificationToken");
        using var csrfDenied = await admin.PostAsync(fresh.Action, new FormUrlEncodedContent(noCsrf));
        Assert.Equal(HttpStatusCode.BadRequest, csrfDenied.StatusCode);
        using var ordinary = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(ordinary, "c33-player");
        var unauthorized = new Dictionary<string, string>(fresh.Fields) { ["FinalizeConfirmation"] = "PUBLISH_OFFICIAL_RESULTS" };
        using var forbidden = await ordinary.PostAsync(fresh.Action, new FormUrlEncodedContent(unauthorized));
        Assert.Contains(forbidden.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Redirect });
        await AssertNotFinalizedAsync();

        var resolutionCount = await ResolutionCountAsync();
        var correctionCount = await CompletionCorrectionCountAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).AcknowledgeCompletionTimeAsync(fixture.EventId, fixture.TeamA, fixture.Admin));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).CorrectCompletionAsync(fixture.EventId, fixture.TeamA, now, "retired", fixture.Admin));
            await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).ResolveBlockerAsync(fixture.EventId, "retired", "retired", true, fixture.Admin));
        }
        Assert.Equal(resolutionCount, await ResolutionCountAsync());
        Assert.Equal(correctionCount, await CompletionCorrectionCountAsync());

        await PostAsync(admin, fresh, ("FinalizeConfirmation", "PUBLISH_OFFICIAL_RESULTS"));
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Archived, await verify.Events.Select(x => x.State).SingleAsync());
        Assert.Single(await verify.EventFinalizations.ToListAsync());
        Assert.Equal(resolutionCount, await verify.FinalReviewResolutions.CountAsync());
        Assert.Equal(correctionCount, await verify.TeamCompletionCorrections.CountAsync());
    }

    [Fact]
    public async Task UnfinalizeRetainsOfficialHistoryAndRejectsOldCycleBeforeNewPublication()
    {
        await ApproveAsync(fixture.First);
        await RejectAsync(fixture.Replacement);
        var first = await ReadinessAsync();
        await FinalizeAsync();
        string originalResults;
        DateTimeOffset? originalScoreTime;
        Guid firstFinalization;
        await using (var db = new ApplicationDbContext(options))
        {
            var snapshot = await db.EventFinalizations.SingleAsync();
            firstFinalization = snapshot.Id;
            originalResults = snapshot.CalculationResultsJson!;
            originalScoreTime = await db.OfficialPlacements.Where(value => value.FinalizationId == snapshot.Id && value.TeamId == fixture.TeamA)
                .Select(value => value.CurrentScoreReachedAt).SingleAsync();
            var r = (await Finalization(db).GetReadinessAsync(fixture.EventId))!;
            await Finalization(db).UnfinalizeAsync(fixture.EventId, "C33 explicit new review cycle", true, Actor, r.EventVersion);
        }
        var reopened = await ReadinessAsync();
        Assert.NotEqual(first.ReviewCycleId, reopened.ReviewCycleId);
        Assert.True(reopened.CanFinalize);
        await using (var db = new ApplicationDbContext(options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(db).FinalizeAsync(fixture.EventId, Actor, first.EventVersion));
        }
        await FinalizeAsync();
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Archived, await verify.Events.Select(x => x.State).SingleAsync());
        var old = await verify.EventFinalizations.SingleAsync(x => x.Id == firstFinalization);
        Assert.NotNull(old.UnfinalizedAt);
        Assert.Equal(originalResults, old.CalculationResultsJson);
        Assert.Equal(originalScoreTime, await verify.OfficialPlacements.Where(value => value.FinalizationId == old.Id && value.TeamId == fixture.TeamA)
            .Select(value => value.CurrentScoreReachedAt).SingleAsync());
        Assert.Equal(2, await verify.EventFinalizations.CountAsync());
        Assert.NotEqual(firstFinalization, await verify.EventFinalizations.Where(x => x.Id != firstFinalization).Select(x => x.Id).SingleAsync());
        Assert.Empty(await verify.FinalReviewResolutions.ToListAsync());
        Assert.Empty(await verify.TeamCompletionCorrections.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Finalization(verify).FinalizeAsync(fixture.EventId, Actor, first.EventVersion));
        Assert.Equal(2, await verify.EventFinalizations.CountAsync());
    }

    [Theory]
    [InlineData("submission.approved")]
    [InlineData("submission.reversed")]
    [InlineData("event.results_published")]
    public async Task FaultAfterDatabaseSaveRollsBackFreshnessAndAllRelatedWrites(string action)
    {
        if (action is "submission.reversed" or "event.results_published") await ApproveAsync(fixture.First);
        if (action == "event.results_published") await RejectAsync(fixture.Replacement);
        var before = await DatabaseStateAsync();
        var readiness = await ReadinessAsync();
        var fault = new AfterSaveBoundary(action, throwFailure: true);
        await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(fault).Options))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                switch (action)
                {
                    case "submission.approved": await Submissions(db).ApproveCurrentAsync(fixture.First, fixture.Admin); break;
                    case "submission.reversed": await Submissions(db).ReverseCurrentAsync(fixture.First, fixture.Admin, "fault reversal"); break;
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
        await RejectAsync(fixture.Replacement);
        var ready = await ReadinessAsync();
        var boundary = new AfterSaveBoundary(reviewWins ? "submission.reversed" : "event.results_published");
        var waiting = new EventReadBoundary();
        async Task<Exception?> RunAsync(bool review, bool winner)
        {
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>(options);
            if (winner) builder.AddInterceptors(boundary); else builder.AddInterceptors(waiting);
            await using var db = new ApplicationDbContext(builder.Options);
            try
            {
                if (review) await Submissions(db).ReverseCurrentAsync(fixture.First, fixture.Admin, "concurrent reversal");
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
            Assert.Empty(await verify.FinalReviewResolutions.ToListAsync());
        }
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Edit")]
    [InlineData("Reverse")]
    public async Task ConcurrentReviewHttpLoserShowsActionableErrorWithoutWritesThenFreshRetrySucceeds(string handler)
    {
        var concurrentWinner = fixture.First;
        if (handler == "Reverse")
        {
            await RejectAsync(fixture.First);
            await ApproveAsync(fixture.Replacement);
            await using var seed = new ApplicationDbContext(options);
            var source = await seed.Submissions.SingleAsync(x => x.Id == fixture.First);
            var pendingWinner = new Submission(Guid.NewGuid(), source.EventId, source.TeamId, source.BoardTileId,
                source.RequirementId, source.DropSnapshotId, source.CreditedParticipantId, source.CreditedOsrsCharacterId,
                source.CreditedCharacterName, source.SubmittedByAccountId, source.ClaimedWeight,
                source.SubmittedAt.AddMinutes(30), null, source.ExpectedEvidenceCode);
            seed.Submissions.Add(pendingWinner);
            await seed.SaveChangesAsync();
            concurrentWinner = pendingWinner.Id;
        }
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
            await Submissions(db).RejectCurrentAsync(concurrentWinner, fixture.Admin, "C33 concurrent winning decision");
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
            Assert.Equal(SubmissionStatus.Rejected, await verify.Submissions.Where(x => x.Id == concurrentWinner).Select(x => x.Status).SingleAsync());

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

    [Fact]
    public async Task ReviewApprovalRefusalRendersEarlierSubmissionLink()
    {
        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {now.AddHours(-3)} WHERE id = {fixture.First}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {now.AddHours(-2)} WHERE id = {fixture.Replacement}");
        }

        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        var before = await SubmissionStateAsync(fixture.Replacement);
        var form = Form(await client.GetStringAsync($"/Admin/Review/Details/{fixture.Replacement}?eventId={fixture.EventId}&search=C33&status=Pending"), "Approve");
        using var response = await client.PostAsync(form.Action, new FormUrlEncodedContent(form.Fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location!));

        Assert.Contains("Earlier upload must be resolved first", html, StringComparison.Ordinal);
        Assert.Contains("Approve or reject the earlier upload before approving this one.", html, StringComparison.Ordinal);
        Assert.Contains($"/Admin/Review/Details/{fixture.First}", html, StringComparison.Ordinal);
        Assert.Equal(before, await SubmissionStateAsync(fixture.Replacement));
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SubmissionStatus.Pending, await verify.Submissions.Where(x => x.Id == fixture.Replacement).Select(x => x.Status).SingleAsync());
        Assert.Empty(await verify.ReviewActions.Where(x => x.SubmissionId == fixture.Replacement && x.Action == ReviewActionType.Approve).ToListAsync());
    }

    [Fact]
    public async Task ReviewDetailsShowsPausedIntervalsAsScreenshotTimeGuidance()
    {
        var pausedAt = now.AddHours(-2.5);
        var resumedAt = now.AddHours(-1.5);
        await using (var db = new ApplicationDbContext(options))
        {
            db.EventStateTransitions.AddRange(
                new EventStateTransition(Guid.NewGuid(), fixture.EventId, EventState.AwaitingFinalReview, EventState.Live, fixture.Admin,
                    resumedAt, "C33 paused interval resumed", effectiveAt: resumedAt),
                new EventStateTransition(Guid.NewGuid(), fixture.EventId, EventState.Live, EventState.AwaitingFinalReview, fixture.Admin,
                    pausedAt, "C33 paused interval started", effectiveAt: pausedAt));
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {now.AddHours(-2)} WHERE id = {fixture.Replacement}");
            await db.SaveChangesAsync();
        }

        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Review/Details/{fixture.Replacement}"));

        Assert.Contains("Paused final-review interval", html, StringComparison.Ordinal);
        Assert.Contains("Verify the screenshot's in-game time against them before deciding.", html, StringComparison.Ordinal);
        Assert.Contains("2026-09-14 09:30 UTC", html, StringComparison.Ordinal);
        Assert.Contains("2026-09-14 10:30 UTC", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Ineligible final-review interval", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Treat it as outside the authoritative live eligibility intervals.", html, StringComparison.Ordinal);
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
        ["Input.CreditedOsrsCharacterId"] = fixture.CorrectionCharacter.ToString()
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
    private async Task<FinalReviewReadiness> ReadinessAsync() { await using var db = new ApplicationDbContext(options); return (await Finalization(db).GetReadinessAsync(fixture.EventId))!; }
    private async Task<DateTimeOffset> SubmissionSubmittedAtAsync(Guid teamId)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.Submissions.Where(value => value.TeamId == teamId && value.Status == SubmissionStatus.Approved)
            .Select(value => value.SubmittedAt).SingleAsync();
    }
    private async Task<int> ResolutionCountAsync() { await using var db = new ApplicationDbContext(options); return await db.FinalReviewResolutions.CountAsync(); }
    private async Task<int> CompletionCorrectionCountAsync() { await using var db = new ApplicationDbContext(options); return await db.TeamCompletionCorrections.CountAsync(); }
    private async Task ApproveAsync(Guid id)
    {
        await using var db = new ApplicationDbContext(options);
        var result = await Submissions(db).ApproveCurrentAsync(id, fixture.Admin);
        Assert.True(result.ApprovedContribution > 0, "The deterministic C33 fixture approval must succeed.");
        Assert.Null(result.BlockingSubmission);
    }
    private async Task RejectAsync(Guid id) { await using var db = new ApplicationDbContext(options); await Submissions(db).RejectCurrentAsync(id, fixture.Admin, "C33 fixture rejection"); }
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
        var eventItem = await db.Events.SingleAsync();
        Assert.Equal(EventState.AwaitingFinalReview, eventItem.State);
        Assert.False(eventItem.ResultsPublished);
        Assert.Null(eventItem.FinalizedAt);
        Assert.Null(eventItem.ArchivedAt);
        Assert.Empty(await db.EventFinalizations.ToListAsync());
        Assert.Empty(await db.OfficialPlacements.ToListAsync());
        Assert.Empty(await db.AuditEntries.Where(x => x.Action == "event.finalized").ToListAsync());
        Assert.Empty(await db.EventStateTransitions.Where(x => x.ToState == EventState.Finalized).ToListAsync());
        Assert.Empty(await db.EventStateTransitions.Where(x => x.ToState == EventState.Archived).ToListAsync());
        Assert.Empty(await db.AuditEntries.Where(x => x.EventId == fixture.EventId && x.Action == "event.results_published").ToListAsync());
        Assert.Empty(await db.PersonalNotifications.Where(x => x.EventId == fixture.EventId && x.Title == "event.results_published").ToListAsync());
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
        builder.UseSetting("ConnectionStrings:Database", database.GetOwnedConnectionString());
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
        var action = WebUtility.HtmlDecode(Regex.Match(form, @"\saction=""([^""]+)""").Groups[1].Value);
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
        await PostAsync(client, form, ("Input.Reason", "C33 review reason"), ("Input.BoardTileId", fixture.Tile.ToString()), ("Input.RequirementId", fixture.Requirement.ToString()), ("Input.CreditedOsrsCharacterId", fixture.CorrectionCharacter.ToString()));
        await using var db = new ApplicationDbContext(options);
        Assert.Contains(await db.ReviewActions.Where(x => x.SubmissionId == submission).ToListAsync(), x => x.Action == handler switch
        { "Approve" => ReviewActionType.Approve, "Reverse" => ReviewActionType.ReverseApproval, "Reject" => ReviewActionType.Reject, _ => ReviewActionType.EditMetadata });
    }

    [Fact]
    public async Task AdminReviewDetailsRendersLegacyReviewActionsOnceThroughSharedAuditPresentation()
    {
        var submittedAt = now.AddMinutes(-30);
        var actions = new[]
        {
            new ReviewAction(Guid.NewGuid(), fixture.Replacement, ReviewActionType.Submitted, fixture.Admin, submittedAt, "Legacy submission note", null, null),
            new ReviewAction(Guid.NewGuid(), fixture.Replacement, ReviewActionType.RequestChanges, fixture.Admin, submittedAt.AddMinutes(1), "Legacy changes reason", null, null),
            new ReviewAction(Guid.NewGuid(), fixture.Replacement, ReviewActionType.Resubmit, fixture.Admin, submittedAt.AddMinutes(2), "Legacy linked attempt reason", null, null),
            new ReviewAction(Guid.NewGuid(), fixture.Replacement, ReviewActionType.Reject, fixture.Admin, submittedAt.AddMinutes(3), "Legacy rejection reason", null, null),
            new ReviewAction(Guid.NewGuid(), fixture.Replacement, ReviewActionType.ReverseApproval, fixture.Admin, submittedAt.AddMinutes(4), "Legacy reversal reason", null, null)
        };
        var approved = new ReviewAction(Guid.NewGuid(), fixture.Replacement, ReviewActionType.Approve, fixture.Admin, submittedAt.AddMinutes(5), "Legacy approval note", "{\"Status\":\"Pending\"}", "{\"Status\":\"Approved\"}");
        var matchedAudit = new AuditEntry(Guid.NewGuid(), approved.PerformedAt, approved.PerformedByAccountId, "c33-admin", "submission.approved", "submission", fixture.Replacement.ToString("D"), approved.Note, fixture.EventId, approved.BeforeSnapshot, approved.AfterSnapshot);

        await using (var db = new ApplicationDbContext(options))
        {
            db.ReviewActions.AddRange(actions.Append(approved));
            db.AuditEntries.Add(matchedAudit);
            await db.SaveChangesAsync();
        }

        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        var html = await client.GetStringAsync($"/Admin/Review/Details/{fixture.Replacement}");

        Assert.Contains("Evidence submitted", html, StringComparison.Ordinal);
        Assert.Contains("Evidence changes requested", html, StringComparison.Ordinal);
        Assert.Contains("Historical linked evidence submitted", html, StringComparison.Ordinal);
        Assert.Contains("Evidence rejected", html, StringComparison.Ordinal);
        Assert.Contains("Evidence approval reversed", html, StringComparison.Ordinal);
        Assert.Contains("Evidence approved", html, StringComparison.Ordinal);
        foreach (var action in actions)
        {
            Assert.Single(Regex.Matches(html, Regex.Escape($"data-audit-entry=\"{action.Id}\"")));
            Assert.Contains(action.Note!, html, StringComparison.Ordinal);
        }
        Assert.Single(Regex.Matches(html, Regex.Escape($"data-audit-entry=\"{matchedAudit.Id}\"")));
        Assert.Single(Regex.Matches(html, Regex.Escape("Evidence approved")));
        Assert.Contains("c33-admin", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Reject")]
    [InlineData("Reverse")]
    [InlineData("Edit")]
    public async Task ReviewReasonOverLimitIsRejectedBeforeAnyMutation(string handler)
    {
        if (handler == "Reverse")
        {
            await RejectAsync(fixture.First);
            await ApproveAsync(fixture.Replacement);
        }
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, "c33-admin");
        var form = Form(await client.GetStringAsync($"/Admin/Review/Details/{fixture.Replacement}"), handler);
        var before = await SubmissionStateAsync(fixture.Replacement);
        var notificationCount = await NotificationCountAsync();
        var fields = ReviewFields(form);
        fields["Input.Reason"] = new string('x', 4001);

        using var response = await client.PostAsync(form.Action, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location!));
        Assert.Contains("Reason must be 4000 characters or fewer.", html, StringComparison.Ordinal);
        Assert.Contains("app-toast-error", html, StringComparison.Ordinal);
        Assert.Equal(before, await SubmissionStateAsync(fixture.Replacement));
        Assert.Equal(notificationCount, await NotificationCountAsync());
    }

    private async Task<int> NotificationCountAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return await db.PersonalNotifications.CountAsync(x => x.EventId == fixture.EventId);
    }

    private async Task<Setup> SeedAsync(ApplicationDbContext db)
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "c33-admin", "C33-ADMIN", now.AddDays(-10));
        admin.SetGlobalRole(GlobalRole.Admin);
        var player = Account.CreateWebsite(Guid.NewGuid(), "c33-player", "C33-PLAYER", now.AddDays(-10));
        foreach (var account in new[] { admin, player }) account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, false);
        var ev = new BingoEvent(Guid.NewGuid(), "C33 final review", "c33-final-review", "UTC", admin.Id, now.AddDays(-10), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        ev.ConfigureSchedule(now.AddDays(-8), now.AddDays(-5), null, now.AddHours(-4), now.AddHours(-1), 20);
        ev.OpenSignups(now.AddDays(-8)); ev.CloseSignups(now.AddDays(-5)); ev.SetDraftRosterPublication(true); ev.StartEvent(now.AddHours(-4)); ev.MarkFirstPublic(now.AddDays(-8));
        var board = new Board(Guid.NewGuid(), ev.Id, "C33 board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "C33 objective", "Finish the run", "Show completion", 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 1, true, false, "One run", true);
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1);
        draft.FinalizeDirect(now.AddDays(-2));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddDays(-2), admin.Id, DraftPublicationMethod.DirectRoster);
        db.AddRange(admin, player, ev, board, tile, requirement);
        var teams = new List<Team>();
        var characters = new List<OsrsCharacter>();
        var submissions = new List<Submission>();
        var alternateCharacter = Guid.Empty;
        for (var index = 0; index < 2; index++)
        {
            var team = new Team(Guid.NewGuid(), ev.Id, index == 0 ? "Team A" : "Team B", index == 0 ? "team-a" : "team-b", TeamFormationType.Drafted, null, true);
            team.Finalize(now.AddDays(-2));
            var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, index + 1, now.AddDays(-6), SignupSource.AdminCreated);
            var character = new OsrsCharacter(Guid.NewGuid(), $"C33 Player {index}", $"C33 PLAYER {index}", now.AddDays(-6));
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, now.AddDays(-6), admin.Id, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null);
            db.AddRange(team, participant, character, assignment, new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, now.AddDays(-4), null, "C33 fixture"));
            if (index == 0)
            {
                // B-Review-2 (U8, A10): a review correction must change a detail, so Team A's participant has a second Playing account to credit.
                var alternate = new OsrsCharacter(Guid.NewGuid(), "C33 Player 0 Alt", "C33 PLAYER 0 ALT", now.AddDays(-6));
                db.AddRange(alternate, new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, alternate.Id, 1, now.AddDays(-6), admin.Id, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null));
                alternateCharacter = alternate.Id;
            }
            for (var count = 0; count < (index == 0 ? 2 : 1); count++)
            {
                var submittedAt = index == 0 ? now.AddHours(-3).AddMinutes(count) : now.AddHours(-2);
                var submission = new Submission(Guid.NewGuid(), ev.Id, team.Id, tile.Id, requirement.Id, null, participant.Id, character.Id, character.DisplayName, player.Id, 1, submittedAt, null, null);
                db.Submissions.Add(submission);
                submissions.Add(submission);
            }
            teams.Add(team); characters.Add(character);
            db.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), publication.Id, team.Id, participant.Id,
                TeamMembershipRole.Participant, null, character.DisplayName));
        }
        db.AddRange(draft, publication);
        await BoardApprovalFixture.PublishAsync(db, board, now.AddDays(-2), [tile], [requirement]);
        await Submissions(db).ApproveCurrentAsync(submissions[2].Id, admin.Id);
        ev.EndEvent(now.AddHours(-1));
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, EventState.Live, EventState.AwaitingFinalReview, admin.Id, now.AddHours(-1), "C33 fixture ended", effectiveAt: now.AddHours(-1)));
        await db.SaveChangesAsync();
        return new(ev.Id, ev.Slug, admin.Id, teams[0].Id, teams[1].Id, submissions[0].Id, submissions[1].Id, tile.Id, requirement.Id, alternateCharacter);
    }
    private sealed record Setup(Guid EventId, string Slug, Guid Admin, Guid TeamA, Guid TeamB, Guid First, Guid Replacement, Guid Tile, Guid Requirement, Guid CorrectionCharacter);
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
