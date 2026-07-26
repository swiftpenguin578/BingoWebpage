using Bingo.Domain.Access;
using Bingo.Domain.Signups;

namespace Bingo.Domain.Tests;

public sealed class Slice2PersistenceDomainTests
{
    [Fact]
    public void CharacterLinkRetainsPairHistoryAndLinkOwnedPreferences()
    {
        var linkedAt = DateTimeOffset.UtcNow.AddDays(-2);
        var accountId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var link = new AccountOsrsCharacter(
            linkId, accountId, characterId, accountId, true, 3, " Main ", 125.5m, linkedAt);

        link.Unlink(linkedAt.AddDays(1));

        Assert.Equal(linkId, link.Id);
        Assert.Equal(accountId, link.AccountId);
        Assert.Equal(characterId, link.OsrsCharacterId);
        Assert.False(link.Active);
        Assert.False(link.Preferred);
        Assert.Equal(linkedAt.AddDays(1), link.UnlinkedAt);
        Assert.Equal(125.5m, link.SavedEhb);

        var relinkingActor = Guid.NewGuid();
        link.Relink(relinkingActor, linkedAt.AddDays(2));
        link.UpdatePreferences("Borrowed", 1, true, 130m, linkedAt.AddDays(2));

        Assert.Equal(linkId, link.Id);
        Assert.True(link.Active);
        Assert.Null(link.UnlinkedAt);
        Assert.Equal(relinkingActor, link.LinkedByAccountId);
        Assert.Equal("Borrowed", link.PersonalLabel);
        Assert.Equal(1, link.SortOrder);
        Assert.Equal(130m, link.SavedEhb);
    }

    [Fact]
    public void PlayingAndInformationalAssignmentsEnforceEhbShape()
    {
        var values = AssignmentValues();
        var playing = new EventParticipantCharacter(
            Guid.NewGuid(), values.EventId, values.ParticipantId, values.CharacterId, 0,
            values.Now, values.ActorId, null, EventCharacterRole.Playing, 42m, EhbSource.Manual, null);
        var informational = new EventParticipantCharacter(
            Guid.NewGuid(), values.EventId, values.ParticipantId, Guid.NewGuid(), 1,
            values.Now, values.ActorId, null, EventCharacterRole.Informational, null, null, null);

        Assert.Equal(42m, playing.EhbSnapshot);
        Assert.Null(informational.EhbSnapshot);
        Assert.Throws<ArgumentException>(() => new EventParticipantCharacter(
            Guid.NewGuid(), values.EventId, values.ParticipantId, Guid.NewGuid(), 2,
            values.Now, values.ActorId, null, EventCharacterRole.Playing, null, EhbSource.Manual, null));
        Assert.Throws<ArgumentException>(() => new EventParticipantCharacter(
            Guid.NewGuid(), values.EventId, values.ParticipantId, Guid.NewGuid(), 2,
            values.Now, values.ActorId, null, EventCharacterRole.Informational, 1m, EhbSource.Manual, null));
        Assert.Throws<ArgumentException>(() => new EventParticipantCharacter(
            Guid.NewGuid(), values.EventId, values.ParticipantId, Guid.NewGuid(), 2,
            values.Now, values.ActorId, null, EventCharacterRole.Playing, 1m, EhbSource.WiseOldMan, null));
    }

    private static (Guid EventId, Guid ParticipantId, Guid CharacterId, Guid ActorId, DateTimeOffset Now) AssignmentValues()
        => (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
}
