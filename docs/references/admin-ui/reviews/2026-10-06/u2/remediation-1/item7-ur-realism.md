# U2 brief70 remediation item7 — UR realism and final gates

Implementation/check evidence, not self-review, independent review or visual
acceptance. Started this remediation clean at exact96bc5b3; items1–6 already
committed separately. No user-owned DB/app refresh, publication or extra workers.

## Changed files

- src/Bingo.Web/TestData/UiReviewScenarioSeeder.cs
- tests/Bingo.IntegrationTests/UiReviewScenarioIntegrationTests.cs
- scripts/check-u2-ur.cjs
- docs/references/admin-ui/reviews/2026-10-06/u2/item5-ur-scenarios.md
- CURRENT_STATUS.md; UI_PAGE_MATRIX.md (handoff/check evidence only; approval unchanged)
- this item7 evidence.

## Production-shaped seed

Failed opening evaluates actual synthetic fields through production
IEventReadinessEvaluator (ScheduledExecution), disables scheduled opening, writes
one failed unresolved attempt with production blocker codes/descriptions, the
System/null-actor event.signup_opening_failed audit with only scheduledFor and
blockerCodes Details (no Before/After), and recipient-specific active website
Admin/SuperAdmin notifications with production title/detail/Manage route/time.
Postponed start uses production IEventLifecycleService readiness and persists its
failed unresolved attempt, matching event.start_postponed System audit and Admin
notifications. No fake lifecycle transitions or acknowledgement of attention.

Imported history is a separate synthetic ur-imported event, as permitted by69b
F2. Uses CreateArchivedHistorical, CsvImport/Confirmed participants without account
IDs, Playing/EhbSource.Import assignments, Preformed finalized teams, frozen
publication rosters/board approval/results and reconstructed contributions.
Only Draft→Archived transition: "Frozen historical import; no live lifecycle
transition." End-time finalization includes consumed[], counters/placements using
the importer's field names, and sourceEventId/manifestHash/inputHash/importHash.
Manifest/input hashes are computed from different synthetic payloads; importHash
is SHA256 of their joined hashes, not repeated placeholder metadata. A single
historical_import.applied audit has importer-shaped counts/provenance Details,
Owner actor, creation timestamp and null Before/After. Compatible frozen WOM rows
use the real assignment fingerprint. Small synthetic counts2teams/6participants/
4tiles substitute for the real import's6/90/25; no real participant data is used.
The existing platform WOM-failure event retains its accepted unavailable scenario
and lifecycle; removed its false attached import provenance. Existing platform
finalizations, hidden events and current-profile rule otherwise retained.

## Authorized test before/after — A10 + brief70 item7

- EventsHeld3→4: one current + two platform archives + the new separate import.
- Global no-contributions assertion→no contributions for every platform event;
  separately assert exactly1 reconstructed imported contribution. The blocked
  current-event approval/contribution assertions remain exact.
- Every approved platform board still requires approved/published audit entries;
  the import instead requires no fabricated platform board audit, as the real
  importer writes none. Import's own audit/approval/publication checked separately.
- Complete platform lifecycle chains unchanged. Only the new import uses the
  exact single Draft→Archived chain, actor/reason/effective/performed dates and
  finalization review-cycle link. This is not an exemption for platform events.
- Scheduled-failure audits now explicitly require null Before/After and System
  actor plus exact Details fields, matching production; all other platform audit
  state assertions remain unchanged.
- Imported Dashboard target moved from platform ur-wom-unavailable to ur-imported;
  imported approval remains unavailable, mixed headline measurable.
- Added exact schedule/attempt/blocker/audit/recipient/notification/provenance/
  hash/count/frozen-results checks, and rendered Audit/inbox proof for both roles.
  Existing authorization, hidden-event, discard audit, cookies/session, printed
  URLs, culture, paging, Cancelled counts and one-current-event assertions retained.

## Execution checkpoints

Focused PostgreSQL/HTTP UR proof:2 passed/0 failed/0 skipped,21s before the final
frozen-results assertion extension. Final rerun and final gates recorded below.
UR safety script:13 passed,0 errors,0.351s. Initial sandbox run could not bind its
owned loopback sockets; elevated run passed. No product assertion waived.
Initial authoring checkpoints: CA1861 constant-array analyzer corrected to static
fields; old global no-contribution assertion exposed the new reconstructed
import; revised EF query first embedded an in-memory event lookup, corrected to
a scalar event ID. These unsuccessful checkpoints are not passing evidence.

## Shared design-system checks1–6 (rerun)

All scans below executed on the final item7 source candidate. Exit1 for rg means
no matches. These are mandatory scoped implementer gates, not independent review.
1 frozen CSS/reference cmp/diff;2 page CSS literal/component scan;3 inline
styles;4 shared markup;5 shared busy timing;6 dirty/fetch/module lifecycle.

### tokens

```sh
cmp src/Bingo.Web/wwwroot/css/admin-design-tokens.css docs/references/admin-ui/ui/tokens.css
```

Exit0; no output.

### components

```sh
cmp src/Bingo.Web/wwwroot/css/admin-design-components.css docs/references/admin-ui/ui/components.css
```

Exit0; no output.

### frozen diff

```sh
git diff --name-only 96bc5b3c3c5b182abd0490a1c3a260878567ef06 -- src/Bingo.Web/wwwroot/css/admin-design-tokens.css src/Bingo.Web/wwwroot/css/admin-design-components.css docs/references/admin-ui/ui/tokens.css docs/references/admin-ui/ui/components.css docs/references/admin-ui/Dashboard.dc.html docs/references/admin-ui/Events.dc.html
```

Exit0; no output.

### page CSS literal/component scan

```sh
rg -n '(^|[ :])#[0-9a-fA-F]{3,8}\b|rgb\(|hsl\(|font-family|box-shadow|border-radius|^\.(btn|card|modal|drawer|toast|pill|tbl|form-|banner)([ :.{-]|$)' src/Bingo.Web/wwwroot/css/admin-design-dashboard.css src/Bingo.Web/wwwroot/css/admin-design-events.css
```

Exit0; exact output:

