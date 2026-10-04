namespace Bingo.Domain.Events;

/// <summary>Immutable ranking boundary chosen when an event is created.</summary>
public enum PlacementRule
{
    LegacyScoreTimeThenEhb = 0,
    CreditedEhbThenScoreTime = 1
}
