# Local rehearsal harness

`local-rehearsal.sh` restores a production PostgreSQL dump into a disposable,
egress-free Docker Compose project on this Mac and runs the exact candidate
image through the release stages. It is the release rehearsal required by
`docs/OPERATIONS.md` §9 (the VM-based procedure is not built). It never touches production,
`/etc/bingo`, production volumes, GHCR, restic or any real provider.

## Run it

Prerequisites: Docker Desktop running, the candidate commit present in this
checkout (`git fetch`), the gitignored `sixlabors.lic` in the repository root,
and the dump stored **outside the repository**.

```sh
scripts/rehearsal/local-rehearsal.sh \
  --dump ~/bingo-rehearsal-private/postgres.dump \
  --history ~/bingo-rehearsal-private/migration-history.txt \
  --candidate <full 40-character candidate SHA> \
  --work ~/bingo-rehearsal-private/run-$(date -u +%Y%m%dT%H%M%SZ)
```

- `--history` (optional, recommended) is the `migration-history.txt` from the
  same backup payload; the restored history must match it exactly.
- `--keep-db [--keep-db-port 55433]` leaves only the migrated rehearsal
  PostgreSQL running on `127.0.0.1:<port>` (no web container) and prints how to
  point a local Development app at it. Default is a full stop.
- `--cleanup --work <dir>` removes the project's containers, network and
  volumes (and the keep-db container). It never deletes the work directory.

Each stage is logged with its exit status in `<work>/stages.tsv`; the run stops
at the first failure and still writes `<work>/report.md`. Command output goes to
private logs under `<work>/logs`, never to the console.

## Stages

1. Build the candidate's production image from `git archive <SHA>` with
   `src/Bingo.Web/Dockerfile` and the `sixlabors_license_key` BuildKit secret;
   record the image ID and the OCI revision label (must equal the SHA).
2. Start the `bingo-rehearsal` project from `compose.rehearsal.yml` with a
   generated fixture env (random DB password and dummy S3 keys). The rendered
   configuration is checked first: internal network only, no published ports,
   no host network, read-only binds, no production paths or env files. Fixtures:
   `postgres:17-alpine` (same as production), an HTTPS S3-compatible fixture
   (versitygw, path-style, region `auto`) whose certificate comes from a per-run
   CA that the web container trusts via `SSL_CERT_FILE` (validation stays on),
   and a Wise Old Man refusal fixture that answers every request with 503 and
   discards it. Egress evidence: no default route and a documentation-range
   address is unreachable. The fixture bucket is created and checked with
   `HeadBucket` before any application stage.
3. Restore the dump (`pg_restore --exit-on-error --no-owner --no-privileges`)
   and record the ordered `__EFMigrationsHistory`. Fails on a mismatch with
   `--history` or on migrations the candidate does not know.
4. Gate counts on the restored baseline (aggregates only): events in
   `AwaitingFinalReview` (must be 0, stops otherwise), drop-tile EHB overrides,
   version-1 Luck checkpoints, completion corrections, published boards without
   an active finalized roster publication, future-effective account switches,
   Setup drafts with a first pick (ids kept privately), conditional and
   non-default-context source drops, banner cleanup counts (when that migration
   is pending), active Super Admins, events
   by state, and the historical import (`det-store-danske-sommerbingo-2026`
   events and their `historical_import.applied` audit rows; an event without
   its audit row stops the run). Then a template clone
   `bingo_rehearsal_premigration` is taken for reruns.
5. `--migrate` with the candidate image. The post-migration history must equal
   the candidate's migration set exactly. Records the derived EHB-override
   cleared count, the cancelled-draft restart backfill set comparison
   (`requires_fresh_order`) and the historical-import
   counts (must be unchanged).
6. `--convert-luck-checkpoints`; requires exit 0 and `Could not convert=0`;
   records the summary line and the before/after v1 counts.
7. `--production-preflight` against the HTTPS S3 fixture.
8. Start web with workers enabled; require Docker health (`--health-probe`),
   an explicit `--health-probe` exit 0, and `/health/live` and `/health/ready`
   200 from inside the network (again after 20 s). `/health/ready` includes the
   storage check and fresh lifecycle and competition-sync worker heartbeats.
   The last 400 web log lines are kept redacted.
9. Stop the project (volumes kept) and write the redacted `report.md`.

Rerun from the pre-migration copy (inside a kept project, before cleanup):
`DROP DATABASE bingo_rehearsal; CREATE DATABASE bingo_rehearsal TEMPLATE bingo_rehearsal_premigration;`
or simply run the harness again into a new work directory after `--cleanup`.

## Not covered

Accepted coverage limits (`docs/OPERATIONS.md` §9):

- No VM isolation: isolation is Docker Desktop's `internal: true` network only
  (no host firewall layer). `--keep-db` publishes PostgreSQL on loopback only.
