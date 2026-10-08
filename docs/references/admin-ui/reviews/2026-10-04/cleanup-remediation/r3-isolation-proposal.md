# H4-2 isolated R3 rehearsal — approval record, 4 October 2026

The approach and coverage limits were approved by the user on 4 October 2026
**after Claude’s review**, as recorded in the supplied planner decisions. The
[quoted Step 0 user assignment](../au-step0/approval-record.md) directs this
attribution correction; the earlier at-writing approval claim is superseded.
Approval permits documenting the accepted procedure only. The owning procedure is
[PRODUCTION_RUNBOOK.md](../../../../../PRODUCTION_RUNBOOK.md#r-3-isolated-rehearsal-procedure--approved-4-october-2026).
The design below is retained under its original filename for provenance; it is no
longer awaiting a procedure decision. Harness/configuration/fixtures are not built
or tested and R3 is unexecuted. Claude's source recheck accepted the procedure;
Step 0 corrects the approval attribution.

## Accepted decision

The approved approach is a future, separately implemented rehearsal harness on a disposable Linux VM,
with a dedicated Compose project (`bingo-r3`), no production host mounts or Docker
socket, no `/etc/bingo`, and no live services. The harness runs the deployment's
application stages explicitly; **never invoke the host `bingo-deploy` wrapper for
this rehearsal**. This is the user-approved interpretation of the R3 full-sequence gate:
it proves restored-data/application stage compatibility, not the wrapper's host,
backup-provider, public DNS/TLS, GHCR or production-R2 integration. If exact wrapper
execution is required instead, a separately approved refactor/testable harness is
needed; that is not authorized by this cleanup.

## Why a compose-project name alone is insufficient

`deploy/host/bingo-ops-lib.sh:5` reads `/etc/bingo/production-ops.env`;
`deploy/host/bingo-deploy:34` requires `/etc/bingo` configuration and later stops web.
`compose.production.yml` has explicit production volume names and public ports.
Do not reuse either unchanged. `src/Bingo.Web/Program.cs:161-163` unconditionally
registers lifecycle, WOM synchronization and WOM management workers.
`src/Bingo.Web/Operations/ProductionOperations.cs` requires R2 in preflight and
R2 availability plus lifecycle/synchronization heartbeats for readiness.
`src/Bingo.Infrastructure/Evidence/R2EvidenceStorage.cs` supports a configured
service URL but requires HTTPS, path-style S3 and signing region `auto`.
Blocking external network alone therefore does not establish a successful preflight.

## Accepted isolation and prerequisites

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

## Accepted ordered stages and evidence

The future harness supplies explicit `docker compose --project-name bingo-r3
--file <approved isolated compose> --env-file <fixture env>` commands. Its rendered
configuration must be inspected for volumes, endpoints, network and secrets before
execution. The approval does not supply built/tested tooling or authorize execution;
concrete commands and fixture compatibility require a later assignment.

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

## Work requiring a later assignment

No production application switch is included in this approval. A new isolated Compose/harness,
local HTTPS storage/WOM fixtures, trusted test CA setup and firewall validation
must be implemented and checked under a separate explicit assignment. Image/tool
availability and local provider compatibility remain unexecuted prerequisites.
Approval of this document permits documenting the accepted procedure, not deploying,
accessing production, transferring backups or executing R3. These remain separately
authorized operator work. R3 stays pending until the approved final-candidate
rehearsal succeeds. The user has accepted the stated wrapper/provider coverage
limit; that acceptance is not production-wrapper/provider verification. Harness
implementation, backup transfer/production access, execution and deployment remain
separately unauthorized in this cleanup assignment.

## Approval attribution correction — Step 0, 4 October 2026

The prior at-writing attribution is superseded: user approval of the procedure
and coverage limits was given after Claude’s review, as recorded in the supplied
planner decisions. Authority for this correction is the [quoted current user
assignment](../au-step0/approval-record.md). No tooling or rehearsal is authorized.
