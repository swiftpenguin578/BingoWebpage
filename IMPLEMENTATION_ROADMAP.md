# OSRS Community Bingo Platform

## Implementation Roadmap

**Status:** Approved roadmap v1.0  
**Last updated:** 2026-07-11  
**Companion documents:** `PRODUCT_REQUIREMENTS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`

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
| 8. Event operations and finalization | Admin closes, reviews, finalizes, and archives events | 7 |
| 9. Production operations | VPS, R2, deployment, backups, monitoring, and restore work | 1–8 |
| 10. Full rehearsal and release | A representative test event passes end to end | 9 |

Milestones 4 and 5 can partly proceed in parallel after the event/signup foundation is stable, but their integration must be verified before evidence submission begins.

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

Establish secure permanent admin access and event-scoped captain access before protected workflows are built.

### Deliverables

- Admin and captain account records.
- Secure password hashing and authentication cookies.
- Login, logout, password-change, and admin reset workflows.
- Admin authorization policy.
- Event/team-scoped captain authorization policy.
- Captain activation, correction-only, expiry, disable, and re-enable behavior.
- Admin account management.
- Audit-entry infrastructure.
- Audit viewer with basic filtering.
- Login rate limiting and failed-login logging.
- Cross-site request forgery protection.
- Development-only first-admin bootstrap process.

### Authorization tests

- Anonymous visitor cannot access captain or admin routes.
- Captain cannot access admin routes.
- Captain cannot access another team's protected data.
- Expired captain account cannot mutate event data.
- Correction-only captain can correct eligible submissions but cannot submit a newly obtained drop.
- Disabled account cannot log in.
- Admin actions create the expected audit actor and timestamp.

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

- Guided event-creation workflow.
- Event details and timezone.
- Signup opening and closing times.
- Event start, end, and submission cutoff.
- Participant cap that can increase but not decrease.
- Preliminary team and board estimates.
- Signup-code configuration.
- Separate publication flags.
- Event state transitions and audit history.
- Create the event and open signups without requiring a completed board.
- Continue private board preparation independently throughout signup and pre-draft setup.

#### Signup form

- Required account-name and EHB questions.
- Optional second account, comments, captain volunteer, and Discord identity.
- Custom short text, long text, yes/no, and single-choice questions.
- Form validation and event signup code.
- Private signup-edit token.
- Signup confirmation page.
- Signup close/reopen behavior.

#### Capacity and waiting list

- Confirm automatically below cap.
- Add later valid signups to waiting list.
- Derive private waiting-list position.
- Increase capacity and promote in signup order.
- Promote after confirmed withdrawal/removal before draft lock.
- Stop automatic promotion after draft lock.
- Admin withdraw/remove with reason.
- Admin-only payment status and comments.

#### CSV fallback

- Download fixed template.
- Preview import.
- Validate required columns.
- Detect likely duplicates.
- Warn about unknown columns.
- Import valid rows with source and audit record.

### Domain tests

- Exact-cap boundary.
- Simultaneous signup near final place.
- Waiting-list ordering with equal timestamps and deterministic sequence.
- Increasing cap by fewer/more places than waiting participants.
- Confirmed withdrawal promotion.
- No automatic promotion after draft lock.
- Cap cannot decrease.
- Signup edit does not change original queue order.
- Duplicate name detection is event-specific only.

### Browser acceptance

- Admin creates an event and opens signups.
- Public sees signup but not participant list, draft, teams, or board.
- Participant signs up and uses private edit link.
- Admin increases cap from 50 to 60 and seven waiting participants are promoted.

### Completion criteria

- The complete signup period can be run without Google Forms.
- CSV remains a tested fallback, not the primary workflow.

## 8. Milestone 4 — OSRS catalogue and board builder

### Objective

Allow admins to maintain source-specific OSRS data and build every known tile without custom code.

### Deliverables

#### Catalogue

