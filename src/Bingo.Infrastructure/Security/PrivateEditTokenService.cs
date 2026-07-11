using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Security;

namespace Bingo.Infrastructure.Security;

public sealed class PrivateEditTokenService : IPrivateEditTokenService
{
    public (string Token, string Hash) Create()
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool Verify(string token, string hash)
    {
        var actual = Encoding.ASCII.GetBytes(Hash(token));
        var expected = Encoding.ASCII.GetBytes(hash);
        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
