# Manual Test Checklist

**Status:** Planning Pass 2 framework; exact routes, accounts, seed prerequisites, and expected values are completed with each implementation slice before handoff.

**Last updated:** 2026-07-27

**Purpose:** Preserve the user's manual acceptance checks outside chat without adding testing controls to the application.

## How to use this checklist

- Reset/regenerate the documented Development scenarios before a slice when its data assumptions require it.
- Record the application revision, date, browser/device, scenario, and account used.
- Check the normal path plus the listed permission, validation, failure, narrow/mobile, keyboard, and no-JavaScript cases that apply.
- A checked item means the observed result matched the written expected result. Record defects beside the failed item; do not rewrite the expectation to match a bug.
- Keep automated-test results in the normal test output. This file records only human-verifiable behavior and visual/interaction acceptance.
- The manual tester is not expected to exercise both success and failure for every mutation. Implementation and review must inventory the slice's mutation endpoints, apply the shared feedback contract consistently, and cover the common mechanism plus high-risk exceptions with focused automated tests. Manual acceptance samples representative successful, validation, permission, and conflict paths.

## Verification record template

- Revision:
- Date:
- Tester:
- Browser/device:
- Seed/scenario:
- Account/role:
- Result: Not run / Passed / Failed / Blocked
- Notes or defect links:

## Slice 1 — Website accounts, authentication, Super Admin, recovery, and disabling

### Executable manual-test environment

Use a disposable Development database, never a retained-data copy. Start PostgreSQL with `docker compose up -d postgres`, then run the app with the Development connection string from `src/Bingo.Web/appsettings.json`. Configure a disposable Discord application through user secrets (do not commit them): `DiscordAuthentication:ClientId`, `DiscordAuthentication:ClientSecret`, and its redirect URI `https://localhost:7131/Account/DiscordCallback`; the exact HTTPS URL printed by `dotnet run` takes precedence. The app deliberately leaves Discord unavailable when these values are absent.

Before running the cases, apply the migration and catalogue snapshot, provision one intended owner through the explicit operator command, and create the remaining roles through the UI:

```bash
dotnet user-secrets set --project src/Bingo.Web "Slice1:BootstrapOwnerPassword" "<10+-character-disposable-password>"
dotnet run --project src/Bingo.Web -- --apply-catalogue-snapshot
dotnet run --project src/Bingo.Web -- --slice1-bootstrap-owner --username slice1-owner --confirm-username slice1-owner
dotnet run --project src/Bingo.Web -- --reset-test-data
dotnet run --project src/Bingo.Web
```

The bootstrap command works only with an empty account table and creates the sole Super Admin; its password is read only from user secrets. The recovery command only promotes an existing active website account. The seed command creates the labelled workflow scenarios and prints the secondary Admin and captain credentials: `SeedAdminTwo` / `SeedAdmin!1234`, and generated captain usernames / `SeedCaptain!1234`. Sign in as `slice1-owner` with the user-secret password. Create a normal User and disabled User through Discord onboarding when credentials are available; create an emergency credential under `/Admin/Accounts` for a real seeded team, generate its setup link, choose a password, then enable it. Use two independent browser profiles for stale-session checks. The exact local URLs are `https://localhost:7131/Account/Login`, `/Account/Settings`, `/Admin/Accounts`, `/Admin/Accounts/Transfer`, `/Admin/Audit`, and `/Captain`; HTTP is `http://localhost:5164`. A retained legacy-database migration rehearsal must use a separate copied database and begin with `--slice1-migration-preflight --slice1-owner <existing-admin-id-or-username>` before applying the migration.

**Prerequisites:** Document the revision, migrated/reset Development database, selected owner account, Discord test application/callback, one User, two Admins, one disabled User, one emergency credential, and two separate browser sessions.

