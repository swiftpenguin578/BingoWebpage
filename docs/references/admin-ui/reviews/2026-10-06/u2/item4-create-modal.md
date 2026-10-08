# U2 item 4 — Create modal and approved uncertain departure

6 October 2026. Implementer evidence, not independent review or acceptance.
Base/HEAD: `90ae954c55805943698408ab4f790336cb53694e` (item3 committed; clean
before item4). **Item4 implemented and checked; item5 and batch gates pending.** No push,
merge, deploy, new worker/reviewer/orchestrator, branch or worktree.

## Resolved question: closing an uncertain Create

`Events.dc.html:699-702` routes a dirty Create close through its discard modal,
including after an uncertain write; `:949-950` uses “Discard unsaved changes?”,
“The name and timezone you entered haven’t been saved.”, Keep editing / Discard.
The retained key/readback can prove the event was already created after a lost
response (AU03; `EventCreationRetryIntegrationTests.LostCommitResponseCanBeReadBackAndRetriedWithoutAnotherCreation`).

`UI_SYSTEM.md:87-90` U-A says never to claim an unknown write was not saved.
`UI_SYSTEM.md:1107-1116` and the accepted U1 Identity binding instead use Check
again / Leave anyway while uncertain, with normal discard for ordinary dirty
input. Brief67 item4 explicitly binds E-16 to Create and requires stopping for
any new product/reference question. The loading-placeholder ruling does not
resolve this distinct departure confirmation.

**Planner question:** approve the same accepted U1 Identity uncertain-departure
choice for Create—Check again (default focus) / Leave anyway, warning that the
event may already exist—while keeping normal Keep editing / Discard when no
unknown write exists? Or retain the reference's actions but approve truthful
uncertain-specific copy? Recommendation: the U1 pattern; no automatic resend,
no database/API/authentication change and no remembered draft.

The draft initially reproduced the reference's ordinary discard copy. Work
stopped when the conflict was confirmed. The planner's 6 October ruling in
`08-decisions.md`, “U2 item 4: leaving the Create dialog after an uncertain
create”, approved Check again / Leave anyway for unknown outcomes and retained
Keep editing / Discard for ordinary dirty input. Implemented and registered in
DELIVERY_PLAN; no automatic resend or claim that an unknown write was unsaved.

EN warning: “The event may already have been created. Check again before creating
another event. Leaving closes this dialog; it does not undo a creation.” DA:
“Eventet kan allerede være oprettet. Tjek igen, før du opretter et nyt event.
Når du forlader dialogen, lukkes den; det fortryder ikke en oprettelse.”
These truthful recovery labels implement the ruling, not user visual acceptance.

## Implementation

- Shared modal-form/template, banners/icons and trap/dirty registration; input
  backdrop does not close (A12, register row). Cancel/Escape/Back honor dirty
  state; pending saves/readbacks refuse leaving. Shared `AdminUI.busy` wraps
  every action; no page busy timers.
- Required name/50 code-point limit, near-limit counter, timezone defaults and
  supported list, local field validation/focus. Duplicate warning includes phase;
  HTML-encoded JSON embeds only DB-role-authorized names/phases, hidden included
  only for SuperAdmin. Warning, not refusal; no new read endpoint.
- GET Create redirects to `/Admin/Events?create=1`; directory opening layers the
  existing query and records one entry. Native POST payload, native invalid-input
  view, redirect/409 behavior, retired-wizard refusal and CheckAgain JSON/404/
  authorization remain. A requested modal POST negotiates JSON validation/completion/
  uncertainty with the existing handler and unchanged atomic service.
- Unknown outcomes lock values and use Check again, never resend. CheckAgain or
  same-key404 says not found/may have been removed and offers a new key with
  typed values retained (approved E-14). Shared AdminFetch owns both fetches,
  credentials/antiforgery and session notice. No raw page fetch.
- Success replaces the modal entry with native Overview (old layout retained
  until U4). Back consumes a one-time row highlight; browser history stores no
  form draft/key. TempData success is set for successful modal readback too, so
  a genuinely lost POST response does not lose the success notification.
- Shared close callback gets navigation context to avoid an extra Back when
  navigation already owns modal dismissal. Existing callback signatures continue
  working; shared shell regression passes in both engines.
- The shared layer accepts an optional `confirmLeave` callback, defaulting to its
  unchanged discard confirmation. Create supplies its approved confirmation for
  Escape even when disabling the focused button moves focus outside the panel.
  Cancel, panel Escape and browser Back/navigation use the same recovery choice.

## E-14 binding-time wording proposal (not visual acceptance)

