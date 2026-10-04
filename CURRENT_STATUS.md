# Current project status

## Active assignment — brief 22 B1/B2 follow-up, 4 October 2026

- Base verified clean: `5cf9081b458a573baa0c32fe423f88375f49534d`.
- Main feature checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`,
  branch `codex/participants-functionality`.
- Planner `/root`, chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`, replaces old UI Planner.
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

## Claude recheck and assigned follow-ups — brief 22

- Source: `/Users/christopher/Documents/BingoWebpage/review-notes/22-b1-b2-remediation-recheck.md`.
  Claude reviewed source only; nothing built or run during that recheck.
- B1 `b38749d..ea60b64`: FAIL on R1; all five brief 21 remediation items pass.
  Confirmed account actions save but lose the response because the confirmation
  temporarily closes the editor. The line-62 browser failure was this real bug.
- B2 `f932893..52be2b6`: PASS with R2 rounding follow-up. The field-specific Danish
  decimal binder is a Claude-accepted planner technical resolution, not product approval.
- Follow-ups dispatched 4 October; no orchestrator, Claude rechecks directly:
  - B1 `/root/au_b1_followup`, `gpt-5.6-luna` / max, replaces unavailable old-tree
    worker; starts at clean `ea60b644aca91789c0e51600cc31ab63f047305b` in B1 checkout.
    Item 1: fix R1 and rerun account-support.browser.js. Item 2: malformed Audit
    date/entry feedback plus three Accounts/Audit test gaps named in brief 22.
    Two new local commits, one per item. No completed work rediscovery.
  - B2 existing chat `01a10444-ca79-71a1-8841-07e1d926ac2d`, local,
    `gpt-6-astra` / high; clean start `c557139` (status only after `52be2b6`).
    Item 3: AwayFromZero at four named stored-EHB comparisons; midpoint ranking
    and PostgreSQL discard tests. One new local commit.
- Workers own focused execution, Release build and diff checks. Results pending;
  no follow-up independent review or manual acceptance claimed. B1's four AU24
  browser assertions remain unverified until the new browser run reaches them.
- Durable evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b1/remediation/`
  and corresponding `au-b2/remediation/`. Reuse prior applicable passing evidence.
- B1 owns SharedResource; B2 owns migrations. Preserve B2 constructor arguments and
  B1 behavior edits when merging shared tests; never replace whole files.
- Existing limits remain: snapshot override equal to automatic is indistinguishable;
  cleanup Down cannot restore cleared values. Operator pre/post migration counts
  are still due. No production access is assigned.
- Both workers must report completion/blockers to this planner and stop for Claude.
  No merge, push, main merge, deploy or B3–B5/RC/UI/rehearsal work assigned now.

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

Complete only the assigned brief 22 follow-ups, then Claude directly rechecks both.
After Claude B1 PASS, coordinate local merge into `codex/participants-functionality`
(not `main`), preserving both lanes, then rerun B1 focused checks including AU15 and
account-support.browser.js. Stop after rechecks and authorized merge. No push,
main merge, deployment or B3 work. R1 conversion/R3 rehearsal release gates remain.
