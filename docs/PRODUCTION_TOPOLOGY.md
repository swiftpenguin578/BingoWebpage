# Production topology

This is the provider-neutral deployment contract for one VPS. It uses the
reviewed web image and standard Docker Compose. The application operations are
implemented in Production Release Pass 2; Pass 3 publishes a release
candidate and records a non-mutating promotion receipt; Pass 4 adds the
repository-side deployment, backup, restore-verification, and operator
runbook contract. Provider provisioning and production mutation remain
explicit operator decisions. See [`PRODUCTION_RUNBOOK.md`](PRODUCTION_RUNBOOK.md)
for the one-time bootstrap and repeat-operation procedures.

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
| `bingo-production-postgres` | `/var/lib/postgresql/data` | PostgreSQL data; explicit physical Compose name |
| `bingo-production-data-protection` | `/var/lib/bingo/data-protection-keys` | Reserved for ASP.NET data-protection keys; explicit physical Compose name |
| `bingo-production-catalogue-images` | `/var/lib/bingo/catalogue-images` | Same-origin OSRS Wiki catalogue-image cache; explicit physical Compose name |
| `bingo-production-caddy-data` | `/data` | Caddy certificates and state; explicit physical Compose name |
| `bingo-production-caddy-config` | `/config` | Caddy runtime config state; explicit physical Compose name |

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
- `BINGO_POSTGRES_DB`, `BINGO_POSTGRES_USER`, and `BINGO_POSTGRES_PASSWORD` for
  PostgreSQL and the application connection. Compose constructs the quoted
  application connection from these same values; the host scripts use the
  same variables for backup, evidence, migration, and readiness.
- `BINGO_DISCORD_CLIENT_ID` and `BINGO_DISCORD_CLIENT_SECRET`; register the
  callback path `/Account/DiscordCallback` for the configured HTTPS domain.
- `BINGO_R2_ACCOUNT_ID`, `BINGO_R2_ACCESS_KEY_ID`,
  `BINGO_R2_SECRET_ACCESS_KEY`, `BINGO_R2_BUCKET`, and
  `BINGO_R2_SERVICE_URL` for the current R2 evidence-storage implementation.
- `BINGO_WISE_OLD_MAN_BASE_URL`, `BINGO_WISE_OLD_MAN_USER_AGENT`,
  `BINGO_WISE_OLD_MAN_API_KEY` (optional), and
  `BINGO_WISE_OLD_MAN_TIMEOUT_SECONDS` for the production Wise Old Man
  identity and request options.
- `BINGO_BOOTSTRAP_OWNER_PASSWORD_FILE` names the temporary root-only file used
  only for the one-shot controlled initial owner command. It is deleted after a
  successful bootstrap and is never part of the long-running web environment;
  `BINGO_CATALOGUE_IMAGE_SYNC_DELAY_MILLISECONDS` controls the existing
  catalogue-cache operation.

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

## Candidate publication and promotion

After the existing CI build-and-test job succeeds for a push to `main`, CI
publishes exactly one `linux/amd64` image to the repository-owned GHCR package.
The only tag is `sha-<full-source-sha>`; the digest returned by the publish step
is authoritative. CI uploads a short-lived candidate receipt bound to the
source SHA, digest, image, platform, and CI run.

The manual production-promotion workflow can be dispatched only from `main`.
Both modes validate the selected successful CI run and exact candidate receipt
before entering the `production` Environment. `promote` preserves the
non-mutating receipt with `deployment: false`; `deploy` receives one approval,
uses `concurrency: production` without cancellation, and calls only the
root-owned host command over strict native OpenSSH. GitHub carries no
application or infrastructure secret.

GHCR is assumed private by default. Do not change package visibility or create
the package/environment as part of this repository pass. Before Pass 4, confirm
repository visibility and the GitHub plan, because required environment
reviewers may be limited for private repositories on some plans; decide then
whether the image remains private.

## Clean initial start

Production startup intentionally requires exactly one active Super Admin. The
root-only `BINGO_BOOTSTRAP_STATE_FILE` marker must contain exactly `new`,
`interrupted`, or `completed`, and is included in every backup. A new host starts
with `new`; a failed first bootstrap leaves `interrupted` so the same path can
resume; `completed` means retained production even when it has zero accounts.

For a genuinely new database, an operator with the real environment file should:

1. Start only PostgreSQL and wait for its health check.
2. Run the reviewed image once with `--migrate`.
3. Run the reviewed image once with `--apply-catalogue-snapshot`; migrations
   must already be applied.
4. Run the reviewed image once with
   `--slice1-bootstrap-owner --username <owner> --confirm-username <owner>`.
   `bingo-deploy` supplies `Slice1__BootstrapOwnerPassword` only to this
   one-shot container from the temporary root-only password file; the persistent
   `web` service never receives it.
5. Run the read-only `--production-preflight` with the reviewed image.
6. Start `web` and `caddy`, then run the release smoke checks. Catalogue-image
   prewarming, if selected, uses the existing `--sync-catalogue-images`
   command after the application is configured.

For `interrupted`, resume the same migration/catalogue/owner path; if the owner
was already created, the host state check skips only that completed owner
command. For `completed` retained data, run the existing
`--slice1-migration-preflight` only when crossing that legacy boundary, then
`--migrate`, `--production-preflight`, and replace `web`. Never apply the
catalogue snapshot to retained data; it deactivates records absent from the
snapshot.

These commands do not touch provider resources, DNS, credentials, or
production data unless an operator runs them against that environment.

The deployment rejects a `new` marker when the application database already
has non-empty migration history, before catalogue or bootstrap mutation. Stop
and reconcile the recorded marker and database state; do not reset either one
to guess which state is authoritative.

A full restore of a correctly bound `none`/`none` baseline restores the database,
configuration, Data Protection keys, and bootstrap marker but deliberately
leaves `web` stopped. Its recovery receipt requires a separately validated
candidate deployment retry; no Super Admin is created during that restore.

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
private network, five explicitly named physical volumes, public ports only on Caddy, and no
PostgreSQL host-port mapping. Pass 4 is the explicit deployment boundary: only
after backup, isolated restore verification, rollback rehearsal, and host
bootstrap exist may an operator replace the dummy GHCR digest with the reviewed
published digest, supply real host-side secret values, run controlled
migrations, and mutate the VPS.
