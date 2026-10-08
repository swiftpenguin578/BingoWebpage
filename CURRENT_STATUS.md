# Current project status

## Final candidate (D1) — 8 October 2026

**Feature branch** `codex/participants-functionality`, head `2dccc075`: U1–U11, the C8 Luck proof, the ImageSharp 4.1.2 upgrade (`f18ba2e0`) and the notification-footer fix (`2dccc075`) are merged. Planner-held worktree `/Users/christopher/Documents/BingoWebpage-feature`. Decisions and evidence: `review-notes/08-decisions.md` in the main folder (from "Parallel lanes to finish by Friday 9 October").

**Verified:**
- Last whole suite on `0a92431b` (U11 merge): Application 118/0, Domain 266/0, Browser 199/0, Integration 1660 passed / 1 failed / 0 skipped. The one failure (`Slice3ScheduleLifecycleIntegrationTests.PublicTextReadinessIsInformationalForManualAndScheduledOpening`) was an Npgsql timeout under parallel load and passed on isolated rerun (8/8), so 2,244 tests are accounted for; the previous clean run was 2,244/0/0 on `1eec9731`. All-page conformance: 85 cases pass in Chromium and WebKit.
- Release build with NuGet audit on: 0 warnings (licensed ImageSharp 4.1.2). GitHub secret `SIXLABORS_LICENSE_KEY` is set (8 October 2026, 15:59 UTC).
- Every admin page is accepted by the user (8 October 2026); `UI_PAGE_MATRIX.md` owns page approval. The user accepted all provisional rulings and proposed wordings ("These all look fine"). No open user decisions remain for this branch except the push and deploy approvals.

**D1 in progress:** `DELIVERY_PLAN.md` status cells and C/D ledger, `docs/PRODUCTION_RUNBOOK.md` and this file are reconciled on `claude/d1-docs` (docs only). Then the final candidate SHA is fixed.

**R-3 rehearsal (user decision: option b, lighter local rehearsal; runbook subsection of the same name):** harness `scripts/rehearsal/local-rehearsal.sh` on `claude/local-rehearsal` (`13472767`). First real run on `9c734a8f` with the user's production dump passed stages 1–4 (AU20 blocker cleared by the user); in progress. Accepted limits: no VM, Docker internal network only, no G4 Down/re-Up check, no real R2/WOM/restic/host wrapper/public TLS. The dump is private (outside the repo) and is deleted afterwards. The planner records the final result here.

**Before the deploy (runbook release-readiness checklist, items 9–11):** install the changed host copies of `bingo-deploy` and `bingo-verify-evidence`; keep the `SIXLABORS_LICENSE_KEY` secret; rollback uses the prior image digest because `main` no longer restores (ImageSharp 3.1.12 advisories).

**Next steps (in order):**
1. Final whole .NET suite on the exact candidate SHA (planner, zero failures and zero skipped).
2. Push of the branch — needs the user's approval.
3. CI green on the pushed SHA.
4. Rehearsal rerun on the candidate SHA (`--keep-db` available for browsing the imported event).
5. Deploy — needs the user's explicit approval (D3).

Open later items live in `BACKLOG.md` (not approved work).

**No push, merge to `main` or deployment is authorized.** Older handoffs are in Git history.
