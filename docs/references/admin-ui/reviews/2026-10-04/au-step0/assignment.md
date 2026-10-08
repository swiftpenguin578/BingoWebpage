# Codex brief: Step 0, then AU lanes B1 and B2 in parallel

Status: **authorized when the user sends this brief, 4 October 2026.** Limited to Step 0, B1 and B2 below. B3–B5, UI integration, RC tickets, rehearsal tooling, push, merge to `main` and deployment remain stopped.

## 1. Authority and baseline
- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`. Start from `5cf9081b458a573baa0c32fe423f88375f49534d` with a clean tree. If the branch has moved, stop and report.
- Plan and decisions: `/Users/christopher/Documents/BingoWebpage/review-notes/17-au-plan.md` (approved) and `08-decisions.md` (sections "Approval records", "AU phase plan", the AU23 and drop-rate sections, and "Invariant: never two current events"). Ticket scope: `DELIVERY_PLAN.md` AU sections and "Approved AU scope additions". Where a ticket and `08-decisions.md` differ, `08-decisions.md` wins; report the difference.
- **Approval rule (user, 4 October):** record an approval as the user's only when it quotes or links the user's own message. Planner-made approvals are labelled as the planner's. Don't infer user approval from an automatic approval review or from silence.
- Follow AGENTS.md (working-tree safety, bounded execution, verification, handoff).

## 2. Step 0: documentation-only, before the lanes split
Done by the planner (`/root`) or the B2 implementer on `codex/participants-functionality`, as one commit. Report its SHA before B1 or B2 code work starts.
1. **AGENTS.md, user-approved amendment** (`08-decisions.md` "AU phase plan"): change "Run tickets sequentially" to allow parallel lanes **only** when the planner's approved plan names them, their file ownership is disjoint, and at most one lane adds database migrations. Each lane uses its own worktree and branch and its own implementer chat, and is merged back into the feature branch only after its independent review passes. Change nothing else in AGENTS.md.
2. **Approval-record corrections** (`16-cleanup-recheck.md`, user answers): H4-2 is approved by the user on 4 October **after Claude's review**, recorded in the planner decisions. Re-attribute the text in `docs/PRODUCTION_RUNBOOK.md`, `CURRENT_STATUS.md`, `DELIVERY_PLAN.md` and `remediation-handoff.md`. The extra H3-4 commit `6b8331d` was **not** approved by the user: say so in `remediation-handoff.md` and `CURRENT_STATUS.md` (the correction itself stands). Don't rewrite git history.
3. **Low notes:**
   - N5: `DELIVERY_PLAN.md` CAT-01 line (~`:1587`) must match the decided layout, where the Super Admin edits the roll group in the rate panel.
   - N4: `DELIVERY_PLAN.md` (~`:529-530`) must treat BR-11's 3 October "0" as to-be-re-run, not as evidence.
   - N3: record that the AU23 pre-check (stop if any drop has "only after" set) is user-approved (`08-decisions.md`).
4. **Ticket scope from Decision 1** (`08-decisions.md`): AU17 narrowed to the Review "Contribution" line plus readback; AU18 narrowed to the WOM refresh outcome and version/reopen history, with no current-event readiness row; AU19 approved. Mark them approved in the registers. Add the "never two current events" invariant to `DATA_MODEL.md` next to the current-event rule.

## 3. Lane B1: small fixes (implementer `gpt-5.6-luna` / `max`)
- **Worktree:** create a new worktree and branch from the Step 0 commit (e.g. `codex/au-b1-small-fixes` under `~/.codex/worktrees/`). Work only there.
- **Files B1 may change:** the WOM page fetch handler and markup, Audit page/query/presenter, Accounts pages and their projections/services, ownership transfer, their tests, translations and their ticket docs. **Not:** Board, scoring, finalization, catalogue, draft, lifecycle or WOM sync/management services. **No migrations.** If a ticket seems to need a migration or a file outside this list, stop and report.
- **Tickets, in order, one commit each:**
  1. **AU15**: replace the typed FETCH challenge with a normal Fetch action; reuse `RefreshAsync` unchanged; keep every guard; generic cooldown wording (D4).
  2. **AU16**: Audit shows hidden-event history under ordinary Audit permission; exact action/area filters; timezone-aware calendar-day dates; strict paging. Invert the two tests that lock in the old behaviour (`C11FinalizedRosterIntegrationTests.cs` ~`:647-649`, `EventQuarantineIntegrationTests.cs` ~`:452-454`).
  3. **AU22**: accurate Accounts projections and readback; the reset response stays transient and bound to its target (a late response can never show account A's secret on account B). Reuse F4's consume-time rule; no new reset policy.
  4. **AU24**: ownership transfer requires typing the destination's public username alongside the owner's password; server-side rejection of missing, wrong or mismatched text; never reuse the legacy `TransferOwnershipAsync(actorId, password, destinationUsername)` overload.
- **Proof:** each ticket's acceptance in `DELIVERY_PLAN.md`, with real PostgreSQL where persistence, authorization or concurrency is involved.

## 4. Lane B2: scoring (implementer `gpt-6-astra` / `high`)
- **Checkout:** stay on `codex/participants-functionality` in the existing worktree, continuing from the Step 0 commit.
- **Files B2 owns:** Board page model and editor data, tile/template models, `EhbCalculator`, `BoardEstimateService`, approval snapshots, contribution allocation, `PublicProgressCalculator` and its consumers, finalization/official snapshots, Final Review page, migrations. **Not** the B1 files.
- **Tickets, in order, one commit each:**
  1. **AU11**: optional tile-local manual total EHB override for drop/catalogue tiles, plus the existing manual estimate for non-drop tiles. Exactly as the AU11 section says: the effective value feeds board/line estimates, credited partial progress, player stats and the EHB ranking input. Catalogue rates, Luck/KC, evidence locks and immutable snapshots stay unchanged.
  2. **AU12**: placement order for **new events only**: full-board finish and time, then lines, tiles, **credited EHB**, then current-score time. Existing events keep the retained rule (finish → finish time → lines → tiles → score time → EHB, `PublicProgressCalculator.cs` ~`:201-207`) permanently, even without official results. Use an explicit, persisted rule boundary set at event creation; never infer it from dates, and never re-rank existing events. Provisional and final standings use the same rule per event.
  3. **AU18 (narrowed)**: show the final WOM refresh outcome on each published version (e.g. "skipped: refreshed less than 5 minutes earlier") and the version/reopen history (who reopened and when), as `FinalReview.dc.html` does. **No "another event is current" readiness row**; keep the existing publish/reopen refusal unchanged.
- **Proof:** AU11/AU12 acceptance in `DELIVERY_PLAN.md`, including an existing event that keeps its order while a new event uses the new one, provisional/final parity, ties and null times, and PostgreSQL microsecond precision at ranking boundaries. AU18: a published version shows a skipped, failed and successful refresh outcome; reopen history is correct after reopen and republish. Migrations: Up on a populated database plus a Down check; record any backfill.
- **Any product question** (e.g. the exact persisted rule-boundary values): stop that ticket, send a proposal to `/root`, and continue the next ticket if it doesn't depend on it.

## 5. Routing
- Planner: UI Planner chat `01a0ec9a-76e3-7252-9850-3f260c612e59` (`/root`).
- Two implementer chats: B1 on Luna/max, B2 on Astra/high. No orchestrator. Each makes its own local commits per ticket (the existing per-item authority). No self-review.
- Report to `/root` before each turn-ending response and immediately for a blocker or decision, with lane, ticket, commit and next step. No `wait_threads` or polling.

## 6. Verification, review and merge
- Focused checks per ticket; build clean; `git diff --check`. Durable evidence under `docs/references/admin-ui/reviews/2026-10-04/au-b1/` and `…/au-b2/`.
- **Each lane stops when its tickets are done and is reviewed by Claude separately** (a commit-by-commit independent review, with a remediation round if needed). Don't wait for the other lane.
- **Merging B1 back:** only after Claude's B1 review passes, merge `codex/au-b1-small-fixes` into `codex/participants-functionality` locally (no push), resolve any conflict without changing either lane's reviewed behaviour, re-run the B1 focused checks on the merged result, and report the merge SHA. The user authorized this merge-back as part of the parallel-lane plan (`08-decisions.md` "AU phase plan"). Nothing else is merged or pushed.
- Stop after B1 and B2. Don't start B3.
