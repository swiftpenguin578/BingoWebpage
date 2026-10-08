# Brief74 item2 — shown skeleton ownership

Parent0e62ae5. Implementer checks only, not independent acceptance.

Shell supersedeUpdate aborts the obsolete read immediately on input while
transferring its shown overlay to a fresh owner. Its original shownAt remains:
debounce/new response inherits the remaining shared400ms minimum. Cancelled
request cleanup cannot remove the new owner's display. A request which never
showed a skeleton cannot clear another display. No page-owned150/400 policy.

A16 navigation claims any results overlay before aborting the old request.
Its page skeleton appears immediately with inherited shownAt. The old page is
hidden/inert before the nested results restoration, so old rows never become
visible; cancellation can still restore the original page correctly.

Checks:
- Native parity fixture Release0 warnings/errors,55.65s.
- admin-design-events-update.browser.js, Chromium and WebKit:16 connected
  cases per engine PASS,exit0. Exact paused-clock proof:
  input at shown+50 aborts old read; stale fulfil ignored; skeleton remains
  across249+1ms debounce and successor's99+1ms remainder. The second-filter
  hold cases retain349+1ms. Sidebar navigation at shown+50 immediately promotes
  to page skeleton, retains349+1ms and never reveals old results.
  Prior focus/caret/scroll/session/failure checks pass too. No sleeps/retries.
- admin-design-loading.browser.js, both engines:12 boundary cases plus5
  hold/abort/Back/focus cases PASS,exit0.
- Node shell syntax0; git diff --check0.

Authoring checkpoint: the existing pre-show typing case expected an obsolete read
to show a skeleton during debounce, giving a300ms inherited hold. Both engines
correctly failed that stale expectation after immediate input abort. Replaced
with an exact assertion that abort-before-show + fast successor shows no skeleton;
the new already-shown cases retain strict400ms assertions. No tolerance/sleep or
failed-class retry added.

Files: shared shell, Events module, Events update browser test, this evidence.
Whole .NET suite planner-only Q-S1, not run. Next:item3 unified query/focus.
