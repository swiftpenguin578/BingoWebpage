# Current project status

## Active handoff — 3 October 2026

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
- Branch: `codex/participants-functionality`.
- Planner: UI Planner `01a0ec9a-76e3-7252-9850-3f260c612e59`, collaboration `/root`.
- User authorized G1–G6. Durable brief:
  `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/handoff.md`.
- Verified initial HEAD: `f2ea1cffb8f4d9c0b23dd68dcf3f6675d1bbb5d5`, clean.
- Expected activation changes: this status, the durable brief and scoped
  FUNCTIONAL_CONTRACTS updates. Commit these separately before implementation.
- One direct `gpt-5.6-luna` / `max` implementer, `/root/fix_batch_implementer`;
  no orchestrator, extra workers or separate chats. Claude owns independent review.
- Report to `/root` before every turn-ending response and for blockers/decisions.
  No planner polling or `wait_threads`. Record exact checkpoint if delivery fails.
- Work sequentially: G1 approval room/order; G2 screenshot-time guidance;
  G3a structural races; G3b manual-team capacity; G4 cancelled-attempt restart;
  G5 finalized Add roles; G6 current public role labels.
- G3a and G3b each get a separate commit. Every item needs scoped executable
  checks, durable evidence and a local commit; no independent PASS claim yet.
- G4 uses the preferred full-Setup restart: `RequiresFreshOrder` preserves
  cancelled pick history while permitting fresh positions, scrambling and
  preassignment on the next attempt. The existing history assertions remain.
- No push, merge, deployment, production access, live provider calls or mutation
  of user-owned databases. Controlled PostgreSQL fixtures only.
- Preserve frozen references (canvas 42 / artifact `1790965722-e7ad`).
  New UI integration, other AU/RC work, R-3 and broad docs cleanup are deferred.

## Retained results and gates

- F1–F9 passed Claude's independent source review. Claude did not execute tests.
- R-2 test-only follow-up `f2ea1cf` passed Claude's named source recheck.
- Prior item checks/evidence: `docs/references/admin-ui/reviews/2026-10-03/`.
- F1 uses the user-performed manual banner cleanup; migration/guard unchanged.
  No agent production access occurred; full production-baseline rehearsal unrun.
- R-1: failed Luck conversion continues to block deployment for this release.
- R-3: full isolated production-backup deploy rehearsal must pass before any
  separately authorized production deployment. It remains unexecuted.
- G1 is implemented and focused-tested in local commit `24c30fc`; evidence is
  recorded in `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/G1-evidence.md`.
  G2 is implemented and committed as `2b7ddbc`; evidence is
  recorded in `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/G2-evidence.md`.
  G3a is implemented and committed as `a8a6c24`; evidence is recorded in
  `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/G3a-evidence.md`.
  G3b is implemented and committed as `56020b8`; evidence is recorded in
  `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/G3b-evidence.md`.
  G4 is implemented and committed as `1b5a139`; evidence is recorded in
  `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/G4-evidence.md`.
  G5 is implemented and committed as `b9bb405`; evidence is recorded in
  `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/G5-evidence.md`.
  G6 is implemented and committed as `02d19db`; its evidence
  is recorded in `docs/references/admin-ui/reviews/2026-10-03/br-td-batch/G6-evidence.md`.
  Claude's independent review is pending; all G1–G6 implementation commits
  are present. No further batch item is authorized.
- Standing scoped per-item local commit authority is confirmed; no push authority.

## Next permitted action

G1–G6 implementation and focused checks are complete; independent review is
pending. Claude reviews the stable per-item commits listed in
`docs/references/admin-ui/reviews/2026-10-03/br-td-batch/review-handoff.md`.
Stop here. Do not start the separate ticket/documentation cleanup, R-3 or another
ticket without authorization. Named review corrections may return to the same
implementer when assigned. No push, merge or deployment is authorized.
