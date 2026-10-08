# U7 Board batch gate summary (items 2 and 3)

Branch `claude/u7-board`. Executed by the implementer (fresh sub-agent from `88h-u7-handoff.md`); independent review pending. Planner rulings U7-E1/E2/E3 are provisional (user to confirm).

## Commits
- `ef3186d1` 2a (U7-E1 c): `POST ?handler=RenewEditing` renews only the active holder's lease (same rule as `AdminCollaborationHub.RenewBoardEditing`; no Version, EditControlVersion or audit change; lapsed lease answers `renewed:false`). `admin-board.js` renews on pointer/key/input activity (page and drawer) at most once a minute (`leaseRenewer` in `admin-board-model.js`); a lost lease re-reads the view, the open draft stays. Handler registered (`AdminEventPagePolicies`, Board gate; classification snapshot). Register row.
- `4e1dd61a` gate fix: 15 Board Danish entries collided case-insensitively with existing resx names (MSB3568: `time`, `Board`, `Rows`, `drop`, `drops`, `manual`, …) and were dropped at build (labels showed English). Now `AdminDesign.`-scoped (English fallback entries; existing `AdminDesign.empty/optional` reused); `Board.cshtml` resolves `BoardModel.ScopedLabelKeys`. `board-page.test.js` guards against case-insensitive duplicates.
- `fb215eda` 2a follow-up: the renewal is a declared background POST in the conformance save-path check (`backgroundPostCount`, marker `/* background POST */`, no busy state; default 0 for other pages). "More figures" toggles in place (planning inputs keep identity; the update probe failed at 1280 before).
- `2b7b4b2a` 2b (U7-E2 a): `headerGrowth: { 390: 42 }` on the board registration; the generic check adds only a declared value at that width. Measured: header delta 22.42 px vs summary change −19.58 px at 390 (42.0 px, Chromium and WebKit); 494–1440 unchanged. Register row.
- 2c (U7-E3): no change; terminal working artwork stays refused.
- `b995f2a4` merge `codex/participants-functionality` (`4c4c048e`). One conflict, `SharedResource.da.resx` tail (U7 Board block vs C8/lifecycle entries): kept both; no exact or case-insensitive duplicates; `SharedResource.resx` has only `AdminDesign.` keys (85). Other files auto-merged.

## Checks (executed, final tree)
- `dotnet build Bingo.slnx -c Release`: 0 warnings, 0 errors.
- Parity fixture rebuilt; stale-evidence fixtures regenerated (8 passed). `node scripts/run-browser-tests.cjs`: 117 executions, 116 passed, 1 failed (`admin-design-page-conformance.browser.js` WebKit: “Microtask checkpoint not reached” at identity 1440, not Board). Rerun of that execution alone: pass, 60 geometry cases (12 pages × 5 widths).
- Board conformance (`BINGO_CONFORMANCE_PAGES=board`): Chromium and WebKit, 390/494/860/1280/1440 pass.
- Integration: `Slice6CatalogueAdministrationIntegrationTests.U7*` (incl. new `U7RenewEditingExtendsOnlyTheHoldersLeaseWithoutVersionOrAudit`, terminal refusal now including RenewEditing), test #14 `TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect`, `AdminEventHandlerClassificationTests`: 42 passed, 0 failed, 0 skipped.
- BrowserTests: `AdminDesignLocalizationTests`, `BoardEditingUiTests`: 8 passed. `board-page.test.js`: pass.
- `git diff --check 69f9cf01 HEAD` clean; frozen CSS `cmp` identical (tokens, components).
- Not run: whole .NET suite (planner).
