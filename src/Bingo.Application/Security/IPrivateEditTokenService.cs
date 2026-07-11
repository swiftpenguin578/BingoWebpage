namespace Bingo.Application.Security;

public interface IPrivateEditTokenService
{
    (string Token, string Hash) Create();
    string Hash(string token);
    bool Verify(string token, string hash);
}
