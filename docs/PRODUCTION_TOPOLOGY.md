# Production topology

This is the provider-neutral deployment contract for one VPS. It uses the
reviewed web image and standard Docker Compose; provider provisioning, CI/image
publication, deployment automation, backups, restore rehearsal, and monitoring
remain later release passes. The application operations in this document are
implemented in Production Release Pass 2.

## Services and network

- `caddy` is the only public entry point. It publishes TCP/UDP ports 80 and
  443, obtains and renews HTTPS certificates for `BINGO_DOMAIN`, and proxies
  normal HTTP requests and WebSocket/SignalR upgrades to `web:8080`.
  Eligible responses are compressed with zstd or gzip.
- `web` is one ASP.NET Core replica. It is reachable only on the private
  `bingo-private` Docker network and accepts an immutable image reference in
  `BINGO_WEB_IMAGE`, normally a GHCR digest such as
  `ghcr.io/example/osrs-community-bingo@sha256:<digest>`.
- `postgres` is PostgreSQL 17 on the same private network. It has no published
  host port; the `web` service connects to it at `postgres:5432`.

The production definition is [`compose.production.yml`](../compose.production.yml)
and the Caddy site is [`deploy/Caddyfile`](../deploy/Caddyfile). Caddy's
standard `reverse_proxy` handles HTTP and WebSocket upgrades; no separate
SignalR proxy path is required.

## Persistent volumes

| Volume | Container path | Contract |
| --- | --- | --- |
| `bingo-production-postgres` | `/var/lib/postgresql/data` | PostgreSQL data |
| `bingo-production-data-protection` | `/var/lib/bingo/data-protection-keys` | Reserved for ASP.NET data-protection keys |
| `bingo-production-catalogue-images` | `/var/lib/bingo/catalogue-images` | Same-origin OSRS Wiki catalogue-image cache |
| `bingo-production-caddy-data` | `/data` | Caddy certificates and state |
| `bingo-production-caddy-config` | `/config` | Caddy runtime config state |

`DataProtection__KeyRingPath` is persisted by the Production application and
validated before it serves. The web image must be able to create and write the
mounted directory; Compose's short-lived `web-init` service establishes that
ownership before `web` starts.

## Configuration

[`deploy/production.env.example`](../deploy/production.env.example) lists the
names only. Keep the real environment file outside source control or provide
the values through an approved secret mechanism. The required groups are:

- `BINGO_DOMAIN` and `BINGO_WEB_IMAGE` for the public hostname and immutable
  application image.
- `BINGO_POSTGRES_DB`, `BINGO_POSTGRES_USER`, `BINGO_POSTGRES_PASSWORD`, and
  `BINGO_DATABASE_CONNECTION` for PostgreSQL and the application connection.
  The database, username, and password in the two settings must agree.
- `BINGO_DISCORD_CLIENT_ID` and `BINGO_DISCORD_CLIENT_SECRET`; register the
  callback path `/Account/DiscordCallback` for the configured HTTPS domain.
- `BINGO_R2_ACCOUNT_ID`, `BINGO_R2_ACCESS_KEY_ID`,
  `BINGO_R2_SECRET_ACCESS_KEY`, `BINGO_R2_BUCKET`, and
  `BINGO_R2_SERVICE_URL` for the current R2 evidence-storage implementation.
- `BINGO_WISE_OLD_MAN_BASE_URL`, `BINGO_WISE_OLD_MAN_USER_AGENT`,
  `BINGO_WISE_OLD_MAN_API_KEY` (optional), and
  `BINGO_WISE_OLD_MAN_TIMEOUT_SECONDS` for the production Wise Old Man
  identity and request options.
- `BINGO_BOOTSTRAP_OWNER_PASSWORD` for the one-shot controlled initial owner
  command, plus `BINGO_CATALOGUE_IMAGE_SYNC_DELAY_MILLISECONDS` for the
  existing catalogue-cache operation.

Compose also sets `ASPNETCORE_ENVIRONMENT=Production`, listens on container
port 8080, trusts the reverse proxy's forwarded HTTPS headers through the
standard ASP.NET container switch, selects R2, and points the catalogue cache
at its persistent volume. Development fake WOM settings and development
credentials are not part of this topology.

## Application operations

- Production uses the built-in JSON console logger and does not trust arbitrary
  external forwarded headers. Caddy remains the only public proxy and
  overwrites forwarded headers.
- `/health/live` is a public, cheap liveness response. `/health/ready` checks
  PostgreSQL, R2 bucket reachability, and timely heartbeats from both hosted
  workers. Caddy returns 404 for `/health/ready`; Compose probes it directly on
  the private web container and starts Caddy only after web is healthy.
- `--migrate` is the only explicit migration command. Normal Production
  startup never migrates. `--production-preflight` is read-only and requires
  no pending migrations, usable R2, a complete catalogue baseline, and exactly
  one active Super Admin. Wise Old Man requests do not gate readiness.

## Clean initial start

Production startup intentionally requires exactly one active Super Admin. For
an empty database, an operator with the real environment file should:

1. Start only PostgreSQL and wait for its health check.
2. Run the reviewed image once with `--migrate`.
3. Run the reviewed image once with `--apply-catalogue-snapshot`; migrations
   must already be applied.
4. Run the reviewed image once with
   `--slice1-bootstrap-owner --username <owner> --confirm-username <owner>`.
   The command consumes `Slice1__BootstrapOwnerPassword` from the environment.
5. Run the read-only `--production-preflight` with the reviewed image.
6. Start `web` and `caddy`, then run the release smoke checks. Catalogue-image
   prewarming, if selected, uses the existing `--sync-catalogue-images`
   command after the application is configured.

For retained data, run the existing `--slice1-migration-preflight` only when
crossing that legacy boundary and correct the specifically reported rows.
Then run `--migrate`, `--production-preflight`, and replace `web`. Never apply
the catalogue snapshot to retained data; it deactivates records absent from
the snapshot.

These commands do not touch provider resources, DNS, credentials, or
production data unless an operator runs them against that environment.

## Safe validation

Use the example file only for Compose rendering; its values are placeholders
and no image is pulled:

```sh
docker compose --env-file deploy/production.env.example \
  -f compose.production.yml config --quiet
docker compose --env-file deploy/production.env.example \
  -f compose.production.yml config
```

The rendered configuration should contain `caddy`, `web`, and `postgres`, one
private network, five named volumes, public ports only on Caddy, and no
PostgreSQL host-port mapping. A later deployment pass will replace the dummy
GHCR digest with the reviewed published digest and supply real secret values.