- Boss/activity CRUD.
- Item CRUD.
- Source-specific drop CRUD.
- Displayed and numeric drop rates.
- Efficient completion rates and EHB values.
- Images and active/inactive behavior.
- One-time initial catalogue import tool.
- Manual editing after import.
- Data source and update timestamp.

#### Tile requirements

- One or more bosses/activities per requirement.
- Eligible-drop selection.
- Target contribution.
- Duplicates allowed.
- Allow higher weightings.
- Multiple requirements per tile.
- Manual objective with target quantity and evidence instructions.
- EHB calculation preview and admin override.

#### Board builder

- Configurable rows and columns.
- Add/remove tiles.
- Drag one tile onto another to swap.
- Keyboard/touch select-and-swap fallback.
- Top-left compaction during shrinking.
- Block shrink if placed tiles exceed new capacity.
- Tile editor side drawer.
- Row, column, and total EHB.
- Row/column highlight on hover and focus.
- Balance spread warning.
- Structural validation.
- Publish immutable event snapshots.
- Save and resume an incomplete private board across multiple admin sessions.
- Permit continued private editing while event signups are open or closed, until board publication.

### Representative acceptance tiles

- Five Zulrah unique-table drops with duplicates allowed.
- Full Voidwaker with duplicates disallowed.
- Theatre of Blood purples allowing manually entered higher weighting.
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
- Published snapshot unaffected by later catalogue changes.
- Concurrent catalogue edit does not corrupt a board draft.

### Completion criteria

- Every tile on the supplied example board can be represented.
- Published boards are historically stable.

## 9. Milestone 5 — Teams and snake draft

### Objective

Allow admins to combine website-drafted teams with manually managed pre-formed teams and complete a private admin-operated snake draft without altering external rosters.

### Deliverables

- Final team count and target size setup.
- Participant remainder/size preview.
- Team name and image management.
- Add pre-formed internal or external teams before or after the draft.
- Optional team affiliation/clan label.
- Manually add, remove, and move members on pre-formed rosters with an audit reason.
- Exclude pre-formed teams and their assigned players from draft order and the available draft pool.
- Captain and co-captain assignment.
- Separate captain accounts.
- Random initial team-order scramble.
- Snake-draft turn calculation.
- Complete participant pool sorted by EHB.
- Available/drafted status without removing drafted players.
- Alternative sorting by name, signup time, and status.
- Record pick.
- Undo latest pick.
- Pause and resume.
- Draft lock.
- Finalize and automatically publish completed rosters/results.
- Audit history.

### Domain tests

- Snake order for two, three, and six teams over several rounds.
- Pick ownership at round boundaries.
- Participant cannot be picked twice.
- Undo restores participant and turn correctly.
- Team configuration cannot change silently after first pick.
- Waiting-list promotion stops when draft locks.
- Pre-formed teams receive no snake-draft turns.
- Pre-formed roster members cannot be selected in the website draft.
- Adding a pre-formed team after finalization preserves draft order and pick history.

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

#### Captain workflow

- Board-first tile selection.
- Fixed event/team derived from account.
- Credited participant selection from own roster.
- Eligible boss/drop selection.
- Credited weight default `1`.
- Higher weight editable only when tile permits it.
- Obtained time default current and editable.
- One image upload.
- Optional note.
- Pending history.
- Edit/withdraw pending submission.
- Changes-requested correction and resubmission.
- Approved/rejected history and reviewer feedback.

#### Evidence storage

- R2 adapter.
- Streamed uploads.
- Size and media-type limits.
- Image decoding validation.
- Checksums and generated keys.
- Original/replacement evidence history.
- Safe evidence display.

#### Admin review

- Event/team/status review queue.
- Evidence preview and full-size view.
- Tile/player/drop/weight/time metadata.
- Prior approved evidence for context.
- Edit metadata with audit before/after.
- Approve.
- Reject with required note.
- Request changes with required note.
- Mark duplicate.
- Hide public image and player together.
- Reverse approval with reason.

### Transaction tests

