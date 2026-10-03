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

## Next permitted action

Implementer commits activation documentation separately, fixes only the named
findings, executes required checks and commits each item group. Record exact
per-group commits and results in a durable recheck handoff. Then stop for
Claude to recheck those commits. Do not start the cleanup brief or another ticket.
