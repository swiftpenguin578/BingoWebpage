using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.BrowserTests;

[Collection(BrowserTestGroup.Name)]
public sealed class PlayingAccountHeaderHttpTests(BrowserTestApplicationFactory factory)
{
    private const string Password = "Playing-header-password-123!";
    private static readonly DateTimeOffset At = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HeaderNamesThePlayingAccountAndTheSharedHandlerSwitchesOnlyTheOwnAccount()
    {
        var seed = await SeedAsync(secondAccount: true, live: true);
        using var player = await SignInAsync(seed.Username);

        var page = WebUtility.HtmlDecode(await player.GetStringAsync("/HowTo"));
        Assert.Contains("data-playing-account", page);
        Assert.Contains($"Playing as {seed.FirstName}", page);
        Assert.Contains("Switch account", page);
        Assert.Contains(seed.SecondName, page);
        Assert.DoesNotContain("Active account", page);

        // Stale expected account: refused with the server message, nothing changes.
        var stale = await PostSwitchAsync(player, page, seed, expected: seed.SecondId, next: seed.FirstId, returnUrl: "/HowTo");
        Assert.Equal("/HowTo", stale.Headers.Location?.OriginalString);
        Assert.Equal(1, await SwapCountAsync(seed.ParticipantId));

        // An outsider cannot switch someone else's participant.
        var outsider = await SeedAccountAsync();
        using var other = await SignInAsync(outsider);
        var otherPage = WebUtility.HtmlDecode(await other.GetStringAsync("/HowTo"));
        Assert.DoesNotContain("data-playing-account", otherPage);
        var forged = await PostRawAsync(other, Token(WebUtility.HtmlDecode(await other.GetStringAsync("/Account/Settings"))), seed, seed.FirstId, seed.SecondId, "/HowTo");
        Assert.Equal(1, await SwapCountAsync(seed.ParticipantId));
        Assert.True(forged.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized);

        // A non-local return URL falls back to the home page; a valid switch lands on the page the player was on.
        var external = await PostSwitchAsync(player, page, seed, seed.FirstId, seed.SecondId, "https://evil.example/steal");
        Assert.Equal("/", external.Headers.Location?.OriginalString);
        Assert.Equal(2, await SwapCountAsync(seed.ParticipantId));
        var switched = WebUtility.HtmlDecode(await player.GetStringAsync("/HowTo"));
        Assert.Contains($"Playing as {seed.SecondName}", switched);
        Assert.Contains($"You're now playing as {seed.SecondName}.", switched);
        Assert.DoesNotContain("UTC", Regex.Match(switched, "You're now playing as[^<]*").Value);

        var back = await PostSwitchAsync(player, switched, seed, seed.SecondId, seed.FirstId, "/HowTo?x=1");
        Assert.Equal("/HowTo?x=1", back.Headers.Location?.OriginalString);
        Assert.Contains($"Playing as {seed.FirstName}", WebUtility.HtmlDecode(await player.GetStringAsync("/HowTo")));
    }

    [Fact]
    public async Task HeaderShowsTheNameOnlyForAOneAccountPlayerAndIsHiddenOtherwise()
    {
        var single = await SeedAsync(secondAccount: false, live: true);
        using var player = await SignInAsync(single.Username);
        var page = WebUtility.HtmlDecode(await player.GetStringAsync("/HowTo"));
        Assert.Contains($"Playing as {single.FirstName}", page);
        Assert.DoesNotContain("Switch account", page);
        Assert.DoesNotContain("public-ui-header-playing-panel", page);

        using var danish = await SignInAsync(single.Username, "da");
        var danishPage = WebUtility.HtmlDecode(await danish.GetStringAsync("/HowTo"));
        Assert.Contains($"Spiller som {single.FirstName}", danishPage);

        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        Assert.DoesNotContain("data-playing-account", await anonymous.GetStringAsync("/HowTo"));

        var unnamed = await SeedAsync(secondAccount: true, live: true, activationRow: false);
        using var stuck = await SignInAsync(unnamed.Username);
        var stuckPage = WebUtility.HtmlDecode(await stuck.GetStringAsync("/HowTo"));
        Assert.Contains("No active playing account – contact an admin", stuckPage);
        Assert.DoesNotContain("public-ui-header-playing-panel", stuckPage);
        using var stuckDanish = await SignInAsync(unnamed.Username, "da");
        Assert.Contains("Ingen aktiv spillekonto – kontakt en admin", WebUtility.HtmlDecode(await stuckDanish.GetStringAsync("/HowTo")));

        var notLive = await SeedAsync(secondAccount: true, live: false);
        using var waiting = await SignInAsync(notLive.Username);
        Assert.DoesNotContain("data-playing-account", await waiting.GetStringAsync("/HowTo"));
    }

