# U2 item 3 — Events directory implementation and evidence

6 October 2026. Implementer/remediator, not independent reviewer.
Base HEAD: `061ff9bff3e1205ac99257447d73b21a5849a947`, clean when item3 started.
Item3 resumed under the user’s loading/failure ruling. Items4/5 have not started at this checkpoint. No push,
merge, deployment, new worker/reviewer/orchestrator, branch or worktree.

## Resolved question: Events first-visit failed-load composition

User decision, 6 October 2026, `08-decisions.md` “U2 Events directory while loading / on failure”: the proposal below is accepted with same-size `.sk` placeholders for loading counts and summary, empty values without placeholders on failure, query-bound controls/headings in both states, and no remembered counts. Equivalent pre-response unavailable-data states use this same rule with an evidence/register entry. The original question and draft description below are historical evidence, not an outstanding blocker.

Brief67 requires server-rendered reads, E-10 reference loading/failure, and a
stop for every undecided reference difference. The Dashboard loading-header
ruling applies to Dashboard, not this separate Events state.

Frozen `Events.dc.html:135-169` keeps the view/filter toolbar and table headings
outside the loading/error conditions. Its failure is a row inside that table
(`:223-234`, `:906`). `:998-999` blanks view counts only while
`S.loading`; after failure it displays the counts from its already-populated
in-memory world. `:993` and `:113-125` leave the summary empty on failure.
On a first server visit with a failed GET, those counts are unavailable.

The preserved draft differs: `_AdminEventsLoadStates.cshtml:13-15` contains
only the error card. The shell failure branch `admin-design-shell.js:514-522`
removes the loading toolbar/table and leaves the loading header, including
“Loading events…”. It neither fabricates counts nor remembers an earlier
directory payload. The current loading toolbar also uses default filter labels
rather than projecting the destination's known query state.

**Question:** may Events use the reference toolbar/table headings on both
loading and failure, projecting the known destination query state, with blank
view counts until a successful GET, an empty summary on failure, and no cached
or remembered counts on any visit? Or should failure omit that toolbar/table?
The latter is the current draft but is not approved.

Recommended proposal (not implemented/accepted): keep the reference toolbar and
table headings; reuse blank loading-style counts on first and later failures;
clear the loading summary on failure; retain known query state. Register the
unavailable failed-load counts as a binding difference, then prove loading/failure
composition and positions in both engines. No new JSON read or cache proposed.
Production work on item3 stopped when this difference was identified.

## Implemented directory

- AdminDesign directory: reference five-column table, tabs/counts/summary,
  imported and Hidden-date lines, lifecycle-only phase, dates/context,
  capacity/no-capacity/retained participants, prioritized attention and extra
  categories in keyboard-accessible hints, empty/no-match states and retry.
- Server owns 25-row paging/clamping; `phase` and legacy `filter` mapping;
  unknown/inaccessible/incompatible parts produce a notice. Explicit query-only
  binding prevents Razor's `page` route value becoming the paging number.
- Names sort using request-culture case-insensitive comparison with ID ties.
  Hidden default order uses descending persisted HiddenAt, then ID.
  Cancelled counts use retained Confirmed. Visible counts exclude hidden/discarded.
  Duplicate-name population is DB-role-rechecked and includes hidden only for
  SuperAdmin; modal embedding/use remains item4.
- Disposable module: server navigation with replace-state canonical query,
  search debounce, sort announcements, overflow observer scheduled through RAF,
  row navigation and hint Escape. The shared shell owns lifecycle and language.
- No directory auto-reload hook. Old site.js directory function/call, mobile-card
  attributes, DisplayPhase/StatusLabel/readiness queries, orphan event-create.js
  and last old Dashboard row-action CSS retired. Existing constructor signature
  retained for compatibility; no lifecycle/readiness call remains.
- Page-family header marker corrects Dashboard-only CSS matching Events through
  Razor's data-attribute null handling. Dashboard position checks rerun below.
  New Events header uses its family localizer, avoiding old Shared “Bingoer”.
- Shared comparator skips dormant page-loading templates whose stylesheet is
  loaded with their instantiated fragment; actual loading remains a separate
  execution gate. Other shared-template classes remain checked.

