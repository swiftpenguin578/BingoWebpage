using Bingo.Domain.Access;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class SubmissionWorkflowTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public async Task B5Round2FormerMemberJoinBoundaryMatchesPickerAndCorrection(int joinedSecondsAfterUpload, bool eligible)
    {
        var setup = await SeedAsync(3, true);
        await using var db = new ApplicationDbContext(options);
        var service = Service(db);
        var created = await service.CreateAsync(Command(setup));
        var submission = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == created.SubmissionId);
        var participant = new EventParticipant(Guid.NewGuid(), setup.EventId, SignupStatus.Confirmed, 10, now.AddDays(-5), SignupSource.Website);
        var character = new OsrsCharacter(Guid.NewGuid(), "Join boundary player", "JOIN BOUNDARY PLAYER", now.AddDays(-5));
        var playing = new EventParticipantCharacter(Guid.NewGuid(), setup.EventId, participant.Id, character.Id, 0, now.AddDays(-5), setup.AdminId, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null);
        var joinedAt = submission.SubmittedAt.AddSeconds(joinedSecondsAfterUpload);
        var leftAt = submission.SubmittedAt.AddSeconds(2);
        var membership = new TeamMembership(Guid.NewGuid(), setup.TeamId, participant.Id, TeamMembershipRole.Participant, joinedAt, null, null);
        membership.Leave(leftAt, "Former member boundary fixture");
        db.AddRange(participant, character, playing, membership);
        await db.SaveChangesAsync();
        var persisted = await db.TeamMemberships.AsNoTracking().SingleAsync(x => x.Id == membership.Id);
        Assert.Equal(joinedAt, persisted.JoinedAt);
        Assert.Equal(leftAt, persisted.LeftAt);
        var baseline = await B5EvidenceStateAsync();
        var choices = await service.GetCorrectionCharactersAsync(submission.Id, setup.AdminId);
        if (!eligible)
        {
            Assert.DoesNotContain(choices, x => x.CharacterId == character.Id);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EditMetadataAsync(new(submission.Id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character.Id, "Correction", submission.Version)));
            Assert.Equal("Choose an unambiguous Playing character assigned in this event to a current or former member of this submission's team.", error.Message);
            Assert.Equal(baseline, await B5EvidenceStateAsync());
            return;
        }
        var choice = Assert.Single(choices, x => x.CharacterId == character.Id);
        Assert.True(choice.LeftTeam);
        Assert.False(choice.Current);
        Assert.False(choice.Released);
        await service.EditMetadataAsync(new(submission.Id, setup.AdminId, setup.TileId, setup.RequirementId, setup.DropId, character.Id, "Correction", submission.Version));
        var corrected = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submission.Id);
        Assert.Equal(participant.Id, corrected.CreditedParticipantId);
        Assert.Equal(character.Id, corrected.CreditedOsrsCharacterId);
        Assert.Equal(setup.TeamId, corrected.TeamId);
        Assert.Equal(submission.SubmittedAt, corrected.SubmittedAt);
        Assert.Equal(submission.Version + 1, corrected.Version);
    }
}
