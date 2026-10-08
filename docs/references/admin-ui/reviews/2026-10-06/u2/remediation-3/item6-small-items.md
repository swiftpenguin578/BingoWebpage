# Brief74 item6 — small-item corrections and affected checks

Parent: 8d0b6e7. No new product/reference decision; implements73a F2 and73b L3–L5.

Before → after:

- Q-H1 geometry was an optional script → admin-design-header-geometry.browser.js
  gates its unchanged strict assertions through the full JS runner in each engine.
  Script selects explicit engine(s), with separate fixture/output directories.
  No assertion/tolerance/timeouts weakened. Normal/narrow fixtures at390/861/
  1280/1440:8/0 Chromium and8/0 WebKit. Durable paired rectangles:
  item6-header-positions-chromium.json and item6-header-positions-webkit.json.
- Imported activity upstream timestamp was null → deterministic import end
  (also fetched time), matching the importer’s required input and writer at
  HistoricalEventImporter.cs:394,:639. Synthetic account input includes the
  same upstreamUpdatedAt. Hashes continue deriving from the actual input.
  Existing exact-null test becomes exact persisted end/fetched assertions.
- Imported live BoardTile and TileTemplate evidence said “Upload an invented
  screenshot.” → HistoricalEventImporter.Disclosure, as the existing frozen
  approval snapshot already does. Added exact4-tile/4-template PG assertions.
  Platform fixtures keep the original evidence instructions. Lifecycle, audit,
  discard/printed-URL/auth/cookie/session assertions unchanged.
- Endpoint proof conditionally skipped its four address assertions unless the
  hostname was localhost → unconditional binding/connection address assertions
  plus an explicit explanatory failure for unsupported remote publication.
  Server identity, concurrent containers, credentials and exact28P01 rejection
  remain unchanged; no retry, timeout change or assertion relaxation.

Executed:

- dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj
  --configuration Release --no-restore --filter
  'FullyQualifiedName~UiReviewScenarioIntegrationTests|FullyQualifiedName~PostgreSqlEndpointIsolationTests'
  --logger 'trx;LogFileName=u2-rem3-item6-postgres.trx' -v:minimal
  →3 passed/0 failed/0 skipped; TRX elapsed29.610169s (test-body summary28s).
  Exact TRX:
  /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/tests/Bingo.IntegrationTests/TestResults/u2-rem3-item6-postgres.trx.
- Affected HTTP classes AdminShellUiTests, AdminDesignLocalizationTests,
  EventCreationUiTests and U2EventsDirectoryHttpTests →33/0/0;
  TRX elapsed32.999470s. Exact TRX:
  /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/tests/Bingo.BrowserTests/TestResults/u2-rem3-final-focused-http.trx.
- Fixture Release refresh0 warnings/0 errors,1.02s. Header wrapper8/0 each;
  Identity full rendered reference33/0 each engine, all paired differences0.
  Durable item6-identity-parity.json. This includes both themes, mobile, uncertain/
  stale/editing/load-failure/language behavior, not independent review.
- git diff --check0. Full JS/clean Release final gates recorded in item7 evidence.

## Shared design checks1–6, touched files

Exact commands, exits and outputs: item6-design-checks.json. No broad redesign.

1. Frozen tokens/components cmp0; baseline frozen/reference diff empty.
2. Literal colour/font-family/shadow/literal-radius scan0 matches (rg exit1).
   Page rules retain existing reference layout/token appearance and approved
   skeleton geometry. Mechanical family prefixes preserve specificity; exact78
   reference→scoped selectors listed in item5b-selector-bindings.json and native
   parser guard passes both engines. Existing MVC validation/readonly/empty-banner
   adapters, dismissed hints, query-fragment display:contents, server column
   sizing, and user-approved loading/failure reservations remain the explicitly
   justified application adapters, not new shared primitive owners.
3.17 inline source lines inspected: dynamic meter/chart geometry, reference
   width/flex/grid/display-contents and existing header min-width. Three Identity
   skeleton lines retain literal14/18px spacing and8px radius, exactly the accepted
   Identity.dc.html:118–120 and parent markup moved unchanged to the shared partial.
   These inherited reference-owned lines are explicitly listed, NOT claimed
   literal-spacing-free. No new colour/font/spacing value introduced.
4.52 shared-class/partial matches; shell/modal/banner/icon/form/save-bar owners
   reused, no page-specific replacement component. Family attributes only.
5.9 timing/busy hits explained: Events250ms search debounce; Create pending
   aria-busy plus shared ui.busy transport; Identity pending aria-busy/spin state
   and shared ui.busy save/check-again; Identity2000ms copy-label reset is not busy
   timing. Accepted Identity state rendering unchanged. No new local busy timer.
6.41 lifecycle/fetch/guard matches: shared shell owns guards, C-CMP-2, A16,
   abort/skeleton handover and stylesheet wait; page modules use shared API and
   dispose owned listeners. Full paired JS gate executes these boundaries.

Remote Docker/DinD/Linux CI unrun; local Docker address proof now cannot silently
skip. Whole .NET suite NOT RUN under Q-S1, delegated to planner. UI acceptance
unchanged; both pages await Claude review, then user visual acceptance.
