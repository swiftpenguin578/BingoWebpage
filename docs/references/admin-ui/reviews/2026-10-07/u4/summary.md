# U4 Overview batch gate summary (items 2 and 3)

Branch `claude/u4-overview`. Executed by the implementer; independent review pending.

## Commits
- Item 2a `7f8ce2cf`: shared `layer.markClean()` in `admin-design-shell.js` `openLayer`; Overview mounts its pickers after open and calls it (also after the evidence-code form reset). Catalogue keeps its page-owned dirty model (not trivial to replace; reported).
- U4-L1 `1f6b1c60`: cross-event links use the fixed text "Open event" (Danish "Åbn event"); name stays in the row text.
- U4-E5 `f4c6bafc`: hidden-event Restore refusal reuses `EventLifecycleService.CurrentEventRefusal` (U4-Q4/Q5 wording).
- U4-E1/E2 `7cc491dc`: DELIVERY_PLAN register rows (E2 row existed; relabelled).
- Merge `ba7e5160`: `codex/participants-functionality` (4b482822). One conflict, `SharedResource.da.resx` (both sides appended entries): kept both, no duplicate keys, XML valid. `SharedResource.resx` has only `AdminDesign.` keys.
- Follow-up: `admin-design-summary-navigation.browser.js` updated (A10): Overview is now a new-layout page, so its summary link carries `data-shell-link` (was asserted absent); clicks target the other link.

## Checks (executed)
- `dotnet build Bingo.slnx -c Release`: 0 warnings, 0 errors (after merge and at the end).
- Parity fixture rebuilt; stale-evidence fixtures regenerated; `node scripts/run-browser-tests.cjs`: 118 executions, 116 passed, 2 failed (summary-navigation, both engines; fixed as above and rerun: both engines pass). Includes page conformance (11 pages, 5 widths, Chromium + WebKit), shell and Overview browser tests.
- `markClean` shell test in `admin-design-shell.browser.js`: Chromium and WebKit pass.
- Integration classes (630 tests, 0 failed, 0 skipped): Slice3DestructiveLifecycle (test #14), Slice3ScheduledLifecycle, EventSignupWarningRemediation, EventQuarantine (both files), AdminEventFunctionalityPass3, StatsPass2ReviewCorrections, C11FinalizedRoster, DraftOperations, PublishedContentRetention, Slice6CatalogueAdministration, Slice10Pass103ActivityProjection, CaptainScopedNavigation, EventCompetitionManagement, SignupQuestionCreationRetry, AdminDashboardIntegration, AdminActionProjection, OverviewServer (9), AdminEventHandlerClassification, AdminDesignShellIntegration, AdminStaleChange.
- BrowserTests project: AdminDesignLocalizationTests, EventCreationUiTests, ManagedCompetitionUiTests, AdminShellUiTests: 19 passed. Application.Tests DashboardHistoryOrdering: 2 passed.
- `git diff --check` clean; frozen CSS `cmp` identical (tokens, components).
- Not run: whole .NET suite (planner).
