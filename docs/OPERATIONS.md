# Operations

Owns the production topology, deployment, release gates, backup and restore, rollback, monitoring and operational procedures. Local setup and operator commands for development are in [DEVELOPMENT.md](DEVELOPMENT.md).

## 1. Topology

### Services

- Production is a provider-neutral single-VPS deployment: the reviewed web image run with standard Docker Compose. The definition is [`compose.production.yml`](../compose.production.yml); the Caddy site is [`deploy/Caddyfile`](../deploy/Caddyfile).
- Initial VPS: Hetzner CX23-class shared vCPU or equivalent, European region, Linux LTS, 2 vCPU, 4 GB RAM, 40 GB disk (enough because screenshots live in R2). Check the current plan and price immediately before provisioning.
- `caddy` is the only public entry point: publishes TCP/UDP 80 and 443, obtains and renews HTTPS certificates for `BINGO_DOMAIN`, proxies HTTP and WebSocket/SignalR upgrades to `web:8080` with the standard `reverse_proxy` (no separate SignalR path), and compresses eligible responses with zstd or gzip.
- `web` is one ASP.NET Core replica, reachable only on the private `bingo-private` network. It takes an immutable image reference in `BINGO_WEB_IMAGE`, normally a GHCR `@sha256:` digest.
- `postgres` is PostgreSQL 17 on the private network with no published host port; `web` connects at `postgres:5432`.
- Compose sets `ASPNETCORE_ENVIRONMENT=Production`, container port 8080, trusts the proxy's forwarded HTTPS headers through the standard ASP.NET container switch, selects R2 storage and points the catalogue cache at its volume. No Development fake WOM settings or development credentials exist in production.
- Production uses the built-in JSON console logger and does not trust arbitrary external forwarded headers; Caddy is the only public proxy and overwrites forwarded headers.
- `DataProtection__KeyRingPath` is persisted and validated before the app serves. The short-lived `web-init` service makes the mounted data-protection and catalogue-cache directories writable by the non-root app user before `web` starts.

### Volumes

Five explicitly named physical volumes:

| Volume | Container path | Contents |
| --- | --- | --- |
| `bingo-production-postgres` | `/var/lib/postgresql/data` | PostgreSQL data |
| `bingo-production-data-protection` | `/var/lib/bingo/data-protection-keys` | ASP.NET data-protection keys |
| `bingo-production-catalogue-images` | `/var/lib/bingo/catalogue-images` | OSRS Wiki catalogue-image cache |
| `bingo-production-caddy-data` | `/data` | Caddy certificates and state |
| `bingo-production-caddy-config` | `/config` | Caddy runtime config |

### Configuration

- [`deploy/production.env.example`](../deploy/production.env.example) lists names only. The real file (`/etc/bingo/production.env`) stays outside source control or comes from an approved secret mechanism.
- `BINGO_DOMAIN` (public hostname) and `BINGO_WEB_IMAGE` (immutable image).
- `BINGO_POSTGRES_DB`, `BINGO_POSTGRES_USER`, `BINGO_POSTGRES_PASSWORD` are the single database authority: Compose builds the quoted app connection from them, and the host scripts use them for backup, evidence verification, migration and readiness. Keep the real password in the shell-quoted root-only env file (punctuation such as `@ # ! $ = ;` is allowed).
- `BINGO_DISCORD_CLIENT_ID`, `BINGO_DISCORD_CLIENT_SECRET`; register the callback `/Account/DiscordCallback` on the configured HTTPS domain.
- R2 evidence storage: `BINGO_R2_ACCOUNT_ID`, `BINGO_R2_ACCESS_KEY_ID`, `BINGO_R2_SECRET_ACCESS_KEY`, `BINGO_R2_BUCKET`, `BINGO_R2_SERVICE_URL`.
- Wise Old Man: `BINGO_WISE_OLD_MAN_BASE_URL`, `BINGO_WISE_OLD_MAN_USER_AGENT`, `BINGO_WISE_OLD_MAN_API_KEY` (optional), `BINGO_WISE_OLD_MAN_TIMEOUT_SECONDS`.
- `BINGO_CATALOGUE_IMAGE_SYNC_DELAY_MILLISECONDS` controls catalogue-cache sync pacing.
- `BINGO_BOOTSTRAP_OWNER_PASSWORD_FILE` (in `production-ops.env`) names a temporary root-only file used only by the one-shot initial-owner command; it is deleted after a successful bootstrap and never enters the long-running web environment.
- The repository carries only the minimal host deploy, encrypted restic backup, isolated restore verification and evidence-integrity scripts ([`deploy/host/`](../deploy/host)), root-only config examples, the sudoers entry, the systemd backup and disk-monitor units ([`deploy/systemd/`](../deploy/systemd)) and this document.

