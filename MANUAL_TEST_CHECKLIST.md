# Manual Test Checklist

**Status:** Planning Pass 2 framework; exact routes, accounts, seed prerequisites, and expected values are completed with each implementation slice before handoff.

**Last updated:** 2026-08-01

**Purpose:** Preserve the user's manual acceptance checks outside chat without adding testing controls to the application.

**Authority boundary:** This file records manual verification journeys, observed results, and accepted evidence only. Product behavior and scope, workflow contracts, data invariants, technical architecture, and UI rules/approval are owned by the active authority documents linked from `README.md`; this checklist does not redefine them.

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

## Slice 5 Pass 5.2A/5.2B — teams and pre-formed external roster CSV

Historical note: Slice 5 acceptance used `TEST 52 — Team and CSV setup`, which has now been retired rather than retained as stale setup data. The current Development reset seeds the Slice 6 baselines documented below.

The retained account/catalogue foundations remain preserved by the real PostgreSQL double-reset regression. The current exact fixture contract is TEST 13, TEST 15, and TEST 62.

- [x] **S5-01 — Passed.** Create Drafted and Pre-formed teams; duplicate creation gives safe feedback with no additional team or audit.
- [x] Derived setup distribution updates for drafted-team changes, including uneven remainders.
- [x] **S5-02 — Passed.** Managed PNG/JPEG/WebP image replacement/removal and retired-asset protection passed.
- [x] **S5-03 — Passed.** Saved managed images remain visibly attached after reload.
- [x] Existing participant Pre-formed roster assignment removes the participant from the draft pool and rejects duplicates.
- [x] **S5-04 — Passed.** Unowned external roster members retain correct EHB/informational semantics without inferred ownership or captain role.
- [x] **S5-05 — Passed.** Reserved external-account retries give safe feedback with no residue.
- [x] Pre-event roster remove/move history, Cancel-draft restoration, and post-start metadata lock passed.
- [x] **S5-06 — Passed.** Numbers semicolon CSV preview/apply and canonical comma CSV passed.
- [x] **S5-07 — Automated/covered and approved.** Multi-row CSV with optional additional `Account` columns and Playing/Informational semantics passed.
- [x] Invalid CSV values, duplicates, reservations, stale/replayed previews, and Drafted-team CSV exclusion passed.

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

Automated acceptance cases remain listed here so their IDs match the [archived Slice 2 manual results](docs/archive/manual-evidence/SLICE_2_MANUAL_TEST_RESULTS.pre-consolidation-2026-08-15.md); a row marked `Automated` there does not need manual repetition. Manual acceptance should concentrate on the end-to-end, visual, responsive, keyboard, localization, and protected-surface cases.

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
- [x] **S3-14A — Passed.** The historical reset check passed; the current reset contract is TEST 13, TEST 15, and TEST 62.
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

**Slice 5 final acceptance (2026-07-29):** S5-01 through S5-10 and the final Public boards/Board-route retests are approved. Automated gates passed: Domain `153/153`, Application `82/82`, Browser `66/66`, Integration `188/188`, combined `489/489`; Release solution build, formatting, `git diff --check`, EF pending-model verification, and the relevant migration/reset checks passed. Slice 5 is ready for packaging/merge/push; those actions remain unperformed.

- [x] **S5-08 — Passed.** Captain assignment, controller acquisition/takeover/release, lease-guarded Start, and safe displaced-controller behavior passed.
- [x] **S5-09 — Passed.** The consolidated private-draft journey, including controller-only cancellation and retained draft history, passed.
- [x] **S5-10 — Passed.** Finalization/publication, frozen public roster privacy, reopening, ledger correction, publication history, and confirmed published Pre-formed correction without a typed reason passed.
- [x] **Final Public boards/Board-route retests — Passed.** Historical TEST 52 roster-only/public-board route acceptance passed. TEST 62 now supersedes it as the current separate-publication fixture.
- [x] Derived roster distribution, captain balancing, picks, repeated undo, pause/resume, finalization, external/pre-formed corrections, CSV scope, concurrent-admin, and permission states passed.

## Slice 6 — Catalogue, board derivation, approval snapshot, preview, and publication

**Slice 6 manual acceptance (2026-07-30):** S6-01 through S6-06 are approved, including the final mixed same-boss single-roll/multiplied-roll Zulrah-style EHB retest. Board preview functionality is accepted; its exact visual match to the established public **View bingo** Board is deferred to the UI overhaul and is not a functional blocker.

