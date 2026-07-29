# Slice 1 — Identity and Account Foundation Implementation Plan

**Status:** Implementation and acceptance gates complete; final independent restricted re-review cleared Slice 1 for commit and push on 2026-07-26.

**Prepared:** 2026-07-24

**Last reviewed:** 2026-07-25

**Depends on:** `PRODUCT_REQUIREMENTS.md`, `FUNCTIONAL_WORKFLOWS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`, `IMPLEMENTATION_ROADMAP.md`

## 1. Objective and boundary

Slice 1 establishes the durable website-account and authentication foundation:

- normal website accounts with `USER`, `ADMIN`, or sole `SUPER_ADMIN` global roles;
- public-username/password and Discord authentication into the same account;
- Discord-first onboarding;
- password changes and administrator-generated reset links;
- Discord link, unlink, and replacement;
- immediate session invalidation after security-sensitive changes;
- global-role grant/revoke and atomic Super-Admin ownership transfer;
- website-account disable and restore;
- sanitized account administration and 25-row filtered audit history;
- preservation of existing generated captain logins as disabled-by-default emergency credentials.

Slice 1 does **not** connect normal accounts to event participants or replace the signup workflow. Those changes depend on Slice 2 account/character migration and Slice 4 signup work.

One narrow Slice 2 foundation must be brought forward: Discord onboarding must create the initial `OsrsCharacter` and preferred `AccountOsrsCharacter` link in the same transaction as the public username and password. Slice 1 therefore creates the minimal character/link entities and constraints needed for onboarding. Full My accounts management, additional/borrowed characters, personal labels/order/EHB editing, event assignments, and swaps remain later slices. Slice 2 deliberately adds no participant claim-link system.

## 2. Evidence classification

### Verified current behavior

- `Account` currently represents only permanent Admin credentials and event/team-scoped Captain credentials.
- `AccountRole` contains only `Admin` and `Captain`.
- Passwords use ASP.NET Core Identity's `PasswordHasher<Account>`.
- Password login uses one secure cookie and a single fixed-window rate limiter.
- Cookie validation checks only whether the account exists and whether its current time-based access mode is disabled.
- The cookie contains account ID, username, role, event/team claims, and the forced-password-change flag. It has no authentication-method, authorization-version, or password-version claim.
- Development setup/bootstrap creates the first `Admin`; it has no Super-Admin concept.
- The account-management page lets any Admin create either Admin or Captain credentials, choose a replacement password, disable any other account, and re-enable it.
- Password change signs out the current cookie but does not invalidate other sessions through a stored version.
- There is no Discord OAuth handler, Discord account entity/state, onboarding route, account-settings Discord workflow, Forgot password page, reset-token model, or Reset password page.
- Event participants contain optional free-text `DiscordIdentity` and OSRS-name fields but no website-account owner ID.
- Finalized captain/co-captain roles generate separate password accounts and later disable those accounts when the role is removed.
- Existing audit writes are immutable rows, but security mutations commonly save the target first and write the audit row in a second save without an enclosing transaction.
- The audit page filters only action/actor and returns the newest 250 entries without pagination.
- Existing account IDs are referenced by competitive and historical records, including event creation/transitions, board/draft control, evidence upload/submission/review, finalization, and audit entries.
- Most of those actor/reference columns are scalar IDs without database foreign keys to `accounts`; preserving account IDs is therefore essential even though deletion is not currently exposed.

### Inferred implementation consequences

- Replacing the existing `accounts` table or assigning new IDs would break historical actor attribution. The migration must transform rows in place.
- `AccountRole` currently conflates global authorization and credential type. The target requires separate `AccountType` and `GlobalRole` concepts.
- Existing Captain rows must move their event/team/time scope into `AccountEventAccess`; keeping that scope on normal website accounts would preserve the wrong authority model.
- Current free-text participant Discord names cannot be migrated into Discord login identities because they are not stable Discord user IDs.
- A complete Slice 1 needs minimal OSRS character/link persistence even though most character functionality belongs to Slice 2.
- Global login identifiers for website and emergency accounts need one collision boundary. Two separate unique indexes would allow ambiguous password-login lookups.

