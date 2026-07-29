namespace Bingo.Domain.Teams;

public sealed class DraftPublicationRoster
{
    private DraftPublicationRoster() { }
    public DraftPublicationRoster(Guid id, Guid publicationCycleId, Guid teamId, Guid participantId, TeamMembershipRole role, int? effectivePickNumber, string publicCharacterName)
    {
        if (string.IsNullOrWhiteSpace(publicCharacterName)) throw new ArgumentException("A frozen public character name is required.", nameof(publicCharacterName));
        Id = id;
        DraftPublicationCycleId = publicationCycleId;
        TeamId = teamId;
        EventParticipantId = participantId;
        Role = role;
        EffectivePickNumber = effectivePickNumber;
        PublicCharacterName = publicCharacterName.Trim();
    }
    public Guid Id { get; private set; }
    public Guid DraftPublicationCycleId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid EventParticipantId { get; private set; }
    public TeamMembershipRole Role { get; private set; }
    public int? EffectivePickNumber { get; private set; }
    public string PublicCharacterName { get; private set; } = string.Empty;
}
