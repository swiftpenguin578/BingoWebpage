# OSRS Community Bingo

An event platform for running OSRS bingo events for one Discord community.

The repository currently contains the Milestone 1 foundation, Milestone 2 identity/access/audit features, Milestone 3 event/signup workflows, and the approved planning documents.

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
