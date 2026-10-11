# JS gate evidence (brief 171)

Linux runs use the official `mcr.microsoft.com/playwright:v1.62.1-noble` image as a `run-server`; the test connects with `exposeNetwork: '<loopback>'` so its controlled fixture stays on host loopback, and its launch options (including `channel: 'chromium'`, as in CI) are forwarded with the `x-playwright-launch-options` header (scratch preload shim, no repository change). Without the channel the server starts the headless shell, whose text metrics differ from CI (the Board header actions then wrap at 494 px, which CI does not show). macOS runs use the local Playwright browsers.

## J1: Participants loading toolbar wraps like the loaded toolbar

Change: in the loading template only, each tab-count bar is sized by a hidden `0` (one tabular digit, same font as the loaded count) instead of `2ch`. Loaded page unchanged.

Toolbar height, loading clone vs loaded, fixture `autumn-bingo-2027` (all counts single-digit), Linux Chromium (classic scrollbar, content = viewport - 331 px):

| Viewport (every 5 px) | Before: loading / loaded | After: loading / loaded |
|---|---|---|
| 1260–1290 | 82 / 36 | 36 / 36 |
| 1295–1300 | 36 / 36 | 36 / 36 |

Tab group width (headless-shell probe): before 436 vs 400 px (Chromium), 429.78 vs 394.66 px (WebKit); after equal in both (count box 7.36 px = loaded digit in WebKit). Linux WebKit (overlay scrollbar) 1260–1300: 36 / 36 before and after.

`BINGO_CONFORMANCE_PAGES=participants admin-design-page-conformance`: before, Linux Chromium fails `participants 1280 normal first yBeyondHeader: -46 vs 0`; after, passes on Linux Chromium, Linux WebKit, macOS Chromium, macOS WebKit. `admin-design-participants` passes on macOS Chromium and WebKit.

Limitation: the skeleton reserves one digit per tab. An event whose real counts have more digits has a wider loaded toolbar; within the few pixels that adds, the loaded toolbar can still wrap where the loading one did not.

## J2: Evidence codes dialog is clean when opened

Change: `openCodes` (`admin-overview.js`) takes the unsaved-changes baseline after the first `paint()`, which sets `#codes-enabled` from the saved state; previously it was taken before, with the checkbox still unchecked.

Test (`admin-design-overview`): on enabled codes, opening and closing untouched by Escape and by Close shows no "Discard unsaved changes?"; a typed code then Close shows it. Without the fix the test fails (`untouched codes dialog closed by Escape without a discard prompt`, macOS Chromium). With the fix it passes on macOS Chromium and WebKit.