Reset Development data first. It leaves exactly TEST 13, TEST 62, and TEST 15, with valid selected catalogue-rate bindings and frozen-rate approval data. Use `TEST 13 — DKL Board` (`test-13-dkl-board`) for S6-02 through S6-04. Then use `TEST 62 — Board publication setup` (`test-62-board-publication-setup`) in this order: verify the finalized roster/approved-private board, dismiss the separate-publish handoff, publish later, inspect the public board, then run the confirmed/reasoned correction. Use `TEST 15 — DKL Live` only to compare the existing public live/progress surface.

**Final retest:** The catalogue, Board editor, frozen EHB, preview overview, finalized-roster reopening, published correction, feedback, and mixed same-boss single-roll/multiplied-roll EHB paths are approved.

- [x] **S6-01 — Catalogue administration — Passed.** Confirmed the retained catalogue, source-drop lifecycle, distinct Nid/Nid (Destroy) values, dependency-safe deletion, and absence of Wiki-import web surfaces.
- [x] **S6-02 — Draft derivation — Passed.** Confirmed live derivation, frozen approved EHB, automatic-EHB validation, manual-objective EHB, and the mixed same-boss single-roll/multiplied-roll Zulrah-style retest.
- [x] **S6-03 — Private approval — Passed.** Confirmed residue-free rejection, valid immutable approval snapshots, history-preserving unapproval, and Draft return after competitive edits.
- [x] **S6-04 — Preview — Passed.** Confirmed deterministic, side-effect-free preview behavior without Admin controls or EHB. Exact visual parity with the established public Board is deferred to the UI overhaul.
- [x] **S6-05 — Separate publication — Passed.** Confirmed the separate publish handoff, dismissal behavior, later publication, and event-start publication gate.
- [x] **S6-06 — Published correction — Passed.** Confirmed confirmation/reason, replacement snapshot and recalculation, retained history, Live correction, and readable feedback.

## Slice 7 — Live account swaps, participant navigation, and team focus

- [x] **S7-01 — participant live context and navigation.** Confirm owned participants reach the correct roster/team-board surface and can see their event, team, role, planned/current Playing account, and event-end context on the standalone route.
- [x] **S7-02 — participant self-swap.** During Live and before event end, select another current Playing account and confirm the planned change, next-whole-minute activation, feedback, and visible focus styling work without exposing another team's private state.
- [x] **S7-03 — captain/co-captain unlinked-member swap.** Confirm a Captain/Co-captain can schedule a Playing-account change only for an unlinked member of their own pre-formed team, while linked and unrelated participants remain protected.
- [x] **S7-04 — private focus authority and visibility.** Confirm Captain/Co-captain focus mutations, normal member read-only visibility, other-team privacy, and explicit Super Admin read-only inspection.
- [x] **S7-05 — concurrency and stale mutation behavior.** Covered by the accepted automated PostgreSQL gate.
- [x] **S7-06 — realtime refresh and reconnect.** Confirm authorized focus updates become visible without leaking focus data to unauthorized viewers.
- [x] **S7-07 — event-end boundary.** Confirm swap/focus mutation is unavailable at or after event end while retained focus can remain visible for historical/UI presentation.
- [x] **Final focus remediation.** Confirm overlapping tile/row/column markers coexist, one marker can be unfocused independently, Clear all affects only the current team, completed tiles cannot be focused or styled, completion clears an existing tile marker, and reversal does not restore it.

Slice 7 manual acceptance approved 2026-07-31.
- [ ] Planned/active account, whole-minute pending swap, unlimited swaps, and event-end freeze.
- [ ] Participant/captain/external-team authority.
- [ ] Team focus visibility, mutation, Super-Admin inspection opt-in, and realtime isolation.

## Slice 8 — Evidence submission, resubmission, review, and public visibility

### Development reset contract and ordering

