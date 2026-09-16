using Bingo.Domain.Catalogue;

namespace Bingo.Application.Catalogue;

public sealed record ApiItem(int Id, string Name, string Icon);
public sealed record ApiHourlyPrices(DateTimeOffset Hour, IReadOnlyDictionary<int, long?> Values);
public sealed record CatalogueApiResult<T>(T? Data, string? Error = null) where T : class
{
    public bool Available => Data is not null;
}
public interface ICatalogueApiClient
{
    Task<CatalogueApiResult<IReadOnlyList<ApiItem>>> GetItemsAsync(CancellationToken ct);
    Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(CancellationToken ct);
    Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(DateTimeOffset hour, CancellationToken ct);
    Task<CatalogueApiResult<IReadOnlySet<string>>> GetBossMetricsAsync(CancellationToken ct);
}
