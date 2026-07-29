# OSRS Community Bingo

An event platform for running OSRS bingo events for one Discord community.

The repository currently contains Milestones 1–7: the application foundation; identity, access, and audit; event signup; catalogue and board building; teams and snake draft; evidence review; and public live boards with rankings.

## Requirements

- .NET 10 SDK (pinned by `global.json`)
- Docker with Docker Compose
- Git

See [DEVELOPMENT_SETUP.md](DEVELOPMENT_SETUP.md) for the complete Mac setup.

## Run locally

Start PostgreSQL:

```bash
docker compose up -d postgres
```

Restore packages and apply migrations:

```bash
dotnet restore Bingo.slnx
dotnet tool restore
dotnet ef database update \
  --project src/Bingo.Infrastructure \
  --startup-project src/Bingo.Web
```

Run the web application:

```bash
dotnet run --project src/Bingo.Web
```

Use the URL printed by ASP.NET Core. The health endpoints are:

- `/health/live`: application process is running
- `/health/ready`: PostgreSQL is reachable

### Create the first local administrator

After applying migrations and starting the app, open `/Account/Setup` on the printed localhost URL. For example:

```text
http://localhost:5164/Account/Setup
```

This setup page is available only in the Development environment, only from the local machine, and only while no administrator exists. Use a password of at least 12 characters. After creation, sign in through `/Account/Login`.

As an alternative for automated local setup, configure `DevelopmentAdminBootstrap` through user secrets or environment variables. Never put a real password in committed settings.

### Operator-only owner password reset

There is no web action for resetting the active Super Admin password. After offline verification, an operator may create one 60-minute, single-use recovery link for the current active owner:

```bash
dotnet run --project src/Bingo.Web -- \
  --slice1-create-owner-reset-link \
  --username <owner-username> \
  --confirm-username <owner-username> \
  --base-url https://bingo.example.com
```

`--username` must be the active Super Admin's exact public username, and `--confirm-username` must match it exactly, including case. The command prints the raw link once. Deliver it only through the verified offline channel; do not put it in logs, tickets, or shell history. A later command supersedes an unused earlier owner-recovery link.

### Create and test an event

After signing in as an administrator:

1. Open **Admin → Events → Create event**.
2. Save the event as a private draft.
3. Add optional signup questions from the event workspace. External/pre-formed roster CSV, if introduced, is deferred to Slice 5.
4. Open signups. This publishes only the event signup page, not teams or the board.
5. Follow the displayed public signup URL in a private browser window to test participant signup.

Signup edit links are shown once on the confirmation page. Only a hash of each private token is stored in PostgreSQL.

Stop PostgreSQL without deleting local data:

```bash
docker compose stop postgres
```

### Reset and seed manual-test scenarios

The development-only reset command removes existing event workflow data and generates clearly named scenarios for each workflow stage. It preserves the existing administrator account, its password, and the OSRS boss/drop catalogue.

Stop the running web application, keep PostgreSQL running, and execute:

```bash
dotnet run --project src/Bingo.Web -- --reset-test-data
```

The command resets generated workflow data and creates only `TEST 13 — DKL Board` and `TEST 15 — DKL Live`. It preserves the retained OSRS catalogue, bootstrap/Super Admin, and the secondary seeded Admin. Both events are internal Development fixtures rather than automatic public current events.

Use `TEST 13 — DKL Board` to review and edit the 5×5 historical DKL comparison board. `TEST 15 — DKL Live` provides the retained full live DKL board, teams, accounts, evidence, and approved progress behavior.

The command prints every seeded captain username. All seeded captain accounts use the local-only password `SeedCaptain!1234`. Your existing administrator username and password are unchanged. It also creates or refreshes the development-only administrator `SeedAdminTwo` with password `SeedAdmin!1234`, which is used to test simultaneous board editing and draft control from a second browser session.

For public-board testing, open `TEST 15 — DKL Live` directly. Approval, reversal, and evidence-visibility changes invalidate open public pages through SignalR; a 30-second refresh remains as a fallback.

This operation is intentionally unavailable outside the Development environment.

### Apply the reviewed OSRS Wiki catalogue

After reviewing **Admin → OSRS catalogue → Wiki import preview**, stop the running web application and apply the approved rebuild:

```bash
dotnet run --project src/Bingo.Web -- --apply-wiki-catalogue
```

The import preserves boss/activity records and clan EHB rates, replaces their imported drop connections with the reviewed special/unique rewards, stores Wiki source and image URLs, and keeps conditional rates as separate variants. It removes old imported drops and catalogue items only when they are no longer connected to any boss. Run the test-data reset afterward so seeded boards are rebuilt from the new catalogue.

### Preserve and restore the reviewed OSRS catalogue

The reviewed catalogue is versioned at `src/Bingo.Web/data/osrs-catalogue.json`. After deliberately reviewing or manually correcting catalogue data, export the database state into that file:

```bash
dotnet run --project src/Bingo.Web -- --export-catalogue-snapshot
```

To populate a freshly migrated deployment whose catalogue tables are empty:

```bash
dotnet run --project src/Bingo.Web -- --apply-catalogue-snapshot
```

Applying the snapshot safely updates the catalogue created by older migrations and adds missing records; it does not delete catalogue records that historical boards may reference. The Wiki import remains a discovery/update workflow; it is not the authoritative deployment seed. Commit and review snapshot changes alongside the catalogue edits that produced them.

### Cache OSRS Wiki catalogue images

Boss and item records retain their original OSRS Wiki image URLs, but the web UI serves those images through a same-origin persistent cache. Development uses the Git-ignored `src/Bingo.Web/data/catalogue-images` directory. In production, set `CatalogueImageCache__LocalPath` to a mounted persistent-volume path; do not rely on a container's temporary filesystem.

Images populate on first use. A deployment can prewarm all reviewed catalogue and board artwork after applying the catalogue snapshot:

```bash
dotnet run --project src/Bingo.Web -- --sync-catalogue-images
```

The synchronization command never changes catalogue records. It accepts only HTTPS OSRS Wiki image URLs, validates returned image types, limits individual files to 8 MB, and reports failed downloads. It waits 500 ms between sources by default and backs off before retrying Wiki rate-limit or temporary-service responses. Override the pacing with `CatalogueImageCache__SyncDelayMilliseconds` when necessary, but do not reduce it below the enforced 250 ms minimum. Cached binaries are operational data and must not be committed to Git.

Delete the local database volume and start clean:

```bash
docker compose down --volumes
```

## Test

Docker must be running because the integration suite starts an isolated PostgreSQL container.

```bash
dotnet test Bingo.slnx
```

Format and verify:

```bash
dotnet format Bingo.slnx
dotnet build Bingo.slnx --configuration Release
```

## Project structure

```text
src/Bingo.Web             Razor pages, HTTP, authorization, and presentation
src/Bingo.Application     Use cases and workflow orchestration
src/Bingo.Domain          Business rules and domain types
src/Bingo.Infrastructure  PostgreSQL, storage, and external integrations
tests/                    Unit, integration, and browser-level tests
```

## Configuration

The default connection string is intentionally local-only and matches `compose.yml`.

Production secrets must be provided through environment variables or a secret store. Never commit `.env` files or real credentials.

## Planning documents

- [Product requirements](PRODUCT_REQUIREMENTS.md)
- [Data model](DATA_MODEL.md)
- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [Implementation roadmap](IMPLEMENTATION_ROADMAP.md)