- [ ] **S1-01** — Migration preflight identifies every legacy Admin/Captain, preserves account IDs, flags ambiguous usernames/scopes, and requires an explicit initial owner.
- [ ] **S1-02** — The selected existing account becomes the sole Super Admin; another Admin remains Admin; both retained password hashes still authenticate.
- [ ] **S1-03** — Every legacy Captain becomes a disabled emergency credential with the correct event/team/participant role scope, and no free-text participant Discord name is linked.
- [ ] **S1-04** — Clean-production rehearsal applies migrations to an empty database, initializes the reviewed catalogue snapshot, creates no Development/test account or workflow data, and provisions exactly one intended Super Admin through controlled operator setup.
- [ ] **S1-05** — First-time Discord authentication reaches onboarding; cancelling, provider failure, or 15-minute onboarding-state expiry creates no account.
- [ ] **S1-06** — Successful onboarding atomically creates the website account, unique public/password-login username, password, initial OSRS character, preferred link, and completed state.
- [ ] **S1-07** — Password creation/change rejects fewer than 10 characters, accepts a valid 10-character password and long printable passphrase, and imposes no character-class composition rule.
- [ ] **S1-08** — A public-username collision retains every other entered value and neither takes over nor merges the existing account.
- [ ] **S1-09** — Returning Discord and public-username/password login open the same website account; Discord-server membership is never requested or required.
- [ ] **S1-10** — Incorrect username/password uses generic feedback; identifier and network throttles activate independently without revealing account existence.
- [ ] **S1-11** — Non-persistent login ends with the browser session or after its 12-hour ticket maximum; **Remember me** persists for no more than 30 days and cannot slide indefinitely.
- [ ] **S1-12** — Password change reissues the intended current session, rejects prior password sessions, and does not unnecessarily invalidate a separate Discord-authenticated session.
- [ ] **S1-13** — A non-persistent password session expires within 12 hours; Remember me expires within 30 days from sign-in even after continued activity; identifier and network failure limits work independently while exposing only generic login feedback.
- [ ] **S1-14** — Forgot password is a static contact-admin page with no username/account lookup form.
- [ ] **S1-15** — Admin can generate a raw reset link once for a User but cannot see/set the replacement password; ordinary Admin cannot generate one for an Admin or Super Admin.
- [ ] **S1-16** — Super Admin can generate an Admin reset link but has no in-product self-reset link; links expire after 60 minutes, and expired, used, and superseded links all fail generically.
- [ ] **S1-17** — Reset completion changes the password, consumes the link, rejects prior password sessions, and records no secret in audit history.
- [ ] **S1-18** — Linked account can unlink after current-password confirmation and remains usable through a reissued password session with all roles/history intact.
- [ ] **S1-19** — Unlinked account can link Discord and linked account can replace Discord directly; an already-used Discord ID fails without mutating either account.
- [ ] **S1-20** — Link/unlink/replace invalidates other sessions, preserves the website account ID and all history, and warns—but does not block—the Super Admin before unlink.
- [ ] **S1-21** — Only Super Admin sees and can execute Admin grant/revoke; the confirmation names the target/before-after role, requires no typed username/reason, emergency credentials are ineligible, and revoke returns the target to User without changing event records.
- [ ] **S1-22** — Grant/revoke rejects the target's prior sessions with accurate access-changed feedback; Super-Admin transfer requires the owner's password plus typed destination username and invalidates affected sessions.
- [ ] **S1-23** — Super-Admin transfer changes recipient to Super Admin and former owner to Admin atomically; stale/concurrent transfer cannot produce zero or two owners.
- [ ] **S1-24** — Admin can disable a User but not an Admin, self, or Super Admin; Super Admin can disable/restore another Admin but not self/current owner.
- [ ] **S1-25** — Disable requires a reason and immediately rejects all target sessions; it retains username/Discord reservations and every event record. Restore needs confirmation but no reason and does not restore expired event authority.
- [ ] **S1-26** — No website-account merge/permanent-delete action exists; clean Development/production reset remains the test-identity cleanup path.
- [ ] **S1-27** — Admin creates separate disabled emergency credentials for one team, each with a globally unique login username; a 60-minute single-use setup/reset link lets the captain choose the password without exposing it to the Admin.
- [ ] **S1-28** — Emergency password setup does not enable access; explicit enablement works only after setup, multiple individual credentials can coexist, and submission cutoff disables them.
- [ ] **S1-29** — Emergency credentials remain usable through the configured submission grace and are durably disabled with audit history at submission cutoff; an Admin must explicitly re-enable them, which is also audited.
- [ ] **S1-30** — One Accounts area keeps website accounts and emergency credentials visibly separate; both search/filter server-side and paginate at 25 rows without losing active filters.
- [ ] **S1-31** — Website rows show the approved summary, and details show every linked OSRS character, non-authoritative Discord display name, participation, role history, and disable history without password/OAuth/setup/reset-token data or unnecessary raw Discord IDs.
- [ ] **S1-32** — Emergency rows show username, event/team, setup/enabled/cutoff state, last login, and only actions authorized for the current Admin/Super Admin.
- [ ] **S1-33** — Audit table shows exactly 25 newest rows per page, retains event/actor/action/entity/date filters across navigation, reaches older history, renders structured changes readably, and offers no edit/delete/export.
- [ ] **S1-34** — Routine website login updates Last login without a main-audit row; failed/throttled attempts reach security logs, while emergency login and approved security-sensitive mutations create durable audit entries.
- [ ] **S1-35** — Grant/revoke/restore notifies the affected account; disablement shows neutral contact-an-admin guidance without exposing its private reason.
- [ ] **S1-36** — Operator recovery has no web action; its owner-reset and explicit ownership-transfer modes enforce identifiers/confirmation and create system audit history.
- [ ] **S1-37** — Every mutation has accurate success/failure/conflict feedback through both enhanced and ordinary no-JavaScript form submission.
- [ ] **S1-38** — Every new Slice 1 page uses the approved shared UI rules and passes desktop, intermediate, narrow/mobile, keyboard, focus, validation-summary, feedback, and no-JavaScript checks.
- [ ] **S1-39** — Login, onboarding, normal account settings/recovery, and emergency-captain setup/login show complete English and Danish headings, controls, help, validation, confirmations, empty/error/permission states, and feedback through the existing language switch; Admin/Super-Admin-only pages may remain English-only.

