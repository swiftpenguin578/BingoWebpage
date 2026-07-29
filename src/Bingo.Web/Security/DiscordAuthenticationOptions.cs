namespace Bingo.Web.Security;

public sealed class DiscordAuthenticationOptions { public const string SectionName = "DiscordAuthentication"; public string? ClientId { get; set; } public string? ClientSecret { get; set; } public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret); }
