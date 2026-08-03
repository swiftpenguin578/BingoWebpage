namespace Bingo.Infrastructure.WiseOldMan;

public sealed class WiseOldManOptions
{
    public const string SectionName = "WiseOldMan";
    public string BaseUrl { get; set; } = "https://api.wiseoldman.net/v2/";
    public string UserAgent { get; set; } = "OSRSCommunityBingo/1.0 (contact: configure WiseOldMan:UserAgent)";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public WiseOldManDevelopmentFakeOptions DevelopmentFake { get; set; } = new();
}

public sealed class WiseOldManDevelopmentFakeOptions
{
    public bool Enabled { get; set; } = true;
    public bool AutomaticSynchronizationEnabled { get; set; } = true;
    public string PlayerMode { get; set; } = "Success";
    public string CompetitionMode { get; set; } = "Complete";
    public int TemporaryFailures { get; set; }
    public int Remaining { get; set; } = 19;
    public int RetryAfterSeconds { get; set; } = 30;
}
