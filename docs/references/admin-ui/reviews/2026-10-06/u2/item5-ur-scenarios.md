# U2 item 5 — UR scenarios and final U2 gate

6 October 2026. Implementer checkpoint, not independent review or acceptance.
HEAD `a89ef250b2c1d09fc9453f7a585f6f96a698a0dd` is committed item4. The tree
was clean after that commit. The preserved docs-only checkpoint was resumed after
the planner clarification below. Item5 now implements the required scenarios;
final gate results are recorded below before its single local commit. No user
database, running review environment, lifecycle invariant or existing UR assertion
was changed. This is implementer evidence, not independent source review.

## Resolved planner question (historical checkpoint)

May brief67's “a Live and a Final review event in the history (per profile)” be
satisfied **across the existing profile pair**—Live in `live`, Final review in
`final-review`—while retaining exactly one visible current event in each profile?

That is the recommended scope-preserving clarification. Requiring both rows
simultaneously in each profile conflicts with the accepted UR/lifecycle boundary;
the implementer cannot silently seed an invalid second visible current event,
weaken the existing assertion, expose hidden history, or fabricate a Live hidden
event to satisfy it. If simultaneous rows really are required, the planner must
resolve the conflict explicitly without delegating an unapproved product change.

**Resolved:** the planner accepted the existing profile pair in `08-decisions.md`,
“U2 item 5 Live and Final review history per profile”,6 October. Live is in `live`;
Final review is in `final-review`. Ruling63 holds. Both phase/history proofs and
the unchanged one-current assertion pass on real PostgreSQL and in both browsers.

## Exact evidence

- `/Users/christopher/Documents/BingoWebpage/review-notes/67-codex-brief-u2-events-dashboard.md:76`
  asks for both Live and Final review in history “per profile”. Its line15 accepts
  UR; line106 says a product/reference question stops that item for the planner.
- Accepted `/Users/christopher/Documents/BingoWebpage/review-notes/63-ur-item2-current-and-hidden.md:10,14`
  requires exactly one visible current event per profile; hidden Final review is
  allowed but must not appear as current. CURRENT_STATUS's retained approvals
  explicitly preserve rulings63/64 and the current assignment protects accepted UR.
- `src/Bingo.Infrastructure/Events/EventCurrentBoundary.cs:9,17`: both Live and
  AwaitingFinalReview are current; non-hidden current events are considered by the
  lifecycle boundary. `EventLifecycleService.cs:375-381` refuses another current
  event with `CURRENT_EVENT_EXISTS`. This is real production behavior, not merely
  a seed-test convention.
- `tests/Bingo.IntegrationTests/UiReviewScenarioIntegrationTests.cs:60-62` asserts
  exactly one non-hidden Live/AwaitingFinalReview/Finalized row and the expected
  profile state. Lines105-107 preserve the actual Dashboard card selection;
  existing current/start refusal and hidden-event assertions remain protected.
- `src/Bingo.Infrastructure/Dashboard/AdminDashboardService.cs:50` excludes
  hidden events for **both** Admin and SuperAdmin Dashboard populations. The
  existing hidden Final review therefore cannot be a second Dashboard history
  row. `BingoEvent.cs:194-198` does not permit hiding Live.
- `src/Bingo.Web/TestData/UiReviewScenarioSeeder.cs:56-98` creates/retains hidden
  historical events before the sole visible current row, selecting Live or Final
  review by the profile. No second visible current row currently exists.

The initial checkpoint used targeted `rg`/`sed` and specifically referenced ruling63;
no broad rediscovery. Its earlier stop was not an item5 implementation/pass.

## Implemented scenarios

- Current/upcoming view has28 rows in `live`,27 in `final-review`: page1 has25,
  page2 has3/2. Existing nine upcoming setups remain;13 more include case-only
  alpha/Alpha, Ægir/Ørn/År, no-capacity and no-dates rows. Both request cultures
  use the real language switch/cookie, culture-aware case-insensitive sort and ID ties.
- Existing Cancelled signup-history event retains3 Confirmed website signups,
  each with an owned Playing character/answer from before cancellation. Directory
  shows3 confirmed / when it was cancelled, not a reconstructed played count.
- Existing private setup now has a due postponed-start boundary and a matching,
  state remains Draft and it remains the earliest preparation card in final-review.
  A new Draft has due failed signup opening. **Corrected by brief70 item7**:
  production disables that schedule; the earlier “enabled schedule and persisted
  attempt agree” claim was wrong. The remediated seed derives actual blockers
  from production evaluators and adds both System audits and Admin notifications.
- **Superseded import claim, corrected by brief70 item7:** attaching provenance to
  the platform WOM-unavailable lifecycle did not reproduce any production writer.
  That platform scenario now stays unmodified platform history. A separate
  archived import uses CsvImport participants, frozen board/rosters/results,
  distinct manifest/input/combined hashes, a single Frozen historical import
  transition and historical_import.applied audit. No existing frozen snapshot is
  rewritten; the owned fresh seed reproduces the importer's supported shape.
- Existing visible archived roster history has two official shared first-place
  snapshots, matching identical line/tile/EHB scores. Both winners render. Existing
  current-phase row follows the clarified profile; no second visible current row.
- `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Warning`
  now overrides the exact noisy category in `scripts/ui-review.py` environment,
  including an inherited Information value. No production logging change.
- Printed guide adds paging/culture/attention/cancelled/missing-settings/history
  links. Create points directly to `/Admin/Events?create=1` because brief67's
  authorized legacy GET entry now302s there. **AssertPrintedUrlsAsync remains
  unchanged** and verifies every printed URL200; GET entry302 remains covered by
  AU03/item4/browser tests. All old Audit, role, hidden, cookie/session, discarded,
  lifecycle/board history assertions, including card lines105–107, are unchanged.
- Added assertions to the existing two-profile theory, never replaced expectations;
  owned PostgreSQL17 + controlled Kestrel fixtures prove data and rendered behavior.
  Existing schema/migrations/reset seeder untouched. No user DB or app was touched.

## Focused checks and earlier corrections

