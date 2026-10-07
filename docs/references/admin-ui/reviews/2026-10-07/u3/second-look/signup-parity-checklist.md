# Signup setup second-look implementation parity — 7 October 2026

Scoped source/structure checklist for user remediation A. This is implementation evidence, not independent review or user visual acceptance. Reference is `docs/references/admin-ui/SignupSetup.dc.html`; app paths below are relative to Pages/Shared, Pages/Admin/Events, wwwroot/js/css or TestData in src/Bingo.Web as named. Line ranges identify the complete element/state block.

| Reference line | App location | Element/state | Result |
|---|---|---|---|
| 232–240,1387–1394 | SignupSetup.cshtml:44; admin-signup-setup.js:22 | Form card, lock banner, first-response date/full explanation | MATCH |
| 244–250,1379–1387 | admin-signup-setup.js:60–65 | Four section headings, descriptions, labelled lists, empty alt/custom copy, final section | MATCH |
| 252–257 | admin-signup-setup.js:66–80 | Rows, custom numbering, title, text/action columns | MATCH |
| 258–265 | admin-signup-setup.js:85–86 | Inline rename: accessible label, input300/autocompleteoff, Enter/Escape, Save/Cancel, pending spinner/error icon/meta; other actions hidden | MATCH (accessible name via aria-label) |
| 266–272,1337–1345 | admin-signup-setup.js:68–72 | Type/required/private/off/information-only metadata; response counts; choices/help with hidden labels | MATCH |
| 276,1341–1344 | admin-signup-setup.js:73; UiReviewScenarioSeeder.cs:AddEvent | Primary and captain always-asked lock chips/tooltips; captain and co-captain present in normal fixtures | MATCH |
| 278–279 | admin-signup-setup.js:74; _AdminDesignIcon.cshtml | Up/down SVGs, Move question up/down accessible labels, titles, edge-disabled buttons | MATCH |
| 281–285 | admin-signup-setup.js:75–78 | Rename/Edit/Turn on/Turn off/Delete styles, visibility and disabled/pending states | MATCH |
| 291 | admin-signup-setup.js:80,32 | Three Add buttons, plus icons replaced by busy spinner, shared pending guard | MATCH |
| 294 | SignupSetup.cshtml:45; admin-signup-setup.js | Polite form announcement | MATCH |
| 334–337 | admin-signup-setup.js:113,134; admin-design-shell.js:openLayer | Visible form behind scrim/dialog, modal attributes, shared focus/scroll/escape/outside guard | MATCH through accepted shared drawer |
| 338–344 | _AdminSignupSetupDrawer.cshtml:3 | Direct dr-head, eyebrow/title, shared32px close with icon and aria-label | MATCH |
| 345–349 | _AdminSignupSetupDrawer.cshtml:4–6 | Direct scrolling dr-body, banner wrapper, sec/form-fields | MATCH |
| 347,1408–1420 | _AdminSignupSetupDrawer.cshtml:5; admin-signup-setup.js:116,136–143 | Handler/refusal/uncertain/readback/session banners and recovery actions | REGISTERED DIFFERENCE: AU05/AU06 exact ID/version/replay and C-CMP-2; retained protected recovery implementation, not reference label matching |
| 350–354 | _AdminSignupSetupDrawer.cshtml:7; admin-signup-setup.js:128,144–148 | Question label/placeholder/aria-required, count threshold85%, limit300, validation text/icon/focus | MATCH |
| 356–359 | _AdminSignupSetupDrawer.cshtml:8; admin-signup-setup.js:128,144–148 | Optional help label, textarea/placeholder, count/limit1000, error icon/focus | MATCH |
| 361–370,503–506 | _AdminSignupSetupDrawer.cshtml:9–13 | Four answer cards/radios, exact titles and subtexts, group label/hint | MATCH |
| 370,1438 | admin-signup-setup.js:121 | Before/after-first-response explanatory answer-type hint | MATCH |
| 372–376 | _AdminSignupSetupDrawer.cshtml:13; admin-signup-setup.js:120 | Existing answered question: read-only answer type and lock explanation/icon | MATCH |
| 378–385 | _AdminSignupSetupDrawer.cshtml:14; admin-signup-setup.js:117–124 | Single-choice visibility, numbered input rows/accessible labels, remove close icons, one-row minimum, Enter inserts next, renumber/focus | MATCH |
| 386 | _AdminSignupSetupDrawer.cshtml:14; admin-signup-setup.js:135 | Add choice plus icon and focus | MATCH |
| 387–388,1063–1078 | _AdminSignupSetupDrawer.cshtml:14; admin-signup-setup.js:144–148 | Choice errors/icon/hint, blank filtering, duplicate invalid markers, combined4000 limit | MATCH |
| 391–395 | _AdminSignupSetupDrawer.cshtml:14; admin-signup-setup.js:122 | Read-only choices/list and lock explanation/icon after answers | MATCH |
| 399–400,1427–1429 | _AdminSignupSetupDrawer.cshtml:15; admin-signup-setup.js:126–127 | Required checkbox, selected/disabled class and all optional/required/locked/new-after-response subtexts | MATCH |
| 403 | _AdminSignupSetupDrawer.cshtml:16 | Public-list note and info icon | MATCH |
| 407–408 | _AdminSignupSetupDrawer.cshtml:19; admin-design-signupsetup.css | Direct sticky shared dr-foot, spacer/right alignment on390/1280 | MATCH |
| 409 | _AdminSignupSetupDrawer.cshtml:19; admin-signup-setup.js:131 | Dirty dot/status; hidden while saving | MATCH |
| 410 | _AdminSignupSetupDrawer.cshtml:19; admin-signup-setup.js:131,135 | Cancel, pending disable and discard guard | MATCH; terminal recovery states follow registered AU05/AU06 |
| 411,1429–1446 | _AdminSignupSetupDrawer.cshtml:19; admin-signup-setup.js:129–150 | 128px primary Add/Save/Checking/Checkagain labels, spinner, unchanged inert/title, shared busy timing | MATCH; recovery behavior REGISTERED AU05/AU06 |
| 1337–1394 | admin-signup-setup.js:22,32,62–82 | Locked event removes mutation controls, preserves readable questions/settings; off co-captain remains visible | MATCH (D16/D17) |
| 519–520 | SignupSetup.cshtml:44; SignupSetup.cshtml.cs:CurrentSnapshot | Imported archive without SignupForm: stored read-only settings, exact absent-form EN/DA message; no invented data | REGISTERED DIFFERENCE: U3-Q11(a) |
| 127,1551 | SignupSetup.cshtml:9 | Short pointer summary retains Schedule/Overview links | REGISTERED DIFFERENCE: U3-Q9/Q10; navigation attribute addressed separately in D |
| loading header | shared loading template | Two empty summary lines atphone≤640px, one wider | REGISTERED DIFFERENCE: U3-Q6 |
| row flash/load animation | shared conformance and styles | No new fade after load/update; shared loading states | REGISTERED DIFFERENCE: existing Q-H1/Q-SK decisions |

Execution: PostgreSQL scoped Signup tests51/51; browser14 interaction groups per engine (Chromium/WebKit), including structural/footer geometry at390/1280, standard fields/icons, rename action exclusion, locked presentation, real mutation/readback/replay and Live read-only. Locked drawer presentation probe changes only its controlled DOM snapshot; persisted first-response protections are tested in PostgreSQL. Generic Signup conformance5 widths per engine passed before D. Screenshots are implementation evidence, not acceptance. Existing review seeded participants do not establish FirstResponseAt; this remediation does not rewrite that historical fixture assumption or imported forms.

Protected differences retain existing register authorities AU05/AU06/AU07, RC02R3/R4, C-CMP-2 and D16/D17. No server mutation/readback/replay contracts were changed. Normal review seed now contains the standard captain/co-captain fields; confirmed-signup fixture lookup explicitly selects PrimaryRegularAccount.
