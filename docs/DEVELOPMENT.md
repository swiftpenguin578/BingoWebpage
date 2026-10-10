# Development

How to set up, run, seed, test and operate DK Legacy Bingo locally, and how CI checks it. The project introduction is [README.md](../README.md); production is in [OPERATIONS.md](OPERATIONS.md).

## Prerequisites

- .NET SDK pinned by `global.json` (10.0.x, `latestPatch`). Install the SDK, not only the runtime; verify with `dotnet --info`.
- Docker with Docker Compose (Docker Desktop on a Mac). PostgreSQL runs in a container; no native install. Verify with `docker compose version`.
- Git with your name and email configured.
- Node 22 and pnpm 11.25.0 (the `packageManager` in `package.json`) for the JavaScript and Playwright checks.
- On a Mac use the Arm64 installers. Keep Safari (WebKit) and a Chromium browser for manual checks. Any editor works.
- Six Labors ImageSharp 4.x checks a license at build time: local builds read the untracked repository-root `sixlabors.lic`; CI and Docker pass the `SIXLABORS_LICENSE_KEY` secret (see `Directory.Build.props`). Never commit the key or the file.
- Keep commands and scripts cross-platform where practical; deployment automation runs on Linux. The Mac default shell is zsh; production paths are case-sensitive; containers are Linux; Docker networking on macOS differs from Linux; use the cross-platform .NET secret and certificate tooling.

## Run locally

Local services: the app with hot reload, PostgreSQL, and filesystem development storage (or a local S3 emulator). Start PostgreSQL, restore, migrate and run:

```bash
docker compose up -d postgres
dotnet restore Bingo.slnx
dotnet tool restore
dotnet ef database update \
  --project src/Bingo.Infrastructure \
  --startup-project src/Bingo.Web
dotnet run --project src/Bingo.Web
```

Health endpoints: `/health/live` (process running) and `/health/ready` (PostgreSQL reachable; production adds storage and worker checks, see OPERATIONS.md §7).

Only when the local Development database is explicitly disposable, recreate that database (never the Docker volume):

```bash
DOTNET_ENVIRONMENT=Development dotnet ef database drop --force \
  --project src/Bingo.Infrastructure \
  --startup-project src/Bingo.Web
DOTNET_ENVIRONMENT=Development dotnet ef database update \
  --project src/Bingo.Infrastructure \
  --startup-project src/Bingo.Web
```

`docker compose stop postgres` keeps the data; `docker compose down --volumes` deletes the local volume.

### First local administrator

Open `/Account/Setup` on the printed localhost URL. It exists only in Development, only from the local machine and only while no administrator exists. Use a password of at least 12 characters, then sign in at `/Account/Login`. For automated setup configure `DevelopmentAdminBootstrap` through user secrets or environment variables.

### Reset and seed manual-test scenarios

The Development-only reset removes event workflow data and seeds clearly named scenarios for each workflow stage. It keeps your administrator account and password and the OSRS catalogue. Stop the web app, keep PostgreSQL running and run:

```bash
dotnet run --project src/Bingo.Web -- --reset-test-data
```

- It prints every seeded captain username; all use the local-only password `SeedCaptain!1234`. It also creates or refreshes `SeedAdminTwo` (`SeedAdmin!1234`) and the waiting-list account `SeedReplacement` (`SeedReplacement!1234`).
- `Vinterbingo 2026` (`test-15-dkl-live`): the ordinary live-board fixture for public interaction checks.
- `Sommerbingo 2026` (`test-13-dkl-board`): live catalogue derivation, private board editing, approval and the private preview.
- `Det Store Danske Vinterbingo 2027` (`test-62-board-publication-setup`): Signups closed with finalized rosters and an approved but private board, for publication, start blockers and corrections.
- Finalized positive fixtures get active frozen publication snapshots; `test-98-missing-playing-assignment` stays the intentional unpublished negative.
- The reset never imports the historical 2026 event and is unavailable outside Development. Never commit real participant data.

### Isolated UI review environment

`scripts/ui-review.py` builds a separate, disposable review environment from the current checkout (needs Docker, Python 3 and the repository .NET SDK):

```sh
python3 scripts/ui-review.py create
python3 scripts/ui-review.py refresh final-review
python3 scripts/ui-review.py refresh live
python3 scripts/ui-review.py stop
```