### Unverified external inputs

- Discord application client ID/secret and environment-specific callback URLs do not exist in repository configuration.
- The existing account that should become the first Super Admin must be selected explicitly for each retained database.

The OSRS-name handling decision is resolved: trust the user to enter the exact name, show that responsibility in the UI, trim surrounding whitespace, and compare case-insensitively for uniqueness. Do not validate OSRS syntax, availability, ownership, Wise Old Man membership, or existence, and do not rewrite internal spelling/spacing.

## 3. Gap matrix

| Capability | Current implementation | Required Slice 1 result |
| --- | --- | --- |
| Normal website account | Absent | `WEBSITE_ACCOUNT` with public username/password and optional Discord association |
| Initial creation | Development-only first-Admin form | Discord callback followed by atomic public-profile/password/first-character onboarding |
| Password login | Admin/Captain only | Generic login for every completed website account plus emergency credentials |
| Discord login | Absent | Returning Discord ID resolves the same website account; no guild-membership requirement |
| Discord relinking | Absent | Password-confirmed `LINK`, `UNLINK`, and atomic `REPLACE` with transition history |
| Session invalidation | Disabled-state lookup only | Authentication method plus authorization/password versions checked against the database |
| Login throttling | One global fixed window | Independent normalized-identifier and network partitions with generic responses |
| Password change | Rehash and sign out current cookie | Increment password version and reissue/reject sessions according to authentication method |
| Forgot/reset password | Admin chooses a replacement password | Generic contact-admin page and hashed, expiring, superseding, single-use reset links |
| Global roles | `Admin` only | `USER`, `ADMIN`, and exactly one active `SUPER_ADMIN` on normal website accounts |
| Role administration | Any Admin can create another Admin | Only Super Admin grants/revokes Admin and atomically transfers ownership |
| Disable/restore | Any Admin can mutate any non-self account | Admin → User only; Super Admin → Admin; no self/current-owner disable; reason only for disable |
| Captain identity | Generated credential is normal captain identity | Normal role later derives from website participant/team; current credentials become explicit emergency accounts |
| Account overview | Credential rows with raw event/team IDs | Sanitized website identity, role, link status, login, participation, and current/history projection |
| Audit log | Newest 250, actor/action filters | Newest 25 per page with event/actor/action/entity/date filters and full retained traversal |

## 4. Recommended target model

### 4.1 Account aggregate

Replace the role/time-scope shape of `Account` with:

- `AccountType`: `WebsiteAccount` or `EmergencyCaptain`;
- `GlobalRole`: nullable for emergency credentials; `User`, `Admin`, or `SuperAdmin` for website accounts;
- `PublicUsername` and normalized public value for website accounts;
- `EmergencyLoginUsername` for emergency credentials;
- one unique normalized login identifier shared by both account types so login is never ambiguous;
- `DiscordUserId`, nullable and unique, plus non-authoritative display metadata;
- `PasswordHash`, nullable only for a disabled emergency credential awaiting initial setup, plus `PasswordChangedAt` and `MustChangePassword`;
- `AuthorizationVersion` and `PasswordVersion`;
- `Active`, current disabled time/actor/reason;
- `OnboardingCompletedAt`, nullable only during a controlled incomplete onboarding/migration state;
- `ProfileOsrsCharacterId`, populated when onboarding completes;
- `CreatedAt`, `LastLoginAt`, and an optimistic concurrency version.

Use the existing account IDs. Do not adopt the full ASP.NET Core Identity database schema solely for this slice; the existing aggregate plus the platform password hasher and cookie/OAuth middleware satisfies the approved architecture with less migration risk.

### 4.2 Minimal onboarding character foundation

Create:

- `OsrsCharacter`: ID, current display name, normalized name, timestamps;
- `AccountOsrsCharacter`: account ID, character ID, active/preferred flags, position, timestamps.

Required constraints:

- one normalized global `OsrsCharacter` row per character name;
- unique account/character link;
- at most one active preferred link per account;
- profile character must be an active link of the same account, revalidated by the onboarding command.

