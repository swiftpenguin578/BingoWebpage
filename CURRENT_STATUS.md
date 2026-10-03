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
- G4's explicit history/contract decision stop remains in force. Report the
  proposal and continue independent items if that stop is reached.
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
- Standing scoped per-item local commit authority is confirmed; no push authority.

## Next permitted action

Implementer commits the expected activation documents, then implements and checks
G1–G6 against the durable brief and cited decisions/reports. Update this handoff
with concise progress and evidence links. At batch end report per-item commits,
checks, deviations and unresolved decisions, then stop for Claude's review.
Do not start the separate ticket/documentation cleanup or another ticket.
