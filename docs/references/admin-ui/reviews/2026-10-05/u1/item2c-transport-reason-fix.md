# U1 item2c — transport-only Reason no longer marks the editor unsaved

Authority: [brief47](/Users/christopher/Documents/BingoWebpage/review-notes/47-u1-item2-transport-reason-fix.md),
planner technical decision within the user's ADM-01/ADM-02 approval (26 September
2026) and decision 45. Not a new user product approval.

## Before → after

- Before: the Disable confirmation's `onConfirm` appended a hidden `Reason` input to
  the Disable form to carry the typed reason in the POST and left it there. After an
  ordinary failure and Cancel, `admin-editor-guard.js` compared the form with its
  baseline, saw the extra field and treated the editor as unsaved, so the next action
  opened "Discard unsaved changes?" instead of its own confirmation. This is the
  failure recorded in [item2-brief46-correction.md](item2-brief46-correction.md)
  (`account-support.browser.js:67`, reason textarea not visible); reproduced again
  with the HEAD version of the production file: same `locator.fill` timeout, now at
  line 68.
- After: `account-manage-dialog.js` `openConfirmation` → `onConfirm` awaits
  `submitManage` and, when the result is not a success (`true` or
  `succeeded: true`), removes the hidden `Reason` input it created, or restores the
  previous value if the form already had one. Success and stale results replace the
  editor content anyway. `admin-editor-guard.js`, the baseline logic and every other
  caller are unchanged; real editor edits still trigger the discard prompt.

Production change: `src/Bingo.Web/wwwroot/js/account-manage-dialog.js` only.

## New check (brief47 "Proof")

Placed in `tests/Bingo.BrowserTests/account-support.browser.js` as a new final
scenario section ("Brief47"), after the transfer checks. The fixture gains an
`editable` flag (default `false`, so earlier scenarios render exactly as before)
that adds one plain text form field (`Note`) to the Manage fixture. The section:

1. Opens Manage, sends a Disable with reason "Transport reason" as an ordinary
   failure (exactly one POST carrying the reason), then Cancel.
2. Asserts no `Reason` input remains in the Disable form; the next Disable opens its
   own confirmation (title "Disable?") with a visible reason field, not the discard
   prompt. Cancel.
3. Edits the `Note` field; the next Disable opens "Discard unsaved changes?" (no
   reason field). Cancel keeps the edit. No further POSTs.

No existing assertion, wait or timeout was changed by this item. The brief46
correction of the same file's earlier scenarios is packaged in the item2 commit;
this commit contains only the new section and the fixture flag, so the file passes
in full only together with that correction.

## Executed (working tree with brief44/46 drafts + this fix)

Bundled Node, `PLAYWRIGHT_CHANNEL=chrome`, fixtures in `/private/tmp/bingo-u1-js-fixtures`:

- `account-support.browser.js` — PASS ("Account support Chromium checks passed.").
- `account-manage-dialog.test.js`, `admin-confirmation-dismiss.browser.js`,
  `admin-stale-change.browser.js` (all four actions) — PASS.
- With the HEAD (pre-fix) `account-manage-dialog.js`: `account-support.browser.js`
  exit 1, `locator.fill` timeout at line 68 (the next Disable never shows its reason
  field).

Full JS runner result: see [item2-completion.md](item2-completion.md).

Implemented checkpoint awaiting Claude's independent review; not visual acceptance.
