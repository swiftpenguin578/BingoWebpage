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
   `/etc/bingo` root-owned and mode `0700`. Copy the three `deploy/host/*.example`
   templates to `/etc/bingo/`, replace placeholders, and make every secret file
   `root:root` mode `0600`: `production.env`, `production-ops.env`,
   `restic.env`, `restic-password`, and `ghcr-readonly.env`.
3. Configure the selected S3-compatible restic repository and initialize it
   interactively. Store only its repository URL, access credentials, and
   password file on the host. Restic encryption is mandatory. The repository
   must be off-host; no provider is implied by this repository.
4. Install `deploy/host/bingo-ops-validation.sh` and
   `deploy/host/bingo-ops-lib.sh` as root-owned files under
   `/usr/local/libexec/` (mode `0750`), with the validation helper beside the
   library. Install `bingo-deploy`,
   `bingo-backup`, `bingo-restore`, and `bingo-verify-evidence` under
   `/usr/local/sbin/` as root-owned mode `0750`. Install the systemd unit and
   timer from `deploy/systemd/`, then enable `bingo-backup.timer` and verify
   the next run with `systemctl list-timers`.
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

After a reboot, Docker and the backup timer must be enabled and the named
volumes must remain present. Check `systemctl is-active docker` and
`systemctl is-enabled bingo-backup.timer` before any deployment.

## Repeat deployment

The existing `production-promotion.yml` has two modes. `promote` validates a
successful `main` CI run, source SHA, candidate artifact, and immutable digest,
then records the existing non-mutating promotion receipt. `deploy` performs
the same validation before entering the `production` Environment, receives
exactly one Environment approval, and uses workflow concurrency `production`
with cancellation disabled. The deploy job transports only host, port, deploy
user, pinned known-hosts, private SSH key, and validated release metadata.

The remote command is the root-owned `bingo-deploy` script. It fails closed on
bad IDs/digests, missing or broad permissions, unavailable Docker/Compose,
missing volumes, malformed configuration, backup/restore failure, migration
or preflight failure, host-key failure, public HTTPS failure, and any remote
command failure. It always uses the full image reference
`ghcr.io/<owner>/<repo>@sha256:<digest>`; a mutable tag is rejected.

For every deploy it:

1. Validates Compose and all five persistent volumes, then starts only
   PostgreSQL if necessary and creates a PostgreSQL custom-format dump.
2. Copies Data Protection keys, `production.env`, and the root-only
   non-credential operational configuration `production-ops.env` into a
   restrictive temporary payload, checksums the complete set, encrypts it
   off-host with restic, records the immutable snapshot ID and checksums, and
   runs an isolated restore verification before changing the application. The
   temporary dump, tar, manifest, and restore material are removed on both
   success and failure; no plaintext payload path is a durable reference.
3. On an empty database, runs `--migrate`,
   `--apply-catalogue-snapshot`, the selected-owner
   `--slice1-bootstrap-owner`, and `--production-preflight`. On retained data,
   it runs `--slice1-migration-preflight --slice1-owner` only when
   `BINGO_RETAINED_LEGACY_PREFLIGHT=required`, then runs `--migrate` and
   `--production-preflight`. The existing preflight output identifies the
   deterministic bad records and the correction target; no extra application
   diagnostic is added here. Never apply the catalogue snapshot to retained
   data.
4. Replaces only `web` with `docker compose up -d --no-deps web`; Caddy and
   PostgreSQL are preserved. It waits for the internal container health check
   and verifies public HTTPS `/health/live`.
5. Writes one root-only deployment receipt under
   `/var/lib/bingo/deployments/`. Successful deployments write exactly one
   success receipt. Any failure after a verified backup snapshot exists writes
   one durable failure receipt before returning the original nonzero status;
   pre-backup failures do not fabricate one. Receipts contain only source SHA,
   image digest, CI/promotion/deployment IDs, available pre/post migration
   histories, backup snapshot/checksums/restic reference, known health results,
   actor, honest timestamps, failure stage, and an explicit rollback outcome.

## Backup and restore

Scheduled and pre-deployment backups use `bingo-backup`. They contain the
custom-format PostgreSQL dump, Data Protection keys, `production.env`, and
`production-ops.env` in one encrypted restic snapshot. The durable local
receipt references only `restic:snapshot:<snapshot-id>` plus checksums and
metadata; restic retention is the only payload retention mechanism. Evidence
objects are not copied into the VPS backup: they remain in versioned private
object storage and are checked separately.

Run an isolated restore rehearsal with:

```sh
sudo /usr/local/sbin/bingo-restore --verify --snapshot <snapshot-id>
```

It restores through restic, requires the local receipt matching that exact
snapshot, verifies the dump, `production.env`, `production-ops.env`, Data
Protection checksums, and payload manifest as one set, loads the dump into a
temporary PostgreSQL container with networking disabled, and runs a query. It
does not touch production. A production disaster restore is an explicit
maintenance operation. After preserving the current deployment receipt and
stopping writes, an operator may install the verified application and
operational configuration only with explicit snapshot confirmation:

```sh
sudo /usr/local/sbin/bingo-restore --verify --snapshot <snapshot-id> \
  --restore-config --confirm-restore <snapshot-id>
```

That install writes only `/etc/bingo/production.env` and
`/etc/bingo/production-ops.env`, both as `root:root` mode `0600`. Restore
PostgreSQL/Data Protection/configuration from this same verified snapshot, run
the reviewed prior-digest preflight, restart only after preflight passes, and
record a new receipt. Do not bulk-delete object-storage evidence during
rollback.

The following host credentials are intentionally re-provisioned rather than
restored from the payload: the restic repository URL/access credentials and
password, the GHCR pull credential, SSH host/deploy keys and known-host data,
and any provider credentials not contained in `production.env` (including
operator-managed infrastructure or object-storage credentials). This avoids
requiring credentials from the backup in order to access the backup.

## Rollback boundary

If failure occurs before web replacement, the old web remains in place. If web
replacement fails and pre/post migration histories are identical, the script
may restore the prior immutable web digest. If migration history changed, it
never auto-rolls back the image: it stops `web` to stop writes and fails with
the explicit restore requirement above. Operators must restore PostgreSQL,
Data Protection keys, and production configuration, then run the prior digest,
preflight, restart, and receipt steps explicitly.

## Evidence integrity

With the host-side R2 credentials configured, run:

```sh
sudo /usr/local/sbin/bingo-verify-evidence
```

The script queries only database-stored object keys and SHA-256 values from
the evidence/banner/team/board asset tables, downloads each object through the
configured S3-compatible endpoint, hashes it locally, and fails on any
mismatch. It never deletes objects or prints credentials/personal data.
