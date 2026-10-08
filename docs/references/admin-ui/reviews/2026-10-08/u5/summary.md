# U5 Participants — batch summary (item 1c part 2, item 3 batch gate)

Branch `claude/u5-participants`. Executed on the merged tree after `git merge --no-ff codex/participants-functionality`
(`4c4c048e`); no push. Status: implemented and executed; **not** independently reviewed; **not** manually accepted
(the user's look at Participants is pending). The whole .NET suite was not run (planner-owned, Q-S1).

## Commits since `68bb0eba`
- `d8eac48a` 1c part 2: Add drawer fixes found by the browser check, `admin-design-participants.browser.js`, three Node tests retired, review scenarios, `scripts/check-u5-ur.cjs`.
- `d495a2f1` parity checklist, DELIVERY_PLAN register rows, scenario screenshots.
- `22ee8142` merge of `codex/participants-functionality` (one conflict: `SharedResource.da.resx` tail, both sides kept; no duplicate keys, XML valid; `SharedResource.resx` unchanged).
- `1b03fdf2` Signup setup “See them on Participants” link carries `data-shell-link` (conformance found it once Participants was a registered page).

## Defects found by the first browser check of the Add drawer (fixed in `d8eac48a`)
1. A plain Escape on an untouched Add drawer opened the discard dialog: the shell snapshots input values at open, before the default place/payment radios are painted. Fixed with `layer.markClean()` after the first paint.
2. After a stale or lost-response Add the list re-read waited behind the shell’s discard prompt (drawer dirty), so the “We couldn’t confirm …” message never showed. The re-read now runs with the drawer’s dirty flag suspended (`rereadBehind`). The participant drawer’s lost-response-with-failed-read path no longer re-reads the list either (it would have prompted to discard the kept edits).
3. Changing a radio in the Add drawer threw `'#' is not a valid selector` (focus restore by empty id).
4. “Up to 1 playing accounts” — singular form added (EN + DA entry).
5. Review seeder: the withdrawn scenario participant was constructed already withdrawn, so `WithdrawnAt` stayed empty and the drawer showed no accounts; fixed in the seeder only (the reader is correct for real withdrawals, which set `WithdrawnAt`).

## Node tests retired (A10) — decision: same behaviour, new markup
| Retired | Why | Replaced by |
| --- | --- | --- |
| `participants-ui.test.js` (source-text asserts on old CSS/`event-manage.js` dialog hooks, `_InternalParticipantForm.cshtml`) | tested markup/CSS/script that no longer exist | `admin-design-participants.browser.js` (both engines) + conformance + shared CSS checks |
| `participant-add-dialog.test.js` (fake-DOM add dialog) | dialog replaced by the Add drawer | Add drawer flow: direct `?add=1`, search/no match, member disabled, pick, ineligible account reason, preselected primary, full-event default and capacity hint, discard guard (Keep editing / Discard), lost response (no resubmit, draft kept), success toast + Show, no history entry |
| `participant-edit-dialog.test.js` (fake-DOM edit dialog) | dialog replaced by the participant drawer | drawer URL, Back/Forward, Discord name hidden, custom answers listed, dirty guard, one Save + toast, read-back, missing participant, withdrawn read-only, old URL redirect |
Server rules, authorization, refusals and persisted-data tests were not touched.

## Checks (all executed on the merged tree unless stated)
| Check | Command | Result |
| --- | --- | --- |
| Participants flows | `admin-design-participants.browser.js` Chromium and WebKit | pass, pass |
| Review scenarios | `node scripts/check-u5-ur.cjs` (UR live, ReviewAdmin; 14 scenarios) | pass (Chromium) |
| Conformance, Participants | `BINGO_CONFORMANCE_PAGES=participants`, 390/494/860/1280/1440 | pass in Chromium and WebKit (before and after the merge) |
| Full JS runner | `node scripts/run-browser-tests.cjs` (fixtures from the stale-evidence test; parity fixture rebuilt) | 117 executions: 109 passed, 8 failed; all 8 passed on rerun of the failing names: `admin-design-page-conformance` Chromium+WebKit (real: the Signup setup link, fixed in `1b03fdf2`), `admin-design-summary-navigation` Chromium+WebKit, `admin-design-results-fill` Chromium, `admin-design-schedule` Chromium (fixture exited 134/143 before ready, Docker start-up), `admin-design-events` WebKit (login timeout), `admin-design-ur` Chromium (navigation race). **117/117 after reruns.** |
| Release build | `dotnet build Bingo.slnx -c Release` | succeeded, 0 warnings, 0 errors |
| Browser C# | `AdminDesignLocalizationTests`, `AdminShellUiTests`, `EventCreationUiTests` | 16/16 |
| Integration (one run) | C11, Slice9Pass92, ParticipantFlow, Slice2MigrationRehearsal, Slice7Pass71, DraftOperations, AdminEventHandlerClassification, U5ParticipantsServer, Slice4AuthenticatedSignup, Slice4ParticipantLifecycle, Slice2Persistence, plus test #14 `TerminalEventRoutesRejectEveryAuditedAdminMutationBeforeAnySideEffect` | 285/285 passed, 0 skipped |
| Hygiene | `git diff --check`; `cmp` of `tokens.css`/`components.css` against `wwwroot/css/admin-design-*` | clean; byte-identical |

## 42d §A10 leftover greps (`src`)
1. `CreateInternalParticipant|_InternalParticipantForm|CancelWomValidation|FillVacancy|CompletePromotionFollowUp|PrivateWithdrawalNote` → only two comments in `Participants.cshtml.cs` (:59, :190, naming the retired handlers; unknown handlers 404) and the service method `CompletePromotionFollowUpAsync` (`ISignupService.cs:177`, `SignupService.cs:2174`): no page or test calls it. **Kept; deletion needs a ruling (U10 with U5-E3).**
2. `ParticipantDiscord|ParticipantCaptain|ParticipantSource|ParticipantTeamId` → none.
3. `Move up in queue|Move down in queue|Participant.s note|left a note` → none.
4. `RemoveFinalizedRosterParticipantAsync` in `Participant*.cs` and `SignupService.cs` → definition (`SignupService.cs:1707`) only; the caller is `Draft.cshtml.cs:268` (Teams path).
5. `PrevalidateReacquireNamesAsync` → called at `SignupService.cs:2664` from `RejoinAsync` (the participant’s own rejoin), not from `RestoreAdminParticipantAsync`.
6. `!team.IncludedInDraft` in `Participants.cshtml.cs` → none.
7. HTTP: old detail URL → 30x to `?participant=` (Slice9Pass92, ParticipantFlow, C11, Slice4 tests; browser test and `check-u5-ur.cjs` follow it); terminal events GET 200 read-only and non-payment/non-note POST refused with no write (test #14; `check-u5-ur.cjs` shows Add disabled and the lock chip on Cancelled, Archived and Live).

## Shared-file changes in this stretch
`UiReviewScenarioSeeder.cs` (open-signup event gets capacity 4, four confirmed, two waiting, one withdrawn; `UiReviewParticipants` record), `UiReviewScenarioCatalogue.cs` (Participants group, 14 links), `SharedResource.da.resx` (one new Danish entry for the singular wording; merge tail), `SignupSetup.cshtml` (`data-shell-link`), `DELIVERY_PLAN.md` (10 register rows after the S5 service-path row).

## Observed, not changed
- At 1280 px the table scrolls horizontally by about 58 px (min width 1020 px, reference breakpoint at 1260 px); this is the reference’s own responsive rule and the conformance geometry passes.
- `event-manage.js` still contains the dead participant dialog code (U5-E3, U10).
