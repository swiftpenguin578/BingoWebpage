using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Evidence;
using Bingo.Infrastructure.Evidence;
using Bingo.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bingo.IntegrationTests;

// Brief 170 / M2 + S-2: unreadable uploads posted by a player through the real Submit
// drawer and Submission correction pages get the ordinary refusal, never a 500, and
// nothing reaches evidence storage. Uses the real validator via LocalEvidenceStorage.
public sealed partial class C20ObjectiveIdentityIntegrationTests
{
    private const string GenericRefusal = "We could not complete that request. Please try again or contact an administrator.";

    [Theory]
    [InlineData("truncated-png")]
    [InlineData("corrupt-png")]
    [InlineData("text")]
    public async Task UnreadableEvidenceIsRefusedOnTheSubmitDrawerWithoutStoring(string kind)
    {
        var f = await SeedAsync();
        var root = Path.Combine(Path.GetTempPath(), $"bingo-evidence-http-{Guid.NewGuid():N}");
        try
        {
            await using var realStorage = RealStorageFactory(root);
            using var player = await RealStorageClientAsync(realStorage, f.Owner);
            var path = $"/Captain/Submit/{f.Tile.Id}?handler=Drawer&eventId={f.Event.Id}&teamId={f.Team.Id}";
            var html = await player.GetStringAsync(path);
            using var content = new MultipartFormDataContent();
            foreach (var pair in new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(html), ["Input.TileId"] = f.Tile.Id.ToString(), ["Input.RequirementId"] = f.Requirement.Id.ToString(), ["Input.DropSnapshotId"] = f.Drop.Id.ToString(), ["Input.CreditedParticipantId"] = f.Participant.Id.ToString(), ["Input.ClaimedWeight"] = "1" })
                content.Add(new StringContent(pair.Value), pair.Key);
            content.Add(new ByteArrayContent(EvidenceImageDecodeLimitTests.UnreadableUpload(kind)), "Input.Evidence", "proof.png");

            using var response = await player.PostAsync(path, content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.False(body.StartsWith('{'), body);
            Assert.Contains(GenericRefusal, body, StringComparison.Ordinal);
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
    [InlineData("truncated-png")]
    [InlineData("text")]
    public async Task UnreadableEvidenceIsRefusedOnTheSubmissionCorrectionWithoutStoring(string kind)
    {
        var f = await SeedAsync();
        Guid submissionId;
        using (var seeded = await ClientAsync(f.Owner)) submissionId = await SubmitAsync(seeded, f);
        var root = Path.Combine(Path.GetTempPath(), $"bingo-evidence-http-{Guid.NewGuid():N}");
        try
        {
            await using var realStorage = RealStorageFactory(root);
            using var player = await RealStorageClientAsync(realStorage, f.Owner);
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
            content.Add(new ByteArrayContent(EvidenceImageDecodeLimitTests.UnreadableUpload(kind)), "Input.Evidence", "proof.png");

            using var response = await player.PostAsync($"/Submissions/{submissionId}?handler=Correct&eventId={f.Event.Id}&teamId={f.Team.Id}", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(GenericRefusal, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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

    private WebApplicationFactory<Program> RealStorageFactory(string root)
    {
        var storage = new LocalEvidenceStorage(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EvidenceStorage:LocalPath"] = root }).Build());
        return fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEvidenceStorage>();
            services.AddSingleton<IEvidenceStorage>(storage);
        }));
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