- Reference: “It wasn’t created. You can create it now.”
- EN: “The event was not found. It may not have been created, or it may have been
  removed since. You can start a new creation request with these details.”
- DA: “Eventet blev ikke fundet. Det er måske ikke blevet oprettet, eller det kan
  være fjernet siden. Du kan starte en ny oprettelse med disse oplysninger.”

## A10 before/after

`EventCreationRetryIntegrationTests.cs`, authenticated request fixture setup:
before `GetStringAsync("/Admin/Events/Create")`, extract key/token from the old
standalone page; after GET Create, assert the explicitly authorized302 and exact
`/Admin/Events?create=1` destination, retrieve directory/modal markup and extract
the same key/token. **Every existing native POST validation, retired input,
conflict, atomic replay, CheckAgain/no-store/ownership, ordinary/anonymous/
disabled account and no-database-change assertion remains unchanged.** This
setup change is required by brief67 §2's explicit GET route choice, not a weakened
AU03 assertion. No `EventCreationUiTests` assertion was changed; its native
invalid-input fallback and authorization/minimal-creation boundaries still pass.

## Final affected checks after the ruling

- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release --no-restore -v:minimal`: **0 warnings/errors**,1.27s after the final shared-layer Escape fix. Incremental, not the final clean Release batch gate.
- `env BINGO_PARITY_ENGINES=chromium,webkit <bundled-node> scripts/check-u2-create.cjs`: **10 passed,0 failed**. Eight loaded modal parity comparisons at1440/390, both themes/engines, **0 differences**; two complete behavior runs including real button activation, Unicode/counter/validation/duplicates, A12/trap/dirty/Escape/Back, pending refusal, uncertain Check again/default focus/Leave anyway,404 retained values/fresh key, session notices, real DA switch, real atomic direct creation and committed-but-lost-response readback, Overview URL replacement and one-time Back highlight/no resubmit. **0 page errors**. Output: `artifacts/u2-create/results.json` and per-comparison JSON/PNG; ignored, synthetic data only.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~U2EventCreateHttpTests|FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~U2EventsDirectoryHttpTests' --logger 'trx;LogFileName=u2-item4-ruling-http.trx' -v:minimal`: **32/0/0**,4s. Final C# helper; actual lost-session POST/CheckAgain302/no data/no creation, JSON validation/replay/readback, native creation boundaries, query guard. Subsequent changes were JS-only.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~EventCreationRetryIntegrationTests|FullyQualifiedName~U2EventsDirectoryIntegrationTests' --logger 'trx;LogFileName=u2-item4-ruling-pg.trx' -v:minimal`: **11/0/0**,11s. AU03 seven plus directory four on owned PostgreSQL17; exact authorized duplicate population. Subsequent changes were JS-only.
- `env PLAYWRIGHT_BROWSER=<chromium|webkit> PLAYWRIGHT_CHANNEL=chromium <bundled-node> tests/Bingo.BrowserTests/admin-design-shell.browser.js`: **2 passing executions**, after the final optional layer confirmation. Focus/layers/menus, ordinary dirty and pending guards, Back/Forward, swap/failure/disposal, URL state, busy/motion/toasts remain passing.
- Shared language regression in both engines: **2 passing executions**, after navigation-close integration; background POST/swap, retained focus/sidebar/scroll and pending/ordinary dirty behavior unchanged. The later optional callback defaults to unchanged behavior for these callers.
- `git diff --check`: exit0. `git diff --name-only 8355680a4eee6a74ae905c5c69a8f50c5f021dcf --` shipped token/component CSS, source token/component CSS, frozen Dashboard/Events HTML: exit0/no output. No frozen CSS/reference edits.
- Full JS runner, whole BrowserTests, final clean Release and Claude's final-SHA whole .NET gate remain **pending**, not claimed passing.

## Earlier checkpoints (superseded, not final failures)

