using Microsoft.Extensions.Caching.Memory;

namespace Bingo.Web.Security;

/// <summary>Short-lived, single-use proof that the current account just confirmed its password.</summary>
public sealed class DiscordLinkStateService(IMemoryCache cache, TimeProvider time)
{
    private const string Prefix = "slice1-discord-link:";

    public string Create(Guid accountId, string purpose)
    {
        var state = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        cache.Set(Prefix + state, new Intent(accountId, purpose, time.GetUtcNow().AddMinutes(15)), TimeSpan.FromMinutes(15));
        return state;
    }

    public bool TryConsumeForChallenge(string? state, Guid accountId, string purpose)
    {
        if (state is null || !cache.TryGetValue<Intent>(Prefix + state, out var intent) || intent is null ||
            intent.AccountId != accountId || intent.Purpose != purpose || intent.ExpiresAt <= time.GetUtcNow() || intent.ChallengeIssued)
            return false;
        cache.Set(Prefix + state, intent with { ChallengeIssued = true }, intent.ExpiresAt - time.GetUtcNow());
        return true;
    }

    public bool TryConsumeForCallback(string? state, Guid accountId, string purpose)
    {
        if (state is null || !cache.TryGetValue<Intent>(Prefix + state, out var intent) || intent is null ||
            intent.AccountId != accountId || intent.Purpose != purpose || intent.ExpiresAt <= time.GetUtcNow() || !intent.ChallengeIssued)
            return false;
        cache.Remove(Prefix + state);
        return true;
    }

    private sealed record Intent(Guid AccountId, string Purpose, DateTimeOffset ExpiresAt, bool ChallengeIssued = false);
}
