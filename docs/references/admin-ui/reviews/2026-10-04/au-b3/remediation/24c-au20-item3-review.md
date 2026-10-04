# B3 (AU20) item 3 review — commit `fa24164` (WOM end update with persisted spaced retries)

Reviewer: Claude sub-agent, read-only. Branch `codex/participants-functionality`. Everything below was read with `git show fa24164:<path>`; no builds or test runs. Line numbers refer to files at `fa24164` unless stated. "Service" means `src/Bingo.Infrastructure/Events/EventCompetitionManagementService.cs`; "EndUpdate.cs" means the new partial file next to it; "client" means `src/Bingo.Infrastructure/WiseOldMan/WiseOldManCompetitionManagementClient.cs`.

I also checked that the later B3 commits (`6625617`, `6d29f2c`, `4b9f156`) do not change the code paths behind findings F1 and F2. Both are still present at the branch head.

## How the mechanism works (summary)

- **Request.** An early end or a Resume (item 2) records `EndUpdateStatus = Pending` and a target end on `EventCompetitionSynchronization`. It does **not** queue a WOM operation directly.
- **Pick-up.** The management worker (`src/Bingo.Web/Events/EventCompetitionManagementWorker.cs:25`) runs `ProcessDueAsync` every 20 seconds. On each pass it lists events whose end update is pending (Service:452-456) and calls `QueueUpdateAsync` for each. That call creates a normal `Update` management operation and runs it straight away (Service:367-371).
- **One attempt.** A pre-write read of WOM (`CheckManagedSourceAsync`), then a PUT through the client, then the result is handled.
- **Retry timing.** Any failure that is retried goes through `EndRetryAtAsync` (EndUpdate.cs:17-23). When the event has a pending end update, the delay is chosen from the operation's persisted `AttemptCount`: 1, 2, 4, 8, 16 minutes, then 30 minutes from then on. The delay is counted from the moment the failure is handled. If WOM gives a later retry time, the later time wins.
- **Persistence.** The due time is stored in `EventCompetitionManagementOperation.NextAttemptAt`. `ClaimAsync` refuses to claim an operation before that time (Service:989).

## Confirmed findings

### F1 — High: a 5xx, timeout or network error on the end update ends in a permanent rejection instead of further retries

**What the code does today**
- For a PUT, the real client never returns the retryable `Unavailable` status. HTTP 5xx, timeouts (`OperationCanceledException` without caller cancellation) and `HttpRequestException` all map to `Unknown` (client:187-205). Only 429 and the local limiter return a retryable status, `RateLimited` (client:156, 166-169).
- After an `Unknown` write, `ExecuteAsync` reads WOM once. If WOM does not already show the target, the operation is marked `Unknown` and a reconciliation read is scheduled 1 minute later (Service:563-573).
- Reconciliation never writes again; it only reads. If the read succeeds and WOM still shows the old end, the code calls `FailOperationAsync("ExternalDrift", …, Conflict)` (Service:849).
- Item 3 added a step to `FailOperationAsync` that sets the pending end update to `Rejected` (Service:1228). The management record is also set to `Conflict`. That status blocks every later `QueueUpdateAsync` for the event (Service:276).

**What is required**
- WA-2 item 4 and the ticket (point 4) say temporary failures are retried with spaced backoff, and retries may continue until publication.
- The B3 decision says only a permanent rejection stops retries.
- The item 3 evidence file itself says unknown outcomes "must not become definite rejection".

**Failure scenario**
1. Admin early-ends at 21:59:55, so the target end is 22:00. The worker attempts the update at about 22:00:10.
2. WOM returns 502 (or the request times out) and does not apply the change. The client reports `Unknown`.
3. The immediate read succeeds and shows the old end of 23:00. The operation becomes `Unknown`, with reconciliation due at 22:01:10.
4. At 22:01:10 the read again shows 23:00. The code records `ExternalDrift`, the end update becomes `Rejected`, and the management record becomes `Conflict`.
5. No further write is ever attempted. At publication the item 4 fallback ("WOM end could not be updated") is used, even though WOM recovered within a minute.

