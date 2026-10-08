# U1 item2 — JS test runner in CI (A9): complete

Authority: [brief43](/Users/christopher/Documents/BingoWebpage/review-notes/43-codex-brief-u1-foundation-identity.md)
"Item 2"; test corrections under briefs [44](/Users/christopher/Documents/BingoWebpage/review-notes/44-u1-item2-test-correction.md)
and [46](/Users/christopher/Documents/BingoWebpage/review-notes/46-u1-item2-account-support-correction.md)
(brief32 §3); production fixes under briefs [45](/Users/christopher/Documents/BingoWebpage/review-notes/45-u1-item2-stale-close-fix.md)
(item2a, `0d8306c`) and [47](/Users/christopher/Documents/BingoWebpage/review-notes/47-u1-item2-transport-reason-fix.md)
(item2c, `63ce639`).

Implementer for this resume: Claude agent assigned by the user (the Codex
implementer was unavailable). Not an independent review.

## Runner result (gate)

```sh
NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules PLAYWRIGHT_CHANNEL=chrome BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/private/tmp/bingo-u1-js-fixtures /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node scripts/run-browser-tests.cjs
```

**JavaScript files: 37 passed, 0 failed, 37 total; runner exit code 0.** Run once at
`63ce639` with the item2 drafts in the working tree (the content of the item2
commit). Counts are files, not individual assertions. Per-file status and timing:
[item2-completion-results.json](item2-completion-results.json). The 37 files are the
36 original `tests/Bingo.BrowserTests/*.js` files plus the item2a regression
`admin-confirmation-dismiss.browser.js`.

Fixtures: the 8 controlled PostgreSQL HTML fixtures in `/private/tmp/bingo-u1-js-fixtures`
from the previous run (fixture generation 8 passed / 0 failed, see
[item2-js-runner-blocked.md](item2-js-runner-blocked.md)) were reused, not regenerated.

History of this gate: baseline 35/1 ([blocked](item2-js-runner-blocked.md)); after
brief44 correction, product finding ([brief44 correction](item2-brief44-correction.md));
after item2a 36/1 ([conflict](item2-account-support-conflict.md)); after brief46
correction 36/1 ([brief46 correction](item2-brief46-correction.md)); after item2c
37/0 (this file).

## Runner and CI job

- `package.json` (private, `packageManager: pnpm@11.25.0`, devDependency
  `playwright` 1.62.1 exact) with `test` / `test:js` → `node scripts/run-browser-tests.cjs`;
  `pnpm-lock.yaml` locks it.
- `scripts/run-browser-tests.cjs`: runs every `tests/Bingo.BrowserTests/*.js` file in
  its own Node process (120 s per-file limit), refuses to start without the eight
  account fixtures, writes each file's log and `results.json` to `artifacts/js-tests/`
  (ignored), prints a file summary and exits 1 if any file fails. Default channel
  `chromium`; `PLAYWRIGHT_CHANNEL` overrides.
- `.github/workflows/ci.yml` job `javascript-tests` ("JavaScript and Playwright
  tests", ubuntu-latest, 30 min, read-only contents): checkout, .NET from
  `global.json`, Node 22, pnpm 11.25.0 + frozen install, Playwright `chromium` and
  `chrome` with system deps (one legacy test selects Chrome), fixture generation via
  the filtered integration test, `pnpm test:js`, results uploaded as an artifact.
- `.gitignore`: `node_modules/`. `README.md`: "JavaScript and Playwright checks".
- **CI itself has not run**; the job is unverified on GitHub Actions. Local runs used
  the bundled Node/Playwright and installed Chrome.

## Test changes (brief32 §3)

| File | Before | After | Authority |
| --- | --- | --- | --- |
| `admin-stale-change.browser.js` | Old inline Disable/Restore confirmation and `<details>` role forms; fixture without the shared confirmation | Shared `_AdminConfirmation` partial + `admin-confirmation.js` loaded; all four actions open `[data-admin-confirmation]`; version read from the action form; shared reason/submit; adds shared-dialog-closed assertion, keeps the old inline/details zero-count; all POST/version/feedback/reconfirmation checks and 30 s waits unchanged | Brief44; user's ADM-01/ADM-02 shared confirmations (26 Sep 2026, `525d5d1`). Detail: [brief44 correction](item2-brief44-correction.md) |
| `account-support.browser.js` (scenario `:53-62`) | One Disable with `failure = true; stale = true`, modal kept open, manual Cancel, version 8 | Ordinary failure (`failure`, not stale): same feedback/body/modal/reason assertions, Cancel, version 7. Then stale (`stale`, not failure): one more POST with its reason, confirmation closes by itself, version 8, editor stale feedback, none in the closed modal. Fixture messages use `failure \|\| stale`; pending-POST count 3 → 4 | Brief46 applying brief45 (planner, within ADM-01/ADM-02). Detail: [brief46 correction](item2-brief46-correction.md) |
| `account-support.browser.js` (new final section) | — (added check) | After ordinary Disable failure + Cancel, no transport `Reason` remains, next Disable opens its own confirmation with a reason field; an edited field still opens the discard prompt | Brief47, committed in item2c. Detail: [item2c](item2c-transport-reason-fix.md) |

No check was deleted, skipped or weakened; no timeout changed.

## Other checks

- `git diff --check` on the staged item2 commit: clean.
- Full .NET suite: not run here (planner runs it on the final U1 commit, per the
  assignment).

## Status

Item2 implemented with its runner gate at 0 failed; awaiting Claude's independent
review. No visual acceptance claimed. Next: U1 item3 (not started).
