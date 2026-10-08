# U3 item3 — first working binding

7 October2026. Checkout: `/Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage`, branch `codex/participants-functionality`. Item2 base `856d421c1c2bc15688abb2dec19f6b8f620fe38a`. Item3 first working binding completed. Explicit user U3-Q11(a) resolves the imported-history boundary below. Subsequent authorized route: separate T1 merge, Accounts/Audit registration and named checks, review refresh, then user-look stop. No item4, independent review or manual acceptance claimed.

## Implemented and checked

Reference Settings/Form tabs, per-card immutable saves/draft isolation, promotion preview, secret-safe code recovery, full stable-ID form readback, exact add receipt replay, custom drawer, dirty rename, answer counts, counted reconfirmation and session-loss draft preservation. Questions redirects to the form tab; old overlay and Participants-owned settings are retired. Shared shell/navigation and blocker links now reach Signup setup. Existing terminal POST refusals and test14 remain unchanged. No migrations or laneT edits.

Q9(a)/Q10 summary resolution is implemented exactly: “Dates are on Schedule; opening and closing signups on Overview.” Both links remain. Danish: “Datoer findes under Tidsplan; åbning og lukning af tilmelding under Oversigt.” At390/494/860/1280/1440, English wraps2/1/1/1/1 lines and Danish2/2/1/1/1 in both engines. Shared loading reservation remains2/2/1/1/1; no exception. Measured line height19.575px, rendered one line19.5625px and two39.125px (WebKit subpixel difference under0.016px). Exact values in `signup-item3-checks.json`.

Register: DELIVERY_PLAN table “Non-reference functionality awaiting page binding” rows for U3-Q9/Q10 summary (difference from reference:127,:1551) and C-CMP-2/AU05/AU06 transports. Existing A-SignupSetup-1 cap row retained. UI_PAGE_MATRIX retires Questions and marks Signup setup awaiting manual acceptance. RC02R4 fixes the two reference “signup order” phrases to “waiting-list order”.

## Resolved decision: U3-Q11(a), imported archived events have no signup form

Live review discovered HTTP500 for the imported synthetic history, matching a real production shape. `SignupSetup.cshtml.cs:536` uses `SignupForms.SingleAsync`. `HistoricalImport/HistoricalEventImporter.cs:269` creates archived history and does not insert a SignupForm or questions. `UiReviewScenarioSeeder.cs:339` intentionally mirrors that shape. This is not a broken fixture to seed over. Normal archived and cancelled events have forms and passed the live URL checks.

D17 requires read-only access. The reference always has standard form fields (`SignupSetup.dc.html:519-520`) and does not define an absent historical form. Silently inventing a form or showing standard questions as recorded facts would change historical meaning. No history, importer or schema has been changed.

Options:

1. **Recommended:** keep the read-only Settings tab showing stored settings; show an explicit form-unavailable state on Signup form: “No signup form was recorded for this imported event.” Current should represent absence explicitly (for example `hasForm:false`, nullable form version, no invented definitions). Do not create or mutate historical data. Record this reference difference. Add one focused PostgreSQL case for page/Current and preserved no-write/terminal refusal, then rerun the affected live links only.
2. Make Signup setup unavailable for imported history and direct to Overview. Requires a D17 exception and a navigation/reference decision.

User7 October2026 approved option1 as U3-Q11(a). Implemented stored Settings read-only and the exact form message, with Danish “Der blev ikke registreret en tilmeldingsformular for denne importerede event.” Current now returns `hasForm:false`, `formVersion:null`, empty questions and `editable:false`. No form, questions or audit rows created. No D17 exception. Register row added beside Q9/Q10. Focused PostgreSQL HTTP test `ImportedArchivedWithoutFormIsReadOnlyAndExplicitWithoutCreatingHistory` passed1/1, including GET200, explicit Current absence, stored cap, disabled editing and D16 POST refusal with no writes. Debug compilation passed. Other107 PG,30 source and both-engine evidence reused; final live verification follows the authorized T1 merge.

## Scoped evidence

Compact durable results, log SHA256s and result excerpts, both-engine measurements and live URL outcomes: `signup-item3-checks.json`. Screenshots beside this file: `signup-settings-390.png`, `signup-settings-1280.png`, `signup-form-1280.png`, `signup-drawer-1280.png`. All data synthetic.

- Debug fixture build and owned review refresh build:0 warnings/errors.
- PostgreSQL:107 distinct scoped cases resolved passing (initial main run90pass/11failed baseline assertions, then named11/11 correction; form run4pass/2failed retired-markup assertions, then final named2/2 correction). Intermediate delete correction still failed inactive-label HTML assertions; final Current inactive-ID assertion retained real deletion effects and passed. No blanket claim that initial commands passed.
- Razor/source tests:30 resolved passing (initial29pass/1 retired-overlay assertion, named correction1/1).
- New Signup setup browser:11 groups each Chromium/WebKit passed, real PostgreSQL operations including uncertainty/replay/changed impact/session loss.
- Generic conformance:5 widths each Chromium/WebKit passed; source registrations/shared components/no-fade, loading/body/frame geometry, Danish and update checks.
- Retained `participants-ui.test.js`, `participant-edit-dialog.test.js` passed; `admin-confirmation-navigation.browser.js`3 groups each engine passed. Retired overlay/code scripts now check retirement; their old UI behavior is covered by the new Signup setup browser script.
- Native invalid add in live review preserved label/type in reopened drawer; Current unchanged after rejected POST.
- Live review:42 authenticated Schedule/Signup setup links200. Imported Signup setup500 unresolved. Discarded Signup setup404 is correct access behavior but its new catalogue entry was erroneous; source filter now excludes Discarded like existing catalogue. That source-only catalogue fix was made after capture; current generated guide still contains the stale discarded link until next refresh.
- `git diff --check` passed. No Release/fullJS/whole.NET run. No manual acceptance.