Labels, reorder commands, additional links, removal, event assignments, EHB, and swaps remain Slice 2.

### 4.3 Security and history records

Create:

- purpose-bound password setup/reset tokens as specified in `DATA_MODEL.md`;
- `AccountDiscordIdentityTransition`;
- `AccountEventAccess` for migrated and newly created emergency credentials;
- structured role/disable/ownership before-and-after audit entries, with the account aggregate retaining only current state.

Extend audit-query data with nullable `EventId`, structured entity identity, and before/after JSON where needed. Existing rows remain immutable. Backfill an event ID only where it can be determined safely from the recorded target; otherwise leave it null rather than infer.

### 4.4 Database invariants

- unique normalized login identifier across both account types;
- unique non-null Discord user ID;
- partial unique index allowing at most one `SUPER_ADMIN`;
- application/operator bootstrap gate requiring exactly one active Super Admin before normal operation;
- emergency accounts cannot hold a global role;
- website accounts cannot contain event/team credential scope;
- password-credential token hash unique;
- delete restrictions on security/history relationships;
- account concurrency token for role, disable, identity-link, password, and ownership races.

PostgreSQL cannot express every cross-column account-type invariant cleanly through EF conventions. Use explicit check constraints where suitable and application/domain validation plus transactional locking for the ownership and active-link invariants.

## 5. Authentication and authorization design

### 5.1 Cookie schemes

Use:

- the existing primary secure application cookie;
- a short-lived external OAuth cookie for the Discord handshake.

An ordinary, non-persistent sign-in lasts until browser-session end with a maximum authentication-ticket lifetime of 12 hours. **Remember me** creates a persistent session with a 30-day absolute maximum; activity cannot extend it indefinitely. Both remain subject to account/version validation on every request.

The primary principal carries account ID, current public/login display name, global role when present, authentication method, authorization version, and password version only for password-authenticated sessions. Event/team authority is queried from authoritative participation/access records rather than stored as durable role claims.

`AccountCookieEvents` rejects a principal when:

- the account is absent or inactive;
- its authorization version differs;
- a password-authenticated principal's password version differs;
- the account type or current login eligibility no longer matches.

Role claims must be rebuilt from current account state at sign-in. Version-changing mutations either reject all old cookies or reissue the current browser explicitly, as required by the approved workflow.

### 5.2 Discord OAuth

Use ASP.NET Core's generic OAuth handler configured for Discord:

- Authorization and token endpoints are external configuration, not event settings.
- Request only the minimum identity scope needed to read the stable Discord user ID and display metadata.
- Do not persist access/refresh tokens.
- Protect correlation/state through the external cookie and middleware.
- A returning callback looks up the unique Discord ID and signs into the existing account.
- A first-time callback stores only protected onboarding state with a 15-minute lifetime; the account is not committed until public username, password, character, preferred link, and onboarding completion can commit atomically.
- Link/replace callbacks contain a protected purpose and initiating account reference; they never fall through to account creation.
- Provider failure or cancellation returns accurate retry feedback without creating or mutating an account.

### 5.3 Password and reset behavior

- Require at least 10 characters and keep the existing 200-character maximum. Accept passphrases/printable characters and add no composition or periodic-expiry rule.
- Keep platform password hashing and rehash-on-login.
- Partition password-login throttles by normalized identifier and network address, without logging raw passwords. Exact thresholds are deployment-configurable technical controls rather than product behavior.
- Forgot password is a static contact-an-admin page with no public username/account lookup form.
- Reset-link generation checks target role authority, stores only a token hash, expires it after 60 minutes, supersedes prior unused tokens, and exposes the raw link once.
- Reset completion locks the account/token, validates unused/unexpired/not-superseded state, writes the new hash, increments password version, consumes the token, and records history atomically.
- Admins never see or choose the replacement password.
- Password change/reset invalidates prior password-authenticated sessions without invalidating a separately Discord-authenticated session. Ordinary password change reissues the current browser's password session.

### 5.4 Role, account-state, and emergency-credential authorization

Implement application/domain commands, not Razor-only checks, for:

