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

    private static OsrsWikiImageCache CreateCache(string root, HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CatalogueImageCache:LocalPath"] = root
        }).Build();
        return new OsrsWikiImageCache(
            new SingleClientFactory(new HttpClient(handler)),
            configuration,
            new TestEnvironment(root),
            NullLogger<OsrsWikiImageCache>.Instance);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CountingImageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([1, 2, 3, 4])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        }
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
