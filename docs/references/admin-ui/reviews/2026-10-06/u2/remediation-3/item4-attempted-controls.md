# Brief74 item4 — immediate attempted control state

Parentb497bf7. Implementer evidence; awaiting Claude review/user acceptance.

A single paintQuery applies the requested query before the read starts:
tab checked/highlight, localized Phase value/active marker/menu selection,
sort class/arrow/aria and localized next-action label, page current marker
and arrow destinations/enabled state. Focus moves to the opposite arrow at the
instant the initiating arrow becomes disabled. Counts/rows remain server-owned.
Failure leaves the attempted state; Try again reads that same URL. Newest intent
paints immediately and stale replies cannot undo it. Applied URL still replaces
only after successful canonical response (existing accepted URL rule unchanged).

Labels/defaults come from Razor metadata and shared icon cloning, not English
in JS. Direct correction of item3: new Participants/Needs attention sorts use
their existing descending-first defaults; repeat sorting toggles current intent.
No CSS/token/component or production business-rule change. Register row updated.

Checks:
- Native fixture Release0 warnings/errors,17.79s; Node syntax0; diff check0.
- admin-design-events-update.browser.js Chromium PASS and WebKit PASS,exit0:
  23 prior outcomes +8 additions. Six slow cases assert selected state before
  any response and at150ms, then after exact400ms hold: tab, phase, identity
  sort, page2, Participants descending and attention descending. Failure preserves
  attempted All highlight while applied URL remains Current; Retry requests All.
  New Current choice during All's shown hold aborts old reply, paints at once,
  and retains the350ms remainder. Prior query/focus/identity/caret/scroll/session
  checks pass. No sleeps/tolerance/retries.
- Full paired JS/loaded parity gate follows after remaining assigned items.
  Whole .NET suite planner-only Q-S1, not run.

Files: Events markup/module, update browser test, DELIVERY_PLAN register, evidence.
Next:item5 destination headers; then separately scoped item5a summary reservation.
