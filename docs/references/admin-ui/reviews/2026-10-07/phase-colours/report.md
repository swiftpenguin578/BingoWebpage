# Brief78 — phase colours

Implemented as one item on `codex/participants-functionality`, clean parent
`5938b086d0215a70b95b99bd6276a2a30d2a2dd4`. This report belongs to the single
phase-colour commit; resolve its SHA from Git. Direct implementer assignment;
no delegated worker or reviewer. Planner commit check and visual acceptance pending.

## Result

The Web-layer `AdminDesignPhasePresentation.For(EventState)` owns badge class
and dot tone for Events, the sidebar (including `data-event-tone`) and Dashboard.
Future new-layout phase bindings use it. Tone names are `info` (blue) and `done`
(violet); dot names are `tone-closed`, `tone-review` and `tone-done`.

R1–R4 applied: Final review dot amber; Setup dot retains `--dk-text-3` grey;
Dashboard Finalized violet, Provisional amber, Imported · archived neutral;
reference `.dc.html` files/local maps unchanged. Discarded is still excluded from
navigation; the helper gives it an outline/grey defensive mapping. No phase red.
Labels/localisation, geometry, non-phase styling and shell JS unchanged. Existing
Setup-only shell fixture tone data remains valid, so that fixture was not edited.
One delivery-register row records the palette and future binding requirement.

## Values and measured contrast

All starting values retained exactly; no contrast-driven adjustment was needed.

| Tone | Light background | Light foreground | Dark background | Dark foreground |
| --- | --- | --- | --- | --- |
| info | `#e8eef7` | `#2f5b8f` | `rgba(122,162,222,.13)` | `#9fbde6` |
| done | `#efebf6` | `#5f4a94` | `rgba(168,142,220,.13)` | `#c0aee6` |

| Tone/theme | Badge on page | Badge on card | Page | Card surface |
| --- | ---: | ---: | ---: | ---: |
| Blue light | 5.9736 | 5.9736 | 6.4351 | 6.9676 |
| Blue dark | 7.8471 | 7.2260 | 9.5633 | 8.9351 |
| Violet light | 6.1804 | 6.1804 | 6.7025 | 7.2571 |
| Violet dark | 7.5819 | 6.9858 | 9.1618 | 8.5599 |

Ratios are `:1`, computed from browser-computed CSS using sRGB relative luminance
and `(Llighter + .05) / (Ldarker + .05)`. Translucent badge backgrounds are
alpha-composited over both page and card **before** luminance conversion. Light
page/card are `#f6f6f3`/`#ffffff`; dark are `#131416`/`#1a1b1e`.
Chromium and WebKit returned identical values; all 16 ratios per engine pass ≥4.5.
[Computed colours and unrounded ratios](contrast.json).

## Executed checks

All commands ran in the assigned checkout. `NODE` below denotes the existing
`/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.
[Compact results, per-test outcomes and durations](checks.json).

- `dotnet build Bingo.slnx --configuration Release --no-restore`: exit0,
  0 warnings/errors. The initial build found CA1861 in the added switcher test;
  fixed with a static readonly expected-tone array, then reran successfully.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~U2DashboardHttpTests|FullyQualifiedName~U2DashboardPresentationTests|FullyQualifiedName~U2EventsDirectoryHttpTests|FullyQualifiedName~AdminShellUiTests' --logger 'trx;LogFileName=phase78-http.trx' --results-directory artifacts/phase-colours`: **17 passed, 0 failed, 0 skipped**.
  Dashboard HTTP cases cover all current-event phases plus finalized/provisional/imported recap badges.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~SwitcherUsesEligiblePhasesStableStartOrderPastSelectionAndHiddenOverview --logger 'trx;LogFileName=phase78-switcher.trx' --results-directory artifacts/phase-colours`: **1 passed, 0 failed, 0 skipped**, real owned PostgreSQL. Covers current and terminal selected-event tones.
- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release --no-restore`: exit0, 0 warnings/errors.
- `NODE scripts/check-u2-events.cjs`: **28 passed / 0 failed** (14 each engine), including rendered badge classes and switcher tone data; existing geometry/localisation/navigation checks retained.
- `NODE scripts/check-u2-dashboard.cjs`: **18 passed / 0 failed** (9 each engine).
- `PLAYWRIGHT_CHANNEL=chromium NODE tests/Bingo.BrowserTests/admin-design-shell.browser.js` and the same with `PLAYWRIGHT_BROWSER=webkit`: exit0 each.
- `PLAYWRIGHT_BROWSER=chromium NODE tests/Bingo.BrowserTests/admin-design-phase-colours.browser.js` and `PLAYWRIGHT_BROWSER=webkit`: exit0 each; 16 contrast ratios each and all phase dot colour rules in both themes.
- `PLAYWRIGHT_BROWSER=chromium NODE tests/Bingo.BrowserTests/admin-design-css-scope.browser.js` and `PLAYWRIGHT_BROWSER=webkit`: exit0 each; 3 page stylesheets / 94 selectors.
- Both `cmp` pairs: reference `ui/tokens.css` = app `admin-design-tokens.css`;
  reference `ui/components.css` = app `admin-design-components.css`, byte-identical.
- `git diff --check` and staged equivalent: pass. `.dc.html` reference files unchanged from parent.

Full JS runner and whole .NET suite intentionally **not run**; deferred to U3's
end-of-batch gate. No independent review claimed. Owned disposable fixtures only;
user review app/database untouched. No push, merge, deployment or U3 work.

## Next permitted action

Planner/user checks this commit directly, followed by the requested quick Events
and sidebar light/dark visual check in the review environment. Visual acceptance
is pending; no review agent. Stop here.

## Changed files

- `CURRENT_STATUS.md`
- `DELIVERY_PLAN.md`
- `docs/references/admin-ui/reviews/2026-10-07/phase-colours/checks.json`
- `docs/references/admin-ui/reviews/2026-10-07/phase-colours/contrast.json`
- `docs/references/admin-ui/reviews/2026-10-07/phase-colours/report.md`
- `docs/references/admin-ui/ui/components.css`
- `docs/references/admin-ui/ui/tokens.css`
- `scripts/check-u2-events.cjs`
- `src/Bingo.Web/Navigation/SharedShellService.cs`
- `src/Bingo.Web/Pages/Admin/Events/Index.cshtml`
- `src/Bingo.Web/Pages/Admin/Index.cshtml`
- `src/Bingo.Web/UI/AdminDesignPhasePresentation.cs`
- `src/Bingo.Web/wwwroot/css/admin-design-components.css`
- `src/Bingo.Web/wwwroot/css/admin-design-tokens.css`
- `tests/Bingo.BrowserTests/U2DashboardHttpTests.cs`
- `tests/Bingo.BrowserTests/admin-design-phase-colours.browser.js`
- `tests/Bingo.IntegrationTests/AdminDesignShellIntegrationTests.cs`
