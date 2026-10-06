using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.BrowserTests;

[Collection(BrowserTestGroup.Name)]
public sealed class U2EventsDirectoryHttpTests(BrowserTestApplicationFactory factory)
{
    [Fact]
    public async Task QueryPageDoesNotBindTheRazorRouteAndInvalidPartsAreNotSilentlyDropped()
    {
        var at = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var username = "u2-dir-" + Guid.NewGuid().ToString("N");
        const string password = "U2-directory-password-123!";
        var actor = Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), at);
        actor.SetGlobalRole(GlobalRole.Admin);
        actor.SetPassword(new PasswordHasher<Account>().HashPassword(actor, password), false, at, incrementVersion: false);
        var name = "Paging " + Guid.NewGuid().ToString("N");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(actor);
            for (var n = 0; n < 31; n++)
                db.Add(new BingoEvent(Guid.NewGuid(), $"{name} {n:00}", $"http-{Guid.NewGuid():N}", "UTC", actor.Id, at, PlacementRule.LegacyScoreTimeThenEhb));
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        var url = "/Admin/Events?search=" + Uri.EscapeDataString(name);
        var first = await client.GetStringAsync(url);
        Assert.Equal(25, Regex.Count(first, "data-open-url="));
        Assert.Contains("1–25 of 31 events", WebUtility.HtmlDecode(first));
        Assert.DoesNotContain("Some parts of this link weren’t available", WebUtility.HtmlDecode(first));
        Assert.DoesNotContain("data-admin-events-control", first);
        var clamped = WebUtility.HtmlDecode(await client.GetStringAsync(url + "&page=999"));
        Assert.Equal(6, Regex.Count(clamped, "data-open-url="));
        Assert.Contains("26–31 of 31 events", clamped);
        Assert.Contains("Some parts of this link weren’t available", clamped);
        var invalid = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Events?view=past&phase=live&unknown=1"));
        Assert.Contains("Some parts of this link weren’t available", invalid);
        Assert.DoesNotContain("aria-checked=\"true\" data-directory-url=\"/Admin/Events?view=past&phase=live", invalid);
        var hidden = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Events?filter=hidden"));
        Assert.Contains("Some parts of this link weren’t available", hidden);
        Assert.DoesNotContain("data-event-hidden=\"true\"", hidden);
        var sortName = "Null-last " + Guid.NewGuid().ToString("N");
        var known = new BingoEvent(Guid.NewGuid(), sortName + " known", $"http-{Guid.NewGuid():N}", "UTC", actor.Id, at, PlacementRule.LegacyScoreTimeThenEhb);
        var unknown = new BingoEvent(Guid.NewGuid(), sortName + " unknown", $"http-{Guid.NewGuid():N}", "UTC", actor.Id, at, PlacementRule.LegacyScoreTimeThenEhb);
        unknown.ConfigureSchedule(at.AddDays(-6), at.AddDays(-5), null, at.AddDays(-4), at.AddDays(-3), null);
        unknown.ConfigureSignup(true, false, null); unknown.OpenSignups(at.AddDays(-6)); unknown.CloseSignups(at.AddDays(-5));
        unknown.StartEvent(at.AddDays(-4)); unknown.EndEvent(at.AddDays(-3));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AddRange(known, unknown); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET actual_started_at = NULL WHERE id = {unknown.Id}");
        }
        foreach (var direction in new[] { "asc", "desc" })
        {
            var sorted = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Events?search=" + Uri.EscapeDataString(sortName) + "&sort=signups&direction=" + direction));
            var ids = Regex.Matches(sorted, "<div class=\"tr row[^\"]*\"[^>]*data-event-id=\"([^\"]+)\"").Select(match => Guid.Parse(match.Groups[1].Value)).ToArray();
            Assert.Equal(new[] { known.Id, unknown.Id }, ids);
            Assert.Contains("Participant count unavailable", sorted);
        }
    }
}