```text
src/Bingo.Web/wwwroot/css/admin-design-events.css:1:.tbl.ev-tbl{--table-cols:minmax(230px,1.5fr) 152px minmax(190px,1.1fr) minmax(170px,1fr) minmax(224px,1.3fr);--table-min:990px}
src/Bingo.Web/wwwroot/css/admin-design-events.css:10:.events-sk-phase{height:20px;border-radius:var(--dk-radius-xl)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:20:.dash-sk-header-open{width:52px;height:28px;flex:none;border-radius:var(--dk-radius-xs)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:21:.tbl.hist{--table-cols:minmax(240px,2.2fr) 96px 170px 170px 124px minmax(150px,1.3fr);--table-min:960px}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:23:.dash-sk-stat-value{height:20px;margin-top:var(--dk-space-3);border-radius:var(--dk-radius-xs)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:25:.dash-sk-chart{height:200px;margin-top:var(--dk-space-5);border-radius:var(--dk-radius-md)}
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css:26:.dash-sk-recap{height:64px;margin-top:var(--dk-space-5);border-radius:var(--dk-radius-md)}
```

### page inline styles

```sh
rg -n 'style=' src/Bingo.Web/Pages/Admin/Index.cshtml src/Bingo.Web/Pages/Admin/Events/Index.cshtml src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml src/Bingo.Web/Pages/Shared/_AdminEventsLoadStates.cshtml src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml
```

Exit0; exact output:

```text
src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml:24:    @if (Model) { <div class="empty is-error" role="row"><div role="cell" style="display:contents"><div class="empty-ic"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("error")' /></div><div class="empty-title">@D["Couldn’t load events"]</div><div class="empty-text">@D["Check your connection and try again. Nothing was changed."]</div><button class="btn" type="button" data-load-retry>@D["Try again"]</button></div></div> }
src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml:25:    else { @foreach (var width in new[] { 52, 38, 46, 60, 34, 48, 42 }) { <div class="tr sk-row" role="row" aria-hidden="true"><div class="td c-name"><div class="sk" style="width:@(width)%"></div></div><div class="td"><div class="sk events-sk-phase" style="width:62%"></div></div><div class="td"><div class="sk events-sk-start" style="width:70%"></div><div class="sk" style="width:44%;height:8px"></div></div><div class="td"><div class="sk" style="width:56%"></div></div><div class="td"><div class="sk" style="width:40%"></div></div></div> } }
src/Bingo.Web/Pages/Admin/Index.cshtml:45:                <div class="meter" title="@D["{0} of {1} places filled", card.ConfirmedParticipants, cap]"><div class="meter-track" style="--meter-w:56px"><div class="meter-fill" style="width:@(Bingo.Web.Pages.Admin.IndexModel.Percent(cap > 0 ? Math.Min(1m, (decimal)card.ConfirmedParticipants / cap) : 0))%"></div></div><span class="meter-label">@card.ConfirmedParticipants/@cap</span></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:81:            <div class="chart" id="chart" style="--bar-count:@data.Chart.Count">
src/Bingo.Web/Pages/Admin/Index.cshtml:82:                <div class="chart-plot" style="--chart-h:208px">
src/Bingo.Web/Pages/Admin/Index.cshtml:86:                    <div class="chart-gridline @(tick == 0 ? "is-base" : "")" style="top:@top%"></div><div class="chart-tick" style="top:@top%">@Bingo.Web.Pages.Admin.IndexModel.Number(tick)</div>
src/Bingo.Web/Pages/Admin/Index.cshtml:96:                    else if (point.IsHistoricalImport || point.TrackingStarts) { <span class="bar-seg @(point.IsHistoricalImport ? "s-muted" : "s-2" + provisional)" style="height:@(Height(point.Participants.Value))px"></span> }
src/Bingo.Web/Pages/Admin/Index.cshtml:97:                    else { <span class="bar-seg @("s-1" + provisional)" style="height:@(Math.Max(0, Height(point.ReturningWebsiteParticipants.Value) - 1))px"></span><span class="bar-seg @("s-2" + provisional)" style="height:@(Math.Max(0, Height(point.NewWebsiteParticipants.Value) - 1))px"></span> }
src/Bingo.Web/Pages/Admin/Index.cshtml:131:        <div class="highlight @(recap.Provisional ? "is-muted" : "")"><div class="highlight-ic"><svg class="ic" viewBox="0 0 16 16">@if (recap.Provisional) { <circle cx="8" cy="8" r="5.5"/><path d="M8 5v3l2 1.5"/> } else { <path d="M5 2.5h6v3.5a3 3 0 0 1-6 0zM5 3.5H3v1a2 2 0 0 0 2 2M11 3.5h2v1a2 2 0 0 1-2 2M8 9v2.5M5.5 13.5h5M6.5 11.5h3"/> }</svg></div><div style="min-width:0;flex:1"><div class="highlight-label">@D[recap.Provisional ? "Results provisional" : "Winner"]</div><div class="highlight-value">@(recap.Provisional ? D["Final review in progress"].Value : Model.Winners(recap.Winners))</div><div class="highlight-sub">@(recap.Provisional ? lastOfficial is null ? D["The winner is shown once results are official."].Value : Model.L("Last official winner: {0} · {1}", Model.Winners(lastOfficial.Winners), lastOfficial.EventName) : Model.L("{0} teams competed", recap.TeamCount))</div></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:132:        @if (recap.WinnerBoard is { } board) { <div class="meter" title="@D["Winner completed {0} of {1} tiles", board.CompletedTiles, board.TotalTiles]"><div class="meter-track" style="--meter-w:48px"><div class="meter-fill" style="width:@Bingo.Web.Pages.Admin.IndexModel.Percent(board.CompletionRatio)%"></div></div><span class="meter-label">@board.CompletedTiles/@board.TotalTiles</span></div> }
src/Bingo.Web/Pages/Admin/Index.cshtml:161:        <div class="td" role="cell">@if (row.WinnerBoard is { } board) { <div class="meter"><div class="meter-track"><div class="meter-fill" style="width:@Bingo.Web.Pages.Admin.IndexModel.Percent(board.CompletionRatio)%"></div></div><span class="meter-label">@board.CompletedTiles/@board.TotalTiles @D["tiles"]</span></div> } else { var hint = D[row.Provisional ? "Shown once results are official." : "Not recorded."].Value; <span class="hint tip-start muted" tabindex="0" aria-label="@hint">—<span class="hint-tip" aria-hidden="true">@hint</span></span> }</div>
src/Bingo.Web/Pages/Admin/Index.cshtml:168:<section class="dash-section fade-in" aria-labelledby="comm-title"><div class="section-head"><h2 class="panel-title" id="comm-title">@D["Community"]</h2><span class="panel-aside">@D["Current figures"]</span></div><div class="card"><div class="stat-strip is-compact" style="--stat-cols:3">
src/Bingo.Web/Pages/Shared/_AdminEventsLoadStates.cshtml:3:    <header class="page-head" data-page-family="events"><div style="min-width:0"><h1 class="h1" tabindex="-1">@D["Events"]</h1><p class="summary" aria-hidden="true"></p></div><div class="head-actions"><a class="btn btn-primary" href="/Admin/Events/Create" data-create-event><partial name="_AdminDesignIcon" model='new AdminDesignIcon("plus")' />@D["Create event"]</a></div></header>
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:73:        @if (item.IsPreparation && item.ParticipantCap is { } cap) { <div class="meter" title="@D["{0} of {1} places confirmed", item.Confirmed, cap]"><div class="meter-track" style="--meter-w:44px"><div class="meter-fill" style="width:@(Bingo.Web.Pages.Admin.IndexModel.Percent(cap > 0 ? Math.Min(1m, (decimal)item.Confirmed / cap) : 0))%"></div></div><span class="meter-label @(item.State == EventState.Draft && item.Confirmed == 0 ? "is-muted" : "")">@Model.PeopleMain(item)</span></div> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:83:else { <div class="empty" role="row"><div role="cell" style="display:contents"><div class="empty-ic"><partial name="_AdminDesignIcon" model='new AdminDesignIcon(hasFilters ? "search" : Model.ActiveView == "hidden" ? "hidden" : "calendar")' /></div><div class="empty-title">@D[emptyTitle]</div><div class="empty-text">@D[emptyText]</div>@if (hasFilters) { <button type="button" class="btn" data-directory-url="@Model.DirectoryUrl(view: Model.ActiveView == "hidden" ? "hidden" : "all", phase: "all", search: "", attention: false)">@D["Clear filters"]</button> } else if (Model.ActiveView != "hidden" && (Model.VisibleCount == 0 || Model.ActiveView == "current")) { <a class="btn @(Model.VisibleCount == 0 ? "btn-primary" : "")" href="/Admin/Events/Create" data-create-event>@D["Create event"]</a> }</div></div> }
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml:31:    <div class="mf-foot"><button class="btn" id="cm-cancel" type="button">@D["Cancel"]</button><button class="btn btn-primary" id="cm-submit" type="button" style="min-width:132px"><span class="spin" hidden></span><span data-component-text>@D["Create event"]</span></button></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:16:        <div class="stat"><div class="sk" style="width:46%"></div><div class="sk dash-sk-stat-value" style="width:38%"></div><div class="sk dash-sk-stat-note" style="width:62%;height:8px"></div></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:20:        <div class="card panel"><div class="sk" style="width:30%"></div><div class="sk dash-sk-chart"></div></div>
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml:21:        <div class="card panel"><div class="sk" style="width:40%"></div><div class="sk dash-sk-recap"></div><div class="sk dash-sk-fact-first" style="width:80%"></div><div class="sk dash-sk-fact" style="width:70%"></div><div class="sk dash-sk-fact" style="width:75%"></div></div>
```

