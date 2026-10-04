# Codex brief: B3 (AU20) remediation

Status: **authorized when the user sends this brief, 4 October 2026.**
- **Authorized:** the items below.
- **Still stopped:** B4–B5, UI integration, RC tickets, rehearsal tooling, push, merge to `main` and deployment.

## 1. Authority
- **Review:** `/Users/christopher/Documents/BingoWebpage/review-notes/24-b3-au20-review.md`, with agent reports 24a–24f. Each item below names its finding; read it before starting.
- **Decisions:** `08-decisions.md` "WA-2 (decided, extends AU20)", "B3 (AU20) brief decisions" and "B3 (AU20) review decisions" (D6: publication stops the end update for good, also after a reopen; no change needed).
- **Starting point:** `codex/participants-functionality` at `4b9f156` with a clean tree. If it has moved, stop and report.
- **Lane:** one implementer, `gpt-6-astra` / `high`. One local commit per item, no self-review, no push.
- **File ownership:** as in brief 23 §4.
- **Display rule:** backend only. No new display on current pages; existing message texts may change.

## 2. Items, in order
1. **F1: temporary WOM failures are retried, not rejected** (24c F1).
   - **Today:** for an end update, a server error, timeout or network error becomes "outcome unknown". The service only reads the competition back, sees the old end, records drift/Conflict and rejects the end update for good. A crash during an attempt ends the same way.
   - **Required:**
     - For end updates, "unknown" followed by a read that shows the old end means **not applied**. Resend on the agreed schedule (1, 2, 4, 8, 16 minutes, then every 30) until success, a permanent rejection, or publication.
     - A read that already shows the target end completes the update.
     - **Temporary:** 5xx, timeouts, network errors, 408 and 429.
     - **Permanent:** the named 400, a missing or invalid code, 401/403/404 and other validation rejections.
     - Keep one attempt at a time.
   - **Proof:**
     - An HTTP-level fake through the real `WiseOldManCompetitionManagementClient`: 502, then a timeout, then 200. The update succeeds, with retries spaced as agreed (controllable clock).
     - A crash mid-attempt (claim expiry) leads to a resend, not a rejection.
2. **F2: write receipts compare only what the response carries** (24c F2).
   - **Today:** `Matches` (`EventCompetitionManagementService.cs:1404-1411`) compares participants, but the real client's success response always has an empty participant list (client `:277`). Every roster update therefore becomes "unknown".
   - **Required:** compare title and window from the write response. Compare participants only if the response actually contains them (or parse them correctly).
   - **Proof:** a successful roster update through the real client's response parsing completes directly.
3. **F3: a Conflict doesn't block replacing an external link** (24e M1).
   - **Today:** `EventCompetitionSynchronizationService.cs:171` refuses replacement and disconnection when the last operation is Conflict. Nothing resets Conflict.
   - **Required:**
     - Approved option 1 blocks only while an operation is queued, sending or unknown. Replacing the link resolves the Conflict.
     - Disconnection keeps its existing rule: only before first Live.
     - Fix the misleading "managed competition controls" message for external links.
   - **Proof:** a competition deleted on WOM during Live gives a Conflict; linking the replacement (exact window) then succeeds; the old code is retired.
4. **F4: early end and Resume never show an error page on a database conflict** (24b M1).
   - **Required:** catch PostgreSQL 40001/40P01 in `EndNowAsync` and `ResumePrematureEndAsync`, and retry the transaction internally a few times. The early end keeps the **original click time**. If it still fails, return a normal "try again" message, as Start does. Take the locks in the same order as the refresh worker (event row, then sync row).
   - **Proof:** a forced serialization conflict leads to a successful early end with the original actual end time.
5. **F5: an outdated target never affects the current target** (24c F3, F4).
   - **Required:**
     - A late failure for an old end target, arriving after Resume, doesn't block or reject the new target.
     - The attempt count and backoff restart for each new target.
     - HTTP 408 is temporary (also covered by item 1).
   - **Proof:** early end, Resume, then the old target's late permanent failure: the new target is still attempted 1 minute later.
6. **Documentation, register, tests and small fixes** (24 "Smaller fixes"):
   - **Design-reference gaps register:**
     - Add the Resume row: `Overview.dc.html` asks for a new end only when the stored end has passed, but AU20 always requires a future replacement end. Add an RC entry.
     - Complete the "WOM end could not be updated" row with:
       - all end-update states;
       - the target and request times;
       - the safe rejection code;
       - the new fields on Final Review readiness and the publish result.
   - **Stale "pending AU20" markers:** remove them at `DELIVERY_PLAN.md:1086`, `:1680` and `:1683`, `FUNCTIONAL_CONTRACTS.md:164` and `FUNCTIONALITY_CHANGES.md:130`.
   - **DATA_MODEL:** the end-update status names are stored data and must never be renamed.
   - **Record the error classification** from item 1 in DELIVERY_PLAN/TECHNICAL_ARCHITECTURE, as a planner technical decision.
   - **Record D6** as the user's decision, linking `08-decisions.md`.
   - **CURRENT_STATUS:** label test results as worker-reported.
   - **Publish feedback and history text:** a final fetch skipped because the end couldn't be updated gets a clear text, not "skipped: no detail".
   - **Tests:**
     - a sub-5-minute mismatch rejected at the fetch site;
     - scheduled and manual fetches resuming after a successful update;
     - a pending end update reset on replacement, in a test that actually sets one;
     - the external-delete guard at dispatch;
     - replacement refused in Final Review;
     - an early end at 22:00:00.0004 → 22:01.
   - **Stats test:** fix the setup of `StatsPass4BoundaryIntegrationTests.cs:125`, which has failed since `444bfc4`, not because of AU20. Test files only.
   - **Runbook, before the R-3 rehearsal and deploy:** count events in Final Review (`AwaitingFinalReview`). Expected 0. If any exist, stop for a decision, because events that ended early under the old rules behave differently under AU20.

## 3. Verification and stop
- **Per item:** focused checks; Release build clean; `git diff --check`. Evidence under `docs/references/admin-ui/reviews/2026-10-04/au-b3/remediation/`, with the commands and results.
- **Then:** run the whole AU20 test set plus the Stats test, and record the results as worker-reported.
- **Stop** after item 6 and report to `/root` with SHAs. Claude rechecks, with one agent for items 1, 2 and 5 together, one for items 3 and 4, and a direct check of item 6.
- Don't start B4.

## 4. Routing
- **Planner:** UI Planner chat `01a0ec9a-76e3-7252-9850-3f260c612e59` (`/root`).
- **Reporting:** report before each turn-ending response, and immediately for a blocker or product question.
