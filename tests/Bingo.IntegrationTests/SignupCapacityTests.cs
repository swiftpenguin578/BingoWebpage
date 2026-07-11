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
        Assert.Equal(SignupStatus.Confirmed, await db.EventParticipants.Where(p => p.PrimaryAccountName == "Second").Select(p => p.SignupStatus).SingleAsync());
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
    public async Task DraftLockStopsAutomaticPromotion()
    {
        var eventId = await CreateOpenEventAsync(1); await using var db = new ApplicationDbContext(_options); var service = CreateService(db); await service.SignUpAsync(Request(eventId, "Locked One")); await service.SignUpAsync(Request(eventId, "Locked Two"));
        var item = await db.Events.SingleAsync(e => e.Id == eventId); item.SetDraftLocked(true); await db.SaveChangesAsync(); var promoted = await service.IncreaseCapacityAndPromoteAsync(eventId, 2);
        Assert.Equal(0, promoted); Assert.Equal(SignupStatus.WaitingList, await db.EventParticipants.Where(p => p.PrimaryAccountName == "Locked Two").Select(p => p.SignupStatus).SingleAsync());
    }

    [Fact]
    public async Task DuplicateDetectionIsScopedToOneEvent()
    {
        var firstEvent = await CreateOpenEventAsync(2); var secondEvent = await CreateOpenEventAsync(2); await using var db = new ApplicationDbContext(_options); var service = CreateService(db);
        var first = await service.SignUpAsync(Request(firstEvent, "Same Name")); var duplicate = await service.SignUpAsync(Request(firstEvent, "same name")); var otherEvent = await service.SignUpAsync(Request(secondEvent, "Same Name"));
        Assert.True(first.Succeeded); Assert.False(duplicate.Succeeded); Assert.True(otherEvent.Succeeded);
    }

    private async Task<Guid> CreateOpenEventAsync(int cap) { await using var db = new ApplicationDbContext(_options); var now = DateTimeOffset.UtcNow; var item = new BingoEvent(Guid.NewGuid(), $"Test {Guid.NewGuid():N}", $"test-{Guid.NewGuid():N}", "Test", "Europe/Copenhagen", now.AddHours(-1), now.AddHours(1), now.AddDays(1), now.AddDays(2), now.AddDays(3), cap, Guid.NewGuid(), now); item.OpenSignups(); db.Events.Add(item); await db.SaveChangesAsync(); return item.Id; }
    private static SignupService CreateService(ApplicationDbContext db) => new(db, new PrivateEditTokenService(), new SecretHasher(), TimeProvider.System);
    private static SignupRequest Request(Guid eventId, string name) => new(eventId, name, 100, null, null, null, false, null, new Dictionary<Guid, string>());
}
