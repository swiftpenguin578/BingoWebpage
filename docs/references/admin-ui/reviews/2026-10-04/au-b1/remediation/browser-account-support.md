# Account support browser rerun evidence

## Scope

This checkpoint records item 5 of `review-notes/21-codex-brief-b1-b2-remediation.md`, as required by the AU24 browser follow-up in `review-notes/20-b1-review.md` and `review-notes/20b-b1-au22-au24-review.md`.

## Rerun

Executed from the B1 checkout with the bundled Node runtime and Playwright module path:

```text
env NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/account-support.browser.js
```

Chromium launched and the fixture reached the account-management failure recovery step, but the process stopped with:

```text
AssertionError [ERR_ASSERTION]: stale values refresh behind confirmation
'7' !== '8'
at tests/Bingo.BrowserTests/account-support.browser.js:62:12
```

This is a **failed browser check**, not a pass. The failure's product-versus-fixture cause is not established in this rerun.

## Assertions that remain unverified

Because the fixture aborts before the transfer section, these AU24 assertions did not execute:

- the selected authorization version is copied into the submitted hidden field (`9`);
- the confirmation dialog shows the selected recipient;
- the typed destination name and current password survive cancelling the dialog;
- the completed browser flow reports no JavaScript errors.

No browser acceptance is claimed for these assertions. The remediation lane preserves the exact failure for the independent review and follow-up routing.

`git diff --check` and the Release web build are run before committing this documentation checkpoint.