### shared components

```sh
rg -n 'partial|class="(card|banner|tbl|modal|btn|hint|meter|pill)|_AdminDesign' src/Bingo.Web/Pages/Admin/Index.cshtml src/Bingo.Web/Pages/Admin/Events/Index.cshtml src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml
```

Exit0; exact output:

```text
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml:25:        <partial name="_AdminDesignBanner" model='new AdminDesignBanner("is-error", "error", "", Id: "cm-failure", Hidden: true)' />
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml:26:        <partial name="_AdminDesignBanner" model='new AdminDesignBanner("is-warning", "warning", "", Id: "cm-uncertain", Hidden: true)' />
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml:27:        <partial name="_AdminDesignBanner" model='new AdminDesignBanner("is-info", "info", "", Id: "cm-not-found", Hidden: true)' />
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml:28:        <div class="field"><div class="lbl-row"><label class="lbl" for="cm-name">@D["Event name"]</label><span class="field-count" data-create-count aria-live="polite" hidden></span></div><input class="input" id="cm-name" name="Input.Name" autocomplete="off" aria-required="true" aria-invalid="false" placeholder="@D["e.g. Autumn Bingo 2027"]" autofocus /><div class="field-err" id="cm-name-err" hidden><partial name="_AdminDesignIcon" model='new AdminDesignIcon("error")' /><span data-component-text></span></div><div class="field-note" id="cm-name-dup" role="status" hidden><partial name="_AdminDesignIcon" model='new AdminDesignIcon("info")' /><span data-component-text></span></div></div>
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml:29:        <div class="field"><label class="lbl" for="cm-tz">@D["Timezone"]</label><div class="select-wrap"><select class="select select-field" id="cm-tz" name="Input.Timezone" aria-describedby="cm-tz-hint"><option value="Europe/Copenhagen">Europe/Copenhagen (UTC+@offset.ToString("hh\\:mm", CultureInfo.InvariantCulture))</option><option value="UTC">UTC (UTC+00:00)</option></select><partial name="_AdminDesignIcon" model='new AdminDesignIcon("chevron-down")' /></div><div class="field-hint" id="cm-tz-hint">@D["Every date and time in this event uses this timezone. You can change it later."]</div><div class="field-err" id="cm-tz-err" hidden></div></div>
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml:31:    <div class="mf-foot"><button class="btn" id="cm-cancel" type="button">@D["Cancel"]</button><button class="btn btn-primary" id="cm-submit" type="button" style="min-width:132px"><span class="spin" hidden></span><span data-component-text>@D["Create event"]</span></button></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:45:                <div class="meter" title="@D["{0} of {1} places filled", card.ConfirmedParticipants, cap]"><div class="meter-track" style="--meter-w:56px"><div class="meter-fill" style="width:@(Bingo.Web.Pages.Admin.IndexModel.Percent(cap > 0 ? Math.Min(1m, (decimal)card.ConfirmedParticipants / cap) : 0))%"></div></div><span class="meter-label">@card.ConfirmedParticipants/@cap</span></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:47:            else { <span class="meter-label">@D["{0} confirmed · No capacity set", card.ConfirmedParticipants]</span> }
src/Bingo.Web/Pages/Admin/Index.cshtml:48:            <a class="btn btn-sm" href="@card.OverviewPath">@D["Open"]</a>
src/Bingo.Web/Pages/Admin/Index.cshtml:56:    <div class="card fade-in"><div class="empty"><div class="empty-ic"><svg class="ic" viewBox="0 0 16 16"><path d="M2.5 13.5h11M4.5 11V8M8 11V4.5M11.5 11V6.5"/></svg></div><div class="empty-title">@D["No events have been held yet"]</div><div class="empty-text">@D["Participation, results and history appear here once the first event has ended."]</div><a class="btn" href="/Admin/Events">@D["Go to Events"]</a></div></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:60:    <section class="card fade-in" aria-label="@D["Headline statistics"]"><div class="stat-strip">
src/Bingo.Web/Pages/Admin/Index.cshtml:68:        <span class="hint @(i == 0 ? "tip-start" : i == 3 ? "tip-end" : "")" tabindex="0" aria-label="@hint"><svg class="hint-ic" viewBox="0 0 16 16"><circle cx="8" cy="8" r="6"/><path d="M8 7.2v3.8M8 5h.01"/></svg><span class="hint-tip" aria-hidden="true">@hint</span></span></div></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:74:        <section class="card panel fade-in" aria-labelledby="part-title">
src/Bingo.Web/Pages/Admin/Index.cshtml:105:                    @if (point.IsHistoricalImport || point.Provisional || point.TrackingStarts) { <span class="pill @(point.IsHistoricalImport ? "is-neutral" : point.Provisional ? "is-warning" : "")">@D[point.IsHistoricalImport ? "Imported" : point.Provisional ? "Provisional" : "Tracking starts"]</span> }
src/Bingo.Web/Pages/Admin/Index.cshtml:128:        <section class="card panel fade-in recap @(data.Chart.Count < 2 ? "is-wide" : "")" aria-labelledby="recap-title">
src/Bingo.Web/Pages/Admin/Index.cshtml:132:        @if (recap.WinnerBoard is { } board) { <div class="meter" title="@D["Winner completed {0} of {1} tiles", board.CompletedTiles, board.TotalTiles]"><div class="meter-track" style="--meter-w:48px"><div class="meter-fill" style="width:@Bingo.Web.Pages.Admin.IndexModel.Percent(board.CompletionRatio)%"></div></div><span class="meter-label">@board.CompletedTiles/@board.TotalTiles</span></div> }
src/Bingo.Web/Pages/Admin/Index.cshtml:136:        <div class="fact"><dt class="fact-label">@D["Approved submissions"]</dt><dd class="fact-value">@if (!recap.ApprovedSubmissions.IsAvailable) { <span class="hint tip-end" tabindex="0" aria-label="@(recap.IsHistoricalImport ? D["Imported history has no evidence submissions; its contributions were reconstructed."].Value : Model.SubmissionHint(history))">—<span class="hint-tip" aria-hidden="true">@(recap.IsHistoricalImport ? D["Imported history has no evidence submissions; its contributions were reconstructed."].Value : Model.SubmissionHint(history))</span></span> } else { @Bingo.Web.Pages.Admin.IndexModel.Value(recap.ApprovedSubmissions) }<span class="fact-sub">@(recap.ApprovedSubmissions.IsAvailable ? recap.Provisional ? D["may change in final review"].Value : "" : D["not available"].Value)</span></dd></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:139:        <div class="panel-foot"><span class="panel-note">@(recap.Provisional ? Model.L("Ended {0}", DateTimePresentation.Format(recap.ActualEndedAt, "d MMM", history.Timezone, CultureInfo.CurrentCulture)) : D[recap.IsHistoricalImport ? "Imported history · attribution reconstructed" : "Finalized results"].Value)</span>@if (data.Chart.Count < 2) { <span class="panel-note">@D["Participation by event appears once a second event has been held."]</span> }<a class="btn btn-quiet btn-sm" href="@recap.OverviewPath">@D["Open event"]<svg class="ic" viewBox="0 0 16 16"><path d="M6 4l4 4-4 4"/></svg></a></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:144:        <section class="card panel recap @(data.Chart.Count < 2 ? "is-wide" : "")"><div class="panel-head"><h2 class="panel-title">@D["Latest event"]</h2></div><div class="empty"><div class="empty-title">@D["No ended event yet"]</div><div class="empty-text">@D["The recap appears after an event has ended."]</div></div></section>
src/Bingo.Web/Pages/Admin/Index.cshtml:147:    <section class="card dash-section fade-in" aria-labelledby="hist-title"><div class="panel-head hist-head"><h2 class="panel-title" id="hist-title">@D["Event history"]</h2><span class="panel-aside">@D["{0} events", data.History.Count]</span></div><div class="tbl-wrap" id="tbl-wrap"><div class="tbl hist" role="table" aria-label="@D["Event history"]"><div class="tr th-row" role="row">
src/Bingo.Web/Pages/Admin/Index.cshtml:158:        @if (row.IsHistoricalImport || row.Provisional) { <span class="pill @(row.IsHistoricalImport ? "is-neutral" : "is-warning")">@D[row.IsHistoricalImport ? "Imported" : row.State == EventState.Live ? "Live · provisional" : "Final review"]</span> }</div><div class="sub">@Model.Range(row.ActualStartedAt, row.ActualEndedAt, row.Timezone)</div></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:159:        <div class="td num" role="cell"><span class="hint tip-end" aria-label="@D["{0} teams", row.TeamCount]">@Bingo.Web.Pages.Admin.IndexModel.Value(row.Participants)<span class="hint-tip" aria-hidden="true">@D["{0} teams", row.TeamCount]</span></span></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:160:        <div class="td num" role="cell">@if (row.ApprovedSubmissions.IsAvailable) { @Bingo.Web.Pages.Admin.IndexModel.Value(row.ApprovedSubmissions) } else { <span class="hint tip-end muted" tabindex="0" aria-label="@Model.SubmissionHint(row)">—<span class="hint-tip" aria-hidden="true">@Model.SubmissionHint(row)</span></span> }</div>
src/Bingo.Web/Pages/Admin/Index.cshtml:161:        <div class="td" role="cell">@if (row.WinnerBoard is { } board) { <div class="meter"><div class="meter-track"><div class="meter-fill" style="width:@Bingo.Web.Pages.Admin.IndexModel.Percent(board.CompletionRatio)%"></div></div><span class="meter-label">@board.CompletedTiles/@board.TotalTiles @D["tiles"]</span></div> } else { var hint = D[row.Provisional ? "Shown once results are official." : "Not recorded."].Value; <span class="hint tip-start muted" tabindex="0" aria-label="@hint">—<span class="hint-tip" aria-hidden="true">@hint</span></span> }</div>
src/Bingo.Web/Pages/Admin/Index.cshtml:162:        <div class="td num c-ehb" role="cell"><span class="hint tip-end muted" tabindex="0" aria-label="@Model.EhbHint(row)">@(row.Ehb.IsAvailable ? row.Ehb.Gain?.ToString("N0", CultureInfo.CurrentCulture) : "—")<span class="hint-tip" aria-hidden="true">@Model.EhbHint(row)</span></span></div>
src/Bingo.Web/Pages/Admin/Index.cshtml:163:        <div class="td" role="cell">@if (row.Provisional) { <span class="hint tip-end" tabindex="0" aria-label="@D["Official results are published after final review."]"><span class="pill is-warning">@D["Provisional"]</span><span class="hint-tip" aria-hidden="true">@D["Official results are published after final review."]</span></span> } else { <div class="cell-main">@Model.Winners(row.Winners)</div> }</div>
src/Bingo.Web/Pages/Admin/Index.cshtml:168:<section class="dash-section fade-in" aria-labelledby="comm-title"><div class="section-head"><h2 class="panel-title" id="comm-title">@D["Community"]</h2><span class="panel-aside">@D["Current figures"]</span></div><div class="card"><div class="stat-strip is-compact" style="--stat-cols:3">
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:24:    <div class="head-actions"><a class="btn btn-primary" href="/Admin/Events/Create" data-create-event><partial name="_AdminDesignIcon" model='new AdminDesignIcon("plus")' />@D["Create event"]</a></div>
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:28:@if (Model.DroppedLinkParts) { <div class="banner is-info page-banner" role="status"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("info")' /><span class="grow">@D["Some parts of this link weren’t available and were cleared. The remaining filters still apply."]</span></div> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:29:@if (Model.ActionProjectionUnavailable) { <div class="banner is-info page-banner" role="status"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("info")' /><span class="grow">@D["Some attention counts could not be loaded. Events are shown, but attention information may be incomplete."]</span><button type="button" class="btn btn-sm" data-directory-url="@Model.DirectoryUrl(page: Model.PageNumber)">@D["Try again"]</button></div> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:35:    @if (Model.IsSuperAdmin) { <span class="tab-sep" aria-hidden="true"></span><label class="tab @(Model.ActiveView == "hidden" ? "is-on" : "")" title="@D["SuperAdmin only: quarantined events"]"><input class="sr" type="radio" name="view-tab" checked="@(Model.ActiveView == "hidden")" data-directory-url="@Model.DirectoryUrl(view: "hidden")"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("hidden")' /><span>@D["Hidden"]</span><span class="tab-count">@Model.HiddenCount</span></label> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:38:    @if (Model.ActiveAttention) { <button class="filter-chip" type="button" data-directory-url="@Model.DirectoryUrl(attention: false)" aria-label="@D["Remove filter: needs attention"]">@D["Needs attention"]<partial name="_AdminDesignIcon" model='new AdminDesignIcon("close")' /></button> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:39:    <div class="search @(Model.ActiveSearch.Length > 0 ? "has-clear" : "")"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("search")' /><input class="input" id="search-input" type="search" placeholder="@D["Search event names"]" aria-label="@D["Search event names"]" value="@Model.ActiveSearch" data-directory-search>
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:40:    @if (Model.ActiveSearch.Length > 0) { <button class="icon-btn clear" type="button" data-directory-clear aria-label="@D["Clear search"]"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("close")' /></button> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:42:    <button class="btn filter-btn @(Model.ActiveFilter != "all" ? "is-active" : "")" id="phase-btn" type="button" data-menu-target="directory-phase-menu" data-menu-align="end" aria-haspopup="menu" aria-expanded="false"><span class="fb-lbl">@D["Phase"]</span>@D[Model.ActiveFilter == "all" ? "All" : Model.StateOptions.Single(option => option.Value == Model.ActiveFilter).Label]<partial name="_AdminDesignIcon" model='new AdminDesignIcon("chevron-down")' /></button>
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:44:    @foreach (var phase in Model.StateOptions) { <button class="menu-item" type="button" role="menuitemradio" aria-checked="@(Model.ActiveFilter == phase.Value ? "true" : "false")" data-directory-url="@Model.DirectoryUrl(phase: phase.Value)"><span class="grow">@D[phase.Label]</span>@if (Model.ActiveFilter == phase.Value) { <partial name="_AdminDesignIcon" model='new AdminDesignIcon("check")' /> }</button> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:48:<div class="card"><div class="tbl-wrap" data-directory-wrap><div class="tbl ev-tbl sticky-first" role="table" aria-label="@D["Events"]" aria-rowcount="@(Model.TotalCount + 1)">
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:69:        <div class="td c-name" role="cell"><div class="name-line"><a class="name-btn" id="open-@item.Id" href="@overview" title="@item.Name">@item.Name</a>@if (item.Participation?.IsHistoricalImport == true) { <span class="pill is-neutral" title="@D["Imported history from before the platform"]">@D["Imported"]</span> }</div>@if (item.HiddenAt is { } hiddenAt) { <div class="sub">@D["Hidden {0}", DateTimePresentation.Format(hiddenAt, "d MMM yyyy", item.Timezone, CultureInfo.CurrentCulture)]</div> }</div>
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:73:        @if (item.IsPreparation && item.ParticipantCap is { } cap) { <div class="meter" title="@D["{0} of {1} places confirmed", item.Confirmed, cap]"><div class="meter-track" style="--meter-w:44px"><div class="meter-fill" style="width:@(Bingo.Web.Pages.Admin.IndexModel.Percent(cap > 0 ? Math.Min(1m, (decimal)item.Confirmed / cap) : 0))%"></div></div><span class="meter-label @(item.State == EventState.Draft && item.Confirmed == 0 ? "is-muted" : "")">@Model.PeopleMain(item)</span></div> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:78:        else { <div class="attn @(item.ScheduledStartPostponed || item.ScheduledOpeningFailed ? "is-failure" : "is-pending")"><span class="attn-main">@if (item.ScheduledStartPostponed || item.ScheduledOpeningFailed) { <partial name="_AdminDesignIcon" model='new AdminDesignIcon("error")' /> } else { <span class="dot" aria-hidden="true"></span> }<span class="attn-text">@labels[0]</span></span>@if (labels.Count > 1) { var more = string.Join(", ", labels.Skip(1)); <span class="hint attn-more tip-end" tabindex="0" aria-label="@D["{0} more: {1}", labels.Count - 1, more]">· +@(labels.Count - 1)<span class="hint-tip" aria-hidden="true">@D["Also: {0}. The event’s Overview explains everything.", more]</span></span> }</div> }</div>
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:83:else { <div class="empty" role="row"><div role="cell" style="display:contents"><div class="empty-ic"><partial name="_AdminDesignIcon" model='new AdminDesignIcon(hasFilters ? "search" : Model.ActiveView == "hidden" ? "hidden" : "calendar")' /></div><div class="empty-title">@D[emptyTitle]</div><div class="empty-text">@D[emptyText]</div>@if (hasFilters) { <button type="button" class="btn" data-directory-url="@Model.DirectoryUrl(view: Model.ActiveView == "hidden" ? "hidden" : "all", phase: "all", search: "", attention: false)">@D["Clear filters"]</button> } else if (Model.ActiveView != "hidden" && (Model.VisibleCount == 0 || Model.ActiveView == "current")) { <a class="btn @(Model.VisibleCount == 0 ? "btn-primary" : "")" href="/Admin/Events/Create" data-create-event>@D["Create event"]</a> }</div></div> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:85:@if (Model.TotalCount > 0) { <div class="tfoot"><div class="tfoot-left"><span class="tnum" aria-live="polite">@(Model.PageCount == 1 ? D["{0} events", Model.TotalCount] : D["{0}–{1} of {2} events", Model.FirstRow, Model.LastRow, Model.TotalCount])</span>@if (Model.ActiveSort.Length > 0) { <span class="tfoot-note">· @D["Sorted by {0}", D[columns.Single(column => column.Item1 == Model.ActiveSort).Item2].Value]</span><button type="button" class="btn btn-quiet btn-sm" data-directory-url="@Model.DirectoryUrl(sort: "", direction: "asc")">@D["Default order"]</button> }</div>@if (Model.PageCount > 1) { <nav class="pager" aria-label="@D["Pagination"]"><button type="button" class="pg" aria-label="@D["Previous page"]" disabled="@(Model.PageNumber == 1)" data-directory-url="@Model.DirectoryUrl(page: Model.PageNumber - 1)"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("chevron-left")' /></button>@for (var page = 1; page <= Model.PageCount; page++) { <button type="button" class="pg @(page == Model.PageNumber ? "is-current" : "")" aria-current="@(page == Model.PageNumber ? "page" : null)" aria-label="@D["Page {0}", page]" data-directory-url="@Model.DirectoryUrl(page: page)">@(page)</button> }<button type="button" class="pg" aria-label="@D["Next page"]" disabled="@(Model.PageNumber == Model.PageCount)" data-directory-url="@Model.DirectoryUrl(page: Model.PageNumber + 1)"><partial name="_AdminDesignIcon" model='new AdminDesignIcon("chevron-right")' /></button></nav> }</div> }
src/Bingo.Web/Pages/Admin/Events/Index.cshtml:87:<partial name="_AdminEventCreateTemplate" model="Model" />
```

