# U1 item 2 — blocked on unchanged baseline

## Checkpoint

HEAD `7a5c15b67fe9034c4b5599f15d350bfd4c4e5bf5` on
`codex/participants-functionality`, assigned worktree. Prior item0 commit:
`1e5848d11661c9912a406908e56f0bbabc050a5c`.
Item2 is **uncommitted and incomplete**. Items3–7 have not started. No existing JS
test, assertion, timeout or production behavior was changed or retried.

Implemented draft: package.json + exact Playwright 1.62.1 pnpm lock, standalone
process runner for all 36 files, CI job (Node22, Chromium and legacy-test Chrome,
controlled HTML fixture generation), ignored node_modules, README commands.
`pnpm install --lockfile-only` passed; `git diff --check` passed. CI itself has not
run. Local execution used the bundled Node/Playwright and installed Chrome.

## Executed evidence

Fixture command:

```sh
BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/private/tmp/bingo-u1-js-fixtures dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction --logger 'trx;LogFileName=item2-fixtures.trx' --results-directory /private/tmp/bingo-u1-item2
```

**8 passed / 0 failed / 0 skipped** on controlled PostgreSQL fixtures.

Full JS runner command (repository root):

```sh
NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules PLAYWRIGHT_CHANNEL=chrome BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/private/tmp/bingo-u1-js-fixtures /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node scripts/run-browser-tests.cjs
```

**35 files passed / 1 failed / 36 total**, exit 1. These are file counts, not
individual Node assertions. All files ran once. Full per-file status/timing:
[item2-js-baseline-results.json](item2-js-baseline-results.json). The sole failure's
complete output is [item2-admin-stale-change-failure.log](item2-admin-stale-change-failure.log).
Other temporary outputs: `artifacts/js-tests/` (ignored); fixture TRX path above.

Failing file: `tests/Bingo.BrowserTests/admin-stale-change.browser.js`.
At `openConfirmation`, line42 (called line49), the existing 30-second Playwright
wait expires for `dialog.account-manage-route-dialog .admin-account-inline-confirmation`.
This is a browser fixture/selector failure, not evidence of a PostgreSQL timeout.

## One focused source lookup and recommended routing

Today: `account-manage-dialog.js:346-349` opens `window.adminConfirmation`.
The failing browser fixture's shell at line30 loads only admin-editor-guard and
account-manage-dialog; it omits the shared confirmation partial/script. The test
still expects inline confirmations for Disable/Restore and details for role
changes (lines37–47). The passing `account-support.browser.js:21` includes the
shared partial and admin-confirmation.js. This source evidence strongly suggests
a stale test fixture/selector contract; no repair has been attempted.

Brief43 item2 explicitly requires stopping/reporting a baseline failure. A10's
page-binding allowance does not authorize changing this Accounts test in U1.
Recommended next step: Claude names the governing shared-confirmation decision
and supplies a precise old→new fixture/selector change preserving every freshness,
POST count, retained-dialog, feedback and reconfirmation assertion under brief32 §3.
Alternative: if the current shared-modal behavior is not approved, route a named
production correction instead. Do not delete/skip/weaken the check or raise its
30-second timeout. Resume this same implementer at item2 only after the decision.

Whole-suite final gate remains pending user execution on the eventual final U1
commit; no final U1 SHA, full Release gate, independent review or visual acceptance
is claimed. The eventual unfiltered command is:
`dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-u1-final-suite-trx --logger "trx"`.
