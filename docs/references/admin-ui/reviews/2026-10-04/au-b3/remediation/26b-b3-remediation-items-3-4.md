# 26b — B3 (AU20) remediation recheck: items 3 and 4

Reviewer: independent read-only agent (Claude), 4 October 2026.
Scope: `codex/participants-functionality`, commits `7be4ba2` (item 3) and `a980609` (item 4); base `4b9f156`, tip `45a09ad`.
Method: `git show` / `git diff` / `git grep` and file reads only. Nothing was built or run. Test results quoted from the worker's evidence files are worker-reported.
Requirements: brief `25-codex-brief-b3-remediation.md` items 3 and 4; original findings `24-b3-au20-review.md` F3/F4, `24e` M1, `24b` M1.

Neither file changed again after its item commit (`git diff 7be4ba2 45a09ad` shows no further change to `EventCompetitionSynchronizationService.cs`, and `git diff a980609 45a09ad` shows none to `EventLifecycleService.cs`). Line numbers below are therefore valid at both the item commit and the tip.

---

## Item 3 — a Conflict no longer blocks replacing an external link

### What changed

- `EventCompetitionSynchronizationService.cs:168-170` (`HasProtectedConnectionAsync`). Before: a connection counted as "protected" (replacement and disconnection refused) if it was not deleted **and** either was not external, or was external with status `Unknown` or `Conflict`. Now it counts as protected only if it is not deleted and not external. External links are no longer protected because of their status.
- `:172-176` (`HasUnresolvedManagementOperationAsync`, unchanged). This still refuses replacement and disconnection while any management operation for the event is in phase Pending, Claimed, Sending, Retry or Unknown. It runs both before WOM validation (`:84`) and again inside the Serializable transaction after the event row is locked (`:113`).
- `:83` and `:111`: the refusal text is now "This connection is not an external link. Use its existing management controls." Only website-created (and other non-external) connections can reach it now, so external links no longer see the misleading "managed competition controls" text.

### Checks against the requirement

| Requirement | Result | How established |
|---|---|---|
| Block only while an operation is queued, sending, retrying or unknown | Met | Traced: `:172-176` covers Pending, Claimed, Sending, Retry and Unknown. |
| A Conflict no longer blocks replacement | Met | Traced: `:168-170` no longer looks at `Conflict`. The worker's test `Au20RemediationDeletedExternalCompetitionConflictCanBeReplacedOrDisconnected` covers this (worker-reported pass). |
| Replacing resolves the Conflict | Met | Traced: `:138-139` calls `RetireExternalConnection` (`EventCompetitionManagement.cs:234-249`). That sets status Deleted, clears the protected code, and clears `LastOperationId` and the applied fingerprints. `Reconfigure` (`EventCompetitionSynchronization.cs:129-136`) resets the end-update status to NotRequired and clears its target, request time and error code. |
| Disconnection keeps its rule (only before first Live) | Met | Unchanged at `:119-120`: a live event's link cannot be cleared. The test's `live=false` case disconnects successfully; the existing `Au20ExternalDeleteAndLiveDisconnectAreRefused` still covers the Live refusal. |
| Website-created competitions keep their previous rules | Met | Traced: for a non-external connection the old and new conditions give the same result (protected unless Deleted). A Conflict on a website-created competition still refuses linking an external competition over it, as before. |
| Old code retired on replacement | Met | Same path as before (`RetireExternalConnection`). The test asserts that `ProtectedVerificationCode` is empty. |
| Exact window still required | Met | Unchanged at `:121-122` (`ScheduleMatches`). |

### Specific questions

**Removing `Unknown` from the protection.** The old rule also protected an external link whose management status was `Unknown`. That status is now covered only by the operation guard. I checked every place that writes management status `Unknown`: `MarkUnknownAsync` (`EventCompetitionManagementService.cs:1277-1295`) moves the operation to phase Unknown in the same transaction, and `RescheduleUnknownAsync` (`:877-897`) only acts when the operation is already Unknown. When an Unknown operation is resolved, the same transaction always rewrites the management status: to Active on success, to the failure status on failure, or to Pending on retry. So management status `Unknown` never exists without an Unknown-phase operation, and the guard still blocks. (Traced.)