Run `dotnet run --project src/Bingo.Web -- --reset-test-data` in Development after applying migrations. The reset creates the named workflow fixtures, including `Sommerbingo 2026` (`test-13-dkl-board`), `Vinterbingo 2026` (`test-15-dkl-live`), `Efterårsbingo 2026` (`test-16-signup-lookup`), `Det Store Danske Vinterbingo 2027` (`test-62-board-publication-setup`), and `Påskebingo 2026` (`test-84-evidence-history`). The approved `Det Store Danske Sommerbingo 2026` record is not Development seed data: run the zero-write `--preflight-historical-import` command with the external operator input before any explicitly authorized apply. Finalized positive fixtures have active frozen roster publication snapshots; `test-98-missing-playing-assignment` intentionally remains unpublished. Use `slice1-owner` with the disposable password supplied to `--slice1-bootstrap-owner` (or `SeedAdminTwo` / `SeedAdmin!1234`) for Admin review and lifecycle actions. Use `SeedEvidenceCaptain` / `SeedEvidence!1234` for the normal linked website Captain on the first team of test-15; use `SeedEvidenceCoCaptain` / `SeedEvidenceCoCaptain!1234` for its distinct website-owned Co-captain; and use `SeedEvidenceParticipant` / `SeedEvidenceParticipant!1234` for its distinct ordinary website-owned Participant. The enabled and disabled/expired emergency credentials remain available as documented above. Reset is idempotent; repeat it before each sequence that mutates shared state.

Run the sequences in this order from a fresh reset unless stated otherwise:

The Admin **End event now** action on TEST 15 moves the event to `AwaitingFinalReview` but does not itself close the still-active submission cutoff; it is not a post-cutoff read-only shortcut and it does not by itself permit finalization. Use TEST 84 for deterministic post-cutoff read-only and Finalized/Archived checks.

1. **Ordinary Participant live workflow — TEST 15.** Sign in as `SeedEvidenceParticipant`, open the rendered first-team board route `/Events/test-15-dkl-live/Board/{teamSlug}`, choose a tile, and submit one screenshot through the ordinary team-board drawer (or its `/Captain/Submit` route-backed fallback) for the credited participant shown by the server. Open `/Submissions`, follow the new row to `/Submissions/{submissionId}`, edit the pending structured fields, and withdraw that submission; expected: the edit and withdrawal succeed through the live cutoff and history remains. Then create a second live submission from the same board route and note its `{participantSubmissionId}`. Before the separate seeded Captain Rejected-history check, sign in as `SeedAdminTwo` / `SeedAdmin!1234`, open `/Admin/Review?eventId={test15EventId}`, open that specific participant submission, and reject it with a written reason. Return as `SeedEvidenceParticipant`, open the actual rejection notification, and confirm it marks read and resolves to `/Submissions/{participantSubmissionId}`; upload a new image through **Create linked resubmission**. Expected: the participant remains the credited owner, the stored credited-character snapshot is unchanged, the new row links `ResubmissionOfSubmissionId` to the rejected predecessor, and a replay or second direct child is denied. The same account can create only for itself; it cannot select a teammate.
2. **Website Co-captain team scope — TEST 15.** Sign in as `SeedEvidenceCoCaptain`, open `/Submissions?eventId={test15EventId}&teamId={firstTeamId}`, confirm the Captain-only team-focus and team-submission-status sections, submit one screenshot for a current teammate through the existing team-board drawer, and confirm the canonical result/history route. Then request legacy `/Captain?eventId={test15EventId}&teamId={firstTeamId}` and `/Captain/Submit/{tileId}?eventId={test15EventId}&teamId={secondTeamId}`; expected: the first resolves only through the canonical workspace, the second is NotFound/denied for a different team, and neither leaks private evidence or assets. Do this before ending TEST 15.
3. **Admin review — TEST 15.** Sign in as `SeedAdminTwo` / `SeedAdmin!1234`, open `/Admin/Review?eventId={test15EventId}`, approve the seeded Pending row or reject it with a reason, and confirm only the allowed review controls and private notification result. Do not end TEST 15 in this sequence; its cutoff remains future and its live evidence journey must stay available.
4. **Deterministic post-cutoff read-only — TEST 84.** After the reset, sign in as `SeedEvidenceParticipant`, open `/Submissions`, and follow the seeded Rejected history row to `/Submissions/{historyRejectedId}`; expected: the record and asset remain readable, but edit, withdraw, and linked-resubmission controls are absent/denied because TEST 84 is already past its authoritative cutoff. A new `/Captain/Submit/{tileId}?eventId={test84EventId}&teamId={historyTeamId}` request is denied/read-only.
5. **Finalized then Archived history — TEST 84.** Sign in as `SeedAdminTwo`, open `/Admin/Events/Finalize/{test84EventId}`, verify the Finalized official-results/history projection, submit the required confirmation to **Archive event**, then reload the same route. Expected: state is Archived, official result history remains visible, public `/Events/test-84-evidence-history/Board` and tile routes remain historical, and the Rejected evidence is not public. This sequence may be repeated from a fresh reset; do not use TEST 15 for the post-cutoff or archive checks.

