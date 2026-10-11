# JS gate evidence (brief 171)

Linux runs use the official `mcr.microsoft.com/playwright:v1.62.1-noble` image as a `run-server`; the test connects with `exposeNetwork: '<loopback>'` so its controlled fixture stays on host loopback (scratch preload shim, no repository change). macOS runs use the local Playwright browsers.

## J1: Participants loading toolbar wraps like the loaded toolbar

Change: in the loading template only, each tab-count bar is sized by a hidden `0` (one tabular digit, same font as the loaded count) instead of `2ch`. Loaded page unchanged.

Toolbar height, loading clone vs loaded, fixture `autumn-bingo-2027` (all counts single-digit), Linux Chromium (classic scrollbar, content = viewport - 331 px):

| Viewport | Before: loading / loaded | After: loading / loaded |
|---|---|---|
| 1260 | 82 / 82 | 82 / 82 |
| 1265–1300 (every 5 px) | 82 / 36 | 36 / 36 |

Tab group width: before 436 vs 400 px (Chromium), 429.78 vs 394.66 px (WebKit); after equal in both (count box 7.36 px = loaded digit in WebKit). Linux WebKit (overlay scrollbar) 1260–1300: 36 / 36 before and after.

`BINGO_CONFORMANCE_PAGES=participants admin-design-page-conformance`: before, Linux Chromium fails `participants 1280 normal first yBeyondHeader: -46 vs 0`; after, passes on Linux Chromium, Linux WebKit, macOS Chromium, macOS WebKit. `admin-design-participants` passes on macOS Chromium and WebKit.

Limitation: the skeleton reserves one digit per tab. An event whose real counts have more digits has a wider loaded toolbar; within the few pixels that adds, the loaded toolbar can still wrap where the loading one did not.