### timers busy fetch

```sh
rg -n 'setTimeout|setInterval|spinner|aria-busy|\.busy\(|fetch\(' src/Bingo.Web/wwwroot/js/admin-dashboard.js src/Bingo.Web/wwwroot/js/admin-events.js src/Bingo.Web/wwwroot/js/admin-event-create.js
```

Exit0; exact output:

```text
src/Bingo.Web/wwwroot/js/admin-event-create.js:18:    panel.setAttribute('aria-busy', String(pending()));
src/Bingo.Web/wwwroot/js/admin-event-create.js:58:    const response=await ui.busy(()=>window.AdminFetch.request(checking?'/Admin/Events/Create?handler=CheckAgain&requestId='+requestId:'/Admin/Events/Create',{
src/Bingo.Web/wwwroot/js/admin-events.js:24:  listen(search, 'input', () => { clearTimeout(timer); timer = setTimeout(searchNow, 250); });
```

### lifecycle integration

```sh
rg -n 'dispose|AbortController|registerDraft|AdminFetch|AdminUI|export function' src/Bingo.Web/wwwroot/js/admin-dashboard.js src/Bingo.Web/wwwroot/js/admin-events.js src/Bingo.Web/wwwroot/js/admin-event-create.js
```

Exit0; exact output:

```text
src/Bingo.Web/wwwroot/js/admin-events.js:2:import { initCreate, disposeCreate } from './admin-event-create.js';
src/Bingo.Web/wwwroot/js/admin-events.js:4:export function dispose() { disposeCreate(); release?.(); release = null; }
src/Bingo.Web/wwwroot/js/admin-events.js:5:export function init(region, ui = window.AdminUI) {
src/Bingo.Web/wwwroot/js/admin-events.js:6:  dispose();
src/Bingo.Web/wwwroot/js/admin-events.js:13:  const life = new AbortController();
src/Bingo.Web/wwwroot/js/admin-event-create.js:3:export function disposeCreate() { cleanup?.(); cleanup = null; }
src/Bingo.Web/wwwroot/js/admin-event-create.js:4:export function initCreate(region, ui) {
src/Bingo.Web/wwwroot/js/admin-event-create.js:5:  disposeCreate();
src/Bingo.Web/wwwroot/js/admin-event-create.js:8:  const t = key => labels[key] || key, life = new AbortController();
src/Bingo.Web/wwwroot/js/admin-event-create.js:58:    const response=await ui.busy(()=>window.AdminFetch.request(checking?'/Admin/Events/Create?handler=CheckAgain&requestId='+requestId:'/Admin/Events/Create',{
src/Bingo.Web/wwwroot/js/admin-event-create.js:82:    unregister=ui.registerDraft(source,{isDirty:dirty,isPending:pending,confirmLeave:askDiscard,discard:()=>{modal?.element.querySelector('#cm-name')&&(modal.element.querySelector('#cm-name').value='');if(modal)modal.element.querySelector('#cm-tz').value='Europe/Copenhagen';status='idle';}});
src/Bingo.Web/wwwroot/js/admin-dashboard.js:3:export function dispose() { release?.(); release = null; }
src/Bingo.Web/wwwroot/js/admin-dashboard.js:4:export function init(region, ui = window.AdminUI) {
src/Bingo.Web/wwwroot/js/admin-dashboard.js:5:  dispose();
src/Bingo.Web/wwwroot/js/admin-dashboard.js:8:  const lifetime = new AbortController();
```