- grant/revoke Admin;
- transfer Super Admin;
- disable/restore website account;
- generate/consume reset link;
- change password;
- complete onboarding;
- link/unlink/replace Discord;
- create/enable/disable/reset emergency credential.

Every mutation rechecks the actor and target inside its transaction. Audit writes occur before the same transaction commits.

Role grant/revoke uses a dedicated confirmation view showing the target and before/after role; neither typed username nor written reason is required. Revoke returns the target to User. Ownership transfer additionally requires the current owner's password and typed destination username. Each global-role mutation invalidates affected sessions. Disable preserves the reserved username and Discord association; version one has no website-account merge or permanent deletion.

Emergency credential creation is available to any enabled Admin, creates one disabled individual credential for one event/team, and permits several separate credentials on the same team. A globally unique login username is chosen at creation, but the Admin never chooses or sees the lasting password. A hashed, purpose-bound setup link expires after 60 minutes and lets the intended captain choose it. Setup leaves the credential disabled; enablement is separate and requires initialized credentials. Reset supersedes prior setup/reset links and repeats that flow.

### 5.5 Account overview, audit, and operator recovery

- One Admin **Accounts** area has separate website-account and emergency-credential datasets; exact tabs/sections remain a UI-layout choice.
- Both datasets use server-side search/filters and 25-row pagination.
- Website rows expose username, role, state, Discord link state, last login, and current event-role summary. Details expose every linked OSRS character, non-authoritative last-known Discord display name, event participation, current/historical event-team roles, and disable history.
- Emergency rows expose username, event/team, setup/enabled/cutoff state, last login, and only authorized setup/reset/enable/disable actions.
- Ordinary Admin and Super-Admin action projections follow the approved authority matrix and are revalidated by commands.
- Audit entries use 25-row filtered pagination and readable structured detail. Routine successful website login updates last login only; failed/throttled login uses security logging; emergency success and security-sensitive mutations remain durable audit events.
- Grant/revoke Admin and restore create an in-site notification. Disable exposes neutral contact-an-admin guidance and keeps the reason private.
- Operator-only owner recovery may generate a 60-minute owner reset link or atomically transfer ownership using explicit identifiers and confirmation; no web action exists.

## 6. Migration and backfill plan

### Stage A — Preflight, no mutation

Produce a diagnostic command/report that:

- lists every existing Admin and Captain account ID/username/access state;
- identifies normalized login collisions;
- lists legacy Admin usernames exactly as stored so the operator can deliberately retain or replace each public value without an external syntax lookup;
- verifies every Captain row has one consistent event, team, participant, and active captain/co-captain membership;
- reports orphaned historical account references;
- for a retained database, requires an explicit existing active Admin ID/username for initial Super Admin ownership;
- refuses to continue on ambiguity instead of choosing an owner or identity automatically.

### Stage B — Expand schema and preserve IDs

One generated EF Core migration, designer, and model snapshot should:

1. Add new account-type, global-role, identity, version, status, and concurrency columns initially nullable where backfill requires it.
2. Create minimal character/link, reset-token, Discord-transition, and event-access tables.
3. Copy existing Admin usernames/password hashes into website-account fields while preserving account IDs, login history, disabled state, and forced-password-change state.
4. Map existing Captain rows to `EMERGENCY_CAPTAIN`, retaining their credential username/password and moving event/team/participant/time scope into `AccountEventAccess`.
5. Derive a migrated emergency access role only from the matching authoritative membership found by preflight.
6. Leave legacy participant `DiscordIdentity` strings untouched and unlinked.
7. Add indexes/check constraints only after the rows are valid.
8. Remove obsolete account event/team/captain columns only after access-row verification.

Migrated emergency credentials are disabled by default. Development scenarios explicitly enable only the credentials required for the scenario being tested.

### Deployment data policy

The retained-data migration path exists for safe local upgrades, automated migration coverage, and any future environment where history must genuinely be retained. It is not the initial production-data transfer plan.

Initial production deployment starts from a clean migrated schema. Test/development website accounts, generated captain credentials, events, signups, participants, teams, boards, evidence, notifications, and audit history are disposable and must not be copied into production. The only application dataset intentionally carried forward is the reviewed OSRS catalogue snapshot from `src/Bingo.Web/data/osrs-catalogue.json`. Catalogue image cache files may be rebuilt from that snapshot's authoritative source URLs.

