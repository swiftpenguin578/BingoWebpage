# OSRS Community Bingo Platform

## Implementation Roadmap

**Status:** Milestone 8A Slices 1–10 accepted; post-functional Application Atlas produced for review

**Last updated:** 2026-08-11

**Companion documents:** `PRODUCT_REQUIREMENTS.md`, `FUNCTIONAL_WORKFLOWS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`, `UI_OVERHAUL_ROADMAP.md`

## 1. Purpose

This roadmap turns the approved product direction into an ordered delivery plan. It defines what will be built, when it will be considered complete, how it will be tested, and which work must precede other work.

No milestone is complete merely because its pages render. Each milestone must satisfy its domain rules, authorization rules, tests, audit requirements, and acceptance checks.

**Roadmap approved:** 2026-07-11

## 2. Delivery principles

### 2.1 Build vertical slices

Each milestone should deliver a usable workflow through the UI, application layer, domain layer, and database rather than creating every database table first and every page later.

### 2.2 Protect competitive integrity early

Transactions, authorization, audit history, and recalculation behavior are implemented alongside the relevant feature, not postponed until the end.

### 2.3 Keep infrastructure replaceable

PostgreSQL, object storage, time, background processing, and external catalogue import are accessed through defined application interfaces.

### 2.4 Prefer explicit code

Domain rules should be readable in named C# types and services. Avoid hidden behavior in controllers, Razor pages, JavaScript, database triggers, or generic frameworks when a clear domain method is easier to understand.

### 2.5 Ship only after rehearsal

The first production event must be preceded by a complete test-event rehearsal including signup, draft, submissions, approval, reversal, ranking, finalization, backup, and restore.

## 3. Milestone overview

| Milestone | Outcome | Depends on |
| --- | --- | --- |
| 0. Project readiness | Decisions, local tools, and external accounts are ready | None |
| 1. Application foundation | Solution runs locally with database, tests, and base UI | 0 |
| 2. Identity, authorization, and audit | Admin/captain access boundaries are enforced | 1 |
| 3. Event creation and signup | Admin can open signup and manage capacity/waiting list | 2 |
| 4. OSRS catalogue and board builder | Admin can model, balance, validate, and publish a board | 2, 3 |
| 5. Teams and snake draft | Admin can create teams and complete a private draft | 3 |
| 6. Evidence submission and review | Captains submit; admins review and reverse evidence | 4, 5 |
| 7. Public boards, progress, and rankings | Visitors see live team boards and evidence | 6 |
| 7A. Concurrent administration hardening | Multiple admins cannot overwrite board work or compete for draft control | 4, 5, 7 |
| 8. Event operations and finalization | Admin closes, reviews, finalizes, and archives events | 7A |
| 8A. Functional expansion and workflow stabilization | Approved new functionality is designed, implemented, and regression-tested before UI finalization | 1–8 |
| 9. UI overhaul and regression | Every stabilized workflow is clear, consistent, responsive, and retested locally | 1–8A |
| 10. Local rehearsal and production preparation | A release candidate passes end to end and deployment/restore tooling is ready | 9 |
| 11. Production deployment and validation | The approved release candidate is deployed last and passes production checks | 10 |

Milestones 4 and 5 can partly proceed in parallel after the event/signup foundation is stable, but their integration must be verified before evidence submission begins.

Milestone 7A is a follow-up hardening milestone for already completed workflows. It must be completed before Milestone 8 and before a real multi-admin draft or board-editing session.

Milestone 9 began while the version-one functional scope was treated as frozen. Planning pass 2 pauses that sequence at the current Pass 12 checkpoint and inserts Milestone 8A. Approved UI work remains a visual and interaction baseline, but affected pages are not final until the selected functional changes are implemented and Milestone 9 resumes.

## 4. Milestone 0 — Project readiness

### Objective

Remove avoidable setup ambiguity before implementation begins.

### Decisions and setup

- Confirm .NET 10 SDK support on the MacBook.
- Choose VS Code with C# Dev Kit or JetBrains Rider.
- Choose Docker Desktop or another compatible macOS container runtime.
- Confirm Git and command-line developer tools.
- Create or identify the source-code repository.
- Decide repository visibility.
- Decide domain name later if it is not yet needed.
- Create a Cloudflare account for R2 before upload integration begins.
- Create a Hetzner account before production infrastructure work.
- Define who receives and stores production credentials.
- Confirm that no real participant data is used in local seed data.

### Planning confirmations

- Product requirements reviewed sufficiently for implementation.
- Data-model rule sections reviewed.
- Architecture approved.
- Wireframes treated as interaction references, not pixel-perfect designs.
- Version-one scope and deferred features understood.

### Completion criteria

- Development machine can run `dotnet`, Docker, and Git.
- Repository and secret-handling approach are agreed.
- No unresolved decision blocks solution creation.

## 5. Milestone 1 — Application foundation

### Objective

Create the deployable skeleton and establish engineering conventions before feature work.

### Deliverables

- .NET solution with `Web`, `Application`, `Domain`, and `Infrastructure` projects.
- Unit, integration, and browser-test projects.
- ASP.NET Core Razor application with a basic responsive layout.
- Local Docker Compose PostgreSQL service.
- Entity Framework Core and Npgsql configuration.
- Initial database migration.
- Configuration validation on startup.
- Environment-specific configuration without committed secrets.
- Structured logging.
- Health endpoint.
- Error handling and user-safe error pages.
- Base design tokens for spacing, typography, colors, statuses, and responsive behavior.
- Continuous-integration workflow running build, formatting checks, and tests.
- Development seed mechanism using fictional data.

### Engineering conventions

- Nullable reference types enabled.
- Warnings treated consistently.
- UTC/timezone-aware timestamp policy established.
- Database naming convention established.
- Domain errors mapped predictably to user-facing validation.
- Migrations reviewed as source code.
- JavaScript modules kept small and page-specific.

### Tests

- Application starts with valid configuration.
- Application fails clearly with missing required configuration.
- Database migration applies to an empty PostgreSQL database.
- Health endpoint distinguishes healthy and database-unavailable states.
- Basic responsive page loads in a browser test.

### Completion criteria

- A new developer can follow the README and run the application locally.
- CI builds and tests successfully.
- No feature-specific shortcuts compromise the chosen project boundaries.

## 6. Milestone 2 — Identity, authorization, and audit

### Objective

Establish secure Discord-linked user/Admin/Super Admin identity, event-scoped captain access, emergency fallback access, and audit foundations before protected workflows are built.

### Deliverables

- Normal website-account records with global `USER`, `ADMIN`, or `SUPER_ADMIN` roles, optional current Discord association, required public-username/password credentials, in-place migration of existing permanent Admin records, plus disabled-by-default emergency captain records. Event-participant ownership and character-assignment migration remain Slice 2.
- Discord-backed initial account creation, required password onboarding, hybrid Discord or public-username/password sign-in to one account, secure password hashing, and secure authentication cookies.
- Login/logout, password change, admin-generated reset links, self-service Discord link/unlink/replace, and emergency password/reset workflows as applicable to each account type.
- Separate Admin and Super Admin authorization policies.
- Controlled initial-owner setup/migration that assigns exactly one Super Admin and cannot be reached from public signup/onboarding.
- Super Admin account management for granting/revoking Admin with strong confirmation, automatic history, and immediate session invalidation.
- Atomic ownership transfer to another active normal website account, preserving exactly one Super Admin and demoting the former owner to Admin; a Discord link is not required.
- Event/team-scoped emergency-captain authorization foundation. Normal website-account captain authorization becomes operational after Slice 2 adds participant ownership.
- Emergency captain activation, automatic submission-cutoff disablement, and explicit re-enable behavior; website-account captain roles remain historical and lifecycle-authorized.
- Audit-entry infrastructure.
- Immutable audit viewer with 25-row pagination and event, actor, action, entity, and date filters.
- Login rate limiting and failed-login logging.
- Cross-site request forgery protection.
- Operator-controlled lost-owner recovery process.

### Authorization tests

- Anonymous visitor cannot access captain or admin routes.
- Captain cannot access admin routes.
- Ordinary Admin cannot grant/revoke Admin or transfer Super Admin ownership.
- Public signup/onboarding cannot claim Admin or Super Admin.
- Emergency captain credentials cannot receive a global privileged role.
- A second Super Admin cannot be created and ownership transfer cannot leave the system ownerless.
- Admin revocation invalidates an already-issued authenticated session.
- Captain cannot access another team's protected data.
- Closed/finalized event state prevents a historical website-account captain role from mutating event data.
- Disabled emergency captain credentials cannot authenticate or mutate event data.
- Closed submission windows reject captain submission mutations while preserving review access.
- Disabled account cannot log in.
- Admin actions create the expected audit actor and timestamp.
- A participating Super Admin receives no other team's focus in the default page/API/realtime projection; explicit inspection grants read-only access for the selected team and disabling it removes access.
- Discord and password login resolve the same account and cannot create duplicate participation.
- Public username changes also change the password-login username while closed event snapshots remain unchanged.
- Generic password failures and reset messaging do not disclose account existence; password attempts are throttled.
- Reset tokens are hashed, expiring, single-use, supersede older tokens, and enforce Admin/Super Admin issuance boundaries.
- A user may link, unlink, or directly replace Discord at any time after fresh password confirmation without changing any event participant or role; link/replace reject an already-used Discord ID.

