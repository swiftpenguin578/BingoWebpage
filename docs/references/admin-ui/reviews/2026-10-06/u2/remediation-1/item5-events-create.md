# U2 brief70 remediation item5 — Events/Create/shared outside click

Changed files:

- `DELIVERY_PLAN.md`: replaces superseded loading-summary and A12 backdrop rows.
- `_AdminEventsLoadStates.cshtml`, `admin-design-events.css`: loading summary
  stays empty with responsive height reserved; count skeletons remain.
- `admin-design-shell.js`: only outside-layer condition changed; clean editable
  modal/drawer closes, dirty invokes the existing shared/custom discard guard.
  Pending/stack/confirmation protections unchanged.
- `admin-event-create.js`, `_AdminEventCreateTemplate.cshtml`, community Danish
  resource: close uses shared setUrl/replace only; no history.back/navigation;
  duplicate substitution callbacks render `$&`, `$$`, `$'`, `$\`` literally;
  exact user EN/DA E14 wording. Unknown Check again/Leave anyway unchanged.
- `EventsDirectoryIntegrationTests.cs`: retains Cancelled confirmed count and
  adds asc/desc null-last assertion to existing real missing-interval fixture.
- `U2EventsDirectoryHttpTests.cs`: actual PostgreSQL missing interval, asserts
  known then unknown in **new row markup** for both Participants directions.
  Existing paging/query/role/hidden assertions unchanged.
- `FixtureHost.cs`: opt-in controlled literal-dollar duplicate name only.
- `check-u2-events.cjs`, `check-u2-create.cjs`, shared shell browser test:
  no summary bars, exact close URL/no requests/same DOM/no skeleton/history,
  clean/dirty drawer/Create and confirmation protections, literal duplicate,
  exact EN/DA not-found wording with app culture switch.
- `admin-parity-compare.cjs`: excludes dormant header templates as already done
  for dormant loading/failure templates. Their page CSS is loaded only when
  rendered; actual skeleton DOM stays in inventory. Destination style readiness
  is awaited before the Events loaded-position capture. This evidence file.

Executed checks (6 October 2026):

- Serial parity fixture Release build: **0 warnings/errors**,2.02s.
- `dotnet test ...Bingo.IntegrationTests... --configuration Release --filter
  FullyQualifiedName~EventsDirectoryIntegrationTests`: **8/0/0**,7s;
  `tests/Bingo.IntegrationTests/TestResults/u2-remediation-item5-directory.trx`.
- BrowserTests filter U2EventsDirectoryHttpTests/U2EventCreateHttpTests/
  AdminDesignLocalizationTests: **6/0/0**,7s;
  `tests/Bingo.BrowserTests/TestResults/u2-remediation-item5-http-final.trx`.
  Actual lost-session302 Create/check reads and existing audit safety retained.
- `env PLAYWRIGHT_CHANNEL=chromium <bundled-node> scripts/check-u2-events.cjs`:
  **26 passed,0 failed**, both engines. Desktop/phone both themes and failed
  composition: each **0 differences**. Loading query/header/toolbar/headings/
  counts positions at1440/1280/861/860/640/390; summary empty with no .sk;
  filters/paging-related query state, culture, partial attention, hidden URL,
  repeated swaps/Back/Forward. `artifacts/u2-events/results.json` + JSON/PNG.
- Same command `scripts/check-u2-create.cjs`: **10 passed,0 failed**, both
  engines;8 rendered modal comparisons each0 differences. Dirty/backdrop,
  keyboard trap, close replace/no navigation/no skeleton/same directory DOM,
  Back stays closed, exact literal-dollar duplicate, unknown/readback/new key,
  EN/DA E14 wording, session drafts and real success/Back highlight pass.
  `artifacts/u2-create/results.json` + JSON/PNG.
- Shared shell standalone runner: **PASS Chromium/WebKit** including clean
  input drawer, dirty backdrop Keep editing/Discard and outside confirmation
  never dismissing. Node syntax checks on changed scripts pass.
- `git diff --check`: exit0. Frozen CSS/references untouched.

Corrected checkpoints: concurrent dotnet builds collided on Web output (MSB4018/
MSB3026); serial rebuild passed, no environmental retry loop. Test authoring
initially used a replacement-pattern string for the literal-dollar test itself;
corrected to callback before executing. Initial HTTP row regex also matched
switcher entries; now only new table row markup. Wait for confirmation detached
before next backdrop click. Initial parity inventory counted deferred Dashboard
header classes against the Events stylesheet, and loaded capture preceded the
destination style; both corrected without weakening rendered geometry assertions.
Failed checkpoints are not passing gates. No old protected assertion removed.

Implementer checks only. Awaiting Claude review, then user visual acceptance.
