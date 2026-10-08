# U2 round 5 item 3 — contain absolute content in the shell scroller

Clean continuation from item 2 `96e0b53bc7b888d4745fd8eacda19318c45d52ce`,
assigned `codex/participants-functionality` checkout. User/planner's explicit
item 3 in `08-decisions.md`, “U2 remediation round 4 recheck”.

Only production change: `main.scroller{position:relative}` in the existing
`admin-design-layout.css` application layout adapter. This gives unanchored
absolute descendants (notably Events row `.sr` text) a containing block inside
the shell scroll area. Frozen tokens/components, page CSS and markup unchanged.
No clipping/overflow suppression, dimensions or accessibility text changed.

## Rendered regression proof

`admin-design-document-scroll.browser.js` uses the existing owned PostgreSQL /
authenticated Razor fixture. Both engines run the real Events, Dashboard and
Identity pages at **390, 494, 860 and 1280 × 342 px**. Events also uses the real
phase menu to select Live and then All phases, checking the updated rows after
each committed filter change. The fixture must contain wide-row `.sr` text on
initial load and reset; Live must return its expected single row.

Every case asserts exact `document.documentElement.scrollWidth/scrollHeight`
equal to the configured viewport, no tolerance. It also scrolls `main.scroller`
and attempts `window.scrollTo(9999,9999)`: the document must stay viewport-sized
at `(0,0)`, while overflowing content must still scroll inside main. Fonts and
finite animations settle using the existing native-frame helper, with no new
sleep, retry, timeout enlargement or relaxed assertion.

- Before the fix: **both engines fail** Events494×342, measuring document
  **730×1577** in the controlled fixture. This reproduces the planner's cause;
  fixture content differs from the user environment's recorded729×1785.
  Durable before measurements: [Chromium](item3-before-chromium.json),
  [WebKit](item3-before-webkit.json).
- After the fix: **20 passed /0 failed per engine**, including12 full-page
  combinations plus8 Events filter/reset combinations. Exact document/container
  scroll metrics and screen-reader bounds: [Chromium](item3-measurements-chromium.json),
  [WebKit](item3-measurements-webkit.json).
- The first post-fix test attempt passed Events initial/Live bounds in both
  engines, then stopped on a wrong test selector (`All` rather than the existing
  `All phases` label). Corrected only that selector, then the entire matrix
  passed. No production workaround or timeout change.

## Commands and gates

Run from `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`.
Node: `/Users/christopher/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node`.

- `PLAYWRIGHT_BROWSER=chromium <node> tests/Bingo.BrowserTests/admin-design-document-scroll.browser.js`: exit0, **20 passed /0 failed**.
- `PLAYWRIGHT_BROWSER=webkit <node> tests/Bingo.BrowserTests/admin-design-document-scroll.browser.js`: exit0, **20 passed /0 failed**.
- `dotnet build Bingo.slnx -c Release --no-restore -v:minimal`: exit0, **0 warnings /0 errors**,38.42s.
- `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj -c Release --no-restore -v:minimal`: exit0, **0 warnings /0 errors**,2.49s; refreshed asset manifest.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-build --no-restore --filter 'FullyQualifiedName~AdminShellUiTests|FullyQualifiedName~AdminDesignLocalizationTests|FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~U2EventsDirectoryHttpTests' --logger 'trx;LogFileName=u2-rem5-item3-focused-http.trx' -v:minimal`: exit0, **34 passed /0 failed /0 skipped**. TRX: `tests/Bingo.BrowserTests/TestResults/u2-rem5-item3-focused-http.trx`.
- `BINGO_PARITY_OUTPUT=artifacts/u2-rem5-item3-identity PLAYWRIGHT_CHANNEL=chromium <node> scripts/check-admin-design-parity.cjs`: exit0, **66 passed /0 failed**,33 per engine, zero differences. [Durable results](item3-identity-results.json).
- `BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY=/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/artifacts/js-fixtures <node> scripts/run-browser-tests.cjs`: **STOPPED**,driver exit143, under the user's explicit waiver below. The full runner had rerun and passed the new20-case document-scroll matrix in both engines, Dashboard9/0 and Events13/0 per engine, body/header, fetch, language and loading checks. It did not complete, and no final full-run total/pass is claimed. The last observed completion was page-family Chromium, with page-family WebKit starting.
- Existing `artifacts/js-tests/results.json` is the older item2 summary and was **not** copied or attributed to this item.

## Shared design system checks 1–6 (touched-file scope)

1. `cmp` tokens and components against `docs/references/admin-ui/ui/`: exit0,
   byte-identical. `git diff --exit-code 96e0b53` on both frozen stylesheets,
   Dashboard/Events/Identity page CSS, `docs/references/admin-ui/ui`, reference
   `*.dc.html`, Razor Pages and production JS: exit0, all unchanged.
2. Page CSS unchanged. The one layout-adapter rule contains only positioning;
   no colours/fonts/shadows/radii. This shared scroller positioning is explicitly
   approved in the planner's item3; the frozen `.scroller` rule stays untouched.
3. Markup unchanged; no inline styles introduced.
4. Existing shared component/partial use unchanged.
5. No production JS, busy handling, timers or spinners changed.
6. Shared guard, transport, loading and swap lifecycle unchanged; no page copies.

`node --check tests/Bingo.BrowserTests/admin-design-document-scroll.browser.js`
and `git diff --check`: exit0. The change is limited to the assigned shell layout
containment; no other page family or U3 implementation started.
Whole .NET suite **NOT RUN**; planner owns its gate. Independent recheck and
manual acceptance not claimed. User app/database untouched; synthetic fixtures
only. Stop after item3, no push/merge/new branch/worktree/worker.

## User gate waiver and stop boundary — 7 October 2026

User: “Skip the gates and wrap up. You can note i waived them”. This overrides
remaining implementer gates for item3. The running full JS gate was stopped
immediately after identifying its unique process and verifying its cwd/group
belonged to the assigned worktree. Its driver returned143. No further test,
build or final gate reruns were performed after the waiver; completed checks
above remain distinct from waived remaining gates. Local staging/commit of
item3 remains explicitly authorized. This is not a full JS pass or an independent
review/manual-acceptance claim. Planner whole .NET suite remains unrun here.

No unresolved implementation issue. Stop after item3 and report its local SHA;
no push, merge, new branch/worktree, worker or next-page implementation.
