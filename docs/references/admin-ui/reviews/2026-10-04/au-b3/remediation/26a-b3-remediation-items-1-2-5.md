# B3 (AU20) remediation recheck: items 1, 2 and 5

Reviewer: Claude sub-agent, read-only, 4 October 2026. Branch `codex/participants-functionality`.
- **Commits checked:** `1a47b5e` (item 1), `f441134` (item 2), `b2c3e1c` (item 5); base `4b9f156`; tip `45a09ad`.
- **Method:** `git show` / `git diff` only. Nothing was built or run. Test results are the implementer's.
- **Line numbers:** given at the tip `45a09ad`, unless stated otherwise. For the service, the domain file, the `EndUpdate.cs` partial and the client, the tip is byte-identical to `b2c3e1c`, because items 3, 4 and 6 did not touch them.
- **Short names:**
  - "Service" means `src/Bingo.Infrastructure/Events/EventCompetitionManagementService.cs`.
  - "EndUpdate.cs" means the partial file next to it.
  - "Client" means `src/Bingo.Infrastructure/WiseOldMan/WiseOldManCompetitionManagementClient.cs`.
  - "Tests" means `tests/Bingo.IntegrationTests/Au20RemediationManagementIntegrationTests.cs`.

## How the fix works now (summary)

**Item 1**
- After a write whose outcome is unknown, the service reads WOM back. If WOM shows the requested values, the update completes.
- If WOM still shows exactly what the website last applied, the new `RetryUnappliedEndAsync` (EndUpdate.cs:28-48) puts the operation back into `Retry`. "Exactly what was last applied" means the title, the window and the acknowledged roster all match (`RemoteConfigurationMatches`, Service:981-1005).
- There are two entry points:
  - **Immediate read** after the write (Service:584-588). The next attempt is due after the agreed delay for the current attempt count.
  - **Scheduled reconciliation read** (Service:858-862). The operation is due at once, because the reconciliation itself already waited the delay.
- `RetryUnappliedEndAsync` returns `null`, and so falls back to the old Unknown handling, in two cases:
  - the stored payload includes teams;
  - the event has no pending end update.
- Repeated failed reconciliation reads no longer turn into Conflict after 3 tries when an end update is pending. That exemption already existed at the base (Service:885).
- The client now classifies HTTP 408 on create/update as `Unknown`; before, it was `Validation` (client:181-185).

**Item 2**
- The write response is checked with the new `MatchesWriteReceipt`, which compares title, start and end only (Service:1431-1434, used at :592).
- The read-back paths still use `Matches`, which also compares participants when the payload includes teams (Service:580, :853, :1436-1443).