- `/usr/bin/python3 scripts/test-ui-review.py`: **13 passed**,0.368s. Existing12
  safety cases retained plus exact EF category override proof.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~UiReviewScenarioIntegrationTests --logger 'trx;LogFileName=u2-item5-ur-final.trx' -v:minimal`: **2/0/0**,23s. Non-microsecond input clock normalizes once; new persisted attempt timestamps and schedule equality remain exact at microseconds. Both roles, both profiles, full old assertions, all guide URLs, new service/HTTP/paging/culture/attention/winner/import/missing-settings proofs pass.
- `env BINGO_PARITY_ENGINES=chromium,webkit <bundled-node> scripts/check-u2-ur.cjs`:
  **4 passed,0 failed**, both profiles/engines,0 page errors. Real UR seed,25+3/2
  paging, cancellation, cultures, attention, no dates/capacity, imported/shared-first
  history and phase. Two repeated Dashboard→Events→Identity cycles with Back/Forward
  per run; detached search/Create listener probes do nothing, one active script,
  no abandoned modal. `tests/Bingo.BrowserTests/admin-design-ur.browser.js` includes
  this proof in the full runner. Ignored synthetic results: `artifacts/u2-ur-browser/`.
- Initial compilation of the new assertions required the actual `SignupStatus`
  property/namespace and static winner-name array (CA1861); corrected. Initial
  runtime caught the guide's retired standalone Create link302; corrected the
  guide's target, not the unchanged printed-URL assertion. New row regex initially
  counted sidebar attributes; now scoped to directory row divs. New winner text
  assertion assumed alphabetic display order; production uses stable TeamId order,
  so assert both independent winner identities/placements and their actual rendered
  joined order. Browser detached-anchor probe initially performed native navigation;
  now cancels only its default action before testing disposed handlers. No product
  assertions or existing expectations were weakened. Those failed checkpoints are
  superseded by the full passing proofs above.

## Shared-check3 mechanical correction

The final inline scan found reference skeleton margins still inline. Moved those
fixed margins/heights to uniquely named Dashboard/Events layout classes and use
existing frozen radius tokens for6/8/10px. No shared class is redefined, no new
tokens/components and no visual geometry change. Source of the aliases is the
reference loading markup, not a new design decision; explicitly justified because
these reference inline styles are outside its style block. Approved loading header
adapters remain registered. Final loading/loaded position parity is rechecked by
the full runner after these exact mechanical edits.

## Completed item checkpoints

Item4: `a89ef250b2c1d09fc9453f7a585f6f96a698a0dd`,17 files. Exact file list,
commands, final results and superseded failures are in `item4-create-modal.md`:
Create10/0 Chromium/WebKit, eight modal parity comparisons0 differences; HTTP
32/0/0, PostgreSQL11/0/0, shared shell2/language2 passing executions, fixture
incremental Release0 warnings/errors, diff check exit0, frozen CSS/HTML unchanged.
Items0–3 remain committed and their existing one-file-per-item evidence is reused.

## Final implementer batch gate — passed

Commands in the assigned checkout; source is exactly the implementation included
in item5's single commit. Final checks ran before committing as requested; only
delivery/evidence text changed afterward. No final-SHA whole .NET pass is claimed.

1. `dotnet clean Bingo.slnx --configuration Release -v:minimal`: exit0.
   `dotnet build Bingo.slnx --configuration Release --no-restore -v:minimal`:
   **0 warnings,0 errors**,21.79s. Clean, not an incremental-only claim.
2. `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release --no-restore -v:minimal`:
   **0 warnings,0 errors**,2.24s; includes latest compiled Razor/static source.
3. `env BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=<checkout>/artifacts/js-fixtures dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction --logger 'trx;LogFileName=u2-final-js-fixtures.trx' -v:minimal`:
   **8/0/0**,12s; freshly generated real owned PostgreSQL stale fixtures, no user DB.
4. `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-build --logger 'trx;LogFileName=u2-final-browser.trx' -v:minimal`:
   **157 passed,0 failed,0 skipped**,11s. The **whole BrowserTests**, not a filter.
5. `env BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=<checkout>/artifacts/js-fixtures PLAYWRIGHT_CHANNEL=chromium <bundled-node> scripts/run-browser-tests.cjs`:
   **59 passed,0 failed,59 total**.12 Chromium,12 WebKit,35 unchanged legacy/default
   executions, every repository JS file. Standard runner pairs all Admin-design
   and Identity files; legacy/unit programs keep the existing runner semantics.
   Final Dashboard16/0, Events26/0, Create10/0, UR4/0; Identity and shared fetch/
   shell/language/theme paired regressions pass. All reference comparisons **0
   differences**. Dashboard loading/loaded positions within1px at1440/1280/861/
   860/390; Events at1440/1280/861/860/640/390, both engines. EN/DA real culture
   switches, both themes, failure/Retry and repeated A16/Back/Forward pass. These
   are after the skeleton mechanical correction. Ignored log metadata is copied
   compactly below for durability; per-comparison JSON/PNG remain synthetic artifacts.
6. `git diff --check`: exit0. Both frozen CSS `cmp` commands exit0/no output;
   frozen CSS/reference `git diff --name-only` from exact baseline: empty. Shared
   checks1–6 and §10 commands/outputs below explicitly record allowed residuals.

Bundled node = `/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.
`<checkout>` = `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
TRX paths are under each test project's ignored `TestResults/`; compact counters,
exact commands and superseded checkpoints are durable here. No raw SQL/credentials
or real participant data committed. No user-owned review create/refresh command
was executed; new scenarios will be available on the next authorized fresh review
create/refresh. Controlled fixtures exercised the real seeder and HTTP/browser pages.

## Shared design system checks1–6 — final implementer evidence

These are scoped implementer checks for both assigned pages, not independent
source review. `rg` exit1 below means zero matches, not a failed test.

### 1. Frozen shared CSS

```sh
cmp src/Bingo.Web/wwwroot/css/admin-design-tokens.css docs/references/admin-ui/ui/tokens.css
```

Exit 0; no output.

```sh
cmp src/Bingo.Web/wwwroot/css/admin-design-components.css docs/references/admin-ui/ui/components.css
```

Exit 0; no output.

```sh
git diff --name-only 8355680a4eee6a74ae905c5c69a8f50c5f021dcf -- src/Bingo.Web/wwwroot/css/admin-design-tokens.css src/Bingo.Web/wwwroot/css/admin-design-components.css docs/references/admin-ui/ui/tokens.css docs/references/admin-ui/ui/components.css docs/references/admin-ui/Dashboard.dc.html docs/references/admin-ui/Events.dc.html
```

Exit 0; no output.

### 2. Page CSS / reference layout only

```sh
rg -n '(^|[ :])#[0-9a-fA-F]{3,8}\b|rgb\(|hsl\(|font-family|box-shadow|border-radius|^\.(btn|card|modal|drawer|toast|pill|tbl|form-|banner)([ :.{-]|$)' src/Bingo.Web/wwwroot/css/admin-design-dashboard.css src/Bingo.Web/wwwroot/css/admin-design-events.css
```

Exit 0; output:

```text
src/Bingo.Web/wwwroot/css/admin-design-events.css:1:.tbl.ev-tbl{--table-cols:minmax(230px,1.5fr) 152px minmax(190px,1.1fr) minmax(170px,1fr) minmax(224px,1.3fr);--table-min:990px}
src/Bingo.Web/wwwroot/css/admin-design-events.css:10:.events-sk-phase{height:20px;border-radius:var(--dk-radius-xl)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:12:.tbl.hist{--table-cols:minmax(240px,2.2fr) 96px 170px 170px 124px minmax(150px,1.3fr);--table-min:960px}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:14:.dash-sk-stat-value{height:20px;margin-top:var(--dk-space-3);border-radius:var(--dk-radius-xs)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:16:.dash-sk-chart{height:200px;margin-top:var(--dk-space-5);border-radius:var(--dk-radius-md)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:17:.dash-sk-recap{height:64px;margin-top:var(--dk-space-5);border-radius:var(--dk-radius-md)}
```

```sh
rg -n '(^|[ :])#[0-9a-fA-F]{3,8}\b|rgb\(|hsl\(|font-family|box-shadow|border-radius:[^v]' src/Bingo.Web/wwwroot/css/admin-design-dashboard.css src/Bingo.Web/wwwroot/css/admin-design-events.css
```

Exit 1; no output.

```sh
rg -n 'dash-sk-|events-sk-|^\.tbl\.' src/Bingo.Web/wwwroot/css/admin-design-dashboard.css src/Bingo.Web/wwwroot/css/admin-design-events.css
```

Exit 0; output:

```text
src/Bingo.Web/wwwroot/css/admin-design-events.css:1:.tbl.ev-tbl{--table-cols:minmax(230px,1.5fr) 152px minmax(190px,1.1fr) minmax(170px,1fr) minmax(224px,1.3fr);--table-min:990px}
src/Bingo.Web/wwwroot/css/admin-design-events.css:10:.events-sk-phase{height:20px;border-radius:var(--dk-radius-xl)}
src/Bingo.Web/wwwroot/css/admin-design-events.css:11:.events-sk-start{margin-bottom:var(--dk-space-2)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:12:.tbl.hist{--table-cols:minmax(240px,2.2fr) 96px 170px 170px 124px minmax(150px,1.3fr);--table-min:960px}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:14:.dash-sk-stat-value{height:20px;margin-top:var(--dk-space-3);border-radius:var(--dk-radius-xs)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:15:.dash-sk-stat-note{margin-top:var(--dk-space-3)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:16:.dash-sk-chart{height:200px;margin-top:var(--dk-space-5);border-radius:var(--dk-radius-md)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:17:.dash-sk-recap{height:64px;margin-top:var(--dk-space-5);border-radius:var(--dk-radius-md)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:18:.dash-sk-fact-first{margin-top:var(--dk-space-5)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:19:.dash-sk-fact{margin-top:14px}
```

The two `.tbl` rules set only the reference table-layout custom properties;
they do not replace shared table styling. Remaining radius declarations consume
existing frozen radius tokens, not literals. Unique skeleton aliases transfer
reference inline geometry (`Dashboard.dc.html:132,137–138`,
`Events.dc.html:175–176`) out of markup. The14px repeated Dashboard fact gap is
the reference's exact geometry, not a new spacing token. Header placeholder and
query-bound loading/failure adapters have approved DELIVERY_PLAN bindings.
Earlier item2/3 evidence records page-style provenance and approved adapters;
final Chromium/WebKit comparisons remain0 differences after the mechanical edit.

### 3. Inline styles

```sh
rg -n 'style="[^"]*(color|font|margin|padding|gap)' src/Bingo.Web/Pages/Admin/Index.cshtml src/Bingo.Web/Pages/Admin/Events/Index.cshtml src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml src/Bingo.Web/Pages/Shared/_AdminEventsLoadStates.cshtml src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml
```

Exit 1; no output.

Full `rg -n 'style='` over these same six markup files returned24 matching lines
(exit0). Remaining inline values are dynamic bar/meter percentages and chart
tick positions; reference widths/heights, grid custom properties and skeleton
absolute positioning; `min-width:0`, `display:contents` and flex sizing; the
Create button's132px minimum width; and the header skeleton's frozen radius
token. No inline colours, fonts or margin/padding/gap. Static skeleton spacing
is now in the unique page layout aliases above. Exact full remaining scan:

```sh
rg -n 'style=' src/Bingo.Web/Pages/Admin/Index.cshtml src/Bingo.Web/Pages/Admin/Events/Index.cshtml src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml src/Bingo.Web/Pages/Shared/_AdminEventsLoadStates.cshtml src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml
```

Exit 0; output:

```text
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml:31:    <div class="mf-foot"><button class="btn" id="cm-cancel" type="button">@D["Cancel"]</button><button class="btn btn-primary" id="cm-submit" type="button" style="min-width:132px"><span class="spin" hidden></span><span data-component-text>@D["Create event"]</span></button></div>
src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml:24:    @if (Model) { <div class="empty is-error" role="row"><div role="cell" style="display:contents"><div class="empty-ic"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("error")' /></div><div class="empty-title">@D["Couldn’t load events"]</div><div class="empty-text">@D["Check your connection and try again. Nothing was changed."]</div><button class="btn" type="button" data-load-retry>@D["Try again"]</button></div></div> }
src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml:25:    else { @foreach (var width in new[] { 52, 38, 46, 60, 34, 48, 42 }) { <div class="tr sk-row" role="row" aria-hidden="true"><div class="td c-name"><div class="sk" style="width:@(width)%"></div></div><div class="td"><div class="sk events-sk-phase" style="width:62%"></div></div><div class="td"><div class="sk events-sk-start" style="width:70%"></div><div class="sk" style="width:44%;height:8px"></div></div><div class="td"><div class="sk" style="width:56%"></div></div><div class="td"><div class="sk" style="width:40%"></div></div></div> } }
src/Bingo.Web/Pages/Shared/_AdminEventsLoadStates.cshtml:3:    <header class="page-head" data-page-family="events"><div style="min-width:0"><h1 class="h1" tabindex="-1">@D["Events"]</h1><p class="summary"><span data-events-summary-placeholder aria-hidden="true"><span class="sk"></span><span class="sk"></span></span></p></div><div class="head-actions"><a class="btn btn-primary" href="/Admin/Events/Create" data-create-event><partial name="_AdminDesignIcon" model='new AdminDesignIcon("plus")' />@D["Create event"]</a></div></header>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:3:    <header class="page-head" data-page-family="dashboard"><div style="min-width:0"><h1 class="h1" tabindex="-1">@T["Dashboard"]</h1><p class="summary" aria-hidden="true"><span>&nbsp;</span></p></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:5:        <div class="next-label" style="position:relative">&nbsp;<span class="sk" style="position:absolute;width:30%;top:5px"></span></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:6:        <div class="next-name" style="position:relative">&nbsp;<span class="sk" style="position:absolute;width:75%;top:5px"></span></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:7:        <div class="next-meta" style="position:relative">&nbsp;<span class="sk" style="position:absolute;width:55%;top:5px"></span></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:8:    </div><span class="sk" style="width:56px;height:6px;flex:none"></span><span class="sk" style="width:52px;height:28px;flex:none;border-radius:var(--dk-radius-xs)"></span></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:16:        <div class="stat"><div class="sk" style="width:46%"></div><div class="sk dash-sk-stat-value" style="width:38%"></div><div class="sk dash-sk-stat-note" style="width:62%;height:8px"></div></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:20:        <div class="card panel"><div class="sk" style="width:30%"></div><div class="sk dash-sk-chart"></div></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:21:        <div class="card panel"><div class="sk" style="width:40%"></div><div class="sk dash-sk-recap"></div><div class="sk dash-sk-fact-first" style="width:80%"></div><div class="sk dash-sk-fact" style="width:70%"></div><div class="sk dash-sk-fact" style="width:75%"></div></div>
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:73:        @if (item.IsPreparation && item.ParticipantCap is { } cap) { <div class="meter" title="@D["{0} of {1} places confirmed", item.Confirmed, cap]"><div class="meter-track" style="--meter-w:44px"><div class="meter-fill" style="width:@(Bingo.Web.Pages.Admin.IndexModel.Percent(cap > 0 ? Math.Min(1m, (decimal)item.Confirmed / cap) : 0))%"></div></div><span class="meter-label @(item.State == EventState.Draft && item.Confirmed == 0 ? "is-muted" : "")">@Model.PeopleMain(item)</span></div> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:83:else { <div class="empty" role="row"><div role="cell" style="display:contents"><div class="empty-ic"><partial name="_AdminDesignIcon" model='new AdminDesignIcon(hasFilters ? "search" : Model.ActiveView == "hidden" ? "hidden" : "calendar")' /></div><div class="empty-title">@D[emptyTitle]</div><div class="empty-text">@D[emptyText]</div>@if (hasFilters) { <button type="button" class="btn" data-directory-url="@Model.DirectoryUrl(view: Model.ActiveView == "hidden" ? "hidden" : "all", phase: "all", search: "", attention: false)">@D["Clear filters"]</button> } else if (Model.ActiveView != "hidden" && (Model.VisibleCount == 0 || Model.ActiveView == "current")) { <a class="btn @(Model.VisibleCount == 0 ? "btn-primary" : "")" href="/Admin/Events/Create" data-create-event>@D["Create event"]</a> }</div></div> }
src/Bingo.Web/Pages/Admin/Index.cshtml:37:                <div class="meter" title="@D["{0} of {1} places filled", card.ConfirmedParticipants, cap]"><div class="meter-track" style="--meter-w:56px"><div class="meter-fill" style="width:@(Bingo.Web.Pages.Admin.IndexModel.Percent(cap > 0 ? Math.Min(1m, (decimal)card.ConfirmedParticipants / cap) : 0))%"></div></div><span class="meter-label">@card.ConfirmedParticipants/@cap</span></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:73:            <div class="chart" id="chart" style="--bar-count:@data.Chart.Count">
src/Bingo.Web/Pages/Admin/Index.cshtml:74:                <div class="chart-plot" style="--chart-h:208px">
src/Bingo.Web/Pages/Admin/Index.cshtml:78:                    <div class="chart-gridline @(tick == 0 ? "is-base" : "")" style="top:@top%"></div><div class="chart-tick" style="top:@top%">@Bingo.Web.Pages.Admin.IndexModel.Number(tick)</div>
src/Bingo.Web/Pages/Admin/Index.cshtml:88:                    else if (point.IsHistoricalImport || point.TrackingStarts) { <span class="bar-seg @(point.IsHistoricalImport ? "s-muted" : "s-2" + provisional)" style="height:@(Height(point.Participants.Value))px"></span> }
src/Bingo.Web/Pages/Admin/Index.cshtml:89:                    else { <span class="bar-seg @("s-1" + provisional)" style="height:@(Math.Max(0, Height(point.ReturningWebsiteParticipants.Value) - 1))px"></span><span class="bar-seg @("s-2" + provisional)" style="height:@(Math.Max(0, Height(point.NewWebsiteParticipants.Value) - 1))px"></span> }
src/Bingo.Web/Pages/Admin/Index.cshtml:122:        <div class="highlight @(recap.Provisional ? "is-muted" : "")"><div class="highlight-ic"><svg class="ic" viewBox="0 0 16 16">@if (recap.Provisional) { <circle cx="8" cy="8" r="5.5"/><path d="M8 5v3l2 1.5"/> } else { <path d="M5 2.5h6v3.5a3 3 0 0 1-6 0zM5 3.5H3v1a2 2 0 0 0 2 2M11 3.5h2v1a2 2 0 0 1-2 2M8 9v2.5M5.5 13.5h5M6.5 11.5h3"/> }</svg></div><div style="min-width:0;flex:1"><div class="highlight-label">@D[recap.Provisional ? "Results provisional" : "Winner"]</div><div class="highlight-value">@(recap.Provisional ? D["Final review in progress"].Value : Model.Winners(recap.Winners))</div><div class="highlight-sub">@(recap.Provisional ? lastOfficial is null ? D["The winner is shown once results are official."].Value : Model.L("Last official winner: {0} · {1}", Model.Winners(lastOfficial.Winners), lastOfficial.EventName) : Model.L("{0} teams competed", recap.TeamCount))</div></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:123:        @if (recap.WinnerBoard is { } board) { <div class="meter" title="@D["Winner completed {0} of {1} tiles", board.CompletedTiles, board.TotalTiles]"><div class="meter-track" style="--meter-w:48px"><div class="meter-fill" style="width:@Bingo.Web.Pages.Admin.IndexModel.Percent(board.CompletionRatio)%"></div></div><span class="meter-label">@board.CompletedTiles/@board.TotalTiles</span></div> }
src/Bingo.Web/Pages/Admin/Index.cshtml:152:        <div class="td" role="cell">@if (row.WinnerBoard is { } board) { <div class="meter"><div class="meter-track"><div class="meter-fill" style="width:@Bingo.Web.Pages.Admin.IndexModel.Percent(board.CompletionRatio)%"></div></div><span class="meter-label">@board.CompletedTiles/@board.TotalTiles @D["tiles"]</span></div> } else { var hint = D[row.Provisional ? "Shown once results are official." : "Not recorded."].Value; <span class="hint tip-start muted" tabindex="0" aria-label="@hint">—<span class="hint-tip" aria-hidden="true">@hint</span></span> }</div>
src/Bingo.Web/Pages/Admin/Index.cshtml:159:<section class="dash-section fade-in" aria-labelledby="comm-title"><div class="section-head"><h2 class="panel-title" id="comm-title">@D["Community"]</h2><span class="panel-aside">@D["Current figures"]</span></div><div class="card"><div class="stat-strip is-compact" style="--stat-cols:3">
```

### 4. Shared markup components

```sh
rg -n 'partial|class="(card|banner|tbl|modal|btn|hint|meter|pill)|_AdminDesign' src/Bingo.Web/Pages/Admin/Index.cshtml src/Bingo.Web/Pages/Admin/Events/Index.cshtml src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml
```

Exit0,43 source lines matched: shell `_AdminDesignLayout`, shared icon/banner/
template partials, Create template using shared modal/form/button classes, and
reference `card`, `tbl`, `hint`, `meter`, `pill`, `btn`, empty/pager classes.
No page-specific replacement component framework. Frozen components byte proof
above and final parity/behavior executions cover their actual rendered use.

### 5. Shared busy behavior

```sh
rg -n 'setTimeout|setInterval|spinner|aria-busy|\.busy\(|fetch\(' src/Bingo.Web/wwwroot/js/admin-dashboard.js src/Bingo.Web/wwwroot/js/admin-events.js src/Bingo.Web/wwwroot/js/admin-event-create.js
```

Exit 0; output:

```text
src/Bingo.Web/wwwroot/js/admin-event-create.js:18:    panel.setAttribute('aria-busy', String(pending()));
src/Bingo.Web/wwwroot/js/admin-event-create.js:58:    const response=await ui.busy(()=>window.AdminFetch.request(checking?'/Admin/Events/Create?handler=CheckAgain&requestId='+requestId:'/Admin/Events/Create',{
src/Bingo.Web/wwwroot/js/admin-events.js:24:  listen(search, 'input', () => { clearTimeout(timer); timer = setTimeout(searchNow, 250); });
```

Every Create POST/Check again uses shared `ui.busy` + `AdminFetch`. The only own
timer is Events'250ms search debounce, not a busy-state timer; disposed on swap.
`aria-busy` reports semantic pending state, not custom timing/spinner machinery.
Dashboard/directory reads use shared swap loading; native link/sort actions are
navigation, not saves. No custom busy spinner/timer. Item4 and final paired Create
checks exercise pending, quick success, uncertain recovery and blocked navigation.

### 6. Shared dirty/fetch/lifecycle integration

```sh
rg -n 'dispose|AbortController|registerDraft|AdminFetch|AdminUI|export function' src/Bingo.Web/wwwroot/js/admin-dashboard.js src/Bingo.Web/wwwroot/js/admin-events.js src/Bingo.Web/wwwroot/js/admin-event-create.js
```

Exit 0; output:

```text
src/Bingo.Web/wwwroot/js/admin-event-create.js:3:export function disposeCreate() { cleanup?.(); cleanup = null; }
src/Bingo.Web/wwwroot/js/admin-event-create.js:4:export function initCreate(region, ui) {
src/Bingo.Web/wwwroot/js/admin-event-create.js:5:  disposeCreate();
src/Bingo.Web/wwwroot/js/admin-event-create.js:8:  const t = key => labels[key] || key, life = new AbortController();
src/Bingo.Web/wwwroot/js/admin-event-create.js:58:    const response=await ui.busy(()=>window.AdminFetch.request(checking?'/Admin/Events/Create?handler=CheckAgain&requestId='+requestId:'/Admin/Events/Create',{
src/Bingo.Web/wwwroot/js/admin-event-create.js:82:    unregister=ui.registerDraft(source,{isDirty:dirty,isPending:pending,confirmLeave:askDiscard,discard:()=>{modal?.element.querySelector('#cm-name')&&(modal.element.querySelector('#cm-name').value='');if(modal)modal.element.querySelector('#cm-tz').value='Europe/Copenhagen';status='idle';}});
src/Bingo.Web/wwwroot/js/admin-events.js:2:import { initCreate, disposeCreate } from './admin-event-create.js';
src/Bingo.Web/wwwroot/js/admin-events.js:4:export function dispose() { disposeCreate(); release?.(); release = null; }
src/Bingo.Web/wwwroot/js/admin-events.js:5:export function init(region, ui = window.AdminUI) {
src/Bingo.Web/wwwroot/js/admin-events.js:6:  dispose();
src/Bingo.Web/wwwroot/js/admin-events.js:13:  const life = new AbortController();
src/Bingo.Web/wwwroot/js/admin-dashboard.js:3:export function dispose() { release?.(); release = null; }
src/Bingo.Web/wwwroot/js/admin-dashboard.js:4:export function init(region, ui = window.AdminUI) {
src/Bingo.Web/wwwroot/js/admin-dashboard.js:5:  dispose();
src/Bingo.Web/wwwroot/js/admin-dashboard.js:8:  const lifetime = new AbortController();
```

Page modules initialize/dispose under the shell, abort their own event listeners,
and register dirty/pending/approved uncertainty departure with `ui.registerDraft`.
Create actions use shared `AdminFetch` for C-CMP-2. No copied shell guard/fetch/
swap implementation. Final paired shell/fetch/Identity tests, lost-session302 HTTP
proofs, UR repeated switches/Back/Forward and disposed-listener probes pass.

## §10 leftover checks — exact outputs

### D retired markup

```sh
rg -n 'admin-dashboard-wip|admin-dashboard-retained|UpcomingMilestones|WiseOldManSynchronizations|RecentAudits|PendingEvidenceSummary|LifecycleReadiness' src
```

Exit 1; no output.

### D retired dependencies

```sh
rg -n 'IEventCompetitionSynchronizationService|GetStartReadinessAsync|AuditEntries' src/Bingo.Web/Pages/Admin/Index.cshtml.cs
```

Exit 1; no output.

### D service integration

```sh
rg -n 'IAdminDashboardService|ICommunityDashboardService' src/Bingo.Web/Pages/Admin/Index.cshtml.cs
```

Exit 0; output:

```text
16:public sealed class IndexModel(IAdminDashboardService dashboard, IStringLocalizer<AdminCommunityResource> text) : PageModel
```

### D Admin routing

```sh
rg -n '"/Admin"' src/Bingo.Web/Navigation/SharedShellService.cs src/Bingo.Web/Pages/Shared/_AdminLayout.cshtml
```

Exit 0; output:

```text
src/Bingo.Web/Pages/Shared/_AdminLayout.cshtml:248:                <a class="admin-nav-link @(isCurrent("/Admin") ? "is-active" : null)" href="/Admin" aria-current="@(isCurrent("/Admin") ? "page" : null)"><svg class="admin-nav-icon" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8"><rect width="7" height="9" x="3" y="3" rx="1" /><rect width="7" height="5" x="14" y="3" rx="1" /><rect width="7" height="9" x="14" y="12" rx="1" /><rect width="7" height="5" x="3" y="16" rx="1" /></svg>@T["Dashboard"]</a>
```

### D raw unavailable

```sh
rg -n 'UnavailableReason' src/Bingo.Web
```

Exit 1; no output.

### E retired readiness

```sh
rg -n 'StatusLabel|EventDisplayPhase|GetStartReadinessAsync' src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs
```

Exit 1; no output.

### E retired hooks

```sh
rg -n 'initializeAdminEventDirectorySearch|data-admin-event-directory-search|data-admin-event-sort-link' src
```

Exit 1; no output.

### E cards

```sh
rg -n 'data-label=' src/Bingo.Web/Pages/Admin/Events/Index.cshtml
```

Exit 1; no output.

### E old wording

```sh
rg -n '\["Workspace"\]|\["Inspect"\]|title or slug' src/Bingo.Web/Pages/Admin/Events/Index.cshtml
```

Exit 1; no output.

### E old hidden links

```sh
rg -n 'filter = "hidden"|asp-route-filter="hidden"|filter=hidden' src
```

Exit 0; output:

```text
src/Bingo.Web/Pages/Admin/Events/Manage.cshtml.cs:178:        return result.Succeeded ? RedirectToPage("Index", new { filter = "hidden" }) : RedirectToPage(new { id });
src/Bingo.Web/Pages/Admin/Events/Manage.cshtml:94:        <p><a class="btn event-create-cancel" asp-page="Index" asp-route-filter="hidden">@T["Back to hidden events"]</a></p>
```

### E query binding

```sh
rg -n 'name = "page"|Name = "page"|Name = "phase"' src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs
```

Exit 0; output:

```text
36:    [BindProperty(SupportsGet = true, Name = "phase")]
39:    [BindProperty(SupportsGet = true, Name = "page")]
40:    [FromQuery(Name = "page")]
```

### E retired create file

```sh
rg -n 'event-create\.js' src tests
```

Exit 0; output:

```text
src/Bingo.Web/wwwroot/js/admin-events.js:2:import { initCreate, disposeCreate } from './admin-event-create.js';
```

### E create entry

```sh
rg -n 'asp-page="Create"|asp-page="/Admin/Events/Create"' src
```

Exit 1; no output.

Allowed residuals: Manage's two legacy Hidden links are expressly retained by
brief67:26/E-20 until U4; native mapping proof is retained from item3 and the
whole157-case HTTP BrowserTests pass. The sole substring `event-create.js` hit
is the approved new `admin-event-create.js` module, not the retired script.
The old-layout `/Admin` hit is the genuine Dashboard navigation link, not the
retired Admin-actions destination. Directory query normalization/legacy Hidden/
dropped parts/clamped paging is covered by passing item3 native HTTP/PG proofs
and final BrowserTests. No unapproved cleanup of retained Manage behavior.

```sh
test ! -e src/Bingo.Web/wwwroot/js/event-create.js
```

Exit 0; no output.

```sh
rg -n '<script[^>]*["/]event-create\.js' src tests
```

Exit 1; no output.

## Full JS runner manifest — final source

All59 executions passed with exit0 and no signal; cumulative test-process elapsed
time256428ms (sum of executions, **not** runner wall time). Copied from ignored
`artifacts/js-tests/results.json`; no participant data or credentials.

```text
account-manage-dialog.test.js | default | PASS | 38ms
account-support.browser.js | default | PASS | 2525ms
admin-confirmation-dismiss.browser.js | default | PASS | 603ms
admin-confirmation-navigation.browser.js | default | PASS | 1688ms
admin-confirmation.browser.js | default | PASS | 1034ms
admin-design-create.browser.js | chromium | PASS | 18282ms
admin-design-create.browser.js | webkit | PASS | 17594ms
admin-design-dashboard.browser.js | chromium | PASS | 12382ms
admin-design-dashboard.browser.js | webkit | PASS | 13334ms
admin-design-events.browser.js | chromium | PASS | 25220ms
admin-design-events.browser.js | webkit | PASS | 25592ms
admin-design-fetch.browser.js | chromium | PASS | 1306ms
admin-design-fetch.browser.js | webkit | PASS | 9016ms
admin-design-language.browser.js | chromium | PASS | 1390ms
admin-design-language.browser.js | webkit | PASS | 905ms
admin-design-shell.browser.js | chromium | PASS | 15889ms
admin-design-shell.browser.js | webkit | PASS | 12107ms
admin-design-theme.browser.js | chromium | PASS | 999ms
admin-design-theme.browser.js | webkit | PASS | 650ms
admin-design-ur.browser.js | chromium | PASS | 32256ms
admin-design-ur.browser.js | webkit | PASS | 31282ms
admin-lifecycle-confirmation.browser.js | default | PASS | 1270ms
admin-review-queue.test.js | default | PASS | 23ms
admin-stale-change.browser.js | default | PASS | 1764ms
board-dialog.test.js | default | PASS | 30ms
board-objective-kind.test.js | default | PASS | 23ms
captain-ledger.test.js | default | PASS | 36ms
catalogue-admin.test.js | default | PASS | 44ms
catalogue-deletion.browser.js | default | PASS | 4954ms
draft-add-team-dialog.test.js | default | PASS | 37ms
draft-roster-dialog.test.js | default | PASS | 44ms
drop-announcement-races.test.js | default | PASS | 38ms
drop-announcement-reconciliation.test.js | default | PASS | 34ms
drop-announcement.test.js | default | PASS | 26ms
event-create-datetime.test.js | default | PASS | 23ms
identity-binding.browser.js | chromium | PASS | 1948ms
identity-binding.browser.js | webkit | PASS | 1418ms
identity-pointer-save.browser.js | chromium | PASS | 752ms
identity-pointer-save.browser.js | webkit | PASS | 641ms
identity-readback.transport.js | chromium | PASS | 1761ms
identity-readback.transport.js | webkit | PASS | 1007ms
identity-timezone-confirmation.browser.js | chromium | PASS | 1886ms
identity-timezone-confirmation.browser.js | webkit | PASS | 1300ms
onboarding-wom-fetch.test.js | default | PASS | 28ms
participant-add-dialog.test.js | default | PASS | 34ms
participant-edit-dialog.test.js | default | PASS | 35ms
participants-ui.test.js | default | PASS | 39ms
public-countdown.test.js | default | PASS | 33ms
public-evidence.test.js | default | PASS | 30ms
public-header-popover.test.js | default | PASS | 26ms
public-leaderboards.test.js | default | PASS | 68ms
public-recent-drops-live.test.js | default | PASS | 144ms
public-recent-drops.test.js | default | PASS | 1141ms
schedule-readback.transport.js | default | PASS | 87ms
signup-code-settings.browser.js | default | PASS | 680ms
signup-questions-overlay.test.js | default | PASS | 29ms
stats-production.test.js | default | PASS | 10815ms
team-board-overlay.test.js | default | PASS | 64ms
transient-toast.test.js | default | PASS | 24ms
```

## Acceptance / limitations / next permitted action

No unresolved product question. All requested implementer gates above pass. Claude's
independent review and **whole .NET suite on final SHA** remain pending; implementer
did not run that whole suite and does not claim it passed. GitHub/Linux execution
and manual visual acceptance are not claimed. Dashboard and Events directory remain
**awaiting Claude review, then user visual acceptance**. Next permitted action:
planner/Claude reviews the stable items0–5 checkpoint and runs its final-SHA suite;
then the user accepts the two pages visually. Stop here. No self-review/independent
pass, U3+, laneT, packaging, push, merge or deployment.

## Per-item commit and changed-file handoff

Exact committed file manifests from `git show --format= --name-only <sha>`;
this is delivery bookkeeping, not independent source review. Per-item evidence
holds exact checks/results, A10 before/after and earlier superseded checkpoints.

### Item 0 — edd7bbcd825a9ab6a34864502faa1205c47c1dbd

Evidence: `item0-docs.md`.

```text
DELIVERY_PLAN.md
PRODUCT_REQUIREMENTS.md
docs/references/admin-ui/reviews/2026-10-06/u2/item0-docs.md
```

### Item 1 — 6c8037b52fd8281fd9e1a9e869dd146ef4c57e38

Evidence: `item1-dashboard-backend.md`.

```text
CURRENT_STATUS.md
UI_PAGE_MATRIX.md
docs/references/admin-ui/reviews/2026-10-06/u2/item1-dashboard-backend.md
src/Bingo.Application/Dashboard/IAdminDashboardService.cs
src/Bingo.Infrastructure/Dashboard/AdminDashboardService.cs
src/Bingo.Web/Pages/Admin/Index.cshtml
src/Bingo.Web/Pages/Admin/Index.cshtml.cs
tests/Bingo.BrowserTests/AuditPresentationTests.cs
tests/Bingo.BrowserTests/U2DashboardHttpTests.cs
tests/Bingo.IntegrationTests/U2DashboardIntegrationTests.cs
tests/Bingo.IntegrationTests/UiReviewScenarioIntegrationTests.cs
```

### Item 2 — 061ff9bff3e1205ac99257447d73b21a5849a947

Evidence: `item2-dashboard-page.md`.

```text
CURRENT_STATUS.md
DELIVERY_PLAN.md
UI_PAGE_MATRIX.md
docs/references/admin-ui/reviews/2026-10-06/u2/item1-dashboard-backend.md
docs/references/admin-ui/reviews/2026-10-06/u2/item2-dashboard-page.md
scripts/check-u2-dashboard.cjs
scripts/lib/admin-parity-fixture.cjs
src/Bingo.Web/AdminCommunityResource.cs
src/Bingo.Web/Navigation/SharedShellService.cs
src/Bingo.Web/Pages/Admin/Index.cshtml
src/Bingo.Web/Pages/Admin/Index.cshtml.cs
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml
src/Bingo.Web/Pages/Shared/_AdminDesignLayout.cshtml
src/Bingo.Web/Pages/Shared/_AdminDesignTemplates.cshtml
src/Bingo.Web/Pages/Shared/_AdminLayout.cshtml
src/Bingo.Web/Resources/AdminCommunityResource.da.resx
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css
src/Bingo.Web/wwwroot/css/admin-design-layout.css
src/Bingo.Web/wwwroot/css/site.transitional.application.css
src/Bingo.Web/wwwroot/js/admin-dashboard.js
src/Bingo.Web/wwwroot/js/admin-design-shell.js
tests/AdminDesignParityFixture/FixtureHost.cs
tests/Bingo.BrowserTests/U2DashboardHttpTests.cs
tests/Bingo.BrowserTests/admin-design-dashboard.browser.js
```

### Item 3 — 90ae954c55805943698408ab4f790336cb53694e

Evidence: `item3-events-directory.md`.

```text
CURRENT_STATUS.md
DELIVERY_PLAN.md
UI_PAGE_MATRIX.md
docs/references/admin-ui/reviews/2026-10-06/u2/item3-events-directory.md
scripts/check-u2-events.cjs
scripts/lib/admin-parity-compare.cjs
scripts/lib/admin-parity-fixture.cjs
src/Bingo.Web/Pages/Admin/Events/Index.cshtml
src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml
src/Bingo.Web/Pages/Shared/_AdminDesignIcon.cshtml
src/Bingo.Web/Pages/Shared/_AdminDesignLayout.cshtml
src/Bingo.Web/Pages/Shared/_AdminDesignTemplates.cshtml
src/Bingo.Web/Pages/Shared/_AdminEventsLoadStates.cshtml
src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml
src/Bingo.Web/Resources/AdminCommunityResource.da.resx
src/Bingo.Web/Resources/SharedResource.da.resx
src/Bingo.Web/wwwroot/css/admin-design-events.css
src/Bingo.Web/wwwroot/css/admin-design-layout.css
src/Bingo.Web/wwwroot/css/site.transitional.application.css
src/Bingo.Web/wwwroot/js/admin-collaboration.js
src/Bingo.Web/wwwroot/js/admin-design-shell.js
src/Bingo.Web/wwwroot/js/admin-events.js
src/Bingo.Web/wwwroot/js/event-create.js
src/Bingo.Web/wwwroot/js/site.js
tests/AdminDesignParityFixture/FixtureHost.cs
tests/Bingo.BrowserTests/U2EventsDirectoryHttpTests.cs
tests/Bingo.BrowserTests/admin-design-events.browser.js
tests/Bingo.IntegrationTests/EventsDirectoryIntegrationTests.cs
tests/Bingo.IntegrationTests/U2EventsDirectoryIntegrationTests.cs
```

### Item 4 — a89ef250b2c1d09fc9453f7a585f6f96a698a0dd

Evidence: `item4-create-modal.md`.

```text
CURRENT_STATUS.md
DELIVERY_PLAN.md
UI_PAGE_MATRIX.md
docs/references/admin-ui/reviews/2026-10-06/u2/item3-events-directory.md
docs/references/admin-ui/reviews/2026-10-06/u2/item4-create-modal.md
scripts/check-u2-create.cjs
src/Bingo.Web/Pages/Admin/Events/Create.cshtml.cs
src/Bingo.Web/Pages/Admin/Events/Index.cshtml
src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml
src/Bingo.Web/Resources/AdminCommunityResource.da.resx
src/Bingo.Web/wwwroot/js/admin-design-shell.js
src/Bingo.Web/wwwroot/js/admin-event-create.js
src/Bingo.Web/wwwroot/js/admin-events.js
tests/Bingo.BrowserTests/U2EventCreateHttpTests.cs
tests/Bingo.BrowserTests/admin-design-create.browser.js
tests/Bingo.IntegrationTests/EventCreationRetryIntegrationTests.cs
```

### Item 5 — this checkpoint's single local commit

Evidence: `item5-ur-scenarios.md`. Final SHA is supplied in the delivery reply;
the file list below is the exact16-file commit scope, not a self-review claim.

```text
CURRENT_STATUS.md
DELIVERY_PLAN.md
UI_PAGE_MATRIX.md
docs/references/admin-ui/reviews/2026-10-06/u2/item5-ur-scenarios.md
scripts/check-u2-ur.cjs
scripts/test-ui-review.py
scripts/ui-review.py
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml
src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml
src/Bingo.Web/TestData/UiReviewScenarioCatalogue.cs
src/Bingo.Web/TestData/UiReviewScenarioSeeder.cs
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css
src/Bingo.Web/wwwroot/css/admin-design-events.css
tests/AdminDesignParityFixture/FixtureHost.cs
tests/Bingo.BrowserTests/admin-design-ur.browser.js
tests/Bingo.IntegrationTests/UiReviewScenarioIntegrationTests.cs
```
