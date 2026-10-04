Source: forwarded user-authorized `review-notes/21-codex-brief-b1-b2-remediation.md`; copied verbatim below. Decisions attributed to the recorded Claude-chat decisions, not fabricated direct quotes.

# Codex brief: B1 and B2 remediation, then B1 merge-back

Status: **authorized when the user sends this brief, 4 October 2026.**
- **Authorized:** the remediation items below, Claude's recheck, then the B1 merge-back.
- **Still stopped:** B3–B5, UI integration, RC tickets, rehearsal tooling, push, merge to `main` and deployment.

## 1. Authority
- **Reviews:**
  - `/Users/christopher/Documents/BingoWebpage/review-notes/19-b2-review.md`, with agent reports 19a–19d.
  - `/Users/christopher/Documents/BingoWebpage/review-notes/20-b1-review.md`, with agent reports 20a–20b.
  - Each item below names its finding; read that finding before starting.
- **Decisions:** `08-decisions.md` "B1/B2 lane review decisions (user, 4 October)" (D1–D5). These are the user's decisions, given in the Claude planner chat; record them as such, linking that section.
- **Rules:** Brief 18's lane rules still apply:
  - B1 and B2 run in parallel, each in its own worktree.
  - File ownership as in brief 18.
  - Only B2 adds a migration.
  - One local commit per item, no self-review, no push.
- **Translations:** `SharedResource*.resx` belongs to **B1**. If a B2 item needs a translation, stop and report to `/root`. Don't take the file over.
- **Starting points:**
  - B2: `codex/participants-functionality` at `f932893`.
  - B1: `codex/au-b1-small-fixes` at `b38749d`.
  - If either branch has moved, stop and report.

## 2. Lane B2 remediation (`gpt-6-astra` / `high`, existing worktree)
1. **AU11 A1: no override without an explicit choice.**
   - **Today:** the custom-challenge estimate field is always submitted (`Board.cshtml:631`), and the server no longer refuses an estimate on drop tiles. Switching a challenge tile to drops therefore saves its estimate as a hidden override.
   - **Required:**
     - An override on a drop tile is only ever set by an explicit admin choice.
     - Changing the objective kind never carries a value over.
     - Saving a drop tile from the current Board page neither sets nor clears an override. The page has no override control yet; BR-10 adds it.
   - **Proof:** an HTTP test for challenge → drops (override stays null), and one for saving a drop tile that already has an override (value unchanged).
2. **AU11 A3: discarding a correction restores exactly what was published.**
   - **Today:** `Board.cshtml.cs:754` compares full-precision recalculated EHB with the 4-decimal snapshot value, so almost every drop tile gets an override.
   - **Required:** after discard, a tile has an override only if it had one when published. Compare at the stored precision, or record override presence explicitly.
   - **Proof:** a PostgreSQL test where the drop tile's calculated EHB has more than 4 decimals.
3. **AU11 A2 / D1: clear old drop-tile overrides.**
   - **What:** a migration that sets `manual_ehb_override` to null on every `tile_templates` row with `objective_type = 'DropRequirements'`.
   - **Context:** production has 1 such row (25.0000).
   - **Don't touch:** approval/publication snapshots, board tile EHB snapshots or official results.
   - **Proof:**
     - Up on a populated database, with the cleared count recorded.
     - A Down check. Down cannot restore the values; document that.
     - The runbook/DELIVERY_PLAN gets a pre-deploy line: re-run the count and record it.
   - **Also:** remove the test assertion that applies the stale 99 (AU11 review "Test change").
4. **AU11 A4: decimal limit independent of language.**
   - **Required:** `ParseLimitsInInvariantCulture = true` on the new `ManualEhb` range.
   - **Proof:** a Danish-culture post of 0.5 is accepted.
5. **AU12 B1 / D3: equal-looking EHB counts as equal.**
   - **Required:**
     - For `PlacementRule.CreditedEhbThenScoreTime` only, compare credited EHB rounded to 4 decimals in both the ordering and `SameRank` (`PublicProgressCalculator.cs:202-229`).
     - The legacy rule is unchanged.
     - Public standings and finalization stay identical.
   - **Proof:**
     - Team A with 1/3 of two 10-EHB tiles and team B with 2/3 of one 10-EHB tile, both new rule: the earlier score time wins.
     - The same case under the legacy rule keeps today's behaviour.
6. **AU12 B2: no silent default rule.**
   - **Required:** make the placement rule a required argument of the basic `BingoEvent` constructor; no default to legacy.
   - **Docs:** fix DATA_MODEL §13.1 ("not the deployed comparator") and the §13.2 wording about imports.
