# Production runbook

This runbook is the repository-side Pass 4 contract for one Ubuntu 24.04 VPS.
It does not select a VPS, object-storage provider, GitHub plan, DNS registrar,
or paid tier. Production mutation remains an operator decision.

## One-time host bootstrap

1. Apply Ubuntu security updates and reboot once. Install Docker Engine and
   the Compose v2 plugin from Docker's official Ubuntu repository, plus
   `restic`, `awscli`, `jq`, `openssh-server`, `openssl`, and `ufw`. Enable
   Docker at boot. Do not install a deployment framework.
2. Create `/srv/bingo` as a root-owned checkout containing the reviewed
   `compose.production.yml`, `deploy/Caddyfile`, and the release scripts. Keep
   `/etc/bingo` root-owned and mode `0700`. Copy the required `deploy/host/*.example`
   templates to `/etc/bingo/`, replace placeholders, and make every secret file
   `root:root` mode `0600`: `production.env`, `production-ops.env`,
   `restic.env`, `restic-password`, `ghcr-readonly.env`, and `monitoring.env`.
   The checked-in monitoring template contains only empty assignments for
   `BINGO_BACKUP_HEARTBEAT_URL` and `BINGO_DISK_HEARTBEAT_URL`; enter the real
   Better Stack heartbeat URLs only in `/etc/bingo/monitoring.env`. Before a
   clean first bootstrap, create the temporary root-only file named by
   `BINGO_BOOTSTRAP_OWNER_PASSWORD_FILE` with the final owner password. It is
   not a production configuration file and is removed after successful owner
   bootstrap; a failed or interrupted owner command leaves it for retry.
3. Configure the selected S3-compatible restic repository and initialize it
   interactively. Store only its repository URL, access credentials, and
   password file on the host. Restic encryption is mandatory. The repository
   must be off-host; no provider is implied by this repository.
4. Install `deploy/host/bingo-ops-validation.sh` and
   `deploy/host/bingo-ops-lib.sh` as root-owned files under
   `/usr/local/libexec/` (mode `0750`), with the validation helper beside the
   library. Install `bingo-deploy`, `bingo-backup`, `bingo-restore`,
   `bingo-verify-evidence`, and `bingo-check-disk` under `/usr/local/sbin/` as
   root-owned mode `0750`. Install the four systemd files from
   `deploy/systemd/` as root-owned mode `0644` under `/etc/systemd/system/`.
   The backup and disk services load `/etc/bingo/monitoring.env`; the backup
   service is the only caller that receives the daily backup heartbeat through
   that environment. Reload systemd, enable both timers, and verify them with
   `systemctl list-timers bingo-backup.timer bingo-disk-monitor.timer`.
5. Create a dedicated SSH deploy user with a key only. It must not be in the
   `docker` group. Disable password SSH and direct root SSH. Restrict UFW to
   TCP 22, 80, and 443. Record the exact host public key in the GitHub
   `PRODUCTION_SSH_KNOWN_HOSTS` secret; SSH uses strict host-key checking and
   never accepts a changed key.
6. Install `deploy/sudoers/bingo-deploy` as a root-owned mode `0440` sudoers
   entry after `visudo` validation, replacing only the deploy username. The
   deploy user may run exactly `/usr/local/sbin/bingo-deploy` as root. The
   application, PostgreSQL, R2, Discord, owner/bootstrap, restic, and
   production configuration secrets never go to GitHub Actions.
7. Configure the host-side GHCR credential as read-only for the package. Keep
   Caddy and PostgreSQL image tags as controlled host maintenance; routine app
   deployment never refreshes those mutable upstream tags. Pin and rehearse
   the host images before the first release.

8. Create `/var/lib/bingo/bootstrap-state` as a root-owned `0600` file containing
   exactly `new` before the first production backup/deploy. The deployment script
   changes it to `interrupted` before first-bootstrap work and to `completed`
   only after production preflight succeeds. Never infer this state from account
   count.

After a reboot, Docker, the backup timer, and the disk-monitor timer must be
enabled and the named volumes must remain present. Check
`systemctl is-active docker`, `systemctl is-enabled bingo-backup.timer`, and
`systemctl is-enabled bingo-disk-monitor.timer` before any deployment.

## Repeat deployment

The existing `production-promotion.yml` has two modes. `promote` validates a
successful `main` CI run, source SHA, candidate artifact, and immutable digest,
then records the existing non-mutating promotion receipt. `deploy` performs
the same validation, and the explicit manual `workflow_dispatch` selecting
`mode: deploy` is the user's production approval. No GitHub Environment,
required reviewer, or environment secret is used. The workflow uses
non-cancelling concurrency `production` and the deploy job transports only
repository-level Actions secrets `PRODUCTION_SSH_HOST`,
`PRODUCTION_SSH_PORT`, `PRODUCTION_SSH_USER`, `PRODUCTION_SSH_KNOWN_HOSTS`,
and `PRODUCTION_SSH_PRIVATE_KEY`, plus validated release metadata.

