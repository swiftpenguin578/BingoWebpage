using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bingo.IntegrationTests;

public sealed partial class Slice1IdentityIntegrationTests
{
    [Fact]
    public async Task TypedOwnershipTransferRequiresMatchingDestinationUsernameAndPreservesAtomicity()
    {
        await using var db = new ApplicationDbContext(options);
        var owner = Website("typed-transfer-owner", GlobalRole.SuperAdmin);
        var destination = Website("typed-transfer-destination");
        var other = Website("typed-transfer-other");
        db.AddRange(owner, destination, other);
        await db.SaveChangesAsync();

        var service = new AccountAdministrationService(db, passwords, time);
        var missing = await Assert.ThrowsAsync<AccountActionException>(() => service.TransferOwnershipAsync(owner.Id, "long-test-password", destination.Id, destination.AuthorizationVersion, string.Empty, CancellationToken.None));
        Assert.Contains("destination username", missing.Message, StringComparison.OrdinalIgnoreCase);
        var wrong = await Assert.ThrowsAsync<AccountActionException>(() => service.TransferOwnershipAsync(owner.Id, "long-test-password", destination.Id, destination.AuthorizationVersion, "other-account", CancellationToken.None));
        Assert.Contains("destination username", wrong.Message, StringComparison.OrdinalIgnoreCase);
        var otherAccountName = await Assert.ThrowsAsync<AccountActionException>(() => service.TransferOwnershipAsync(owner.Id, "long-test-password", destination.Id, destination.AuthorizationVersion, other.PublicUsername!, CancellationToken.None));
        Assert.Contains("destination username", otherAccountName.Message, StringComparison.OrdinalIgnoreCase);
        db.ChangeTracker.Clear();
        var rejected = await db.Accounts.Where(x => x.Id == owner.Id || x.Id == destination.Id || x.Id == other.Id).ToListAsync();
        Assert.Equal(GlobalRole.SuperAdmin, rejected.Single(x => x.Id == owner.Id).GlobalRole);
        Assert.Equal(GlobalRole.User, rejected.Single(x => x.Id == destination.Id).GlobalRole);
        Assert.Equal(GlobalRole.User, rejected.Single(x => x.Id == other.Id).GlobalRole);
        Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
        Assert.Equal(GlobalRole.User, destination.GlobalRole);
        Assert.Empty(await db.AuditEntries.ToListAsync());

        await service.TransferOwnershipAsync(owner.Id, "long-test-password", destination.Id, destination.AuthorizationVersion, destination.PublicUsername!.ToUpperInvariant(), CancellationToken.None);

        db.ChangeTracker.Clear();
        var persisted = await db.Accounts.Where(x => x.Id == owner.Id || x.Id == destination.Id).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(GlobalRole.Admin, persisted.Single(x => x.Id == owner.Id).GlobalRole);
        Assert.Equal(GlobalRole.SuperAdmin, persisted.Single(x => x.Id == destination.Id).GlobalRole);
        Assert.Single(persisted, x => x.GlobalRole == GlobalRole.SuperAdmin);
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.Action == "account.ownership_transferred"));
    }

    [Fact]
    public async Task OwnershipTransferHttpRejectsMissingOrMismatchedConfirmationWithoutMutation()
    {
        Guid ownerId;
        Guid destinationId;
        long destinationVersion;
        await using (var seed = new ApplicationDbContext(options))
        {
            var owner = Website("typed-transfer-http-owner", GlobalRole.SuperAdmin);
            var destination = Website("typed-transfer-http-destination");
            ownerId = owner.Id;
            destinationId = destination.Id;
            destinationVersion = destination.AuthorizationVersion;
            seed.AddRange(owner, destination);
            await seed.SaveChangesAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        var loginToken = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "typed-transfer-http-owner",
            ["Input.Password"] = "long-test-password",
            ["__RequestVerificationToken"] = loginToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var transfer = await client.GetStringAsync("/Admin/Accounts/Transfer");
        var transferToken = Regex.Match(transfer, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var rejected = await client.PostAsync("/Admin/Accounts/Transfer", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.DestinationId"] = destinationId.ToString(),
            ["Input.ExpectedAuthorizationVersion"] = destinationVersion.ToString(CultureInfo.InvariantCulture),
            ["Input.CurrentPassword"] = "long-test-password",
            ["__RequestVerificationToken"] = transferToken
        }));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedHtml = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("Destination username confirmation", rejectedHtml, StringComparison.OrdinalIgnoreCase);

        using var serverRejected = await client.PostAsync("/Admin/Accounts/Transfer", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.DestinationId"] = destinationId.ToString(),
            ["Input.ExpectedAuthorizationVersion"] = destinationVersion.ToString(CultureInfo.InvariantCulture),
            ["Input.DestinationUsernameConfirmation"] = "not-the-destination",
            ["Input.CurrentPassword"] = "long-test-password",
            ["__RequestVerificationToken"] = transferToken
        }));
        Assert.Equal(HttpStatusCode.OK, serverRejected.StatusCode);
        var serverRejectedHtml = await serverRejected.Content.ReadAsStringAsync();
        Assert.Contains("destination username confirmation does not match", serverRejectedHtml, StringComparison.OrdinalIgnoreCase);

        await using var verify = new ApplicationDbContext(options);
        var persisted = await verify.Accounts.Where(x => x.Id == ownerId || x.Id == destinationId).ToListAsync();
        Assert.Equal(GlobalRole.SuperAdmin, persisted.Single(x => x.Id == ownerId).GlobalRole);
        Assert.Equal(GlobalRole.User, persisted.Single(x => x.Id == destinationId).GlobalRole);
        Assert.Empty(await verify.AuditEntries.Where(x => x.Action == "account.ownership_transferred").ToListAsync());
    }
}
