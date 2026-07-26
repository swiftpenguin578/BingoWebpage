using Bingo.Application.Access;
using Bingo.Domain.Access;
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
        await _database.StartAsync();
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
        var eventId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var account = Account.CreateEmergency(Guid.NewGuid(), "captain", "CAPTAIN", now);
        dbContext.Accounts.Add(account);
        var access = new AccountEventAccess(Guid.NewGuid(), account.Id, eventId, teamId, null, null, null, now.AddDays(1));
        access.Enable();
        dbContext.AccountEventAccesses.Add(access);
        await dbContext.SaveChangesAsync();
        var principal = new AccountAuthenticationService(
            dbContext,
            new PasswordHasher<Account>(),
            TimeProvider.System).CreatePrincipal(account);
        var requirements = new IAuthorizationRequirement[]
        {
            new AccountAccessRequirement(AccountAccessMode.CorrectionOnly),
            new TeamScopeRequirement()
        };
        var handler = new AccountAuthorizationHandler(dbContext, TimeProvider.System);
        var ownContext = new AuthorizationHandlerContext(requirements, principal, new TeamScope(eventId, teamId));
        var otherContext = new AuthorizationHandlerContext(requirements, principal, new TeamScope(eventId, Guid.NewGuid()));

        await handler.HandleAsync(ownContext);
        await handler.HandleAsync(otherContext);

        Assert.True(ownContext.HasSucceeded);
        Assert.False(otherContext.HasSucceeded);
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
}