Create still follows its existing native route in this item3 draft. Item4 owns
the modal route, dirty/discard, uncertain readback, duplicate notice and success.
No Create behavior completion is claimed.

## A10 / A-Events-4 before and after

Inventory42b Events §8 explicitly names
`EventsDirectoryIntegrationTests.cs:92` for this approved count change.
The file was unchanged and green in item1, as brief67 required for that item.

Before: after participants descending sort, require Cancelled to be the last
row, then require its ParticipantCount to be null.
After: require the Cancelled row's ParticipantCount to be exactly 0 in this
zero-confirmed fixture. Its position is no longer defined by unavailable-last
because its count is now available. New PostgreSQL proof below requires exactly
1 for a Cancelled event with a retained confirmed participant.
All other assertions remain unchanged, including retained/import/missing
participation, authorization, attention categories and ordinal ordering of the
existing capitalized ASCII fixture. New EN/DA mixed-case/ÆØÅ fixtures prove U2-2.

## Executed checks and results

Commands run in the assigned checkout, against owned test PostgreSQL only.

- `dotnet build Bingo.slnx --configuration Release --no-restore -v:minimal`:
  **0 warnings, 0 errors**, 18.19s (incremental, not the final clean-build gate).
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~EventsDirectoryIntegrationTests|FullyQualifiedName~EventQuarantineIntegrationTests' --logger 'trx;LogFileName=u2-item3-directory-quarantine.trx' -v:minimal`:
  **20 passed, 0 failed, 0 skipped**, 45s. Includes authenticated hidden markup/
  legacy mapping and unchanged quarantine/restore/authorization behavior.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~U2EventsDirectoryIntegrationTests' --logger 'trx;LogFileName=u2-item3-new-directory.trx' -v:minimal`:
  **4 passed, 0 failed, 0 skipped**, 6s. Paging/counts/drop notices; EN/DA
  culture order and case-equivalent ID ties; DB-rechecked hidden/duplicate-name
  population; newest HiddenAt; Cancelled confirmed count. Non-microsecond inputs
  Now+1 tick and Now+17 ticks roundtrip through PostgreSQL as Now and Now+10 ticks,
  with exact persisted ordering/assertions and no tolerance.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~AdminShellUiTests|FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~U2DashboardHttpTests' --logger 'trx;LogFileName=u2-item3-http-compatibility.trx' -v:minimal`:
  **33 passed, 0 failed, 0 skipped**, 8s.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~U2EventsDirectoryHttpTests' --logger 'trx;LogFileName=u2-item3-query-http.trx' -v:minimal`:
  **1 passed, 0 failed, 0 skipped**, 1s. Real authenticated 25/6 paging, page999
  clamp, no false route-page notice, invalid parts notice and ordinary-Admin
  hidden exclusion.
- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release --no-restore -v:minimal`:
  final refresh **0 warnings, 0 errors**, 6.78s.
- Bundled Node: `scripts/check-u2-events.cjs`: **10 passed, 0 failed**.
  Chromium/WebKit × 1440/390 × light/dark: **8 comparisons, 0 differences**,
  exact shared styles/icons and positions within 1px. Two behavior executions:
  sort/announcement, phase/search/clear, invalid-link notice, actual language
  cookie/switch, failed-load retry, three Dashboard/Identity/Events cycles,
  Back/Forward and no page errors. Failure recovery passed, **failure composition/
  position parity did not run and is not approved**. Reference normalization
  replaces sample inputs with the actual service rows and applies the approved
  AU04 default order only; frozen file/styles remain untouched.
  Runtime artifacts: `artifacts/u2-events/results.json`, per-case JSON/PNG.
- Bundled Node: `scripts/check-u2-dashboard.cjs`, after header marker correction:
  **16 passed, 0 failed**, both engines. Four loaded comparisons 0 differences;
  ten loading/loaded header position comparisons at 1440/1280/861/860/390 within
  1px; two keyboard/sort/DA/retry/disposal/history executions. Runtime artifacts:
  `artifacts/u2-dashboard/results.json`.

Historical corrective runs: first wrong solution filename MSB1009; Razor
`@page` variable parse and case-insensitive duplicate resource warnings fixed;
new test analyzer CA1861/CA1875 fixed. New EN case-tie test first assumed Alpha
occupied the first two positions; English Ægir precedes it, so the full culture
ordering assertion was retained and tie assertion selects the equivalent names.
Fixture first attempted to serialize PageModel/PageContext; it now exports only
row data. Initial loaded parity exposed false paging notice, header CSS leakage,
All-phases/All and footer-range wording differences; corrected to reference.
Mobile test selectors first targeted hidden theme controls, ambiguous toggles,
then the covered center of the scrim; final test opens/closes with visible
sidebar buttons. Passing results above supersede these failed checkpoints.

## Shared design system checks 1–6 and leftovers

1. Both `cmp` commands comparing shipped tokens/components to reference
   `ui/tokens.css`/`components.css`: exit0, no output. Frozen files and HTML
   references unchanged.
2. Page CSS colour-literal/font-family/shadow/radius/shared-class scan: no hits.
   Scoped .ev-tbl rules mirror the reference page style. Only extra rule is
   .ev-tbl hint dismissal for Escape; behavior/accessibility, no redesign.
3. Events page inline style hits: meter-track dynamic width, meter fill percent,
   reference empty-cell display:contents. No colour/font/spacing literals.
   Loading rows use reference skeleton dimensions. The approved count/summary
   reservations are layout bindings, not edits to frozen components.
4. Shared shell/icon/host partials and frozen component classes used; table is
   reference grid-table markup. No page-specific modal or toast copy in item3.
5. Page JS timer scan: one setTimeout, 250ms search debounce, **not a busy timer**.
   No setInterval/spinner/aria-busy/fetch hit. No saves in the directory module;
   item4 will use shared AdminUI.busy.
6. Shared dirty/fetch/swap scripts retained. Module exposes init/dispose and aborts
   listeners, disconnects observer, cancels frames and search timer. A16 executed
   in both engines as above; the resolved failure composition is executed below.

Inventory §10:
- `StatusLabel|EventDisplayPhase|GetStartReadinessAsync` in Index model: 0 hits.
- Retired directory function/search/sort/hooks/event-create.js in src: 0 hits.
  Tests contain one negative assertion for the removed data-admin-events-control.
- `data-label|Workspace|Inspect|title or slug|asp-page="Create"` in Index view:
  0 hits; temporary native Create href remains until item4.
- `Name = "page"` and `Name = "phase"`: present; page has FromQuery too.
- Hidden-filter leftover: **one authorized exception**,
  `Manage.cshtml:94 asp-route-filter="hidden"`, explicitly retained by brief67
  E-20 until U4. Quarantine HTTP and new PG legacy mapping passed.
- `git diff --check`: exit0. Final clean Release/full JS/whole BrowserTests and
  Claude final-SHA whole .NET gate **not run for this incomplete item3**.

## Resumed loading/failure ruling and final item checks

- Added the DELIVERY_PLAN register row for Events unavailable-data counts/summary.
  Static templates contain labels and placeholders only; no counts are cached.
  The shell projects destination view, phase, search, attention, sort/direction and
  page query into pending controls. Failure clears summary/count placeholders,
  retains the query-bound toolbar/headings and uses the reference error/retry row.
  Failed-state controls can retry another view/search/phase/sort.
- Count boxes reserve two character widths and a fixed line-height; summary reserves
  one line on desktop and two below860px. No numeric value is clipped or abbreviated.
  These data-independent layout reservations are the explicit user-ruling adaptation.
  Rendered-reference normalization contains only the fixture data/AU04 order plus
  these registered reservations and unavailable failure values. Frozen HTML unchanged.
- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj
  --configuration Release --no-restore -v:minimal`: **0 warnings/errors**.
