namespace Bingo.Domain.Integrations.WiseOldMan;

/// <summary>
/// The origin of a Wise Old Man competition is independent from the
/// credential/capability attached to the link.  In particular, adopting a
/// management code never turns an external competition into a website-created
/// competition.
/// </summary>
public enum EventCompetitionProvenance
{
    Unknown = 0,
    WebsiteCreated = 1,
    External = 2,
    ExternallyCreated = External
}

public enum EventCompetitionWriteCapability
{
    Unknown = 0,
    ReadOnly = 1,
    Writable = 2
}

public enum EventCompetitionCredentialStatus
{
    NotApplicable = 0,
    Unverified = 1,
    Valid = 2,
    Invalid = 3,
    Revoked = 4,
    Unavailable = 5
}

