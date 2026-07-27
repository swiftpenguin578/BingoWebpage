using Bingo.Domain.Access;
using Bingo.Domain.Events;

namespace Bingo.Domain.Tests;

public sealed class TimestampNormalizationTests
{
    [Fact]
    public void EventNormalizesCopenhagenOffsetsToUtc()
    {
        var local = new DateTimeOffset(2026, 7, 11, 18, 0, 0, TimeSpan.FromHours(2));
        var item = new BingoEvent(Guid.NewGuid(), "Test", "utc-test", "Test", "Europe/Copenhagen", local, local.AddDays(1), local.AddDays(2), local.AddDays(3), local.AddDays(4), 10, Guid.NewGuid(), local);

        Assert.Equal(TimeSpan.Zero, item.SignupOpensAt!.Value.Offset);
        Assert.Equal(16, item.SignupOpensAt!.Value.Hour);
        Assert.Equal(TimeSpan.Zero, item.CreatedAt.Offset);
    }

    [Fact]
    public void EmergencyAccessLifecycleTimesAreStoredAsUtc()
    {
        var local = new DateTimeOffset(2026, 7, 11, 18, 0, 0, TimeSpan.FromHours(2));
        var access = new AccountEventAccess(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, local, local.AddHours(1), local.AddHours(2));

        Assert.Equal(TimeSpan.Zero, access.ActiveFrom!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, access.ExpiresAt!.Value.Offset);
    }
}
