using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Bingo.Infrastructure.Teams;
using Bingo.Web.Events;
using Bingo.Web.Pages.Admin.Review;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed partial class SubmissionWorkflowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithLoopbackPort()
        .WithDatabase("bingo_submission_tests").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly DateTimeOffset now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(database);
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetOwnedConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task CaptainCannotSubmitForAnotherTeamOrPlayer()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);

        var wrongTeam = Command(setup) with { TeamId = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(wrongTeam));

        var wrongPlayer = Command(setup) with { CreditedParticipantId = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(wrongPlayer));
        Assert.Empty(await db.Submissions.ToListAsync());
    }

    [Fact]
    public async Task AdminCannotCreateEvidenceAndCommitBoundaryNeverDeletesCommittedAsset()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).CreateAsync(Command(setup) with { ActorAccountId = setup.AdminId }));
        Assert.Empty(await db.Submissions.ToListAsync());

        var storage = new RecordingEvidenceStorage();
        using var notifierCancellation = new CancellationTokenSource();
        var notifier = new CancellingNotifier(notifierCancellation);
        var committed = await Service(db, storage: storage, notifier: notifier).CreateAsync(Command(setup), notifierCancellation.Token);
        Assert.NotEqual(Guid.Empty, committed.SubmissionId);
        Assert.Single(await db.Submissions.ToListAsync());
        Assert.Empty(storage.DeletedTokens);

        var preCommitStorage = new RecordingEvidenceStorage();
        using var preCommitCancellation = new CancellationTokenSource();
        preCommitStorage.CancelAfterStore = preCommitCancellation;
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service(db, storage: preCommitStorage).CreateAsync(Command(setup), preCommitCancellation.Token));
        Assert.Single(preCommitStorage.DeletedTokens);
        Assert.All(preCommitStorage.DeleteTokens, token => Assert.False(token.IsCancellationRequested));
    }

    [Fact]
    public async Task CompletionFactUsesImmutableSubmittedAtAndRebalancesSurvivingEvidenceAtomically()
    {
        var setup = await SeedAsync(target: 2, allowHigherWeights: true, dropMaximum: 2, createAlternateWeightDrop: true);
        var submittedAt = new[] { now.AddMinutes(-20), now.AddMinutes(-10) };
        var clock = new MutableTimeProvider(submittedAt[0]);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(value => value.Id == setup.TeamId);
        team.Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();

        var service = Service(db, clock);
        var first = await service.CreateAsync(Command(setup) with { ClaimedWeight = 1 });
        clock.Set(submittedAt[1]);
        var second = await service.CreateAsync(Command(setup) with { DropSnapshotId = setup.AlternateDropId });
        clock.Set(now.AddHours(1));
        await service.ApproveCurrentAsync(first.SubmissionId, setup.AdminId);
        clock.Set(now.AddHours(2));
        await service.ApproveCurrentAsync(second.SubmissionId, setup.AdminId);

        var firstSubmissionTime = await db.Submissions.Where(value => value.Id == first.SubmissionId).Select(value => value.SubmittedAt).SingleAsync();
        var secondSubmissionTime = await db.Submissions.Where(value => value.Id == second.SubmissionId).Select(value => value.SubmittedAt).SingleAsync();
        Assert.Equal(submittedAt[0], firstSubmissionTime);
        Assert.Equal(submittedAt[1], secondSubmissionTime);
        Assert.NotEqual(secondSubmissionTime, await db.Submissions.Where(value => value.Id == second.SubmissionId).Select(value => value.ReviewedAt).SingleAsync());

        var board = await db.Boards.SingleAsync(value => value.EventId == setup.EventId);
        var fact = await db.TileCompletionFacts.SingleAsync(value => value.EventId == setup.EventId && value.TeamId == setup.TeamId && value.BoardTileId == setup.TileId && value.ApprovalSnapshotId == board.ActiveApprovalSnapshotId);
        Assert.True(fact.IsComplete);
        Assert.Equal(secondSubmissionTime, fact.CompletedAt);
        Assert.Contains(first.SubmissionId.ToString("D"), fact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.SubmissionId.ToString("D"), fact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);

        clock.Set(now.AddHours(3));
        await service.ReverseCurrentAsync(first.SubmissionId, setup.AdminId, "Reverse the earlier contribution; later evidence can carry the objective.");

        var survivingContribution = await db.SubmissionContributions.SingleAsync(value => value.SubmissionId == second.SubmissionId);
        Assert.Equal(2, survivingContribution.Amount);
        fact = await db.TileCompletionFacts.SingleAsync(value => value.Id == fact.Id);
        Assert.True(fact.IsComplete);
        Assert.Equal(secondSubmissionTime, fact.CompletedAt);
        Assert.DoesNotContain(first.SubmissionId.ToString("D"), fact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.SubmissionId.ToString("D"), fact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);

        var eventSlug = await db.Events.Where(value => value.Id == setup.EventId).Select(value => value.Slug).SingleAsync();
        var publicBoard = await new PublicBoardService(db, clock).GetEventBoardAsync(eventSlug);
        var publicTeam = Assert.Single(publicBoard!.Teams);
        Assert.True(publicTeam.Progress.Tiles.Single().Complete);
        Assert.Equal(secondSubmissionTime, publicTeam.Progress.CurrentScoreReachedAt);
    }

    [Fact]
    public async Task PublicBoardReadKeepsOneSnapshotAcrossConcurrentApproval()
    {
        var setup = await SeedAsync(target: 2, allowHigherWeights: false);
        var clock = new MutableTimeProvider(now.AddMinutes(-30));
        await using (var seed = new ApplicationDbContext(options))
        {
            var team = await seed.Teams.SingleAsync(value => value.Id == setup.TeamId);
            team.Finalize(now.AddDays(-2));
            await seed.SaveChangesAsync();
        }

        var first = await CreateApprovedAsync(setup, clock, now.AddMinutes(-20), 1);
        clock.Set(now.AddMinutes(-10));
        SubmissionResult pending;
        await using (var create = new ApplicationDbContext(options))
            pending = await Service(create, clock).CreateAsync(Command(setup) with { ClaimedWeight = 1 });

        var boundary = new PauseAfterTileCompletionFacts();
        var readOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(boundary).Options;
        await using var reader = new ApplicationDbContext(readOptions);
        var read = new PublicBoardService(reader, clock).GetEventBoardAsync($"event-{setup.EventId:N}");
        await boundary.Ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            clock.Set(now.AddMinutes(1));
            await using var writer = new ApplicationDbContext(options);
            await Service(writer, clock).ApproveCurrentAsync(pending.SubmissionId, setup.AdminId);
        }
        finally { boundary.Release.TrySetResult(); }

        var snapshot = await read;
        var snapshotTeam = Assert.Single(snapshot!.Teams);
        Assert.False(snapshotTeam.Progress.BoardComplete);
        Assert.Equal(1, snapshotTeam.Tiles.Single().Approved);
        Assert.DoesNotContain(snapshot.RecentDrops, value => value.SubmissionId == pending.SubmissionId);
        Assert.Contains(snapshot.RecentDrops, value => value.SubmissionId == first.SubmissionId);

        await using var verify = new ApplicationDbContext(options);
        var current = await new PublicBoardService(verify, clock).GetEventBoardAsync($"event-{setup.EventId:N}");
        var currentTeam = Assert.Single(current!.Teams);
        Assert.True(currentTeam.Progress.BoardComplete);
        Assert.Contains(current.RecentDrops, value => value.SubmissionId == pending.SubmissionId);
    }

    [Fact]
    public async Task PublicBoardReadKeepsOneSnapshotAcrossReversalThatLeavesTileIncomplete()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true, dropMaximum: 2, createAlternateWeightDrop: true);
        var clock = new MutableTimeProvider(now.AddMinutes(-30));
        await using (var seed = new ApplicationDbContext(options))
        {
            var team = await seed.Teams.SingleAsync(value => value.Id == setup.TeamId);
            team.Finalize(now.AddDays(-2));
            await seed.SaveChangesAsync();
        }

        SubmissionResult olderSubmission;
        clock.Set(now.AddMinutes(-20));
        await using (var create = new ApplicationDbContext(options))
            olderSubmission = await Service(create, clock).CreateAsync(Command(setup) with { DropSnapshotId = setup.AlternateDropId, ClaimedWeight = 2 });
        clock.Set(now.AddMinutes(-10));
        SubmissionResult laterSubmission;
        await using (var create = new ApplicationDbContext(options))
            laterSubmission = await Service(create, clock).CreateAsync(Command(setup) with { ClaimedWeight = 1 });
        clock.Set(now);
        await using (var reviewLater = new ApplicationDbContext(options))
            await Service(reviewLater, clock).ApproveCurrentAsync(laterSubmission.SubmissionId, setup.AdminId);
        clock.Set(now.AddMinutes(1));
        await using (var reviewOlder = new ApplicationDbContext(options))
            await Service(reviewOlder, clock).ApproveCurrentAsync(olderSubmission.SubmissionId, setup.AdminId);

        var boundary = new PauseAfterTileCompletionFacts();
        var readOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(boundary).Options;
        await using var reader = new ApplicationDbContext(readOptions);
        var read = new PublicBoardService(reader, clock).GetEventBoardAsync($"event-{setup.EventId:N}");
        await boundary.Ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            clock.Set(now.AddMinutes(2));
            await using var writer = new ApplicationDbContext(options);
            await Service(writer, clock).ReverseCurrentAsync(laterSubmission.SubmissionId, setup.AdminId, "Use the surviving earlier submission");
        }
        finally { boundary.Release.TrySetResult(); }

        var snapshot = await read;
        var snapshotTeam = Assert.Single(snapshot!.Teams);
        Assert.True(snapshotTeam.Progress.Tiles.Single().Complete);
        Assert.Equal(now.AddMinutes(-10), snapshotTeam.Progress.CurrentScoreReachedAt);
        Assert.Contains(snapshot.RecentDrops, value => value.SubmissionId == laterSubmission.SubmissionId);
        Assert.Contains(snapshot.RecentDrops, value => value.SubmissionId == olderSubmission.SubmissionId);

        await using var verify = new ApplicationDbContext(options);
        var current = await new PublicBoardService(verify, clock).GetEventBoardAsync($"event-{setup.EventId:N}");
        var currentTeam = Assert.Single(current!.Teams);
        Assert.False(currentTeam.Progress.Tiles.Single().Complete);
        Assert.Null(currentTeam.Progress.CurrentScoreReachedAt);
        Assert.DoesNotContain(current.RecentDrops, value => value.SubmissionId == laterSubmission.SubmissionId);
        Assert.Contains(current.RecentDrops, value => value.SubmissionId == olderSubmission.SubmissionId);
    }

    [Fact]
    public async Task NineToEightReversalUsesLatestSurvivingTileSubmissionTime()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: false, boardRows: 3, boardColumns: 3);
        var clock = new MutableTimeProvider(now.AddMinutes(-30));
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(value => value.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var service = Service(db, clock);
        var tileIds = setup.TileIds!;
        var requirementIds = setup.RequirementIds!;
        var submissions = new List<Guid>();
        var submittedTimes = new List<DateTimeOffset>();
        for (var index = 0; index < tileIds.Count; index++)
        {
            var submittedAt = now.AddMinutes(-30 + index);
            clock.Set(submittedAt);
            var dropId = await db.BoardRequirementDropSnapshots.Where(value => value.RequirementId == requirementIds[index])
                .Select(value => (Guid?)value.Id).SingleAsync();
            var created = await service.CreateAsync(Command(setup) with
            {
                BoardTileId = tileIds[index],
                RequirementId = requirementIds[index],
                DropSnapshotId = dropId
            });
            submissions.Add(created.SubmissionId);
            submittedTimes.Add(submittedAt);
        }

        clock.Set(now.AddHours(1));
        foreach (var submissionId in submissions) await service.ApproveCurrentAsync(submissionId, setup.AdminId);
        var board = await db.Boards.SingleAsync(value => value.EventId == setup.EventId);
        Assert.Equal(9, await db.TileCompletionFacts.CountAsync(value => value.TeamId == setup.TeamId && value.ApprovalSnapshotId == board.ActiveApprovalSnapshotId && value.IsComplete));

        clock.Set(now.AddHours(3));
        await service.ReverseCurrentAsync(submissions[^1], setup.AdminId, "Reverse the ninth tile.");

        var activeFacts = await db.TileCompletionFacts.Where(value => value.TeamId == setup.TeamId && value.ApprovalSnapshotId == board.ActiveApprovalSnapshotId).ToListAsync();
        Assert.Equal(8, activeFacts.Count(value => value.IsComplete));
        Assert.Equal(submittedTimes[^2], activeFacts.Where(value => value.IsComplete).Max(value => value.CompletedAt));
        Assert.Null(activeFacts.Single(value => value.BoardTileId == tileIds[^1]).CompletedAt);
        var slug = await db.Events.Where(value => value.Id == setup.EventId).Select(value => value.Slug).SingleAsync();
        var publicTeam = Assert.Single((await new PublicBoardService(db, clock).GetEventBoardAsync(slug))!.Teams);
        Assert.False(publicTeam.Progress.BoardComplete);
        Assert.Equal(8, publicTeam.Progress.CompletedTiles);
        Assert.Equal(submittedTimes[^2], publicTeam.Progress.CurrentScoreReachedAt);
        Assert.Null(publicTeam.Progress.BoardCompletedAt);
    }

    [Fact]
    public async Task MultiRequirementTileCompletionUsesLatestObjectiveSubmissionAndBothProvenanceChains()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: false, duplicatesAllowed: true, additionalObjectivesPerTile: 1);
        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(value => value.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var service = Service(db, clock);
        var requirementIds = setup.RequirementIds!;
        var dropIds = setup.DropIds!;
        var firstTime = now.AddMinutes(-20);
        var secondTime = now.AddMinutes(-10);
        var first = await service.CreateAsync(Command(setup) with { RequirementId = requirementIds[0], DropSnapshotId = dropIds[0] });
        clock.Set(secondTime);
        var second = await service.CreateAsync(Command(setup) with { RequirementId = requirementIds[1], DropSnapshotId = dropIds[1] });
        clock.Set(now.AddHours(1));
        await service.ApproveCurrentAsync(first.SubmissionId, setup.AdminId);
        clock.Set(now.AddHours(2));
        await service.ApproveCurrentAsync(second.SubmissionId, setup.AdminId);

        var board = await db.Boards.SingleAsync(value => value.EventId == setup.EventId);
        var fact = await db.TileCompletionFacts.SingleAsync(value => value.TeamId == setup.TeamId && value.BoardTileId == setup.TileId && value.ApprovalSnapshotId == board.ActiveApprovalSnapshotId);
        Assert.True(fact.IsComplete);
        Assert.Equal(secondTime, fact.CompletedAt);
        Assert.Contains(first.SubmissionId.ToString("D"), fact.QualifyingContributionsJson!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.SubmissionId.ToString("D"), fact.QualifyingContributionsJson!, StringComparison.OrdinalIgnoreCase);
        var slug = await db.Events.Where(value => value.Id == setup.EventId).Select(value => value.Slug).SingleAsync();
        var team = Assert.Single((await new PublicBoardService(db, clock).GetEventBoardAsync(slug))!.Teams);
        Assert.Equal(secondTime, team.Progress.CurrentScoreReachedAt);
        Assert.True(team.Progress.Tiles.Single().Complete);
        Assert.Equal(firstTime, await db.Submissions.Where(value => value.Id == first.SubmissionId).Select(value => value.SubmittedAt).SingleAsync());
    }

    [Theory]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.Participant, EvidenceActorKind.Participant)]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.Captain, EvidenceActorKind.Captain)]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.CoCaptain, EvidenceActorKind.Captain)]
    [InlineData(GlobalRole.SuperAdmin, TeamMembershipRole.Participant, EvidenceActorKind.Participant)]
    public async Task GlobalRoleComposesWithTheGenuineEventMembershipForSubmissionAuthority(GlobalRole globalRole, TeamMembershipRole membershipRole, EvidenceActorKind expectedKind)
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        var suffix = Guid.NewGuid().ToString("N");
        var account = Account.CreateWebsite(Guid.NewGuid(), $"additive-{globalRole}-{membershipRole}-{suffix}", $"ADDITIVE-{suffix}", now);
        account.SetGlobalRole(globalRole);
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == setup.ParticipantId);
        participant.AssignOwner(account);
        var membership = await db.TeamMemberships.SingleAsync(x => x.TeamId == setup.TeamId && x.EventParticipantId == setup.ParticipantId);
        membership.ChangeRole(membershipRole);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var authority = new EvidenceAuthority(db);
        var scope = await authority.ResolveActorAsync(account.Id, setup.EventId, setup.TeamId, now);
        Assert.Equal(expectedKind, scope.Kind);
        Assert.Equal(setup.ParticipantId, scope.CreditedParticipantId);
        var authorized = await authority.AuthorizeAsync(account.Id, setup.EventId, setup.TeamId, setup.ParticipantId, now);
        Assert.Equal(expectedKind, authorized.Kind);

        var created = await Service(db, clock).CreateAsync(Command(setup) with { ActorAccountId = account.Id });
        Assert.NotEqual(Guid.Empty, created.SubmissionId);

        var eventItem = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        eventItem.EndEvent(now.AddMinutes(-31));
        await db.SaveChangesAsync();
        clock.Set(eventItem.SubmissionCutoffAt!.Value.AddTicks(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, clock).CreateAsync(Command(setup) with { ActorAccountId = account.Id }));
        Assert.Single(await db.Submissions.ToListAsync());
    }

    [Fact]
    public async Task PrivateEvidenceMutationsDoNotAnnouncePublicProgressUntilApprovalOrReversal()
    {
        var setup = await SeedAsync(target: 4, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var notifier = new RecordingProgressNotifier();
        var service = Service(db, notifier: notifier);

        var edited = await service.CreateAsync(Command(setup));
        await service.CorrectAsync(new(edited.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId, setup.ParticipantId, 1, "edited", null));
        await service.WithdrawAsync(edited.SubmissionId, setup.CaptainId);

        var rejected = await service.CreateAsync(Command(setup));
        await service.RejectCurrentAsync(rejected.SubmissionId, setup.AdminId, "Please include the full game message.");
        Assert.Empty(notifier.EventIds);

        var approved = await service.CreateAsync(Command(setup));
        await service.ApproveCurrentAsync(approved.SubmissionId, setup.AdminId);
        Assert.Single(notifier.EventIds);
        await service.ReverseCurrentAsync(approved.SubmissionId, setup.AdminId, "Correction required.");
        Assert.Equal(2, notifier.EventIds.Count);
        var audits = await db.AuditEntries.Where(x => x.EventId == setup.EventId && x.TargetType == "submission").ToListAsync();
        Assert.Equal(8, audits.Count);
        Assert.Equal(3, audits.Count(audit => audit.Action == "submission.created"));
        Assert.Contains(audits, audit => audit.Action == "submission.created");
        Assert.Contains(audits, audit => audit.Action == "submission.corrected");
        Assert.Contains(audits, audit => audit.Action == "submission.withdrawn");
        Assert.Contains(audits, audit => audit.Action == "submission.rejected");
        Assert.Contains(audits, audit => audit.Action == "submission.approved");
        Assert.Contains(audits, audit => audit.Action == "submission.reversed");
        var correctedAudit = Assert.Single(audits, audit => audit.TargetId == edited.SubmissionId.ToString("D") && audit.Action == "submission.corrected");
        Assert.Equal("edited", correctedAudit.Details);
        Assert.Contains("\"CaptainNotePresent\":true", correctedAudit.BeforeState ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"CaptainNotePresent\":true", correctedAudit.AfterState ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"CaptainNote\":", correctedAudit.BeforeState ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"CaptainNote\":", correctedAudit.AfterState ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Version\"", correctedAudit.BeforeState ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Version\"", correctedAudit.AfterState ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminReviewGraceContextUsesScheduledOrAuthoritativeEarlyEnd()
    {
        var scheduled = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var scheduledSubmission = await Service(db).CreateAsync(Command(scheduled));
        var scheduledUpload = now.AddHours(5);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {scheduledUpload} WHERE id = {scheduledSubmission.SubmissionId}");
        db.ChangeTracker.Clear();

        var scheduledPage = new DetailsModel(db, Service(db));
        Assert.IsType<PageResult>(await scheduledPage.OnGetAsync(scheduledSubmission.SubmissionId, CancellationToken.None));
        Assert.Equal(60, scheduledPage.Details.MinutesAfterEventEnd);
        var expectedScheduledEnd = now.AddHours(4);
        Assert.Equal(expectedScheduledEnd.AddTicks(-(expectedScheduledEnd.Ticks % TimeSpan.TicksPerMicrosecond)), scheduledPage.Details.EventEndsAt);

        var earlySubmission = await Service(db).CreateAsync(Command(scheduled));
        var earlyEnd = now.AddHours(1);
        var earlyUpload = now.AddHours(2);
        var eventItem = await db.Events.SingleAsync(x => x.Id == scheduled.EventId);
        eventItem.EndEvent(earlyEnd);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE submissions SET submitted_at = {earlyUpload} WHERE id = {earlySubmission.SubmissionId}");
        db.ChangeTracker.Clear();

        var earlyPage = new DetailsModel(db, Service(db));
        Assert.IsType<PageResult>(await earlyPage.OnGetAsync(earlySubmission.SubmissionId, CancellationToken.None));
        Assert.Equal(60, earlyPage.Details.MinutesAfterEventEnd);
        Assert.Equal(earlyEnd.AddTicks(-(earlyEnd.Ticks % TimeSpan.TicksPerMicrosecond)), earlyPage.Details.EventEndsAt);
    }

    [Fact]
    public async Task ReleasedPlayingAssignmentCanBeUsedForAdminCorrectionAfterReassignment()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var submission = await service.CreateAsync(Command(setup));
        var assignment = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == setup.ParticipantId && x.ReleasedAt == null);
        var oldCharacterId = assignment.OsrsCharacterId;
        assignment.Release(setup.AdminId, now);
        var replacementCharacter = new OsrsCharacter(Guid.NewGuid(), "Replacement Player", "REPLACEMENT PLAYER", now);
        db.OsrsCharacters.Add(replacementCharacter);
        db.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, setup.ParticipantId, replacementCharacter.Id, 1, now, setup.AdminId, null, EventCharacterRole.Playing, 500, EhbSource.Manual, null));
        await db.SaveChangesAsync();

        // B-Review-2 (U8, A10): an identical correction is now refused, so the released account is proven
        // selectable by correcting to the current account first and then back to the released one.
        var initial = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission.SubmissionId);
        await service.EditMetadataAsync(new(submission.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, replacementCharacter.Id, "current character", initial.Version));
        Assert.Equal(replacementCharacter.Id, await db.Submissions.Where(x => x.Id == submission.SubmissionId).Select(x => x.CreditedOsrsCharacterId).SingleAsync());
        var current = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission.SubmissionId);
        await service.EditMetadataAsync(new(submission.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, oldCharacterId, "historical character", current.Version));
        Assert.Equal(oldCharacterId, await db.Submissions.Where(x => x.Id == submission.SubmissionId).Select(x => x.CreditedOsrsCharacterId).SingleAsync());
    }

    [Fact]
    public async Task PrivateEvidenceRouteUsesCreditedOwnerCurrentLeadershipAdminAndRejectsOthers()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var participantAccount = Account.CreateWebsite(Guid.NewGuid(), "route-participant", "ROUTE-PARTICIPANT", now);
        var unrelatedAccount = Account.CreateWebsite(Guid.NewGuid(), "route-unrelated", "ROUTE-UNRELATED", now);
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == setup.ParticipantId);
        participant.AssignOwner(participantAccount);
        db.Accounts.AddRange(participantAccount, unrelatedAccount);
        await db.SaveChangesAsync();
        var result = await Service(db).CreateAsync(Command(setup));
        var assetId = await db.EvidenceAssets.Where(x => x.SubmissionId == result.SubmissionId && x.Active).Select(x => x.Id).SingleAsync();

        async Task<IActionResult> ReadAs(Guid? accountId)
        {
            var page = new Bingo.Web.Pages.EvidenceModel(db, new FakeEvidenceStorage(), new EvidenceAuthority(db), new FixedTimeProvider(now));
            var http = new DefaultHttpContext { User = accountId is Guid id ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")) : new ClaimsPrincipal(new ClaimsIdentity()) };
            page.PageContext = new PageContext { HttpContext = http };
            return await page.OnGetAsync(assetId, CancellationToken.None);
        }

        Assert.IsType<FileStreamResult>(await ReadAs(participantAccount.Id));
        Assert.IsType<FileStreamResult>(await ReadAs(setup.CaptainId));
        Assert.IsType<FileStreamResult>(await ReadAs(setup.AdminId));
        Assert.False((await ReadAs(unrelatedAccount.Id)) is FileStreamResult);
        Assert.IsType<NotFoundResult>(await ReadAs(null));
    }

    [Fact]
    public async Task ArchivedPrivateEvidenceAllowsCurrentTeamMembersAndRejectsFormerOrUnrelatedAccounts()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var participantAccount = Account.CreateWebsite(Guid.NewGuid(), "archived-owner", "ARCHIVED-OWNER", now);
        var teammateAccount = Account.CreateWebsite(Guid.NewGuid(), "archived-teammate", "ARCHIVED-TEAMMATE", now);
        var unrelatedAccount = Account.CreateWebsite(Guid.NewGuid(), "archived-other", "ARCHIVED-OTHER", now);
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == setup.ParticipantId);
        participant.AssignOwner(participantAccount);
        var participantMembership = await db.TeamMemberships.SingleAsync(x => x.TeamId == setup.TeamId && x.EventParticipantId == setup.ParticipantId);
        var teammate = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 2, now, SignupSource.AdminCreated);
        teammate.AssignOwner(teammateAccount);
        var teammateMembership = new TeamMembership(Guid.NewGuid(), setup.TeamId, teammate.Id, TeamMembershipRole.Participant, now, null, "Archived evidence test");
        db.AddRange(participantAccount, teammateAccount, unrelatedAccount, teammate, teammateMembership);
        await db.SaveChangesAsync();
        var result = await Service(db).CreateAsync(Command(setup));
        await Service(db).RejectCurrentAsync(result.SubmissionId, setup.AdminId, "Archived test rejection");
        var assetId = await db.EvidenceAssets.Where(x => x.SubmissionId == result.SubmissionId && x.Active).Select(x => x.Id).SingleAsync();
        var ev = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        ev.EndEvent(now.AddHours(1)); ev.FinalizeResults(now.AddHours(1)); ev.Archive(now.AddHours(2));
        await db.SaveChangesAsync();

        async Task<IActionResult> ReadAs(Guid accountId)
        {
            var page = new Bingo.Web.Pages.EvidenceModel(db, new FakeEvidenceStorage(), new EvidenceAuthority(db), new FixedTimeProvider(now));
            page.PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "test")) } };
            return await page.OnGetAsync(assetId, CancellationToken.None);
        }

        Assert.IsType<FileStreamResult>(await ReadAs(participantAccount.Id));
        Assert.IsType<FileStreamResult>(await ReadAs(teammateAccount.Id));
        participantMembership.Leave(now.AddHours(3), "Former credited owner test");
        await db.SaveChangesAsync();
        Assert.IsType<FileStreamResult>(await ReadAs(participantAccount.Id));

        var formerOwnerDetail = new Bingo.Web.Pages.Submissions.SubmissionModel(
            db, Service(db), new EvidenceAuthority(db), new FixedTimeProvider(now), new PassthroughLocalizer(),
            NullLogger<Bingo.Web.Pages.Submissions.SubmissionModel>.Instance);
        formerOwnerDetail.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, participantAccount.Id.ToString())], "test"))
            }
        };
        Assert.IsType<PageResult>(await formerOwnerDetail.OnGetAsync(result.SubmissionId, CancellationToken.None));
        Assert.True(formerOwnerDetail.IsArchivedFormerOwner);

        teammateMembership.Leave(now.AddHours(3), "Former member test");
        await db.SaveChangesAsync();
        Assert.False((await ReadAs(teammateAccount.Id)) is FileStreamResult);
        Assert.False((await ReadAs(unrelatedAccount.Id)) is FileStreamResult);
    }

    [Fact]
    public async Task EmergencyCredentialNeedsTheAuthoritativeReopenedWindowAndExplicitReenableForEverySubmissionMutation()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "retired", "RETIRED", now); emergency.Enable();
        var access = new AccountEventAccess(Guid.NewGuid(), emergency.Id, setup.EventId, setup.TeamId, null, null, null, null); access.Enable();
        db.AddRange(emergency, access); await db.SaveChangesAsync();
        var command = Command(setup) with { ActorAccountId = emergency.Id };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).CreateAsync(command));
        Assert.Empty(await db.Submissions.ToListAsync());
        Assert.Empty(await db.AuditEntries.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyFinalizationThenUnfinalizationKeepsEveryUploadMutationClosedUntilExplicitReopen(bool emergency)
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        if (!emergency)
        {
            var owner = Account.CreateWebsite(Guid.NewGuid(), "evidence-owner", "EVIDENCE-OWNER", now.AddDays(-1));
            db.Accounts.Add(owner);
            (await db.EventParticipants.SingleAsync(value => value.Id == setup.ParticipantId)).AssignOwner(owner);
            setup = setup with { CaptainId = owner.Id };
        }
        (await db.Teams.SingleAsync(value => value.Id == setup.TeamId)).Finalize(now.AddMinutes(-1));
        await db.SaveChangesAsync();
        var service = Service(db, clock);
        var pending = await service.CreateAsync(Command(setup));
        var rejected = await service.CreateAsync(Command(setup));
        await service.RejectCurrentAsync(rejected.SubmissionId, setup.AdminId, "Replace the incomplete screenshot.");
        // The current publication boundary does not support a generic pending-review
        // override. Decide the initial submission before publishing; a new pending
        // submission is created and corrected only after the explicit reopen below.
        await service.ApproveCurrentAsync(pending.SubmissionId, setup.AdminId);
        var ev = await db.Events.SingleAsync(value => value.Id == setup.EventId);
        var originalCutoff = ev.SubmissionCutoffAt;
        Assert.True(originalCutoff > now);
        ev.EndEvent(now);
        var earlyCutoff = ev.SubmissionCutoffAt;
        Assert.NotEqual(originalCutoff, earlyCutoff);
        clock.Set(now.AddMinutes(31));
        Assert.True(ev.CloseSubmissionsIfDue(clock.GetUtcNow()));
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, EventState.Live,
            EventState.AwaitingFinalReview, setup.AdminId, now, "Early event end", effectiveAt: now));
        await db.SaveChangesAsync();
        var finals = new EventFinalizationService(db, new PublicBoardService(db, clock), clock);
        var readiness = (await finals.GetReadinessAsync(ev.Id))!;
        var firstCycle = readiness.ReviewCycleId;
        Assert.False(readiness.SubmissionWindowOpen);
        Assert.DoesNotContain(readiness.Blockers, value => value.Key == "submission-window");
        Assert.Empty(readiness.Blockers);
        var actor = new LifecycleActor(setup.AdminId, "admin");
        await finals.FinalizeAsync(ev.Id, actor, ev.Version);
        var official = await db.EventFinalizations.SingleAsync(value => value.EventId == ev.Id);
        var originalInputs = official.CalculationInputsJson;
        clock.Set(now.AddMinutes(32));
        await finals.UnfinalizeAsync(ev.Id, "Correct official results while uploads stay closed", true, actor, ev.Version);
        // Explicitly enabled emergency access must still obey the event's closed window.
        if (emergency)
            (await db.AccountEventAccesses.SingleAsync(value => value.AccountId == setup.CaptainId)).Enable();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        ev = await db.Events.SingleAsync(value => value.Id == setup.EventId);
        Assert.NotNull(ev.FinalizedAt);
        Assert.NotNull(ev.SubmissionsClosedAt);
        Assert.Null(ev.ReopenedSubmissionCutoffAt);
        Assert.Equal(earlyCutoff, ev.SubmissionCutoffAt);
        Assert.False(ev.AcceptsNewSubmissions(now));
        Assert.False(ev.AcceptsEmergencySubmissions(now));
        readiness = (await finals.GetReadinessAsync(ev.Id))!;
        Assert.NotEqual(firstCycle, readiness.ReviewCycleId);
        Assert.False(readiness.SubmissionWindowOpen);
        Assert.DoesNotContain(readiness.Blockers, value => value.Key == "submission-window");
        official = await db.EventFinalizations.SingleAsync(value => value.EventId == ev.Id);
        Assert.NotNull(official.UnfinalizedAt);
        Assert.Equal(originalInputs, official.CalculationInputsJson);
        Assert.Single(await db.OfficialPlacements.Where(value => value.EventId == ev.Id).ToListAsync());
        var auditCount = await db.AuditEntries.CountAsync();
        var actionCount = await db.ReviewActions.CountAsync();
        var assets = await db.EvidenceAssets.OrderBy(value => value.Id).Select(value => value.Id).ToListAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Command(setup)));
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(auditCount, await verify.AuditEntries.CountAsync());
            Assert.Equal(actionCount, await verify.ReviewActions.CountAsync());
            Assert.Equal(assets, await verify.EvidenceAssets.OrderBy(value => value.Id).Select(value => value.Id).ToListAsync());
            Assert.Equal(2, await verify.Submissions.CountAsync());
            Assert.Equal("captain note", (await verify.Submissions.SingleAsync(value => value.Id == pending.SubmissionId)).CaptainNote);
        }
        Assert.Throws<InvalidOperationException>(() => ev.ReopenSubmissions(now, now));
        Assert.False(ev.AcceptsNewSubmissions(now));
        ev.ReopenSubmissions(earlyCutoff!.Value.AddMinutes(10), clock.GetUtcNow());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        readiness = (await finals.GetReadinessAsync(ev.Id))!;
        Assert.True(readiness.SubmissionWindowOpen);
        Assert.Contains(readiness.Blockers, value => value.Key == "submission-window");
        var reopened = await service.CreateAsync(Command(setup));
        await service.CorrectAsync(new CorrectSubmissionCommand(reopened.SubmissionId, setup.CaptainId,
            setup.TileId, setup.RequirementId, setup.DropId, setup.ParticipantId, 2, "Explicitly reopened edit"));
        var attempt = await service.CreateAsync(Command(setup) with { CaptainNote = "Explicitly reopened ordinary attempt" });
        Assert.Equal(4, await db.Submissions.CountAsync());
        Assert.Equal(SubmissionStatus.Rejected, (await db.Submissions.SingleAsync(value => value.Id == rejected.SubmissionId)).Status);
        Assert.Equal(SubmissionStatus.Approved, (await db.Submissions.SingleAsync(value => value.Id == pending.SubmissionId)).Status);
        Assert.Equal(SubmissionStatus.Pending, (await db.Submissions.SingleAsync(value => value.Id == reopened.SubmissionId)).Status);
        Assert.Null(await db.Submissions.Where(value => value.Id == attempt.SubmissionId).Select(value => value.ResubmissionOfSubmissionId).SingleAsync());
        Assert.Contains(await db.AuditEntries.Where(value => value.TargetId == attempt.SubmissionId.ToString("D")).ToListAsync(), value => value.Action == "submission.created");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovalCapsContributionAndReversalRebalancesLaterApprovedEvidence(bool failChildAudit)
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        var service = Service(db, clock);
        var first = await service.CreateAsync(Command(setup) with { ClaimedWeight = 2 });
        clock.Set(now.AddMinutes(-10));
        var second = await service.CreateAsync(Command(setup) with { ClaimedWeight = 2 });
        clock.Set(now);

        Assert.Equal(2, (await service.ApproveCurrentAsync(first.SubmissionId, setup.AdminId)).ApprovedContribution);
        Assert.Equal(1, (await service.ApproveCurrentAsync(second.SubmissionId, setup.AdminId)).ApprovedContribution);
        Assert.Equal(3, await db.SubmissionContributions.Where(x => x.ReversedAt == null).SumAsync(x => x.Amount));

        var childContribution = await db.SubmissionContributions.SingleAsync(value => value.SubmissionId == second.SubmissionId);
        var originalVersions = await db.Submissions.OrderBy(value => value.Id).Select(value => value.Version).ToListAsync();
        var originalAssets = await db.EvidenceAssets.OrderBy(value => value.Id).Select(value => value.Id).ToListAsync();
        var auditCount = await db.AuditEntries.CountAsync();
        var actionCount = await db.ReviewActions.CountAsync();
        if (failChildAudit)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE FUNCTION rebalance_fail_child_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                    BEGIN
                        IF NEW.action = 'submission.contribution_rebalanced' THEN
                            RAISE EXCEPTION 'rebalance child audit failure injection';
                        END IF;
                        RETURN NEW;
                    END;
                    $$;
                    CREATE TRIGGER rebalance_fail_child_audit BEFORE INSERT ON audit_entries
                        FOR EACH ROW EXECUTE FUNCTION rebalance_fail_child_audit();
                    """);
                var failure = await Record.ExceptionAsync(() => service.ReverseCurrentAsync(first.SubmissionId, setup.AdminId, "Approved the wrong screenshot"));
                Assert.NotNull(failure);
                Assert.Contains("rebalance child audit failure injection", failure.ToString(), StringComparison.Ordinal);
                await using var verify = new ApplicationDbContext(options);
                var submissions = await verify.Submissions.AsNoTracking().OrderBy(value => value.Id).ToListAsync();
                Assert.All(submissions, value => Assert.Equal(SubmissionStatus.Approved, value.Status));
                Assert.Equal(originalVersions, submissions.Select(value => value.Version));
                Assert.Equal(2, submissions.Single(value => value.Id == first.SubmissionId).ApprovedContribution);
                Assert.Equal(1, submissions.Single(value => value.Id == second.SubmissionId).ApprovedContribution);
                var contributions = await verify.SubmissionContributions.AsNoTracking().ToListAsync();
                Assert.Equal(2, contributions.Count);
                Assert.All(contributions, value => Assert.Null(value.ReversedAt));
                Assert.Equal(2, contributions.Single(value => value.SubmissionId == first.SubmissionId).Amount);
                Assert.Equal(1, contributions.Single(value => value.Id == childContribution.Id).Amount);
                Assert.Equal(auditCount, await verify.AuditEntries.CountAsync());
                Assert.Equal(actionCount, await verify.ReviewActions.CountAsync());
                Assert.Equal(originalAssets, await verify.EvidenceAssets.OrderBy(value => value.Id).Select(value => value.Id).ToListAsync());
            }
            finally
            {
                await using var cleanup = new ApplicationDbContext(options);
                await cleanup.Database.ExecuteSqlRawAsync("DROP TRIGGER rebalance_fail_child_audit ON audit_entries; DROP FUNCTION rebalance_fail_child_audit();");
            }
            return;
        }

        await service.ReverseCurrentAsync(first.SubmissionId, setup.AdminId, "Approved the wrong screenshot");

        Assert.Equal(2, await db.SubmissionContributions.Where(x => x.ReversedAt == null).SumAsync(x => x.Amount));
        Assert.Equal(SubmissionStatus.Reversed, await db.Submissions.Where(x => x.Id == first.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(2, await db.Submissions.Where(x => x.Id == second.SubmissionId).Select(x => x.ApprovedContribution).SingleAsync());
        Assert.Contains(await db.ReviewActions.Where(x => x.SubmissionId == second.SubmissionId).ToListAsync(), x => x.Action == ReviewActionType.RebalanceContribution);
        await using var committed = new ApplicationDbContext(options);
        var childAudit = Assert.Single(await committed.AuditEntries.Where(value => value.Action == "submission.contribution_rebalanced").ToListAsync());
        Assert.Equal(setup.EventId, childAudit.EventId);
        Assert.Equal(setup.AdminId, childAudit.ActorAccountId);
        Assert.Equal("admin", childAudit.ActorUsername);
        Assert.Equal("submission", childAudit.TargetType);
        Assert.Equal(second.SubmissionId.ToString("D"), childAudit.TargetId);
        Assert.Contains(first.SubmissionId.ToString("D"), childAudit.Details, StringComparison.Ordinal);
        Assert.Contains(childContribution.Id.ToString("D"), childAudit.Details, StringComparison.Ordinal);
        using var before = JsonDocument.Parse(childAudit.BeforeState!);
        using var after = JsonDocument.Parse(childAudit.AfterState!);
        Assert.Equal(1, before.RootElement.GetProperty("ApprovedContribution").GetInt32());
        Assert.Equal(2, after.RootElement.GetProperty("ApprovedContribution").GetInt32());
        Assert.Equal(setup.RequirementId, after.RootElement.GetProperty("RequirementId").GetGuid());
        Assert.Equal(setup.ParticipantId, after.RootElement.GetProperty("CreditedParticipantId").GetGuid());
        var localHistory = await committed.ReviewActions.SingleAsync(value => value.SubmissionId == second.SubmissionId && value.Action == ReviewActionType.RebalanceContribution);
        using var localBefore = JsonDocument.Parse(localHistory.BeforeSnapshot!);
        using var localAfter = JsonDocument.Parse(localHistory.AfterSnapshot!);
        var committedChildVersion = await committed.Submissions.Where(value => value.Id == second.SubmissionId).Select(value => value.Version).SingleAsync();
        Assert.Equal(committedChildVersion - 1, localBefore.RootElement.GetProperty("Version").GetInt32());
        Assert.Equal(committedChildVersion, localAfter.RootElement.GetProperty("Version").GetInt32());
        Assert.False(before.RootElement.TryGetProperty("Version", out _));
        Assert.False(after.RootElement.TryGetProperty("Version", out _));
        // ReviewAction alone retains the version used to order same-time actions.
        // Every behavior field must still exactly match the redacted Audit snapshot.
        var localBeforeBehavior = JsonSerializer.SerializeToElement(localBefore.RootElement.EnumerateObject()
            .Where(value => value.Name != "Version").ToDictionary(value => value.Name, value => value.Value));
        var localAfterBehavior = JsonSerializer.SerializeToElement(localAfter.RootElement.EnumerateObject()
            .Where(value => value.Name != "Version").ToDictionary(value => value.Name, value => value.Value));
        Assert.True(JsonElement.DeepEquals(before.RootElement, localBeforeBehavior));
        Assert.True(JsonElement.DeepEquals(after.RootElement, localAfterBehavior));
        Assert.Equal(auditCount + 2, await committed.AuditEntries.CountAsync());
        Assert.Equal(actionCount + 2, await committed.ReviewActions.CountAsync());
        Assert.Equal(originalAssets, await committed.EvidenceAssets.OrderBy(value => value.Id).Select(value => value.Id).ToListAsync());

    }

    [Fact]
    public async Task LaterApprovalReturnsEarlierPendingBlockAndUnblocksAfterResolution()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        var service = Service(db, clock);
        var earlierTime = now.AddMinutes(-20);
        var earlier = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var later = await service.CreateAsync(Command(setup));
        var eventVersion = await db.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync();
        var actionCount = await db.ReviewActions.CountAsync();

        clock.Set(now);
        var refused = await service.ApproveCurrentAsync(later.SubmissionId, setup.AdminId);

        Assert.Equal(0, refused.ApprovedContribution);
        var block = refused.BlockingSubmission;
        Assert.NotNull(block);
        Assert.Equal(earlier.SubmissionId, block.SubmissionId);
        Assert.Equal(earlierTime, block.SubmittedAt);
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == earlier.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == later.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Empty(await db.SubmissionContributions.ToListAsync());
        Assert.Equal(actionCount, await db.ReviewActions.CountAsync());
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(eventVersion, await verify.Events.Where(x => x.Id == setup.EventId).Select(x => x.Version).SingleAsync());
            Assert.Empty(await verify.SubmissionContributions.ToListAsync());
        }

        await service.RejectCurrentAsync(earlier.SubmissionId, setup.AdminId, "Resolve the earlier pending upload.");
        var approved = await service.ApproveCurrentAsync(later.SubmissionId, setup.AdminId);
        Assert.Equal(1, approved.ApprovedContribution);
        Assert.Null(approved.BlockingSubmission);
        Assert.Equal(SubmissionStatus.Rejected, await db.Submissions.Where(x => x.Id == earlier.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Approved, await db.Submissions.Where(x => x.Id == later.SubmissionId).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task LaterApprovalUsesRoomAndUploadOrderWithoutBlockingWhenBothFit()
    {
        var setup = await SeedAsync(target: 4, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        var service = Service(db, clock);
        var earlier = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var later = await service.CreateAsync(Command(setup));

        clock.Set(now);
        var laterApproval = await service.ApproveCurrentAsync(later.SubmissionId, setup.AdminId);
        var earlierApproval = await service.ApproveCurrentAsync(earlier.SubmissionId, setup.AdminId);

        Assert.Equal(2, laterApproval.ApprovedContribution);
        Assert.Null(laterApproval.BlockingSubmission);
        Assert.Equal(2, earlierApproval.ApprovedContribution);
        Assert.Null(earlierApproval.BlockingSubmission);
        var board = await db.Boards.SingleAsync(x => x.EventId == setup.EventId);
        var fact = await db.TileCompletionFacts.SingleAsync(x => x.EventId == setup.EventId && x.TeamId == setup.TeamId && x.BoardTileId == setup.TileId && x.ApprovalSnapshotId == board.ActiveApprovalSnapshotId);
        Assert.True(fact.IsComplete);
        Assert.Equal(await db.Submissions.Where(x => x.Id == later.SubmissionId).Select(x => x.SubmittedAt).SingleAsync(), fact.CompletedAt);
    }

    [Fact]
    public async Task LaterApprovalBlocksWhenItConsumesAnEarlierDropCap()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true, dropMaximum: 1);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        var service = Service(db, clock);
        var earlier = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var later = await service.CreateAsync(Command(setup));

        var refused = await service.ApproveCurrentAsync(later.SubmissionId, setup.AdminId);

        Assert.Equal(0, refused.ApprovedContribution);
        Assert.NotNull(refused.BlockingSubmission);
        Assert.Equal(earlier.SubmissionId, refused.BlockingSubmission.SubmissionId);
        Assert.Empty(await db.SubmissionContributions.ToListAsync());
    }

    [Fact]
    public async Task LaterApprovalSimulatesMultipleEarlierPendingUploadsCumulatively()
    {
        var setup = await SeedAsync(target: 2, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now.AddMinutes(-30));
        var service = Service(db, clock);
        var first = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-20));
        var second = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var later = await service.CreateAsync(Command(setup));

        var refused = await service.ApproveCurrentAsync(later.SubmissionId, setup.AdminId);

        Assert.Equal(0, refused.ApprovedContribution);
        Assert.NotNull(refused.BlockingSubmission);
        Assert.Equal(second.SubmissionId, refused.BlockingSubmission.SubmissionId);
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == first.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == second.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == later.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Empty(await db.SubmissionContributions.ToListAsync());
    }

    [Fact]
    public async Task LaterApprovalSimulatesEarlierUploadsSharingOneDropCapCumulatively()
    {
        var setup = await SeedAsync(target: 4, allowHigherWeights: false, dropMaximum: 2);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now.AddMinutes(-30));
        var service = Service(db, clock);
        var first = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-20));
        var second = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var later = await service.CreateAsync(Command(setup));

        var refused = await service.ApproveCurrentAsync(later.SubmissionId, setup.AdminId);

        Assert.Equal(0, refused.ApprovedContribution);
        Assert.NotNull(refused.BlockingSubmission);
        Assert.Equal(second.SubmissionId, refused.BlockingSubmission.SubmissionId);
        Assert.Empty(await db.SubmissionContributions.ToListAsync());
    }

    [Fact]
    public async Task LaterApprovalBlocksWithOnlyOneRoomLeftAtTargetAboveOne()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now.AddMinutes(-50));
        var service = Service(db, clock);
        var approvedFirst = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-40));
        var approvedSecond = await service.CreateAsync(Command(setup));
        clock.Set(now);
        Assert.Equal(1, (await service.ApproveCurrentAsync(approvedFirst.SubmissionId, setup.AdminId)).ApprovedContribution);
        Assert.Equal(1, (await service.ApproveCurrentAsync(approvedSecond.SubmissionId, setup.AdminId)).ApprovedContribution);

        clock.Set(now.AddMinutes(-20));
        var earlier = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var later = await service.CreateAsync(Command(setup));
        var refused = await service.ApproveCurrentAsync(later.SubmissionId, setup.AdminId);

        Assert.Equal(0, refused.ApprovedContribution);
        Assert.NotNull(refused.BlockingSubmission);
        Assert.Equal(earlier.SubmissionId, refused.BlockingSubmission.SubmissionId);
        Assert.Equal(2, await db.SubmissionContributions.Where(x => x.ReversedAt == null).SumAsync(x => x.Amount));
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == earlier.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == later.SubmissionId).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task ApprovalOrderBlockIsScopedToTheSameObjective()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: true, additionalObjectivesPerTile: 1);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        var service = Service(db, clock);
        var earlier = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var otherObjective = await service.CreateAsync(Command(setup) with
        {
            RequirementId = setup.RequirementIds![1],
            DropSnapshotId = setup.DropIds![1]
        });

        var approved = await service.ApproveCurrentAsync(otherObjective.SubmissionId, setup.AdminId);

        Assert.Equal(1, approved.ApprovedContribution);
        Assert.Null(approved.BlockingSubmission);
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == earlier.SubmissionId).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task ApprovalOrderBlockIsScopedToTheSameTeam()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        var otherTeamId = Guid.NewGuid();
        var otherParticipantId = Guid.NewGuid();
        var otherCharacterId = Guid.NewGuid();
        var otherTeam = new Team(otherTeamId, setup.EventId, "Team Two", $"team-{otherTeamId:N}", TeamFormationType.Drafted, null, true);
        otherTeam.Finalize(now.AddDays(-2));
        var otherParticipant = new EventParticipant(otherParticipantId, setup.EventId, SignupStatus.Confirmed, 2, now.AddDays(-5), SignupSource.AdminCreated);
        var otherCharacter = new OsrsCharacter(otherCharacterId, "Player Two", "PLAYER TWO", now.AddDays(-5));
        var otherAssignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, otherParticipantId, otherCharacterId, 0,
            now.AddDays(-5), setup.AdminId, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null);
        var otherMembership = new TeamMembership(Guid.NewGuid(), otherTeamId, otherParticipantId, TeamMembershipRole.Participant,
            now.AddDays(-4), null, "test");
        var publicationId = await db.DraftPublicationCycles
            .Where(x => db.DraftSessions.Any(d => d.Id == x.DraftSessionId && d.EventId == setup.EventId) && x.SupersededAt == null)
            .Select(x => x.Id).SingleAsync();
        db.AddRange(otherTeam, otherParticipant, otherCharacter, otherAssignment, otherMembership,
            new DraftPublicationRoster(Guid.NewGuid(), publicationId, otherTeamId, otherParticipantId,
                TeamMembershipRole.Participant, null, otherCharacter.DisplayName));
        await db.SaveChangesAsync();

        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        var service = Service(db, clock);
        var earlier = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var otherTeamLater = new Submission(Guid.NewGuid(), setup.EventId, otherTeamId, setup.TileId, setup.RequirementId,
            setup.DropId, otherParticipantId, otherCharacterId, otherCharacter.DisplayName, setup.AdminId, 1,
            now.AddMinutes(-10), null, null);
        db.Submissions.Add(otherTeamLater);
        await db.SaveChangesAsync();

        var approved = await service.ApproveCurrentAsync(otherTeamLater.Id, setup.AdminId);

        Assert.Equal(1, approved.ApprovedContribution);
        Assert.Null(approved.BlockingSubmission);
        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == earlier.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Approved, await db.Submissions.Where(x => x.Id == otherTeamLater.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task ConcurrentApprovalInBothLockOrdersRespectsEarlierUpload()
    {
        await RunRaceAsync(laterFirst: true);
        await RunRaceAsync(laterFirst: false);

        async Task RunRaceAsync(bool laterFirst)
        {
            var setup = await SeedAsync(target: 1, allowHigherWeights: true,
                identitySuffix: $"-{(laterFirst ? "later" : "earlier")}-{Guid.NewGuid():N}");
            await using (var setupDb = new ApplicationDbContext(options))
            {
                (await setupDb.Teams.SingleAsync(x => x.Id == setup.TeamId)).Finalize(now.AddDays(-2));
                await setupDb.SaveChangesAsync();
            }
            var clock = new MutableTimeProvider(now.AddMinutes(-20));
            SubmissionResult earlier;
            await using (var create = new ApplicationDbContext(options))
                earlier = await Service(create, clock).CreateAsync(Command(setup));
            clock.Set(now.AddMinutes(-10));
            SubmissionResult later;
            await using (var create = new ApplicationDbContext(options))
                later = await Service(create, clock).CreateAsync(Command(setup));

            var firstId = laterFirst ? later.SubmissionId : earlier.SubmissionId;
            var secondId = laterFirst ? earlier.SubmissionId : later.SubmissionId;
            var boundary = new PauseAfterEventLock();
            var firstOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(boundary).Options;
            await using var firstDb = new ApplicationDbContext(firstOptions);
            await firstDb.Database.OpenConnectionAsync();
            var firstTask = TryApproveAsync(firstDb, firstId, setup.AdminId);
            await boundary.Ready.Task.WaitAsync(TimeSpan.FromSeconds(15));

            await using var secondDb = new ApplicationDbContext(options);
            await secondDb.Database.OpenConnectionAsync();
            var secondTask = TryApproveAsync(secondDb, secondId, setup.AdminId);
            Assert.True(await WaitForDatabaseBlockAsync(secondTask, secondDb, firstDb), "The second approval must wait on the event-row approval lock.");
            boundary.Release.TrySetResult();
            var first = await firstTask;
            var second = await secondTask;

            await using var verify = new ApplicationDbContext(options);
            var states = await verify.Submissions.AsNoTracking().Where(x => x.Id == earlier.SubmissionId || x.Id == later.SubmissionId).ToListAsync();
            Assert.Single(states, x => x.Status == SubmissionStatus.Approved);
            Assert.Single(states, x => x.Status == SubmissionStatus.Pending);
            Assert.Single(await verify.SubmissionContributions.AsNoTracking().Where(x => x.SubmissionId == earlier.SubmissionId || x.SubmissionId == later.SubmissionId).ToListAsync());
            if (laterFirst)
            {
                Assert.Null(first.Error);
                Assert.Equal(earlier.SubmissionId, first.Result?.BlockingSubmission?.SubmissionId);
                Assert.Null(second.Error);
                Assert.Equal(1, second.Result?.ApprovedContribution);
            }
            else
            {
                Assert.Null(first.Error);
                Assert.Equal(1, first.Result?.ApprovedContribution);
                Assert.NotNull(second.Error);
                var serialization = second.Error as PostgresException ?? second.Error.InnerException as PostgresException;
                Assert.Equal(PostgresErrorCodes.SerializationFailure, serialization?.SqlState);
            }

            async Task<(SubmissionApprovalResult? Result, Exception? Error)> TryApproveAsync(ApplicationDbContext review, Guid id, Guid adminId)
            {
                try { return (await new SubmissionService(review, new FakeEvidenceStorage(), new FixedTimeProvider(now)).ApproveCurrentAsync(id, adminId), null); }
                catch (Exception exception) { return (null, exception); }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReversalRebalancingNeverExceedsConfiguredDropCap(bool duplicatesAllowed)
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true, duplicatesAllowed: duplicatesAllowed, dropMaximum: 1);
        await using var db = new ApplicationDbContext(options);
        var alternateDropId = Guid.NewGuid();
        db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(
            alternateDropId, setup.RequirementId, Guid.NewGuid(), Guid.NewGuid(), "Alternate boss", "Alternate drop", "1/10", 0.1m, 1, 1, 2));
        // This eligible alternate must belong to the published contract, not just
        // the private working copy, before it can be submitted.
        var alternate = db.BoardRequirementDropSnapshots.Local.Single(x => x.Id == alternateDropId);
        var approvalRequirement = await db.BoardApprovalRequirementSnapshots.SingleAsync(x => x.BoardRequirementSnapshotId == setup.RequirementId);
        db.BoardApprovalRequirementDropSnapshots.Add(new(Guid.NewGuid(), approvalRequirement.Id, alternate.SourceDropId, alternate.ItemIdSnapshot,
            alternate.BossName, alternate.ItemName, alternate.DisplayRate, alternate.NumericProbability, alternate.MaximumContribution, alternate.EhbPerContribution, alternate.CreditedWeight, 1));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        var service = Service(db, clock);
        var first = await service.CreateAsync(Command(setup) with { ClaimedWeight = 2 });
        clock.Set(now.AddMinutes(-10));
        var second = await service.CreateAsync(Command(setup) with { ClaimedWeight = 2, DropSnapshotId = alternateDropId });
        clock.Set(now);

        Assert.Equal(1, (await service.ApproveCurrentAsync(first.SubmissionId, setup.AdminId)).ApprovedContribution);
        Assert.Equal(1, (await service.ApproveCurrentAsync(second.SubmissionId, setup.AdminId)).ApprovedContribution);

        await service.ReverseCurrentAsync(first.SubmissionId, setup.AdminId, "Approved the wrong screenshot");

        Assert.Equal(1, await db.SubmissionContributions.Where(x => x.SubmissionId == second.SubmissionId).Select(x => x.Amount).SingleAsync());
        Assert.Equal(1, await db.SubmissionContributions.Where(x => x.ReversedAt == null).SumAsync(x => x.Amount));
        Assert.DoesNotContain(await db.ReviewActions.Where(x => x.SubmissionId == second.SubmissionId).ToListAsync(), x => x.Action == ReviewActionType.RebalanceContribution);
    }

    [Fact]
    public async Task ApprovedEvidenceUsesTheStoredIdentityWithoutLegacyPrivacyState()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var result = await service.CreateAsync(Command(setup));

        await service.ApproveCurrentAsync(result.SubmissionId, setup.AdminId);

        var approved = await db.Submissions.SingleAsync(x => x.Id == result.SubmissionId);
        Assert.Equal(SubmissionStatus.Approved, approved.Status);
        Assert.NotEqual(Guid.Empty, approved.CreditedOsrsCharacterId);
    }

    [Fact]
    public async Task OneSubmissionCannotBeApprovedTwice()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var submission = await service.CreateAsync(Command(setup));
        await service.ApproveCurrentAsync(submission.SubmissionId, setup.AdminId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveCurrentAsync(submission.SubmissionId, setup.AdminId));

        Assert.Equal(1, await db.SubmissionContributions.CountAsync(x => x.SubmissionId == submission.SubmissionId));
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.TargetId == submission.SubmissionId.ToString("D")));
    }

    [Fact]
    public async Task ApprovalRollsBackWhenTheMainAuditWriteFails()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var submission = await Service(db).CreateAsync(Command(setup));
        var targetId = submission.SubmissionId.ToString("D");
        var reviewActionCount = await db.ReviewActions.CountAsync(x => x.SubmissionId == submission.SubmissionId);
        var contributionCount = await db.SubmissionContributions.CountAsync(x => x.SubmissionId == submission.SubmissionId);
        var auditCount = await db.AuditEntries.CountAsync(x => x.TargetId == targetId);

        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE OR REPLACE FUNCTION submission_workflow_fail_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.action = 'submission.approved' THEN
                        RAISE EXCEPTION 'submission audit failure injection';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER submission_workflow_fail_audit
                    BEFORE INSERT ON audit_entries
                    FOR EACH ROW EXECUTE FUNCTION submission_workflow_fail_audit();
                """);

            var failure = await Record.ExceptionAsync(() => Service(db).ApproveCurrentAsync(submission.SubmissionId, setup.AdminId));
            Assert.NotNull(failure);
            Assert.Contains("submission audit failure injection", failure?.ToString() ?? string.Empty, StringComparison.Ordinal);

            await using var verify = new ApplicationDbContext(options);
            var persisted = await verify.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission.SubmissionId);
            Assert.Equal(SubmissionStatus.Pending, persisted.Status);
            Assert.Equal(0, persisted.ApprovedContribution);

            var actions = await verify.ReviewActions.AsNoTracking().Where(x => x.SubmissionId == submission.SubmissionId).ToListAsync();
            Assert.Equal(reviewActionCount, actions.Count);
            Assert.DoesNotContain(actions, x => x.Action == ReviewActionType.Approve);
            Assert.Equal(contributionCount, await verify.SubmissionContributions.CountAsync(x => x.SubmissionId == submission.SubmissionId));

            var audits = await verify.AuditEntries.AsNoTracking().Where(x => x.TargetId == targetId).ToListAsync();
            Assert.Equal(auditCount, audits.Count);
            Assert.DoesNotContain(audits, x => x.Action == "submission.approved");
        }
        finally
        {
            await using var cleanup = new ApplicationDbContext(options);
            await cleanup.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS submission_workflow_fail_audit ON audit_entries; DROP FUNCTION IF EXISTS submission_workflow_fail_audit();");
        }
    }

    [Fact]
    public async Task AdminReviewMutationsFailClosedAfterFinalizationWithoutAudits()
    {
        var setup = await SeedAsync(target: 4, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var pending = await service.CreateAsync(Command(setup));
        var approved = await service.CreateAsync(Command(setup));
        await service.ApproveCurrentAsync(approved.SubmissionId, setup.AdminId);
        var characterId = await db.EventParticipantCharacters
            .Where(x => x.EventParticipantId == setup.ParticipantId && x.ReleasedAt == null)
            .Select(x => x.OsrsCharacterId)
            .SingleAsync();

        var eventItem = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        eventItem.EndEvent(now.AddMinutes(-1));
        eventItem.FinalizeResults(now);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EditMetadataCurrentAsync(new(
            pending.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId,
            characterId, "Closed correction")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectCurrentAsync(pending.SubmissionId, setup.AdminId, "Closed rejection"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveCurrentAsync(pending.SubmissionId, setup.AdminId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseCurrentAsync(approved.SubmissionId, setup.AdminId, "Closed reversal"));

        Assert.Equal(SubmissionStatus.Pending, await db.Submissions.Where(x => x.Id == pending.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(SubmissionStatus.Approved, await db.Submissions.Where(x => x.Id == approved.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(3, await db.AuditEntries.CountAsync(x => x.EventId == setup.EventId && x.TargetType == "submission"));
    }

    [Fact]
    public async Task RejectionNotifiesLinkedCreditedParticipantAndCurrentCoCaptainExactlyOnce()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var participantAccount = Account.CreateWebsite(Guid.NewGuid(), "participant", "PARTICIPANT", now.AddDays(-2));
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == setup.ParticipantId);
        participant.AssignOwner(participantAccount);
        (await db.TeamMemberships.SingleAsync(x => x.TeamId == setup.TeamId && x.EventParticipantId == setup.ParticipantId)).ChangeRole(TeamMembershipRole.Captain);
        var coCaptainAccount = Account.CreateWebsite(Guid.NewGuid(), "co-captain", "CO-CAPTAIN", now.AddDays(-2));
        var coCaptain = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 2, now.AddDays(-2), SignupSource.AdminCreated);
        coCaptain.AssignOwner(coCaptainAccount);
        db.AddRange(participantAccount, coCaptainAccount, coCaptain,
            new TeamMembership(Guid.NewGuid(), setup.TeamId, coCaptain.Id, TeamMembershipRole.CoCaptain, now.AddDays(-1), null, "Seeded co-captain"));
        await db.SaveChangesAsync();

        var service = Service(db);
        var submission = await service.CreateAsync(Command(setup));
        await service.RejectCurrentAsync(submission.SubmissionId, setup.AdminId, "The screenshot does not establish the claimed drop.");

        var notifications = await db.PersonalNotifications.AsNoTracking().Where(x => x.Title == "evidence.rejected").ToListAsync();
        Assert.Equal(3, notifications.Count);
        Assert.Equal(new[] { participantAccount.Id, coCaptainAccount.Id, setup.CaptainId }.OrderBy(x => x), notifications.Select(x => x.RecipientAccountId).OrderBy(x => x));
        Assert.Equal($"/Submissions/{submission.SubmissionId}", notifications.Single(x => x.RecipientAccountId == participantAccount.Id).Route);
        Assert.Equal($"/Submissions/{submission.SubmissionId}", notifications.Single(x => x.RecipientAccountId == coCaptainAccount.Id).Route);
        Assert.All(notifications, notification =>
        {
            Assert.Contains("Event ", notification.Detail, StringComparison.Ordinal);
            Assert.Contains("Manual tile", notification.Detail, StringComparison.Ordinal);
            Assert.Contains("does not establish the claimed drop", notification.Detail, StringComparison.Ordinal);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectCurrentAsync(submission.SubmissionId, setup.AdminId, "A second decision is not allowed."));
        Assert.Equal(3, await db.PersonalNotifications.CountAsync(x => x.Title == "evidence.rejected"));
    }

    [Fact]
    public async Task MaximumLengthNotesAndReasonsFitAuditAndNotificationBoundaries()
    {
        var setup = await SeedAsync(target: 10, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var suffix = Guid.NewGuid().ToString("N");
        var owner = Account.CreateWebsite(Guid.NewGuid(), $"boundary-owner-{suffix}", $"BOUNDARY-OWNER-{suffix}", now.AddDays(-2));
        (await db.EventParticipants.SingleAsync(x => x.Id == setup.ParticipantId)).AssignOwner(owner);
        db.Accounts.Add(owner);
        await db.SaveChangesAsync();

        var createNote = new string('"', 2_000) + new string('\\', 2_000);
        var editNote = new string('\\', 2_000) + new string('"', 2_000);
        var rejectionReason = new string('"', 2_000) + new string('\\', 2_000);
        var reversalReason = new string('\\', 2_000) + new string('"', 2_000);

        void AssertSnapshot(string? state, bool captainNotePresent, bool reviewerNotePresent, SubmissionStatus status)
        {
            Assert.NotNull(state);
            Assert.True(state!.Length <= 4_000);
            using var document = JsonDocument.Parse(state);
            var root = document.RootElement;
            Assert.Equal(captainNotePresent, root.GetProperty("CaptainNotePresent").GetBoolean());
            Assert.Equal(reviewerNotePresent, root.GetProperty("CurrentReviewerNotePresent").GetBoolean());
            Assert.Equal((int)status, root.GetProperty("Status").GetInt32());
            Assert.DoesNotContain("\"CaptainNote\":", state, StringComparison.Ordinal);
            Assert.DoesNotContain("\"CurrentReviewerNote\":", state, StringComparison.Ordinal);
            Assert.DoesNotContain(createNote, state, StringComparison.Ordinal);
            Assert.DoesNotContain(editNote, state, StringComparison.Ordinal);
            Assert.DoesNotContain(rejectionReason, state, StringComparison.Ordinal);
            Assert.DoesNotContain(reversalReason, state, StringComparison.Ordinal);
        }

        var service = Service(db);
        var created = await service.CreateAsync(Command(setup) with { CaptainNote = createNote });
        var createdAudit = Assert.Single(await db.AuditEntries.Where(x => x.TargetId == created.SubmissionId.ToString("D") && x.Action == "submission.created").ToListAsync());
        Assert.Equal(createNote, createdAudit.Details);
        Assert.Null(createdAudit.BeforeState);
        AssertSnapshot(createdAudit.AfterState, captainNotePresent: true, reviewerNotePresent: false, status: SubmissionStatus.Pending);

        await service.CorrectAsync(new CorrectSubmissionCommand(
            created.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            setup.ParticipantId, 1, editNote));
        var editedAudit = Assert.Single(await db.AuditEntries.Where(x => x.TargetId == created.SubmissionId.ToString("D") && x.Action == "submission.corrected").ToListAsync());
        Assert.Equal(editNote, editedAudit.Details);
        AssertSnapshot(editedAudit.BeforeState, captainNotePresent: true, reviewerNotePresent: false, status: SubmissionStatus.Pending);
        AssertSnapshot(editedAudit.AfterState, captainNotePresent: true, reviewerNotePresent: false, status: SubmissionStatus.Pending);
        Assert.Equal(editNote, await db.Submissions.Where(x => x.Id == created.SubmissionId).Select(x => x.CaptainNote).SingleAsync());

        var rejected = await service.CreateAsync(Command(setup) with { CaptainNote = null });
        await service.RejectCurrentAsync(rejected.SubmissionId, setup.AdminId, rejectionReason);
        var rejectionAudit = Assert.Single(await db.AuditEntries.Where(x => x.TargetId == rejected.SubmissionId.ToString("D") && x.Action == "submission.rejected").ToListAsync());
        Assert.Equal(rejectionReason, rejectionAudit.Details);
        AssertSnapshot(rejectionAudit.BeforeState, captainNotePresent: false, reviewerNotePresent: false, status: SubmissionStatus.Pending);
        AssertSnapshot(rejectionAudit.AfterState, captainNotePresent: false, reviewerNotePresent: true, status: SubmissionStatus.Rejected);
        Assert.Equal(rejectionReason, await db.Submissions.Where(x => x.Id == rejected.SubmissionId).Select(x => x.CurrentReviewerNote).SingleAsync());

        var notification = Assert.Single(await db.PersonalNotifications.AsNoTracking().Where(x => x.Title == "evidence.rejected" && x.RecipientAccountId == setup.CaptainId).ToListAsync());
        Assert.True(notification.Detail.Length <= 1_000);
        using var notificationDocument = JsonDocument.Parse(notification.Detail);
        var notificationRoot = notificationDocument.RootElement;
        Assert.Equal($"Event {setup.EventId:N}", notificationRoot.GetProperty("eventName").GetString());
        Assert.Equal("Manual tile", notificationRoot.GetProperty("tile").GetString());
        Assert.Equal("Test drop", notificationRoot.GetProperty("drop").GetString());
        var reasonExcerpt = notificationRoot.GetProperty("reason").GetString();
        Assert.NotNull(reasonExcerpt);
        Assert.NotEmpty(reasonExcerpt);
        Assert.True(reasonExcerpt!.Length < rejectionReason.Length);
        Assert.StartsWith(reasonExcerpt, rejectionReason, StringComparison.Ordinal);
        Assert.Equal($"/Submissions/{rejected.SubmissionId}", notification.Route);

        var approved = await service.CreateAsync(Command(setup) with { CaptainNote = null });
        await service.ApproveCurrentAsync(approved.SubmissionId, setup.AdminId);
        await service.ReverseCurrentAsync(approved.SubmissionId, setup.AdminId, reversalReason);
        var reversalAudit = Assert.Single(await db.AuditEntries.Where(x => x.TargetId == approved.SubmissionId.ToString("D") && x.Action == "submission.reversed").ToListAsync());
        Assert.Equal(reversalReason, reversalAudit.Details);
        AssertSnapshot(reversalAudit.BeforeState, captainNotePresent: false, reviewerNotePresent: false, status: SubmissionStatus.Approved);
        AssertSnapshot(reversalAudit.AfterState, captainNotePresent: false, reviewerNotePresent: true, status: SubmissionStatus.Reversed);
        Assert.Equal(reversalReason, await db.Submissions.Where(x => x.Id == approved.SubmissionId).Select(x => x.CurrentReviewerNote).SingleAsync());
    }

    [Fact]
    public async Task PendingEditsCanKeepOrReplaceTheActiveAsset()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var submission = await service.CreateAsync(Command(setup));
        await service.CorrectAsync(new CorrectSubmissionCommand(
            submission.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            setup.ParticipantId, 1, "clearer screenshot"));

        var assets = await db.EvidenceAssets.Where(x => x.SubmissionId == submission.SubmissionId).OrderBy(x => x.UploadedAt).ToListAsync();
        Assert.Single(assets);
        Assert.True(assets[0].Active);
        var originalAssetId = assets[0].Id;

        await using var replacement = new MemoryStream([1, 2, 3]);
        await service.CorrectAsync(new CorrectSubmissionCommand(
            submission.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            setup.ParticipantId, 1, "replacement screenshot", null, "replacement.png", replacement));

        assets = await db.EvidenceAssets.Where(x => x.SubmissionId == submission.SubmissionId).OrderBy(x => x.UploadedAt).ToListAsync();
        Assert.Equal(2, assets.Count);
        Assert.False(assets.Single(x => x.Id == originalAssetId).Active);
        Assert.Equal(EvidenceAssetRole.ReplacementEvidence, assets.Single(x => x.Active).Role);
        Assert.Contains(await db.ReviewActions.ToListAsync(), x => x.SubmissionId == submission.SubmissionId && x.Action == ReviewActionType.ReplaceEvidence);
    }

    [Fact]
    public async Task NeutralOwnerMutationPathRejectsTeamCaptainAndAllowsCreditedOwner()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var owner = Account.CreateWebsite(Guid.NewGuid(), "neutral-owner", "NEUTRAL OWNER", now.AddDays(-2));
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, "password"), false, now, false);
        db.Accounts.Add(owner);
        (await db.EventParticipants.SingleAsync(x => x.Id == setup.ParticipantId)).AssignOwner(owner);
        await db.SaveChangesAsync();

        var service = Service(db);
        var submission = await service.CreateAsync(Command(setup));
        var correction = new CorrectSubmissionCommand(submission.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            setup.ParticipantId, 1, "captain must not mutate neutral route", OwnerOnly: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectAsync(correction));

        await service.CorrectAsync(correction with { ActorAccountId = owner.Id, CaptainNote = "owner correction" });
        Assert.Equal("owner correction", await db.Submissions.Where(x => x.Id == submission.SubmissionId).Select(x => x.CaptainNote).SingleAsync());
    }

    [Fact]
    public async Task PostedWeightCannotOverrideBoardDefinedRequirementWeight()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);

        var result = await Service(db).CreateAsync(Command(setup) with { ClaimedWeight = 99 });

        Assert.Equal(1, await db.Submissions.Where(x => x.Id == result.SubmissionId).Select(x => x.ClaimedWeight).SingleAsync());
    }

    [Fact]
    public async Task PendingRetargetingPersistsTheDestinationSnapshotWeightForCaptainAndAdmin()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var originalDrop = await db.BoardRequirementDropSnapshots.SingleAsync(x => x.Id == setup.DropId);
        var alternateDropId = Guid.NewGuid();
        db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(
            alternateDropId, setup.RequirementId, Guid.NewGuid(), Guid.NewGuid(), "Alternate boss", "Alternate drop", "1/10", 0.1m,
            null, 1, 1));
        var alternate = db.BoardRequirementDropSnapshots.Local.Single(x => x.Id == alternateDropId);
        var publishedRequirement = await db.BoardApprovalRequirementSnapshots.SingleAsync(x => x.BoardRequirementSnapshotId == setup.RequirementId);
        db.BoardApprovalRequirementDropSnapshots.Add(new(Guid.NewGuid(), publishedRequirement.Id, alternate.SourceDropId, alternate.ItemIdSnapshot,
            alternate.BossName, alternate.ItemName, alternate.DisplayRate, alternate.NumericProbability, alternate.MaximumContribution, alternate.EhbPerContribution, alternate.CreditedWeight, 1));
        await db.SaveChangesAsync();
        var service = Service(db);

        var captainSubmission = await service.CreateAsync(Command(setup));
        await service.CorrectAsync(new CorrectSubmissionCommand(
            captainSubmission.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, alternateDropId,
            setup.ParticipantId, 99, "Retargeted by captain"));
        Assert.Equal(1, await db.Submissions.Where(x => x.Id == captainSubmission.SubmissionId).Select(x => x.ClaimedWeight).SingleAsync());

        var adminSubmission = await service.CreateAsync(Command(setup) with { DropSnapshotId = originalDrop.Id });
        var characterId = await db.EventParticipantCharacters
            .Where(x => x.EventParticipantId == setup.ParticipantId && x.ReleasedAt == null)
            .Select(x => x.OsrsCharacterId)
            .SingleAsync();
        const string adminReason = "  Retargeted by Admin  ";
        await service.EditMetadataCurrentAsync(new(
            adminSubmission.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, alternateDropId,
            characterId, adminReason));
        Assert.Equal(1, await db.Submissions.Where(x => x.Id == adminSubmission.SubmissionId).Select(x => x.ClaimedWeight).SingleAsync());
        var adminAudit = Assert.Single(await db.AuditEntries.Where(x => x.TargetId == adminSubmission.SubmissionId.ToString("D") && x.Action == "submission.corrected").ToListAsync());
        Assert.Equal("Retargeted by Admin", adminAudit.Details);
    }

    [Fact]
    public async Task ReversedSubmissionCanHaveOneReopenedOrdinaryAttemptAndKeepsInactivePredecessorContribution()
    {
        var setup = await SeedAsync(target: 2, allowHigherWeights: true);
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        (await db.Teams.SingleAsync(value => value.Id == setup.TeamId)).Finalize(now.AddDays(-2));
        await db.SaveChangesAsync();
        var service = Service(db, clock);
        var predecessor = await service.CreateAsync(Command(setup));
        await service.ApproveCurrentAsync(predecessor.SubmissionId, setup.AdminId);
        await service.ReverseCurrentAsync(predecessor.SubmissionId, setup.AdminId, "Reverse for corrected evidence.");

        Assert.Equal(SubmissionStatus.Reversed, await db.Submissions.Where(x => x.Id == predecessor.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.True(await db.SubmissionContributions.AnyAsync(x => x.SubmissionId == predecessor.SubmissionId && x.ReversedAt != null));
        Assert.Contains(await db.AuditEntries.Where(x => x.TargetId == predecessor.SubmissionId.ToString("D")).ToListAsync(), x => x.Action == "submission.reversed");

        var eventItem = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        eventItem.EndEvent(now.AddMinutes(-1));
        await db.SaveChangesAsync();
        clock.Set(eventItem.SubmissionCutoffAt!.Value.AddMinutes(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Command(setup) with { CaptainNote = "closed window" }));

        eventItem.ReopenSubmissions(clock.GetUtcNow().AddHours(1), clock.GetUtcNow());
        await db.SaveChangesAsync();
        clock.Set(now.AddMinutes(1));
        var attempt = await service.CreateAsync(Command(setup) with { CaptainNote = "corrected evidence" });
        Assert.Equal(SubmissionStatus.Pending, attempt.Status);
        Assert.Null(await db.Submissions.Where(x => x.Id == attempt.SubmissionId).Select(x => x.ResubmissionOfSubmissionId).SingleAsync());
        Assert.Contains(await db.AuditEntries.Where(x => x.TargetId == attempt.SubmissionId.ToString("D")).ToListAsync(), x => x.Action == "submission.created");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveCurrentAsync(predecessor.SubmissionId, setup.AdminId));
        var attemptSubmittedAt = await db.Submissions.Where(value => value.Id == attempt.SubmissionId).Select(value => value.SubmittedAt).SingleAsync();
        clock.Set(now.AddMinutes(2));
        await service.ApproveCurrentAsync(attempt.SubmissionId, setup.AdminId);
        Assert.True(await db.SubmissionContributions.AnyAsync(x => x.SubmissionId == attempt.SubmissionId && x.ReversedAt == null));
        Assert.True(await db.SubmissionContributions.AnyAsync(x => x.SubmissionId == predecessor.SubmissionId && x.ReversedAt != null));
        var board = await db.Boards.SingleAsync(value => value.EventId == setup.EventId);
        var fact = await db.TileCompletionFacts.SingleAsync(value => value.TeamId == setup.TeamId && value.BoardTileId == setup.TileId && value.ApprovalSnapshotId == board.ActiveApprovalSnapshotId);
        Assert.True(fact.IsComplete);
        Assert.Equal(attemptSubmittedAt, fact.CompletedAt);
        Assert.Contains(attempt.SubmissionId.ToString("D"), fact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(predecessor.SubmissionId.ToString("D"), fact.QualifyingContributionsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PendingCopyIsAllowedButApprovedNonDuplicateDropCannotBeSubmittedAgain()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: false, manualObjective: false, duplicatesAllowed: false);
        await using var db = new ApplicationDbContext(options);
        var clock = new MutableTimeProvider(now.AddMinutes(-20));
        var service = Service(db, clock);

        var first = await service.CreateAsync(Command(setup));
        clock.Set(now.AddMinutes(-10));
        var pendingCopy = await service.CreateAsync(Command(setup));
        Assert.Equal(SubmissionStatus.Pending, pendingCopy.Status);

        clock.Set(now);
        await service.ApproveCurrentAsync(first.SubmissionId, setup.AdminId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Command(setup)));
        Assert.Contains("approved contribution limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmissionSnapshotsTheCodeActiveAtServerSubmissionTime()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true, evidenceCode: "FUN-CODE");
        await using var db = new ApplicationDbContext(options);

        var result = await Service(db).CreateAsync(Command(setup));

        Assert.Equal("FUN-CODE", await db.Submissions.Where(x => x.Id == result.SubmissionId).Select(x => x.ExpectedEvidenceCode).SingleAsync());
    }

    [Fact]
    public async Task EnabledEvidenceCodeWithoutAnActiveIntervalBlocksSubmission()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true, evidenceCode: string.Empty);
        await using var db = new ApplicationDbContext(options);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).CreateAsync(Command(setup)));

        Assert.Contains("no code is active", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaptainAssignmentDoesNotCreateAccountsOrCredentialTokens()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var accountCount = await db.Accounts.CountAsync();
        var tokenCount = await db.PasswordCredentialTokens.CountAsync();
        var accessCount = await db.AccountEventAccesses.CountAsync();
        var membership = await db.TeamMemberships.SingleAsync(x => x.TeamId == setup.TeamId && x.EventParticipantId == setup.ParticipantId);
        membership.ChangeRole(TeamMembershipRole.Captain);
        await db.SaveChangesAsync();
        Assert.Equal(accountCount, await db.Accounts.CountAsync());
        Assert.Equal(tokenCount, await db.PasswordCredentialTokens.CountAsync());
        Assert.Equal(accessCount, await db.AccountEventAccesses.CountAsync());
    }

    [Fact]
    public async Task PublicProgressIsRebuiltFromApprovedActiveContributionsOnly()
    {
        var setup = await SeedAsync(target: 2, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(x => x.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));
        await db.SaveChangesAsync();
        var submissions = Service(db);
        var approved = await submissions.CreateAsync(Command(setup));
        await submissions.ApproveCurrentAsync(approved.SubmissionId, setup.AdminId);
        await submissions.CreateAsync(Command(setup));
        var publicBoards = new PublicBoardService(db, new FixedTimeProvider(now));

        var initial = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");

        var initialTeam = Assert.Single(initial!.Teams);
        var initialRosterPlayer = Assert.Single(initial.RosterPlayers!);
        Assert.Equal(setup.ParticipantId, initialRosterPlayer.PlayerId);
        Assert.Equal(["Player One"], initialRosterPlayer.PlayingAccountNames);
        Assert.Equal(1, Assert.Single(initialTeam.Tiles).Approved);
        Assert.False(initialTeam.Progress.BoardComplete);
        Assert.Single(initial.PlayerLeaderboard);
        var initialDropTeam = Assert.Single(initial.DropEhbTeams!);
        Assert.Equal(1, initialDropTeam.PlayerCount);
        Assert.Equal(1, initialDropTeam.ContributingPlayerCount);
        Assert.Equal(1, initialDropTeam.TotalDrops);
        var initialDropPlayer = Assert.Single(initialDropTeam.Players);
        Assert.Equal("Player One", initialDropPlayer.PlayerName);
        Assert.Equal(["Player One"], initialDropPlayer.PlayingAccountNames);
        Assert.Equal(initialDropTeam.DropEhb, initialDropTeam.Players.Sum(value => value.DropEhb));
        var recentDrop = Assert.Single(initial.RecentDrops);
        Assert.Equal(approved.SubmissionId, recentDrop.SubmissionId);
        Assert.Equal("Player One", recentDrop.PlayerName);
        Assert.NotNull(recentDrop.EvidenceAssetId);

        await submissions.ReverseCurrentAsync(approved.SubmissionId, setup.AdminId, "Wrong evidence");
        var reversed = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");
        var reversedTeam = Assert.Single(reversed!.Teams);
        var reversedRosterPlayer = Assert.Single(reversed.RosterPlayers!);
        Assert.Equal(initialRosterPlayer.PlayerId, reversedRosterPlayer.PlayerId);
        Assert.Equal(["Player One"], reversedRosterPlayer.PlayingAccountNames);
        Assert.Equal(0, Assert.Single(reversedTeam.Tiles).Approved);
        var zeroContributor = Assert.Single(reversedTeam.Progress.Players);
        Assert.Equal("Player One", zeroContributor.PlayerName);
        Assert.Equal(0, zeroContributor.EstimatedEhb);
        Assert.Equal(0, zeroContributor.ApprovedContribution);
        Assert.Equal(0, zeroContributor.ApprovedSubmissions);
        Assert.Empty(reversed.PlayerLeaderboard);
        var reversedDropTeam = Assert.Single(reversed.DropEhbTeams!);
        Assert.Equal(1, reversedDropTeam.PlayerCount);
        Assert.Equal(0, reversedDropTeam.ContributingPlayerCount);
        Assert.Equal(0, reversedDropTeam.TotalDrops);
        Assert.Equal(0, reversedDropTeam.DropEhb);
        var reversedDropPlayer = Assert.Single(reversedDropTeam.Players);
        Assert.Equal("Player One", reversedDropPlayer.PlayerName);
        Assert.Equal(0, reversedDropPlayer.ApprovedSubmissions);
        Assert.Empty(reversedDropTeam.MvpNames);
        Assert.Empty(reversed.RecentDrops);
    }

    [Fact]
    public async Task PublicBoardResultUsesProvisionalLeaderThenOfficialPlacementSnapshot()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(value => value.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));
        await db.SaveChangesAsync();

        var clock = new MutableTimeProvider(now);
        var publicBoards = new PublicBoardService(db, clock);
        var open = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");
        Assert.True(open!.SubmissionsOpen);
        Assert.Null(open.EventResult);

        var bingoEvent = await db.Events.SingleAsync(value => value.Id == setup.EventId);
        var reviewCycleId = Guid.NewGuid();
        bingoEvent.EndEvent(now);
        db.EventStateTransitions.Add(new EventStateTransition(
            reviewCycleId, bingoEvent.Id, EventState.Live, EventState.AwaitingFinalReview, setup.AdminId, now,
            "Test event ended", effectiveAt: now));
        await db.SaveChangesAsync();

        clock.Set(bingoEvent.SubmissionCutoffAt!.Value.AddTicks(1));
        var awaitingReview = await publicBoards.GetEventBoardAsync(bingoEvent.Slug);
        Assert.False(awaitingReview!.SubmissionsOpen);
        Assert.Equal(new PublicEventResult("Team One", $"team-{setup.TeamId:N}", false), awaitingReview.EventResult);

        var finalizedAt = clock.GetUtcNow().AddMinutes(1);
        bingoEvent.FinalizeResults(finalizedAt);
        var finalization = new EventFinalizationSnapshot(Guid.NewGuid(), bingoEvent.Id, 1, finalizedAt, setup.AdminId, reviewCycleId);
        db.EventFinalizations.Add(finalization);
        db.OfficialPlacements.Add(new OfficialPlacementSnapshot(
            Guid.NewGuid(), finalization.Id, bingoEvent.Id, setup.TeamId, "Historic Team One", 1,
            boardComplete: false, boardCompletedAt: null, completedLines: 0, completedTiles: 0, ehbTiebreak: 0));
        await db.SaveChangesAsync();

        var finalized = await publicBoards.GetEventBoardAsync(bingoEvent.Slug);
        Assert.Equal(new PublicEventResult("Historic Team One", $"team-{setup.TeamId:N}", true), finalized!.EventResult);
    }

    [Fact]
    public async Task PublicBoardResultRendersOfficialFirstPlaceTie()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(value => value.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));
        var secondTeamId = Guid.NewGuid();
        db.Teams.Add(new Team(secondTeamId, setup.EventId, "Team Two", $"team-{secondTeamId:N}", TeamFormationType.Drafted, null, true));
        var bingoEvent = await db.Events.SingleAsync(value => value.Id == setup.EventId);
        var finalizedAt = now.AddMinutes(1);
        var reviewCycleId = Guid.NewGuid();
        bingoEvent.EndEvent(now);
        db.EventStateTransitions.Add(new EventStateTransition(
            reviewCycleId, bingoEvent.Id, EventState.Live, EventState.AwaitingFinalReview, setup.AdminId, now,
            "Test event ended", effectiveAt: now));
        bingoEvent.FinalizeResults(finalizedAt);
        var finalization = new EventFinalizationSnapshot(Guid.NewGuid(), bingoEvent.Id, 1, finalizedAt, setup.AdminId, reviewCycleId);
        db.EventFinalizations.Add(finalization);
        db.OfficialPlacements.AddRange(
            new OfficialPlacementSnapshot(Guid.NewGuid(), finalization.Id, bingoEvent.Id, setup.TeamId, "Historic Team One", 1, false, null, 0, 0, 0),
            new OfficialPlacementSnapshot(Guid.NewGuid(), finalization.Id, bingoEvent.Id, secondTeamId, "Historic Team Two", 1, false, null, 0, 0, 0));
        await db.SaveChangesAsync();

        var finalized = await new PublicBoardService(db, new FixedTimeProvider(now.AddMinutes(2))).GetEventBoardAsync(bingoEvent.Slug);

        Assert.Equal("Historic Team One / Historic Team Two", finalized!.EventResult!.TeamName);
        Assert.Null(finalized.EventResult.TeamSlug);
        Assert.True(finalized.EventResult.IsOfficial);
        Assert.Equal(["Historic Team One", "Historic Team Two"], finalized.EventResult.Teams!.Select(value => value.TeamName).ToArray());
    }

    [Fact]
    public async Task PublicRecentDropsProjectHistoricalProgressBeforeVisibleLimit()
    {
        var setup = await SeedAsync(target: 30, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(x => x.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now);
        var submissions = Service(db, clock);
        for (var index = 0; index < 26; index++)
        {
            var submission = await submissions.CreateAsync(Command(setup));
            await submissions.ApproveCurrentAsync(submission.SubmissionId, setup.AdminId);
            clock.Set(clock.GetUtcNow().AddMinutes(1));
        }

        var board = await new PublicBoardService(db, clock).GetEventBoardAsync($"event-{setup.EventId:N}", 25);

        Assert.Equal(25, board!.RecentDrops.Count);
        Assert.Equal(Enumerable.Range(2, 25).OrderByDescending(value => value), board.RecentDrops.Select(value => value.ProgressAfter));
        Assert.All(board.RecentDrops, value => Assert.Equal(30, value.Target));
    }

    [Fact]
    public async Task PublicRecentDropsFilterTheFullApprovedSetBeforeVisibleLimit()
    {
        var setup = await SeedAsync(target: 30, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(x => x.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));
        await db.SaveChangesAsync();
        var clock = new MutableTimeProvider(now);
        var submissions = Service(db, clock);
        for (var index = 0; index < 26; index++)
        {
            var submission = await submissions.CreateAsync(Command(setup));
            await submissions.ApproveCurrentAsync(submission.SubmissionId, setup.AdminId);
            clock.Set(clock.GetUtcNow().AddMinutes(1));
        }

        var singleTermBoard = await new PublicBoardService(db, clock).GetEventBoardAsync(
            $"event-{setup.EventId:N}", 25, "test drop", $"team-{setup.TeamId:N}");
        var board = await new PublicBoardService(db, clock).GetEventBoardAsync(
            $"event-{setup.EventId:N}", 25, "test drop + no matching term", $"team-{setup.TeamId:N}");

        Assert.Equal(26, board!.RecentDropFilteredTotal!.Value);
        Assert.Equal(singleTermBoard!.RecentDropFilteredTotal, board.RecentDropFilteredTotal);
        Assert.Equal(25, board.RecentDrops.Count);
        Assert.All(board.RecentDrops, value => Assert.Equal("Team One", value.TeamName));
        Assert.Equal(Enumerable.Range(2, 25).OrderByDescending(value => value), board.RecentDrops.Select(value => value.ProgressAfter));
    }

    [Fact]
    public async Task PublicProgressAllocatesCombinedTileEhbAcrossTeamAndPlayerContributions()
    {
        var setup = await SeedAsync(target: 2, allowHigherWeights: false, tileEhb: 12, dropEhb: 100);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(x => x.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));
        await db.SaveChangesAsync();
        var submissions = Service(db);
        var first = await submissions.CreateAsync(Command(setup));
        await submissions.ApproveCurrentAsync(first.SubmissionId, setup.AdminId);
        var publicBoards = new PublicBoardService(db, new FixedTimeProvider(now));

        var partial = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");

        var partialTeam = Assert.Single(partial!.Teams);
        Assert.Equal(6, partialTeam.Progress.EhbTiebreak);
        Assert.Equal(6, Assert.Single(partial.PlayerLeaderboard).EstimatedEhb);

        var second = await submissions.CreateAsync(Command(setup));
        await submissions.ApproveCurrentAsync(second.SubmissionId, setup.AdminId);
        var complete = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");

        var completeTeam = Assert.Single(complete!.Teams);
        Assert.True(completeTeam.Progress.BoardComplete);
        Assert.Equal(12, completeTeam.Progress.EhbTiebreak);
        Assert.Equal(12, Assert.Single(complete.PlayerLeaderboard).EstimatedEhb);
    }

    [Fact]
    public async Task PublicProgressUsesEffectiveDropAmountsForRecentCompletionAndRankings()
    {
        var setup = await SeedAsync(target: 2, allowHigherWeights: false, duplicatesAllowed: false, tileEhb: 12, dropEhb: 100);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(value => value.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));

        var originalDrop = await db.BoardRequirementDropSnapshots.SingleAsync(value => value.Id == setup.DropId);
        var aliasDropId = Guid.NewGuid();
        var distinctDropId = Guid.NewGuid();
        db.BoardRequirementDropSnapshots.AddRange(
            new BoardRequirementDropSnapshot(aliasDropId, setup.RequirementId, Guid.NewGuid(), originalDrop.ItemIdSnapshot,
                "Alias boss", "Alias drop", "1/10", 0.1m, 1, 100, 1),
            new BoardRequirementDropSnapshot(distinctDropId, setup.RequirementId, Guid.NewGuid(), Guid.NewGuid(),
                "Distinct boss", "Distinct drop", "1/10", 0.1m, 1, 100, 1));
        // These aliases are part of this fixture's published rules, not private edits.
        var approvedRequirement = await db.BoardApprovalRequirementSnapshots.SingleAsync(x => x.BoardRequirementSnapshotId == setup.RequirementId);
        foreach (var drop in db.BoardRequirementDropSnapshots.Local.Where(x => x.Id == aliasDropId || x.Id == distinctDropId).ToList())
            db.BoardApprovalRequirementDropSnapshots.Add(new(Guid.NewGuid(), approvedRequirement.Id, drop.SourceDropId, drop.ItemIdSnapshot,
                drop.BossName, drop.ItemName, drop.DisplayRate, drop.NumericProbability, drop.MaximumContribution, drop.EhbPerContribution, drop.CreditedWeight, 1));
        var character = await (from assignment in db.EventParticipantCharacters
                               join osrsCharacter in db.OsrsCharacters on assignment.OsrsCharacterId equals osrsCharacter.Id
                               where assignment.EventId == setup.EventId && assignment.EventParticipantId == setup.ParticipantId
                               select new { assignment.OsrsCharacterId, osrsCharacter.DisplayName }).SingleAsync();
        var firstAt = now.AddMinutes(-15);
        var aliasAt = now.AddMinutes(-10);
        var distinctAt = now.AddMinutes(-5);
        var expectedDistinctAt = distinctAt.AddTicks(-(distinctAt.Ticks % TimeSpan.TicksPerMicrosecond));
        var first = AddApproved(setup.DropId!.Value, firstAt);
        var alias = AddApproved(aliasDropId, aliasAt);
        AddApproved(distinctDropId, distinctAt);
        await db.SaveChangesAsync();

        var board = await new PublicBoardService(db, new FixedTimeProvider(now)).GetEventBoardAsync($"event-{setup.EventId:N}", 3);

        var publicTeam = Assert.Single(board!.Teams);
        Assert.True(publicTeam.Progress.BoardComplete);
        Assert.Equal(expectedDistinctAt, publicTeam.Progress.BoardCompletedAt);
        Assert.Equal([2, 1, 1], board.RecentDrops.Select(value => value.ProgressAfter));
        Assert.Equal(1, board.RecentDrops.Single(value => value.SubmissionId == alias.Id).ProgressAfter);
        Assert.Equal(12, publicTeam.Progress.EhbTiebreak);
        var ranking = Assert.Single(board.PlayerLeaderboard);
        Assert.Equal(12, ranking.EstimatedEhb);
        Assert.Equal(2, ranking.ApprovedContribution);
        Assert.Equal(2, ranking.ApprovedSubmissions);

        Submission AddApproved(Guid dropId, DateTimeOffset submittedAt)
        {
            var submission = new Submission(Guid.NewGuid(), setup.EventId, setup.TeamId, setup.TileId, setup.RequirementId, dropId,
                setup.ParticipantId, character.OsrsCharacterId, character.DisplayName, setup.CaptainId, 1, submittedAt, null, null);
            submission.Approve(1, submittedAt);
            db.Submissions.Add(submission);
            db.SubmissionContributions.Add(new SubmissionContribution(Guid.NewGuid(), submission.Id, setup.TeamId, setup.RequirementId,
                dropId, setup.ParticipantId, 1, submittedAt));
            return submission;
        }
    }

    [Fact]
    public async Task PublicTileExposesApprovedEvidenceAndStoredPlayerSnapshot()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(x => x.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));
        await db.SaveChangesAsync();
        var submissions = Service(db);
        var result = await submissions.CreateAsync(Command(setup));
        await submissions.ApproveCurrentAsync(result.SubmissionId, setup.AdminId);
        var publicBoards = new PublicBoardService(db, new FixedTimeProvider(now));

        var board = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");
        var details = await publicBoards.GetTileAsync($"event-{setup.EventId:N}", $"team-{setup.TeamId:N}", setup.TileId);

        Assert.True(Assert.Single(board!.Teams).Progress.BoardComplete);
        Assert.Single(board.PlayerLeaderboard);
        var recentDrop = Assert.Single(board.RecentDrops);
        Assert.Equal("Player One", recentDrop.PlayerName);
        Assert.NotNull(recentDrop.DropName);
        Assert.NotNull(recentDrop.EvidenceAssetId);
        var evidence = Assert.Single(details!.Evidence);
        Assert.Equal("Player One", evidence.PlayerName);
        Assert.NotNull(evidence.EvidenceAssetId);
    }

    [Fact]
    public async Task RejectedSubmissionCanCreateRepeatedOrdinaryAttemptsWithImmutableCreditSnapshots()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var predecessor = await service.CreateAsync(Command(setup));
        await service.RejectCurrentAsync(predecessor.SubmissionId, setup.AdminId, "Show the full game message.");
        var original = await db.Submissions.SingleAsync(x => x.Id == predecessor.SubmissionId);

        var attempt = await service.CreateAsync(Command(setup) with { CaptainNote = "ordinary attempt" });

        var savedAttempt = await db.Submissions.SingleAsync(x => x.Id == attempt.SubmissionId);
        Assert.Equal(SubmissionStatus.Rejected, await db.Submissions.Where(x => x.Id == predecessor.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Null(savedAttempt.ResubmissionOfSubmissionId);
        Assert.Equal(original.CreditedParticipantId, savedAttempt.CreditedParticipantId);
        Assert.Equal(original.CreditedOsrsCharacterId, savedAttempt.CreditedOsrsCharacterId);
        Assert.Equal(original.CreditedCharacterName, savedAttempt.CreditedCharacterName);
        Assert.Single(await db.EvidenceAssets.Where(x => x.SubmissionId == attempt.SubmissionId && x.Active).ToListAsync());
        Assert.Contains(await db.ReviewActions.Where(x => x.SubmissionId == attempt.SubmissionId).ToListAsync(), x => x.Action == ReviewActionType.Submitted);
        Assert.Contains(await db.AuditEntries.Where(x => x.TargetId == attempt.SubmissionId.ToString("D")).ToListAsync(), x => x.Action == "submission.created");

        var repeated = await service.CreateAsync(Command(setup) with { CaptainNote = "repeated ordinary attempt" });
        Assert.NotEqual(attempt.SubmissionId, repeated.SubmissionId);
        Assert.Equal(SubmissionStatus.Pending, repeated.Status);
        Assert.Null(await db.Submissions.Where(x => x.Id == repeated.SubmissionId).Select(x => x.ResubmissionOfSubmissionId).SingleAsync());
    }

    [Fact]
    public async Task ParticipantAuthoritySeesOnlyItsOwnCandidateWhileWebsiteLeadershipSeesCurrentTeamCandidates()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var participantAccount = Account.CreateWebsite(Guid.NewGuid(), "participant", "PARTICIPANT", now.AddDays(-2));
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == setup.ParticipantId);
        participant.AssignOwner(participantAccount);
        var otherParticipantId = Guid.NewGuid(); var otherCharacterId = Guid.NewGuid();
        var otherParticipant = new EventParticipant(otherParticipantId, setup.EventId, SignupStatus.Confirmed, 2, now.AddDays(-2), SignupSource.AdminCreated);
        var otherCharacter = new OsrsCharacter(otherCharacterId, "Other Player", "OTHER PLAYER", now);
        var questionId = await db.SignupQuestions.Where(x => x.EventId == setup.EventId).Select(x => x.Id).SingleAsync();
        var otherAssignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, otherParticipantId, otherCharacterId, 0, now, setup.AdminId, questionId, EventCharacterRole.Playing, 1, EhbSource.Manual, null);
        db.AddRange(participantAccount, otherParticipant, otherCharacter, otherAssignment, new TeamMembership(Guid.NewGuid(), setup.TeamId, otherParticipantId, TeamMembershipRole.Participant, now, null, null));
        await db.SaveChangesAsync();

        var authority = new EvidenceAuthority(db);
        var participantScope = await authority.ResolveActorAsync(participantAccount.Id, setup.EventId, setup.TeamId, now, CancellationToken.None);
        var participantCandidates = await authority.GetCurrentTeamCandidatesAsync(participantScope, now, CancellationToken.None);
        Assert.Single(participantCandidates);
        Assert.Equal(setup.ParticipantId, participantCandidates[0].ParticipantId);

        var emergencyScope = await authority.ResolveActorAsync(setup.CaptainId, setup.EventId, setup.TeamId, now, CancellationToken.None);
        var leadershipCandidates = await authority.GetCurrentTeamCandidatesAsync(emergencyScope, now, CancellationToken.None);
        Assert.Equal(2, leadershipCandidates.Count);
    }

    [Theory]
    [InlineData(TeamMembershipRole.Captain, false)]
    [InlineData(TeamMembershipRole.CoCaptain, false)]
    [InlineData(TeamMembershipRole.Captain, true)]
    public async Task CreationRechecksDemotionCommittedAfterInitialAuthorization(TeamMembershipRole role, bool self)
    {
        var setup = await SeedAsync(3, true);
        var (actorId, membershipId) = await AddWebsiteCaptainAsync(setup, role, self);
        var demotion = new BeforeSubmissionTransaction(async () =>
        {
            await using var roles = new ApplicationDbContext(options);
            var changed = await new TeamCaptainAuthorityService(roles, new FixedTimeProvider(now)).ChangeRoleAsync(
                new(setup.EventId, membershipId, TeamMembershipRole.Participant, setup.AdminId, "admin"));
            Assert.True(changed.Succeeded, changed.Error);
        });
        var submissionOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(demotion).Options;
        await using var db = new ApplicationDbContext(submissionOptions);
        // A previously tracked role must not override the authoritative reread.
        await db.TeamMemberships.LoadAsync();
        var storage = new RecordingEvidenceStorage();
        var command = Command(setup) with { ActorAccountId = actorId };
        if (self)
            Assert.Equal(SubmissionStatus.Pending, (await Service(db, storage: storage).CreateAsync(command)).Status);
        else
        {
            var denied = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, storage: storage).CreateAsync(command));
            Assert.Contains("only for themselves", denied.Message);
        }
        Assert.True(demotion.Called);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(self ? 1 : 0, await verify.Submissions.CountAsync());
        Assert.Equal(self ? 1 : 0, await verify.EvidenceAssets.CountAsync());
        Assert.Equal(self ? 1 : 0, storage.StoredCount);
        Assert.Equal(self ? 1 : 0, await verify.AuditEntries.CountAsync(x => x.Action == "submission.created"));
    }

    [Theory]
    [InlineData(TeamMembershipRole.Captain)]
    [InlineData(TeamMembershipRole.CoCaptain)]
    public async Task CreationAllowsCurrentWebsiteLeadershipForTeammates(TeamMembershipRole role)
    {
        var setup = await SeedAsync(3, true);
        var (actorId, _) = await AddWebsiteCaptainAsync(setup, role, false);
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(SubmissionStatus.Pending, (await Service(db).CreateAsync(Command(setup) with { ActorAccountId = actorId })).Status);
        Assert.Single(await db.EvidenceAssets.ToListAsync());
    }

    [Fact]
    public async Task CreationWaitsForAnInFlightRoleMutationAtTheEventBoundary()
    {
        var setup = await SeedAsync(3, true);
        var (actorId, membershipId) = await AddWebsiteCaptainAsync(setup, TeamMembershipRole.Captain, false);
        await using var roles = new ApplicationDbContext(options);
        await using var roleTransaction = await roles.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var changed = await new TeamCaptainAuthorityService(roles, new FixedTimeProvider(now)).ChangeRoleAsync(
            new(setup.EventId, membershipId, TeamMembershipRole.Participant, setup.AdminId, "admin"));
        Assert.True(changed.Succeeded, changed.Error);
        var storage = new RecordingEvidenceStorage();
        await using var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        var create = Service(db, storage: storage).CreateAsync(Command(setup) with { ActorAccountId = actorId });
        var blocked = await WaitForDatabaseBlockAsync(create, db, roles);
        // The direct role mutation holds the same event boundary as live withdrawal.
        await roleTransaction.CommitAsync();
        var conflict = await Record.ExceptionAsync(() => create.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(blocked, "Creation must wait on the in-flight role mutation at its event boundary.");
        var postgres = Assert.IsType<PostgresException>(conflict?.GetBaseException());
        Assert.Equal(PostgresErrorCodes.SerializationFailure, postgres.SqlState);
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.Submissions.ToListAsync());
        Assert.Empty(await verify.EvidenceAssets.ToListAsync());
        Assert.Equal(0, storage.StoredCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveWithdrawalIsRetiredWithoutMutationAndDoesNotRevokeCurrentSubmissionAuthority(bool self)
    {
        var setup = await SeedAsync(3, true);
        var (actorId, membershipId) = await AddWebsiteCaptainAsync(setup, TeamMembershipRole.Captain, self);
        Guid participantId;
        await using (var fixture = new ApplicationDbContext(options))
            participantId = await fixture.TeamMemberships.Where(x => x.Id == membershipId).Select(x => x.EventParticipantId).SingleAsync();

        var before = await RetiredLiveWithdrawalStateAsync(setup.EventId, participantId, membershipId);
        await using (var db = new ApplicationDbContext(options))
        {
            var result = await new SignupService(db, new SecretHasher(), new FixedTimeProvider(now)).WithdrawLiveAsync(
                new(setup.EventId, participantId, setup.AdminId, "admin"));
            Assert.False(result.Succeeded);
            Assert.False(result.Changed);
            Assert.Contains("Roster membership is fixed after the event first goes Live.", result.Error, StringComparison.Ordinal);
            Assert.Null(result.MembershipId);
            Assert.Null(result.ParticipantId);
            Assert.Null(result.EffectiveAtUtc);
        }

        // The retired command must not alter the participant, registration, current
        // membership/role or any retained history, evidence, vacancy, replacement,
        // or Wise Old Man operation before the still-authorized create is attempted.
        Assert.Equal(before, await RetiredLiveWithdrawalStateAsync(setup.EventId, participantId, membershipId));

        var storage = new RecordingEvidenceStorage();
        await using var create = new ApplicationDbContext(options);
        var submitted = await Service(create, storage: storage).CreateAsync(Command(setup) with { ActorAccountId = actorId });
        Assert.Equal(SubmissionStatus.Pending, submitted.Status);
        Assert.Equal(1, storage.StoredCount);
        Assert.Single(await create.EvidenceAssets.Where(x => x.SubmissionId == submitted.SubmissionId && x.Active).ToListAsync());
        Assert.Single(await create.ReviewActions.Where(x => x.SubmissionId == submitted.SubmissionId && x.Action == ReviewActionType.Submitted).ToListAsync());
        Assert.Single(await create.AuditEntries.Where(x => x.EventId == setup.EventId && x.TargetId == submitted.SubmissionId.ToString("D") && x.Action == "submission.created").ToListAsync());
    }

    private async Task<bool> WaitForDatabaseBlockAsync(Task operation, ApplicationDbContext waiting, ApplicationDbContext blocking,
        CancellationToken cancellationToken = default)
    {
        // Identify the actual backends; pg_stat_activity query text can be truncated.
        var waitingPid = ((NpgsqlConnection)waiting.Database.GetDbConnection()).ProcessID;
        var blockingPid = ((NpgsqlConnection)blocking.Database.GetDbConnection()).ProcessID;
        await using var monitor = new NpgsqlConnection(database.GetOwnedConnectionString());
        await monitor.OpenAsync(cancellationToken);
        for (var attempt = 0; attempt < 100 && !operation.IsCompleted; attempt++)
        {
            await using var query = new NpgsqlCommand("SELECT @blockingPid = ANY(pg_blocking_pids(@waitingPid))", monitor);
            query.Parameters.AddWithValue("blockingPid", blockingPid);
            query.Parameters.AddWithValue("waitingPid", waitingPid);
            if ((bool)(await query.ExecuteScalarAsync(cancellationToken))!) return true;
            await Task.Delay(25, cancellationToken);
        }
        return false;
    }

    private async Task<(Guid ActorId, Guid MembershipId)> AddWebsiteCaptainAsync(Setup setup, TeamMembershipRole role, bool self)
    {
        await using var db = new ApplicationDbContext(options);
        var actor = Account.CreateWebsite(Guid.NewGuid(), "current-captain", "CURRENT-CAPTAIN", now);
        var participant = self ? await db.EventParticipants.SingleAsync(x => x.Id == setup.ParticipantId)
            : new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 2, now, SignupSource.Website);
        participant.AssignOwner(actor);
        var membership = self ? await db.TeamMemberships.SingleAsync(x => x.EventParticipantId == participant.Id)
            : new TeamMembership(Guid.NewGuid(), setup.TeamId, participant.Id, role, now, null, "test");
        membership.ChangeRole(role);
        db.Accounts.Add(actor);
        if (!self) db.AddRange(participant, membership);
        await db.SaveChangesAsync();
        return (actor.Id, membership.Id);
    }

    private async Task<string> RetiredLiveWithdrawalStateAsync(Guid eventId, Guid participantId, Guid membershipId)
    {
        await using var db = new ApplicationDbContext(options);
        var state = new
        {
            Event = await db.Events.AsNoTracking().Where(x => x.Id == eventId)
                .Select(x => new { x.State, x.DraftLocked, x.Version, x.BoardPublished, x.StatsEvidenceRevision, x.CancelledAt, x.CancelledByAccountId, x.CancellationReason })
                .SingleAsync(),
            Participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.AccountId, x.SignupStatus, x.SignupSequence, x.SignedUpAt, x.ConfirmedAt, x.WaitingListedAt, x.WithdrawnAt, x.WithdrawnByAccountId, x.StatusReason, x.FormVersion, x.ResponseVersion, x.Source })
                .ToListAsync(),
            Registrations = await db.EventParticipantCharacters.AsNoTracking().Where(x => x.EventParticipantId == participantId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.EventParticipantId, x.OsrsCharacterId, x.RegistrationOrder, x.RegisteredAt, x.RegisteredByAccountId, x.SignupQuestionId, x.EventRole, x.EhbSnapshot, x.EhbSource, x.EhbFetchedAt, x.ReleasedAt, x.ReleasedByAccountId, x.Version })
                .ToListAsync(),
            Memberships = await db.TeamMemberships.AsNoTracking().Where(x => db.Teams.Any(team => team.Id == x.TeamId && team.EventId == eventId)).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.TeamId, x.EventParticipantId, x.Role, x.JoinedAt, x.LeftAt, x.AssignedByDraftPickId, x.Source, x.ReplacesMembershipId, x.Version, x.AssignmentReason })
                .ToListAsync(),
            PublishedRoster = await db.DraftPublicationRosters.AsNoTracking().Where(x => db.Teams.Any(team => team.Id == x.TeamId && team.EventId == eventId)).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.DraftPublicationCycleId, x.TeamId, x.EventParticipantId, x.Role, x.EffectivePickNumber, x.PublicCharacterName })
                .ToListAsync(),
            RoleTransitions = await db.TeamMembershipRoleTransitions.AsNoTracking().Where(x => x.TeamMembershipId == membershipId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.TeamMembershipId, x.FromRole, x.ToRole, x.ChangedByAccountId, x.ChangedAt })
                .ToListAsync(),
            EventHistory = await db.EventStateTransitions.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.FromState, x.ToState, x.PerformedByAccountId, x.PerformedAt, x.EffectiveAt, x.Reason, x.Scheduled })
                .ToListAsync(),
            Submissions = await db.Submissions.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Status, x.Version, x.CreditedParticipantId, x.CreditedOsrsCharacterId, x.SubmittedByAccountId, x.SubmittedAt, x.ReviewedAt, x.ResubmissionOfSubmissionId })
                .ToListAsync(),
            EvidenceAssets = await db.EvidenceAssets.AsNoTracking().Where(x => db.Submissions.Any(submission => submission.Id == x.SubmissionId && submission.EventId == eventId)).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.SubmissionId, x.StorageKey, x.Active, x.UploadedAt, x.UploadedByAccountId, x.Role })
                .ToListAsync(),
            ReviewActions = await db.ReviewActions.AsNoTracking().Where(x => db.Submissions.Any(submission => submission.Id == x.SubmissionId && submission.EventId == eventId)).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.SubmissionId, x.Action, x.PerformedByAccountId, x.PerformedAt, x.Note })
                .ToListAsync(),
            Contributions = await db.SubmissionContributions.AsNoTracking().Where(x => db.Submissions.Any(submission => submission.Id == x.SubmissionId && submission.EventId == eventId)).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.SubmissionId, x.TeamId, x.RequirementId, x.DropSnapshotId, x.CreditedParticipantId, x.Amount, x.AppliedAt, x.ReversedAt })
                .ToListAsync(),
            Audits = await db.AuditEntries.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.OccurredAt, x.ActorAccountId, x.Action, x.TargetType, x.TargetId, x.Details, x.BeforeState, x.AfterState })
                .ToListAsync(),
            Notifications = await db.PersonalNotifications.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.RecipientAccountId, x.Title, x.Detail, x.Route, x.CreatedAt, x.ReadAt, x.EventId })
                .ToListAsync(),
            CharacterSwaps = await db.EventParticipantCharacterSwaps.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.EventParticipantId, x.PreviousOsrsCharacterId, x.NextOsrsCharacterId, x.EffectiveAtUtc, x.RecordedAtUtc, x.RecordedByAccountId, x.Reason, x.Sequence })
                .ToListAsync(),
            PromotionFollowUps = await db.WaitingListPromotionFollowUps.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.EndedMembershipId, x.ReplacementMembershipId, x.PromotedParticipantId, x.CreatedAt, x.CompletedByAccountId, x.CompletedAt })
                .ToListAsync(),
            Wom = new
            {
                Management = await db.EventCompetitionManagements.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(),
                Synchronization = await db.EventCompetitionSynchronizations.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(),
                Operations = await db.EventCompetitionManagementOperations.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(),
                UpdateAllSlots = await db.EventCompetitionUpdateAllSlots.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(),
                CharacterActivities = await db.EventCompetitionCharacterActivities.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(),
                CharacterMetrics = await db.EventCompetitionCharacterMetricActivities.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.OsrsCharacterId).ThenBy(x => x.Metric)
                    .Select(x => new { x.EventId, x.Generation, x.OsrsCharacterId, x.Metric, x.AssignmentFingerprint }).ToListAsync()
            }
        };
        return JsonSerializer.Serialize(state);
    }

    private sealed class BeforeSubmissionTransaction(Func<Task> before) : DbTransactionInterceptor
    {
        public bool Called { get; private set; }
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            Called = true;
            await before();
            return result;
        }
    }

    private async Task<SubmissionResult> CreateApprovedAsync(Setup setup, MutableTimeProvider clock, DateTimeOffset submittedAt, int claimedWeight)
    {
        clock.Set(submittedAt);
        SubmissionResult submission;
        await using (var create = new ApplicationDbContext(options))
            submission = await Service(create, clock).CreateAsync(Command(setup) with { ClaimedWeight = claimedWeight });
        clock.Set(submittedAt.AddMinutes(1));
        await using (var review = new ApplicationDbContext(options))
            await Service(review, clock).ApproveCurrentAsync(submission.SubmissionId, setup.AdminId);
        return submission;
    }

    private SubmissionService Service(ApplicationDbContext db, TimeProvider? clock = null, IEvidenceStorage? storage = null, IProgressNotifier? notifier = null) => new(db, storage ?? new FakeEvidenceStorage(), clock ?? new FixedTimeProvider(now), notifier);

    private static CreateSubmissionCommand Command(Setup setup) => new(
        setup.CaptainId, setup.EventId, setup.TeamId, setup.TileId, setup.RequirementId, setup.DropId,
        setup.ParticipantId, 1, "captain note", "proof.png", new MemoryStream([1, 2, 3]));

    private async Task<Setup> SeedAsync(int target, bool allowHigherWeights, string? evidenceCode = null, bool manualObjective = false, bool duplicatesAllowed = true, int? dropMaximum = null, decimal tileEhb = 1, decimal dropEhb = 1, int boardRows = 1, int boardColumns = 1, bool createAlternateWeightDrop = false, int additionalObjectivesPerTile = 0, string? identitySuffix = null)
    {
        await using var db = new ApplicationDbContext(options);
        var eventId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var captainId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var tileId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        var dropId = manualObjective ? (Guid?)null : Guid.NewGuid();
        var ev = new BingoEvent(eventId, $"Event {eventId:N}", $"event-{eventId:N}", "", "UTC", now.AddDays(-10), now.AddDays(-8), now.AddHours(-1), now.AddHours(4), now.AddHours(4.5), 20, adminId, now.AddDays(-20));
        ev.OpenSignups();
        ev.MarkFirstPublic(now.AddDays(-8));
        ev.CloseSignups();
        ev.SetDraftRosterPublication(true);
        ev.StartEvent(now.AddHours(-1));
        if (evidenceCode is not null) ev.SetEvidenceCodeEnabled(true);
        var team = new Team(teamId, eventId, "Team One", $"team-{teamId:N}", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(participantId, eventId, SignupStatus.Confirmed, 1, now.AddDays(-5), SignupSource.Website);
        var form = new SignupForm(Guid.NewGuid(), eventId, now.AddDays(-5));
        var primaryQuestion = new SignupQuestion(Guid.NewGuid(), form.Id, eventId, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var suffix = identitySuffix ?? string.Empty;
        var captain = Account.CreateWebsite(captainId, $"captain{suffix}", $"CAPTAIN{suffix}", now.AddDays(-10));
        var captainParticipant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1000, now.AddDays(-5), SignupSource.Website);
        captainParticipant.AssignOwner(captain);
        var captainMembership = new TeamMembership(Guid.NewGuid(), teamId, captainParticipant.Id, TeamMembershipRole.Captain, now.AddDays(-4), null, null);
        var captainAccess = new AccountEventAccess(Guid.NewGuid(), captainId, eventId, teamId, participantId, now.AddDays(-1), now.AddHours(5), now.AddHours(30));
        captainAccess.Enable();
        var admin = Account.CreateWebsite(adminId, $"admin{suffix}", $"ADMIN{suffix}", now.AddDays(-10));
        admin.SetGlobalRole(GlobalRole.Admin);
        var board = new Board(boardId, eventId, "Board", boardRows, boardColumns);
        var tileIds = Enumerable.Range(0, boardRows * boardColumns).Select(index => index == 0 ? tileId : Guid.NewGuid()).ToList();
        var objectivesPerTile = additionalObjectivesPerTile + 1;
        var requirementIds = Enumerable.Range(0, tileIds.Count * objectivesPerTile).Select(index => index == 0 ? requirementId : Guid.NewGuid()).ToList();
        var dropIds = Enumerable.Range(0, requirementIds.Count).Select(index => manualObjective ? (Guid?)null : index == 0 ? dropId : Guid.NewGuid()).ToList();
        var tiles = tileIds.Select((id, index) => new BoardTile(id, boardId, Guid.NewGuid(), index / boardColumns, index % boardColumns,
            "Manual tile", "Complete it", "Show the message", tileEhb)).ToList();
        var requirements = requirementIds.Select((id, index) => new BoardRequirementSnapshot(id, tileIds[index / objectivesPerTile], index % objectivesPerTile, target, duplicatesAllowed,
            allowHigherWeights, "Complete runs", manualObjective, allowHigherWeights ? 2 : 1)).ToList();
        var drops = requirements.Select((requirement, index) => dropIds[index] is Guid eligibleDropId
            ? new BoardRequirementDropSnapshot(eligibleDropId, requirement.Id, Guid.NewGuid(), Guid.NewGuid(), "Test boss", "Test drop", "1/10", 0.1m,
                dropMaximum ?? (duplicatesAllowed ? null : 1), dropEhb, createAlternateWeightDrop && index == 0 ? 1 : allowHigherWeights ? 2 : 1)
            : null).Where(value => value is not null).Cast<BoardRequirementDropSnapshot>().ToList();
        Guid? alternateDropId = null;
        if (createAlternateWeightDrop)
        {
            alternateDropId = Guid.NewGuid();
            drops.Add(new BoardRequirementDropSnapshot(alternateDropId.Value, requirements[0].Id, Guid.NewGuid(), Guid.NewGuid(), "Alternate test boss", "Alternate test drop", "1/20", 0.05m,
                dropMaximum ?? (duplicatesAllowed ? null : 1), dropEhb, 2));
        }
        var character = new OsrsCharacter(Guid.NewGuid(), $"Player One{suffix}", $"PLAYER ONE{suffix}", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), eventId, participantId, character.Id, 0, now, adminId, primaryQuestion.Id, EventCharacterRole.Playing, 500, EhbSource.Manual, null);
        var draft = new DraftSession(Guid.NewGuid(), eventId, 1);
        draft.FinalizeDirect(now.AddDays(-1));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddDays(-1), adminId, DraftPublicationMethod.DirectRoster);
        var publishedRoster = new DraftPublicationRoster(Guid.NewGuid(), publication.Id, teamId, participantId,
            TeamMembershipRole.Participant, null, character.DisplayName);
        db.AddRange(ev, form, primaryQuestion, team, participant, character, assignment, captain, captainParticipant, captainMembership, admin, board, captainAccess,
            draft, publication, publishedRoster,
            new TeamMembership(Guid.NewGuid(), teamId, participantId, TeamMembershipRole.Participant, now.AddDays(-4), null, null));
        db.AddRange(tiles);
        db.AddRange(requirements);
        db.BoardRequirementDropSnapshots.AddRange(drops);
        if (!string.IsNullOrEmpty(evidenceCode)) db.EvidenceCodes.Add(new EvidenceCode(Guid.NewGuid(), eventId, evidenceCode, now.AddMinutes(-10), adminId, now.AddMinutes(-10), null));
        await BoardApprovalFixture.PublishAsync(db, board, now.AddDays(-1), tiles, requirements, drops);
        return new Setup(eventId, teamId, participantId, captainId, adminId, tileId, requirementId, dropId, tileIds, requirementIds, dropIds, alternateDropId);
    }

    private sealed record Setup(Guid EventId, Guid TeamId, Guid ParticipantId, Guid CaptainId, Guid AdminId, Guid TileId, Guid RequirementId, Guid? DropId,
        IReadOnlyList<Guid>? TileIds = null, IReadOnlyList<Guid>? RequirementIds = null, IReadOnlyList<Guid?>? DropIds = null, Guid? AlternateDropId = null);

    private sealed class PauseAfterTileCompletionFacts : DbCommandInterceptor
    {
        private int paused;
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("tile_completion_facts", StringComparison.OrdinalIgnoreCase) &&
                Interlocked.Exchange(ref paused, 1) == 0)
            {
                Ready.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class PauseAfterEventLock : DbCommandInterceptor
    {
        private int paused;
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM events", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase) &&
                Interlocked.Exchange(ref paused, 1) == 0)
            {
                Ready.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset current = value;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }

    private sealed class FakeEvidenceStorage : IEvidenceStorage
    {
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoredEvidence($"{eventId}/{submissionId}.png", originalFilename, "image/png", 3, 1, 1, new string('a', 64)));
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingEvidenceStorage : IEvidenceStorage
    {
        public int StoredCount { get; private set; }
        public CancellationTokenSource? CancelAfterStore { get; set; }
        public List<string> DeletedTokens { get; } = [];
        public List<CancellationToken> DeleteTokens { get; } = [];
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default)
        {
            StoredCount++;
            CancelAfterStore?.Cancel();
            return Task.FromResult(new StoredEvidence($"{eventId}/{submissionId}.png", originalFilename, "image/png", 3, 1, 1, new string('b', 64)));
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1]));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { DeletedTokens.Add(storageKey); DeleteTokens.Add(cancellationToken); return Task.CompletedTask; }
    }

    private sealed class CancellingNotifier(CancellationTokenSource source) : IProgressNotifier
    {
        public Task NotifyProgressChangedAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            source.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class RecordingProgressNotifier : IProgressNotifier
    {
        public List<Guid> EventIds { get; } = [];

        public Task NotifyProgressChangedAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            EventIds.Add(eventId);
            return Task.CompletedTask;
        }
    }

    private sealed class PassthroughLocalizer : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