**Can a Conflict from an in-flight end update leave something that later acts on the new link?** No. (Traced.)
- A Conflict from an operation is always written by `FailOperationAsync` (`:1243-1275`). That moves the operation itself to Failed, which is terminal, and marks a matching pending end update Rejected (`:1250-1256`).
- `QueueUpdateAsync` creates at most one Pending or Retry update operation per event: it reuses an existing one (`:338-376`). If an unresolved operation did coexist with a Conflict (for example the `SourceMismatch` Conflict at `:291-296`, which leaves any existing operation as it is), the guard refuses the replacement until that operation finishes.
- Replacement runs in a Serializable transaction that re-checks the operation guard after locking the event row. A new operation cannot slip in between the check and the commit without one of the two transactions failing.
- After replacement, any old operation receipt is refused: `CompleteUpdateAsync` returns `StaleReceipt` when the management record is Deleted or the competition ID differs (`:1089-1093`). The new link has no management record until an Admin adopts a new code (`RebindExternalConnection`, `:162-167`). Until then, `QueueUpdateAsync` treats the link as read-only and rejects a pending end update as `MissingCredential` (`:268-274`). Because the replacement needs an exact window, there is no end update to perform anyway.

### Findings

- **I3-1 — Low (proof gap). The proof Conflict comes from a roster/title update, not from an end update.**
  - **What the code/test does:** `Au20RemediationManagementIntegrationTests.cs` (the new theory, at `7be4ba2` lines 127-148) triggers the Conflict through `QueueUpdateAsync` with a changed fingerprint and a `NotFound` read. That gives `SourceMissing`, operation Failed and management Conflict. It then replaces the link (Live) or disconnects it (pre-Live).
  - **What was required:** "a competition deleted on WOM during Live gives a Conflict; linking the replacement (exact window) then succeeds; the old code is retired." This is met literally.
  - **Gap:** the more realistic case (an early end queues an end update, WOM returns `SourceMissing`, and the end update is marked Rejected) is not tested together with replacement. The test also does not assert management status Deleted or the end-update status after replacement.
  - **Why only Low:** by trace, the rejected end update is reset by `Reconfigure`. The existing test `Au20ExternalOptionOneRetiresOldCodeAndOperation` already proves that reset for Pending and Rejected end updates.
  - **Basis:** traced.
- **I3-2 — Note. The new message is less specific for website-created competitions.** Website-created competitions now also see "This connection is not an external link. Use its existing management controls." It is accurate, but less informative than the old text, which named title, schedule, roster and deletion. No action is needed unless the UI phase wants a clearer text. Traced.
- **I3-3 — Note. A long WOM outage keeps replacement blocked.** While WOM is unavailable, an end update stays in Retry (item 1 schedule: every 30 minutes until publication). During that time the replacement stays blocked with "Wait for the current WOM operation…". This is the approved option 1 behaviour, recorded here so it is a known consequence. Traced.
- **I3-4 — Note (outside item 3). Adopting a new code does not clear a Conflict.** If an Admin adopts a new code on an external link that is in Conflict and is not replaced, `AdoptProtectedCredential` (`EventCompetitionManagement.cs:148-163`) does not reset the status. `QueueUpdateAsync` (`:287-290`) then keeps refusing. Recovery now exists through replacement (relinking, possibly to the same competition ID), so this is not a blocker. Traced.

---

## Item 4 — early end and Resume retry database conflicts internally

### What changed (`EventLifecycleService.cs`)

