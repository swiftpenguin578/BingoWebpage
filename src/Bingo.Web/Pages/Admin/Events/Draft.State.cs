using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Microsoft.AspNetCore.Mvc;
using Bingo.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

// U6 (brief 93): the Teams / Draft workspace is drawn in the browser from one
// authoritative page state. The page embeds it; every command and live update re-reads
// it (no-store). It carries display data only; AU14 outcome checks use Readback.
public sealed partial class DraftModel
{
    public DateTimeOffset? FirstPublishedAt { get; private set; }
    private BingoEvent? LoadedEvent { get; set; }
    private IReadOnlyList<string> LoadedBlockers { get; set; } = [];
    private DerivedDraftState? LoadedDerived { get; set; }
    private List<(Guid Id, int Number, Guid ParticipantId, Guid TeamId)> ActivePickIds { get; set; } = [];
    public TeamsPageState? State { get; private set; }

    // The header summary as the page script draws it (stage sentence).
    public string Summary => State is not { } state ? string.Empty : state.Stage switch
    {
        "setup" => state.Teams.Count == 0 ? Localize("Add a team for each captain to get started.")
            : state.Teams.Count(team => team.Included) < 2 ? Localize("Assemble each roster by hand, then finalize to publish them.")
            : Localize("Prepare teams and captains, then run the snake draft."),
        "final" => Localize("Published rosters for {0}.", state.EventName),
        "running" => string.Empty,
        _ => Localize("Rosters for {0}.", state.EventName)
    };

