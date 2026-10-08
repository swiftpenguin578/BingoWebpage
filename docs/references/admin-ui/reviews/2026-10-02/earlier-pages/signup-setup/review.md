Signup setup source review — changes required

Reviewer: /root/dashboard_backend/overview_review, gpt-6.1-sol/high. Direct reporting to planner /root under the explicit standalone reference-review assignment. Read-only source comparison; no browser, runtime, database, provider, build, tests, source edits, staging or commits. Outstanding Overview findings were not re-reviewed.

Reference corrections

R1 — P2: uncertain custom-question readback can report success for the wrong definition.
Source: /Users/christopher/Documents/BingoWebpage/docs/references/admin-ui/SignupSetup.dc.html:1146 and :1152; terminal found presentation :1401, :1413, :1444. Related README.md:924.
Editing a Single choice question before the first response, changing only its choices, selecting the “request did not complete” uncertain outcome, and checking again compares label/help/type/required but never the choices. The old question is declared to have “the values you entered”; Save disappears even though the edited choices were never stored. Compare the complete normalized persisted definition, including ordered choices.
For an uncertain add, any newly appearing custom question with the same normalized label is treated as found, even if another admin created it with a different type/help/options. The careful “may have come from your request” wording does not prevent the terminal state from removing Save and steering the user to close an unsaved distinct draft. Preserve the draft and an ambiguous recovery path; use the already identified request key/returned question ID for authoritative reconciliation. A label alone must not finalize the intended add. This is a concrete reference recovery defect, alongside the separate acknowledged backend idempotency gap.

R2 — P2: the other settings card can destroy an uncertain operation’s comparison baseline.
Source: SignupSetup.dc.html:777, :781, :793, :804, :825, :837, :841; independent card enablement :1287 and :1311.
Submit a capacity or code change with the applied-but-response-lost outcome. Before checking that card, save the other card: its stale refresh (or subsequent successful retry) refreshes the shared view.settings.version while the first card remains uncertain. Check again then compares the server against the refreshed version, not the version submitted by the uncertain request, and can say “It wasn’t saved” for an applied operation. Retain immutable per-request baselines/reconciliation evidence or resolve pending uncertainty before rebasing it. Returning a new settings version alone is insufficient unless this pending state is preserved.

R3 — P2: switching inline account renames silently loses dirty input.
Source: SignupSetup.dc.html:900; account-add success :878; enabled actions :1359, :1364, :1378.
Type an unsaved replacement label for one optional account field, then choose Rename on another account field. startRename replaces the single state.rename object without a discard/save guard. Adding an account field also replaces it with the new field’s inline rename. Both actions remain available while the first rename is dirty. Preserve the draft or guard these transitions with the existing save/discard behavior. This requires no new component design.

R4 — P3: capacity consequence names the wrong promotion order.
Source: SignupSetup.dc.html:1292 and README.md:835. Application: /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/src/Bingo.Infrastructure/Signups/SignupService.cs:2804 and :2561.
The preview says promotion is “in signup order.” The accepted contract is current waiting-queue order; the service orders by WaitingListedAt, then SignupSequence and ID, and moving a confirmed participant to waiting resets their queue entry. Original signup chronology need not match that queue. Use “waiting-list order”/“queue order” in both copies.

Existing application defect, separate implementation authorization

B1 — P2: the current signup-code handler does not enforce the 100-character limit server-side.
Source: /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage/src/Bingo.Web/Pages/Admin/Events/Participants.cshtml.cs:134, :170, :381; src/Bingo.Infrastructure/Security/SecretHasher.cs:12.
StringLength(100) exists on the bound value, but OnPostSignupCodeAsync never checks ModelState or length and passes the retained bound string directly to the hasher. The installed page capability filter checks lifecycle/route authorization, not input validity. A valid-version POST containing more than 100 characters follows the hash/persist path despite validation errors. The reference validates this correctly; the defect is in the existing endpoint, not a reason to redesign the reference. Authorized implementation should reject it before mutation and exercise the actual POST boundary; this review did not execute that request.

Integration: verified existing behavior and necessary gaps