### Security review

- Cookies use appropriate secure settings.
- No password hash or token appears in audit data or logs.
- Authorization is enforced server-side on every mutation.

### Completion criteria

- Access boundaries pass integration tests.
- Later milestones can use policies rather than implementing ad hoc role checks.

## 7. Milestone 3 — Event creation and signup

### Objective

Allow admins to create an event, configure signup, open only the signup page, and manage confirmed/waiting participants.

### Deliverables

#### Event setup

- Guided, multi-session event-creation workflow.
- Save a private draft with only event name and timezone required; allow other setup values and custom questions to be entered immediately without requiring them for the initial save.
- Event details and timezone.
- Unique generated URL identifier that admins may edit until first public exposure and that remains stable afterward.
- Public description required only before signup opens.
- Optional event banner available during creation and replaceable/removable later without affecting readiness.
- Copenhagen-defaulted supported-timezone selector instead of a raw timezone identifier field; later timezone changes preserve UTC schedule instants and use confirmation/audit protections.
- Signup opening and closing times.
- Scheduled or manual signup opening; manual opening preserves a valid explicit close or proposes future draft time when no later than event start, otherwise event start.
- Event start, end, and submission cutoff.
- Scheduled event-start attempt that remains pre-live and creates an **Automatic start postponed** action when draft, board, Captain/access, or other start-readiness blockers remain; never backdate live eligibility or surprise-start later. Clearing blockers requires **Start event now**, with no reason after schedule and a required reason before it.
- Optional informational draft time that never starts the draft automatically.
- Submission cutoff defaults to 30 minutes after event end and cannot be earlier than event end.
- Participant cap that can increase but not decrease.
- Optional board dimensions and board-editor EHB planning estimates; event setup has no authoritative draft team-count/team-size inputs.
- Signup-code configuration.
- Separate publication flags.
- Event state transitions and audit history.
- Explicitly discard an accidental or experimental event when it has no participants, teams, event-scoped accounts, or evidence; board/setup content does not block discard, while a minimal tombstone and the old URL identifier remain.
- Separate readiness gates for saving a draft, opening signup, starting the event, and finalization.
- Create the event and open signups without requiring a completed board.
- Continue private board preparation independently throughout signup and pre-draft setup.

#### Signup form

- An authenticated website account before normal public signup; initial creation uses Discord without requiring Discord-server membership, while returning users may use either login method.
- First-account onboarding that creates a case-insensitively unique public/password-login username, required password, and separate preferred linked-character record from the same OSRS-format input.
- One website account may own at most one signup per event.
- One built-in required playing-account question with its required event-specific EHB snapshot, backed by trust-based many-to-many website-account/character links and a unique character assignment within each event; character links do not grant authorization or prove ownership.
- Additional custom Account questions configured as playing or informational. They are always optional; an answered playing account requires its own EHB.
- Yes/No support-alt questions when no account name is required.
- Always-present captain-volunteer question.
- Comments and availability only through admin-added custom questions.
- Custom Text (multiline), Number, Yes/No, Single choice, and Account questions.
- First-response structural lock: later questions are optional; answered question types, options, Account roles, stable keys, and answer shapes cannot be rewritten.
- Closed-signup safe edits for label, help, order, and public visibility until draft start.
- Disable-and-replace workflow that preserves historical answers and form versions.
- Post-draft form freeze with a separate hide/restore privacy control that preserves answers and automatic history without requiring a written reason.
- Form validation and event signup code.
- Mode-specific signup readiness: **Open now** uses the current instant and ignores scheduled-opening configuration, while scheduled opening requires and revalidates its configured instant.
- Signup-opening blockers for missing public description, non-positive capacity, invalid schedule/closing, invalid lifecycle state, unavailable Discord login configuration, damaged built-in questions, invalid custom-question definitions, or an enabled signup-code requirement without a usable code.
- Signup-opening warnings for a disabled waiting list and reopening a populated form. These require acknowledgement but no typed reason.
- Authenticated participant editing only while signup is open.
- Approved deterministic migration for imported/legacy records without inferred ownership, plus later explicit Admin identity recovery where required.
- Atomic signup create/edit transaction covering participant, answers, new trust links, event-character reservations, EHB snapshots, form marker/version, and capacity status.
- Confirmed and waiting-list signups reserve Account answers equally; pre-draft withdrawal releases the reservations without deleting history.
- Account-specific conflict recovery that rolls back the whole attempt, preserves the other entered form values, and leaves a previously saved signup unchanged.
- Built-in primary playing account as the automatic initial active account at event start, with no separate signup selector.
- Authenticated signup confirmation showing status, exact waiting-list position, regular accounts/EHB, alt accounts, submitted answers, and current edit/withdraw availability.
- Unlisted public signup table with separate confirmed/waiting-list sections, exact waiting-list positions, the primary regular OSRS account as event-facing identity, all participant-facing answers public, and no website-username projection.
- Role-aware expanded signup-table columns for drafted-team captains/co-captains before/during the draft, excluding payment, private notes, security/recovery data, and audit history.
- Draft-finalization handoff that keeps the signup page intact and admin-authorized, but redirects non-admin signup-route requests to published team rosters.
- Signup close/reopen behavior.

#### Capacity and waiting list

- Confirm automatically below cap.
- Add later valid signups to waiting list.
- Derive private waiting-list position.
- Increase capacity and promote in signup order.
- Promote after confirmed withdrawal before draft lock.
- Stop automatic promotion after draft lock.
- Preserve signup order/status across ordinary edits.
- Cancel while open and rejoin only at the end of the current queue.
- Allow post-close participant withdrawal until draft start without participant self-restore.
- Allow pre-draft admin restoration only when account reservations remain available, using current capacity or the end of the waiting list without displacing prior promotions.
- Create idempotent durable in-site notifications for the linked participant and every enabled admin when a waiting-list participant is promoted; include the event/participant/trigger for admins and do not depend on Discord messaging.
- Transfer event-participant ownership to a different website account after strong confirmation only for wrong/duplicate ownership, while preserving event state/history and rejecting a destination already participating in the event.
- One `WITHDRAWN` inactive status for participant- or admin-initiated withdrawal, with no separate `REMOVED` target state and no required written reason.
- Admin-only binary unpaid/paid value and private notes.
- One pre-draft participant workspace covering active/waiting/withdrawn state, identity, accounts/EHB, answers/visibility, payment/notes, queue, team, search, and filters.
- Post-close/pre-draft admin correction using ordinary validation/reservation rules without changing queue position.
- Manual internal participant creation that bypasses public window/code but obeys required fields, capacity/waiting, and account uniqueness; external rosters remain outside the pool.
- Linked-participant notifications for admin withdrawal, restoration, and event-account changes, excluding payment, notes, and ordinary answer edits.

### Domain tests

- Exact-cap boundary.
- Simultaneous signup near final place.
- Waiting-list ordering with equal timestamps and deterministic sequence.
- Increasing cap by fewer/more places than waiting participants.
- Confirmed withdrawal promotion.
- No automatic promotion after draft lock.
- Cap cannot decrease.
- Signup edit does not change original queue order.
- Cancel/rejoin receives a new queue position.
- Post-close withdrawal cannot be self-restored.
- Admin restoration respects account reservations and never displaces a promoted participant.
- Participant and per-admin promotion notifications are each emitted once even when the command is retried.
- Identity transfer preserves event history, revokes old access, and rejects duplicate destination participation.
- Duplicate name detection is event-specific only.

### Browser acceptance

- Admin creates an event and opens signups.
- Public sees the signup board and approved signup fields, but not the draft, teams, or bingo board.
- Participant signs up and uses the Planning Pass 2 approved identity/edit path. Preserve current private-edit-link coverage only for existing legacy/imported migration records until Slice 4 completes the authenticated-signup transition.
- Admin increases cap from 50 to 60 and seven waiting participants are promoted.

### Completion criteria

- The complete signup period can be run without Google Forms.
- Ordinary website-draft participant signup does not use CSV. Future CSV, if needed, is limited to Slice 5 external/pre-formed roster workflow.

## 8. Milestone 4 — OSRS catalogue and board builder

### Objective

Allow admins to maintain source-specific OSRS data and build every known tile without custom code.

### Deliverables

#### Catalogue

- Admin create/edit/deactivate/reactivate for bosses/activities and source drops; item identities are managed through source drops.
- Super-Admin-only permanent deletion of genuinely unused catalogue rows after confirmation and a complete dependency check; referenced data is deactivated instead.
- Displayed and numeric drop rates.
- Efficient completion rates and EHB values.
- Images and active/inactive behavior.
- Super-Admin-only bulk import preview/apply with exact changes, conflict reporting, confirmation, and preview version/hash revalidation.
- Manual editing after import.
- Data source and update timestamp.

#### Tile requirements

- One or more bosses/activities per requirement.
- Eligible-drop selection.
- Target contribution.
- Duplicates allowed.
- Optional fixed contribution weight, configured by the board designer and defaulting to `1`.
- Multiple requirements per tile.
- Manual/custom objective with target quantity, objective-specific completion criteria, and explicit manual EHB.
- Automatic EHB calculation preview for every catalogue/drop tile; a missing estimate is a validation defect that cannot be bypassed with a manual override.

#### Board builder