### inline styling literals

```sh
rg -n 'style="[^"]*(color|font|margin|padding|gap)' src/Bingo.Web/Pages/Admin/Index.cshtml src/Bingo.Web/Pages/Admin/Events/Index.cshtml src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml src/Bingo.Web/Pages/Shared/_AdminEventsLoadStates.cshtml src/Bingo.Web/Pages/Shared/_AdminEventsPendingTable.cshtml
```

Exit1; no output.

### CSS literal values

```sh
rg -n '(^|[ :])#[0-9a-fA-F]{3,8}\b|rgb\(|hsl\(|font-family|box-shadow|border-radius:[^v]' src/Bingo.Web/wwwroot/css/admin-design-dashboard.css src/Bingo.Web/wwwroot/css/admin-design-events.css
```

Exit1; no output.

The .tbl rules only set reference column/minimum-width properties, not shared
component appearance. Skeleton radii use frozen tokens. Unique page skeleton
aliases carry reference geometry; the14px fact gap is reference-owned. Loading
header positioning is now page CSS, not loaded layout or inline spacing.
Remaining19 inline lines are reference/dynamic widths/heights/grid/flex/ticks,
not colours/fonts/spacing. Components use shared classes/partials,43 scan lines.
Only own timer is250ms Events search debounce; Create uses shared busy and
AdminFetch, A16 uses the single150/400 loading settings; Dashboard local sort is
atomic server-order DOM replacement. Module disposal/AbortController/registered
drafts/C-CMP-2 remain shared. Paired behavior gates cover actual execution.

