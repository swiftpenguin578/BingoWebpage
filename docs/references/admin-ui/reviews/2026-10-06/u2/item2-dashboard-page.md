# U2 item 2 — checked Dashboard binding, awaiting review

Date: 6 October 2026. Role: implementer/remediator, not independent reviewer.
Base/current HEAD: `6c8037b52fd8281fd9e1a9e869dd146ef4c57e38`.
Item2 is checked for its scoped commit; the resumed resolution/results below
supersede the historical blocked checkpoint. Items3–5 are next.
No workers, reviewer, orchestrator, branch/worktree creation, push, merge or deployment.

## Historical planner question — resolved by user option (b)

Brief67 requires server-rendered Dashboard reads, the reference loading/failure
states, and stopping this item for every unapproved reference difference.

Exact question: **On an A16 first visit to Dashboard from an event page, before
Dashboard data has arrived, may the loading/failed-load header omit the
current/next-event card and retain an empty summary line, or must it render a
card-sized placeholder? What should happen on later visits when a prior Dashboard
header could be cached?**

Reference evidence: frozen Dashboard.dc.html:106–124 renders the summary and
next-event card independently of loading/failure. :128–139 contains the statistics
skeleton; :143–152 contains failure. `renderVals` :722 keeps `next: this.vmNext()`
while its summary is blank when not ready. The reference has a complete in-memory
dataset; the authorized server-rendered integration has no Dashboard payload
until the ordinary GET response arrives.

Current draft difference, not approved: admin-design-shell.js:389–392 changes the
heading to Dashboard, removes old event-page header actions, and removes the
summary element. This avoids displaying unrelated Identity header content but
changes the reference's header/card composition and vertical positions in loading
and failure. No loading/failure position-parity pass is claimed. No new JSON read,
speculative totals, prefetch, or header-cache behavior has been added. Ruling56
authorizes the reversible overlay, not this new community-page header treatment;
decisions08 A16 and the accepted address-bar/loading rule remain protected.

Recommended planner choice: authorize a destination title with an empty reserved
summary line, no data-dependent card on a first visit, and the same treatment on
later visits (no stale card/cache). Register that difference explicitly. This is
a **proposal only**, not an implemented approved decision. Production work on item2
stopped when this question was identified.

## Preserved draft changes

- Index opts into the accepted Admin shell and binds statistics, chart, ended
  recap, six history sorts, current/next card, and community values to the service.
- State-based metric hints use a new page-family localizer, not raw Infrastructure
  UnavailableReason. Unknown participants render an em dash and no fabricated bar
  or numeric ARIA count; latest participation delta requires an available count.
- Chart module owns pointer/focus/arrow/Escape behavior, actual Overview navigation,
  sort announcement, table overflow observation and cleanup.
- Reference Dashboard-only CSS is copied into its page stylesheet; retired
  Dashboard CSS is removed except row-action styles still used by Events until3.
- Shared shell has destination-kind loading/failure templates. Its draft header
  treatment is the question above, not accepted behavior.
- A4 leftover check identified old-layout Admin-action overview/retry URLs still
  pointing to Dashboard; changed those to /notifications#admin-actions-heading,
  preserving the real Dashboard navigation link. Accepted U1 design topbar already
  used that destination; no composition change there.
- Controlled PostgreSQL/Kestrel parity fixture now supplies retained participants
  and exports the actual Dashboard read model to the test manifest only. Production
  has no new JSON endpoint. Generalized reference test helper preserves its default
  Identity behavior; new Dashboard runner/wrapper cover both engines.
- Item1 evidence corrects an explicitly historical incomplete-checkpoint sentence.
  No committed product/test assertion from item1 was reverted.

Files modified/untracked at this checkpoint (repository-relative):

```
CURRENT_STATUS.md
UI_PAGE_MATRIX.md
docs/references/admin-ui/reviews/2026-10-06/u2/item1-dashboard-backend.md
docs/references/admin-ui/reviews/2026-10-06/u2/item2-dashboard-page.md
scripts/lib/admin-parity-fixture.cjs
scripts/check-u2-dashboard.cjs
src/Bingo.Web/AdminCommunityResource.cs
src/Bingo.Web/Navigation/SharedShellService.cs
src/Bingo.Web/Pages/Admin/Index.cshtml
src/Bingo.Web/Pages/Admin/Index.cshtml.cs
src/Bingo.Web/Pages/Shared/_AdminDesignTemplates.cshtml
src/Bingo.Web/Pages/Shared/_AdminDashboardLoadStates.cshtml
src/Bingo.Web/Pages/Shared/_AdminLayout.cshtml
src/Bingo.Web/Resources/AdminCommunityResource.da.resx
src/Bingo.Web/wwwroot/css/admin-design-dashboard.css
src/Bingo.Web/wwwroot/css/admin-design-layout.css
src/Bingo.Web/wwwroot/css/site.transitional.application.css
src/Bingo.Web/wwwroot/js/admin-design-shell.js
src/Bingo.Web/wwwroot/js/admin-dashboard.js
tests/AdminDesignParityFixture/FixtureHost.cs
tests/Bingo.BrowserTests/U2DashboardHttpTests.cs
tests/Bingo.BrowserTests/admin-design-dashboard.browser.js
```