- Configurable rows and columns.
- Add/remove tiles.
- Event-board-owned tiles only; no duplicate, cross-event copy, import, or reusable-template workflow.
- Drag one tile onto another to swap.
- Keyboard/touch select-and-swap fallback.
- Top-left compaction during shrinking.
- Block shrink if placed tiles exceed new capacity.
- Tile editor side drawer.
- Row, column, and total EHB.
- Row/column highlight on hover and focus.
- Balance spread warning.
- Structural validation.
- Require every board position to be filled before approval.
- Explicit board approval/unapproval, with any private competitive-content edit invalidating approval while preserving history.
- Board approval requires a completely filled grid but no per-tile evidence-instruction field.
- Derive unapproved board catalogue data and every EHB estimate from current catalogue rows, automatically invalidating/recalculating after relevant catalogue changes.
- Create the immutable competitive snapshot at **Approve board**, not publication.
- Keep approved board values frozen until explicit unapproval or a competitive edit returns the board to Draft and resumes live catalogue derivation.
- Add **Preview board** using the actual public responsive renderer: live data while Draft, the active frozen snapshot while approved, no administrator-only editing/EHB controls, and no approval/publication side effect.
- Publish the active immutable approval snapshot without recalculating it.
- Save and resume an incomplete private board across multiple admin sessions.
- Permit continued private editing while event signups are open or closed, until board publication.

### Representative acceptance tiles

- Five Zulrah unique-table drops with duplicates allowed.
- Full Voidwaker with duplicates disallowed.
- Theatre of Blood purples with Scythe of Vitur using a board-defined higher contribution weight while ordinary purples remain at `1`.
- Five Barrows and five Moons drops as two requirements.
- Timed four-player Theatre of Blood manual objective.
- Three Inferno completions as a repeated manual objective.
- A random low-value or troll drop added to a boss and selected for a tile.

### Tests

- Duplicate-allowed and duplicate-disallowed contribution rules.
- Multi-requirement completion.
- Manual target quantity.
- Board resize compaction and blocked shrink.
- Row/column EHB after tile swap.
- Relevant catalogue changes update every derived value on an unapproved board and its preview.
- Approved snapshot and preview remain unaffected by later catalogue changes.
- Unapproval resumes live catalogue derivation; reapproval creates a new immutable version without deleting the superseded one.
- Concurrent catalogue/board edits cannot create a mixed or partial approval snapshot.
- Preview never approves, publishes, or mutates board state.
- Admin catalogue mutation and Super-Admin-only delete/import authorization.
- Referenced catalogue deletion is blocked while genuinely unused deletion succeeds.
- Bulk import apply aborts if its preview is stale.
- Catalogue/drop tile with missing automatic EHB blocks approval.
- Manual/custom objective requires and uses its explicit manual EHB.
- Editing an approved unpublished tile returns the board to Draft.

### Completion criteria

- Every tile on the supplied example board can be represented.
- Published boards are historically stable.
- General rules and submission guidance are sourced from the permanent global Rules page and source-controlled public how-to pages rather than board tiles.

## 9. Milestone 5 — Teams and snake draft

### Objective

Allow admins to combine website-drafted teams with manually managed pre-formed teams and complete a private admin-operated snake draft without altering external rosters.

### Deliverables

- Derive team count from active drafted teams rather than a separate setup input.
- Derive balanced final roster sizes from included confirmed participants, with a preview of the larger size and the number of teams that will have one fewer player.
- Keep board-editor team-count/team-size estimates isolated to board-EHB planning.
- Event-unique team name, optional affiliation, and managed team-image upload; no arbitrary team image URL.
- Add pre-formed internal or external teams before or after the draft.
- Deliver CSV only for one selected external/pre-formed team; ordinary draft-pool participants continue through website signup or explicit Admin creation. Each data row represents one member: column one is the primary account, column two its required EHB, and later columns are additional accounts without EHB. Captain/Co-captain assignment remains manual. Preview validates the complete file and apply is atomic and replay-safe.
- Optional team affiliation/clan label.
- Manually add, remove, and move members on pre-formed rosters before event start with automatic structured history and no required typed reason.
- Exclude pre-formed teams and their assigned players from draft order and the available draft pool.
- Captain and co-captain assignment.
- Require every drafted team to have an actual Captain before draft start; co-captain alone is insufficient, and all captain/co-captain memberships occupy normal derived-size roster positions.
- Website-account captain/co-captain event/team role assignment, plus explicitly enabled emergency credentials when needed.
- Random initial team-order scramble.
- Skip teams with larger preassigned captain/co-captain rosters until lower-count teams catch up.
- Snake-draft turn calculation.
- Compact per-team `current/final` capacity display, including smaller-team denominators.
- Complete participant pool sorted by the EHB snapshot of each participant's built-in primary playing account; secondary-account EHB is not summed or substituted.
- Available/drafted status without removing drafted players.
- Alternative sorting by name, signup time, and status.
- Record pick.
- Repeatedly undo the latest active pick, including back to zero picks.
- Pause and resume.
- Draft lock.
- Finalize and automatically publish completed rosters plus effective pick order.
- After draft finalization succeeds, show a separate **Publish board?** popup/page with a **Publish board** action only when every board position is filled and the board is explicitly approved; dismissed/ineligible boards remain private with an admin action.
- Reopen a finalized draft before event start with strong confirmation and a required written reason, without unlocking drafted-team structure or unpublishing an already published board.
- Audit history.

### Domain tests

- Snake order for two, three, and six teams over several rounds.
- Pick ownership at round boundaries.
- Derived distribution for divisible and remainder participant totals.
- Preassigned captain/co-captain seats count toward derived roster size, and larger starting rosters are skipped until the others catch up.
- Co-captain alone does not satisfy drafted-team captain readiness.
- Impossible preassignment imbalance blocks draft start.
- Board-editor planning estimates do not create teams or constrain the draft.
- Participant cannot be picked twice.
- Repeated latest-pick undo restores each participant and turn correctly, including back to zero picks.
- Team configuration cannot change silently after first pick.
- The first recorded pick permanently locks drafted-team creation/removal/formation even after repeated undo returns to zero.
- Team display metadata remains editable after draft finalization but locks at event start.
- Waiting-list promotion stops when draft locks.
- Participant self-withdrawal stops when draft locks; admin withdrawal remains available after draft and during live events.
- Post-draft withdrawal ends current membership/future eligibility while preserving draft picks, historical membership, registered accounts, evidence, and contributions.
- Post-draft withdrawal revokes website event/team mutation access immediately; during live play its separate competitive drop-eligibility boundary remains the first full UTC minute after confirmation.
- No automatic post-draft promotion: admins contact waiting-list participants and explicitly select an available replacement from the ordered list.
- Roster-replacement membership links the chosen waiting participant to the vacancy prospectively without rewriting the draft.
- Replacement is optional; when no waiting participant is available, admins may create a validated unique-account internal replacement directly on the vacant team.
- Live withdrawal and replacement use separate next-full-UTC-minute boundaries, preserve pre-withdrawal evidence, grant no retroactive eligibility, and retain any vacancy gap.
- Event-start readiness requires one actual Captain or enabled emergency captain credential per team; co-captain alone is insufficient.
- Admin captain/co-captain assignment, promotion, demotion, and revocation remain available after draft/live start, with immediate Discord-identity-based authority and urgent missing-captain warnings that do not stop a live event.
- In-site vacancy notifications for all admins/remaining team captains, replacement notifications for the replacement/current team captains, and role-change notifications for the affected participant.
- Pre-formed teams receive no snake-draft turns.
- Pre-formed roster members cannot be selected in the website draft.
- Adding a pre-formed team after finalization preserves draft order and pick history.
- Finalization rejects any unassigned confirmed draft-pool participant and any roster distribution differing by more than one.
- Public finalized results contain the effective active pick order but not undone attempts or internal controller/audit data.
- Draft finalization never publishes the board itself; the eligible post-finalization **Publish board** action uses a separate transaction.
- Reopening requires a reason, withdraws public roster/pick projections, preserves transition history and board publication, and permits repicking without structural team changes.

### Browser acceptance

- Admin scrambles six teams.
- Admin adds an already-drafted external clan team before the draft and confirms it receives no turns.
- Records multiple rounds of picks.
- Captains can maintain an overview of all participants.
- Draft remains private until finalization.
- Finalized rosters become public together.
- Admin adds or corrects a pre-formed team after draft finalization without modifying historical picks.

### Completion criteria

- A real draft can be operated from voice chat without spreadsheets.

## 10. Milestone 6 — Evidence submission and review

### Objective

Replace Discord drop-channel submissions and manual spreadsheet updates.

### Deliverables

#### Participant and captain workflow

- Board-first tile selection.
- Fixed event/team derived from account.
- Ordinary participants are locked to themselves; captains/co-captains select a current teammate.
- Credited playing account is derived from that participant's active account at server submission time; submitters receive no account selector.
- Eligible boss/drop selection.
- Credited weight comes from the selected board-requirement snapshot and defaults to `1`.
- Submitters cannot override the selected drop's board-defined contribution weight.
- No separately typed drop-received time; the screenshot's clan-event UTC overlay is the evidence of when the drop occurred.
- Reviewer validation that the credited account was active at the screenshot time, supported by UTC swap history.
- One image upload.
- Optional note.
- Pending history.
- Edit/withdraw pending submission.
- Approved/rejected history and reviewer feedback.
- A **Resubmit** action on rejected evidence that prefills ordinary structured values, preserves the original credited participant/account as read-only, requires a new screenshot, creates a linked historical record, and remains subject to the upload cutoff.
- Participant-owned pending/rejected history and pending edit/withdraw through cutoff; captains retain complete team history.
- Participant navigation to roster before board publication and the existing team board afterward.
- Participant/captain swaps only during `LIVE`; event end freezes swaps/focus while evidence grace continues through cutoff.