- [x] **S8-RM-01** — The exact reset identities, pending/rejected records, live/cutoff ordering, standalone fallback, finalized/archived progression, and expected private/public results above passed in English for the approved reachable surfaces; Danish manual inspection remains deferred to the UI overhaul.

### Slice 8 consolidated manual acceptance — 2026-08-01

- **S8-01 — Historical acceptance superseded only for route ownership.** Participant and Captain history now use the one canonical `/Submissions` workspace and `/Submissions/{id}` detail route; legacy Captain history routes are compatibility aliases only.
- **S8-02 — Passed after correction.** Linked resubmission uses the shared upload interaction, retains success feedback, and Admin review exposes the existing local time plus UTC.
- **S8-03 — Passed after correction.** Co-captain teammate submission retains visible success feedback.
- **S8-04 — Passed.** Emergency authority boundaries behaved as expected.
- **S8-05 — Passed after correction.** Only the selected requirement’s drops are visible/selectable; crafted cross-tile POSTs remain server-rejected.
- **S8-06 — Passed after correction.** TEST 84 post-cutoff history is reachable and read-only through normal navigation.
- **S8-07 — Passed.** Public approved-only/privacy behavior remained correct.
- **S8-08 — Scoped exception.** Manual snapshot-mutation steps were intentionally dropped as unreasonable; public privacy checks remain required and passed.
- **S8-09 — Passed.** Existing automated authority/concurrency boundary coverage remains accepted.
- **S8-10 — Deferred by approved boundary.** Danish manual inspection remains deferred to the UI overhaul; existing localization behavior and tests remain preserved.

### Final manual correction notes

The final correction retest passed in Safari after replacing `optgroup.options` with `group.querySelectorAll("option")`; initial server filtering, native requirement-change synchronization, and cross-tile server rejection all remain accepted. Slice 8 is accepted after the independent implementation review, all restricted re-reviews, final scope-delta review, and consolidated manual acceptance passed. The remaining Slice 8 packaging/worktree note is historical traceability; the current Slice 9 and Slice 10 records follow below.

- [x] Participant self-submit/edit/withdraw and Captain/Co-captain teammate-submit use server-derived credited character; no account selector or cross-team/private-history leakage.
- [x] Rejected-only linked resubmission uses a new image, copies credited snapshots, preserves predecessor history, blocks replay/stale writes, and remains cutoff-bound.
- [x] Admin Pending review exposes only reasonless Approve or reasoned Reject; rejection reaches linked credited participant/current linked Captain/Co-captains once, with unlinked fallback to eligible leadership.
- [x] Admin reasoned metadata correction derives participant from the selected Playing character and preserves submission time, weight, contribution, and active asset.
- [x] Approved and Archived public/tile/board evidence shows only active Approved evidence and stored credited-character snapshots; no private notes/reasons/authority data appear publicly or in realtime payloads.
- [x] Only Pending evidence blocks finalization; Approved reversal preserves exact deduction/rebalancing/history.
- [x] Deprecated Admin upload, Request Changes, same-record resubmit, active duplicate, privacy/visibility controls, hidden placeholders, routes, and controls are absent; private non-Approved evidence remains scoped, linked Rejected resubmission and protected drawer/standalone fallback remain.

### Slice 8 final automated acceptance — 2026-08-02

Domain `156/156`, Application `83/83`, Browser `67/67`, and Integration `230/230` passed with zero failures/skips (`536/536` combined). Durable final Integration TRX: `/private/tmp/slice8-final-automated-gates-20260801/integration-rerun.trx`. Release build, formatting, EF pending-model, `git diff --check`, clean/retained migration/preflight, Development double-reset, parity, artifact/secret scan, and staged-state checks passed; no required gate remains unverified.

## Slice 9 — Event end, live replacement, finalization, notifications, and history

