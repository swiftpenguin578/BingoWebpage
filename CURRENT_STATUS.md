# Current project status

## Active assignment — Step 0 and AU lanes B1/B2, 4 October 2026

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

## B1 completion — awaiting independent review

- Clean final checkpoint verified by planner: `b38749d`, branch `codex/au-b1-small-fixes`.
- Ticket commits: AU15 `b5cde26`, AU16 `90ce3f0`, AU22 `b44fa6e`, AU24 `b38749d`.
  AU15 amendment from `06703fc` is explicitly reconciled in `05ced42`.
- Worker reports focused PostgreSQL, BrowserTests, Release build, Node overlay and
  diff checks passed. Planner has not independently rerun these checks.
- Exceptions: AU15 PG/HTTP schedule assertion remains unverified after identical
  Step 0 baseline reproduction; AU24 Playwright Chromium launch is blocked by
  SIGABRT/EPERM. Neither is recorded as passing.
- Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b1/` in B1 checkout.
- B1 stopped for Claude's independent review; not merged into this checkout.

## B2 completion — awaiting independent review

- Clean implementation checkpoint verified by planner: `d01c08c8528dd85a15a1d04e816b92eccc55466a`.
- Ticket commits: AU11 `a628cda`, AU12 `0f79ede`, AU18 `d01c08c`.
- Worker reports focused checks passed: AU11 PostgreSQL 12 + mechanics 10 and
  calculator 14; AU12 PostgreSQL 6 + ranking 19 + regressions 2; AU18 PostgreSQL
  7 + rendered/localization/manual-refresh/reopening regressions 3. Final Release
  build: zero warnings/errors. Planner has not independently rerun these checks.
- Durable evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b2/`
  (`au11.md`, `au12.md`, `au18.md`). AU12 includes populated migration Up/Down.
- Outcome-only sync dependency and temporary resource ownership were planner
  technical resolutions, not user approvals. Resource ownership returns to B1.
- B2 stopped: Claude commit-by-commit review pending; manual acceptance deferred.
  No B3, integration, merge, push or deploy is authorized by this completion.

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

Claude reviews each lane independently, commit by commit:
- B1: `3ce941b..b38749d` in its separate checkout, including `05ced42` reconciliation.
- B2: `3ce941b..d01c08c` in this checkout; later planner commits change status only.
Both implementers have stopped. Lane evidence remains authoritative for check
limitations. B1 local merge-back requires Claude B1 PASS and focused checks on the
merged result, preserving B2's resource keys. No push, deployment or B3 work.
