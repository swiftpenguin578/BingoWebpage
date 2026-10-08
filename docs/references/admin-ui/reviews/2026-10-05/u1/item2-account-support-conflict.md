# U1 item2 — existing test conflicts with the corrected stale contract

HEAD / item2a commit: `0d8306ca9a3e4cb08da4cbde567c399b699f6e60`.
The authorized brief45 production fix is committed separately before the runner,
as required. New shared-dismissal regression passes; the unchanged brief44-corrected
`admin-stale-change.browser.js` passes all four actions. Production scope stayed
within the two authorized files.

## Full runner result

After item2a, ran once from the assigned checkout:

```sh
NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules PLAYWRIGHT_CHANNEL=chrome BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/private/tmp/bingo-u1-js-fixtures /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node scripts/run-browser-tests.cjs
```

**36 files passed / 1 failed / 37 total**, exit 1. [Full file statuses](item2-after-brief45-results.json).
Sole failure: `account-support.browser.js:56`, unchanged 30-second wait for visible
shared confirmation feedback. [Complete output](item2-account-support-failure.log).
No retry, assertion change or timeout change.

## Exact conflict and recommended next action

`account-support.browser.js:53-54` sets both `failure = true` and `stale = true`.
Lines56–62 expect shared feedback, explicitly assert the modal stays visible with
reason `Support request`, manually cancel, then assert refreshed version `8` in
Manage. Those assertions passed on the original baseline because they expected
the stale-close defect that brief45 subsequently corrected. Brief45 instead
requires a stale response to dismiss without success and resume refreshed Manage;
ordinary failures must still retain the confirmation and its values.

This is an existing-test contract conflict, not a demonstrated production
regression: the exact corrected stale-flow test now passes all four actions.
Recommended precise follow-up: authorize separate ordinary-failure and stale
scenarios in account-support, retaining reason preservation for ordinary failure,
asserting close/resume/version8/visible feedback for stale, and preserving all
single-POST/body/anti-repeat/navigation/transfer checks. Record each changed
expectation against brief45/ADM-02 under brief32 §3. Do not weaken or drop checks.
No change to this test has been made pending that routing/authority.

Item2 runner, brief44 correction and their evidence remain uncommitted; items3–7
unstarted. Existing prior passes reused. `git diff --check` passed. Full final .NET
suite remains user/Claude's gate on the eventual final U1 commit; no acceptance
or independent review claim.