**Item 5**
- `FailOperationAsync` (Service:1243-1275) first locks the event row and the synchronization row. It then compares the attempted end (from the operation's stored payload) with the current `EndUpdateTargetAt`.
- If the end update is pending and the two values differ, the failure is "outdated":
  - the end update is not rejected;
  - the management record is set back to `Pending`, not `Failed`/`Conflict`;
  - the operation itself still ends `Failed`, so it stays in history.
- The attempt count is reset to 0 when a pending/retry operation is coalesced to a different end value (Service:370-371, domain `ReplaceDesired`). The same happens when a stale in-flight operation is refreshed (Service:781-783, domain `RequeueCurrent`).
- The old target is identified by **value** (end timestamp), not by a generation counter.

## Findings

### R1 — Medium: a resend after a late-applied write is treated as external drift and permanently rejects the end update

- **What the code does:**
  - Every attempt, including a resend created by item 1, starts with `CheckManagedSourceAsync` (Service:574, :926-942).
  - That check accepts only one remote state: the one the website last applied (`RemoteConfigurationMatches` against `LastAppliedRemoteFingerprint`).
  - Any other successful read becomes `FailOperationAsync("ExternalDrift", …, Conflict)`. For an end update that is not outdated, this calls `RejectEndUpdate` (Service:1255) and sets the management record to Conflict.
  - The check has no branch for "WOM already shows the values this operation wants to send". Only the post-write and reconciliation paths have that branch (`Matches` at Service:580 and :853).
- **What was required:**
  - Item 1 asks that a read showing the target end completes the update, and that temporary failures never become permanent rejections.
  - Before item 1, an end update was never resent after an unknown outcome, so this path could not be reached. Item 1 creates it.
- **Failure scenario:**
  1. Early end; the target is 22:00. The attempt at 22:00:10 times out on our side (`HttpClient` timeout), or a gateway returns 504. Meanwhile WOM's backend is still processing the PUT.
  2. The immediate read-back runs before WOM commits, so it shows the old end 23:00. Item 1 correctly schedules a resend at 22:01:10.
  3. WOM then commits the first PUT. The competition now ends at 22:00.
  4. At 22:01:10 the resend's pre-write check reads 22:00. That is not the last applied fingerprint (23:00), so the result is "changed outside Bingo": `ExternalDrift`, the end update is `Rejected`, and the management record is `Conflict`.
  5. WOM has the correct end, but the website records a permanent rejection. Publication then uses the "WOM end could not be updated" fallback. The effect at publication is inferred; I did not trace it.
- **Fix direction:** in the pre-write check, a successful read that already `Matches` the operation's payload should complete the operation through `CompleteUpdateAsync` without writing.
- **Traced vs inferred:**
  - The code path is traced (ExecuteAsync → CheckManagedSourceAsync → FailOperationAsync → RejectEndUpdate).
  - How likely it is that WOM commits a write after its response was lost is inferred. It is a common pattern with timeouts and 504s.
- **Tests:** no test covers it.

### R2 — Low: a malformed or forbidden read still turns into a permanent rejection

- **What the code does:**
  - The read client (`WiseOldManClient.GetCompetitionAsync`) returns `Invalid` in three cases:
    - HTTP 400 or 403;
    - incomplete competition details;
    - an unreadable body (`JsonException`), for example an HTML maintenance page served with status 200.
  - Neither the pre-write check (Service:933-941) nor reconciliation (Service:863-873) treats `Invalid` as temporary. Both end in `ExternalDrift` → Conflict → end update `Rejected`.
- **What was required:** item 1 says a read that fails is rescheduled, and only a definite permanent answer stops the retries.
- **Failure scenario:** during a WOM incident, a proxy returns a 200 HTML page for the competition read. The pending end update is permanently rejected, although no WOM answer said the end was refused.
- **Assessment:**
  - This was present before the remediation, and the brief's classification list covers write responses.
  - I report it because the review question asks whether *any* path still turns a temporary failure into a permanent rejection.
  - Likelihood is low.
- **Traced vs inferred:** traced. The likelihood is inferred.

### R3 — Low: an old target that is "unknown" still delays the new target by its old reconciliation backoff

- **What the code does:**
  - While an operation is `Unknown`, the management status is `Unknown`. `QueueUpdateAsync` then refuses to queue anything (Service:287-290).
  - After a Resume, the new target therefore waits until the old operation's reconciliation is due. That due time comes from the old attempt count, up to 30 minutes (`RescheduleUnknownAsync`, Service:891).
  - At that point, item 1 turns the old operation into a due `Retry`, and the same worker pass coalesces it to the new target with the count reset. From then on, the new target's spacing is correct.
- **What was required:** "the attempt count and backoff restart for each new target" (item 5; 24c F4).
- **Failure scenario:**
  1. WOM reads are down for an hour, so the old target's operation sits in `Unknown` with 30-minute reconciliation spacing.
  2. WOM recovers at 22:31, just after the Admin presses Resume.
  3. The new target is first sent at the next reconciliation, up to about 30 minutes later, instead of at once.
- **Assessment:** this keeps one attempt at a time and is never a fast loop. It is a delay, not a rejection.
- **Traced vs inferred:** traced.

### R4 — Low: changing 408 in the client also changes Create and roster updates

- **What the code does:** the 408 branch (client:181-185) sits in the shared `SendAsync`, which is used by both `CreateAsync` and `UpdateAsync`.
  - **Create:** a 408 used to be `Validation`, so the create failed and the Admin could retry. It is now `Unknown`. A website Create left "unknown" is never resolved and blocks linking for that event; this is the known dead end 24e L2. The create is still never blindly resent, so no duplicate competitions arise.
  - **Non-end update (pre-Live roster):** a 408 used to fail the operation. Now the update is read back:
    - if the read shows the target, the update completes, which is an improvement;
    - if the read shows the old state, the old path applies: reconciliation, then `ExternalDrift`/Conflict.
  - Either way, Admin recovery is needed for that branch, as before.
  - **Delete (`SendDeleteAsync`) and update-all participants:** not changed.
- **What was required:** non-end operations keep their previous semantics; a create is never blindly resent.
- **Assessment:**
  - No duplicates are possible.
  - The only visible change is that a create that got a 408 now ends in the stuck "unknown" state instead of a clean failure.
  - Treating 408 as ambiguous is defensible.
- **Traced vs inferred:** traced. That WOM actually returns 408 on create is inferred to be rare.

### R5 — Note: a late permanent answer for an old target does not invalidate the credential

- **What the code does:**
  - When the failure is outdated, `FailOperationAsync` skips both `MarkCredentialInvalid` and `MarkFailure` (Service:1264).
  - A 401/403 is about the management code, not about the target. Ignoring it costs one more request with the same code.
  - That next request, for the current target, gets 401 again. It is not outdated, so the end update is rejected and the credential is marked invalid.
  - Other "outdated" failure causes are found again in the same way on the next attempt, because the guards run again at queue and dispatch: `ExternalDrift`, `SourceMissing`, `SharedSource`, credential states.
- **Assessment:**
  - The final state is correct; there is one extra WOM call.
  - The test `Au20RemediationLateOldTargetRejectionCannotBlockResume(Unauthorized)` asserts `CanWrite == true` after the late 401 (tests:193). It therefore locks this behaviour in.
  - Accept it, or change it so that a 401/403 invalidates the credential regardless of target.
- **Traced vs inferred:** traced.

### R6 — Note: smaller observations

- **A→B→A coalesced within one worker pass keeps the old count.**
  - The reset compares the pending operation's end with the new end (Service:371).
  - Suppose Resume (A→B) and a new early end (B→A) both land between two worker passes, and the new target A is the same minute value as the old one. The count is then not reset.
  - The window is about 20 seconds and needs the same minute value. Very unlikely.
- **A→B→A with a late result for the first A.**
  - A late success for A completes the current target A. That is correct, because WOM now has A.
  - A late permanent 400 for A rejects the current A. That is correct, because the same value would be refused again.
  - A late success or failure for B while the target is A: the success is recorded as applied metadata without completing; the failure is treated as outdated. Both are traced and correct.
- **Combined payload (end plus title).**
  - During Live, every update payload is title + window, without teams.
  - A resend is the identical absolute PUT. The read proved that title, window and roster are all unchanged, so the resend is safe.
  - Payloads with teams are excluded from the resend (EndUpdate.cs:38). A pending end update together with a teams payload is effectively unreachable:
    - an early end requires Live;
    - a teams payload after Live is converted to `StaleUpdate` at Service:682-683.
  - The check reads the *stored* payload rather than the end-only payload actually sent (Service:572-573). That only matters in a narrow race, and there it falls back to the old Unknown behaviour.
- **Publication during the immediate path.**
  - `RetryUnappliedEndAsync` returns "Stopped" without changing an operation that is still `Sending` (EndUpdate.cs:37).
  - After claim expiry, that operation becomes `Unknown` and is then skipped by reconciliation forever. This is the existing 24c F6 queue-hygiene issue; it is harmless at this site's scale.
- **Item 5 proof timing.**
  - The test advances the clock 1 minute before the next pass (tests:195). After an outdated failure, though, a fresh operation for the new target is created and sent on the **next worker pass**, not after a 1-minute backoff.
  - So the test would also pass with no clock advance. The behaviour is better than the brief's wording requires.

## Item checks in detail

### Item 1 — temporary failures are retried (commit `1a47b5e`)

| Check | Result | Where |
|---|---|---|
| Unknown, then the read shows the old end → not applied → resend on schedule | Yes. Immediate read: `Retry` at `EndRetryAtAsync` (1, 2, 4, 8, 16, 30 min by attempt count). Reconciliation read: due at once, after the reconciliation delay already waited. | EndUpdate.cs:28-48; Service:584-588, :858-862 |
| The read shows the target → completes | Yes, `Matches`, then `CompleteUpdateAsync` | Service:580-583, :853-856 |
| The read fails → reschedule, not 3 strikes | Yes for `Unavailable`/`RateLimited` (exempt while an end is pending). **No** for `Invalid` reads (R2). | Service:868-871, :885; R2 |
| Temporary: 5xx, timeout, network, 408, 429 | 5xx/timeout/network/408 → `Unknown`, then read-back. 429 → `RateLimited`, then `Retry` (later of backoff and provider time). | client:166-170, :181-185, :192-211; Service:1157-1170 |
| Permanent: named 400, missing/invalid code, 401/403/404, other validation | All end in `FailOperationAsync`, then `RejectEndUpdate`: `Validation`, `Unauthorized` (+ credential invalid), `NotFound`. Missing/unwritable code → `RejectPendingEndAsync` at queue time, or a failure at dispatch. | client:171-191; Service:270, :284, :512-519, :538-540, :1171-1182 |
| Crash mid-attempt → resend | Yes. Claim expiry, then `Unknown` with the agreed delay, then the reconciliation read shows the old end, then `Retry` and a resend. | Service:436-443, :1298-1299; EndUpdate.cs:40 |
| One attempt at a time | Kept. The resend goes through `Retry`, then `ClaimAsync` (row lock, Serializable). `RetryUnappliedEndAsync` locks the event and operation rows and checks the expected phase. | Service:1007-1027; EndUpdate.cs:31-36 |
| No fast loop | None found. Every `Retry` that item 1 creates is due at least 1 minute after the last write. The reconciliation-path resend happens at once, but only after a scheduled reconciliation delay of at least 1 minute. | EndUpdate.cs:40 |
| Non-end operations unchanged | Create: unchanged (never resent). Delete: unchanged. Non-end update: `RetryUnappliedEndAsync` returns `null`, so the old Unknown/reconciliation/Conflict path applies. Only 408 changes (R4). | EndUpdate.cs:38; R4 |
| Remaining path to permanent rejection | R1 (late-applied write) and R2 (`Invalid` read). | |
| **Proof:** real client + HTTP fake, 502 → timeout → 200, controlled clock | Present. `Au20RemediationRealHttpTemporaryWritesRetryOldWindow` (tests:18-67) builds the real `WiseOldManCompetitionManagementClient` around an `HttpMessageHandler` fake, through `IHttpClientFactory`, with the real limiter and the `TestClock`. Variants: 502, then timeout / network / 408, then 200. It asserts `Retry` with `NextAttemptAt` = +1 and +2 minutes, no dispatch 1 µs early, gaps of exactly 1 and 2 minutes, then `Succeeded`. The read side is a stub that returns the fake's current remote state. That is acceptable, because the brief only requires the write to go through the real client. | tests:15-16, :334-342 |
| **Proof:** crash/claim expiry | Present. `Au20RemediationExpiredClaimReadsOldWindowThenResends` (tests:69-102): a claimed (`Sending`) operation, +5 min → `Unknown` with no write, +1 min → one write through the real client, end update `Succeeded`. | |

### Item 2 — write receipts (commit `f441134`)

| Check | Result |
|---|---|
| The receipt compares title and window, and participants only if present | It compares title and window only. The real client never returns participants on a write (client:282), so "only if present" comes down to "never". That matches the requirement. |
| The read-back path still compares participants | Yes. Service:580 and :853 use `Matches`, which compares participant names when the payload includes teams. It should, because `GetCompetitionAsync` returns the participations. |
| Can a wrong roster be marked applied? | A 200 from WOM is trusted for the roster, as before AU20 item 3 added the check. The next pre-write check compares WOM's actual participants with `LastAcknowledgedRosterJson` (Service:984-1004) and would catch a mismatch as drift. No weakening beyond the pre-AU20 baseline. |
| Note | `ParseSuccessAsync` uses the *requested* title/start/end when the response omits them (client:278-280). So the window check only bites when WOM echoes the fields. This is pre-existing and acceptable. |
| **Proof:** real client parsing | Present. `Au20RemediationRealHttpRosterReceiptCompletesWithoutReconciliation` (tests:104-126): the real client parses a receipt with `id/title/startsAt/endsAt` and no participants; the request has teams. It asserts `Succeeded`, 1 write and exactly 1 read (the pre-write check only), and that the roster is acknowledged. |

### Item 5 — outdated targets (commit `b2c3e1c`)

| Check | Result |
|---|---|
| A late failure for the old target doesn't block or reject the new target | Yes. An outdated failure skips `RejectEndUpdate` and sets the management record to `Pending` (Service:1253-1264), so `QueueUpdateAsync` is no longer paused. The comparison runs under the event and synchronization row locks. |
| Attempt count and backoff restart per new target | Yes for `Pending`/`Retry` coalescing and for a stale `Sending` refresh. A new operation starts at 0. Partly no while the old operation is `Unknown` (R3). |
| 408 temporary | Yes (item 1, client:181-185) |
| How the old target is identified | By end value: `DeserializePayload(operation.DesiredPayloadJson).EndsAt` against `EndUpdateTargetAt`, and against `EventEndsAt` in `CompleteUpdateAsync` (Service:1107). There is no generation counter. A→B→A analysis: R6, correct. |
| Late success of the old target | Recorded as applied metadata; does not complete the new target (Service:1104-1111). The new target is queued on the next pass. |
| **Proof:** early end, Resume, late permanent failure for the old target, new target attempted | Present. `Au20RemediationLateOldTargetRejectionCannotBlockResume` (tests:151-203), with Validation (named 400 code) and Unauthorized. Not rejected, `CanWrite` stays true, the next pass sends the new target, end update `Succeeded`, new operation `AttemptCount == 1`. It uses the stub management client, which is fine for this item. See the R6 note on "1 minute later". |
| **Proof:** count restart | `Au20RemediationResumeRestartsBackoffAfterLongOldTargetRetry` (tests:205-238): six 429s reach 30-minute spacing; after Resume, `AttemptCount` is 0, then 1, with the next attempt +1 minute. |

### Test weakening

- `1a47b5e`, `f441134` and `b2c3e1c` change only the new file `Au20RemediationManagementIntegrationTests.cs`. They delete no test lines.
- Across the whole range `4b9f156..45a09ad`, the only removed test lines belong to item 6:
  - `Au20EarlyEndRetainsActualPrecisionAndQueuesCeilingMinute` was parameterized and gained the 22:00:00.0004 case. All the old cases are kept.
  - The setup of `StatsPass4BoundaryIntegrationTests` changed, as the brief asked.
- The new remediation tests were not edited after their item commits.
- The base test `Au20UnknownEndUpdateReconcilesWithSpacedReadsWithoutASecondWrite` is unchanged and still consistent: its reads are unavailable and then show the target, so no resend is expected.
- **Nothing is weakened.**

## Verdict

| Item | Verdict | In one line |
|---|---|---|
| 1 Temporary failures retried | **PASS with one fix (R1)** | The required behaviour and both proofs are present and use the real client. A write that WOM applies after its response was lost now makes the resend's pre-write check report drift and permanently reject the end update (R1, Medium). An unreadable or forbidden read still ends in rejection (R2, Low, pre-existing). |
| 2 Write receipts | **PASS** | The receipt compares title and window; read-backs still compare participants; the proof goes through the real client's parsing. |
| 5 Outdated targets | **PASS with notes** | Late failures for an old target are isolated by end value, and the count restarts when an operation is coalesced. An `Unknown` old operation can still delay the new target by its old backoff (R3, Low). A late 401 for the old target doesn't invalidate the credential (R5, Note). |
| Tests not weakened | **PASS** | |