#### Evidence storage

- R2 adapter.
- Streamed uploads.
- Size and media-type limits.
- Image decoding validation.
- Checksums and generated keys.
- Original evidence plus retained historical asset versions.
- Safe evidence display.
- Separate enabled/disabled evidence-code mode with custom or generated codes, immediate/scheduled activation, and immutable per-submission code snapshots.

#### Admin review

- Current active-event review queue with team/tile/status filters and newest submissions first.
- Evidence preview and full-size view.
- Tile/player/drop/weight metadata plus immutable submission time.
- Prior approved evidence for context.
- Edit metadata with audit before/after.
- Approve.
- Reject with required note.
- Treat duplicate or unusable evidence as rejection rather than separate review states.
- Derive credited participant from any corrected credited playing account.
- Require a reason for tile/requirement, drop, or credited-account corrections.
- Keep server submission time, snapshot weight, calculated contribution, and evidence image reviewer-immutable.
- Notify the linked credited participant and current linked team captains/co-captains after rejection.
- Public projections expose only active Approved evidence, metadata, and stored credited-character snapshots; private non-Approved history remains scoped to its authorized participants, current team leadership/emergency authority, and Admins.
- Do not retain a hidden-but-still-approved evidence state; use reasoned approval reversal followed by corrected/redacted resubmission when an image must be removed.
- Reverse approval with reason.

### Transaction tests

- Two admins cannot approve one submission twice.
- Concurrent approve/reject cannot produce conflicting decisions; the stale action returns the newer result.
- Rejection requires a reason and emits each approved recipient notification once.
- Concurrent/repeated resubmission cannot create two direct children; the corrected attempt retains the predecessor's credited account after a later swap and requires a new image.
- Correcting the credited account derives its participant and revalidates account activity.
- Captain request tampering cannot change team.
- Credited player must belong to team.
- One drop contributes to one tile.
- Posted weight tampering cannot override the selected drop's board-defined weight.
- Contribution capped at remaining requirement target.
- Reversal removes its contribution and reallocates freed capacity to later eligible approved evidence.
- Replacement evidence preserves the original.

### Completion criteria

- Approval and reversal produce correct auditable contribution records.
- Evidence visibility follows status/team scope and every approved evidence record is public.

## 11. Milestone 7 — Public boards, progress, and rankings

### Objective

Deliver the participant-facing centerpiece and automatic competitive calculations.

### Deliverables

#### Public board

- Public bingo overview of all teams.
- Compact board previews.
- Team name, rank, tiles, lines, and finisher status.
- Expand/zoom into one board.
- Previous/next team navigation.
- Reduced-motion support.
- Mobile list/grid behavior.
- Tile detail view.
- Approved evidence list.
- No hidden-evidence/player placeholder; private non-Approved evidence is omitted from public projections.

#### Calculation engine

- Requirement progress.
- Tile completion and obtained completion time.
- Row and column completion.
- No diagonal calculation.
- Full-board completion.
- Ranking tuple.
- Provisional winner.
- Player EHB contribution leaderboard.
- Rebuildable progress caches.

#### Live behavior

- SignalR invalidation after approval/reversal.
- Refresh fallback.
- No pending progress leaked to another team or public view.

### Calculation tests

- Center tile contributes to its row and column but counts as one tile.
- Overlapping completed lines count separately.
- Full-board completion uses immutable submission time, not review time.
- Finishers outrank non-finishers.
- Non-finishers rank by lines, then tiles, then EHB tie-break.
- Reversal can remove a line, full board, and provisional win.
- Public contribution identity comes only from active Approved evidence's stored credited-character snapshot.

### Browser acceptance

- Visitor compares six teams and opens a board.
- Visitor opens a tile and sees approved evidence.
- Approval updates board without full manual spreadsheet refresh.
- Mobile board remains readable.

### Completion criteria

- Public state is reproducible from approved evidence.
- No private pending evidence is exposed.

## 11.1 Milestone 7A — Concurrent administration hardening

### Objective

Make the completed board-builder and draft workflows safe when multiple administrators have the same event open.

### Deliverables

#### Board editing

- Show when another administrator is actively editing the board.
- Open the board in view mode by default so multiple administrators can review it together without claiming editing control.
- Add one renewable board-editor lease that is acquired only through an explicit Edit board action.
- Keep other administrators in a live view-only mode and allow confirmed takeover or manual release.
- Add one optimistic concurrency version to the board aggregate, covering its tiles, requirements, layout, and publication state.
- Include the loaded version with every board mutation, including tile edits, moves, swaps, removal, and resizing.
- Reject stale saves with a clear reload-and-review message while preserving the newer saved version.
- Renew the editing lease only while the editor interacts with the board and release it after five minutes of inactivity, so an abandoned open tab does not permanently lock the board.

#### Draft control

- Add a single active draft-controller lease.
- Restrict start, pick, undo, pause/resume, and finalization to the controlling administrator.
- Give other administrators a live read-only view showing the controller, current turn, and picks.
- Allow an explicit confirmed takeover when the controller is unavailable.
- Audit controller acquisition, release, and takeover.
- Retain serializable pick transactions and database uniqueness constraints as final integrity safeguards.

### Tests

- Two admins cannot hold board editing control simultaneously, and a racing claim cannot silently overwrite the current editor.
- Concurrent move, resize, and tile edits either complete atomically or return a recoverable conflict without corrupting positions.
- Two admins cannot start or mutate the same draft concurrently.
- A non-controlling admin can observe current turns and picks but cannot record or undo a pick.
- Controller takeover transfers write authority without changing pick history or turn order.
- Retried or racing draft requests cannot assign one participant twice or create two picks for one turn.

### Browser acceptance

- A second admin opening an actively edited board sees the other admin’s presence.
- Multiple admins can review the same board together without taking the editing lease.
- A confirmed takeover immediately returns the previous editor to view mode.
- A stale board form is still rejected as a final integrity safeguard.
- A second admin opening an active draft sees a live read-only view and the current controller.
- A confirmed takeover immediately makes the previous controller read-only and records who transferred control.

### Completion criteria

- Multiple administrators can safely observe the same event without silently overwriting board changes or competing for draft actions.

## 12. Milestone 8 — Event operations and finalization

### Objective

Allow admins to safely run the event lifecycle and create official historical results.

### Deliverables

- Operations dashboard.
- Start event and publish board controls.
- Scheduled event end and submission grace period.
- Automatic scheduled end using the configured instant even after delayed processing.
- Early admin end with strong confirmation, required reason, preserved scheduled end, and unchanged submission cutoff.
- Manual submission reopening with cutoff and reason.
- Distinction between obtained window and submission window.
- Every review shows immutable submission time; only post-end submissions add minutes after event end and authoritative event end as **Latest clan event time** in UTC.
- Manual visual comparison of the screenshot plugin timestamp with that event-end boundary; no second typed time, screenshot OCR, or fixed upload-hours rule.
- Final-review checklist.
- Click-through to filtered underlying records.
- Automatic blocker resolution.
- Manual “mark resolved anyway” with confirmation and reason.
- Completion-time comparison and correction.
- Provisional placements.
- Admin finalization confirmation.
- Official placement snapshots.
- Preserve website-account captain/co-captain role history while lifecycle rules remove mutation authority.
- Disable explicitly enabled emergency captain credentials automatically at submission cutoff; never generate them from captain assignment/finalization.
- Unfinalization with reason.
- Confirmed archive with no typed reason, read-only historical event visibility, and reasoned unfinalization.
- Preserved pre-live event cancellation with confirmation/reason and suppression of every later scheduled transition.
- One production current/public operational event, enforced by normal application transitions while the Development-only scenario seeder and automated fixtures retain explicit multi-state scenarios.
- Archived participant read-only access to their own rejected/withdrawn evidence without a separate dashboard.

### Tests

- Drop after event end remains invalid during grace period.
- Evidence obtained in time can be submitted during grace period.
- Scheduled end persisted late still uses the configured effective instant.
- Early end requires a reason, preserves the scheduled end, and does not move the submission cutoff.
- A populated pre-live cancellation preserves history and no scheduled worker later reactivates it.
- Archiving preserves official results/URLs and removes the event from current operational queries.
- Production permits non-overlapping signup-open/closed windows while retaining a singleton live/review/finalized current event; Development fixtures are internally marked and never exempt Production commands.
- Reopening upload window does not extend obtained window.
- Finalization blocked while unresolved conditions remain.
- Manual override clears only blocker status and does not mutate submissions.
- Finalization snapshot matches recalculated rankings.
- Unfinalization preserves previous official snapshot.
- Submission cutoff disables enabled emergency credentials without expiring website-account captain role history.
- The real-clock scheduled-end transition is manually verified during Milestone 10's full rehearsal; Milestone 8 verifies its state rule automatically.

### Completion criteria

