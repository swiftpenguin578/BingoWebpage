# OSRS Community Bingo

An event platform for running OSRS bingo events for one Discord community.

The repository contains the current version-one functional foundation and an in-progress UI baseline; it is not release-ready. For behavior and authority, start with [PRODUCT_REQUIREMENTS.md](PRODUCT_REQUIREMENTS.md), [FUNCTIONAL_CONTRACTS.md](FUNCTIONAL_CONTRACTS.md), [DATA_MODEL.md](DATA_MODEL.md), [TECHNICAL_ARCHITECTURE.md](TECHNICAL_ARCHITECTURE.md), [UI_SYSTEM.md](UI_SYSTEM.md), and [UI_PAGE_MATRIX.md](UI_PAGE_MATRIX.md). Use [CURRENT_STATUS.md](CURRENT_STATUS.md) and [DELIVERY_PLAN.md](DELIVERY_PLAN.md) for current state and remaining order, and [MANUAL_TEST_CHECKLIST.md](MANUAL_TEST_CHECKLIST.md) for manual verification only.

## Requirements

- .NET 10 SDK (pinned by `global.json`)
- Docker with Docker Compose
- Git

See [DEVELOPMENT_SETUP.md](DEVELOPMENT_SETUP.md) for the complete Mac setup.
The provider-neutral production topology and exact deployment/restore procedure
are [PRODUCTION_TOPOLOGY.md](docs/PRODUCTION_TOPOLOGY.md) and
[PRODUCTION_RUNBOOK.md](docs/PRODUCTION_RUNBOOK.md).

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

When the configured local Development database is explicitly disposable, recreate only that database (never the Docker volume) with:

```bash
DOTNET_ENVIRONMENT=Development dotnet ef database drop --force \
  --project src/Bingo.Infrastructure \
  --startup-project src/Bingo.Web
DOTNET_ENVIRONMENT=Development dotnet ef database update \
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
3. Add optional signup questions from the event workspace. External/pre-formed roster CSV is limited to the dedicated Admin team workflow and is not part of ordinary participant signup.
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

The command resets generated workflow data and creates named Development fixtures for ordinary workflow stages. It does not import or synchronize `Det Store Danske Sommerbingo 2026`, does not seed a real historical roster, and does not create a temporary Live historical event. Finalized positive fixtures intended to expose public rosters receive the same active frozen publication snapshot as normal draft finalization, so their `/Events/{slug}/Teams` routes are reachable; deliberately incomplete blocker fixtures remain unpublished. The reset preserves the retained OSRS catalogue, bootstrap/Super Admin, and secondary seeded Admin. All events are internal Development fixtures rather than automatic public current events; obsolete or manually created disposable events are removed.

Use `Sommerbingo 2026` (`test-13-dkl-board`) to review live catalogue derivation, private board editing, approval/unapproval, and the private demonstration preview. `Det Store Danske Vinterbingo 2027` (`test-62-board-publication-setup`) is SignupClosed with finalized rosters and an approved but private board: it is the direct separate-publication, start-blocker, frozen-public-board, and exceptional-correction fixture. `Vinterbingo 2026` (`test-15-dkl-live`) remains the ordinary Development live-board fixture for public interaction checks. The prior `test-101-danish-summer-bingo-2026` historical-fixture claims are stale and must not be treated as the approved historical record; implementation must replace or remove conflicting fixture claims without committing real participant data.

The command prints every seeded captain username. All seeded captain accounts use the local-only password `SeedCaptain!1234`. Your existing administrator username and password are unchanged. It also creates or refreshes the development-only administrator `SeedAdminTwo` with password `SeedAdmin!1234`, the linked waiting-list account `SeedReplacement` with password `SeedReplacement!1234`, and the `Vinterbingo 2026` evidence accounts documented in `MANUAL_TEST_CHECKLIST.md`.

For public-board testing, open `Vinterbingo 2026` directly. Approval and reversal invalidate open public pages through SignalR. Eligible participants receive announcements and live Drops NEW updates; the board does not automatically reload. Reconnecting or navigating reconciles update state. See the drop-announcement checklist in `MANUAL_TEST_CHECKLIST.md` for the current acceptance setup and verification status.

This operation is intentionally unavailable outside the Development environment.

### Import the approved historical event

The historical import is a separate, dormant operator workflow; it is not part
of Development reset and is not available through a web route. Run the explicit
CLI preflight first and apply only after preflight succeeds:

```bash
dotnet run --project src/Bingo.Web -- --preflight-historical-import --historical-import-input /secure/path/historical-input.json
dotnet run --project src/Bingo.Web -- --apply-historical-import --historical-import-input /secure/path/historical-input.json --historical-import-actor ActiveSuperAdmin --confirm-historical-import "Det Store Danske Sommerbingo 2026"
```

The operator supplies the private 90-participant/93-account mapping outside
Git. The checked-in public manifest is pinned to SHA-256
`e5297b20fc5e4a842b6a1e5ab378128cbe1c2bad16033fc875c54607c0d49438` and carries
the exact corrected 402 counter units across 150 team/tile cells. The apply
operation creates **Det Store Danske Sommerbingo 2026** directly as archived
history using the frozen contract in
[`PRODUCT_REQUIREMENTS.md`](PRODUCT_REQUIREMENTS.md),
[`FUNCTIONAL_CONTRACTS.md`](FUNCTIONAL_CONTRACTS.md),
[`DATA_MODEL.md`](DATA_MODEL.md), and
[`TECHNICAL_ARCHITECTURE.md`](TECHNICAL_ARCHITECTURE.md). It is transactional,
audited, fail-closed, and a no-op only for an exact matching import hash; a
divergent hash fails closed and any apply failure rolls back without partial
state. Apply also requires the exact event-name confirmation and validates the
named actor as an active SuperAdmin inside the locked serializable transaction.
The apply is a separately authorized operation: do not run it against production during
implementation or local rehearsal; production import and any rehearsal-event
hide/removal require separate post-deployment authorization.

### Apply the reviewed OSRS Wiki catalogue

After reviewing **Admin → OSRS catalogue → Wiki import preview**, stop the running web application and apply the approved rebuild:

```bash
dotnet run --project src/Bingo.Web -- --apply-wiki-catalogue
```

The import preserves boss/activity records and clan EHB rates, replaces their imported drop connections with reviewed rewards, and stores Wiki source and image URLs. Each imported source drop retains one authoritative rate/probability and records assumptions in its note. It removes old imported drops and catalogue items only when they are no longer connected to any boss. Run the test-data reset afterward so seeded boards are rebuilt from the new catalogue.

### Preserve and restore the reviewed OSRS catalogue

The reviewed catalogue is versioned at `src/Bingo.Web/data/osrs-catalogue.json`. Production rebuilds start from the restored database backup and do not apply this snapshot during deployment. The snapshot loader is retained for CI, Development, and manual-test databases only:

```bash
dotnet run --project src/Bingo.Web -- --apply-catalogue-snapshot
```

Applying the snapshot safely updates a disposable catalogue created by migrations and adds missing records; it does not delete catalogue records that historical boards may reference. The Wiki import remains a discovery/update workflow; it is not the authoritative deployment seed. Commit and review snapshot changes alongside the catalogue edits that produced them.

### Catalogue API mapping and price reports

Existing Admin Catalogue activity/drop editors have collapsed API sections. Suggestions
use unique exact names only; check the matched item and its variant before validating.
Ordinary page views, typing and saving without validation do not fetch provider data.
New items need a fetched or manual GP value, including zero; explicitly untradeable
items use zero. Adding an existing shared item retains its value. Manual/untradeable
values survive bulk refresh. Selecting API hourly average and validating with an
available price explicitly switches a fixed value back to API pricing.

Read-only coverage report (no actor required):

```sh
dotnet run --project src/Bingo.Web -- --catalogue-price-report
```

After reviewing the report and authorizing changes for the intended database, explicitly
apply exact matches, mapping checks and available prices as an active Super Admin:

```sh
dotnet run --project src/Bingo.Web -- --sync-catalogue-prices --actor-id <active-super-admin-account-guid>
```

Both commands use the configured `ConnectionStrings:Database` and return before normal
startup migrations. Apply checks the actor and writes catalogue changes and audit records
in one transaction. A provider failure produces an error without changes; retry the report.
The report includes proposed item IDs/values and unresolved items/sources. It does not
prove deployed coverage until run against that catalogue. Do not use a user-owned database
for test applies. Re-run the report after correcting unresolved mappings in existing editors.

Pricing requests use `https://prices.runescape.wiki/api/v1/osrs/` bulk `/mapping` and `/1h`,
with this exact raw header on every request:
`DKLegacy - Community bingo item pricing - Discord: @chrisschmidt`.
Two-sided hourly prices use a midpoint rounded away from zero; one side uses that price.
A missing item/price is not evidence of untradeability and keeps an existing fallback.
Changed item IDs clear old verification and API-derived prices while retaining explicit
manual/untradeable values. Successful provider responses cache for five minutes, failures
for 30 seconds; Wiki requests time out after 15 seconds and retry transient 502/503/504 once.
WOM validation reads boss keys from `/v2/efficiency/rates?type=main&metric=ehb` through the
existing shared request limiter. It never imports numerical efficiency rates or proves
competition metric/mode coverage (a later Stats pass).