## §10 leftover rg checks (rerun)

### D retired markup

```sh
rg -n 'admin-dashboard-wip|admin-dashboard-retained|UpcomingMilestones|WiseOldManSynchronizations|RecentAudits|PendingEvidenceSummary|LifecycleReadiness' src
```

Exit1; no output.

### D retired dependencies

```sh
rg -n 'IEventCompetitionSynchronizationService|GetStartReadinessAsync|AuditEntries' src/Bingo.Web/Pages/Admin/Index.cshtml.cs
```

Exit1; no output.

### D service integration

```sh
rg -n 'IAdminDashboardService|ICommunityDashboardService' src/Bingo.Web/Pages/Admin/Index.cshtml.cs
```

Exit0; exact output:

```text
16:public sealed class IndexModel(IAdminDashboardService dashboard, IStringLocalizer<AdminCommunityResource> text) : PageModel
```

### D Admin routing

```sh
rg -n '"/Admin"' src/Bingo.Web/Navigation/SharedShellService.cs src/Bingo.Web/Pages/Shared/_AdminLayout.cshtml
```

Exit0; exact output:

```text
src/Bingo.Web/Pages/Shared/_AdminLayout.cshtml:248:                <a class="admin-nav-link @(isCurrent("/Admin") ? "is-active" : null)" href="/Admin" aria-current="@(isCurrent("/Admin") ? "page" : null)"><svg class="admin-nav-icon" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8"><rect width="7" height="9" x="3" y="3" rx="1" /><rect width="7" height="5" x="14" y="3" rx="1" /><rect width="7" height="9" x="14" y="12" rx="1" /><rect width="7" height="5" x="3" y="16" rx="1" /></svg>@T["Dashboard"]</a>
```

