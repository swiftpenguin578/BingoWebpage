using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class SubmissionWorkflowTests
{
    [Fact]
    public async Task B5CorrectionRetainsFormerPlayingIdentityThroughReadinessAndResults()
    {
        var setup = await SeedAsync(2, true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var created = await service.CreateAsync(Command(setup));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCorrectionCharactersAsync(created.SubmissionId, setup.CaptainId));
        var before = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == created.SubmissionId);
        var assets = JsonSerializer.Serialize(await db.EvidenceAssets.AsNoTracking().ToListAsync());
        var former = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 10, now.AddDays(-5), SignupSource.Website);
        var character = new OsrsCharacter(Guid.NewGuid(), "Former player", "FORMER PLAYER", now.AddDays(-5));
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, former.Id, character.Id, 0, now.AddDays(-5), setup.AdminId, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null);
        assignment.Release(setup.AdminId, now.AddMinutes(-1));
        var membership = new TeamMembership(Guid.NewGuid(), setup.TeamId, former.Id, TeamMembershipRole.Participant, now.AddDays(-4), null, null);
        membership.Leave(now.AddMinutes(1), "Retained membership");
        db.AddRange(former, character, assignment, membership);
        await db.SaveChangesAsync();
        var choice = Assert.Single(await service.GetCorrectionCharactersAsync(created.SubmissionId, setup.AdminId), x => x.CharacterId == character.Id);
        Assert.True(choice.Released); Assert.True(choice.LeftTeam); Assert.False(choice.Current);
        await service.EditMetadataAsync(new(created.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character.Id, "Correct retained account", before.Version));
        var corrected = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == created.SubmissionId);
        Assert.Equal(former.Id, corrected.CreditedParticipantId); Assert.Equal(setup.TeamId, corrected.TeamId);
        Assert.Equal(before.SubmittedAt, corrected.SubmittedAt); Assert.Equal(assets, JsonSerializer.Serialize(await db.EvidenceAssets.AsNoTracking().ToListAsync()));
        var action = await db.ReviewActions.SingleAsync(x => x.SubmissionId == created.SubmissionId && x.Action == ReviewActionType.EditMetadata);
        Assert.Contains(former.Id.ToString(), action.AfterSnapshot); Assert.Contains(before.CreditedParticipantId.ToString(), action.BeforeSnapshot);
        Assert.Single(await db.AuditEntries.Where(x => x.Action == "submission.corrected").ToListAsync());
        await service.ApproveAsync(created.SubmissionId, setup.AdminId, expectedVersion: corrected.Version);
        var clock = new FixedTimeProvider(now.AddHours(5));
        var ev = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        ev.EndEvent(now); ev.CloseSubmissionsIfDue(clock.GetUtcNow());
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, EventState.Live, EventState.AwaitingFinalReview, setup.AdminId, now, "End", effectiveAt: now));
        await db.SaveChangesAsync();
        var boards = new PublicBoardService(db, clock);
        var board = (await boards.GetEventBoardAsync(ev.Slug))!;
        Assert.Equal(2, Assert.Single(Assert.Single(board.Teams).Progress.Players, x => x.PlayerId == former.Id).ApprovedContribution);
        Assert.Contains(board.PlayerLeaderboard, x => x.PlayerId == former.Id && x.ApprovedContribution == 2);
        Assert.Contains(Assert.Single(board.DropEhbTeams!).Players, x => x.PlayerId == former.Id && x.ApprovedContribution == 2);
        Assert.Equal(1, Assert.Single(board.DropEhbTeams!).PlayerCount);
        Assert.DoesNotContain(board.RosterPlayers!, x => x.PlayerId == former.Id);
        var finals = new EventFinalizationService(db, boards, clock);
        var readiness = (await finals.GetReadinessAsync(ev.Id))!;
        Assert.Empty(readiness.Blockers); Assert.Single(readiness.Placements);
        await finals.FinalizeAsync(ev.Id, new LifecycleActor(setup.AdminId, "admin"), ev.Version);
        Assert.Single(await db.OfficialPlacements.ToListAsync());
        var published = (await boards.GetEventBoardAsync(ev.Slug))!;
        Assert.Equal(2, Assert.Single(Assert.Single(published.Teams).Progress.Players, x => x.PlayerId == former.Id).ApprovedContribution);
        Assert.NotNull(published.EventResult);
    }

    [Theory]
    [InlineData("informational")]
    [InlineData("never-member")]
    [InlineData("ambiguous")]
    [InlineData("outside-event")]
    public async Task B5CorrectionInvalidAttributionLeavesNoPartialWrite(string invalid)
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var created = await Service(db).CreateAsync(Command(setup));
        var s = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == created.SubmissionId);
        var character = new OsrsCharacter(Guid.NewGuid(), "Candidate", "CANDIDATE", now);
        db.OsrsCharacters.Add(character);
        var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 10, now, SignupSource.Website);
        db.EventParticipants.Add(participant);
        if (invalid != "outside-event")
        {
            var assignment = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 0, now, setup.AdminId, null,
                invalid == "informational" ? EventCharacterRole.Informational : EventCharacterRole.Playing,
                invalid == "informational" ? null : 1, invalid == "informational" ? null : EhbSource.Manual, null);
            if (invalid == "ambiguous") assignment.Release(setup.AdminId, now);
            db.EventParticipantCharacters.Add(assignment);
            if (invalid == "ambiguous") db.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, setup.ParticipantId, character.Id, 1, now, setup.AdminId, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null));
        }
        if (invalid != "never-member") db.TeamMemberships.Add(new TeamMembership(Guid.NewGuid(), setup.TeamId, participant.Id, TeamMembershipRole.Participant, now, null, null));
        await db.SaveChangesAsync();
        var baseline = await B5EvidenceStateAsync();
        Assert.DoesNotContain(await Service(db).GetCorrectionCharactersAsync(s.Id, setup.AdminId), x => x.CharacterId == character.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).EditMetadataAsync(new(s.Id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character.Id, "Invalid correction", s.Version)));
        Assert.Equal(baseline, await B5EvidenceStateAsync());
    }

    [Fact]
    public async Task B5CorrectionRefusesCharacterRoleChangedAfterPickerLoad()
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var created = await Service(db).CreateAsync(Command(setup));
        var choice = Assert.Single(await Service(db).GetCorrectionCharactersAsync(created.SubmissionId, setup.AdminId));
        var submission = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == created.SubmissionId);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE event_participant_characters SET event_role = 'Informational', ehb_snapshot = NULL, ehb_source = NULL WHERE osrs_character_id = {choice.CharacterId}");
        var baseline = await B5EvidenceStateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).EditMetadataAsync(new(submission.Id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, choice.CharacterId, "Stale account", submission.Version)));
        Assert.Equal(baseline, await B5EvidenceStateAsync());
    }

    [Theory]
    [InlineData(5, false, 2, 0, 5, false)]
    [InlineData(3, true, 1, 2, 1, true)]
    [InlineData(2, false, 2, 0, 2, true)]
    [InlineData(2, true, 0, 2, 0, true)]
    public async Task B5ContributionMatchesApprovalAllocation(int target, bool earlierApproval, int add, int used, int remaining, bool completes)
    {
        var setup = await SeedAsync(target, true);
        await using var db = new ApplicationDbContext(options);
        var first = earlierApproval ? await Service(db).CreateAsync(Command(setup)) : null;
        var created = await Service(db, new FixedTimeProvider(now.AddSeconds(1))).CreateAsync(Command(setup));
        if (first is not null) await Service(db).ApproveCurrentAsync(first.SubmissionId, setup.AdminId);
        var read = await Service(db).GetReviewReadbackAsync(created.SubmissionId, setup.AdminId);
        Assert.True(read.Known);
        var state = read.State!;
        Assert.Null(state.Contribution.BlockingSubmission);
        Assert.Equal(new SubmissionContributionNumbers(add, 2, remaining, used, target, completes), state.Contribution.Values);
        if (add == 0)
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).ApproveCurrentAsync(created.SubmissionId, setup.AdminId));
        else
        {
            Assert.Equal(add, (await Service(db).ApproveCurrentAsync(created.SubmissionId, setup.AdminId)).ApprovedContribution);
            var approved = (await Service(db).GetReviewReadbackAsync(created.SubmissionId, setup.AdminId)).State!;
            Assert.Equal(SubmissionStatus.Approved, approved.Status);
            Assert.Equal(state.Contribution.Values, approved.Contribution.Values);
            Assert.Equal(ReviewActionType.Approve, approved.LatestAction!.Type);
            Assert.False(approved.LatestAction.ReasonPresent);
        }
    }

    [Fact]
    public async Task B5ContributionReturnsExistingBlockInsteadOfAnApprovalClaim()
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var first = await Service(db).CreateAsync(Command(setup));
        var later = await Service(db, new FixedTimeProvider(now.AddSeconds(1))).CreateAsync(Command(setup));
        var read = (await Service(db).GetReviewReadbackAsync(later.SubmissionId, setup.AdminId)).State!;
        Assert.Null(read.Contribution.Values);
        Assert.Equal(new SubmissionApprovalBlock(first.SubmissionId, now), read.Contribution.BlockingSubmission);
        var approval = await Service(db).ApproveCurrentAsync(later.SubmissionId, setup.AdminId);
        Assert.Equal(read.Contribution.BlockingSubmission, approval.BlockingSubmission);
        Assert.Equal(0, approval.ApprovedContribution);
    }

    [Fact]
    public async Task B5ReviewReadbackReportsOtherAdminAndAllCorrectedFieldsWithoutAttributionToRequest()
    {
        var setup = await SeedAsync(8, true, createAlternateWeightDrop: true);
        await using var db = new ApplicationDbContext(options);
        var actor = Account.CreateWebsite(Guid.NewGuid(), "other-reviewer", "OTHER-REVIEWER", now);
        actor.SetGlobalRole(GlobalRole.Admin); db.Accounts.Add(actor); await db.SaveChangesAsync();
        var service = Service(db);
        var created = await service.CreateAsync(Command(setup));
        var original = (await service.GetReviewReadbackAsync(created.SubmissionId, setup.AdminId)).State!;
        await service.EditMetadataAsync(new(created.SubmissionId, actor.Id, setup.TileId, setup.RequirementId, setup.AlternateDropId, original.CreditedCharacterId, "Other admin correction", original.Version));
        var corrected = (await service.GetReviewReadbackAsync(created.SubmissionId, setup.AdminId)).State!;
        Assert.Equal(setup.EventId, corrected.EventId); Assert.Equal(setup.TeamId, corrected.TeamId);
        Assert.Equal(setup.TileId, corrected.BoardTileId); Assert.Equal(setup.RequirementId, corrected.RequirementId);
        Assert.Equal(setup.AlternateDropId, corrected.DropSnapshotId); Assert.Equal(2, corrected.Weight);
        Assert.Equal(original.CreditedCharacterId, corrected.CreditedCharacterId); Assert.Equal(setup.ParticipantId, corrected.CreditedParticipantId);
        Assert.True(corrected.Version > original.Version);
        Assert.Equal(ReviewActionType.EditMetadata, corrected.LatestAction!.Type);
        Assert.Equal(actor.Id, corrected.LatestAction.ActorId); Assert.Equal("other-reviewer", corrected.LatestAction.ActorName);
        Assert.True(corrected.LatestAction.ReasonPresent); Assert.Equal(now, corrected.LatestAction.At);
        await service.ApproveAsync(created.SubmissionId, actor.Id, expectedVersion: corrected.Version);
        var approved = (await service.GetReviewReadbackAsync(created.SubmissionId, setup.AdminId)).State!;
        Assert.Equal(SubmissionStatus.Approved, approved.Status); Assert.Equal(actor.Id, approved.LatestAction!.ActorId);
        await service.ReverseAsync(created.SubmissionId, actor.Id, "Other admin reversal", expectedVersion: approved.Version);
        var reversed = (await service.GetReviewReadbackAsync(created.SubmissionId, setup.AdminId)).State!;
        Assert.Equal(SubmissionStatus.Reversed, reversed.Status); Assert.Equal(ReviewActionType.ReverseApproval, reversed.LatestAction!.Type);
        Assert.True(reversed.LatestAction.ReasonPresent); Assert.Equal(0, reversed.Contribution.Values!.Add);
        var rejectedId = (await service.CreateAsync(Command(setup))).SubmissionId;
        await service.RejectCurrentAsync(rejectedId, actor.Id, "Other admin rejection");
        var rejected = (await service.GetReviewReadbackAsync(rejectedId, setup.AdminId)).State!;
        Assert.Equal(SubmissionStatus.Rejected, rejected.Status); Assert.Equal(ReviewActionType.Reject, rejected.LatestAction!.Type);
        Assert.Equal(actor.Id, rejected.LatestAction.ActorId); Assert.True(rejected.LatestAction.ReasonPresent);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetReviewReadbackAsync(created.SubmissionId, setup.CaptainId));
        var baseline = await B5EvidenceStateAsync();
        await service.GetReviewReadbackAsync(created.SubmissionId, setup.AdminId);
        Assert.Equal(baseline, await B5EvidenceStateAsync());
    }

    [Fact]
    public async Task B5ReviewFailedReadbackIsUnknownAndDoesNotReplay()
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var created = await Service(db).CreateAsync(Command(setup));
        var baseline = await B5EvidenceStateAsync();
        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).AddInterceptors(new B5ReadFailure()).Options;
        await using var failing = new ApplicationDbContext(failingOptions);
        var result = await Service(failing).GetReviewReadbackAsync(created.SubmissionId, setup.AdminId);
        Assert.False(result.Known); Assert.Null(result.State);
        Assert.Equal(baseline, await B5EvidenceStateAsync());
    }

    private sealed class B5ReadFailure : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM submissions", StringComparison.Ordinal)) throw new TimeoutException("Controlled read failure");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private async Task<string> B5EvidenceStateAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new { Submissions = await db.Submissions.AsNoTracking().ToListAsync(), Assets = await db.EvidenceAssets.AsNoTracking().ToListAsync(), Reviews = await db.ReviewActions.AsNoTracking().ToListAsync(), Audits = await db.AuditEntries.AsNoTracking().ToListAsync(), Events = await db.Events.AsNoTracking().ToListAsync() });
    }
}
