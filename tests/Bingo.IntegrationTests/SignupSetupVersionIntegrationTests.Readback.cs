using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bingo.Application.Signups;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class SignupSetupVersionIntegrationTests
{
    [Fact]
    public async Task CapacityMaximumReturnsClearFailureWithoutChangingTheSavedVersionOrAudit()
    {
        var seed = await SeedAsync(); await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false }); await LoginAsync(client, seed.Admin);
        var route = $"/Admin/Events/SignupSetup/{seed.EventId}"; var page = await client.GetStringAsync(route);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var accepted = await SettingsPostAsync(client, route + "?handler=SignupAdministration", page, new()
        {
            ["SignupAdministration.Version"] = (await EventVersionAsync(seed.EventId)).ToString(CultureInfo.InvariantCulture),
            ["SignupAdministration.ParticipantCap"] = "10000"
        });
        Assert.True(accepted.Succeeded, accepted.Error); Assert.Equal(10_000, accepted.Settings!.ParticipantCap);
        var before = await SnapshotAsync(seed.EventId);
        var rejected = await SettingsPostAsync(client, route + "?handler=SignupAdministration", page, new()
        {
            ["SignupAdministration.Version"] = accepted.Settings.EventVersion.ToString(CultureInfo.InvariantCulture),
            ["SignupAdministration.ParticipantCap"] = "10001"
        });
        Assert.False(rejected.Succeeded); Assert.Equal(BingoEvent.ParticipantCapMaximumMessage, rejected.Error);
        Assert.Equal(accepted.Settings, rejected.Settings); Assert.Equal(before, await SnapshotAsync(seed.EventId));
    }

    [Fact]
    public async Task JsonFormMutationReturnsCurrentCountForReconfirmationAndReadbackTracksExactQuestionId()
    {
        var seed = await SeedAsync(); await using var factory = Factory(); using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, seed.Admin); var route = $"/Admin/Events/SignupSetup/{seed.EventId}"; var page = await client.GetStringAsync(route);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        using var current = await client.GetFromJsonAsync<JsonDocument>(route + "?handler=Current");
        var question = current!.RootElement.GetProperty("questions").EnumerateArray().Single(q => q.GetProperty("id").GetGuid() == seed.Custom);
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(page), ["questionId"] = seed.Custom.ToString(), ["confirmed"] = "true", ["expectedAnswerCount"] = "1", ["expectedEventRegistrationReleaseCount"] = "0", ["expectedQuestionVersion"] = question.GetProperty("version").GetInt32().ToString(CultureInfo.InvariantCulture) };
        using var rejected = await client.PostAsync(route + "?handler=Deactivate", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode); var result = (await rejected.Content.ReadFromJsonAsync<SignupAdministrationResult>())!;
        Assert.False(result.Succeeded); Assert.True(result.RequiresConfirmation); Assert.Equal(seed.Custom, result.Impact!.QuestionId); Assert.Equal(0, result.Impact.AnswerCount);
        fields["expectedAnswerCount"] = "0";
        using var applied = await client.PostAsync(route + "?handler=Deactivate", new FormUrlEncodedContent(fields));
        Assert.True((await applied.Content.ReadFromJsonAsync<SignupAdministrationResult>())!.Succeeded);
        using var after = await client.GetFromJsonAsync<JsonDocument>(route + "?handler=Current");
        Assert.False(after!.RootElement.GetProperty("questions").EnumerateArray().Single(q => q.GetProperty("id").GetGuid() == seed.Custom).GetProperty("active").GetBoolean());
        Assert.True(after.RootElement.GetProperty("formVersion").GetInt32() > current.RootElement.GetProperty("formVersion").GetInt32());
    }

    [Theory]
    [InlineData(EventState.Draft, true)]
    [InlineData(EventState.Live, false)]
    [InlineData(EventState.AwaitingFinalReview, false)]
    [InlineData(EventState.Cancelled, false)]
    [InlineData(EventState.Archived, false)]
    public async Task CurrentReturnsAuthorizedNoStoreSettingsAndFullDefinitionByStableIdWithoutWriting(EventState phase, bool editable)
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            db.Entry(await db.Events.SingleAsync(e => e.Id == seed.EventId)).Property(e => e.State).CurrentValue = phase;
            foreach (var q in await db.SignupQuestions.Where(q => q.EventId == seed.EventId && (q.Id == seed.Custom || q.Id == seed.Account)).ToListAsync()) q.UpdatePresentation("Same label", q.HelpText);
            await db.SaveChangesAsync();
        }
        await using var factory = Factory(); using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var route = $"/Admin/Events/SignupSetup/{seed.EventId}";
        using (var anonymous = await client.GetAsync(route + "?handler=Current")) { Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode); Assert.Contains("Login", anonymous.Headers.Location!.ToString()); }
        await LoginAsync(client, seed.Admin); var before = await SnapshotAsync(seed.EventId);
        using var page = await client.GetAsync(route); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var response = await client.GetAsync(route + "?handler=Current"); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(body); var current = json.RootElement;
        Assert.Equal(editable, current.GetProperty("editable").GetBoolean()); Assert.Equal(seed.EventId, current.GetProperty("eventId").GetGuid());
        Assert.Equal(phase.ToString(), current.GetProperty("phase").GetString()); Assert.Equal(await EventVersionAsync(seed.EventId), current.GetProperty("settings").GetProperty("eventVersion").GetInt64());
        var questions = current.GetProperty("questions").EnumerateArray().ToArray(); Assert.Equal(4, questions.Length);
        Assert.Equal(2, questions.Count(q => q.GetProperty("label").GetString() == "Same label"));
        Assert.Equal(4, questions.Select(q => q.GetProperty("id").GetGuid()).Distinct().Count());
        Assert.Contains(questions, q => q.GetProperty("id").GetGuid() == seed.CoCaptain && !q.GetProperty("active").GetBoolean());
        Assert.All(questions, q => { Assert.True(q.TryGetProperty("version", out _)); Assert.True(q.TryGetProperty("systemField", out _)); Assert.True(q.TryGetProperty("accountRole", out _)); Assert.True(q.TryGetProperty("options", out _)); });
        Assert.Equal(before, await SnapshotAsync(seed.EventId));
        await using (var db = new ApplicationDbContext(options)) { var hidden = await db.Events.SingleAsync(e => e.Id == seed.EventId); db.Entry(hidden).Property(e => e.HiddenAt).CurrentValue = Now; db.Entry(hidden).Property(e => e.HiddenByAccountId).CurrentValue = await db.Accounts.Where(a => a.LoginName == seed.Admin).Select(a => a.Id).SingleAsync(); db.Entry(hidden).Property(e => e.HiddenReason).CurrentValue = "Synthetic hidden-read fixture"; await db.SaveChangesAsync(); }
        using var hiddenPage = await client.GetAsync(route); using var hiddenRead = await client.GetAsync(route + "?handler=Current");
        Assert.Equal(HttpStatusCode.NotFound, hiddenPage.StatusCode); Assert.Equal(HttpStatusCode.NotFound, hiddenRead.StatusCode);
    }
}
