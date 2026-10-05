using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Navigation;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.IntegrationTests;

public sealed partial class AdminDesignShellIntegrationTests
{
    // Community pages are not bound yet: opt in only inside this HTTP fixture.
    private WebApplicationFactory<Program> RememberedEventFactory() => IdentityFactory().WithWebHostBuilder(builder =>
        builder.ConfigureServices(services => services.Configure<RazorPagesOptions>(settings =>
            settings.Conventions.AddPageApplicationModelConvention("/Admin/Events/Index", model => model.EndpointMetadata.Add(new AdminDesignAttribute())))));

    [Fact]
    public async Task RememberedEventSessionSetsShowsReplacesRetainsPastAndClearsOnSignout()
    {
        var admin = Admin(); var first = Event(admin, EventState.Draft, "First selection", 2);
        var past = Event(admin, EventState.Archived, "Past selection", -12);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, first, past); await db.SaveChangesAsync(); }
        await using var factory = RememberedEventFactory(); using var client = await IdentityClientAsync(factory);
        using var opened = await client.GetAsync($"/Admin/Events/Schedule/{first.Id}");
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        var cookie = Assert.Single(opened.Headers.GetValues("Set-Cookie"), value => value.StartsWith(AdminEventSession.CookieName + "=", StringComparison.Ordinal));
        Assert.StartsWith($"{AdminEventSession.CookieName}={first.Id:D};", cookie);
        Assert.Contains("httponly", cookie); Assert.Contains("samesite=lax", cookie); Assert.Contains("secure", cookie);
        Assert.DoesNotContain("expires=", cookie); Assert.DoesNotContain("max-age=", cookie);
        Assert.DoesNotContain("data-admin-design", await opened.Content.ReadAsStringAsync());
        await client.GetStringAsync("/"); // Leaving Admin keeps the session preference.
        var community = await client.GetStringAsync("/Admin/Events/Index");
        Assert.Contains($"data-selected-event-id=\"{first.Id}\"", community);
        Assert.Contains($"href=\"/Admin/Events/Identity/{first.Id}\"", community);
        Assert.DoesNotContain("crumb-mid", Regex.Match(community, "<nav class=\"crumbs\".*?</nav>", RegexOptions.Singleline).Value);
        using var replaced = await client.GetAsync($"/Admin/Events/Identity/{past.Id}");
        Assert.Contains(replaced.Headers.GetValues("Set-Cookie"), value => value.StartsWith($"{AdminEventSession.CookieName}={past.Id:D};", StringComparison.Ordinal));
        var after = await client.GetStringAsync("/Admin/Events/Index");
        Assert.Contains($"data-selected-event-id=\"{past.Id}\"", after);
        Assert.DoesNotContain($"data-selected-event-id=\"{first.Id}\"", after);
        using var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = IdentityFields(after)["__RequestVerificationToken"] }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), value => value.StartsWith(AdminEventSession.CookieName + "=;", StringComparison.Ordinal) && value.Contains("expires=Thu, 01 Jan 1970"));
    }

    [Theory]
    [InlineData("discarded")]
    [InlineData("hidden")]
    [InlineData("deleted")]
    [InlineData("no-access")]
    public async Task RememberedEventIsRevalidatedAndDroppedOnEveryRender(string change)
    {
        var admin = Admin(); var item = Event(admin, change == "hidden" ? EventState.AwaitingFinalReview : EventState.Draft, "Remembered fixture", -5);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = RememberedEventFactory(); using var client = await IdentityClientAsync(factory);
        await client.GetStringAsync($"/Admin/Events/Identity/{item.Id}");
        await using (var db = new ApplicationDbContext(options))
        {
            var persisted = await db.Events.SingleAsync();
            if (change == "discarded") db.Entry(persisted).Property(value => value.State).CurrentValue = EventState.Discarded;
            if (change == "hidden") persisted.Hide(admin.Id, Now, persisted.Name, "Controlled visibility change");
            if (change == "deleted") db.Events.Remove(persisted);
            if (change == "no-access") (await db.Accounts.SingleAsync()).SetGlobalRole(GlobalRole.User);
            await db.SaveChangesAsync();
        }
        using var response = await client.GetAsync(change == "no-access" ? "/" : "/Admin/Events/Index");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(AdminEventSession.CookieName + "=;", StringComparison.Ordinal) && value.Contains("expires=Thu, 01 Jan 1970"));
        Assert.DoesNotContain($"data-selected-event-id=\"{item.Id}\"", await response.Content.ReadAsStringAsync());
        using var next = await client.GetAsync(change == "no-access" ? "/" : "/Admin/Events/Index");
        Assert.False(next.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(value => value.StartsWith(AdminEventSession.CookieName + "=", StringComparison.Ordinal)));
    }
}
