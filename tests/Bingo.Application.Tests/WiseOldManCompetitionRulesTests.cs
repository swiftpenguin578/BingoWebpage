using Bingo.Application.Integrations.WiseOldMan;

namespace Bingo.Application.Tests;

public sealed class WiseOldManCompetitionRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlayerNormalizationCollapsesProviderAliases()
    {
        Assert.Equal("alice smith", WiseOldManCompetitionRules.NormalizePlayerName("  Alice_Smith  "));
        Assert.Equal("alice smith", WiseOldManCompetitionRules.NormalizePlayerName("Alice-Smith"));
        Assert.Equal("alice smith", WiseOldManCompetitionRules.NormalizePlayerName("Alice\tSmith"));
    }

    [Fact]
    public void EmptyTeamIsDeniedWithAffectedTeam()
    {
        var errors = WiseOldManCompetitionRules.Validate(
            "Autumn Bingo",
            Now.AddDays(1),
            Now.AddDays(2),
            [new WiseOldManCompetitionWriteTeam("Ravens", [])],
            Now);

        Assert.Contains("Cannot create WOM competition with empty teams. Affected team: Ravens.", errors);
    }

    [Fact]
    public void PlausibleNamesAreValidatedLocallyWithoutProviderLookup()
    {
        var errors = WiseOldManCompetitionRules.Validate(
            "Autumn Bingo",
            Now.AddDays(1),
            Now.AddDays(2),
            [new WiseOldManCompetitionWriteTeam("Ravens", ["A player", "new-name"])],
            Now);

        Assert.Empty(errors);
    }

    [Fact]
    public void AliasCollisionAndLocalLimitsAreReported()
    {
        var errors = WiseOldManCompetitionRules.Validate(
            new string('E', 51),
            Now.AddDays(1),
            Now.AddDays(2),
            [
                new WiseOldManCompetitionWriteTeam(new string('T', 31), ["Alice_Smith"]),
                new WiseOldManCompetitionWriteTeam("  Ravens  ", ["Alice-Smith"])
            ],
            Now);

        Assert.Contains(errors, error => error.Contains("between 1 and 50", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("between 1 and 30", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("duplicated after WOM normalization", StringComparison.Ordinal));
    }

    [Fact]
    public void ProviderLimitsCountUnicodeCharactersRatherThanUtf16CodeUnits()
    {
        var errors = WiseOldManCompetitionRules.Validate(
            new string('界', WiseOldManCompetitionRules.MaximumCompetitionTitleLength),
            Now.AddDays(1),
            Now.AddDays(2),
            [new WiseOldManCompetitionWriteTeam(new string('界', WiseOldManCompetitionRules.MaximumTeamNameLength), ["Alice"])],
            Now);

        Assert.DoesNotContain(errors, error => error.Contains("between 1 and 50", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("between 1 and 30", StringComparison.Ordinal));
    }
}
