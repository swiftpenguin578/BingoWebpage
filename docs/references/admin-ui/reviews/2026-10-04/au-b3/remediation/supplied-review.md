# B3 review: AU20 (WOM configured window, early end, Resume, retries, fallback)

Range `2649008..4b9f156` (register commit plus items 1–6). Reviewed 4 October 2026 by Claude (planner), read-only.

**How this review was done**
- Six reviewer agents, one per item:
  - [24a](24a-au20-item1-review.md): item 1
  - [24b](24b-au20-item2-review.md): item 2
  - [24c](24c-au20-item3-review.md): item 3
  - [24d](24d-au20-item4-review.md): item 4
  - [24e](24e-au20-item5-review.md): item 5
  - [24f](24f-au20-docs-register-scope-review.md): item 6, the register and scope
- Claude traced every finding marked "verified".
- Nothing was built or run. Test counts are the worker's.

## Verdict: FAIL, remediation round needed

| Item | Verdict | In one line |
| --- | --- | --- |
| 1 Exact window | PASS with notes | All three 5-minute leeways are gone; the stored reason values are unchanged and new ones appended. |
| 2 Early end / Resume | PASS with one fix | Rounding and the same-transaction update are correct; a database conflict gives an error page. |
| 3 Retries | **FAIL** | A WOM outage ends the update for good instead of retrying; roster updates now end as "unknown". |
| 4 Fetch suppression / fallback | PASS with notes | Every fetch path is blocked after the real end; Publish is never blocked. One question for you (D6). |
| 5 External link / WA-9 | PASS with one fix | The old code is never reused and nothing is deleted on WOM, but a "conflict" link can never be replaced. |
| 6 Docs, register, scope | PASS with notes | Accurate; one register row missing; scope clean; no test weakened. |

---

## Required fixes

**F1 (High, verified): one WOM outage permanently stops the end update.** (item 3)
- **Today:**
  - When WOM answers the end update with a server error (5xx), times out, or the network fails, the client reports "outcome unknown" (`WiseOldManCompetitionManagementClient.cs:187-205`).
  - The service never sends the update again. It only reads the competition back.
  - Because the update didn't happen, WOM still shows the old end. The service treats that as "drift": the operation becomes Conflict, and the end update is marked rejected for good (`EventCompetitionManagementService.cs:860`, then `RejectEndUpdate` in `FailOperationAsync`).
  - A crash during an attempt ends the same way.
  - The 1/2/4/8/16/30-minute retries therefore only ever apply to rate limits (429). The backoff test hides this by injecting a status that the real client never returns.
- **Example:** an early end at 21:59:55 happens while WOM has a 2-minute outage. About a minute later the update is permanently rejected, and the event goes to the "could not be updated" fallback at publication, although WOM was back almost at once.
- **Required:**
  - For end updates, "outcome unknown" followed by a read that still shows the old end means **not applied**. Send it again on the agreed retry schedule.
  - Server errors, timeouts, network errors and 408 are temporary.
  - Only a definite permanent answer stops the retries: the named 400, a missing or invalid code, 401/403/404 or another validation rejection.
  - Repeating the same end is harmless on WOM, so resending is safe.
- **Proof:** an HTTP-level fake that returns 502, then a timeout, then 200, through the real client; the retries are spaced as agreed and the update succeeds.

**F2 (Medium, verified): roster updates before Live now end as "unknown".** (item 3)
- **Today:**
  - Item 3 added a check that the update response matches the request, including participants (`EventCompetitionManagementService.cs:1404-1411`).
  - The real client always builds the success response with an empty participant list (client `:277`).
  - So every successful roster update is treated as unconfirmed. Management pauses until a read a minute later sorts it out, and if that read fails 3 times the result is Conflict.
  - The test helper fills in participants, so the tests miss it.
- **Required:** compare only what the write response actually carries (title and window), or parse the participants. Test with the real client's parsing.

**F3 (Medium, verified): a "conflict" link can never be replaced.** (item 5)
- **Today:** once an external link's last operation ends in Conflict, replacing or disconnecting it is refused for good, with a misleading "managed competition controls" message (`EventCompetitionSynchronizationService.cs:171`).
- **Why it matters:**
  - Approved option 1 and the design only block while an operation is queued or unknown, and nothing ever resets Conflict.
  - **Example:** the owner deletes the competition on WOM during Live, and the admin can't link the replacement.
  - **Combined with F1:** a single WOM outage during an early end makes an external link permanently stuck.
- **Required:** Conflict doesn't block replacement or disconnection of an external link; replacing it is the resolution. Test: competition deleted on WOM, then replaced.

