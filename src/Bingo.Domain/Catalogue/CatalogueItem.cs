namespace Bingo.Domain.Catalogue;

public sealed class CatalogueItem
{
    private CatalogueItem() { }
    public CatalogueItem(Guid id, string name, string normalizedName) { Id = id; Name = name; NormalizedName = normalizedName; Active = true; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty; public string NormalizedName { get; private set; } = string.Empty; public string? ExternalIdentifier { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool Active { get; private set; }
    public long Version { get; private set; } = 1;
    public string? Notes { get; private set; }
    public void Update(string name, string normalizedName, string? externalId, string? notes, string? imageUrl = null) { Name = name; NormalizedName = normalizedName; ExternalIdentifier = externalId; Notes = notes; ImageUrl = imageUrl; }
    public void SetActive(bool active) => Active = active;
    public void AdvanceVersion() => Version++;
}
