using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

// U3 / DP:966–971 / C4: these existing operations now belong to SignupSetup;
// terminal mutation/refusal, replay, version and data-integrity assertions are retained.

public sealed partial class SignupQuestionCreationRetryIntegrationTests
{
    [Theory]
    [InlineData(EventState.Cancelled, false)]
    [InlineData(EventState.Cancelled, true)]
    [InlineData(EventState.Finalized, false)]
    [InlineData(EventState.Finalized, true)]
    [InlineData(EventState.Archived, false)]
    [InlineData(EventState.Archived, true)]
    public async Task TerminalQuestionsAllowOnlyExactCommittedAddReplay(EventState terminal, bool account)
    {
        var seed = await SeedAsync();
        var other = await SeedAsync();
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(client, seed.Admin.LoginName);
        var route = $"/Admin/Events/SignupSetup/{seed.EventId}";
        var page = await client.GetStringAsync(route);
        var fields = account ? new Dictionary<string, string> { ["role"] = "Playing" }
            : new() { ["Input.Label"] = "Terminal retry", ["Input.Type"] = "Text" };
        fields["addRequestId"] = Guid.NewGuid().ToString();
        fields["expectedFormVersion"] = seed.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields["__RequestVerificationToken"] = Token(page);
        var postRoute = account ? route + "?handler=AddAccount" : route;
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var created = await PostAsync(client, postRoute, fields);
        Assert.True(created.Succeeded, created.Error);
        Assert.False(created.Replayed);
        await using (var db = new ApplicationDbContext(options))
        {
            foreach (var item in await db.Events.Where(x => x.Id == seed.EventId || x.Id == other.EventId).ToListAsync())
            {
                if (terminal == EventState.Cancelled) item.Cancel(seed.Admin.Id, Now, "Controlled terminal retry", protectedHistoryExists: true);
                else db.Entry(item).Property(x => x.State).CurrentValue = terminal;
            }
            await db.SaveChangesAsync();
        }
        var before = await SnapshotAsync();
        var replay = await PostAsync(client, postRoute, fields);
        Assert.Equal(created with { Replayed = true }, replay);
        Assert.Equal(before, await SnapshotAsync());

        foreach (var invalid in new[] {
            new Dictionary<string, string>(fields) { ["addRequestId"] = Guid.NewGuid().ToString() },
            new Dictionary<string, string>(fields) { ["addRequestId"] = "invalid-guid" },
            new Dictionary<string, string>(fields) { ["expectedFormVersion"] = "" },
            new Dictionary<string, string>(fields) { ["expectedFormVersion"] = created.FormVersion!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            new Dictionary<string, string>(fields) { [account ? "role" : "Input.Label"] = account ? "Informational" : "Different intent" }
        })
        {
            await AssertTerminalReadOnlyAsync(client, seed.EventId, postRoute, invalid);
            Assert.Equal(before, await SnapshotAsync());
        }
        await AssertTerminalReadOnlyAsync(client, seed.EventId, route + "?handler=UnknownMutation", fields);
        Assert.Equal(before, await SnapshotAsync());
        var otherRoute = $"/Admin/Events/SignupSetup/{other.EventId}" + (account ? "?handler=AddAccount" : "");
        await AssertTerminalReadOnlyAsync(client, other.EventId, otherRoute, fields);
        Assert.Equal(before, await SnapshotAsync());

        using var otherClient = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(otherClient, other.Admin.LoginName);
        fields["__RequestVerificationToken"] = Token(await otherClient.GetStringAsync($"/Admin/Events/Manage/{seed.EventId}"));
        otherClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        before = await SnapshotAsync();
        await AssertTerminalReadOnlyAsync(otherClient, seed.EventId, postRoute, fields);
        Assert.Equal(before, await SnapshotAsync());
        await AssertCountsAsync(seed, 1);
    }

    private static async Task AssertTerminalReadOnlyAsync(HttpClient client, Guid eventId, string route, Dictionary<string, string> fields)
    {
        using var response = await client.PostAsync(route, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Admin/Events/Manage/{eventId}", response.Headers.Location!.OriginalString);
        var page = await client.GetStringAsync(response.Headers.Location.OriginalString);
        // U4 / OS-1: Manage (Overview) renders on the design layout; its toast carries the refusal.
        var message = Regex.Match(page, "data-toast[^>]*>.*?<span class=\"grow\" data-component-text>(.*?)</span>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal("This event is read-only in its current lifecycle state.", WebUtility.HtmlDecode(message));
    }
}
