# Current project status

## Active handoff — 3 October 2026

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
- Branch: `codex/participants-functionality`.
- Planner: UI Planner `01a0ec9a-76e3-7252-9850-3f260c612e59`, collaboration `/root`.
- Active assignment: named G-batch review remediation only, from
  `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/remediation-review.md`.
- Verified clean starting HEAD: `059faf5ba904b4a35c54eca4021fa306a2ea0586`.
- Expected planner activation edits: this file, retained review copy above,
  and FUNCTIONAL_CONTRACTS clarification of postponed-start recovery.
- Same direct implementer `/root/fix_batch_implementer`, `gpt-5.6-luna` / `max`.
  No orchestrator, additional worker or new chat. Claude performs named recheck.
- Report to `/root` before every turn-ending response and immediately for
  blockers/decisions, with exact checkpoint and next action. No wait_threads.

## Review result and scope

- Claude's independent source review: G1 and G3a FAIL; G3b requires fixes.
  G2, G4, G5 and G6 pass with recorded notes. Claude did not execute tests.
- Required findings: G1-1 cumulative credit reservation; G1-2 deterministic
  fixtures and checked approval results; G1-3 missing/weak checks;
  G1-6/G2-1 Danish translations/time label; G1-7 integration/docs notes;
  G3a-1/G3a-2 real inclusion-race fix and decisive regression;
  G3b-1 manual Remove/Move Running gate; G3b-2 unchanged-cap Schedule saves.
- Separate follow-up commits by group: G1, G2 translations, G3a, G3b.
  Include finding IDs, tests, necessary documentation and durable evidence.
  Preserve earlier commits and all unrelated work; no amendments.
- Run focused PostgreSQL checks and the full C33FinalizationFreshnessTests,
  DraftOperationsIntegrationTests and SubmissionWorkflowTests classes.
  Race regression must prove the second transaction is waiting and fail on
  the defective implementation, not merely accept either end state.
- Accepted G3a-3 clarification: retain postponed-start recovery until actual
  start/configured end; do not add a configured-start cutoff.
- Accepted notes are not extra fixes. No broad cleanup or optional expansion.

## Implemented, tested and committed

- Activation documentation checkpoint: `711d794`.
- G1 remediation (G1-1/G1-2/G1-3/G1-6/G1-7): `3c2a0a665daf70a89f88c317c2aa72c43cfdee97`.
- G2 translation remediation (G2-1): `a4a640c`.
- G3a concurrency remediation (G3a-1/G3a-2): `93d75f7`; generated-SQL
  boundary probe correction: `4a99106`.
- G3b membership/capacity remediation (G3b-1/G3b-2): `6628e4a`.
- Focused PostgreSQL checks passed for each changed boundary. Final Release
  build passed with 0 warnings and 0 errors. Full classes passed: C33
  Finalization Freshness 29/29, Draft Operations 56/56, Submission Workflow
  79/79. Exact commands and evidence are in
  `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/remediation-recheck-handoff.md`.

## Retained boundaries and evidence

- Original G implementation tip: `02d19db`; per-item inventory/evidence:
  `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/review-handoff.md`.
- Original approved brief: `br-td-batch/handoff.md` in that directory.
- Detailed review reports remain in
  `/Users/christopher/Documents/BingoWebpage/review-notes/14a-g1-g2-review.md`
  and `14b-g3-g6-review.md`; read relevant named findings only.
- F1–F9 and R-2 previously passed Claude source review. Evidence remains in
  `docs/references/admin-ui/reviews/2026-10-03/`.
- R-1 conversion failure remains a deploy blocker this release. R-3 is still
  unexecuted: isolated restored production-backup rehearsal required before
  separately authorized deployment; record G4 migration backfill count there.
- No push, merge, deployment, production/provider access or user-owned DB
  mutation. Use controlled fixtures. Frozen references stay unchanged.
- Other AU/RC work, UI integration and broad ticket/docs cleanup are deferred.

## Review handoff and next permitted action

- Implementation and required execution are complete and locally committed;
  Claude's independent named recheck is pending. This status is not an
  independent review or product acceptance.
- Claude should recheck `3c2a0a6`, `a4a640c`, `93d75f7`, `4a99106` and
  `6628e4a` against the named findings and the durable handoff. Do not begin
  another ticket, cleanup brief, deployment, rehearsal, push or merge.
