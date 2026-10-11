# JS CI failure inventory (brief 165, item 1)

Branch `js-gate` from `9794b0c1`. Prepared 10 October 2026.

## Sources

- CI runs read with `gh run view --log` (read-only):
  - `38080198631`: latest `main` push, `9794b0c1`. This is the run the brief names.
  - Earlier runs, used to tell steady failures from intermittent ones. `38078668460` has only partial JS logs, so the counts below cover the 8 complete runs:
    - `38079409858`, `38078668460` and `38077513804` (`audit-docs-round` PR)
    - `38076308369` (PR)
    - `38061075958` and `38058295374` (`main`)
    - `38057598431` and `38056869544` (`october-live-readiness` PR)
- Local reproduction ran on Linux browsers. The official `mcr.microsoft.com/playwright:v1.62.1-noble` container served `run-server`. The tests connected to it from the host with `exposeNetwork: '<loopback>'`, so each test's own controlled fixture stayed on host loopback. To reproduce CI CPU contention, a second container used `--cpus=1`. All runs went through a scratch preload shim; no repository code changed. macOS Chromium and WebKit were also run for comparison.

## Summary

| # | Test [engine] | Latest main run | Failed in the 8 complete runs | Classification |
|---|---|---|---|---|
| 1 | `admin-design-page-conformance` [chromium] | FAIL | 8/8 | **Real product bug: Participants loading-skeleton toolbar wraps on classic-scrollbar browsers** |
| 2 | `admin-design-page-conformance` [webkit] | FAIL (120 s timeout) | 8/8 | Test runtime budget (single file is longer than the per-file limit on CI runners) |
| 3 | `admin-design-overview` [webkit] | FAIL | 8/8 | Test timing: two unsettled-state races. The investigation also turned up a separate **real product bug** in the Evidence codes dialog; see 3b |
| 4 | `admin-design-board-drawer` [webkit] | pass | 7/8 (also in the partial run `38078668460`) | Test timing (drawer entrance not settled before hit-testing) |
| 5 | `admin-design-wom-connection` [webkit] | pass | 6/8 | Test timing under CPU contention; exact race not yet pinned |
| 6 | `admin-design-draft-running` [webkit] | pass | 2/8 (one with a different error) | Linux WebKit environment: a navigation-cancelled fetch is reported as a page error |
| 7 | `admin-design-draft` [webkit] | pass | 1/8 | Same as 6 |
| 8 | `admin-design-board-preview` [chromium] | pass | 1/8 | Test timing (narrow-window measurement taken before relayout) |
| 9 | `admin-design-destination-header` [webkit] | pass | 0/8 | Not observed; brief reports it as intermittent; no evidence gathered |
| — | `chromium-live`/`webkit-*` "1 differences" (shard 3, older runs) | n/a | only before the `test:parity` removal | Gone: it came from the removed `test:parity` step, not a current JS file |

## 1. `admin-design-page-conformance` [chromium]: real product bug

**CI error, identical in every run:** `participants 1280 normal first yBeyondHeader: -46 vs 0` (`scripts/check-admin-page-conformance.cjs`).

**Reproduction:**
- Linux Chromium: fails the same way (`BINGO_CONFORMANCE_PAGES=participants`, `-46 vs 0`).
- macOS Chromium and WebKit: pass. `TZ=UTC` alone does not reproduce it.
- Linux WebKit: passes, consistent with WebKit using overlay scrollbars there.

**Root cause:**
- The Admin `.scroller` has `scrollbar-gutter: stable` (`admin-design-components.css:102`). On classic, non-overlay scrollbar platforms (Linux Chromium, and Windows browsers), the gutter takes about 15 px. At a 1280 px viewport that leaves a 949 px content width instead of 964 px.
- The Participants **loading** toolbar (`_AdminParticipantsLoadStates.cshtml`) needs 960.5 px on one row:
  - tabs: 429.8 px, because each `.tab-count` holds a 16.1 px number bar
  - right group: 518.7 px
  - gap: 12 px
