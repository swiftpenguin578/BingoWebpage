# OSRS Community Bingo Platform

## Technical Architecture

**Status:** Approved architecture v1.1 with Planning Pass 2 target changes documented
**Last updated:** 2026-07-25
**Companion documents:** `PRODUCT_REQUIREMENTS.md`, `DATA_MODEL.md`

## 1. Architecture decision

Version one will use a portable ASP.NET Core monolith hosted on a small Hetzner VPS.

**Decision approved:** 2026-07-11

Chosen stack:

- .NET 10 LTS
- ASP.NET Core
- Razor Pages and Razor components for server-rendered UI
- Small focused JavaScript modules for browser-native interactions such as drag-and-drop
- SignalR for live board and review-queue updates where useful
- Entity Framework Core
- Npgsql PostgreSQL provider
- PostgreSQL
- Cloudflare R2 for uploaded evidence and other durable image assets
- Docker Compose
- Caddy for HTTPS and reverse proxying
- GitHub Container Registry for application images
- Automated off-site PostgreSQL backups

.NET 10 is an active LTS release supported until November 2028 at the time of this decision.

## 2. Why this architecture fits

- The expected scale is small: approximately 100 participants and a few events per year.
- Most complexity is domain logic rather than traffic volume.
- One deployable application is easier to understand and operate than separate frontend and backend systems.
- C# makes ranking, contribution, state-transition, and audit rules explicit and testable.
- PostgreSQL provides transactions and constraints needed for concurrent review, waiting-list promotion, and drafting.
- R2 keeps screenshot growth away from the VPS disk.
- Docker, PostgreSQL, and S3-compatible storage keep the application portable.
- The expected base VPS cost is approximately €6–8 per active month, excluding domain and optional extras.

## 3. Deliberately excluded infrastructure

Version one does not require:

- Microservices
- Kubernetes
- A separate single-page frontend
- Redis
- A dedicated message broker
- Elasticsearch
- A separate background-worker server
- Multiple application replicas
- A managed realtime service

These can be reconsidered only if measured usage or operational problems justify them.

## 4. Logical application structure

```text
src/
├── Bingo.Web
│   ├── Public pages
│   ├── Captain pages
│   ├── Admin pages
│   ├── HTTP endpoints
│   ├── Authentication and authorization
│   ├── SignalR hubs
│   └── Browser assets
│
├── Bingo.Application
│   ├── Event workflows
│   ├── Signup and waiting-list workflows
│   ├── Draft workflows
│   ├── Board-building workflows
│   ├── Submission and review workflows
│   ├── Finalization workflows
│   └── Interfaces for storage, time, and notifications
│
├── Bingo.Domain
│   ├── Entities and value objects
│   ├── State transitions
│   ├── Contribution calculations
│   ├── Board progress calculations
│   ├── Ranking calculations
│   ├── Draft turn calculations
│   └── Domain errors
│
└── Bingo.Infrastructure
    ├── Entity Framework Core
    ├── PostgreSQL migrations
    ├── R2 object storage
    ├── Password and token services
    ├── Scheduled processing
    └── External catalogue import

tests/
├── Bingo.Domain.Tests
├── Bingo.Application.Tests
├── Bingo.IntegrationTests
└── Bingo.BrowserTests
```

Dependency direction:

```text
Bingo.Web ──────────┐
                    ├──> Bingo.Application ──> Bingo.Domain
Bingo.Infrastructure┘
```

The domain project has no dependency on ASP.NET, Entity Framework, PostgreSQL, R2, or Hetzner.

## 5. Web rendering and interaction

### 5.1 Default rendering

Pages are rendered on the server using Razor. This provides:

- Fast initial HTML
- Straightforward authorization
- Simple form validation
- Accessible navigation without requiring JavaScript for basic operations
- One C# application to understand

### 5.2 Focused interactive behavior

JavaScript is used only where it improves the interaction materially:

- Mission Control-style board transition
- Tile drag-and-drop swapping
- Image preview before upload
- Side drawer behavior
- Responsive navigation details

All important server mutations still use authenticated server endpoints and server-side validation.

### 5.3 Live updates

SignalR publishes small invalidation/update messages after:

- Submission approval or reversal
- Tile completion
- Board completion
- Catalogue changes that affect an open draft-board editor
- Draft pick
- Event state change

Clients then update the affected view or request fresh data. SignalR messages do not contain authoritative secret data and do not replace database transactions.

At production scale, clients should refresh only the affected progress data. The existing full-page safety reload may remain as a recovery mechanism, but progress notifications must not cause every connected client to reload simultaneously. Use targeted fetches and/or randomized jitter, and verify the behavior with 100 connected clients before deployment.

The site must remain usable if the realtime connection is temporarily unavailable. A normal refresh retrieves the authoritative state.

## 6. Persistence

### 6.1 PostgreSQL

PostgreSQL stores:

- Event configuration
- Signup and waiting-list data
- Draft-participating and pre-formed teams, manual rosters, and draft picks
- Boss/drop catalogue and event snapshots
- Boards, tiles, and requirements
- Submission metadata and review history
- Derived progress caches
- Accounts and event access
- Audit entries
- Asset metadata

Screenshot binary data is not stored in PostgreSQL.

### 6.2 Entity Framework Core

Entity Framework Core provides:

- Schema migrations
- Transaction boundaries
- Optimistic concurrency tokens
- Relational constraints
- Typed query code

Database-specific constraints and indexes are still used where needed. Important competitive invariants must not rely solely on UI or application checks.

### 6.3 Transactions

The following operations require a database transaction:

- Event draft creation with its creation audit record
- Event discard, event-owned setup cleanup, and tombstone audit record
- Waiting-list promotion
- Draft pick and undo
- Submission approval
- Approval reversal
- Board approval snapshot
- Board publication from the active approval snapshot
- Event finalization
- Event unfinalization

Submission review uses a concurrency token or row lock so two admins cannot apply the same evidence twice.

Board and draft administration use two complementary concurrency mechanisms:

