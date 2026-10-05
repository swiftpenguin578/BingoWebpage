# Current project status

## U1 foundation + Identity — in progress, 5 October 2026

- Authority: [brief43](/Users/christopher/Documents/BingoWebpage/review-notes/43-codex-brief-u1-foundation-identity.md), [plan42](/Users/christopher/Documents/BingoWebpage/review-notes/42-ui-integration-plan.md), and [decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md).
- Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`; clean starting HEAD `89d938292a2a22e0648d12f3885731c768244905` verified. Sole implementer `/root/u1_implementer`, `gpt-6-astra` / high; dispatcher actual parent `/root` in the fresh U1 chat supersedes brief43's historical chat ID. Claude is planner and independent reviewer through the user.
- Items 0–7 run sequentially, one scoped local commit each. Item0 records approved ownership and shell decisions only; frozen reference assets stay unchanged. No page is bound or visually accepted yet. Durable evidence: [U1](docs/references/admin-ui/reviews/2026-10-05/u1/).
- Baseline accepted by [Claude review41](/Users/christopher/Documents/BingoWebpage/review-notes/41-sweep-server-fixes-recheck.md) at `89d9382`: Release build 0 warnings/errors; Domain 265, Application 118, Browser 148, Integration 1,459 — **1,990 passed / 0 failed / 0 skipped** in Claude's scratch PostgreSQL run. No baseline rerun required. Earlier accepted test-health/B5 evidence remains in Git and the linked reviews.
- Final whole-suite gate remains **pending user execution** on U1's final commit. User/Claude executes the unfiltered .NET suite; implementer runs focused applicable checks, Release build, diff checks and the full new JS runner. A passing checkpoint is not independent review or manual acceptance.

## Retained release gates and prior evidence

- B1/B2 merge `4065cb0`, evidence `40e624a`; Claude merge scope PASS per supplied B3 assignment. B1 follow-ups `404524d` / `a9399b2` and B2 `5545845` passed Claude source recheck. Their execution evidence remains workers’.
- Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b1/remediation/`, `au-b2/remediation/`, `au-b3/` and `au-b4/remediation/`; approval provenance in `au-step0/approval-record.md` and supplied assignment/decisions beside it.
- B3/AU20 is accepted as B4’s prerequisite. R1 conversion failure remains blocking; R3 isolated rehearsal is still required on the final candidate. Harness/transfer/rehearsal execution are unassigned; no production or user-owned database access here.
- Snapshot override equal to automatic remains indistinguishable; cleanup Down cannot restore cleared values. Preserve operator pre/post migration-count gates. The user’s 4 October zero counts for conditional-on-parent and non-default per-drop context are recorded in [08-decisions.md, B4 brief decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-brief-decisions); this worker did not execute them.
- F1–F9/R2, G1–G6 and H cleanup source-review evidence remains retained. H4-2 written procedure and its coverage limits were approved after Claude review on 4 October as recorded in the supplied decisions; tooling remains unbuilt/untested and R3 unrun. H3-4 `6b8331d` was not user-approved; accurate code-description correction and Git history remain.
- AU17 scope stays Contribution/readback only beyond AU17a; AU18 stays WOM outcome/version history only. No extra per-account Review context or current-event readiness UI.

## Next permitted action

Complete brief43 items in order through item7, then stop for Claude independent
review and user visual acceptance (both themes, English/Danish, phone width).
Item2 baseline JS failures stop that item for a recorded decision; existing test
expectations stay unchanged. Environment failure stops dependent checks. No U2–U10,
lane T, rehearsal, push, main merge or deployment is authorized.
