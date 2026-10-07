# U3 item0 follow-up — count summaries

User authority: 7 October 2026, `08-decisions.md`, “Count summaries load like the tab counts”. Events retains its two fixed phrases while loading; number placeholders use the existing tab-count markup. Attention and no-attention content still require loaded data. Identity/Dashboard summaries stay empty. Q6 remains two lines at ≤640px and one wider.

The optional `countSummary` registration owns expected fixed words (EN/DA). Generic conformance checks those words, numeric skeleton size/markup and absence of data-dependent content; undeclared families must remain empty. The register records that the Events reference has an empty loading summary. No lane T files changed.

## Executed checks

- Debug fixture build: `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --no-restore -c Debug` — exit0, no warnings/errors. The uncommitted Schedule fixture-launcher selector was used to run Debug; that independent launcher change is deliberately excluded from this count-summary commit.
- `BINGO_PARITY_CONFIGURATION=Debug BINGO_CONFORMANCE_PAGES=identity,dashboard,events PLAYWRIGHT_BROWSER=<engine> node scripts/check-admin-page-conformance.cjs` — Identity/Dashboard20 cases passed per engine; initial Chromium Events exposed0.4px inline baseline growth. A bounded alignment probe selected `vertical-align:bottom`, preserving the original line box.
- After that CSS correction, `BINGO_PARITY_CONFIGURATION=Debug BINGO_CONFORMANCE_PAGES=events PLAYWRIGHT_BROWSER=<engine> node scripts/check-admin-page-conformance.cjs` —5/5 per engine, exit0. Unaffected Identity/Dashboard evidence reused.
- `BINGO_PARITY_CONFIGURATION=Debug PLAYWRIGHT_BROWSER=<engine> node tests/Bingo.BrowserTests/admin-design-events-header.browser.js` —10/10 per engine, exit0; loaded reference geometry and Q6 height unchanged.
- Same environment, `node tests/Bingo.BrowserTests/admin-design-destination-header.browser.js` —12/12 per engine, exit0; EN/DA success/failure, fixed count words and exact150/400 timing.
- JS syntax and `git diff --check` — exit0. `node` is `/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.

Compact geometry/results: `count-summary-checks.json`. Raw logs: `/tmp/u3-count-summary-{events,header,destination}-{chromium,webkit}.log`. No full JS runner, Release build or whole .NET suite. Manual acceptance remains pending; this requested correction has no open product question. Schedule work is preserved outside this commit; next permitted action is resume item1.