- Each board aggregate carries an optimistic concurrency version covering its tiles, requirements, layout, and publication state. Every mutating request includes the loaded version; stale writes return a recoverable conflict and do not overwrite newer content.
- A separate renewable board-editor lease provides the normal user-facing edit lock. Opening a board does not acquire it; explicit edit, release, and confirmed takeover actions change ownership while viewers receive live updates. Navigating away sends a keepalive release request, while client activity renews the lease at a throttled interval. The five-minute inactivity expiry remains the fallback for interrupted or abandoned browsers.
- A draft session carries an active-controller lease tied to an administrator account and lease/version value. Server-side authorization checks that lease for start, pick, undo, pause/resume, and finalization.
- Draft observers receive live state but remain read-only. A confirmed takeover atomically replaces the controller lease and writes an audit entry.
- Draft-pick transactions retain serializable isolation and uniqueness constraints as the final integrity boundary even when controller requests race or are retried.
- Draft setup derives team count from active drafted teams and derives balanced final sizes from included confirmed participants; it does not persist an admin-entered target size. Board-editor team/size estimates remain isolated board-EHB inputs.
- Starting, picking, and finalizing revalidate that preassigned drafted-team members can still produce final rosters differing by no more than one and that no included confirmed participant can be stranded.
- Draft start also revalidates that every drafted team already has a current `CAPTAIN` membership; `CO_CAPTAIN` alone is insufficient. All captain/co-captain assignments consume ordinary roster positions.
- Turn calculation skips any drafted team whose current roster is larger than the smallest drafted-team roster, allowing teams with fewer preassigned captains/co-captains to catch up before the larger roster becomes eligible again. The compact per-team `current/final` projection comes from the same authoritative calculation.
- Undo is a repeatable transactional stack operation over the latest active pick. Each request marks one pick undone, ends its membership, and recalculates the next turn from the remaining active ledger; no arbitrary undo-count limit exists.
- Recording the first pick permanently locks the drafted-team set and formation types. Undoing every pick does not reopen structural team configuration. Team display metadata remains writable until the separate event-start lock.
- Draft finalization transactionally publishes only the effective active-pick order and roster projection. After success, the UI may open a **Publish board?** prompt/page. Its separate board-publication command revalidates full grid, approval, and private state in a new transaction; failure cannot roll back or corrupt the completed draft.
- Reopening a finalized draft is a pre-event exceptional transition requiring strong confirmation and a written reason. It withdraws the public roster/pick-order projection, preserves the board's independent publication, retains structural team locks, and appends transition history before allowing stack undo/repicking.
- Board approval uses the board aggregate's optimistic concurrency version. Approval requires a full grid and valid tiles; any later private competitive-content edit atomically returns the board to Draft while retaining the superseded approval record.
- A Draft board reads catalogue-backed names, images, rates, variants, and EHB mechanics live. Relevant catalogue writes invalidate its derived projections and notify open editors; the server recalculates authoritative tile, line, total, and per-player estimates from current catalogue rows rather than treating cached editor values as competitive history.
- Approve board is the snapshot transaction. It locks or version-checks the board and every referenced catalogue row, validates the complete grid, calculates all competitive values, writes a new immutable `BoardApprovalSnapshot`, and assigns it as the board's active approval version atomically. A concurrency conflict fails without producing a partial snapshot.
- Validated preview and publication read the active approval snapshot without recalculation. Explicit unapproval or a private competitive edit clears the active pointer, retains the superseded snapshot, returns the board to Draft, and resumes live catalogue derivation.
- Preview board shares the public board renderer and responsive component rules. Draft preview supplies live derived catalogue data, validated preview supplies the active frozen snapshot, administrator-only EHB/edit controls are omitted, and preview has no command path that can approve or publish.
- Catalogue/drop tiles must return a valid automatic EHB calculation to pass board validation. Missing mechanics are repaired in catalogue/requirement data rather than bypassed. Only custom/manual objectives accept an explicit manual EHB value.
- Enabled Admins may create, edit, deactivate, and reactivate catalogue rows. Permanent deletion and bulk-import preview/apply require `RequireSuperAdmin`; deletion also runs a transactional dependency check and fails for any board, source-drop, snapshot, asset/cache, import-review, or historical reference. Bulk apply verifies the reviewed preview version/hash and aborts on stale catalogue state.
- Tiles are event-board-owned aggregates. The application exposes move/swap operations but no duplicate, cross-event copy, import, or reusable-template command.
- The permanent public Rules page reads a singleton, versioned global rules document. Updates pass through an administrator-authorized application command with validation, optimistic concurrency, and automatic audit history; they require no reason, send no participant notification, and have no event-lifecycle effect.
- Public how-to pages are source-controlled Razor/content assets with stable anonymous routes and no runtime editor or persistence model.
- Tile records retain only objective wording and custom/manual completion criteria. General evidence/submission guidance links to the global Rules and how-to pages, and board approval has no per-tile evidence-instruction invariant.

Discarding an accidental or experimental event is a server-authoritative transaction. The server rechecks that no participants, teams, event-scoped account access, submissions, or evidence exist immediately before cleanup. Racing creation of any protected record makes the discard fail rather than deleting newly created data. Event-owned setup data may be removed, while the terminal event tombstone, retired slug, actor, time, and audit record remain. Before event-banner metadata is removed, the same transaction writes an event-owned managed-banner cleanup outbox record. Only after commit does storage deletion run; success or an already-missing object completes the record, while a safe failure message and retry metadata remain for the existing lifecycle worker. The cleanup service accepts only the discarded event's own namespaced managed key, so it cannot target another event, global/catalogue media, or arbitrary storage.

### 6.4 Event timezones

Event timezone choices come from the server's supported canonical timezone set, with `Europe/Copenhagen` selected by default. The UI presents friendly labels and offsets but posts the canonical ID for server validation. Changing an event timezone never rewrites stored UTC schedule values; any schedule change is a separate explicit operation.

## 7. Object storage

Cloudflare R2 stores:

- Original evidence screenshots
- Replacement evidence
- Admin evidence attachments
- Event banners
- Team images
- Boss and item images when locally managed

R2 is accessed through an application-owned storage interface using its S3-compatible API. Provider-specific code stays in `Bingo.Infrastructure`.

Uploads are streamed through the application to R2 in version one. This allows authorization, size validation, media-type inspection, and checksum calculation in one place.

Upload protections include:

- Configurable maximum image size
- Allowed image media types
- Image decoding validation
- Generated storage keys rather than user filenames
- Original filename stored only as metadata
- Checksum calculation
- No public bucket listing
- Evidence served through authorized application routes or time-limited object URLs

