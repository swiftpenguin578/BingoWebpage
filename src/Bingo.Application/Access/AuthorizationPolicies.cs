namespace Bingo.Application.Access;

public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string Captain = "Captain";
    public const string CaptainFullAccess = "CaptainFullAccess";
    public const string CaptainCorrectionAccess = "CaptainCorrectionAccess";
    public const string CaptainTeamScoped = "CaptainTeamScoped";
}
