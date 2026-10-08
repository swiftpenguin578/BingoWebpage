namespace Bingo.Domain.Teams;

/// <summary>
/// Describes how an immutable roster publication was assembled.
/// Historical rows are deliberately explicit when the retained records do not
/// prove whether a website draft occurred.
/// </summary>
public enum DraftPublicationMethod
{
    HistoricalUnknown = 1,
    WebsiteDraft = 2,
    DirectRoster = 3
}
