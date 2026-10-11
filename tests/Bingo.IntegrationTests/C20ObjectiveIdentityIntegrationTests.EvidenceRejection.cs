using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Evidence;
using Bingo.Infrastructure.Evidence;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bingo.IntegrationTests;

// Brief 170 / M2 + S-2 + S-3: unreadable uploads posted by a player through the real
// Submit drawer and Submission correction pages get the specific evidence refusal in
// the request language, never a 500 or the generic failure, are not logged as
// unexpected (4101), and nothing reaches evidence storage. Uses the real validator
// via LocalEvidenceStorage.
public sealed partial class C20ObjectiveIdentityIntegrationTests
{
    private const string UnsupportedEnglish = "Evidence must be a PNG, JPEG, or WebP image.";
    private const string UnsupportedDanish = "Beviset skal være et PNG-, JPEG- eller WebP-billede.";

    [Theory]
    [InlineData("truncated-png", "en", UnsupportedEnglish)]
    [InlineData("corrupt-png", "en", UnsupportedEnglish)]
    [InlineData("text", "en", UnsupportedEnglish)]
    [InlineData("text", "da", UnsupportedDanish)]
    [InlineData("lossless-jpeg", "en", UnsupportedEnglish)]
    [InlineData("fuzzed-webp-argument", "en", UnsupportedEnglish)]
    [InlineData("fuzzed-webp-index", "da", UnsupportedDanish)]
    [InlineData("oversized", "en", "The evidence image dimensions are too large.")]
    [InlineData("oversized", "da", "Billedets dimensioner er for store.")]
    public async Task UnreadableEvidenceIsRefusedOnTheSubmitDrawerWithoutStoring(string kind, string culture, string expected)
    {
        var f = await SeedAsync();
        var root = Path.Combine(Path.GetTempPath(), $"bingo-evidence-http-{Guid.NewGuid():N}");
        var logs = new UnexpectedFailureLog();
        try
        {
            await using var realStorage = RealStorageFactory(root, logs);
            using var player = await RealStorageClientAsync(realStorage, f.Owner);
            player.DefaultRequestHeaders.Add("Accept-Language", culture);
            var path = $"/Captain/Submit/{f.Tile.Id}?handler=Drawer&eventId={f.Event.Id}&teamId={f.Team.Id}";
            var html = await player.GetStringAsync(path);
            using var content = new MultipartFormDataContent();
            foreach (var pair in new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(html), ["Input.TileId"] = f.Tile.Id.ToString(), ["Input.RequirementId"] = f.Requirement.Id.ToString(), ["Input.DropSnapshotId"] = f.Drop.Id.ToString(), ["Input.CreditedParticipantId"] = f.Participant.Id.ToString(), ["Input.ClaimedWeight"] = "1" })
                content.Add(new StringContent(pair.Value), pair.Key);
            content.Add(new ByteArrayContent(Upload(kind)), "Input.Evidence", "proof.png");

            using var response = await player.PostAsync(path, content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.False(body.StartsWith('{'), body);
            Assert.Contains(expected, body, StringComparison.Ordinal);
            Assert.DoesNotContain(GenericFailureEnglish, body, StringComparison.Ordinal);
            Assert.Equal(0, logs.Count);
            await using var db = fixture.Db();
            Assert.False(await db.Submissions.AnyAsync(x => x.EventId == f.Event.Id));
            Assert.False(await db.EvidenceAssets.AnyAsync());
            Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("truncated-png", "en", UnsupportedEnglish)]
    [InlineData("text", "da", UnsupportedDanish)]
    public async Task UnreadableEvidenceIsRefusedOnTheSubmissionCorrectionWithoutStoring(string kind, string culture, string expected)
    {
        var f = await SeedAsync();
        Guid submissionId;
        using (var seeded = await ClientAsync(f.Owner)) submissionId = await SubmitAsync(seeded, f);
        var root = Path.Combine(Path.GetTempPath(), $"bingo-evidence-http-{Guid.NewGuid():N}");
        try
        {
            var logs = new UnexpectedFailureLog();
            await using var realStorage = RealStorageFactory(root, logs);
            using var player = await RealStorageClientAsync(realStorage, f.Owner);
            player.DefaultRequestHeaders.Add("Accept-Language", culture);
            await using var before = fixture.Db();
            var original = await before.Submissions.AsNoTracking().SingleAsync(x => x.Id == submissionId);
            var assetsBefore = await before.EvidenceAssets.CountAsync();
            var path = $"/Submissions/{submissionId}?eventId={f.Event.Id}&teamId={f.Team.Id}";
            var html = await player.GetStringAsync(path);
            using var content = new MultipartFormDataContent();
            foreach (var field in new[] { "Input.ExpectedVersion", "Input.RequirementId", "Input.DropSnapshotId", "Input.CreditedParticipantId", "Input.ClaimedWeight" })
                content.Add(new StringContent(FieldValue(html, field)), field);
            content.Add(new StringContent(Token(html)), "__RequestVerificationToken");
            content.Add(new StringContent(f.Tile.Id.ToString()), "Input.BoardTileId");
            content.Add(new StringContent("Corrected note"), "Input.Note");
            content.Add(new ByteArrayContent(Upload(kind)), "Input.Evidence", "proof.png");

            using var response = await player.PostAsync($"/Submissions/{submissionId}?handler=Correct&eventId={f.Event.Id}&teamId={f.Team.Id}", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains(expected, body, StringComparison.Ordinal);
            Assert.DoesNotContain(GenericFailureEnglish, body, StringComparison.Ordinal);
            Assert.Equal(0, logs.Count);
            await using var after = fixture.Db();
            var current = await after.Submissions.AsNoTracking().SingleAsync(x => x.Id == submissionId);
            Assert.Equal(original.Version, current.Version);
            Assert.Equal(original.CaptainNote, current.CaptainNote);
            Assert.Equal(assetsBefore, await after.EvidenceAssets.CountAsync());
            Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private const string GenericFailureEnglish = "We could not complete that request. Please try again or contact an administrator.";

    // A failure outside the listed refusals still gets the generic text and the 4101 log.
    [Fact]
    public void UnlistedFailureKeepsTheGenericMessageAndUnexpectedLog()
    {
        var logs = new UnexpectedFailureLog();
        using var loggers = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var text = new StringLocalizer<SharedResource>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));

        var listed = SafeUserFailure.Message(text, loggers.CreateLogger("test"), new InvalidOperationException(UnsupportedEnglish));
        Assert.Equal(0, logs.Count);
        var unlisted = SafeUserFailure.Message(text, loggers.CreateLogger("test"), new InvalidOperationException("Evidence storage is unreachable."));

        Assert.Equal(UnsupportedEnglish, listed);
        Assert.Equal(GenericFailureEnglish, unlisted);
        Assert.Equal(1, logs.Count);
    }

    private static byte[] Upload(string kind) => kind == "oversized"
        // 8000x8000 = 64M pixels, over the 50M limit, rejected from the header.
        ? EvidenceImageDecodeLimitTests.CraftZeroFilledRgbaPng(8_000, 8_000)
        : EvidenceImageDecodeLimitTests.UnreadableUpload(kind);

    private WebApplicationFactory<Program> RealStorageFactory(string root, UnexpectedFailureLog logs)
    {
        var storage = new LocalEvidenceStorage(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EvidenceStorage:LocalPath"] = root }).Build());
        return fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(logs);
            services.RemoveAll<IEvidenceStorage>();
            services.AddSingleton<IEvidenceStorage>(storage);
        }));
    }

    // Counts SafeUserFailure's "unexpected failure" entries (event id 4101).
    private sealed class UnexpectedFailureLog : ILoggerProvider, ILogger
    {
        private int count;
        public int Count => Volatile.Read(ref count);
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Id == 4101) Interlocked.Increment(ref count);
        }
        public void Dispose() { }
    }

    private static async Task<HttpClient> RealStorageClientAsync(WebApplicationFactory<Program> factory, Bingo.Domain.Access.Account account)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var html = await client.GetStringAsync("/Account/Login");
        using var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = account.LoginName, ["Input.Password"] = "password", ["__RequestVerificationToken"] = Token(html) }));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        return client;
    }

    private static string FieldValue(string html, string name)
    {
        var input = Regex.Match(html, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>").Value;
        Assert.NotEmpty(input);
        return WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value);
    }
}