All participant/admin-supplied application images use managed upload through the shared asset-storage path rather than arbitrary external image URLs. Event banners, team images, and custom board/tile artwork reuse the evidence-submission interaction and core upload protections while using decorative-asset authorization and retention instead of evidence semantics. Upload, replacement, and removal remain independent from competitive evidence retention.

External image-source URLs are accepted only for global OSRS catalogue bosses/activities and items. Catalogue infrastructure fetches, validates, and caches those sources through the existing catalogue-image path; no other product form or domain record exposes an image-URL field.

## 8. Authentication and authorization

### 8.1 Accounts

ASP.NET Core authentication uses secure cookies and one normal website account with public username/password plus an optional current Discord association. Initial account creation starts with Discord; successful onboarding requires the public username and password before the account can submit signup. After onboarding the account may unlink Discord and remain password-accessible. Passwords use ASP.NET Core Identity's approved password hasher and are never logged.

Discord OAuth client/callback configuration is a deployment prerequisite for normal public signup, not an event setting. Signup readiness checks that the login path is configured and enabled but does not make a live Discord API call during manual or scheduled opening. Provider outages are handled at authentication time with accurate retry feedback and do not mutate event lifecycle state.

The first account journey after a successful Discord callback routes an incomplete profile through onboarding before committing an event signup. The user enters an independent website username, required password, and first OSRS character. The UI may recommend using the primary character as the username but applies exact-spelling responsibility only to the OSRS field. The server trims surrounding whitespace and normalizes case only for each separate uniqueness boundary; it does not call OSRS or Wise Old Man or validate syntax, availability, ownership, membership, or existence. It commits the unique public/login username, password hash, separately entered trust-based character link, preferred-account selection, and onboarding completion atomically. A database uniqueness conflict returns an accurate field-specific error without merging website accounts or character ownership.

The public username is also the normal password-login username. Subsequent sign-in may use it with the password or use Discord. Both methods resolve the same account ID. Discord display names are retained only as non-authoritative support metadata.

Username changes accept any valid case-insensitively unique website username; they do not depend on an active character link. The UI confirms that the new public username becomes the password-login username. The transaction reissues the current session but does not invalidate unrelated sessions solely for a name change. Event-facing names come from registered OSRS characters, so the rename does not mutate event participants or assignments.

Account types:

- Normal website account with a global `USER`, `ADMIN`, or `SUPER_ADMIN` role and optional current Discord association
- Disabled-by-default event/team-scoped emergency captain account

Discord guild membership is not required. Discord display names are non-authoritative metadata; authorization never depends on a display name or an OSRS character name.

Passwords require at least 10 characters, accept passphrases/printable characters up to the existing 200-character input limit, and have no character-class composition or arbitrary periodic-change rule. Password login uses generic success/failure disclosure and independent per-account-identifier and per-network rate limits. Password-authenticated cookies carry both the global authorization version and a password version; changing a password or consuming an admin-generated reset increments the latter so prior password sessions fail without unnecessarily ending a separate Discord-authenticated session.

A non-persistent primary cookie ends with the browser session and carries a ticket with a 12-hour maximum lifetime. **Remember me** creates a persistent ticket with a 30-day absolute maximum that sliding renewal cannot extend indefinitely. The external Discord/OAuth cookie protects purpose/correlation state, and first-time onboarding state expires after 15 minutes without creating a partial database account.

Version one does not collect email. The public Forgot password page is static, asks for no username, directs the person to contact an administrator, and exposes no account lookup. After offline identity verification, an enabled Admin can issue a reset link for a normal user; only the Super Admin can issue one for an Admin, and the Super Admin uses Discord or operator recovery. The service generates a high-entropy token, stores only its hash, applies a 60-minute expiry, makes it single-use, and supersedes older unused links. The raw link is exposed once for the admin to deliver through the verified offline channel; the admin never sees or selects the replacement password. Generation and completion write structured history without a typed reason.

Account settings support `LINK`, `UNLINK`, and `REPLACE` Discord commands at any time, including during an event and for privileged accounts. All require fresh password reauthentication; link/replace also require a purpose-bound OAuth callback whose Discord ID is globally unused. Each command locks the account/unique identity, appends the transition, increments the authorization version, and changes the association atomically. The current browser receives a fresh password-authenticated session while other sessions are invalidated. `UNLINK` may leave Discord null; `REPLACE` has no intermediate null state. Event participants and every role derive from the unchanged website account ID, so these commands cannot move or rewrite signup, team, evidence, My accounts, or history. The Super Admin confirmation warns that losing the password after unlinking requires operator recovery.

### 8.2 Authorization

Authorization policies enforce:

- Admin-only event and catalogue management
- Participant access to their linked event-participant record and own submission scope
- Captain/co-captain access derived from one event/team roster role
- Drafted-team captain/co-captain access to the expanded confirmed signup projection only before draft finalization
- Team-focus read access for current team members, mutation for that team's captains/co-captains, and explicit opt-in cross-team read-only inspection only for the designated Super Admin
- Global role management and ownership transfer only for the designated Super Admin
- Emergency-account activation/disablement and lifecycle-based website-account role authorization
- Evidence visibility rules

Authorization is checked on every server mutation. Hiding an interface control is not a security boundary.

The captain draft-information view reuses the signup-board projection with a role-aware column set. It exposes participant-submitted answers, including fields hidden from the public projection, but never joins paid/unpaid status, private admin notes, identity-recovery/security metadata, or audit data. Only captains/co-captains of drafted teams may load it, and only until draft finalization. After finalization, the signup route remains an admin-authorized signup page; non-admin requests redirect to the published team-roster route rather than rendering roster content through the signup page.

Team-focus queries do not inherit ordinary admin visibility. A current team membership grants read access; captain/co-captain on that team grants mutation. For another team, the designated Super Admin must explicitly enable a team-scoped inspection mode before a read projection or realtime subscription is authorized. The default page, API response, and SignalR subscription contain no cross-team focus data. Inspection is read-only, visibly identified, limited to the current team/page session, and not persisted as a show-all preference. Realtime focus invalidations remain authorized and team-scoped rather than broadcast as public board events. Focus writes use optimistic concurrency and have no path into competitive progress calculations.