    [Theory]
    [InlineData("//evil.example/x")]
    [InlineData("/\\evil.example/x")]
    [InlineData("%2F%2Fevil.example/x")]
    [InlineData("https://evil.example/x")]
    [InlineData("javascript:alert(1)")]
    public async Task UnsafeReturnUrlsFallBackToTheHomePage(string returnUrl)
    {
        var seed = await SeedAsync(secondAccount: true, live: true);
        using var player = await SignInAsync(seed.Username);
        var page = WebUtility.HtmlDecode(await player.GetStringAsync("/HowTo"));
        var response = await PostSwitchAsync(player, page, seed, seed.FirstId, seed.SecondId, returnUrl);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task SwitchWithoutAnAntiforgeryTokenIsRefusedAndWritesNothing()
    {
        var seed = await SeedAsync(secondAccount: true, live: true);
        using var player = await SignInAsync(seed.Username);
        var response = await player.PostAsync("/playing-account/switch", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventId"] = seed.EventId.ToString(),
            ["ParticipantId"] = seed.ParticipantId.ToString(),
            ["ExpectedCurrentCharacterId"] = seed.FirstId.ToString(),
            ["NextCharacterId"] = seed.SecondId.ToString(),
            ["returnUrl"] = "/HowTo"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, await SwapCountAsync(seed.ParticipantId));
    }

    private sealed record Seed(string Username, Guid ParticipantId, Guid EventId, Guid FirstId, string FirstName, Guid SecondId, string SecondName);

    private async Task<string> SeedAccountAsync()
    {
        var username = "ph-" + Guid.NewGuid().ToString("N");
        var account = Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), At);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, Password), false, At, incrementVersion: false);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(account);
        await db.SaveChangesAsync();
        return username;
    }

    private async Task<Seed> SeedAsync(bool secondAccount, bool live, bool activationRow = true)
    {
        var username = "ph-" + Guid.NewGuid().ToString("N");
        var owner = Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), At);
        owner.SetPassword(new PasswordHasher<Account>().HashPassword(owner, Password), false, At, incrementVersion: false);
        var eventId = Guid.NewGuid();
        var item = new BingoEvent(eventId, "Header event " + eventId.ToString("N"), $"ph-{eventId:N}", "UTC", owner.Id, At, PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(At.AddDays(-3), At.AddDays(-2), null, At.AddHours(-4), At.AddDays(5), 20);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(At.AddDays(-3));
        item.CloseSignups(At.AddDays(-2));
        if (live) item.StartEvent(At.AddHours(-4));
        var participantId = Guid.NewGuid();
        var participant = new EventParticipant(participantId, eventId, SignupStatus.Confirmed, 1, At.AddDays(-3), SignupSource.AdminCreated);
        participant.AssignOwner(owner);
        var first = new OsrsCharacter(Guid.NewGuid(), "First " + eventId.ToString("N")[..8], ("FIRST " + eventId.ToString("N")[..8]), At);
        var second = new OsrsCharacter(Guid.NewGuid(), "Second " + eventId.ToString("N")[..8], ("SECOND " + eventId.ToString("N")[..8]), At);
        var team = new Team(Guid.NewGuid(), eventId, "Header team", "header-team", TeamFormationType.Preformed, null, false);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participantId, TeamMembershipRole.Participant, At.AddDays(-2), null, "test");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.AddRange(owner, item, participant, first, team, membership);
        db.EventParticipantCharacters.Add(new(Guid.NewGuid(), eventId, participantId, first.Id, 0, At.AddDays(-3), owner.Id, null, EventCharacterRole.Playing, 10, EhbSource.Manual, null));
        if (secondAccount)
        {
            db.Add(second);
            db.EventParticipantCharacters.Add(new(Guid.NewGuid(), eventId, participantId, second.Id, 1, At.AddDays(-3), owner.Id, null, EventCharacterRole.Playing, 9, EhbSource.Manual, null));
        }
        // The one-account Live player deliberately has no account-switch row; the two-account player has the go-live activation row.
        if (live && secondAccount && activationRow)
            db.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(Guid.NewGuid(), eventId, participantId, null, first.Id, At.AddHours(-4), At.AddHours(-4), null, null));
        await db.SaveChangesAsync();
        return new(username, participantId, eventId, first.Id, first.DisplayName, second.Id, second.DisplayName);
    }

    private async Task<int> SwapCountAsync(Guid participantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.EventParticipantCharacterSwaps.CountAsync(x => x.EventParticipantId == participantId);
    }

    private async Task<HttpClient> SignInAsync(string username, string? culture = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        if (culture is not null) client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture=c%3D{culture}%7Cuic%3D{culture}");
        var login = await client.GetStringAsync("/Account/Login");
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = Password,
            ["__RequestVerificationToken"] = Token(login)
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        return client;
    }

    private static string Token(string html) => Token(html, required: true)!;

    private static string? Token(string html, bool required)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success) return required ? throw new InvalidOperationException("No antiforgery token on the page.") : null;
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Task<HttpResponseMessage> PostSwitchAsync(HttpClient client, string pageHtml, Seed seed, Guid expected, Guid next, string returnUrl) =>
        PostRawAsync(client, Token(pageHtml), seed, expected, next, returnUrl);

    private static Task<HttpResponseMessage> PostRawAsync(HttpClient client, string token, Seed seed, Guid expected, Guid next, string returnUrl) =>
        client.PostAsync("/playing-account/switch", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EventId"] = seed.EventId.ToString(),
            ["ParticipantId"] = seed.ParticipantId.ToString(),
            ["ExpectedCurrentCharacterId"] = expected.ToString(),
            ["NextCharacterId"] = next.ToString(),
            ["returnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        }));
}
