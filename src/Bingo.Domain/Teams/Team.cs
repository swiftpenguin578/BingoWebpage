namespace Bingo.Domain.Teams;

public sealed class Team
{
    private Team() { }
    public Team(Guid id, Guid eventId, string name, string slug, TeamFormationType formationType, string? affiliationName, bool includedInDraft, DateTimeOffset? createdAt = null)
    {
        Id = id; EventId = eventId; Name = name; Slug = slug; FormationType = formationType; AffiliationName = affiliationName; IncludedInDraft = includedInDraft; Active = true; CreatedAt = (createdAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    /// <summary>
    /// Creates a team using the current product model.  FormationType is retained on
    /// the entity only for tolerant reads of older rows; IncludedInDraft is the
    /// authoritative participation flag for all new mutations.
    /// </summary>
    public Team(Guid id, Guid eventId, string name, string slug, string? affiliationName, bool includedInDraft, DateTimeOffset? createdAt = null)
        : this(id, eventId, name, slug, includedInDraft ? TeamFormationType.Drafted : TeamFormationType.Preformed, affiliationName, includedInDraft, createdAt)
    {
    }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    // Retained only as a null compatibility projection. Team images are managed assets.
    public string? ImageUrl => ActiveImageAssetId is null ? null : null;
    public Guid? ActiveImageAssetId { get; private set; }
    public TeamFormationType FormationType { get; private set; }
    public string? AffiliationName { get; private set; }
    public bool IncludedInDraft { get; private set; }
    public int? DraftPosition { get; private set; }
    public bool Active { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? MetadataLockedAt { get; private set; }
    public void Update(string name, string slug, string? affiliationName, string? imageUrl) { Name = name; AffiliationName = affiliationName; }
    public void SetActiveImage(Guid? assetId) => ActiveImageAssetId = assetId;
    public void LockMetadata(DateTimeOffset now) => MetadataLockedAt = now.ToUniversalTime();
    public void AdvanceVersion() => Version++;
    public void SetDraftPosition(int? position) { if (!IncludedInDraft && position is not null) throw new InvalidOperationException("A team excluded from the website draft cannot be placed in draft order."); DraftPosition = position; }
    public void SetIncludedInDraft(bool included)
    {
        IncludedInDraft = included;
        // FormationType is historical/descriptive data. Do not rewrite it when
        // current draft participation changes; callers must use IncludedInDraft.
        if (!included) DraftPosition = null;
    }
    public void Finalize(DateTimeOffset now) => FinalizedAt = now.ToUniversalTime();
    public void SetActive(bool active) => Active = active;
}