Global account authorization uses separate `RequireAdmin` and `RequireSuperAdmin` policies over a normal website account's global role, independent of its current Discord-link state. Controlled setup/migration assigns the sole initial Super Admin to a configured owner account; public login/onboarding has no privileged-role path. A database uniqueness constraint prevents a second Super Admin, while an atomic ownership-transfer command locks the current owner and destination so it cannot leave zero owners. The destination becomes Super Admin and the source becomes Admin in one transaction.

Only the Super Admin may grant or revoke ordinary Admin. Emergency/legacy captain accounts fail privileged-role validation. Grant/revoke uses an explicit before/after confirmation without typed username or reason; revoke returns the target to User rather than disabling or rewriting event relationships. Transfer requires the owner's current password and typed destination public username. Every global-role change increments an authorization version checked by the authentication cookie/session validator, so grant, revoke, and transfer invalidate affected sessions immediately. A rejected stale session receives an accurate access-changed/sign-in-again outcome rather than an application error. Lost-owner recovery remains an operator-controlled deployment procedure.

Website-account disable/enable uses a separate application command from global-role mutation. An enabled Admin may disable a `USER`; only the Super Admin may disable or restore an `ADMIN`. The command prohibits self-disable and the current Super Admin, locks/version-checks the target, requires a reason for disable, increments `authorization_version`, and preserves every event-owned or historical relationship plus the reserved username and Discord association. A disabled account cannot authenticate and its identifiers remain unavailable to other accounts. Re-enable is confirmed and audited without a typed reason and does not bypass current event/membership authorization.

The global Accounts area uses separate sanitized, server-paginated projections for website accounts and emergency credentials, with 25 rows per page. Website search/filters cover normalized username, global role, active state, Discord-link state, and event participation; rows include username, role, state, link state, last login, and current event-role summary. Detail queries include every linked OSRS character, non-authoritative last-known Discord display metadata, event participation, current/historical event-team roles, and disable history. Emergency projections include login username, event/team, setup/enabled/cutoff state, last login, and only currently authorized controls. Neither projection returns password hashes, OAuth tokens, setup/reset-token material, or unnecessary raw Discord identifiers. Version one provides neither website-account merge nor permanent account deletion.

Audit history is an immutable newest-first server query with a fixed page size of 25 and filters for event, actor, action, entity, and date range. Pagination preserves filters and can traverse the complete retained history; it is not a retention cap. Detail rendering converts structured before/after fields into human-readable values without mutating the stored event. Version one has no audit export. Routine successful website login updates `last_login_at` only; failed attempts and throttling use security logs. Emergency-credential success and security-sensitive password, Discord-link, role, disable/restore, ownership, and emergency-access mutations write durable audit events.

Grant/revoke Admin and restore commands append a personal notification for the target. Disable keeps its written reason private to Admin/audit projections; a stale or attempted authenticated route returns neutral contact-an-admin guidance without disclosing the reason.

Lost-owner recovery is an operator-only command surface, never a web route. It can generate a purpose-bound 60-minute reset link for the current owner or atomically transfer ownership to a specified active website account. It requires explicit source/destination identifiers plus an explicit confirmation argument and records a system-actor audit event.

Personal notification read state and unresolved operational work are different projections. Opening a personal notification may mark it read, but pending reviews, postponed starts, waiting-list follow-up, vacancies, and missing-captain conditions remain in the Admin action inbox until their authoritative underlying state is resolved.

### 8.3 Signup ownership and editing

Normal public signup requires an authenticated website account and creates one event-participant record owned by it. A new account must first complete Discord-backed creation and public-username/password onboarding; a returning account may arrive through either sign-in method. The participant may edit the signup only while the event is accepting signups. Closing signup revokes edit authorization on the server even for an existing authenticated session.

The signup form loads the authenticated account's trust-based OSRS character links. A missing character is added through My accounts before the participant returns to signup; the final Account control is not an inline free-text link-creation path. An existing character assigned to that participant in the event remains available while editing even if its global link is later unlinked or transferred; replacing it requires a current available link. Global links are many-to-many and do not prove ownership. A partial unique `(event_id, osrs_character_id)` constraint over unreleased assignments protects current event reservations for both confirmed and waiting-list participants. The complete signup/create or edit command commits the participant, answers, event assignments, saved-EHB updates for regular-account answers, event EHB snapshots, form marker/version, and capacity/status result in one transaction. The server translates a uniqueness race into an account-specific “already assigned in this event” validation result, rolls back the whole attempt, and returns the other posted values for correction. A failed edit leaves the previously committed signup unchanged.

My accounts is an authenticated global account-management surface. Optional personal labels and ordering are presentation metadata, not game-mode or ownership assertions. Each link may store an optional personal EHB default. Unlinking hides the association from future selection but does not cascade into event assignments, evidence, or activity. A spelling correction atomically relinks the entry and every currently participant-editable signup, while closed/locked/live/history assignments remain unchanged; an event uniqueness conflict rolls back the whole correction.

Event character assignments distinguish internal `PLAYING` from `INFORMATIONAL`; the UI calls them **Regular account** and **Alt account**. The built-in required Account question and any additional regular Account questions create playing assignments. An alt Account question may record a named helper alt, while a Yes/No question records only its existence. Only regular accounts participate in active eligibility, evidence credit, or Wise Old Man synchronization. The built-in primary regular assignment is the automatic initial active account and receives the initial activation at event start. Signup close freezes the assigned set and roles for participant editing, while live swaps remain available among the frozen regular accounts.

Each regular assignment stores its own signup-time EHB snapshot. Secondary Account questions are structurally optional, but when answered, regular-role validation requires EHB in the same compound control. Alt accounts have no EHB. The Account control starts with the link's optional saved EHB; saving updates both that personal default and the independent event snapshot. Later My accounts edits do not silently rewrite the event snapshot. Draft sorting and balancing use only the assignment produced by the built-in primary Account question; secondary values are neither summed nor substituted.

WoM-assisted EHB entry is an explicit per-account command that runs only after a non-empty character name is present. It may report that the trusted name could not be found, but it does not become a prerequisite for account creation or linking. It populates the form but does not replace the stored event snapshot. Before implementation, inspect the current official API documentation and usage limits, then apply server-side cache reuse, request throttling, `Retry-After`/backoff behavior, and user-facing not-found/unavailable/rate-limited results. A failure leaves the existing manual field value intact.

