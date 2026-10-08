# Round 3 item 7 — real rendered reference and browser gates

Authority: brief55 item7, planner rulings57/58, and the direct user mobile-placement decision recorded in UI_SYSTEM and DELIVERY_PLAN. Earlier failures and elapsed-work accountability remain in item7-checkpoint.md. This is implementer QA, not independent review or manual acceptance.

## Implementation and controlled environment

The standalone `tests/AdminDesignParityFixture` launches the real Razor application on an ephemeral Kestrel port against its own PostgreSQL17 Testcontainers database. It seeds synthetic accounts/events only, keeps the fixed2027 clock for rendered dates, rejects WOM calls, and cleans up its owned container. It never reads the reviewer's credentials or uses the user's database/running app. Ruling57 sets only the fixture cookie options to System time because production Login stamps expiry from wall clock; Login.cshtml.cs:74 and ChangePasswordModel.CreatePasswordSessionProperties are observations, not changes in this round. Ruling58-1 waits for an actual database connection/SELECT1 before migration (bounded60s,500ms interval, every attempt logged). The first changed-condition run succeeded in10.8s; no user container was touched.

`scripts/check-admin-design-parity.cjs` serves the frozen reference locally, exposes its existing component solely to select states, and renders both surfaces with identical vendored font bytes. No frozen reference file changes. It compares rendered text, SVG geometry, element typography/spacing/colour and selected dimensions; captures app/reference PNGs plus exact JSON differences; traverses actual markup and nested template contents to reject undefined CSS classes. It waits for finite CSS transitions to settle rather than sampling intermediate colours. Motion remains separately covered by shell tests. `BINGO_PARITY_SCENARIOS` supports focused development runs; default CI runs every scenario in both engines.

Actual served layout proof requires all four fingerprinted script URLs, switcher navigation retaining a document marker, a real full-load old page followed by Back to a usable Identity, and the persisted-pageshow recovery path. The latter explicitly dispatches a persisted PageTransitionEvent to exercise recovery deterministically; this does not claim that the browser actually chose bfcache for a no-store response. Phone tests exercise the real language POST, retained document/sidebar, dirty Keep editing/Discard, keyboard controls, Escape unlock and the861px return to desktop placement.

## Exact changes and expectation authority

- `admin-design-fetch.browser.js`: Chromium's exact intercepted `FixtureSession=controlled` assertion is unchanged. Only WebKit uses58-2's real Kestrel proof: call the production AdminFetch helper with caller credentials=omit; require authenticated Current handler200 and exact seeded identity values, then clear cookies and require session-lost/Login. This fails if the helper stops enforcing same-origin credentials; it is not an unrelated successful login. Other header/outcome assertions remain.
- `identity-timezone-confirmation.browser.js`:58-3's optional diagnostic records URL/history length/state/popstate/layer DOM and shell index transitions. It proved a real race: a new push during a preceding pop fetch used a stale internal index and created a hole. Production now derives the next index from the current browser history entry. Back calls, dialog assertions and30s waits are unchanged; no waitUntil workaround was used.
- Same file and `fixtures/identity.cjs`: old separate `Also saved: Event name.` → exact inline ` Your other changes (event name) are saved at the same time.` under58-4/U-F/brief55. Full modal text and the exact changed-field set are still asserted; no extra unchanged fields, no omission when changed, no reason field, newline/frozen basis/pending/onePOST/no retry protections changed. Danish exact sentence added to AdminDesignLocalizationTests.
- Theme, binding and readback tests gain engine selection only. Runner executes43 files once plus8 shell/Identity files again in WebKit (51 executions). CI installs WebKit, builds the controlled fixture, runs both required batches and uploads reports; CI itself was not executed here.
- Actual rendered timezone rows use the reference Intl formatter per engine (WebKit English uses “at”, Chromium a comma). The server supplies the immutable instant and zone as data attributes, retaining the original bare date text node and offset span for HTTP compatibility. No handler value, saved timestamp or server assertion changed.
- Actual server validation classes are styled; the unused `validation-summary` class was removed from the shared-design banner; reference `fade-in` is retained. Frozen tokens/components remain unchanged.
- V14 initially still failed: phone crumb13.23px vs reference108.13px. The user directly decided to move both theme and language into the hamburger only at mobile widths. Exactly one live controls wrapper moves at the existing860px breakpoint; desktop/tablet outside it stay unchanged and bell remains in topbar. Language swaps retain an open drawer so restored focus is reachable. Reference control styles are compared while the drawer is open. Phone crumb assertion changes from exact reference width to **at least** reference width under that explicit placement exception; actual118.78px in both engines. All other phone element comparisons remain. This is a user decision, not planner approval.