- [x] Consolidated manual cases finalized for the Slice 9 handoff.
- [x] Scheduled/early end, upload grace, cutoff, and UTC screenshot review.
- [x] Withdrawal, vacancy, optional replacement, and captain-warning paths.
- [x] Blocker resolution, finalization/unfinalization, archive, and participant read-only history.
- [x] Personal notification versus unresolved-action behavior.

### Slice 9 consolidated manual checklist — Passes 9.1–9.3

Development reset: `dotnet run --project src/Bingo.Web -- --reset-test-data` after migrations. Reset is idempotent and recreates the named workflow fixtures while removing outdated or manually created disposable events. Finalized fixtures intended to expose rosters have one active frozen publication cycle; the explicit missing-playing-assignment blocker remains unpublished. The linked `SeedReplacement` waiting-list participant belongs to `Vinterbingo 2026` (`test-15-dkl-live`). Use `SeedAdminTwo` / `SeedAdmin!1234` for Admin actions, `SeedReplacement` / `SeedReplacement!1234` for the linked replacement, `SeedEvidenceCaptain` / `SeedEvidence!1234`, `SeedEvidenceCoCaptain` / `SeedEvidenceCoCaptain!1234`, and `SeedEvidenceParticipant` / `SeedEvidenceParticipant!1234` for the existing test-15 evidence journeys. Use the reset/bootstrap owner credentials where the existing Admin route requires the owner identity. If a retained database fails the final-review migration preflight, record the reported event and official-snapshot IDs, reconcile the retained transition history from backup/operational records, and rerun migrations before attempting reset; do not delete the database to bypass the preflight.
The user externally completed two post-correction resets against the recreated local Development database through the Release `--no-build` path, both with exit code `0`. Direct inventory confirms the five approved events, including the minimal TEST 16 signup-lookup fixture, and complete DKL/Slice 9 fixture relationships described below; no unrelated manual/test events remain.

Approved DKL fixture inventory, recreated on every reset:

- `TEST 13 — DKL Board` / `test-13-dkl-board`: `SignupClosed`, `Europe/Copenhagen`, future schedule, 20-person cap, 5×5 `DKL comparison board` in `Draft`, no teams or waiting-list rows, and no publication pointer. Use `/Events/test-13-dkl-board/Board` and the Admin board/editor routes for private derivation, approval, and preview checks.
- `Vinterbingo 2026` / `test-15-dkl-live`: `Live`, started before reset with a future scheduled end and normal +30-minute cutoff, 60 confirmed drafted participants across six named teams of ten, finalized draft with 48 picks, one active frozen roster publication cycle, six enabled Captain authorities plus the disabled emergency coverage account, published 5×5 DKL board/approval snapshots, approved progress, one Pending and one Rejected review fixture, and one linked `SeedReplacement` waiting participant with a frozen Playing account. Use `/Events/test-15-dkl-live/Teams`, `/Events/test-15-dkl-live/Board`, `/Admin/Events/Participant/{eventId}/Participants/{participantId}`, `/Admin/Events/Manage/{eventId}`, `/Admin/Review`, and canonical `/Submissions` plus legacy `/Captain` alias routes for the Slice 9 journeys.

Required routes: `/Admin/Events`, `/Admin/Events/Manage/{eventId}`, `/Admin/Events/Finalize/{eventId}`, `/Admin/Events/Participant/{eventId}/Participants/{participantId}`, `/Admin/Review`, `/notifications`, `/Account/MyEvents`, `/Events/{slug}/Board`, and `/Evidence/{id}`.

- [x] TEST 15: exercise the due/premature-end path, confirmed reasoned resume with a future replacement end, stale/repeated recovery rejection, and the second authoritative end; confirm ordinary uploads/cutoffs and emergency access are not silently reopened.
- [x] TEST 15: withdraw a live participant, leave the vacancy open, promote `SeedReplacement`, verify prospective membership/authority/history, and mark the waiting-list follow-up complete. Confirm notification reading does not resolve the Admin action.
- [x] Final review: use a no-completed-team event to confirm no completion acknowledgement is required; use completed teams to confirm each team requires **Completion time inspected**. Verify reasoned correction, reinspection after correction, and strongly confirmed/reasoned exceptional override.
- [x] Finalize, unfinalize with confirmation/reason, re-finalize, and archive through `/Admin/Events/Finalize/{eventId}`. Confirm both official snapshot cycles/resolutions remain visible, results are deterministic, and uploads do not reopen.
- [x] Finalization: submit the normal finalization POST without the bound confirmation value and confirm it is denied; submit the checked confirmation and confirm official publication succeeds. Review a resumed-cycle submission timestamp in the final-review gap and confirm the Admin review page identifies it as outside an eligible live interval.
- [x] TEST 84: archive the seeded finalized event, open `/Account/MyEvents`, follow the archived evidence-history link, and confirm the former participant sees only their own rejected/withdrawn history read-only. Confirm another participant, team-private evidence, submission, edit, review, and public rejected evidence remain unavailable.
- [x] As Admin, inspect `/notifications`: personal unread count/read state and Admin action count/list remain separate. Confirm direct destinations for pending review, waiting follow-up, postponed start, vacancy, and missing Captain appear only while authoritative work is unresolved.
- [x] Follow the waiting-list action link after the vacancy is filled and confirm the promoted participant route still renders the explicit Mark follow-up complete form; complete it once and repeat the stale/idempotent POST.

