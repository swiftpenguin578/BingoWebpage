using System.Net;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class EventCompetitionManagementIntegrationTests
{
    [Fact]
    public async Task Rc09W1Wa9AppliedCreateAndDeleteReportCompletedRatherThanQueued()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await SeedEventAsync(clock, live: false);
        await SetAdminPasswordAsync(fixture.Actor.Id, clock.GetUtcNow());
        var provider = new RecordingManagementClient
        {
            CreateHandler = (payload, _) => Task.FromResult(new WiseOldManCompetitionWriteResult(
                WiseOldManCompetitionWriteStatus.Success,
                new WiseOldManCompetition(7701, payload.Title, payload.StartsAt, payload.EndsAt, clock.GetUtcNow(),
                    payload.Teams.SelectMany(team => team.Participants).Select(name => new WiseOldManCompetitionParticipant(name, "REGULAR", null)).ToArray()),
                ProtectedVerificationCode: "protected:controlled-code"))
        };
        await using var original = CreateAdminFactory(clock);
        await using var factory = original.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWiseOldManCompetitionManagementClient>();
            services.AddSingleton<IWiseOldManCompetitionManagementClient>(provider);
            services.RemoveAll<ICompetitionCredentialProtector>();
            services.AddSingleton<ICompetitionCredentialProtector>(new PassthroughCredentialProtector());
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(client, fixture.Actor.Username);
        var route = $"/Admin/Events/WiseOldMan/{fixture.EventId}";
        foreach (var (handler, message) in new[] {
            ("CreateManagedCompetition", "The WOM competition was created and linked."),
            ("DeleteManagedCompetition", "The website-created WOM competition was deleted.") })
        {
            var page = await client.GetStringAsync(route);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            using var response = await client.PostAsync(route + "?handler=" + handler, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["EventVersion"] = InputValue(page, "EventVersion"),
                ["ManagedCompetitionDeleteId"] = "7701",
                ["ConfirmManagedCompetitionDelete"] = "true",
                ["__RequestVerificationToken"] = InputValue(page, "__RequestVerificationToken")
            }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(result.RootElement.GetProperty("succeeded").GetBoolean(), result.RootElement.ToString());
            Assert.Equal("applied", result.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(message, result.RootElement.GetProperty("message").GetString());
        }
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(1, provider.DeleteCalls);
    }

    [Fact]
    public async Task CCmp2W1WomStructuredRefusalClearsSecretAndCurrentIsNoStore()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await SeedEventAsync(clock, live: false);
        await SetAdminPasswordAsync(fixture.Actor.Id, clock.GetUtcNow());
        await using var factory = CreateAdminFactory(clock);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(client, fixture.Actor.Username);
        var route = $"/Admin/Events/WiseOldMan/{fixture.EventId}";
        var page = await client.GetStringAsync(route);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        using var current = await client.GetAsync(route + "?handler=Current");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.True(current.Headers.CacheControl!.NoStore);
        using var state = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.Equal(fixture.EventId, state.RootElement.GetProperty("eventId").GetGuid());
        Assert.True(state.RootElement.GetProperty("integration").GetProperty("canLink").GetBoolean());
        var secret = new string('s', 4001);
        using var refused = await client.PostAsync(route + "?handler=AdoptCompetitionCredential", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventVersion"] = InputValue(page, "EventVersion"),
            ["CompetitionVerificationCode"] = secret,
            ["__RequestVerificationToken"] = InputValue(page, "__RequestVerificationToken")
        }));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        var json = await refused.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        using var result = JsonDocument.Parse(json);
        Assert.False(result.RootElement.GetProperty("succeeded").GetBoolean());
        Assert.Equal("refused", result.RootElement.GetProperty("outcome").GetString());
        await using var verify = CreateDb();
        Assert.Empty(await verify.EventCompetitionManagements.ToListAsync());
    }

    [Theory]
    [InlineData(EventState.Cancelled)]
    [InlineData(EventState.Finalized)]
    [InlineData(EventState.Archived)]
    public async Task D16D17EveryWomPostRefusedOnTerminalEventWithoutWrites(EventState terminal)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await SeedEventAsync(clock, live: false);
        await SetAdminPasswordAsync(fixture.Actor.Id, clock.GetUtcNow());
        await using (var db = CreateDb())
        {
            db.Entry(await db.Events.SingleAsync()).Property(x => x.State).CurrentValue = terminal;
            await db.SaveChangesAsync();
        }
        await using var factory = CreateAdminFactory(clock);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(client, fixture.Actor.Username);
        var route = $"/Admin/Events/WiseOldMan/{fixture.EventId}";
        var page = await client.GetStringAsync(route);
        await using var before = CreateDb();
        var audits = await before.AuditEntries.CountAsync();
        var version = (await before.Events.SingleAsync()).Version;
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        foreach (var handler in new[] { "Competition", "DisconnectCompetition", "FetchCompetition", "CreateManagedCompetition", "AdoptCompetitionCredential", "DeleteManagedCompetition", "MakeDevelopmentCompetitionDue", "UnknownWomMutation" })
        {
            using var response = await client.PostAsync(route + "?handler=" + handler, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["EventVersion"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["CompetitionId"] = "1234",
                ["CompetitionVerificationCode"] = "secret-never-saved", ["ConfirmCompetitionClear"] = "true",
                ["ConfirmManagedCompetitionDelete"] = "true", ["ManagedCompetitionDeleteId"] = "1234",
                ["__RequestVerificationToken"] = InputValue(page, "__RequestVerificationToken")
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal($"/Admin/Events/Manage/{fixture.EventId}", response.Headers.Location!.OriginalString);
        }
        await using var verify = CreateDb();
        Assert.Equal(version, (await verify.Events.SingleAsync()).Version);
        Assert.Equal(audits, await verify.AuditEntries.CountAsync());
        Assert.Empty(await verify.EventCompetitionManagementOperations.ToListAsync());
        Assert.Empty(await verify.EventCompetitionManagements.ToListAsync());
        Assert.Empty(await verify.EventCompetitionSynchronizations.ToListAsync());
    }

    [Theory]
    [InlineData(GlobalRole.Admin)]
    [InlineData(GlobalRole.SuperAdmin)]
    public async Task CCmp1HiddenWomPageAndReadbackRemain404ForEveryAdmin(GlobalRole role)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var fixture = await SeedEventAsync(clock, live: false);
        await SetAdminPasswordAsync(fixture.Actor.Id, clock.GetUtcNow());
        await using (var db = CreateDb())
        {
            (await db.Accounts.SingleAsync(x => x.Id == fixture.Actor.Id)).SetGlobalRole(role);
            var item = await db.Events.SingleAsync();
            db.Entry(item).Property(x => x.State).CurrentValue = EventState.Archived;
            item.Hide(fixture.Actor.Id, clock.GetUtcNow(), item.Name, "C-CMP-1 controlled hidden fixture");
            await db.SaveChangesAsync();
        }
        await using var factory = CreateAdminFactory(clock);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(client, fixture.Actor.Username);
        foreach (var query in new[] { "", "?hidden=true", "?handler=Current", "?handler=Current&hidden=true" })
        {
            using var response = await client.GetAsync($"/Admin/Events/WiseOldMan/{fixture.EventId}" + query);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
