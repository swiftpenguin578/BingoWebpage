# U3 post-batch remediation — 7 October 2026

All requested scoped checks pass. Three separate items: approved T merge `3cbf654ef8b87b4312cdddec8357590fecc1a028`; fixture/conformance evidence `96a402b4`; unknown-timezone/navigation fix is the commit containing this report. The original batch checkpoint `ea336943` and all previous evidence are preserved, with no repeated full JS or whole .NET run. [Exact results](checks.json); [items 1–2](item2.md).

Schedule retains an unsupported stored timezone ID and reports `displayTimezone: UTC` separately. Its dates, actual-transition notes and derived cutoff display in UTC with an explanatory note and shell link to Identity. It remains read-only until the stored timezone is supported; the existing server rejection of invalid timezone saves remains unchanged. No stored instant, version, history or authorization rule changes. Signup setup applies the same explicit note/fallback to its server-rendered response timestamp; supported-zone presentation stays unchanged. A bounded import search found Schedule is the only production caller of admin-date-time.js; its conversion rules still reject invalid zones and DST gaps/folds.

The shell now cleans incomplete modules, registered drafts and pending layers when initialization throws, shows a visible page failure with retry, and leaves navigation usable. It does not label this as a connection failure or hard-reload a failed in-page initialization. Tests deliberately throw from initialization after opening a pending drawer/registering a throwing draft, and from disposal; initial load, page swap, retry, sidebar, breadcrumb and switcher recovery pass.

Checks on final production changes:

- Release solution build: 0 warnings/errors. Separate Release parity host rebuilt before browser runs: 0 warnings/errors. The first fixture build used a nonexistent method name; corrected to the existing RecordAcceptedResponse. Synthetic failure-page encoding was corrected with explicit UTF-8 metadata for WebKit. Final runs pass.
- Exact requested PostgreSQL test: 8 passed / 0 failed / 0 skipped; regenerated the eight files in artifacts/js-fixtures actually consumed by the runner. Four stale replay action groups pass again after shell changes. Raw fixtures are ignored, with hashes committed in item 2.
- All seven registered pages: 45 conformance cases per engine at 390/494/860/1280/1440, including Dashboard variants; frame/style, no-fade, document, update and Danish checks pass. Accounts/Audit failures are resolved, not bypassed.
- Schedule: 10 groups per engine. Signup setup: 15 groups per engine. Unknown-timezone regression: English/Danish in both engines, retained ID/version/instants, explicit UTC, no toast, working navigation. Shell suite with new failure cases: both engines pass.
- Shared CSS cmp and git diff --check pass. No schema migration, T page remediation outside the approved merge, acceptance inference, push or deployment.

Commands: `dotnet build Bingo.slnx --configuration Release --no-restore`; `dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj -c Release --no-restore`; exact item 2 PostgreSQL command is in item2.md. Browser commands use the configured Node runtime, `BINGO_PARITY_CONFIGURATION=Release`, and `PLAYWRIGHT_BROWSER=chromium`/`webkit` for `scripts/check-admin-page-conformance.cjs`, `admin-design-schedule.browser.js`, `admin-design-signup-setup.browser.js`, `admin-design-unknown-timezone.browser.js` and `admin-design-shell.browser.js`. Stale replay uses `BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY="$PWD/artifacts/js-fixtures"` and `admin-stale-change.browser.js`.

Parity addendum: Identity.cshtml:142 is the approved fallback precedent. Schedule/Signup reference dates and fields remain, with the registered explicit UTC note/link for unsupported IDs. Shared initialization failure uses the existing failure card and recovery button, with accurate new English/Danish text. DELIVERY_PLAN register row cites decisions08 “Bug, Schedule on an unknown-timezone event”. Prior complete form/drawer parity checklist and followup/parity-addendum.md remain applicable.

Review environment refreshed through the existing owned workflow (Release). Both stale markers preserved; live container ownership checks passed. [Verified links and browser results](live-links.json). Synthetic login: ReviewAdmin / ReviewOnly!1234.

- [schedule](http://127.0.0.1:5310/Admin/Events/Schedule/f860cc45-512f-43fd-9505-c0ee3d6cb8ca)
- [signupsetup](http://127.0.0.1:5310/Admin/Events/SignupSetup/f860cc45-512f-43fd-9505-c0ee3d6cb8ca?tab=form)
- [signupsetup-settings](http://127.0.0.1:5310/Admin/Events/SignupSetup/82a9fb53-6439-4006-bef2-5586569ea405?tab=settings)
- [signupsetup-form](http://127.0.0.1:5310/Admin/Events/SignupSetup/82a9fb53-6439-4006-bef2-5586569ea405?tab=form)

Remaining failures: none in the requested scoped checks. Stop for planner review and user visual acceptance; Signup setup is not yet accepted. Planner owns the whole .NET gate and any T2 environment handover. No further automatic implementation or broad-gate repetition.
