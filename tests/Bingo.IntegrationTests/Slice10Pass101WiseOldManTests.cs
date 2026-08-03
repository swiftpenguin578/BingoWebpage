using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Infrastructure.WiseOldMan;
using Bingo.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bingo.IntegrationTests;

public sealed class Slice10Pass101WiseOldManTests
{
    [Fact]
    public async Task SuccessfulPlayerLookupIsCachedForFiveMinutesWithOriginalFetchTime()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var calls = 0;
        var client = CreateClient(clock, _ =>
        {
            Interlocked.Increment(ref calls);
            return Response("{\"ehb\":12.5}", 19);
        });

        var first = await client.LookupPlayerAsync("Some Name");
        clock.Advance(TimeSpan.FromMinutes(4));
        var second = await client.LookupPlayerAsync("some name");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(12.5m, second.Ehb);
        Assert.Equal(first.FetchedAt, second.FetchedAt);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FinalThreeRequestsAreRejectedLocallyAndBootstrapRecoversAfterMissingHeaders()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var responses = new Queue<HttpResponseMessage>([
            Response("{\"ehb\":1}", 3),
            new(HttpStatusCode.OK) { Content = JsonContent.Create(new { ehb = 2m }) }
        ]);
        var firstCalls = 0;
        var client = CreateClient(clock, _ => { Interlocked.Increment(ref firstCalls); return responses.Dequeue(); });

        var first = await client.LookupPlayerAsync("First");
        Assert.Equal(3, client.GetStatus().ObservedRemaining);
        var locallyRejected = await client.LookupPlayerAsync("Second");
        clock.Advance(TimeSpan.FromSeconds(30));
        var missingHeaderResponses = new Queue<HttpResponseMessage>([
            new(HttpStatusCode.OK) { Content = JsonContent.Create(new { ehb = 3m }) },
            Response("{\"ehb\":4}", 19)
        ]);
        var recoveryCalls = 0;
        var recoveryClient = CreateClient(clock, _ => { Interlocked.Increment(ref recoveryCalls); return missingHeaderResponses.Dequeue(); });
        var failedBootstrap = await recoveryClient.LookupPlayerAsync("Third");
        clock.Advance(TimeSpan.FromSeconds(61));
        var recovered = await recoveryClient.LookupPlayerAsync("Fourth");