## Gate results and limits

Final candidate rendered parity: **66 passed /0 failed**, Chromium and WebKit, including CSS class inventory **0 undefined**. Reports and paired images are in `rendered/`; findings map in `visual-findings.md`. The same checker is also run against the preserved49ae49c archive; `baseline/` retains its negative report/screenshots. A failing baseline suite demonstrates regression sensitivity; individual checks that cannot reach later states on broken navigation are reported as errors, not invented assertion failures. The V-table distinguishes shared behavior checks from static images.

Final clean nonincremental Release: **0 warnings /0 errors**,25.90s. Fixture build:0/0. Focused localization:2 passed/0 failed/0 skipped. Earlier focused real PostgreSQL shell/Identity run:35 passed/1 failed/0 skipped, sole failure a bare-date HTTP markup assertion after an intermediate wrapper; production was corrected to preserve the bare node, then that exact test passed1/0/0. These are separate runs, not an uninterrupted36-test pass. All17 protected IdentityFieldConflict tests passed in the original35; test14 and their assertions are unchanged. Earlier item0–6 passing evidence remains applicable.

Final full JS: **51 passed / 0 failed / 51 total**, exit 0. Same final checker against baseline49ae49c: **0 passed / 60 failed**, exit 1; final candidate: **66 passed / 0 failed**, exit 0. Counts differ because broken baseline scenarios stop at their first unreachable-state/error assertion while candidate scenarios reach all subsequent comparisons. Raw results, not normalized counts, are retained in baseline/results.json and rendered/results.json. Diff check and both frozen CSS byte comparisons passed. Whole .NET final-SHA gate remains **pending user/Claude execution**, zero failed/zero skipped required. CI unrun; Claude fresh rendered recheck and user EN/DA/light/dark/phone/Safari acceptance pending. Round2 Claude2031/0/0 is historical baseline evidence only.

## Reproduce

From the assigned checkout, after restore/browser installation:

```sh
dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release
pnpm test:js
pnpm test:parity
```

For baseline reproduction extract `git archive 49ae49c` into a disposable directory, copy only the current standalone fixture project/source into it, build that fixture, then run the current parity checker with `BINGO_PARITY_ROOT` pointing to that archive. Do not replace baseline production sources. Preserved archive SHA256: `19584d1ea86a840700e9d0c717ae6640c36e8d62a2562b76488b12e80eccc5a1`. Local archive `/private/tmp/bingo-u1-r3-baseline-49ae49c.tar`; controlled extraction `/private/tmp/bingo-u1-r3-baseline-ri9ie73i`.

Required final user/Claude command (unfiltered):

```sh
cd /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
dotnet test Bingo.slnx --configuration Release --no-restore --results-directory /private/tmp/bingo-u1-final-suite-trx --logger "trx"
```

Stop after this item7 commit. Next owner is Claude for the named fresh rendered recheck, then the user for visual acceptance including Safari. No UR/U2+/laneT or publication is authorized here.


The committed `checks/` directory preserves final build/JS logs, focused PostgreSQL/localization results and the58-3 history diagnosis. `rendered/` includes both engines' complete successful screenshot/JSON pairs; `baseline/` includes the same checker's negative before evidence. The earlier54-pass comparator lacked the V14 width assertion and therefore was not a complete pass; the subsequent60-pass/2-fail width run correctly exposed it before the user decision. Those development paths remain recorded in the checkpoint and no final claim relies on the incomplete pass.
