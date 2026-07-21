using Bingo.Application.Evidence;
using Bingo.Application.Boards;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class SubmissionWorkflowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_submission_tests").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly DateTimeOffset now = new(2026, 7, 13, 18, 0, 0, TimeSpan.Zero);

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

    [Fact]
    public async Task CaptainPrivacyRequestHidesApprovedEvidenceUntilAdminRestoresIt()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var result = await service.CreateAsync(Command(setup) with { RequestPublicPrivacy = true });

        await service.ApproveAsync(result.SubmissionId, setup.AdminId);

        var approved = await db.Submissions.SingleAsync(x => x.Id == result.SubmissionId);
        Assert.True(approved.PublicPrivacyRequested);
        Assert.True(approved.PublicEvidenceHidden);
        Assert.True(approved.PublicPlayerHidden);

        await service.SetVisibilityAsync(result.SubmissionId, setup.AdminId, false);
        Assert.False(approved.PublicEvidenceHidden);
        Assert.False(approved.PublicPlayerHidden);

        await service.SetVisibilityAsync(result.SubmissionId, setup.AdminId, true);
        Assert.True(approved.PublicEvidenceHidden);
        Assert.True(approved.PublicPlayerHidden);
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
    }

    [Fact]
    public async Task ReplacementEvidencePreservesTheOriginalAsInactiveHistory()
    {
        var setup = await SeedAsync(target: 3, allowHigherWeights: true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var submission = await service.CreateAsync(Command(setup));
        await using var replacement = new MemoryStream([4, 5, 6]);

        await service.CorrectAsync(new CorrectSubmissionCommand(
            submission.SubmissionId, setup.CaptainId, setup.TileId, setup.RequirementId, setup.DropId,
            setup.ParticipantId, 1, "clearer screenshot", "replacement.png", replacement));

        var assets = await db.EvidenceAssets.Where(x => x.SubmissionId == submission.SubmissionId).OrderBy(x => x.UploadedAt).ToListAsync();
        Assert.Equal(2, assets.Count);
        Assert.Contains(assets, x => x.Role == EvidenceAssetRole.OriginalEvidence && !x.Active);
        Assert.Contains(assets, x => x.Role == EvidenceAssetRole.ReplacementEvidence && x.Active);
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
    public async Task FinalizedCaptainProvisioningGeneratesOneLinkedAccountAndPassword()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var membership = await db.TeamMemberships.SingleAsync(x => x.TeamId == setup.TeamId && x.EventParticipantId == setup.ParticipantId);
        membership.ChangeRole(TeamMembershipRole.Captain);
        await db.SaveChangesAsync();
        var provisioner = new CaptainAccountProvisioner(db, new PasswordHasher<Account>(), new AuditWriter(db, new FixedTimeProvider(now)), new FixedTimeProvider(now));

        var first = await provisioner.ProvisionEventAsync(setup.EventId, setup.AdminId, "admin", CancellationToken.None);
        var second = await provisioner.ProvisionEventAsync(setup.EventId, setup.AdminId, "admin", CancellationToken.None);

        var credential = Assert.Single(first);
        Assert.StartsWith("PlayerOne", credential.Username, StringComparison.Ordinal);
        Assert.Equal(16, credential.Password.Length);
        Assert.Empty(second);
        var account = await db.Accounts.SingleAsync(x => x.CaptainParticipantId == setup.ParticipantId);
        Assert.Equal(setup.TeamId, account.TeamId);
        Assert.True(account.MustChangePassword);
        Assert.Equal(1, await db.Accounts.CountAsync(x => x.CaptainParticipantId == setup.ParticipantId));
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
        var publicBoards = new PublicBoardService(db);

        var initial = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");

        var initialTeam = Assert.Single(initial!.Teams);
        Assert.Equal(1, Assert.Single(initialTeam.Tiles).Approved);
        Assert.False(initialTeam.Progress.BoardComplete);
        Assert.Single(initial.PlayerLeaderboard);

        await submissions.ReverseAsync(approved.SubmissionId, setup.AdminId, "Wrong evidence");
        var reversed = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");
        Assert.Equal(0, Assert.Single(Assert.Single(reversed!.Teams).Tiles).Approved);
        Assert.Empty(reversed.PlayerLeaderboard);
    }

    [Fact]
    public async Task PublicTileKeepsPrivateApprovalButHidesPlayerAndAsset()
    {
        var setup = await SeedAsync(target: 1, allowHigherWeights: false);
        await using var db = new ApplicationDbContext(options);
        var team = await db.Teams.SingleAsync(x => x.Id == setup.TeamId);
        team.Finalize(now.AddMinutes(-30));
        await db.SaveChangesAsync();
        var submissions = Service(db);
        var result = await submissions.CreateAsync(Command(setup) with { RequestPublicPrivacy = true });
        await submissions.ApproveAsync(result.SubmissionId, setup.AdminId);
        var publicBoards = new PublicBoardService(db);

        var board = await publicBoards.GetEventBoardAsync($"event-{setup.EventId:N}");
        var details = await publicBoards.GetTileAsync($"event-{setup.EventId:N}", $"team-{setup.TeamId:N}", setup.TileId);

        Assert.True(Assert.Single(board!.Teams).Progress.BoardComplete);
        Assert.Empty(board.PlayerLeaderboard);
        var evidence = Assert.Single(details!.Evidence);
        Assert.True(evidence.Hidden);
        Assert.Null(evidence.PlayerName);
        Assert.Null(evidence.EvidenceAssetId);
    }

    private SubmissionService Service(ApplicationDbContext db) => new(db, new FakeEvidenceStorage(), new FixedTimeProvider(now));

    private static CreateSubmissionCommand Command(Setup setup) => new(
        setup.CaptainId, setup.EventId, setup.TeamId, setup.TileId, setup.RequirementId, setup.DropId,
        setup.ParticipantId, 1, "captain note", "proof.png", new MemoryStream([1, 2, 3]));

    private async Task<Setup> SeedAsync(int target, bool allowHigherWeights, string? evidenceCode = null, bool manualObjective = false, bool duplicatesAllowed = true)
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
        ev.StartEvent(now.AddHours(-1));
        if (evidenceCode is not null) ev.SetEvidenceCodeEnabled(true);
        var team = new Team(teamId, eventId, "Team One", $"team-{teamId:N}", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(participantId, eventId, "Player One", "PLAYER ONE", 500, SignupStatus.Confirmed, 1, now.AddDays(-5), SignupSource.Website, null);
        var captain = new Account(captainId, "captain", "CAPTAIN", AccountRole.Captain, now.AddDays(-10));
        captain.ScopeCaptain(eventId, teamId, now.AddDays(-1), now.AddHours(5), now.AddHours(30));
        var admin = new Account(adminId, "admin", "ADMIN", AccountRole.Admin, now.AddDays(-10));
        var board = new Board(boardId, eventId, "Board", 1, 1);
        board.Publish(now.AddDays(-1));
        db.AddRange(ev, team, participant, captain, admin, board,
            new TeamMembership(Guid.NewGuid(), teamId, participantId, TeamMembershipRole.Participant, now.AddDays(-4), null, null),
            new BoardTile(tileId, boardId, Guid.NewGuid(), 0, 0, "Manual tile", "Complete it", "Show the message", 1),
            new BoardRequirementSnapshot(requirementId, tileId, 0, target, duplicatesAllowed, allowHigherWeights, "Complete runs", manualObjective, allowHigherWeights ? 2 : 1));
        if (dropId is Guid eligibleDropId) db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(eligibleDropId, requirementId, Guid.NewGuid(), "Test boss", "Test drop", "1/10", 0.1m, duplicatesAllowed ? null : 1, 1, allowHigherWeights ? 2 : 1));
        if (!string.IsNullOrEmpty(evidenceCode)) db.EvidenceCodes.Add(new EvidenceCode(Guid.NewGuid(), eventId, evidenceCode, now.AddMinutes(-10), adminId, now.AddMinutes(-10), null));
        await db.SaveChangesAsync();
        return new Setup(eventId, teamId, participantId, captainId, adminId, tileId, requirementId, dropId);
    }

    private sealed record Setup(Guid EventId, Guid TeamId, Guid ParticipantId, Guid CaptainId, Guid AdminId, Guid TileId, Guid RequirementId, Guid? DropId);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FakeEvidenceStorage : IEvidenceStorage
    {
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoredEvidence($"{eventId}/{submissionId}.png", originalFilename, "image/png", 3, 1, 1, new string('a', 64)));
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