        Assert.True(first.Succeeded);
        Assert.Equal(WiseOldManLookupStatus.RateLimited, locallyRejected.Status);
        Assert.Equal(1, firstCalls);
        Assert.Equal(2, recoveryCalls);
        Assert.True(failedBootstrap.Succeeded);
        Assert.True(recovered.Succeeded);
        Assert.Equal(4m, recovered.Ehb);
    }

    [Fact]
    public async Task BootstrapAdmissionSerializesConcurrentRequests()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var handler = new DelegateHandler(async (_, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
            }
            return Response("{\"ehb\":7}", 19);
        });
        var client = new WiseOldManClient(
            new SingleClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://fake.test/") }),
            new WiseOldManRequestLimiter(clock, NullLogger<WiseOldManRequestLimiter>.Instance), clock, NullLogger<WiseOldManClient>.Instance);

        var first = client.LookupPlayerAsync("First");
        await firstStarted.Task;
        var second = client.LookupPlayerAsync("Second");
        await Task.Delay(30);
        Assert.Equal(1, calls);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task UnexpectedTransportExceptionReleasesAdmissionAndFailsClosed()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var calls = 0;
        var client = CreateClient(clock, _ =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException("fake stream failure");
            return Response("{\"ehb\":7}", 19);
        });

        var failed = await client.LookupPlayerAsync("Unexpected");
        Assert.Equal(WiseOldManLookupStatus.Unavailable, failed.Status);
        Assert.NotNull(failed.RetryAt);
        clock.Advance(TimeSpan.FromSeconds(61));
        var recovered = await client.LookupPlayerAsync("Recovered");

        Assert.True(recovered.Succeeded);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task NotFoundUnavailableAnd429ResponsesRemainLocalAndHonorRetryAfter()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var responses = new Queue<HttpResponseMessage>([
            ResponseWithStatus(HttpStatusCode.NotFound, "{}", 19),
            ResponseWithStatus(HttpStatusCode.InternalServerError, "{}", 18),
            ResponseWithStatus(HttpStatusCode.TooManyRequests, "{}", 17, retryAfterSeconds: 30),
            Response("{\"ehb\":9}", 16)
        ]);
        var calls = 0;
        var client = CreateClient(clock, _ => { Interlocked.Increment(ref calls); return responses.Dequeue(); });

        var notFound = await client.LookupPlayerAsync("Missing");
        var unavailable = await client.LookupPlayerAsync("Unavailable");
        var limited = await client.LookupPlayerAsync("Limited");
        var locallyBlocked = await client.LookupPlayerAsync("Still Limited");
        clock.Advance(TimeSpan.FromSeconds(31));
        var recovered = await client.LookupPlayerAsync("Recovered");

        Assert.Equal(WiseOldManLookupStatus.NotFound, notFound.Status);
        Assert.Equal(WiseOldManLookupStatus.Unavailable, unavailable.Status);
        Assert.NotNull(unavailable.RetryAt);
        Assert.Equal(WiseOldManLookupStatus.RateLimited, limited.Status);
        Assert.Equal(WiseOldManLookupStatus.RateLimited, locallyBlocked.Status);
        Assert.True(recovered.Succeeded);
        Assert.Equal(4, calls);
        Assert.Equal(9m, recovered.Ehb);
    }

    [Fact]
    public async Task CompetitionDetailsUseOneMetricRequestAndParseParticipationDeltas()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var client = CreateClient(clock, request =>
        {
            Assert.Equal("/competitions/42?metric=ehb", request.RequestUri!.PathAndQuery);
            return Response("{\"id\":42,\"title\":\"Test competition\",\"startsAt\":\"2026-08-03T10:00:00Z\",\"endsAt\":\"2026-08-03T12:00:00Z\",\"updatedAt\":\"2026-08-03T12:01:00Z\",\"participations\":[{\"player\":{\"username\":\"Alice\",\"type\":\"REGULAR\"},\"deltas\":[{\"metric\":\"ehb\",\"values\":{\"gained\":12.5,\"start\":10,\"end\":22}}]}]}", 19);
        });

        var result = await client.GetCompetitionAsync(42);

        Assert.True(result.Succeeded);
        Assert.Equal("Test competition", result.Competition!.Title);
        var participant = Assert.Single(result.Competition.Participants);
        Assert.Equal("Alice", participant.Username);
        Assert.Equal(12.5m, participant.EhbDelta);
        Assert.Equal(DateTimeOffset.Parse("2026-08-03T12:01:00Z", CultureInfo.InvariantCulture), result.Competition.LastUpdatedAt);
    }

    [Fact]
    public async Task DevelopmentFakeRoutesConfiguredV2BasePathForPlayerAndCompetition()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var options = new WiseOldManOptions
        {
            BaseUrl = "https://api.wiseoldman.net/v2/",
            DevelopmentFake = new WiseOldManDevelopmentFakeOptions
            {
                PlayerMode = "Success",
                CompetitionMode = "Complete",
                Remaining = 19
            }
        };
        using var httpClient = new HttpClient(new WiseOldManDevelopmentFakeHandler(options, clock))
        {
            BaseAddress = new Uri(options.BaseUrl)
        };
        var client = new WiseOldManClient(
            new SingleClientFactory(httpClient),
            new WiseOldManRequestLimiter(clock, NullLogger<WiseOldManRequestLimiter>.Instance),
            clock,
            NullLogger<WiseOldManClient>.Instance);

        var player = await client.LookupPlayerAsync("Rasmus Zebak");
        var competition = await client.GetCompetitionAsync(1515);

        Assert.True(player.Succeeded);
        Assert.Equal(12.5m, player.Ehb);
        Assert.True(competition.Succeeded);
        Assert.Equal(1515, competition.Competition!.Id);
    }

    [Fact]
    public void SignupLookupTokenBindsCharacterValuePurposeAndExpiry()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var service = new SignupLookupTokenService(new EphemeralDataProtectionProvider());
        var fetched = clock.GetUtcNow();
        var token = service.Create("SOME NAME", 12.5m, fetched, fetched, fetched.AddMinutes(5));

        Assert.True(service.TryValidate(token, "SOME NAME", 12.5m, clock.GetUtcNow(), out var validatedFetchedAt));
        Assert.Equal(fetched, validatedFetchedAt);
        Assert.False(service.TryValidate(token, "OTHER NAME", 12.5m, clock.GetUtcNow(), out _));
        Assert.False(service.TryValidate(token, "SOME NAME", 12.6m, clock.GetUtcNow(), out _));
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.False(service.TryValidate(token, "SOME NAME", 12.5m, clock.GetUtcNow(), out _));
    }

    private static WiseOldManClient CreateClient(TestClock clock, Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        var handler = new DelegateHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://fake.test/") };
        return new(new SingleClientFactory(httpClient), new WiseOldManRequestLimiter(clock, NullLogger<WiseOldManRequestLimiter>.Instance), clock, NullLogger<WiseOldManClient>.Instance);
    }

    private static HttpResponseMessage Response(string json, int remaining)
    {
        return ResponseWithStatus(HttpStatusCode.OK, json, remaining);
    }

    private static HttpResponseMessage ResponseWithStatus(HttpStatusCode status, string json, int remaining, int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(json) };
        response.Headers.Add("RateLimit-Limit", "20");
        response.Headers.Add("RateLimit-Remaining", remaining.ToString(System.Globalization.CultureInfo.InvariantCulture));
        response.Headers.Add("RateLimit-Reset", "60");
        if (retryAfterSeconds is { } retryAfter) response.Headers.Add("Retry-After", retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return response;
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response;
        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> response) => this.response = (request, _) => Task.FromResult(response(request));
        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => this.response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset value = now;
        public override DateTimeOffset GetUtcNow() => value;
        public void Advance(TimeSpan amount) => value += amount;
    }
}