If WOM is completely down (reads fail too), reconciliation keeps rescheduling with the spaced delays. The first successful read after recovery still ends in `ExternalDrift`/`Rejected`. The same thing happens after a crash mid-attempt: the 5-minute claim expiry turns the operation into `Unknown`, and the read then shows the old end.

**Consequence.** The documented 1/2/4/8/16/30-minute **write** retries are reachable only for HTTP 429 and local limiter refusals. The tests hide this, because they inject `WiseOldManCompetitionWriteStatus.Unavailable` directly. The real client never returns that status for an update.

**Fix direction (for the planner).** For a pending end update, a successful reconciliation read that shows the old values proves the write was not applied. That case should put the operation back into `Retry` with the spaced delay, not `Fail`. It is a resend with read-back proof, not a blind resend.

Status: traced end to end (client → `ExecuteAsync` → `ReconcileUnknownAsync` → `FailOperationAsync`).

### F2 — Medium: regression for existing pre-live roster updates — every successful PUT with teams is now recorded as "unknown outcome"

**What the code does today**
- Item 3 added a check before a successful update is saved: `Matches(result.Competition, updatePayload)`. If it fails, the result becomes `MarkUnknownAsync("MismatchedReceipt")` (Service:575-576).
- For a payload that includes teams, `Matches` compares participant names (Service:1393-1400).
- The real client's `ParseSuccessAsync` always builds the receipt with an empty participant list (client:277).
- So every successful roster update with at least one participant fails the check:
  1. the operation becomes `Unknown`;
  2. the management record becomes `Unknown` (Service:1249-1255);
  3. `QueueUpdateAsync` is paused for that event until reconciliation;
  4. a reconciliation read 1 minute later usually confirms the update and completes it.
- Non-end reconciliation still has the 3-attempt cap (Service:861). If WOM reads are unavailable during that window, a write that actually succeeded escalates to `ReconciliationRequired`/`Conflict`, which needs Admin recovery.

**What is required.** Brief section 3 item 3 says to keep every existing guard. The review task asks that existing create/update operations are not broken.

**Failure scenario.** An Admin finalizes a pre-live roster. WOM accepts the PUT. The website then shows an unknown-outcome state for about a minute and runs an extra read. Any roster edit in that minute waits behind the paused status. If WOM reads fail three times, a successful write becomes a Conflict.

**Why tests miss it.** The test helper `Success(...)` (tests/.../EventCompetitionManagementIntegrationTests.cs:1166-1171) fills participants from the payload, unlike the real client.

Status: traced. The user-visible effect on the current Manage page is inferred.

### F3 — Low: a late permanent failure of an old target blocks the new Resume target

**What the code does today**
- If an attempt for target E1 is in flight (`Sending`) when the Admin presses Resume (new target E2), a 400 or other permanent response for E1 runs `FailOperationAsync`.
- `RejectEndUpdate(E1)` is correctly a no-op, because the target is now E2.
- The management record is still marked `Failed`, with `LastErrorAt` set to the failure time. That time is **after** E2's `EndUpdateRequestedAt`.
- The bypass at Service:277-278 (`EndUpdateRequestedAt > LastErrorAt`) therefore does not apply. `QueueUpdateAsync` returns "paused pending Admin recovery" on every pass, and E2 is never attempted.

**Scenario.** The E1 attempt starts at 22:00:10. The Admin presses Resume at 22:00:11. WOM returns 400 for E1 at 22:00:12. E2 stays `Pending` without any attempt until publication, which then falls back.

**Requirement.** WA-2 item 3 (Resume) says the retry rules apply to the Resume update too.

Status: traced. The timing window is narrow.

### F4 — Low: a new target continues the old attempt count

**What the code does today**
- When a `Retry` operation is coalesced with a new target, `ReplaceDesired` (EventCompetitionManagement.cs:297-307) resets `NextAttemptAt` to now, so the first attempt for the new target is immediate. That part is correct.
- It does not reset `AttemptCount`. If the new target's first attempt then fails, the next retry is 30 minutes later instead of 1 minute.
- Any increase in the event's version (Service:349-359) also resets `NextAttemptAt` to now.

**Scenario.** E1 has failed six times (now on 30-minute spacing). The Admin Resumes to E2, which is attempted at once. WOM returns 429. The next E2 attempt comes 30 minutes later, not 1 minute later.

