namespace Bingo.Application.Security;

public interface ISecretHasher
{
    string Hash(string value);
    bool Verify(string value, string encodedHash);
}
