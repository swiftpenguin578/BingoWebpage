using System.Data.Common;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Teams;
using Bingo.Web;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Localization;

namespace Bingo.IntegrationTests;

public sealed partial class Slice1IdentityIntegrationTests
{
    private Account Website(Guid id, string username, GlobalRole role = GlobalRole.User)
    {
        var account = Account.CreateWebsite(id, username, AccountAuthenticationService.NormalizeUsername(username), time.GetUtcNow());
        account.SetPassword(passwords.HashPassword(account, "long-test-password"), false, time.GetUtcNow(), incrementVersion: false);
        account.SetGlobalRole(role);
        return account;
    }

    [Theory]
    [InlineData(GlobalRole.User, GlobalRole.User, false)]
    [InlineData(GlobalRole.Admin, GlobalRole.User, true)]
    [InlineData(GlobalRole.Admin, GlobalRole.Admin, false)]
    [InlineData(GlobalRole.Admin, GlobalRole.SuperAdmin, false)]
    [InlineData(GlobalRole.SuperAdmin, GlobalRole.User, true)]
    [InlineData(GlobalRole.SuperAdmin, GlobalRole.Admin, true)]
    public async Task AccountSupportDisableRestoreEnforcesHierarchyAndInvalidatesBothSessions(GlobalRole actorRole, GlobalRole targetRole, bool permitted)
    {
        await using var db = new ApplicationDbContext(options);
        var actor = Website("support-actor", actorRole);
        var target = Website("support-target", targetRole);
        db.AddRange(actor, target);
        await db.SaveChangesAsync();
        var service = new AccountAdministrationService(db, passwords, time);
        var principal = new AccountAuthenticationService(db, passwords, time).CreatePrincipal(target);
        if (!permitted)
        {
            await Assert.ThrowsAsync<AccountActionException>(() => service.DisableAsync(actor.Id, target.Id, "Support request", target.AuthorizationVersion, CancellationToken.None));
            Assert.True(target.Active);
            target.Disable(time.GetUtcNow());
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<AccountActionException>(() => service.RestoreAsync(actor.Id, target.Id, target.AuthorizationVersion, CancellationToken.None));
            Assert.False(target.Active);
            Assert.Empty(await db.AuditEntries.ToListAsync());
            return;
        }

        await service.DisableAsync(actor.Id, target.Id, "Support request", target.AuthorizationVersion, CancellationToken.None);
        Assert.Null((await ValidateCookieAsync(db, principal)).Principal);
        var disabledPrincipal = new AccountAuthenticationService(db, passwords, time).CreatePrincipal(target);
        await service.RestoreAsync(actor.Id, target.Id, target.AuthorizationVersion, CancellationToken.None);
        Assert.Null((await ValidateCookieAsync(db, disabledPrincipal)).Principal);
        Assert.True(target.Active);
        Assert.Equal(2, await db.AuditEntries.CountAsync());
        Assert.Equal(2, await db.Accounts.CountAsync());
    }

    [Fact]
    public async Task AccountSupportProtectsSelfOwnerAndRoleChangesFromOrdinaryAdmins()
    {
        await using var db = new ApplicationDbContext(options);
        var owner = Website("support-owner", GlobalRole.SuperAdmin);
        var admin = Website("support-admin", GlobalRole.Admin);
        var user = Website("support-user");
        db.AddRange(owner, admin, user);
        await db.SaveChangesAsync();
        var service = new AccountAdministrationService(db, passwords, time);
        await Assert.ThrowsAsync<AccountActionException>(() => service.DisableAsync(owner.Id, owner.Id, "Self", owner.AuthorizationVersion, CancellationToken.None));
        await Assert.ThrowsAsync<AccountActionException>(() => service.RestoreAsync(admin.Id, admin.Id, admin.AuthorizationVersion, CancellationToken.None));
        await Assert.ThrowsAsync<AccountActionException>(() => service.GrantAdminAsync(admin.Id, user.Id, user.AuthorizationVersion, CancellationToken.None));
        await Assert.ThrowsAsync<AccountActionException>(() => service.RevokeAdminAsync(admin.Id, admin.Id, admin.AuthorizationVersion, CancellationToken.None));
        await Assert.ThrowsAsync<AccountActionException>(() => service.RevokeAdminAsync(owner.Id, owner.Id, owner.AuthorizationVersion, CancellationToken.None));
        Assert.Empty(await db.AuditEntries.ToListAsync());
        Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
        Assert.Equal(GlobalRole.User, user.GlobalRole);
    }

