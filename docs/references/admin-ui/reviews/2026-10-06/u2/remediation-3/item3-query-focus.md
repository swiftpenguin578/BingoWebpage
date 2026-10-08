# Brief74 item3 — one query state and pager focus

Parentaa74562. Implementer evidence only.

Events keeps one query initialized from the canonical server state and updated
to each latest requested intent, then to the current response's canonical state.
All control paths compose their own changed fields from it and the search input
as typed now. Tab change and click paths share controlNow; every action cancels
debounce. Search keeps the pending view/sort and retains the reference's existing
reset-to-page1 rule. Newer reads abort older reads and edits still invalidate stale
responses; typed input is never replaced by a response.

Phase eligibility is emitted from the existing server PhaseAllowed predicate
(public for Razor reuse, no rule change) as data-phase-views. View composition
keeps an eligible latest phase or resets to all, without another JS copy of the
phase rules. Sorting uses the existing server asc/desc toggle. Attention, chips,
clear/default order, page and partial-information retry share the same query.

Shared preserve treats a disabled/hidden/inert replacement as unfocusable.
Events fallback: the opposite pager arrow if enabled, otherwise first results
heading. Existing focus/caret/selection/scroll restoration remains intact.

Checks:
- Affected real PG/HTTP U2EventsDirectoryHttpTests:1 passed/0 failed/0 skipped,
  duration1s. TRX:
  tests/Bingo.BrowserTests/TestResults/u2-rem3-item3-http.trx.
  Command: dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj
  --configuration Release --no-restore --filter FullyQualifiedName~U2EventsDirectoryHttpTests
  --logger 'trx;LogFileName=u2-rem3-item3-http.trx' -v:minimal.
- Native fixture build0 warnings/errors22.32s; post-HTTP asset manifest refresh
 0 warnings/errors1.83s.
- admin-design-events-update.browser.js: Chromium PASS, WebKit PASS,exit0.
 16 prior connected cases plus4 query composition and3 pager outcomes:
  typed alpha -> tab before250ms makes one All+alpha read and no later debounce;
  tab during a shown search keeps alpha, aborts old read, inherits350ms;
  sort -> typing keeps identity sort; page -> typing keeps view and resetspage1
  for changed search. Exact request parameters asserted, not just input text.
  Next at final page -> Previous; Previous at first page -> Next;
  actual single-page response fixture -> no pager -> results heading.
  The latter substitutes a real filtered single-page server document to exercise
  pager disappearance; it is a focus boundary check, not a population mutation.
  No body focus, no sleeps/retries, prior150/400/identity/scroll proofs pass.
- git diff --check0. Whole .NET suite not run, planner-only Q-S1.

Files: Events model/markup/module, shared shell, Events update browser test,
this evidence. No product/reference question remains. Next:item4 immediate
attempted control state; loaded result/URL remain server-owned.
