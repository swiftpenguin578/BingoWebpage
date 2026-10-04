# Current project status

## Active assignment — brief 21 B1/B2 remediation, 4 October 2026

- Base verified clean: `5cf9081b458a573baa0c32fe423f88375f49534d`.
- Main feature checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`,
  branch `codex/participants-functionality`.
- Planner `/root`, UI Planner `01a0ec9a-76e3-7252-9850-3f260c612e59`.
- Exact user authorization and attribution policy:
  `docs/references/admin-ui/reviews/2026-10-04/au-step0/approval-record.md`.
  Full assignment: `assignment.md` beside it; supplied decisions: `decisions.md`.
- Step 0 completed and reported before dispatch: `3ce941b6718fd27fa514c309eabeb74d4ba17c3c`.
- B1 Luna `gpt-5.6-luna` / max works in the separate
  `/Users/christopher/.codex/worktrees/au-b1-small-fixes/BingoWebpage` checkout,
  branch `codex/au-b1-small-fixes`. B2 Astra `gpt-6-astra` / high used this checkout.
  Both started from Step 0; no orchestrator or extra workers.

## Lane ownership and stops

- B1: AU15 → AU16 → AU22 → AU24. WOM Fetch page only (RefreshAsync unchanged),
  Audit, Accounts/ownership, their projections/tests/translations and ticket docs.
  No scoring/Board/finalization/catalogue/draft/lifecycle/WOM management or sync
  edits; no migrations. C11FinalizedRosterIntegrationTests changes are B1-owned.
- B2: AU11 → AU12 → narrowed AU18. Board/editor/EHB/estimates/contribution,
  scoring/snapshots/Final Review and migrations. Existing events retain old ranking;
  explicit persisted event-creation boundary for new events. No B1 file edits.
- Use separate test files for B2 additions where B1 owns an existing test class.
  Shared translation/test/infrastructure file needs outside ownership are reported
  before edits. No silent overlap. B1 ticket-doc sections and B2 ticket-doc sections
  are disjoint; lane evidence/status stays in its own au-b1/ or au-b2/ directory.
- One local commit per ticket with focused checks and durable evidence.
- Report lane/ticket/commit/next action to `/root` before every turn-ending response
  and immediately for blockers/decisions. No wait_threads or planner polling.
- Each lane stops for Claude’s independent review, without waiting for the other.
  B1 local merge-back only after Claude B1 PASS; no push/main merge/deploy.
- B3–B5, RC/UI integration and rehearsal tooling/execution remain stopped.

## Remediation completion — both lanes awaiting Claude recheck

- Assignment: `/Users/christopher/Documents/BingoWebpage/review-notes/21-codex-brief-b1-b2-remediation.md`.
  Decisions: `08-decisions.md`, section "B1/B2 lane review decisions (user, 4 October)".
  B2 preserves supplied copies in `au-b2/remediation/assignment.md` and `decisions.md`.
- B1 clean checkpoint verified: `ea60b644aca91789c0e51600cc31ab63f047305b`.
  Recheck range: `b38749d..ea60b64`. Commits: `c32acea`, `95d9fdb`, `8c59d41`,
  `d4b2631`, `f49bf65`; handoff `ea60b64`.
- B1 reports focused PostgreSQL/HTTP checks, Release builds and diff checks passed.
  AU15 fixture test now passes. Chromium launches, but account-support.browser.js
  stops at line 62 (expected 8, observed 7); four AU24 assertions remain unverified.
  Exact limits: `au-b1/remediation/browser-account-support.md` in B1 checkout.
- B2 clean checkpoint verified: `52be2b6`. Recheck range: `f932893..52be2b6`.
  Eight item commits: `043eeb4`, `e3851b7`, `2498917`, `8fa7800`, `c47b086`,
  `882b780`, `d7c3e04`, `52be2b6`.
- B2 reports all focused HTTP/PostgreSQL, ranking, constructor and outcome-history
  checks passed; final Release build zero warnings/errors and diff checks passed.
  Planner verified Git checkpoints; has not independently rerun worker checks.
- Durable evidence root: `docs/references/admin-ui/reviews/2026-10-04/`;
  lane-specific evidence under `au-b1/remediation/` and `au-b2/remediation/`.
- B2 constructor-call edits in shared test paths were a planner technical routing
  exception; combine with B1 behavior edits at merge, never replace whole files.
  B1 exclusively owns SharedResource; B2 remediation did not edit it.
- D2 records user approval after Claude review of the earlier outcome-only WOM
  dependency, sourced to brief 21 and the supplied decision record.
- Limits: published snapshot format cannot distinguish a historical override equal
  to the automatic value; discard uses approved stored-precision comparison.
  Cleanup migration Down cannot restore cleared values; predeploy count still due.
- Both lanes stopped. Claude independent rechecks and manual acceptance pending.
  No production access, merge, push or deployment performed in this round.

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

Claude rechecks B1 `b38749d..ea60b64` and B2 `f932893..52be2b6` separately against
brief 21 and original findings. After Claude B1 PASS, planner may coordinate local
merge-back into `codex/participants-functionality`, preserving both lanes' fixes,
explicit constructor arguments and resource keys, then rerun B1 focused checks.
No merge to main, push, deployment or B3 work. R1/R3 release gates remain.
