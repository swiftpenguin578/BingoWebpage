using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Catalogue;

namespace Bingo.Web.Catalogue;

public sealed partial class OsrsWikiImageCache
{
    public const string EndpointPath = "/media/osrs-wiki";
    private const string WikiHost = "oldschool.runescape.wiki";
    private const long MaximumBytes = 8 * 1024 * 1024;
    private const int MaximumConcurrentDownloads = 1;
    private static readonly TimeSpan DownloadPacing = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DefaultFailureCooldown = TimeSpan.FromSeconds(30);
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<OsrsWikiImageCache> logger;
    private readonly string root;
    private readonly TimeProvider time;
    private readonly ConcurrentDictionary<string, Lazy<Task<CachedWikiImage>>> downloads = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Failure> failures = new(StringComparer.Ordinal);
    private readonly ImageDownloadState downloadState;
    // Production registers TimeProvider.System as one singleton, so all cache
    // instances share this one-download policy. Separate clocks keep tests
    // deterministic without mixing unrelated time domains.
    private static readonly ConditionalWeakTable<TimeProvider, ImageDownloadState> SharedDownloadStates = new();

    public OsrsWikiImageCache(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<OsrsWikiImageCache> logger,
        TimeProvider? time = null)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        this.time = time ?? TimeProvider.System;
        downloadState = SharedDownloadStates.GetValue(this.time, static _ => new());
        var configuredPath = configuration["CatalogueImageCache:LocalPath"] ?? "data/catalogue-images";
        root = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath));
        Directory.CreateDirectory(root);
    }

    public string? GetPublicUrl(string? source)
    {
        var normalized = OsrsWikiImageUrl.Normalize(source);
        return TryGetWikiUri(normalized, out _)
            ? QueryString.Create("source", normalized!).ToUriComponent().Insert(0, EndpointPath)
            : normalized;
    }

    public async Task<CachedWikiImage> GetAsync(string source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = OsrsWikiImageUrl.Normalize(source);
        if (!TryGetWikiUri(normalized, out var sourceUri))
            throw new InvalidOperationException("Only HTTPS images hosted by the OSRS Wiki can be cached.");

        var key = CacheKey(normalized!);
        var existing = FindExisting(key);
        if (existing is not null)
        {
            failures.TryRemove(key, out _);
            return existing;
        }

        if (failures.TryGetValue(key, out var failure) && failure.RetryAt > time.GetUtcNow())
            throw failure.CreateException();

        var sharedDownload = downloads.GetOrAdd(key, _ => new Lazy<Task<CachedWikiImage>>(
            () => DownloadAndRememberAsync(key, normalized!, sourceUri),
            LazyThreadSafetyMode.ExecutionAndPublication));
        var download = sharedDownload.Value;
        _ = download.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                downloads.TryRemove(new KeyValuePair<string, Lazy<Task<CachedWikiImage>>>(key, sharedDownload));
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return await download.WaitAsync(cancellationToken);
    }

    public bool IsCached(string source)
    {
        var normalized = OsrsWikiImageUrl.Normalize(source);
        return TryGetWikiUri(normalized, out _) && FindExisting(CacheKey(normalized!)) is not null;
    }

    private CachedWikiImage? FindExisting(string key)
    {
        foreach (var pair in Extensions)
        {
            var path = Path.Combine(root, key + pair.Value);
            if (File.Exists(path)) return new CachedWikiImage(path, pair.Key);
        }

        return null;
    }

    private async Task<CachedWikiImage> DownloadAndRememberAsync(string key, string normalized, Uri sourceUri)
    {
        await WaitForDownloadSlotAsync();
        try
        {
            try
            {
                var cached = await DownloadAsync(key, sourceUri);
                failures.TryRemove(key, out _);
                return cached;
            }
            // The shared download never uses an individual caller's token. A
            // cancellation here therefore belongs to the provider request itself
            // (for example the configured HttpClient timeout) and is safe to
            // negative-cache with other failed fetches.
            catch (Exception exception)
            {
                var failureException = exception is OperationCanceledException
                    ? new HttpRequestException("The OSRS Wiki image request timed out.", exception)
                    : exception;
                var retryAt = failureException.Data[RetryAtDataKey] is DateTimeOffset requestedRetryAt
                    ? requestedRetryAt
                    : time.GetUtcNow().Add(DefaultFailureCooldown);
                if (failureException.Data[ProviderRetryAtDataKey] is DateTimeOffset requestedProviderRetryAt)
                    SetProviderCooldown(requestedProviderRetryAt);
                TrimFailures();
                failures[key] = new Failure(retryAt, failureException.Message, failureException is HttpRequestException http ? http.StatusCode : null);
                LogCacheFailure(logger, failureException, normalized);
                throw failureException;
            }
        }
        finally
        {
            downloadState.Gate.Release();
        }
    }

    private async Task<CachedWikiImage> DownloadAsync(string key, Uri sourceUri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        using var response = await httpClientFactory.CreateClient("OsrsWikiImages")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
        if (!response.IsSuccessStatusCode)
        {
            var failure = new HttpRequestException(
                $"The OSRS Wiki image request returned {(int)response.StatusCode}.",
                null,
                response.StatusCode);
            var retryAt = RetryAt(response, out var providerRequestedRetryAt);
            failure.Data[RetryAtDataKey] = retryAt;
            if (providerRequestedRetryAt) failure.Data[ProviderRetryAtDataKey] = retryAt;
            throw failure;
        }

        if (response.RequestMessage?.RequestUri is not Uri finalUri ||
            !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(finalUri.Host, WikiHost, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The OSRS Wiki image redirected to an unapproved host.");

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !Extensions.TryGetValue(mediaType, out var extension))
            throw new InvalidOperationException($"The OSRS Wiki returned unsupported media type '{mediaType ?? "unknown"}'.");
        if (response.Content.Headers.ContentLength > MaximumBytes)
            throw new InvalidOperationException("The OSRS Wiki image exceeds the configured size limit.");

        var finalPath = Path.Combine(root, key + extension);
        var temporaryPath = Path.Combine(root, $".{key}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(CancellationToken.None);
            await using var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer, CancellationToken.None);
                if (read == 0) break;
                total += read;
                if (total > MaximumBytes) throw new InvalidOperationException("The OSRS Wiki image exceeds the configured size limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), CancellationToken.None);
            }

            await output.FlushAsync(CancellationToken.None);
            File.Move(temporaryPath, finalPath, false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        return new CachedWikiImage(finalPath, mediaType);
    }

    private async Task WaitForDownloadSlotAsync()
    {
        await downloadState.Gate.WaitAsync(CancellationToken.None);
        try
        {
            TimeSpan delay;
            lock (downloadState.Sync)
            {
                var now = time.GetUtcNow();
                var start = downloadState.NextDownloadAt > now ? downloadState.NextDownloadAt : now;
                if (downloadState.ProviderCooldownUntil > start)
                    start = downloadState.ProviderCooldownUntil;
                delay = start - now;
                downloadState.NextDownloadAt = start.Add(DownloadPacing);
            }

            if (delay > TimeSpan.Zero) await Task.Delay(delay, CancellationToken.None);
        }
        catch
        {
            downloadState.Gate.Release();
            throw;
        }
    }

    private void SetProviderCooldown(DateTimeOffset retryAt)
    {
        lock (downloadState.Sync)
        {
            if (retryAt > downloadState.ProviderCooldownUntil)
                downloadState.ProviderCooldownUntil = retryAt;
        }
    }

    private DateTimeOffset RetryAt(HttpResponseMessage response, out bool providerRequested)
    {
        var now = time.GetUtcNow();
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            providerRequested = true;
            return SafeAdd(now, delta);
        }
        if (retryAfter?.Date is { } date && date > now)
        {
            providerRequested = true;
            return date;
        }
        providerRequested = false;
        return SafeAdd(now, DefaultFailureCooldown);
    }

    private void TrimFailures()
    {
        if (failures.Count < 4096) return;
        var now = time.GetUtcNow();
        foreach (var entry in failures.Where(x => x.Value.RetryAt <= now).ToArray())
            failures.TryRemove(entry.Key, out _);
        if (failures.Count >= 4096 && failures.Keys.FirstOrDefault() is { } oldest)
            failures.TryRemove(oldest, out _);
    }

    private const string RetryAtDataKey = "Bingo.Web.Catalogue.OsrsWikiImageCache.RetryAt";
    private const string ProviderRetryAtDataKey = "Bingo.Web.Catalogue.OsrsWikiImageCache.ProviderRetryAt";

    private static DateTimeOffset SafeAdd(DateTimeOffset value, TimeSpan amount) =>
        amount >= DateTimeOffset.MaxValue - value ? DateTimeOffset.MaxValue : value.Add(amount);

    private static string CacheKey(string source) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

    private static bool TryGetWikiUri(string? source, out Uri uri)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var parsed) &&
            string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parsed.Host, WikiHost, StringComparison.OrdinalIgnoreCase) &&
            (parsed.AbsolutePath.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
             parsed.AbsolutePath.StartsWith("/Special:Redirect/file/", StringComparison.OrdinalIgnoreCase) ||
             parsed.AbsolutePath.StartsWith("/w/Special:Redirect/file/", StringComparison.OrdinalIgnoreCase)))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    [LoggerMessage(1, LogLevel.Warning, "Could not cache OSRS Wiki image {Source}")]
    private static partial void LogCacheFailure(ILogger logger, Exception exception, string source);

    private sealed record Failure(DateTimeOffset RetryAt, string Message, HttpStatusCode? StatusCode)
    {
        public HttpRequestException CreateException() => new(Message, null, StatusCode);
    }

    private sealed class ImageDownloadState
    {
        public SemaphoreSlim Gate { get; } = new(MaximumConcurrentDownloads, MaximumConcurrentDownloads);
        public object Sync { get; } = new();
        public DateTimeOffset NextDownloadAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset ProviderCooldownUntil { get; set; } = DateTimeOffset.MinValue;
    }

}

public sealed record CachedWikiImage(string Path, string MediaType);
