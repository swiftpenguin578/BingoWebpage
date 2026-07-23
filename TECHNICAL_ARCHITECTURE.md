# OSRS Community Bingo Platform

## Technical Architecture

**Status:** Approved architecture v1.0  
**Last updated:** 2026-07-11  
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

- Waiting-list promotion
- Draft pick and undo
- Submission approval
- Approval reversal
- Board publication snapshot
- Event finalization
- Event unfinalization

Submission review uses a concurrency token or row lock so two admins cannot apply the same evidence twice.

Board and draft administration use two complementary concurrency mechanisms:

- Each board aggregate carries an optimistic concurrency version covering its tiles, requirements, layout, and publication state. Every mutating request includes the loaded version; stale writes return a recoverable conflict and do not overwrite newer content.
- A separate renewable board-editor lease provides the normal user-facing edit lock. Opening a board does not acquire it; explicit edit, release, and confirmed takeover actions change ownership while viewers receive live updates. Navigating away sends a keepalive release request, while client activity renews the lease at a throttled interval. The five-minute inactivity expiry remains the fallback for interrupted or abandoned browsers.
- A draft session carries an active-controller lease tied to an administrator account and lease/version value. Server-side authorization checks that lease for start, pick, undo, pause/resume, and finalization.
- Draft observers receive live state but remain read-only. A confirmed takeover atomically replaces the controller lease and writes an audit entry.
- Draft-pick transactions retain serializable isolation and uniqueness constraints as the final integrity boundary even when controller requests race or are retried.

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

## 8. Authentication and authorization

### 8.1 Accounts

ASP.NET Core authentication uses secure cookies and password hashing.

Account types:

- Permanent admin account
- Event-scoped captain account
- Event-scoped co-captain account

Public participants do not have accounts.

### 8.2 Authorization

Authorization policies enforce:

- Admin-only event and catalogue management
- Captain access to one event and one team
- Correction-only behavior after submission closure
- Account activation and expiry
- Evidence visibility rules

Authorization is checked on every server mutation. Hiding an interface control is not a security boundary.

### 8.3 Public signup editing

Private signup-edit links use high-entropy random tokens. Only token hashes are stored in PostgreSQL.

Tokens can be regenerated or revoked by an admin.

## 9. Scheduled behavior

A single in-process ASP.NET Core background service performs short idempotent checks at a regular interval.

It may:

- Open or close scheduled signup availability
- Close normal submission availability
- Expire captain accounts 24 hours after the event ends
- Promote waiting-list participants after capacity changes when not completed synchronously
- Perform low-priority cache reconciliation

Event timestamps remain authoritative. If the application was offline when a scheduled time passed, the next request and background check derive the correct effective state.

Critical competitive actions such as finalization remain explicit admin actions.

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
- Captain submission and correction
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
