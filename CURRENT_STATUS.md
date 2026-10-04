# Current project status

## Active assignment — B3 / AU20, 4 October 2026

- Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`.
- Clean baseline verified `b126c556ccd2e16cb59829837581646a3179847e`; planner's authorized documentation prerequisite `2649008` follows it.
- Planner `/root`, replacement chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`. Sole implementer `/root/au_b3_implementer`, `gpt-6-astra` / high; no orchestrator or extra workers.
- User supplied assignment: [B3 brief](docs/references/admin-ui/reviews/2026-10-04/au-b3/supplied-brief.md). Section 6's old planner chat is retired by the current assignment.
- Six ordered implementation items, one scoped local commit each; all await external Claude review. No current-page display, B4/B5, RC/UI integration, rehearsal, push, main merge or deployment.
- Item 1 checkpoint `52f0af6`: exact configured UTC checks and AU18 structured readback; focused checks passed, Claude review pending. Evidence: `au-b3/item1-exact-window.md`.
- Item 2 implemented: local early-end/Resume and pending-state migration; focused PostgreSQL and Release build passed. Evidence: `au-b3/item2-local-end-resume.md`.
- Item 3 other-4xx classification reported to planner before implementation; resolution pending. Items 3–6 are not complete.
- Unrelated broad-check finding retained in item 1 evidence: StatsPass4Boundary fixture expects an open submission window after configured cutoff; no silent Stats fix.
- AU20 absent-reference binding is recorded once in DELIVERY_PLAN's register; placement remains for UI integration.

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

Continue B3's six-item brief through focused execution and local checkpoints; stop after item 6 for Claude's independent review. R1 conversion/R3 release gates remain.
