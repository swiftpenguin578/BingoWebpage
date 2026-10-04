# Current project status

## Active assignment — test-health batch, 4 October 2026

- Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`; clean starting HEAD `0179b9b3f4d4dff00a401f642e6a80374b5dc807` verified.
- Planner `/root`, chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`; sole implementer `/root/au_b5_implementer`, explicitly assigned `gpt-6-astra` / high. No orchestrator, extra worker or self-review.
- Authority: [brief 32](/Users/christopher/Documents/BingoWebpage/review-notes/32-codex-brief-test-health.md), [08-decisions.md B5 review D14 and approved test-change rule](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b5-review-decisions-user-4-october). Only the 16 failures and one flaky test are in scope; B5 remediation remains stopped.
- Clean-baseline reproduction: Integration 16 FAIL / 1 PASS / 0 skipped (includes F and the withdrawal theory case); Browser 1 FAIL / 0 PASS / 0 skipped. All listed failures reproduced.
- Cause-group commits: outdated tests `3f519429a2f645d1db93592088c24e6814841b22`; D16 code fix `f87e51865d62527dd1cc75155ef07cedd0a53252`; setup/migration fixtures `6a1f3af0479fd809f298282fe289fc06dcaa51a3`; deterministic flaky fixture `ae48c69d9cd42d039ceb48ce8b9921c6f32c1bf9`.
- Full corrected classes passed: C11 59, capacity 23, creation Browser 28; seven setup/migration classes 361; D16 lifecycle/retry classes 43. All zero failures/skips. The flaky 16-test class passed five consecutive runs. Existing migrations and test14 remain unchanged. Controlled PostgreSQL/provider doubles only.
- The initially unclear #14 was stopped until the user recorded **D16 option a**. Terminal Questions now returns only an exact committed-add replay; every other POST gets the exact Manage/read-only refusal. Its first added proof exposed unknown-handler fallback (3 failures); the corrected full classes passed 43/43. No failure was waived.
- **Batch gate is the whole suite with zero failures and zero skipped tests**, plus clean Release build and diff check. Completed-code clean Release build passed, 0 warnings/errors. The unfiltered whole suite passed **1921/1921**, zero failures/skips: Domain 265, Application 118, Browser 148, Integration 1390. The final documentation commit is verified with the same gate; its exact SHA/result is recorded in the terminal delivery report. External independent review and technical acceptance remain pending. [Evidence](docs/references/admin-ui/reviews/2026-10-04/test-health/evidence.md).
- B5 implementation remains at six commits ending `0179b9b`; [Claude review 31](/Users/christopher/Documents/BingoWebpage/review-notes/31-b5-review.md) identifies separately routed B5 remediation A1/C1/C2/B1/B2/E1. None is included here. RC07/RC05/RC04–DRF binding remains pending. B4 remains accepted at `fdc73dc` per [review 29 appended PASS](/Users/christopher/Documents/BingoWebpage/review-notes/29-b4-remediation-recheck.md#follow-up-recheck-2049fb2fdc73dc-direct-claude-4-october).

## Retained release gates and prior evidence

- B1/B2 merge `4065cb0`, evidence `40e624a`; Claude merge scope PASS per supplied B3 assignment. B1 follow-ups `404524d` / `a9399b2` and B2 `5545845` passed Claude source recheck. Their execution evidence remains workers’.
- Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b1/remediation/`, `au-b2/remediation/`, `au-b3/` and `au-b4/remediation/`; approval provenance in `au-step0/approval-record.md` and supplied assignment/decisions beside it.
- B3/AU20 is accepted as B4’s prerequisite. R1 conversion failure remains blocking; R3 isolated rehearsal is still required on the final candidate. Harness/transfer/rehearsal execution are unassigned; no production or user-owned database access here.
- Snapshot override equal to automatic remains indistinguishable; cleanup Down cannot restore cleared values. Preserve operator pre/post migration-count gates. The user’s 4 October zero counts for conditional-on-parent and non-default per-drop context are recorded in [08-decisions.md, B4 brief decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-brief-decisions); this worker did not execute them.
- F1–F9/R2, G1–G6 and H cleanup source-review evidence remains retained. H4-2 written procedure and its coverage limits were approved after Claude review on 4 October as recorded in the supplied decisions; tooling remains unbuilt/untested and R3 unrun. H3-4 `6b8331d` was not user-approved; accurate code-description correction and Git history remain.
- AU17 scope stays Contribution/readback only beyond AU17a; AU18 stays WOM outcome/version history only. No extra per-account Review context or current-event readiness UI.

## Next permitted action

After the required final-commit whole-suite verification recorded in the terminal handoff, stop for external Claude independent review of this test-health batch. All 16 named failures and F are resolved in the completed-code gate; no test is waived. No B5 remediation, UI binding, rehearsal, next batch, push, merge to any branch or deployment is authorized.
