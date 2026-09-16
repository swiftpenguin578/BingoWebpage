namespace Bingo.Domain.Announcements;

public sealed class DropAnnouncementAcknowledgement
{
    private DropAnnouncementAcknowledgement() { }

    public DropAnnouncementAcknowledgement(Guid id, Guid accountId, Guid eventId, Guid submissionId)
    {
        Id = id;
        AccountId = accountId;
        EventId = eventId;
        SubmissionId = submissionId;
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public DateTimeOffset? BannerAcknowledgedAt { get; private set; }
    public DateTimeOffset? DropsAcknowledgedAt { get; private set; }

    public void AcknowledgeBanner(DateTimeOffset at) => BannerAcknowledgedAt ??= at.ToUniversalTime();
    public void AcknowledgeDrops(DateTimeOffset at) => DropsAcknowledgedAt ??= at.ToUniversalTime();
}
