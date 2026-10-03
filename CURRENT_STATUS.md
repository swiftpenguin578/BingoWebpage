# Current project status

## Active handoff — 4 October 2026

- Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
- Branch: `codex/participants-functionality`.
- Planner: UI Planner `01a0ec9a-76e3-7252-9850-3f260c612e59`, collaboration `/root`.
- User explicitly authorized the named H cleanup remediation round only.
- Verified clean starting HEAD: `f6b5bd9e75957892f7323ae55d29c380f1181266`.
- Durable assignment/review and decisions:
  `docs/references/admin-ui/reviews/2026-10-04/cleanup-remediation/review.md`
  and `decisions.md` beside it. Original detailed reviews are in
  `/Users/christopher/Documents/BingoWebpage/review-notes/15a-h1-h3-review.md`
  and `15b-h4-h7-review.md`.
- This round's user override: one direct `gpt-6-astra` / `high` implementer,
  `/root/cleanup_astra_remediator`. Previous Luna worker remains stopped;
  collaboration cannot change its model, so carry forward its evidence/context.
- No orchestrator, extra workers or new chats. Claude independently rechecks.
- Send a report to `/root` before every turn-ending response and immediately
  for blockers or decisions; include exact checkpoint and next action.
  No wait_threads or routine planner polling.

## Scope and approval boundary

- Claude review: H1/H3/H4/H5 FAIL, H2 small misses, H6/H7 PASS with H6 string gap.
  No reviewer builds/tests were run. Prior worker checks remain evidence only.
- Fix only Required fixes in review.md. One follow-up commit per H1–H6 group,
  naming findings. No amendments. H7 unchanged.
- H4-2 is PROPOSE FIRST: write a separate isolated-rehearsal proposal, report it
  to the planner/user, and do not place the procedure into the runbook before
  explicit user approval. Continue other independent items while pending.
- H5-8/H5-9 are ticket/document text only. Option 1 supersedes older H5-9 text:
  always enter final chance; retire Only after from new input/editor/panel;
  retain history columns; NO EHB parent-chance fix ticket. Preserve existing
  production roll groups. Activity team size is informational, editable by Admins.
- AU23 ordinary rate text includes N x rolls; advanced roll groups stay
  SuperAdmin-only. Apply the final decided Catalogue layout, not earlier proposals.
- No production-code changes except H6-1 Danish resource string. No H5 ticket
  implementation, provider calls, production/user DB access or mutation,
  deployment/rehearsal, push, merge, frozen-reference edits or UI integration.
- Proof maps every finding to exact changed file:line; check evidence/path
  integrity and named stale-phrase searches. Preserve real historical evidence;
  sanitize secrets/personal data, do not invent absent evidence or provenance.
- Expected activation edits: this file and durable review/decisions above.
  Commit activation separately before item commits. Preserve unrelated changes.

## Retained outcomes and gates

- F1–F9, R-2 and G1–G6 passed Claude independent source review/recheck.
- G accepted checkpoint: `576c661`; H original candidate: `f6b5bd9`.
- Original H handoff and prior evidence:
  `docs/references/admin-ui/reviews/2026-10-03/doc-ticket-cleanup/h1-h7-final-handoff.md`.
- R-1: Luck conversion failure blocks deployment for this release.
- R-3 remains unexecuted; procedure requires correction/approval before the
  isolated final-candidate rehearsal and separately authorized production deploy.
- D8 queue order remains proposed. Claude plans remaining batching/parallel work
  after this review passes; nothing here starts AU/RC/UI tickets.

## Next permitted action

Implementer commits activation documents, prepares H4-2 proposal early and sends
it to `/root`, then fixes other named items while awaiting the user's decision.
At round end report each finding, commit and exact evidence/line references;
record unresolved H4 approval separately and stop for Claude's named recheck.
Do not delete this file or claim a failed/blocked/unexecuted gate passed.