**Assessment.** This is arguably fine, because it is still spaced and is not a fast loop. Recorded for the planner to accept or reject.

Status: traced.

### F5 — Low: HTTP 408 is treated as a permanent rejection

**What the code does today.** Every 4xx other than 401, 403, 404 and 429 becomes `Validation` (client:181-185). Through `HandleResultFailureAsync`, that stops the end update permanently. This includes HTTP 408 Request Timeout, which is transient by nature.

**Assessment.**
- 409 and 422 as permanent are consistent with the planner resolution that Codex cites.
- 408 is the only status in that group that is clearly transient. WOM rarely sends it, so the risk is low.

Status: traced.

### F6 — Low: reconciliation queue could stall after publication

**What the code does today**
- After publication, `ReconcileUnknownAsync` returns early for update operations (Service:791). It does not clear `NextAttemptAt` and does not change the phase.
- Those operations stay in the reconciliation query (Service:433-436), which takes the 50 oldest due `Unknown` operations, ordered by `NextAttemptAt`.

**Consequence.** Once 50 such dead operations exist, other events' reconciliations are never selected. A smaller cost: published events' dead operations are queried again on every 20-second pass.

**Assessment.** This is unrealistic at the site's scale.

Status: traced. The impact is inferred.

### Note N1 — How soon after the click the first attempt happens

- Early end and Resume do not call `QueueUpdateAsync` themselves (`EventLifecycleService.cs:417-422` only records the request).
- The first attempt happens on the next worker pass. That is at most about 20 seconds plus the length of the previous pass (worker:25). Before the PUT, the attempt also does a pre-write read.
- This meets "at once" in spirit (no backoff before the first attempt). The planner may want to note the up-to-20-second delay.

### Note N2 — Change to `CompleteUpdateAsync`

- The removed lines (old Service ~1034-1036) used to record the current preview's fingerprint as "applied", even when that was not what had been sent.
- Recording `operation.DesiredFingerprint` instead is more correct. At worst it causes one redundant follow-up write.

### Note N3 — The other-4xx classification is not recorded in the decisions file

- The evidence (`au-b3/item3-end-update-retries.md`) attributes the classification to "Planner `/root`, chat `01a10660-…`":
  - 429 is retried, using the later of the scheduled backoff and WOM's retry time;
  - 401/403, 404 and other Validation responses stop.
- I could not verify this. `review-notes/08-decisions.md` has no matching entry.
- The brief (item 3, "Other 4xx: report to `/root`") required this report. The planner should confirm that it accepted the classification and record it.

## Answers to the specific checks

**Schedule**
- First attempt: on the next worker pass (≤ about 20 seconds).
- Retry delays: `AttemptCount` 1→1 min, 2→2, 3→4, 4→8, 5→16, 6 and above→30 (EndUpdate.cs:20). This matches the decision.
- Persisted: yes, in `NextAttemptAt`, which `ClaimAsync` enforces (Service:989). It survives a restart.
- Repeated worker passes do not reset the due time (coalescing guard at Service:349-357).
- The delay is counted from when the failure is handled, which is just after the attempt.
- Stops at publication: a due operation is cancelled at claim (Service:991-996), `QueueUpdateAsync` refuses (Service:254), reconciliation returns early (Service:791), and a late receipt is refused (Service:1065-1069).
- But see F1: the write retries apply only to 429 and limiter refusals.

**One attempt at a time**
- `ClaimAsync` takes a row lock (`FOR UPDATE`) on the operation inside a Serializable transaction, re-reads it, and moves it to `Sending`. A second worker then sees `Sending` and backs off.
- `QueueUpdateAsync` locks the management row and refuses to create a second operation while one is `Claimed`/`Sending`/`Pending`/`Retry`. While an operation is `Unknown`, the management `Unknown` status blocks new operations.
- `CompetitionReferenceLock` is held around the dispatch.
- Manual actions (`SignupService:2262`) go through the same `QueueUpdateAsync`.
- Crash mid-attempt: after 5 minutes, `Sending` becomes `Unknown` with reconciliation (Service:424-431, 1261-1262). No resend happens without a read. The not-applied outcome then hits F1.