- The **loaded** toolbar needs only 925.4 px, because the real counts are single digits at 7.4 px each.
- So at 949 px the skeleton toolbar wraps to two rows (82 px tall) and the loaded one does not (36 px tall). When the data arrives, the card jumps up 46 px.
- Measured on macOS Chromium at a 1265 px viewport, which matches a 1280 px viewport minus a classic scrollbar: skeleton toolbar `h: 82`, loaded `h: 36`.

**User impact:** on Windows and Linux browsers with classic scrollbars, at viewports roughly between the 1260 px breakpoint and about 1296 px, the Participants page shifts by 46 px when loading finishes. This breaks the Q-H1/Q-SK2 rule that loading and loaded geometry match. The test is correct to fail.

**Needs a planner decision. Not fixed here.** Possible directions:
- Make the skeleton count bar's reserved width match a real count, or make it the same width as the loaded count.
- Or let the toolbar wrap identically in both states.

## 2. `admin-design-page-conformance` [webkit]: test runtime budget

**CI error:** `FAIL ... (timed out after 120 s)`. Every run reaches `dashboard`, family 13 of 15, before the per-file limit in `scripts/run-browser-tests.cjs` kills it. The `events` and `draft` families never run on CI.

**Local timing (whole file):**

| Engine | macOS time | Result |
|---|---|---|
| WebKit | 75 s | pass |
| Chromium | 108 s | pass |

CI runners are slower. Chromium fails at Participants before it reaches the limit; with that bug fixed, it would also be close to or over 120 s.

**Classification:** test design and runtime. No assertion fails.

**Remedy for item 2:** split the file into families. `BINGO_CONFORMANCE_PAGES` already filters by family, so a few `admin-design-page-conformance-*.browser.js` entry points each fit the limit and shard independently. No assertion changes.

## 3. `admin-design-overview` [webkit]: test timing, plus one product bug found on the way

**CI error, all 8 complete runs:** `page.waitForFunction: Timeout 30000ms exceeded` at line 125. This is `await page.keyboard.press('Escape');await page.waitForFunction(()=>!document.querySelector('.dtp-pop'))`.

**Reproduction:**
- Linux WebKit: fails, intermittently at line 125 and at line 119.
- macOS WebKit and Linux Chromium: pass.

**Root cause (a), line 125:**
- Picking a trailing-month day (`button.click()` on "11", in July) re-renders the grid into July. The focused June day is removed, so focus falls to `<body>`.
- The picker moves focus to the hour column on the **next animation frame** (`focusSoon` in `admin-date-time.js`).
- Linux WebKit's frame cadence leaves that one-frame window open long enough for the test's Escape to land on `<body>`. The picker's Escape handler sits on the popup and inputs, so the popup stays open.
- Trace: active element right after the pick is `BODY`; on the following frame it is `code-from-h-12`.
- No user can press a key inside one frame, so this is a test timing problem.
- Remedy: wait until focus is inside `.dtp-pop` before pressing Escape.

**Root cause (b), line 119:**
- Closing the dialog after a saved toggle starts an in-place `refresh()` of the Overview, and the test reopens the dialog while that refresh is still running.
- Adding a wait for the refresh to settle removed every line-119 failure in 4 runs.
- The refresh signals that it has settled by focusing `#now-title`.
- Remedy: wait for that state before reopening.

### 3b. Real product bug: Evidence codes dialog reports unsaved changes when untouched

Found while tracing 3. The current tests do not assert this behaviour, so it is not a cause of the CI failure.

**Reproduction:** on an event whose evidence codes are already enabled, open **Evidence codes** and immediately press Escape or click **Close** without touching anything. A **"Discard unsaved changes?"** prompt appears. It reproduces on macOS Chromium and on WebKit.

**Cause:**
- In `openCodes` (`admin-overview.js`), `layer.markClean()` runs right after `mountDateTime(...)`. At that moment `#codes-enabled` still has its default `checked = false`.
- The closing `paint()` then sets `toggle.checked = true` from `state.codes.enabled`.
- So the baseline differs from what is shown, and the dialog counts as dirty.
- Field values at open and at close are identical (`codes-enabled=true; code-from-date=3 Jun 2027; code-from-time=12:00; ...`).