    [Fact]
    public async Task AccountSupportTransferRejectsWrongPasswordDisabledAndStaleRecipientWithoutAudit()
    {
        await using var db = new ApplicationDbContext(options);
        var owner = Website("support-transfer-owner", GlobalRole.SuperAdmin);
        var target = Website("support-transfer-target");
        db.AddRange(owner, target);
        await db.SaveChangesAsync();
        var service = new AccountAdministrationService(db, passwords, time);
        var wrongPassword = await Assert.ThrowsAsync<AccountActionException>(() => service.TransferOwnershipAsync(owner.Id, "wrong", target.Id, target.AuthorizationVersion, target.PublicUsername!, CancellationToken.None));
        Assert.Equal("The current password is incorrect.", wrongPassword.Message);
        var previousVersion = target.AuthorizationVersion;
        target.Disable(time.GetUtcNow());
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<StaleAccountChangeException>(() => service.TransferOwnershipAsync(owner.Id, "long-test-password", target.Id, previousVersion, target.PublicUsername!, CancellationToken.None));
        var disabled = await Assert.ThrowsAsync<AccountActionException>(() => service.TransferOwnershipAsync(owner.Id, "long-test-password", target.Id, target.AuthorizationVersion, target.PublicUsername!, CancellationToken.None));
        Assert.Contains("disabled", disabled.Message);
        Assert.Empty(await db.AuditEntries.ToListAsync());
        Assert.Equal(GlobalRole.SuperAdmin, owner.GlobalRole);
    }

    [Fact]
    public async Task AccountSupportReloadsAnActorsRevokedRoleBeforeAuthorizingMutation()
    {
        await using var stale = new ApplicationDbContext(options);
        var owner = Website("support-stale-owner", GlobalRole.SuperAdmin);
        var admin = Website("support-stale-admin", GlobalRole.Admin);
        var target = Website("support-stale-target");
        stale.AddRange(owner, admin, target);
        await stale.SaveChangesAsync();
        await using (var change = new ApplicationDbContext(options))
            await new AccountAdministrationService(change, passwords, time).RevokeAdminAsync(owner.Id, admin.Id, admin.AuthorizationVersion, CancellationToken.None);
        await Assert.ThrowsAsync<AccountActionException>(() => new AccountAdministrationService(stale, passwords, time).DisableAsync(admin.Id, target.Id, "Stale permission", target.AuthorizationVersion, CancellationToken.None));
        Assert.True(target.Active);
    }

    [Fact]
    public async Task AccountSupportResetRaceReloadsActorAfterConcurrentDemotion()
    {
        var owner = Website(Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "reset-race-demotion-owner", GlobalRole.SuperAdmin);
        var actor = Website(Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "reset-race-demotion-actor", GlobalRole.Admin);
        var target = Website(Guid.Parse("00000000-0000-0000-0000-0000000000a3"), "reset-race-demotion-target");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(owner, actor, target);
            await seed.SaveChangesAsync();
        }

        var error = await RunResetRaceAsync(
            actor.Id,
            target.Id,
            administration => administration.RevokeAdminAsync(owner.Id, actor.Id, actor.AuthorizationVersion, CancellationToken.None));