- Two admins cannot approve one submission twice.
- Captain request tampering cannot change team.
- Credited player must belong to team.
- One drop contributes to one tile.
- Weight above one rejected when tile disallows it.
- Contribution capped at remaining requirement target.
- Reversal removes exactly its approved contribution.
- Replacement evidence preserves the original.

### Completion criteria

- Approval and reversal produce correct auditable contribution records.
- Evidence remains private or public according to status and privacy flags.

## 11. Milestone 7 — Public boards, progress, and rankings

### Objective

Deliver the participant-facing centerpiece and automatic competitive calculations.

### Deliverables

#### Public board

- Mission Control-style overview of all teams.
- Compact board previews.
- Team name, rank, tiles, lines, and finisher status.
- Expand/zoom into one board.
- Previous/next team navigation.
- Reduced-motion support.
- Mobile list/grid behavior.
- Tile detail view.
- Approved evidence list.
- Hidden evidence/player placeholder.

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
- Full-board completion uses obtained time, not review time.
- Finishers outrank non-finishers.
- Non-finishers rank by lines, then tiles, then EHB tie-break.
- Reversal can remove a line, full board, and provisional win.
- Hidden player is excluded from public contribution identity as required.

### Browser acceptance

- Visitor compares six teams and opens a board.
- Visitor opens a tile and sees approved evidence.
- Approval updates board without full manual spreadsheet refresh.
- Mobile board remains readable.

### Completion criteria

- Public state is reproducible from approved evidence.
- No private pending evidence is exposed.

## 12. Milestone 8 — Event operations and finalization

### Objective

Allow admins to safely run the event lifecycle and create official historical results.

### Deliverables

- Operations dashboard.
- Start event and publish board controls.
- Scheduled event end and submission grace period.
- Manual submission reopening with cutoff and reason.
- Distinction between obtained window and submission window.
- Final-review checklist.
- Click-through to filtered underlying records.
- Automatic blocker resolution.
- Manual “mark resolved anyway” with confirmation and reason.
- Completion-time comparison and correction.
- Provisional placements.
- Admin finalization confirmation.
- Official placement snapshots.
- Captain account expiry scheduling.
- Unfinalization with reason.
- Archive and historical event visibility.

### Tests

- Drop after event end remains invalid during grace period.
- Evidence obtained in time can be submitted during grace period.
- Reopening upload window does not extend obtained window.
- Finalization blocked while unresolved conditions remain.
- Manual override clears only blocker status and does not mutate submissions.
- Finalization snapshot matches recalculated rankings.
- Unfinalization preserves previous official snapshot.
- Captain expiry calculated 24 hours after finalization.

### Completion criteria

- Admin can move an event from signup through archived history without direct database edits.

## 13. Milestone 9 — Production operations

### Objective

Make deployment, backup, monitoring, and recovery reliable enough for a live community event.

### Deliverables

#### Infrastructure

- Hetzner VPS.
- Linux hardening and firewall.
- Docker and Docker Compose.
- Caddy and HTTPS.
- PostgreSQL private network exposure only.
- R2 production bucket and restricted credentials.
- DNS and production domain.

#### Deployment

- GitHub Actions build/test/image workflow.
- Production image in registry.
- Server deployment command/script.
- Controlled migration step.
- Health check and rollback procedure.
- Configuration and secret inventory.

#### Backup

- Nightly PostgreSQL logical backup.
- More frequent active-event backup schedule.
- Pre-migration backup.
- Encryption and upload off-server.
- Retention cleanup.
- Backup failure alert.
- Documented test restore.
- Final event backup.

#### Monitoring

- External uptime check.
- Application and container logs.
- Disk, memory, and database usage observation.
- R2/storage failure visibility.
- Background-service health.
- Cost and provider billing alerts.

#### VPS lifecycle

- Bootstrap procedure.
- Maintenance mode.
- Hibernation checklist.
- Delete-server checklist.
- Restore-from-backup checklist.
- DNS restoration procedure.

### Operational acceptance

- Fresh VPS can be provisioned from documentation.
- Application deploys without manually editing source.
- Backup restores into a clean PostgreSQL instance.
- Restored app reconnects every tested evidence object.
- A failed deployment can be rolled back safely.
- Deleting a rehearsal VPS does not destroy durable event data.

