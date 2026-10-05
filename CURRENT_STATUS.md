# Current project status

## U1 named remediation — items 1–10 implemented; named recheck, visual acceptance and final whole .NET gate pending, 5 October 2026

- Authority: [brief50](/Users/christopher/Documents/BingoWebpage/review-notes/50-codex-brief-u1-remediation.md), [review49](/Users/christopher/Documents/BingoWebpage/review-notes/49-u1-review.md), U-A/B/C/E/F/G in [decisions08](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md), and [brief51 clock ruling](/Users/christopher/Documents/BingoWebpage/review-notes/51-u1-rem-item9-clock.md). Brief43's protected scope remains; no U-D decision exists.
- Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`. Sole implementer `/root/u1_implementer`, `gpt-6-astra` / high; actual dispatcher parent `/root` supersedes historical chat IDs. Claude owns independent review through the user.
- Remediation started from verified clean `ecbb947a1f24a43462df3a9ab3437c20c56e0e82`. Claude's whole .NET run there was **2021 passed / 1 failed / 0 skipped**, exit1: Application118, Domain265, Browser149, Integration1489 passed/1 failed. The failure was R1's stale Identity terminal-view expectation. Review49 was **FAIL**. Earlier status saying that baseline suite/review was still pending was stale; that suite was not a pass.
- Accepted pre-U1 backend baseline remains `89d9382`, Claude review41 and 1990/0/0. Original U1 items0–7 ended at `ecbb947`; evidence remains in [U1](docs/references/admin-ui/reviews/2026-10-05/u1/). Claude implemented items2c/2 under the user's assignment; their run provenance remains in item2-completion.md.
- Ten ordered local remediation commits, no amendments or packaging commit:
  1. `7f9eb42` R1 Identity terminal row (18 focused passes).
  2. `dd4fbe2` R12 recursive fail-closed event policy inventory (22 focused passes, including unchanged test14).
  3. `ff8b667` R3/U-A/R10 status classification, readback session notice and localized field labels (focused JS and localization pass).
  4. `e9b8401` R2/R8/R9/R6 fragment history, layer guard/cleanup, mobile resize, retained sidebar (focused JS pass).
  5. `51dcd09` R7/U-C safe timezone fallback and phase/date wording (PG/HTTP2/0/0, localization1/0/0).
  6. `a08d1cc` R4/U-F/R16 edited conflict choice, unchanged Save, reference timezone/readback text and shell name refresh (binding/timezone/readback JS pass; PG/HTTP2/0/0).
  7. `9afd895` R11/U-B/R17/U-E accessible bell, shell details/reference loading/menu/logo (JS pass; PG/HTTP2/0/0).
  8. `29d80ae` U-G scoped Danish event terminology, preserving old pages (localization2/0/0, PG/HTTP2/0/0).
  9. `11fd274` R5/R13 exact reads/dialog/validation/labels and evidence correction; brief51 deterministic fake clock (five explicit stability passes; full JS41/0; affected Slice3 PG/HTTP1/0/0; affected .NET UI34/0/0).
  10. This commit: R14/R15 authority/doc corrections, immutable setup-node pin, matrix/status handoff.
- Durable [remediation evidence](docs/references/admin-ui/reviews/2026-10-05/u1/remediation/) has one file per item and precise assertion-change authority. The original claim that no assertions were weakened was incorrect and is explicitly corrected in item7 evidence and item9 mapping.
- Executed candidate gates: full JS **41 passed / 0 failed**, complete non-incremental Release build **0 warnings / 0 errors**, diff check and frozen CSS comparisons clean. Initial item9 full JS40/1 timing failure is retained as history; brief51's paused clock resolved it without changing exact timing expectations. Final-commit build/JS results are reported in the dispatcher callback, without a separate evidence-only commit.
- The **final whole .NET gate remains pending user/Claude execution** on the reported final SHA; zero failures and zero skips are required per project. The implementer has not run that suite. CI has not run. No independent recheck or visual acceptance is claimed.

## Retained release gates and prior evidence

- B1/B2 merge `4065cb0`, evidence `40e624a`; Claude merge scope PASS per supplied B3 assignment. B1 follow-ups `404524d` / `a9399b2` and B2 `5545845` passed Claude source recheck. Their execution evidence remains workers’.
- Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b1/remediation/`, `au-b2/remediation/`, `au-b3/` and `au-b4/remediation/`; approval provenance in `au-step0/approval-record.md` and supplied assignment/decisions beside it.
- B3/AU20 is accepted as B4’s prerequisite. R1 conversion failure remains blocking; R3 isolated rehearsal is still required on the final candidate. Harness/transfer/rehearsal execution are unassigned; no production or user-owned database access here.
- Snapshot override equal to automatic remains indistinguishable; cleanup Down cannot restore cleared values. Preserve operator pre/post migration-count gates. The user’s 4 October zero counts for conditional-on-parent and non-default per-drop context are recorded in [08-decisions.md, B4 brief decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-brief-decisions); this worker did not execute them.
- F1–F9/R2, G1–G6 and H cleanup source-review evidence remains retained. H4-2 written procedure and its coverage limits were approved after Claude review on 4 October as recorded in the supplied decisions; tooling remains unbuilt/untested and R3 unrun. H3-4 `6b8331d` was not user-approved; accurate code-description correction and Git history remain.
- AU17 scope stays Contribution/readback only beyond AU17a; AU18 stays WOM outcome/version history only. No extra per-account Review context or current-event readiness UI.

## Next permitted action

Stop under brief50 after item10. Claude rechecks the named R1–R17 findings and
recorded U decisions against the stable remediation diff; the user checks the
shell and Identity in light/dark, Danish/English and phone width, including
confirmations/errors/toasts. UI_PAGE_MATRIX owns acceptance and remains awaiting
approval. User/Claude runs on the reported final SHA (zero failed/zero skipped):

```sh
cd /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-u1-final-suite-trx --logger "trx"
```

No U2–U10, lane T, rehearsal, push, main merge or deployment. CI is unrun;
setup-node now uses the verified immutable v4.4.0 commit instead of a mutable tag.
