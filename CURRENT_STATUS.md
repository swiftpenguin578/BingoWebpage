# Current project status

## Active assignment — B3 / AU20 review26 follow-up, 4 October 2026

- Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`.
- Clean baseline verified `b126c556ccd2e16cb59829837581646a3179847e`; planner's authorized documentation prerequisite `2649008` follows it.
- Planner `/root`, replacement chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`. Sole implementer `/root/au_b3_implementer`, `gpt-6-astra` / high; no orchestrator or extra workers.
- User supplied assignment: [B3 brief](docs/references/admin-ui/reviews/2026-10-04/au-b3/supplied-brief.md). Section 6's old planner chat is retired by the current assignment.
- Original B3 six commits `52f0af6` through `4b9f156` were source-reviewed by Claude in review24: FAIL. No reviewer build or execution; historical implementation evidence remains in `au-b3/`.
- User authorized [remediation brief25](docs/references/admin-ui/reviews/2026-10-04/au-b3/remediation/supplied-brief.md). Clean baseline `4b9f15657978728160a00960bd45bf41cb04abb3` reverified; six ordered new local commits, no extra workers.
- Remediation checkpoints: `1a47b5e` temporary failures; `f441134` write receipts; `7be4ba2` external Conflict; `a980609` lifecycle database retries; `b2c3e1c` target isolation; final item6 documentation/tests checkpoint containing this status.
- Implementer reports (not independently rerun): final whole-AU20 plus authorized Stats test passed 63/63; stored enum/name checks 2/2; Release solution build zero warnings/errors; diff checks clean. Per-item focused counts/commands are in `docs/references/admin-ui/reviews/2026-10-04/au-b3/remediation/item1-temporary-failures.md` through `item6-final-handoff.md`.
- Temporary HTTP502/timeout/network/408 retries, real PostgreSQL claim expiry and forced40001 lifecycle recovery/exhaustion, stale-target rejection/backoff, all six named proof gaps and publication wording executed. No new schema; prior complete migration Up/Down/backfill evidence reused.
- The earlier StatsPass4Boundary fixture failure is fixed under brief25's explicit test-only authorization and passed in the final run. No Stats production edits or broad-suite pass claimed.
- Corrected error classification is planner technical resolution, superseding the earlier item3 resolution. D6 is the user's decision: first publication permanently stops end updates, including after reopen, retaining the same WOM basis; verbatim source in `remediation/supplied-decisions.md`.
- Claude source-only [review26](docs/references/admin-ui/reviews/2026-10-04/au-b3/remediation/26-b3-remediation-recheck.md) passed remediation items2/3/5/6; items1/4 require R1/R2. No reviewer execution or manual acceptance. User authorized exactly two follow-up commits from reverified clean `45a09ad0b2aa982c8603cd116a6e5d9bfbfa89c6`.
- Follow-up R1 recognizes an already-applied target before resending, retaining roster checks and existing receipt fences; worker-reported checks in `remediation/followup1-late-apply.md`. R2 tolerant rollback/commit-conflict proof is next. No new current-page display or Luck production change.
- Bindings register has the complete end-state/read-model row, structured-outcome row and Resume row; RC01/OS-1 correction queued. UI placement/copy decisions remain for integration.
- Runbook requires an authorized Final Review (`AwaitingFinalReview`) count before R-3 and deploy: expected0, otherwise stop for decision. Count/rehearsal/deployment not executed.

## Retained B1/B2 evidence and limits

- B1/B2 merge `4065cb0`, evidence `40e624a`; Claude merge scope PASS per supplied B3 assignment. Prior checks are reused, not rerun or independently reviewed here.
- B1 follow-ups `404524d` / `a9399b2` and B2 `5545845` passed Claude source recheck. Execution evidence remains workers'.
- Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b1/remediation/` and `au-b2/remediation/`; merge evidence `au-b1/remediation/merge-back.md`.
- Approval attribution: `docs/references/admin-ui/reviews/2026-10-04/au-step0/approval-record.md`, supplied `assignment.md` and `decisions.md` beside it.
- Snapshot override equal to automatic remains indistinguishable; cleanup Down cannot restore cleared values. Operator pre/post migration counts remain due. No production access assigned.

## Prior outcomes / corrected approvals

- F1–F9/R2 and G1–G6 passed Claude source review; historical evidence retained.
- H cleanup accepted by Claude source recheck (`cleanup-recheck.md` beside the
  Step 0 assignment), subject to attribution corrections now included here.
- H4-2 procedure/limits approved by user after Claude review on 4 October, as
  directed by the quoted Step 0 assignment and supplied planner decisions.
  Earlier at-writing attribution is corrected. Tooling unbuilt/untested, R3 unrun.
- H3-4 `6b8331d`: not user-approved, per the current assignment’s attribution
  correction. Accurate code-description correction stands; Git history unchanged.
- R1 conversion failure remains blocking; R3 is required on final candidate.
- AU23 precheck is approved in this assignment, but AU23 is not started.
- AU17 Contribution/readback only, AU18 WOM outcome/version history only, AU19
  approved. No additional Review context/current-event readiness UI is authorized.

## Next permitted action

Complete review26 follow-up R2 after the R1 commit, then stop for Claude direct recheck of `45a09ad..HEAD`. Deferred review26 notes remain unchanged. No B4/B5, UI/RC implementation, rehearsal, push, main merge, deployment or production access. R1 conversion/R3 and operator migration-count gates remain.
