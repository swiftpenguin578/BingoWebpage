using System.Net;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Bingo.Web.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bingo.IntegrationTests;

// U6 item 0b (brief 93): D17 read-only Teams view on terminal events, F1 stay on Teams.
public sealed partial class DraftOperationsIntegrationTests
{
    [Theory]
    [InlineData("Finalized")]
    [InlineData("Cancelled")]
    [InlineData("Archived")]
    public async Task U6TerminalTeamsPageIsReadableAndEveryMutationStaysRefused(string state)
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostFinalizeAsync(setup.EventId, CancellationToken.None, true));
        var teamId = await TeamIdAsync(setup.EventId, "Second");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var route = $"/Admin/Events/Draft/{setup.EventId}";
        var token = AntiforgeryToken(await client.GetStringAsync(route));
        await using (var transition = new ApplicationDbContext(options))
            await transition.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET state = {state} WHERE id = {setup.EventId}");

        // D17: the page, its state read and the AU14 readback answer on terminal events.
        using (var page = await client.GetAsync(route)) Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        foreach (var handler in new[] { "State", "Readback" })
        {
            using var read = await client.GetAsync($"{route}?handler={handler}");
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal("application/json", read.Content.Headers.ContentType?.MediaType);
            Assert.Contains("no-store", read.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }
        using (var stateRead = await client.GetAsync($"{route}?handler=State"))
            Assert.Contains("\"stage\":\"terminal\"", await stateRead.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Every Draft POST but ChangeRole keeps the route refusal and writes nothing.
        var before = await RosterStateAsync();
        using (var add = await client.PostAsync($"{route}?handler=AddMember", Form(token, new() { ["teamId"] = teamId.ToString(), ["participantId"] = setup.PlayerIds[3].ToString(), ["confirmed"] = "true" })))
        {
            Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
            Assert.Equal($"/Admin/Events/Manage/{setup.EventId}", add.Headers.Location?.OriginalString);
        }
        using (var configure = await client.PostAsync($"{route}?handler=Configure", Form(token, new() { ["teamCount"] = "2", ["targetSize"] = "2" })))
        {
            Assert.Equal(HttpStatusCode.Redirect, configure.StatusCode);
            Assert.Equal($"/Admin/Events/Manage/{setup.EventId}", configure.Headers.Location?.OriginalString);
        }
        Assert.Equal(before, await RosterStateAsync());
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(SignupStatus.Confirmed, (await verify.EventParticipants.SingleAsync(x => x.Id == setup.PlayerIds[3])).SignupStatus);
    }

    [Fact]
    public async Task U6HiddenEventTeamsReadsAreNotFound()
    {
        var setup = await SeedAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var route = $"/Admin/Events/Draft/{setup.EventId}";
        await using (var finish = new ApplicationDbContext(options))
            await finish.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET state = 'Finalized' WHERE id = {setup.EventId}");
        await using (var hide = new ApplicationDbContext(options))
        {
            var item = await hide.Events.SingleAsync(x => x.Id == setup.EventId);
            item.Hide(setup.FirstAdminId, now, item.Name, "U6 hidden check");
            await hide.SaveChangesAsync();
        }
        foreach (var path in new[] { route, route + "?handler=State" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var readback = await client.GetAsync(route + "?handler=Readback");
        // C-CMP-1: the readback answers 404 or "unknown" (Known=false), never the hidden state.
        var body = readback.StatusCode == HttpStatusCode.OK ? await readback.Content.ReadAsStringAsync() : string.Empty;
        Assert.DoesNotContain("\"known\":true", body, StringComparison.Ordinal);
    }

    // F1: finalizing stays on Teams. A JSON request gets the outcome and whether an
    // approved board is ready; a form post redirects back to Teams, never to Board.
    [Fact]
    public async Task U6FinalizeStaysOnTeams()
    {
        var setup = await SeedAsync();
        await StartAndScrambleAsync(setup);
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[2], CancellationToken.None));
        await ExecuteAsync(setup.EventId, setup.FirstAdminId, page => page.OnPostPickAsync(setup.EventId, setup.PlayerIds[3], CancellationToken.None));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, await LoginNameAsync(setup.FirstAdminId));
        var route = $"/Admin/Events/Draft/{setup.EventId}";
        var token = AntiforgeryToken(await client.GetStringAsync(route));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{route}?handler=Finalize") { Content = Form(token, new() { ["confirmed"] = "true" }) };
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"outcome\":\"done\"", json, StringComparison.Ordinal);
        Assert.Contains("\"finalized\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"boardReady\":false", json, StringComparison.Ordinal);
    }
}