## Slice 2 — My accounts, OSRS characters, event assignments, and identity migration

### Executable manual-test environment

Use a disposable migrated Development database and a normal website account completed through Discord onboarding. Keep a second normal website account available for shared-character checks, and retain one seeded event with the existing fixed signup/edit form plus one event that displays the draft and public board projections. Do not use a production or retained-data rehearsal database for manual acceptance.

Start PostgreSQL and the application with the documented Development settings:

```bash
docker compose up -d postgres
dotnet run --project src/Bingo.Web -- --reset-test-data
dotnet run --project src/Bingo.Web
```

The exact HTTPS URL printed by `dotnet run` takes precedence. The Slice 2 routes are `/Account/Onboarding`, `/Account/MyAccounts`, `/Account/Settings`, the existing `/Events/{slug}/Signup` and private `/Events/{slug}/Signup/Edit/{token}` compatibility routes, `/Admin/Events/Draft`, and the existing public board/team/tile routes. Use two browser profiles when checking two website accounts. Record the event slug and participant used for projection checks.

Automated acceptance cases remain listed here so their IDs match `SLICE_2_MANUAL_TEST_RESULTS.md`; a row marked `Automated` there does not need manual repetition. Manual acceptance should concentrate on the end-to-end, visual, responsive, keyboard, localization, and protected-surface cases.