Exactly one playing account is active/drop-eligible for a participant at a time. The primary account activates at event start. Normal swaps are available only while the event is `LIVE`, are unlimited during that state, and append an immutable transition with effective and recorded UTC timestamps. Event end closes normal swaps. The swap mutation uses optimistic concurrency or a row lock so simultaneous requests cannot both succeed from the same prior active character. Participants act for themselves, captains/co-captains act for unlinked members of their own external team, and admins may make audited corrections.

Evidence creation derives rather than selects the credited playing character. An ordinary participant is locked to themselves; a captain/co-captain selects a current teammate. In the same transaction, the service resolves that participant's active character at immutable server submission time and snapshots it on the submission. The form never exposes an account selector, and later submitter edits preserve the credited fields. The screenshot's clan-plugin UTC overlay remains the evidence of when the drop occurred. Review shows the latest relevant account transition in UTC; full history remains available to admins for audit, visual validation, corrections, and later activity attribution.

The review summary displays immutable server **Submission time**, calculated minutes after event end when applicable, and **Latest clan event time** using the authoritative event end formatted in UTC. The admin compares that boundary with the `DD/MM/YYYY HH:mm UTC` timestamp visible in the screenshot. No OCR, second typed timestamp, or maximum upload-delay rule is encoded; the configured cutoff alone controls upload acceptance.

Evidence review has only `Approve` and `Reject` decisions. Approval needs no note; rejection requires one. Duplicate or unusable evidence is rejected rather than entering an intermediate state. While the active upload window remains open, a rejected submission can be resubmitted through a dedicated command that prefills editable structured values, requires a new screenshot, and creates a linked new record. It copies the rejected record's credited participant/account snapshots as read-only even after a later account swap. A unique predecessor constraint and transaction lock make the command idempotent under retries/concurrency. Rejection does not create post-cutoff correction access. The rejection transaction appends review history and creates idempotent notifications for the linked credited participant and current linked team captains/co-captains.

Before approval, a reviewer may correct tile/requirement, qualifying drop, or credited playing character with a required reason. The participant is derived from the character's event assignment. The command locks or version-checks the pending submission, revalidates authorization and every structured allocation/account rule, records before/after values, and rejects stale decisions. It never changes the immutable server submission time, snapshot weight, calculated contribution, or evidence asset. Approval records the human decision that the screenshot time satisfies event/account eligibility. Reversal is a separate reasoned transaction that preserves history and recalculates authoritative progress.

Approved evidence metadata, credited player, and screenshot are public; participant/captain privacy requests, player hiding, and hidden-but-still-approved screenshots are not part of the target. Removing an approved screenshot from public view therefore uses the ordinary reasoned approval-reversal transaction. Any corrected/redacted attempt is a linked new submission subject to the normal upload cutoff.

There is no public feedback or evidence-report persistence/API in version one. Community evidence concerns, bugs, and feedback are received through Discord; a valid evidence concern enters the existing administrator reversal/resubmission workflow.

Normal swaps are recorded immediately but become evidence-effective at the first whole UTC minute strictly after the request. While a future-effective transition is pending, the old account remains active and another participant swap is rejected. This makes each plugin-displayed minute belong unambiguously to one playing account.

After draft finalization, participant navigation resolves to the published team roster until the board is available, then to the existing team-board route. Role-aware queries add only the participant's own non-public submissions and their team's focus projection. They do not change the approved public-board HTML/data projection for anonymous users or opponents. If the private focus layer is visually disabled by an authorized viewer, the server authorization and shared marker state remain unchanged.

Participants may create evidence for themselves and edit/withdraw their own pending evidence through the active submission cutoff, including the post-end grace period. Captains retain team-wide scope. At cutoff, server authorization closes every participant/captain submission mutation while leaving participant-owned history readable and admin review available.

The unlisted public signup table treats every participant-facing answer as public in version one. The retained visibility field is fixed/defaulted true for future compatibility; there is no admin-only custom question or post-draft privacy mutation. Confirmed and waiting-listed profiles are shown separately using their primary regular OSRS characters as event-facing names; waiting-listed profiles show exact derived positions. Generated Account/Alt account headings remain consistent. Website username, Discord identity, payment, Admin notes, security data, and audit data never enter the projection. Alt-account answers are excluded from later roster, evidence, progress, and leaderboard projections.

Signup-form changes use versioned application commands. Published forms reject definition mutations while signup is open. The first-response marker and question structural fields are concurrency-checked in the same transaction so a racing signup cannot be accepted under a definition an admin simultaneously rewrites. After the marker is set, the database/application boundary rejects required additions and structural edits; disabling and safe metadata changes create a new form version while preserving question and answer rows.

Existing legacy/imported records may temporarily use the private-token compatibility path until Slice 4. Any retained tokens use high-entropy random values with only hashes stored in PostgreSQL and are removed with that migration. Slice 2 adds no claim-token path and never auto-links participant ownership by Discord display name, website username, or OSRS character name.

Participants may withdraw through a separate confirmed mutation after signup closes and before the draft starts. This does not reopen signup-field editing and performs any waiting-list promotion transactionally.

Participant- or admin-initiated withdrawal before draft start uses the same `WITHDRAWN` transition and releases the participant's current event-character reservations in the same transaction. Assignment history and acting identity are retained. Any subsequent signup for a released character creates a new current assignment rather than mutating the historical row.

Normal edits preserve signup ordering and status. Cancellation while signup is open uses the withdrawal transition and releases reservations. Rejoining reuses the same event-participant identity but must reacquire all reservations and obtains a new signup timestamp/sequence at the end of the queue. Post-close withdrawal has no participant self-service undo. Admin restoration before draft start uses current capacity/end-of-waiting-list rules and never displaces an existing promotion. Each compound action and resulting promotion is one transaction.

Waiting-list promotion writes idempotent durable in-site notifications for the linked participant and every enabled administrator in the promotion transaction. Participant notification links to the authoritative confirmation page; admin notifications link to participant management and include the event, participant, and promotion trigger. Recipient-and-transition idempotency prevents duplicate inbox entries on retries. No Discord message integration is required.

Event-participant ownership transfer is an admin-only correction for a participant attached to the wrong or duplicate website account; lost Discord access uses same-account relinking instead. The command uses strong confirmation, locks the participant, verifies that the destination account has no participant in that event, changes ownership/derived access atomically, and records structured old/new identity history. It does not merge accounts or My accounts links. Authorization is resolved from current ownership on every request, so the previous account loses event access immediately without requiring its unrelated global login session to be destroyed.

