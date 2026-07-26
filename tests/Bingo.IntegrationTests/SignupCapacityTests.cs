using Bingo.Application.Signups;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class SignupCapacityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_signup_tests").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> _options = null!;
    public async Task InitializeAsync() { await _database.StartAsync(); _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_database.GetConnectionString()).Options; await using var db = new ApplicationDbContext(_options); await db.Database.EnsureCreatedAsync(); }
    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task ExactCapPlacesNextSignupOnWaitingListAndIncreasePromotesIt()
    {
        var eventId = await CreateOpenEventAsync(1);
        await using var db = new ApplicationDbContext(_options); var service = CreateService(db);
        var first = await service.SignUpAsync(Request(eventId, "First")); var second = await service.SignUpAsync(Request(eventId, "Second")); var promoted = await service.IncreaseCapacityAndPromoteAsync(eventId, 2);
        Assert.Equal(SignupStatus.Confirmed, first.Status); Assert.Equal(SignupStatus.WaitingList, second.Status); Assert.Equal(1, second.WaitingListPosition); Assert.Equal(1, promoted);
        var secondId = await db.PrimaryCharacters().Where(p => p.Name == "Second").Select(p => p.ParticipantId).SingleAsync();
        Assert.Equal(SignupStatus.Confirmed, await db.EventParticipants.Where(p => p.Id == secondId).Select(p => p.SignupStatus).SingleAsync());
    }

    [Fact]
    public async Task RemovingConfirmedParticipantPromotesFirstWaitingParticipantBeforeDraftLock()
    {
        var eventId = await CreateOpenEventAsync(1);
        await using var db = new ApplicationDbContext(_options);
        var service = CreateService(db);

        await service.SignUpAsync(Request(eventId, "Confirmed"));
        await service.SignUpAsync(Request(eventId, "Waiting First"));
        await service.SignUpAsync(Request(eventId, "Waiting Second"));

        var confirmedId = await db.PrimaryCharacters().Where(p => p.Name == "Confirmed").Select(p => p.ParticipantId).SingleAsync();
        var confirmed = await db.EventParticipants.SingleAsync(p => p.Id == confirmedId);
        confirmed.Remove(DateTimeOffset.UtcNow, "Cannot participate");
        await db.SaveChangesAsync();

        var promoted = await service.PromoteAvailablePlacesAsync(eventId);

        Assert.Equal(1, promoted);
        Assert.Equal(
            SignupStatus.Confirmed,
            await db.EventParticipants.Where(p => p.Id == db.PrimaryCharacters().Where(a => a.Name == "Waiting First").Select(a => a.ParticipantId).Single()).Select(p => p.SignupStatus).SingleAsync());
        Assert.Equal(
            SignupStatus.WaitingList,
            await db.EventParticipants.Where(p => p.Id == db.PrimaryCharacters().Where(a => a.Name == "Waiting Second").Select(a => a.ParticipantId).Single()).Select(p => p.SignupStatus).SingleAsync());
    }

    [Fact]
    public async Task SimultaneousSignupForLastPlaceCannotOverfillCap()
    {
        var eventId = await CreateOpenEventAsync(1);
        await using var db1 = new ApplicationDbContext(_options); await using var db2 = new ApplicationDbContext(_options); var service1 = CreateService(db1); var service2 = CreateService(db2);
        var results = await Task.WhenAll(service1.SignUpAsync(Request(eventId, "Concurrent One")), service2.SignUpAsync(Request(eventId, "Concurrent Two")));
        Assert.Single(results, r => r.Status == SignupStatus.Confirmed); Assert.Single(results, r => r.Status == SignupStatus.WaitingList);
    }

    [Fact]
    public async Task DraftLockPreventsCapacityChangesAndAutomaticPromotion()
    {
        var eventId = await CreateOpenEventAsync(1); await using var db = new ApplicationDbContext(_options); var service = CreateService(db); await service.SignUpAsync(Request(eventId, "Locked One")); await service.SignUpAsync(Request(eventId, "Locked Two"));
        var item = await db.Events.SingleAsync(e => e.Id == eventId); item.SetDraftLocked(true); await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.IncreaseCapacityAndPromoteAsync(eventId, 2));
        Assert.Equal(0, await service.PromoteAvailablePlacesAsync(eventId));
        Assert.Equal(1, await db.Events.Where(e => e.Id == eventId).Select(e => e.ParticipantCap).SingleAsync());
        Assert.Equal(SignupStatus.WaitingList, await db.EventParticipants.Where(p => p.Id == db.PrimaryCharacters().Where(a => a.Name == "Locked Two").Select(a => a.ParticipantId).Single()).Select(p => p.SignupStatus).SingleAsync());
    }

    [Fact]
    public async Task DraftLockRejectsNewSignupsIncludingAdminImports()
    {
        var eventId = await CreateOpenEventAsync(2); await using var db = new ApplicationDbContext(_options);
        var item = await db.Events.SingleAsync(e => e.Id == eventId); item.SetDraftLocked(true); await db.SaveChangesAsync(); var service = CreateService(db);

        var website = await service.SignUpAsync(Request(eventId, "Late Website"));
        var import = await service.SignUpAsync(Request(eventId, "Late Import") with { BypassAvailability = true });

        Assert.False(website.Succeeded); Assert.False(import.Succeeded);
        Assert.Contains("locked", website.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.EventParticipants.Where(p => p.EventId == eventId).ToListAsync());
    }

    [Fact]
    public async Task AdminCreatedExternalPlayerDoesNotConsumeSignupCapacity()
    {
        var eventId = await CreateOpenEventAsync(1); await using var db = new ApplicationDbContext(_options); var now = DateTimeOffset.UtcNow;
        db.EventParticipants.Add(new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated, null));
        await db.SaveChangesAsync(); var service = CreateService(db);

        var firstSignup = await service.SignUpAsync(Request(eventId, "Internal One"));
        var secondSignup = await service.SignUpAsync(Request(eventId, "Internal Two"));

        Assert.Equal(SignupStatus.Confirmed, firstSignup.Status);
        Assert.Equal(SignupStatus.WaitingList, secondSignup.Status);
    }

    [Fact]
    public async Task DuplicateDetectionIsScopedToOneEvent()
    {
        var firstEvent = await CreateOpenEventAsync(2); var secondEvent = await CreateOpenEventAsync(2); await using var db = new ApplicationDbContext(_options); var service = CreateService(db);
        var first = await service.SignUpAsync(Request(firstEvent, "Same Name")); var duplicate = await service.SignUpAsync(Request(firstEvent, "same name")); var otherEvent = await service.SignUpAsync(Request(secondEvent, "Same Name"));
        Assert.True(first.Succeeded); Assert.False(duplicate.Succeeded); Assert.True(otherEvent.Succeeded);
    }

    [Fact]
    public async Task AdminImportBypassesAvailabilityButStillRespectsParticipantCap()
    {
        var eventId = await CreateOpenEventAsync(1);
        await using var db = new ApplicationDbContext(_options);
        var item = await db.Events.SingleAsync(e => e.Id == eventId);
        item.ConfigureSignup(false, false, false, null);
        item.CloseSignups();
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var first = await service.SignUpAsync(Request(eventId, "Imported One") with { BypassAvailability = true, Source = SignupSource.CsvImport, SecondAccountName = "Imported Helper" });
        var second = await service.SignUpAsync(Request(eventId, "Imported Two") with { BypassAvailability = true, Source = SignupSource.CsvImport });

        Assert.Equal(SignupStatus.Confirmed, first.Status);
        Assert.Equal(SignupStatus.WaitingList, second.Status);
        var importedAssignments = await db.EventParticipantCharacters
            .Where(x => x.EventParticipantId == first.ParticipantId)
            .OrderBy(x => x.RegistrationOrder)
            .ToListAsync();
        Assert.Collection(
            importedAssignments,
            playing =>
            {
                Assert.Equal(EventCharacterRole.Playing, playing.EventRole);
                Assert.Equal(100m, playing.EhbSnapshot);
                Assert.Equal(EhbSource.Import, playing.EhbSource);
            },
            informational =>
            {
                Assert.Equal(EventCharacterRole.Informational, informational.EventRole);
                Assert.Null(informational.EhbSnapshot);
                Assert.Null(informational.EhbSource);
            });
    }

    [Fact]
    public async Task FixedSignupEditReplacesAuthoritativeAssignmentsAndPreservesHistory()
    {
        var eventId = await CreateOpenEventAsync(2);
        await using var db = new ApplicationDbContext(_options);
        var signup = CreateService(db);
        var created = await signup.SignUpAsync(Request(eventId, "Original Main") with { SecondAccountName = "Original Helper" });
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == created.ParticipantId);
        var authority = new EventParticipantCharacterService(db, TimeProvider.System);

        await authority.ApplyFixedSignupAssignmentsAsync(
            participant, "Edited Main", 456m, "Edited Helper", EhbSource.Manual, null, CancellationToken.None);
        await db.SaveChangesAsync();

        var primary = await db.PrimaryCharacters().SingleAsync(x => x.ParticipantId == participant.Id);
        Assert.Equal("Edited Main", primary.Name);
        Assert.Equal(456m, primary.Ehb);
        var all = await db.EventParticipantCharacters
            .Where(x => x.EventParticipantId == participant.Id)
            .OrderBy(x => x.RegistrationOrder)
            .ToListAsync();
        Assert.Equal(4, all.Count);
        Assert.Equal(2, all.Count(x => x.ReleasedAt is not null));
        Assert.All(all.Where(x => x.ReleasedAt is not null), x => Assert.Null(x.ReleasedByAccountId));
        Assert.Single(all, x => x.ReleasedAt == null && x.EventRole == EventCharacterRole.Informational && x.EhbSnapshot == null);
    }

    [Fact]
    public async Task AdminCorrectionUpdatesPrimarySnapshotWithAdminSource()
    {
        var eventId = await CreateOpenEventAsync(2);
        await using var db = new ApplicationDbContext(_options);
        var created = await CreateService(db).SignUpAsync(Request(eventId, "Correction Target"));
        var participant = await db.EventParticipants.SingleAsync(x => x.Id == created.ParticipantId);
        var authority = new EventParticipantCharacterService(db, TimeProvider.System);

        await authority.ApplyFixedSignupAssignmentsAsync(
            participant, "Correction Target", 789m, null, EhbSource.AdminCorrection, null, CancellationToken.None);
        await db.SaveChangesAsync();

        var assignment = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null);
        Assert.Equal(789m, assignment.EhbSnapshot);
        Assert.Equal(EhbSource.AdminCorrection, assignment.EhbSource);
    }

    private async Task<Guid> CreateOpenEventAsync(int cap) { await using var db = new ApplicationDbContext(_options); var now = DateTimeOffset.UtcNow; var item = new BingoEvent(Guid.NewGuid(), $"Test {Guid.NewGuid():N}", $"test-{Guid.NewGuid():N}", "Test", "Europe/Copenhagen", now.AddHours(-1), now.AddHours(1), now.AddDays(1), now.AddDays(2), now.AddDays(3), cap, Guid.NewGuid(), now); item.OpenSignups(); db.Events.Add(item); await db.SaveChangesAsync(); return item.Id; }
    private static SignupService CreateService(ApplicationDbContext db) => new(db, new EventParticipantCharacterService(db, TimeProvider.System), new PrivateEditTokenService(), new SecretHasher(), TimeProvider.System);
    private static SignupRequest Request(Guid eventId, string name) => new(eventId, name, 100, null, null, null, false, null, new Dictionary<Guid, string>());
}
