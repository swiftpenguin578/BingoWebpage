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
    public long? CatalogueValueGp { get; private set; }
    public CataloguePriceSource PriceSource { get; private set; }
    public DateTimeOffset? PriceObservedAt { get; private set; }
    public long? RejectedPriceGp { get; private set; }
    public DateTimeOffset? RejectedPriceObservedAt { get; private set; }
    public ApiMappingStatus MappingStatus { get; private set; }
    public DateTimeOffset? MappingCheckedAt { get; private set; }
    public string? MatchedApiName { get; private set; }
    public string? MatchedApiIcon { get; private set; }

    public decimal? ArtworkX { get; private set; }
    public decimal? ArtworkY { get; private set; }
    public decimal? ArtworkWidth { get; private set; }
    public decimal? ArtworkHeight { get; private set; }
    public decimal? ArtworkScale { get; private set; }
    public decimal? ArtworkRotation { get; private set; }

    // The approved editor's ranges, including its measured default-fit dimensions.
    public void SetArtwork(decimal x, decimal y, decimal width, decimal height, decimal scale, decimal rotation)
    {
        if (x is < 0 or > 100 || y is < 0 or > 100 || width is < 5 or > 150 || height is < 5 or > 200
            || scale is < .5m or > 2.5m || rotation is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(x), "Artwork settings are outside the supported range.");
        ArtworkX = x; ArtworkY = y; ArtworkWidth = width; ArtworkHeight = height;
        ArtworkScale = scale; ArtworkRotation = rotation;
    }
    public void ResetArtwork()
    {
        ArtworkX = ArtworkY = ArtworkWidth = ArtworkHeight = ArtworkScale = ArtworkRotation = null;
    }

    public void ConfigureApi(string? identifier)
    {
        identifier = string.IsNullOrWhiteSpace(identifier) ? null : identifier.Trim();
        if (int.TryParse(identifier, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var itemId) && itemId > 0)
            identifier = itemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (identifier == ExternalIdentifier) return;
        ExternalIdentifier = identifier;
        MappingStatus = ApiMappingStatus.NotConfigured; MappingCheckedAt = null;
        MatchedApiName = null; MatchedApiIcon = null;
        RestorePriceRejection(null, null);
        if (PriceSource == CataloguePriceSource.Api) SetPrice(null, CataloguePriceSource.Missing, null);
    }
    public void RecordMapping(ApiMappingStatus status, DateTimeOffset? checkedAt, string? name = null, string? icon = null)
    {
        MappingStatus = ExternalIdentifier is null ? ApiMappingStatus.NotConfigured : status;
        MappingCheckedAt = checkedAt?.ToUniversalTime();
        MatchedApiName = name; MatchedApiIcon = icon;
    }
    public void SetPrice(long? value, CataloguePriceSource source, DateTimeOffset? observedAt)
    {
        if (value is < 0 || !Enum.IsDefined(source) || (source == CataloguePriceSource.Missing) != (value is null)
            || (source == CataloguePriceSource.Untradeable && value != 0))
            throw new ArgumentException("A non-negative value and matching price source are required.");
        CatalogueValueGp = value; PriceSource = source; PriceObservedAt = observedAt?.ToUniversalTime();
        if (value is not null) RestorePriceRejection(null, null);
    }
    public bool CheckApiPriceCandidate(long value, DateTimeOffset observedAt)
    {
        if (CataloguePricing.IsSuspiciousMove(CatalogueValueGp, value))
        {
            RestorePriceRejection(value, observedAt);
            return false;
        }
        RestorePriceRejection(null, null);
        return true;
    }
    public void RestorePriceRejection(long? candidate, DateTimeOffset? observedAt)
    {
        if (candidate is < 0 || (candidate is null) != (observedAt is null)) throw new ArgumentException("A rejected candidate and its observation time are required together.");
        RejectedPriceGp = candidate; RejectedPriceObservedAt = observedAt?.ToUniversalTime();
    }
    public bool ApplyApiPrice(long? value, DateTimeOffset observedAt, bool replaceFixedValue = false)
    {
        if (value is null || !replaceFixedValue && PriceSource is CataloguePriceSource.Manual or CataloguePriceSource.Untradeable) return false;
        if (!CheckApiPriceCandidate(value.Value, observedAt)) return false;
        SetPrice(value, CataloguePriceSource.Api, observedAt); return true;
    }
    public void Update(string name, string normalizedName, string? externalId, string? notes, string? imageUrl = null) { Name = name; NormalizedName = normalizedName; ConfigureApi(externalId); Notes = notes; ImageUrl = imageUrl; }
    public void SetActive(bool active) => Active = active;
    public void AdvanceVersion() => Version++;
}