- `:117-122` and `:163-168`: `EndNowAsync` and `ResumePrematureEndAsync` read the clock once (`clickedAt`), then run the old method body (now `EndNowAttemptAsync` / `ResumePrematureEndAttemptAsync`) through `RetryLifecycleWriteAsync`.
- `:225-234` `RetryLifecycleWriteAsync`: at most 3 attempts. An attempt that throws a conflict exception gets `db.ChangeTracker.Clear()` and is run again. After 3 conflicts it returns the normal message "This event changed while it was being ended/resumed. Review its current state and try again." It does not throw.
- `:236-241` `IsLifecycleWriteConflict`: walks the `InnerException` chain for a `PostgresException` with SqlState 40001 or 40P01. This also catches the error when EF wraps it in `DbUpdateException` during `SaveChanges`.
- `:157` and `:219`: inside each attempt, the first catch clause is the conflict filter. It rolls back and rethrows. It comes before the `DbUpdateConcurrencyException`, `InvalidOperationException` and `DbUpdateException` clauses, so a wrapped conflict is not turned into "could not be ended".
- `:140` / `:187`: the attempt uses `now = clickedAt`. In EndNow, the actual end, the rounded configured end (`BingoEvent.EndEarly`, domain `:416-422`, a pure function of the click time), the reason-required check, the submission close, the audit time and the end-update request time are therefore all identical on every attempt.
- `:408-414` `EventAsync`: now `SELECT … FOR UPDATE` on the event row, plus `ReloadAsync`, followed by the version check. Only these two methods call it (`:139`, `:186`).
- `:452-462` `RecordPendingEndUpdateAsync`: `ReloadAsync` added after the sync-row `FOR UPDATE`.

### Checks against the requirement

| Requirement | Result | How established |
|---|---|---|
| Catch 40001/40P01 during the FOR UPDATE queries | Met | Traced. Worker test forces a real 40001 at the sync-row `FOR UPDATE` (worker-reported pass). |
| Catch 40001/40P01 at SaveChanges (wrapped) | Met by trace | The inner-exception walk handles the `DbUpdateException` wrapper. Not tested. |
| Catch 40001 at CommitAsync | **Probably not met** | See I4-1. |
| Retry with fresh DbContext state | Met | `ChangeTracker.Clear()` between attempts. Each attempt opens a new Serializable transaction (new snapshot) and reloads the event and sync rows. |
| Original click time kept | Met | Traced, and asserted by the worker's test. The clock moves 2 minutes during the collision; `ActualEndedAt`, the transition's `EffectiveAt` and `EndUpdateRequestedAt` all equal the click time (truncated to PostgreSQL microseconds), and `EventEndsAt` equals 12:01. |
| Normal "try again" message after retries | Met | `:233`. The test's exhaust case asserts "try again", 3 collisions, and unchanged state and version. |
| Lock order the same as the refresh worker | Met | See below. |
| No double side effects | Met | See below. |
| A legitimate version conflict is still reported | Met | See below. |

**Lock order (traced).**
- Early end and Resume take locks in this order: `accounts` row `FOR UPDATE` (`EventMutationAuthorization.cs:35`), then advisory lock 7303004 (`EventCurrentBoundary.cs:20-21`), then the `events` row, then the `event_competition_synchronizations` row.
- The refresh worker (`EventCompetitionSynchronizationService.cs:264-268` AcquireLease, `:330-332` FinalizeLease) and the end-update failure path (`EventCompetitionManagementService.cs:1250-1251`) both lock the event row, then the sync row.
- The worker never takes the account lock or the advisory lock, so no lock cycle is possible. Before this change, the lifecycle read the event row without `FOR UPDATE`, so this change aligns the order.

**Side effects (traced).**
- The attempt writes only database rows: the event, the sync row, one `EventStateTransition` and one `AuditEntry` (`:426-430`).
- It sends no notifications and makes no WOM call.
- A failed attempt's rows are rolled back, and their tracked copies are discarded by `ChangeTracker.Clear()`, so a retry cannot save them twice. The test asserts a single transition after EndNow recovers.
- `RequestEndUpdate` only sets the target and request time, so running it again has no extra effect.

**Version conflicts (traced).** On each attempt, `EventAsync` reloads the locked event row and compares it with the `version` the Admin's page submitted. Suppose the competing transaction was another Admin's change, or the scheduled end: it advanced the version. The retry then throws `DbUpdateConcurrencyException` and returns "This event changed while it was being ended…", so it does not override that change. A refresh-worker collision changes only the sync row, so the retry succeeds, which is the intended case.

