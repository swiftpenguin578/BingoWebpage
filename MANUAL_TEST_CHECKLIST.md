# Manual Test Checklist

**Status:** Planning Pass 2 framework; exact routes, accounts, seed prerequisites, and expected values are completed with each implementation slice before handoff.

**Last updated:** 2026-07-25

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

- [ ] Exact manual cases to be finalized before Slice 2 handoff.
- [ ] Shared/borrowed-character and event-uniqueness cases.
- [ ] Link/unlink/preferred/rename and historical-preservation cases.
- [ ] Migration/backfill inspection.

## Slice 3 — Event creation, scheduling, readiness, cancellation, archive, and current-event policy

- [ ] Exact manual cases to be finalized before Slice 3 handoff.
- [ ] Minimal draft, manual/scheduled opening, and readiness blockers.
- [ ] Empty discard versus populated cancellation.
- [ ] Production singleton behavior and Development multi-scenario exception.
- [ ] Finalized archive and historical-route preservation.

## Slice 4 — Signup, EHB/account questions, public signup board, capacity, and participants

- [ ] Exact manual cases to be finalized before Slice 4 handoff.
- [ ] Confirmed/waiting-list creation, collision rollback, edit, withdrawal, rejoin, and promotion.
- [ ] Public/admin answer visibility and optional historical answers.
- [ ] Participant administration, payment, account correction, notification, and restoration.

## Slice 5 — Teams, captain roles, external teams, and draft

- [ ] Exact manual cases to be finalized before Slice 5 handoff.
- [ ] Derived roster distribution, captain balancing, picks, repeated undo, pause/resume, and finalization.
- [ ] External/pre-formed team and post-finalization correction paths.
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
