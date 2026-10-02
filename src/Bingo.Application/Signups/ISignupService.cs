using Bingo.Domain.Signups;
using Bingo.Domain.Teams;

namespace Bingo.Application.Signups;

public interface ISignupService
{
    Task<SignupAdministrationResult> ApplyQuestionMutationAsync(SignupQuestionMutationRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<SignupAdministrationResult>(new NotSupportedException("Signup question mutation is not available."));

    Task<SignupAdministrationResult> EnableCoCaptainAsync(Guid eventId, Guid questionId, Guid actorAccountId, string actorName, CancellationToken cancellationToken = default)
        => Task.FromException<SignupAdministrationResult>(new NotSupportedException("Co-captain configuration is not available."));

    Task<SignupAdministrationResult> DeleteQuestionAsync(Guid eventId, Guid questionId, Guid actorAccountId, string actorName, CancellationToken cancellationToken = default)
        => Task.FromException<SignupAdministrationResult>(new NotSupportedException("Signup question deletion is not available."));

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

    Task<ParticipantLifecycleResult> RejoinAsync(Guid eventId, Guid participantId, Guid accountId, string actorName, string? womValidationConfirmationToken, CancellationToken cancellationToken = default)
        => RejoinAsync(eventId, participantId, accountId, actorName, cancellationToken);

    /// <summary>Self-service rejoin may require the current admission code; Admin restore never does.</summary>
    Task<ParticipantLifecycleResult> RejoinAsync(
        Guid eventId,
        Guid participantId,
        Guid accountId,
        string actorName,
        string? signupCode,
        string? womValidationConfirmationToken,
        CancellationToken cancellationToken = default)
        => RejoinAsync(eventId, participantId, accountId, actorName, womValidationConfirmationToken, cancellationToken);

    Task<ParticipantLifecycleResult> RestoreAsync(Guid eventId, Guid participantId, Guid adminAccountId, string adminName, CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantLifecycleResult>(new NotSupportedException("Participant lifecycle is not available."));

    Task<ParticipantLifecycleResult> RestoreAsync(Guid eventId, Guid participantId, Guid adminAccountId, string adminName, string? womValidationConfirmationToken, CancellationToken cancellationToken = default)
        => RestoreAsync(eventId, participantId, adminAccountId, adminName, cancellationToken);

    /// <summary>Confirms one selected waiting-list participant atomically.</summary>
    Task<ParticipantQueueMutationResult> ConfirmWaitingParticipantAsync(
        ConfirmWaitingParticipantRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantQueueMutationResult>(new NotSupportedException("Selected waiting-list confirmation is not available."));

    /// <summary>Moves one confirmed participant to the end of the waiting list.</summary>
    Task<ParticipantQueueMutationResult> MoveConfirmedParticipantToWaitingAsync(
        MoveConfirmedParticipantToWaitingRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantQueueMutationResult>(new NotSupportedException("Selected waiting-list movement is not available."));

    /// <summary>Admin restore boundary with an explicit full-event capacity override.</summary>
    Task<ParticipantLifecycleResult> RestoreAdminParticipantAsync(
        AdminParticipantRestoreRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<ParticipantLifecycleResult>(new NotSupportedException("Admin participant restoration is not available."));

    /// <summary>Adds a participant from the owner's saved Playing account links without questionnaire input.</summary>
    Task<AdminParticipantResult> AddSavedParticipantAsync(
        AddSavedParticipantRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<AdminParticipantResult>(new NotSupportedException("Saved-account participant addition is not available."));

    /// <summary>Changes the primary Playing account for a pre-draft participant.</summary>
    Task<EventAccountMutationResult> SwitchAdminPrimaryAsync(
        SwitchAdminPrimaryRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<EventAccountMutationResult>(new NotSupportedException("Primary account switching is not available."));

    Task<EventAccountMutationResult> AddEventParticipantAccountAsync(
        AddEventParticipantAccountRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<EventAccountMutationResult>(new NotSupportedException("Event account additions are not available."));

    Task<EventAccountMutationResult> RemoveEventParticipantAccountAsync(
        RemoveEventParticipantAccountRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<EventAccountMutationResult>(new NotSupportedException("Event account removals are not available."));

    Task<EventAccountMutationResult> CorrectEventParticipantAccountAsync(
        CorrectEventParticipantAccountRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<EventAccountMutationResult>(new NotSupportedException("Event account corrections are not available."));

    // Readable aliases for future Participants route binding.
    Task<ParticipantQueueMutationResult> ConfirmParticipantAsync(ConfirmWaitingParticipantRequest request, CancellationToken cancellationToken = default)
        => ConfirmWaitingParticipantAsync(request, cancellationToken);
    Task<ParticipantQueueMutationResult> MoveParticipantToWaitingAsync(MoveConfirmedParticipantToWaitingRequest request, CancellationToken cancellationToken = default)
        => MoveConfirmedParticipantToWaitingAsync(request, cancellationToken);
    Task<AdminParticipantResult> AddAdminParticipantFromSavedAccountsAsync(AddSavedParticipantRequest request, CancellationToken cancellationToken = default)
        => AddSavedParticipantAsync(request, cancellationToken);
    Task<EventAccountMutationResult> SwitchParticipantPrimaryAsync(SwitchAdminPrimaryRequest request, CancellationToken cancellationToken = default)
        => SwitchAdminPrimaryAsync(request, cancellationToken);

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

    /// <summary>
    /// Adds one participant to an already-finalized roster while the event is
    /// still pre-Live. The operation is deliberately separate from signup
    /// admission and from the retired vacancy/replacement workflow.
    /// </summary>
    Task<FinalizedRosterMutationResult> AddFinalizedRosterParticipantAsync(
        FinalizedRosterAddRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<FinalizedRosterMutationResult>(new NotSupportedException("Finalized-roster additions are not available."));

    /// <summary>Removes one current finalized-roster membership before first Live.</summary>
    Task<FinalizedRosterMutationResult> RemoveFinalizedRosterParticipantAsync(
        FinalizedRosterRemoveRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromException<FinalizedRosterMutationResult>(new NotSupportedException("Finalized-roster removals are not available."));

    // Short aliases keep the application boundary readable for route-backed
    // callers while the explicit names above remain the canonical contract.
    Task<FinalizedRosterMutationResult> AddRosterMemberAsync(
        FinalizedRosterAddRequest request,
        CancellationToken cancellationToken = default)
        => AddFinalizedRosterParticipantAsync(request, cancellationToken);

    Task<FinalizedRosterMutationResult> RemoveRosterMemberAsync(
        FinalizedRosterRemoveRequest request,
        CancellationToken cancellationToken = default)
        => RemoveFinalizedRosterParticipantAsync(request, cancellationToken);

    Task<PromotionFollowUpResult> CompletePromotionFollowUpAsync(Guid eventId, Guid followUpId, Guid adminAccountId, string adminName, CancellationToken cancellationToken = default)
        => Task.FromException<PromotionFollowUpResult>(new NotSupportedException("Promotion follow-up is not available."));
}

public sealed record ParticipantLifecycleResult(bool Succeeded, string? Error, SignupStatus? Status = null, int? WaitingPosition = null, bool Changed = false, string? WomValidationConfirmationToken = null);
public sealed record ConfirmWaitingParticipantRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid ActorAccountId,
    string ActorName,
    bool ExpandCapacityWhenFull = false,
    long? ExpectedEventVersion = null,
    int? ExpectedResponseVersion = null);
public sealed record MoveConfirmedParticipantToWaitingRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid ActorAccountId,
    string ActorName,
    long? ExpectedEventVersion = null,
    int? ExpectedResponseVersion = null);
public sealed record ParticipantQueueMutationResult(
    bool Succeeded,
    string? Error = null,
    Guid? ParticipantId = null,
    SignupStatus? Status = null,
    int? WaitingPosition = null,
    Guid? PromotedParticipantId = null,
    int? EffectiveParticipantCap = null,
    bool Changed = false);
public sealed record AdminParticipantRestoreRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid ActorAccountId,
    string ActorName,
    bool ExpandCapacityWhenFull = false,
    long? ExpectedEventVersion = null,
    int? ExpectedResponseVersion = null,
    string? WomValidationConfirmationToken = null);
public sealed record AddSavedParticipantRequest(
    Guid EventId,
    Guid OwnerAccountId,
    Guid ActorAccountId,
    string ActorName,
    IReadOnlyList<Guid> PlayingCharacterIds,
    Guid PrimaryCharacterId,
    PaymentStatus Payment = PaymentStatus.Unpaid,
    bool ExpandCapacityWhenFull = false,
    long? ExpectedEventVersion = null);
public sealed record SwitchAdminPrimaryRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid NextCharacterId,
    Guid ActorAccountId,
    string ActorName,
    Guid? ExpectedCurrentCharacterId = null,
    int? ExpectedResponseVersion = null);
public sealed record AddEventParticipantAccountRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid CharacterId,
    EventCharacterRole Role,
    decimal? Ehb,
    Guid ActorAccountId,
    string ActorName,
    Guid? SignupQuestionId = null,
    int? ExpectedResponseVersion = null);
public sealed record RemoveEventParticipantAccountRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid AssignmentId,
    Guid ActorAccountId,
    string ActorName,
    int? ExpectedResponseVersion = null);
public sealed record CorrectEventParticipantAccountRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid AssignmentId,
    Guid CharacterId,
    decimal? Ehb,
    Guid ActorAccountId,
    string ActorName,
    int? ExpectedResponseVersion = null);
public sealed record EventAccountMutationResult(
    bool Succeeded,
    string? Error = null,
    Guid? ParticipantId = null,
    Guid? PrimaryCharacterId = null,
    int? PlayingAccountCount = null,
    int? TotalAccountCount = null,
    bool Changed = false);
public sealed record SignupAdministrationResult(
    bool Succeeded,
    string? Error = null,
    int PromotedParticipants = 0,
    int? EffectiveParticipantCap = null,
    SignupQuestionImpact? Impact = null,
    bool RequiresConfirmation = false);
public enum SignupQuestionMutationKind
{
    DeleteQuestion,
    DisableCoCaptain
}
public sealed record SignupQuestionMutationRequest(
    Guid EventId,
    Guid QuestionId,
    Guid ActorAccountId,
    string ActorName,
    SignupQuestionMutationKind Operation,
    bool Confirmed,
    int ExpectedAnswerCount,
    int ExpectedEventRegistrationReleaseCount,
    int ExpectedQuestionVersion);
public sealed record SignupQuestionImpact(
    Guid QuestionId,
    int AnswerCount,
    int EventRegistrationReleaseCount,
    int QuestionVersion);
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
    int? ExpectedResponseVersion = null,
    string? WomValidationConfirmationToken = null);
public sealed record AdminParticipantResult(bool Succeeded, string? Error, Guid? ParticipantId = null, SignupStatus? Status = null, int? WaitingPosition = null, string? WomValidationConfirmationToken = null);
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
    long? ExpectedVacancyVersion = null,
    string? WomValidationConfirmationToken = null);
public sealed record LiveParticipantResult(
    bool Succeeded,
    string? Error = null,
    bool Changed = false,
    Guid? MembershipId = null,
    Guid? ParticipantId = null,
    DateTimeOffset? EffectiveAtUtc = null,
    Guid? FollowUpId = null,
    string? WomValidationConfirmationToken = null);
public sealed record PromotionFollowUpResult(bool Succeeded, string? Error = null, bool Changed = false);

public sealed record FinalizedRosterAddRequest(
    Guid EventId,
    Guid TeamId,
    Guid ActorAccountId,
    string ActorName,
    Guid? WebsiteAccountId = null,
    Guid? ParticipantId = null,
    TeamMembershipRole Role = TeamMembershipRole.Participant,
    long? ExpectedTeamVersion = null,
    Guid? PlayingCharacterId = null,
    decimal? PlayingEhb = null);

public sealed record FinalizedRosterRemoveRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid ActorAccountId,
    string ActorName,
    bool Confirmed = false,
    long? ExpectedMembershipVersion = null);

public sealed record FinalizedRosterMutationResult(
    bool Succeeded,
    string? Error = null,
    bool Changed = false,
    Guid? ParticipantId = null,
    Guid? MembershipId = null,
    string? TeamName = null,
    int? CurrentTeamMemberCount = null,
    string? WomSyncStatus = null,
    string? WomSyncError = null,
    int? TargetTeamSize = null,
    bool TeamIsShort = false);
