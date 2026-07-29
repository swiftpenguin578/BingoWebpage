using Bingo.Domain.Access;

namespace Bingo.Domain.Tests;

public sealed class AccountAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmergencyAccessIsFullOnlyWhileEnabledAndItsEventWindowControlsMutations()
    {
        var access = CreateAccess(Now.AddHours(1), Now.AddHours(3), Now.AddHours(5));
        access.Enable();

        Assert.Equal(AccountAccessMode.Disabled, access.GetAccessMode(Now));
        Assert.Equal(AccountAccessMode.Full, access.GetAccessMode(Now.AddHours(2)));
        Assert.Equal(AccountAccessMode.Full, access.GetAccessMode(Now.AddHours(4)));
        Assert.Equal(AccountAccessMode.Disabled, access.GetAccessMode(Now.AddHours(5)));
    }

    private static AccountEventAccess CreateAccess(DateTimeOffset? activeFrom, DateTimeOffset? correctionOnlyFrom, DateTimeOffset? expiresAt) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, activeFrom, correctionOnlyFrom, expiresAt);
}
