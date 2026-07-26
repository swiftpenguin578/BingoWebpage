using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice2PersistenceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice2_persistence")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task CharacterAndLinkUniquenessIsEnforcedWithoutExclusiveCharacterOwnership()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var first = Website("link-first", now);
        var second = Website("link-second", now);
        var shared = new OsrsCharacter(Guid.NewGuid(), "Shared Main", "SHARED MAIN", now);
        db.AddRange(first, second, shared);
        db.AccountOsrsCharacters.AddRange(
            new AccountOsrsCharacter(Guid.NewGuid(), first.Id, shared.Id, first.Id, true, 0, "Main", 10m, now),
            new AccountOsrsCharacter(Guid.NewGuid(), second.Id, shared.Id, second.Id, true, 0, "Borrowed", 20m, now));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.AccountOsrsCharacters.CountAsync(x => x.OsrsCharacterId == shared.Id));
        Assert.Collection(
            await db.AccountOsrsCharacters.OrderBy(x => x.SavedEhb).Select(x => x.SavedEhb!.Value).ToListAsync(),
            value => Assert.Equal(10m, value),
            value => Assert.Equal(20m, value));

        db.AccountOsrsCharacters.Add(new AccountOsrsCharacter(Guid.NewGuid(), first.Id, shared.Id, first.Id, false, 1, null, null, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.OsrsCharacters.Add(new OsrsCharacter(Guid.NewGuid(), "shared main", "shared main", now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task OnlyOneActivePreferredLinkPerAccountIsEnforced()
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var account = Website("preferred-owner", now);
        var first = new OsrsCharacter(Guid.NewGuid(), "Preferred One", "PREFERRED ONE", now);
        var second = new OsrsCharacter(Guid.NewGuid(), "Preferred Two", "PREFERRED TWO", now);
        db.AddRange(account, first, second);
        db.AccountOsrsCharacters.AddRange(
            new AccountOsrsCharacter(Guid.NewGuid(), account.Id, first.Id, account.Id, true, 0, null, null, now),
            new AccountOsrsCharacter(Guid.NewGuid(), account.Id, second.Id, account.Id, true, 1, null, null, now));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ParticipantOwnershipAllowsExternalRowsButOnlyOneOwnerPerEvent()
    {
        var seed = await SeedEventAsync("ownership");
        await using var db = new ApplicationDbContext(options);
        var owner = Website("participant-owner", seed.Now);
        var owned = Participant(seed.EventId, "Owned", 1, seed.Now);
        var duplicate = Participant(seed.EventId, "Duplicate owner", 2, seed.Now);
        var externalOne = Participant(seed.EventId, "External one", 3, seed.Now);
        var externalTwo = Participant(seed.EventId, "External two", 4, seed.Now);
        owned.AssignOwner(owner);
        duplicate.AssignOwner(owner);
        db.AddRange(owner, owned, externalOne, externalTwo);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.EventParticipants.CountAsync(x => x.AccountId == null));

        db.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CurrentAssignmentEventConsistencyUniquenessAndConcurrencyAreEnforced()
    {
        var seed = await SeedEventAsync("assignment");
        Guid firstAssignmentId;
        Guid actorId;
        await using (var db = new ApplicationDbContext(options))
        {
            var actor = Website("assignment-actor", seed.Now);
            actorId = actor.Id;
            var firstParticipant = Participant(seed.EventId, "First assignment", 1, seed.Now);
            var secondParticipant = Participant(seed.EventId, "Second assignment", 2, seed.Now);
            var character = new OsrsCharacter(Guid.NewGuid(), "Contended", "CONTENDED", seed.Now);
            var firstAssignment = Playing(seed.EventId, firstParticipant.Id, character.Id, actor.Id, seed.Now);
            firstAssignmentId = firstAssignment.Id;
            db.AddRange(actor, firstParticipant, secondParticipant, character, firstAssignment);
            await db.SaveChangesAsync();

            db.EventParticipantCharacters.Add(Playing(seed.EventId, secondParticipant.Id, character.Id, actor.Id, seed.Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            var other = await SeedEventAsync("assignment-other");
            db.EventParticipantCharacters.Add(Playing(other.EventId, firstParticipant.Id, character.Id, actor.Id, seed.Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var first = new ApplicationDbContext(options);
        await using var second = new ApplicationDbContext(options);
        var firstCopy = await first.EventParticipantCharacters.SingleAsync(x => x.Id == firstAssignmentId);
        var secondCopy = await second.EventParticipantCharacters.SingleAsync(x => x.Id == firstAssignmentId);
        firstCopy.Release(actorId, seed.Now.AddMinutes(1));
        secondCopy.Release(actorId, seed.Now.AddMinutes(2));
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private async Task<(Guid EventId, DateTimeOffset Now)> SeedEventAsync(string slug)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = new ApplicationDbContext(options);
        var creator = Website($"creator-{slug}-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(
            Guid.NewGuid(), slug, $"{slug}-{Guid.NewGuid():N}", "", "UTC",
            now, now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddHours(1), 20, creator.Id, now);
        db.AddRange(creator, bingoEvent);
        await db.SaveChangesAsync();
        return (bingoEvent.Id, now);
    }

    private static Account Website(string name, DateTimeOffset now)
        => Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);

    private static EventParticipant Participant(Guid eventId, string name, long sequence, DateTimeOffset now)
        => new(Guid.NewGuid(), eventId, SignupStatus.Confirmed, sequence, now, SignupSource.AdminCreated, null);

    private static EventParticipantCharacter Playing(Guid eventId, Guid participantId, Guid characterId, Guid actorId, DateTimeOffset now)
        => new(Guid.NewGuid(), eventId, participantId, characterId, 0, now, actorId, null, EventCharacterRole.Playing, 1m, EhbSource.Manual, null);
}
