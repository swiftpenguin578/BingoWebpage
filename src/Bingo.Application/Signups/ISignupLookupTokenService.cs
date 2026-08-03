namespace Bingo.Application.Signups;

public interface ISignupLookupTokenService
{
    string Create(string normalizedCharacterName, decimal ehb, DateTimeOffset fetchedAt, DateTimeOffset issuedAt, DateTimeOffset expiresAt);

    bool TryValidate(string? token, string normalizedCharacterName, decimal ehb, DateTimeOffset now, out DateTimeOffset fetchedAt);
}
