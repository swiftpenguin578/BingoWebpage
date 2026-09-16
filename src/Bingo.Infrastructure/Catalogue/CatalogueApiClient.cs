using System.Net;
using System.Text.Json;
using Bingo.Application.Catalogue;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.WiseOldMan;

namespace Bingo.Infrastructure.Catalogue;

public sealed class CatalogueApiClient(IHttpClientFactory factory, TimeProvider time, WiseOldManRequestLimiter limiter) : ICatalogueApiClient, IDisposable
{
    public const string UserAgent = "DKLegacy - Community bingo item pricing - Discord: @chrisschmidt";
    private static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultWikiRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaximumInlineWikiRetryDelay = TimeSpan.FromSeconds(2);
    public static void ConfigurePriceClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://prices.runescape.wiki/api/v1/osrs/");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
    }
    public void Dispose() => gate.Dispose();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<(string Client, string Path), (DateTimeOffset Until, object Result)> cache = new();
    private DateTimeOffset wikiCooldownUntil = DateTimeOffset.MinValue;

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
            var now = time.GetUtcNow();
            if (cache.TryGetValue(cacheKey, out var saved) && saved.Until > now) return (CatalogueApiResult<T>)saved.Result;
            if (!wom && wikiCooldownUntil > now)
            {
                var blocked = new CatalogueApiResult<T>(null, "The price API is temporarily limiting requests. Retry validation later.");
                cache[cacheKey] = (wikiCooldownUntil, blocked);
                return blocked;
            }

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
                // Retry one transient price API failure. The provider's requested
                // cooldown is shared by every price endpoint and is never capped.
                if (!wom && IsTransientWikiFailure(response.StatusCode))
                {
                    var retryAt = GetWikiRetryAt(response);
                    SetWikiCooldown(retryAt);
                    var retryDelay = retryAt - time.GetUtcNow();
                    if (response.StatusCode == HttpStatusCode.TooManyRequests || retryDelay > MaximumInlineWikiRetryDelay)
                    {
                        result = new(null, "The price API is temporarily limiting requests. Retry validation later.");
                        response.Dispose();
                        response = null;
                    }
                    else
                    {
                        response.Dispose();
                        response = null;
                        await Task.Delay(retryDelay > TimeSpan.Zero ? retryDelay : DefaultWikiRetryDelay, ct);
                        response = await client.GetAsync(path, ct);
                        if (IsTransientWikiFailure(response.StatusCode))
                        {
                            SetWikiCooldown(GetWikiRetryAt(response));
                            result = new(null, "The price API is temporarily limiting requests. Retry validation later.");
                        }
                        else
                        {
                            result = await ParseResponseAsync(response, parse, ct);
                        }
                    }
                }
                else
                {
                    result = await ParseResponseAsync(response, parse, ct);
                }

                if (!wom && result.Available) wikiCooldownUntil = DateTimeOffset.MinValue;
                if (admission is not null) await admission.CompleteAsync(response, result.Available, false, CancellationToken.None);
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
            var cacheNow = time.GetUtcNow();
            var failureUntil = cacheNow.Add(FailureCacheDuration);
            var cacheUntil = result.Available ? cacheNow.Add(SuccessCacheDuration)
                : !wom && wikiCooldownUntil > failureUntil ? wikiCooldownUntil : failureUntil;
            cache[cacheKey] = (cacheUntil, result);
            return result;
        }
        finally { gate.Release(); }
    }

    private static bool IsTransientWikiFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static async Task<CatalogueApiResult<T>> ParseResponseAsync<T>(HttpResponseMessage response, Func<JsonElement, T> parse, CancellationToken ct) where T : class
    {
        if (!response.IsSuccessStatusCode) throw new HttpRequestException();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return new(parse(json.RootElement));
    }

    private DateTimeOffset GetWikiRetryAt(HttpResponseMessage response)
    {
        var now = time.GetUtcNow();
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return SafeAdd(now, delta);
        if (retryAfter?.Date is { } date && date > now)
            return date;
        return SafeAdd(now, response.StatusCode == HttpStatusCode.TooManyRequests ? FailureCacheDuration : DefaultWikiRetryDelay);
    }

    private void SetWikiCooldown(DateTimeOffset retryAt)
    {
        if (retryAt > wikiCooldownUntil) wikiCooldownUntil = retryAt;
    }

    private static DateTimeOffset SafeAdd(DateTimeOffset value, TimeSpan amount) =>
        amount >= DateTimeOffset.MaxValue - value ? DateTimeOffset.MaxValue : value.Add(amount);
}
