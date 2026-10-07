using Bingo.Domain.Auditing;
using Bingo.Web.UI;

namespace Bingo.Web.Pages.Admin.Audit;

/// <summary>One entry as the Audit page shows it (row and drawer).</summary>
public sealed record AuditEntryView(AuditEntry Entry, AuditPresentation Presentation, IndexModel.EventOption? Event)
{
    public bool Automated => Entry.ActorAccountId is null;
    public bool HasEvent => Entry.EventId is not null;
}
