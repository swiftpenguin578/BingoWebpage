# Brief76 item5 — no load fades

Authority: user addendum, 7 October2026; 08-decisions.md “No load fades”.
This is implementer verification, not independent review or visual acceptance.

## Change and protected scope

- Removed all six Dashboard page-card/section fade-in markers and Identity's
  form-card marker. Only page markup and family CSS changed in production.
- Family-scoped fade-in guards prevent a legacy load marker from fading again.
  Events also guards the reference results marker (the current server results
  container already has no results class; no new wrapper is introduced).
- Loaded empty states have their own reference fade independent of those
  markers (components.css:257); Dashboard/Events suppress that too. Events
  page notices and Identity's readonly page notice similarly suppress their
  load fade (:264); Identity's static unsupported-timezone note has an explicit
  no-load-fade markup marker. Interactive conflict/timezone notes and inline
  field errors are not blanket-disabled.
- Menus, modals, drawers, scrims, rowFlash and rows.swap-a/b remain active;
  no shared behavior JS, lifecycle, authorization, database, URL or timing
  changes. Frozen tokens/components/reference HTML remain byte-identical.
- DELIVERY_PLAN register records the intentional reference animation exception,
  including the rule for future new-layout families (U3 conformance remains
  outside this assignment). Accepted language-switch cross-fade is an
  interaction, unchanged; this item concerns page/results loads.

## Executed animation proof

Command (each engine):
PLAYWRIGHT_BROWSER=chromium|webkit <bundled-node>
tests/Bingo.BrowserTests/admin-design-load-animation.browser.js.

Chromium **15 passed /0 failed**; WebKit **15 passed /0 failed**.
Real controlled PostgreSQL/Kestrel fixture; motion enabled, not reduced.
Three page families, fast and slow navigation: observe DOM insertion and
native frames, inspect content getAnimations({subtree:true}), require no
animations. Exact fake clock checks149/150ms and399/400ms; no sleeps.
Events real fast/slow view updates and an empty search result receive the
same check. Six family probes assert legacy load-marker animation=none and
the eight preserved interaction animation names remain unchanged.
Observer is attached above the replaced region so it sees insertion, not
only the departing nodes; native frames are captured before clock install.

Test authoring corrections, not passing gates: first attempt used a guessed
CSS filename and failed the readiness checkpoint; corrected to the exact
fingerprinted href in the server response. Second attempt stalled because
Playwright clock installation is context-wide: a new page in the old context
saved an already-faked RAF. Only validated owned Node PID62571 was stopped;
driver ended1 due browser closure, no pass claimed. The corrected proof uses
a fresh authenticated browser context per case, capturing genuine native RAF,
and passes in both engines. User review app/database untouched.

## Design checks1–6

1. Frozen tokens/components copies compare exactly to reference and initial
   round4 baseline; no reference HTML change.
2. Added only family-prefixed animation:none rules. They intentionally override
   reference page/results/empty/notice load fades under the registered decision.
   No custom color/font/shadow/radius; native CSS selector guard required.
3. No new inline styling;24 existing matches are retained reference or dynamic
   chart/meter/skeleton geometry (item4 register explains text-row exceptions).
4. Existing shared cards/forms/tables/banners/components reused (66 matches);
   only load-marker class removal/static-note marker added.
5. No action timer/spinner change;23 existing matches unchanged.
6. Shared navigation/update/guard/transport/lifecycle unchanged (42 matches).
Exact final commands/output are recorded in item5-design-checks.json.

Final Release/affected HTTP/full JS/rendered results: final-gates.md.
Whole .NET suite **NOT RUN**, Q-S1/Q-S2 planner-only after independent review
passes. Both pages remain awaiting Claude review, then user visual acceptance.
No workers, reviewer, push, merge, deployment, U3 or self-review.