Later API candidates outside 0.5×–2× of a trusted positive catalogue value, or changes
between zero and positive, are rejected. Exact ratio boundaries and zero→zero are accepted.
The catalogue API section and operator report identify the rejected candidate; the trusted
value remains usable. Review flagged changes and enter a checked manual value or retry a
valid current API result. The initial population still needs operator checking, and this
simple guard does not establish that a market price is authentic. It does not reprice an
event: event-start values and values first introduced through a published correction freeze
once. Existing frozen prices remain usable after catalogue edits.

Catalogue snapshot exports use schema v2 and retain mapping/price metadata. Schema v1
imports remain accepted; absent legacy IDs preserve existing metadata, while changing
an explicit ID invalidates its old API association. The checked-in snapshot was populated
on 2026-09-16 with 195 verified API item mappings/prices, 116 confirmed untradeables at
0 GP and 68 verified WOM source mappings. Initial prices use 173 bulk-hour observations
and the latest available completed hourly observation for 22 sparsely traded items;
each retains its actual observation timestamp. This one-time population does not change
runtime refresh policy: an absent hourly price retains the stored catalogue fallback.
Clean bootstrap applies this snapshot through the deployment procedure. The retained
`--migrate` path includes the one-time `20260916100000_PopulateRetainedCatalogue`
migration, which carries this frozen approved payload inside the migration. It resolves
each item by its exact name and normalized name and each boss by its exact name and slug,
then fills only missing eligible mapping and pricing fields. It preserves configured or
conflicting mappings, manual values, newer observations, metadata, extra records, drop
rates and frozen event prices. Each changed row receives a version increment and a
before/after audit entry under the `system/catalogue-migration` actor. Missing or
ambiguous identities fail the migration transaction without partial changes, and the
payload is never read from mutable Web JSON at runtime. On clean bootstrap, the migration
recognizes the seed-only catalogue and defers to the following full snapshot import.
The bulk price-refresh command alone cannot supply the approved untradeable classifications
and historical-hour fallbacks. Never run the whole snapshot importer against retained
production data.
The default WOM User-Agent now identifies DKLegacy with the operator's Discord contact;
environment overrides must retain a valid contact-bearing structured User-Agent.

### Cache OSRS Wiki catalogue images

Boss and item records retain their original OSRS Wiki image URLs, but the web UI serves those images through a same-origin persistent cache. Development uses the Git-ignored `src/Bingo.Web/data/catalogue-images` directory. In production, set `CatalogueImageCache__LocalPath` to a mounted persistent-volume path; do not rely on a container's temporary filesystem.

Images populate on first use. A deployment can prewarm all reviewed catalogue and board artwork after the database restore/rebuild:

```bash
dotnet run --project src/Bingo.Web -- --sync-catalogue-images
```

The synchronization command never changes catalogue records. It accepts only HTTPS OSRS Wiki image URLs, validates returned image types, limits individual files to 8 MB, and reports failed downloads. The on-demand cache coalesces identical misses, retains successful files across restarts, allows one Wiki image fetch at a time with at least 500 ms between provider starts, honors provider-directed pauses across cold image keys, applies bounded negative cooldowns, and leaves provider failures local so rendered clients can show their no-art state. The command waits 500 ms between sources by default and backs off before retrying Wiki rate-limit or temporary-service responses. Override the pacing with `CatalogueImageCache__SyncDelayMilliseconds` when necessary, but do not reduce it below the enforced 250 ms minimum. Cached binaries are operational data and must not be committed to Git.

Delete the local database volume and start clean:

```bash
docker compose down --volumes
```

## Test

Docker must be running because the integration suite starts an isolated PostgreSQL container.

```bash
dotnet test Bingo.slnx --no-restore
```

Verify formatting and build:

```bash
dotnet format Bingo.slnx --no-restore --verify-no-changes
dotnet build Bingo.slnx --configuration Release --no-restore
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

Production secrets must be provided through environment variables, the documented
temporary root-only bootstrap password file, or a secret store. Never commit
`.env` files or real credentials.

Production Compose uses `BINGO_POSTGRES_DB`, `BINGO_POSTGRES_USER`, and
`BINGO_POSTGRES_PASSWORD` as the one database authority. The application
connection is constructed from those same values, and the host scripts use
those variables for PostgreSQL backup, evidence verification, migrations, and
readiness. Keep a real password in a shell-quoted root-only env file; quoted
connection fields support ordinary punctuation such as `@`, `#`, `!`, `$`,
`=`, and `;`.

## Planning documents

- [Product requirements](PRODUCT_REQUIREMENTS.md)
- [Functional contracts](FUNCTIONAL_CONTRACTS.md)
- [Data model](DATA_MODEL.md)
- [Technical architecture](TECHNICAL_ARCHITECTURE.md)
- [UI system](UI_SYSTEM.md)
- [UI page matrix](UI_PAGE_MATRIX.md)
- [Current status](CURRENT_STATUS.md)
- [Delivery plan](DELIVERY_PLAN.md)
- [Manual test checklist](MANUAL_TEST_CHECKLIST.md)
