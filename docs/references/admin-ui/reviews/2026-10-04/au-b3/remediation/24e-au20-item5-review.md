# B3 (AU20) item 5 review — external link option 1 and WA-9 (commit `6d29f2c`)

Reviewer: Claude sub-agent, read-only. No builds or test runs. All line numbers are at `6d29f2c` unless stated.
Short names: **Sync** = `src/Bingo.Infrastructure/Events/EventCompetitionSynchronizationService.cs`, **Mgmt** = `src/Bingo.Infrastructure/Events/EventCompetitionManagementService.cs`, **Entity** = `src/Bingo.Domain/Integrations/WiseOldMan/EventCompetitionManagement.cs`, **SyncEntity** = `src/Bingo.Domain/Integrations/WiseOldMan/EventCompetitionSynchronization.cs`, **Tests** = `tests/Bingo.IntegrationTests/Au20ExternalReplacementIntegrationTests.cs`.

Sources checked: ticket AU20 point 6 (`b126c55:DELIVERY_PLAN.md:453-506`), option 1 text (`FUNCTIONALITY_CHANGES.md:111-115`, comparison `:136-170`), reference `Wom.dc.html:608-609, 881-886`, `06b` WA-5/WA-9, brief `23` item 5, evidence `au-b3/item5-external-replacement.md`.

## What the commit does (summary)

1. **Link/replace/disconnect (`Sync:71-160`).** The old "any non-deleted management record blocks" rule is replaced by two checks, each done once before the provider validation and again under the event row lock:
   - `HasProtectedConnectionAsync` (`Sync:168-171`) blocks when a non-deleted management record is website-created/Unknown provenance, **or** is external with status Unknown **or Conflict**.
   - `HasUnresolvedManagementOperationAsync` (`Sync:173-177`) blocks when any management operation for the event (any type, including website Create) is Pending, Claimed, Sending, Retry or Unknown.
   - Disconnect is now refused once `ActualStartedAt` is set (`Sync:119`), not only when the state is Live. Final Review and later states stay refused (`Sync:116-117`). The exact configured-window check still runs (`Sync:121-122`).
   - In the same transaction as the new link, the old external management record is retired (`Sync:138-139`).
2. **Retirement (`Entity:234-249`).** `RetireExternalConnection` marks the record Deleted, empties the protected code, sets ReadOnly / NotApplicable, clears the applied receipt and `LastOperationId`. Operation history is kept.
3. **Later code adoption (`Mgmt:151-169`, `Entity:251-273`).** If the record is Deleted, `RebindExternalConnection` reuses the same row with the new competition ID **and** the new code, Active status, cleared receipt.
4. **End-update state reset (`SyncEntity:134-136`).** `Reconfigure` now always resets the end-update status/target/error to NotRequired.
5. **Readback (`Mgmt:36-48`).** A Deleted record is shown as no record; the operation shown is the record's `LastOperationId`, or (with no live record) only an unfinished Create while nothing is linked. No provider call.

## Findings

### M1 — Medium — An external connection whose last operation ended in Conflict can never be replaced or disconnected, and there is no recovery action (traced)

