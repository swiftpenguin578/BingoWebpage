# Brief76 item4 — preserved pre-Q-SK2 question/measurements

Resolved by user Q-SK2(a), 7 October2026: first body block means the already
aligned headline card. Reference bars/gaps stay; real one-line text-height rows
are reserved in family CSS. Only additional label/note wrapping may move the
participation section. Applies to text-line skeletons across all three pages.
The measurements and NOT-RUN checkpoint below are historical, not final results;
see item4-text-line-rows.md and final-gates.md for the continuation.

Committed parent d66422a0e3caa1465a08bd73b11c343dce06a429. No production
item4 edit or item4 commit. Items1–3 remain committed; changed-files.json records
their exact SHAs and per-item inventory.

## Exact question for the planner

Does item4 authorize changing the Dashboard headline-stat skeleton's intrinsic
height away from Dashboard.dc.html:130–139, to align the following participation
section with the loaded section? If so, should it match the loaded one-line
statistic baseline, while permitting additional data-dependent label/note wrapping?
Alternatively, does "first body block" mean the headline card itself, which
already matches at all requested widths?

This is a reference difference, not a stylesheet-readiness failure. Brief76
requires first section/card and table starts to match, while allowing
data-dependent heights below. The frozen reference itself moves the participation
section because its loading headline card is shorter than the loaded headline.
Changing that card needs an explicit interpretation/register rule; no fixed
position, remembered data or silent reference exception has been introduced.

## Executed measurements, not a passing item4 gate

Owned PostgreSQL/Kestrel fixture, actual shell navigation and family CSS;
paused fake clock150ms and remaining400ms hold; all three pages at
390/494/860/1280/1440, Chromium and WebKit. Fifteen paired measurements per
engine; both diagnostic commands exited0. Diagnostic mode deliberately records
differences rather than asserting the proposed body invariant. Strict mode retains
the section comparison and is not claimed to pass.

Durable complete rectangles: item4-positions-chromium.json and
item4-positions-webkit.json, including actual and frozen reference loading/loaded.
Script: scripts/check-u2-body.cjs; runner wrapper:
tests/Bingo.BrowserTests/admin-design-body-geometry.browser.js.
Invocation (both engines separately):

```sh
BINGO_BODY_DIAGNOSTIC=1 PLAYWRIGHT_BROWSER=chromium /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/admin-design-body-geometry.browser.js
BINGO_BODY_DIAGNOSTIC=1 PLAYWRIGHT_BROWSER=webkit /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/admin-design-body-geometry.browser.js
```

Both engines give the same following-section displacement, after subtracting
the allowed header-height change (pixels):

| Width | Actual Dashboard participation section | Frozen reference section |
| --- | ---: | ---: |
| 390 | 101 | 119.125 |
| 494 | 30.21875 | 66.21875 |
| 860 | 30.21875 | 30.21875 |
| 1280 | 15.109375 | 15.109375 |
| 1440 | 15.109375 | 15.109375 |

The actual first headline card x/y/width differences are0 after header growth
at all five widths. Events toolbar/card/table differences are0; Identity first
form-card differences are0. Dashboard header growth is19.5625px at390, otherwise0;
Events growth23.5625px at390, otherwise0; Identity0.

Dashboard.dc.html:132 uses skeleton label/value/note boxes8/20/8px and12px
top margins. Frozen ui/components.css:463–475 uses real statistic typography,
6/7px margins and a17px minimum note line. The one-row intrinsic difference is
15.109375px, doubled at two-row widths. Additional text wrapping differs between
reference sample data and the UR fixture. Thus the measured ~30px discrepancy
is reproduced, but it also exists in the reference skeleton.

The reference has no Dashboard history-table skeleton; the table-top comparison
is available for Events. No extra Dashboard skeleton composition was invented.

## Checkpoint and limitations

- Items1–3 checks/results/TRX paths are in their respective evidence files.
- Checkpoint git diff --check, unchanged frozen tokens/components/reference HTML,
  and Node syntax for the shell/body script/wrapper all exit0.
- The final clean Release, full JS runner, combined rendered checks and design
  checks1–6 are NOT RUN at this incomplete item4 boundary. Per-item fixture Release
  builds passed0 warnings/errors; these are not a final clean-build claim.
- Whole .NET suite NOT RUN under Q-S1; planner owns it. No whole-suite TRX/pass.
- No independent review or visual acceptance claimed. Dashboard/Events remain
  awaiting Claude review, then user visual acceptance. User review app/database
  untouched; no push/merge/deploy/workers.

Next permitted action: planner resolves the headline skeleton/reference question;
then the same implementer finishes item4, executes focused final gates, commits
item4 and updates the stop-boundary handoff.
