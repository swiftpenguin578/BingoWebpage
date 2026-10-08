# Supplied decision excerpts

Verbatim sections from [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md), supplied by the user; captured 4 October 2026.

- **WA-2 (decided, extends AU20):**
  1. The WOM check compares the competition with the website's **configured** start and end, exactly, at every stage, final review included. Actual times are not used for this check; they still drive the upload cutoff, eligibility and the review window.
  2. An early end or Resume always succeeds locally. It is never blocked on WOM.
  3. **Early end:** the WOM end is set to the click time rounded **up** to the next whole minute (21:59:55 → 22:00:00). The same value is stored as the event's configured end; the actual end keeps the precise click time. No lead time is needed.
     **Resume** (corrected 3 October; the earlier wording wrongly applied the click-time rule to Resume): Resume requires the admin to choose a replacement end in the future. The WOM end is set to that **replacement end**, which already passes the schedule's validation and increments. It is not set to the resume click time, and no click-time rounding applies. The replacement end is stored as the configured end, as the existing Resume does. The retry and fallback rules in steps 4–5 apply to this update too. Revised after Codex's WOM source check: WOM accepts editing an existing competition's end into the past (not before its start) and recalculates the end values from the last snapshots inside the revised window. The past-date rejection applies only to creation. Source: WOM `EditCompetitionService.ts` / `CreateCompetitionService.ts`, read from public source, not a live call.
  4. Temporary failures are retried with spaced backoff, never a fast loop that spams WOM's API. Because a past end is accepted, retries may continue until results are published.
  5. While the WOM end is unmatched, no fetch runs after the actual end, because it would count post-end gains. Once the update succeeds, normal fetches resume and give correct end-window data. If the update has still not succeeded at publication (WOM down for a long time, missing or invalid verification code, or another non-transient rejection), the event is marked "WOM end could not be updated", the final fetch is skipped, and the last fetch before the actual end is the official WOM data. Luck's freshness timestamp shows its age.
  6. External ID-only competitions can't be edited without a verification code, so they always take the step 5 fallback after an early end.
  7. AU20 must also list Resume as a third place where the 5-minute check lives. An end before the start returns HTTP 400 `COMPETITION_START_DATE_AFTER_END_DATE`; handle it as a non-transient rejection.
  - Known limit: WOM's end values come from the last player snapshot inside the window, so precision depends on how recently players were updated before the end.
  - The snapshot limit is known and accepted: players know to log out right before an event ends if they want their data fully updated.

## B3 (AU20) brief decisions (user, 4 October)
- **"WOM end could not be updated" display: backend only (option b).** The state is persisted and exposed in the service/read models, but not shown on the current pages. Its placement in the new UI is decided in the UI integration plan.
- **General rule (user):** all AU work targets the new UI. Nothing is merged to `main` before the new UI is implemented, so tickets add no new display to the current (old) pages. Current pages only need to keep working where their existing handlers are touched.
- **Retry spacing for the WOM end update (planner technical choice, explained to the user):** the first attempt at once, then retries after 1, 2, 4, 8 and 16 minutes, then every 30 minutes until official results are published. A permanent rejection stops the retries. Matches WA-2 item 4.

## B3 (AU20) review decisions (user, 4 October)
- **D6 (option a):** publication stops the WOM end update for good, including after a reopen. A reopened and republished version uses the same WOM basis as before, so corrections never change WOM numbers. No retry restarts on reopen.