Already exists/reuse:
- UpdateSignupAdministrationAsync plus BingoEvent.SetParticipantCap supports decreases down to confirmed count, always-on waiting, pre-draft phase limits, expected event version and queue promotion/audit/notifications. The old IncreaseParticipantCap helper is not the active baseline. Manage.OnPostCapacityAsync currently owns the transport.
- Participants.OnPostSignupCodeAsync already retains an existing hash on enabled+blank, requires a code to enable without stored protection, clears it when disabled, advances the event/form versions and excludes secrets from audit. Keep the 100-character enforcement correction separate as B1.
- Questions uses persisted SignupForm.FirstResponseAt for format/options/required locks, not current participant totals. After that boundary, send only label/help in edits: Questions.cshtml.cs:326 rejects structural keys even if their values are unchanged. New questions are optional; no backfill is performed.
- Account/system fields are distinct from custom types; primary/captain are protected; custom-only ordering exists. Delete/co-captain-disable already validates expected answer count, event-registration-release count and question version and returns updated impact. SignupService.cs:65, :100–103 deletes only the selected question’s saved answers and releases only its unreleased event assignments; it does not unlink global accounts or alter signup status/history.
- Per-question counts and release counts already load in Questions.cshtml.cs:394–418. Project them into rows; no new count service/table is required. Text already renders as a multiline textarea; the current Admin label at Questions.cshtml.cs:510 needs the approved Text terminology change.
- The ordinary public table excludes deleted/inactive/co-captain answers and retains legacy public flags; the separate authorized captain draft view has its existing access projection. Do not add a per-question privacy setting or broaden audience visibility during this consolidation (Signups.cshtml.cs:43, :79).

Approved integration work, not new product decisions:
- Consolidate capacity, code and form transports into the approved Signup setup route/tabs, retaining separate mutations; retire capacity from Manage/Schedule ownership. The reference’s tabs, previews, drawer and in-place counted confirmations are accepted interactions awaiting binding.
- Return refreshed event/settings version and values for each settings response. Current SignupAdministrationResult at ISignupService.cs:258 has no new event version; current handlers redirect rather than supply the intended response contract. Retain pending baselines as R2 requires.
- Add client-baseline version checks on custom/account add, custom edit, account rename, move and co-captain enable. Event row locks and ORM concurrency do not reject a stale rendered client whose operation arrives after another committed change. Delete/disable impact checks already exist. Reference mocks also have partial checks: do not infer full concurrency implementation from them.
- Add idempotent request keys for question/account creation and return the created ID. Current Questions additions create fresh GUIDs and return generic success/redirect without a request key. This is necessary for safe uncertain retry; R1 remains an independently fixable reference state-handling issue.
- Report or reject required-to-optional normalization when a first response arrives before an add. Questions.cshtml.cs:63 and :79 silently replace Required=false and :101 only says “Question added.” The reference shows the intended explanation. Preserve FirstResponseAt semantics for imported/admin-created/accepted responses.
- Reuse existing impact data, authorization, hidden/not-found and lifecycle guards when binding read-only/error states and direct navigation. No new shared framework is needed.

Authority reconciliation

At the time of the targeted read, PRODUCT_REQUIREMENTS.md:1387 still said cap only increases after first publication and waiting support can be toggled; UI_PAGE_MATRIX.md:31 still listed Schedule capacity. These are obsolete against the explicit current approved brief. The reference is correct to permit bounded decreases and omit the waiting toggle. The active Luck writer owns changing authority/status documents; this reviewer made no edits. Planner should reconcile the existing owners, not reopen an already approved product decision. README’s claim that max100 is already enforced needs B1’s qualification.

Shared reuse and evidence limits

SignupSetup imports ui/tokens.css, ui/components.css and ui/behavior.js and uses existing shell, fields, cards, banners, buttons, drawer/dialog/toast and DKAdmin focus/Tab-trapping/exit behavior. New real tabs, inline per-card save bars, question/choice rows and drawer-behind dimming are documented in Components.dc.html:400–431 and shared components.css:803–852. No concrete shared-source regression was found in the directly affected consumers. Optional extraction and subjective spacing are not required corrections. No visual/keyboard/reduced-motion/manual acceptance pass is claimed; Claude’s handoff claims are not independently executed evidence.

Stable capture: 20 directly relevant reference/application files matched before/after SHA-256, recorded in manifest-before.json and manifest-after.json in this evidence directory. SignupSetup.dc.html SHA-256 ac28a9a454a0be8037055d1b4699cef867444142353f85a3d69670d75d4c09b3. Snapshots are under reference/ and application/. Application branch verified codex/participants-functionality; existing dirty Participants/Dashboard/Luck work preserved. Unrelated changing sources/diff were not reviewed.

Next permitted step: planner records R1–R4 for named reference correction when an authorized writer is available, and separately routes B1/approved integration gaps under implementation authority. Recheck the named corrections and their direct consequences using this stable capture; source review remains changes-required and visual acceptance remains outstanding.