- Admin can move an event from signup through archived history without direct database edits.

## 12.1 Milestone 8A — Functional expansion and workflow stabilization

### Objective

Define and implement the explicitly approved functional expansion before spending more time finalizing UI that the new workflows may change.

This milestone does not automatically promote the entire former post-version-one backlog. Planning pass 2 authorizes a scope review and the addition of functionality in principle; each selected feature still needs an explicit product decision, acceptance criteria, and impact review before implementation.

### Entry checkpoint

- Preserve the current Pass 12 implementation and all previously approved UI passes as a recoverable checkpoint.
- Run focused tests and a release build for the touched checkpoint where needed to distinguish existing defects from later feature regressions.
- Record known incomplete responsive, permission, error, keyboard, accessibility, and no-JavaScript states without polishing them solely to obtain a UI approval that may soon be invalidated.
- Do not run the full Milestone 9 manual regression at entry. That regression belongs after the selected functional workflows stabilize.

### Planning gate

- Inventory the requested functionality and classify each item as selected now, explicitly deferred, or rejected.
- For every selected item, identify affected roles, workflows, routes, authorization policies, audit events, domain rules, persistence, migrations, privacy, concurrency, realtime behavior, operations, and existing UI passes.
- Resolve conflicts with `PRODUCT_REQUIREMENTS.md`, `DATA_MODEL.md`, and `TECHNICAL_ARCHITECTURE.md`; update those sources of truth before or with implementation.
- Define acceptance criteria, representative edge cases, migration/backfill behavior, and focused test coverage.
- Order the selected work as vertical slices. Cross-cutting identity and authorization foundations must precede dependent participant or submission workflows.
- Produce a UI impact map that marks existing screens as unaffected, reusable with changes, or requiring a later redesign.

### Selected scope and dependency order

Planning Pass 2 has approved the target workflows for hybrid identity, trust-based character links, event-unique character assignment, My accounts, signup/account questions, participant administration, teams/draft, board publication, participant/captain live access, evidence resubmission/public visibility, lifecycle cancellation/archive, current-event selection, role/account administration, and historical access. `FUNCTIONAL_WORKFLOWS.md` contains their capability contracts and acceptance scenarios.

The dependency/migration order below is approved as the implementation sequence. Public tile artwork/preview is governed by the approved managed-image/board rules. Slice 10's explicit Wise Old Man EHB fetch and bounded competition synchronization/activity contract are approved in `SLICE_10_IMPLEMENTATION_PLAN.md`.

### Approved implementation sequence

These are the ten functional delivery slices inside Milestone 8A. They are not a renumbering of the page passes in `UI_OVERHAUL_ROADMAP.md`:

1. Website accounts, password/Discord authentication, Super Admin ownership, recovery, role mutation, and account disable/restore.
2. My accounts, trust-based OSRS-character links, event assignments, uniqueness rules, and legacy identity migration.
3. Event creation, scheduling/readiness, current-event selection, cancellation, archive, and lifecycle blockers.
4. Signup questions, per-regular-account EHB, unlisted public signup table, My events, capacity/waiting list, participant administration, and promotion notifications.
5. Teams, captain/co-captain authority, external teams, roster rules, and draft.
6. Catalogue permissions/import, live Draft-board derivation, approval snapshots, public-style preview, and publication.
7. Participant/captain live navigation, active-account swaps, team focus, and role-aware visibility.
8. Evidence creation, resubmission/review, public approved evidence, and removal of privacy-request behavior.
9. Event end, reasoned recovery from a premature manual or automatic end before finalization, live replacements, finalization/history, archive access, and operational notifications.
10. Wise Old Man explicit EHB fetching plus cached Live competition synchronization and participant/team activity projections.

Each slice finalizes its exact manual cases in `MANUAL_TEST_CHECKLIST.md` before handoff. The checklist is durable repository documentation so the user may run it immediately or return to it later; it does not replace automated coverage.

Before implementation begins for each remaining major slice, run one independent read-only implementation-readiness review against the approved slice plan, current code, and source-of-truth documents. Resolve only concrete blockers and explicit product decisions, incorporate the accepted corrections into that slice's plan, and then implement. The review must also confirm manual-test reachability and minimum Development fixtures, complete removal coverage for deprecated behavior, an explicit operational path for fail-closed retained data, and a lean complexity budget for proposed persistence/services/routes/policies/jobs/abstractions. It freezes the approved scope and non-goals, distinguishes required dependencies from optional or adjacent work, and defines which newly discovered product changes require user approval. Optional suggestions do not become implementation requirements, and unrelated defects are recorded separately unless they block safe implementation or verification. Any material user-approved change during implementation or remediation must be recorded in the slice plan and affected source-of-truth documents before that changed behavior is implemented. The post-implementation independent review then compares the exact base-to-current diff against the final updated plan, checking both missing approved scope and unapproved additions or complexity. Do not turn this into repeated speculative review: a second planning review is justified only when implementation uncovers a genuine contradiction or missing product rule. This gate applies to Slice 8 and must be repeated for Slices 9 and 10.

After that post-implementation review and its remediation clear, run one bounded manual-acceptance preflight before handing the checklist to the user. Walk the exact documented journeys against the authoritative Development reset, use the intended accounts and roles, and follow real rendered controls, forms, notification destinations, and consecutive lifecycle steps. The preflight must catch broken navigation, filter/authorization mismatches, non-rendering destinations, missing fixtures, and a successful step that leaves the next step unreachable. One authenticated route-level journey may cover several checklist steps; do not create one test per sentence or reopen architecture, scope, visual design, or the complete suite. Subjective visual/responsive checks remain for the user. A failed journey receives only the smallest bounded correction before the preflight resumes.

Slices 1, 2, and 3 are implemented, independently cleared, verified, manually accepted where applicable, committed, and pushed on their accepted branches. Their detailed records remain in `SLICE_1_IMPLEMENTATION_PLAN.md`, `SLICE_2_IMPLEMENTATION_PLAN.md`, and `SLICE_3_IMPLEMENTATION_PLAN.md`. The bounded seven-pass Slice 4 signup plan is approved in `SLICE_4_IMPLEMENTATION_PLAN.md`; implementation has not started and must remain inside one approved pass per task.

### Post-functional Application Atlas gate

**Atlas status (2026-08-11):** Complete as a read-only consolidation/audit deliverable at accepted source commit `2301d63ae166873a750266ce5ee6a087f3039054`. The durable source is `APPLICATION_ATLAS.md` and the standalone interactive review surface is `APPLICATION_ATLAS.html`. All ten functional slices are accepted. The Atlas-derived UI impact map and dependency-based Milestone 9 pass order are now recorded in `UI_OVERHAUL_ROADMAP.md` section 3.8. F-04 (Live identity) and F-06 (permanent Rules/how-to) remain explicit product-decision gates; the map does not authorize behavior changes.

After Slice 10 is accepted and before the UI overhaul begins, create a product-facing Application Atlas of the completed website. Its durable source is `APPLICATION_ATLAS.md`; its primary review surface is a standalone interactive HTML visualization, with an optional PDF summary. Cover role-based navigation, lifecycle transitions, workflow swimlanes, capability-by-state, scheduled automation, data/history ownership, notification triggers, and a complete feature inventory. Classify functionality as required, awkward, duplicated, rare/edge-case, possibly unnecessary, unreachable, or obsolete, and identify missing navigation or workflow gaps.

The Atlas must include a dedicated lifecycle consistency audit rather than only a high-level state diagram. For every event state and valid recovery path, map the permitted transition, initiating role, available actions, progression blockers and resolution destinations, scheduled and actual timestamps, submission cutoff behavior, and the authoritative route/service/policy enforcing it. Show related signup, draft, roster, board-publication, evidence, final-review, official-results, and archive substates separately so a feature-level lock is not mistaken for a whole-event prohibition. Compare this map against the implemented guards, UI wording, and source-of-truth documents; record contradictory labels, unreachable actions, duplicated guards, and missing Admin progression guidance as explicit review findings.

This is a read-only consolidation/audit gate: it does not authorize removal, redesign, or new functionality until the user reviews and approves a resulting change.

Deferred Ponytail cleanup is non-blocking and is not an Atlas defect: remove 51 unreferenced Bootstrap/jQuery distribution variants/source maps while retaining referenced assets/licenses; retire the stale seven-argument `EventParticipant` constructor and integer `SnakeDraftOrder.GetNextEligibleTurn` test overload after fixture updates; remove `Team.ImageUrl` and ignored `Update` slug/imageUrl parameters; remove zero-caller `DraftSession.ConfigureTargetSize`; remove the unreferenced `ApplicationDependencies` wrapper; and remove constructor dependencies retained only by no-op assignments in Manage/Participant/Finalize. The approximately 82,540-line estimate is overwhelmingly vendor distribution files and does not represent architectural complexity.

Only after all ten Milestone 8A functional slices are complete, functionally regressed, and reviewed through the Application Atlas does work proceed to the separate big-roadmap **Milestone 9 — UI overhaul and regression**. Milestone 9 performs the complete site-wide UI pass and full regression through the page passes in `UI_OVERHAUL_ROADMAP.md`. It must not begin merely because a similarly numbered UI page pass is available.

### Selected public-guidance slice