### Safe validation

Render Compose with the example file; values are placeholders and no image is pulled:

```sh
docker compose --env-file deploy/production.env.example \
  -f compose.production.yml config --quiet
docker compose --env-file deploy/production.env.example \
  -f compose.production.yml config
```

The output must show `caddy`, `web`, `postgres`, one private network, five named volumes, public ports only on Caddy and no PostgreSQL host port.

## 2. One-time host bootstrap

The procedure targets one Ubuntu 24.04 VPS. The repository selects no VPS, object-storage provider, GitHub plan, DNS registrar or paid tier; production mutation is an operator decision. Development/test accounts, generated captain credentials, events, signups, participants, teams, boards, evidence, notifications and audit are never transferred to production; retained-database migration support stays required for local upgrade testing.

1. Apply Ubuntu security updates and reboot once. Install Docker Engine and the Compose v2 plugin from Docker's official Ubuntu repository, plus `restic`, `awscli`, `jq`, `openssh-server`, `openssl`, `ufw`. Enable Docker at boot. No deployment framework.
2. Create `/srv/bingo` as a root-owned checkout with the reviewed `compose.production.yml`, `deploy/Caddyfile` and release scripts. Keep `/etc/bingo` root-owned mode `0700`. Copy the `deploy/host/*.example` templates there, replace placeholders, and make `production.env`, `production-ops.env`, `restic.env`, `restic-password`, `ghcr-readonly.env`, `monitoring.env` `root:root` mode `0600`. The checked-in monitoring template has only empty `BINGO_BACKUP_HEARTBEAT_URL` and `BINGO_DISK_HEARTBEAT_URL`; real Better Stack URLs go only in `/etc/bingo/monitoring.env`.
3. Before a clean first bootstrap, create the temporary root-only file named by `BINGO_BOOTSTRAP_OWNER_PASSWORD_FILE` containing the final owner password. It is not a config file; it is removed after a successful owner bootstrap and left in place for retry after a failed or interrupted owner command.
4. Configure an S3-compatible restic repository and initialize it interactively. Store only its URL, access credentials and password file on the host. Restic encryption is mandatory; the repository must be off-host.
5. Install `deploy/host/bingo-ops-validation.sh` and `deploy/host/bingo-ops-lib.sh` root-owned `0750` side by side under `/usr/local/libexec/`. Install `bingo-deploy`, `bingo-backup`, `bingo-restore`, `bingo-verify-evidence`, `bingo-check-disk` root-owned `0750` under `/usr/local/sbin/`. Install the four `deploy/systemd/` units root-owned `0644` under `/etc/systemd/system/` (exact commands in §7). The backup and disk services load `/etc/bingo/monitoring.env`; the backup service is the only caller that receives the daily backup heartbeat. Reload systemd, enable both timers, verify with `systemctl list-timers bingo-backup.timer bingo-disk-monitor.timer`.
6. Create a dedicated SSH deploy user, key only, not in the `docker` group. Disable password SSH and direct root SSH. UFW allows only TCP 22, 80, 443. Record the exact host public key in the GitHub secret `PRODUCTION_SSH_KNOWN_HOSTS`; SSH uses strict host-key checking and never accepts a changed key.
7. Install `deploy/sudoers/bingo-deploy` root-owned `0440` after `visudo` validation, changing only the username. The deploy user may run exactly `/usr/local/sbin/bingo-deploy` as root. Application, PostgreSQL, R2, Discord, owner/bootstrap, restic and production config secrets never go to GitHub Actions.
8. Configure the host GHCR credential as read-only for the package. Caddy and PostgreSQL image tags are controlled host maintenance: routine app deploys never refresh those upstream tags. Pin and rehearse the host images.
9. Create `/var/lib/bingo/bootstrap-state` root-owned `0600` containing exactly `new` before the first backup or deploy.