- [ ] **S2-01** — A clean PostgreSQL database migrates to the current model, and a representative retained Slice 1 database migrates deterministically without losing existing account/character links, participant IDs, playing EHB, informational characters, or unowned participant records.
- [ ] **S2-02** — OSRS-character matching is case-insensitive, one website account cannot duplicate its own link, and two website accounts may deliberately link the same trusted or borrowed character.
- [ ] **S2-03** — My accounts retains one account/character history row across unlink/reactivation, keeps personal label/order/saved EHB on that link, and permits at most one active preferred character.
- [ ] **S2-04** — An event permits only one current assignment for a normalized OSRS character; playing assignments require an event EHB snapshot while informational assignments have no EHB.
- [ ] **S2-05** — Existing signup creation, private-link editing, CSV import, and Admin correction store and display the primary playing character, optional informational character, and event EHB through event assignments rather than removed participant fields.
- [ ] **S2-06** — Imported and external participants remain valid without a website-account owner; ownership is never guessed from website username, Discord text, or an OSRS-character link.
- [ ] **S2-07** — Discord onboarding clearly collects separate website-username and first-OSRS-character values, creates the preferred My accounts link atomically, and retains entered values after a field-specific collision.
- [ ] **S2-08** — My accounts supports the empty state, add/reactivate, label, saved-EHB edit, ordering, and preferred selection while preventing one account from mutating another account's links.
- [ ] **S2-09** — Saved EHB belongs to one website-account/character link and does not change another borrower's default or an already submitted event snapshot.
- [ ] **S2-10** — Unlinking a character used by an upcoming or live registration requires an understandable confirmation and clearly explains that the event registration remains.
- [ ] **S2-11** — A spelling correction preserves link metadata and updates only currently editable account-owned assignments; a conflict names the affected event safely and rolls back the whole correction.
- [ ] **S2-12** — Account settings clearly distinguishes website identity from OSRS characters and Discord; a correct-password username change updates the login/display username, while wrong-password and case-insensitive collision attempts leave it unchanged.
- [ ] **S2-13** — A successful website-username change refreshes the current session without changing Remember-me expiry, invalidating unrelated sessions, or changing My accounts, event assignments, teams, evidence, roles, or event-facing names.
- [ ] **S2-14** — Emergency credentials cannot open My accounts or use website-username rename, and their reserved login names still block conflicting normal website usernames.
- [x] **S2-15 — Superseded by Slice 4.** Private signup edit tokens and fixed signup/edit fields were intentionally removed; authenticated ownership is the current path.
- [ ] **S2-16** — Active runtime code and projections no longer read or write the removed participant primary-name, normalized-name, second-name, or EHB authority; matching names in migrations and fixed-form view models are intentional compatibility.
- [ ] **S2-17** — Every User-facing Slice 2 page, label, validation message, warning, empty state, success, and safe failure is understandable in both English and Danish.
- [ ] **S2-18** — My accounts and username settings remain usable at desktop, intermediate, and narrow widths with logical keyboard order, visible focus, restored validation focus, and understandable empty/warning/conflict states.
- [ ] **S2-19** — Discord's external authorization page requires JavaScript and is outside the application's fallback boundary. Automated native-form coverage verifies the local onboarding, My accounts, website-username rename, sign-out, and new-username sign-in endpoints without requiring a disproportionate manual no-JavaScript journey.
- [ ] **S2-20** — Using representative retained participants, verify the live draft and public board/team/tile/submission routes show assignment-backed OSRS names/EHB while preserving their existing composition, Pass 12 overlay behavior, responsive route navigation, and no-JavaScript fallbacks; the board editor remains visually and functionally unchanged.

## Slice 3 — Event creation, scheduling, readiness, cancellation, archive, and current-event policy

- [ ] **S3-01** — As an Admin, open `/Admin/Events/Create` at desktop and narrow widths. The familiar five-step flow remains usable with JavaScript, and name plus Copenhagen-defaulted supported timezone create a private draft with every optional section blank.
- [ ] **S3-02** — Disable JavaScript and create the same minimal private draft. Every step's fields remain reachable in document order, the native submit redirects to its Admin event workspace, and no public signup/board/team state is exposed.
- [ ] **S3-03** — Enter an optional full setup (description, valid half-hour schedule, capacity, signup setting/question, buy-in, and board rows/columns limited to 1–8). Verify entered values remain after creation; leave a different optional section blank and verify it does not block creation.
- [ ] **S3-04** — Enter an invalid/ambiguous DST time, an invalid custom question, or an invalid board dimension outside 1–8. Verify field-specific feedback retains input and creates no event. A partial date/time manual case is not applicable to the combined calendar control.
- [ ] **S3-05** — Verify a generated private event link is editable before first public exposure, rejects invalid/colliding links safely, and does not disclose another event's details.
- [x] **S3-06** — Approved: Event identity saves redirect back to Manage with mutation feedback. Banner behavior remains covered by the recorded identity checks.
- [x] **S3-07** — Passed: a first-public timezone change previews stored instants and requires confirmation; after actual start it requires a written reason.
- [ ] **S3-08** — Use two browser sessions to submit conflicting identity changes. Verify the stale session receives safe conflict feedback, retains its attempted values for review, and does not overwrite the latest saved identity.
- [x] **S3-09** — Passed for the functional interaction boundary: native creation/identity feedback, direct-route fallback, keyboard/focus, and validation behavior. Visual review and styling are deferred to the full UI overhaul.
- [ ] **S3-10** — Confirm a normal User cannot use `/Admin/Events/Create` or an identity route, and the Admin audit trail records creation and identity updates without a signup code, upload payload, or other secret.

