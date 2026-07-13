namespace Bingo.Domain.Boards;

public sealed class BoardRequirementBossSnapshot { private BoardRequirementBossSnapshot() { } public BoardRequirementBossSnapshot(Guid id, Guid requirementId, Guid bossId, string name, decimal? rate) { Id = id; RequirementId = requirementId; BossActivityId = bossId; BossName = name; EfficientRate = rate; } public Guid Id { get; private set; } public Guid RequirementId { get; private set; } public Guid BossActivityId { get; private set; } public string BossName { get; private set; } = string.Empty; public decimal? EfficientRate { get; private set; } }
