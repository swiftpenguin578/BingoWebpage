using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Bingo.Web.Security;

/// <summary>Protected, short-lived proof of a completed Discord callback before account creation.</summary>
public sealed class DiscordOnboardingStateService(IDataProtectionProvider protectionProvider, IMemoryCache cache, TimeProvider time)
{
    private const string CookieName = "Bingo.Discord.Onboarding";
    private const string Purpose = "slice1-discord-account-creation-v1";
    private readonly IDataProtector protector = protectionProvider.CreateProtector(Purpose);

    public void Issue(HttpResponse response, string discordUserId, string? displayName, string? returnUrl = null)
    {
        var state = new State(discordUserId, displayName, returnUrl, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), time.GetUtcNow().AddMinutes(15));
        cache.Set(CacheKey(state.Nonce), state.ExpiresAt, state.ExpiresAt - time.GetUtcNow());
        response.Cookies.Append(CookieName, protector.Protect(System.Text.Json.JsonSerializer.Serialize(state)), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/Account",
            Expires = state.ExpiresAt
        });
    }

    public bool TryRead(HttpRequest request, out State state)
    {
        state = default!;
        if (!request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrWhiteSpace(value)) return false;
        try { state = System.Text.Json.JsonSerializer.Deserialize<State>(protector.Unprotect(value))!; }
        catch (CryptographicException) { return false; }
        catch (System.Text.Json.JsonException) { return false; }
        return state is not null && state.ExpiresAt > time.GetUtcNow() && cache.TryGetValue<DateTimeOffset>(CacheKey(state.Nonce), out var expiry) && expiry == state.ExpiresAt;
    }

    public void Consume(HttpResponse response, State state)
    {
        cache.Remove(CacheKey(state.Nonce));
        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/Account", Secure = true, SameSite = SameSiteMode.Lax });
    }

    private static string CacheKey(string nonce) => "slice1-discord-onboarding:" + nonce;
    public sealed record State(string DiscordUserId, string? DisplayName, string? ReturnUrl, string Nonce, DateTimeOffset ExpiresAt);
}