## Executed checks — not final acceptance

Commands in assigned checkout:
- `dotnet build src/Bingo.Web/Bingo.Web.csproj --configuration Release --no-restore`:
  **0 warnings / 0 errors**, 17.62s, early draft.
- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release --no-restore`:
  initial duplicate case-only resource key gave 1 warning; fixed the key.
  Later added Summary helper failed CA1826; changed to direct indexing.
  Subsequent builds **0 warnings / 0 errors**, including 11.39s final displayed
  fixture build before the last unknown-value/A4 edits. No final clean build claimed.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~U2DashboardHttpTests|FullyQualifiedName~AuditPresentationTests|FullyQualifiedName~AdminShellUiTests' --logger 'trx;LogFileName=u2-item2-http.trx'`:
  **11 passed / 0 failed / 0 skipped**, 7s, before adding unknown-value theories.
- Same filter with `u2-item2-http-final.trx`: **12 passed / 1 failed / 0 skipped**,
  11s. The new DA theory passed culture only in the query string, but Program.cs
  :79–89 deliberately accepts culture cookie/Accept-Language, not query strings;
  the response is English. Existing tests stay unchanged. Next technical correction:
  use the existing accepted language boundary in this new test.
- Bundled Node `scripts/check-u2-dashboard.cjs`: first two executions found
  rendering defects (standard `d` format emitted a short date; Razor emitted
  literal s-1@provisional/s-2@provisional class text). Fixed both. The next
  execution reported regular-content parity **0 differences** for light/dark in
  both Chromium and WebKit, within 1px positional tolerance. It **exited1** later
  on WebKit `ResizeObserver loop completed with undelivered notifications.`
  after overflow observation was added. Do not claim the combined browser proof passed.
- Earlier runs before overflow observation passed keyboard arrows/Escape,
  actual sort URL/live announcement, Danish switch and back, failure/retry,
  disposed-bar inertness, repeated Identity/Dashboard swaps and Back/Forward
  in both browsers; those earlier combined runs failed their then-unfixed parity.
  Proof artifacts are ignored `artifacts/u2-dashboard/*.json` and PNGs;
  inspect their latest identities rather than treating them as final-SHA evidence.

Parity test uses the real service output as reference sample data in memory,
never edits the frozen source. Approved D8 current Live provisional/latest-ended,
D9 current label, D11 no-capacity, and D14 unknown metric primitive are normalized
explicitly in the test source. It does not establish loading/failure parity,
all phone widths, every state/import/shared-winner fixture, or final acceptance.

Known technical work after the planner ruling (not additional product questions):
- Fix new DA HTTP test to use Accept-Language/culture cookie.
- Fix overflow-observer scheduling/cleanup without suppressing browser errors.
- Respect decisions08 U-G: Danish Admin wording uses “event/events,” not “bingo/
  bingoer”; draft localizer still needs this terminology correction.
- Complete D14/state wording evidence, missing-state/phone/loading parity and
  focused regression checks, then commit item2 before item3.

## Mechanical/source protection checks

`git diff --check`: exit0.
`rg -n 'admin-dashboard-wip|admin-dashboard-retained|UpcomingMilestones|WiseOldManSynchronizations|RecentAudits|PendingEvidenceSummary|LifecycleReadiness' src`: no matches.
`rg -n 'UnavailableReason' src/Bingo.Web`: no matches.
Handler contains IAdminDashboardService; no old readiness/WOM/audit operations queries.
A4 URLs checked as above; Dashboard nav remains /Admin.
Frozen tokens/components/Dashboard.dc.html/Events.dc.html: no diff from exact
initial SHA 8355680. Protected EventsDirectoryIntegrationTests remains unchanged.

Full JS runner, whole BrowserTests, final clean Release solution build, final-SHA
whole .NET suite, and complete batch A16/leftover gates have **not** run.
No self-review or independent Claude review claimed.

## Acceptance and next permitted action

Completed per-item commits and exact changed-file sets:

- Item0 `edd7bbcd825a9ab6a34864502faa1205c47c1dbd`:
  DELIVERY_PLAN.md; PRODUCT_REQUIREMENTS.md;
  docs/references/admin-ui/reviews/2026-10-06/u2/item0-docs.md.
