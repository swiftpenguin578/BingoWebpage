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

### Create and test an event

After signing in as an administrator:

1. Open **Admin → Events → Create event**.
2. Save the event as a private draft.
3. Add optional signup questions or preview a CSV import from the event workspace.
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

The command creates events named `TEST 00` through `TEST 13` covering private setup, open signups, waiting lists, pre-board setup, board editing, draft setup, an in-progress draft, finalized teams, a live event, final review, a populated submission-review queue, finalized official results, a fully completed board, a large pre-draft setup, and an editable DKL comparison board. Board scenarios use the retained OSRS catalogue.

`TEST 09 — Evidence` is the newest live event and is therefore selected automatically by the admin review queue. It contains real local evidence images and fixtures for pending, changes-requested, rejected, withdrawn, approved, privacy-hidden, duplicate-checksum, replacement-history, weighted, capped, and reversal-rebalancing cases.

Use `TEST 08 — Review` for the final-review checklist, blocker overrides, and finalization. Use `TEST 10 — Finished` for official snapshot, archive, unfinalization, historical-version, and locked-submission testing. Use `TEST 11 — Complete` for completion-time corrections and completed-board finalization.

Use `TEST 12 — Large Draft` to test scrambling and starting a draft with 60 confirmed players, four drafted teams, and a target of 15 players per team.

Use `TEST 13 — DKL Board` to review and edit the 5×5 historical DKL comparison board. Its tiles follow the workbook order and seed the currently understood eligible drops, objective quantities, and weighted megarares. Descriptions call out intentionally uncertain selections such as God Wars, Araxxor, Maggot King, and Doom so they can be corrected directly in the board editor.

The command prints every seeded captain username. All seeded captain accounts use the local-only password `SeedCaptain!1234`. Your existing administrator username and password are unchanged. It also creates or refreshes the development-only administrator `SeedAdminTwo` with password `SeedAdmin!1234`, which is used to test simultaneous board editing and draft control from a second browser session.

For public-board testing, open the site home page after seeding and select `TEST 09 — Evidence`. Its public overview contains ranked teams, approved progress, and a completed first row for `Seeded Ravens`. Open that team, then select completed or in-progress tiles to verify public evidence and the hidden-evidence placeholder. Approval, reversal, and evidence-visibility changes invalidate open public pages through SignalR; a 30-second refresh remains as a fallback.

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