- Replace event-specific free-text public rules with one permanent global public Rules page.
- Allow any enabled administrator to edit that Rules document at any time with validation, optimistic concurrency, and automatic history, but no typed reason, participant notification, or event-readiness dependency.
- Add stable anonymous routes for source-controlled how-to pages, beginning with **How to submit drops**; do not build an in-application how-to editor.
- Link event and submission surfaces to the relevant global pages while keeping general guidance out of board-tile data and approval rules.
- Migrate any useful existing public-rules content into the global document without retaining an event-owned authoring model.
- Cover anonymous access, administrator authorization, concurrent edit failure, and absence of event-lifecycle side effects.

### Delivery and regression

- Implement each selected feature through the appropriate Web, Application, Domain, and Infrastructure layers.
- Preserve historical competitive records, authoritative evidence/progress calculations, server-side authorization, auditability, privacy, transactions, and concurrency protections.
- Add domain, application, integration, and browser coverage in proportion to each slice's risk.
- Run focused regression after each slice and broader automated regression when a slice changes a shared boundary.
- Finalize and record the slice's exact manual checks in `MANUAL_TEST_CHECKLIST.md` before handoff.
- Apply the established flagship visual system, shared controls, responsiveness, accessibility, feedback, and progressive-enhancement rules to every newly created page from its first implementation; defer only final cross-site polish to Milestone 9.
- Localize every page reachable by anonymous visitors, normal Users, captains/co-captains, or emergency captains completely in English and Danish through the existing language switch. Admin/Super-Admin-only pages may remain English-only.
- Update the UI pass descriptions and order when the final feature-impact map is known.

### Completion criteria

- Selected functionality and deferred scope are explicitly recorded.
- Every selected slice satisfies its acceptance criteria and focused regression checks.
- Required migrations, backfills, operational changes, and documentation are complete and verified at the appropriate level.
- The UI impact map and revised Milestone 9 pass order are approved.
- No unresolved product-rule or architecture decision blocks UI finalization.

## 13. Milestone 9 — UI overhaul and regression

### Objective

After all ten Milestone 8A functional slices are complete, run a complete UI pass over the functionally stabilized application, then full regression, before any production deployment work begins. This separate Milestone 9 reuses the already approved flagship visual system and interaction decisions; it does not discard approved UI, but it must apply the late shared rules consistently to every page and state, including pages touched in earlier passes.

### Deliverables

#### Workflow and navigation

- Review every admin, captain, and public workflow from start to finish.
- Revisit every page from Passes 1–13, including already reviewed pages, so late global rules are applied site-wide rather than only to screens changed by Milestone 8A.
- Make the current event and team context visible on every relevant page.
- Separate accounts, events, and workflow records so entries from different events are not visually bundled together.
- Improve navigation, breadcrumbs, return paths, empty states, confirmation messages, and error messages.
- Replace developer-oriented wording with community-friendly labels and explanations.

#### Visual and interaction consistency

- Establish consistent page widths, spacing, forms, tables, buttons, dialogs, and status indicators.
- Replace oversized controls and panels with the compact density and alignment system defined in `UI_OVERHAUL_ROADMAP.md`.
- Progressively enhance frequent inline actions so only the affected component updates, while retaining standard Razor form fallbacks.
- Introduce semantic success, error, warning, and information notifications instead of styling every response as success.
- Add a top-right role-based action inbox for pending admin review, plus durable participant/captain notifications for rejected submissions.
- Revisit dense event creation, board editing, draft, review, finalization, and account pages.
- Reuse the combined date-time picker across date/time workflows.
- Improve desktop and mobile layouts without changing approved business rules.
- Complete keyboard, focus, colour-contrast, and permission/error-page checks.

#### Regression

- Repeat every Milestone 1–8A manual test against the overhauled interface.
- Add browser automation for stable critical paths discovered during manual testing.
- Recheck multi-admin board and draft control.
- Recheck mobile captain evidence upload and public bingo overview views.
- Record and resolve UI edge cases before declaring a release candidate.
- Complete the ordered page passes and approval gates in `UI_OVERHAUL_ROADMAP.md`.

### Completion criteria

- The complete local application is understandable without developer knowledge and passes the full regression list.

## 14. Milestone 10 — Local rehearsal and production preparation

### Objective

Prove the polished application end to end and prepare every deployment asset before creating the production server.

### Production preparation

- Production Docker image and Compose configuration.
- GitHub Actions build, test, and image workflow.
- Controlled database migration and rollback commands.
- Cloudflare R2 integration tested locally with restricted credentials.
- Configuration and secret inventory.
- Clean-production data initialization: apply migrations to an empty database, initialize the reviewed `src/Bingo.Web/data/osrs-catalogue.json` snapshot, provision the intended Super Admin through controlled operator setup, and verify that no Development/test accounts or workflow records are present.
- Health checks and background-service health reporting.
- PostgreSQL backup, encryption, retention, and restore scripts.
- Pre-migration and final-event backup procedures.
- Monitoring and alert configuration prepared but not yet pointed at production.
- Hetzner bootstrap, firewall, Caddy, DNS, maintenance, hibernation, deletion, and restoration runbooks.

### Rehearsal data

All rehearsal data is fictional and disposable. It is never copied into the production database; only the reviewed OSRS catalogue snapshot is carried into the initial production environment.

- Fictional event.
- At least six teams.
- Representative captain/co-captain accounts.
- 5x5 board containing every important objective pattern.
- Enough fictional evidence to complete tiles, lines, and a board.
- Waiting list, withdrawal, and promotion examples.
- Private non-Approved evidence remains out of the public rehearsal projection.
- Incorrect approval and reversal example.

### Production-scale capacity rehearsal

The target is not merely 100 registered players. The application must be rehearsed with 100 simultaneous viewers and realistic bursts on production-sized resources before the first live event.

- Run the release build with the same CPU and memory limits as the intended low-cost VPS.
- Seed at least 100 participants, six to eight teams, a full 5x5 or 6x6 board, realistic approved evidence, and image-heavy tile history.
- Test 100 users opening public boards over a short period, then remaining connected through SignalR for at least 30 minutes.
- Trigger repeated approvals and reversals while those viewers are connected and measure the resulting refresh burst.
- Test at least 10 simultaneous evidence uploads near the configured maximum size and 20 simultaneous evidence-image views.
- Test admin review, public board switching, and captain submission while the public-view load is active.
- Run Wise Old Man synchronization as a cached background/manual operation, never as one external API request per page view.
- Record request latency, error rate, CPU, memory, database connections and query time, network throughput, SignalR disconnects, and evidence-storage latency.

Initial release targets:

- Fewer than 1% failed application requests during the rehearsal.
- Normal cached/read pages have a p95 response time below 1.5 seconds.
- Live progress becomes visible within four seconds under load.
- Sustained CPU remains below 80%, memory remains stable without swapping or out-of-memory restarts, and the PostgreSQL connection pool is not exhausted.
- The test is repeated on a temporary instance of the intended VPS size. The instance may be deleted immediately afterward and is not the final deployment.

Known load-sensitive areas to resolve or validate:

- Public progress currently has a 30-second safety reload, producing about 3.3 page requests per second with 100 connected viewers even when nothing changes.
- A SignalR progress notification currently causes connected viewers to reload within the same short window. Add jitter or replace full-page reloads with targeted data refreshes before the production-scale rehearsal.
- Evidence images should be served by object storage/CDN with viewing-sized derivatives so the application process does not proxy many full-resolution files concurrently.
- Expensive public-board queries must be measured and indexed or cached where the rehearsal identifies a bottleneck.

### Rehearsal sequence

1. Create event.
2. Open signup.
3. Fill capacity and waiting list.
4. Increase capacity and verify promotion.
5. Close signup.
6. Continue editing the incomplete private board created before or during signup.
7. Configure draft teams and add a pre-formed external team.
8. Run and undo part of a snake draft, confirming the pre-formed team receives no turns.
9. Finalize draft and then add/correct another pre-formed roster without changing pick history.
10. Build, resize, balance, and publish board.
11. Assign website-account captain roles and explicitly exercise one disabled-by-default emergency credential.
12. Submit normal, duplicate, weighted, and manual evidence.
13. Reject invalid evidence and submit a corrected attempt as a new submission.
14. Approve, reject with notification, reverse, and resubmit corrected evidence.
15. Verify live boards and rankings.
16. Leave the application running across the scheduled event-end time and confirm it automatically enters grace/final review within one lifecycle polling interval.
17. Reopen submissions without extending obtained window.
18. Resolve and override finalization blockers.
19. Correct a completion time.
20. Finalize results.
21. Verify emergency credential cutoff disablement and historical website-account-role authorization.
22. Archive event.
23. Create a local release-candidate backup.
24. Restore into a clean local environment.
25. Verify historical results and evidence after restoration.
26. Run the production-scale capacity rehearsal and save its measurements with the release candidate.

### Pre-deployment release gate

- No critical or high-severity defect remains.
- Domain, authorization, integration, and browser suites pass.
- Current desktop and mobile browser acceptance tests pass.
- Backup and clean local restore succeed.
- Admins have rehearsed review, reversal, and finalization.
- Captains have trialed mobile screenshot submission.
- Evidence upload limits are tested with realistic screenshots.
- The production-scale capacity targets pass on the intended VPS size, or the VPS size is increased before the event.
- Public approved-evidence visibility and reversal/resubmission behavior are verified.
- A manual emergency fallback procedure exists.

