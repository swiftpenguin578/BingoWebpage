using Bingo.Domain.Events;
using Bingo.Web.Pages.Admin.Events;

namespace Bingo.Web.Security;

public enum AdminEventPageKind
{
    Index, Create, Manage, Identity, Schedule, Questions, SignupSetup, Participants, Participant,
    Board, BoardPreview, Draft, Finalize, WiseOldMan
}

/// <summary>Explicit route gate; application services still own mutation-time invariants.</summary>
public enum AdminEventHandlerGate
{
    Read, RetainedArtwork, Service, Hide, QuestionAdd, Signup, Identity, Setup,
    Schedule, Resume, Review, EvidenceCodes, Board, BoardCorrection,
    WomSetup, WomFetch, WomDevelopment
}

public sealed record AdminEventPagePolicy(
    AdminEventPageKind Kind,
    bool HasEventContext,
    bool ViewableOnTerminalEvents,
    IReadOnlyDictionary<string, AdminEventHandlerGate> Handlers)
{
    public AdminEventHandlerGate Handler(string httpMethod, string? name) =>
        Handlers.TryGetValue($"{httpMethod.ToUpperInvariant()}:{name}", out var gate)
            ? gate
            : throw new InvalidOperationException($"Unclassified admin event handler: {Kind} {httpMethod}:{name}");
}