**Needs a planner decision** on whether to fix it in this batch.

## 4. `admin-design-board-drawer` [webkit]: test timing

**CI error:** `TypeError: undefined is not an object (evaluating 'document.elementsFromPoint(x, y)[0].closest')` in `footState`, at line 159, the reopened drawer after a save.

**Reproduction:** Linux WebKit 1 of 5 runs. On failure, a probe taken a few milliseconds later finds the buttons inside the viewport and hit-testable.

**Root cause:** the test reads the footer geometry as soon as `#ed-save` exists, while the reopened drawer is still entering. The entrance transform puts the button points outside the viewport, so `elementsFromPoint` returns nothing.

**Remedy:** wait for the drawer's animations to finish, using the same `getAnimations().finished` pattern `board-preview` already uses, before `footState()`.

## 5. `admin-design-wom-connection` [webkit]: test timing under CPU contention

**CI error:** `page.waitForFunction: Timeout 30000ms exceeded` at line 14, column 96. After a real adopt, the dialog never closes and the "stored protected" notice never appears.

**Reproduction:**

| Environment | Result |
|---|---|
| Linux WebKit, normal CPU | 0/4 failures |
| Linux WebKit, `--cpus=1` | 2/2 failures (once at line 14 like CI, once at line 12: unsent code empty after reload) |
| Linux WebKit, `--cpus=1`, instrumented | 0/4 failures |

**Classification:** timing-sensitive test. No product defect identified: the draft is written to `sessionStorage` synchronously on `input` and is present after reload. The exact race was not pinned in item 1; item 2 must pin it before fixing. The brief says this test has never passed on Linux.

## 6–7. `admin-design-draft-running` / `admin-design-draft` [webkit]: Linux WebKit environment

**CI error:** `no page errors` fails with `'/127.0.0.1:<port>/Admin/Events/Draft/<id>?handler=State due to access control checks.'`.

**Reproduction:** Linux WebKit, `draft-running` 1 of 3 runs.

**Trace:** the page error, whose full text is `Fetch API cannot load http://127.0.0.1:…?handler=State due to access control checks.`, fires about 30 ms before the test's next `page.goto`. The draft page's periodic `?handler=State` read is in flight when the navigation cancels it.

**Not a product exception:** `AdminFetch.request` catches fetch rejections (`admin-design-fetch.js:60-61`). Linux WebKit reports the cancelled load itself as a page error; macOS WebKit does not.

**Options (planner decision):**
- (a) Test: before each `page.goto` away from a running draft, wait until no `?handler=State` request is in flight.
- (b) Product: abort the polling read on `pagehide`. That is a product change.

A narrow skip filter for this exact message would loosen the "no page errors" assertion, so it is not proposed.

**Separately:** run `38061075958` had a one-off `draft-running` [webkit] timeout waiting for the "Finalize the draft?" modal, at line 159. It was not seen again and needs another look if it recurs.

## 8. `admin-design-board-preview` [chromium]: test timing

**CI error (run `38079409858` only):** `board keeps the team board minimum width and scrolls sideways {"width":617.19,…,"scrolls":false,"body":1038,"panel":1040,"vw":390}`.

**Root cause:** after `setViewportSize({width: 390})`, the test waits two animation frames. The modal panel still measured 1040 px, the pre-resize width, so the measurement came before relayout.

**Reproduction:** passed on Linux Chromium 1/1.

**Remedy:** wait until the panel width has settled to the narrow viewport before measuring.

## 9. `admin-design-destination-header` [webkit]

- No failures in the 8 complete CI runs scanned.
- Passed 1/1 on Linux WebKit.
- Listed in the brief as intermittent. No root cause can be stated without a failing log.

## Stop point

Item 1 found **two real product bugs**:
- 1: Participants skeleton toolbar wrap, 46 px shift on classic-scrollbar browsers.
- 3b: Evidence codes dialog falsely dirty.

Per the brief, work stops here for a planner decision before item 2.
