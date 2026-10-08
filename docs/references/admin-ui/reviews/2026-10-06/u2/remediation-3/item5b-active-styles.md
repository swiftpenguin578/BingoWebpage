# Brief74 item5b — active, family-scoped page styles

Parent: 0db74ff3af60c1040def025f263029da3f57d5d0. Implements the planner's
7 October technical direction in 08-decisions, “Brief 74 item 5b approach”.
This resolves the preserved inactive-media WebKit blocker; no product decision.

- All three page stylesheets have zero-specificity family prefixes, including
  selectors targeting the page root itself. Removing only those prefixes yields
  each parent stylesheet exactly (trimmed trailing newline): 38 Dashboard,
  26 Events and 14 Identity selectors. Declarations and reference exceptions
  unchanged. Design check2's exact 78 reference→scoped bindings are in
  item5b-selector-bindings.json. Shared layout/tokens/components are not page CSS.
- Loaded and skeleton roots carry their own family. Incoming CSS loads active,
  cannot match the leaving family, and is awaited before the destination swap.
  Same-href links retain identity; obsolete links are removed after the swap.
  Abort/error removes staged links; errors retain the existing full-load fallback.
  Language uses the same helper. CSS waiting counts toward shared150/400 timing.
- Added a discovery-based native CSS-parser guard for every page stylesheet.
  All78 selectors pass Chromium and WebKit. Unknown/unscoped page selectors fail.
- Native-frame proof passes20 cases per engine: all six Dashboard/Events/Identity
  directions at149/151/650ms, uncached visit, same-href preservation, CSS error
  fallback and language. Paused fake clock, no sleeps/retries. Native RAF observes
  computed cascade and every leaving descendant's presentation: no destination
  frame without loaded active CSS and no incoming-style restyling of leaving page.

Executed checks:

- Fixture Release build:0 warnings/0 errors,2.92s; refreshed manifest build0/0,
  0.95s. Subsequent whitespace correction fixture build0/0,2.26s.
- Focused AdminShellUiTests + AdminDesignLocalizationTests:4 passed/0 failed/
  0 skipped,191ms. TRX: tests/Bingo.BrowserTests/TestResults/u2-rem3-item5b-http.trx.
- Dashboard rendered reference:9/0 each engine; Events:13/0 each engine, all
  comparisons0 differences. Both themes, narrow/wide, DA and navigation included.
- Identity initial full parity:31/2 per engine. The two named loading/failure
  differences were textContent joining “IdentityName…” instead of “Identity Name…”
  in item5's moved partial. Added the reference's literal inter-element space;
  focused scenario loading-failure now2/0 per engine,0 differences. Other31
  passing comparisons reused. Output artifacts/u2-rem3-identity-correction-*.
  An initial incorrect scenario filter ran0 cases; it is NOT a passing proof.
  Two wrong script-name invocations failed MODULE_NOT_FOUND before execution;
  corrected to scripts/check-admin-design-parity.cjs, not retried unchanged.
- Mechanical CSS normalization, node syntax and git diff --check pass.
  Final clean Release/full JS/design checks are recorded with item6's batch gates.

Historical failed attempts remain in item5b-stylesheet-blocker.md, explicitly
superseded by this approach. Scoped implementer evidence, not independent review
or visual acceptance. Whole .NET suite NOT RUN under Q-S1; planner owns that gate.
