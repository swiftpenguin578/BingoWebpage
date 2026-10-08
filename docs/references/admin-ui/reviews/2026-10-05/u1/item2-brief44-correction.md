# U1 item2 — brief44 correction and exposed product blocker

Resume baseline: HEAD `7a5c15b67fe9034c4b5599f15d350bfd4c4e5bf5`, existing
item2 drafts intact. Prior 44 item1 passes and 8 fixture-generation passes reused.
No production changes, timeout changes, test retries or new workers.

## Exact test change / authority

Authority: [brief44](/Users/christopher/Documents/BingoWebpage/review-notes/44-u1-item2-test-correction.md)
and `08-decisions.md` “U1 execution”, naming the user's 26 September Admin
simplification ADM-01/ADM-02 shared-confirmation decision in DELIVERY_PLAN,
implemented by `525d5d1`. This is the brief32 §3 recorded-decision mapping.

| Before | After |
| --- | --- |
| Fixture shell has only editor guard and account-manage-dialog | Loads localized shared `_AdminConfirmation.cshtml` and admin-confirmation.js before both existing scripts |
| Disable/Restore use inline confirmation; role actions use details/summary | All four click their existing action trigger and wait for shared `[data-admin-confirmation]`; helper returns shared dialog plus action form |
| Freshness field read from old confirmation form | Exact expected version read from the action form submitted by the shared dialog |
| Disable reason input and old submit button | Shared reason textarea and `[data-admin-confirmation-action]` |
| Stale-closed assertion checks only obsolete inline/details nodes | Adds exact shared dialog `.open === false`; **keeps** the old zero-count inline/details assertion |
| Reconfirmation version read from obsolete form | Reads refreshed version from action form; exact not-equal and no-second-POST assertions retained |

All four actions remain in the loop. Zero pre-confirm requests, exactly one POST,
correct handler and multipart version, retained Manage dialog, visible stale
feedback, no duplicate summary, fresh reconfirmation and no page errors remain
unchanged. All existing waits remain unchanged at 30 seconds.

## Executed corrected proof — FAIL, product finding

```sh
NODE_PATH=/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules PLAYWRIGHT_CHANNEL=chrome BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/private/tmp/bingo-u1-js-fixtures /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/admin-stale-change.browser.js
```

One run, exit 1. First action **Disable** reaches the real shared dialog and posts,
then times out at test line50 waiting for the stale editor to be visible. The
locator repeatedly resolves the updated `data-account-change-stale="true"`
section, but it remains **hidden**. Later actions/assertions are retained but were
not reached; no passing claim for them. [Complete failure output](item2-brief44-corrected-failure.log).
The full runner was not repeated after this named failure. Its prior baseline
remains 35 files passed / 1 failed; it is not a corrected gate pass.

## Concrete source cause / next authority needed

`account-manage-dialog.js:310-314` replaces stale content, calls its finish,
then `closeConfirmation(false)`, intending to resume Manage. The latter calls
`window.adminConfirmation.cancel()` (`:75`). Shared `admin-confirmation.js:15-16`
refuses to finish while pending. Its submit handler sets pending before awaiting
`request.onConfirm` (`:51-53`); only afterwards does it clear pending (`:54`).
The account callback returns `{ succeeded: false, ... }` (`:316`), so the shared
handler displays failure feedback and leaves the confirmation open (`:55-59`).
Manage stays suspended/closed with the new stale content, matching the observed
hidden locator. This contradicts brief44's required stale-close/reopen flow.

**Stop required by brief44's last paragraph.** No production fix is authorized by
that brief. Recommend a narrowly authorized stale-result close/handoff after
pending clears, preserving pending dismissal protection and other failure drafts;
then rerun this exact test without weakening assertions. Claude must route the
named production correction. Do not infer authority to repair it here.

Item2, including the authorized test correction, remains uncommitted/incomplete;
items3–7 unstarted. `git diff --check` passed. Current status and dispatcher callback
record this checkpoint; no full suite, independent review or visual acceptance.
