# Account support browser rerun evidence

## Scope

This checkpoint records item 5 of `review-notes/21-codex-brief-b1-b2-remediation.md`, as required by the AU24 browser follow-up in `review-notes/20-b1-review.md` and `review-notes/20b-b1-au22-au24-review.md`.

## R1 follow-up and rerun

The account-manage response guard now accepts a closed editor only while the
same shared confirmation remains active. A newer request still invalidates the
response by request ID, and a closed editor without that confirmation still
discards the late answer.

Executed from the B1 checkout with the bundled Node runtime and Playwright module path:

```text
env NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/account-support.browser.js
```

Result: **passed**. Chromium reached all account-management recovery and transfer
assertions, including the four AU24 checks that were previously unverified:

- the selected authorization version is copied into the submitted hidden field (`9`);
- the confirmation dialog shows the selected recipient;
- the typed destination name and current password survive cancelling the dialog;
- the completed browser flow reports no JavaScript errors.

The focused Node dialog test also exited 0. `git diff --check` passed for this
checkpoint. The Release web build is recorded with the second B1 item so the
lane has one final build gate after all authorized source changes.