    public async Task<IActionResult> OnGetStateAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        CurrentAccountId = AdminId;
        if (!await Load(id, null, ct)) return NotFound();
        await LoadControllerState(id, ct);
        return new JsonResult(await BuildStateAsync(id, ct));
    }

    private async Task<TeamsPageState> BuildStateAsync(Guid id, CancellationToken ct)
    {
        var ev = LoadedEvent!;
        var now = time.GetUtcNow();
        var draftState = Draft!.State;
        var finalized = draftState == DraftState.Finalized;
        // T-24 (planner default, to confirm): Final Review and terminal events show the
        // final rosters read-only; Live keeps the role menu (stage "locked").
        var stage = ev.State switch
        {
            EventState.Cancelled or EventState.Finalized or EventState.Archived or EventState.Discarded => "terminal",
            EventState.AwaitingFinalReview => "review",
            EventState.Live => "locked",
            _ => draftState == DraftState.Running ? "running" : finalized ? "final" : draftState == DraftState.Paused ? "paused" : "setup"
        };
        var activeCycle = await db.ActiveRosterPublications(id).OrderByDescending(x => x.CycleNumber).Select(x => new { x.PublishedAt, x.CycleNumber }).FirstOrDefaultAsync(ct);
        var boardExists = await db.Boards.AsNoTracking().AnyAsync(x => x.EventId == id, ct);
        var accounts = await (from participant in db.EventParticipants.AsNoTracking()
                              join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
                              where participant.EventId == id
                              select new { participant.Id, AccountId = account.Id, account.PublicUsername }).ToListAsync(ct);
        var accountNames = accounts.ToDictionary(x => x.Id, x => x.PublicUsername);
        var participantAccounts = accounts.Select(x => x.AccountId).ToHashSet();
        var draftedOrder = Teams.Where(x => x.IncludedInDraft).OrderBy(x => x.DraftPosition ?? int.MaxValue).ThenBy(x => x.Name).ToList();
        TurnState? next = null;
        int? totalPicks = null;
        if (CurrentTurn is { } turn && LoadedDerived is { } derived && derived.Blockers.Count == 0 && DraftOrderReady)
        {
            var sizes = derived.RosterSizes.ToDictionary(x => x.Key, x => x.Value);
            sizes[turn.TeamId] = sizes.GetValueOrDefault(turn.TeamId) + 1;
            var picked = ActivePickIds.Select(x => x.TeamId).Append(turn.TeamId).ToList();
            var following = SnakeDraftOrder.GetNextEligibleTurn(picked, draftedOrder.Select(x => x.Id).ToList(), sizes, derived.Distribution);
            if (following is not null) next = new(following.TeamId, Teams.Single(x => x.Id == following.TeamId).Name);
            totalPicks = ActivePickIds.Count + derived.ProjectedFinalSizes.Values.Sum() - derived.RosterSizes.Values.Sum();
        }
        var latest = ActivePickIds.Count == 0 ? (ValueTuple<Guid, int, Guid, Guid>?)null : ActivePickIds[^1];
        var unplaced = Participants.Count(x => x.TeamId is null && x.SignupStatus == SignupStatus.Confirmed);
        var endsAt = ev.EventEndsAt;
        var reviewRoles = ev.State == EventState.AwaitingFinalReview && ev.AcceptsNewSubmissions(now);
        var correctionAccounts = CanCorrectPreLiveRoster
            ? RosterWebsiteAccounts.Where(a => !participantAccounts.Contains(a.Id)).SelectMany(a => a.Characters.Select(c => new AccountChoice(a.Id, a.LoginName, c.Id, c.DisplayName, c.SavedEhb))).ToList()
            : [];
        return new TeamsPageState(
            id, EventName, ev.State.ToString(), stage, draftState.ToString(),
            ev.State == EventState.SignupClosed, CanDirectPreEventRosterMutation(ev), endsAt, endsAt is null || endsAt <= now,
            ev.ActualStartedAt, activeCycle?.PublishedAt ?? FirstPublishedAt, activeCycle?.CycleNumber, boardExists,
            Draft.FirstPickRecorded, ActivePickIds.Count > 0 || Draft.FirstPickRecorded, DraftOrderReady, CanDirectFinalize, CanCorrectPreLiveRoster,
            stage == "review" ? reviewRoles : stage != "terminal" && CanChangeCaptainRoles,
            new ControlState(DraftControllerAccountId, DraftControllerName, DraftControllerAccountId is null ? "none" : CanControlDraft ? "me" : "other", DraftControllerLeaseExpiresAt),
            Teams.Select(team => new TeamState(team.Id, team.Name, team.IncludedInDraft, team.DraftPosition, team.Version, team.ProjectedFinalSize, team.HasCurrentCaptain, team.HasUsableCaptain,
                team.Members.Select(m => new MemberState(m.MembershipId, m.ParticipantId, m.Name, m.Ehb, RoleKey(m.Role), m.Version, m.OverallPick, m.Tag)).ToList())).ToList(),
            Participants.Where(x => x.SignupStatus == SignupStatus.Confirmed).Select(p => new ParticipantState(p.Id, p.Name, p.Ehb, p.CaptainVolunteer, p.TeamId, accountNames.GetValueOrDefault(p.Id))).ToList(),
            correctionAccounts,
            CurrentTurn is null ? null : new CurrentTurnState(CurrentTurn.PickNumber, CurrentTurn.RoundNumber, CurrentTurn.TeamId, CurrentTurn.TeamName, totalPicks, next),
            latest is { } l ? new LatestPickState(l.Item1, l.Item2, l.Item3, Participants.FirstOrDefault(x => x.Id == l.Item3)?.Name ?? string.Empty, l.Item4) : null,
            Distribution is { DraftedTeamCount: >= 2 } d ? new DistributionState(d.IncludedParticipants, d.DraftedTeamCount, d.LargerSize, d.SmallerSize, d.LargerTeamCount) : null,
            LoadedBlockers.Select(x => Localize(x)).ToList(), unplaced, ConfirmedCount, AvailableCount);
    }

    private static string RoleKey(TeamMembershipRole role) => role switch { TeamMembershipRole.Captain => "C", TeamMembershipRole.CoCaptain => "CC", _ => "P" };

    public sealed record TeamsPageState(Guid EventId, string EventName, string EventState, string Stage, string DraftState,
        bool SignupClosed, bool RosterOpen, DateTimeOffset? EndsAt, bool EndMissingOrPast, DateTimeOffset? LiveSince, DateTimeOffset? PublishedAt, int? PublicationCycle, bool BoardExists,
        bool FirstPickRecorded, bool EverPicked, bool OrderReady, bool CanDirectFinalize, bool CanCorrect, bool CanChangeRoles,
        ControlState Control, IReadOnlyList<TeamState> Teams, IReadOnlyList<ParticipantState> Participants, IReadOnlyList<AccountChoice> Accounts,
        CurrentTurnState? Turn, LatestPickState? LatestPick, DistributionState? Distribution, IReadOnlyList<string> Blockers, int Unplaced, int Included, int Available);
    public sealed record ControlState(Guid? AccountId, string? Name, string Who, DateTimeOffset? ExpiresAt);
    public sealed record TeamState(Guid Id, string Name, bool Included, int? Position, long Version, int FinalSize, bool HasCaptain, bool HasUsableCaptain, IReadOnlyList<MemberState> Members);
    public sealed record MemberState(Guid Id, Guid ParticipantId, string Name, decimal Ehb, string Role, long Version, int? Pick, string Tag);
    public sealed record ParticipantState(Guid Id, string Name, decimal Ehb, bool Volunteer, Guid? TeamId, string? Account);
    public sealed record AccountChoice(Guid AccountId, string Login, Guid CharacterId, string Name, decimal? Ehb);
    public sealed record TurnState(Guid TeamId, string TeamName);
    public sealed record CurrentTurnState(int PickNumber, int Round, Guid TeamId, string TeamName, int? Total, TurnState? Next);
    public sealed record LatestPickState(Guid PickId, int Number, Guid ParticipantId, string Name, Guid TeamId);
    public sealed record DistributionState(int Included, int Teams, int Larger, int Smaller, int LargerCount);
}