This checklist is the compact Slice 9 manual handoff. S9-01 through S9-07 passed; S9-05 preserved placements, metrics, and Version 1 history after unchanged TEST 84 re-finalization. S9-06 accepted the owner-rendered archived evidence link, current raw-image display, and unrelated/anonymous privacy boundaries; viewer presentation is deferred to the UI overhaul. Consolidated Slice 9 manual acceptance and final automated verification are approved; Slice 10 was not started.

## Slice 10 — Wise Old Man integration

Reset Development data after applying migrations. The reset is idempotent and makes no Wise Old Man request:

```bash
dotnet run --project src/Bingo.Web -- --reset-test-data
```

Use `SeedAdminTwo` / `SeedAdmin!1234` for the Development journeys. After reset, TEST 15 (`/Admin/Events` → follow the rendered **TEST 15 — DKL Live** Manage link) is `Live` with local competition `1515`, a complete cached generation fetched three hours ago and therefore due for manual refresh. TEST 16 (`test-16-signup-lookup`) is `SignupOpen` with an empty signup roster. `SeedAdminTwo` has linked fictional `Dev Lookup Player` with saved EHB `12.5`; this is the deterministic My Accounts and signup/edit lookup character. The reset seeds TEST 15 with fictional `Dev Player 001` plus current regular `Dev Activity Secondary` and never contacts Wise Old Man.

In Development, the configured `WiseOldMan:DevelopmentFake` transport handles all WoM routes without network access. Override these settings before starting the app to exercise the named controls, then reset again:

```text
WiseOldMan__DevelopmentFake__AutomaticSynchronizationEnabled=true|false
WiseOldMan__DevelopmentFake__PlayerMode=Success|NotFound|Unavailable|RateLimited
WiseOldMan__DevelopmentFake__CompetitionMode=Complete|Incomplete|Unavailable|RateLimited
WiseOldMan__DevelopmentFake__TemporaryFailures=0..3
WiseOldMan__DevelopmentFake__Remaining=19 (use 3 to prove the final-three reserve)
WiseOldMan__DevelopmentFake__RetryAfterSeconds=30
```

The fake serves `GET /players/{username}` and official-shape `GET /competitions/1515?metric=ehb`; it never exposes or calls WoM write endpoints. Set `AutomaticSynchronizationEnabled=false` and restart the Development app for manual lookup, due-refresh, and partial/recovery checks; this prevents the worker from consuming the deterministic due state. Set it back to `true` only for the lifecycle stop/resume check. The control is ignored outside Development. Return to `Success`/`Complete`, `Remaining=19`, and reset before the final cached-projection check.

