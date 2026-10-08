using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

// Brief 87 (U5) server items. Every service here is built WITHOUT a Wise Old Man
// validator: a provider call would throw, so a passing test proves none is made.
public sealed partial class U5ParticipantsServerIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_u5_participants")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    // P-3 / D2: Add when full joins the waiting list; the explicit +1 confirms only the
    // new participant with the exact new cap; a repeated request writes nothing.
    [Fact]
    public async Task AddWhenFullWaitsAndExplicitPlaceConfirmsOnlyThatParticipantOnce()
    {
        var world = await SeedAsync(capacity: 2, confirmed: 2, waiting: 1);
        var (firstOwner, firstCharacter) = await SavedOwnerAsync(world, "Add Waiter", 14.5m);
        var (secondOwner, secondCharacter) = await SavedOwnerAsync(world, "Add Placed", 22m);

        await using (var db = Db())
        {
            var version = await EventVersionAsync(db, world.EventId);
            var waiting = await Service(db).AddSavedParticipantAsync(new(world.EventId, firstOwner, world.AdminId, "admin", [firstCharacter], firstCharacter, ExpectedEventVersion: version));
            Assert.True(waiting.Succeeded, waiting.Error);
            Assert.Equal(SignupStatus.WaitingList, waiting.Status);
            Assert.Equal(2, waiting.WaitingPosition);
            Assert.Equal(2, waiting.EffectiveParticipantCap);
            Assert.False(waiting.AddedPlace);
        }

        long versionBeforePlace;
        AddSavedParticipantRequest placeRequest;
        await using (var db = Db())
        {
            versionBeforePlace = await EventVersionAsync(db, world.EventId);
            placeRequest = new(world.EventId, secondOwner, world.AdminId, "admin", [secondCharacter], secondCharacter, PaymentStatus.Paid, ExpandCapacityWhenFull: true, ExpectedEventVersion: versionBeforePlace);
            var placed = await Service(db).AddSavedParticipantAsync(placeRequest);
            Assert.True(placed.Succeeded, placed.Error);
            Assert.Equal(SignupStatus.Confirmed, placed.Status);
            Assert.Equal(3, placed.EffectiveParticipantCap);
            Assert.True(placed.AddedPlace);
        }

        var hash = await StateHashAsync(world.EventId);
        await using (var db = Db())
        {
            var repeated = await Service(db).AddSavedParticipantAsync(placeRequest);
            Assert.False(repeated.Succeeded);
            var fresh = await Service(db).AddSavedParticipantAsync(placeRequest with { ExpectedEventVersion = await EventVersionAsync(db, world.EventId) });
            Assert.False(fresh.Succeeded);
        }
        Assert.Equal(hash, await StateHashAsync(world.EventId));

        await using var verify = Db();
        var item = await verify.Events.SingleAsync(x => x.Id == world.EventId);
        Assert.Equal(3, item.ParticipantCap);
        Assert.Equal(versionBeforePlace + 1, item.Version);
        // Nobody else was promoted: the original waiter is still first in the queue.
        Assert.Equal(SignupStatus.WaitingList, await StatusAsync(verify, world.Participants[2]));
        Assert.Equal(3, await verify.EventParticipants.CountAsync(x => x.EventId == world.EventId && x.SignupStatus == SignupStatus.Confirmed));
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == world.EventId && x.Action == "participant.admin_saved_created" && x.Details!.Contains("\"expandedCapacity\":true")).ToListAsync());
    }

    // D3 / P-7 (RL-1): admin Restore uses stored accounts with no provider; capacity
    // and reservation rules still hold; refusals write nothing.
    [Fact]
    public async Task RestoreReacquiresStoredAccountsWithoutWiseOldManAndRefusalsWriteNothing()
    {
        var world = await SeedAsync(capacity: 1, confirmed: 1, waiting: 0);
        var restored = world.Participants[0];
        var character = await PrimaryCharacterAsync(restored);
        await WithdrawAsync(world, restored);

        // A place is open: a normal restore confirms and reacquires the stored account.
        var open = await RestoreAsync(world, restored, expand: false);
        Assert.True(open.Succeeded, open.Error);
        Assert.Equal(SignupStatus.Confirmed, open.Status);
        Assert.Equal(1, open.EffectiveParticipantCap);
        Assert.False(open.AddedPlace);
        Assert.Equal(character, await PrimaryCharacterAsync(restored));

        // Full: normal restore joins the waiting list; the explicit place confirms with cap + 1.
        await WithdrawAsync(world, restored);
        await AddConfirmedFillerAsync(world, "Restore Filler");
        var waiting = await RestoreAsync(world, restored, expand: false);
        Assert.True(waiting.Succeeded, waiting.Error);
        Assert.Equal(SignupStatus.WaitingList, waiting.Status);
        Assert.Equal(1, waiting.EffectiveParticipantCap);
        await WithdrawAsync(world, restored);
        var placed = await RestoreAsync(world, restored, expand: true);
        Assert.True(placed.Succeeded, placed.Error);
        Assert.Equal(SignupStatus.Confirmed, placed.Status);
        Assert.Equal(2, placed.EffectiveParticipantCap);
        Assert.True(placed.AddedPlace);

        // Refusals: a claimed account, a place option while places are open, a locked draft.
        await WithdrawAsync(world, restored);
        await ClaimAsync(world, character);
        var hash = await StateHashAsync(world.EventId);
        var claimed = await RestoreAsync(world, restored, expand: false);
        Assert.False(claimed.Succeeded);
        Assert.Contains("assigned", claimed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(hash, await StateHashAsync(world.EventId));

        await ReleaseClaimAsync(world, character);
        hash = await StateHashAsync(world.EventId);
        var notFull = await RestoreAsync(world, restored, expand: true);
        Assert.False(notFull.Succeeded);
        Assert.Equal(hash, await StateHashAsync(world.EventId));

        await SetPhaseAsync(world.EventId, EventState.SignupClosed, draftLocked: true);
        hash = await StateHashAsync(world.EventId);
        var locked = await RestoreAsync(world, restored, expand: false);
        Assert.False(locked.Succeeded);
        Assert.Equal(hash, await StateHashAsync(world.EventId));
    }

    // S5: after the draft starts, Withdraw is refused; there is no silent reroute to
    // finalized-roster removal (that stays on Teams).
    [Fact]
    public async Task WithdrawAfterDraftStartIsRefusedWithoutRosterRemoval()
    {
        var world = await SeedAsync(capacity: 2, confirmed: 2, waiting: 0);
        var member = world.Participants[0];
        Guid membershipId;
        await using (var db = Db())
        {
            var team = new Team(Guid.NewGuid(), world.EventId, "S5 team", $"s5-{Guid.NewGuid():N}", TeamFormationType.Drafted, null, true, DateTimeOffset.UtcNow);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, member, TeamMembershipRole.Captain, DateTimeOffset.UtcNow, null, "s5");
            db.AddRange(team, membership);
            await db.SaveChangesAsync();
            membershipId = membership.Id;
        }

        foreach (var (state, locked) in new[] { (EventState.SignupClosed, true), (EventState.Live, true) })
        {
            await SetPhaseAsync(world.EventId, state, locked);
            var hash = await StateHashAsync(world.EventId);
            await using (var db = Db())
            {
                var result = await Service(db).WithdrawAsync(world.EventId, member, world.AdminId, "admin", true, null, 1);
                Assert.False(result.Succeeded);
                Assert.False(string.IsNullOrEmpty(result.Error));
            }
            Assert.Equal(hash, await StateHashAsync(world.EventId));
            await using var verify = Db();
            Assert.Null(await verify.TeamMemberships.Where(x => x.Id == membershipId).Select(x => x.LeftAt).SingleAsync());
            Assert.Equal(SignupStatus.Confirmed, await StatusAsync(verify, member));
        }
    }

    // Withdrawal promotion is reported structurally for the toast.
    [Fact]
    public async Task WithdrawingAConfirmedParticipantReportsThePromotedWaiter()
    {
        var world = await SeedAsync(capacity: 1, confirmed: 1, waiting: 2);
        await using var db = Db();
        var result = await Service(db).WithdrawAsync(world.EventId, world.Participants[0], world.AdminId, "admin", true);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal([world.Participants[1]], result.PromotedParticipantIds);
        Assert.Equal(1, result.EffectiveParticipantCap);
        var waiting = await Service(db).WithdrawAsync(world.EventId, world.Participants[2], world.AdminId, "admin", true);
        Assert.True(waiting.Succeeded, waiting.Error);
        Assert.Empty(waiting.PromotedParticipantIds!);
    }

    /* ---------------- helpers ---------------- */

    private ApplicationDbContext Db() => new(options);

    internal static SignupService Service(ApplicationDbContext db) => new(db, new SecretHasher(), TimeProvider.System);

    private sealed record World(Guid EventId, Guid AdminId, Guid PrimaryQuestionId, Guid SecondQuestionId, Guid AltQuestionId,
        Guid CaptainQuestionId, Guid CoCaptainQuestionId, Guid CustomQuestionId, List<Guid> Participants, List<Guid> Owners);

    private async Task<World> SeedAsync(int capacity, int confirmed, int waiting)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = Db();
        var admin = Website($"u5-admin-{Guid.NewGuid():N}", now, GlobalRole.Admin);
        var item = new BingoEvent(Guid.NewGuid(), "U5 Participants", $"u5-{Guid.NewGuid():N}", "", "UTC", now.AddHours(-1), now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3), capacity, admin.Id, now);
        item.OpenSignups(now);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Main account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var second = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "second_playing", "Second account", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Playing);
        var alt = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "alt_account", "Alt account", SignupQuestionType.Account, false, 2, null, SignupSystemField.None, EventCharacterRole.Informational);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "captain_volunteer", "Volunteer to captain", SignupQuestionType.YesNo, true, 3, null, SignupSystemField.CaptainVolunteer);
        var coCaptain = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "co_captain_name", "Requested co-captain", SignupQuestionType.Text, false, 4, null, SignupSystemField.CoCaptainName);
        var custom = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "availability", "Availability", SignupQuestionType.Text, true, 5, null);
        db.AddRange(admin, item, form, primary, second, alt, captain, coCaptain, custom);
        var participants = new List<Guid>(); var owners = new List<Guid>();
        for (var index = 0; index < confirmed + waiting; index++)
        {
            var owner = Website($"u5-owner-{Guid.NewGuid():N}", now);
            var status = index < confirmed ? SignupStatus.Confirmed : SignupStatus.WaitingList;
            var participant = new EventParticipant(Guid.NewGuid(), item.Id, status, index + 1, now.AddMinutes(index - 60), SignupSource.Website);
            participant.AssignOwner(owner);
            var character = new OsrsCharacter(Guid.NewGuid(), $"U5 Seed {index}", $"U5 SEED {Guid.NewGuid():N}", now);
            db.AddRange(owner, participant, character,
                new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, 10 + index, now),
                new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, 0, now, owner.Id, primary.Id, EventCharacterRole.Playing, 10 + index, EhbSource.Manual, null),
                new SignupAnswer(Guid.NewGuid(), participant.Id, primary.Id, primary.Label, string.Empty, character.Id));
            participants.Add(participant.Id); owners.Add(owner.Id);
        }
        await db.SaveChangesAsync();
        return new World(item.Id, admin.Id, primary.Id, second.Id, alt.Id, captain.Id, coCaptain.Id, custom.Id, participants, owners);
    }

    private async Task<(Guid Owner, Guid Character)> SavedOwnerAsync(World world, string name, decimal? savedEhb)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = Db();
        var owner = Website($"u5-saved-{Guid.NewGuid():N}", now);
        var character = new OsrsCharacter(Guid.NewGuid(), name, $"{name.ToUpperInvariant()} {Guid.NewGuid():N}", now);
        db.AddRange(owner, character, new AccountOsrsCharacter(Guid.NewGuid(), owner.Id, character.Id, owner.Id, true, 0, null, savedEhb, now));
        await db.SaveChangesAsync();
        return (owner.Id, character.Id);
    }

    private async Task AddConfirmedFillerAsync(World world, string name)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = Db();
        var owner = Website($"u5-filler-{Guid.NewGuid():N}", now);
        var sequence = await db.EventParticipants.Where(x => x.EventId == world.EventId).MaxAsync(x => x.SignupSequence) + 1;
        var participant = new EventParticipant(Guid.NewGuid(), world.EventId, SignupStatus.Confirmed, sequence, now, SignupSource.AdminCreated);
        participant.AssignOwner(owner);
        var character = new OsrsCharacter(Guid.NewGuid(), name, $"{name.ToUpperInvariant()} {Guid.NewGuid():N}", now);
        db.AddRange(owner, participant, character, new EventParticipantCharacter(Guid.NewGuid(), world.EventId, participant.Id, character.Id, 0, now, world.AdminId, world.PrimaryQuestionId, EventCharacterRole.Playing, 5, EhbSource.Manual, null));
        await db.SaveChangesAsync();
    }

    private async Task<Guid> ClaimAsync(World world, Guid characterId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = Db();
        var owner = Website($"u5-claim-{Guid.NewGuid():N}", now);
        var participant = new EventParticipant(Guid.NewGuid(), world.EventId, SignupStatus.WaitingList, 900, now, SignupSource.Website);
        participant.AssignOwner(owner);
        db.AddRange(owner, participant, new EventParticipantCharacter(Guid.NewGuid(), world.EventId, participant.Id, characterId, 0, now, owner.Id, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null));
        await db.SaveChangesAsync();
        return participant.Id;
    }

    private async Task ReleaseClaimAsync(World world, Guid characterId)
    {
        await using var db = Db();
        var claim = await db.EventParticipantCharacters.SingleAsync(x => x.EventId == world.EventId && x.OsrsCharacterId == characterId && x.ReleasedAt == null);
        claim.Release(world.AdminId, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task WithdrawAsync(World world, Guid participantId)
    {
        await using var db = Db();
        var result = await Service(db).WithdrawAsync(world.EventId, participantId, world.AdminId, "admin", true);
        Assert.True(result.Succeeded, result.Error);
    }

    private async Task<ParticipantLifecycleResult> RestoreAsync(World world, Guid participantId, bool expand)
    {
        await using var db = Db();
        var eventVersion = await EventVersionAsync(db, world.EventId);
        var responseVersion = await db.EventParticipants.Where(x => x.Id == participantId).Select(x => x.ResponseVersion).SingleAsync();
        return await Service(db).RestoreAdminParticipantAsync(new(world.EventId, participantId, world.AdminId, "admin", expand, eventVersion, responseVersion));
    }

    private async Task SetPhaseAsync(Guid eventId, EventState state, bool draftLocked)
    {
        await using var db = Db();
        var item = await db.Events.SingleAsync(x => x.Id == eventId);
        db.Entry(item).Property(x => x.State).CurrentValue = state;
        db.Entry(item).Property(x => x.DraftLocked).CurrentValue = draftLocked;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> PrimaryCharacterAsync(Guid participantId)
    {
        await using var db = Db();
        return await db.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
            .OrderBy(x => x.RegistrationOrder).Select(x => x.OsrsCharacterId).FirstAsync();
    }

    private static Task<long> EventVersionAsync(ApplicationDbContext db, Guid eventId) =>
        db.Events.AsNoTracking().Where(x => x.Id == eventId).Select(x => x.Version).SingleAsync();

    private static Task<SignupStatus> StatusAsync(ApplicationDbContext db, Guid participantId) =>
        db.EventParticipants.AsNoTracking().Where(x => x.Id == participantId).Select(x => x.SignupStatus).SingleAsync();

    // Everything a participant mutation can touch, so "no write" is a single comparison.
    private async Task<string> StateHashAsync(Guid eventId)
    {
        await using var db = Db();
        var item = await db.Events.AsNoTracking().Where(x => x.Id == eventId).Select(x => new { x.Version, x.ParticipantCap }).SingleAsync();
        var participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
            .Select(x => $"{x.Id}:{x.SignupStatus}:{x.ResponseVersion}:{x.PaymentReceived}:{x.AdminNotes}:{x.CaptainVolunteer}:{x.SignupSequence}").ToListAsync();
        var assignments = await db.EventParticipantCharacters.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id)
            .Select(x => $"{x.Id}:{x.OsrsCharacterId}:{x.ReleasedAt}:{x.SignupQuestionId}:{x.EhbSnapshot}").ToListAsync();
        var answers = await db.SignupAnswers.AsNoTracking().Where(x => db.EventParticipants.Any(p => p.Id == x.EventParticipantId && p.EventId == eventId)).OrderBy(x => x.Id)
            .Select(x => $"{x.Id}:{x.Value}:{x.OsrsCharacterId}").ToListAsync();
        var memberships = await db.TeamMemberships.AsNoTracking().Where(x => db.Teams.Any(t => t.Id == x.TeamId && t.EventId == eventId)).OrderBy(x => x.Id)
            .Select(x => $"{x.Id}:{x.LeftAt}:{x.Role}").ToListAsync();
        var audits = await db.AuditEntries.AsNoTracking().CountAsync(x => x.EventId == eventId);
        var notifications = await db.PersonalNotifications.AsNoTracking().CountAsync(x => x.EventId == eventId);
        var characters = await db.OsrsCharacters.AsNoTracking().CountAsync();
        var links = await db.AccountOsrsCharacters.AsNoTracking().CountAsync();
        return string.Join("|", item, string.Join(",", participants), string.Join(",", assignments), string.Join(",", answers), string.Join(",", memberships), audits, notifications, characters, links);
    }

    private static Account Website(string name, DateTimeOffset now, GlobalRole role = GlobalRole.User)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);
        account.SetGlobalRole(role);
        return account;
    }
}