### Completion criteria

- Another future session can recreate production using documentation, source, secrets, database backup, and R2 data.

## 14. Milestone 10 — Full rehearsal and release

### Objective

Prove the complete system before the first real bingo.

### Rehearsal data

- Fictional event.
- At least six teams.
- Representative captain/co-captain accounts.
- 5x5 board containing every important objective pattern.
- Enough fictional evidence to complete tiles, lines, and a board.
- Waiting list, withdrawal, and promotion examples.
- Hidden evidence example.
- Incorrect approval and reversal example.

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
11. Activate captain accounts.
12. Submit normal, duplicate, weighted, and manual evidence.
13. Request changes and replace evidence.
14. Approve, reject, hide, and reverse evidence.
15. Verify live boards and rankings.
16. Enter grace period.
17. Reopen submissions without extending obtained window.
18. Resolve and override finalization blockers.
19. Correct a completion time.
20. Finalize results.
21. Verify captain expiry.
22. Archive event.
23. Create backup.
24. Restore to a clean environment.
25. Verify historical results and evidence.

### Release gate

The first live event cannot begin until:

- No critical or high-severity defect remains.
- Domain and authorization test suites pass.
- Browser acceptance tests pass on current desktop and mobile browsers.
- Backup and clean restore succeed.
- Admins have rehearsed review, reversal, and finalization.
- Captains have trialed mobile screenshot submission.
- Evidence upload limits are tested with realistic screenshots.
- Public privacy behavior is verified.
- Monitoring and contact responsibilities are assigned.
- A manual emergency fallback procedure exists.

## 15. Cross-cutting test matrix

### 15.1 Domain unit tests

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

### 15.2 Integration tests

Use real PostgreSQL in containers for:

- Constraints
- Transactions
- Concurrency
- Migrations
- Snapshot immutability
- Authorization queries
- Audit persistence

### 15.3 Browser tests

Cover only high-value workflows rather than every visual detail:

- Signup and editing
- Waiting-list administration
- Draft
- Captain submission
- Admin review
- Public board
- Board builder
- Finalization

### 15.4 Manual exploratory tests

- Mobile photo upload
- Slow or interrupted connection
- Large screenshot behavior
- Two admins reviewing simultaneously
- Event crossing midnight/timezone boundaries
- Reduced motion
- Keyboard board editing
- Screen-reader labels on critical controls

### 15.5 Security tests

- Route authorization
- Team identifier tampering
- Private signup-token guessing/rate limiting
- CSRF
- Unsafe upload formats
- Oversized upload
- Stored script content in names/comments
- Hidden evidence access
- Expired account access
- Secret leakage in logs

## 16. Data and privacy tasks

Before production:

- Decide retention period for rejected/withdrawn evidence.
- Decide whether original evidence is retained indefinitely with archived events.
- Publish a short privacy notice for signup data and screenshots.
- State that player names, teams, approved drops, and approved evidence may become public.
- State that comments, payment status, and waiting-list identities remain private.
- Define who can request removal or correction after an event.
- Define admin responsibilities for sensitive chat visible in screenshots.

These decisions do not require a complex legal system but must be written clearly for community members.

Detailed decisions may be deferred until the signup and evidence milestones, when the real controls and storage behavior are being implemented. They must be finalized before production signup opens and before real evidence is uploaded.

## 17. Operational risks and mitigations

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

## 18. Suggested implementation order inside each milestone

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

## 19. Definition of done for a feature

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

## 20. Ready-to-code checklist

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

## 21. Recommended first coding boundary

When coding is authorized, begin only with Milestones 1 and 2:

- Solution and projects
- Local PostgreSQL
- Base Razor UI
- CI and tests
- Admin/captain authentication
- Authorization policies
- Audit infrastructure

Do not begin board calculations or production hosting in the first coding change. Establishing the foundation and access model first reduces rework across every later workflow.
