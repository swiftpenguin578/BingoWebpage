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