### Stage C — Controlled owner bootstrap

The schema constraint enforces at most one Super Admin. For a retained database, an operator command promotes the preflight-selected existing website account and records the initial ownership transition. For a clean production database, controlled operator setup creates/provisions the intended owner after migrations and catalogue initialization. Neither path chooses the first or oldest account, and neither is exposed through public onboarding.

Production readiness and privileged application startup fail closed while the database has zero or multiple active owners.

Development setup/bootstrap creates or promotes a clearly configured local Super Admin only in Development and never exposes first-owner election to public onboarding.

### Stage D — Cutover verification

Exercise both launch modes:

- a retained-data copy validates the in-place migration and historical-reference guarantees; and
- a clean production-like PostgreSQL database validates that migrations create no test workflow/account data, the reviewed catalogue snapshot is initialized, and controlled owner provisioning produces exactly one Super Admin.

For the retained-data path:

- compare account IDs and historical actor references before/after;
- verify all Admin password hashes still authenticate;
- verify chosen ownership and role counts;
- verify every legacy Captain became a disabled emergency credential with the correct scope;
- confirm no Discord identity was inferred from free text;
- exercise rollback using the documented database backup rather than relying on a lossy EF `Down` migration after credential/schema cutover.

## 7. Implementation order within Slice 1

1. Add domain enums/entities/invariants and unit tests.
2. Add persistence mappings and the preflight report.
3. Generate the expand/backfill/contract migration and controlled Super-Admin bootstrap command.
4. Add primary/external cookie configuration, versioned principals, partitioned throttling, and password authentication.
5. Add Discord returning-login and atomic onboarding.
6. Add account settings for password and Discord link/unlink/replace.
7. Add reset-link generation and completion.
8. Add Super-Admin role/ownership commands and website-account disable/restore.
9. Replace the legacy account pages with the sanitized overview and correctly authorized actions; convert credential creation to emergency-only.
10. Implement 25-row audit pagination/filters and structured security history.
11. Convert captain provisioning/finalization/seed behavior to `AccountEventAccess` emergency credentials without yet granting normal participant-based captain access.
12. Run migration verification, focused regression, complete automated checks required by the repository, and the finalized manual cases.

Do not begin broad cross-site UI polishing here. Every new page must nevertheless use the established shared visual system, controls, spacing, feedback, responsive, accessibility, and progressive-enhancement rules from its first implementation. Login, onboarding, Account settings, Forgot/Reset password, emergency-captain setup/login, and every other Slice 1 route reachable by anonymous visitors, normal Users, captains/co-captains, or emergency captains must be complete in English and Danish through the existing language switch. Admin/Super-Admin-only account, audit, and operator surfaces may remain English-only. The final whole-site pass remains big-roadmap Milestone 9.

## 8. Affected implementation areas

### Existing code requiring replacement or adaptation

- `src/Bingo.Domain/Access/Account.cs`
- `src/Bingo.Domain/Access/AccountRole.cs`
- `src/Bingo.Domain/Access/AccountAccessMode.cs`
- `src/Bingo.Application/Access/*`
- `src/Bingo.Infrastructure/Persistence/ApplicationDbContext.cs`
- access/audit persistence configurations and the EF model snapshot
- `src/Bingo.Web/Program.cs`
- `src/Bingo.Web/Security/AccountAuthenticationService.cs`
- `src/Bingo.Web/Security/AccountAuthorizationHandler.cs`
- `src/Bingo.Web/Security/AccountCookieEvents.cs`
- `src/Bingo.Web/Security/CaptainAccountProvisioner.cs`
- `src/Bingo.Web/Security/DevelopmentAdminBootstrapper.cs`
- `src/Bingo.Web/Pages/Account/*`
- `src/Bingo.Web/Pages/Admin/Accounts/*`
- `src/Bingo.Web/Pages/Admin/Audit/*`
- `src/Bingo.Web/Pages/Shared/_Layout.cshtml`
- `src/Bingo.Web/Navigation/SharedShellService.cs`
- `src/Bingo.Web/TestData/DevelopmentScenarioSeeder.cs`
- event finalization, draft-role, evidence, and review queries that read current Account scope/role fields.

