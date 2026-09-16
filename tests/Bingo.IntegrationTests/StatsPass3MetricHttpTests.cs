using System.Net;
using System.Text;
using System.Text.Json;
using Bingo.Infrastructure.WiseOldMan;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bingo.IntegrationTests;

public sealed class StatsPass3MetricHttpTests
{
    [Fact]
    public async Task ExistingCaseInsensitiveEhbMetricParsingIsPreserved()
    {
        using var http = new HttpClient(new Handler(_ => Json(new[] { new { metric = "EHB", values = new { start = 10, end = 25, gained = 15 } } })))
        { BaseAddress = new("https://api.wiseoldman.net/v2/") };
        var result = await Client(http).GetCompetitionAsync(42);
        Assert.True(result.Succeeded); Assert.Equal(15, Assert.Single(result.Competition!.Participants).EhbDelta);
    }

    [Fact]
    public async Task FullMetricSetUsesOnePluralRequestPlusEhbAndIgnoresTotal()
    {
        var metrics = CompetitionMetricContract.SupportedMetrics.Order(StringComparer.Ordinal).ToArray();
        var calls = 0;
        var deltas = metrics.Append("ehb").Select(metric => new { metric, values = new { start = 10, end = 25, gained = 15 } })
            .Append(new { metric = "total", values = new { start = 9000, end = 9999, gained = 999 } });
        using var http = new HttpClient(new Handler(request =>
        {
            calls++;
            var requested = request.RequestUri!.Query.TrimStart('?').Split('&');
            Assert.Equal(metrics.Length + 1, requested.Length);
            Assert.All(requested, x => Assert.StartsWith("metrics=", x, StringComparison.Ordinal));
            Assert.Equal(metrics.Append("ehb").Order(StringComparer.Ordinal), requested.Select(x => x[8..]).Order(StringComparer.Ordinal));
            return Json(deltas);
        }))
        { BaseAddress = new("https://api.wiseoldman.net/v2/") };
        var result = await Client(http).GetCompetitionAsync(42, [.. metrics, "vorkath", "ehb"]);
        Assert.True(result.Succeeded); Assert.Equal(1, calls);
        var player = Assert.Single(result.Competition!.Participants);
        Assert.Equal(15, player.EhbDelta); Assert.Equal(metrics.Length + 1, player.Metrics!.Count);
        Assert.DoesNotContain("total", player.Metrics.Keys);
        Assert.All(player.Metrics.Values, x => { Assert.Equal(10, x.Start); Assert.Equal(25, x.End); Assert.Equal(15, x.Gained); });
        Assert.Equal(DateTimeOffset.Parse("2026-09-15T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture), player.UpstreamUpdatedAt);
    }

    [Theory]
    [InlineData("{\"metric\":\"vorkath\",\"values\":{\"start\":10,\"end\":\"bad\",\"gained\":15}}")]
    [InlineData("{\"metric\":\"vorkath\",\"values\":null}")]
    [InlineData("{\"metric\":\"vorkath\",\"values\":{}}")]
    [InlineData("{\"metric\":\"total\",\"values\":{\"start\":900,\"end\":999,\"gained\":99}}")]
    [InlineData("{\"metric\":\"vorkath\",\"values\":{\"start\":-1,\"end\":-1,\"gained\":0}},{\"metric\":\"vorkath\",\"values\":{\"start\":0,\"end\":5,\"gained\":5}}")]
    public async Task PartialMalformedOrDuplicateBossMetricPreservesEhb(string boss)
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
            "{\"id\":42,\"title\":\"Synthetic\",\"startsAt\":\"2026-09-15T00:00:00Z\",\"endsAt\":\"2026-09-16T00:00:00Z\",\"participations\":[{\"player\":{\"username\":\"Fixture\"},\"deltas\":[{\"metric\":\"ehb\",\"values\":{\"start\":10,\"end\":25,\"gained\":15}}," + boss + "]}]}", Encoding.UTF8, "application/json")
        }))
        { BaseAddress = new("https://api.wiseoldman.net/v2/") };
        var result = await Client(http).GetCompetitionAsync(42, ["vorkath", "zulrah"]);
        Assert.True(result.Succeeded); var player = Assert.Single(result.Competition!.Participants);
        Assert.Equal(15, player.EhbDelta); Assert.False(player.Metrics!.ContainsKey("zulrah"));
        Assert.Null(player.Metrics.GetValueOrDefault("vorkath")?.End);
    }

    private static HttpResponseMessage Json(object deltas) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            id = 42,
            title = "Synthetic",
            startsAt = "2026-09-15T00:00:00Z",
            endsAt = "2026-09-16T00:00:00Z",
            participations = new[] { new { player = new { username = "Fixture", updatedAt = "2026-09-15T10:00:00Z" }, deltas } }
        }), Encoding.UTF8, "application/json")
    };
    private static WiseOldManClient Client(HttpClient http) => new(new Factory(http), new(TimeProvider.System, NullLogger<WiseOldManRequestLimiter>.Instance), TimeProvider.System, NullLogger<WiseOldManClient>.Instance);
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request)); }
}
