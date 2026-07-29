namespace Bingo.Application.Access;

public static class AccountClaims
{
    public const string EventId = "bingo:event_id";
    public const string TeamId = "bingo:team_id";
    public const string MustChangePassword = "bingo:must_change_password";
    public const string AuthenticationMethod = "bingo:authentication_method";
    public const string AuthorizationVersion = "bingo:authorization_version";
    public const string PasswordVersion = "bingo:password_version";
    public const string AccountType = "bingo:account_type";
}