Pass 3.2 is approved: no-JavaScript creation passed; unset schedules show “Not set”; and board dimensions accept 1–8 while rejecting values outside that range. Visual review is deferred to the full UI overhaul.

Pass 3.3 is approved: private/open/closed schedule editing, proposed Open-now and Reopen closing behavior, capacity/waiting-list behavior, multiple non-overlapping signup windows, overlap rejection, exact back-to-back boundaries, and exact-link/unlisted signup behavior passed. Calendar appearance remains deferred to the full UI overhaul.

- [x] **S3-11 — Passed.** Create and Schedule show localized values while posting canonical `yyyy-MM-ddTHH:mm`; a future five-minute opening schedules and opens automatically without a checkbox.
- [x] **S3-12 — Passed.** Opening and closing edits preserve untouched values, and overlap feedback appears in notification history.
- [x] **S3-13 — Passed.** Postponed-start readiness updates after blocker resolution; manual Start resolves the attempt, preserves its history/configured start, records the actual start separately, and immediately renders Live controls.
- [x] **S3-14A — Passed.** Reset Development data and verify only TEST 13 and TEST 15 remain.
- [x] **S3-14B — Passed.** Fixture-only data has no automatic public current event and one real current event is selected.
- [x] **S3-15 — Passed.** “Event discarded.” appeared exactly once on Admin Events through enhanced and no-JavaScript journeys.
- [x] **S3-16 — Passed.** Protected participant/team/access/submission/evidence history blocks discard without cleanup.
- [x] **S3-17 — Passed.** Never-public cancellation preserves private history and exposes no public event or reason.
- [x] **S3-18 — Passed.** Previously public cancellation remains generic publicly; explicitly account-owned confirmed and waiting-list cancellation notifications are accepted as Automated for Slice 3 because the retained pre-Slice-4 signup UI creates unowned participants. The private reason is excluded.
- [x] **S3-19 — Passed.** Finalized archive preserves historical public routes and results.
- [x] **S3-20 — Passed.** Archived unfinalization succeeds without a competing current event and preserves snapshots.
- [x] **S3-21 — Automated.** Competing Live/review/finalized current events block archived unfinalization atomically.
- [x] **S3-22 — Passed.** Repeated destructive actions and expired schedules produce no duplicate lifecycle effects.
- [x] **S3-23 — Passed.** No-JavaScript and enhanced lifecycle forms agree.
- [ ] **Slice 3 review remediation — terminal route retest.** As an Admin, open a populated Cancelled event and submit a stale direct POST to one configuration route (for example Identity or Board); verify readable read-only feedback, no mutation, and read-only Manage history. Also verify Finalized/Archived expose only Archive or reasoned Unfinalize on their lifecycle history route. This is the sole additional manual check before restricted re-review; it does not re-open prior passed manual cases.
- [ ] Minimal draft, manual/scheduled opening, and readiness blockers.
- [ ] Empty discard versus populated cancellation.
- [ ] Multiple non-overlapping signup windows (including back-to-back) work through exact links; overlaps and live/review/finalized singleton conflicts fail safely, while Development fixtures do not weaken Production commands.
- [ ] Finalized archive and historical-route preservation.

