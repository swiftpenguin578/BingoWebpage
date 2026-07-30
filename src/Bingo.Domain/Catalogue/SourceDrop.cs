namespace Bingo.Domain.Catalogue;

public sealed class SourceDrop
{
    private SourceDrop() { }
    public SourceDrop(Guid id, Guid bossActivityId, Guid itemId, string displayRate, decimal? numericProbability, decimal? defaultEhbEstimate, DateTimeOffset updatedAt) { Id = id; BossActivityId = bossActivityId; ItemId = itemId; DisplayRate = displayRate; NumericProbability = numericProbability; DefaultEhbEstimate = defaultEhbEstimate; DataUpdatedAt = updatedAt.ToUniversalTime(); Active = true; }
    public Guid Id { get; private set; }
    public Guid BossActivityId { get; private set; }
    public Guid ItemId { get; private set; }
    public string DisplayRate { get; private set; } = string.Empty; public decimal? NumericProbability { get; private set; }
    public string? RateConditionNote { get; private set; }
    public decimal? DefaultEhbEstimate { get; private set; }
    public DropProbabilityScope ProbabilityScope { get; private set; } = DropProbabilityScope.Participant;
    public bool ConditionalOnParent { get; private set; }
    public decimal? ParentProbability { get; private set; }
    public int AssumedParticipants { get; private set; } = 1;
    public int RollsPerCompletion { get; private set; } = 1;
    public string RollGroup { get; private set; } = "default";
    public string? DataSource { get; private set; }
    public DateTimeOffset DataUpdatedAt { get; private set; }
    public bool Active { get; private set; }
    public long Version { get; private set; } = 1;
    public void Update(string displayRate, decimal? probability, string? condition, decimal? ehb, string? source, DateTimeOffset now) { DisplayRate = displayRate; NumericProbability = probability; RateConditionNote = condition; DefaultEhbEstimate = ehb; DataSource = source; DataUpdatedAt = now.ToUniversalTime(); }
    public void SetRateMechanics(DropProbabilityScope scope, bool conditionalOnParent, decimal? parentProbability, int assumedParticipants, int rollsPerCompletion, string? rollGroup)
    {
        if (conditionalOnParent && parentProbability is not (> 0 and <= 1)) throw new ArgumentOutOfRangeException(nameof(parentProbability));
        ArgumentOutOfRangeException.ThrowIfLessThan(assumedParticipants, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rollsPerCompletion, 1);
        ProbabilityScope = scope;
        ConditionalOnParent = conditionalOnParent;
        ParentProbability = conditionalOnParent ? parentProbability : null;
        AssumedParticipants = scope == DropProbabilityScope.Team ? assumedParticipants : 1;
        RollsPerCompletion = rollsPerCompletion;
        RollGroup = string.IsNullOrWhiteSpace(rollGroup) ? "default" : rollGroup.Trim();
    }
    public decimal? EffectiveProbabilityPerRoll()
    {
        return NumericProbability is > 0 and <= 1 ? NumericProbability : null;
    }
    public decimal? EffectiveProbabilityPerCompletion() => CalculateProbabilityPerCompletion(EffectiveProbabilityPerRoll(), RollsPerCompletion);
    public static decimal? CalculateProbabilityPerCompletion(decimal? probabilityPerRoll, int rollsPerCompletion)
    {
        if (probabilityPerRoll is not (> 0 and <= 1) || rollsPerCompletion < 1) return null;
        var noDropProbability = 1m;
        for (var roll = 0; roll < rollsPerCompletion; roll++) noDropProbability *= 1 - probabilityPerRoll.Value;
        return 1 - noDropProbability;
    }
    public void ChangeItem(Guid itemId) => ItemId = itemId;
    public void SetActive(bool active) => Active = active;
    public void AdvanceVersion() => Version++;
}
