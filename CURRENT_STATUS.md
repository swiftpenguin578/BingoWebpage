# Current project status

## Active handoff — 3 October 2026

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
- Branch: `codex/participants-functionality`.
- Planner: UI Planner `01a0ec9a-76e3-7252-9850-3f260c612e59`, collaboration `/root`.
- Active assignment: H1–H7 ticket/documentation cleanup, per
  `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/handoff.md`.
- Verified clean initial HEAD: `576c6615c64af4d0959152dbcbf7c327106f624f`.
- Expected activation changes: this status, durable cleanup brief and accepted
  G recheck copy in the same evidence folder. Commit activation separately.
- Same direct implementer `/root/fix_batch_implementer`, `gpt-5.6-luna` / `max`.
  No orchestrator, extra workers or separate chats. Claude independently reviews.
- Report to `/root` before every turn-ending response and on blockers/decisions;
  include exact checkpoint and next owner/action. No wait_threads or polling.

## Scope and checkpoints

- H1 first: preserve compact historical evidence from temporary folders or
  Claude's backup; sanitize secrets/real participant data, omit builds/large logs,
  record omissions and total bytes. Never fabricate missing evidence.
- H2 current status/registers; H3 owning behaviour docs; H4 release gates;
  H5 ownership/tickets for every named finding; H6 exact test/comment cleanup
  plus N-1 missing-draft refusal and N-2 wording/localization; H7 CLAUDE.md only.
- Apply approved D1–D8 in the brief. Recheck current state and skip/report
  items already closed by G commits. No implementation of findings listed in H5.
- H6 is the only production-code exception. Preserve existing assertions;
  add/execute the focused missing-draft test and applicable build/checks.
- H7 changes only the specified CLAUDE.md lines and exact approved paragraph.
  Do not change AGENTS.md or extend Claude workflow policy to this assignment.
- One local commit per H item, including scoped evidence and needed docs.
  No amendments, unrelated staging or deletion of the active status file.
- Proposed remaining AU order and then UI integration order are recommendations
  for user approval, not authority to begin or run them in parallel.

## Retained outcomes and release gates

- F1–F9 and R-2 passed Claude independent source review; no test reruns by Claude.
- G1–G6 including named remediation now PASS, accepted by Claude at `576c661`.
  Preserved verdict: `doc-ticket-cleanup/g-batch-accepted-recheck.md` under
  `docs/references/admin-ui/reviews/2026-10-03/`.
- G execution evidence: `br-td-batch/remediation-recheck-handoff.md` under that
  directory. Recorded C33 29/29, DraftOperations 56/56, SubmissionWorkflow 79/79,
  Release build 0 warnings/errors. These are worker execution results.
- R-1 conversion failure remains a deployment blocker for this release.
- R-3 isolated restored-production-backup rehearsal is still unexecuted and must
  pass on the final candidate before separately authorized production deployment.
  Include G4 requires_fresh_order migration backfill count in that rehearsal.
- No production/provider access, user database mutation, push, merge, deployment,
  R-3 execution, frozen-reference edits or UI integration. Preserve other work.

## Next permitted action

Commit activation docs, execute H1–H7 only, with scoped checks and evidence.
At completion update this status accurately, report per-item commits, omissions,
size of retained evidence, open decisions and proposed queue order; then STOP for
Claude independent review. Do not start the remaining AU/UI tickets or rehearsal.
