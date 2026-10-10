# Development

How to set up, run, seed, test and operate DK Legacy Bingo locally, and how CI
checks it. The project introduction is [`README.md`](../README.md). Production
operation is in [`PRODUCTION_RUNBOOK.md`](PRODUCTION_RUNBOOK.md) and
[`PRODUCTION_TOPOLOGY.md`](PRODUCTION_TOPOLOGY.md).

## Prerequisites

- .NET SDK pinned by `global.json` (10.0.x). Install the SDK, not only the
  runtime. Verify with `dotnet --info`.
- Docker with Docker Compose (Docker Desktop on a Mac). PostgreSQL runs in a
  container; no native PostgreSQL is needed. Verify with `docker compose version`.
- Git, with your name and email configured.
- Node 22 and pnpm 11.25.0 for the JavaScript and Playwright checks.
- On a Mac, use the Arm64 installers (`uname -m` prints `arm64` on Apple Silicon).
  Keep Safari (WebKit) and a Chromium browser for manual checks. Any editor works;
  VS Code with the C# Dev Kit and JetBrains Rider both do.
- Six Labors ImageSharp 4.x checks a license at build time. Local builds read the
  untracked repository-root `sixlabors.lic`; CI and Docker pass the
  `SIXLABORS_LICENSE_KEY` secret (see `Directory.Build.props`). Never commit the key
  or the file.

## Run locally

Start PostgreSQL, restore packages and tools, and apply migrations:

```bash
docker compose up -d postgres
dotnet restore Bingo.slnx
dotnet tool restore
dotnet ef database update \
  --project src/Bingo.Infrastructure \
  --startup-project src/Bingo.Web
```

Run the web application and open the URL it prints:

```bash
dotnet run --project src/Bingo.Web
```

Health endpoints: `/health/live` (process is running) and `/health/ready`
(PostgreSQL is reachable).

When the configured local Development database is explicitly disposable, recreate
only that database (never the Docker volume):

```bash
DOTNET_ENVIRONMENT=Development dotnet ef database drop --force \
  --project src/Bingo.Infrastructure \
  --startup-project src/Bingo.Web
DOTNET_ENVIRONMENT=Development dotnet ef database update \
  --project src/Bingo.Infrastructure \
  --startup-project src/Bingo.Web
```

Stop PostgreSQL without deleting data with `docker compose stop postgres`. To delete
the local database volume and start clean, run `docker compose down --volumes`.

### First local administrator

After migrating and starting the app, open `/Account/Setup` on the printed
localhost URL (for example `http://localhost:5164/Account/Setup`). The page exists
only in the Development environment, only from the local machine and only while no
administrator exists. Use a password of at least 12 characters, then sign in at
`/Account/Login`. For automated local setup, configure `DevelopmentAdminBootstrap`
through user secrets or environment variables.

### Reset and seed manual-test scenarios

The Development-only reset removes existing event workflow data and generates
clearly named scenarios for each workflow stage. It keeps your administrator
account and password and the OSRS boss/drop catalogue. Stop the web application,
keep PostgreSQL running and run:

```bash
dotnet run --project src/Bingo.Web -- --reset-test-data
```

The command prints every seeded captain username. All seeded captain accounts use
the local-only password `SeedCaptain!1234`. It also creates or refreshes the
Development administrator `SeedAdminTwo` (`SeedAdmin!1234`) and the waiting-list
account `SeedReplacement` (`SeedReplacement!1234`). Useful fixtures:

- `Vinterbingo 2026` (`test-15-dkl-live`): the ordinary live-board fixture for
  public interaction checks.
- `Sommerbingo 2026` (`test-13-dkl-board`): live catalogue derivation, private board
  editing, approval and the private preview.
- `Det Store Danske Vinterbingo 2027` (`test-62-board-publication-setup`): Signups
  closed with finalized rosters and an approved but private board, for separate
  publication, start blockers and corrections.

The reset never imports the historical 2026 event and is unavailable outside the
Development environment. Never commit real participant data.

### Isolated UI review environment

`scripts/ui-review.py` builds a separate, disposable review environment from the
current checkout (Docker, Python 3 and the repository .NET SDK are required):

```sh
python3 scripts/ui-review.py create
python3 scripts/ui-review.py refresh final-review
python3 scripts/ui-review.py refresh live
python3 scripts/ui-review.py stop
```

