using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Bingo.Application.Signups;
using Microsoft.AspNetCore.DataProtection;

namespace Bingo.Web.Security;

public sealed class SignupLookupTokenService(IDataProtectionProvider protectionProvider) : ISignupLookupTokenService
{
    private const string Purpose = "signup-ehb-wise-old-man-v1";
    private readonly IDataProtector protector = protectionProvider.CreateProtector(Purpose);

    public string Create(string normalizedCharacterName, decimal ehb, DateTimeOffset fetchedAt, DateTimeOffset issuedAt, DateTimeOffset expiresAt) =>
        protector.Protect(JsonSerializer.Serialize(new Payload(Purpose, normalizedCharacterName, ehb.ToString("G29", CultureInfo.InvariantCulture), fetchedAt, issuedAt, expiresAt)));

    public bool TryValidate(string? token, string normalizedCharacterName, decimal ehb, DateTimeOffset now, out DateTimeOffset fetchedAt)
    {
        fetchedAt = default;
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(protector.Unprotect(token));
            if (payload is null || payload.Purpose != Purpose || payload.NormalizedCharacterName != normalizedCharacterName || !decimal.TryParse(payload.Ehb, NumberStyles.Number, CultureInfo.InvariantCulture, out var tokenEhb) || tokenEhb != ehb || payload.IssuedAt > now || payload.ExpiresAt <= now || payload.FetchedAt > now || now - payload.FetchedAt >= TimeSpan.FromMinutes(5)) return false;
            fetchedAt = payload.FetchedAt;
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException or ArgumentException)
        {
            return false;
        }
    }

    private sealed record Payload(string Purpose, string NormalizedCharacterName, string Ehb, DateTimeOffset FetchedAt, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
}