Dispatch checklist:

1. Select `production-promotion.yml` on the `main` branch; any other ref is
   rejected.
2. Supply `source_sha`, `image_digest`, and `ci_run_id` from the same successful
   `main` CI run that produced the candidate and its candidate receipt. Use the
   full source SHA, the exact `sha256:` image digest, and the numeric CI run ID.
3. Select `promote` to record a non-mutating receipt, or select `deploy` to
   start deployment; the explicit manual `deploy` dispatch is production
   approval. Candidate and input validation fails closed before either action.

For one-off Compose operator commands, explicitly set `BINGO_WEB_IMAGE` to the
currently deployed immutable image. The deploy script exports that variable
transiently, while `/etc/bingo/production.env` may still point to an older
image.

The remote command is the root-owned `bingo-deploy` script. It fails closed on
bad IDs/digests, missing or broad permissions, unavailable Docker/Compose,
missing volumes, malformed configuration, backup/restore failure, migration
or preflight failure, host-key failure, public HTTPS failure, and any remote
command failure. It always uses the full image reference
`ghcr.io/<owner>/<repo>@sha256:<digest>`; a mutable tag is rejected.

For every deploy it:

1. Captures the current running web container's immutable image and OCI source
   revision, then stops `web`. The stop remains in force through the authoritative
   pre-change backup, migration, catalogue/bootstrap, preflight, and new-web
   readiness; a short public interruption is accepted.
2. Validates Compose and all five physical volumes, starts only PostgreSQL if
   necessary, and creates a PostgreSQL custom-format dump. The backup manifest
   binds the captured running image/source, dump, migration history, Data
   Protection keys, both configs, explicit bootstrap state, and every checksum.
   Restic tags bind the backup ID and manifest checksum; the local receipt is
   secret-free corroboration, never a restore prerequisite.
3. On `new` or `interrupted` state, runs `--migrate`,
   `--apply-catalogue-snapshot`, the selected-owner
   `--slice1-bootstrap-owner`, and `--production-preflight`. An interrupted
   owner command that already succeeded is skipped only after the explicit
   active-owner check. On `completed` state, it runs the legacy migration
   preflight only when `BINGO_RETAINED_LEGACY_PREFLIGHT=required`, then
   `--migrate` and `--production-preflight`. On a completed/retained database,
   `--migrate` executes the one-time `20260916100000_PopulateRetainedCatalogue`
   operation when it is pending. That migration carries the reviewed payload,
   resolves exact item normalized-name/name and boss slug/name identities, fills
   only missing eligible mapping and pricing fields, preserves operator and frozen
   event data, and writes system-actor before/after audits. It fails atomically on
   missing or ambiguous identities. Never apply the full catalogue snapshot to
   completed/retained data. Before any catalogue/bootstrap mutation, a
   `new` marker with non-empty migration history is rejected for operator
   reconciliation; the marker is not changed. Any failure before replacement
   leaves `web` stopped; changed or unknown migration history never permits old
   code to run.
4. Replaces only `web` with `docker compose up -d --no-deps web`; Caddy and
   PostgreSQL are not pulled, refreshed, or replaced. After new web health, it
   starts Caddy only when no Caddy container exists, then verifies public HTTPS
   `/health/live`.
5. Writes one root-only deployment receipt under
   `/var/lib/bingo/deployments/`. Successful deployments write exactly one
   success receipt. Any failure after a verified backup snapshot exists writes
   one durable failure receipt before returning the original nonzero status;
   pre-backup failures do not fabricate one. Receipts contain only source SHA,
   image digest, CI/promotion/deployment IDs, available pre/post migration
   histories, backup snapshot/checksums/restic reference, known health results,
   actor, honest timestamps, failure stage, and an explicit rollback outcome.
   Post-failure history is captured when possible; `recoveryRequired` is false
   only when pre/post histories are positively verified identical.

## Backup and restore

Scheduled and pre-deployment backups use `bingo-backup`. They contain the
custom-format PostgreSQL dump, migration history, Data Protection keys,
   `production.env`, `production-ops.env`, and `bootstrap-state` in one encrypted
   restic snapshot. The temporary bootstrap password file is deliberately not
   copied into the snapshot, a receipt, a log, or Compose configuration. Every
   snapshot also records the actually running immutable web