- `live` is the default profile; `final-review` ends the sole visible current event into Final review. Both rebuild the review database from the current migrations, the catalogue snapshot and the review scenarios, with dates relative to the run. The app runs at <http://127.0.0.1:5310>.
- The fixed PostgreSQL 17 container `bingo-ui-review` binds only `127.0.0.1:54339`. The script refuses databases named in `appsettings.Local.json` and occupied ports, and never stops foreign processes. Storage, logs and `scenarios.md` (page/state list with a link and account per scenario) live in the gitignored `artifacts/ui-review/`.
- The Wise Old Man client is always the local fake. All review accounts use the local-only password `ReviewOnly!1234`; sign out before switching.

| Account | Review role |
| --- | --- |
| ReviewOwner | SuperAdmin, including hidden event pages |
| ReviewAdmin | Plain Admin |
| ReviewCaptain / ReviewSecondCaptain | Team captains |
| ReviewCoCaptain / ReviewSecondCoCaptain | Team co-captains |
| ReviewParticipant / ReviewSecondMember | Team participants |
| ReviewFormer | Former team member with retained history |
| ReviewWebsite | Plain website account with no event membership |
| ReviewDisabled | Disabled website account (login is refused on purpose) |

## Operator commands

These act on the database in `ConnectionStrings:Database`. Use them only on a database you own. Production deployment commands (`--migrate`, `--production-preflight`, `--convert-luck-checkpoints`) are in OPERATIONS.md.

### Lost-owner recovery

There is no web action for resetting the active Super Admin. After offline verification an operator either creates one 60-minute, single-use reset link:

```bash
dotnet run --project src/Bingo.Web -- \
  --slice1-create-owner-reset-link \
  --username <owner-username> \
  --confirm-username <owner-username> \
  --base-url https://bingo.example.com
```

or atomically transfers ownership to another active website account (the previous owner becomes Admin), recorded as a system audit event:

```bash
dotnet run --project src/Bingo.Web -- --slice1-recover-owner --username <new-owner> --confirm-username <new-owner>
```

`--confirm-username` must match `--username` exactly, including case. The reset link is printed once and delivered only through the verified offline channel, never into logs, tickets or shell history. A later link supersedes an unused earlier one.

### Historical event import

A dormant operator workflow with no web route and no part in the Development reset (contract in PRODUCT.md §19). Run the preflight first and apply only after it succeeds:

```bash
dotnet run --project src/Bingo.Web -- --preflight-historical-import --historical-import-input /secure/path/historical-input.json
dotnet run --project src/Bingo.Web -- --apply-historical-import --historical-import-input /secure/path/historical-input.json --historical-import-actor ActiveSuperAdmin --confirm-historical-import "Det Store Danske Sommerbingo 2026"
```

The private participant/account mapping stays outside Git. The apply is transactional, audited and fail-closed, is a no-op only for an exact matching import hash, needs the exact event-name confirmation and an active SuperAdmin actor, and needs separate authorization before it touches production.

### Catalogue commands

The reviewed catalogue is `src/Bingo.Web/data/osrs-catalogue.json` (snapshot schema v2; v1 still accepted).

- `dotnet run --project src/Bingo.Web -- --apply-catalogue-snapshot` loads it into a CI, Development or manual-test database. It adds missing records and never deletes any. Never run it against retained production data.
- `dotnet run --project src/Bingo.Web -- --apply-wiki-catalogue` applies a reviewed OSRS Wiki rebuild (stop the web app first); run `--reset-test-data` afterwards.
- `dotnet run --project src/Bingo.Web -- --catalogue-price-report` is a read-only price and mapping report. `dotnet run --project src/Bingo.Web -- --sync-catalogue-prices --actor-id <active-super-admin-account-guid>` applies exact matches, mapping checks and prices in one audited transaction; a provider failure changes nothing. Never test applies on a user-owned database.
- Price and mapping operations are read-only unless the explicit apply switch and an audited actor are given. Provider calls happen only for a requested validation, suggestion or refresh, never on render, typing or selection; they are fetched in bulk, and WOM calls go through the shared request limiter.
- `dotnet run --project src/Bingo.Web -- --sync-catalogue-images` prewarms the same-origin OSRS Wiki image cache: HTTPS Wiki URLs only, 8 MB per file, 500 ms pacing (override with `CatalogueImageCache__SyncDelayMilliseconds`, minimum 250 ms). Development caches to the gitignored `src/Bingo.Web/data/catalogue-images`; production sets `CatalogueImageCache__LocalPath` to a persistent volume. Cached binaries are never committed.