- **Code today:** `HasProtectedConnectionAsync` (`Sync:168-171`) blocks link changes whenever an external management record has `Status == Conflict` (or Unknown). Conflict is a *terminal* record status: it is written by `FailOperationAsync` when an operation is already **Failed**, for example `SourceMissing` (competition deleted on WOM, `Mgmt:852`), `ExternalDrift` (`Mgmt:860`), `ReconciliationRequired` (`Mgmt:875`), `SharedSource`, `ManagementChanged`, and `SourceMismatch` (`Mgmt:293`, `Mgmt:535`). Nothing resets it: code re-adoption calls `AdoptProtectedCredential`/`ApplyCredentialValidation` (`Mgmt:173`, `Mgmt:1205-1225`), which change only credential fields, not `Status`; there is no resolve/acknowledge method in the service (public methods are Preview/Get/Adopt/Replace/Create/QueueUpdate/Delete/ProcessDue). `QueueUpdateAsync` also refuses Conflict (`Mgmt:287-290`).
- **Requirement:** option 1 allows replacement before/during Live and disconnection before first Live "regardless of stored/rejected code", keeping only the **active/unresolved-operation** guards (`DELIVERY_PLAN.md` AU20 point 6; `FUNCTIONALITY_CHANGES.md:111-115`: "Preserve active/unresolved-operation guards"). The reference blocks the Replace control only for a queued or unknown operation (`Wom.dc.html:609` `opBusy` = `queued || unknown`; `:882`), not for conflict.
- **Failure scenario:** event Live, linked to external competition 9801 with a stored code. The competition owner deletes 9801 on WOM. The next end/schedule update reconciles as `SourceMissing` → operation Failed, record Conflict. The Admin now tries to replace the link with 9802 (exact window). `ConfigureAsync` returns "This event has a managed WOM competition. Use managed competition controls for its title, schedule, roster, and deletion." There are no such controls for an external link, so the event is stuck on a deleted competition for the rest of Live; at publication it takes the "WOM end could not be updated"/fallback path or has no data. The same happens before Live (Disconnect also refused). This is exactly the case where replacement is most needed.
- **Also:** the message is wrong for this case (it names "managed" controls).
- **Suggested fix direction (for the planner):** for an external record, block only on an unresolved operation (already covered by `HasUnresolvedManagementOperationAsync`); treat record status Unknown as unresolved only while an Unknown operation exists. Add a test: external record in Conflict after a Failed operation → replacement and pre-Live disconnect succeed and retire the code.
- **Not tested:** no test puts an external record into Conflict.

### L1 — Low — The end-update reset is untested; fixture never has a pending end update (traced)

- **Code today:** `Reconfigure` resets end-update state (`SyncEntity:134-136`). The test `Au20ExternalOptionOneRetiresOldCodeAndOperation` asserts `EndUpdateStatus == NotRequired` after replacement (Tests, second `using` block), but the fixture `Au20ExternalAsync` never calls `RequestEndUpdate`, so the status was already NotRequired. The assertion passes with or without the new lines.
- **Correctness (traced):** the reset itself is right for replacement. A replacement must match the configured window exactly (`Sync:121-122`), so the new competition's end already equals the configured end; NotRequired is the true state. A queued/retrying end-update operation for the old competition blocks the replacement (`Sync:173-177`), so there is no in-flight end update to orphan. A worker that read "Pending" before the replacement and then calls `RejectPendingEndAsync` re-reads under the event lock and `RejectEndUpdate` is a no-op unless status is still Pending with the same target (`EndUpdate.cs:24-33`, `SyncEntity:56-61`). With no new code, `QueueUpdateAsync` finds the Deleted record and only rejects a pending end (`Mgmt:268-274`) — the old code is never used.
- **Side effect (inferred benign):** `Reconfigure` is also called by `CompleteCreateAsync` (`Mgmt:1032`), `CompleteUpdateAsync` when the source changed and no end update is Pending (`Mgmt:1100-1101`), and `CompleteDeleteAsync` (`Mgmt:1125`). These now also reset a Rejected status. A successful update receipt carries the configured window (mismatched receipts become Unknown per item 6), so resetting after it is consistent. Not covered by a test.
- **Fix:** seed a Pending (and a Rejected) end update in the fixture before replacement.

### L2 — Low — An Unknown website Create now blocks both Create and external linking permanently (traced; pre-existing reconciliation gap, now more consequential)