`live` is the default profile; `final-review` ends the sole visible current event
into Final review. Both rebuild the owned review database from the current
migrations, the checked-in catalogue snapshot and the review scenarios, with dates
relative to the run. The app runs at <http://127.0.0.1:5310> and the design
references (`docs/references/admin-ui`) are served at <http://127.0.0.1:5320>. The
fixed PostgreSQL 17 container `bingo-ui-review` binds only `127.0.0.1:54339`. The
script refuses databases named in `appsettings.Local.json`, refuses occupied ports
and never stops foreign processes. Storage, logs and `scenarios.md` (the grouped
page/state list with a link and account per scenario) live in the gitignored
`artifacts/ui-review/`. The Wise Old Man client is always the local development
fake. All review accounts use the local-only password `ReviewOnly!1234`; sign out
before switching accounts.

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

These run against the database in `ConnectionStrings:Database`. Use them only on a
database you own.

**Owner password recovery.** There is no web action for resetting the active Super
Admin password. After offline verification, create one 60-minute, single-use link:

```bash
dotnet run --project src/Bingo.Web -- \
  --slice1-create-owner-reset-link \
  --username <owner-username> \
  --confirm-username <owner-username> \
  --base-url https://bingo.example.com
```

`--username` must be the active Super Admin's exact public username and
`--confirm-username` must match it exactly, including case. The link is printed once;
deliver it only through the verified offline channel, never into logs, tickets or
shell history. A later command supersedes an unused earlier link.

**Historical event import.** A separate, dormant operator workflow with no web route
and no part in Development reset. Run the preflight first and apply only after it
succeeds:

```bash
dotnet run --project src/Bingo.Web -- --preflight-historical-import --historical-import-input /secure/path/historical-input.json
dotnet run --project src/Bingo.Web -- --apply-historical-import --historical-import-input /secure/path/historical-input.json --historical-import-actor ActiveSuperAdmin --confirm-historical-import "Det Store Danske Sommerbingo 2026"
```

