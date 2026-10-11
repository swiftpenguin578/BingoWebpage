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

## Item 2 (brief 165): test-side fixes (partial: stopped on a new product bug)

| Inventory | Fix | Failing engine | Result after fix |
|---|---|---|---|
| §2 page-conformance runtime | Five entries `admin-design-page-conformance-1..5.browser.js` set `BINGO_CONFORMANCE_PAGES`; `admin-conformance-blocks.source.js` asserts every registered family is in exactly one entry; output folder per entry; timings estimated (local Linux time x 1.5) | WebKit (timeout), Chromium (J1) | each entry 1/1 on Linux Chromium and Linux WebKit; slowest entry 41 s (entry 2, Linux WebKit) against the 120 s limit |
| §3 overview | wait for the Overview refresh to settle (`#now-title` focused) before reopening; wait for focus inside `.dtp-pop` before Escape; Escape in the picker must leave the dialog open | Linux WebKit | 5/5 Linux WebKit (previous test version: 1 of 3 failed at the Escape wait) |
| §4 board-drawer | wait for the reopened drawer's finite animations before hit-testing the footer | Linux WebKit | 5/5 Linux WebKit |
| §5 wom-connection | pinned below; wait until the session-loss layer is detached before typing the new code | Linux WebKit | 5/5 Linux WebKit, 2/2 Linux WebKit at `--cpus=1` |
| §6–7 draft, draft-running (J3 c) | navigate away only when no `?handler=State` request is in flight | Linux WebKit | `draft` 5/5; `draft-running` 3/5 (see below) |
| §8 board-preview | after resizing to 390 px wait until the panel fits the viewport and finite animations finish (was two frames) | Linux Chromium | 5/5 Linux Chromium |

### §5 pinned race

Every CI failure is line 14 (`waitForFunction` after the real adopt). After "Keep editing" closes the session-loss dialog, that layer stays in the layer stack until its exit animation ends (`afterExit`, a timer of the animation duration). The test types the new code and submits at once; when the adopt answer arrives before the session-loss layer is popped, the code dialog's `layer.close()` is refused because it is not the top layer, so the dialog stays open. Proof: with the exit animation stretched to 1.5 s (scratch copy, `.modal.is-closing{animation-duration:1500ms}`), the old test fails at that line every time (macOS WebKit, `U9_WOM_VARIANTS=base`); with the wait it passes. A person cannot type a code and submit within the exit animation, so this is not treated as a product bug. The one local line-12 failure noted in item 1 (unsent code empty after reload) did not recur in 7 runs at `--cpus=1`/`0.5`; not pinned.

### Stop: new product bug in the Draft page (no fix made)

`draft-running` [Linux WebKit] failed 2 of 5 runs after the J3 change:
- 1 run: `no page errors` again (`?handler=State due to access control checks`): a State read that starts after the in-flight check (for example the 150 ms debounced realtime re-read) is still cancelled by the navigation. The test-side wait narrows the window but cannot close it; only the product fix in BACKLOG (abort the read on leave) removes it.
- 1 run: `an own release is not reported as a lapse` (line 154). **Product bug:** after **Release control**, the page can show "Your control lapsed." In `admin-draft.js`, `run()` re-reads with `L.own = 'release'` and then clears `L.own`; the realtime `draftChanged` re-read (150 ms debounce, allowed once `L.pending` is cleared) can start while that re-read is in flight, captures `before.who === 'me'`, and finishes after `L.own` was cleared, so `refresh()` treats the change as a lapse. Deterministic reproduction (scratch copy of the test): delay `?handler=State` responses by 400 ms around the release; the banner "Your control lapsed. …" appears in 2/2 runs (macOS Chromium). A slow connection on draft day can show it to a real admin.

Note: the removal of the old single entry `admin-design-page-conformance.browser.js` landed by mistake in commit `822591d1` (J1 evidence); it belongs to this item, whose commit adds the five entries.
