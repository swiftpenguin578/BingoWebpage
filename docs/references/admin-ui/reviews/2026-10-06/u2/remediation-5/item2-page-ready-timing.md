# U2 round 5 item 2 — L1 page readiness versus CSS readiness

Item 1 checkpoint: `e0e2c122dc7734fd357bb6231fafefa09162ad0c` (clean before
item 2). Applies the planner's decision in `08-decisions.md`, “U2 remediation
round 4 recheck”; no additional product/reference decision made.

The shell now reads the response body in parallel with family CSS and tracks
whether that page read is pending. The delayed skeleton is eligible only while
both its family CSS is ready and the page read remains pending. Completed or
failed reads cannot trigger a new skeleton when CSS finishes later. Existing
150 ms eligibility, 400 ms minimum measured from actual show time, inherited
skeleton ownership, successful swaps and failure stylesheet retention remain.

## Exact-clock proof — Chromium and WebKit

`admin-design-skeleton-styles.browser.js` exercises both Dashboard → Events and
Events → Dashboard, always with previously uncached destination family CSS:

| Response/body | CSS | Expected and verified |
| --- | --- | --- |
| Ready at 100 ms | Ready at 200 ms | Current content visible through 199 ms; no skeleton insertion even transiently; destination visible at exactly 200 ms; no 400 ms hold. |
| Ready at 300 ms | Ready at 200 ms | Skeleton first shown at 200 ms; still shown through 599 ms; destination visible at exactly 600 ms. |
| Headers at 100 ms, body at 300 ms | Ready at 200 ms | Same 200–600 ms skeleton; headers alone do not make the page ready. |

- Before the source fix: both engines fail `response100/CSS200 never inserts a skeleton` (`1 != 0`), reproducing L1.
- After the fix: **24 cases passed per engine**, zero failures, including all
  item 1 fast-failure cases and existing native CSS ownership/cancellation/
  cached/generic/error checks. Native frame and mutation sentinels report zero
  unstyled family frames. Exact 149/150 and 399/400 assertions remain unchanged.
- Initial full runner: **81 passed / 2 failed** (exit 1). Both failures were
  `admin-design-styles.browser.js:50`, an obsolete expectation that an instantly
  ready response shows a skeleton at 150 ms while CSS is pending. Exact initial
  results: [item2-initial-js-results.json](item2-initial-js-results.json).
- Updated that affected test to the explicitly approved L1 behavior for all six
  family directions at CSS-ready times 149/151/650 ms. It now requires no
  skeleton insertion (mutation records), the old page visible while CSS waits,
  direct swap at the exact CSS-ready time and no 400 ms hold. Existing native
  frame/presentation, active stylesheet, same-href, CSS-error fallback and language
  assertions remain unchanged. This changes the expected behavior to the planner
  ruling; it does not relax timing precision or unrelated assertions.
- No new sleep, retry, timeout enlargement or tolerance. Existing minimum-hold
  assertions remain for genuinely pending responses in the timing test.

## Commands and focused gates

Run from `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
Node: `/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.

- `PLAYWRIGHT_BROWSER=chromium <node> tests/Bingo.BrowserTests/admin-design-skeleton-styles.browser.js`: exit 0, 24 passed.
- `PLAYWRIGHT_BROWSER=webkit <node> tests/Bingo.BrowserTests/admin-design-skeleton-styles.browser.js`: exit 0, 24 passed.
- `dotnet build Bingo.slnx -c Release --no-restore -v:minimal`: exit 0, **0 warnings / 0 errors**, 31.47 s.
- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj -c Release --no-restore -v:minimal`: exit 0, **0 warnings / 0 errors**, 1.04 s.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminShellUiTests|FullyQualifiedName~AdminDesignLocalizationTests|FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~U2EventsDirectoryHttpTests' --logger 'trx;LogFileName=u2-rem5-item2-focused-http.trx' -v:minimal`: exit 0, **34 passed / 0 failed / 0 skipped**. TRX: `tests/Bingo.BrowserTests/TestResults/u2-rem5-item2-focused-http.trx`.
- `PLAYWRIGHT_BROWSER=chromium <node> tests/Bingo.BrowserTests/admin-design-styles.browser.js`: exit 0, **20 passed**, after the named legacy expectation correction.
- `PLAYWRIGHT_BROWSER=webkit <node> tests/Bingo.BrowserTests/admin-design-styles.browser.js`: exit 0, **20 passed**, same correction.
- `BINGO_PARITY_OUTPUT=artifacts/u2-rem5-item2-identity PLAYWRIGHT_CHANNEL=chromium <node> scripts/check-admin-design-parity.cjs`: exit 0, **66 passed / 0 failed**, 33 per engine, zero visual differences. Reused after the test-only correction; production source unchanged. Durable results: [item2-identity-results.json](item2-identity-results.json).
- Final `BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/artifacts/js-fixtures <node> scripts/run-browser-tests.cjs`: exit 0, **83 passed / 0 failed** (35 default, 24 Chromium, 24 WebKit). Exact per-file exits/durations: [item2-js-results.json](item2-js-results.json). Dashboard **9/0**, Events **13/0** per engine, plus Identity, body/header, loading, native CSS, language, shell, UR and shared-component checks pass. Existing eight account stale-change fixtures reused. No source changes after the focused checks; only the named legacy test expectation changed before this final runner.

## Shared design checks 1–6 (touched-file scope)

1. `cmp src/Bingo.Web/wwwroot/css/admin-design-tokens.css docs/references/admin-ui/ui/tokens.css` and the equivalent components comparison: exit 0, byte-identical. `git diff --exit-code 3a26196 -- src/Bingo.Web/wwwroot/css docs/references/admin-ui/ui 'docs/references/admin-ui/*.dc.html' src/Bingo.Web/Pages`: exit 0; page CSS, frozen references and markup unchanged throughout the round.
2. No page CSS changes; existing family-scoped layout and registered exceptions preserved.
3. No markup changes or inline styles introduced.
4. Existing shared component/template use preserved; no page-specific copies.
5. No action/busy changes or page timer/spinner introduced. Shared loading constants and timers remain 150/400 ms. The new readiness flag gates the existing shared helper.
6. Shared page read, CSS preparation, guard, cancellation and swap lifecycle remain in `admin-design-shell.js`; no copied page behavior. HTTP status and body-read failure still flow through the existing failure/fallback path.

`node --check` on all three changed JS files and `git diff --check`: exit 0.
Whole .NET suite **NOT RUN**; planner owns its gate after independent recheck.
No independent review or manual acceptance claimed. No user-owned app/database
mutation, push, merge, branch/worktree creation, worker or extra item.
Stop after item 2. No unresolved implementation issue; planner recheck and
whole-suite gate remain pending, followed by user visual acceptance as applicable.
