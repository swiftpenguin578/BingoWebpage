# AU09 independent review — changes required

Reviewer: /root/au09_reviewer, fresh read-only independent review. Return owner: /root orchestrator. Reviewed checkout `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`, packaged HEAD `0ec8add9314a65ab66751bf44bbb4f5195ff854f`. No repository files changed or user application/database actions performed.

## Required finding

**P2 — Freeze the expected text as submitted across the multipart boundary.**

Location: `src/Bingo.Web/wwwroot/js/event-identity.js`, lines 24–31 (canonical input capture at lines 8–11; multipart dispatch at line 230).

A Description or BuyInDescription textarea containing multiple lines supplies LF line endings to `new FormData(form)`. `createIdentityReadbackSession` trims and freezes that LF string. Native multipart serialization of this same FormData normalizes line endings to CRLF. The server's `EventIdentityValues.Canonical()` and `BingoEvent.UpdateIdentity` trim optional text but do not normalize embedded line endings, so the stored/current value contains CRLF. With an applied save whose response is lost, Check again compares the frozen LF value with that successfully submitted/stored CRLF value and reports Different indefinitely. No concurrent edit or unseen server merge is needed. This defeats the required matching-current-state outcome for ordinary multiline Identity text.

Executed isolated Chromium reproduction against the shipped session helper and native `Request` multipart serialization:

```json
{
  "expectedDescription": "First line\nSecond line",
  "submittedDescription": "First line\r\nSecond line",
  "state": "different"
}
```

Proof source: `/private/tmp/au09-review-20261002/newline-proof.js`. The read response is controlled using the exact serialized submitted value; no actual database/browser walkthrough is claimed. Server source inspection establishes that the inner CRLF is retained. The initial sandboxed browser launch failed; the identical isolated fixture ran successfully with approved elevated browser execution. No blocked action was bypassed.

Smallest required correction: align the dispatch-frozen intended tuple and untouched-field comparison with the values actually submitted across the existing form boundary, while preserving explicitly reviewed current values and original AU08 edit intent. Do not derive expectations from later readback or change AU08 domain/persistence semantics. Extend the focused transport proof to use a real multiline textarea and serialized POST value, and retain a genuinely different multiline-value case. Same implementer remediates; same reviewer rechecks this finding and its direct consequences.

## Approved scope mapping

- Delivered: existing Identity Current GET handler under existing Admin, visibility and lifecycle filters; no-store response with event ID and four complete canonical values only; no write/audit/receipt side effects.
- Delivered subject to the finding: immutable client expected tuple separate from original edit intent; reviewed Use current and observed untouched-field selection; full-value matching, Different and Unknown semantics; GET-only recovery after uncertain enhanced timezone save, with draft retention and mutation retry prevention.
- Delivered: narrow uncertain-result server marker, truthful English/Danish wording, AU09 authority promotion, focused PostgreSQL/authenticated HTTP and controlled transport evidence.
- Missing required behavior: correct matching-current-state result for normally submitted multiline text, as described above.
- No unmapped material additions, changed explicit non-goals, or unbudgeted tables/services/policies/jobs/migrations/frameworks identified. The Current handler reuses the approved existing page/read boundary. Existing AU08 write/conflict/timezone safeguards are not reimplemented by the patch.
- Full new-reference/ordinary-save frontend binding, conflict-choice UI, reload/navigation persistence, visual/manual acceptance and broader release gates remain explicitly deferred. These are not additional AU09 findings.

## Evidence and boundaries

Verified the complete stable AU09-only patch and all ten current source hashes against the supplied manifest. Reviewed applicable AGENTS, CURRENT_STATUS, DELIVERY_PLAN AU09/4.2.1, FUNCTIONAL_CONTRACTS 4.3, changed source/tests and directly affected confirmation/filter/domain boundaries. Prior AU01–AU08 were not rereviewed. Orchestrator CURRENT_STATUS ownership metadata and separately authorized UI_PAGE_MATRIX reference-acceptance metadata do not change the reviewed AU09 source.

Reused and inspected the implementation raw logs/TRX: seven distinct PostgreSQL/authenticated HTTP cases pass across the initial 6/7 run plus the corrected hidden-actor fixture 1/1. The retained first failure is a fixture FK error, not a passing run. Client readback transport and affected timezone fixture report PASS; Release build reports zero warnings/errors. No tests/build/full suite were rerun for general confidence. Read failure execution is controlled client transport, not a PostgreSQL outage test. Existing single-line fixture values do not exercise the reported multipart/newline defect.

Reviewed patch SHA-256: `e6c48332713c00aa500633465f8eb4f2e373bf5d0f91ffbc9cc79f866bc3254e`.
Reviewed ten-source manifest SHA-256: `dfd24551229569db1cb4ba7204f040242c22b786447db6851e868c6d08548456`.
Exact ten source hashes copied to `reviewed-source.sha256` alongside this report.

Verdict: changes required for the single P2 above. No other required finding identified. Technical review is not manual acceptance.
