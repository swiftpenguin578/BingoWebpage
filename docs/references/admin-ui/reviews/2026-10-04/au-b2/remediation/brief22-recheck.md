# Recheck of the B1 and B2 remediation (brief 21)

Reviewed 4 October 2026 by Claude (planner), read-only, with three agents:
- [22a](22a-b1-remediation-recheck.md): B1
- [22b](22b-b2-au11-remediation-recheck.md): B2 AU11
- [22c](22c-b2-au12-au18-docs-recheck.md): B2 AU12, AU18 and docs

Claude traced the findings marked "verified". Nothing was built or run.

## Verdict
- **B1 (`b38749d..ea60b64`): FAIL on one item.** All five remediation fixes pass. The browser test that failed (line 62) exposed a real bug that the earlier AU22 commit `b44fa6e` introduced (below). It must be fixed before merge-back.
- **B2 (`f932893..52be2b6`): PASS, with one small follow-up** (rounding mode, below). The follow-up can be checked by Claude alone, without agents.
- **Merging B1 and B2:** `git merge-tree` reports no conflicts. B2's new required placement-rule argument doesn't break B1's files (inferred, not built).

## B1

**R1 (High, verified): every confirmed account action looks failed, although it was saved.**
- **Where:** on Admin → Accounts → Manage, actions that ask for confirmation, such as Disable, Restore and Grant admin.
- **Why:** the shared confirmation window closes the account editor behind it while it is open (`admin-confirmation.js:97`). AU22 added two checks that throw away the server's answer whenever the editor isn't open (`account-manage-dialog.js:305`, `:325`). The same function already expects the editor to be closed during a confirmation (`:293`).
- **What the admin sees:**
  - The server applies the change.
  - The confirmation window says "Could not complete the account action".
  - After cancelling, the editor still shows the old values.
  - If the admin retries, the second attempt is refused as out of date.
- **How it was caught:** this is exactly the browser test's failure at line 62 (expected version 8, saw 7).
- **Required:** the late-answer check must ignore an answer only when the admin has actually moved away (a newer request, or the editor closed by the admin). The editor being temporarily closed by the confirmation window must not count. Re-run `account-support.browser.js`. After the fix it should reach the four AU24 checks; if they still can't run, record them as unverified.

**Passed (22a):**
- **AU24:** both old transfer methods are deleted, and a missing or blank name is refused before the password check (`AccountAdministrationService.cs:46`, verified).
- **AU16:**
  - opening a single entry, with "This entry isn't available";
  - the event dropdown, with hidden events marked;
  - a normal message for out-of-range dates;
  - new tests for the autumn clock change, a non-admin being refused and redaction.
- **AU15:** the test setup now passes at any time of day, and its non-Live refusal check runs.
- **AU22:** the leftover success message is removed, and the cookie wording is corrected (D5).

**Low (fix alongside R1, they're cheap):**
- A malformed `?From=abc` or `?entry=abc` shows an empty page with no message. It should show the normal validation or "entry unavailable" message.
- Test gaps:
  - the AU24 HTTP test is stopped by the page's required-field rule, so it never reaches the server-side check;
  - no test shows that the right account keeps its reset message;
  - the autumn test doesn't check just outside the day edges.

## B2

**Passed (22b, 22c):**
- **AU11, A1:** the override changes only with an explicit "change override" flag, which the current page never sends. Switching challenge → drops always clears it. Saving a drop tile keeps its existing override. The tests are real HTTP posts against PostgreSQL.
- **AU11, A3:** discard compares at 4 decimals using the rates frozen at publication, so later catalogue changes create nothing.
- **AU11, A2/D1:** the migration clears only `tile_templates.manual_ehb_override` on drop tiles. Snapshots and results are untouched (tested). Down is documented as non-restoring. The runbook has the pre-deploy count query. Before AU11 these values were ignored everywhere, so clearing them restores exactly what admins saw (checked at `3ce941b`).
- **AU11, A4:** the range limit is fixed. A dedicated input reader for this one field also fixes MVC reading a Danish "0.5" as 5; Codex's first test failed exactly that way. It's limited to this field.
  - **Approval:** a planner technical decision, not a product change. Claude accepts it.
- **AU12:**
  - equal-to-4-decimals EHB ties for new events (D3), with the legacy rule unchanged;
  - the placement rule is now a required argument everywhere it matters;
  - all 125 changed test lines only add that argument.
- **AU18:** outcome and reason are stored by name, using private serializer options, so nothing else changes. The test reads the names back into reordered enums.
- **Step 0 docs:** all approval texts corrected, and D2 recorded.

**R2 (Low, verified): rounding mode at the half.**
- **Today:** the four places that compare EHB with a stored 4-decimal value use .NET's default rounding, which sends a half to the even digit. PostgreSQL's `numeric(…,4)` rounds a half away from zero. So a value like 1.00005 is compared as 1.0000, but stored as 1.0001.
- **The four places:**
  - `PublicProgressCalculator.cs:232`: ranking. Added in remediation.
  - `Board.cshtml.cs:756`: discard correction. Added in remediation.
  - `Board.cshtml.cs:440` and `:1194`: published-scoring protection. These existed before B2.
- **Effect:** only at exact midpoints.
  - At `:756` the tile gets a hidden override on discard.
  - At `:232` the ranking and the stored official EHB can disagree.
  - At `:440` and `:1194` an unchanged tile may be treated as changed.
- **Required:** use `MidpointRounding.AwayFromZero` at all four places, with one midpoint test each for ranking and discard.

**Notes for later:**
- **For B3 (AU20):**
  - The refresh outcome enum names and numbers are now stored data. Don't rename, remove or renumber them; add new ones at the end with an explicit number.
  - AU20 builds on the `SkipReason` / `LeaseDecision` mechanism, as noted in 19.
- **Legacy defaults still left:**
  - `Rank(...)` still defaults to the legacy rule;
  - the 13-argument compatibility constructor gives legacy without naming it.
  - Both production callers are explicit. Worth tightening whenever that code is touched next.
- **Migration count:** the migration reports its cleared count with `RAISE NOTICE`, which production logging may not show. Record the count after migration (expected 0) next to the pre-deploy count.
- **DELIVERY_PLAN:** `DELIVERY_PLAN.md:1636-1642` still says "RC05/RC07/RC08 pending", meaning not delivered. Harmless.

## Follow-up brief (to send to Codex)
**B1** (Luna, B1 worktree, one commit each):
1. Fix R1. Re-run `account-support.browser.js` and record the result honestly.
2. Show a message for a malformed `From` or `entry`. Add the three small tests listed under "Low".

**B2** (Astra, existing worktree):
3. Fix R2 at the four places, with midpoint tests.

**Then:**
- Each lane stops and reports. Claude rechecks both directly.
- After the B1 recheck passes, merge B1 into `codex/participants-functionality` locally and re-run B1's checks and `account-support.browser.js` on the merged result.
- No push, no B3.
