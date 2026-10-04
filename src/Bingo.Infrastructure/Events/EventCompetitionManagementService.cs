using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Events;

public sealed partial class EventCompetitionManagementService(
    ApplicationDbContext db,
    IWiseOldManCompetitionManagementClient managementClient,
    IWiseOldManCompetitionClient competitionClient,
    ICompetitionCredentialProtector credentialProtector,
    TimeProvider time) : IEventCompetitionManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(5);

    public async Task<EventCompetitionManagementPreview?> PreviewAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var projection = await BuildProjectionAsync(eventId, cancellationToken);
        return projection?.Preview;
    }

    public async Task<EventCompetitionManagementView?> GetAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var projection = await BuildProjectionAsync(eventId, cancellationToken);
        if (projection is null) return null;
        var storedManagement = await db.EventCompetitionManagements.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
        var management = storedManagement?.Status == EventCompetitionManagementStatus.Deleted ? null : storedManagement;
        var operation = await db.EventCompetitionManagementOperations.AsNoTracking()
            .Where(x => x.EventId == eventId && (management != null
                ? x.Id == management.LastOperationId
                : (storedManagement == null || x.Type == EventCompetitionManagementOperationType.Create && x.Phase != EventCompetitionManagementOperationPhase.Succeeded) &&
                  (projection.Synchronization == null || projection.Synchronization.CompetitionId == null)))
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var pendingCreate = management is null
            && operation?.Type == EventCompetitionManagementOperationType.Create
            && operation.Phase is EventCompetitionManagementOperationPhase.Pending or EventCompetitionManagementOperationPhase.Claimed or EventCompetitionManagementOperationPhase.Sending or EventCompetitionManagementOperationPhase.Retry or EventCompetitionManagementOperationPhase.Unknown;
        var active = management is not null && management.Status != EventCompetitionManagementStatus.Deleted || pendingCreate;
        var provenance = management?.Provenance ?? projection.Synchronization?.Provenance ?? EventCompetitionProvenance.Unknown;
        var writeCapability = management?.WriteCapability
            ?? (projection.Synchronization?.CompetitionId is not null
                ? EventCompetitionWriteCapability.ReadOnly
                : EventCompetitionWriteCapability.Unknown);
        var credentialState = management?.CredentialStatus.ToString()
            ?? (projection.Synchronization?.CompetitionId is not null
                ? EventCompetitionCredentialStatus.NotApplicable.ToString()
                : null);
        var status = management?.Status.ToString()
            ?? operation?.Phase switch
            {
                EventCompetitionManagementOperationPhase.Unknown => "Unknown",
                EventCompetitionManagementOperationPhase.Failed => "Failed",
                EventCompetitionManagementOperationPhase.Cancelled => "Cancelled",
                EventCompetitionManagementOperationPhase.Pending or EventCompetitionManagementOperationPhase.Claimed or EventCompetitionManagementOperationPhase.Sending or EventCompetitionManagementOperationPhase.Retry => "Pending",
                _ => "NotManaged"
            }
            ?? "NotManaged";
        var lastErrorCode = management?.LastErrorCode ?? operation?.SafeErrorCode;
        var lastError = management?.LastError ?? operation?.SafeError;
        return new(
            active,
            status,
            management?.CompetitionId,
            management?.CompetitionTitle,
            management?.CompetitionStartsAt,
            management?.CompetitionEndsAt,
            lastErrorCode,
            lastError,
            management?.LastAppliedAt,
            operation?.Phase is EventCompetitionManagementOperationPhase.Pending or EventCompetitionManagementOperationPhase.Retry or EventCompetitionManagementOperationPhase.Unknown ? operation.NextAttemptAt : null,
            operation?.UpdatedAt,
            operation?.Type.ToString(),
            credentialState,
            projection.Event.ActualStartedAt is not null,
            projection.Preview,
            provenance,
            writeCapability,
            management?.CanWrite == true,
            management?.CanDelete == true && projection.Event.ActualStartedAt is null,
            operation?.Id, operation?.Phase, operation?.Type, operation?.NextAttemptAt,
            management?.CredentialStatus ?? EventCompetitionCredentialStatus.NotApplicable);
    }

    public async Task<EventCompetitionManagementResult> AdoptCredentialAsync(
        Guid eventId,
        long expectedEventVersion,
        string verificationCode,
        LifecycleActor actor,
        CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(actor, cancellationToken);
        var candidate = verificationCode?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
            return new(false, "Enter the Wise Old Man management code.", Status: "Invalid", ErrorCode: "InvalidCredential");

        // If WOM ever adds a dedicated validation operation, use it here. The
        // default client implementation is explicitly Unsupported, so code
        // adoption never manufactures a PUT/DELETE merely to probe a secret.
        var link = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(x => x.EventId == eventId)
            .Select(x => new { x.CompetitionId, x.Provenance })
            .SingleOrDefaultAsync(cancellationToken);
        if (link?.CompetitionId is not { } competitionId)
            return new(false, "Link a Wise Old Man competition before supplying a management code.", Status: "NotLinked", ErrorCode: "NotLinked");
        if (link.Provenance == EventCompetitionProvenance.Unknown)
            return new(false, "The Wise Old Man link provenance is unknown; re-link it before supplying a management code.", Status: "ReadOnly", ErrorCode: "UnknownProvenance");

        WiseOldManCredentialValidationResult validation;
        try
        {
            validation = await managementClient.ValidateCredentialAsync(competitionId, candidate, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            validation = new(WiseOldManCredentialValidationStatus.Unavailable,
                ErrorCode: "CredentialUnavailable", Message: "Wise Old Man could not validate the supplied management code.");
        }

        var protectedCode = credentialProtector.Protect(candidate);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var item = await db.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null) return new(false, "The event was not found.", ErrorCode: "EventMissing");
        if (item.Version != expectedEventVersion)
            return new(false, "This event changed in another request. Reload before changing its WOM credential.", ErrorCode: "StaleEvent");

        var state = await db.EventCompetitionSynchronizations
            .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
        if (state?.CompetitionId is not { } persistedCompetitionId || persistedCompetitionId != competitionId)
            return new(false, "The Wise Old Man link changed before its management code was saved.", ErrorCode: "SourceMismatch");

        var now = time.GetUtcNow();
        var management = await db.EventCompetitionManagements
            .SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
        var wasExisting = management is not null && management.Status != EventCompetitionManagementStatus.Deleted;
        if (management is null)
        {
            management = new EventCompetitionManagement(
                Guid.NewGuid(), eventId, state.Id, competitionId,
                state.CompetitionTitle ?? $"Competition {competitionId}",
                state.CompetitionStartsAt ?? item.EventStartsAt ?? now,
                state.CompetitionEndsAt ?? item.EventEndsAt ?? now.AddHours(1),
                protectedCode, "external-credential", now,
                state.Provenance, EventCompetitionCredentialStatus.Unverified);
            db.EventCompetitionManagements.Add(management);
        }
        else if (management.Status == EventCompetitionManagementStatus.Deleted)
        {
            management.RebindExternalConnection(state.Id, competitionId,
                state.CompetitionTitle ?? $"Competition {competitionId}",
                state.CompetitionStartsAt ?? item.EventStartsAt ?? now,
                state.CompetitionEndsAt ?? item.EventEndsAt ?? now.AddHours(1), protectedCode, now);
        }
        else
        {
            // Provenance is deliberately not changed here. Replacing an
            // external code only changes capability, never ownership.
            management.AdoptProtectedCredential(protectedCode, now);
        }

        ApplyCredentialValidation(management, validation, now);
        item.AdvanceVersion();
        var action = wasExisting ? "event.competition_credential_replaced" : "event.competition_credential_adopted";
        var outcome = validation.Status switch
        {
            WiseOldManCredentialValidationStatus.Valid => "Wise Old Man management capability was validated and stored protected.",
            WiseOldManCredentialValidationStatus.Invalid => "The supplied Wise Old Man management code was rejected.",
            WiseOldManCredentialValidationStatus.Revoked => "The supplied Wise Old Man management code is revoked.",
            WiseOldManCredentialValidationStatus.Unavailable => "Wise Old Man credential validation was unavailable; the code remains unverified.",
            _ => "Wise Old Man management capability was stored protected and remains unverified."
        };
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, action,
            "event", eventId.ToString(), outcome, eventId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var succeeded = validation.Status is WiseOldManCredentialValidationStatus.Valid or WiseOldManCredentialValidationStatus.Unsupported;
        var status = validation.Status switch
        {
            WiseOldManCredentialValidationStatus.Valid => "Valid",
            WiseOldManCredentialValidationStatus.Invalid => "Invalid",
            WiseOldManCredentialValidationStatus.Revoked => "Revoked",
            WiseOldManCredentialValidationStatus.Unavailable => "Unavailable",
            _ => "Unverified"
        };
        return new(succeeded, succeeded ? outcome : validation.Message ?? outcome,
            Status: status, ErrorCode: succeeded ? null : validation.ErrorCode ?? "InvalidCredential");
    }

    public Task<EventCompetitionManagementResult> ReplaceCredentialAsync(
        Guid eventId,
        long expectedEventVersion,
        string verificationCode,
        LifecycleActor actor,
        CancellationToken cancellationToken = default)
        => AdoptCredentialAsync(eventId, expectedEventVersion, verificationCode, actor, cancellationToken);

    public async Task<EventCompetitionManagementResult> CreateAsync(Guid eventId, long expectedEventVersion, LifecycleActor actor, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(actor, cancellationToken);
        var projection = await BuildProjectionAsync(eventId, cancellationToken);
        if (projection is null) return new(false, "The event was not found.");
        if (!projection.Preview.Valid)
            return new(false, string.Join(" ", projection.Preview.Errors.DefaultIfEmpty("The event is not eligible for WOM creation.")), ErrorCode: "InvalidConfiguration");
        if (projection.Event.Version != expectedEventVersion)
            return new(false, "This event changed in another request. Reload before creating its WOM competition.");

        EventCompetitionManagementOperation operation;
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (item is null) return new(false, "The event was not found.");
            if (item.Version != expectedEventVersion) return new(false, "This event changed in another request. Reload before creating its WOM competition.");
            var existingSync = await db.EventCompetitionSynchronizations.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
            var management = await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
            if (existingSync?.CompetitionId is not null && (management is null || management.Status == EventCompetitionManagementStatus.Deleted))
                return new(false, "This event already has a manually linked WOM competition.");
            if (management is not null && management.Status != EventCompetitionManagementStatus.Deleted)
                return new(false, "This event already has a managed WOM competition.");
            var existing = await db.EventCompetitionManagementOperations
                .Where(x => x.EventId == eventId && x.Type == EventCompetitionManagementOperationType.Create
                    && x.Phase != EventCompetitionManagementOperationPhase.Succeeded
                    && x.Phase != EventCompetitionManagementOperationPhase.Failed
                    && x.Phase != EventCompetitionManagementOperationPhase.Cancelled)
                .OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(true, "WOM creation is already in progress.", existing.Id, existing.Phase.ToString());
            }

            var payload = ToPayload(projection.Preview, includeTeams: true);
            operation = new EventCompetitionManagementOperation(
                Guid.NewGuid(), eventId, null, EventCompetitionManagementOperationType.Create,
                JsonSerializer.Serialize(payload, JsonOptions), projection.Preview.Fingerprint, item.Version, time.GetUtcNow());
            operation.SetActor(actor.Id, actor.Username);
            db.EventCompetitionManagementOperations.Add(operation);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await ExecuteAsync(operation.Id, cancellationToken);
    }

    public async Task<EventCompetitionManagementResult> QueueUpdateAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var projection = await BuildProjectionAsync(eventId, cancellationToken);
        if (projection is null) return new(false, "The event was not found.");
        if (await EndUpdatesStoppedAsync(eventId, cancellationToken)) return new(false, "WOM end updates stop at official publication.", Status: "Stopped", ErrorCode: "ResultsPublished");
        var endPending = projection.Synchronization?.EndUpdateStatus == EventCompetitionEndUpdateStatus.Pending;
        var management = await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
        if (management is null || management.Status == EventCompetitionManagementStatus.Deleted)
        {
            if (endPending) await RejectPendingEndAsync(eventId, projection.Synchronization!.EndUpdateTargetAt, "MissingCredential", cancellationToken);
            if (projection.Synchronization?.CompetitionId is not null)
                return new(true, "The linked Wise Old Man competition is read-only: data can be fetched, but local schedule and roster changes cannot be synchronized upstream.", Status: "ReadOnly", ErrorCode: "ReadOnly");
            return new(true, Status: "NotManaged");
        }
        if (!management.CanWrite)
        {
            var (code, message, status) = management.CredentialStatus switch
            {
                EventCompetitionCredentialStatus.Invalid => ("InvalidCredential", "The Wise Old Man management code was rejected; no upstream write was queued.", "Invalid"),
                EventCompetitionCredentialStatus.Revoked => ("CredentialRevoked", "The Wise Old Man management code is revoked; no upstream write was queued.", "Revoked"),
                EventCompetitionCredentialStatus.Unavailable => ("CredentialUnavailable", "The protected Wise Old Man management code is unavailable; no upstream write was queued.", "Unavailable"),
                _ => ("ReadOnly", "This Wise Old Man connection is read-only; no upstream write was queued.", "ReadOnly")
            };
            if (endPending) await RejectPendingEndAsync(eventId, projection.Synchronization!.EndUpdateTargetAt, code, cancellationToken);
            return new(false, message, Status: status, ErrorCode: code);
        }
        if (management.Status is EventCompetitionManagementStatus.Unknown or EventCompetitionManagementStatus.Conflict
            || management.Status == EventCompetitionManagementStatus.Failed && !string.Equals(management.LastErrorCode, "InvalidConfiguration", StringComparison.Ordinal)
                && !(endPending && projection.Synchronization!.EndUpdateRequestedAt > management.LastErrorAt))
            return new(false, management.LastError ?? "Managed WOM synchronization is paused pending Admin recovery.", Status: management.Status.ToString());
        if (projection.Synchronization?.CompetitionId != management.CompetitionId)
        {
            management.MarkFailure(management.LastOperationId ?? Guid.Empty, EventCompetitionManagementStatus.Conflict, "SourceMismatch", "The managed WOM source link no longer matches its protected management record.", time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return new(false, management.LastError, Status: "Conflict");
        }
        if (projection.Event.IsHidden || projection.Event.State is EventState.Cancelled or EventState.Discarded)
            return new(false, "Managed WOM updates are unavailable for this event state.", Status: "Failed");
        var includeTeams = projection.Event.ActualStartedAt is null;
        if (includeTeams && !projection.Preview.IsFinalizedPreLive)
            return new(true, "Managed WOM roster updates are suspended until the published draft is finalized again.", Status: "Suspended");
        var errors = WiseOldManCompetitionRules.Validate(
            projection.Preview.Title,
            projection.Preview.StartsAt,
            projection.Preview.EndsAt,
            projection.Preview.Teams.Select(team => new WiseOldManCompetitionWriteTeam(team.Name, team.Participants)).ToArray(),
            time.GetUtcNow());
        if (includeTeams && errors.Count != 0)
        {
            management.MarkFailure(management.LastOperationId ?? Guid.Empty, EventCompetitionManagementStatus.Failed, "InvalidConfiguration", string.Join(" ", errors), time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return new(false, string.Join(" ", errors), Status: "Failed");
        }

        var fingerprint = includeTeams
            ? projection.Preview.Fingerprint
            : WiseOldManCompetitionRules.Fingerprint(new { projection.Preview.Title, projection.Preview.StartsAt, projection.Preview.EndsAt, RosterLocked = true });
        if (!endPending && string.Equals(management.LastAppliedLocalFingerprint, fingerprint, StringComparison.Ordinal))
        {
            if (management.CredentialStatus == EventCompetitionCredentialStatus.Unverified)
                return new(true, "The protected Wise Old Man management code remains unverified; no synthetic validation write was issued.", Status: "Unverified", ErrorCode: "CredentialUnverified");
            return new(true, Status: "Unchanged");
        }

        if (await HasSharedCompetitionReferenceAsync(eventId, management.CompetitionId, cancellationToken))
            return new(false, "This managed WOM competition is referenced by another event; automatic updates are paused.", Status: "Conflict", ErrorCode: "SharedSource");

        var payload = ToPayload(projection.Preview, includeTeams);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var lockedManagement = await db.EventCompetitionManagements.FromSqlInterpolated($"SELECT * FROM event_competition_management WHERE event_id = {eventId} FOR UPDATE").SingleAsync(cancellationToken);
        lockedManagement.ObserveActualStart(projection.Event.ActualStartedAt, time.GetUtcNow());
        if (await HasSharedCompetitionReferenceAsync(eventId, lockedManagement.CompetitionId, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(false, "This managed WOM competition is referenced by another event; automatic updates are paused.", Status: "Conflict", ErrorCode: "SharedSource");
        }
        var activeDelete = await db.EventCompetitionManagementOperations.AsNoTracking()
            .Where(x => x.EventId == eventId && x.Type == EventCompetitionManagementOperationType.Delete
                && x.Phase != EventCompetitionManagementOperationPhase.Succeeded
                && x.Phase != EventCompetitionManagementOperationPhase.Failed
                && x.Phase != EventCompetitionManagementOperationPhase.Cancelled)
            .OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
        if (activeDelete is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true, "WOM deletion is already in progress.", activeDelete.Id, activeDelete.Phase.ToString());
        }
        var inFlight = await db.EventCompetitionManagementOperations.AsNoTracking()
            .Where(x => x.EventId == eventId && x.Type == EventCompetitionManagementOperationType.Update
                && (x.Phase == EventCompetitionManagementOperationPhase.Claimed || x.Phase == EventCompetitionManagementOperationPhase.Sending))
            .OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
        if (inFlight is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true, "A WOM update is already in flight; the latest local state will be picked up after it completes.", inFlight.Id, inFlight.Phase.ToString());
        }
        var pending = await db.EventCompetitionManagementOperations
            .Where(x => x.EventId == eventId && x.Type == EventCompetitionManagementOperationType.Update
                && (x.Phase == EventCompetitionManagementOperationPhase.Pending || x.Phase == EventCompetitionManagementOperationPhase.Retry))
            .OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
        if (pending is not null)
        {
            // Repeated worker/readback requests must not erase a persisted backoff.
            if (pending.DesiredFingerprint == fingerprint && pending.EventVersion == projection.Event.Version)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(true, OperationId: pending.Id, Status: pending.Phase.ToString(), RetryAt: pending.NextAttemptAt);
            }
            pending.ReplaceDesired(payloadJson, fingerprint, projection.Event.Version, time.GetUtcNow(),
                resetAttempts: endPending && DeserializePayload(pending.DesiredPayloadJson).EndsAt != payload.EndsAt);
            lockedManagement.MarkPending(pending.Id, time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, "WOM update coalesced.", pending.Id, pending.Phase.ToString());
        }
        var operation = new EventCompetitionManagementOperation(Guid.NewGuid(), eventId, lockedManagement.Id, EventCompetitionManagementOperationType.Update, payloadJson, fingerprint, projection.Event.Version, time.GetUtcNow());
        db.EventCompetitionManagementOperations.Add(operation);
        lockedManagement.MarkPending(operation.Id, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ExecuteAsync(operation.Id, cancellationToken);
    }

    public async Task<EventCompetitionManagementResult> DeleteAsync(Guid eventId, long expectedEventVersion, long targetCompetitionId, bool confirmed, LifecycleActor actor, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(actor, cancellationToken);
        if (!confirmed) return new(false, "Confirm the exact WOM competition before deleting it.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (item is null) return new(false, "The event was not found.");
        if (item.Version != expectedEventVersion) return new(false, "This event changed in another request. Reload before deleting its WOM competition.");
        if (item.ActualStartedAt is not null || item.State == EventState.Live)
            return new(false, "A WOM competition cannot be deleted after the event has started.");
        var management = await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
        if (management is null || management.Status == EventCompetitionManagementStatus.Deleted || management.CompetitionId != targetCompetitionId)
            return new(false, "The selected WOM competition is not the current managed competition.");
        if (management.Provenance != EventCompetitionProvenance.WebsiteCreated)
            return new(false, "An externally created Wise Old Man competition can never be deleted remotely.", Status: "ReadOnly", ErrorCode: "ExternalDeleteDenied");
        if (!management.CanDelete)
            return new(false, "This Wise Old Man competition does not currently have an eligible protected management credential.", Status: "Failed", ErrorCode: "CredentialUnavailable");
        if (management.ActualStartedAt is not null)
            return new(false, "A WOM competition cannot be deleted after the event has started.");
        if (string.IsNullOrWhiteSpace(management.ProtectedVerificationCode))
            return new(false, "This WOM competition has no protected management credential.");
        var shared = await db.EventCompetitionManagements.AnyAsync(x => x.EventId != eventId && x.CompetitionId == targetCompetitionId && x.Status != EventCompetitionManagementStatus.Deleted, cancellationToken)
            || await db.EventCompetitionSynchronizations.AnyAsync(x => x.EventId != eventId && x.CompetitionId == targetCompetitionId, cancellationToken);
        if (shared) return new(false, "This WOM competition is shared by another event and cannot be deleted here.");
        var existing = await db.EventCompetitionManagementOperations
            .Where(x => x.EventId == eventId
                && x.Phase != EventCompetitionManagementOperationPhase.Succeeded
                && x.Phase != EventCompetitionManagementOperationPhase.Failed
                && x.Phase != EventCompetitionManagementOperationPhase.Cancelled)
            .OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            var message = existing.Type == EventCompetitionManagementOperationType.Delete
                ? "WOM deletion is already in progress."
                : "A WOM update is already in progress; deletion will remain blocked until it completes.";
            return new(true, message, existing.Id, existing.Phase.ToString());
        }
        var payload = JsonSerializer.Serialize(new DeletePayload(targetCompetitionId), JsonOptions);
        var operation = new EventCompetitionManagementOperation(Guid.NewGuid(), eventId, management.Id, EventCompetitionManagementOperationType.Delete, payload, management.LastAppliedLocalFingerprint, item.Version, time.GetUtcNow());
        operation.SetActor(actor.Id, actor.Username);
        db.EventCompetitionManagementOperations.Add(operation);
        management.MarkPending(operation.Id, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ExecuteAsync(operation.Id, cancellationToken);
    }

    public async Task ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var expiredClaims = await db.EventCompetitionManagementOperations.AsNoTracking()
            .Where(x => x.Phase == EventCompetitionManagementOperationPhase.Sending && x.SendingAt != null && x.SendingAt <= now - ClaimTimeout)
            .OrderBy(x => x.SendingAt).Select(x => x.Id).Take(50).ToListAsync(cancellationToken);
        foreach (var operationId in expiredClaims)
        {
            try { await MarkExpiredClaimUnknownAsync(operationId, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        }

        var reconciliation = await db.EventCompetitionManagementOperations.AsNoTracking()
            .Where(x => x.Phase == EventCompetitionManagementOperationPhase.Unknown
                && x.NextAttemptAt != null && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).Select(x => x.Id).Take(50).ToListAsync(cancellationToken);
        foreach (var operationId in reconciliation)
        {
            try { await ReconcileUnknownAsync(operationId, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        }

        var due = await db.EventCompetitionManagementOperations.AsNoTracking()
            .Where(x => (x.Phase == EventCompetitionManagementOperationPhase.Pending || x.Phase == EventCompetitionManagementOperationPhase.Retry) && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt).Select(x => x.Id).Take(50).ToListAsync(cancellationToken);
        foreach (var operationId in due)
        {
            try { await ExecuteAsync(operationId, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        }

        var pendingEndEvents = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(x => x.EndUpdateStatus == EventCompetitionEndUpdateStatus.Pending)
            .Select(x => x.EventId).Take(50).ToListAsync(cancellationToken);
        foreach (var eventId in pendingEndEvents)
            await QueueUpdateAsync(eventId, cancellationToken);

        var managedEventIds = await db.EventCompetitionManagements.AsNoTracking()
            .Where(x => (x.Status == EventCompetitionManagementStatus.Active || x.Status == EventCompetitionManagementStatus.Pending)
                || x.Status == EventCompetitionManagementStatus.Failed && x.LastErrorCode == "InvalidConfiguration")
            .Join(db.Events.AsNoTracking().Where(x => x.HiddenAt == null && x.State != EventState.Cancelled && x.State != EventState.Discarded), x => x.EventId, x => x.Id, (x, _) => x.EventId)
            .Take(50).ToListAsync(cancellationToken);
        foreach (var eventId in managedEventIds)
        {
            try { await QueueUpdateAsync(eventId, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        }
    }

    private async Task<EventCompetitionManagementResult> ExecuteAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var claimed = await ClaimAsync(operationId, cancellationToken);
        if (claimed is null) return new(true, Status: "Sending", OperationId: operationId);
        var claim = claimed.Value;
        var operation = claim.Operation;
        if (operation.Type == EventCompetitionManagementOperationType.Delete && (claim.Event.ActualStartedAt is not null || claim.Management?.ActualStartedAt is not null))
            return await FailOperationAsync(operation.Id, "LiveLocked", "A WOM delete cannot be dispatched after the event has started.", EventCompetitionManagementStatus.Failed, cancellationToken);

        WiseOldManCompetitionWriteResult result;
        if (operation.Type == EventCompetitionManagementOperationType.Create)
        {
            var createValidation = await ValidateCreateDispatchAsync(operation, cancellationToken);
            if (createValidation is not null)
                return await FailOperationAsync(operation.Id, createValidation.Code, createValidation.Message, EventCompetitionManagementStatus.Failed, cancellationToken);
            var payload = DeserializePayload(operation.DesiredPayloadJson);
            result = await managementClient.CreateAsync(payload, cancellationToken);
            if (result.Status == WiseOldManCompetitionWriteStatus.Unknown)
                return await MarkUnknownAsync(operation.Id, result.ErrorCode ?? "UnknownOutcome", result.Message ?? "The WOM creation outcome is unknown.", null, cancellationToken);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.ProtectedVerificationCode))
                return await HandleResultFailureAsync(operation.Id, result, cancellationToken);
            return await PersistProviderReceiptWithRetryAsync(
                () => CompleteCreateAsync(operation.Id, operation, payload, result, cancellationToken),
                cancellationToken);
        }

        var management = claim.Management;
        if (management is null) return await FailOperationAsync(operation.Id, "MissingManagement", "The managed WOM record is missing.", EventCompetitionManagementStatus.Failed, cancellationToken);
        if (operation.Type == EventCompetitionManagementOperationType.Delete && management.Provenance != EventCompetitionProvenance.WebsiteCreated)
            return await FailOperationAsync(operation.Id, "ExternalDeleteDenied", "An externally created Wise Old Man competition can never be deleted remotely.", EventCompetitionManagementStatus.Failed, cancellationToken);
        if (operation.Type == EventCompetitionManagementOperationType.Update && !management.CanWrite)
            return await FailOperationAsync(operation.Id, management.CredentialStatus switch
            {
                EventCompetitionCredentialStatus.Invalid => "InvalidCredential",
                EventCompetitionCredentialStatus.Revoked => "CredentialRevoked",
                EventCompetitionCredentialStatus.Unavailable => "CredentialUnavailable",
                _ => "ReadOnly"
            }, "The Wise Old Man connection is not currently writable; no upstream write was sent.", EventCompetitionManagementStatus.Failed, cancellationToken);
        await using var competitionLock = await CompetitionReferenceLock.AcquireAsync(db, management.CompetitionId, cancellationToken);
        var dispatchValidation = operation.Type == EventCompetitionManagementOperationType.Delete
            ? await ValidateDeleteDispatchAsync(operation, management, cancellationToken)
            : await ValidateUpdateDispatchAsync(operation, management, cancellationToken);
        if (dispatchValidation is not null)
        {
            if (operation.Type == EventCompetitionManagementOperationType.Update
                && dispatchValidation.Code == "StaleUpdate")
                return await RefreshStaleUpdateAsync(operation.Id, cancellationToken);
            return await FailOperationAsync(operation.Id, dispatchValidation.Code, dispatchValidation.Message, dispatchValidation.Status, cancellationToken);
        }
        var sourceCompetitionId = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(x => x.EventId == operation.EventId)
            .Select(x => x.CompetitionId)
            .SingleOrDefaultAsync(cancellationToken);
        if (sourceCompetitionId != management.CompetitionId)
            return await FailOperationAsync(operation.Id, "SourceMismatch", "The managed WOM source link no longer matches its protected management record.", EventCompetitionManagementStatus.Conflict, cancellationToken);
        string verificationCode;
        try { verificationCode = credentialProtector.Unprotect(management.ProtectedVerificationCode); }
        catch { return await FailOperationAsync(operation.Id, "CredentialUnavailable", "The protected WOM management credential could not be opened.", EventCompetitionManagementStatus.Failed, cancellationToken); }
        if (string.IsNullOrWhiteSpace(verificationCode)) return await FailOperationAsync(operation.Id, "CredentialUnavailable", "The protected WOM management credential is empty.", EventCompetitionManagementStatus.Failed, cancellationToken);

        if (operation.Type == EventCompetitionManagementOperationType.Delete)
        {
            result = await managementClient.DeleteAsync(management.CompetitionId, verificationCode, cancellationToken);
            if (result.Status == WiseOldManCompetitionWriteStatus.Unknown)
            {
                var read = await competitionClient.GetCompetitionAsync(management.CompetitionId, cancellationToken);
                if (read.Status == WiseOldManCompetitionStatus.NotFound)
                    return await PersistProviderReceiptWithRetryAsync(
                        () => CompleteDeleteAsync(operation.Id, operation, cancellationToken),
                        cancellationToken);
                return await MarkUnknownAsync(operation.Id, RedactProviderText(result.ErrorCode, verificationCode) ?? "UnknownOutcome", RedactProviderText(result.Message, verificationCode) ?? "The WOM deletion outcome is unknown.", read.RetryAt, cancellationToken);
            }
            if (result.Status == WiseOldManCompetitionWriteStatus.NotFound)
                return await PersistProviderReceiptWithRetryAsync(
                    () => CompleteDeleteAsync(operation.Id, operation, cancellationToken),
                    cancellationToken);
            if (!result.Succeeded) return await HandleResultFailureAsync(operation.Id, result, cancellationToken, verificationCode);
            return await PersistProviderReceiptWithRetryAsync(
                () => CompleteDeleteAsync(operation.Id, operation, cancellationToken),
                cancellationToken);
        }

        var updatePayload = DeserializePayload(operation.DesiredPayloadJson);
        var currentProjection = await BuildProjectionAsync(operation.EventId, cancellationToken);
        if (updatePayload.IncludeTeams
            && claim.Event.ActualStartedAt is null
            && currentProjection is not null
            && currentProjection.Event.ActualStartedAt is null
            && !currentProjection.Preview.IsFinalizedPreLive)
            return await SuspendRosterOperationAsync(operation.Id, "The published draft was reopened before this WOM roster update was dispatched.", cancellationToken);
        if (currentProjection?.Event.ActualStartedAt is not null && updatePayload.IncludeTeams && currentProjection is not null)
            updatePayload = ToPayload(currentProjection.Preview, includeTeams: false);
        var sourceCheck = await CheckManagedSourceAsync(operation.Id, management, cancellationToken);
        if (sourceCheck is not null) return sourceCheck;
        result = await managementClient.UpdateAsync(management.CompetitionId, updatePayload, verificationCode, cancellationToken);
        if (result.Status == WiseOldManCompetitionWriteStatus.Unknown)
        {
            var read = await competitionClient.GetCompetitionAsync(management.CompetitionId, cancellationToken);
            if (read.Succeeded && Matches(read.Competition!, updatePayload))
                return await PersistProviderReceiptWithRetryAsync(
                    () => CompleteUpdateAsync(operation.Id, operation, updatePayload, read.Competition!, cancellationToken),
                    cancellationToken);
            if (read.Succeeded && RemoteConfigurationMatches(read.Competition!, management))
            {
                var retry = await RetryUnappliedEndAsync(operation.Id, reconciled: false, result.RetryAt, cancellationToken);
                if (retry is not null) return retry;
            }
            return await MarkUnknownAsync(operation.Id, RedactProviderText(result.ErrorCode, verificationCode) ?? "UnknownOutcome", RedactProviderText(result.Message, verificationCode) ?? "The WOM update outcome is unknown.", read.RetryAt, cancellationToken);
        }
        if (!result.Succeeded) return await HandleResultFailureAsync(operation.Id, result, cancellationToken, verificationCode);
        if (result.Competition!.Id != management.CompetitionId || !MatchesWriteReceipt(result.Competition, updatePayload))
            return await MarkUnknownAsync(operation.Id, "MismatchedReceipt", "The WOM update response did not confirm the requested window.", null, cancellationToken);
        return await PersistProviderReceiptWithRetryAsync(
            () => CompleteUpdateAsync(operation.Id, operation, updatePayload, result.Competition!, cancellationToken),
            cancellationToken);
    }

    private async Task<DispatchFailure?> ValidateCreateDispatchAsync(
        EventCompetitionManagementOperation operation,
        CancellationToken cancellationToken)
    {
        if (!await IsEnabledAdminAsync(operation.ActorAccountId, cancellationToken))
            return new("AdminRevoked", "The originating Admin is no longer enabled for this WOM operation.", EventCompetitionManagementStatus.Failed);

        var projection = await BuildProjectionAsync(operation.EventId, cancellationToken);
        if (projection is null)
            return new("EventMissing", "The event no longer exists for this WOM operation.", EventCompetitionManagementStatus.Failed);
        if (projection.Event.Version != operation.EventVersion || projection.Preview.Fingerprint != operation.DesiredFingerprint)
            return new("StaleCreate", "The event changed before the WOM creation was dispatched; review the current preview and retry.", EventCompetitionManagementStatus.Conflict);
        if (!projection.Preview.Valid)
            return new("InvalidConfiguration", "The event is no longer eligible for WOM creation.", EventCompetitionManagementStatus.Failed);
        if (projection.Synchronization?.CompetitionId is not null)
            return new("SourceAlreadyLinked", "The event acquired a WOM source before creation was dispatched.", EventCompetitionManagementStatus.Conflict);
        if (projection.Management is not null && projection.Management.Status != EventCompetitionManagementStatus.Deleted)
            return new("AlreadyManaged", "The event already has a managed WOM competition.", EventCompetitionManagementStatus.Conflict);
        return null;
    }

    private async Task<DispatchFailure?> ValidateDeleteDispatchAsync(
        EventCompetitionManagementOperation operation,
        EventCompetitionManagement management,
        CancellationToken cancellationToken)
    {
        if (!await IsEnabledAdminAsync(operation.ActorAccountId, cancellationToken))
            return new("AdminRevoked", "The originating Admin is no longer enabled for this WOM operation.", EventCompetitionManagementStatus.Failed);

        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operation.EventId && x.HiddenAt == null, cancellationToken);
        if (item is null)
            return new("EventMissing", "The event no longer exists for this WOM operation.", EventCompetitionManagementStatus.Failed);
        if (item.Version != operation.EventVersion)
            return new("StaleDelete", "The event changed before the WOM deletion was dispatched; reload and confirm the current competition.", EventCompetitionManagementStatus.Conflict);
        if (item.ActualStartedAt is not null || item.State == EventState.Live)
            return new("LiveLocked", "A WOM delete cannot be dispatched after the event has started.", EventCompetitionManagementStatus.Failed);

        var currentManagement = await db.EventCompetitionManagements.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == operation.EventId, cancellationToken);
        if (currentManagement is null || currentManagement.Id != operation.ManagementId || currentManagement.Status == EventCompetitionManagementStatus.Deleted)
            return new("ManagementChanged", "The managed WOM record changed before deletion was dispatched.", EventCompetitionManagementStatus.Conflict);
        if (currentManagement.Provenance != EventCompetitionProvenance.WebsiteCreated)
            return new("ExternalDeleteDenied", "An externally created Wise Old Man competition can never be deleted remotely.", EventCompetitionManagementStatus.Failed);
        if (!currentManagement.CanDelete)
            return new("CredentialUnavailable", "This Wise Old Man competition does not currently have an eligible protected management credential.", EventCompetitionManagementStatus.Failed);
        if (currentManagement.CompetitionId != management.CompetitionId || currentManagement.LastAppliedLocalFingerprint != operation.DesiredFingerprint)
            return new("ManagementChanged", "The managed WOM record changed before deletion was dispatched.", EventCompetitionManagementStatus.Conflict);
        var target = DeserializeDeletePayload(operation.DesiredPayloadJson).CompetitionId;
        if (target != currentManagement.CompetitionId)
            return new("TargetChanged", "The selected WOM competition changed before deletion was dispatched.", EventCompetitionManagementStatus.Conflict);
        var sourceCompetitionId = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(x => x.EventId == operation.EventId).Select(x => x.CompetitionId).SingleOrDefaultAsync(cancellationToken);
        if (sourceCompetitionId != currentManagement.CompetitionId)
            return new("SourceMismatch", "The managed WOM source link changed before deletion was dispatched.", EventCompetitionManagementStatus.Conflict);
        if (await HasSharedCompetitionReferenceAsync(operation.EventId, target, cancellationToken))
            return new("SharedSource", "This WOM competition is shared by another event and cannot be deleted here.", EventCompetitionManagementStatus.Conflict);
        return null;
    }

    private async Task<DispatchFailure?> ValidateUpdateDispatchAsync(
        EventCompetitionManagementOperation operation,
        EventCompetitionManagement management,
        CancellationToken cancellationToken)
    {
        var currentManagement = await db.EventCompetitionManagements.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == operation.EventId, cancellationToken);
        if (currentManagement is null || currentManagement.Id != operation.ManagementId || currentManagement.Status == EventCompetitionManagementStatus.Deleted)
            return new("ManagementChanged", "The managed WOM record changed before the update was dispatched.", EventCompetitionManagementStatus.Conflict);
        if (currentManagement.CompetitionId != management.CompetitionId)
            return new("ManagementChanged", "The managed WOM record changed before the update was dispatched.", EventCompetitionManagementStatus.Conflict);
        if (!currentManagement.CanWrite)
            return new(currentManagement.CredentialStatus switch
            {
                EventCompetitionCredentialStatus.Invalid => "InvalidCredential",
                EventCompetitionCredentialStatus.Revoked => "CredentialRevoked",
                EventCompetitionCredentialStatus.Unavailable => "CredentialUnavailable",
                _ => "ReadOnly"
            }, "The Wise Old Man connection is not currently writable; no upstream write was sent.", EventCompetitionManagementStatus.Failed);
        if (await HasSharedCompetitionReferenceAsync(operation.EventId, management.CompetitionId, cancellationToken))
            return new("SharedSource", "This managed WOM competition is referenced by another event; automatic updates are paused.", EventCompetitionManagementStatus.Conflict);

        var projection = await BuildProjectionAsync(operation.EventId, cancellationToken);
        if (projection is null)
            return new("EventMissing", "The event no longer exists for this WOM operation.", EventCompetitionManagementStatus.Failed);
        var payload = DeserializePayload(operation.DesiredPayloadJson);
        if (projection.Event.ActualStartedAt is not null && payload.IncludeTeams)
            return new("StaleUpdate", "The event changed before the WOM update was dispatched; the latest state will be queued again.", EventCompetitionManagementStatus.Conflict);
        var currentFingerprint = LocalFingerprint(projection, payload.IncludeTeams);
        if (projection.Event.Version != operation.EventVersion || currentFingerprint != operation.DesiredFingerprint)
            return new("StaleUpdate", "The event changed before the WOM update was dispatched; the latest state will be queued again.", EventCompetitionManagementStatus.Conflict);
        if (projection.Event.IsHidden || projection.Event.State is EventState.Cancelled or EventState.Discarded)
            return new("InvalidState", "Managed WOM updates are unavailable for this event state.", EventCompetitionManagementStatus.Failed);
        if (payload.IncludeTeams && !projection.Preview.IsFinalizedPreLive)
            return new("DraftReopened", "The published draft was reopened before this WOM roster update was dispatched.", EventCompetitionManagementStatus.Active);
        return null;
    }

    private async Task<EventCompetitionManagementResult> RefreshStaleUpdateAsync(Guid operationId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var operation = await db.EventCompetitionManagementOperations
            .FromSqlInterpolated($"SELECT * FROM event_competition_management_operations WHERE id = {operationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (operation is null || operation.Type != EventCompetitionManagementOperationType.Update
            || operation.Phase != EventCompetitionManagementOperationPhase.Sending)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true, "The WOM update is no longer pending dispatch.", operationId, operation?.Phase.ToString() ?? "Unknown");
        }

        var item = await db.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {operation.EventId} AND hidden_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var management = operation.ManagementId is { } managementId
            ? await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken)
            : null;
        var now = time.GetUtcNow();
        if (item is null)
        {
            operation.Fail("EventMissing", "The event no longer exists for this WOM operation.", now);
            management?.MarkFailure(operationId, EventCompetitionManagementStatus.Failed, "EventMissing", "The event no longer exists for this WOM operation.", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, "The event no longer exists for this WOM operation.", operationId, "Failed", ErrorCode: "EventMissing");
        }
        if (management is null)
        {
            operation.Fail("MissingManagement", "The managed WOM record is missing.", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, "The managed WOM record is missing.", operationId, "Failed", ErrorCode: "MissingManagement");
        }

        var projection = await BuildProjectionAsync(operation.EventId, cancellationToken);
        if (projection is null)
        {
            operation.Fail("EventMissing", "The event no longer exists for this WOM operation.", now);
            management.MarkFailure(operationId, EventCompetitionManagementStatus.Failed, "EventMissing", "The event no longer exists for this WOM operation.", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, "The event no longer exists for this WOM operation.", operationId, "Failed", ErrorCode: "EventMissing");
        }
        if (projection.Event.IsHidden || projection.Event.State is EventState.Cancelled or EventState.Discarded)
        {
            const string code = "InvalidState";
            const string message = "Managed WOM updates are unavailable for this event state.";
            operation.Fail(code, message, now);
            management.MarkFailure(operationId, EventCompetitionManagementStatus.Failed, code, message, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, message, operationId, "Failed", ErrorCode: code);
        }

        var includeTeams = projection.Event.ActualStartedAt is null;
        if (includeTeams && !projection.Preview.IsFinalizedPreLive)
        {
            const string code = "DraftReopened";
            const string message = "The published draft was reopened before this WOM roster update was dispatched.";
            operation.Fail(code, message, now);
            management.MarkFailure(operationId, EventCompetitionManagementStatus.Active, code, message, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, message, operationId, "Failed", ErrorCode: code);
        }

        var errors = WiseOldManCompetitionRules.Validate(
            projection.Preview.Title,
            projection.Preview.StartsAt,
            projection.Preview.EndsAt,
            projection.Preview.Teams.Select(team => new WiseOldManCompetitionWriteTeam(team.Name, team.Participants)).ToArray(),
            now);
        if (includeTeams && errors.Count != 0)
        {
            const string code = "InvalidConfiguration";
            var message = string.Join(" ", errors);
            operation.Fail(code, message, now);
            management.MarkFailure(operationId, EventCompetitionManagementStatus.Failed, code, message, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, message, operationId, "Failed", ErrorCode: code);
        }

        var payload = ToPayload(projection.Preview, includeTeams);
        operation.RequeueCurrent(JsonSerializer.Serialize(payload, JsonOptions), LocalFingerprint(projection, includeTeams), item.Version, now,
            resetAttempts: projection.Synchronization?.EndUpdateStatus == EventCompetitionEndUpdateStatus.Pending
                && DeserializePayload(operation.DesiredPayloadJson).EndsAt != payload.EndsAt);
        management.MarkPending(operationId, now);
        management.ObserveActualStart(item.ActualStartedAt, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, "The WOM update was refreshed from the current event state and queued again.", operationId, "Pending");
    }

    private async Task<bool> IsEnabledAdminAsync(Guid? accountId, CancellationToken cancellationToken)
        => accountId is { } id && await db.Accounts.AsNoTracking().AnyAsync(x => x.Id == id && x.Active
            && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), cancellationToken);

    private async Task<bool> HasSharedCompetitionReferenceAsync(Guid eventId, long competitionId, CancellationToken cancellationToken)
        => await db.EventCompetitionManagements.AsNoTracking().AnyAsync(x => x.EventId != eventId
            && x.CompetitionId == competitionId && x.Status != EventCompetitionManagementStatus.Deleted, cancellationToken)
            || await db.EventCompetitionSynchronizations.AsNoTracking().AnyAsync(x => x.EventId != eventId
                && x.CompetitionId == competitionId, cancellationToken);

    private static string LocalFingerprint(Projection projection, bool includeTeams)
        => includeTeams
            ? projection.Preview.Fingerprint
            : WiseOldManCompetitionRules.Fingerprint(new { projection.Preview.Title, projection.Preview.StartsAt, projection.Preview.EndsAt, RosterLocked = true });

    private async Task ReconcileUnknownAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var operation = await db.EventCompetitionManagementOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
        if (operation is null || operation.Phase != EventCompetitionManagementOperationPhase.Unknown) return;
        if (operation.Type == EventCompetitionManagementOperationType.Update && await EndUpdatesStoppedAsync(operation.EventId, cancellationToken)) return;
        if (operation.Type == EventCompetitionManagementOperationType.Create)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var current = await db.EventCompetitionManagementOperations.SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
            current?.ClearReconciliation(time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var management = operation.ManagementId is { } managementId
            ? await db.EventCompetitionManagements.AsNoTracking().SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken)
            : null;
        if (management is null)
        {
            await FailOperationAsync(operationId, "MissingManagement", "The managed WOM record is missing.", EventCompetitionManagementStatus.Failed, cancellationToken);
            return;
        }

        var read = await competitionClient.GetCompetitionAsync(management.CompetitionId, cancellationToken);
        if (operation.Type == EventCompetitionManagementOperationType.Delete)
        {
            if (read.Status == WiseOldManCompetitionStatus.NotFound)
            {
                await PersistProviderReceiptWithRetryAsync(() => CompleteDeleteAsync(operationId, operation, cancellationToken), cancellationToken);
                return;
            }
            if (read.Status is WiseOldManCompetitionStatus.RateLimited or WiseOldManCompetitionStatus.Unavailable)
            {
                await RescheduleUnknownAsync(operationId, "ReconciliationUnavailable", read.Message ?? "WOM deletion reconciliation is temporarily unavailable.", read.RetryAt, cancellationToken);
                return;
            }
            if (operation.AttemptCount >= 3)
            {
                await FailOperationAsync(operationId, "DeleteReconciliationRequired", "The WOM deletion outcome remains unresolved; Admin review is required.", EventCompetitionManagementStatus.Conflict, cancellationToken);
                return;
            }
            await RescheduleUnknownAsync(operationId, "DeleteStillPresent", "The WOM competition still exists; deletion was not resent and will be checked again.", null, cancellationToken);
            return;
        }

        var payload = DeserializePayload(operation.DesiredPayloadJson);
        if (read.Succeeded && Matches(read.Competition!, payload))
        {
            await PersistProviderReceiptWithRetryAsync(() => CompleteUpdateAsync(operationId, operation, payload, read.Competition!, cancellationToken), cancellationToken);
            return;
        }
        if (read.Succeeded && RemoteConfigurationMatches(read.Competition!, management))
        {
            var retry = await RetryUnappliedEndAsync(operationId, reconciled: true, read.RetryAt, cancellationToken);
            if (retry is not null) return;
        }
        if (read.Status == WiseOldManCompetitionStatus.NotFound)
        {
            await FailOperationAsync(operationId, "SourceMissing", "The managed WOM competition no longer exists; automatic updates are paused.", EventCompetitionManagementStatus.Conflict, cancellationToken);
            return;
        }
        if (read.Status is WiseOldManCompetitionStatus.RateLimited or WiseOldManCompetitionStatus.Unavailable)
        {
            await RescheduleUnknownAsync(operationId, "ReconciliationUnavailable", read.Message ?? "WOM update reconciliation is temporarily unavailable.", read.RetryAt, cancellationToken);
            return;
        }
        await FailOperationAsync(operationId, "ExternalDrift", "The managed WOM competition does not match the requested update; Admin review is required.", EventCompetitionManagementStatus.Conflict, cancellationToken);
    }

    private async Task RescheduleUnknownAsync(Guid operationId, string code, string message, DateTimeOffset? retryAt, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var operation = await db.EventCompetitionManagementOperations.SingleAsync(x => x.Id == operationId, cancellationToken);
        if (operation.Phase != EventCompetitionManagementOperationPhase.Unknown)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (operation.AttemptCount >= 3 && !await IsPendingEndOperationAsync(operation, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            await FailOperationAsync(operationId, "ReconciliationRequired", "The WOM operation outcome remains unresolved; Admin review is required.", EventCompetitionManagementStatus.Conflict, cancellationToken);
            return;
        }
        var next = await EndRetryAtAsync(operation, retryAt, cancellationToken, attemptOffset: 1);
        operation.ScheduleReconciliation(next, time.GetUtcNow());
        var management = operation.ManagementId is { } managementId
            ? await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken)
            : null;
        management?.MarkFailure(operationId, EventCompetitionManagementStatus.Unknown, code, message, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<EventCompetitionManagementResult> PersistProviderReceiptWithRetryAsync(
        Func<Task<EventCompetitionManagementResult>> persist,
        CancellationToken cancellationToken)
    {
        Exception? lastFailure = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await persist();
            }
            catch (Exception exception) when (IsPersistenceFailure(exception) && attempt < 2)
            {
                lastFailure = exception;
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken);
            }
        }

        throw lastFailure ?? new InvalidOperationException("The WOM provider receipt could not be persisted.");
    }

    private static bool IsPersistenceFailure(Exception exception)
        => exception is DbUpdateException or NpgsqlException or TimeoutException;

    private async Task<EventCompetitionManagementResult?> CheckManagedSourceAsync(
        Guid operationId,
        EventCompetitionManagement management,
        CancellationToken cancellationToken)
    {
        var read = await competitionClient.GetCompetitionAsync(management.CompetitionId, cancellationToken);
        if (read.Succeeded && RemoteConfigurationMatches(read.Competition!, management)) return null;
        if (read.Status is WiseOldManCompetitionStatus.RateLimited or WiseOldManCompetitionStatus.Unavailable)
            return await RetryReadAsync(operationId, read.Message ?? "Wise Old Man could not be checked before the managed update.", read.RetryAt, cancellationToken);
        var code = read.Status == WiseOldManCompetitionStatus.NotFound ? "SourceMissing" : "ExternalDrift";
        var message = read.Status == WiseOldManCompetitionStatus.NotFound
            ? "The managed WOM competition no longer exists; automatic updates are paused."
            : read.Succeeded
                ? "The managed WOM competition changed outside Bingo; automatic updates are paused for Admin review."
                : "The managed WOM competition could not be verified; automatic updates are paused for Admin review.";
        return await FailOperationAsync(operationId, code, message, EventCompetitionManagementStatus.Conflict, cancellationToken);
    }

    private async Task<EventCompetitionManagementResult> RetryReadAsync(
        Guid operationId,
        string message,
        DateTimeOffset? retryAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var operation = await db.EventCompetitionManagementOperations.SingleAsync(x => x.Id == operationId, cancellationToken);
        var management = operation.ManagementId is { } managementId
            ? await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken)
            : null;
        var dueAt = await EndRetryAtAsync(operation, retryAt, cancellationToken);
        operation.Retry(dueAt, "SourceCheckUnavailable", message, time.GetUtcNow());
        management?.MarkFailure(operationId, EventCompetitionManagementStatus.Pending, "SourceCheckUnavailable", message, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(false, message, operationId, "Retry", dueAt, "SourceCheckUnavailable");
    }

    private async Task<EventCompetitionManagementResult> SuspendRosterOperationAsync(
        Guid operationId,
        string message,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var operation = await db.EventCompetitionManagementOperations.SingleAsync(x => x.Id == operationId, cancellationToken);
        operation.Cancel(time.GetUtcNow());
        if (operation.ManagementId is { } managementId)
        {
            var management = await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken);
            management?.MarkFailure(operationId, EventCompetitionManagementStatus.Active, "DraftReopened", message, time.GetUtcNow());
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, message, operationId, "Suspended");
    }

    private static bool RemoteConfigurationMatches(WiseOldManCompetition actual, EventCompetitionManagement management)
    {
        if (!string.Equals(RemoteFingerprint(actual), management.LastAppliedRemoteFingerprint, StringComparison.Ordinal)) return false;
        if (string.IsNullOrWhiteSpace(management.LastAcknowledgedRosterJson)) return true;
        WiseOldManCompetitionWriteTeam[] expectedTeams;
        try
        {
            expectedTeams = JsonSerializer.Deserialize<WiseOldManCompetitionWriteTeam[]>(management.LastAcknowledgedRosterJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return false;
        }

        var expectedPlayers = expectedTeams
            .SelectMany(team => team.Participants)
            .Select(WiseOldManCompetitionRules.NormalizePlayerName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualPlayers = actual.Participants
            .Select(participant => WiseOldManCompetitionRules.NormalizePlayerName(participant.Username))
            .Order(StringComparer.Ordinal)
            .ToArray();
        return expectedPlayers.SequenceEqual(actualPlayers, StringComparer.Ordinal);
    }

    private async Task<(EventCompetitionManagementOperation Operation, BingoEvent Event, EventCompetitionManagement? Management)?> ClaimAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var operation = await db.EventCompetitionManagementOperations.FromSqlInterpolated($"SELECT * FROM event_competition_management_operations WHERE id = {operationId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (operation is not null) await db.Entry(operation).ReloadAsync(cancellationToken);
        if (operation is null || (operation.Phase != EventCompetitionManagementOperationPhase.Pending && operation.Phase != EventCompetitionManagementOperationPhase.Retry)
            || operation.NextAttemptAt > time.GetUtcNow()) return null;
        var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {operation.EventId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (item is null) return null;
        if (operation.Type == EventCompetitionManagementOperationType.Update && await EndUpdatesStoppedAsync(operation.EventId, cancellationToken))
        {
            operation.Cancel(time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var management = operation.ManagementId is { } managementId ? await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken) : null;
        operation.Claim(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (operation, item, management);
    }

    private async Task<EventCompetitionManagementResult> CompleteCreateAsync(Guid operationId, EventCompetitionManagementOperation operation, WiseOldManCompetitionWritePayload payload, WiseOldManCompetitionWriteResult result, CancellationToken cancellationToken)
    {
        var competition = result.Competition!;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {operation.EventId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var currentOperation = await db.EventCompetitionManagementOperations.FromSqlInterpolated($"SELECT * FROM event_competition_management_operations WHERE id = {operationId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (item is null || currentOperation is null) return new(false, "The event disappeared while saving the WOM receipt.", operationId, "Unknown");
        var fingerprint = await EventCompetitionSynchronizationService.StatsAssignmentFingerprintAsync(db, operation.EventId, cancellationToken);
        var state = await db.EventCompetitionSynchronizations.SingleOrDefaultAsync(x => x.EventId == operation.EventId, cancellationToken);
        if (state is null)
        {
            state = new EventCompetitionSynchronization(Guid.NewGuid(), operation.EventId, 1, competition.Id, competition.Title, competition.StartsAt, competition.EndsAt, fingerprint, time.GetUtcNow(), EventCompetitionProvenance.WebsiteCreated);
            db.EventCompetitionSynchronizations.Add(state);
        }
        else
        {
            state.Reconfigure(competition.Id, competition.Title, competition.StartsAt, competition.EndsAt, fingerprint, time.GetUtcNow(), EventCompetitionProvenance.WebsiteCreated);
        }
        var management = await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.EventId == operation.EventId, cancellationToken);
        var protectedCode = result.ProtectedVerificationCode!;
        if (management is null)
        {
            management = new EventCompetitionManagement(Guid.NewGuid(), operation.EventId, state.Id, competition.Id, competition.Title, competition.StartsAt, competition.EndsAt, protectedCode, operation.DesiredFingerprint, time.GetUtcNow(), EventCompetitionProvenance.WebsiteCreated, EventCompetitionCredentialStatus.Valid);
            db.EventCompetitionManagements.Add(management);
            management.MarkApplied(operationId, operation.DesiredFingerprint, RemoteFingerprint(competition), JsonSerializer.Serialize(payload.Teams, JsonOptions), competition.Title, competition.StartsAt, competition.EndsAt, time.GetUtcNow());
        }
        else
        {
            // A pre-live recreate may reuse the soft-deleted management row,
            // but the successful provider create is a new website-owned
            // connection. Rebind every identity/provenance field before
            // recording the receipt so no stale external/deleted lineage can
            // authorize or address the new remote competition.
            management.RebindWebsiteCreatedConnection(state.Id, competition.Id);
            management.MarkApplied(operationId, operation.DesiredFingerprint, RemoteFingerprint(competition), JsonSerializer.Serialize(payload.Teams, JsonOptions), competition.Title, competition.StartsAt, competition.EndsAt, time.GetUtcNow());
            management.ReplaceProtectedCredential(protectedCode, time.GetUtcNow());
        }
        management.MarkCredentialValid(time.GetUtcNow());
        management.ObserveActualStart(item.ActualStartedAt, time.GetUtcNow());
        currentOperation.Succeed(competition.Id, "protected-management-code", time.GetUtcNow());
        item.AdvanceVersion();
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), operation.ActorAccountId, operation.ActorUsername ?? "Admin", "event.competition_created", "event", operation.EventId.ToString(), $"Created managed Wise Old Man competition {competition.Id}.", operation.EventId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, "WOM competition created and linked.", operationId, "Succeeded");
    }

    private async Task<EventCompetitionManagementResult> CompleteUpdateAsync(Guid operationId, EventCompetitionManagementOperation operation, WiseOldManCompetitionWritePayload payload, WiseOldManCompetition competition, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {operation.EventId} FOR UPDATE").SingleAsync(cancellationToken);
        await db.Entry(item).ReloadAsync(cancellationToken);
        var state = await db.EventCompetitionSynchronizations.SingleOrDefaultAsync(x => x.EventId == operation.EventId, cancellationToken);
        if (state is not null) await db.Entry(state).ReloadAsync(cancellationToken);
        var currentOperation = await db.EventCompetitionManagementOperations.SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
        if (currentOperation is not null) await db.Entry(currentOperation).ReloadAsync(cancellationToken);
        var management = operation.ManagementId is { } managementId
            ? await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken)
            : null;
        if (currentOperation is null || state is null || management is null) return new(false, "The WOM update receipt could not be saved.", operationId, "Unknown", ErrorCode: "ReceiptSaveFailed");
        if (currentOperation.Phase is not (EventCompetitionManagementOperationPhase.Sending or EventCompetitionManagementOperationPhase.Unknown)
            || state.CompetitionId != competition.Id || management.CompetitionId != competition.Id
            || management.Status == EventCompetitionManagementStatus.Deleted
            || await EndUpdatesStoppedAsync(operation.EventId, cancellationToken))
            return new(false, "The WOM update receipt no longer belongs to the current event connection.", operationId, "Stopped", ErrorCode: "StaleReceipt");
        var currentPreview = await BuildProjectionAsync(operation.EventId, cancellationToken);
        var localFingerprint = operation.DesiredFingerprint;
        var assignmentFingerprint = payload.IncludeTeams
            ? await EventCompetitionSynchronizationService.StatsAssignmentFingerprintAsync(db, operation.EventId, cancellationToken)
            : state.AssignmentFingerprint;
        var titleChanged = state.CompetitionTitle != competition.Title;
        var sourceChanged = state.CompetitionId != management.CompetitionId
            || state.CompetitionStartsAt != competition.StartsAt
            || state.CompetitionEndsAt != competition.EndsAt
            || state.AssignmentFingerprint != assignmentFingerprint;
        if (state.EndUpdateStatus == EventCompetitionEndUpdateStatus.Pending && !payload.IncludeTeams)
        {
            state.UpdateMetadata(management.CompetitionId, competition.Title, competition.StartsAt, competition.EndsAt, time.GetUtcNow());
            if (competition.StartsAt == item.EventStartsAt && competition.EndsAt == item.EventEndsAt)
            {
                state.CompleteEndUpdate(competition.EndsAt);
                state.MakeNormalRefreshDue(time.GetUtcNow());
            }
        }
        else if (sourceChanged)
            state.Reconfigure(management.CompetitionId, competition.Title, competition.StartsAt, competition.EndsAt, assignmentFingerprint, time.GetUtcNow());
        else if (titleChanged)
            state.UpdateMetadata(management.CompetitionId, competition.Title, competition.StartsAt, competition.EndsAt, time.GetUtcNow());
        var rosterJson = payload.IncludeTeams
            ? JsonSerializer.Serialize(payload.Teams, JsonOptions)
            : management.LastAcknowledgedRosterJson ?? "[]";
        management.MarkApplied(operationId, localFingerprint, RemoteFingerprint(competition), rosterJson, competition.Title, competition.StartsAt, competition.EndsAt, time.GetUtcNow());
        management.MarkCredentialValid(time.GetUtcNow());
        management.ObserveActualStart(currentPreview?.Event.ActualStartedAt, time.GetUtcNow());
        currentOperation.Succeed(management.CompetitionId, "existing-management-code", time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, "WOM competition update completed.", operationId, "Succeeded");
    }

    private async Task<EventCompetitionManagementResult> CompleteDeleteAsync(Guid operationId, EventCompetitionManagementOperation operation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var state = await db.EventCompetitionSynchronizations.SingleOrDefaultAsync(x => x.EventId == operation.EventId, cancellationToken);
        var currentOperation = await db.EventCompetitionManagementOperations.SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
        var management = operation.ManagementId is { } managementId
            ? await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken)
            : null;
        if (management is null) return new(false, "The WOM delete receipt could not be saved.", operationId, "Unknown", ErrorCode: "ReceiptSaveFailed");
        if (state is not null) state.Reconfigure(null, null, null, null, state.AssignmentFingerprint, time.GetUtcNow());
        management.MarkDeleted(operationId, time.GetUtcNow());
        currentOperation?.Succeed(management.CompetitionId, "deleted", time.GetUtcNow());
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), operation.ActorAccountId, operation.ActorUsername ?? "Admin", "event.competition_deleted", "event", operation.EventId.ToString(), $"Deleted managed Wise Old Man competition {management.CompetitionId}.", operation.EventId));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, "WOM competition deleted.", operationId, "Succeeded");
    }

    private async Task<EventCompetitionManagementResult> HandleResultFailureAsync(Guid operationId, WiseOldManCompetitionWriteResult result, CancellationToken cancellationToken, string? sensitiveValue = null)
    {
        var safeCode = RedactProviderText(result.ErrorCode, sensitiveValue);
        var safeMessage = RedactProviderText(result.Message, sensitiveValue);
        var affected = result.AffectedParticipants?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();
        if (result.Status is WiseOldManCompetitionWriteStatus.RateLimited or WiseOldManCompetitionWriteStatus.Unavailable)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var operation = await db.EventCompetitionManagementOperations.SingleAsync(x => x.Id == operationId, cancellationToken);
            var management = operation.ManagementId is { } id ? await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) : null;
            var retryAt = await EndRetryAtAsync(operation, result.RetryAt, cancellationToken);
            var retryCode = safeCode ?? (result.Status == WiseOldManCompetitionWriteStatus.RateLimited ? "RateLimited" : "Unavailable");
            var retryMessage = AppendAffected(safeMessage ?? (result.Status == WiseOldManCompetitionWriteStatus.RateLimited ? "Wise Old Man is rate-limited." : "Wise Old Man is temporarily unavailable."), affected);
            operation.Retry(retryAt, retryCode, retryMessage, time.GetUtcNow());
            management?.MarkFailure(operationId, EventCompetitionManagementStatus.Pending, retryCode, retryMessage, time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(false, retryMessage, operationId, "Retry", retryAt, retryCode, affected);
        }
        var errorCode = safeCode ?? result.Status.ToString();
        var message = AppendAffected(safeMessage ?? "Wise Old Man rejected the operation.", affected);
        if (result.Status == WiseOldManCompetitionWriteStatus.Unauthorized)
        {
            return await FailOperationAsync(operationId, errorCode, message, EventCompetitionManagementStatus.Failed, cancellationToken, invalidateCredential: true);
        }
        if (result.Status == WiseOldManCompetitionWriteStatus.Unavailable)
        {
            await MarkCredentialStateAsync(operationId, EventCompetitionCredentialStatus.Unavailable, cancellationToken);
        }
        var failed = await FailOperationAsync(operationId, errorCode, message, EventCompetitionManagementStatus.Failed, cancellationToken);
        return failed with { AffectedParticipants = affected };
    }

    private async Task MarkCredentialStateAsync(Guid operationId, EventCompetitionCredentialStatus status, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var operation = await db.EventCompetitionManagementOperations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken);
        if (operation?.ManagementId is { } managementId)
        {
            var management = await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken);
            if (management is not null)
            {
                var now = time.GetUtcNow();
                if (status == EventCompetitionCredentialStatus.Invalid) management.MarkCredentialInvalid(now);
                else if (status == EventCompetitionCredentialStatus.Revoked) management.MarkCredentialRevoked(now);
                else management.MarkCredentialUnavailable(now);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static string AppendAffected(string message, IReadOnlyList<string>? affected)
        => affected is { Count: > 0 }
            ? $"{message} Affected accounts: {string.Join(", ", affected)}."
            : message;

    private static string? RedactProviderText(string? value, string? sensitiveValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var safe = string.IsNullOrWhiteSpace(sensitiveValue) ? value.Trim() : value.Replace(sensitiveValue, "[redacted]", StringComparison.Ordinal);
        return safe.Length <= 500 ? safe : safe[..500];
    }

    private static void ApplyCredentialValidation(
        EventCompetitionManagement management,
        WiseOldManCredentialValidationResult validation,
        DateTimeOffset now)
    {
        switch (validation.Status)
        {
            case WiseOldManCredentialValidationStatus.Valid:
                management.MarkCredentialValid(now);
                break;
            case WiseOldManCredentialValidationStatus.Invalid:
                management.MarkCredentialInvalid(now);
                break;
            case WiseOldManCredentialValidationStatus.Revoked:
                management.MarkCredentialRevoked(now);
                break;
            case WiseOldManCredentialValidationStatus.Unavailable:
                management.MarkCredentialUnavailable(now);
                break;
            case WiseOldManCredentialValidationStatus.Unsupported:
                // Keep the honest unverified state until a real update or
                // update-all operation proves or rejects the code.
                break;
        }
    }

    private async Task<EventCompetitionManagementResult> FailOperationAsync(Guid operationId, string code, string message, EventCompetitionManagementStatus status, CancellationToken cancellationToken, bool invalidateCredential = false)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var operation = await db.EventCompetitionManagementOperations.SingleAsync(x => x.Id == operationId, cancellationToken);
        var outdatedEnd = false;
        if (operation.Type == EventCompetitionManagementOperationType.Update)
        {
            await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {operation.EventId} FOR UPDATE").SingleAsync(cancellationToken);
            var state = await db.EventCompetitionSynchronizations.FromSqlInterpolated($"SELECT * FROM event_competition_synchronizations WHERE event_id = {operation.EventId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (state is not null) await db.Entry(state).ReloadAsync(cancellationToken);
            var attemptedEnd = DeserializePayload(operation.DesiredPayloadJson).EndsAt;
            outdatedEnd = state?.EndUpdateStatus == EventCompetitionEndUpdateStatus.Pending && state.EndUpdateTargetAt != attemptedEnd;
            if (!outdatedEnd) state?.RejectEndUpdate(attemptedEnd, code);
        }
        operation.Fail(code, message, time.GetUtcNow()); // The old outcome remains in operation history.
        if (operation.ManagementId is { } managementId)
        {
            var management = await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken);
            if (management is not null)
            {
                await db.Entry(management).ReloadAsync(cancellationToken);
                if (outdatedEnd) management.MarkPending(operationId, time.GetUtcNow());
                else
                {
                    if (invalidateCredential) management.MarkCredentialInvalid(time.GetUtcNow());
                    management.MarkFailure(operationId, status, code, message, time.GetUtcNow());
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(false, message, operationId, "Failed", ErrorCode: code);
    }

    private async Task<EventCompetitionManagementResult> MarkUnknownAsync(Guid operationId, string code, string message, DateTimeOffset? retryAt, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var operation = await db.EventCompetitionManagementOperations.SingleAsync(x => x.Id == operationId, cancellationToken);
        if (operation.Phase != EventCompetitionManagementOperationPhase.Sending)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true, OperationId: operationId, Status: operation.Phase.ToString());
        }
        var next = await EndRetryAtAsync(operation, retryAt, cancellationToken);
        operation.MarkUnknown(code, message, time.GetUtcNow(), next);
        if (operation.ManagementId is { } managementId)
        {
            var management = await db.EventCompetitionManagements.SingleOrDefaultAsync(x => x.Id == managementId, cancellationToken);
            management?.MarkFailure(operationId, EventCompetitionManagementStatus.Unknown, code, message, time.GetUtcNow());
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(false, message, operationId, "Unknown", next, code);
    }

    private Task<EventCompetitionManagementResult> MarkExpiredClaimUnknownAsync(Guid operationId, CancellationToken cancellationToken)
        => MarkUnknownAsync(operationId, "ClaimExpired", "The previous WOM management attempt expired before its outcome was recorded.", time.GetUtcNow(), cancellationToken);

    private async Task<Projection?> BuildProjectionAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.State != EventState.Discarded, cancellationToken);
        if (item is null) return null;
        var activePublication = await db.ActiveRosterPublicationAsync(eventId, cancellationToken);
        var publicationRows = activePublication is null
            ? []
            : await db.DraftPublicationRosters.AsNoTracking()
                .Where(x => x.DraftPublicationCycleId == activePublication.Id)
                .OrderBy(x => x.TeamId)
                .ThenBy(x => x.EffectivePickNumber)
                .ThenBy(x => x.PublicCharacterName)
                .ToListAsync(cancellationToken);
        var teams = await db.Teams.AsNoTracking().Where(x => x.EventId == eventId && x.Active).OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var teamViews = new List<EventCompetitionManagementTeamView>();
        var errors = new List<string>();
        if (activePublication is not null)
        {
            // Validate every active finalized team against the immutable publication,
            // including teams with no publication rows. Public roster projection uses
            // the publication rows only, so an empty team cannot disappear from WOM
            // validation or make an incomplete publication look valid.
            teams = teams.Where(x => x.FinalizedAt is not null || publicationRows.Any(row => row.TeamId == x.Id)).ToList();
            var publishedParticipantIds = publicationRows.Select(row => row.EventParticipantId).Distinct().ToArray();
            var currentAssignments = publishedParticipantIds.Length == 0
                ? new List<AssignmentRow>()
                : await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                         join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                         where assignment.EventId == eventId
                             && publishedParticipantIds.Contains(assignment.EventParticipantId)
                             && assignment.ReleasedAt == null
                             && assignment.EventRole == EventCharacterRole.Playing
                         orderby assignment.EventParticipantId, assignment.RegistrationOrder, assignment.Id
                         select new AssignmentRow(assignment.EventParticipantId, assignment.Id, assignment.OsrsCharacterId, character.DisplayName, character.NormalizedName))
                    .ToListAsync(cancellationToken);
            var currentAssignmentsByParticipant = currentAssignments
                .GroupBy(row => row.ParticipantId)
                .ToDictionary(group => group.Key, group => group.ToArray());
            foreach (var team in teams)
            {
                var names = new List<string>();
                foreach (var publicationRow in publicationRows.Where(x => x.TeamId == team.Id))
                {
                    if (currentAssignmentsByParticipant.TryGetValue(publicationRow.EventParticipantId, out var assignments) && assignments.Length > 0)
                    {
                        // Publication rows remain the membership/provenance boundary;
                        // the payload names are the current active Playing assignments,
                        // including an optional second Playing account.
                        names.AddRange(assignments.Select(assignment => assignment.DisplayName));
                    }
                    else
                    {
                        names.Add(publicationRow.PublicCharacterName);
                        errors.Add($"Published participant {publicationRow.EventParticipantId} has no current eligible Playing assignment for team {team.Name}.");
                    }
                }
                teamViews.Add(new(team.Id, team.Name, names, names.Count == 0));
                if (names.Count == 0) errors.Add($"Team {team.Name} has no published roster members.");
            }
        }
        else
        {
            var teamIds = teams.Select(x => x.Id).ToArray();
            var memberships = await db.TeamMemberships.AsNoTracking().Where(x => teamIds.Contains(x.TeamId) && x.LeftAt == null).OrderBy(x => x.JoinedAt).ToListAsync(cancellationToken);
            var participantIds = memberships.Select(x => x.EventParticipantId).Distinct().ToArray();
            var participants = await db.EventParticipants.AsNoTracking().Where(x => participantIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            var assignments = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                                     join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                     where assignment.EventId == eventId && assignment.ReleasedAt == null && assignment.EventRole == EventCharacterRole.Playing
                                     select new AssignmentRow(assignment.EventParticipantId, assignment.Id, assignment.OsrsCharacterId, character.DisplayName, character.NormalizedName)).ToListAsync(cancellationToken);
            var byParticipant = assignments.GroupBy(x => x.ParticipantId).ToDictionary(x => x.Key, x => x.ToArray());
            foreach (var team in teams)
            {
                var names = new List<string>();
                foreach (var membership in memberships.Where(x => x.TeamId == team.Id))
                {
                    if (!participants.TryGetValue(membership.EventParticipantId, out var participant) || participant.SignupStatus != SignupStatus.Confirmed) continue;
                    if (!byParticipant.TryGetValue(membership.EventParticipantId, out var rows) || rows.Length == 0)
                    {
                        errors.Add($"Participant {membership.EventParticipantId} has no eligible Playing assignment for team {team.Name}.");
                        continue;
                    }
                    names.AddRange(rows.Select(x => x.DisplayName));
                }
                teamViews.Add(new(team.Id, team.Name, names, names.Count == 0));
            }
        }
        var sync = await db.EventCompetitionSynchronizations.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
        var management = await db.EventCompetitionManagements.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
        var draftFinalized = activePublication is not null;
        var startsAt = item.EventStartsAt ?? DateTimeOffset.MinValue;
        var endsAt = item.EventEndsAt ?? DateTimeOffset.MinValue;
        var payloadTeams = teamViews.Select(x => new WiseOldManCompetitionWriteTeam(x.Name, x.Participants)).ToArray();
        if (item.IsHidden) errors.Add("The event must be visible before WOM management.");
        if (item.ActualStartedAt is not null) errors.Add("A WOM competition can only be created before the event starts.");
        errors.AddRange(WiseOldManCompetitionRules.Validate(item.Name, startsAt, endsAt, payloadTeams, time.GetUtcNow()));
        if (item.EventStartsAt is null || item.EventEndsAt is null) errors.Add("The event must have a configured start and end before WOM management.");
        if (teamViews.Count == 0) errors.Add("The event must have at least one active team before WOM management.");
        var fingerprint = WiseOldManCompetitionRules.Fingerprint(new
        {
            item.Id,
            item.Name,
            StartsAt = item.EventStartsAt,
            EndsAt = item.EventEndsAt,
            DraftFinalized = draftFinalized,
            DraftPublicationMethod = activePublication?.PublicationMethod.ToString() ?? DraftPublicationMethod.HistoricalUnknown.ToString(),
            Teams = teamViews.Select(team => new
            {
                team.TeamId,
                Name = WiseOldManCompetitionRules.NormalizeTeamName(team.Name),
                Participants = team.Participants.Select(WiseOldManCompetitionRules.NormalizePlayerName).Order(StringComparer.Ordinal).ToArray()
            }).ToArray()
        });
        var preview = new EventCompetitionManagementPreview(eventId, item.Name, startsAt, endsAt, teamViews, errors.Distinct(StringComparer.Ordinal).ToArray(), fingerprint, item.State == EventState.SignupClosed && draftFinalized, sync?.CompetitionId is not null && (management is null || management.Status == EventCompetitionManagementStatus.Deleted), management is not null && management.Status != EventCompetitionManagementStatus.Deleted);
        return new Projection(item, preview, sync, management);
    }

    private static WiseOldManCompetitionWritePayload ToPayload(EventCompetitionManagementPreview preview, bool includeTeams)
        => new(preview.Title, preview.StartsAt, preview.EndsAt, includeTeams ? preview.Teams.Select(team => new WiseOldManCompetitionWriteTeam(WiseOldManCompetitionRules.NormalizeTeamName(team.Name), team.Participants.ToArray())).ToArray() : [], includeTeams);

    private static WiseOldManCompetitionWritePayload DeserializePayload(string json)
        => JsonSerializer.Deserialize<WiseOldManCompetitionWritePayload>(json, JsonOptions) ?? throw new InvalidOperationException("The stored WOM operation payload is invalid.");

    private static DeletePayload DeserializeDeletePayload(string json)
        => JsonSerializer.Deserialize<DeletePayload>(json, JsonOptions) ?? throw new InvalidOperationException("The stored WOM delete payload is invalid.");

    private static string RemoteFingerprint(WiseOldManCompetition competition)
        => WiseOldManCompetitionRules.Fingerprint(new { competition.Id, competition.Title, competition.StartsAt, competition.EndsAt });

    // The write adapter's receipt carries title/window, not a participant snapshot.
    private static bool MatchesWriteReceipt(WiseOldManCompetition actual, WiseOldManCompetitionWritePayload expected)
        => string.Equals(actual.Title.Trim(), expected.Title.Trim(), StringComparison.OrdinalIgnoreCase)
            && actual.StartsAt.ToUniversalTime() == expected.StartsAt.ToUniversalTime()
            && actual.EndsAt.ToUniversalTime() == expected.EndsAt.ToUniversalTime();

    private static bool Matches(WiseOldManCompetition actual, WiseOldManCompetitionWritePayload expected)
    {
        if (!MatchesWriteReceipt(actual, expected)) return false;
        if (!expected.IncludeTeams) return true;
        var actualNames = actual.Participants.Select(x => WiseOldManCompetitionRules.NormalizePlayerName(x.Username)).Order(StringComparer.Ordinal).ToArray();
        var expectedNames = expected.Teams.SelectMany(x => x.Participants).Select(WiseOldManCompetitionRules.NormalizePlayerName).Order(StringComparer.Ordinal).ToArray();
        return actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal);
    }

    private async Task RequireAdminAsync(LifecycleActor actor, CancellationToken cancellationToken)
    {
        var allowed = await db.Accounts.AsNoTracking().AnyAsync(x => x.Id == actor.Id && x.Active
            && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), cancellationToken);
        if (!allowed) throw new UnauthorizedAccessException("An enabled Admin is required for WOM management.");
    }

    private sealed record Projection(BingoEvent Event, EventCompetitionManagementPreview Preview, EventCompetitionSynchronization? Synchronization, EventCompetitionManagement? Management);
    private sealed record DispatchFailure(string Code, string Message, EventCompetitionManagementStatus Status);
    private sealed record AssignmentRow(Guid ParticipantId, Guid AssignmentId, Guid CharacterId, string DisplayName, string NormalizedName);
    private sealed record DeletePayload(long CompetitionId);
}
