using System.Net;
using Bingo.Infrastructure.Catalogue;
using Bingo.Infrastructure.WiseOldMan;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bingo.IntegrationTests;

public sealed class CataloguePriceHttpTests
{
    [Fact]
    public async Task BulkRequestsUseApprovedHeaderAndCacheAndPreserveOneSidedValues()
    {
        using var handler = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("mapping", StringComparison.Ordinal)
            ? "[{\"id\":4151,\"name\":\"Abyssal whip\",\"icon\":\"Abyssal whip.png\"}]"
            : "{\"timestamp\":1789470000,\"data\":{\"4151\":{\"avgHighPrice\":797124,\"avgLowPrice\":781558},\"6\":{\"avgHighPrice\":null,\"avgLowPrice\":191598}}}");
        using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        Assert.Single((await api.GetItemsAsync(default)).Data!);
        var prices = (await api.GetHourlyPricesAsync(default)).Data!;
        Assert.Equal(789341L, prices.Values[4151]); Assert.Equal(191598L, prices.Values[6]);
        Assert.False(prices.Values.ContainsKey(999));
        await api.GetHourlyPricesAsync(default); await api.GetItemsAsync(default);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("{\"timestamp\":1789470000,\"data\":{\"1\":{\"avgHighPrice\":-1,\"avgLowPrice\":1}}}")]
    [InlineData("<html>unavailable</html>")]
    [InlineData("{}")]
    [InlineData("{\"timestamp\":1789470000,\"data\":{\"1\":{}}}")]
    [InlineData("{\"timestamp\":1789470000,\"data\":{\"1\":{\"avgHighPrice\":1}}}")]
    [InlineData("{\"timestamp\":1789470001,\"data\":{}}")]
    public async Task BadResponsesAreUnavailableNotZero(string payload)
    {
        using var handler = new Handler(_ => payload); using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        Assert.False((await api.GetHourlyPricesAsync(default)).Available);
        Assert.False((await api.GetHourlyPricesAsync(default)).Available);
        Assert.Equal(1, handler.Calls);
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"id\":1,\"name\":\"Missing icon\"}]")]
    [InlineData("[{\"id\":0,\"name\":\"Bad ID\",\"icon\":\"a.png\"}]")]
    [InlineData("[{\"id\":1,\"name\":\"A\",\"icon\":\"a.png\"},{\"id\":1,\"name\":\"B\",\"icon\":\"b.png\"}]")]
    public async Task MalformedMappingsAreUnavailable(string payload)
    {
        using var handler = new Handler(_ => payload); using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        Assert.False((await api.GetItemsAsync(default)).Available);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, 2)]
    [InlineData(HttpStatusCode.NotFound, 1)]
    public async Task WikiFailureHasBoundedRetryAndFailureCache(HttpStatusCode status, int expectedCalls)
    {
        using var handler = new Handler(_ => "{}") { Status = status }; using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        Assert.False((await api.GetItemsAsync(default)).Available);
        Assert.False((await api.GetItemsAsync(default)).Available);
        Assert.Equal(expectedCalls, handler.Calls);
    }

    [Fact]
    public async Task TimeoutIsUnavailableButCallerCancellationIsPropagated()
    {
        using var handler = new Handler(_ => throw new TaskCanceledException()); using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        Assert.False((await api.GetItemsAsync(default)).Available);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.GetItemsAsync(cancellation.Token));
    }

    [Fact]
    public async Task BossMetadataExtractsOnlyExactKeysAndSharesWomCooldown()
    {
        using var handler = new Handler(request =>
        {
            Assert.Equal("https://api.wiseoldman.net/v2/efficiency/rates?type=main&metric=ehb", request.RequestUri!.AbsoluteUri);
            return "[{\"boss\":\"zulrah\",\"rate\":99999},{\"boss\":\"theatre_of_blood_hard_mode\",\"rate\":12345}]";
        });
        using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using (var api = new CatalogueApiClient(factory, new FixedTime(), limiter))
        {
            var metrics = (await api.GetBossMetricsAsync(default)).Data!;
            Assert.Equal(2, metrics.Count); Assert.Contains("zulrah", metrics); Assert.Contains("theatre_of_blood_hard_mode", metrics);
        }
        handler.Status = HttpStatusCode.TooManyRequests;
        using (var api = new CatalogueApiClient(factory, new FixedTime(), limiter)) Assert.False((await api.GetBossMetricsAsync(default)).Available);
        await using var admission = await limiter.AdmitAsync(default);
        Assert.False(admission.AllowedRequest);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task HistoricalRequestsKeepExactUtcHourAndSeparateLatestAndHourCaches()
    {
        var hour = new DateTimeOffset(2026, 9, 15, 11, 0, 0, TimeSpan.Zero);
        using var handler = new Handler(request =>
        {
            var timestamp = request.RequestUri!.Query.Length == 0 ? hour.AddHours(1).ToUnixTimeSeconds()
                : long.Parse(request.RequestUri.Query[11..], System.Globalization.CultureInfo.InvariantCulture);
            Assert.StartsWith("https://prices.runescape.wiki/api/v1/osrs/1h", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                timestamp,
                data = new Dictionary<string, object>
                {
                    ["1"] = new { avgHighPrice = (long?)timestamp, avgLowPrice = (long?)null }
                }
            });
        });
        using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        var first = (await api.GetHourlyPricesAsync(hour.ToOffset(TimeSpan.FromHours(2)), default)).Data!;
        Assert.Equal(hour, first.Hour);
        Assert.Equal(hour.ToUnixTimeSeconds(), first.Values[1]);
        Assert.Equal(first, (await api.GetHourlyPricesAsync(hour, default)).Data);
        var previous = (await api.GetHourlyPricesAsync(hour.AddHours(-1), default)).Data!;
        Assert.Equal(hour.AddHours(-1), previous.Hour);
        Assert.Equal(hour.AddHours(-1).ToUnixTimeSeconds(), previous.Values[1]);
        Assert.Equal(hour.AddHours(1), (await api.GetHourlyPricesAsync(default)).Data!.Hour);
        Assert.Equal(3, handler.Calls);
    }

    [Theory]
    [InlineData(10L, 11L, 11L)]
    [InlineData(0L, 0L, 0L)]
    [InlineData(null, 0L, 0L)]
    [InlineData(13L, null, 13L)]
    [InlineData(null, null, null)]
    [InlineData(long.MaxValue, long.MaxValue, long.MaxValue)]
    public async Task HistoricalHourlyValuesUseSafeMidpointOneSideAndRealZero(long? high, long? low, long? expected)
    {
        var hour = new DateTimeOffset(2026, 9, 15, 11, 0, 0, TimeSpan.Zero);
        using var handler = new Handler(_ => System.Text.Json.JsonSerializer.Serialize(new
        {
            timestamp = hour.ToUnixTimeSeconds(),
            data = new Dictionary<string, object> { ["1"] = new { avgHighPrice = high, avgLowPrice = low } }
        }));
        using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        Assert.Equal(expected, (await api.GetHourlyPricesAsync(hour, default)).Data!.Values[1]);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, 1789470000, 1)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 1789470000, 2)]
    [InlineData(HttpStatusCode.OK, 1789466400, 1)]
    public async Task MissingOrWrongHistoricalHourRemainsUnavailable(HttpStatusCode status, long timestamp, int calls)
    {
        using var handler = new Handler(_ => System.Text.Json.JsonSerializer.Serialize(new
        {
            timestamp,
            data = new Dictionary<string, object> { ["1"] = new { avgHighPrice = 10, avgLowPrice = 20 } }
        }))
        { Status = status };
        using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        var hour = DateTimeOffset.FromUnixTimeSeconds(1789470000);
        Assert.False((await api.GetHourlyPricesAsync(hour, default)).Available);
        Assert.False((await api.GetHourlyPricesAsync(hour, default)).Available);
        Assert.Equal(calls, handler.Calls);
    }

    [Fact]
    public async Task HistoricalRequestsRejectIncompleteOrMisalignedHoursBeforeHttp()
    {
        using var handler = new Handler(_ => "{}");
        using var factory = new Factory(handler);
        using var limiter = new WiseOldManRequestLimiter(new FixedTime(), NullLogger<WiseOldManRequestLimiter>.Instance);
        using var api = new CatalogueApiClient(factory, new FixedTime(), limiter);
        var now = new FixedTime().GetUtcNow();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.GetHourlyPricesAsync(now, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => api.GetHourlyPricesAsync(now.AddHours(-1).AddTicks(1), default));
        Assert.Equal(0, handler.Calls);
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);
    }
    private sealed class Factory(Handler handler) : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient client = Create(handler);
        private readonly HttpClient wom = new(handler, false) { BaseAddress = new Uri("https://api.wiseoldman.net/v2/") };
        private static HttpClient Create(Handler handler) { var client = new HttpClient(handler, false); CatalogueApiClient.ConfigurePriceClient(client); return client; }
        public HttpClient CreateClient(string name) => name == "WiseOldMan" ? wom : client;
        public void Dispose() { client.Dispose(); wom.Dispose(); }
    }
    private sealed class Handler(Func<HttpRequestMessage, string> payload) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (request.RequestUri!.Host == "prices.runescape.wiki") Assert.Equal("DKLegacy - Community bingo item pricing - Discord: @chrisschmidt", Assert.Single(request.Headers.GetValues("User-Agent")));
            var response = new HttpResponseMessage(Status) { Content = new StringContent(payload(request)) };
            if (request.RequestUri.Host == "api.wiseoldman.net")
            {
                response.Headers.Add("RateLimit-Limit", "100"); response.Headers.Add("RateLimit-Remaining", "99"); response.Headers.Add("RateLimit-Reset", "60");
                if (Status == HttpStatusCode.TooManyRequests) response.Headers.Add("Retry-After", "30");
            }
            return Task.FromResult(response);
        }
    }
}
