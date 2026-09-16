using System.Net;
using System.Text.Json;
using Bingo.Application.Catalogue;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.WiseOldMan;

namespace Bingo.Infrastructure.Catalogue;

public sealed class CatalogueApiClient(IHttpClientFactory factory, TimeProvider time, WiseOldManRequestLimiter limiter) : ICatalogueApiClient, IDisposable
{
    public const string UserAgent = "DKLegacy - Community bingo item pricing - Discord: @chrisschmidt";
    public static void ConfigurePriceClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://prices.runescape.wiki/api/v1/osrs/");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
    }
    public void Dispose() => gate.Dispose();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<(string Client, string Path), (DateTimeOffset Until, object Result)> cache = new();

    public Task<CatalogueApiResult<IReadOnlyList<ApiItem>>> GetItemsAsync(CancellationToken ct) =>
        FetchAsync<IReadOnlyList<ApiItem>>("mapping", false, root =>
        {
            var items = root.EnumerateArray().Select(x => new ApiItem(x.GetProperty("id").GetInt32(),
                x.GetProperty("name").GetString()!, x.GetProperty("icon").GetString()!)).ToArray();
            if (items.Length == 0 || items.Any(x => x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 200 || x.Icon is null || x.Icon.Length > 500)
                || items.Select(x => x.Id).Distinct().Count() != items.Length) throw new JsonException();
            return items;
        }, ct);

    public Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(CancellationToken ct) =>
        FetchAsync("1h", false, root => ParseHourlyPrices(root), ct);

    public Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(DateTimeOffset hour, CancellationToken ct)
    {
        hour = hour.ToUniversalTime();
        if (hour.Ticks % TimeSpan.TicksPerHour != 0 || hour.AddHours(1) > time.GetUtcNow())
            throw new ArgumentOutOfRangeException(nameof(hour), "A completed UTC hour is required.");
        var path = "1h?timestamp=" + hour.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        return FetchAsync(path, false, root => ParseHourlyPrices(root, hour), ct);
    }

    private ApiHourlyPrices ParseHourlyPrices(JsonElement root, DateTimeOffset? requestedHour = null)
    {
        var hour = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("timestamp").GetInt64());
        if (hour.ToUnixTimeSeconds() % 3600 != 0 || hour.AddHours(1) > time.GetUtcNow()
            || requestedHour is { } expected && hour != expected) throw new JsonException();
        var values = new Dictionary<int, long?>();
        foreach (var entry in root.GetProperty("data").EnumerateObject())
        {
            if (!int.TryParse(entry.Name, out var id) || id <= 0) throw new JsonException();
            var high = entry.Value.GetProperty("avgHighPrice"); var low = entry.Value.GetProperty("avgLowPrice");
            values.Add(id, CataloguePricing.Midpoint(high.ValueKind == JsonValueKind.Null ? null : high.GetInt64(),
                low.ValueKind == JsonValueKind.Null ? null : low.GetInt64()));
        }
        if (values.Count == 0) throw new JsonException();
        return new ApiHourlyPrices(hour, values);
    }

    public Task<CatalogueApiResult<IReadOnlySet<string>>> GetBossMetricsAsync(CancellationToken ct) =>
        FetchAsync<IReadOnlySet<string>>("efficiency/rates?type=main&metric=ehb", true, root =>
        {
            var result = root.EnumerateArray().Select(x => x.GetProperty("boss").GetString()!).ToHashSet(StringComparer.Ordinal);
            if (result.Count == 0 || result.Any(x => string.IsNullOrWhiteSpace(x))) throw new JsonException();
            return result;
        }, ct);

    private async Task<CatalogueApiResult<T>> FetchAsync<T>(string path, bool wom, Func<JsonElement, T> parse, CancellationToken ct) where T : class
    {
        await gate.WaitAsync(ct);
        try
        {
            var clientName = wom ? "WiseOldMan" : "OsrsWikiPrices";
            var cacheKey = (clientName, path);
            if (cache.TryGetValue(cacheKey, out var saved) && saved.Until > time.GetUtcNow()) return (CatalogueApiResult<T>)saved.Result;
            CatalogueApiResult<T> result;
            HttpResponseMessage? response = null;
            WiseOldManRequestLimiter.WiseOldManAdmission? admission = null;
            try
            {
                if (wom)
                {
                    admission = await limiter.AdmitAsync(ct);
                    if (!admission.AllowedRequest) return new(null, "Wise Old Man is temporarily unavailable. Try validation again later.");
                }
                var client = factory.CreateClient(clientName);
                response = await client.GetAsync(path, ct);
                // Retry a transient Wiki failure once. WOM retries remain owned by its shared limiter.
                if (!wom && response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                {
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                    response = await client.GetAsync(path, ct);
                }
                if (!response.IsSuccessStatusCode) throw new HttpRequestException();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                result = new(parse(json.RootElement));
                if (admission is not null) await admission.CompleteAsync(response, true, false, CancellationToken.None);
            }
            catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException or OperationCanceledException)
            {
                if (admission is not null) await admission.CompleteAsync(response, false, e is HttpRequestException or OperationCanceledException, CancellationToken.None);
                ct.ThrowIfCancellationRequested();
                result = new(null, "The API is temporarily unavailable or returned invalid data. Your stored value is kept; retry validation later.");
            }
            finally
            {
                response?.Dispose();
                if (admission is not null) await admission.DisposeAsync();
            }
            foreach (var expired in cache.Where(x => x.Value.Until <= time.GetUtcNow()).Select(x => x.Key).ToArray())
                cache.Remove(expired);
            // Historical hours have distinct identities; bound retained entries even during a burst of old-event corrections.
            if (cache.Count >= 64) cache.Remove(cache.MinBy(x => x.Value.Until).Key);
            cache[cacheKey] = (time.GetUtcNow().Add(result.Available ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30)), result);
            return result;
        }
        finally { gate.Release(); }
    }
}
