# U2 brief72 item1 — Events results-only shared update

Authorized clean start05f95d844adeba18e8e7fd8854e040315c96a03e,
codex/participants-functionality. Implementer evidence, not self-review or
independent approval. No workers/new worktree/branch/publication.

## Changes / exact ownership

- src/Bingo.Web/wwwroot/js/admin-design-shell.js: reusable update read primitive,
  C-CMP-2 AdminFetch classification with actual current input in the session
  notice; shared delayedLoading150/400 settings (no page timing/fetch copy);
  dirty guard, abort of superseded reads, replace-state commit only after success,
  no A16 region swap/module disposal. Shared focus snapshot now captures selection
  and an aria-label selector for replaced pager controls. Reversible loading body
  reserves measured height so it cannot clamp scroll merely because of a shorter
  skeleton. Optional scrollRegions retain table horizontal scroll. Failed reads
  keep old URL and display results-only Try again; missing focused control falls
  back to the results heading.
- src/Bingo.Web/wwwroot/js/admin-events.js: page adapter supplies server response
  fragments only: rows/empty state/footer/chips/banners, current sort state.
  Header, summary, tabs/counts, toolbar/search, phase/sort controls and table
  scroller remain connected. Summary action itself also remains connected.
  Values refresh in place from the response; input value is never overwritten,
  even when the server normalizes the query. Search edit revisions reject stale
  responses,250ms debounce settles one read; a new settled read aborts the old one.
  Create query retained. Hint placement/overflow/disposal retained.
- src/Bingo.Web/Pages/Admin/Events/Index.cshtml: small fragment markers and stable
  sort IDs; Clear remains the same node, hidden when empty. No layout redesign.
- src/Bingo.Web/wwwroot/css/admin-design-events.css: one display:contents fragment
  wrapper adapter; no shared component/token edit.
- DELIVERY_PLAN.md: Events A16 per-page results-only rule, kept summary/tab counts,
  interactive query controls and single shared loading settings.
- tests/Bingo.BrowserTests/admin-design-events-update.browser.js: real
  authenticated Razor/owned PostgreSQL fixture, paused browser clock and response
  bodies obtained from the actual server; both engines via full runner discovery.
- this evidence.

## Checks

Initial owned fixture Release0 warnings/errors4.01s. Final serial fixture refresh
0 warnings/errors0.80s; refresh necessary after HTTP project rebuild to align
fingerprinted asset manifests. No source/environment retry workaround added.
Unchanged U2EventsDirectoryHttpTests real PG/HTTP1 passed/0 failed/0 skipped,
1s; TRX tests/Bingo.BrowserTests/TestResults/u2-rem2-item1-http.trx.

Existing rendered Events runner:26 passed/0 failed Chromium/WebKit,0 differences
including both themes390/1440, query-bound A16 loading/failure composition and
retained hidden/attention/culture behavior. Exact generated JSON/PNGs remain in
artifacts/u2-events. Shared shell standalone PASS both engines (guards/layers/
menus/focus/Back/Forward/disposal/busy/failure/fallback). Frozen CSS cmp exit0,
git diff --check exit0; Node syntax exit0.

Final exact-clock proof: PASS Chromium and WebKit,11 connected cases per engine
(2search timing,5action controls,scroll,superseded typing,failure/retry,session).
 No sleeps,
approximate timing tolerances or failed-test retries. Clock paused only after
fonts/styles/module readiness, so timing assertions exclude fixture startup.
Fast search:249ms debounce0requests;250ms1request;149ms later no skeleton,
fulfil and atomic update. Slow search:150ms pending appearance, response at151,
still shown399ms later, removal exactly at400ms. Header/summary/tabs/toolbar/
input/scroller identity and visibility observed on every DOM mutation. Selection
1..4 backward and full alpha text remain; one settled request, C-CMP-2 header.
View/phase/sort/page/attention actions preserve the starting control or the exact
aria-label replacement pager control.390px vertical100/horizontal140 (nonzero
actual values asserted) survive a slow filter update. Mid-request appended X
survives; two settled queries produce two requests, first aborted, only final
alphaX applied. Failure/retry preserves URL/retained nodes; keyboard-focused
removed Retry falls back to first results heading. Lost session preserves input
and URL and opens the existing alertdialog, no region swap.

Authoring checkpoints, not passes: session fixture used dialog instead of the
actual shared alertdialog; WebKit scroll measured before page CSS readiness;
stale fingerprint manifest after another .NET build; WebKit pointer Retry did
not take keyboard focus. Corrected test setup/manifests, keeping exact assertions.
The initial fast/slow/control/scroll proof passed both engines before added
failure/session cases. Replaced source remains the same controlled fixture,
not a mock implementation.

Item2 separately owns shown-skeleton handover/minimum/cancel/Back behavior and
additional fake-clock cases; not silently claimed covered here. Whole batch gates,
item8 concurrency and final-SHA suite remain required. Acceptance remains awaiting
Claude review, then user visual acceptance. Item1 is ready for its scoped checkpoint after the planner's phase-focus ruling; items2–8
have not started. No whole-suite/concurrent Integration run claimed.

## Phase focus ruling — 7 October 2026

Planner corrected brief72 and 08-decisions: match Events.dc.html:834.
Phase choice closes the menu and restores the same Phase opener before the shared
read; that button remains focused during and after the update. No reference
exception. Shell exposes its existing closeMenu helper; Events uses it on phase
choice. The paired exact fake-clock test covers closure and retained opener focus
through the150/400 slow-results hold, in addition to all prior cases.

Affected rerun: Chromium PASS; WebKit PASS. Fixture Release rebuild0 warnings/errors
(0.78s). Prior item1 scoped evidence above is retained; final batch gates and
item2 superseding-skeleton cases remain required. Awaiting independent Claude
review, then user visual acceptance; not self-reviewed or manually accepted.
