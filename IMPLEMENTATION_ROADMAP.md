# OSRS Community Bingo Platform

## Implementation Roadmap

**Status:** Approved roadmap v1.0  
**Last updated:** 2026-07-11  
**Companion documents:** `PRODUCT_REQUIREMENTS.md`, `DATA_MODEL.md`, `TECHNICAL_ARCHITECTURE.md`, `UI_OVERHAUL_ROADMAP.md`

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
| 9. UI overhaul and regression | Every workflow is clear, consistent, responsive, and retested locally | 1–8 |
| 10. Local rehearsal and production preparation | A release candidate passes end to end and deployment/restore tooling is ready | 9 |
| 11. Production deployment and validation | The approved release candidate is deployed last and passes production checks | 10 |

Milestones 4 and 5 can partly proceed in parallel after the event/signup foundation is stable, but their integration must be verified before evidence submission begins.

Milestone 7A is a follow-up hardening milestone for already completed workflows. It must be completed before Milestone 8 and before a real multi-admin draft or board-editing session.

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
- Optional fixed contribution weight, configured by the board designer and defaulting to `1`.
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
- Automatically generated separate captain/co-captain accounts with name-based randomized usernames, one-time temporary passwords, and participant linkage.
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
- Credited weight comes from the selected board-requirement snapshot and defaults to `1`.
- Submitters cannot override the selected drop's board-defined contribution weight.
- Immutable server-generated submission time; no captain-entered obtained time.
- One image upload.
- Optional note.
- Optional captain request to hide the approved screenshot and credited player publicly.
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
- Separate enabled/disabled evidence-code mode with custom or generated codes, immediate/scheduled activation, and immutable per-submission code snapshots.

#### Admin review

- Current active-event review queue with team/tile/status filters and newest submissions first.
- Evidence preview and full-size view.
- Tile/player/drop/weight metadata plus immutable submission time.
- Prior approved evidence for context.
- Edit metadata with audit before/after.
- Approve.
- Reject with required note.
- Request changes with required note.
- Mark duplicate.
- Hide public image and player together.
- Restore public image and player when an automatic captain privacy request is unnecessary.
- Reverse approval with reason.

### Transaction tests

- Two admins cannot approve one submission twice.
- Captain request tampering cannot change team.
- Credited player must belong to team.
- One drop contributes to one tile.
- Posted weight tampering cannot override the selected drop's board-defined weight.
- Contribution capped at remaining requirement target.
- Reversal removes its contribution and reallocates freed capacity to later eligible approved evidence.
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
- Full-board completion uses immutable submission time, not review time.
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
- The real-clock scheduled-end transition is manually verified during Milestone 10's full rehearsal; Milestone 8 verifies its state rule automatically.

### Completion criteria

- Admin can move an event from signup through archived history without direct database edits.

## 13. Milestone 9 — UI overhaul and regression

### Objective

Turn the functionally complete application into a clear and consistent experience before any production deployment work begins.

### Deliverables

#### Workflow and navigation

- Review every admin, captain, and public workflow from start to finish.
- Make the current event and team context visible on every relevant page.
- Separate accounts, events, and workflow records so entries from different events are not visually bundled together.
- Improve navigation, breadcrumbs, return paths, empty states, confirmation messages, and error messages.
- Replace developer-oriented wording with community-friendly labels and explanations.

#### Visual and interaction consistency

- Establish consistent page widths, spacing, forms, tables, buttons, dialogs, and status indicators.
- Replace oversized controls and panels with the compact density and alignment system defined in `UI_OVERHAUL_ROADMAP.md`.
- Progressively enhance frequent inline actions so only the affected component updates, while retaining standard Razor form fallbacks.
- Introduce semantic success, error, warning, and information notifications instead of styling every response as success.
- Add a top-right role-based action inbox: pending review for admins and changes-requested submissions for captains.
- Revisit dense event creation, board editing, draft, review, finalization, and account pages.
- Reuse the combined date-time picker across date/time workflows.
- Improve desktop and mobile layouts without changing approved business rules.
- Complete keyboard, focus, colour-contrast, and permission/error-page checks.

#### Regression

- Repeat every milestone manual test against the overhauled interface.
- Add browser automation for stable critical paths discovered during manual testing.
- Recheck multi-admin board and draft control.
- Recheck mobile captain evidence upload and public Mission Control views.
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
- Health checks and background-service health reporting.
- PostgreSQL backup, encryption, retention, and restore scripts.
- Pre-migration and final-event backup procedures.
- Monitoring and alert configuration prepared but not yet pointed at production.
- Hetzner bootstrap, firewall, Caddy, DNS, maintenance, hibernation, deletion, and restoration runbooks.

### Rehearsal data

