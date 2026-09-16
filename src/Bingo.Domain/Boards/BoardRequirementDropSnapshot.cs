using System.ComponentModel.DataAnnotations.Schema;
using Bingo.Domain.Catalogue;

namespace Bingo.Domain.Boards;

public sealed class BoardRequirementDropSnapshot
{
    private BoardRequirementDropSnapshot() { }
    public BoardRequirementDropSnapshot(Guid id, Guid requirementId, Guid sourceDropId, Guid itemIdSnapshot, string boss, string item, string displayRate, decimal? probability, int? maximum, decimal? ehb, int creditedWeight = 1, DropProbabilityScope probabilityScope = DropProbabilityScope.Participant, bool conditionalOnParent = false, decimal? parentProbability = null, int assumedParticipants = 1, int rollsPerCompletion = 1, string rollGroup = "default", string? rateCondition = null) { ArgumentOutOfRangeException.ThrowIfLessThan(creditedWeight, 1); Id = id; RequirementId = requirementId; SourceDropId = sourceDropId; ItemIdSnapshot = itemIdSnapshot; BossName = boss; ItemName = item; DisplayRate = displayRate; NumericProbability = probability; MaximumContribution = maximum; EhbPerContribution = ehb; CreditedWeight = creditedWeight; ProbabilityScope = probabilityScope; ConditionalOnParent = conditionalOnParent; ParentProbability = parentProbability; AssumedParticipants = assumedParticipants; RollsPerCompletion = rollsPerCompletion; RollGroup = rollGroup; RateCondition = rateCondition; }
    public Guid Id { get; private set; }
    public Guid RequirementId { get; private set; }
    public Guid SourceDropId { get; private set; }
    public Guid ItemIdSnapshot { get; private set; }
    public string BossName { get; private set; } = string.Empty; public string ItemName { get; private set; } = string.Empty; public string DisplayRate { get; private set; } = string.Empty; public decimal? NumericProbability { get; private set; }
    public int? MaximumContribution { get; private set; }
    public decimal? EhbPerContribution { get; private set; }
    public int CreditedWeight { get; private set; } = 1; // These fields are populated only by the immutable approval projection; draft rows do not own them.
    [NotMapped] public DropProbabilityScope ProbabilityScope { get; private set; }
    [NotMapped] public bool ConditionalOnParent { get; private set; }
    [NotMapped] public decimal? ParentProbability { get; private set; }
    [NotMapped] public int AssumedParticipants { get; private set; } = 1;
    [NotMapped] public int RollsPerCompletion { get; private set; } = 1;
    [NotMapped] public string RollGroup { get; private set; } = "default";
    [NotMapped] public string? RateCondition { get; private set; }
}