### Completion criteria

- A tagged release candidate and all deployment, backup, restore, and rollback assets are ready before the production VPS is created.

## 15. Milestone 11 — Production deployment and post-deployment validation

### Objective

Deploy the already approved release candidate as the final implementation step, then perform focused production checks and corrections.

### Deployment

- Create and harden the Hetzner VPS.
- Configure Docker, Caddy, HTTPS, DNS, PostgreSQL private exposure, and production secrets.
- Create the R2 production bucket and restricted credentials.
- Initialize a clean migrated production database, load the reviewed OSRS catalogue snapshot, and provision the intended Super Admin through the controlled operator path; do not import Development/test accounts, events, signups, participants, teams, boards, evidence, notifications, or audit history.
- Deploy the exact rehearsed image and run controlled migrations.
- Enable backups, monitoring, uptime checks, logs, resource alerts, storage alerts, and provider billing alerts.

### Post-deployment validation

- Run public, admin, and captain smoke tests over the production domain.
- Verify signup, image upload/R2 retrieval, review, live progress, and SignalR updates.
- Verify scheduled background services and health checks.
- Create an encrypted production backup and restore it into a clean temporary environment.
- Exercise rollback once before the first real event.
- Correct deployment-specific defects, redeploy, and repeat affected checks.
- Verify maintenance, hibernation, server deletion, DNS restoration, and durable-data recovery documentation.

### Completion criteria

- Production passes smoke, backup, restore, monitoring, and rollback checks without changing the approved application scope.

## 16. Cross-cutting test matrix

### 16.1 Domain unit tests

Prioritize exhaustive deterministic tests for:

- Waiting-list ordering and promotion
- Draft order
- Board resize packing
- Contribution and duplicate rules
- Higher weights
- Manual quantities
- Requirement/tile/line/board completion
- Ranking
- Reversal
- Finalization blockers

### 16.2 Integration tests

Use real PostgreSQL in containers for:

- Constraints
- Transactions
- Concurrency
- Migrations
- Snapshot immutability
- Authorization queries
- Audit persistence

### 16.3 Browser tests

Cover only high-value workflows rather than every visual detail:

- Signup and editing
- Waiting-list administration
- Draft
- Captain submission
- Admin review
- Public board
- Board builder
- Finalization

### 16.4 Manual exploratory tests

- Use `MANUAL_TEST_CHECKLIST.md` as the durable per-slice manual test index and finalize its exact cases before each slice handoff.
- Mobile photo upload
- Slow or interrupted connection
- Large screenshot behavior
- Two admins reviewing simultaneously
- Event crossing midnight/timezone boundaries
- Reduced motion
- Keyboard board editing
- Screen-reader labels on critical controls

### 16.5 Security tests

- Route authorization
- Team identifier tampering
- Private signup-token guessing/rate limiting
- CSRF
- Unsafe upload formats
- Oversized upload
- Stored script content in names/comments
- Private non-Approved evidence access
- Expired account access
- Secret leakage in logs

## 17. Data and privacy tasks

Before production:

- Decide retention period for rejected/withdrawn evidence.
- Decide whether original evidence is retained indefinitely with archived events.
- Publish a short privacy notice for signup data and screenshots.
- State that player names, teams, approved drops, and approved evidence may become public.
- State that the paid/unpaid value and Admin notes remain private; document that waiting-list OSRS identities and every participant-facing signup answer appear on the unlisted signup table.
- Define who can request removal or correction after an event.
- Define admin responsibilities for sensitive chat visible in screenshots.

These decisions do not require a complex legal system but must be written clearly for community members.

Detailed decisions may be deferred until the signup and evidence milestones, when the real controls and storage behavior are being implemented. They must be finalized before production signup opens and before real evidence is uploaded.

## 18. Operational risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Screenshot storage grows unexpectedly | R2, upload limits, image optimization, usage monitoring |
| Admin approves wrong evidence | Public evidence review, audit, reversible approval |
| Two admins approve simultaneously | Transaction and concurrency control |
| VPS fails during event | Frequent off-site database backups and documented restore |
| Database restored but screenshots missing | R2 inventory/checksum validation |
| Captain credential shared | Separate accounts, reset/disable controls, team scope |
| Catalogue rate changes | Published event snapshots |
| Board rule cannot represent a future tile | Manual objective escape hatch and multiple requirements |
| Signup spam | Event signup code, rate limiting, admin removal |
| Free disk exhausted | Screenshots off-server, disk monitoring, log retention |
| VPS deleted without recoverable backup | Mandatory verified hibernation checklist |
| Scope expansion delays release | Version-one boundary and deferred-feature list |

### Planning Pass 2 feature register

This register began as the post-version-one backlog. Items explicitly selected during Planning Pass 2 move into Milestone 8A while unselected items remain deferred candidates. Every selected item still requires a complete workflow contract and regression pass.

#### Hybrid website account, Discord linkage, and submission permissions — identity foundation selected

- Start normal account creation through Discord, then require public-username/password onboarding.
- Let Discord and public-username/password sign-in resolve the same durable website account. The public username is also the password-login username.
- Discord-server membership is not required. This allows participants from external clans to sign in without joining the community server.
- One website account may own at most one event-participant record per event and may participate in several events.
- Access is granted by linking the authenticated website account to an event participant, not by checking Discord guild membership, current Discord ID, or OSRS character name.
- Keep email out of version-one recovery. Let admins issue secure reset links under the approved user/Admin/Super Admin boundaries.
- Allow password-authenticated users to link, unlink, or directly replace Discord at any time while preserving the website account and all event history. Link/replace require an unused Discord ID; Super Admin unlink shows an operator-recovery warning.
- The participant may edit signup data only while signup is open.
- An authenticated participant may submit evidence only for themselves and only for their current event team.
- Captains and co-captains may submit evidence for any participant on their own team.
- Participants are locked to themselves when submitting; captains/co-captains may choose any current teammate, and the server snapshots that participant's current active account without an account selector.
- Before/during the draft, drafted-team captains/co-captains see the confirmed signup table with participant-answer columns expanded; external-team captains do not. After finalization, the signup page remains available to admins while non-admin requests redirect to team rosters.
- Captains/co-captains can set non-authoritative tile/row/column focus highlights.
- Admins retain event-wide submission and correction access.
- Captain/co-captain is an event/team role on the website account rather than a separate normal login or current Discord ID.
- Keep temporary captain accounts disabled by default and available only as an explicitly enabled emergency fallback.
- Preserve external/pre-formed roster records without requiring every member to use public signup or own a website account. One or more explicitly enabled team-scoped emergency captain credentials provide submission access without claiming roster records.

#### Event cancellation, archive, and current-event policy — selected

- Keep discard limited to empty experimental events.
- Add a terminal preserved cancellation path for populated events that have not entered live play. Require strong confirmation and a reason, stop scheduled transitions/mutations, retain every record, and expose only a generic status when the event was already public.
- Archive only finalized events with confirmation and no typed reason. Keep public URLs/results intact and move the event from current navigation to previous events.
- Keep production to one current/public operational event from signup publication through finalization, while allowing unlimited private drafts and archived/cancelled history.
- Enforce the production rule in normal application transitions rather than a database uniqueness constraint. Preserve the Development-only seeded lifecycle matrix and automated fixtures, and require explicit event IDs/slugs wherever multiple test scenarios exist.
- Keep archived participant-owned rejected/withdrawn evidence readable but immutable; the public historical board remains the primary destination.

#### Website-account disable and restore — selected

- Let an enabled Admin disable a `USER`; reserve disabling/restoring an `ADMIN` for the Super Admin.
- Prohibit self-disable and disabling the active Super Admin.
- Require strong confirmation and a reason for disable, invalidate all sessions immediately, and preserve every participant, role, character, submission, evidence, contribution, and display snapshot.
- Confirm and audit re-enable without requiring a reason; re-enable restores authentication only and never bypasses event lifecycle/membership authority.

#### Public feedback/reporting — deferred

- Do not add an in-application evidence-report, bug-report, or feedback form in version one.
- Use the community Discord feedback channel. Valid evidence concerns use administrator reversal and corrected/redacted resubmission.

#### Multiple OSRS accounts and active-account swaps — core rules selected

- Allow one event participant to register more than two OSRS character names.
- Allow the same OSRS character to be linked to several Discord identities globally; the link grants no website authority and does not assert exclusive ownership.
- Assign an OSRS character to at most one participant within an event through a transactional uniqueness constraint. A later event may assign it to a different participant.
- Mark each named Account answer internally as playing or informational according to its question, while the UI calls them Regular account and Alt account. Alt accounts cannot be activated, credited with drops, or synchronized to Wise Old Man.
- Allow exactly one drop-eligible character per participant at a time.
- Record every swap as an append-only history entry containing the participant, previous account, new account, effective UTC time, recorded time, and actor.
- Freeze the registered account set and playing/informational roles when signup closes. During the event, allow unlimited swaps only among the participant's registered playing accounts.
- Permit normal swaps only while the event is live; activate the primary at event start and freeze normal swaps at event end.
- Participants make their own account swaps; captains/co-captains may swap for unlinked members of their own external team. Admin corrections or backdated swaps require an audit reason.
- Evidence stores immutable website submission time but does not duplicate the in-game screenshot timestamp in a typed field.
- Display the official event end and account-swap history in UTC during review to match the plugin.
- Require the reviewer to confirm that the credited character was active at the screenshot time.
- Do not use OCR; the reviewer reads the timestamp directly from the screenshot.
- Retain full swap history for validation and audit, but normally show only the latest relevant transition.
- Use the plugin's whole-minute `DD/MM/YYYY HH:mm UTC` evidence format and display it beside immutable submission time. Do not encode a maximum upload-duration rule; admins apply changing community policies manually.
- Make a normal swap evidence-effective at the next whole UTC minute, keep the old account active until then, and block a second request during the short pending transition.

