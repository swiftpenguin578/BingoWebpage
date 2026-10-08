using System.Net;
using System.Net.Http.Json;
using Bingo.Domain.Events;

namespace Bingo.IntegrationTests;

public sealed partial class IdentityFieldConflictIntegrationTests
{
    [Theory]
    [InlineData("applied-lost-response")]
    [InlineData("unapplied")]
    [InlineData("other-admin-matching")]
    [InlineData("other-admin-different")]
    [InlineData("unseen-disjoint-merge")]
    [InlineData("use-current")]
    public async Task IdentityReadbackObservesFullCurrentTupleWithoutAttributionOrWrites(string scenario)
    {
        using var first = await ClientAsync("first-admin");
        using var second = await ClientAsync("second-admin");
        var draft = Fields(await first.GetStringAsync(Route));
        draft["Input.Name"] = "Intended";
        draft["Input.Description"] = new string('D', 4_000);
        draft["Input.BuyInDescription"] = new string('B', 2_000);
        var expected = new EventIdentityValues("Intended", draft["Input.Description"], draft["Input.BuyInDescription"], "UTC");
        if (scenario == "use-current")
        {
            var theirs = Fields(await second.GetStringAsync(Route)); theirs["Input.Name"] = "Reviewed name";
            using var theirSave = await second.PostAsync(Route, new FormUrlEncodedContent(theirs));
            Assert.Equal(HttpStatusCode.Redirect, theirSave.StatusCode);
            draft = Fields(await PostPageAsync(first, draft));
            draft["Input.NameResolution"] = "UseCurrent";
            expected = expected with { Name = "Reviewed name" };
            Assert.Equal("Intended", draft["Input.Name"]); // Original intent stays separate.
        }
        if (scenario == "unseen-disjoint-merge")
        {
            // The client observed UTC at dispatch. AU08 correctly retains this unseen disjoint edit.
            var theirs = Fields(await second.GetStringAsync(Route)); theirs["Input.Timezone"] = "Europe/Copenhagen";
            using var theirSave = await second.PostAsync(Route, new FormUrlEncodedContent(theirs));
            Assert.Equal(HttpStatusCode.Redirect, theirSave.StatusCode);
        }
        if (scenario is "applied-lost-response" or "unseen-disjoint-merge" or "use-current")
        {
            using var lostResponse = await first.PostAsync(Route, new FormUrlEncodedContent(draft));
            Assert.Equal(HttpStatusCode.Redirect, lostResponse.StatusCode);
            // No response content or success state is used by the following reconciliation.
        }
        if (scenario.StartsWith("other-admin", StringComparison.Ordinal))
        {
            var theirs = Fields(await second.GetStringAsync(Route));
            theirs["Input.Name"] = expected.Name; theirs["Input.Description"] = expected.Description!;
            theirs["Input.BuyInDescription"] = scenario == "other-admin-matching" ? expected.BuyInDescription! : "Different";
            using var theirSave = await second.PostAsync(Route, new FormUrlEncodedContent(theirs));
            Assert.Equal(HttpStatusCode.Redirect, theirSave.StatusCode);
        }
        var before = await ReadAsync(); var audits = await AuditCountAsync();
        for (var repeat = 0; repeat < 2; repeat++)
        {
            using var read = await first.GetAsync(Route + "?handler=Current");
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.True(read.Headers.CacheControl!.NoStore);
            var json = await read.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("eventId,values,version", string.Join(",", json.EnumerateObject().Select(property => property.Name).Order())); // No receipts, secrets or save attribution.
            Assert.Equal(before.Version, json.GetProperty("version").GetInt64());
            Assert.Equal(eventId, json.GetProperty("eventId").GetGuid());
            var values = json.GetProperty("values"); Assert.Equal(4, values.EnumerateObject().Count());
            var current = new EventIdentityValues(values.GetProperty("name").GetString()!, values.GetProperty("description").GetString(),
                values.GetProperty("buyInDescription").GetString(), values.GetProperty("timezone").GetString()!);
            Assert.Equal(scenario is "applied-lost-response" or "other-admin-matching" or "use-current", current == expected);
            Assert.Equal(before.Name, current.Name); Assert.Equal(before.Description, current.Description);
            Assert.Equal(before.BuyInDescription, current.BuyInDescription); Assert.Equal(before.Timezone, current.Timezone);
        }
        Assert.Equal(before.Version, (await ReadAsync()).Version); Assert.Equal(audits, await AuditCountAsync());
    }

    [Fact]
    public async Task IdentityReadbackUsesExistingAuthorizationAndHiddenEventBoundary()
    {
        using var ordinary = await ClientAsync("ordinary-account");
        using var denied = await ordinary.GetAsync(Route + "?handler=Current");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode); Assert.Contains("AccessDenied", denied.Headers.Location!.ToString());
        using var admin = await ClientAsync("first-admin");
        await EditAsync(item => { item.OpenSignups(Now); item.CloseSignups(Now); item.StartEvent(Now); item.EndEvent(Now.AddHours(1)); item.Hide(item.CreatedByAccountId, Now.AddHours(2), null, "Synthetic visibility test"); });
        using var hidden = await admin.GetAsync(Route + "?handler=Current");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); Assert.Equal(0, await AuditCountAsync());
    }
}