        Assert.IsType<AccountActionException>(error);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(GlobalRole.User, await verify.Accounts.Where(x => x.Id == actor.Id).Select(x => x.GlobalRole).SingleAsync());
        Assert.Empty(await verify.PasswordCredentialTokens.Where(x => x.AccountId == target.Id).ToListAsync());
    }

    [Fact]
    public async Task AccountSupportResetRaceReloadsTargetAfterConcurrentPromotion()
    {
        var owner = Website(Guid.Parse("00000000-0000-0000-0000-0000000000b1"), "reset-race-promotion-owner", GlobalRole.SuperAdmin);
        var actor = Website(Guid.Parse("00000000-0000-0000-0000-0000000000b2"), "reset-race-promotion-actor", GlobalRole.Admin);
        var target = Website(Guid.Parse("00000000-0000-0000-0000-0000000000b3"), "reset-race-promotion-target");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(owner, actor, target);
            await seed.SaveChangesAsync();
        }

        var error = await RunResetRaceAsync(
            actor.Id,
            target.Id,
            administration => administration.GrantAdminAsync(owner.Id, target.Id, target.AuthorizationVersion, CancellationToken.None));

        Assert.IsType<AccountActionException>(error);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(GlobalRole.Admin, await verify.Accounts.Where(x => x.Id == target.Id).Select(x => x.GlobalRole).SingleAsync());
        Assert.Empty(await verify.PasswordCredentialTokens.Where(x => x.AccountId == target.Id).ToListAsync());
    }

    [Fact]
    public async Task AccountSupportResetRaceReloadsTargetAfterConcurrentDisable()
    {
        var owner = Website(Guid.Parse("00000000-0000-0000-0000-0000000000c1"), "reset-race-disable-owner", GlobalRole.SuperAdmin);
        var actor = Website(Guid.Parse("00000000-0000-0000-0000-0000000000c2"), "reset-race-disable-actor", GlobalRole.Admin);
        var target = Website(Guid.Parse("00000000-0000-0000-0000-0000000000c3"), "reset-race-disable-target");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(owner, actor, target);
            await seed.SaveChangesAsync();
        }

        var error = await RunResetRaceAsync(
            actor.Id,
            target.Id,
            administration => administration.DisableAsync(owner.Id, target.Id, "Concurrent policy disable", target.AuthorizationVersion, CancellationToken.None));

        Assert.IsType<InvalidOperationException>(error);
        await using var verify = new ApplicationDbContext(options);
        var savedTarget = await verify.Accounts.SingleAsync(x => x.Id == target.Id);
        Assert.False(savedTarget.Active);
        Assert.Equal("Concurrent policy disable", savedTarget.DisabledReason);
        Assert.Empty(await verify.PasswordCredentialTokens.Where(x => x.AccountId == target.Id).ToListAsync());
    }

    [Fact]
    public async Task ResetConsumptionRechecksRecipientAndIssuerAuthorityAndSupersedesRetiredRecipients()
    {
        var owner = Website(Guid.NewGuid(), $"reset-policy-owner-{Guid.NewGuid():N}", GlobalRole.SuperAdmin);
        var grantTarget = Website(Guid.NewGuid(), $"reset-policy-grant-{Guid.NewGuid():N}");
        var disabledTarget = Website(Guid.NewGuid(), $"reset-policy-disabled-{Guid.NewGuid():N}");
        var transferTarget = Website(Guid.NewGuid(), $"reset-policy-transfer-{Guid.NewGuid():N}");
        var revokedIssuer = Website(Guid.NewGuid(), $"reset-policy-revoked-issuer-{Guid.NewGuid():N}", GlobalRole.Admin);
        var disabledIssuer = Website(Guid.NewGuid(), $"reset-policy-disabled-issuer-{Guid.NewGuid():N}", GlobalRole.Admin);
        var issuerTarget = Website(Guid.NewGuid(), $"reset-policy-issuer-target-{Guid.NewGuid():N}");
        var disabledIssuerTarget = Website(Guid.NewGuid(), $"reset-policy-disabled-target-{Guid.NewGuid():N}");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(owner, grantTarget, disabledTarget, transferTarget, revokedIssuer, disabledIssuer, issuerTarget, disabledIssuerTarget);
            await seed.SaveChangesAsync();
        }

        var grantToken = await GenerateResetForTestAsync(owner.Id, grantTarget.Id);
        await MutateAccountForTestAsync(service => service.GrantAdminAsync(owner.Id, grantTarget.Id, grantTarget.AuthorizationVersion, CancellationToken.None));
        await AssertResetRejectedForTestAsync(grantToken, grantTarget.Id, superseded: true);

        var disabledToken = await GenerateResetForTestAsync(owner.Id, disabledTarget.Id);
        await MutateAccountForTestAsync(service => service.DisableAsync(owner.Id, disabledTarget.Id, "Policy test disable", disabledTarget.AuthorizationVersion, CancellationToken.None));
        await AssertResetRejectedForTestAsync(disabledToken, disabledTarget.Id, superseded: true);

        var revokedIssuerToken = await GenerateResetForTestAsync(revokedIssuer.Id, issuerTarget.Id);
        await MutateAccountForTestAsync(service => service.RevokeAdminAsync(owner.Id, revokedIssuer.Id, revokedIssuer.AuthorizationVersion, CancellationToken.None));
        await AssertResetRejectedForTestAsync(revokedIssuerToken, issuerTarget.Id, superseded: false);

        var disabledIssuerToken = await GenerateResetForTestAsync(disabledIssuer.Id, disabledIssuerTarget.Id);
        await MutateAccountForTestAsync(service => service.DisableAsync(owner.Id, disabledIssuer.Id, "Issuer policy test disable", disabledIssuer.AuthorizationVersion, CancellationToken.None));
        await AssertResetRejectedForTestAsync(disabledIssuerToken, disabledIssuerTarget.Id, superseded: false);

        var transferToken = await GenerateResetForTestAsync(owner.Id, transferTarget.Id);
        await MutateAccountForTestAsync(service => service.TransferOwnershipAsync(owner.Id, "long-test-password", transferTarget.Id, transferTarget.AuthorizationVersion, transferTarget.PublicUsername!, CancellationToken.None));
        await AssertResetRejectedForTestAsync(transferToken, transferTarget.Id, superseded: true);
    }

    [Fact]
    public async Task ResetConsumptionStillSucceedsWhenIssuerAndRecipientRemainAuthorized()
    {
        var issuer = Website(Guid.NewGuid(), $"reset-policy-normal-issuer-{Guid.NewGuid():N}", GlobalRole.Admin);
        var target = Website(Guid.NewGuid(), $"reset-policy-normal-target-{Guid.NewGuid():N}");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(issuer, target);
            await seed.SaveChangesAsync();
        }

        var token = await GenerateResetForTestAsync(issuer.Id, target.Id);
        await using (var consume = new ApplicationDbContext(options))
        {
            await new AccountIdentityService(consume, new PasswordHasher<Account>(), time).ConsumeResetAsync(token, "normal-reset-password", CancellationToken.None);
        }
        await using var verify = new ApplicationDbContext(options);
        var savedToken = await verify.PasswordCredentialTokens.SingleAsync(item => item.TokenHash == AccountIdentityService.Hash(token));
        Assert.NotNull(savedToken.UsedAt);
        Assert.Null(savedToken.SupersededAt);
        Assert.Single(await verify.AuditEntries.Where(item => item.Action == "account.password_reset").ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetConsumptionAndRecipientPromotionSerializeInEitherLockOrder(bool promotionWins)
    {
        var owner = Website(Guid.NewGuid(), $"reset-race-recipient-owner-{Guid.NewGuid():N}", GlobalRole.SuperAdmin);
        var target = Website(Guid.NewGuid(), $"reset-race-recipient-target-{Guid.NewGuid():N}");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(owner, target);
            await seed.SaveChangesAsync();
        }
        var token = await GenerateResetForTestAsync(owner.Id, target.Id);
        var error = await RunResetConsumeRaceAsync(
            token,
            promotionWins,
            administration => administration.GrantAdminAsync(owner.Id, target.Id, target.AuthorizationVersion, CancellationToken.None));

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(GlobalRole.Admin, await verify.Accounts.Where(item => item.Id == target.Id).Select(item => item.GlobalRole).SingleAsync());
        var savedToken = await verify.PasswordCredentialTokens.SingleAsync(item => item.TokenHash == AccountIdentityService.Hash(token));
        if (promotionWins)
        {
            Assert.IsType<InvalidOperationException>(error);
            Assert.Null(savedToken.UsedAt);
            Assert.NotNull(savedToken.SupersededAt);
            Assert.Empty(await verify.AuditEntries.Where(item => item.Action == "account.password_reset").ToListAsync());
        }
        else
        {
            Assert.Null(error);
            Assert.NotNull(savedToken.UsedAt);
            Assert.Null(savedToken.SupersededAt);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetConsumptionAndIssuerRevocationSerializeInEitherLockOrder(bool revocationWins)
    {
        var owner = Website(Guid.NewGuid(), $"reset-race-issuer-owner-{Guid.NewGuid():N}", GlobalRole.SuperAdmin);
        var issuer = Website(Guid.NewGuid(), $"reset-race-issuer-admin-{Guid.NewGuid():N}", GlobalRole.Admin);
        var target = Website(Guid.NewGuid(), $"reset-race-issuer-target-{Guid.NewGuid():N}");
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(owner, issuer, target);
            await seed.SaveChangesAsync();
        }
        var token = await GenerateResetForTestAsync(issuer.Id, target.Id);
        var error = await RunResetConsumeRaceAsync(
            token,
            revocationWins,
            administration => administration.RevokeAdminAsync(owner.Id, issuer.Id, issuer.AuthorizationVersion, CancellationToken.None));

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(GlobalRole.User, await verify.Accounts.Where(item => item.Id == issuer.Id).Select(item => item.GlobalRole).SingleAsync());
        var savedToken = await verify.PasswordCredentialTokens.SingleAsync(item => item.TokenHash == AccountIdentityService.Hash(token));
        if (revocationWins)
        {
            Assert.IsType<InvalidOperationException>(error);
            Assert.Null(savedToken.UsedAt);
            Assert.Empty(await verify.AuditEntries.Where(item => item.Action == "account.password_reset").ToListAsync());
        }
        else
        {
            Assert.Null(error);
            Assert.NotNull(savedToken.UsedAt);
            Assert.Null(savedToken.SupersededAt);
        }
    }

    [Fact]
    public async Task AccountSupportDisableReasonSurvivesRestoreAuditAndManageProjection()
    {
        var actor = Website(Guid.Parse("00000000-0000-0000-0000-0000000000d1"), "disable-history-actor", GlobalRole.Admin);
        var target = Website(Guid.Parse("00000000-0000-0000-0000-0000000000d2"), "disable-history-target");
        await using var db = new ApplicationDbContext(options);
        db.AddRange(actor, target);
        await db.SaveChangesAsync();

        const string reason = "Verified duplicate account after support review.";
        var administration = new AccountAdministrationService(db, passwords, time);
        await administration.DisableAsync(actor.Id, target.Id, $"  {reason}  ", target.AuthorizationVersion, CancellationToken.None);
        var disabledAudit = await db.AuditEntries.SingleAsync(x => x.Action == "account.disabled" && x.TargetId == target.Id.ToString());
        Assert.Equal(reason, disabledAudit.Details);
        Assert.Equal(reason, AuditPresenter.Present(disabledAudit, new AuditPassthroughLocalizer()).Reason);

        await administration.RestoreAsync(actor.Id, target.Id, target.AuthorizationVersion, CancellationToken.None);
        db.ChangeTracker.Clear();

        var page = new Bingo.Web.Pages.Admin.Accounts.ManageModel(
            db,
            administration,
            new AccountIdentityService(db, passwords, time))
        {
            PageContext = new PageContext(new ActionContext(new DefaultHttpContext(), new RouteData(), new PageActionDescriptor()))
        };
        page.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(page.HttpContext, new DictionaryTempDataProvider());
        Assert.IsType<PageResult>(await page.OnGetAsync(target.Id, CancellationToken.None));
        var disabledHistory = Assert.Single(page.AccountView!.DisableHistory, entry => entry.State == "Disabled");
        Assert.Equal(reason, disabledHistory.Reason);
        Assert.Equal(actor.LoginName, disabledHistory.ActorName);
        Assert.Equal(disabledAudit.OccurredAt, disabledHistory.OccurredAt);
        Assert.Contains(page.AccountView.DisableHistory, entry => entry.State == "Restored" && entry.ActorName == actor.LoginName);

        var savedTarget = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == target.Id);
        Assert.True(savedTarget.Active);
        Assert.Null(savedTarget.DisabledReason);
        Assert.Equal(reason, await db.AuditEntries.Where(x => x.Id == disabledAudit.Id).Select(x => x.Details).SingleAsync());
    }

    private async Task<string> GenerateResetForTestAsync(Guid issuerId, Guid targetId)
    {
        await using var context = new ApplicationDbContext(options);
        return await new AccountIdentityService(context, new PasswordHasher<Account>(), time).GenerateResetLinkAsync(issuerId, targetId, CancellationToken.None);
    }

    private async Task MutateAccountForTestAsync(Func<AccountAdministrationService, Task> mutation)
    {
        await using var context = new ApplicationDbContext(options);
        await mutation(new AccountAdministrationService(context, passwords, time));
    }

    private async Task AssertResetRejectedForTestAsync(string rawToken, Guid targetId, bool superseded)
    {
        await using (var attempt = new ApplicationDbContext(options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new AccountIdentityService(attempt, new PasswordHasher<Account>(), time).ConsumeResetAsync(rawToken, "rejected-reset-password", CancellationToken.None));

        await using var verify = new ApplicationDbContext(options);
        var token = await verify.PasswordCredentialTokens.SingleAsync(item => item.TokenHash == AccountIdentityService.Hash(rawToken));
        Assert.Null(token.UsedAt);
        Assert.Equal(superseded, token.SupersededAt is not null);
        Assert.Empty(await verify.AuditEntries.Where(item => item.Action == "account.password_reset" && item.TargetId == targetId.ToString()).ToListAsync());
    }

    private async Task<Exception?> RunResetConsumeRaceAsync(
        string rawToken,
        bool mutationWins,
        Func<AccountAdministrationService, Task> mutation)
    {
        if (mutationWins)
        {
            var mutationBarrier = new AccountPairLockBarrier();
            var consumeProbe = new AccountPairReadProbe();
            var mutationOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(mutationBarrier).Options;
            var consumeOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(consumeProbe).Options;
            Task mutationTask = MutateAsync(mutationOptions);
            Task<Exception?>? consumeTask = null;
            try
            {
                await mutationBarrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
                consumeTask = ConsumeAsync(consumeOptions);
                await consumeProbe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
                mutationBarrier.Release.TrySetResult();
                await mutationTask;
                return await consumeTask;
            }
            finally
            {
                mutationBarrier.Release.TrySetResult();
                if (consumeTask is not null) await Record.ExceptionAsync(async () => await consumeTask);
                await Record.ExceptionAsync(async () => await mutationTask);
            }
        }

        var consumeBarrier = new AccountPairLockBarrier();
        var mutationProbe = new AccountPairReadProbe();
        var consumeContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(consumeBarrier).Options;
        var mutationContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(mutationProbe).Options;
        Task<Exception?> consumeOperation = ConsumeAsync(consumeContextOptions);
        Task? mutationOperation = null;
        try
        {
            await consumeBarrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            mutationOperation = MutateAsync(mutationContextOptions);
            await mutationProbe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            consumeBarrier.Release.TrySetResult();
            var error = await consumeOperation;
            await mutationOperation;
            return error;
        }
        finally
        {
            consumeBarrier.Release.TrySetResult();
            await Record.ExceptionAsync(async () => await consumeOperation);
            if (mutationOperation is not null) await Record.ExceptionAsync(async () => await mutationOperation);
        }

        async Task MutateAsync(DbContextOptions<ApplicationDbContext> contextOptions)
        {
            await using var context = new ApplicationDbContext(contextOptions);
            await mutation(new AccountAdministrationService(context, passwords, time));
        }

        async Task<Exception?> ConsumeAsync(DbContextOptions<ApplicationDbContext> contextOptions)
        {
            await using var context = new ApplicationDbContext(contextOptions);
            try
            {
                await new AccountIdentityService(context, new PasswordHasher<Account>(), time).ConsumeResetAsync(rawToken, "race-reset-password", CancellationToken.None);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }
    }

    private async Task<Exception> RunResetRaceAsync(
        Guid resetActorId,
        Guid targetId,
        Func<AccountAdministrationService, Task> mutation)
    {
        var mutationBarrier = new AccountPairLockBarrier();
        var resetProbe = new AccountPairReadProbe();
        var mutationOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(mutationBarrier).Options;
        var resetOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(resetProbe).Options;
        var mutationTask = MutateAsync();
        Task<string>? resetTask = null;
        try
        {
            await mutationBarrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            resetTask = ResetAsync();
            await resetProbe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            mutationBarrier.Release.TrySetResult();
            await mutationTask;
            var error = await Record.ExceptionAsync(async () => await resetTask);
            if (error is null) throw new Xunit.Sdk.XunitException("The reset link was issued after a concurrent account mutation.");
            return error;
        }
        finally
        {
            mutationBarrier.Release.TrySetResult();
            if (resetTask is not null) await Record.ExceptionAsync(async () => await resetTask);
            await Record.ExceptionAsync(async () => await mutationTask);
        }

        async Task MutateAsync()
        {
            await using var context = new ApplicationDbContext(mutationOptions);
            await mutation(new AccountAdministrationService(context, passwords, time));
        }

        async Task<string> ResetAsync()
        {
            await using var context = new ApplicationDbContext(resetOptions);
            return await new AccountIdentityService(context, passwords, time).GenerateResetLinkAsync(resetActorId, targetId, CancellationToken.None);
        }
    }

    private sealed class AccountPairLockBarrier : DbCommandInterceptor
    {
        private int entered;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (IsAccountPairLock(command) && Interlocked.CompareExchange(ref entered, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class AccountPairReadProbe : DbCommandInterceptor
    {
        private int entered;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (IsAccountPairLock(command) && Interlocked.CompareExchange(ref entered, 1, 0) == 0) Reached.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private static bool IsAccountPairLock(DbCommand command) => command.CommandText.Contains("FROM accounts", StringComparison.OrdinalIgnoreCase)
        && command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase);

    private sealed class AuditPassthroughLocalizer : IStringLocalizer<AuditResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    [Fact]
    public async Task AccountSupportResetSupersedesExpiresAtSixtyMinutesAndNeverAuditsSecrets()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero));
        await using var db = new ApplicationDbContext(options);
        var owner = Website("support-reset-owner", GlobalRole.SuperAdmin);
        var target = Website("support-reset-target");
        db.AddRange(owner, target);
        await db.SaveChangesAsync();
        var service = new AccountIdentityService(db, passwords, clock);
        var old = await service.GenerateResetLinkAsync(owner.Id, target.Id, CancellationToken.None);
        var current = await service.GenerateResetLinkAsync(owner.Id, target.Id, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConsumeResetAsync(old, "new-support-password", CancellationToken.None));
        db.ChangeTracker.Clear();
        var token = await db.PasswordCredentialTokens.SingleAsync(x => x.TokenHash == AccountIdentityService.Hash(current));
        Assert.Equal(clock.GetUtcNow().AddMinutes(60), token.ExpiresAt);
        clock.Set(token.ExpiresAt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConsumeResetAsync(current, "new-support-password", CancellationToken.None));
        foreach (var audit in await db.AuditEntries.ToListAsync())
        {
            var serialized = System.Text.Json.JsonSerializer.Serialize(audit);
            Assert.DoesNotContain(old, serialized);
            Assert.DoesNotContain(current, serialized);
            Assert.DoesNotContain(AccountIdentityService.Hash(current), serialized);
        }
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.Action == "account.reset_link_created"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccountSupportRestorePreservesExpiredMembershipWithoutRestoringAuthority(bool membershipExpired)
    {
        var now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        await using var db = new ApplicationDbContext(options);
        var admin = Website("support-restore-admin", GlobalRole.Admin);
        var target = Website("support-restore-user");
        target.Disable(now.AddHours(-1));
        var item = Event(now.AddHours(-3), now.AddHours(-1));
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now.AddHours(-4));
        item.EndEvent(now.AddHours(-2));
        var team = new Team(Guid.NewGuid(), item.Id, "Support team", "support-team", TeamFormationType.Drafted, null, true);
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.Website);
        participant.AssignOwner(target);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Captain, now.AddHours(-2), null, "Fixture");
        if (membershipExpired) membership.Leave(now.AddMinutes(-30), "Expired membership");
        db.AddRange(admin, target, item, team, participant, membership);
        await db.SaveChangesAsync();
        await new AccountAdministrationService(db, passwords, new MutableTimeProvider(now)).RestoreAsync(admin.Id, target.Id, target.AuthorizationVersion, CancellationToken.None);
        Assert.True(target.Active);
        var authorization = new AuthorizationHandlerContext([new AccountAccessRequirement(AccountAccessMode.Full)], new AccountAuthenticationService(db, passwords, time).CreatePrincipal(target), new TeamScope(item.Id, team.Id));
        await new AccountAuthorizationHandler(db, new MutableTimeProvider(now)).HandleAsync(authorization);
        Assert.False(authorization.HasSucceeded);
        Assert.Equal(membershipExpired ? now.AddMinutes(-30) : (DateTimeOffset?)null, (await db.TeamMemberships.AsNoTracking().SingleAsync()).LeftAt);
        Assert.Equal(target.Id, (await db.EventParticipants.AsNoTracking().SingleAsync()).AccountId);
    }
}
