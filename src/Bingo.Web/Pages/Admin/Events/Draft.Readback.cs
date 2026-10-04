using System.Data;
using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Teams;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Teams;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

public sealed partial class DraftModel
{
    public async Task<IActionResult> OnGetReadbackAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
        if (User.GetAccountId() is not { } actor || !await db.Accounts.AsNoTracking().AnyAsync(x => x.Id == actor && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), ct)) return Forbid();

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.HiddenAt == null, ct);
            if (ev is null) return new JsonResult(new DraftReadback(null));
            CurrentAccountId = actor;
            if (!await Load(id, null, ct)) return new JsonResult(new DraftReadback(null));
            var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
            var teams = await db.Teams.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id)
                .Select(x => new DraftTeamState(x.Id, x.Version, x.Name, x.AffiliationName, x.ActiveImageAssetId, x.IncludedInDraft, x.Active, x.DraftPosition)).ToListAsync(ct);
            var teamIds = teams.Select(x => x.TeamId).ToList();
            var memberships = await db.TeamMemberships.AsNoTracking().Where(x => teamIds.Contains(x.TeamId)).OrderBy(x => x.Id)
                .Select(x => new DraftMembershipState(x.Id, x.TeamId, x.EventParticipantId, x.Role, x.Version, x.AssignedByDraftPickId, x.JoinedAt, x.LeftAt)).ToListAsync(ct);
            var picks = draft is null ? [] : await db.DraftPicks.AsNoTracking().Where(x => x.DraftSessionId == draft.Id).OrderBy(x => x.PickNumber).ThenBy(x => x.Id)
                .Select(x => new DraftPickState(x.Id, x.TeamId, x.EventParticipantId, x.PickNumber, x.RoundNumber, x.PickedAt, x.UndoneAt)).ToListAsync(ct);
            var participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id)
                .Select(x => new DraftParticipantState(x.Id, x.AccountId, x.SignupStatus, x.ResponseVersion)).ToListAsync(ct);
            var characters = await db.EventParticipantCharacters.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id)
                .Select(x => new DraftCharacterState(x.Id, x.EventParticipantId, x.OsrsCharacterId, x.EventRole, x.EhbSnapshot, x.ReleasedAt, x.Version)).ToListAsync(ct);
            var publication = await db.ActiveRosterPublicationAsync(id, ct);
            var management = await db.EventCompetitionManagements.AsNoTracking().Where(x => x.EventId == id)
                .Select(x => new { x.Status, x.LastOperationId, x.UpdatedAt }).SingleOrDefaultAsync(ct);
            var operation = management?.LastOperationId is { } operationId
                ? await db.EventCompetitionManagementOperations.AsNoTracking().Where(x => x.Id == operationId && x.EventId == id)
                    .Select(x => new DraftSynchronizationOperation(x.Id, x.Type, x.Phase, x.EventVersion, x.UpdatedAt, x.SafeErrorCode, x.CreatedAt)).SingleOrDefaultAsync(ct)
                : null;
            var status = management is not null && management.Status != EventCompetitionManagementStatus.Deleted
                ? management.Status.ToString()
                : await db.EventCompetitionSynchronizations.AsNoTracking().AnyAsync(x => x.EventId == id && x.CompetitionId != null, ct) ? "ReadOnly" : "NotManaged";
            // A local roster queue call can fail before an operation exists. Keep
            // its existing recorded outcome separate from the managed operation.
            var localOutcome = await db.AuditEntries.AsNoTracking().Where(x => x.EventId == id && x.Action.StartsWith("roster.finalized_") && x.Action.EndsWith(".wom_sync"))
                .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Select(x => new { x.OccurredAt, x.AfterState }).FirstOrDefaultAsync(ct);
            string? localStatus = null;
            if (localOutcome?.AfterState is { } json)
            {
                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("womStatus", out var value) && value.ValueKind == JsonValueKind.String) localStatus = value.GetString();
                }
                catch (JsonException) { /* An unreadable local outcome does not invalidate the roster state. */ }
            }
            var state = new DraftCurrentState(id, ev.Version, ev.State, draft?.Id, draft?.State ?? DraftState.Setup, draft?.Version,
                draft?.ControllerAccountId, draft?.ControllerLeaseExpiresAt, draft?.ControlVersion, draft?.FirstPickRecordedAt, draft?.RequiresFreshOrder ?? false,
                teams, memberships, picks, participants, characters, CurrentTurn, Teams.Where(x => x.HasUsableCaptain).Select(x => x.Id).ToList(),
                publication?.Id, publication?.CycleNumber, new(status, operation, localStatus, localOutcome?.OccurredAt), publication?.PublishedAt);
            await tx.CommitAsync(ct);
            return new JsonResult(new DraftReadback(state));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new JsonResult(new DraftReadback(null));
        }
    }

    // Authoritative state only. Retain the intended team/pick IDs and every edited
    // field at the client; a matching state is not a receipt or safe-retry signal.
    public sealed record DraftReadback(DraftCurrentState? State) { public bool Known => State is not null; }
    public sealed record DraftCurrentState(Guid EventId, long EventVersion, EventState EventState, Guid? DraftId, DraftState State, long? Version,
        Guid? ControllerId, DateTimeOffset? ControlExpiresAt, long? ControlVersion, DateTimeOffset? FirstPickRecordedAt, bool RequiresFreshOrder,
        IReadOnlyList<DraftTeamState> Teams, IReadOnlyList<DraftMembershipState> Memberships, IReadOnlyList<DraftPickState> Picks, IReadOnlyList<DraftParticipantState> Participants, IReadOnlyList<DraftCharacterState> Characters,
        TurnView? CurrentTurn, IReadOnlyList<Guid> UsableCaptainTeamIds, Guid? RosterPublicationId, int? RosterPublicationCycle,
        DraftRosterSynchronization Synchronization, DateTimeOffset? RosterPublishedAt = null);
    public sealed record DraftTeamState(Guid TeamId, long Version, string Name, string? Affiliation, Guid? ImageAssetId, bool IncludedInDraft, bool Active, int? DraftPosition);
    public sealed record DraftMembershipState(Guid MembershipId, Guid TeamId, Guid ParticipantId, TeamMembershipRole Role, long Version,
        Guid? PickId, DateTimeOffset JoinedAt, DateTimeOffset? LeftAt);
    public sealed record DraftPickState(Guid PickId, Guid TeamId, Guid ParticipantId, int PickNumber, int RoundNumber, DateTimeOffset PickedAt, DateTimeOffset? UndoneAt);
    public sealed record DraftParticipantState(Guid ParticipantId, Guid? AccountId, SignupStatus SignupStatus, int Version);
    public sealed record DraftCharacterState(Guid AssignmentId, Guid ParticipantId, Guid CharacterId, EventCharacterRole Role, decimal? Ehb, DateTimeOffset? ReleasedAt, int Version);
    // These are the last recorded outcomes, never proof that today's roster is synchronized.
    public sealed record DraftRosterSynchronization(string ManagementStatus, DraftSynchronizationOperation? LastOperation, string? LastLocalQueueStatus, DateTimeOffset? LastLocalQueueAt);
    public sealed record DraftSynchronizationOperation(Guid OperationId, EventCompetitionManagementOperationType Type, EventCompetitionManagementOperationPhase Phase,
        long EventVersion, DateTimeOffset UpdatedAt, string? ErrorCode, DateTimeOffset CreatedAt);
}
