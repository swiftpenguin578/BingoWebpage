using Bingo.Web.UI;

namespace Bingo.Web.Pages.Submissions;

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
    Guid? PlayerFilter,
    string EventTimezone = DateTimePresentation.DefaultTimezoneId);
