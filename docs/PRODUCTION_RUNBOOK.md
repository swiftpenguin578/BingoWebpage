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

## Release-readiness checklist — 3 October 2026

These are release-gate requirements, not authorization to access production or
execute R3. Read-only production checks require separately authorized operator
access. Migration, conversion, Down/Up and rehearsal checks run on isolated
restored copies only under an approved procedure; production deployment has its
own authorization. The 3 October entries are operator-reported, not agent-verified.
Record candidate/image identity, exact migration history, timestamp and outcomes.
Before deploying `20261004093705_ClearLegacyDropTileEhbOverrides`, the authorized
operator must re-run and record this count immediately before migration:
`SELECT count(*) FROM tile_templates WHERE objective_type = 'DropRequirements' AND manual_ehb_override IS NOT NULL;`
The migration emits its actual cleared count. D1 authorizes clearing all such rows;
the user's earlier report (4 October) was 1 row at 25.0000, not a current preflight.
Approval/publication, board-tile EHB and official-result snapshots are untouched.
Down cannot restore forgotten override values; recovery requires the authorized
pre-deploy backup procedure, not guessing values. See B2 remediation decisions D1.
**H4-2 procedure and coverage limits were approved by the user on 4 October 2026 after Claude’s review, as recorded in the planner decisions; attribution corrected by the quoted Step 0 user assignment.**
See [the quoted assignment and provenance](references/admin-ui/reviews/2026-10-04/au-step0/approval-record.md).
The [accepted isolated procedure](#r-3-isolated-rehearsal-procedure--approved-4-october-2026)
below replaces the unsafe host-wrapper instruction. Never invoke host `bingo-deploy`
as rehearsal. Tooling is not built/tested and R3 is unexecuted; this documentation
approval does not authorize harness implementation, backup transfer or execution.

1. **Banner cleanup verification only (X-1/F1).** The one-time deletion was
   reported complete on 3 October. Record exact migration history first. If
   `20260926233834_RetireEventBanners` is pending and legacy schema exists, verify:

   ```sql
   SELECT COUNT(*) FROM event_banner_assets;
   SELECT COUNT(*) FROM event_banner_cleanups;
   SELECT COUNT(*) FROM events WHERE banner_asset_id IS NOT NULL;
   ```

   Require zero for both tables and event references. If the migration is already
   applied, verify its history entry and expected dropped tables/column instead;
   do not run queries against absent schema or treat that error as a zero count.
   Unexpected rows or schema/history mismatch stop the release for an operator
   decision. This checklist does **not** authorize another reference/asset/object
   deletion or any provider access from a rehearsal. The [one-time record](#event-banner-retirement-one-time-manual-cleanup-path)
   preserves the historical cleanup/recovery boundary; a DB backup contains no
   object bytes. No new cleanup result is claimed here.
2. **Luck v1 conversion (LK-2/R-1).** After `--migrate` succeeds and before
   preflight or web replacement, run the exact candidate's `--convert-luck-checkpoints` stage using the explicitly
   isolated harness after its separately authorized implementation and validation.
   The production deployment wrapper already
   invokes this stage; do not run an unqualified Compose command from this checklist.

   Require `Could not convert=0` and a zero exit status. A failed conversion
   leaves the v1 row unchanged and blocks the release; R-1 remains blocked in
   the 3 October record. Do not relabel the row or use newer provider data as
   a shortcut.
3. **All retained completion corrections (BR-7).** Count all retained rows;
   this avoids a nonexistent global review-cycle start across multiple events:

   ```sql
   SELECT COUNT(*) FROM team_completion_corrections;
   ```

   The user's earlier check reported zero on 3 October; that is not verification
   of this corrected all-rows query. Re-run and record the count. Any nonzero
   count stops the release for investigation because current readiness/ranking
   ignore retained corrections; do not rewrite history or official snapshots.
4. **Published boards without active finalized roster publication (BR-11).**
   Match `DraftPublicationQueries.ActiveRosterPublications`, not an event flag:

   ```sql
   SELECT COUNT(*)
   FROM boards b
   WHERE b.state = 'Published'
     AND NOT EXISTS (
       SELECT 1
       FROM draft_publication_cycles c
       JOIN draft_sessions d ON d.id = c.draft_session_id
       WHERE d.event_id = b.event_id
         AND d.state = 'Finalized'
         AND c.superseded_at IS NULL
         AND EXISTS (
           SELECT 1 FROM draft_publication_rosters r
           WHERE r.draft_publication_cycle_id = c.id
         )
     );
   ```

   The 3 October zero was from the user's earlier check, not this corrected query.
   It must be re-run. A Finalized draft, unsuperseded cycle and at least one roster
   row are all required; a true `team_rosters_published` flag proves none of them.
   Any nonzero result stops release for roster-lifecycle reconciliation.
5. **Future-effective account switches.** At the recorded check time, run:

   ```sql
   SELECT COUNT(*)
   FROM event_participant_character_swaps
   WHERE effective_at_utc > :check_time_utc;
   ```

   Inspect every returned row with its microsecond-precision timestamp and
   attribution dependencies. No production count is recorded in the 3 October
   handoff, so this check remains unknown until the operator runs it. Any row
   requires a deterministic decision that preserves submitted attribution and
   historical transitions; do not silently cancel, rewrite, or backdate it.
6. **G4 cancelled-draft restart migration.** On the isolated restored baseline,
   before applying `20261003184632_AllowCancelledDraftRestart`, record:

   ```sql
   SELECT COUNT(*) FROM draft_sessions
   WHERE state = 'Setup' AND first_pick_recorded_at IS NOT NULL;
   ```

   Capture the eligible row identities privately for set comparison (no participant
   data in committed evidence). After migration and before web/workers start,
   require that the exact same eligible set has `requires_fresh_order = true` and
   that its count matches; check untouched ineligible rows retain the default false.
   If the migration was already applied in the backup, report that fact and the
   current counts; do not mislabel them as a newly observed backfill.
   On a separate disposable baseline clone, verify **Down** removes only the new
   column and the matching migration-history entry without deleting draft rows or
   `first_pick_recorded_at`; then re-Up restores the column and eligible backfill.
   Resolve the exact predecessor/tooling and any later migrations in the approved
   rehearsal harness. Never downgrade production or the primary rehearsal copy.
   Counts and Down/Up execution are unknown/unexecuted here and required for R3.
7. **R-3 final-candidate rehearsal — procedure approved, execution pending.** Follow
   the accepted procedure below only after separate harness/backup-transfer/execution
   authorization. Require every isolated stage and release-gate check to pass on
   the exact final candidate. R-1 conversion failure still blocks deployment.
   Procedure approval is not a rehearsal pass or Claude independent-review PASS.

## R-3 isolated rehearsal procedure — approved 4 October 2026

The approach and its stated coverage limits were approved by the user on 4 October 2026 after Claude’s review, as recorded in the planner decisions; attribution corrected by the quoted Step 0 user assignment.
The [approval record and original design](references/admin-ui/reviews/2026-10-04/cleanup-remediation/r3-isolation-proposal.md)
retain the decision's provenance. This section owns the accepted procedure.

Use a disposable Linux VM and dedicated `bingo-r3` Compose project with an isolated
restored production database, denied external access, local WOM refusal and trusted
HTTPS S3 fixtures. Run the exact reviewed candidate's application stages explicitly.
**Never invoke the host `bingo-deploy` wrapper as rehearsal**: it reads `/etc/bingo`,
stops live web and uses production configuration/volumes. No `/etc/bingo`, live-service
or production-volume access is permitted from the rehearsal environment.

**Accepted coverage after a successful run:** restored-data compatibility, ordered migration
history, migration, Luck conversion, application preflight, worker startup and local
web health. It does **not** test the production host deployment wrapper, real WOM/R2
connectivity/credentials/permissions/objects, production restic/backup-provider or
GHCR integration, public DNS/TLS, or recovery of production data-protection keys.
Local fixtures/health cannot be reported as real-provider or public-TLS verification.
These limits were accepted, not waived by an implementer. Any later scope expansion
or application/worker switch requires its own approval.

**Execution state:** the user approved the procedure and coverage limits on
4 October 2026 after Claude’s review, as recorded in the supplied planner decisions.
Claude’s [cleanup recheck](references/admin-ui/reviews/2026-10-04/au-step0/cleanup-recheck.md)
is complete. This did not approve commit `5cf9081` after the fact.
Harness/configuration/fixtures are not built or tested and R3 is unexecuted.
Approval permits documenting this procedure only. Harness implementation, obtaining
or transferring a backup, production access, execution and deployment each require
a later explicit assignment. Do not execute this section under cleanup authority.

### Isolation and prerequisites

1. An authorized operator exports the backup and exact migration-history manifest,
   plus the final reviewed candidate image and required dependency images into the
   disposable VM using approved offline transfer. Record hashes and source/image
   identity. No production environment file, provider credentials or deployment
   keys are loaded as executable configuration. Backup contents remain restricted
   local data, never committed evidence. Preserve the original backup read-only.
2. Before restoring, deny outbound traffic from VM and containers (IPv4 and IPv6,
   DNS and host-gateway paths included). A Compose `internal: true` network is an
   additional layer, not the sole guarantee. No host network, extra external
   networks, published ports, Docker socket or production volumes. Permit only
   the dedicated project services. Prove external/provider destinations unreachable
   using an approved local deny-test target; do not test against real providers.
3. Create fresh project-scoped DB, catalogue-cache and data-protection volumes.
   Configure only fixture database credentials and fixture Discord/WOM/R2 settings.
   Restored database-held WOM verification codes stay confined by the network;
   do not expose them in command output or fixture logs. Fresh data-protection keys
   test the configured storage round-trip, not recovery of production protected data.
4. Provide local HTTPS S3-compatible storage with a fixture bucket and dummy keys,
   trusted by the exact candidate container without disabling TLS validation
   (e.g. mount the test CA trust bundle using the runtime-supported trust path).
   Verify `HeadBucket` with path-style signing region `auto` before application
   preflight. No production objects are needed for the readiness probe. Provide a
   local WOM refusal fixture returning deterministic failures for all requests and
   discarding/redacting bodies and authentication headers. No success response that
   invents provider data. This exercises worker startup/failure handling, not WOM
   synchronization correctness. Configure all other external URLs to local refusal
   endpoints or leave them behind the independent egress deny.
5. Keep workers enabled to exercise ordinary startup and readiness heartbeats.
   Lifecycle changes are confined to the expendable restored database; record
   aggregate before/after counts and preserve a pre-start database snapshot. Never
   reuse the rehearsal database for production or migration-count evidence after
   workers start. If workers fail to produce readiness under controlled provider
   failures, stop and report: do not waive health, bypass preflight, or silently add
   a worker-disable switch. Such a switch and corresponding readiness semantics
   require separate code approval and would reduce what this rehearsal proves.

### Ordered stages and evidence

The future harness supplies explicit `docker compose --project-name bingo-r3
--file <approved isolated compose> --env-file <fixture env>` commands. Its rendered
configuration must be inspected for volumes, endpoints, network and secrets before
execution. The harness is not built/tested; concrete runnable commands, fixture
compatibility, trusted CA setup and migration tooling must be delivered and checked
under a later assignment before any R3 execution.

1. Restore the transferred production dump to the isolated PostgreSQL service;
   compare the complete ordered `__EFMigrationsHistory` with the backup manifest.
   Fail on any mismatch. Take an isolated baseline backup and prove its restore to
   a second disposable DB; this substitutes a local backup/restore proof for the
   production restic wrapper/provider path, explicitly outside the accepted coverage.
2. Record gate counts on the restored baseline, including all retained completion
   corrections, published boards missing active finalized roster publications,
   future-effective switches, version-1 Luck scopes, and G4 Setup drafts with a
   non-null `first_pick_recorded_at`. Retain aggregate counts only.
3. Run the exact candidate web image with `--migrate`; capture exit status and the
   complete ordered post-migration history. Check G4's eligible rows now have
   `requires_fresh_order = true`. On a separate clone test G4 Down to its immediate
   predecessor and re-Up with the matching EF migration tooling/artifact; record
   column removal/recreation and backfill behavior. Do not downgrade the main
   rehearsal DB or an image with later migrations without the reviewed rollback
   chain. Tooling and exact target must be resolved in the approved harness.
4. On the main rehearsal DB run `--convert-luck-checkpoints`; require exit 0 and
   `Could not convert=0`, record summary counts. R1 remains blocking on failure.
5. Run `--production-preflight` with local HTTPS S3 fixture; require exit 0. This
   validates migrations, catalogue baseline, exactly one active SuperAdmin and
   local storage availability/data-protection round-trip. It proves no real R2
   credential, object, permission or availability property.
6. Start web from the same candidate/configuration. Require the native
   `--health-probe` (ready), plus `/health/live` and `/health/ready` through the
   isolated network. Record worker heartbeat health and bounded startup logs,
   aggregate data changes and egress-deny evidence. Stop on any failure. Local
   health is not public production HTTPS or external-provider verification.
7. Stop the disposable project; preserve a redacted report, image/source hashes,
   migration lists, counts, command outcomes and isolation checks. Retain/dispose
   private backup data only under operator retention authority; no automatic
   destructive cleanup of user data. Re-run after relevant final-candidate changes.

### Completion and release boundary

Retain the R-1 failure rule: a failed conversion blocks the release; never relabel
unconverted v1 data or substitute newer provider data. All final-candidate release
gates, including baseline history and G4 clone-only Down/re-Up checks, still apply.
Any failed isolation, migration, conversion, preflight, heartbeat or health check
leaves R3 failed/unexecuted as applicable and blocks deployment. Do not bypass a
failed stage or claim the production wrapper passed. Preserve the failing stage,
private restored-data checkpoint and redacted evidence for the next authorized owner.
A relevant candidate change requires a new rehearsal. Only a successful authorized
final-candidate rehearsal can satisfy R3; production deployment still requires
separate explicit user approval.

## Event-banner retirement (one-time manual cleanup path)

The user selected and completed a one-time manual cleanup for the known
cancelled event on 2026-10-03. The production details below are sanitized and
user-reported; the agent did not inspect or mutate production, call Cloudflare
R2, or delete an object. The pre-cleanup exact-key checks found one event
reference, zero evidence/team/tile-image references, and zero prior cleanup
rows. In a targeted transaction, the user cleared the event's banner reference,
advanced its version, and deleted exactly one banner-asset row (`DELETE 1`). A
query outside the transaction then returned zero `event_banner_assets` rows.
The user subsequently confirmed deletion of the exact PNG in Cloudflare R2.
No event identifier, object key, image, or participant data is recorded here.

The migration identity is `20260926233834_RetireEventBanners`. The selected
rollout does not add an automated object-cleanup command or retain a general
cleanup ledger. The sequence below records the one-time authorized cleanup path;
it is not a repeat release-checklist instruction. Any unexpected new assets need
separate operator authorization, and none of these provider steps run in R3:

1. Record the deployed release, exact migration history, target migration, and
   the exact production object/reference checks. These release-time facts are
   required for a production-shaped rehearsal; aggregate row counts do not
   establish the migration baseline.
2. Complete the targeted reference-clear/version-increment and exact asset-row
   delete in one controlled transaction. Confirm that both legacy tables have
   zero rows and that no event still references a legacy asset.
3. Delete only the exact banner object after the non-banner reference checks
   show that it is not shared. Confirm exact absence in object storage. Never
   delete a prefix, event folder, filename pattern, or bucket listing.
4. Invoke the normal deployment sequence. `bingo-deploy` stops `web` before
   taking the authoritative database backup, then runs migrations and
   `--production-preflight` while the service remains stopped. “Pre-migrate”
   therefore means before the migration command, not before deployment
   downtime begins.

The existing migration remains the schema-retirement gate. Its nontransactional
bootstrap creates temporary `event_banner_retirement_keys` state and copies any
exact keys still present in `event_banner_assets` or `event_banner_cleanups`.
With both legacy tables empty, no ledger rows are inserted, the existing
pending-key guard passes, and the event reference, legacy tables, and temporary
ledger are removed. If an unexpected row appears, the unchanged guard fails
closed with the source schema and exact temporary rows available for operator
recovery. A successful migration drops that temporary ledger, so it is not a
durable audit record and does not preserve cleanup outcomes.

The ordinary database backup does not contain object bytes. If the exact PNG
has already been deleted and a later migration or deployment step fails, a
database restore cannot restore that object. The existing recovery rule still
governs migration history and web code; object restoration requires a separate
provider backup or version, which this path does not assume. `Down` remains a
disposable schema-shape rehearsal: it recreates empty legacy banner tables and
the nullable event column, but cannot restore deleted object bytes or claim a
production rollback.

The controlled PostgreSQL proof is in
`tests/Bingo.IntegrationTests/BannerRetirementMigrationTests.cs`: it covers the
manually-cleaned one-asset shape, the empty schema path, and the unchanged
pending-key/shared-object guard. Those fixtures do not prove the full host
deployment sequence or live object-store behavior. A production-shaped
rehearsal must begin from the exact migration baseline in the release receipt
and run through `--migrate`, `--production-preflight`, and web replacement.

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
