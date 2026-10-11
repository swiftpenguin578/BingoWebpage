using System.Diagnostics;
using Bingo.Web.Catalogue;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bingo.BrowserTests;

public sealed class OsrsWikiImageCacheTests
{
    [Theory]
    [InlineData("https://oldschool.runescape.wiki/images/Test_image.png?123")]
    [InlineData("https://oldschool.runescape.wiki/Special:Redirect/file/Unsired_detail.png")]
    public async Task WikiImagesPreferHttp2AndAreDownloadedOnceAndServedFromPersistentCache(string source)
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var handler = new CountingImageHandler();
            var cache = CreateCache(root, handler);

            var first = await cache.GetAsync(source);
            var second = await cache.GetAsync(source);

            Assert.Equal("image/png", first.MediaType);
            Assert.Equal(first.Path, second.Path);
            Assert.True(File.Exists(first.Path));
            Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(first.Path));
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(System.Net.HttpVersion.Version20, handler.RequestVersion);
            Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, handler.RequestVersionPolicy);
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

            // Observe the existing shared task after both original waiters have
            // cancelled. A handler response is not the cache's failure completion:
            // recording RetryAt and removing the in-flight entry happen afterwards.
            var completion = cache.GetAsync(source);
            release.SetResult();
            var failure = await Assert.ThrowsAsync<HttpRequestException>(() => completion);
            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, failure.StatusCode);
            Assert.Equal(1, handler.RequestCount);
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

    // Brief 167 / F02 and brief 170 / M1: fragment, query and encoding variants of one
    // wiki file share one cache key and one fetch of the canonical file URL, for both
    // the /images/ and the Special:Redirect forms.
    [Theory]
    [InlineData("https://oldschool.runescape.wiki/images/Abyssal_whip.png", new[]
    {
        "https://oldschool.runescape.wiki/images/Abyssal_whip.png",
        "https://oldschool.runescape.wiki/images/Abyssal_whip.png#a",
        "https://oldschool.runescape.wiki/images/Abyssal_whip.png#b",
        "https://oldschool.runescape.wiki/images/Abyssal_whip.png?x=1",
        "https://OLDSCHOOL.runescape.wiki:443/images/%41byssal%5Fwhip.png?x=2#c"
    })]
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/Caf%C3%A9_whip.png", new[]
    {
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/Caf%C3%A9_whip.png",
        "https://oldschool.runescape.wiki/w/SPECIAL:REDIRECT/FILE/Caf%C3%A9_whip.png",
        "https://oldschool.runescape.wiki/special:redirect/file/Caf%C3%A9_whip.png#a",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/Caf%C3%A9%20whip.png",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/caf%C3%A9_whip.png",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/Caf%C3%A9___whip.png?x=1",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/_Caf%C3%A9%20_%20whip.png_",
        "https://oldschool.runescape.wiki/w/Special:Redirect/file/Cafe%CC%81_whip.png"
    })]
    public async Task FragmentQueryAndEncodingVariantsShareOneCanonicalKeyAndFetch(string canonical, string[] variants)
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var handler = new CountingImageHandler();
            var cache = CreateCache(root, handler);

            var paths = new List<string>();
            foreach (var variant in variants) paths.Add((await cache.GetAsync(variant)).Path);

            Assert.Single(paths.Distinct());
            Assert.Single(Directory.EnumerateFiles(root));
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(canonical, handler.RequestUris.Single().AbsoluteUri);
            Assert.All(variants, variant => Assert.Equal(canonical, OsrsWikiImageCache.CanonicalSource(variant)));
            Assert.Single(variants.Select(cache.GetPublicUrl).Distinct());
            Assert.All(variants, variant => Assert.True(cache.IsCached(variant)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    // Existing cache files stay valid: every catalogue Special:Redirect source already
    // has the canonical spelling, so its key does not change.
    [Fact]
    public void EveryCatalogueRedirectSourceCanonicalisesToItself()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var sources = new List<string>();
        void Collect(System.Text.Json.JsonElement element)
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("imageUrl") && property.Value.ValueKind == System.Text.Json.JsonValueKind.String) sources.Add(property.Value.GetString()!);
                    else Collect(property.Value);
                }
            }
            else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray()) Collect(item);
            }
        }
        Collect(document.RootElement);

        var redirects = sources.Where(source => source.Contains("/Special:Redirect/file/", StringComparison.Ordinal)).ToList();
        Assert.Equal(356, redirects.Count);
        Assert.All(redirects, source =>
            Assert.Equal(Bingo.Application.Catalogue.OsrsWikiImageUrl.Normalize(source), OsrsWikiImageCache.CanonicalSource(source)));
    }

    [Theory]
    [InlineData("https://example.com/images/Abyssal_whip.png")]
    [InlineData("http://oldschool.runescape.wiki/images/Abyssal_whip.png")]
    [InlineData("https://oldschool.runescape.wiki:8443/images/Abyssal_whip.png")]
    [InlineData("https://user@oldschool.runescape.wiki/images/Abyssal_whip.png")]
    [InlineData("https://oldschool.runescape.wiki.example.com/images/Abyssal_whip.png")]
    [InlineData("https://oldschool.runescape.wiki/api.php?action=query")]
    [InlineData("https://oldschool.runescape.wiki/images/../api.php")]
    [InlineData("https://oldschool.runescape.wiki/images/")]
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/")]
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/__%20_")]
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/Abyssal%2Fwhip.png")]
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/Abyssal%3Fwhip.png")]
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/Abyssal%23whip.png")]
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/Abyssal%0Awhip.png")]
    public async Task SourcesOutsideTheWikiImagePathsAreRejectedWithoutFetching(string source)
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var handler = new CountingImageHandler();
            var cache = CreateCache(root, handler);

            await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(source));

            Assert.Equal(0, handler.RequestCount);
            Assert.False(cache.IsCached(source));
            Assert.False(cache.GetPublicUrl(source)?.StartsWith(OsrsWikiImageCache.EndpointPath, StringComparison.Ordinal) ?? false);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task FilesCachedUnderTheOldUncanonicalKeyAreStillServedWithoutFetching()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            const string source = "https://oldschool.runescape.wiki/images/Legacy_image.png?7263b";
            var legacyKey = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)));
            var legacyPath = Path.Combine(root, legacyKey + ".png");
            await File.WriteAllBytesAsync(legacyPath, [9, 9]);
            var handler = new CountingImageHandler();
            var cache = CreateCache(root, handler);

            var cached = await cache.GetAsync(source);

            Assert.Equal(legacyPath, cached.Path);
            Assert.True(cache.IsCached(source));
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PendingMissesAreCappedAndOverflowIsRefusedWithoutQueueing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var clock = new TestClock(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
            const string warmSource = "https://oldschool.runescape.wiki/images/Warm.png";
            var warm = await CreateCache(root, new CountingImageHandler(), clock).GetAsync(warmSource);
            var handler = new BlockingImageHandler(started, release);
            var cache = CreateCache(root, handler, clock);

            var pending = new List<Task<CachedWikiImage>> { cache.GetAsync("https://oldschool.runescape.wiki/images/Queued-0.png") };
            await started.Task;
            for (var index = 1; index < OsrsWikiImageCache.MaximumPendingDownloads; index++)
                pending.Add(cache.GetAsync($"https://oldschool.runescape.wiki/images/Queued-{index}.png"));

            var stopwatch = Stopwatch.StartNew();
            await Assert.ThrowsAsync<OsrsWikiImageQueueFullException>(() => cache.GetAsync("https://oldschool.runescape.wiki/images/Overflow.png#x"));
            await Assert.ThrowsAsync<OsrsWikiImageQueueFullException>(() => cache.GetAsync("https://oldschool.runescape.wiki/images/Overflow-2.png"));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));

            // A variant of a queued key joins the existing download; cached images stay available.
            pending.Add(cache.GetAsync("https://oldschool.runescape.wiki/images/Queued-5.png#again"));
            Assert.Equal(warm.Path, (await cache.GetAsync(warmSource).WaitAsync(TimeSpan.FromSeconds(1))).Path);
            Assert.Equal(1, handler.RequestCount);
            Assert.All(pending, task => Assert.False(task.IsCompleted));

            release.SetResult();
            var first = await pending[0].WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(first.Path));
        }
        finally
        {
            release.TrySetResult();
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
        public List<Uri> RequestUris { get; } = [];
        public Version? RequestVersion { get; private set; }
        public HttpVersionPolicy? RequestVersionPolicy { get; private set; }
        public System.Net.HttpStatusCode Status { get; set; } = System.Net.HttpStatusCode.OK;
        public int? RetryAfterSeconds { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUris.Add(request.RequestUri!);
            RequestVersion = request.Version;
            RequestVersionPolicy = request.VersionPolicy;
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