- [x] **S10-01 — Cached public/team projection.** With `AutomaticSynchronizationEnabled=false`, `PlayerMode=Success`, `CompetitionMode=Complete`, and `Remaining=19`, reset and start the app. As `SeedAdminTwo`, open `/Account/MyAccounts` and use **Fetch from Wise Old Man** for `Dev Lookup Player`; confirm the saved EHB becomes `12.5`. Open `/Events/test-16-signup-lookup/Signup`, select `Dev Lookup Player` as the regular account, use its explicit **Fetch from Wise Old Man**, submit the valid signup, and open the rendered Edit action. Restart with `PlayerMode=Unavailable`, use the Edit page’s explicit fetch to show failure feedback, then submit a manually edited valid signup and confirm the optional WoM failure does not block it. Finally use anonymous `/Events/test-15-dkl-live/Board?view=leaderboards&ranking=activity` and `/Events/test-15-dkl-live/Board/touch-kids-not-grass`: confirm `Fetched from Wise Old Man`, team totals, participant-based average, tied MVPs, and both `Dev Player 001` / `Dev Activity Secondary` account rows. No page render makes a WoM request. **Passed.** The successful action at step 8 had no visible feedback; cached/team EHB is functionally correct, but its presentation is visually broken and deferred to the UI overhaul.
- [x] **S10-02 — Bootstrap, reserve, and known retry feedback.** Restart with `PlayerMode=Success`, `Remaining=3`, and automatic synchronization disabled. The first explicit My Accounts fetch observes the final-three reserve; the next explicit fetch is rejected locally, makes no external request, and displays the known reset time. Restart with `PlayerMode=RateLimited`, `Remaining=19`, `RetryAfterSeconds=30`, and automatic synchronization disabled; use the explicit My Accounts fetch and confirm localized failure feedback includes the known retry time. Restore `PlayerMode=Success`. **Passed.** Development fake acceptance of arbitrary non-missing names in Success mode is deterministic test behavior; production remains authoritative to real WoM.
- [x] **S10-03 — Due manual refresh and partial recovery.** Reset with automatic synchronization disabled. Sign in as `SeedAdminTwo`, follow the rendered TEST 15 Manage link, and use **Refresh cached activity**. Confirm success (the seeded cache is due). On the same Development-only Manage page, click **Make next refresh due (Development TEST 15)** and confirm the due-state success feedback. Set `CompetitionMode=Incomplete`, restart, follow the rendered TEST 15 Manage link again, and use **Refresh cached activity**: the configured fake omits `Dev Activity Secondary`; Manage shows that exact missing current Playing account name, while public Board/TeamBoard show fresh matched values, a provisional/partial warning, missing-count and participant/account coverage, never the missing name. Confirm no missing account is displayed as zero and no prior-generation value is carried forward. Click **Make next refresh due (Development TEST 15)** again, set `CompetitionMode=Complete`, restart, follow the rendered link, and use **Refresh cached activity** once more; confirm complete rankings recover. The Development-only due control prepares TEST 15’s persisted due/manual-cooldown admission state, including the authoritative successful-time/cooldown facts and normal due time; it is safe to repeat and does not bypass the normal production cooldown/retry rules. A failed refresh is an error, not a success, and its retry/reset time is shown when known. **Passed after the Live competition-synchronization capability correction.** The Admin manual **Refresh cached activity** feature is explicitly approved and remains in scope.
- [x] **S10-04 — Lifecycle stop and legitimate resume.** Reset with `AutomaticSynchronizationEnabled=true`, `CompetitionMode=Unavailable`, `TemporaryFailures=3`, and `Remaining=19`. The first automatic fetch after an event’s initial transition into `Live` is scheduled two hours after that transition; later successful normal fetches remain two hours apart. Wait for the worker’s first attempted refresh and note the private retry time/count on Manage. End TEST 15 through its existing Manage **End event** action and confirmation; after the retry becomes due, confirm the synchronization attempt timestamp/count does not change while the event is outside `Live`, while its cached public/team state remains readable. Use Manage’s **Resume event** action with a future replacement end and a written reason, confirm the event returns to `Live`, and wait for the next due retry: a future persisted due time remains future, while an overdue persisted due time permits one prompt fetch and then receives a new two-hour normal anchor. Lifecycle switching itself creates no extra fetch. No lifecycle/readiness action depends on WoM availability. **Passed.**
- [x] **S10-05 — Retained state after event end.** With a complete or partial cache, end TEST 15 through Manage and reload the public Board and team route. Confirm the last cached timestamp and the retained complete or provisional available rankings remain visible without refreshing; if the latest generation has zero matched accounts, rankings remain hidden. Do not use a direct URL to bypass the rendered Manage navigation. **Passed.** Screenshots confirmed stale cached activity, timestamp, totals/ranks/coverage after event end; the visual table layout is deferred to the UI overhaul.
- [x] **S10-06 — Final reset/reachability.** Restore `AutomaticSynchronizationEnabled=false`, `PlayerMode=Success`, `CompetitionMode=Complete`, `TemporaryFailures=0`, `Remaining=19`, reset once more, and confirm TEST 16 is still `SignupOpen`/empty, TEST 15 is `Live` with due local cache, `/Account/MyAccounts`, `/Events/test-16-signup-lookup/Signup`, the rendered TEST 15 Manage link, and both cached public routes are reachable. No real WoM request is permitted at any step. **Passed; the Development fake prevented real WoM calls.**