/// <summary>
/// Every HTTP handler is named here. The reflection proof fails for additions or
/// removals without a deliberate classification. No filename or substring policy.
/// </summary>
public static class AdminEventPagePolicies
{
    public static IReadOnlyDictionary<Type, AdminEventPagePolicy> All { get; } =
        new Dictionary<Type, AdminEventPagePolicy>
        {
            [typeof(IndexModel)] = Page(AdminEventPageKind.Index, false, false,
                ("GET:", AdminEventHandlerGate.Read)),
            [typeof(CreateModel)] = Page(AdminEventPageKind.Create, false, false,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:CheckAgain", AdminEventHandlerGate.Read),
                ("POST:", AdminEventHandlerGate.Service)),
            [typeof(ManageModel)] = Page(AdminEventPageKind.Manage, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:Current", AdminEventHandlerGate.Read),
                ("POST:State", AdminEventHandlerGate.Signup),
                ("POST:PrepareSignupConfirmation", AdminEventHandlerGate.Signup),
                ("POST:PrepareStartConfirmation", AdminEventHandlerGate.Signup),
                ("POST:PrepareEndConfirmation", AdminEventHandlerGate.Signup),
                ("POST:PrepareResumeConfirmation", AdminEventHandlerGate.Signup),
                ("POST:PrepareDestructiveConfirmation", AdminEventHandlerGate.Signup),
                ("POST:OpenSignup", AdminEventHandlerGate.Signup),
                ("POST:CloseSignup", AdminEventHandlerGate.Signup),
                ("POST:ReopenSignup", AdminEventHandlerGate.Signup),
                ("POST:Capacity", AdminEventHandlerGate.Signup),
                ("POST:SignupWindow", AdminEventHandlerGate.Signup),
                ("POST:ConfirmSignup", AdminEventHandlerGate.Signup),
                ("POST:RestoreHidden", AdminEventHandlerGate.Signup),
                ("POST:StartEvent", AdminEventHandlerGate.Service),
                ("POST:EndEvent", AdminEventHandlerGate.Service),
                ("POST:Discard", AdminEventHandlerGate.Service),
                ("POST:Cancel", AdminEventHandlerGate.Service),
                ("POST:Hide", AdminEventHandlerGate.Hide),
                ("POST:ResumeEvent", AdminEventHandlerGate.Resume),
                ("POST:ReopenSubmissions", AdminEventHandlerGate.Review),
                ("POST:EnableEvidenceCodes", AdminEventHandlerGate.EvidenceCodes),
                ("POST:DisableEvidenceCodes", AdminEventHandlerGate.EvidenceCodes),
                ("POST:CreateEvidenceCode", AdminEventHandlerGate.EvidenceCodes)),
            [typeof(IdentityModel)] = Page(AdminEventPageKind.Identity, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:Current", AdminEventHandlerGate.Read),
                ("POST:", AdminEventHandlerGate.Identity)),
            [typeof(ScheduleModel)] = Page(AdminEventPageKind.Schedule, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:Current", AdminEventHandlerGate.Read),
                ("POST:", AdminEventHandlerGate.Schedule)),
            [typeof(QuestionsModel)] = Page(AdminEventPageKind.Questions, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("POST:", AdminEventHandlerGate.Signup)),
            [typeof(SignupSetupModel)] = Page(AdminEventPageKind.SignupSetup, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:Current", AdminEventHandlerGate.Read),
                ("POST:SignupAdministration", AdminEventHandlerGate.Setup),
                ("POST:SignupCode", AdminEventHandlerGate.Setup),
                ("POST:", AdminEventHandlerGate.QuestionAdd),
                ("POST:AddAccount", AdminEventHandlerGate.QuestionAdd),
                ("POST:EditAccount", AdminEventHandlerGate.Signup),
                ("POST:Deactivate", AdminEventHandlerGate.Signup),
                ("POST:CoCaptain", AdminEventHandlerGate.Signup),
                ("POST:Move", AdminEventHandlerGate.Signup),
                ("POST:Edit", AdminEventHandlerGate.Signup),
                ("POST:Replace", AdminEventHandlerGate.Signup)),
            [typeof(ParticipantsModel)] = Page(AdminEventPageKind.Participants, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:SearchOwnerAccounts", AdminEventHandlerGate.Read),
                ("POST:Withdraw", AdminEventHandlerGate.Setup),
                ("POST:CancelWomValidation", AdminEventHandlerGate.Setup),
                ("POST:SignupAdministration", AdminEventHandlerGate.Setup),
                ("POST:CreateInternalParticipant", AdminEventHandlerGate.Setup),
                ("POST:Payment", AdminEventHandlerGate.Service)),
            [typeof(ParticipantModel)] = Page(AdminEventPageKind.Participant, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("POST:", AdminEventHandlerGate.Signup),
                ("POST:CancelWomValidation", AdminEventHandlerGate.Signup),
                ("POST:Restore", AdminEventHandlerGate.Signup),
                ("POST:AdminNote", AdminEventHandlerGate.Service),
                ("POST:Payment", AdminEventHandlerGate.Service),
                ("POST:Withdraw", AdminEventHandlerGate.Service),
                ("POST:FillVacancy", AdminEventHandlerGate.Service),
                ("POST:CompletePromotionFollowUp", AdminEventHandlerGate.Service)),
            [typeof(BoardModel)] = Page(AdminEventPageKind.Board, true, false,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:EditorData", AdminEventHandlerGate.Read),
                ("GET:Readback", AdminEventHandlerGate.Read),
                ("GET:TileImage", AdminEventHandlerGate.RetainedArtwork),
                ("POST:Create", AdminEventHandlerGate.Board),
                ("POST:TakeEditing", AdminEventHandlerGate.Board),
                ("POST:AcquireEditing", AdminEventHandlerGate.Board),
                ("POST:ReleaseEditing", AdminEventHandlerGate.Board),
                ("POST:CreateTile", AdminEventHandlerGate.Board),
                ("POST:EditTile", AdminEventHandlerGate.Board),
                ("POST:Move", AdminEventHandlerGate.Board),
                ("POST:Resize", AdminEventHandlerGate.Board),
                ("POST:TeamSize", AdminEventHandlerGate.Board),
                ("POST:Publish", AdminEventHandlerGate.Board),
                ("POST:DiscardCorrection", AdminEventHandlerGate.Board),
                ("POST:Approve", AdminEventHandlerGate.Board),
                ("POST:Unapprove", AdminEventHandlerGate.Board),
                ("POST:Remove", AdminEventHandlerGate.Board),
                ("POST:ApproveState", AdminEventHandlerGate.Board),
                ("POST:PublishState", AdminEventHandlerGate.Board),
                ("POST:CorrectPublished", AdminEventHandlerGate.BoardCorrection)),
            [typeof(BoardPreviewModel)] = Page(AdminEventPageKind.BoardPreview, true, false,
                ("GET:", AdminEventHandlerGate.Read)),
            [typeof(DraftModel)] = Page(AdminEventPageKind.Draft, true, false,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:Readback", AdminEventHandlerGate.Read),
                ("GET:TeamImage", AdminEventHandlerGate.Read),
                ("POST:AddTeam", AdminEventHandlerGate.Setup),
                ("POST:RemoveDraftTeam", AdminEventHandlerGate.Setup),
                ("POST:WithdrawParticipant", AdminEventHandlerGate.Setup),
                ("POST:UpdateTeam", AdminEventHandlerGate.Setup),
                ("POST:AddMember", AdminEventHandlerGate.Setup),
                ("POST:RemoveMember", AdminEventHandlerGate.Setup),
                ("POST:MoveMember", AdminEventHandlerGate.Setup),
                ("POST:Scramble", AdminEventHandlerGate.Setup),
                ("POST:Start", AdminEventHandlerGate.Setup),
                ("POST:Configure", AdminEventHandlerGate.Setup),
                ("POST:Pick", AdminEventHandlerGate.Setup),
                ("POST:Undo", AdminEventHandlerGate.Setup),
                ("POST:Cancel", AdminEventHandlerGate.Setup),
                ("POST:Finalize", AdminEventHandlerGate.Setup),
                ("POST:AcquireControl", AdminEventHandlerGate.Setup),
                ("POST:TakeControl", AdminEventHandlerGate.Setup),
                ("POST:ReleaseControl", AdminEventHandlerGate.Setup),
                ("POST:ChangeRole", AdminEventHandlerGate.Service)),
            [typeof(FinalizeModel)] = Page(AdminEventPageKind.Finalize, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("GET:Current", AdminEventHandlerGate.Read),
                ("POST:Resolve", AdminEventHandlerGate.Service),
                ("POST:AcknowledgeCompletion", AdminEventHandlerGate.Service),
                ("POST:CorrectCompletion", AdminEventHandlerGate.Service),
                ("POST:Finalize", AdminEventHandlerGate.Service),
                ("POST:Unfinalize", AdminEventHandlerGate.Service),
                ("POST:Archive", AdminEventHandlerGate.Service)),
            [typeof(WiseOldManModel)] = Page(AdminEventPageKind.WiseOldMan, true, true,
                ("GET:", AdminEventHandlerGate.Read),
                ("POST:Competition", AdminEventHandlerGate.WomSetup),
                ("POST:DisconnectCompetition", AdminEventHandlerGate.WomSetup),
                ("POST:CreateManagedCompetition", AdminEventHandlerGate.WomSetup),
                ("POST:AdoptCompetitionCredential", AdminEventHandlerGate.WomSetup),
                ("POST:DeleteManagedCompetition", AdminEventHandlerGate.WomSetup),
                ("POST:FetchCompetition", AdminEventHandlerGate.WomFetch),
                ("POST:MakeDevelopmentCompetitionDue", AdminEventHandlerGate.WomDevelopment)),
        };