Participant administration uses one application query/command boundary even if responsive UI routes are later split. The pre-draft workspace projects all three statuses, identity/source/queue state, regular and alt assignments, EHB, public participant answers, private paid/unpaid state, Admin notes, and team state. Admin corrections after signup close reuse signup validation and reservation constraints without changing queue/status.

`My events` queries only explicit `EventParticipant.AccountId` ownership. It separates current participation from history and resolves state-aware destinations through the same lifecycle policy used by signup, table, roster, board, and results routes. It never infers ownership from website username, Discord, or OSRS-character links.

Admin-created internal participants bypass only public availability and signup-code checks; they use the same validation, capacity count, waiting-list ordering, and reservation transaction as website signups. External/pre-formed roster records are explicitly classified outside that signup pool rather than being excluded merely because their source is `ADMIN_CREATED`.

Payment is stored as a private boolean and defaults unpaid. The migration from the current multi-value enum maps only existing `PAID` to true and maps every other legacy value to false while preserving the old value in migration/audit evidence. Participant status migration maps legacy `REMOVED` to `WITHDRAWN` and retains its original timestamp/history.

Admin withdrawal, restoration, and registered-account correction create idempotent in-site notifications for a linked participant. Payment, private-note, and non-account answer corrections do not. Every command still records automatic structured history without requiring a typed reason.

After draft start, participant self-withdrawal is rejected server-side. Admin withdrawal remains available through the live event. The command ends current membership and future eligibility while preserving the draft pick, membership history, registered-account reservations, evidence, and contributions. It creates a durable vacancy/admin action item but never invokes the automatic promotion service.

Post-draft replacement is optional and uses a separate transaction. For a normal replacement it locks the vacancy and chosen waiting-list participant, verifies that the waiting participant is still available with intact frozen character assignments, changes them to confirmed, and creates a prospective `ROSTER_REPLACEMENT` membership linked to the ended membership. The waiting list is displayed in original order but the selected candidate is admin-authoritative because availability is confirmed outside the application. If no waiting participant is available, the command may atomically create a validated internal participant and unique event-character assignments before creating the membership. The vacancy may instead remain open. The draft ledger remains unchanged.

During a live replacement, eligibility is time-bounded: withdrawal ends at the first full UTC minute after confirmation, and replacement membership plus primary-account activation begins at the first full UTC minute after replacement confirmation. The departed member retains reviewable evidence for qualifying pre-withdrawal drop times, the replacement receives no pre-activation eligibility, and any interval between the two effective instants remains a real vacancy.

The withdrawal command revokes the participant's website event/team mutation authority as soon as the transaction commits. The later whole-minute timestamp is specifically the competitive drop-eligibility boundary used for plugin/evidence comparison; it is not an authorization grace period.

Captain/co-captain mutations are admin-only, event/team-scoped role-transition commands over an active membership and website account. Event-start readiness blocks unless every team has a current Captain or active team emergency credential; co-captain alone is insufficient. Withdrawal atomically revokes the departing member's role. Removing the last current captain during live play is allowed because real-world withdrawal may require it, but emits a prominent team/admin warning rather than silently granting authority or stopping the event. A later assignment takes effect immediately, survives authentication-method changes, is retained in role history, and creates an in-site notification for the affected linked participant.

Vacancy creation writes idempotent in-site notifications for every enabled admin and remaining linked team captain/co-captain. Replacement confirmation writes them for the linked replacement and current linked team captains/co-captains. Discord remains the manual availability-contact channel.

External/pre-formed teams may contain roster members without website accounts. Submission access uses one or more explicitly enabled, individually audited emergency captain credentials for that team. These credentials do not own a participant or OSRS character and do not receive participant-only focus controls.

Emergency credentials are individual rather than shared. Any enabled Admin may create multiple credentials for one event/team, each using the global login-identifier namespace. Creation stores a disabled, uninitialized credential and exposes one purpose-bound, hashed, single-use 60-minute setup link; the intended captain chooses the normal-policy password while the credential remains disabled. Reset supersedes earlier setup/reset links and uses the same flow. Password setup and explicit enablement are separate transactions, and the Admin never chooses or sees the lasting password. Only an initialized credential may be enabled. Submission cutoff disables it as already specified.

## 9. Scheduled behavior

A single in-process ASP.NET Core background service performs short idempotent checks at a regular interval.

It may:

- Open or close scheduled signup availability
- Attempt scheduled event start through the authoritative event-start readiness policy
- Transition a live event to awaiting final review at its configured end
- Close normal submission availability at its separate cutoff
- Disable enabled emergency captain credentials at submission cutoff
- Promote waiting-list participants after capacity changes when not completed synchronously
- Perform low-priority cache reconciliation

Event timestamps remain authoritative. If the application was offline when a scheduled time passed, the next request and background check derive the correct effective state.

Normal production publication/lifecycle commands query and lock the current operational event before moving another event into `SIGNUP_OPEN`, `SIGNUP_CLOSED`, `LIVE`, `AWAITING_FINAL_REVIEW`, or `FINALIZED`. A conflict returns the blocking event and recovery route without partially mutating either event. This is intentionally application-level rather than a PostgreSQL uniqueness constraint because `DevelopmentScenarioSeeder` and automated fixtures require several simultaneous lifecycle scenarios. That seeder remains guarded by `environment.IsDevelopment()` and writes labelled fixtures directly; test/development pages and services use explicit event IDs/slugs rather than arbitrary singleton selection when several scenarios exist.

Cancellation is an administrator transaction available only before `LIVE`. It locks the event, requires confirmation/reason, records the transition, closes signup and event-scoped mutation access, disables emergency access, and makes every scheduler branch ignore the event. It preserves all owned data and publishes only a generic cancellation state when `first_public_at` already exists. Archive is a confirmed `FINALIZED → ARCHIVED` transaction requiring no reason; it preserves official snapshots and public URLs while removing the event from current operational queries. Reasoned unfinalization of an archived event rechecks the production singleton policy.

Scheduled event end uses the configured instant as `actual_ended_at`, even when the transition is persisted later. Early end is an administrator-authorized transaction requiring strong confirmation and a reason; it records the confirmation time as the effective end without rewriting the scheduled end or submission cutoff. Both paths use the same transition policy, close new-drop eligibility, and leave grace-period uploads available until the active cutoff. Evidence-image UTC timestamps remain a visual review input rather than an OCR or fixed upload-hours subsystem.