image digest and source revision (scheduled backups inspect the running web;
deployment backups pass the identity captured before quiescence). The durable
local receipt references only `restic:snapshot:<snapshot-id>` plus checksums and
metadata; restic retention is the only payload retention mechanism. Evidence
objects are not copied into the VPS backup: they remain in private object
storage and are checked separately. This repository does not assume that R2
object versioning or deletion protection is enabled.

`bingo-backup.service` loads `/etc/bingo/monitoring.env` and invokes
`bingo-backup --reason scheduled`. Only that scheduled invocation reports the
daily heartbeat: it sends a normal heartbeat after the complete backup and
successful retention, and sends `/fail` for every other scheduled nonzero exit.
Deployment and manual backup reasons do not reset the nightly heartbeat window.
Reporting is best-effort and cannot change the backup exit status; the URL is
validated and supplied to `curl` through its config stdin, never as an argument
or log output.

Run an isolated restore rehearsal with:

```sh
sudo /usr/local/sbin/bingo-restore --verify --snapshot <snapshot-id>
```

It restores through restic, verifies the snapshot tags, dump, migration history,
`production.env`, `production-ops.env`, bootstrap state, Data Protection
checksums, and payload manifest as one set, loads the dump into a temporary
PostgreSQL container with networking disabled, and runs a query. A local receipt
is checked when present but is not required, so a rebuilt host can recover after
total loss.

The one exact full-restore path, after restic/GHCR/provider credentials are
re-provisioned, is an explicit maintenance operation. Keep writes stopped and
confirm the same snapshot ID in both arguments:

```sh
sudo /usr/local/sbin/bingo-restore --verify --snapshot <snapshot-id> \
  --full-restore --confirm-restore <snapshot-id>
```

This restores PostgreSQL exactly by dropping and recreating the configured
database through the maintenance database, verifies the restored migration
history against the backup before any application preflight, restores Data
Protection keys, both configs, and bootstrap state, then pulls the recorded
prior immutable app digest, runs production preflight, starts `web`, starts
Caddy only if absent, verifies health, and writes a recovery receipt. A
`none`/`none` baseline snapshot restores the same data/config state, leaves
`web` stopped, writes the receipt with `candidateRetryRequired: true`, and
requires a separately validated candidate deployment retry; it does not run
bootstrap or preflight. It never deletes R2 evidence objects.

The following host credentials are intentionally re-provisioned rather than
restored from the payload: the restic repository URL/access credentials and
password, the GHCR pull credential, SSH host/deploy keys and known-host data,
and any provider credentials not contained in `production.env` (including
operator-managed infrastructure or object-storage credentials). This avoids
requiring credentials from the backup in order to access the backup.

## Rollback boundary

If failure occurs before web replacement, `web` remains stopped. For every
failure after the deployment backup, the script captures post-failure migration
history when possible. If web replacement fails and pre/post migration histories
are available and identical, the script restores only the captured prior
immutable web digest. If migration history changed or either history is
unavailable, it marks recovery required, keeps `web` stopped, and requires the
explicit full restore above; old code never writes against an uncertain schema.
The original deployment failure status is preserved even when diagnostics or
receipt writing fail.

## Event-banner retirement (prepared procedure; not executed here)

BNR-01 retires the event-banner feature and its schema without assuming that a
production database or object store is empty. The migration identity is
`20260926233834_RetireEventBanners`. It must be rehearsed against disposable
PostgreSQL containers and a disposable object-store fixture before any
production decision. This task prepared and tested the procedure only; it did
not inspect, mutate, clean, or migrate production.

1. Take and verify the ordinary database/object-store backup and record the
   deployed release, migration history, and the exact target migration. Do not
   run a destructive SQL shortcut or drop either legacy banner table. The
   application release may run with the legacy tables still present because it
   no longer reads or writes them.
2. Apply the migration once. Its non-transactional bootstrap creates the
   temporary `event_banner_retirement_keys` ledger and copies the exact
   `(event_id, storage_key)` pairs from both `event_banner_assets` and
   `event_banner_cleanups`. On a populated database, the guarded migration is
   expected to stop while any ledger row is `pending`; the failed migration
   history entry is not a successful cleanup signal, and the ledger remains
   available for recovery.
3. For each ledger row, inspect exactly the recorded object key in object
   storage and cross-check every active storage-reference source, including
   `evidence_assets`, `team_image_assets`, `board_tile_image_assets`, catalogue
   image references, and any other unrelated object inventory. A key is marked
   `deleted` only after the exact banner-only object deletion succeeds. Mark it
   `missing` only after an exact object-store absence is confirmed. If any
   evidence, team, tile, catalogue, or other non-banner reference shares the
   key, do not delete the object and mark it `shared-retained` instead. A failed
   object-store operation increments `attempt_count`, records
   `last_attempted_at` and a safe `last_failure`, and leaves the row `pending`.
