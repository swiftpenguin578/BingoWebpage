# U2 brief70 remediation item6 — shared loading timing

Changed: `admin-design-shell.js`, `DELIVERY_PLAN.md`,
`tests/Bingo.BrowserTests/admin-design-loading.browser.js`,
`admin-design-shell.browser.js`, its extracted shared fixture
`fixtures/admin-design-shell-fixture.cjs`, and this evidence.

One shared delayed-loading lifecycle with `LOADING_DELAY_MS=150` and
`LOADING_MINIMUM_MS=400`, next to the named unchanged busy600/quick250 minima.
Navigation and Events tab/filter/sort/page updates all use this shell path.
Dashboard's synchronous server-order row replacement is atomic below threshold;
no asynchronous loading to show. Current DOM stays visibly in place during the
delay and is temporarily inert so an already-guarded navigation cannot discard
new input. Delay/hold cancel on abort; old module stays undisposed until compatible
HTML/imports and the hold are complete. Failure states and dirty/address-bar
rules remain: fast failure shows only failed-load UI; a shown slow skeleton holds
its minimum before failure. Language swap stays on its accepted no-skeleton path.
No page-local timing, cache or remembered data.

Executed checks:

- Exact paused fake-clock proof in **Chromium and WebKit**,12 cases each:
  push and replace modes, response completion0/149/151/549/550/650ms.
  Before150 current DOM is visible and URL unchanged; pending appearance exactly
 150; after appearance remains through549 (399ms), disappears exactly550
  (400ms) or at650 for later completion. Fast0/149 has no transient insertion,
  verified by MutationObserver. Both engines **PASS**,24 exact cases total.
  No sleeps, retries or approximate timing tolerances. Bounded CDP drain awaits
  native import/microtasks with the elapsed clock frozen.
- Added pre-threshold cancellation at100ms, subsequent navigation successful,
  then550ms clock advance: stale timer cannot insert a skeleton or leave inert
  content. Fast failure retains URL/retry/failure and has no .sk. Both PASS.
- Shared shell standalone Chromium/WebKit **PASS**: focus/layers/dirty guards,
  reversible overlay, Back/Forward, failure/fallback, disposal and busy unchanged.
- Paired real PostgreSQL/Razor Dashboard runner **18 passed,0 failed**; all
  loaded390/860/1280/reference/theme and approved loading positions0 differences.
  Paired Events runner **26 passed,0 failed**, loading/query positions and failed
  composition0 differences. Durable executable scripts; generated exact JSON/PNG
  in `artifacts/u2-dashboard/` and `artifacts/u2-events/`.
- `git diff --check`, Node syntax: exit0. Frozen CSS untouched.

The general suite runner discovers the new `admin-design-` timing test and runs
it in both engines. No new product question. Timing is implemented for the user's
visual trial/tuning, not independently reviewed or manually accepted.