Manual and scheduled opening call the same application/domain readiness policy with an explicit opening mode. Manual opening ignores any stored scheduled-opening value and supplies the current authoritative instant. Scheduling persists the stable active warning codes that the Admin acknowledged. Scheduled opening transactionally revalidates the complete event/form configuration at execution time; a blocker or newly active unacknowledged warning leaves the event private, records one failed attempt, disables delayed retry, and creates one durable Admin action/notification rather than partially publishing signup. External-provider reachability is not part of this transaction.

Scheduled event start follows the same fail-closed pattern. The worker revalidates finalized draft state, published board state, Captain/emergency access, and every other live-transition invariant. Failure stores a scheduled-start attempt, leaves the event pre-live, and creates/updates one idempotent **Automatic start postponed** admin action containing current blocker codes. The worker does not surprise-start the event on a later retry after blockers clear. An admin must invoke **Start event now**; the transition uses actual time, needs no written reason after the scheduled instant, and requires one for an early start.

Critical competitive actions such as finalization remain explicit admin actions.

Submission-cutoff processing atomically records closure and disables enabled emergency captain credentials for that event. Normal website-account captain/co-captain roles remain historical; every mutation still fails because event state/upload-window authorization is evaluated server-side. Reopening submissions does not automatically re-enable an emergency credential.

Finalization is an administrator-confirmed transaction with no required reason on the normal path. It locks/rechecks the final-review aggregate, rejects any pending submission or other unresolved blocker, recalculates progress/rankings, and appends official placement/statistic snapshots. A reasoned blocker override changes only blocker resolution metadata. Unfinalization requires confirmation/reason, supersedes snapshots without deleting them, and never reopens uploads.

## 10. Deployment topology

```text
Internet
   │
   ▼
Caddy :443
   │
   ▼
ASP.NET Core container
   ├── PostgreSQL container on private Docker network
   └── Cloudflare R2 over HTTPS
```

Docker services:

- `web`: ASP.NET Core application
- `postgres`: PostgreSQL
- `caddy`: TLS termination and reverse proxy

PostgreSQL is not exposed publicly.

Only ports 80 and 443 are public. SSH is restricted with key authentication and firewall rules.

## 11. VPS specification

Initial target:

- Hetzner CX23-class shared-vCPU server or equivalent
- European region
- Linux LTS distribution
- 2 vCPU
- 4 GB RAM
- 40 GB local disk

This is sufficient for the expected workload if screenshots are stored in R2.

The exact current plan and price must be checked immediately before provisioning.

## 12. Deployment workflow

Recommended workflow:

1. Merge reviewed code to the deployment branch.
2. GitHub Actions runs tests.
3. GitHub Actions builds the Docker image.
4. The image is pushed to GitHub Container Registry.
5. The server pulls the new immutable image.
6. A pre-deployment database backup is created for schema-changing releases.
7. Entity Framework migrations run as a controlled deployment step.
8. Docker Compose replaces the application container.
9. A health check verifies the release.
10. The previous image remains available for rollback.

Database migrations must be designed for safe forward deployment. Application rollback cannot automatically reverse a destructive database migration.

Initial production provisioning uses an empty PostgreSQL database. After controlled migrations, deployment initializes the reviewed `src/Bingo.Web/data/osrs-catalogue.json` snapshot and provisions the intended Super Admin through the operator-only setup path. Development/test accounts, generated captain credentials, events, signups, participants, teams, boards, evidence, notifications, and audit history are not transferred to production. Retained-database migration support remains required for local upgrade testing and any future environment that genuinely needs historical preservation; it is separate from initial production bootstrap.

## 13. Backup and recovery

### 13.1 Database backups

During normal operation:

- Nightly compressed logical PostgreSQL backup
- More frequent backups during an active event, such as every 4–6 hours
- Backup before production database migration
- Encrypted upload to off-site object storage
- Retention policy with daily, weekly, and event-finalization backups
- Automated checksum verification
- Periodic test restore into a temporary database

### 13.2 Evidence storage protection

- R2 evidence objects are separate from the VPS lifecycle.
- Database backup and R2 bucket must be sufficient to reconnect evidence metadata to stored objects.
- Accidental-deletion protection or object versioning should be evaluated before production.
- A finalized-event inventory records expected evidence keys and checksums.

### 13.3 Recovery objectives

Initial planning targets:

- During an active event: lose no more than several hours of database changes in a full disaster
- Outside an event: restore the latest nightly backup
- Restore service within a few hours when credentials and provider access are available

These targets will be refined during operational planning.

## 14. Monitoring and logs

The application provides:

- Structured application logs
- Health endpoint
- Database connectivity health check
- R2 connectivity check that does not expose credentials
- Failed-login and rate-limit events
- Backup success/failure status
- Background-service status
- Submission review and recalculation error alerts

Logs must not contain passwords, signup edit tokens, storage credentials, or full private evidence URLs.

Low-cost external uptime monitoring should check the public health endpoint more frequently during signup and live event periods.

## 15. Security baseline

- Automatic HTTPS through Caddy
- Secure, HTTP-only, same-site authentication cookies
- Cross-site request forgery protection
- Content Security Policy
- Server-side authorization policies
- Login and signup rate limits
- Upload size and type validation
- Parameterized database access through EF Core
- Secrets stored outside source control
- No public PostgreSQL port
- Minimal container privileges
- Regular OS and container image updates
- Audit log for competitive and privileged actions
- Optional admin two-factor authentication evaluated before live use

## 16. Portability

The application can later move to:

- Another VPS provider
- Render
- Railway
- Azure Container Apps
- Any container host with PostgreSQL and S3-compatible storage access

Portability is preserved by:

- Standard Docker image
- Standard PostgreSQL schema and migrations
- Npgsql rather than provider-specific database features without justification
- Storage interface around S3 operations
- No Hetzner-specific application logic
- Configuration through environment variables/secrets
- Reproducible server bootstrap documentation