**F4 (Medium, verified): early end and Resume can give an error page on a database conflict.** (item 2)
- **Today:**
  - Both now lock the WOM sync row inside a serializable transaction.
  - They don't catch PostgreSQL's serialization or deadlock errors (`EventLifecycleService.cs:150-152`, `:204-206`), unlike Start (`:111`).
  - If a WOM refresh touches that row at the same moment, the admin gets an error page. Nothing is saved. Clicking again moves the actual end.
- **Required:**
  - Retry the transaction internally, keeping the **original click time**, a few times before reporting a normal "try again" message.
  - Take the locks in the same order as the refresh worker.

**F5 (Low): after Resume, the new target can be blocked or delayed.** (item 3)
- **Today:**
  - A late permanent failure for the old target, arriving after Resume, blocks the new target, so it is never attempted.
  - The new target also keeps the old attempt count, so its first retry can be 30 minutes away instead of 1.
- **Required:**
  - Failures for an outdated target never affect the current target.
  - The attempt count restarts for each new target.

## Smaller fixes (same round)

**Documentation and register:**
- **Register row for Resume** (24f R1), with an RC entry:
  - `Overview.dc.html` asks for a new end only when the stored end has passed. AU20 always requires a future replacement end.
  - **Example:** an admin ends early by mistake at 21:59:05 and wants to resume at 21:59:30. A UI built to the reference would hide the end field, and the server refuses.
- **Complete the "could not be updated" register row:** name all end-update states, the target and request times, the safe rejection code, and the new fields on Final Review readiness and the publish result.
- **Remove stale "pending AU20" markers:**
  - `DELIVERY_PLAN.md:1086`, `:1680`, `:1683`
  - `FUNCTIONAL_CONTRACTS.md:164`
  - `FUNCTIONALITY_CHANGES.md:130`
- **DATA_MODEL:** the end-update status is stored by name, so those names must never be renamed.
- **Label results:** mark CURRENT_STATUS test results as worker-reported.
- **Record the error classification** (as corrected by F1) in DELIVERY_PLAN. The Codex planner decided it; the user didn't approve it.

**Publish message:** when the final fetch is skipped because the end couldn't be updated, the publish message and history currently say "skipped: no detail". Give them a clear text.

**Tests:**
- a sub-5-minute mismatch rejected at the fetch site;
- scheduled and manual fetches resuming after a successful update;
- a meaningful test that the pending end update is reset on replacement (today's test never sets one);
- the external-delete guard at dispatch;
- replacement refused in Final Review;
- a click just after a whole minute (22:00:00.0004 → 22:01).

**Stats test failure:**
- **Which test:** `StatsPass4BoundaryIntegrationTests.cs:125`.
- **Cause:** it has failed since `444bfc4` (3 October, F6), not because of AU20. The test moves the clock 2 hours past the end, then expects uploads to still be open.
- **Fix:** test setup only.

**Deploy check** (runbook, before the R-3 rehearsal and deploy):
- **The risk:** events that ended early under the old rules and are still in Final Review at deploy time behave differently.
  - Their WOM window was matched to the old rules, so it may now mismatch (24a N2).
  - Or the test-style "old" row could allow a fetch after the real end (24f N3).
- **The check:** count events in Final Review just before deploying. If any exist, stop for a decision. Expected count: 0.

## Noted, no action now
- **A website Create stuck "unknown" is never resolved,** and it now also blocks linking for that event (24e L2). This follows WA-9, but it is a dead end with no admin action. Candidate for a later ticket.
- **Race between the update and the publish transaction:** if an update succeeds in the same moment as the publish, the publication can say "skipped, end unmatched" while the status says the update succeeded. No post-end data gets in (24d L1).
- **Audit:** the early-end audit entry doesn't record the original configured end. The replacement audit entry doesn't say the old code was retired.
- **Item 1 (exact window) is solid:** every way configured times are saved stores whole minutes, so exact matching can't fail on precision.

## Decision for you

**D6: after results are reopened, should the WOM end update try again?**
- **Today:** Publish permanently stops the end update.
  - If the event was published with "WOM end could not be updated" and an admin later reopens the results to fix something, the update stays stopped.
  - The republished version uses the same pre-end WOM data again, even if WOM has been working for days.
  - This follows your WA-2 rule ("retries until results are published").
- **(a) Recommended: keep it.** Reopening is for correcting results, and it then changes nothing about WOM. Each published version's WOM basis stays predictable, and corrections never silently change WOM numbers.
- **(b) Restart the update on reopen.** A republish could then use a correct final fetch if WOM has recovered. But a correction would also change the WOM numbers, which nobody asked for, and it adds complexity.
