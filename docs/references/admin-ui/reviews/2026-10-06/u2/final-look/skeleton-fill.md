# U2 final-look — fill the locked Events results skeleton

Clean start `f818293da3402a16cdd1f7e85e526fe485f19072`, assigned
`codex/participants-functionality` checkout. Planner ruling: `08-decisions.md`,
“Events results skeleton has a large empty space”. One local commit, no push.

The shared update helper repeats the existing `.sk-row` nodes to fill the retained
results height. Its wrapper clips only the final partial row to keep the exact
locked height and scroll range. Both initial insertion and inherited pending
replacement use this helper. Normal restoration still removes the placeholder
and restores the previous min-height before real results arrive. No Events code,
CSS, frozen reference, loading delay/minimum or transport behavior changed.

## Requested checks

- `PLAYWRIGHT_BROWSER=chromium /Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node tests/Bingo.BrowserTests/admin-design-results-fill.browser.js`: exit0;390/1280px pass.
- Same command with `PLAYWRIGHT_BROWSER=webkit`: exit0;390/1280px pass.
- Both use real authenticated Razor markup/styles and an owned PostgreSQL fixture.
  To exercise the explicitly requested50+ displayed rows without changing product
  pagination or a database, the private browser's All rows reuse that markup54
  times. Past is an actual fetched short response. The response is held with the
  existing exact fake clock; no sleep, retry or timeout enlargement added.
- Baseline fails in both engines: skeleton406px within3132px locked results.
  Before measurements: [Chromium](skeleton-fill-before-chromium.json),
  [WebKit](skeleton-fill-before-webkit.json).
- Fixed measurements: [Chromium](skeleton-fill-chromium.json),
  [WebKit](skeleton-fill-webkit.json). Initial and inherited skeletons fill the
  locked3132px exactly; gap0 (<58px row height),54 skeleton rows, main scrollTop600
  unchanged; horizontal scroll unchanged. Success restores original minHeight
  and the real shorter Past height. Exact149/150ms and inherited349/350ms
  boundaries remain asserted; no page errors.
- `PLAYWRIGHT_BROWSER=chromium <same node> tests/Bingo.BrowserTests/admin-design-events.browser.js`: exit0, **13 passed/0 failed**, zero rendered differences.
- Same Events command with `PLAYWRIGHT_BROWSER=webkit`: exit0, **13 passed/0 failed**, zero rendered differences. Existing themes/widths, failure composition, filters/sort, language, retry, repeated A16/Back/Forward and controlled fault outcomes retained.
- `git diff --check`: exit0; rerun on the final scoped diff before commit.

No full JS runner, Release build or .NET suite run for this assignment, as
explicitly instructed. User-owned application/database untouched. No independent
review or manual acceptance claimed. No unresolved implementation issue. Stop
after the single commit; planner recheck and final-SHA suite remain with planner.