- Fictional event.
- At least six teams.
- Representative captain/co-captain accounts.
- 5x5 board containing every important objective pattern.
- Enough fictional evidence to complete tiles, lines, and a board.
- Waiting list, withdrawal, and promotion examples.
- Hidden evidence example.
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
11. Activate captain accounts.
12. Submit normal, duplicate, weighted, and manual evidence.
13. Request changes and replace evidence.
14. Approve, reject, hide, and reverse evidence.
15. Verify live boards and rankings.
16. Leave the application running across the scheduled event-end time and confirm it automatically enters grace/final review within one lifecycle polling interval.
17. Reopen submissions without extending obtained window.
18. Resolve and override finalization blockers.
19. Correct a completion time.
20. Finalize results.
21. Verify captain expiry.
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
- Public privacy behavior is verified.
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
- Hidden evidence access
- Expired account access
- Secret leakage in logs

## 17. Data and privacy tasks

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

### Post-version-one feature backlog

These features are intentionally separate from the approved version-one scope. They affect identity, evidence validation, and live-event operations enough to require their own design and regression pass.

#### Discord sign-in and submission permissions

- Add Discord sign-in for participants after the version-one workflows are stable.
- Treat Discord as the website login identity. OSRS character names remain event signup data because names and eligible accounts can change between events.
- Discord-server membership is not required. This allows participants from external clans to sign in without joining the community server.
- Access is granted by linking the authenticated Discord identity to an event signup, not by checking Discord guild membership.
- An authenticated participant may submit evidence only for themselves and only for their current event team.
- Captains and co-captains may submit evidence for any participant on their own team.
- Admins retain event-wide submission and correction access.
- Keep temporary captain accounts available until the Discord migration is complete and as an emergency fallback.

#### Multiple OSRS accounts and active-account swaps

- Allow one event signup to contain more than two OSRS character names.
- Exactly one character is eligible to receive bingo drops for that participant at a time.
- Record every swap as an append-only history entry containing the participant, previous account, new account, effective UTC time, recorded time, and actor.
- Participants make their own account swaps and the new active account takes effect immediately. Admin corrections or backdated swaps require an audit reason.
- Evidence records the drop-received time shown by the in-game UTC overlay separately from the immutable website submission time.
- Store and compare all times in UTC. Display both UTC and the event timezone during review so admins do not need to convert times manually.
- Validate that the receiving character was active when the drop was received and that the evidence was submitted no more than 60 minutes later.
- Do not use OCR. The submitter enters the displayed UTC time and the reviewer compares it with the screenshot.
- Define the exact boundary rule for a drop received at the same minute as a swap and the admin-override behavior for missing or incorrect timestamps before implementation.

#### Team-only board focus

- Let captains and co-captains mark individual tiles and complete rows as current team priorities.
- Focus markers are visible only to members of that team and admins, never to opponents or the public.
- Start with a simple focused/not-focused state. Optional colors, notes, columns, and expiry times can be considered after the basic workflow is tested.

#### Public board preview and tile artwork

- Add a **Preview board** action to the private board editor that renders the actual public-board visual treatment before publication.
- The preview must omit administrator-only information such as EHB estimates and editing controls, and use the same responsive tile layout, artwork, text treatment, and completion states as the public website.
- Allow a board designer to override a tile's automatic artwork with a custom tile image. The current editor stores an optional managed image URL; replace or supplement this with the shared decorative-asset upload workflow when that storage path is implemented.
- When no custom tile image is supplied, derive artwork from the selected boss/activity images.
- Support a deterministic composition of up to four selected boss/activity images. Define ordering, cropping, missing-image fallbacks, accessibility text, mobile behavior, and snapshot semantics before implementation.
- Custom artwork always takes precedence over automatic boss composition.
- Preview and published rendering must use immutable event-board snapshots rather than changing when the global catalogue is edited later.

#### Wise Old Man integration

- Link an event to its Wise Old Man competition.
- Synchronize player gained EHB and display player and team EHB leaderboards.
- Store the source competition, last successful synchronization time, and synchronization errors.
- Use one background synchronization shared by every viewer; public and team pages must only read the locally cached result.
- During a live event, synchronize at most once every two hours by default. Keep the interval configurable, but never let ordinary page traffic trigger Wise Old Man requests.
- A manual refresh must obey the same global cooldown. If data was refreshed recently, show the cached result and its timestamp instead of calling Wise Old Man again.
- Respect `Retry-After` and apply increasing backoff after rate limits or failures. Do not immediately retry a `429 Too Many Requests` response.
- Load tests must use a fake or recorded Wise Old Man response. Test the real integration separately with a single synchronization plus explicit rate-limit and stale-cache scenarios.
- Wise Old Man standings combine gained EHB from every character that participant registered for the event, regardless of which character was active for bingo-drop eligibility at that moment.
- Rank the participant by the combined total, not each character separately. Show the per-character breakdown beneath the participant total so multiple-account players are not misleadingly split across leaderboard positions.
- Team EHB is the sum of those participant-level totals, with each registered character counted once through its owning participant.

#### Manual signup opening deadline

- When an admin manually opens signups, set the opening time to now, rounded consistently with the existing scheduler.
- Set the automatic closing time to the earlier of three months after opening and the event start time.
- Admins may then edit the future closing time or close signups manually.
- Starting the draft continues to close and lock signups automatically.
- Reject manual opening when the event has already started or no valid future signup window remains.

#### Waiting-list promotion regression

- The existing workflow promotes the earliest waiting-listed participant when a confirmed participant is removed or withdrawn before draft lock.
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
