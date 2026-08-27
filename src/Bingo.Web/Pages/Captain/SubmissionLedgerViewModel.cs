namespace Bingo.Web.Pages.Captain;

public sealed record SubmissionLedgerViewModel(
    Guid EventId,
    Guid TeamId,
    string LedgerPage,
    string DetailPage,
    IReadOnlyList<IndexModel.SubmissionView> Submissions,
    int PageNumber,
    int TotalPages,
    int TotalSubmissionCount,
    string? Search,
    Guid? PlayerFilter);