### Bootstrap state

- Startup requires exactly one active Super Admin. The root-only marker `BINGO_BOOTSTRAP_STATE_FILE` contains exactly `new`, `interrupted` or `completed` and is in every backup. A new host starts at `new`; `bingo-deploy` sets `interrupted` before first-bootstrap work and `completed` only after production preflight succeeds; `completed` means retained production even with zero accounts. Never infer the state from the account count.
- The new-database branch is schema and owner bootstrap only and leaves the catalogue empty, so it is not a supported production rebuild (preflight needs an active activity, item and drop). To rebuild production, restore a reviewed backup (§5) and then run the normal retained deployment.
- New-database steps: start only PostgreSQL and wait for health; run the image once with `--migrate`; `bingo-deploy` has no restore step and never applies `--apply-catalogue-snapshot`; for a controlled bootstrap rehearsal only, run `--slice1-bootstrap-owner --username <owner> --confirm-username <owner>` with `Slice1__BootstrapOwnerPassword` supplied from the temporary file to that one-shot container only; then the read-only `--production-preflight` must fail closed on the empty catalogue. Do not start `web`.
- `interrupted`: resume the same migration/owner path; the owner command is skipped only after the explicit active-owner check shows it already succeeded.
- `completed`: run `--slice1-migration-preflight` only when crossing that legacy boundary (`BINGO_RETAINED_LEGACY_PREFLIGHT=required`, else `skip`), then `--migrate`, `--production-preflight`, replace `web`. Never apply the catalogue snapshot to retained data: it deactivates records absent from the snapshot.
- Deployment rejects a `new` marker when the database already has non-empty migration history, before any catalogue or bootstrap mutation. Stop and reconcile; never reset the marker or the database to guess.

## 3. Release gates and deployment contract

### Principles

- Merge, build and deploy are separate: a merge changes only history; the CI build makes an immutable candidate without touching production; deploy is operator-approved and does backup, migration, pull, web replacement and health checks, with Caddy as the entry point throughout.
- Test and build run automatically after merge; production deploy needs manual approval. A merge never deploys silently. Approval triggers one documented workflow; never edit the server, toggle pages or replace files inside the container.
- The app deploys as one unit; a small CSS or copy fix follows the same image and replacement path, including the backup. With one replica a brief interruption of requests and SignalR connections is accepted; zero-downtime or multi-replica deployment is not required.
- `--migrate` is the only migration command; normal Production startup never migrates. `--production-preflight` is read-only and requires no pending migrations, usable R2, at least one active activity, item and drop, and exactly one active Super Admin. Wise Old Man never gates readiness.
- Keep the complete infrastructure and operations checklist, including optional safety items. Evaluate each as its deployment step approaches, with provider/tier options, current costs, tradeoffs, a hobby-project recommendation and the consequence of deferring it. Optional items are never silently removed.
- No external account, purchase, paid tier, credential acceptance, DNS change or production mutation without the user's explicit approval. Keep repository automation provider-portable where practical.

### Candidate publication

- Keep the existing PR/main CI job and check name. After `build-and-test` succeeds on a push to `main`, CI publishes exactly one `linux/amd64` image from the current Dockerfile to the repository-owned GHCR package. The only tag is `sha-<full-source-sha>` (trace only); the digest is authoritative.
- CI uploads a short-lived candidate receipt binding image, digest, source SHA, platform, CI run ID/URL and timestamp. The job uses job-scoped least privilege, no PAT or PR secrets, and actions pinned to full commits.

### Release checks

- Release-gate queries are run by the operator with separately authorized access; agents never access production or run them.
- Before each release the operator records the candidate SHA, the exact deployed migration history and the check timestamp, runs every read-only check, and preserves the baseline on an isolated restored copy. Mutating migration, conversion or Down checks run only on isolated copies under the rehearsal procedure (§9). Never infer live-database authority; aggregate counts do not establish the migration baseline.
- The gate is complete only when each check has a recorded candidate identity and a passing result. Record results without participant data. Never clear, transition, backfill or rewrite rows to pass a check.

