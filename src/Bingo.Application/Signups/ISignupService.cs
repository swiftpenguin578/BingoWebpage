using Bingo.Domain.Signups;
using Bingo.Domain.Teams;

namespace Bingo.Application.Signups;

public interface ISignupService
{
    Task<SignupResult> SignUpAuthenticatedAsync(AuthenticatedSignupRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<SignupResult>(new NotSupportedException("Authenticated signup is not available."));

    Task<int> IncreaseCapacityAndPromoteAsync(Guid eventId, int newCap, CancellationToken cancellationToken = default);

    Task<SignupAdministrationResult> UpdateSignupAdministrationAsync(
        Guid eventId,
        long expectedVersion,
        int newCap,
        bool waitingListEnabled,
        Guid actorAccountId,
        string actorName,
        bool confirmWaitingListDisablement = false,
        CancellationToken cancellationToken = default)
        => Task.FromException<SignupAdministrationResult>(new NotSupportedException("Signup administration is not available."));

    Task<int> PromoteAvailablePlacesAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<ParticipantLifecycleResult> WithdrawAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, bool byAdmin, string? privateNote = null, CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantLifecycleResult>(new NotSupportedException("Participant lifecycle is not available."));

    Task<ParticipantLifecycleResult> WithdrawAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, bool byAdmin, string? privateNote, long? expectedMembershipVersion, CancellationToken cancellationToken = default)
        => WithdrawAsync(eventId, participantId, actorAccountId, actorName, byAdmin, privateNote, cancellationToken);

    Task<ParticipantLifecycleResult> RejoinAsync(Guid eventId, Guid participantId, Guid accountId, string actorName, CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantLifecycleResult>(new NotSupportedException("Participant lifecycle is not available."));

    Task<ParticipantLifecycleResult> RestoreAsync(Guid eventId, Guid participantId, Guid adminAccountId, string adminName, CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantLifecycleResult>(new NotSupportedException("Participant lifecycle is not available."));

    Task<ParticipantPaymentResult> SetPaymentAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, PaymentStatus payment, CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantPaymentResult>(new NotSupportedException("Participant payment is not available."));

    Task<ParticipantPaymentResult> SetAdminNotesAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, string? notes, string? expectedNotes, CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantPaymentResult>(new NotSupportedException("Participant notes are not available."));

    Task<AdminParticipantResult> CorrectAdminParticipantAsync(AdminParticipantChangeRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<AdminParticipantResult>(new NotSupportedException("Participant correction is not available."));

    Task<AdminParticipantResult> CreateAdminParticipantAsync(AdminParticipantChangeRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<AdminParticipantResult>(new NotSupportedException("Internal participant creation is not available."));

    Task<ParticipantOwnershipTransferResult> TransferParticipantOwnershipAsync(ParticipantOwnershipTransferRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantOwnershipTransferResult>(new NotSupportedException("Participant ownership transfer is not available."));

    Task<LiveParticipantResult> WithdrawLiveAsync(LiveWithdrawalRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<LiveParticipantResult>(new NotSupportedException("Live participant withdrawal is not available."));

    Task<LiveParticipantResult> ReplaceVacancyAsync(LiveReplacementRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<LiveParticipantResult>(new NotSupportedException("Live participant replacement is not available."));

    Task<PromotionFollowUpResult> CompletePromotionFollowUpAsync(Guid eventId, Guid followUpId, Guid adminAccountId, string adminName, CancellationToken cancellationToken = default)
        => Task.FromException<PromotionFollowUpResult>(new NotSupportedException("Promotion follow-up is not available."));
}

public sealed record ParticipantLifecycleResult(bool Succeeded, string? Error, SignupStatus? Status = null, int? WaitingPosition = null, bool Changed = false);
public sealed record SignupAdministrationResult(bool Succeeded, string? Error = null, int PromotedParticipants = 0, int? EffectiveParticipantCap = null);
public sealed record ParticipantPaymentResult(bool Succeeded, string? Error, bool Changed = false);
public sealed record AdminAccountAnswer(string? CharacterName, decimal? Ehb);
public sealed record AdminParticipantChangeRequest(
    Guid EventId,
    Guid? ParticipantId,
    Guid ActorAccountId,
    string ActorName,
    Guid? OwnerAccountId,
    IReadOnlyDictionary<Guid, AdminAccountAnswer> AccountAnswers,
    IReadOnlyDictionary<Guid, string> Answers,
    int? ExpectedResponseVersion = null);
public sealed record AdminParticipantResult(bool Succeeded, string? Error, Guid? ParticipantId = null, SignupStatus? Status = null, int? WaitingPosition = null);
public sealed record ParticipantOwnershipTransferRequest(Guid EventId, Guid ParticipantId, Guid ActorAccountId, string ActorName, Guid? DestinationOwnerAccountId, Guid? ExpectedOwnerAccountId = null, bool Confirmed = false);
public sealed record ParticipantOwnershipTransferResult(bool Succeeded, string? Error, bool Changed = false);
public sealed record LiveWithdrawalRequest(Guid EventId, Guid ParticipantId, Guid ActorAccountId, string ActorName, long? ExpectedMembershipVersion = null);
public sealed record LiveReplacementRequest(
    Guid EventId,
    Guid EndedMembershipId,
    Guid ActorAccountId,
    string ActorName,
    Guid? WaitingParticipantId = null,
    AdminParticipantChangeRequest? InternalParticipant = null,
    long? ExpectedVacancyVersion = null);
public sealed record LiveParticipantResult(
    bool Succeeded,
    string? Error = null,
    bool Changed = false,
    Guid? MembershipId = null,
    Guid? ParticipantId = null,
    DateTimeOffset? EffectiveAtUtc = null,
    Guid? FollowUpId = null);
public sealed record PromotionFollowUpResult(bool Succeeded, string? Error = null, bool Changed = false);