- Item1 `6c8037b52fd8281fd9e1a9e869dd146ef4c57e38`:
  CURRENT_STATUS.md; UI_PAGE_MATRIX.md;
  docs/references/admin-ui/reviews/2026-10-06/u2/item1-dashboard-backend.md;
  src/Bingo.Application/Dashboard/IAdminDashboardService.cs;
  src/Bingo.Infrastructure/Dashboard/AdminDashboardService.cs;
  src/Bingo.Web/Pages/Admin/Index.cshtml;
  src/Bingo.Web/Pages/Admin/Index.cshtml.cs;
  tests/Bingo.BrowserTests/AuditPresentationTests.cs;
  tests/Bingo.BrowserTests/U2DashboardHttpTests.cs;
  tests/Bingo.IntegrationTests/U2DashboardIntegrationTests.cs;
  tests/Bingo.IntegrationTests/UiReviewScenarioIntegrationTests.cs.

Dashboard and Events directory remain **awaiting Claude review, then user visual
acceptance**. Only items0/1 are committed. Obtain the header ruling above, preserve
this work, remediate the listed technical failures, complete/commit item2, then
continue items3–5. No U3+, lane T, packaging, push, merge or deployment.

## Resumed resolution and passing item2 checkpoint — 6 October 2026

The user approved option (b), recorded in decisions08 “U2 Dashboard header while
loading”. Supersedes the question and draft above: every loading visit has a
fresh Dashboard title, reserved summary, and .sk card-sized placeholder; failure
removes the card/placeholder. No remembered card. DELIVERY_PLAN register updated.
Template uses the real card's three line boxes; layout adapters keep its 420px
desktop slot and full-width ≤860px slot, without changing frozen CSS.
Empty/unknown figures are never used to manufacture a current event.

Corrected overflow writes to a cancellable animation frame, with observer/frame
cleanup. Corrected the new DA HTTP test to POST the existing /Language handler
with real antiforgery, assert culture-cookie issuance and render all six sorts.
New page-family Danish strings use event/events per U-G. Hints retain reference
viewport placement and Escape dismissal with page-owned listener cleanup.

Latest executed results:
- Fixture Release build: 0 warnings/errors, 8.04s.
- Focused U2DashboardHttpTests + AuditPresentationTests + AdminShellUiTests:
  **13 passed / 0 failed / 0 skipped**, 9s; u2-item2-resumed-http.trx.
- UiReviewScenarioIntegrationTests: **2/0/0**, 19s; u2-item2-ur.trx.
- Shared shell and language browser regressions: both files exit0 in both Chromium
  and WebKit (4 executions); accepted dirty guard, Back/Forward, disposal, fallback,
  background culture-cookie swap and focus/scroll retained.
- Dashboard runner: **16 passed / 0 failed**: both themes regular-content reference
  comparator 0 differences in both engines; loading-vs-loaded head, summary,
  card and first-content positions within 1px at 1440/1280/861/860/390px in both
  engines; keyboard/Escape/sort announcement, actual language switch, no-card
  failure/retry, disposed listeners, repeated swaps and Back/Forward all pass.
- Source leftovers listed above: no retired Dashboard or raw-reason occurrences;
  service binding present, operations queries absent. Frozen CSS/references unchanged.
  git diff --check exit0. Full batch gates remain scheduled after item5.

Proposed binding-time wording (reference → EN / DA; names/numbers are actual data):
- D8 reference “Final review” Live pill → “Live · provisional” / “Live · foreløbig”.
  Reference “N awaiting final review” → “N live or awaiting final review” /
  “N live eller til afsluttende gennemgang”.
- D9 reference “Next event” for Live → “Current event” / “Aktuelt event”.
- D10 reference future-date card → “Start was due <date>” /
  “Start var planlagt til <dato>”.
- D11 set-cap/date assumptions → “N confirmed · No capacity set” /
  “N bekræftede · Ingen kapacitet angivet”; “Not announced” / “Ikke annonceret”.
- D12 single sample winner → all actual shared winners, separated by “ · ”,
  unchanged team names in both languages.
- D14 English Infrastructure reasons replaced by page-state hints: actual
  interval unavailable, imported reconstruction, missing board approval/interval,
  EHB compatibility/coverage/frozen snapshot, tracked-history boundary, provisional
  figures and community boundary. The resx records each exact EN/DA string.
  No UnavailableReason rendering or unknown-to-zero conversion.

This is implementer execution evidence, not self-review or independent review.
Item2 is ready for its scoped checkpoint commit, awaiting Claude review and user
visual acceptance. Items3–5 are the next authorized work; no later lane/publication.

Final item2 continuation: after adding viewport hint placement, one no-build
fixture run showed the layout adapter missing (default 8px body margin, 52
geometry differences); inferred stale static-asset build metadata, not an
approved design change. Rebuilt the fixture (0 warnings/errors, 1.45s) before
rerunning. Final Dashboard runner **16 passed / 0 failed**, both engines and all
five loading widths. No tolerance was increased and no browser error suppressed.
Final leftovers/diff checks passed; Admin-action URLs now leave only the real
Dashboard navigation link at /Admin. Final full-batch gates remain after item5.