**Slice 10 consolidated manual acceptance (2026-08-03):** S10-01 through S10-06 passed. The two S10-01 presentation notes and S10-05 table-layout note are deferred to the UI overhaul and are not functional blockers.

## Historical event import — local operator acceptance

**Accepted 2026-09-01.** The corrected one-time import of `Det Store Danske
Sommerbingo 2026` was applied only to the local Development database. The user
manually inspected the resulting archived event and reported that everything
looks good. The archived public result preserves the approved six-team roster,
exact 402 counter units across 150 team/tile cells, itemless reconstructed
approved rows in Recent Drops without fabricated drops or evidence,
within-team EHB-weighted attribution/timing, the exact historical disclosure,
the complete source WoM snapshot without normal refresh, and the approved
Maggot King and Superior Slayer rules.

The reviewed public manifest SHA-256 is
`e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438`. Apply is
preflight-first and exact-hash idempotent; divergent input fails closed and
transaction failure leaves no partial event. The named active SuperAdmin and
exact event-name confirmation are validated inside the locked serializable
apply transaction.

- [x] Independent Sol High closure review: the three additional blockers
  (Production CLI reachability, reviewed public-manifest pinning, and active
  SuperAdmin authorization inside the locked serializable apply transaction)
  were remediated before local commit
  `c4130f437b82cf5ceb5f130cf8a77a3a05ae2079` (`Add historical 2026 event import`).
- [x] Focused verification passed: `HistoricalBoardReferenceTests` 25/25;
  `Bingo.IntegrationTests` Release compilation with 0 warnings/errors;
  `HistoricalImportIntegrationTests.AppliesFictionalOperatorInputAndExactRerunIsNoOp`
  1/1 using isolated Docker/Testcontainers; and
  `Slice10Pass103ActivityProjectionTests.DevelopmentTest15DueControlIsIdempotentAndResetRemainsCachedOnly`
  1/1 using isolated Docker/Testcontainers.
- [ ] Production import, rehearsal-event hide/removal, push, deployment, and
  production migration remain separate explicitly authorized operations and
  were not performed.

## Milestone 9 — Complete UI overhaul and full regression

This section is the remaining final UI and whole-application regression gate after the version-one functional foundation. Its order and release gates are owned by `DELIVERY_PLAN.md`, and page approval is owned by `UI_PAGE_MATRIX.md`.

- [ ] Apply the approved site-wide visual rules to every public, participant, captain, Admin, Super-Admin, error, privacy, and empty state.
- [ ] Repeat every completed Slice 1–10 manual checklist against the final interface.
- [ ] Verify current Safari and Chromium on desktop, intermediate/tablet, and representative mobile widths.
- [ ] Verify keyboard-only use, focus order/visibility, screen-reader labels, reduced motion, contrast, validation summaries, and no-JavaScript fallbacks.
- [ ] Verify all permission, empty, loading, success, warning, error, conflict, and stale-state presentations.
- [ ] Verify the shared masthead notification and account popups in light/dark desktop and constrained widths: visible trigger text, viewport containment/z-index, keyboard/focus order, outside-click and Escape close, focus return, and clean reopen.
- [ ] Verify `/Submissions` exposes the signed-in current team member's complete retained team ledger, including departed credited members, and normal authenticated navigation reaches it. From the team-specific Board main progress sidebar, all authorized current members use the canonical `Team history` button to `/Submissions`; Captains/co-captains additionally see the team-focus and team-submission-status sections. Pending/rejected mutations are owner-only for ordinary participants; eligible Captains/co-captains retain broader server-authorized editing; teammate-owned and all other states remain read-only for ordinary participants.
- [ ] Open an evidence rejection notification as the credited participant and as an eligible Captain/co-captain recipient. Both must mark the notification read and resolve to the same canonical stored route `/Submissions/{id}`; relevant general submission navigation resolves to `/Submissions`, Admin review notifications remain `/Admin/Review/Details/{id}`, and no route exposes another team's private evidence.
- [ ] Confirm no critical/high defect remains before the production-preparation milestone.