- No real R2, Wise Old Man or Discord credentials, permissions, objects or
  connectivity; local health is not public HTTPS.
- No host `bingo-deploy`/`bingo-backup`/`bingo-restore` wrapper, Caddy, public
  DNS/TLS, restic, GHCR pull, or production Data Protection keys (fresh keys).
- No migration Down/re-Up clone check (needs the EF migration tooling; not built here).
- A dump restored from a running database is only as current as its snapshot.

## Obtaining the dump from the production host

Production access is the user's own action; this harness never connects to the
host. The scheduled/pre-deploy backup already contains exactly what is needed:
a `pg_dump --format=custom --no-owner --no-privileges` dump and its
`migration-history.txt`, inside `payload.tgz` in an encrypted restic snapshot
(see `deploy/host/bingo-backup`).

**Preferred: restic restore of the latest snapshot** (no load on the live DB):

```sh
# On the production host, as root
set -a; . /etc/bingo/restic.env; set +a
export RESTIC_PASSWORD_FILE=/etc/bingo/restic-password
restic snapshots --tag bingo-production --latest 1          # note the snapshot ID
sudo /usr/local/sbin/bingo-restore --verify --snapshot <snapshot-id>   # optional integrity check
stage=$(mktemp -d /root/bingo-rehearsal-export.XXXXXX)
restic restore <snapshot-id> --target "$stage"
payload=$(find "$stage" -type f -name payload.tgz)
tar -xzf "$payload" -C "$stage" postgres.dump migration-history.txt payload-manifest.json
sha256sum "$stage"/postgres.dump "$stage"/migration-history.txt
jq -r '.dumpSha256, .migrationHistorySha256, .runningSourceSha' "$stage"/payload-manifest.json   # must match
```

Do not extract or copy `production.env`, `production-ops.env`, `bootstrap-state`
or `data-protection-keys` from the payload; the rehearsal does not use them.

**Alternative: a fresh dump with the same command the backup uses** (read-only
on the live database):

```sh
# On the production host, as root
cd /srv/bingo
stage=$(mktemp -d /root/bingo-rehearsal-export.XXXXXX)
docker compose --project-directory /srv/bingo --env-file /etc/bingo/production.env \
  -f /srv/bingo/compose.production.yml exec -T postgres sh -c \
  'pg_dump --format=custom --no-owner --no-privileges -U "$POSTGRES_USER" -d "$POSTGRES_DB"' >"$stage/postgres.dump"
docker compose --project-directory /srv/bingo --env-file /etc/bingo/production.env \
  -f /srv/bingo/compose.production.yml exec -T postgres sh -c \
  'psql -X -A -t -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"' >"$stage/migration-history.txt"
sha256sum "$stage"/postgres.dump "$stage"/migration-history.txt
```

**Copy it to this Mac securely** (SSH/scp only, never e-mail, chat or cloud
drives), into a private directory outside every repository checkout:

```sh
mkdir -m 700 -p ~/bingo-rehearsal-private
scp <ssh-user>@<host>:/root/bingo-rehearsal-export.XXXXXX/postgres.dump \
    <ssh-user>@<host>:/root/bingo-rehearsal-export.XXXXXX/migration-history.txt \
    ~/bingo-rehearsal-private/
chmod 600 ~/bingo-rehearsal-private/*
shasum -a 256 ~/bingo-rehearsal-private/postgres.dump ~/bingo-rehearsal-private/migration-history.txt   # compare with the host
```

(If root login is disabled, `install -o <ssh-user> -m 600` the two files into
that user's home first.) Then delete the host staging directory:
`rm -rf /root/bingo-rehearsal-export.XXXXXX`.

The dump contains participant data (WOM verification codes, Discord IDs,
account data). Keep it and every `--work` directory private, never inside a
repository, and delete them (plus `--cleanup`) when the planner is done.

## Browsing the rehearsed data (`--keep-db`)

The harness prints the exact environment for a local Development app against
the kept database. It disables the Wise Old Man fake/real client and Discord so
the app makes no provider calls. Sign in with an account that exists in the
restored data (your own production login works, since its password hash was
restored). Workers still run and Development startup may write to this copy:
it is disposable and never evidence. Evidence images are not available (local
storage is empty). Remove everything with `--cleanup --work <dir>`.

## Synthetic dry run

`make-synthetic-dump.sh --image <main-state image> --work <empty dir> --out <file.dump>`
builds a synthetic production-like dump (no real data): an empty database taken
through main's deploy order (`--migrate`, `--apply-catalogue-snapshot`, owner
bootstrap), the historical import applied with fictional operator input, one
version-1 Luck checkpoint and one drop-tile EHB override. The candidate is
always built unmodified by the harness.
