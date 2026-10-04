# Recheck of the B3 (AU20) remediation (brief 25)

Range `4b9f156..45a09ad`. Rechecked 4 October 2026 by Claude (planner), read-only.

**How this recheck was done**
- Two agents:
  - [26a](26a-b3-remediation-items-1-2-5.md): items 1, 2 and 5
  - [26b](26b-b3-remediation-items-3-4.md): items 3 and 4
- Claude checked item 6 directly and verified both findings below in the code.
- Nothing was built or run. Test counts are the worker's.

## Verdict: two small fixes, then PASS

| Item | Verdict |
| --- | --- |
| 1 Temporary failures retried | PASS with one fix (R1) |
| 2 Write receipts | PASS |
| 3 Conflict replacement | PASS |
| 4 Lifecycle conflicts | PASS with one fix (R2) |
| 5 Outdated targets | PASS with notes |
| 6 Docs, register, tests, Stats, runbook gate | PASS (checked directly) |

**What now works:**
- **Item 1:** WOM outages, timeouts, network errors, 408 and 429 lead to resends on the agreed schedule. This is proven through the real WOM client with a fake HTTP layer: 502, then a timeout, then 200, with the agreed spacing. A crash also leads to a resend.
- **Item 2:** write replies are checked on title and window only, while read-backs still check participants.
- **Item 3:** an external link in Conflict can be replaced. Website-created competitions keep their old rules.
- **Item 4:** the click time is captured once, there are 3 attempts with fresh state, the lock order is consistent, there are no duplicate side effects, and a real conflict with another admin is still reported.
- **Item 5:** a late failure for an old end no longer affects the new end, and the attempt count restarts.
- **Item 6:**
  - the Resume register row and its RC01 entry;
  - the complete "could not be updated" row;
  - the stale texts fixed;
  - the stored names marked never to be renamed;
  - the error classification recorded as a planner decision;
  - D6 recorded as the user's;
  - the runbook "events in Final Review" count gate;
  - the clear skip message;
  - the legitimate Stats test fix.

## Fixes

**R1 (Medium, verified): a late WOM success turns into a permanent rejection.** (item 1)
- **Today:** before every resend, `CheckManagedSourceAsync` (`EventCompetitionManagementService.cs:926-942`) accepts only WOM showing the *last applied* state. Anything else is "ExternalDrift", which leads to Conflict and a rejected end update.
- **Example:**
  1. The first update times out.
  2. The read straight after still shows the old end.
  3. WOM then applies the update a moment later.
  4. On the resend, the check sees the new end and permanently rejects the update, although WOM is correct.

  Timeouts where WOM did apply the change are common, so this case is realistic.
- **Required:** if the check before the write finds WOM already matching the target (title, window, and the roster when the update includes one), complete the update without writing.
- **Proof:** the timeout + late-apply case through the real client.

**R2 (Medium, inferred): a conflict at commit can still give an error page.** (item 4, and Start)
- **Today:**
  - On a conflict, the catch does `await tx.RollbackAsync(ct); throw;` (`EventLifecycleService.cs:157`, `:219`; Start `:111`).
  - If the conflict is raised at COMMIT, the transaction is already completed, and Npgsql most likely throws "This NpgsqlTransaction has completed" from the rollback.
  - That error replaces the conflict, so the retry loop doesn't recognise it. Under serializable isolation, the commit is a common place for this conflict.
- **Required:** make the rollback tolerant (ignore a failed rollback) in all three places.
- **Proof:** a test that forces 40001 at commit. If that test shows the rollback doesn't throw, record it and the finding closes.

## Notes (no action)
- After Resume, if the old target's operation is still "unknown", the new target can wait up to 30 minutes for that operation's reconciliation (26a R3).
- A WOM read that returns 400/403 or an unreadable answer still ends in Conflict. This existed before, and replacing the link is now the way out (26a R2).
- A 408 on a website Create now leaves it "unknown" (the known dead end from 24e L2) instead of a clean failure. It never causes duplicate creates (26a R4).
- The proof tests for items 3 and 5 pass, but each misses one assertion: the final status, and the 1-minute timing (26a, 26b).

## Follow-up (to send to Codex)
Same lane (Astra/high), from `45a09ad`, two commits:
1. R1, with the late-apply test.
2. R2 in `EndNowAsync`, `ResumePrematureEndAsync` and `StartNowAsync`, with a forced commit-conflict test.

Then stop. Claude rechecks directly, without agents. After that, B3 is accepted and B4 can be briefed.
