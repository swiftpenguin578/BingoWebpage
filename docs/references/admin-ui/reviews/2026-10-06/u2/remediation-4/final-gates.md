# Brief76 round4 — focused gate ledger

## Item4 checkpoint (before authorized item5)

- Item4 body proof25/0 per engine; header proof8/0 per engine; Identity rendered
  reference33/0 per engine. Exact rectangles/results in item4-qsk2-positions-*,
  final-header-positions-* and final-identity-parity.json.
- Affected HTTP/localization34 passed/0 failed/0 skipped; TRX elapsed
  14.529559s (start03:40:01.5592730+02:00, finish03:40:16.0888320+02:00).
  Command: dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj
  --configuration Release --no-restore --filter
  'FullyQualifiedName~AdminShellUiTests|FullyQualifiedName~AdminDesignLocalizationTests|FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~U2EventsDirectoryHttpTests'
  --logger 'trx;LogFileName=u2-rem4-final-focused-http.trx' -v:minimal.
  Exact TRX:
  /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/tests/Bingo.BrowserTests/TestResults/u2-rem4-final-focused-http.trx.
- Clean Release0 warnings/errors52.08s, fixture manifest refresh0/0 1.92s;
  exact commands/results in item4-release-check.json.
- Shared design1–6/source/diff/frozen checks passed with explained matches in
  item4-text-line-rows.md and final-design-checks.json; native parser85 scoped
  selectors per engine. Frozen tokens/components/reference HTML unchanged.
- Two full JS attempts STOPPED, driver143 each: stale fixture manifest; then
  old WebKit header helper querying a not-yet-created CSS-waited skeleton.
  Both corrections and focused rechecks documented in item4-text-line-rows.md.
  Neither is a completed full-batch pass. Do not read old runtime results.json
  as evidence for this round.

User has added item5 ("No load fades") after item4. Final complete JS and
affected rendered/design/Release checks will follow that change. No final batch
completion claim at this item4 commit; all final results must be appended below.

Whole .NET suite NOT RUN under Q-S1/Q-S2. Planner-only gate after independent
review passes. No whole-suite TRX/count/time claimed. Page approval unchanged:
awaiting Claude review, then user visual acceptance. User review app/database
untouched; no push/merge/deploy/workers/self-review.

## Final item5 / stop-boundary gates

- Full JS runner **83 passed /0 failed**, driver exit0:35 default,24 Chromium,
  24 WebKit. Summed individual execution duration710.976s (not whole-run wall
  time). Exact command, per-file exits/timings and totals: item5-js-results.json.
  Reused the established eight controlled account stale-change HTML fixtures;
  no broad Integration rediscovery or whole-suite run.
- Both engines rerun body25/0 (Dashboard first-card/participation text-wrap rule,
  Events table and Identity first-card at390/494/860/1280/1440), Events header10/0
  (390/640/860/861/1280, normal/large summary), Dashboard header8/0 (normal/narrow
  at390/861/1280/1440). Durable rectangles are in item4-qsk2-positions-*,
  item5-events-header-positions-* and final-header-positions-*; only animations
  changed after the item4 body/header measurements, and the same geometry assertions passed
  again in the completed final runner. Native CSS20/0 and skeleton/CSS16/0 each;
  repeated loading/navigation/Back/Forward, retained query/focus and header
  checks all pass in the runner. No timing/assertion relaxations.
- Dashboard reference9/0 and Events reference13/0 per engine; UR profiles2/0
  per engine. Identity reference **33/0 per engine**, separately rerun after
  removing its load marker; zero differences: item5-identity-parity.json.
- No-load animation proof **15/0 per engine** in focused runs and again in the
  full runner; motion enabled, insertion observer survives the region swap,
  genuine native RAF, exact149/150/399/400 clock. Empty result included; eight
  interaction animation rules remain active. See item5-no-load-fades.md.
- Final affected HTTP/localization **34 passed /0 failed /0 skipped**, exit0.
  TRX elapsed11.694190s (start2026-10-07T04:07:47.1649790+02:00,
  finish2026-10-07T04:07:58.8591690+02:00).
  Command: dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj
  --configuration Release --no-build --no-restore --filter
  'FullyQualifiedName~AdminShellUiTests|FullyQualifiedName~AdminDesignLocalizationTests|FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~U2EventsDirectoryHttpTests'
  --logger 'trx;LogFileName=u2-rem4-item5-focused-http.trx' -v:minimal.
  Exact TRX:
  /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/tests/Bingo.BrowserTests/TestResults/u2-rem4-item5-focused-http.trx.
- Final clean Release build **0 warnings /0 errors**,36.49s; fixture metadata
  refresh **0/0**,1.41s. Exact commands in item5-release-check.json. An initial
  clean command rejected unsupported --no-restore (exit1, before cleaning);
  corrected command then passed, no production or timeout workaround.
- Design checks1–6, diff/syntax and frozen CSS/reference checks passed. Native
  CSS parser verifies all3 page stylesheets, **94 scoped selectors per engine**.
  Exact source commands/output: item5-design-checks.json; registered reference
  exceptions and retained matches explained in item5-no-load-fades.md.

Stop after item5, one local commit per item; no source changes after final
runtime checks. No unresolved product/reference question. Independent Claude
review and user visual acceptance are pending; UI_PAGE_MATRIX unchanged.
Whole .NET suite **NOT RUN** (Q-S1/Q-S2); no whole-suite count/timing/TRX claimed.
Planner runs it on the final candidate SHA after independent review passes.
Remote Linux/Docker-in-Docker CI unrun; user's review environment not refreshed
or modified. Next permitted action: planner independent review, then planner
whole-suite gate and user visual acceptance. No U3+, palette/laneT, packaging,
push, merge, deployment or self-review.
