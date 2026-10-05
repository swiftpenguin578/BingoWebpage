# U1 item2a — completed stale-result handoff

Authority: [brief45](/Users/christopher/Documents/BingoWebpage/review-notes/45-u1-item2-stale-close-fix.md), planner technical decision within the user's historical ADM-01/ADM-02 approval and stale-change contract (not new user product approval).

Before: the Accounts stale callback attempted to cancel the shared dialog while
its request was pending; cancellation was refused, leaving refreshed Manage
content hidden. After: the callback returns `{ succeeded: false, dismiss: true }`.
The shared owner clears pending, closes/resumes the editor and resolves false.
Accounts then focuses the refreshed editor and shows one inline stale message,
hiding duplicate validation markup. Non-shared handling is unchanged. Other
failures retain their open dialog and values; pending dismissal stays refused.
Production changes are exactly admin-confirmation.js and account-manage-dialog.js.

Executed against controlled browser fixtures with bundled Node/Playwright and
`PLAYWRIGHT_CHANNEL=chrome`, using the previously passing 8-case PostgreSQL HTML
fixture generation:

- `node tests/Bingo.BrowserTests/admin-confirmation-dismiss.browser.js` — PASS:
  completed dismissal closes/resumes/restores opener focus/resolves false;
  ordinary failure keeps its reason and feedback; pending API cancel, Escape and
  Back remain refused; no page errors.
- `node tests/Bingo.BrowserTests/admin-stale-change.browser.js` — PASS for
  **Disable, Restore, GrantAdmin, RevokeAdmin**; exact brief44-corrected test
  unchanged. Version, single POST, visible recovery and fresh reconfirmation
  assertions all pass. Its brief44 correction is packaged with item2.
- `git diff --check` — PASS.

Execution environment prefixes: `NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules`, `BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/private/tmp/bingo-u1-js-fixtures`; Node executable `/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.

The first mutation request was not executed because automatic approval review
reported model capacity. The user explicitly authorized one unchanged-check retry;
it succeeded. No bypass, model change or approval setting change occurred.

Implemented/checked checkpoint awaiting Claude review; not full-suite or visual
acceptance. Next: finish item2 full JS runner, separate runner commit, then U1 item3.