- `node scripts/check-u2-events.cjs`: **26 passed,0 failed**. Both engines:
  loaded parity at1440/390 in both themes, failed-reference composition (all positions,
  text, icons and computed styles), **0 differences**; first visit from Identity at
 1440/1280/861/860/640/390, loading/loaded header, summary, action, toolbar, tabs,
  search, phase, table headings and each count position/size **within1px**.
  Query selection, empty loading counts with .sk, blank failure counts/summary/no .sk,
  retry, EN/DA real culture switch, repeated A16/Back/Forward and zero page errors pass.
- A separate owned PostgreSQL fixture intercepts only the scheduled-opening SELECT
  to fail its read-only attention projection. Both engines render the partial banner
  and all22 visible rows without falsely claiming nothing needs attention. SuperAdmin
  legacy Hidden mapping renders newest-hidden first, Hidden date and
  `Manage/{id}?hidden=true`; no reload hook. No user-owned app/database touched.
- Shell and language browser regressions in Chromium/WebKit: **4 passing executions**.
- Dashboard recheck: **16 passed,0 failed**, both themes/engines and header positions.
  The first recheck detected dormant Events failure-template `.ev-tbl` without its
  page stylesheet; class inventory now excludes both dormant loading/failure
  fragments (not instantiated states). Events failure is explicitly checked above.
- Historical resumed check failures: missing JS parenthesis fixed before fixture;
  count skeleton used10px instead of reserved text line-height (fixed); first-visit
  geometry sampled before page stylesheet finished (waits explicit table CSS + settle);
  failure reference retained whitespace-summary reservation (normalized the approved
  empty value) and concatenated block text (compares separate exact message/recovery
  elements). Final26/0 supersedes these checkpoints, not a skipped assertion.
- `git diff --check` and both frozen CSS `cmp`: exit0/no output. Named §10 retired
  source scans:0 hits. Whole .NET suite remains Claude’s final-SHA gate, unclaimed.

## Changed files (repository-relative)

```
CURRENT_STATUS.md
DELIVERY_PLAN.md
UI_PAGE_MATRIX.md
docs/references/admin-ui/reviews/2026-10-06/u2/item3-events-directory.md
scripts/lib/admin-parity-compare.cjs
scripts/lib/admin-parity-fixture.cjs
scripts/check-u2-events.cjs
src/Bingo.Web/Pages/Admin/Events/Index.cshtml
src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml
src/Bingo.Web/Pages/Shared/_AdminEventsLoadStates.cshtml
src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml
src/Bingo.Web/Pages/Shared/_AdminDesignIcon.cshtml
src/Bingo.Web/Pages/Shared/_AdminDesignLayout.cshtml
src/Bingo.Web/Pages/Shared/_AdminDesignTemplates.cshtml
src/Bingo.Web/Resources/AdminCommunityResource.da.resx
src/Bingo.Web/Resources/SharedResource.da.resx
src/Bingo.Web/wwwroot/css/admin-design-layout.css
src/Bingo.Web/wwwroot/css/admin-design-events.css
src/Bingo.Web/wwwroot/css/site.transitional.application.css
src/Bingo.Web/wwwroot/js/admin-collaboration.js
src/Bingo.Web/wwwroot/js/admin-events.js
src/Bingo.Web/wwwroot/js/admin-design-shell.js
src/Bingo.Web/wwwroot/js/event-create.js (deleted orphan)
src/Bingo.Web/wwwroot/js/site.js
tests/AdminDesignParityFixture/FixtureHost.cs
tests/Bingo.IntegrationTests/EventsDirectoryIntegrationTests.cs
tests/Bingo.IntegrationTests/U2EventsDirectoryIntegrationTests.cs
tests/Bingo.BrowserTests/U2EventsDirectoryHttpTests.cs
tests/Bingo.BrowserTests/admin-design-events.browser.js
```

## Acceptance and next permitted action

Dashboard and Events matrix rows remain **awaiting Claude review, then user
visual acceptance**. No independent review or manual acceptance claimed.
The Events loading/failure question is resolved and all resumed proofs above passed.
Item3 was committed as `90ae954c55805943698408ab4f790336cb53694e`.
Continue item4/Create, item5/UR and final gates; any new product question belongs
to its owning item, not this resolved loading rule.
No U3+, lane T, packaging, push, merge or deployment.