### Expected new areas

- domain account type/global-role, reset-token, Discord-transition, emergency-access, and minimal character/link entities;
- application account-authentication, onboarding, password, Discord-link, role, ownership, disable, reset, account-query, and audit-query contracts;
- infrastructure command/query services and persistence configurations;
- Discord OAuth configuration/options and purpose-bound callback handling;
- account onboarding/settings/Forgot password/Reset password pages;
- operator preflight, owner-bootstrap, and recovery commands;
- one consistent Slice 1 migration with designer and model snapshot.

## 9. Automated verification plan

### Domain tests

- account-type/global-role combinations;
- sole-owner transfer state transition;
- Admin grant/revoke restrictions;
- disable/restore actor-target matrix;
- authorization/password version increments;
- reset-token expiry/use/supersession;
- Discord transition invariants;
- emergency-access disabled/default/time behavior;
- public-name and preferred-link invariants.

### Integration tests with PostgreSQL

- unique public/login identifier and Discord ID races;
- atomic onboarding rollback on username/link collision;
- partial unique Super-Admin constraint and atomic transfer;
- role revocation and disable invalidating old cookies;
- password change/reset invalidating password cookies while leaving unrelated Discord-authenticated cookies valid where required;
- reset token one-use, expiry, supersession, and concurrent consumption;
- Discord link/replace collision rollback and transition history;
- Admin/Super-Admin reset and disable authority;
- legacy Admin/Captain migration preserving IDs, hashes, scope, and historical references;
- emergency credential scope and explicit enablement;
- audit mutation atomicity, sanitization, filtering, and stable pagination.

### Browser tests

- public login and Forgot password routes;
- Discord-first onboarding callback simulation;
- password and Discord login reaching the same account;
- username-collision form preservation;
- account settings link/unlink/change states;
- reset link flow;
- Super-Admin-only role/transfer controls;
- disable/restore permission and stale-session behavior;
- sanitized account overview and 25-row audit navigation;
- no-JavaScript form fallbacks for ordinary mutations.

## 10. Approved delivery boundary and verification gate

- Slice 1 brings forward only the minimum character/link records required for onboarding and account administration. Full My accounts commands remain Slice 2.
- Migration preflight is read-only and stops on any login collision, ambiguous emergency scope, orphaned historical reference, or missing explicit retained-database owner.
- One coherent generated EF Core migration, designer, and model snapshot performs the interdependent account/security transformation and is exercised against both empty and retained-data PostgreSQL databases.
- The normal Development database may be reset/reseeded. Retained-data verification uses a separate copy and must prove ID, hash, scope, and historical-reference preservation.
- Missing Discord configuration leaves password/Admin access operational but presents accurate Discord-unavailable feedback and blocks signup readiness rather than creating partial accounts.
- Implementation follows the ordered stages in section 7 with focused checks at each boundary and preserves the existing uncommitted Pass 12 work unless an account-model dependency requires a focused change.
- Completion requires the applicable domain, PostgreSQL integration, browser, Release build, formatting, authorization, migration, English/Danish, responsive, keyboard, accessibility, and no-JavaScript checks.
- The exact manual cases remain durable in `MANUAL_TEST_CHECKLIST.md`; defects return to Slice 1 before Slice 2 begins.

Approval of this plan does not itself authorize application code, migration generation/application, database reset, or test-data mutation.

## 11. Implementation inputs

No unresolved product decision blocks implementation. Before implementation can complete:

1. Select the existing account that will become Super Admin in each retained database; never infer this from “first account.”
2. Supply Discord application configuration through secrets/environment configuration for local callback testing.

The initial production data decision is resolved: deploy a clean migrated database, initialize only the reviewed OSRS catalogue snapshot, then provision the intended owner through controlled operator setup. Retained-data migration coverage remains required for local upgrade safety. These are implementation/deployment inputs, not permission to mutate the current database.