### D raw unavailable

```sh
rg -n 'UnavailableReason' src/Bingo.Web
```

Exit1; no output.

### E retired readiness

```sh
rg -n 'StatusLabel|EventDisplayPhase|GetStartReadinessAsync' src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs
```

Exit1; no output.

### E retired hooks

```sh
rg -n 'initializeAdminEventDirectorySearch|data-admin-event-directory-search|data-admin-event-sort-link' src
```

Exit1; no output.

### E cards

```sh
rg -n 'data-label=' src/Bingo.Web/Pages/Admin/Events/Index.cshtml
```

Exit1; no output.

### E old wording

```sh
rg -n '\["Workspace"\]|\["Inspect"\]|title or slug' src/Bingo.Web/Pages/Admin/Events/Index.cshtml
```

Exit1; no output.

### E old hidden links

```sh
rg -n 'filter = "hidden"|asp-route-filter="hidden"|filter=hidden' src
```

Exit0; exact output:

```text
src/Bingo.Web/Pages/Admin/Events/Manage.cshtml.cs:178:        return result.Succeeded ? RedirectToPage("Index", new { filter = "hidden" }) : RedirectToPage(new { id });
src/Bingo.Web/Pages/Admin/Events/Manage.cshtml:94:        <p><a class="btn event-create-cancel" asp-page="Index" asp-route-filter="hidden">@T["Back to hidden events"]</a></p>
```