The private participant/account mapping stays outside Git. The apply is
transactional, audited and fail-closed, is a no-op only for an exact matching import
hash, requires the exact event-name confirmation and an active SuperAdmin actor, and
needs separate authorization before it touches production. The contract is in
[`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md),
[`FUNCTIONAL_CONTRACTS.md`](FUNCTIONAL_CONTRACTS.md),
[`DATA_MODEL.md`](DATA_MODEL.md) and
[`TECHNICAL_ARCHITECTURE.md`](TECHNICAL_ARCHITECTURE.md).

### Catalogue commands

The reviewed catalogue is versioned at `src/Bingo.Web/data/osrs-catalogue.json`
(snapshot schema v2; v1 input is still accepted). Production starts from the
restored database backup and never applies the snapshot during deployment.

- `dotnet run --project src/Bingo.Web -- --apply-catalogue-snapshot` loads the
  snapshot into a CI, Development or manual-test database. It adds missing records
  and never deletes records that historical boards may reference. Never run it
  against retained production data.
- `dotnet run --project src/Bingo.Web -- --apply-wiki-catalogue` applies a reviewed
  OSRS Wiki rebuild (stop the web application first). Run `--reset-test-data`
  afterwards so seeded boards use the new catalogue.
- `dotnet run --project src/Bingo.Web -- --catalogue-price-report` is a read-only
  price and mapping coverage report. After reviewing it,
  `dotnet run --project src/Bingo.Web -- --sync-catalogue-prices --actor-id <active-super-admin-account-guid>`
  applies exact matches, mapping checks and available prices in one audited
  transaction. A provider failure changes nothing. Do not use a user-owned database
  for test applies.
- `dotnet run --project src/Bingo.Web -- --sync-catalogue-images` prewarms the
  same-origin cache of OSRS Wiki catalogue images (HTTPS Wiki URLs only, 8 MB per
  file, paced at 500 ms by default; override with
  `CatalogueImageCache__SyncDelayMilliseconds`, never below the enforced 250 ms).
  Development caches to the git-ignored `src/Bingo.Web/data/catalogue-images`; in
  production set `CatalogueImageCache__LocalPath` to a mounted persistent volume.
  Cached binaries are operational data and are not committed.

Price rules worth knowing: requests go to `https://prices.runescape.wiki/api/v1/osrs/`
(bulk `/mapping` and `/1h`) with the DKLegacy contact User-Agent; two-sided hourly
prices use the midpoint rounded away from zero; a missing price is not evidence of
untradeability; manual and untradeable values survive refresh; and a candidate
outside 0.5x to 2x of a trusted positive value (or a change between zero and
positive) is rejected and flagged. Event-start prices freeze once per event.

## Test

Docker must be running. Ordinary PostgreSQL integration tests share one
`postgres:17-alpine` container and one migrated template per xUnit class
collection; each test gets a uniquely named database cloned from it. Migration and
server-fact tests keep dedicated containers. The tests use their own
Testcontainers databases, never a local application or review database.

```bash
dotnet test Bingo.slnx --no-restore
dotnet format Bingo.slnx --no-restore --verify-no-changes
dotnet build Bingo.slnx --configuration Release --no-restore
```

Test projects: `tests/Bingo.Domain.Tests`, `tests/Bingo.Application.Tests`,
`tests/Bingo.IntegrationTests` (PostgreSQL), `tests/Bingo.BrowserTests` (the .NET
tests plus the standalone `*.js` browser programs) and
`tests/AdminDesignParityFixture` (the real-layout fixture for the parity check).

### JavaScript and Playwright checks

Every `tests/Bingo.BrowserTests/*.js` file runs in its own Node process:

```sh
pnpm install --frozen-lockfile
pnpm exec playwright install --with-deps chromium chrome webkit
export BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY="$PWD/artifacts/js-fixtures"
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --filter FullyQualifiedName~AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction
dotnet build tests/AdminDesignParityFixture/AdminDesignParityFixture.csproj --configuration Release
pnpm test:js
pnpm test:parity
```

`pnpm test` is the same as `pnpm test:js`. The runner defaults to Playwright
Chromium (`PLAYWRIGHT_CHANNEL=chrome` selects an installed Chrome locally); the
`admin-design-*` and `identity-*` files also run in WebKit. `BROWSER_TEST_TIMEOUT_MS`
raises the per-file timeout (default 120000). Results are written per file and
engine to `artifacts/js-tests/`. `pnpm test:parity` compares the frozen design
references in `docs/references/admin-ui` with the real Razor pages served by Kestrel
in both engines, using only its own PostgreSQL container and a synthetic account;
reports and screenshot pairs go to `artifacts/admin-parity/`. Set
`BINGO_PARITY_ROOT` to run the identical assertions against an extracted baseline.
CI shards the runner by `BROWSER_TEST_SHARD=k/N`, balanced by
`scripts/browser-test-timings.json` (`node scripts/update-browser-test-timings.cjs`
refreshes the timings).

## CI and release

`.github/workflows/ci.yml` runs on pull requests and on pushes to `main`:

- **other-tests**: restore, `dotnet format` verification, Release build, then the
  Domain, Application and Browser .NET tests.
- **integration**: the Integration tests in eight duration-balanced shards, each
  with a PostgreSQL service.
- **javascript-tests**: the Node and Playwright programs in nine shards. It is not
  yet a release gate (`continue-on-error`).
- **build-and-test**: the single required check; it needs `other-tests` and
  `integration`.
- **publish-image** (pushes to `main` only, after `build-and-test`): publishes one
  `linux/amd64` image to GHCR and records its immutable digest in a candidate
  artifact.

Deployment is the manual `.github/workflows/production-promotion.yml`
(`workflow_dispatch`, from `main`, with the source SHA, image digest and CI run ID).
Its `promote` mode only records a receipt; `deploy` mode is the production approval.
The release gates and the operator procedure are in
[`DELIVERY_PLAN.md`](DELIVERY_PLAN.md#release-and-deploy-gates) and
[`PRODUCTION_RUNBOOK.md`](PRODUCTION_RUNBOOK.md). The local rehearsal harness is
documented in `scripts/rehearsal/README.md`.

## Configuration and secrets

The default connection string is local-only and matches `compose.yml`. Production
secrets come from environment variables, the documented temporary root-only
bootstrap password file or a secret store; never commit `.env` files or real
credentials, and keep production credentials out of Markdown files, shell history and
screenshots. Use a password manager for admin bootstrap, storage, hosting and backup
secrets.

Production Compose uses `BINGO_POSTGRES_DB`, `BINGO_POSTGRES_USER` and
`BINGO_POSTGRES_PASSWORD` as the one database authority: the application connection
is built from the same values, and the host scripts use them for backup, evidence
verification, migrations and readiness. Keep a real password in a shell-quoted
root-only env file; quoted fields allow punctuation such as `@`, `#`, `!`, `$`, `=`
and `;`.

Graphical database clients (DBeaver, pgAdmin) are for inspection only. The
application and its migrations are the way to change the schema.

## Project structure

```text
src/Bingo.Web             Razor pages, HTTP, authorization, and presentation
src/Bingo.Application     Use cases and workflow orchestration
src/Bingo.Domain          Business rules and domain types
src/Bingo.Infrastructure  PostgreSQL, storage, and external integrations
tests/                    Unit, integration, and browser-level tests
scripts/                  Browser-check runner, UI review, rehearsal harness
deploy/                   Production host scripts and examples
docs/                     Product, technical, UI and delivery documents
```
