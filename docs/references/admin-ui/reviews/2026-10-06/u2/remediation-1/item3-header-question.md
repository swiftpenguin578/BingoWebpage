# U2 brief70 remediation item3 — Dashboard, resolved and implemented

6 October 2026. User decision option(a) resolved the question below: loaded
reference geometry is exact; at390px fresh loading→loaded movement is bounded
by one summary text line, zero at860/1280. No truncation or remembered card.
Implementation/check evidence, not self-review or visual acceptance.

## Implementation and changed files

- `IAdminDashboardService.cs`, `AdminDashboardService.cs`: event timezone and
  existing stored competition-link state supplied with history/chart. Metric
  populations/compatibility stay unchanged.
- `Index.cshtml`, `Index.cshtml.cs`, `admin-dashboard.js`: reference M5
  note/footer/returning percentage/tracking footer/stat notes/import hints,
  unavailable EHB-by-state hint, empty text, muted EHB, history hint tab stops,
  Winner ascending default and complete bar names. Dates use event timezone;
  Signups-open uses tone-open. All server-computed history sort orders are sent
  with the same authenticated result; browser reorders existing rows, preserves
  focused control and exact scroll, replaces URL through shared setUrl. No
  duplicate client ordering, metric fetch, page load or extra navigation history.
- `_AdminDashboardLoadStates.cshtml`, `admin-design-dashboard.css`,
  `admin-design-layout.css`: remove loaded fixed-width/truncation adapters;
  placeholder positioning is loading-only page CSS with tokens.
- `admin-design-shell.js`: all generic destination skeletons remove previous
  page-owned header actions; failed header also has none. Replace-mode shell URL
  writes use replaceState (Events still keeps its existing behavior).
- `_Layout.cshtml`: public Admin-actions Retry targets
  /notifications#admin-actions-heading.
- `AdminCommunityResource.da.resx`: restored reference wording in Danish.
- `U2DashboardPresentationTests.cs`, `admin-design-shell.browser.js`,
  `scripts/check-u2-dashboard.cjs`: HTTP/timezone/state rendering, old-action
  cleanup, exact loaded/reference and fresh loading positions and interactions.
- `DELIVERY_PLAN.md`: phone decision refinement and per-page synchronous history
  sort binding. `CURRENT_STATUS.md`: clears resolved stop. This evidence file.

## Executed checks

- Fixture Release build: 0 warnings,0 errors,7.08s.
- Dashboard HTTP/presentation: **6 passed,0 failed,0 skipped**,8s;
  `tests/Bingo.BrowserTests/TestResults/u2-remediation-item3-final.trx`.
  UTC31Mar23:30→Copenhagen1Apr proves Month/Range/overdue date; reference
  accessible names and imported/unlinked/linked-but-incompatible states tested.
- `env PLAYWRIGHT_CHANNEL=chromium <bundled-node> scripts/check-u2-dashboard.cjs`:
  **18 passed,0 failed** across Chromium/WebKit. 10 rendered comparisons
  (both themes plus loaded390/860/1280) each **0 differences**. All six fresh
  header loading→loaded runs meet the user bounds; exact positions in
  `artifacts/u2-dashboard/results.json` and per-comparison JSON/PNG files.
  Keyboard chart/Escape/arrows, sort no document requests/no history push,
  exact retained focus/scroll, Winner asc, hint tab stops, real culture switch
  EN/DA, failure/retry/disposal, repeated switches/Back/Forward pass.
- Shared shell runner separately Chromium and WebKit: PASS focus/layers/menus,
  dirty navigation, reversible swap/failure/fallback, disposal and previous
  header-action exclusion. Frozen references/tokens/components untouched.
- `git diff --check`: exit0.

## Evidence-backed unsupported reference states (for planner, no new wording)

