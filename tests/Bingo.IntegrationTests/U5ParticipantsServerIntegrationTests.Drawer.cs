using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bingo.IntegrationTests;

// Brief 87 item 0b: the drawer's single Save (U5-Q2), typed event-only names (U5-Q1)
// and the shared RSN rule (U5-Q4), all on PostgreSQL.
public sealed partial class U5ParticipantsServerIntegrationTests
{
    [Fact]
    public async Task DrawerSaveChangesAccountsAnswersPaymentAndNoteTogetherWithoutSavedLinks()
    {
        var world = await SeedAsync(capacity: 3, confirmed: 2, waiting: 0);
        var participant = world.Participants[0];
        var current = await CurrentAsync(participant);
        var links = await CountLinksAsync();

        var result = await SaveAsync(world, participant, current, playing:
        [
            new(current.PrimaryAssignment, current.PrimaryName, 10m),
            new(null, "Event Only", 33.5m, Primary: true)
        ], informational: [new(null, "Bank Alt", null)], answers: new()
        {
            [world.CaptainQuestionId] = "true", [world.CoCaptainQuestionId] = "U5 Seed 1", [world.CustomQuestionId] = "Evenings"
        }, paid: true, note: "Paid by GP drop");
        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Changed);

        await using var verify = Db();
        var item = await verify.EventParticipants.SingleAsync(x => x.Id == participant);
        Assert.True(item.PaymentReceived);
        Assert.Equal("Paid by GP drop", item.AdminNotes);
        Assert.True(item.CaptainVolunteer);
        Assert.Equal(current.ResponseVersion + 1, item.ResponseVersion);
        // F05: the typed account is now primary; the old primary keeps its own EHB in another slot.
        var primary = await verify.AdminPrimaryCharacters().Where(x => x.ParticipantId == participant).SingleAsync();
        Assert.Equal("Event Only", primary.Name);
        Assert.Equal(33.5m, primary.Ehb);
        Assert.Equal(3, await verify.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant && x.ReleasedAt == null));
        Assert.Equal(world.SecondQuestionId, await verify.EventParticipantCharacters.Where(x => x.Id == current.PrimaryAssignment).Select(x => x.SignupQuestionId).SingleAsync());
        Assert.Equal("Evenings", await verify.SignupAnswers.Where(x => x.EventParticipantId == participant && x.SignupQuestionId == world.CustomQuestionId).Select(x => x.Value).SingleAsync());
        // U5-Q1: the typed names are event-only, never saved accounts of the player.
        Assert.Equal(links, await CountLinksAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == world.EventId && x.Action == "participant.corrected").ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == world.EventId && x.Action == "participant.payment_updated").ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == world.EventId && x.Action == "participant.admin_note_updated").ToListAsync());
        Assert.Single(await verify.PersonalNotifications.Where(x => x.EventId == world.EventId && x.Title == "participant.accounts_changed").ToListAsync());

        // Repeat with the old baseline: stale, nothing written.
        var hash = await StateHashAsync(world.EventId);
        var repeated = await SaveAsync(world, participant, current, playing: [new(current.PrimaryAssignment, current.PrimaryName, 10m, Primary: true)], paid: true, note: "x");
        Assert.Equal("stale", repeated.Outcome);
        Assert.Equal(hash, await StateHashAsync(world.EventId));
    }

    [Fact]
    public async Task DrawerSaveIsAllOrNothingWhenTheAuditFlushFails()
    {
        var world = await SeedAsync(capacity: 3, confirmed: 1, waiting: 0);
        var participant = world.Participants[0];
        var current = await CurrentAsync(participant);
        var hash = await StateHashAsync(world.EventId);
        var failing = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).AddInterceptors(new FailOnAudit("participant.corrected")).Options;
        await using (var db = new ApplicationDbContext(failing))
        {
            await Assert.ThrowsAnyAsync<Exception>(() => Service(db).SaveAdminParticipantDrawerAsync(Request(world, participant, current,
                [new(current.PrimaryAssignment, "Renamed Main", 12m, Primary: true)], null, new() { [world.CustomQuestionId] = "Mornings" }, true, "note")));
        }
        Assert.Equal(hash, await StateHashAsync(world.EventId));
    }

    [Fact]
    public async Task DrawerSaveRefusesStaleBaselinesAndLockedAccountChangesButKeepsPaymentAndNoteLater()
    {
        var world = await SeedAsync(capacity: 3, confirmed: 1, waiting: 0);
        var participant = world.Participants[0];
        var current = await CurrentAsync(participant);

        var hash = await StateHashAsync(world.EventId);
        var staleVersion = await SaveAsync(world, participant, current with { ResponseVersion = current.ResponseVersion + 7 },
            playing: [new(current.PrimaryAssignment, current.PrimaryName, 10m, Primary: true)], paid: true, note: null);
        Assert.Equal("stale", staleVersion.Outcome);
        var stalePayment = await SaveAsync(world, participant, current, playing: null, paid: true, note: null, expectedPaid: true);
        Assert.Equal("stale", stalePayment.Outcome);
        Assert.Equal(hash, await StateHashAsync(world.EventId));

        // D16: on a Finalized event, payment and note save; account or answer changes are refused.
        await SetPhaseAsync(world.EventId, EventState.Finalized, draftLocked: true);
        hash = await StateHashAsync(world.EventId);
        var locked = await SaveAsync(world, participant, current, playing: [new(current.PrimaryAssignment, "Other Name", 10m, Primary: true)], paid: true, note: "later");
        Assert.Equal("refused", locked.Outcome);
        var lockedAnswers = await SaveAsync(world, participant, current, playing: null, answers: new() { [world.CustomQuestionId] = "x" }, paid: true, note: "later");
        Assert.Equal("refused", lockedAnswers.Outcome);
        Assert.Equal(hash, await StateHashAsync(world.EventId));
        var privateOnly = await SaveAsync(world, participant, current, playing: null, paid: true, note: "later");
        Assert.True(privateOnly.Succeeded, privateOnly.Error);
        await using var verify = Db();
        var item = await verify.EventParticipants.SingleAsync(x => x.Id == participant);
        Assert.True(item.PaymentReceived);
        Assert.Equal("later", item.AdminNotes);
        Assert.Equal(current.ResponseVersion, item.ResponseVersion);
    }

    [Fact]
    public async Task RsnRuleRefusesNewNamesAtEveryEntryPointAndLeavesStoredNamesAlone()
    {
        var world = await SeedAsync(capacity: 3, confirmed: 1, waiting: 0);
        var participant = world.Participants[0];
        // A stored legacy name outside the rule stays usable and untouched.
        await using (var db = Db())
        {
            var assignment = await db.EventParticipantCharacters.SingleAsync(x => x.EventParticipantId == participant && x.ReleasedAt == null);
            var legacy = await db.OsrsCharacters.SingleAsync(x => x.Id == assignment.OsrsCharacterId);
            db.Entry(legacy).Property(x => x.DisplayName).CurrentValue = "Legacy.Name!!";
            db.Entry(legacy).Property(x => x.NormalizedName).CurrentValue = "LEGACY.NAME!!";
            await db.SaveChangesAsync();
        }
        var current = await CurrentAsync(participant);
        var hash = await StateHashAsync(world.EventId);

        // Admin drawer: a new name outside the rule is refused before any write.
        foreach (var name in new[] { "Thirteen Chars", "Bad.Name", "" })
        {
            var refused = await SaveAsync(world, participant, current, playing: [new(current.PrimaryAssignment, current.PrimaryName, 10m, Primary: true), new(null, name, 1m)], paid: false, note: null);
            Assert.Equal("invalid", refused.Outcome);
            Assert.Equal("playing:1", refused.Field);
        }
        Assert.Equal(hash, await StateHashAsync(world.EventId));
        var keeps = await SaveAsync(world, participant, current, playing: [new(current.PrimaryAssignment, "Legacy.Name!!", 11m, Primary: true), new(null, "Ok_Name-12", 1m)], paid: false, note: null);
        Assert.True(keeps.Succeeded, keeps.Error);
        await using (var verify = Db())
            Assert.True(await verify.OsrsCharacters.AnyAsync(x => x.DisplayName == "Legacy.Name!!"));

        // Profile (My accounts): add and rename are refused before the provider; an
        // unchanged stored name is not re-validated (it reaches the provider step).
        var owner = world.Owners[0];
        await using (var db = Db())
        {
            var accounts = new MyAccountsService(db, TimeProvider.System);
            var add = await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.AddOrReactivateAsync(owner, "Way too long name", null, 1m, CancellationToken.None));
            Assert.Equal(RsnRule.Message, add.Message);
            var link = await db.AccountOsrsCharacters.SingleAsync(x => x.AccountId == owner);
            var rename = await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.UpdateAsync(owner, link.Id, link.Version, "Bad!Name", null, 1m, CancellationToken.None));
            Assert.Equal(RsnRule.Message, rename.Message);
            var correct = await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.CorrectAsync(owner, link.Id, "Bad!Name", CancellationToken.None));
            Assert.Equal(RsnRule.Message, correct.Message);
            var unchanged = await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.UpdateAsync(owner, link.Id, link.Version, "Legacy.Name!!", "label", 1m, CancellationToken.None));
            Assert.NotEqual(RsnRule.Message, unchanged.Message);
        }

        // Onboarding: the first account name follows the rule.
        await using (var db = Db())
        {
            var identities = new AccountIdentityService(db, new PasswordHasher<Account>(), TimeProvider.System);
            var onboarding = await Assert.ThrowsAsync<InvalidOperationException>(() => identities.CompleteOnboardingAsync($"discord-{Guid.NewGuid():N}", "Discord", $"u5new{Guid.NewGuid():N}"[..14], "Name/With/Slash", "a-long-enough-password-1", CancellationToken.None));
            Assert.Equal(RsnRule.Message, onboarding.Message);
        }
    }

    /* ---------------- helpers ---------------- */

    private sealed record Current(int ResponseVersion, Guid PrimaryAssignment, string PrimaryName, bool Paid, string? Note);

    private async Task<Current> CurrentAsync(Guid participantId)
    {
        await using var db = Db();
        var participant = await db.EventParticipants.AsNoTracking().SingleAsync(x => x.Id == participantId);
        var primary = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                             join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                             where assignment.EventParticipantId == participantId && assignment.ReleasedAt == null && assignment.EventRole == EventCharacterRole.Playing
                             orderby assignment.RegistrationOrder
                             select new { assignment.Id, character.DisplayName }).FirstAsync();
        return new(participant.ResponseVersion, primary.Id, primary.DisplayName, participant.PaymentReceived, participant.AdminNotes);
    }

    private static AdminParticipantDrawerSaveRequest Request(World world, Guid participant, Current current, List<AdminDrawerAccount>? playing, List<AdminDrawerAccount>? informational,
        Dictionary<Guid, string>? answers, bool paid, string? note, bool? expectedPaid = null) =>
        new(world.EventId, participant, world.AdminId, "admin", paid ? PaymentStatus.Paid : PaymentStatus.Unpaid, note,
            (expectedPaid ?? current.Paid) ? PaymentStatus.Paid : PaymentStatus.Unpaid, current.Note, current.ResponseVersion, playing, informational, answers);

    private async Task<AdminParticipantDrawerSaveResult> SaveAsync(World world, Guid participant, Current current, List<AdminDrawerAccount>? playing,
        bool paid, string? note, List<AdminDrawerAccount>? informational = null, Dictionary<Guid, string>? answers = null, bool? expectedPaid = null)
    {
        await using var db = Db();
        return await Service(db).SaveAdminParticipantDrawerAsync(Request(world, participant, current, playing, informational, answers, paid, note, expectedPaid));
    }

    private async Task<int> CountLinksAsync()
    {
        await using var db = Db();
        return await db.AccountOsrsCharacters.CountAsync();
    }

    private sealed class FailOnAudit(string action) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry => entry.State == EntityState.Added && entry.Entity.Action == action)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated audit persistence failure."))
                : ValueTask.FromResult(result);
    }
}