Commands used for executable scope (`NODE` means the bundled absolute Node runtime):

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~SignupSetupVersionIntegrationTests|FullyQualifiedName~SignupCodeValidationIntegrationTests|FullyQualifiedName~SignupQuestionCreationRetryIntegrationTests|FullyQualifiedName~AdminEventHandlerClassificationTests|FullyQualifiedName~TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect|FullyQualifiedName~SignupSettingsHandlerPromotesWaitingParticipantOnlyAfterCapacityIncrease|FullyQualifiedName~AuditAtomicityBatchIntegrationTests|FullyQualifiedName~RenderedSignupAllowsEmptyOptionalAccountsAndEnforcesCodeBeforePersisting'
# Named corrections, same test project/options:
--filter 'FullyQualifiedName~AuthenticatedCodePostEnforcesLengthAndPreservesRetainClearAndRequiredSemantics|FullyQualifiedName~SubmittedBaselineRejectsStaleMissingAndMalformedBeforeAnyMutation|FullyQualifiedName~ConcurrentHttpAddsSerializeAndRejectLoserWithoutExtraDefinitionOrAudit|FullyQualifiedName~FirstResponseStillNormalizesAddsAndLocksExistingShape|FullyQualifiedName~ActualHttpFormsBindIdentityBaselineAndEnforceAuthenticationAntiforgery'
# Retained form scope, same project/options:
--filter 'FullyQualifiedName~AdminQuestionsRouteAddsOneCustomQuestionAndPreservesInvalidInput|FullyQualifiedName~DeleteQuestionRemovesAnswersAndReservationsWithoutResurrectingAccounts|FullyQualifiedName~QuestionsEditAfterFirstResponseChangesOnlyPresentationAndRejectsCraftedStructure|FullyQualifiedName~QuestionsEditInactiveRetainedQuestionDoesNotMutate|FullyQualifiedName~QuestionsReorderSkipsSeparatedAccountRowsAndPreservesTheirIdentity'
# Final named deletion correction:
--filter 'FullyQualifiedName~DeleteQuestionRemovesAnswersAndReservationsWithoutResurrectingAccounts'
dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --no-restore -c Debug --filter 'FullyQualifiedName~EventCreationUiTests|FullyQualifiedName~AdminShellUiTests'
# Named source correction, same project/options:
--filter 'FullyQualifiedName~ParticipantActionsKeepRouteFallbackAndShareDesktopDialogContract'
BINGO_PARITY_CONFIGURATION=Debug PLAYWRIGHT_BROWSER=<chromium|webkit> NODE tests/Bingo.BrowserTests/admin-design-signup-setup.browser.js
BINGO_PARITY_CONFIGURATION=Debug BINGO_CONFORMANCE_PAGES=signupsetup PLAYWRIGHT_BROWSER=<chromium|webkit> NODE scripts/check-admin-page-conformance.cjs
PLAYWRIGHT_BROWSER=<chromium|webkit> NODE tests/Bingo.BrowserTests/admin-confirmation-navigation.browser.js
NODE tests/Bingo.BrowserTests/participants-ui.test.js
NODE tests/Bingo.BrowserTests/participant-edit-dialog.test.js
BINGO_UI_REVIEW_CONFIGURATION=Debug python3 scripts/ui-review.py refresh
NODE /tmp/u3-signup-review-check.cjs
```

## Live review links

Owned environment remains running; preserved `artifacts/ui-review/owner.json.stale-20261007` remains alongside valid current owner. Refreshed IDs replace earlier Schedule links. Sign in as synthetic ReviewAdmin (local-only password in `artifacts/ui-review/scenarios.md`).

- [Private setup Settings](http://127.0.0.1:5310/Admin/Events/SignupSetup/0a9f0a72-907d-421b-aab4-41c58a2ac7d3)
- [Private setup form](http://127.0.0.1:5310/Admin/Events/SignupSetup/0a9f0a72-907d-421b-aab4-41c58a2ac7d3?tab=form)
- [Open signups form](http://127.0.0.1:5310/Admin/Events/SignupSetup/70942055-08bd-4fb0-80c0-d3e75d396165?tab=form)
- [Closed signups form](http://127.0.0.1:5310/Admin/Events/SignupSetup/ffaeb4b3-1d89-46e7-8a62-015e40c3a06e?tab=form)
- [Live read-only form](http://127.0.0.1:5310/Admin/Events/SignupSetup/4fe06a21-3202-496e-9f15-2bbbfb5613fe?tab=form)
- [Archived read-only form](http://127.0.0.1:5310/Admin/Events/SignupSetup/472d3dae-1a62-4130-b528-c1860a194832?tab=form)
- [Cancelled read-only form](http://127.0.0.1:5310/Admin/Events/SignupSetup/39849e08-09d9-4329-aa75-c00a44e064f1?tab=form)
- [Imported history — known500](http://127.0.0.1:5310/Admin/Events/SignupSetup/83c614fb-7e9e-4d00-9831-aa1a25d2ded0?tab=form)
- [Private setup Schedule](http://127.0.0.1:5310/Admin/Events/Schedule/0a9f0a72-907d-421b-aab4-41c58a2ac7d3)
- [Frozen Signup setup reference](http://127.0.0.1:5320/SignupSetup.dc.html)
