using System.Diagnostics;
using Bingo.Web.Catalogue;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bingo.BrowserTests;

public sealed class OsrsWikiImageCacheTests
{
    [Fact]
    public async Task WikiImagesAreDownloadedOnceAndServedFromPersistentCache()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var handler = new CountingImageHandler();
            var cache = CreateCache(root, handler);
            const string source = "https://oldschool.runescape.wiki/images/Test_image.png?123";

            var first = await cache.GetAsync(source);
            var second = await cache.GetAsync(source);

            Assert.Equal("image/png", first.MediaType);
            Assert.Equal(first.Path, second.Path);
            Assert.True(File.Exists(first.Path));
            Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(first.Path));
            Assert.Equal(1, handler.RequestCount);
            Assert.True(cache.IsCached(source));

            var restartedHandler = new CountingImageHandler();
            var restarted = CreateCache(root, restartedHandler);
            var afterRestart = await restarted.GetAsync(source);
            Assert.Equal(first.Path, afterRestart.Path);
            Assert.Equal(0, restartedHandler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ConcurrentMissesShareOneDownloadTask()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new BlockingImageHandler(started, release);
            var cache = CreateCache(root, handler);
            const string source = "https://oldschool.runescape.wiki/images/Concurrent.png";

            var first = cache.GetAsync(source);
            await started.Task;
            using var cancelledWaiter = new CancellationTokenSource();
            var cancelled = cache.GetAsync(source, cancelledWaiter.Token);
            cancelledWaiter.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            var second = cache.GetAsync(source);
            await Task.Delay(30);
            Assert.Equal(1, handler.RequestCount);
            release.SetResult();

            var results = await Task.WhenAll(first, second);
            Assert.Equal(results[0].Path, results[1].Path);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AllCancelledWaitersLeaveCompletedFailureRemovableAndPermitHealthy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var clock = new TestClock(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handler = new BlockingImageHandler(started, release)
            {
                Status = System.Net.HttpStatusCode.ServiceUnavailable,
                RetryAfterSeconds = 1
            };
            var cache = CreateCache(root, handler, clock);
            const string source = "https://oldschool.runescape.wiki/images/Cancelled.png";

            using var firstCancellation = new CancellationTokenSource();
            using var secondCancellation = new CancellationTokenSource();
            var first = cache.GetAsync(source, firstCancellation.Token);
            await started.Task;
            var second = cache.GetAsync(source, secondCancellation.Token);
            firstCancellation.Cancel();
            secondCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);

            release.SetResult();
            await handler.ResponseReturned.Task;
            await Task.Delay(50);
            clock.Advance(TimeSpan.FromSeconds(2));
            handler.Status = System.Net.HttpStatusCode.OK;
            handler.RetryAfterSeconds = null;

            var recovered = await cache.GetAsync(source).WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(File.Exists(recovered.Path));
            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task CrossKeyMissesAreSingleFlightAndPacedWhileWarmHitsBypassGate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var clock = new TestClock(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
            var handler = new PacedImageHandler();
            var cache = CreateCache(root, handler, clock);
            const string firstSource = "https://oldschool.runescape.wiki/images/Paced-first.png";
            const string secondSource = "https://oldschool.runescape.wiki/images/Paced-second.png";
            const string thirdSource = "https://oldschool.runescape.wiki/images/Paced-third.png";

            var first = cache.GetAsync(firstSource);
            await handler.FirstStarted.Task;
            var second = cache.GetAsync(secondSource);
            await Task.Delay(30);
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(1, handler.MaximumConcurrent);

            handler.ReleaseFirst.SetResult();
            await Task.WhenAll(first, second);
            Assert.Equal(2, handler.RequestCount);
            Assert.Equal(1, handler.MaximumConcurrent);
            Assert.Equal(2, handler.ProviderStarts.Count);
            Assert.True(handler.ProviderStarts[1] - handler.ProviderStarts[0] >= TimeSpan.FromMilliseconds(450));

            var third = cache.GetAsync(thirdSource);
            await Task.Delay(30);
            var warm = await cache.GetAsync(firstSource).WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(File.Exists(warm.Path));
            Assert.Equal(2, handler.RequestCount);
            await third;
            Assert.Equal(3, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ProviderRetryAfterPausesDifferentImageKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var clock = new TestClock(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
            var retryAt = clock.GetUtcNow().AddSeconds(1);
            var handler = new ProviderCooldownImageHandler(retryAt);
            var cache = CreateCache(root, handler, clock);
            const string firstSource = "https://oldschool.runescape.wiki/images/Rate-limited-first.png";
            const string secondSource = "https://oldschool.runescape.wiki/images/Rate-limited-second.png";

            await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetAsync(firstSource));
            var second = cache.GetAsync(secondSource);
            await Task.Delay(100);
            Assert.Equal(1, handler.RequestCount);

            var recovered = await second;
            Assert.True(File.Exists(recovered.Path));
            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MissingImageNegativeCooldownDoesNotPauseDifferentImageKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var clock = new TestClock(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
            var handler = new CountingImageHandler { Status = System.Net.HttpStatusCode.NotFound };
            var cache = CreateCache(root, handler, clock);
            const string missingSource = "https://oldschool.runescape.wiki/images/Missing.png";
            const string availableSource = "https://oldschool.runescape.wiki/images/Available.png";

            await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetAsync(missingSource));
            handler.Status = System.Net.HttpStatusCode.OK;
            var available = await cache.GetAsync(availableSource).WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(File.Exists(available.Path));
            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task FailedImageIsNegativeCachedUntilRetryAfterCooldown()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var clock = new TestClock(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
            var handler = new CountingImageHandler { Status = System.Net.HttpStatusCode.TooManyRequests, RetryAfterSeconds = 45 };
            var cache = CreateCache(root, handler, clock);
            const string source = "https://oldschool.runescape.wiki/images/Unavailable.png";

            await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetAsync(source));
            await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetAsync(source));
            Assert.Equal(1, handler.RequestCount);

            clock.Advance(TimeSpan.FromSeconds(44));
            await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetAsync(source));
            Assert.Equal(1, handler.RequestCount);

            clock.Advance(TimeSpan.FromSeconds(1));
            handler.Status = System.Net.HttpStatusCode.OK;
            handler.RetryAfterSeconds = null;
            var recovered = await cache.GetAsync(source);
            Assert.True(File.Exists(recovered.Path));
            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PublicUrlProxiesOnlyOsrsWikiImages()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var cache = CreateCache(root, new CountingImageHandler());

            var wikiUrl = cache.GetPublicUrl("https://oldschool.runescape.wiki/w/File:Hydra.png");
            var catalogueUrl = cache.GetPublicUrl("https://oldschool.runescape.wiki/Special:Redirect/file/Nex.png");
            var externalUrl = cache.GetPublicUrl("https://example.com/hydra.png");

            Assert.StartsWith(OsrsWikiImageCache.EndpointPath + "?source=", wikiUrl, StringComparison.Ordinal);
            Assert.Contains("Special%3ARedirect", wikiUrl, StringComparison.Ordinal);
            Assert.StartsWith(OsrsWikiImageCache.EndpointPath + "?source=", catalogueUrl, StringComparison.Ordinal);
            Assert.Equal("https://example.com/hydra.png", externalUrl);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static OsrsWikiImageCache CreateCache(string root, HttpMessageHandler handler, TimeProvider? time = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CatalogueImageCache:LocalPath"] = root
        }).Build();
        return new OsrsWikiImageCache(
            new SingleClientFactory(new HttpClient(handler)),
            configuration,
            new TestEnvironment(root),
            NullLogger<OsrsWikiImageCache>.Instance,
            time);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CountingImageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public System.Net.HttpStatusCode Status { get; set; } = System.Net.HttpStatusCode.OK;
        public int? RetryAfterSeconds { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(Status)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([1, 2, 3, 4])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            if (RetryAfterSeconds is { } seconds) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            return Task.FromResult(response);
        }
    }

    private sealed class BlockingImageHandler(TaskCompletionSource started, TaskCompletionSource release) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public System.Net.HttpStatusCode Status { get; set; } = System.Net.HttpStatusCode.OK;
        public int? RetryAfterSeconds { get; set; }
        public TaskCompletionSource ResponseReturned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            started.TrySetResult();
            await release.Task;
            var response = new HttpResponseMessage(Status)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([1, 2, 3, 4])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            if (RetryAfterSeconds is { } seconds) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
            ResponseReturned.TrySetResult();
            return response;
        }
    }

    private sealed class ProviderCooldownImageHandler(DateTimeOffset retryAt) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(RequestCount == 1
                ? System.Net.HttpStatusCode.ServiceUnavailable
                : System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([1, 2, 3, 4])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            if (RequestCount == 1) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAt);
            return Task.FromResult(response);
        }
    }

    private sealed class PacedImageHandler : HttpMessageHandler
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private int active;
        private int requests;
        private int maximumConcurrent;

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<TimeSpan> ProviderStarts { get; } = [];
        public int RequestCount => Volatile.Read(ref requests);
        public int MaximumConcurrent => Volatile.Read(ref maximumConcurrent);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref requests);
            lock (ProviderStarts) ProviderStarts.Add(stopwatch.Elapsed);
            var current = Interlocked.Increment(ref active);
            while (current > Volatile.Read(ref maximumConcurrent) && Interlocked.CompareExchange(ref maximumConcurrent, current, Volatile.Read(ref maximumConcurrent)) != Volatile.Read(ref maximumConcurrent)) { }
            try
            {
                if (requestNumber == 1)
                {
                    FirstStarted.SetResult();
                    await ReleaseFirst.Task;
                }

                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new ByteArrayContent([1, 2, 3, 4])
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                return response;
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset value = now;
        public override DateTimeOffset GetUtcNow() => value;
        public void Advance(TimeSpan amount) => value += amount;
    }

    private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Bingo.BrowserTests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = contentRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
