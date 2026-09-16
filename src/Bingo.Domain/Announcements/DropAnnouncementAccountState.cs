namespace Bingo.Domain.Announcements;

public sealed class DropAnnouncementAccountState
{
    private DropAnnouncementAccountState() { }

    public DropAnnouncementAccountState(Guid id, Guid accountId, Guid eventId)
    {
        Id = id;
        AccountId = accountId;
        EventId = eventId;
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid EventId { get; private set; }
    public DateTimeOffset? ExpansionCooldownUntil { get; private set; }
    public long LastAutomaticExpansionOrdinal { get; private set; }

    public void SetExpansionCooldown(DateTimeOffset until) => ExpansionCooldownUntil = until.ToUniversalTime();
    public void ClaimAutomaticExpansion(long boundary, DateTimeOffset cooldownUntil)
    {
        LastAutomaticExpansionOrdinal = Math.Max(LastAutomaticExpansionOrdinal, boundary);
        SetExpansionCooldown(cooldownUntil);
    }
}
