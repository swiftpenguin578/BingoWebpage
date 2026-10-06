using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AccessAndAuditTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_access_tests")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private DbContextOptions<ApplicationDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(_database);
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .Options;
        await using var dbContext = new ApplicationDbContext(_options);
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task DisabledAccountCannotAuthenticate()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = new ApplicationDbContext(_options);
        var account = Account.CreateWebsite(Guid.NewGuid(), "disabled", "DISABLED", now);
        account.SetGlobalRole(GlobalRole.Admin);
        var hasher = new PasswordHasher<Account>();
        account.SetPasswordHash(hasher.HashPassword(account, "long-test-password"), false);
        account.Disable(now);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        var service = new AccountAuthenticationService(dbContext, hasher, TimeProvider.System);

        var result = await service.ValidateCredentialsAsync("disabled", "long-test-password", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CaptainAuthorizationMatchesOnlyItsOwnTeam()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = new ApplicationDbContext(_options);
        var account = Account.CreateEmergency(Guid.NewGuid(), "captain", "CAPTAIN", now); account.Enable();
        dbContext.Accounts.Add(account); await dbContext.SaveChangesAsync();
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new(System.Security.Claims.ClaimTypes.NameIdentifier, account.Id.ToString())], "legacy"));
        var requirements = new IAuthorizationRequirement[] { new AccountAccessRequirement(AccountAccessMode.Full), new TeamScopeRequirement() };
        var handler = new AccountAuthorizationHandler(dbContext, TimeProvider.System);
        var context = new AuthorizationHandlerContext(requirements, principal, new TeamScope(Guid.NewGuid(), Guid.NewGuid()));
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task AuditWriterStoresActorActionAndTimestampWithoutSecrets()
    {
        await using var dbContext = new ApplicationDbContext(_options);
        var writer = new AuditWriter(dbContext, TimeProvider.System);

        await writer.WriteAsync(Guid.NewGuid(), "admin", "account.disabled", "account", Guid.NewGuid().ToString(), "Requested by organizer");

        var entry = await dbContext.AuditEntries.SingleAsync();
        Assert.Equal("admin", entry.ActorUsername);
        Assert.Equal("account.disabled", entry.Action);
        Assert.NotEqual(default, entry.OccurredAt);
        Assert.DoesNotContain("password", entry.Details ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StandaloneAuditRejectsPendingChangesWithoutStagingOrSavingAnything()
    {
        await using var db = new ApplicationDbContext(_options);
        var account = Account.CreateWebsite(Guid.NewGuid(), "unsaved", "UNSAVED", new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
        db.Accounts.Add(account);
        var writer = new AuditWriter(db, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(null, "admin", "account.disabled", "account"));
        Assert.Empty(db.AuditEntries.Local);
        Assert.Equal(EntityState.Added, db.Entry(account).State);
        await using var verify = new ApplicationDbContext(_options);
        Assert.False(await verify.Accounts.AnyAsync(x => x.Id == account.Id));
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task StagedAuditAndMutationFollowTheOwnersTransaction()
    {
        var now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        var account = Account.CreateWebsite(Guid.NewGuid(), "staged", "STAGED", now);
        await using (var db = new ApplicationDbContext(_options))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Accounts.Add(account);
            new AuditWriter(db, TimeProvider.System).Stage(null, "admin", "account.created", "account", account.Id.ToString());
            await using (var beforeSave = new ApplicationDbContext(_options))
            {
                Assert.Empty(await beforeSave.Accounts.ToListAsync());
                Assert.Empty(await beforeSave.AuditEntries.ToListAsync());
            }
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        await using var verify = new ApplicationDbContext(_options);
        Assert.Empty(await verify.Accounts.ToListAsync());
        Assert.Empty(await verify.AuditEntries.ToListAsync());
        verify.Accounts.Add(account);
        await new AuditWriter(verify, TimeProvider.System).WriteAndSaveAsync(null, "admin", "account.created", "account", account.Id.ToString());
        await using var committed = new ApplicationDbContext(_options);
        Assert.Equal(account.Id, (await committed.Accounts.SingleAsync()).Id);
        Assert.Equal(account.Id.ToString(), (await committed.AuditEntries.SingleAsync()).TargetId);
    }

    [Fact]
    public async Task FailedAuditInsertRollsBackTheExplicitlyOwnedMutation()
    {
        var id = Guid.NewGuid();
        await using (var db = new ApplicationDbContext(_options))
        {
            db.Accounts.Add(Account.CreateWebsite(id, "atomic", "ATOMIC", new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero)));
            // PostgreSQL rejects the oversized audit payload after the owning save begins.
            await Assert.ThrowsAsync<DbUpdateException>(() => new AuditWriter(db, TimeProvider.System)
                .WriteAndSaveAsync(null, "admin", "account.created", "account", id.ToString(), new string('x', 4001)));
        }
        await using var verify = new ApplicationDbContext(_options);
        Assert.Empty(await verify.Accounts.ToListAsync());
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task CaptainSecurityMutationAndAuditPersistTogether(bool provision, bool failAudit)
    {
        var now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "organizer", "ORGANIZER", now);
        var item = new BingoEvent(Guid.NewGuid(), "Captain audit fixture", "captain-audit-fixture", "UTC", admin.Id, now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var form = new SignupForm(Guid.NewGuid(), item.Id, now);
        var question = new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), "Captain", "CAPTAIN", now);
        var team = new Team(Guid.NewGuid(), item.Id, "Captain team", "captain-team", TeamFormationType.Preformed, null, false, now);
        var captain = Account.CreateEmergency(Guid.NewGuid(), "captain-login", "CAPTAIN-LOGIN", now);
        captain.Enable();
        await using (var setup = new ApplicationDbContext(_options))
        {
            setup.AddRange(admin, item, form, question, participant, character, team,
                new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, 0, now, null, question.Id, EventCharacterRole.Playing, 1, EhbSource.Manual, null),
                new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Captain, now, null, "Fixture"));
            if (!provision)
                setup.AddRange(captain, new AccountEventAccess(Guid.NewGuid(), captain.Id, item.Id, team.Id, participant.Id, null, null, null));
            await setup.SaveChangesAsync();
            // Only this disposable PostgreSQL fixture rejects these Audit INSERTs.
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_captain_test_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.actor_username = 'audit-failure' THEN
                        RAISE EXCEPTION 'Injected captain audit failure' USING ERRCODE = 'P0001';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER reject_captain_test_audit BEFORE INSERT ON audit_entries
                    FOR EACH ROW EXECUTE FUNCTION reject_captain_test_audit();
                """);
        }
        GeneratedCaptainCredential? credential = null;
        await using (var mutate = new ApplicationDbContext(_options))
        {
            var service = new CaptainAccountProvisioner(mutate, new PasswordHasher<Account>(), new AuditWriter(mutate, TimeProvider.System), TimeProvider.System);
            async Task Mutate()
            {
                var actor = failAudit ? "audit-failure" : "organizer";
                if (provision)
                    credential = await service.ProvisionParticipantAsync(item.Id, team.Id, participant.Id, admin.Id, actor, CancellationToken.None);
                else
                    await service.DisableParticipantAsync(participant.Id, admin.Id, actor, "Captain replaced", CancellationToken.None);
            }
            await Assert.ThrowsAsync<InvalidOperationException>(Mutate);
        }
        await using var verify = new ApplicationDbContext(_options);
        Assert.Empty(await verify.AuditEntries.ToListAsync());
        Assert.Equal(provision ? 1 : 2, await verify.Accounts.CountAsync());
        Assert.Equal(provision ? 0 : 1, await verify.AccountEventAccesses.CountAsync());
        Assert.Empty(await verify.PasswordCredentialTokens.ToListAsync());
        Assert.Null(credential);
        if (!provision) Assert.True((await verify.Accounts.SingleAsync(x => x.Id == captain.Id)).Active);
    }
}
