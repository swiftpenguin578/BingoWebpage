using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Pages.Admin.Review;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class SubmissionWorkflowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_submission_tests").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
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

    [Theory]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.Participant, EvidenceActorKind.Participant)]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.Captain, EvidenceActorKind.Captain)]
    [InlineData(GlobalRole.Admin, TeamMembershipRole.CoCaptain, EvidenceActorKind.Captain)]
    [InlineData(GlobalRole.SuperAdmin, TeamMembershipRole.Participant, EvidenceActorKind.Participant)]
    public async Task GlobalRoleComposesWithTheGenuineEventMembershipForSubmissionAuthority(GlobalRole globalRole, TeamMembershipRole membershipRole, EvidenceActorKind expectedKind)
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
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

        var created = await Service(db).CreateAsync(Command(setup) with { ActorAccountId = account.Id });
        Assert.NotEqual(Guid.Empty, created.SubmissionId);

        var eventItem = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        eventItem.EndEvent(now.AddMinutes(-31));
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).CreateAsync(Command(setup) with { ActorAccountId = account.Id }));
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
        await service.RejectAsync(rejected.SubmissionId, setup.AdminId, "Please include the full game message.");
        Assert.Empty(notifier.EventIds);

        var approved = await service.CreateAsync(Command(setup));
        await service.ApproveAsync(approved.SubmissionId, setup.AdminId);
        Assert.Single(notifier.EventIds);
        await service.ReverseAsync(approved.SubmissionId, setup.AdminId, "Correction required.");
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
    public async Task ReleasedPlayingAssignmentCannotBeUsedForAdminCorrectionAfterReassignment()
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

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EditMetadataAsync(new(submission.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, oldCharacterId, "historical character", null)));
        var unchanged = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission.SubmissionId);
        Assert.Equal(oldCharacterId, unchanged.CreditedOsrsCharacterId);
        await service.EditMetadataAsync(new(submission.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, replacementCharacter.Id, "current character", unchanged.Version));
        Assert.Equal(replacementCharacter.Id, await db.Submissions.Where(x => x.Id == submission.SubmissionId).Select(x => x.CreditedOsrsCharacterId).SingleAsync());
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
        await Service(db).RejectAsync(result.SubmissionId, setup.AdminId, "Archived test rejection");
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
        var clock = new MutableTimeProvider(now.AddHours(5));
        await using var db = new ApplicationDbContext(options);
        var captain = await db.Accounts.SingleAsync(account => account.Id == setup.CaptainId);
        captain.SetPassword(new PasswordHasher<Account>().HashPassword(captain, "emergency-password"), false, now, incrementVersion: false);
        var ev = await db.Events.SingleAsync(item => item.Id == setup.EventId);
        await db.SaveChangesAsync();

        var lifecycle = new EmergencyCredentialLifecycleService(db, clock);
        await lifecycle.ApplyAsync(CancellationToken.None);
        var administration = new AccountAdministrationService(db, new PasswordHasher<Account>(), clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.SetEmergencyEnabledAsync(setup.AdminId, setup.CaptainId, true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, clock).CreateAsync(Command(setup)));

        ev.EndEvent();
        ev.ReopenSubmissions(clock.GetUtcNow().AddMinutes(10), clock.GetUtcNow());
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, clock).CreateAsync(Command(setup)));

        await administration.SetEmergencyEnabledAsync(setup.AdminId, setup.CaptainId, true, CancellationToken.None);
        var service = Service(db, clock);
        var created = await service.CreateAsync(Command(setup));
        await service.CorrectAsync(new CorrectSubmissionCommand(created.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId, setup.ParticipantId, 1, "corrected"));
        await service.WithdrawAsync(created.SubmissionId, setup.CaptainId);

        clock.Set(clock.GetUtcNow().AddMinutes(10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Command(setup)));
        await lifecycle.ApplyAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Command(setup)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectAsync(new CorrectSubmissionCommand(created.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId, setup.ParticipantId, 1, "closed")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.WithdrawAsync(created.SubmissionId, setup.CaptainId));
        Assert.Equal(2, await db.AuditEntries.CountAsync(entry => entry.Action == "account.emergency_cutoff_disabled" && entry.TargetId == setup.CaptainId.ToString()));
        Assert.Contains(await db.AuditEntries.ToListAsync(), entry => entry.Action == "account.emergency_enabled");
    }

    [Fact]
    public async Task ApprovalCapsContributionAndReversalRebalancesLaterApprovedEvidence()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var first = await service.CreateAsync(Command(setup) with { ClaimedWeight = 2 });
        var second = await service.CreateAsync(Command(setup) with { ClaimedWeight = 2 });

        Assert.Equal(2, await service.ApproveAsync(first.SubmissionId, setup.AdminId));
        Assert.Equal(1, await service.ApproveAsync(second.SubmissionId, setup.AdminId));
        Assert.Equal(3, await db.SubmissionContributions.Where(x => x.ReversedAt == null).SumAsync(x => x.Amount));

        await service.ReverseAsync(first.SubmissionId, setup.AdminId, "Approved the wrong screenshot");

        Assert.Equal(2, await db.SubmissionContributions.Where(x => x.ReversedAt == null).SumAsync(x => x.Amount));
        Assert.Equal(SubmissionStatus.Reversed, await db.Submissions.Where(x => x.Id == first.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(2, await db.Submissions.Where(x => x.Id == second.SubmissionId).Select(x => x.ApprovedContribution).SingleAsync());
        Assert.Contains(await db.ReviewActions.Where(x => x.SubmissionId == second.SubmissionId).ToListAsync(), x => x.Action == ReviewActionType.RebalanceContribution);
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
        await db.SaveChangesAsync();
        var service = Service(db);
        var first = await service.CreateAsync(Command(setup) with { ClaimedWeight = 2 });
        var second = await service.CreateAsync(Command(setup) with { ClaimedWeight = 2, DropSnapshotId = alternateDropId });

        Assert.Equal(1, await service.ApproveAsync(first.SubmissionId, setup.AdminId));
        Assert.Equal(1, await service.ApproveAsync(second.SubmissionId, setup.AdminId));

        await service.ReverseAsync(first.SubmissionId, setup.AdminId, "Approved the wrong screenshot");

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

        await service.ApproveAsync(result.SubmissionId, setup.AdminId);

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
        await service.ApproveAsync(submission.SubmissionId, setup.AdminId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(submission.SubmissionId, setup.AdminId));

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

            var failure = await Record.ExceptionAsync(() => Service(db).ApproveAsync(submission.SubmissionId, setup.AdminId));
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
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var pending = await service.CreateAsync(Command(setup));
        var approved = await service.CreateAsync(Command(setup));
        await service.ApproveAsync(approved.SubmissionId, setup.AdminId);
        var characterId = await db.EventParticipantCharacters
            .Where(x => x.EventParticipantId == setup.ParticipantId && x.ReleasedAt == null)
            .Select(x => x.OsrsCharacterId)
            .SingleAsync();

        var eventItem = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        eventItem.EndEvent(now.AddMinutes(-1));
        eventItem.FinalizeResults(now);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EditMetadataAsync(new(
            pending.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId,
            characterId, "Closed correction")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(pending.SubmissionId, setup.AdminId, "Closed rejection"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(pending.SubmissionId, setup.AdminId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseAsync(approved.SubmissionId, setup.AdminId, "Closed reversal"));

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
        await service.RejectAsync(submission.SubmissionId, setup.AdminId, "The screenshot does not establish the claimed drop.");

        var notifications = await db.PersonalNotifications.AsNoTracking().Where(x => x.Title == "evidence.rejected").ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Equal(new[] { participantAccount.Id, coCaptainAccount.Id }.OrderBy(x => x), notifications.Select(x => x.RecipientAccountId).OrderBy(x => x));
        Assert.Equal($"/Submissions/{submission.SubmissionId}", notifications.Single(x => x.RecipientAccountId == participantAccount.Id).Route);
        Assert.Equal($"/Submissions/{submission.SubmissionId}", notifications.Single(x => x.RecipientAccountId == coCaptainAccount.Id).Route);
        Assert.All(notifications, notification =>
        {
            Assert.Contains("Event ", notification.Detail, StringComparison.Ordinal);
            Assert.Contains("Manual tile", notification.Detail, StringComparison.Ordinal);
            Assert.Contains("does not establish the claimed drop", notification.Detail, StringComparison.Ordinal);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(submission.SubmissionId, setup.AdminId, "A second decision is not allowed."));
        Assert.Equal(2, await db.PersonalNotifications.CountAsync(x => x.Title == "evidence.rejected"));
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
        await service.RejectAsync(rejected.SubmissionId, setup.AdminId, rejectionReason);
        var rejectionAudit = Assert.Single(await db.AuditEntries.Where(x => x.TargetId == rejected.SubmissionId.ToString("D") && x.Action == "submission.rejected").ToListAsync());
        Assert.Equal(rejectionReason, rejectionAudit.Details);
        AssertSnapshot(rejectionAudit.BeforeState, captainNotePresent: false, reviewerNotePresent: false, status: SubmissionStatus.Pending);
        AssertSnapshot(rejectionAudit.AfterState, captainNotePresent: false, reviewerNotePresent: true, status: SubmissionStatus.Rejected);
        Assert.Equal(rejectionReason, await db.Submissions.Where(x => x.Id == rejected.SubmissionId).Select(x => x.CurrentReviewerNote).SingleAsync());

        var notification = Assert.Single(await db.PersonalNotifications.AsNoTracking().Where(x => x.Title == "evidence.rejected").ToListAsync());
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
        await service.ApproveAsync(approved.SubmissionId, setup.AdminId);
        await service.ReverseAsync(approved.SubmissionId, setup.AdminId, reversalReason);
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
        await service.EditMetadataAsync(new(
            adminSubmission.SubmissionId, setup.AdminId, setup.TileId, setup.RequirementId, alternateDropId,
            characterId, adminReason));
        Assert.Equal(1, await db.Submissions.Where(x => x.Id == adminSubmission.SubmissionId).Select(x => x.ClaimedWeight).SingleAsync());
        var adminAudit = Assert.Single(await db.AuditEntries.Where(x => x.TargetId == adminSubmission.SubmissionId.ToString("D") && x.Action == "submission.corrected").ToListAsync());
        Assert.Equal("Retargeted by Admin", adminAudit.Details);
    }

    [Fact]
    public async Task ReversedSubmissionCanHaveOneReopenedLinkedChildAndKeepsInactivePredecessorContribution()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db, clock);
        var predecessor = await service.CreateAsync(Command(setup));
        await service.ApproveAsync(predecessor.SubmissionId, setup.AdminId);
        await service.ReverseAsync(predecessor.SubmissionId, setup.AdminId, "Reverse for corrected evidence.");

        Assert.Equal(SubmissionStatus.Reversed, await db.Submissions.Where(x => x.Id == predecessor.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.True(await db.SubmissionContributions.AnyAsync(x => x.SubmissionId == predecessor.SubmissionId && x.ReversedAt != null));
        Assert.Contains(await db.AuditEntries.Where(x => x.TargetId == predecessor.SubmissionId.ToString("D")).ToListAsync(), x => x.Action == "submission.reversed");

        var eventItem = await db.Events.SingleAsync(x => x.Id == setup.EventId);
        eventItem.EndEvent(now.AddMinutes(-1));
        await db.SaveChangesAsync();
        clock.Set(eventItem.SubmissionCutoffAt!.Value.AddMinutes(1));
        await using var closedEvidence = new MemoryStream([8, 8, 8]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResubmitAsync(new ResubmitSubmissionCommand(
            predecessor.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            "closed window", "closed.png", closedEvidence)));

        eventItem.ReopenSubmissions(clock.GetUtcNow().AddHours(1), clock.GetUtcNow());
        await db.SaveChangesAsync();
        await using var evidence = new MemoryStream([9, 9, 9]);
        var child = await service.ResubmitAsync(new ResubmitSubmissionCommand(
            predecessor.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            "corrected evidence", "corrected.png", evidence));
        Assert.Equal(SubmissionStatus.Pending, child.Status);
        Assert.Equal(predecessor.SubmissionId, await db.Submissions.Where(x => x.Id == child.SubmissionId).Select(x => x.ResubmissionOfSubmissionId).SingleAsync());
        Assert.Contains(await db.AuditEntries.Where(x => x.TargetId == child.SubmissionId.ToString("D")).ToListAsync(), x => x.Action == "submission.resubmitted");

        await using var replayEvidence = new MemoryStream([7, 7, 7]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResubmitAsync(new ResubmitSubmissionCommand(
            predecessor.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            "duplicate", "duplicate.png", replayEvidence)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(predecessor.SubmissionId, setup.AdminId));
        await service.ApproveAsync(child.SubmissionId, setup.AdminId);
        Assert.True(await db.SubmissionContributions.AnyAsync(x => x.SubmissionId == child.SubmissionId && x.ReversedAt == null));
        Assert.True(await db.SubmissionContributions.AnyAsync(x => x.SubmissionId == predecessor.SubmissionId && x.ReversedAt != null));
    }

    [Fact]
    public async Task PendingCopyIsAllowedButApprovedNonDuplicateDropCannotBeSubmittedAgain()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: false, manualObjective: false, duplicatesAllowed: false);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);

        var first = await service.CreateAsync(Command(setup));
        var pendingCopy = await service.CreateAsync(Command(setup));
        Assert.Equal(SubmissionStatus.Pending, pendingCopy.Status);

        await service.ApproveAsync(first.SubmissionId, setup.AdminId);

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
        await submissions.ApproveAsync(approved.SubmissionId, setup.AdminId);
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

        await submissions.ReverseAsync(approved.SubmissionId, setup.AdminId, "Wrong evidence");
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
            await submissions.ApproveAsync(submission.SubmissionId, setup.AdminId);
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
            await submissions.ApproveAsync(submission.SubmissionId, setup.AdminId);
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
        await submissions.ApproveAsync(first.SubmissionId, setup.AdminId);
        var publicBoards = new PublicBoardService(db, new FixedTimeProvider(now));

        var partial = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");

        var partialTeam = Assert.Single(partial!.Teams);
        Assert.Equal(6, partialTeam.Progress.EhbTiebreak);
        Assert.Equal(6, Assert.Single(partial.PlayerLeaderboard).EstimatedEhb);

        var second = await submissions.CreateAsync(Command(setup));
        await submissions.ApproveAsync(second.SubmissionId, setup.AdminId);
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
        var character = await (from assignment in db.EventParticipantCharacters
                               join osrsCharacter in db.OsrsCharacters on assignment.OsrsCharacterId equals osrsCharacter.Id
                               where assignment.EventId == setup.EventId && assignment.EventParticipantId == setup.ParticipantId
                               select new { assignment.OsrsCharacterId, osrsCharacter.DisplayName }).SingleAsync();
        var firstAt = now.AddMinutes(-15);
        var aliasAt = now.AddMinutes(-10);
        var distinctAt = now.AddMinutes(-5);
        var first = AddApproved(setup.DropId!.Value, firstAt);
        var alias = AddApproved(aliasDropId, aliasAt);
        AddApproved(distinctDropId, distinctAt);
        await db.SaveChangesAsync();

        var board = await new PublicBoardService(db, new FixedTimeProvider(now)).GetEventBoardAsync($"event-{setup.EventId:N}", 3);

        var publicTeam = Assert.Single(board!.Teams);
        Assert.True(publicTeam.Progress.BoardComplete);
        Assert.Equal(distinctAt, publicTeam.Progress.BoardCompletedAt);
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
        await submissions.ApproveAsync(result.SubmissionId, setup.AdminId);
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
    public async Task RejectedSubmissionCanCreateOneLinkedResubmissionWithImmutableCreditSnapshots()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var predecessor = await service.CreateAsync(Command(setup));
        await service.RejectAsync(predecessor.SubmissionId, setup.AdminId, "Show the full game message.");
        var original = await db.Submissions.SingleAsync(x => x.Id == predecessor.SubmissionId);

        await using var evidence = new MemoryStream([4, 5, 6]);
        var child = await service.ResubmitAsync(new ResubmitSubmissionCommand(
            predecessor.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            "linked attempt", "resubmission.png", evidence));

        var savedChild = await db.Submissions.SingleAsync(x => x.Id == child.SubmissionId);
        Assert.Equal(SubmissionStatus.Rejected, await db.Submissions.Where(x => x.Id == predecessor.SubmissionId).Select(x => x.Status).SingleAsync());
        Assert.Equal(predecessor.SubmissionId, savedChild.ResubmissionOfSubmissionId);
        Assert.Equal(original.CreditedParticipantId, savedChild.CreditedParticipantId);
        Assert.Equal(original.CreditedOsrsCharacterId, savedChild.CreditedOsrsCharacterId);
        Assert.Equal(original.CreditedCharacterName, savedChild.CreditedCharacterName);
        Assert.Single(await db.EvidenceAssets.Where(x => x.SubmissionId == child.SubmissionId && x.Active).ToListAsync());
        Assert.Contains(await db.ReviewActions.Where(x => x.SubmissionId == child.SubmissionId).ToListAsync(), x => x.Action == ReviewActionType.Resubmit);
        Assert.Contains(await db.AuditEntries.Where(x => x.TargetId == child.SubmissionId.ToString("D")).ToListAsync(), x => x.Action == "submission.resubmitted");

        await using var replayEvidence = new MemoryStream([7, 8, 9]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResubmitAsync(new ResubmitSubmissionCommand(
            predecessor.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            "replay", "replay.png", replayEvidence)));
    }

    [Fact]
    public async Task ParticipantAuthoritySeesOnlyItsOwnCandidateWhileEmergencyLeadershipSeesCurrentTeamCandidates()
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
        var participantCandidates = await authority.GetCurrentTeamCandidatesAsync(participantScope, CancellationToken.None);
        Assert.Single(participantCandidates);
        Assert.Equal(setup.ParticipantId, participantCandidates[0].ParticipantId);

        var emergencyScope = await authority.ResolveActorAsync(setup.CaptainId, setup.EventId, setup.TeamId, now, CancellationToken.None);
        var leadershipCandidates = await authority.GetCurrentTeamCandidatesAsync(emergencyScope, CancellationToken.None);
        Assert.Equal(2, leadershipCandidates.Count);
    }

    private SubmissionService Service(ApplicationDbContext db, TimeProvider? clock = null, IEvidenceStorage? storage = null, IProgressNotifier? notifier = null) => new(db, storage ?? new FakeEvidenceStorage(), clock ?? new FixedTimeProvider(now), notifier);

    private static CreateSubmissionCommand Command(Setup setup) => new(
        setup.CaptainId, setup.EventId, setup.TeamId, setup.TileId, setup.RequirementId, setup.DropId,
        setup.ParticipantId, 1, "captain note", "proof.png", new MemoryStream([1, 2, 3]));

    private async Task<Setup> SeedAsync(int target, bool allowHigherWeights, string? evidenceCode = null, bool manualObjective = false, bool duplicatesAllowed = true, int? dropMaximum = null, decimal tileEhb = 1, decimal dropEhb = 1)
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
        ev.StartEvent(now.AddHours(-1));
        if (evidenceCode is not null) ev.SetEvidenceCodeEnabled(true);
        var team = new Team(teamId, eventId, "Team One", $"team-{teamId:N}", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(participantId, eventId, SignupStatus.Confirmed, 1, now.AddDays(-5), SignupSource.Website);
        var form = new SignupForm(Guid.NewGuid(), eventId, now.AddDays(-5));
        var primaryQuestion = new SignupQuestion(Guid.NewGuid(), form.Id, eventId, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = Account.CreateEmergency(captainId, "captain", "CAPTAIN", now.AddDays(-10));
        captain.Enable();
        var captainAccess = new AccountEventAccess(Guid.NewGuid(), captainId, eventId, teamId, participantId, now.AddDays(-1), now.AddHours(5), now.AddHours(30));
        captainAccess.Enable();
        var admin = Account.CreateWebsite(adminId, "admin", "ADMIN", now.AddDays(-10));
        admin.SetGlobalRole(GlobalRole.Admin);
        var board = new Board(boardId, eventId, "Board", 1, 1);
        var tile = new BoardTile(tileId, boardId, Guid.NewGuid(), 0, 0, "Manual tile", "Complete it", "Show the message", tileEhb);
        var requirement = new BoardRequirementSnapshot(requirementId, tileId, 0, target, duplicatesAllowed, allowHigherWeights, "Complete runs", manualObjective, allowHigherWeights ? 2 : 1);
        var drop = dropId is Guid eligibleDropId
            ? new BoardRequirementDropSnapshot(eligibleDropId, requirementId, Guid.NewGuid(), Guid.NewGuid(), "Test boss", "Test drop", "1/10", 0.1m, dropMaximum ?? (duplicatesAllowed ? null : 1), dropEhb, allowHigherWeights ? 2 : 1)
            : null;
        var character = new OsrsCharacter(Guid.NewGuid(), "Player One", "PLAYER ONE", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), eventId, participantId, character.Id, 0, now, adminId, primaryQuestion.Id, EventCharacterRole.Playing, 500, EhbSource.Manual, null);
        db.AddRange(ev, form, primaryQuestion, team, participant, character, assignment, captain, admin, board, captainAccess,
            new TeamMembership(Guid.NewGuid(), teamId, participantId, TeamMembershipRole.Participant, now.AddDays(-4), null, null),
            tile, requirement);
        if (drop is not null) db.BoardRequirementDropSnapshots.Add(drop);
        if (!string.IsNullOrEmpty(evidenceCode)) db.EvidenceCodes.Add(new EvidenceCode(Guid.NewGuid(), eventId, evidenceCode, now.AddMinutes(-10), adminId, now.AddMinutes(-10), null));
        await BoardApprovalFixture.PublishAsync(db, board, now.AddDays(-1), [tile], [requirement], drop is null ? [] : [drop]);
        return new Setup(eventId, teamId, participantId, captainId, adminId, tileId, requirementId, dropId);
    }

    private sealed record Setup(Guid EventId, Guid TeamId, Guid ParticipantId, Guid CaptainId, Guid AdminId, Guid TileId, Guid RequirementId, Guid? DropId);

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
        public CancellationTokenSource? CancelAfterStore { get; set; }
        public List<string> DeletedTokens { get; } = [];
        public List<CancellationToken> DeleteTokens { get; } = [];
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default)
        {
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