## Slice 4 — Signup, EHB/account questions, public signup board, capacity, and participants

### Consolidated post-4.7 review (accepted 2026-07-29)

- [x] Sign in normally and confirm signup/edit uses the event's current questions and My Accounts. There is no private-edit link, setting, or CSV import control for ordinary participants.
- [x] As an Admin before draft lock, correct a participant's answers and Regular/Alt accounts; confirm their place, timestamp, payment, and note remain unchanged. A reserved account changes nothing.
- [x] Create one participant with a selected website owner and one explicitly unowned participant while public signup is closed. Confirm normal confirmed/waiting placement and source without inferred ownership.
- [x] Transfer one participant by typing the destination username twice. Confirm the old account immediately loses My Events/confirmation access, the new account gains it, and My Accounts links, team/history, payment, and notes do not move.
- [x] Confirm payment/Admin notes stay private. Protected board, draft, team, tile, and submission routes remain available, while obsolete private-edit and ordinary-participant CSV controls are absent.
- [x] **Restricted Slice 4 remediation evidence.** Focused authenticated/PostgreSQL route tests prove retained Admin/private history versus anonymous output, stale-edit no-residue, shared withdrawal/promotion notifications, canonical Yes/No values, and My Events confirmation → roster → board → finalized/archived destinations.

- [x] **S4-01 — Passed.** The unauthenticated Login/My Accounts safe-return, normal-account, and emergency-credential boundaries passed. Changing a Regular account updates EHB to that account's saved default (or clears it) without copying the prior account's value.
- [x] **S4-02 — Passed.** Creation with a preferred Regular account/EHB and captain volunteer confirms only status, selected event accounts/EHB, and answers; website username, Discord identity, payment, Admin notes, token, and secrets remain excluded.
- [x] **S4-03 — Passed.** Open-signup editing updates answers, Regular/Alt selections, EHB, and captain volunteer without changing status/order/time/payment/Admin fields; closed signup hides Edit and rejects direct POST safely.
- [x] **S4-04 — Passed.** An unlinked current assignment remains editable without relinking; replacements require an active My Accounts link, and unavailable/duplicate replacements preserve the form and existing registration.
- [x] **S4-05 — Passed for current My events ownership/isolation and accessible destinations.** Two normal accounts see only explicitly owned records, and emergency credentials are excluded; current/history separation and event overview behavior passed.
- [x] **Lifecycle destinations — Automated.** Rendered HTTP coverage verifies confirmation, roster, published board, Finalized, and Archived destinations.
- [x] **Cancellation notifications — Automated.** Eligible owned confirmed/waiting participants receive generic notifications; withdrawn/removed/unowned participants and the private reason are excluded.
- [x] **S4-06 — Passed.** Form builder acceptance is recorded in CURRENT_STATUS.md.
- [x] **S4-07 — Passed.** Structural locking/replacement acceptance is recorded in CURRENT_STATUS.md.
- [x] **S4-08 — Passed.** Withdrawn confirmation states, open rejoin, closed read-only feedback, and stale rejoin protection passed.
- [x] **S4-09 — Passed.** Exact-link signup-table privacy, headings, positions, frozen EHB, answer history, lifecycle destinations, Admin historical access, and My Events ownership/destinations passed.
- [x] **S4-09 lifecycle presentation.** Roster-before-board and board-after-publication destinations plus Draft finalized / Board published / Event ready / Start postponed presentation were accepted.
- [x] Public/Admin answer visibility and optional historical answers.
- [x] Participant administration, payment, account correction, notification, and restoration.

- [x] **S4-10 — Passed.** Admin workspace filtering, totals, ownership presentation, and accepted smooth anchor returns passed; instant-jump review is deferred to the whole-site UI overhaul.
- [x] **S4-11 — Passed.** Repeatedly change Paid/Unpaid from participant details and confirm immediate save, `#payment` return, and accurate feedback. No-JavaScript parity is best-effort for these controls.

### Slice 4 Pass 4.6B — accepted

