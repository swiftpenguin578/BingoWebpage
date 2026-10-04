# Current project status

## Active assignment — Step 0 and AU lanes B1/B2, 4 October 2026

- Base verified clean: `5cf9081b458a573baa0c32fe423f88375f49534d`.
- Main feature checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`,
  branch `codex/participants-functionality`.
- Planner `/root`, UI Planner `01a0ec9a-76e3-7252-9850-3f260c612e59`.
- Exact user authorization and attribution policy:
  `docs/references/admin-ui/reviews/2026-10-04/au-step0/approval-record.md`.
  Full assignment: `assignment.md` beside it; supplied decisions: `decisions.md`.
- Step 0: one documentation-only commit, report its SHA before lane code begins.
- Then B1 Luna `gpt-5.6-luna` / max in a NEW worktree/branch
  `codex/au-b1-small-fixes`; B2 Astra `gpt-6-astra` / high in this worktree.
  Both start from the Step 0 commit. No orchestrator or extra workers.

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

Planner completes/checks/commits Step 0, reports SHA, creates B1 worktree at that
commit and dispatches the two named lanes/models. Workers record lane-local
handoffs; planner reconciles current status without overwriting their evidence.