#### Team-only board focus

- Let captains/co-captains mark individual tiles, complete rows, and complete columns as current team priorities.
- Focus markers are visible by default only to current members of that team, never to opponents, the public, ordinary cross-team admins, or a participating Super Admin viewing another team.
- Render authorized team focus on the existing team-board view without changing the finished public/opponent projection; a view-only focus toggle/summary is the fallback if the private layer is crowded.
- Let the Super Admin explicitly enable a visibly identified, team/page-session-scoped **Inspect team focus** mode. Only then load that team's focus; do not retain a persistent show-all preference.
- Super Admin inspection is read-only unless that account is also a captain/co-captain of the target team.
- Use team-scoped realtime invalidation and optimistic concurrency so markers cannot leak or silently overwrite newer changes.
- Start and remain with a simple focused/not-focused state: no colors, notes, timers, weights, progress effects, or public history.
- Freeze focus at event end.

#### Super Admin and global role management — core rules selected

- Maintain exactly one active Super Admin on a normal website account.
- Assign the initial owner through controlled setup/migration, never first public signup or onboarding.
- Let only the Super Admin grant/revoke Admin for normal website accounts; emergency captain accounts are ineligible.
- Require strong confirmation and automatic before/after history for role changes, with no typed reason.
- Invalidate existing authenticated sessions immediately when Admin is revoked.
- Transfer ownership atomically to another active normal website account: destination becomes Super Admin, source becomes Admin, and zero/two-owner states cannot commit.
- Keep lost-owner recovery operator-controlled and outside public account flows.

#### Captain submission lifecycle

- Ordinary participants submit only for themselves; captains/co-captains choose a current teammate, and that participant's current active playing account is derived.
- Captains/co-captains cannot delegate team-wide submission authority to ordinary participants.
- Keep submissions and pending edits available after event end through the submission cutoff, including for unlinked external teammates.
- Close those mutations at cutoff for all teams; an admin reopening restores the window, while an emergency credential still needs explicit re-enablement.

#### Public board preview and tile artwork

- Add a **Preview board** action to the private board editor that renders the actual public-board visual treatment before publication.
- The preview must omit administrator-only information such as EHB estimates and editing controls, and use the same responsive tile layout, artwork, text treatment, and completion states as the public website.
- Allow a board designer to override a tile's automatic artwork with a custom tile image uploaded through the shared decorative-asset workflow. Replace the current URL-backed editor field; arbitrary image URLs are not supported outside the OSRS catalogue.
- When no custom tile image is supplied, derive artwork from the selected boss/activity images.
- Support a deterministic composition of up to four selected boss/activity images. Define ordering, cropping, missing-image fallbacks, accessibility text, mobile behavior, and snapshot semantics before implementation.
- Custom artwork always takes precedence over automatic boss composition.
- Draft preview uses current catalogue-derived values and therefore changes when the catalogue changes. Approved preview and published rendering use the active immutable approval snapshot.

#### Wise Old Man integration

- Add an explicit per-playing-account **Fetch from Wise Old Man** action in My accounts and signup/edit that uses the selected character name and populates EHB while preserving manual fallback. It is not available for Alt accounts, Admin correction/internal creation, CSV/external rosters, or automatic page activity.
- A failed, unavailable, or rate-limited fetch shows accurate feedback without clearing an existing value; WoM availability never blocks signup opening or manual EHB entry.
- Review the current official API documentation and rate/usage rules immediately before implementation; do not rely on planning-time assumptions.
- Cache and throttle signup lookups server-side, honor `Retry-After`, and never fetch on every keystroke, render, or ordinary public request.
- Store the submitted EHB snapshot, source, and fetch time rather than treating later WoM changes as retroactive signup data.
- Link an event to its Wise Old Man competition.
- Synchronize player gained EHB and display player and team EHB leaderboards.
- Store the source competition, last successful synchronization time, and synchronization errors.
- Use one background synchronization shared by every viewer; public and team pages must only read the locally cached result.
- During a live event, synchronize at most once every two hours by default. Keep the interval configurable, but never let ordinary page traffic trigger Wise Old Man requests.
- A manual refresh must obey the same global cooldown. If data was refreshed recently, show the cached result and its timestamp instead of calling Wise Old Man again.
- Respect `Retry-After` and apply increasing backoff after rate limits or failures. Do not immediately retry a `429 Too Many Requests` response.
- Load tests must use a fake or recorded Wise Old Man response. Test the real integration separately with a single synchronization plus explicit rate-limit and stale-cache scenarios.
- Wise Old Man standings combine gained EHB from every `PLAYING` character that participant registered for the event, regardless of which playing character was active for bingo-drop eligibility at that moment.
- Rank the participant by the combined total, not each character separately. Show the per-character breakdown beneath the participant total so multiple-account players are not misleadingly split across leaderboard positions.
- Never send informational Account answers or Yes/No support-alt answers to Wise Old Man and do not display them in activity standings.
- Team EHB is the sum of those participant-level totals, with each registered character counted once through its owning participant.
- Team average divides participant totals by represented current participants, not regular-account count; tied top participants share MVP.
- Use one competition-details request per due Live event, not separate requests per player/team. When expected regular accounts are absent, persist matched fresh values only, mark the cache partial/provisional, show deterministic available rankings with privacy-safe coverage when at least one matches, show no rankings when zero match, and show exact missing-account diagnostics only to Admins.
- Send every manual and automatic request through one shared limiter. Preserve the last three observed requests as reserve, block manual calls locally with an accurate roughly-one-minute retry message, and expose observed limit/remaining/reset plus sync status to Admins.
- A locally blocked automatic sync remains pending. Each cycle has one initial attempt plus at most three retries, honoring `Retry-After` or scheduling approximately 1-, 2-, and 4-minute delays; failure four exhausts the cycle without shifting its separately anchored next ordinary cycle. Never hold a worker asleep or create a tight retry loop.

#### Manual signup opening deadline

- When an admin manually opens signups, set the opening time to now, rounded consistently with the existing scheduler.
- Preserve a valid explicit future closing time no later than event start.
- When no valid closing remains, propose future draft time when it is no later than event start, otherwise event start, and show the supplied value before confirmation.
- Do not persist a proposal until readiness, acknowledgement, confirmation, and overlap checks succeed.
- Admins may then edit the future closing time or close signups manually.
- Starting the draft continues to close and lock signups automatically.
- Reject manual opening when the event has already started or no valid future signup window remains.

#### Waiting-list promotion regression

- The existing workflow promotes the earliest waiting-listed participant when a confirmed participant is withdrawn before draft lock.
- Keep this behavior covered by a direct integration regression test.
- Automatic promotion remains disabled after draft lock.

## 19. Suggested implementation order inside each milestone

For each workflow:

1. Confirm acceptance example.
2. Write or refine domain test.
3. Implement domain rule.
4. Implement application command/query.
5. Add database mapping and constraint.
6. Add authorization policy.
7. Add UI.
8. Add integration/browser coverage.
9. Add audit behavior.
10. Manually verify against wireframe and acceptance criteria.

This sequence keeps business rules independent from the page implementation.

## 20. Definition of done for a feature

A feature is done only when:

- Acceptance behavior is implemented.
- Server-side validation exists.
- Authorization is enforced.
- Appropriate audit entries exist.
- Domain tests pass.
- Required integration/browser tests pass.
- Empty, error, and completed UI states exist.
- Mobile behavior is usable.
- Accessibility basics are present.
- Logs contain enough context without secrets.
- Documentation is updated.
- No known critical defect remains.

## 21. Ready-to-code checklist

Planning is sufficiently complete to begin implementation when the following are true:

- [x] Purpose and version-one scope defined
- [x] Roles and permissions defined
- [x] Signup and waiting-list rules defined
- [x] Draft rules defined
- [x] Board and tile rules defined
- [x] Evidence lifecycle defined
- [x] Ranking and EHB behavior defined
- [x] Finalization behavior defined
- [x] Major workflows wireframed
- [x] Logical data model defined
- [x] Technical architecture selected
- [x] Hosting and hibernation approach selected
- [x] macOS development compatibility confirmed
- [ ] Product requirements marked approved after final review
- [ ] Data model marked approved after final review
- [x] Roadmap reviewed and approved
- [ ] Initial production deadline or target event chosen
- [ ] External accounts and credentials available when their milestone begins

The last two unchecked external items do not prevent local Milestone 1 development, but production deployment cannot complete without them.

## 22. Recommended first coding boundary

When coding is authorized, begin only with Milestones 1 and 2:

- Solution and projects
- Local PostgreSQL
- Base Razor UI
- CI and tests
- Admin/captain authentication
- Authorization policies
- Audit infrastructure

Do not begin board calculations or production hosting in the first coding change. Establishing the foundation and access model first reduces rework across every later workflow.