The reference only models measured platform counts, linked usable WOM data and
ended review events. Actual unknown platform approval retains existing localized
“Published board approval or a usable event interval is unavailable.” and the
reference unavailable hint structure, never a fabricated zero. A stored linked
competition without compatible coverage cannot truthfully say WOM wasn’t linked:
it retains the existing “No compatible stored EHB coverage is available.”.
Live has no actual ended date: approved Live wording/provisional hint is retained,
rather than falsely using “Ended…awaiting final review.”. Unusable intervals
retain their existing unknown hints. These are identified rather than new
invented wording; brief70 explicitly requires listing such real-data gaps.
U2-1 team-count Players hint remains (authorized binding), without an extra
keyboard tab stop; available Approved values are plain exactly as reference.

## Corrected check checkpoints

CA1826/keyword diagnostics corrected before final build. A newly added broad
hint-count assertion omitted the reference board/provisional hints; replaced by
exact per-cell assertions, without changing production for the faulty count.
Earlier full-region sort lost18px in WebKit; replaced with explicitly permitted
page-local server-order re-render; exact scroll assertion remains.
An old zero-shift861px test assumed one-line summary despite the reference's
five-line summary just above its mobile breakpoint. Current user decision
explicitly sets390/860/1280 gates; loaded reference rules stay unchanged at861.
No861 zero-shift pass is claimed. Final run above supersedes these checkpoints.

## Preserved original question/diagnostic (resolved, not current blocker)

# U2 brief70 remediation item3 — planner question before implementation

At the earlier checkpoint item3 stopped before implementation for this question.
Then source HEAD:
`a6af48bb6a0fb35e68d3cae6e43cafda39770945` (items1–2 committed).
That diagnostic made no production edits; no reviewer/self-review or user DB access.

## Exact question and recommendation

May the earlier “real card replaces it without layout shift at any width” rule be
relaxed **only for the data-dependent header geometry** when a fresh uncached
Dashboard response arrives? Keep the loaded summary naturally wrapping and card
content-sized exactly as the reference; use the approved fresh card-sized
placeholder, but permit the summary/card to settle to the real dimensions.

Recommended: yes, prioritize reference loaded-state sizing and truthful complete
summary; register the bounded loading-geometry exception. Otherwise the planner
must choose an explicit loaded-state reservation/layout difference. The implementer
must not silently introduce one or waive the previous rule.

## Conflicting authorities and measured boundary

- brief70 item3 /69a M4: loading adapters must not change the loaded header;
  summary never truncated and card follows reference sizing.
- `docs/references/admin-ui/Dashboard.dc.html:37,40`: desktop card has only
  flex:none/max-width420; mobile ≤860px width100%. Shared summary at
  `ui/components.css:108` wraps; header stacks below860 at:436.
- User “U2 Dashboard header while loading”, option(b), decisions08:379 and
  DELIVERY_PLAN:1181: fresh uncached skeleton, same reserved summary height,
  real card replaces it **without layout shift at any width**.
- `Index.cshtml.cs:25–34`: summary includes last-ended/date/age only when a recap
  exists. A valid one-Live-event/no-ended-recap summary is one line at390;
  the controlled fixture's real recap summary is two. No cached/remembered data
  may determine which future header height is needed.

A fixed no-data placeholder cannot match both natural one- and two-line loaded
headers at the same viewport. This is not solved by reserving two lines everywhere:
that changes the reference's one-line loaded header. A fixed420 desktop real card
likewise contradicts M4. Only the loading placeholder may be420 without changing
loaded sizing; its bounds still cannot match every content-sized real card.

## Bounded executed diagnostic — Chromium and WebKit

Owned PostgreSQL/Kestrel parity fixture, actual compiled Razor/local fonts. No
repository CSS was edited. In each browser only the four unwanted Dashboard
adapters were removed from the loaded stylesheet via CSSOM. The resulting loaded
header uses the existing reference page rules. Then the existing fresh loading
header template was placed in the same position and measured.

The alternate one-Live summary is a **browser-only supported text variant** based
on the actual PageModel's no-recap branch, not a second DB-seeded scenario. It
demonstrates the dimension choice without mutating event data. All six viewport/
engine executions completed, exit0; both engines produced identical values.