- **Code today:** WA-9 is implemented by treating any Unknown operation as blocking (`Sync:173-177`). An Unknown **Create** is never reconciled: `ReconcileUnknownAsync` only clears its retry time (`Mgmt:803-811`), and no path moves it to Failed/Cancelled. `CreateAsync` also refuses while it exists (`Mgmt:240-246`).
- **Requirement:** WA-9 says linking must *wait for* Sending/Unknown Create. That is honoured. But `06b` WA-9 already noted Unknown Create is never reconciled; with this change the event can neither create nor link for its whole life unless someone edits the database.
- **Scenario:** Create times out (Unknown) at 18:00 before Live; the competition was in fact never created on WOM. The Admin cannot link an external competition or retry Create; the event goes Live with no WOM data.
- **Status:** consistent with the decision text; not recorded as a known dead end in item 5/6 evidence. Planner should record it or give Unknown Create a reconciliation/Admin-resolve path (outside item 5 scope).

### L3 — Low — External-delete refusal is still tested only at the request level (traced)

- `Au20ExternalDeleteAndLiveDisconnectAreRefused` calls `DeleteAsync` and checks `DeleteCalls == 0`. `DeleteAsync` refuses because `CanDelete` needs WebsiteCreated (`Entity:102`). The dispatch-level guard (`ExternalDeleteDenied`, `Mgmt:509-510`) is never executed by inserting a Delete operation for an external record. `06b` coverage notes asked for this. The guard code itself is intact and there is no new delete path (see confirmed list).

### Notes (no action required unless the planner wants it)

- **N1 — Create completion has no own guard.** `CompleteCreateAsync` overwrites the link unconditionally (`Mgmt:1024-1033`, no phase or "already linked" check). Safety rests entirely on the link side refusing while the Create is Pending/Claimed/Sending/Retry/Unknown, and on no path moving an outstanding Create to a terminal phase (expired claims go to Unknown, still blocking). Traced as safe today; a defensive "op still Sending/Unknown and sync unlinked or same" check would make it robust.
- **N2 — Audit does not mention retirement.** The replacement/disconnect audit row (`Sync:141-145`) says "Linked/Cleared…" but not that a stored code was retired. No secret leaks; just less traceable.
- **N3 — Test expectation changed in an existing test.** `ManualLinkWaitsForManagedProviderWriteOnTheSameCompetition` now expects the managed update to be `Unknown` instead of success, because the provider double returns a window 15 minutes off (mismatched-receipt contract from an earlier item). Its lock/link-refusal assertions are kept. Reasonable, but it is a contract change absorbed in item 5; the item that introduced the contract should own it.
- **N4 — Page handler unchanged.** No new display. The page's Disconnect check (`WiseOldMan.cshtml.cs:75`) requires link provenance External; code adoption never changes provenance, so external links with a code pass. Legacy Unknown-provenance links still cannot be disconnected from the page (service would allow it) — pre-existing, outside option 1.
- **N5 — Re-linking the same ID also retires the code.** Saving the same external ID again retires its stored code (requires re-entry). Conservative; acceptable.

## Confirmed (traced, no finding)