### E query binding

```sh
rg -n 'name = "page"|Name = "page"|Name = "phase"' src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs
```

Exit0; exact output:

```text
36:    [BindProperty(SupportsGet = true, Name = "phase")]
39:    [BindProperty(SupportsGet = true, Name = "page")]
40:    [FromQuery(Name = "page")]
```

### E retired create file

```sh
rg -n 'event-create\.js' src tests
```

Exit0; exact output:

```text
src/Bingo.Web/wwwroot/js/admin-events.js:2:import { initCreate, disposeCreate } from './admin-event-create.js';
```

### E create entry

```sh
rg -n 'asp-page="Create"|asp-page="/Admin/Events/Create"' src
```

Exit1; no output.

Allowed residuals unchanged: Manage's legacy filter=hidden links are supported
by the directory alias; retained old-page composition outside U2 is not retired.
The single event-create.js substring is the active admin-event-create.js import,
not the retired orphan file. All retired Dashboard queries/readiness/mobile cards/
directory hook scans have0 matches. Approval rows stay awaiting Claude review,
then user visual acceptance.

## Final gate results

- Final clean command: `dotnet clean Bingo.slnx --configuration Release -v:minimal`, exit0.
- `dotnet build Bingo.slnx --configuration Release --no-restore -v:minimal`:
  **0 warnings,0 errors**,32.88s. Final frozen-results field-name correction and
  all final UR assertions are included. Owned parity fixture build0/0,7.44s.
- Final UR PostgreSQL/HTTP: **2 passed,0 failed,0 skipped**,43s,
  `tests/Bingo.IntegrationTests/TestResults/u2-remediation-item7-ur-final.trx`.
- Full JS runner `env BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=<checkout>/artifacts/js-fixtures PLAYWRIGHT_CHANNEL=chromium <node> scripts/run-browser-tests.cjs`:
  **61 passed,0 failed**. Includes every admin-design/identity test in Chromium
  and WebKit, plus unchanged default tests. Exact150/400 fake-clock proofs and
  shared shell passed both. Generated `artifacts/js-tests/results.json` and
  per-execution logs retained. Required HTTP fixture generator **8/0/0**,12s,
  `tests/Bingo.IntegrationTests/TestResults/u2-remediation-js-fixtures.trx`.
- Paired real Razor/PostgreSQL rendered checks: Dashboard **18/0**, Events
  **26/0**, Create **10/0**, extended UR **4/0**. Both themes and both engines;
  all loaded reference comparisons0 differences, Dashboard390/860/1280 exact;
  loading movement≤one summary line390,0 at860/1280. Events query-bound loading/
  failed structure, clean/dirty outside-close, URL replace, literal-dollar notice,
  EN/DA recovery and real culture switch passed. UR verifies live/final-review
  profiles, paging/culture/Cancelled/missing settings/import/shared first,
  System/import audit and recipient inbox rows, repeated A16/Back/Forward/disposal.
  Scripts: `scripts/check-u2-{dashboard,events,create,ur}.cjs`; exact JSON/PNGs
  under `artifacts/u2-{dashboard,events,create,ur-browser}/`.
- UR browser authoring checkpoint: Back updates address bar before asynchronous
  DOM swap; immediate old assertion observed0 rows. Final proof awaits actual
  visible destination DOM on every Back/Forward, then retains exact25 rows and
  single-module/no-stale-listener assertions. No sleeps or row-count tolerance.
- `git diff --check` exit0; Node syntax exit0; frozen CSS/HTML diffs empty.
  Review command EF logging is Warning; owned fixture minimum Warning unchanged.
  Both matrix approval states unchanged. No GitHub Actions/Linux proof claimed.

## Whole .NET — user-directed stop / completion waiver

The whole suite started after item7 commit on exact
`c0cae2567b0880cf7a2058c6a7e32c63cba63b5b`. The user then explicitly directed:
“Wrap up now … the planner can run it while I do visual inspection”. Completion
of the implementer's whole-suite gate is **waived by that command**, not passed.
Domain265/0/0, Application118/0/0 and whole BrowserTests160/0/0 completed
(543 completed tests total); Integration was still running and was cancelled.
Whole-command wall189.93s; exit1 from cancellation, not a reported test failure.
There is **no completed Integration final TRX** and no whole-suite pass claim.
Exact raw TRX counters/times and absolute paths are durable in the local
[execution record](artifacts/final-sha-results.json), status `user-stopped`.
This ignored generated artifact is beside this one tracked item7 evidence file.
The final item7 amendment only records the user-stop decision in this evidence
and CURRENT_STATUS; production/test source is unchanged from the executed SHA.

Command: `/usr/bin/time -p dotnet test Bingo.slnx --configuration Release --no-build --logger 'trx;LogFileName=u2-remediation-final.trx' -v:minimal`.
Exact TRX paths, under this checkout:

- `tests/Bingo.Domain.Tests/TestResults/u2-remediation-final.trx`
- `tests/Bingo.Application.Tests/TestResults/u2-remediation-final.trx`
- `tests/Bingo.BrowserTests/TestResults/u2-remediation-final.trx`
- `tests/Bingo.IntegrationTests/TestResults/u2-remediation-final.trx`

The first three TRX paths exist with0 failed/0 notExecuted. The Integration path
above is the requested destination only; it does not exist after cancellation.
No whole-suite pass is claimed. The planner/Claude can run the final whole suite
while the user does visual inspection, as explicitly requested. The three unsupported
reference data states and reasons remain listed in item3 evidence (unknown
platform approval, linked-but-incompatible WOM, Live is not Ended); no newly
invented reference wording or product question. User visual acceptance of these
pages/loading timing remains pending. Stop after7: planner/Claude review/whole-suite
execution and user visual inspection may proceed in parallel; no U3+, laneT, packaging or publication.