- Existing AU03 PostgreSQL: `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~EventCreationRetryIntegrationTests --logger 'trx;LogFileName=u2-item4-au03.trx' -v:minimal`: **7/0/0**,8s. Before final readback-TempData helper; underlying service unchanged.
- Focused HTTP: `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~U2EventCreateHttpTests|FullyQualifiedName~EventCreationUiTests' --logger 'trx;LogFileName=u2-item4-http-final.trx' -v:minimal`: **31/0/0**,2s. Actual disabled-session POST and CheckAgain302/login/accessChanged/no data/no creation, modal JSON validation/one-key replay/readback and protected native checks. Before final readback-TempData helper; rerun after ruling.
- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release --no-restore -v:minimal` on preserved source: **0 warnings/errors**,7.16s. Earlier CA1859 required the concrete JsonResult helper return type; corrected. Earlier solution builds also0 warnings/errors; these are incremental, **not final clean Release gate**.
- `node scripts/check-u2-create.cjs`: draft executions, **not a full pass**.
  Exact modal positions/text/icons/styles at1440/390, light/dark: **0 differences
  in both engines**. Chromium earlier candidate completed required/counter/
  duplicate, A12/keyboard/dirty/history, pending guard, unknown404/fresh key,
  session notices, real culture switch, real direct creation and lost-response
  readback, Overview replacement and one-time Back/no resubmit.
- WebKit draft parity passed; behavior was incomplete. Recorded events showed
  its first failed Unicode test had input/change but **no button click activation**,
  not evidence of a validator result. Settled Enter exercises code-point validation;
  the latest run progressed then timed out waiting for the held write's busy state.
  Corrected the redundant name blur/change repaint and avoid replacing unchanged
  submit text during pointer activation. Both final real-button behavior runs pass.
- Chromium after ruling initially reached the generic discard dialog on Escape
  when focus left a disabled submit button. Added the scoped optional layer
  confirmation callback above; full Create10/0 and shared shell2 pass afterward.
- Playwright does not intercept the redirect's next network hop (established U1
  `admin-design-fetch.browser.js` evidence). A302 stub reached the real signed-in
  Login handler, which redirected away; no modal notice was produced. The draft
  browser fixture now uses the helper's equivalent navigation-header classification
  for deterministic retained-value notices. **Actual302 is proven by HTTP above**;
  shared helper's followed-response classification remains in its existing runner.
- New HTTP setup initially expected a relative login Location, but middleware uses
  an absolute URI. Corrected to assert the exact AbsolutePath and retained query;
  the first29/2/0 and nullable compile checkpoint are superseded by31/0/0.
- Shared shell browser tests Chromium/WebKit: **2 passing executions** after
  navigation-close callback integration. Full JS runner/whole BrowserTests,
  final page rechecks and Claude final-SHA whole .NET gate **not run for item4**.
- `git diff --check`: exit0. Frozen token/component `cmp`: exit0/no output.
  No frozen CSS/HTML changes. Item3 gates remain in its committed evidence.

## Shared checks and changed files

1. Frozen tokens/components unchanged (cmp above).
2. No Create page CSS added; modal/field/banner/spin/counter/flash styles are shared.
3. Sole new modal inline style: reference shared primary minimum width132px,
   matching the existing shared save control. No colour/font/spacing inline additions.
4. Shared layer, modal-form classes, confirmation and banner/icon partials used.
5. Page script scan: `aria-busy` sets semantic pending state; `.spin` toggles the
   shared component child. **No setTimeout/setInterval**; `AdminUI.busy` owns timing.
6. Shared dirty/fetch/swap lifecycle used; listeners/request AbortController and
   draft registration are disposed. A16 final rechecks remain.

Repository-relative item4 files:

```
CURRENT_STATUS.md
DELIVERY_PLAN.md
UI_PAGE_MATRIX.md
docs/references/admin-ui/reviews/2026-10-06/u2/item3-events-directory.md (stale next-action correction)
docs/references/admin-ui/reviews/2026-10-06/u2/item4-create-modal.md
scripts/check-u2-create.cjs
src/Bingo.Web/Pages/Admin/Events/Create.cshtml.cs
src/Bingo.Web/Pages/Admin/Events/Index.cshtml
src/Bingo.Web/Pages/Admin/Events/Index.cshtml.cs
src/Bingo.Web/Pages/Shared/_AdminEventCreateTemplate.cshtml
src/Bingo.Web/Resources/AdminCommunityResource.da.resx
src/Bingo.Web/wwwroot/js/admin-design-shell.js
src/Bingo.Web/wwwroot/js/admin-events.js
src/Bingo.Web/wwwroot/js/admin-event-create.js
tests/Bingo.BrowserTests/U2EventCreateHttpTests.cs
tests/Bingo.BrowserTests/admin-design-create.browser.js
tests/Bingo.IntegrationTests/EventCreationRetryIntegrationTests.cs
```

## Acceptance / next action

Dashboard and Events directory remain **awaiting Claude review, then user visual
acceptance**. No self-review, independent pass or visual acceptance claimed.
The uncertain-departure question is resolved, registered and checked. Commit
item4 once, then proceed to item5 only within its protected UR/lifecycle scope;
raise any consequential authority conflict before altering the accepted scenario
invariants. Batch gates follow completed item5. Do not restart baseline discovery.
No U3+, laneT, packaging, push, merge or deployment.