| Check | Pass | If it fails |
| --- | --- | --- |
| Events awaiting Final Review, run immediately before deploy (and on any rehearsal database) | `SELECT count(*) FROM events WHERE state = 'AwaitingFinalReview';` = 0 | Stop for a planner/user decision. |
| Retained completion corrections | `SELECT COUNT(*) FROM team_completion_corrections;` = 0 (no global cycle-time parameter) | Stop for investigation; readiness and ranking ignore retained corrections; never rewrite history or snapshots. |
| Published boards without an active finalized roster publication | Query below = 0 | Stop; reconcile through the publication lifecycle (the event's `team_rosters_published` flag alone proves nothing). |
| Future-effective account switches | `SELECT COUNT(*) FROM event_participant_character_swaps WHERE effective_at_utc > :check_time_utc;` = 0; inspect any row privately with its microsecond timestamp | Stop for an operator decision that preserves submitted attribution and history; never silently cancel or backdate. |
| Luck checkpoint conversion | Exit 0 and `Could not convert=0` (§8) | Preserve v1 rows and the reason; stop before preflight and web; no provider-data shortcut. |
| Rehearsal when migrations change | §9 passes on the exact candidate | Stop. |

```sql
SELECT COUNT(*) FROM boards b
WHERE b.state = 'Published'
  AND NOT EXISTS (
    SELECT 1 FROM draft_publication_cycles c
    JOIN draft_sessions d ON d.id = c.draft_session_id
    WHERE d.event_id = b.event_id
      AND d.state = 'Finalized'
      AND c.superseded_at IS NULL
      AND EXISTS (SELECT 1 FROM draft_publication_rosters r WHERE r.draft_publication_cycle_id = c.id));
```

## 4. Repeat deployment and candidate promotion

### Before deploying

- After any reboot, Docker, the backup timer and the disk-monitor timer must be enabled and the named volumes present. Check `systemctl is-active docker`, `systemctl is-enabled bingo-backup.timer` and `systemctl is-enabled bingo-disk-monitor.timer`.
- When a release changes `deploy/host/*` scripts, reinstall the root-owned `0750` copies under `/usr/local/sbin/` (§2 step 5) before deploying; otherwise the old sequence runs.
- Pass the release checks (§3).

### Promotion workflow

- `.github/workflows/production-promotion.yml` is manual and dispatchable only from `main`. Inputs: full `source_sha`, exact `sha256:` `image_digest` and numeric `ci_run_id`, all from the same successful `main` CI run that produced the candidate receipt. It validates their syntax and that the run's candidate receipt binds them, before either mode; validation fails closed.
- `promote` records a non-mutating promotion receipt: no SSH, pull, migration, Compose operation or other mutation. `deploy`, selected by explicit manual `workflow_dispatch`, is the user's production approval.
- The workflow uses non-cancelling `concurrency: production` and transports only SSH data plus validated release metadata; native OpenSSH with strict host-key checking invokes the narrowly sudoable root-owned host command.
- Only five repository-level Actions secrets exist: `PRODUCTION_SSH_HOST`, `PRODUCTION_SSH_PORT`, `PRODUCTION_SSH_USER`, `PRODUCTION_SSH_KNOWN_HOSTS`, `PRODUCTION_SSH_PRIVATE_KEY`. App, database, R2, Discord and restic secrets stay on the VPS.
- The GHCR package and repository stay private on GitHub Free. No GitHub Environment, required reviewer or environment secret; do not change package visibility or add Environment configuration.

### What `bingo-deploy` does

The remote command fails closed on bad IDs or digests, missing or broad permissions, unavailable Docker/Compose, missing volumes, malformed config, backup/restore, migration or preflight failure, host-key failure, public HTTPS failure or any remote command failure. It always uses `ghcr.io/<owner>/<repo>@sha256:<digest>`; mutable tags are rejected. Every deploy:

1. Captures the running web container's immutable image and OCI source revision, then stops `web`. `web` stays stopped through backup, migration, owner bootstrap, preflight and new-web readiness; a short public interruption is accepted.
2. Validates Compose and all five volumes, starts only PostgreSQL if needed, and takes the backup (§5): a custom-format dump plus a manifest binding the captured image/source, dump, migration history, data-protection keys, both configs, bootstrap state and every checksum. Restic tags bind the backup ID and manifest checksum; the local receipt is secret-free corroboration, never a restore prerequisite.
3. Runs the bootstrap branch for `new`/`interrupted` (§2). For `completed`: legacy `--slice1-migration-preflight` only when `BINGO_RETAINED_LEGACY_PREFLIGHT=required`, then `--migrate`, then `--convert-luck-checkpoints` (§8), then `--production-preflight`. The catalogue snapshot loader is for CI, Development and manual-test databases only; production deployment never applies it.
4. Replaces only `web` with `docker compose up -d --no-deps web`. Caddy and PostgreSQL are never pulled, refreshed or replaced. After web health it starts Caddy only if no Caddy container exists, then verifies public HTTPS `/health/live`.
5. Writes one root-only receipt under `/var/lib/bingo/deployments/`: exactly one success receipt on success; any failure after a verified backup writes one durable failure receipt and returns the original nonzero status; pre-backup failures write none. Receipts contain only source SHA, digest, CI/promotion/deployment IDs, available pre/post migration histories, backup snapshot/checksums/restic reference, known health results, actor, honest timestamps, failure stage and rollback outcome. `recoveryRequired` is false only when pre/post histories are positively verified identical.

- Rollout of data changes always uses this normal backup and deployment path, never a reset; private inputs stay outside committed artifacts.
- Static asset URLs are versioned. Never patch the live container: fix in a branch, merge, build a new image and deploy it.

## 5. Backup and restore

### Policy

- Event data and submission metadata are backed up regularly; evidence storage is durable with a recovery plan; recovery is tested before a live event.
- Recovery targets: during an event lose at most several hours of database changes; outside events restore the latest nightly backup; restore service within a few hours given credentials and provider access.
- Implemented: nightly scheduled backup (`bingo-backup.timer`, 03:15 UTC), a backup before every deploy, encrypted off-site restic upload, restic daily/weekly/monthly retention (`BINGO_KEEP_DAILY/WEEKLY/MONTHLY`), checksum verification and isolated test restore. The policy also calls for backups every 4–6 hours during active events and event-finalization retention; neither is automated, so the operator runs extra backups during events.

### Contents

- Scheduled and pre-deploy backups use `bingo-backup`: one encrypted restic snapshot with the custom-format dump, migration history, data-protection keys, `production.env`, `production-ops.env` and `bootstrap-state`. The temporary bootstrap password file is never copied into a snapshot, receipt, log or Compose config.
- Every snapshot records the actually running web image digest and source revision (scheduled: inspect the running web; deploy: the identity captured before quiescence).
- The local receipt references only `restic:snapshot:<snapshot-id>` plus checksums and metadata; restic retention is the only payload retention mechanism.
- `monitoring.env` is not in the backup payload (see Re-provisioning).

### Evidence objects

- Evidence objects are not in the VPS backup; they stay in private object storage, separate from the VPS lifecycle, and are checked separately. The database backup plus the bucket must suffice to reconnect evidence metadata to objects.
- The repository assumes no R2 object versioning or deletion protection; the operator decides R2 deletion protection or another recovery path (still open). Integrity checks are not deletion recovery.
- `sudo /usr/local/sbin/bingo-verify-evidence` (with host R2 credentials) reads only database-stored object keys and SHA-256 values from the evidence, team and board asset tables, downloads and hashes each object, and fails on any mismatch. It never deletes objects or prints credentials or personal data.
- The only evidence inventory is `bingo-verify-evidence`'s live database check.

### Restore

Isolated restore rehearsal:

```sh
sudo /usr/local/sbin/bingo-restore --verify --snapshot <snapshot-id>
```

It verifies snapshot tags, dump, migration history, both configs, bootstrap state, data-protection checksums and manifest as one set, loads the dump into a temporary network-disabled PostgreSQL container and runs a query. A local receipt is checked if present but not required, so a rebuilt host can recover after total loss.

Full restore, an explicit maintenance operation after restic, GHCR and provider credentials are re-provisioned, with writes stopped and the same snapshot ID given twice:

```sh
sudo /usr/local/sbin/bingo-restore --verify --snapshot <snapshot-id> \
  --full-restore --confirm-restore <snapshot-id>
```

- It drops and recreates the database through the maintenance database, verifies the restored migration history against the backup before any preflight, restores data-protection keys, both configs and bootstrap state, pulls the recorded prior image digest, runs preflight, starts `web`, starts Caddy only if absent, verifies health and writes a recovery receipt.
- A `none`/`none` baseline snapshot restores data and config, leaves `web` stopped, writes the receipt with `candidateRetryRequired: true`, runs no bootstrap or preflight, and needs a separately validated candidate deployment retry. Restore never deletes R2 evidence objects.

### Re-provisioning

- Re-provisioned, not restored from the payload: restic URL, credentials and password; GHCR pull credential; SSH host and deploy keys and known hosts; provider credentials not in `production.env`.
- On rebuild or move, re-copy the empty monitoring template, re-enter the account-side heartbeat URLs, restore `root:root 0600`, reinstall the scripts and units (§7 modes), reload and activate both timers.

## 6. Rollback

- Migrations are designed for safe forward deployment; an application rollback cannot reverse a destructive migration. Changed migration history never permits automatic image rollback.
- A failure before web replacement leaves `web` stopped. After the backup, post-failure migration history is captured when possible. If web replacement fails and pre/post histories are available and identical, only the captured prior immutable digest is restored. Changed or unavailable history marks recovery required, keeps `web` stopped and requires the explicit full restore (§5); old code never writes against an uncertain schema.
- Prior-image rollback fails closed while any event has non-null `HiddenAt` (older code would re-expose hidden rows): preflight lists the affected events, and the operator restores them through a compatible app or stays on the new image. No automatic data clearing or visibility rewrite (`bingo-deploy` rollback guard).
- The original deployment failure status is preserved even when diagnostics or receipt writing fail.

## 7. Monitoring and logs

### Health and application signals

- `/health/live` is public and cheap. `/health/ready` checks PostgreSQL, configured R2 bucket reachability and fresh heartbeats from the lifecycle and competition-sync workers; WOM never affects readiness. Caddy returns 404 for `/health/ready`; Compose probes it on the private container and starts Caddy only after web is healthy.
- The app provides structured logs, the health endpoints, database and R2 checks that expose no credentials, failed-login and rate-limit events, backup status, background-service status, and review/recalculation error alerts.

### Better Stack (account-side)

- A public `/health/live` monitor; the nightly backup heartbeat with a daily expectation and 1-hour grace; the disk heartbeat with a 15-minute expectation and 10-minute grace. Email alerting is configured in Better Stack, not in the repository.

### Backup heartbeat

- `bingo-backup.service` loads `monitoring.env` and runs `bingo-backup --reason scheduled`. Only the scheduled run reports: a normal heartbeat after the complete backup and successful retention, `/fail` on any other nonzero exit. Deploy and manual backups do not reset the window. Reporting is best-effort and cannot change the backup exit status; the URL is validated and passed to `curl` through config stdin, never argv or logs.

### Disk monitor

- `bingo-check-disk` covers the filesystems for `/var/lib/docker`, `/var/lib/bingo`, `/var/log/bingo`, `/srv/bingo`, `/etc/bingo` (an identical mount counts once). Below 85% is healthy and sends the normal heartbeat; 85% or more, or an inspection failure, sends `/fail` and exits nonzero; a later healthy run recovers. R2 and B2 are excluded. If heartbeat delivery fails, it exits nonzero locally without printing the URL.
- `bingo-disk-monitor.timer`: `OnCalendar=*-*-* *:00/15:00 UTC`, `Persistent=true`, a oneshot every 15 minutes with no daemon or app dependency.

### Install and verify

```sh
sudo install -o root -g root -m 0600 deploy/host/monitoring.env.example /etc/bingo/monitoring.env
sudo install -o root -g root -m 0750 deploy/host/bingo-check-disk /usr/local/sbin/bingo-check-disk
sudo install -o root -g root -m 0644 deploy/systemd/bingo-backup.service /etc/systemd/system/bingo-backup.service
sudo install -o root -g root -m 0644 deploy/systemd/bingo-backup.timer /etc/systemd/system/bingo-backup.timer
sudo install -o root -g root -m 0644 deploy/systemd/bingo-disk-monitor.service /etc/systemd/system/bingo-disk-monitor.service
sudo install -o root -g root -m 0644 deploy/systemd/bingo-disk-monitor.timer /etc/systemd/system/bingo-disk-monitor.timer
sudo systemctl daemon-reload
sudo systemctl enable bingo-backup.timer bingo-disk-monitor.timer
sudo systemctl start bingo-backup.timer bingo-disk-monitor.timer
sudo systemctl list-timers bingo-backup.timer bingo-disk-monitor.timer
```

- After entering the URLs, verify the disk path with `sudo systemctl start bingo-disk-monitor.service` and `sudo systemctl status bingo-disk-monitor.service --no-pager`. Observe a backup with `sudo systemctl start bingo-backup.service` and `sudo systemctl status bingo-backup.service --no-pager`.
- Never print or journal the monitoring env file. Never run `bingo-backup --reason scheduled` manually; only the systemd service carries the heartbeat env.

## 8. Operational procedures

### One-off Compose commands

- Explicitly set `BINGO_WEB_IMAGE` to the currently deployed immutable image; `bingo-deploy` exports it only transiently and `/etc/bingo/production.env` may name an older image.

### Luck checkpoint conversion

- The only historical write exception: an explicit, bounded, idempotent v1→v2 Luck conversion. It rebuilds only from retained v1 checkpoint observations, rates and attribution, preserves original times, records conversion provenance, leaves scopes with missing inputs unavailable, and never fetches WOM, recalculates from evidence, applies newer evidence, rewrites placements or changes provider data.
- `LuckCheckpointV2` keeps the v1 rows. Run `docker compose run --rm --no-deps web --convert-luck-checkpoints` after `--migrate` succeeds and before `--production-preflight` and web replacement; `bingo-deploy` runs it there on every deploy. It never runs at app startup.
- Each event is reported once: `Converted` (validated and converted in the event-lock transaction, original calculation, fetch and upstream times kept); `AlreadyConverted` (marker present, no write; reruns are safe); `CouldNotConvert` (v1 row unchanged, bounded reason, nonzero exit, deployment stops before preflight and web; fix through a controlled backup/rehearsal and rerun; never relabel the row or use newer provider data).
- The summary line is authoritative: `Could not convert=0` is required before deployment continues.

### Data conversions

- Conversions touching unfinished or Live events never auto-resume Paused drafts, match accountless participants by name, cancel pending remote operations, enable disabled scheduled opening, rewrite finalized rosters or recalculate historical results. Each needs a deterministic evidence-based rule or a controlled operator decision. Waiting-list enablement never invokes legacy promote-all or expands capacity.

### Hibernation

- A powered-off Hetzner server is still billed; to stop charges, delete the server after preserving state. Server-tied automatic backups die with the server, so rely on independent off-site backups or a retained snapshot.
- Procedure: stop the web service and all mutations (there is no maintenance mode); take a final backup off-site; verify it and run or confirm a test restore; export deployment config without secrets; preserve secrets in a password manager or secret store; record the deployed image and migration version; optionally keep a protected Hetzner snapshot; confirm R2 evidence inventory and access; delete the VPS and any unused billed IPv4; keep the domain, R2 data, backups and documentation.

## 9. Rehearsal

- A rehearsal must pass on the exact final candidate before deploy when migrations change: a disposable restored database with denied external access and local WOM and HTTPS S3 fixtures, running restore with ordered history → migrate → Luck conversion → preflight → web and health. Use the local harness in [`scripts/rehearsal/`](../scripts/rehearsal) ([README](../scripts/rehearsal/README.md)). Any failure stops the release; deployment is approved separately.
- Accepted coverage limits: no host wrapper, real providers, restic or GHCR, public DNS/TLS or production data-protection keys. Local fixtures and health are never reported as real-provider or public-TLS verification.
- Never invoke the host `bingo-deploy` wrapper as a rehearsal: it reads `/etc/bingo`, stops live web and uses production config and volumes.
- A release candidate must pass a production-sized load rehearsal covering public viewing, SignalR updates, evidence uploads and viewing, and admin review.
- The VM-based rehearsal is not built; see the BACKLOG row "Full R-3 rehearsal harness" in [BACKLOG.md](../BACKLOG.md).
