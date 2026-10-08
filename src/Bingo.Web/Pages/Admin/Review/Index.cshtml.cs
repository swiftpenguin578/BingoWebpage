using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Review;

[AdminDesign]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    public BingoEvent? Event { get; private set; }
    public Guid? EventId => Event?.Id;
    public string EventName => Event?.Name ?? string.Empty;
    public string EventTimezone => Event?.Timezone is { Length: > 0 } zone ? zone : DateTimePresentation.DefaultTimezoneId;
    public bool ReviewOpen => Event is { } item && EventStatePolicy.Allows(item.State, EventCapability.ReviewEvidence);
    public string Search { get; private set; } = string.Empty;
    public SubmissionStatus? Status { get; private set; }
    public IReadOnlyList<ReviewList.Row> AllRows { get; private set; } = [];
    public IReadOnlyList<ReviewList.Row> Rows { get; private set; } = [];
    public int Count(SubmissionStatus? status) => status is null ? AllRows.Count : AllRows.Count(x => x.Status == status);
    public bool Missing { get; private set; }
    public bool Filtered => Search.Length > 0 || Status is not null;

    public async Task<IActionResult> OnGetAsync([FromQuery] Guid? eventId, [FromQuery] string? search, [FromQuery] SubmissionStatus? status, CancellationToken ct)
    {
        Search = search?.Trim() ?? string.Empty; Status = status;
        if (eventId is null) return Page();

        // C-CMP-1: a hidden or unknown event is Not Found, never an empty queue or another event.
        Event = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
        if (Event is null) { Missing = true; return new PageResult { StatusCode = StatusCodes.Status404NotFound }; }
        AllRows = await ReviewList.RowsAsync(db, Event, ct);
        Rows = ReviewList.Filter(AllRows, Search, Status);
        return Page();
    }

    public string QueueUrl(string? search, SubmissionStatus? status) => ReviewList.QueueUrl(Event!.Id, search, status);
    public string DetailsUrl(Guid id) => ReviewList.DetailsUrl(id, Event!.Id, Search, Status);
}
