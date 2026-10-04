using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice1IdentityIntegrationTests
{
    [Fact]
    public async Task TypedOwnershipTransferRequiresMatchingDestinationUsernameAndPreservesAtomicity()
    {
        await using var db = new ApplicationDbContext(options);
        var owner = Website("typed-transfer-owner", GlobalRole.SuperAdmin);
        var destination = Website("typed-transfer-destination");
        db.AddRange(owner, destination);
        await db.SaveChangesAsync();

        var service = new AccountAdministrationService(db, passwords, time);
        var missing = await Assert.ThrowsAsync<AccountActionException>(() => service.TransferOwnershipAsync(owner.Id, "long-test-password", destination.Id, destination.AuthorizationVersion, string.Empty, CancellationToken.None));
        Assert.Contains("destination username", missing.Message, StringComparison.OrdinalIgnoreCase);
        var wrong = await Assert.ThrowsAsync<AccountActionException>(() => service.TransferOwnershipAsync(owner.Id, "long-test-password", destination.Id, destination.AuthorizationVersion, "other-account", CancellationToken.None));
        Assert.Contains("destination username", wrong.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
        Assert.Equal(GlobalRole.User, destination.GlobalRole);
        Assert.Empty(await db.AuditEntries.ToListAsync());

        await service.TransferOwnershipAsync(owner.Id, "long-test-password", destination.Id, destination.AuthorizationVersion, destination.PublicUsername!, CancellationToken.None);

        db.ChangeTracker.Clear();
        var persisted = await db.Accounts.Where(x => x.Id == owner.Id || x.Id == destination.Id).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(GlobalRole.Admin, persisted.Single(x => x.Id == owner.Id).GlobalRole);
        Assert.Equal(GlobalRole.SuperAdmin, persisted.Single(x => x.Id == destination.Id).GlobalRole);
        Assert.Single(persisted, x => x.GlobalRole == GlobalRole.SuperAdmin);
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.Action == "account.ownership_transferred"));
    }
}