## Test

Docker must be running. Ordinary integration tests share one `postgres:17-alpine` container and one migrated template per xUnit collection; each test gets its own cloned database. Migration and server-fact tests own their containers. Tests never use a local app or review database.

```bash
dotnet test Bingo.slnx --no-restore
dotnet format Bingo.slnx --no-restore --verify-no-changes
dotnet build Bingo.slnx --configuration Release --no-restore
```

Test projects: `tests/Bingo.Domain.Tests`, `tests/Bingo.Application.Tests`, `tests/Bingo.IntegrationTests` (PostgreSQL), `tests/Bingo.BrowserTests` (.NET tests plus the standalone `*.js` browser programs). `tests/AdminDesignParityFixture` is the controlled real-layout fixture (with its own PostgreSQL) that the admin-design browser tests start through `scripts/lib/admin-parity-fixture.cjs`; build it before `pnpm test:js`.

### JavaScript and Playwright checks

Every `tests/Bingo.BrowserTests/*.js` file runs in its own Node process. Locally run only the files your change touches (`PLAYWRIGHT_BROWSER=chromium|webkit node tests/Bingo.BrowserTests/<file>`); the full runner runs in GitHub CI (see ../AGENTS.md). Setup and the full runner:

```sh
pnpm install --frozen-lockfile
pnpm exec playwright install --with-deps chromium chrome webkit
export BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY="$PWD/artifacts/js-fixtures"
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction
dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release
pnpm test:js
```

- `pnpm test` is the same as `pnpm test:js`. The runner defaults to Playwright Chromium (`PLAYWRIGHT_CHANNEL=chrome` selects an installed Chrome); `admin-design-*` and `identity-*` files also run in WebKit. `BROWSER_TEST_TIMEOUT_MS` sets the per-file timeout (default 120000). Results go per file and engine to `artifacts/js-tests/`.
- CI shards the runner with `BROWSER_TEST_SHARD=k/N`, balanced by `scripts/browser-test-timings.json` (`node scripts/update-browser-test-timings.cjs` refreshes it).

## CI and release

`.github/workflows/ci.yml` runs on pull requests and pushes to `main`:

- **other-tests**: restore, `dotnet format` verification, Release build, then the Domain, Application and Browser .NET tests.
- **integration**: the Integration tests in eight duration-balanced shards, each with a PostgreSQL service.
- **javascript-tests**: the Node and Playwright programs in nine shards; not yet a gate (`continue-on-error`).
- **build-and-test**: the single required check; needs `other-tests` and `integration`.
- **publish-image** (pushes to `main` only, after `build-and-test`): publishes the candidate image (OPERATIONS.md §3).

Production images are built for the target Linux architecture in GitHub Actions; the Mac builds Arm64 images locally, and production never depends on an image built only on a developer machine. Deployment is the manual `.github/workflows/production-promotion.yml`; gates, procedure and the local rehearsal are in OPERATIONS.md (harness: `scripts/rehearsal/README.md`).

## Configuration and secrets

- The default connection string is local-only and matches `compose.yml`.
- Production secrets come from environment variables, the temporary root-only bootstrap password file or a secret store (OPERATIONS.md §1). Never commit `.env` files or real credentials; keep production credentials out of Markdown, shell history and screenshots. Use a password manager for admin bootstrap, storage, hosting and backup secrets.
- Graphical database clients (DBeaver, pgAdmin) are for inspection only; the schema changes only through the app and its migrations.

## Project structure

```text
src/Bingo.Web             Razor pages, HTTP, authorization, and presentation
src/Bingo.Application     Use cases and workflow orchestration
src/Bingo.Domain          Business rules and domain types
src/Bingo.Infrastructure  PostgreSQL, storage, and external integrations
tests/                    Unit, integration, and browser-level tests
scripts/                  Browser-check runner, UI review, rehearsal harness
deploy/                   Production host scripts and examples
docs/                     Product, architecture, UI, operations and development documents
```
