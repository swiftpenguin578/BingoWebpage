# U3 batch gate — 7 October 2026

Tested implementation commit `5ef3ab3ad4e6c23fe9a0e6c8c3bc25404c89ca11`. Release solution build ran once: 0 warnings/errors, 18.83 s. Full JS runner ran once, including both engines: **97 passed / 5 failed / 102 total**. Existing compiled Debug parity fixture reused; no whole .NET suite. The reused stale-account HTML predates T1 and is incompatible with the current drawer replay, so that check is unpassed (fixture failure, not an established product defect). [Per-file results](js-results.json).

The five separate user-ruling commits are d7e40d5d, 5143515e, 20bab650, f166a04e, 5ef3ab3a. Their scoped checks and current parity are in [follow-up evidence](../followup/parity-addendum.md). Earlier form/drawer checklist remains [here](../second-look/signup-parity-checklist.md), with that addendum superseding named icon/date/count rows.

Known scoped T conformance failures: Accounts count placeholders: 0 versus 2; Audit at 390 px: summary 43.125 px versus 39.15 px. No T remediation or bypass. Accounts/Audit interaction tests pass independently. User visual acceptance of T1 remains recorded; Signup acceptance not inferred.

- FAIL `admin-design-events.browser.js` [chromium], exit 1, no signal: [admin-design-events.browser.js.chromium.log](admin-design-events.browser.js.chromium.log).
- FAIL `admin-design-events.browser.js` [webkit], exit 1, no signal: [admin-design-events.browser.js.webkit.log](admin-design-events.browser.js.webkit.log).
- FAIL `admin-design-page-conformance.browser.js` [chromium], exit 1, no signal: [admin-design-page-conformance.browser.js.chromium.log](admin-design-page-conformance.browser.js.chromium.log).
- FAIL `admin-design-page-conformance.browser.js` [webkit], exit 1, no signal: [admin-design-page-conformance.browser.js.webkit.log](admin-design-page-conformance.browser.js.webkit.log).
- FAIL `admin-stale-change.browser.js` [default], exit 1, no signal: [admin-stale-change.browser.js.default.log](admin-stale-change.browser.js.default.log).

After the single run, the named Events assertion correction was committed separately as `12d9d6c5`: focused Events rerun **28 passed / 0 failed**, both engines. [Correction and evidence](../followup/events-assertion-fix.md). This resolves the two stale Events assertions without repeating or relabeling the original batch run. Remaining unpassed checks: full conformance in both engines (Accounts failure), separately established Audit conformance failure, and stale-account replay requiring fresh T1 HTML from its PostgreSQL fixture. Recommend lane T owns the two page failures; planner regenerates stale HTML with AdminStaleChangeIntegrationTests and reruns only its replay. No page-code bypass, fixture invention or additional .NET run performed.

Review environment: 61 URLs verified 200/no unexpected redirect; both engines verify normal Signup form/drawer and Q11 EN/DA absent historical form/read-only Current. Synthetic ReviewAdmin / ReviewOnly!1234.

- [Private setup [Draft] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/c1b03bc4-bbbd-4c58-a3cc-115fe9b358ad?tab=form)
- [Imported — frozen synthetic history [Archived] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/9c3560a7-e04a-4b6d-b307-3d9f5dadc0de?tab=form)
- [Live — published board correction [Live] — signup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/9dbd1dcb-40f9-4246-9852-9e732870e930?tab=form)

Current complete scenarios: `artifacts/ui-review/scenarios.md`. The known handover removed the container; verified missing/healthy daemon, preserved second marker `owner.json.stale-20261007-2` alongside the first, recreated via normal ownership workflow. Environment remains active for planner-coordinated handover only.

No push, deployment, new page family or automatic T2 handover. Stop for planner/user disposition of gate failures and any visual findings. No further full runner/build repetition without authorization.

Executed commands (Node from the configured Codex runtime):

- `dotnet build Bingo.slnx --configuration Release --no-restore`
- `BINGO_PARITY_CONFIGURATION=Debug BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY="$PWD/artifacts/js-fixtures" node scripts/run-browser-tests.cjs`
- Focused correction: `BINGO_PARITY_CONFIGURATION=Debug BINGO_PARITY_ENGINES=chromium,webkit node scripts/check-u2-events.cjs`
- `cmp docs/references/admin-ui/ui/components.css src/Bingo.Web/wwwroot/css/admin-design-components.css`
- `git diff --check`

The Release solution build excludes the separately hosted parity fixture; the runner reused its current compiled Debug output. Durable logs/results above distinguish actual executed checks from the planner-owned .NET gate. Register rows for the current UI changes remain in DELIVERY_PLAN.md (follow-up items 4–5 and the earlier D/E/F decisions); no new product difference was introduced by the assertion correction.
