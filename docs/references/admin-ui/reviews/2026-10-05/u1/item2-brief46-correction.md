# Item2 brief46 correction and new stop — 5 October 2026

HEAD remains `0d8306ca9a3e4cb08da4cbde567c399b699f6e60`; item2 drafts are uncommitted. Items3–7 are unstarted. The one user-approved retry succeeded through the unchanged automatic approval check. No approval bypass or production edit was used.

## Exact authority and before → after

Authority: planner brief46 (`/Users/christopher/Documents/BingoWebpage/review-notes/46-u1-item2-account-support-correction.md`), corresponding decisions08 item2b entry, and brief45. This is a planner decision within historical user ADM-01/ADM-02 approval, not a new user product approval.

- Before: one Disable request had `failure=true; stale=true`, required modal/reason preservation, then Cancel before reading version8. After: ordinary failure uses `failure=true; stale=false`, retains all feedback/body/modal/reason assertions, then Cancel resumes version7. The new stale scenario uses `failure=false; stale=true`, asserts one additional POST with its reason, automatic modal dismissal, version8 editor and server feedback without visible duplicate modal feedback. This separates ordinary failure from brief45 stale dismissal exactly as brief46 directs.
- The fixture validation/status fields now use `failure || stale` so the separated stale response supplies the required server stale text rather than success text.
- Pending POST count shifts from3 to4 for the added request. The original pending single-POST, Back protection, success toast/editor, transfer assertions and all waits remain unchanged. No old safety assertion was deleted or weakened. No selector parts were dropped in this correction.
- The brief44-corrected admin-stale test and production files are unchanged in this resume.

## Executed result

Full runner: **36 files passed / 1 failed / 37 total**, exit1. Command:

```sh
NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules PLAYWRIGHT_CHANNEL=chrome BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/private/tmp/bingo-u1-js-fixtures /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node scripts/run-browser-tests.cjs
```

Failure: `account-support.browser.js:67:37`, `locator.fill: Timeout 30000ms exceeded`, filling `Stale support request`: the shared modal textarea resolves but is not visible. Exact output: [failure](item2-brief46-failure.log); all file results: [results](item2-after-brief46-results.json). All ordinary-failure assertions through Cancel and version7 passed. The stale POST and subsequent pending/transfer assertions were not reached in this file; preserved assertions are not claimed as executed passes. All other36 files passed, including the corrected four-action stale test and item2a dismissal regression.

## Focused source finding and decision needed

Observed today: after ordinary failure and Cancel, the next Disable does not expose its reason field. One focused lookup indicates an intervening discard confirmation: account-manage-dialog.js adds hidden Reason to the original form during confirmation (lines369–370), retains the form after ordinary failure, and guards the next action with dirtyForms (lines24–26,152–154). admin-editor-guard.js compares FormData including Reason (line40) and opens the shared discard dialog (line61), which has no reason field. This is a source-based explanation; the runtime failure did not capture the dialog title.

Brief46 expects the next action to reach the stale confirmation directly. Options require planner authority: account for the discard transition in the test if intended, or authorize a named production correction to distinguish transport-only Reason from an unsaved editor draft. Recommendation: resolve that intended ordinary-failure→next-action behavior explicitly before changing either boundary. Do not reset/reload the fixture, bypass the guard, alter waits or silently expand production scope to get green.

Stopped with no further test or production changes and no runner retry. Item2 zero-failure gate remains unmet; no item2 commit or later item may proceed. Full .NET suite remains pending user/Claude execution at eventual final U1 SHA; independent review and visual acceptance are pending.
