namespace Bingo.Domain.Teams;

public sealed class Team
{
    private Team() { }
    public Team(Guid id, Guid eventId, string name, string slug, TeamFormationType formationType, string? affiliationName, bool includedInDraft)
    {
        Id = id; EventId = eventId; Name = name; Slug = slug; FormationType = formationType; AffiliationName = affiliationName; IncludedInDraft = includedInDraft; Active = true;
        if (formationType == TeamFormationType.Preformed && includedInDraft) throw new InvalidOperationException("Pre-formed teams cannot receive draft turns.");
    }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public TeamFormationType FormationType { get; private set; }
    public string? AffiliationName { get; private set; }
    public bool IncludedInDraft { get; private set; }
    public int? DraftPosition { get; private set; }
    public bool Active { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public void Update(string name, string slug, string? affiliationName, string? imageUrl) { Name = name; Slug = slug; AffiliationName = affiliationName; ImageUrl = imageUrl; }
    public void SetDraftPosition(int? position) { if (!IncludedInDraft && position is not null) throw new InvalidOperationException("A pre-formed team cannot be placed in draft order."); DraftPosition = position; }
    public void Finalize(DateTimeOffset now) => FinalizedAt = now.ToUniversalTime();
    public void SetActive(bool active) => Active = active;
}
