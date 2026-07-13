namespace Bingo.Domain.Boards;

public sealed class TemplateRequirementBoss { private TemplateRequirementBoss() { } public TemplateRequirementBoss(Guid id, Guid requirementId, Guid bossActivityId) { Id = id; RequirementId = requirementId; BossActivityId = bossActivityId; } public Guid Id { get; private set; } public Guid RequirementId { get; private set; } public Guid BossActivityId { get; private set; } }
