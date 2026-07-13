namespace Bingo.Domain.Boards;

public sealed class TemplateRequirementDrop { private TemplateRequirementDrop() { } public TemplateRequirementDrop(Guid id, Guid requirementId, Guid sourceDropId, int? maximumContribution) { Id = id; RequirementId = requirementId; SourceDropId = sourceDropId; MaximumContribution = maximumContribution; } public Guid Id { get; private set; } public Guid RequirementId { get; private set; } public Guid SourceDropId { get; private set; } public int? MaximumContribution { get; private set; } }