| Width | Real loaded header height | One-Live summary header height | Fresh loading header height | Real / loading card width |
| --- | ---: | ---: | ---: | ---: |
|390|173.0625|153.5|153.5|358 /358|
|860|153.5|153.5|153.5|828 /828|
|1280|76.34375|76.34375|76.34375|420 /169.1875|

At390, the reference-sized real summary is39.125px high versus19.5625px for the
valid one-line state/loading reservation. The real card's y=170.71875 versus
151.15625 while loading: **19.5625px vertical difference**. At1280, the unadapted
fresh placeholder has natural169.1875px width; a loading-only420px width can fix
this fixture, but cannot predict a different real card's intrinsic content width.
No final parity pass is claimed from this diagnostic.

Fixture command:
`dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release --no-restore -v:minimal`:
**0 warnings,0 errors**,15.43s.

Diagnostic command: bundled Node `-e` with the exact code below, in the assigned
checkout. Owned generated logs are ignored at
`artifacts/u2-remediation-header-sizing/`. No production edits/artifacts committed.

```javascript
const {chromium,webkit}=require("playwright");
const {startFixture,login}=require("./scripts/lib/admin-parity-fixture.cjs");
(async()=>{
const fixture=await startFixture(process.cwd(),process.cwd()+"/artifacts/u2-remediation-header-sizing");
const results=[];let browser;
try {
for(const engine of ["chromium","webkit"]){
browser=await ({chromium,webkit}[engine]).launch({headless:true});
const context=await browser.newContext({viewport:{width:1280,height:1000},reducedMotion:"reduce"});
const page=await login(context,fixture);
for(const width of [390,860,1280]){
await page.setViewportSize({width,height:1000});
await page.goto(fixture.origin+"/Admin");
await page.waitForFunction(()=>window.AdminUI);
await page.evaluate(()=>document.fonts.ready);
const values=await page.evaluate(()=>{
// Remove only the four unwanted loading adapters, in this diagnostic browser,
// to measure the reference-like loaded header without changing repository CSS.
for(const sheet of document.styleSheets){
if(!sheet.href?.includes("admin-design-layout"))continue;
for(let i=sheet.cssRules.length-1;i>=0;i--){
if(sheet.cssRules[i].cssText.includes('[data-page-family="dashboard"]'))sheet.deleteRule(i);
}
}
const head=document.querySelector('[data-page-region] .page-head');
const rect=(head,selector)=>{const r=head.querySelector(selector).getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height};};
const loaded={headHeight:head.getBoundingClientRect().height,summary:rect(head,".summary"),card:rect(head,".next-event")};
head.querySelector(".summary").textContent="1 event since Oct 2026";
const oneLiveEvent={headHeight:head.getBoundingClientRect().height,summary:rect(head,".summary"),card:rect(head,".next-event")};
const pending=document.querySelector('[data-page-header-template="dashboard"]').content.firstElementChild.cloneNode(true);
head.replaceWith(pending);
const loading={headHeight:pending.getBoundingClientRect().height,summary:rect(pending,".summary"),card:rect(pending,".next-event")};
return {loaded,oneLiveEvent,loading};
});
results.push({engine,width,...values});
}
await browser.close();browser=null;
}
console.log(JSON.stringify(results));
}finally{await browser?.close();await fixture.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
```

Exact measured output:

```json
[{"engine":"chromium","width":390,"loaded":{"headHeight":173.0625,"summary":{"x":16,"y":107.59375,"width":358,"height":39.125},"card":{"x":16,"y":170.71875,"width":358,"height":76.34375}},"oneLiveEvent":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":141.6875,"height":19.5625},"card":{"x":16,"y":151.15625,"width":358,"height":76.34375}},"loading":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":116.59375,"height":19.5625},"card":{"x":16,"y":151.15625,"width":358,"height":76.34375}}},{"engine":"chromium","width":860,"loaded":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":391.078125,"height":19.5625},"card":{"x":16,"y":151.15625,"width":828,"height":76.34375}},"oneLiveEvent":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":141.6875,"height":19.5625},"card":{"x":16,"y":151.15625,"width":828,"height":76.34375}},"loading":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":116.59375,"height":19.5625},"card":{"x":16,"y":151.15625,"width":828,"height":76.34375}}},{"engine":"chromium","width":1280,"loaded":{"headHeight":76.34375,"summary":{"x":284,"y":138.78125,"width":520,"height":19.5625},"card":{"x":828,"y":82,"width":420,"height":76.34375}},"oneLiveEvent":{"headHeight":76.34375,"summary":{"x":284,"y":138.78125,"width":520,"height":19.5625},"card":{"x":828,"y":82,"width":420,"height":76.34375}},"loading":{"headHeight":76.34375,"summary":{"x":284,"y":138.78125,"width":116.59375,"height":19.5625},"card":{"x":1078.8125,"y":82,"width":169.1875,"height":76.34375}}},{"engine":"webkit","width":390,"loaded":{"headHeight":173.0625,"summary":{"x":16,"y":107.59375,"width":358,"height":39.125},"card":{"x":16,"y":170.71875,"width":358,"height":76.34375}},"oneLiveEvent":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":141.6875,"height":19.5625},"card":{"x":16,"y":151.15625,"width":358,"height":76.34375}},"loading":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":116.59375,"height":19.5625},"card":{"x":16,"y":151.15625,"width":358,"height":76.34375}}},{"engine":"webkit","width":860,"loaded":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":391.078125,"height":19.5625},"card":{"x":16,"y":151.15625,"width":828,"height":76.34375}},"oneLiveEvent":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":141.6875,"height":19.5625},"card":{"x":16,"y":151.15625,"width":828,"height":76.34375}},"loading":{"headHeight":153.5,"summary":{"x":16,"y":107.59375,"width":116.59375,"height":19.5625},"card":{"x":16,"y":151.15625,"width":828,"height":76.34375}}},{"engine":"webkit","width":1280,"loaded":{"headHeight":76.34375,"summary":{"x":284,"y":138.78125,"width":520,"height":19.5625},"card":{"x":828,"y":82,"width":420,"height":76.34375}},"oneLiveEvent":{"headHeight":76.34375,"summary":{"x":284,"y":138.78125,"width":520,"height":19.5625},"card":{"x":828,"y":82,"width":420,"height":76.34375}},"loading":{"headHeight":76.34375,"summary":{"x":284,"y":138.78125,"width":116.59375,"height":19.5625},"card":{"x":1078.8125,"y":82,"width":169.1875,"height":76.34375}}}]
```

## Saved state / checks not yet eligible

Item1:`50df2da759270c657f3ef1018d0fdd7f8b33fd7c`; item2:
`a6af48bb6a0fb35e68d3cae6e43cafda39770945`. Their passing focused checks and exact
changed files are in item1/item2 evidence. Only this question evidence and the
required CURRENT_STATUS checkpoint remain uncommitted. A blocked item is not a
completed item and must not receive a completion commit.

Items3–7 and final batch gates are unfinished. In particular, there is **no
final item7 SHA yet** on which to run the required whole .NET suite; no whole-suite
pass/TRX claimed. No clean Release/final JS/rendered/UR batch acceptance claimed.
Matrix rows stay awaiting Claude review, then user visual acceptance.

Stop-check `git diff --check`:exit0. Both token/component `cmp` commands:exit0,
no output. Exact baseline-to-checkpoint `git diff --name-only` limited to the
two deployed frozen CSS files, two reference shared CSS files and Dashboard/
Events reference HTML:exit0, no output. Only this question evidence and
CURRENT_STATUS are uncommitted; no implementation files pending.

Next permitted action: planner resolves the exact header-geometry priority above;
resume item3 from these preserved item1–2 commits, then sequential items4–7 and
the final-SHA whole suite/all required gates. No push/merge/deploy.