**"Commit succeeded but the connection was lost" (traced).** That surfaces as an `NpgsqlException` or IO error, not 40001/40P01. It is not retried internally, so the event cannot be ended twice. If the Admin resubmits, the version check refuses with "This event changed…". The error page in that case comes from the existing behaviour and is outside this brief's 40001/40P01 scope.

### Findings

- **I4-1 — Medium. A serialization failure at COMMIT probably escapes as an error page.**
  - **What the code does:** `EventLifecycleService.cs:157` and `:219` handle a conflict like this: `catch (Exception ex) when (IsLifecycleWriteConflict(ex)) { await tx.RollbackAsync(ct); throw; }`.
  - **Why that fails at COMMIT:** under Serializable, PostgreSQL can report 40001 at `COMMIT` itself. Its SSI check is allowed to run at commit time. When `COMMIT` fails, the server has already ended the transaction and the connection returns to idle. Npgsql then treats the transaction as completed, and `Rollback` throws `InvalidOperationException("This NpgsqlTransaction has completed; it is no longer usable.")`. That string is present in the Npgsql 10.0.3 assembly the project uses. Because the exception is raised inside the catch block, it replaces the 40001. `RetryLifecycleWriteAsync`'s filter does not match it, since it has no `PostgresException` inner exception. The Admin therefore gets an unhandled exception and an error page, which is exactly what item 4 was meant to remove.
  - **Required:** catch 40001/40P01 wherever it can surface, including `CommitAsync`, and never show an error page.
  - **Failure scenario:** an Admin clicks Resume (which runs the predicate queries `EventFinalizations.AnyAsync`, `OtherCurrentEvents` and the overlap query). At the same time a Serializable transaction elsewhere writes a row those predicates cover, forming an rw-dependency pivot. PostgreSQL detects it at `COMMIT` and returns 40001, `RollbackAsync` throws, and the page errors. The same pattern already existed in `StartNowAsync:111`, so Start has the same exposure.
  - **Fix:** make the rollback in the conflict clause tolerant: skip it or swallow its failure when the transaction is already completed, or simply rethrow and let `await using` dispose the transaction. Also add a test that forces 40001 at commit, for example with two real Serializable transactions that have a read/write pivot, or with a transaction interceptor.
  - **Basis:** inferred from Npgsql's documented and observed behaviour (`IsCompleted`: "the transaction has been committed/rolled back"), plus the presence of the error string. Not run. The worker's tests force the conflict only at `SELECT … FOR UPDATE`, never at commit. If a commit-time test shows that `RollbackAsync` does not throw, this finding closes.
- **I4-2 — Low (proof gap). Only one of the conflict locations is tested.** The proof covers 40001 at the sync-row `FOR UPDATE` only. Not tested: 40001 wrapped in `DbUpdateException` at `SaveChanges` (correct by trace), 40001 at `CommitAsync` (see I4-1), and 40P01 deadlock (handled by the same filter, by trace). Traced.
- **I4-3 — Note. Retries run immediately.** There is no delay between attempts. This is acceptable: each new attempt takes a new snapshot after the competing transaction has committed, and the locks it takes wait for any holder. Traced.
- **I4-4 — Note (existing behaviour). Some error paths do not clear the change tracker.** The non-conflict catch clauses (`:158-160`, `:220-222`) roll back but do not clear the change tracker, unlike `StartNowAsync:111-114`. The DbContext belongs to one web request and the page only renders the message, so there is no visible effect. Traced.

---

## Verdict

| Item | Verdict | Open findings |
|---|---|---|
| 3: a Conflict no longer blocks replacing an external link | **PASS** | I3-1 Low (proof gap only), I3-2 to I3-4 Notes |
| 4: early end and Resume retry database conflicts internally | **FAIL (narrow)** | I4-1 Medium: a 40001 at COMMIT probably becomes an error page, because `RollbackAsync` on a completed transaction throws. Everything else is met: retry, original click time, fresh state, lock order, no double side effects, version conflicts still reported. A tolerant rollback plus a commit-time conflict test would turn this into PASS. I4-2 Low. |
