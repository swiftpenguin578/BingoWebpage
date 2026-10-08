# U6 Teams / Draft — batch summary (implementer evidence)

Branch `claude/u6-teams`, worktree `BingoWebpage-u6`. Items: `d5f54408` 0a, `0b55acb3` 0b, `9732ebf5` 1a, `69d040cd` 1b,
`95940712` 1c, `26f4dd5e` 2 (U6-E2 (a) short-vs-drafted-size sentence), merge of `codex/participants-functionality`
(`97d08f7a`) at `0e797bfd`. Awaiting independent review and the user's look; not technical or manual acceptance.

## Merge resolution (`0e797bfd`)
- `_AdminDesignIcon.cshtml`: feature `more` (r=1) and `eye` kept, U6 `undo` added (one `more` case).
- `AdminEventHandlerClassificationTests.cs`: feature Board/BoardPreview rows, U6 Draft row (GET:State, no WithdrawParticipant, viewable on terminal events).
- `admin-page-conformance-pages.cjs`: U7 board (headerGrowth, backgroundPostCount), U5 participants (numberCounts) and U6 draft (fixtureEnv, update.url, referenceEvent) all kept; the runner/checks auto-merged with both sides' features.
- Resources: three exact duplicate names from both sides removed (`We couldn’t confirm whether this went through: {0}.`, `Take over…`, `AdminDesign.remove {0}` in both files; values identical). No case-insensitive duplicates; `SharedResource.resx` holds only `AdminDesign.`-prefixed names.
- Legacy-shell assertions stay on WiseOldMan; `event-manage.js` keeps both lanes' removals (auto-merged).

## Batch gate (executed at `0e797bfd`)
| Check | Result |
| --- | --- |
| Release builds: Bingo.Web, parity fixture, IntegrationTests, BrowserTests | 0 warnings, 0 errors |
| Full JS runner (`scripts/run-browser-tests.cjs`, admin-design files in Chromium and WebKit) | 117 passed, 1 failed: `admin-design-ur.browser.js` [webkit] navigation timeout to `/notifications`; rerun alone: PASS (2/2) |
| All-page conformance, Chromium | PASS 70 geometry cases + frame/style, no-fade, document, update, Danish |
| All-page conformance, WebKit | PASS 70 |
| AdminDesignLocalizationTests + ManagedCompetitionUiTests + AdminShellUiTests + EventCreationUiTests | 20/20 |
| Integration (PostgreSQL): DraftOperations, C11FinalizedRoster, CaptainAuthority, Slice3DestructiveLifecycle (test #14), DraftReadbackB5*, Slice3DraftStart, Slice3ScheduledLifecycle, CaptainScopedNavigation, AdminEventHandlerClassificationTests, Slice1Identity, ParticipantFlow, PublishedContentRetention, AuditAtomicityBatch, AdminDesignShell, AdminDesignIdentity | 365/365 |
| Whole .NET suite | not run (planner) |

## Evidence
- Parity checklist: `parity-checklist.md`; review screenshots and results: `ur/` (`scripts/check-u6-ur.cjs`, 18 scenarios).
- Browser flows: `tests/Bingo.BrowserTests/admin-design-draft.browser.js`, `admin-design-draft-running.browser.js` (both engines in the runner).
- Register rows: DELIVERY_PLAN “Bindings not shown in the design references” (U6 rows).

## Open for the user's look (provisional)
T-24 composition; “Historical paused draft.” wording; lost-connection banner wording; populated team-removal wording.
