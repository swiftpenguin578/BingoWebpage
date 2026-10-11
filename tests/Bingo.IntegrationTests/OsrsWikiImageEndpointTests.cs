using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Catalogue;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

// Brief 170: the anonymous /media/osrs-wiki endpoint through the real pipeline.
public sealed class OsrsWikiImageEndpointTests(PostgreSqlTestFixture databaseFixture) : IClassFixture<PostgreSqlTestFixture>, IAsyncLifetime
{
    private const string ReferencedSource = "https://oldschool.runescape.wiki/Special:Redirect/file/Endpoint_referenced_boss.png";
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wiki_image_endpoint").WithUsername("bingo").WithPassword("wiki_image_endpoint_only"));
    private readonly string root = Path.Combine(Path.GetTempPath(), $"bingo-wiki-endpoint-{Guid.NewGuid():N}");
    private readonly WikiLog wiki = new();
    private WebApplicationFactory<Program> factory = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        Directory.CreateDirectory(root);
        await using (var db = Db())
        {
            var boss = new BossActivity(Guid.NewGuid(), "Endpoint boss", "endpoint-boss", "Boss", 1m, DateTimeOffset.UtcNow);
            boss.Update(boss.Name, boss.Category, 1m, null, null, null, DateTimeOffset.UtcNow, ReferencedSource);
            db.BossActivities.Add(boss);
            await db.SaveChangesAsync();
        }

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .UseSetting("CatalogueImageCache:LocalPath", root)
            .ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddHttpClient("OsrsWikiImages").ConfigurePrimaryHttpMessageHandler(() => new WikiHandler(wiki));
            }));
    }

    public async Task DisposeAsync()
    {
        await factory.DisposeAsync();
        await database.DisposeAsync();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public async Task ReferencedUncachedSourceIsFetchedOnceAndServedAnonymously()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var first = await client.GetAsync(OsrsWikiImageCache.PublicUrl(ReferencedSource));
        using var second = await client.GetAsync(OsrsWikiImageCache.PublicUrl(ReferencedSource));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("image/png", first.Content.Headers.ContentType?.MediaType);
        Assert.Equal(WikiLog.Body, await first.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(["https://oldschool.runescape.wiki/w/Special:Redirect/file/Endpoint_referenced_boss.png"], wiki.Requests);
        Assert.True(File.Exists(CachePath(OsrsWikiImageCache.CanonicalSource(ReferencedSource)!)));
    }

    [Theory]
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/Not_referenced_anywhere.png")]
    [InlineData("https://oldschool.runescape.wiki/images/Not_referenced_anywhere.png")]
    // A near-miss of a referenced file name is a different file.
    [InlineData("https://oldschool.runescape.wiki/w/Special:Redirect/file/Endpoint_referenced_boss_2.png")]
    public async Task UnreferencedWikiSourceIsNotFoundWithoutFetchingOrWriting(string source)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(OsrsWikiImageCache.PublicUrl(source));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(wiki.Requests);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    [Fact]
    public async Task SourceSavedAfterTheLastLoadIsFetchedOnItsFirstRequest()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var before = await client.GetAsync(OsrsWikiImageCache.PublicUrl("https://oldschool.runescape.wiki/w/Special:Redirect/file/Saved_later.png"));
        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);

        await using (var db = Db())
        {
            var item = new CatalogueItem(Guid.NewGuid(), "Saved later", "SAVED LATER");
            item.Update(item.Name, item.NormalizedName, null, null, "https://oldschool.runescape.wiki/w/Special:Redirect/file/Saved_later.png");
            db.CatalogueItems.Add(item);
            await db.SaveChangesAsync();
        }

        using var after = await client.GetAsync(OsrsWikiImageCache.PublicUrl("https://oldschool.runescape.wiki/w/Special:Redirect/file/Saved_later.png"));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Single(wiki.Requests);
    }

    [Fact]
    public async Task CachedFileIsServedEvenWhenNoLongerReferenced()
    {
        const string source = "https://oldschool.runescape.wiki/w/Special:Redirect/file/Cached_earlier.png";
        await File.WriteAllBytesAsync(CachePath(OsrsWikiImageCache.CanonicalSource(source)!), [7, 7, 7]);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(OsrsWikiImageCache.PublicUrl(source));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([7, 7, 7], await response.Content.ReadAsByteArrayAsync());
        Assert.Empty(wiki.Requests);
    }

    // Brief 170 / L1 with S-1: a file cached under a referenced source's older key is
    // served on the real PublicUrl request without a wiki fetch, and re-keyed.
    [Fact]
    public async Task LegacyCachedFileForAReferencedSourceIsServedWithoutFetching()
    {
        const string stored = "https://oldschool.runescape.wiki/images/Endpoint_legacy.png?7263b";
        await using (var db = Db())
        {
            var item = new CatalogueItem(Guid.NewGuid(), "Endpoint legacy", "ENDPOINT LEGACY");
            item.Update(item.Name, item.NormalizedName, null, null, stored);
            db.CatalogueItems.Add(item);
            await db.SaveChangesAsync();
        }
        var legacyPath = CachePath(stored);
        await File.WriteAllBytesAsync(legacyPath, [9, 9]);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(OsrsWikiImageCache.PublicUrl(stored));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([9, 9], await response.Content.ReadAsByteArrayAsync());
        Assert.Empty(wiki.Requests);
        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(CachePath("https://oldschool.runescape.wiki/images/Endpoint_legacy.png")));
    }

    private string CachePath(string canonical, string extension = ".png") =>
        Path.Combine(root, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))) + extension);

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options);

    // Records every request the cache sends to the wiki; handlers are created per client.
    private sealed class WikiLog
    {
        public static readonly byte[] Body = [1, 2, 3, 4];
        private readonly List<string> requests = [];

        public IReadOnlyList<string> Requests { get { lock (requests) return requests.ToArray(); } }

        public void Add(Uri uri) { lock (requests) requests.Add(uri.AbsoluteUri); }
    }

    private sealed class WikiHandler(WikiLog log) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            log.Add(request.RequestUri!);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(WikiLog.Body) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        }
    }
}
