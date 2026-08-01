using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice8Pass81PersistenceIntegrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260731180603_NormalizeCompletedTileFocusMarkers";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice8_pass81_persistence")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task RetainedBackfillUsesLatestSwapOrUniquePlayingAssignmentAndAddsCardinalityRules()
    {
        var seed = await SeedBaseAsync(includeAmbiguousParticipant: false);
        var swappedSubmissionId = Guid.NewGuid();
        var noSwapSubmissionId = Guid.NewGuid();
        await InsertLegacySubmissionAsync(swappedSubmissionId, seed.EventId, seed.SwappedParticipantId, seed.AccountId, DateTimeOffset.UtcNow);
        await InsertLegacySubmissionAsync(noSwapSubmissionId, seed.EventId, seed.UniqueParticipantId, seed.AccountId, DateTimeOffset.UtcNow);

        await using (var migrated = new ApplicationDbContext(options))
        {
            await migrated.GetService<IMigrator>().MigrateAsync();
            var rows = await migrated.Submissions.AsNoTracking().Where(x => x.Id == swappedSubmissionId || x.Id == noSwapSubmissionId).ToDictionaryAsync(x => x.Id);
            Assert.Equal(seed.SwappedCharacterId, rows[swappedSubmissionId].CreditedOsrsCharacterId);
            Assert.Equal("Swapped character", rows[swappedSubmissionId].CreditedCharacterName);
            Assert.Equal(seed.UniqueCharacterId, rows[noSwapSubmissionId].CreditedOsrsCharacterId);
            Assert.Equal("Unique character", rows[noSwapSubmissionId].CreditedCharacterName);
            Assert.All(rows.Values, row => Assert.Equal(1, row.Version));
        }

        await using (var cardinality = new ApplicationDbContext(options))
        {
            var duplicateAssets = new[]
            {
                NewAsset(Guid.NewGuid(), swappedSubmissionId, seed.AccountId, true),
                NewAsset(Guid.NewGuid(), swappedSubmissionId, seed.AccountId, true)
            };
            cardinality.EvidenceAssets.AddRange(duplicateAssets);
            await Assert.ThrowsAsync<DbUpdateException>(() => cardinality.SaveChangesAsync());
        }

        await using (var predecessor = new ApplicationDbContext(options))
        {
            var first = new Submission(Guid.NewGuid(), seed.EventId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                seed.UniqueParticipantId, seed.UniqueCharacterId, "Unique character", seed.AccountId, 1, DateTimeOffset.UtcNow, null, null,
                resubmissionOfSubmissionId: swappedSubmissionId);
            var second = new Submission(Guid.NewGuid(), seed.EventId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
                seed.UniqueParticipantId, seed.UniqueCharacterId, "Unique character", seed.AccountId, 1, DateTimeOffset.UtcNow, null, null,
                resubmissionOfSubmissionId: swappedSubmissionId);
            predecessor.Submissions.AddRange(first, second);
            await Assert.ThrowsAsync<DbUpdateException>(() => predecessor.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task RetainedChangesRequestedConvertsToRejectedBeforeDeprecatedColumnsAreDropped()
    {
        var seed = await SeedBaseAsync(includeAmbiguousParticipant: false);
        var submissionId = Guid.NewGuid();
        await InsertLegacySubmissionAsync(submissionId, seed.EventId, seed.UniqueParticipantId, seed.AccountId, DateTimeOffset.UtcNow, "ChangesRequested");

        await using var migrated = new ApplicationDbContext(options);
        await migrated.GetService<IMigrator>().MigrateAsync();

        var submission = await migrated.Submissions.AsNoTracking().SingleAsync(x => x.Id == submissionId);
        Assert.Equal(SubmissionStatus.Rejected, submission.Status);
        Assert.Equal("Rejected", await migrated.Database.SqlQuery<string>($"SELECT status AS \"Value\" FROM submissions WHERE id = {submissionId}").SingleAsync());
        Assert.Equal(0, await migrated.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM information_schema.columns WHERE table_name = 'submissions' AND column_name IN ('public_evidence_hidden', 'public_player_hidden', 'public_privacy_requested', 'duplicate_of_submission_id')").SingleAsync());
    }

    [Fact]
    public async Task RetainedPreflightFailsClosedWithStableAffectedIds()
    {
        var seed = await SeedBaseAsync(includeAmbiguousParticipant: true);
        var ambiguousSubmissionId = Guid.NewGuid();
        var duplicateSubmissionId = Guid.NewGuid();
        var hiddenApprovedSubmissionId = Guid.NewGuid();
        var invalidParticipantSubmissionId = Guid.NewGuid();
        var orphanAssetId = Guid.NewGuid();
        var orphanContributionId = Guid.NewGuid();
        var orphanReviewId = Guid.NewGuid();
        await InsertLegacySubmissionAsync(ambiguousSubmissionId, seed.EventId, seed.AmbiguousParticipantId, seed.AccountId, DateTimeOffset.UtcNow);
        await InsertLegacySubmissionAsync(duplicateSubmissionId, seed.EventId, seed.UniqueParticipantId, seed.AccountId, DateTimeOffset.UtcNow);
        await InsertLegacySubmissionAsync(hiddenApprovedSubmissionId, seed.EventId, seed.UniqueParticipantId, seed.AccountId, DateTimeOffset.UtcNow, "Approved", publicEvidenceHidden: true);
        await InsertLegacySubmissionAsync(invalidParticipantSubmissionId, seed.EventId, Guid.NewGuid(), seed.AccountId, DateTimeOffset.UtcNow);
        await using (var legacy = new ApplicationDbContext(options))
        {
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO evidence_assets (id, submission_id, storage_key, original_filename, media_type, byte_size, pixel_width, pixel_height, checksum, uploaded_at, uploaded_by_account_id, role, active)
                VALUES ({Guid.NewGuid()}, {duplicateSubmissionId}, {"one"}, {"one.png"}, {"image/png"}, {1L}, {1}, {1}, {"one"}, {DateTimeOffset.UtcNow}, {seed.AccountId}, {"OriginalEvidence"}, TRUE),
                       ({Guid.NewGuid()}, {duplicateSubmissionId}, {"two"}, {"two.png"}, {"image/png"}, {1L}, {1}, {1}, {"two"}, {DateTimeOffset.UtcNow}, {seed.AccountId}, {"ReplacementEvidence"}, TRUE),
                       ({orphanAssetId}, {Guid.NewGuid()}, {"orphan"}, {"orphan.png"}, {"image/png"}, {1L}, {1}, {1}, {"orphan"}, {DateTimeOffset.UtcNow}, {seed.AccountId}, {"OriginalEvidence"}, FALSE);
                INSERT INTO submission_contributions (id, submission_id, team_id, requirement_id, drop_snapshot_id, credited_participant_id, amount, applied_at, reversed_at)
                VALUES ({orphanContributionId}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, NULL, {seed.UniqueParticipantId}, {1}, {DateTimeOffset.UtcNow}, NULL);
                INSERT INTO review_actions (id, submission_id, action, performed_by_account_id, performed_at, note, before_snapshot, after_snapshot)
                VALUES ({orphanReviewId}, {Guid.NewGuid()}, {"Reject"}, {seed.AccountId}, {DateTimeOffset.UtcNow}, NULL, NULL, NULL);
                """);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => legacy.GetService<IMigrator>().MigrateAsync());
            Assert.Contains(ambiguousSubmissionId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(duplicateSubmissionId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(hiddenApprovedSubmissionId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(invalidParticipantSubmissionId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(orphanAssetId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(orphanContributionId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(orphanReviewId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains("ambiguous_attribution", exception.MessageText, StringComparison.Ordinal);
            Assert.Contains("duplicate_active_assets", exception.MessageText, StringComparison.Ordinal);
            Assert.Contains("approved_hidden", exception.MessageText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RetainedPreflightRejectsFutureOnlyAndNonPlayingSwapTargetsAndUnsupportedLegacyValues()
    {
        var seed = await SeedBaseAsync(includeAmbiguousParticipant: false);
        var futureCharacter = new OsrsCharacter(Guid.NewGuid(), "Future character", "FUTURE CHARACTER", DateTimeOffset.UtcNow);
        var informationalCharacter = new OsrsCharacter(Guid.NewGuid(), "Informational character", "INFORMATIONAL CHARACTER", DateTimeOffset.UtcNow);
        var futureSubmissionId = Guid.NewGuid();
        var invalidTargetSubmissionId = Guid.NewGuid();
        var unsupportedSubmissionId = Guid.NewGuid();
        var unsupportedReviewId = Guid.NewGuid();
        await using (var current = new ApplicationDbContext(options))
        {
            current.OsrsCharacters.AddRange(futureCharacter, informationalCharacter);
            current.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), seed.EventId, seed.UniqueParticipantId, futureCharacter.Id, 1, DateTimeOffset.UtcNow, null, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null));
            current.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), seed.EventId, seed.SwappedParticipantId, informationalCharacter.Id, 2, DateTimeOffset.UtcNow, null, null, EventCharacterRole.Informational, null, null, null));
            current.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(Guid.NewGuid(), seed.EventId, seed.UniqueParticipantId, seed.UniqueCharacterId, futureCharacter.Id, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow, seed.AccountId, "future-only test"));
            current.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(Guid.NewGuid(), seed.EventId, seed.SwappedParticipantId, seed.SwappedCharacterId, informationalCharacter.Id, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, seed.AccountId, "non-playing test"));
            await current.SaveChangesAsync();
        }
        await InsertLegacySubmissionAsync(futureSubmissionId, seed.EventId, seed.UniqueParticipantId, seed.AccountId, DateTimeOffset.UtcNow);
        await InsertLegacySubmissionAsync(invalidTargetSubmissionId, seed.EventId, seed.SwappedParticipantId, seed.AccountId, DateTimeOffset.UtcNow);
        await InsertLegacySubmissionAsync(unsupportedSubmissionId, seed.EventId, seed.UniqueParticipantId, seed.AccountId, DateTimeOffset.UtcNow, "UnsupportedLegacyStatus");
        await using (var legacy = new ApplicationDbContext(options))
        {
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO review_actions (id, submission_id, action, performed_by_account_id, performed_at, note, before_snapshot, after_snapshot)
                VALUES ({unsupportedReviewId}, {unsupportedSubmissionId}, {"UnsupportedLegacyAction"}, {seed.AccountId}, {DateTimeOffset.UtcNow}, NULL, NULL, NULL);
                """);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => legacy.GetService<IMigrator>().MigrateAsync());
            Assert.Contains(futureSubmissionId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(invalidTargetSubmissionId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(unsupportedSubmissionId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains(unsupportedReviewId.ToString(), exception.MessageText, StringComparison.Ordinal);
            Assert.Contains("future_only_swaps", exception.MessageText, StringComparison.Ordinal);
            Assert.Contains("invalid_swap_targets", exception.MessageText, StringComparison.Ordinal);
            Assert.Contains("unsupported_status", exception.MessageText, StringComparison.Ordinal);
            Assert.Contains("unsupported_review_actions", exception.MessageText, StringComparison.Ordinal);
        }
    }

    private async Task<Seed> SeedBaseAsync(bool includeAmbiguousParticipant)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), $"slice8-{Guid.NewGuid():N}", "SLICE8", DateTimeOffset.UtcNow.AddDays(-2));
        var item = new BingoEvent(Guid.NewGuid(), "Slice 8 retained", $"slice8-{Guid.NewGuid():N}", "UTC", account.Id, DateTimeOffset.UtcNow.AddDays(-2));
        var swappedParticipant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, item.CreatedAt, SignupSource.AdminCreated);
        var uniqueParticipant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 2, item.CreatedAt, SignupSource.AdminCreated);
        var ambiguousParticipant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 3, item.CreatedAt, SignupSource.AdminCreated);
        var oldCharacter = new OsrsCharacter(Guid.NewGuid(), "Old character", "OLD CHARACTER", item.CreatedAt);
        var swappedCharacter = new OsrsCharacter(Guid.NewGuid(), "Swapped character", "SWAPPED CHARACTER", item.CreatedAt);
        var uniqueCharacter = new OsrsCharacter(Guid.NewGuid(), "Unique character", "UNIQUE CHARACTER", item.CreatedAt);
        var ambiguousCharacter = new OsrsCharacter(Guid.NewGuid(), "Ambiguous one", "AMBIGUOUS ONE", item.CreatedAt);
        var secondAmbiguousCharacter = new OsrsCharacter(Guid.NewGuid(), "Ambiguous two", "AMBIGUOUS TWO", item.CreatedAt);
        var assignments = new List<EventParticipantCharacter>
        {
            new(Guid.NewGuid(), item.Id, swappedParticipant.Id, oldCharacter.Id, 0, item.CreatedAt, null, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null),
            new(Guid.NewGuid(), item.Id, swappedParticipant.Id, swappedCharacter.Id, 1, item.CreatedAt, null, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null),
            new(Guid.NewGuid(), item.Id, uniqueParticipant.Id, uniqueCharacter.Id, 0, item.CreatedAt, null, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null)
        };
        if (includeAmbiguousParticipant)
        {
            assignments.Add(new(Guid.NewGuid(), item.Id, ambiguousParticipant.Id, ambiguousCharacter.Id, 0, item.CreatedAt, null, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null));
            assignments.Add(new(Guid.NewGuid(), item.Id, ambiguousParticipant.Id, secondAmbiguousCharacter.Id, 1, item.CreatedAt, null, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null));
        }
        var swap = new EventParticipantCharacterSwap(Guid.NewGuid(), item.Id, swappedParticipant.Id, oldCharacter.Id, swappedCharacter.Id, item.CreatedAt.AddHours(1), item.CreatedAt.AddHours(2), null, "retained test");
        await using (var current = new ApplicationDbContext(options))
        {
            await current.GetService<IMigrator>().MigrateAsync();
            current.AddRange(account, item, swappedParticipant, uniqueParticipant, ambiguousParticipant, oldCharacter, swappedCharacter, uniqueCharacter, ambiguousCharacter, secondAmbiguousCharacter);
            current.EventParticipantCharacters.AddRange(assignments);
            current.EventParticipantCharacterSwaps.Add(swap);
            await current.SaveChangesAsync();
            await current.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        }
        return new(item.Id, account.Id, swappedParticipant.Id, swappedCharacter.Id, uniqueParticipant.Id, uniqueCharacter.Id, ambiguousParticipant.Id);
    }

    private async Task InsertLegacySubmissionAsync(Guid id, Guid eventId, Guid participantId, Guid accountId, DateTimeOffset submittedAt, string status = "Pending", bool publicEvidenceHidden = false)
    {
        await using var legacy = new ApplicationDbContext(options);
        await legacy.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO submissions (id, event_id, team_id, board_tile_id, requirement_id, drop_snapshot_id, credited_participant_id, submitted_by_account_id, claimed_weight, approved_contribution, submitted_at, captain_note, status, public_evidence_hidden, public_player_hidden, expected_evidence_code, current_reviewer_note, duplicate_of_submission_id, reviewed_at, public_privacy_requested)
            VALUES ({id}, {eventId}, {Guid.NewGuid()}, {Guid.NewGuid()}, {Guid.NewGuid()}, NULL, {participantId}, {accountId}, {1}, {0}, {submittedAt}, NULL, {status}, {publicEvidenceHidden}, {false}, NULL, NULL, NULL, NULL, {false});
            """);
    }

    private static EvidenceAsset NewAsset(Guid id, Guid submissionId, Guid accountId, bool active) =>
        new(id, submissionId, $"slice8/{id:N}", "proof.png", "image/png", 1, 1, 1, id.ToString("N"), DateTimeOffset.UtcNow, accountId, EvidenceAssetRole.OriginalEvidence);

    private sealed record Seed(Guid EventId, Guid AccountId, Guid SwappedParticipantId, Guid SwappedCharacterId, Guid UniqueParticipantId, Guid UniqueCharacterId, Guid AmbiguousParticipantId);
}
