namespace Bingo.Web.Security;

public sealed class DevelopmentAdminBootstrapOptions
{
    public const string SectionName = "DevelopmentAdminBootstrap";

    public bool Enabled { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