4. Retry failed rows from the ledger; never rediscover keys from the source
   tables and never widen a deletion to a prefix, event folder, filename, or
   bucket listing. The ledger check constraint permits only `pending`,
   `deleted`, `missing`, and `shared-retained`, and completed statuses require
   `completed_at`.
5. Re-run the same migration only after every row has a completed status. The
   successful transaction drops the banner foreign key, event reference,
   legacy asset/outbox tables, and temporary ledger together. If the guard
   fails again, the source schema and exact ledger references remain available
   for another retry. Verify the applied migration, absence of the retired
   schema, and unchanged evidence/team/tile/catalogue rows afterward.

`Down` is a disposable schema-shape rehearsal only. It recreates empty legacy
banner tables and the nullable event column; it cannot restore deleted object
bytes or claim production rollback. Production execution remains a separately
approved operator action.

## Historical Luck checkpoint conversion

The `LuckCheckpointV2` migration preserves version-1 Luck rows so they can be
converted from their retained payloads. Run the bounded conversion operation after
`--migrate` has succeeded and before `--production-preflight` or web replacement:

```sh
docker compose run --rm --no-deps web --convert-luck-checkpoints
```

The normal `bingo-deploy` sequence runs this operation in that position. It does not
fetch Wise Old Man data, recalculate from current evidence, or run during application
startup. Each event is reported exactly once with one of these outcomes:

- `Converted`: the v1 payload was validated and converted to v2 in its event-lock
  transaction, preserving the original calculation, fetch and upstream times.
- `AlreadyConverted`: the event already carries the v2 conversion marker; no write
  is performed. A rerun is therefore safe and reports already-converted events.
- `CouldNotConvert`: the original v1 row remains unchanged and the output includes
  a bounded reason. The command exits nonzero, so deployment must stop before
  preflight/web replacement. Resolve the reported payload or data issue using a
  controlled backup/rehearsal, then rerun the same command; do not relabel the row
  or use newer provider data as a shortcut.

The summary line is authoritative for the command exit decision:
`Could not convert=0` is required before continuing deployment. Production currently
has one known schema-version-1 checkpoint; that count does not establish the exact
production migration history and must be checked against the deployment receipt.

## Evidence integrity

With the host-side R2 credentials configured, run:

```sh
sudo /usr/local/sbin/bingo-verify-evidence
```

The script queries only database-stored object keys and SHA-256 values from
the evidence/team/board asset tables, downloads each object through the
configured S3-compatible endpoint, hashes it locally, and fails on any
mismatch. It never deletes objects or prints credentials/personal data.
Integrity detection is not deletion recovery. The separate decision about an
accepted R2 deletion-recovery path remains open; bucket-lock and R2-backup
design are outside this repository-side monitoring slice.

## Host monitoring

The external Better Stack resources are account-side: a public `/health/live`
monitor, a nightly backup heartbeat with a daily expectation and one-hour
grace, and a disk heartbeat with a 15-minute expectation and ten-minute grace.
Email alerting is configured in Better Stack, not in this repository.

`bingo-check-disk` checks the filesystems covering `/var/lib/docker`,
`/var/lib/bingo`, `/var/log/bingo`, `/srv/bingo`, and `/etc/bingo`, counting an
identical mount only once. Usage strictly below 85% is healthy and reports a
normal heartbeat. Usage at or above 85%, or an inspection failure, reports
`/fail` and exits nonzero. A later healthy run sends the normal heartbeat for
recovery. R2 and B2 are external and are not included in this check. If
heartbeat delivery fails, the script returns a local nonzero result without
printing the URL.

`bingo-disk-monitor.timer` uses `OnCalendar=*-*-* *:00/15:00 UTC` with
`Persistent=true`; it runs the oneshot service every 15 minutes and has no
daemon or application dependency.

Install and activate the host monitoring files with root ownership and the
following exact modes:

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

After entering the URLs in `/etc/bingo/monitoring.env`, verify the disk path
with `sudo systemctl start bingo-disk-monitor.service` and inspect only its
status with `sudo systemctl status bingo-disk-monitor.service --no-pager`.
Do not print or journal the monitoring environment file. A scheduled backup
can be observed with `sudo systemctl start bingo-backup.service` followed by
`sudo systemctl status bingo-backup.service --no-pager`; do not invoke
`bingo-backup --reason scheduled` manually, because only the systemd service
should carry the daily heartbeat environment.

The monitoring environment is intentionally not part of the encrypted backup
payload. When rebuilding or moving the VPS, re-copy the empty template,
re-enter the existing account-side heartbeat URLs, restore `root:root` mode
`0600`, reinstall the scripts and units with the modes above, then reload and
activate both timers. This is deliberate re-provisioning, not backup restore.
