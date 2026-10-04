using Bingo.Application.Events;

namespace Bingo.Application.Tests;

public sealed class Au20StoredOutcomeContractTests
{
    [Fact]
    public void Au18StoredEnumNamesAndNumbersRemainStableAndAu20OnlyAppends()
    {
        var original = new[] { "EventUnavailable", "EventNotInFinalReview", "IncompleteEventWindow", "NoCompetition", "RefreshInProgress", "RetryDelay", "NotDue", "ServiceUnavailable" };
        for (var number = 0; number < original.Length; number++)
            Assert.Equal(original[number], ((EventCompetitionRefreshSkipReason)number).ToString());
        Assert.Equal(8, (int)EventCompetitionRefreshSkipReason.EndWindowUnmatched);
        Assert.Equal(9, (int)EventCompetitionRefreshSkipReason.EndCouldNotBeUpdated);
    }
}