- **Lifecycle bounds.** Replacement allowed in Draft/SignupOpen/SignupClosed/Live; refused in AwaitingFinalReview, Finalized, Archived, Cancelled, Discarded (`Sync:116-117`). Disconnect refused once `ActualStartedAt` is set (`Sync:119`). Exact configured window enforced (`Sync:121-122`; test with a 1-second mismatch keeps the old link and code). Final Review refusal is pre-existing and not re-tested here.
- **Rejected/stored code does not block.** Record status Active/Pending/Failed with any credential status passes `HasProtectedConnectionAsync`; tests cover valid and Invalid credentials, pre-Live and Live, replace and disconnect (6 cases).
- **Old code is retired atomically and never reused.** Retirement happens in the same serializable transaction, under the event row lock, as the new link (`Sync:104-147`). The code and competition ID live in the same row and are only changed together (`Entity:234-273`). Every sender reads both from one row snapshot: management Update/Delete dispatch (`Mgmt:507-539` plus `ValidateUpdateDispatchAsync` `Mgmt:654-660` and the source-ID check `Mgmt:530-535`); update-all (`EventCompetitionUpdateAllService.cs` `ValidateEligibility` requires `management.CompetitionId == slot.CompetitionId` and sync match, code taken from that row). A retired record is Deleted with an empty code, so dispatch fails `ManagementChanged`/`CredentialUnavailable` and update-all skips. Code adoption validates against the linked ID, then re-checks under the lock that the link still has that ID (`Mgmt:141-143`), so a code typed for A cannot be stored against B. **A code for competition A is never sent with B.**
- **Concurrency of the guard.** Both checks re-run inside a serializable transaction after `SELECT … FOR UPDATE` on the event. `CreateAsync` takes the same event lock; `QueueUpdateAsync` locks the management row that retirement updates (serialization failure → "changed in another request"). Update-all holds the competition reference lock that `ConfigureAsync` also takes for old and new IDs. (Inferred from PostgreSQL serializable/row-lock semantics.)
- **No external WOM deletion.** No new delete call; retirement is local only; `RecordingManagementClient.DeleteCalls` stays 0 in all new tests.
- **Pending end updates for the old competition.** Cannot target the new competition: blocked while the operation is unresolved; once terminal, state is reset with the new link; a later end update for the new competition needs a new code and uses the new row (see L1).
- **WA-9 both orders.** Create Sending/Unknown → link refused (test, both outcomes). Link first → Create refused, 0 Create calls (test). Create started and finished during link provider validation → link refused under lock, Create's link kept (test). Pending/Claimed Create also blocks (same phase list).
- **Late responses after replacement.** Fetch: finalization requires same generation, competition, lease owner (`Sync:335`); `Reconfigure` increments generation; test shows held fetch fails, new link kept, no activity rows. Update: `CompleteUpdateAsync` requires op Sending/Unknown, matching competition IDs, record not Deleted (`Mgmt:1076-1080`); and replacement can't happen while it is outstanding.
- **Readback.** `GetAsync` makes no provider call (test: provider calls equal only the validation call). A retired record shows as no record, no operation, no `LastAppliedAt`; after re-adoption the record has no operation (old success not shown as proof).

## Executed vs claimed

I ran nothing. The evidence file claims 17 new cases (counted in the test file: 6 + 3 + 2 + 2 + 1 + 1 + 1 + 1 = 17), an earlier 19/19 run, a final AU20 filter run of 44/44, a zero-warning Release build and `git diff --check`. No test output log is committed; these are **claimed, not reproduced**.

## Acceptance coverage (item 5)

| Acceptance point | Code | Executed assertion in tests |
| --- | --- | --- |
| Replace before/during Live with valid or rejected code | Yes | Yes (4 theory cases) |
| Disconnect before first Live with valid or rejected code | Yes | Yes (2 cases) |
| Disconnect refused after first Live | Yes | Yes (Live case) |
| Replace/disconnect refused in Final Review and later | Yes (pre-existing) | No |
| Exact configured window kept on replacement | Yes | Yes (1-second mismatch) |
| Unresolved operation blocks (Pending/Sending/Unknown) | Yes | Yes (3 cases; Claimed/Retry by code only) |
| Replacement after a Conflict/terminal failure | **No (M1)** | No |
| Old code retired, never reused; new code bound to new ID | Yes | Yes |
| No external WOM deletion | Yes | Request level only (L3) |
| End-update state reset on replacement | Yes | No — assertion is vacuous (L1) |
| WA-9 Create Sending/Unknown → link waits | Yes | Yes (2 cases) |
| WA-9 link first → Create refused | Yes | Yes |
| WA-9 Create completes during link validation | Yes | Yes |
| Late fetch response after replacement | Yes | Yes |
| Late Update response after replacement | Yes (by blocking) | No direct test |
| Readback never fetches; old success not proof | Yes | Yes |
| ID-only credentials | Yes | Claimed via existing ID-only adoption test (not re-read here) |

## Suggested verdict

**PASS with notes, conditional on M1.** Credential retirement, code/competition pairing, WA-9 and the no-delete rule are correct and tested. But an external link whose last operation ended in Conflict can never be replaced or disconnected, which is narrower than approved option 1. If the planner rates M1 as required now, this is FAIL until M1 is fixed and tested; L1 and L3 are small test additions.
