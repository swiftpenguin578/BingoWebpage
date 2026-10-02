using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Signups;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.Security;
using Bingo.Web.TestData;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice1IdentityIntegrationTests
{
    [Fact]
    public async Task DevelopmentResetClearsCreationOperationsAndRetainsSeededCreationJourney()
    {
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        const string ownerUsername = "creation-reset-owner";
        await new OperatorRecoveryService(db, clock, passwords).BootstrapOwnerAsync(
            ownerUsername, "synthetic-reset-password", ownerUsername, CancellationToken.None);
        var owner = await db.Accounts.SingleAsync(x => x.LoginName == ownerUsername);
        await new CatalogueSnapshotService(db, clock).ApplyAsync(Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json"));
        var requestId = Guid.NewGuid();
        var created = await new EventCreationService(db, clock).CreateAsync(requestId, "Before reset", "UTC", new(owner.Id, owner.LoginName));
        Assert.Equal(EventCreationOutcome.Completed, created.Outcome);
        Assert.Equal(created.EventId, (await db.EventCreationOperations.SingleAsync()).EventId);
        var form = await db.SignupForms.SingleAsync(x => x.EventId == created.EventId);
        var field = await new SignupService(db, new SecretHasher(), clock).AddQuestionAsync(new(
            Guid.NewGuid(), created.EventId!.Value, owner.Id, form.Version, "Before reset field", SignupQuestionType.Text));
        Assert.True(field.Succeeded, field.Error);
        Assert.Equal(field.QuestionId, (await db.SignupQuestionCreationOperations.SingleAsync()).QuestionId);

        // Exercise the real Development reset, including its explicit PostgreSQL
        // TRUNCATE list and the full seed transaction, with a referencing row present.
        var reset = await new DevelopmentScenarioSeeder(db, new DevelopmentEnvironment(), passwords, new SeedEvidenceStorage(), clock).ResetAndSeedAsync();
        db.ChangeTracker.Clear();
        Assert.Empty(await db.EventCreationOperations.ToListAsync());
        Assert.Empty(await db.SignupQuestionCreationOperations.ToListAsync());
        Assert.False(await db.Events.AnyAsync(x => x.Id == created.EventId));
        Assert.Equal(reset.Scenarios.Count, await db.Events.CountAsync());
        var seededOwner = await db.Accounts.SingleAsync(x => x.Id == owner.Id);
        Assert.True(seededOwner.Active);
        Assert.Equal(GlobalRole.SuperAdmin, seededOwner.GlobalRole);
        var admin = await db.Accounts.SingleAsync(x => x.LoginName == DevelopmentScenarioSeeder.SecondaryAdminUsername);
        Assert.True(admin.Active);
        Assert.Equal(GlobalRole.Admin, admin.GlobalRole);
        var draft = await db.Events.SingleAsync(x => x.Slug == "test-03-draft-discard-candidate");
        Assert.Equal(EventState.Draft, draft.State);
        Assert.True(await db.SignupForms.AnyAsync(x => x.EventId == draft.Id));

        // The seeded Admin can still complete and replay the ticket's creation
        // journey after reset; reset has not left stale outcome/FK state behind.
        var actor = new LifecycleActor(admin.Id, admin.LoginName);
        var service = new EventCreationService(db, clock);
        var afterReset = await service.CreateAsync(requestId, "After reset", "UTC", actor);
        Assert.Equal(EventCreationOutcome.Completed, afterReset.Outcome);
        Assert.Equal(afterReset, await service.CheckAgainAsync(requestId, actor));
        Assert.Equal(afterReset, await service.CreateAsync(requestId, "After reset", "UTC", actor));
        Assert.Equal(afterReset.EventId, (await db.EventCreationOperations.SingleAsync()).EventId);
        Assert.Equal(reset.Scenarios.Count + 1, await db.Events.CountAsync());
    }
}
