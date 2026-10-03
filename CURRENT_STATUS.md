# Current project status

## Active handoff — 3 October 2026

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
- Branch: `codex/participants-functionality`.
- Planner: UI Planner `01a0ec9a-76e3-7252-9850-3f260c612e59`, collaboration `/root`.
- Active assignment: H1–H7 ticket/documentation cleanup, per
  `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/handoff.md`.
- Verified clean starting HEAD: `576c6615c64af4d0959152dbcbf7c327106f624f`.
- Activation checkpoint: `e9e65d6`. H1 durable evidence checkpoint:
  `6c7603f`.
- Direct implementer: `/root/fix_batch_implementer`, `gpt-5.6-luna` / `max`.
  No extra workers, chats, production access, provider calls, user-database
  mutation, push, merge, deployment or rehearsal. Claude independently reviews.
- Report to `/root` before every turn-ending response and on blockers/decisions;
  no wait_threads or polling.

## Completed implementation and review state

- F1–F9 and R-2 passed Claude's independent source review at the accepted
  checkpoints; Claude did not rerun tests. F1 records the user's one-time manual
  banner cleanup, F2/F3 the Luck snapshot/conversion paths, F4 reset-token
  reauthorization, F5 slug allocation, F6 late-end handling, F7 participant
  versions, F8 AU13 planning estimate and F9 finalization validation.
- R-1 remains a deployment blocker because a failed Luck conversion blocks this
  release. R-3 remains unexecuted: the full `bingo-deploy` sequence must pass on
  an isolated restored production backup and final candidate before deployment,
  including the G4 `requires_fresh_order` backfill count.
- G1–G6 passed Claude's named remediation recheck at `576c661`. Worker evidence
  recorded C33 29/29, Draft Operations 56/56, Submission Workflow 79/79 and a
  Release build with 0 warnings/errors; Claude did not rerun these checks.
- H1 is committed at `6c7603f`: 63 sanitized durable evidence files totaling
  137,885 bytes, with source inventory and omissions in
  `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1/H1-evidence.md`.
  The owning evidence pointers now use repository paths.
- H2 reconciles stale status/register wording, marks Dashboard prototype notes
  historical, records committed F/Dashboard state, and adds the approved/proposed
  AU16–AU24 register. It does not start any ticket or change runtime behavior.

## Protected scope and open choices

- H3 owns behavior-document corrections; H4 owns release readiness; H5 owns
  ticket ownership; H6 owns cosmetic test/comment cleanup plus N-1/N-2; H7 owns
  only the named `CLAUDE.md` paragraph and bullet. No H5 ticket is implemented.
- AU23's ordinary-input roll-count treatment remains an open product choice;
  record it and do not infer a rule. D8 remains a proposed order for approval:
  remaining AU tickets, then UI integration tickets, then final candidate, R-3
  rehearsal and deploy. Nothing in this status starts that queue.
- Existing user/operator facts and prior worker evidence are preserved as such;
  this file does not claim production verification, manual acceptance or a fresh
  full-suite run.

## Next permitted action

Complete H3–H7 only with one local commit per item and scoped checks/evidence.
Then update this status with exact commits, skipped/already-resolved findings,
open choices and the proposed queue order, and stop for Claude's independent
review. Do not start AU/RC implementation, UI integration, R-3, cleanup beyond
this brief, push or merge.