**Stale targets**
- A successful response for the old target cannot mark the new target done:
  - `CompleteEndUpdate` requires the WOM end to equal the event's current configured end (Service:1083);
  - the domain method requires target equality (`EventCompetitionSynchronization.CompleteEndUpdate`);
  - `RejectEndUpdate` is guarded the same way.
- After an old-target success, the next pass queues the new target, because the fingerprint differs.
- The guard is a target-value comparison, not a generation number. That is sufficient here, because the values differ.
- See F3 for the failure side.

**Error classification (end update)**

| Response | Classification | Effect |
|---|---|---|
| 400 `COMPETITION_START_DATE_AFTER_END_DATE` | `Validation` | stops (tested through the real client and the service) |
| Missing or invalid verification code | stopped with no write | missing/Deleted record → `MissingCredential`; `!CanWrite` → its credential code; 401/403 → credential marked Invalid, then stops |
| 404 | stop | |
| Other 4xx (including 408) | stop | see F5 for 408 |
| 429 | retry, later of backoff and WOM's retry time | |
| 5xx, timeout, network | `Unknown`, read-only reconciliation | finally `Rejected` (F1) |

- No permanent error is retried forever: a stopped request is `Rejected`, and the management record is `Failed`/`Conflict`.

**Guards and fetch leases**
- Existing claim, operation, delete, shared-reference and source-check guards are kept.
- Fetch leases are not touched. A matching receipt makes a normal refresh due (`MakeNormalRefreshDue`).
- F2 is a regression in existing pre-live update behaviour.

**Provider text**
- `EndUpdateErrorCode` stores the provider's `code` field, with the verification code redacted and capped at 100 characters (domain `RejectEndUpdate`).
- `SafeError` keeps provider messages up to 500 characters, as before.
- No audit entries were added.
- This is the same exposure class as before, not a new leak.

## Acceptance coverage (item 3)

"Claimed" means the evidence file reports the test as passed; I did not run any tests.

| Requirement | Test | Executed assertion? |
|---|---|---|
| Spacing 1,2,4,8,16,30,30 with a controllable clock; no early or repeat dispatch | `Au20EndUpdateBackoffIsPersistedAndRepeatedWorkerPassesNeverAccelerateIt` | Claimed. It asserts `NextAttemptAt`, attempt count and write counts per step. **But** it uses `Unavailable`, which the real client never returns for a PUT (F1). |
| 429: later of backoff and WOM's retry time | `Au20RateLimitUsesLaterOfBackoffAndProviderDue` | Claimed (first step only) |
| Non-transient 400 stops | `Au20NamedHttp400Retains…` (real client) + `Au20PermanentRejectionNeverRetries…` (service theory; also 401, 404, other Validation) | Claimed |
| Missing or invalid credential stops with no write | `Au20AbsentOrInvalidCredentialStopsWithoutWriting` | Claimed |
| Unknown outcome: spaced reads, no second write | `Au20UnknownEndUpdateReconcilesWithSpacedReadsWithoutASecondWrite` | Claimed. Reads are forced to `Unavailable` after the write, so the "WOM shows the old end" path (F1) is **not** covered. |
| Stop at publication (due retry) | `Au20OfficialHistoryStopsADueEndRetry` | Claimed |
| Stop at publication (reconciliation, late receipt) | none | Not tested |
| Concurrency: one write in flight | `Au20ConcurrentEndWorkersHaveOnlyOneProviderWriteInFlight` (two in-process workers) | Claimed |
| Success path marks the end matched | end of the backoff test | Claimed |
| Stale target (Resume during an in-flight attempt), success and failure | none | Not tested (F3 untested) |
| Crash mid-attempt / expired claim | none for the end update | Not tested |
| Transient 5xx/timeout through the real client | none | Not tested (F1) |
| Existing pre-live roster update with the real client's receipt shape | none (test double differs) | Not tested (F2) |

## Suggested verdict

**FAIL.** The decided transient-retry behaviour is not reached for the main transient cases. A WOM 5xx, timeout or network error ends in a permanent `Rejected` end update after one reconciliation read (F1). Item 3 also changes successful pre-live roster updates into "unknown outcome" with the real client (F2). Both need a fix and a test that uses the real client's result shapes before item 3 can pass.
