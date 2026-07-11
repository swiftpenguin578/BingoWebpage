using Bingo.Domain.Access;

namespace Bingo.Domain.Tests;

public sealed class AccountAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CaptainMovesFromInactiveToFullToCorrectionOnlyToExpired()
    {
        var account = CreateCaptain(Now.AddHours(1), Now.AddHours(3), Now.AddHours(5));

        Assert.Equal(AccountAccessMode.Disabled, account.GetAccessMode(Now));
        Assert.Equal(AccountAccessMode.Full, account.GetAccessMode(Now.AddHours(2)));
        Assert.Equal(AccountAccessMode.CorrectionOnly, account.GetAccessMode(Now.AddHours(4)));
        Assert.Equal(AccountAccessMode.Disabled, account.GetAccessMode(Now.AddHours(5)));
    }

    [Fact]
    public void ReEnableCanRemoveAutomaticExpiry()
    {
        var account = CreateCaptain(null, null, Now.AddHours(-1));
        Assert.Equal(AccountAccessMode.Disabled, account.GetAccessMode(Now));

        account.Enable(expiresAt: null);

        Assert.Equal(AccountAccessMode.Full, account.GetAccessMode(Now));
    }

    [Fact]
    public void DisabledAdminHasNoAccess()
    {
        var account = new Account(Guid.NewGuid(), "admin", "ADMIN", AccountRole.Admin, Now);
        account.Disable(Now);

        Assert.Equal(AccountAccessMode.Disabled, account.GetAccessMode(Now));
    }

    private static Account CreateCaptain(
        DateTimeOffset? activeFrom,
        DateTimeOffset? correctionOnlyFrom,
        DateTimeOffset? expiresAt)
    {
        var account = new Account(Guid.NewGuid(), "captain", "CAPTAIN", AccountRole.Captain, Now);
        account.ScopeCaptain(Guid.NewGuid(), Guid.NewGuid(), activeFrom, correctionOnlyFrom, expiresAt);
        return account;
    }
}
