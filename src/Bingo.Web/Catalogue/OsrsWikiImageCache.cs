using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Catalogue;

namespace Bingo.Web.Catalogue;

public sealed partial class OsrsWikiImageCache
{
    public const string EndpointPath = "/media/osrs-wiki";
    private const string WikiHost = "oldschool.runescape.wiki";
    private const long MaximumBytes = 8 * 1024 * 1024;
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
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.Ordinal);

    public OsrsWikiImageCache(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<OsrsWikiImageCache> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
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
        var normalized = OsrsWikiImageUrl.Normalize(source);
        if (!TryGetWikiUri(normalized, out var sourceUri))
            throw new InvalidOperationException("Only HTTPS images hosted by the OSRS Wiki can be cached.");

        var key = CacheKey(normalized!);
        var existing = FindExisting(key);
        if (existing is not null) return existing;

        var gate = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            existing = FindExisting(key);
            if (existing is not null) return existing;

            using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
            using var response = await httpClientFactory.CreateClient("OsrsWikiImages")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

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
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                var buffer = new byte[81920];
                long total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    total += read;
                    if (total > MaximumBytes) throw new InvalidOperationException("The OSRS Wiki image exceeds the configured size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                await output.FlushAsync(cancellationToken);
                File.Move(temporaryPath, finalPath, false);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }

            return new CachedWikiImage(finalPath, mediaType);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCacheFailure(logger, exception, normalized!);
            throw;
        }
        finally
        {
            gate.Release();
            locks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, gate));
        }
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
}

public sealed record CachedWikiImage(string Path, string MediaType);