- [x] Correct a pre-draft participant using active Account and custom questions. Queue time/status/waiting position and payment/note remain unchanged; a reserved character rejects the whole correction.
- [x] Create one owned and one explicitly unowned internal participant while public signup is closed. Active-form validation, normal confirmed/waiting placement, source, and no inferred ownership passed.
- [x] Transfer one participant by entering the exact destination username twice. Old My Events/confirmation access is revoked, new access works, history/assignments/team remain unchanged, and both owners receive generic notifications.
- [x] Correction, creation, and transfer are rejected once the draft is locked, and no Admin-only data appears in public projections.

## Slice 5 — Teams, captain roles, external teams, and draft

- [ ] Exact manual cases to be finalized before Slice 5 handoff.
- [ ] Derived roster distribution, captain balancing, picks, repeated undo, pause/resume, and finalization.
- [ ] External/pre-formed team and post-finalization correction paths.
- [ ] If introduced for Slice 5, CSV is limited to external/pre-formed team rosters; ordinary draft-pool participants are not imported through CSV.
- [ ] Concurrent-admin and permission states.

## Slice 6 — Catalogue, board derivation, approval snapshot, preview, and publication

- [ ] Exact manual cases to be finalized before Slice 6 handoff.
- [ ] Admin catalogue edit/deactivate/reactivate and Super-Admin-only deletion/import.
- [ ] Live recalculation of unapproved board EHB after catalogue changes.
- [ ] Approval snapshot stability, unapproval, reapproval, and stale-concurrency handling.
- [ ] Public-style Preview board without approval/publication side effects.
- [ ] Complete-board validation and separate publication.

## Slice 7 — Live account swaps, participant navigation, and team focus

- [ ] Exact manual cases to be finalized before Slice 7 handoff.
- [ ] Planned/active account, whole-minute pending swap, unlimited swaps, and event-end freeze.
- [ ] Participant/captain/external-team authority.
- [ ] Team focus visibility, mutation, Super-Admin inspection opt-in, and realtime isolation.

## Slice 8 — Evidence submission, resubmission, review, and public visibility

- [ ] Exact manual cases to be finalized before Slice 8 handoff.
- [ ] Participant self-submit and captain teammate-submit with server-derived account.
- [ ] Pending edit/withdraw, reject notification, cutoff-bound Resubmit, and account-after-swap preservation.
- [ ] Admin metadata correction, approve/reject concurrency, reversal, and recalculation.
- [ ] Every approved screenshot/player public; no privacy or hidden-approved path.

## Slice 9 — Event end, live replacement, finalization, notifications, and history

- [ ] Exact manual cases to be finalized before Slice 9 handoff.
- [ ] Scheduled/early end, upload grace, cutoff, and UTC screenshot review.
- [ ] Withdrawal, vacancy, optional replacement, and captain-warning paths.
- [ ] Blocker resolution, finalization/unfinalization, archive, and participant read-only history.
- [ ] Personal notification versus unresolved-action behavior.

## Slice 10 — Wise Old Man integration

- [ ] Exact manual cases to be finalized before Slice 10 handoff.
- [ ] Per-playing-account EHB fetch success, not found, unavailable, rate-limited, and manual fallback.
- [ ] Cache timestamp, cooldown, stale result, retry/backoff, and manual refresh behavior.
- [ ] Multi-character participant totals and informational-account exclusion.

## Milestone 9 — Complete UI overhaul and full regression

This section runs only after Milestone 8A Slices 1–10 are complete. It is not UI Roadmap Pass 11, which covers the public-signup journey.

- [ ] Apply the approved site-wide visual rules to every public, participant, captain, Admin, Super-Admin, error, privacy, and empty state.
- [ ] Repeat every completed Slice 1–10 manual checklist against the final interface.
- [ ] Verify current Safari and Chromium on desktop, intermediate/tablet, and representative mobile widths.
- [ ] Verify keyboard-only use, focus order/visibility, screen-reader labels, reduced motion, contrast, validation summaries, and no-JavaScript fallbacks.
- [ ] Verify all permission, empty, loading, success, warning, error, conflict, and stale-state presentations.
- [ ] Confirm no critical/high defect remains before the production-preparation milestone.