    public static AdminEventPagePolicy? For(Type modelType) => All.GetValueOrDefault(modelType);

    public static bool Allows(AdminEventHandlerGate gate, EventState state) => gate switch
    {
        AdminEventHandlerGate.Identity => EventStatePolicy.Allows(state, EventCapability.ConfigureIdentity),
        AdminEventHandlerGate.Signup => EventStatePolicy.Allows(state, EventCapability.ConfigureSignup),
        AdminEventHandlerGate.Setup or AdminEventHandlerGate.Board => EventStatePolicy.Allows(state, EventCapability.ConfigureIdentityOrSchedule),
        AdminEventHandlerGate.Schedule => state == EventState.Live || EventStatePolicy.Allows(state, EventCapability.ConfigureIdentityOrSchedule),
        AdminEventHandlerGate.Resume => EventStatePolicy.Allows(state, EventCapability.ResumeEvent),
        AdminEventHandlerGate.Review => EventStatePolicy.Allows(state, EventCapability.ReviewEvidence),
        AdminEventHandlerGate.EvidenceCodes => EventStatePolicy.Allows(state, EventCapability.ConfigureEvidenceCodes),
        AdminEventHandlerGate.BoardCorrection => state is EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview,
        AdminEventHandlerGate.WomSetup => state is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live,
        AdminEventHandlerGate.WomFetch => state is EventState.Live or EventState.AwaitingFinalReview,
        AdminEventHandlerGate.WomDevelopment => state == EventState.Live,
        _ => true // Reads, service-owned handlers, exact Hide and reconciled adds.
    };

    private static AdminEventPagePolicy Page(AdminEventPageKind kind, bool hasEventContext, bool terminal,
        params (string Handler, AdminEventHandlerGate Gate)[] handlers) =>
        new(kind, hasEventContext, terminal, handlers.ToDictionary(item => item.Handler, item => item.Gate, StringComparer.Ordinal));
}
