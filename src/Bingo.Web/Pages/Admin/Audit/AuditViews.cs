using Bingo.Domain.Auditing;
using Bingo.Web.UI;

namespace Bingo.Web.Pages.Admin.Audit;

/// <summary>One entry as the Audit page shows it (row and drawer).</summary>
public sealed record AuditEntryView(AuditEntry Entry, AuditPresentation Presentation, IndexModel.EventOption? Event)
{
    public bool Automated => Entry.ActorAccountId is null;
    public bool HasEvent => Entry.EventId is not null;
}

/// <summary>Brief 147 item 2: "@website · Playing" for one affected account.</summary>
public static class AuditAccountLine
{
    public static string Of(AuditAffectedAccount account) =>
        string.Join(" · ", new[] { account.Website is { } website ? "@" + website : null, account.Playing }.Where(part => part is not null));
}