Catalogue boss/item artwork retains its reviewed OSRS Wiki source URL in PostgreSQL while the application serves it through a same-origin cache. The cache is populated on demand or with `--sync-catalogue-images`, enforces an HTTPS Wiki-host allowlist plus media-type and size validation, and uses `CatalogueImageCache:LocalPath`. Development cache files are Git-ignored; production must point this path at a persistent volume so deployments and container replacement do not discard the cache. A cache miss can be rebuilt from the authoritative source URLs and therefore is not part of database backup state.

Moving hosts requires deploying the container, restoring or connecting PostgreSQL, configuring R2 credentials, and updating DNS.

## 17. Pausing versus hibernating

### 17.1 Powering off is not a cost-saving pause

Hetzner continues billing a cloud server while it exists, even when powered off. Powering off is useful for maintenance but does not stop charges.

### 17.2 True hibernation

To stop server charges between bingos, the server must be deleted after preserving all required state.

Safe hibernation procedure:

1. Put the website into maintenance mode.
2. Stop new mutations.
3. Create a final PostgreSQL logical backup.
4. Upload the encrypted backup off-server.
5. Verify checksum and perform or confirm a test restore.
6. Export deployment configuration without secrets.
7. Securely preserve required secrets in a password manager or secret store.
8. Record deployed image version and database migration version.
9. Optionally create a protected Hetzner snapshot for faster recreation.
10. Confirm R2 evidence inventory and access.
11. Delete the VPS and any separately billed unused IPv4 resource.
12. Keep the domain, R2 data, database backup, and deployment documentation.

Automatic server backups tied to a deleted server are also deleted. A retained snapshot or independent off-site backups must be used instead.

### 17.3 Restore procedure

1. Create a new compatible VPS.
2. Run the documented bootstrap process.
3. Configure DNS, firewall, Caddy, and secrets.
4. Pull the recorded application image or current approved release.
5. Start PostgreSQL.
6. Restore the latest verified database backup.
7. Run any required forward migrations.
8. Configure and verify R2 connectivity.
9. Run integrity and evidence-inventory checks.
10. Run smoke tests.
11. Disable maintenance mode.

The goal is a documented restore within a few hours rather than preserving an opaque server indefinitely.

## 18. Hibernation cost tradeoff

While hibernated:

- VPS compute cost becomes zero after deletion.
- Domain renewal continues.
- R2 storage may remain inside its free allowance or incur small usage-based storage cost.
- A retained Hetzner snapshot incurs storage cost.
- Independent backup storage may incur a small cost.

For a short gap between bingos, leaving the €6–8 VPS running may be simpler. For a long gap, deletion and documented restoration can reduce cost.

## 19. Local development

Local development uses Docker Compose or a locally installed PostgreSQL instance.

### 19.1 macOS and Apple Silicon

The primary development machine is a MacBook. The chosen stack supports macOS and Apple Silicon.

Recommended local tools:

- Native Arm64 .NET 10 SDK on an Apple Silicon Mac
- Visual Studio Code with C# Dev Kit, or JetBrains Rider if a full IDE is preferred
- Docker Desktop or another compatible macOS container runtime
- Git and the macOS command-line developer tools
- A cross-platform PostgreSQL client when desired

Visual Studio for Mac is retired and is not part of the development plan.

Official .NET, PostgreSQL, Caddy, and ASP.NET container images support the required Linux deployment environment. Local machines may run Arm64 images while the initial Hetzner CX server uses x64 images.

Application images are built as multi-architecture images or are built for the target Linux architecture in GitHub Actions. Production releases must not depend on an image built only for the developer Mac's architecture.

The repository uses cross-platform commands and scripts where practical. Deployment automation runs on Linux rather than depending on macOS-specific behavior.

Potential macOS differences to document for contributors:

- Default shell is zsh rather than PowerShell or Command Prompt.
- Filesystem paths use `/` and are case-sensitive in production even if the local Mac volume is case-insensitive.
- Linux containers do not behave exactly like Windows containers.
- Host-to-container networking uses Docker's macOS conventions.
- Secrets and development certificates use cross-platform .NET tooling rather than Windows certificate-store assumptions.

Developer services:

- ASP.NET Core application with hot reload
- PostgreSQL
- Local S3-compatible emulator or filesystem-backed development storage
- Local email capture only if email notifications are later added

Seed data should create:

- One sample event in each important state
- Six teams
- One pre-formed external team excluded from the website draft
- Representative captains and admins
- A 5x5 example board
- Pending, approved, rejected, and reversed evidence metadata
- Waiting-list and draft examples

No real participant comments or evidence screenshots belong in seed data.

## 20. Test strategy summary

### Unit tests

- Waiting-list promotion
- Snake-draft order
- Pre-formed-team exclusion from draft order and participant pool
- Duplicate drop rules
- Higher-weight submission rules
- Multi-requirement tile completion
- Manual objective quantities
- Row/column completion
- Full-board completion time
- Ranking and tie-breakers
- Approval reversal

### Integration tests

- PostgreSQL constraints and transactions
- Concurrent evidence approval
- Captain team authorization
- Event snapshot immutability
- Finalization blockers and overrides
- Audit creation
- R2 storage adapter against a test-compatible endpoint

### Browser tests

- Public board overview and drill-down
- Signup, private editing, capacity, and waiting list
- Captain submission and pending-submission editing
- Admin review and reversal
- Board building and resizing
- Snake draft
- Pre-formed team creation and roster correction before and after draft finalization
- Event finalization

## 21. Architecture acceptance criteria

Architecture planning is approved when:

1. The selected stack is understandable and acceptable to the maintainer.
2. Expected active-month infrastructure cost is acceptable.
3. Backup and restore responsibilities are understood.
4. Hibernation behavior is understood: powered-off servers are billed; deleted servers are not.
5. Provider portability is preserved.
6. No unnecessary distributed infrastructure is required.
7. Security and upload boundaries are defined.
8. The application can be restored from documented source, secrets, database backup, and R2 data.

## 22. Primary references

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Npgsql Entity Framework Core provider](https://www.npgsql.org/efcore/)
- [Hetzner cloud billing FAQ](https://docs.hetzner.com/cloud/billing/faq/)
- [Hetzner backup and snapshot overview](https://docs.hetzner.com/cloud/servers/backups-snapshots/overview/)
- [Hetzner cloud price adjustment, June 2026](https://docs.hetzner.com/de/general/infrastructure-and-availability/price-adjustment/)
- [Cloudflare R2 pricing](https://developers.cloudflare.com/r2/pricing/)
