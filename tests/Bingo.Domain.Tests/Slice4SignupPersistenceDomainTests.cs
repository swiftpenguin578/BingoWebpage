using Bingo.Domain.Signups;

namespace Bingo.Domain.Tests;

public sealed class Slice4SignupPersistenceDomainTests
{
    [Fact]
    public void FormQuestionAnswerAndPaymentInvariantsAreExplicit()
    {
        var now = DateTimeOffset.UtcNow;
        var form = new SignupForm(Guid.NewGuid(), Guid.NewGuid(), now);
        var regular = new SignupQuestion(Guid.NewGuid(), form.Id, form.EventId, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, form.EventId, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
        var participant = new EventParticipant(Guid.NewGuid(), form.EventId, SignupStatus.Confirmed, 1, now, SignupSource.Website);

        form.RecordAcceptedResponse(now);
        participant.SetPaymentReceived(true);
        var answer = new SignupAnswer(Guid.NewGuid(), participant.Id, regular.Id, regular.Label, string.Empty, Guid.NewGuid());

        Assert.Equal(now, form.FirstResponseAt);
        Assert.True(participant.PaymentReceived);
        Assert.Equal(PaymentStatus.Paid, participant.PaymentStatus);
        Assert.NotNull(answer.OsrsCharacterId);
        Assert.Throws<InvalidOperationException>(() => regular.Deactivate());
        Assert.Throws<InvalidOperationException>(() => captain.Deactivate());
        Assert.Throws<ArgumentException>(() => new SignupQuestion(Guid.NewGuid(), form.Id, form.EventId, "bad", "Bad", SignupQuestionType.Account, false, 2, null));
    }

    [Fact]
    public void WithdrawnIsTheSingleInactiveStateAndAltAssignmentsNeverCarryEhb()
    {
        var now = DateTimeOffset.UtcNow;
        var participant = new EventParticipant(Guid.NewGuid(), Guid.NewGuid(), SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated);
        participant.Withdraw(now.AddMinutes(1), "Retained withdrawal");

        Assert.Equal(SignupStatus.Withdrawn, participant.SignupStatus);
        Assert.Equal(now.AddMinutes(1), participant.WithdrawnAt);
        Assert.Throws<ArgumentException>(() => new EventParticipantCharacter(Guid.NewGuid(), participant.EventId, participant.Id, Guid.NewGuid(), 0, now, null, Guid.NewGuid(), EventCharacterRole.Informational, 1m, EhbSource.Manual, null));
    }

    [Fact]
    public void RejoinPreservesIdentityButGetsNewQueueHistory()
    {
        var then = DateTimeOffset.UtcNow.AddHours(-1);
        var now = DateTimeOffset.UtcNow;
        var participant = new EventParticipant(Guid.NewGuid(), Guid.NewGuid(), SignupStatus.WaitingList, 4, then, SignupSource.Website);

        participant.Withdraw(now, "Participant withdrawal");
        participant.Rejoin(SignupStatus.WaitingList, 9, now.AddMinutes(1));

        Assert.Equal(SignupStatus.WaitingList, participant.SignupStatus);
        Assert.Equal(9, participant.SignupSequence);
        Assert.Equal(now.AddMinutes(1), participant.SignedUpAt);
        Assert.Equal(now.AddMinutes(1), participant.WaitingListedAt);
        Assert.Null(participant.WithdrawnAt);
        Assert.Null(participant.StatusReason);
    }
}