7. **AU18 C1: store names, not numbers.**
   - **Required:** the final WOM refresh status and skip reason in `CalculationInputsJson` are stored as names (`EventFinalizationService.cs:185`, `:282-298`).
   - **Reading:** accept the numeric form too only if any test or local data already uses it. Production has none.
   - **Proof:** a test where a stored version reads back the correct reason after the enum is reordered in a test double, or an equivalent check.
8. **Step 0 documentation (19d):**
   - Correct `cleanup-remediation/remediation-handoff.md:85`, `:108`, `:146-147` and `:154-162`.
     - The H3-4 follow-up `6b8331d` is not user-approved.
     - H4-2 was approved by the user on 4 October after Claude's review.
     - Commit `5cf9081` was never approved after the fact.
     - Claude's recheck is done (`16-cleanup-recheck.md`).
   - Correct `docs/PRODUCTION_RUNBOOK.md:366` the same way.
   - Set RC05/RC07/RC08 in `FUNCTIONALITY_CHANGES.md:85-86` back to "proposed", matching `DELIVERY_PLAN.md:381`.
   - Record D2 (the user approved the AU18 sync change after review) where the planner resolution is recorded.

## 3. Lane B1 remediation (`gpt-5.6-luna` / `max`, B1 worktree)
1. **AU24 F1: a missing confirmation is a mismatch.**
   - **Today:** `AccountAdministrationService.cs:56` checks only when a name is given. The overloads at `:37` and `:44` pass null.
   - **Required:**
     - Null or empty confirmation is rejected.
     - Remove the two old overloads or make them non-public, including the legacy `TransferOwnershipAsync(actorId, password, destinationUsername)`.
     - Move their tests to the confirmed method.
   - **Proof:**
     - Rejection for each of: missing name, another existing account's name, wrong name.
     - Different capitals are accepted.
     - One HTTP rejection with the database reloaded and unchanged.
2. **AU16 D4: the two missing parts.**
   - **Single-entry read:** open one audit entry by ID under ordinary Audit permission. A missing, unauthorized or redacted-away entry returns a clear "entry unavailable" result, never an error page. Secrets stay redacted, and hidden-event entries are readable as in the list.
   - **Event filter choices:** an authorized list of events, hidden ones included, to choose from instead of typing an event ID. The page can keep its current layout; the new design is bound in UI integration.
   - **Also:**
     - `?To=9999-12-31` and other out-of-range dates give a normal validation message, not a 500.
     - Add tests for the autumn clock-change day, a non-admin being refused, and a hidden event's row shown with secrets redacted.
3. **AU15 test setup.**
   - **Today:** the seeder rounds its clock to the half hour but the fake competition uses the unrounded clock, so the PostgreSQL test fails more than 5 minutes past :00/:30 (20a).
   - **Required:** align them so the test passes at any time and its non-Live refusal check runs. Test files only.
4. **AU22 low notes and D5.**
   - When a link is discarded for the wrong account, discard its success message too.
   - Correct the evidence: per D5, the link is carried for one redirect in the encrypted TempData cookie; this is accepted, not "never in persistent client state".
5. **Browser test.**
   - Re-run `account-support.browser.js` in a working Chromium.
   - If that's impossible here, record exactly which assertions stay unverified (20b, AU24 section). Don't claim them.

## 4. Verification, recheck and merge-back
- **Per item:**
  - Focused checks; Release build clean; `git diff --check`.
  - Evidence under `docs/references/admin-ui/reviews/2026-10-04/au-b1/remediation/` and `…/au-b2/remediation/`.
- **When a lane is done:** it stops and reports to `/root` with commit SHAs. Claude rechecks each lane separately; don't wait for the other lane.
- **B1 merge-back, only after Claude's B1 recheck passes:**
  - Merge `codex/au-b1-small-fixes` into `codex/participants-functionality` locally.
  - Resolve any conflict without changing either lane's reviewed behaviour.
  - Re-run B1's focused checks on the merged result, including the fixed AU15 test.
  - Report the merge SHA.
- **If B2's recheck isn't finished yet,** the merge may still happen. B2's remediation then continues on the merged branch, and Claude's B2 recheck covers the merge.
- **Nothing is pushed.** Stop after both rechecks; don't start B3.

## 5. Routing
- **Planner:** UI Planner chat `01a0ec9a-76e3-7252-9850-3f260c612e59` (`/root`).
- **Implementers:** B1 on Luna/max, B2 on Astra/high, the same chats as before if available.
- **Reporting:** report before each turn-ending response, and immediately for a blocker or product question. No polling.
