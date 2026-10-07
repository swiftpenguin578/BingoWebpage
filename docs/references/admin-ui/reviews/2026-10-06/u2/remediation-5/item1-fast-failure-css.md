# U2 round 5 item 1 — M1 fast-failure family CSS

Start: clean `3a261968f190d559eedf5658118083606574a8b4`, assigned
`codex/participants-functionality` checkout. Implementer only; no independent
review or manual acceptance claimed. No push, merge, new branch or worktree.

The shared navigation failure path now retains its prepared family stylesheet
immediately after inserting its failure placeholder. The existing skeleton and
successful-page retention paths remain intact. No templates or CSS changed.

## Focused proof

`admin-design-skeleton-styles.browser.js` adds uncached Events and Dashboard
HTTP 500 cases. Native CSS is released at 50 ms; HTTP fails at 100 ms. Both
assert the failure placeholder/retry control, applied family CSS sentinel,
retained stylesheet with a sheet, exact 100 ms completion and no 400 ms hold.
Existing mutation/native-frame inspection requires zero unstyled frames.

- Before the source fix: Chromium and WebKit fail the Events computed CSS
  sentinel (`empty != events`), reproducing M1.
- After the fix: **18 cases passed per engine**, zero failures. Includes both
  new cases plus existing native uncached/cached timing, generic skeleton,
  cancellation/ownership, stale-link reload and CSS-error fallback checks.
- Test-development corrections: native head links normalize hrefs to absolute
  URLs; use URL pathname comparisons. The fixture pauses at a 60000 ms clock
  origin; assert exact elapsed time from the start. Initial selector/clock
  assertions failed before these corrections. No sleeps, retries, assertion
  tolerances or timeout enlargement were added.
- An initial bare `node` invocation could not find Node (exit 127); all executed
  browser checks use the established bundled executable below.

## Commands and gates

Run from `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
Node: `/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.

- `PLAYWRIGHT_BROWSER=chromium <node> tests/Bingo.BrowserTests/admin-design-skeleton-styles.browser.js`: exit 0, 18 passed.
- `PLAYWRIGHT_BROWSER=webkit <node> tests/Bingo.BrowserTests/admin-design-skeleton-styles.browser.js`: exit 0, 18 passed.
- `dotnet build Bingo.slnx -c Release --no-restore -v:minimal`: exit 0, **0 warnings / 0 errors**, 1.09 s.
- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj -c Release --no-restore -v:minimal`: exit 0, **0 warnings / 0 errors**, 1.23 s.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminShellUiTests|FullyQualifiedName~AdminDesignLocalizationTests|FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~U2EventsDirectoryHttpTests' --logger 'trx;LogFileName=u2-rem5-item1-focused-http.trx' -v:minimal`: exit 0, **34 passed / 0 failed / 0 skipped**. TRX: `tests/Bingo.BrowserTests/TestResults/u2-rem5-item1-focused-http.trx`.
- `BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/artifacts/js-fixtures <node> scripts/run-browser-tests.cjs`: exit 0, **83 passed / 0 failed** (35 default, 24 Chromium, 24 WebKit). Exact per-file exits/durations: [item1-js-results.json](item1-js-results.json). Existing eight account stale-change fixtures reused. Dashboard **9/0**, Events **13/0** per engine, plus body/header, native styles, loading, language and shell cases pass in this full runner.
- `BINGO_PARITY_OUTPUT=artifacts/u2-rem5-item1-identity PLAYWRIGHT_CHANNEL=chromium <node> scripts/check-admin-design-parity.cjs`: exit 0, **66 passed / 0 failed**, 33 per engine, zero visual differences. Durable comparison results: [item1-identity-results.json](item1-identity-results.json).

## Shared design system checks 1–6 (touched-file scope)

1. `cmp` tokens and components against `docs/references/admin-ui/ui/`: exit 0, byte-identical. `git diff --exit-code 3a26196 -- src/Bingo.Web/wwwroot/css docs/references/admin-ui/ui 'docs/references/admin-ui/*.dc.html' src/Bingo.Web/Pages`: exit 0. Frozen references, all page CSS and markup unchanged.
2. No page CSS changes; existing scoped layout rules/reference exceptions preserved.
3. No markup changes or new inline styles.
4. Existing shared templates/components used; no page-specific copy introduced.
5. No action/busy implementation changed or added. Scoped `rg` in shell shows only existing exit, shared loading/busy and toast timers; existing skeleton/update `aria-busy` lifecycle. New line is stylesheet ownership only.
6. Shared navigation/CSS ownership remains in `admin-design-shell.js`; no page adapter or transport/guard copies. Scoped `rg` confirms `delayedLoading`, `preparePageStyles` and navigation/update fetch boundaries in the existing shell.

`node --check` on both changed JS files and `git diff --check`: exit 0.
Whole .NET suite **NOT RUN**; planner owns its final candidate gate.
User review app/database untouched; rendered tests use owned synthetic fixtures.
Next permitted action after passed gates/commit: item 2 (planner timing ruling L1).
