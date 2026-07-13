namespace Bingo.Domain.Evidence;

public sealed class ReviewAction
{
    private ReviewAction() { }
    public ReviewAction(Guid id, Guid submissionId, ReviewActionType action, Guid accountId, DateTimeOffset performedAt, string? note, string? before, string? after)
    { Id = id; SubmissionId = submissionId; Action = action; PerformedByAccountId = accountId; PerformedAt = performedAt.ToUniversalTime(); Note = Clean(note); BeforeSnapshot = before; AfterSnapshot = after; }
    public Guid Id { get; private set; }
    public Guid SubmissionId { get; private set; }
    public ReviewActionType Action { get; private set; }
    public Guid PerformedByAccountId { get; private set; }
    public DateTimeOffset PerformedAt { get; private set; }
    public string? Note { get; private set; }
    public string? BeforeSnapshot { get; private set; }
    public string? AfterSnapshot { get; private set; }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
