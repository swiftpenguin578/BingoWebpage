# U9 batch gate summary (Claude implementer, 8 October 2026)

Branch `claude/u9-final-wom`, Codex items 0a-2b (head `eb19bd07`) plus the report-101 remediation and the merge of `codex/participants-functionality` (U5-U8, then U10 part 1 at `54459b65`).

## Report 101 findings (one commit each)

| Finding | Commit | Fix |
| --- | --- | --- |
| M1 | 39f83e93 | WOM Create/Delete are "applied" only for status Succeeded, the code for Valid/Unverified/Unchanged; every other successful status (Claimed) is "queued" with the queued wording. PostgreSQL test with a seeded Claimed Create. |
| M2 | 10a56b74 | C33 asserts the rendered `toast is-error` / `role="alert"` carrying the stale text and no status toast carrying it. Temporarily changing the expected text failed 4 tests. |
| L1 | 346d82af | The 1x1 fixture completes the board, so the Score reached cell (last cell) reads "At completion"/"Ved fuldførelse"; the dated value is in the Full board cell. Both cells are pinned separately. |
| L6 | 0123c707 | Hero "Next scheduled fetch" = `RetryDueAt ?? NormalDueAt`; eligibility stays in the Fetch-now reason line. |
| L2 | deb20e40 | Both scripts need `outcome` in applied/queued/skipped/failed/refused; otherwise uncertain, then readback. |
| L5 | 5b103f4f | WOM readback status comes from `operationPhase` (numeric or name) before `management.status`. |
| L4 | 9825df7e | Blank line removed; U9 rows stay in the register table. |
| L3 | cd2900a2 | `LocalizeConfigureError` (WOM Link/Disconnect; unknown/provider text becomes the localized fallback), `LocalizeRefusal` (Final review incl. the four U9-Q1 sentences with the event name as parameter), 25 Danish entries, new Danish source test. |
| extra | 3b7f1d0f | Two WOM page assertions that still matched retired markup (Create button, terminal wording) updated. |

## Merge resolutions
Conformance pages, templates, shell page kinds and classification rows keep both sides. Resx: no exact duplicates remained; "Open Board" took the U7 value; "None yet" (WOM) collided case-insensitively with U7 "none yet", so WOM uses `AdminDesign.None yet` (English and Danish entries). Legacy-shell assertions: moved from WiseOldMan to `/Admin/UiReferences` (Shell and Identity tests); the Remembered-event test now opens Schedule and drops its old-layout assertion.

## Checks (final head)
See the handoff message for counts.

Run on the post-U10 merge head (`9db5816d`):

- Release build of `Bingo.slnx` and the parity fixture: 0 warnings, 0 errors.
- Full JS runner, both engines: 125 executions passed, 0 failed. An earlier run on the U5-U8 merge, with another suite loading the machine, showed three failures (draft-running chromium, page-conformance chromium, WOM webkit); none reproduced on the final head.
- All-page conformance: Chromium and WebKit, 86 PASS lines each, exit 0.
- AdminDesignLocalizationTests: 8 passed.
- Integration (classification, U9 Final review/WOM, C33, test #14, CCmp, D16/D17, Shell and Identity, Create-visibility and terminal WOM assertions): 94 passed, 0 failed, 0 skipped.
- Before the second merge: Final and WOM browser scripts and Final/WOM conformance passed on Chromium and WebKit; AccountConfirmation fixture test 8 passed.
- No whole .NET suite was run.
