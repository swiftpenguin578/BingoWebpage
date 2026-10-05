# Current project status

## U1 remediation round 2 — four items implemented; named recheck, visual acceptance and final whole .NET gate pending, 5 October 2026

- Authority: [brief53](/Users/christopher/Documents/BingoWebpage/review-notes/53-codex-brief-u1-remediation-2.md), [recheck52](/Users/christopher/Documents/BingoWebpage/review-notes/52-u1-recheck.md), named 52a/52b findings and U-F in [decisions08](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md). Brief43/50 protected scope and precise test-change rules remain.
- Checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`. Sole implementer `/root/u1_implementer`, `gpt-6-astra` / high; actual dispatcher parent `/root` supersedes historical chat IDs. Claude owns independent review through the user.
- Round 2 began at verified clean `9986ea185578ff7a9bdcbc5695c966031ae609d2`. Recheck52 was **FAIL** on 52a N1's history loop; U-F remained partial. This implementation does not claim a subsequent independent pass or visual approval.
- Claude's whole .NET scratch run at `9986ea1` exited **1**: Application **118/0/0**, Domain **265/0/0**, Browser **150/0/0**, Integration **1496 passed / 1 failed / 0 skipped**. The sole failure was `AdminStaleChangeIntegrationTests.AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction(Restore, false)` in `InitializeAsync`: PostgreSQL `28P01` password authentication failure while migrating a fresh Testcontainers database, before test code. The same-build whole-class rerun passed **15/15**. These are separate results, not an uninterrupted whole-suite pass. The per-test-container observation is outside U1 scope; no test-health fix was made.
- Four ordered local commits:
  1. `3520b89` — adopt fragment history entries, safe foreign-entry reload, exact clean/dirty skip → switch → Back proofs (52a N1).
  2. `cc80388` — Cancel, focusable aria-disabled Save/tooltip, clean status after revert and corresponding exact assertions (U-F / 52b N2–N4).
  3. `7e89ab9` — restore theme after context refresh, preserve success toast/usability if refresh throws, scoped Danish neuter grammar (52b N1/N5, 52a R16).
  4. This commit — server-valid Identity fixtures/exact Also saved fields, real Check again `readback: true` proof, persisted Copenhagen DST date projection, status/evidence handoff (52b N6, 52a T1/T2).
- Durable [round 2 evidence](docs/references/admin-ui/reviews/2026-10-05/u1/remediation-2/) contains one file per item with assertion-change authority. Focused shell/Identity JS passed; localization **2/0/0** and controlled PostgreSQL switcher checks **2/0/0** passed. Affected .NET UI **34/0/0** passed. Candidate full JS **41 passed / 0 failed**; non-incremental Release **0 warnings / 0 errors**; diff check and frozen CSS comparisons clean. Final-commit verification is reported in the dispatcher callback without an extra evidence-only commit.
- Prior evidence remains: accepted backend `89d9382`, review41 and Claude **1990/0/0**; original U1 ended at `ecbb947`, review49 FAIL and Claude **2021/1/0**; round 1 ten commits ended at `9986ea1`, implementer final-commit JS **41/0**, Release **0 warnings / 0 errors**, affected UI **34/0/0**. See [original U1](docs/references/admin-ui/reviews/2026-10-05/u1/) and [round 1](docs/references/admin-ui/reviews/2026-10-05/u1/remediation/). Earlier inaccurate assertion-preservation claims remain explicitly corrected in item7/item9 evidence. Claude's item2c/item2 implementation and run provenance remain in item2-completion.md.
- **Final whole .NET gate pending user/Claude execution on the reported round 2 final SHA**, requiring zero failures and zero skips in every project. The implementer has not launched that suite. CI is unrun; independent named recheck and user visual acceptance remain pending. UI_PAGE_MATRIX owns page acceptance.

## Retained release gates and prior evidence

- B1/B2 merge `4065cb0`, evidence `40e624a`; Claude merge scope PASS per supplied B3 assignment. B1 follow-ups `404524d` / `a9399b2` and B2 `5545845` passed Claude source recheck. Their execution evidence remains workers’.
- Evidence: `docs/references/admin-ui/reviews/2026-10-04/au-b1/remediation/`, `au-b2/remediation/`, `au-b3/` and `au-b4/remediation/`; approval provenance in `au-step0/approval-record.md` and supplied assignment/decisions beside it.
- B3/AU20 is accepted as B4’s prerequisite. R1 conversion failure remains blocking; R3 isolated rehearsal is still required on the final candidate. Harness/transfer/rehearsal execution are unassigned; no production or user-owned database access here.
- Snapshot override equal to automatic remains indistinguishable; cleanup Down cannot restore cleared values. Preserve operator pre/post migration-count gates. The user’s 4 October zero counts for conditional-on-parent and non-default per-drop context are recorded in [08-decisions.md, B4 brief decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-brief-decisions); this worker did not execute them.
- F1–F9/R2, G1–G6 and H cleanup source-review evidence remains retained. H4-2 written procedure and its coverage limits were approved after Claude review on 4 October as recorded in the supplied decisions; tooling remains unbuilt/untested and R3 unrun. H3-4 `6b8331d` was not user-approved; accurate code-description correction and Git history remain.
- AU17 scope stays Contribution/readback only beyond AU17a; AU18 stays WOM outcome/version history only. No extra per-account Review context or current-event readiness UI.

## Next permitted action

Stop under brief53 after item4. Claude rechecks the named round 2 findings and
their direct consequences against the stable four-commit diff. The user checks
shell and Identity in light/dark, Danish/English and phone width, including
confirmations/errors/toasts. User/Claude runs the unfiltered final suite on the
reported final SHA (zero failed/zero skipped):

```sh
cd /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-u1-final-suite-trx --logger "trx"
```

No U2–U10, lane T, rehearsal, push, main merge or deployment. CI is unrun;
setup-node retains its immutable v4.4.0 commit pin.
