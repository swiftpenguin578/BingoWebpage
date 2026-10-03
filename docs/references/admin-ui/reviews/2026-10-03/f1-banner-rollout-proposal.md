# F1 banner retirement rollout reconciliation

Date: 2026-10-03

## Decision and status

The earlier automated preflight and retained-ledger recommendation in this
file is **superseded and was not selected**. The user selected and completed a
one-time manual cleanup for the known cancelled event. This reconciliation
does not add an automated cleanup command, change the retention policy, or
change the migration and its pending-key guard.

The production facts below are user-run and sanitized. The agent did not access
production, call Cloudflare R2, delete an object, or mutate a user-owned
database.

## Sanitized manual cleanup record

The pre-cleanup exact-key checks reported one event banner reference and zero
references from evidence, team-image, or board-tile-image storage. The earlier
cleanup-row count was zero. In one targeted transaction, the user cleared the
cancelled event's banner reference, incremented the event version, and deleted
exactly one banner-asset row (`DELETE 1`). A subsequent query outside the
transaction returned zero rows from `event_banner_assets`. The user then
confirmed deletion of the exact PNG from Cloudflare R2. No event identifier,
object key, image, or participant data is recorded here.

The manual path is therefore:

1. Check the exact banner key and every known non-banner reference at release
   time. These are operator facts about the production object store and
   database, not prerequisites for implementing or testing this repository
   change.
2. In a controlled transaction, clear the matching event reference while
   advancing the event version, then delete exactly the matching banner-asset
   row. Confirm that the legacy asset and cleanup tables contain no rows before
   the retirement migration.
3. Delete the exact banner object only after the reference checks establish
   that it is no longer used by a retained non-banner source. Do not delete a
   prefix, event folder, or bucket listing.
4. Invoke the normal deployment path after the manual cleanup. The existing
   migration remains the final schema-retirement gate.

## Existing migration and recovery boundary

`20260926233834_RetireEventBanners` creates the
`event_banner_retirement_keys` table as temporary compatibility state, copies
any exact keys that still exist in the two legacy tables, and fails closed when
any copied row is pending. A successful migration drops the ledger together
with the legacy tables. Consequently, the migration does **not** preserve a
durable ledger or exact cleanup outcomes after success. The selected manual
path relies on completing the known cleanup before this migration; it does not
claim that the ledger is an audit store.

With both legacy tables empty, the migration's bootstrap inserts no ledger
rows, the existing pending-key guard passes, and the legacy tables, event
reference, and temporary ledger are removed. If an unexpected row appears,
the unchanged guard still fails closed and leaves the source schema and
temporary exact-key rows available for operator recovery. This guard is a
failure boundary, not an automated cleanup mechanism.

`bingo-deploy` stops `web` before it takes the authoritative database backup
and then runs migrations and preflight while the service remains stopped. The
manual cleanup must therefore be completed and recorded before invoking that
deployment command; “pre-migrate” does not mean before the deployment's
downtime begins. The ordinary database backup does not contain object bytes.
If exact object deletion has already completed and a later migration or deploy
step fails, restoring that database backup cannot restore the deleted PNG. The
existing runbook's recovery rule still applies to migration history and web
code, while object restoration requires a separately managed provider backup
or version; no such restoration is assumed by this batch.

## Controlled fixture proof

`BannerRetirementMigrationTests` provides the repository-side evidence without
live provider calls:

- `ManuallyCleanedSingleAssetRetiresSchemaWithoutPendingLedger` starts from
  the migration immediately before retirement, creates one banner asset with
  no cleanup row, applies the selected reference-clear/version-increment and
  exact-row delete transaction, verifies zero legacy rows, and runs the
  unchanged retirement migration.
- `EmptyDatabaseRetiresSchemaAndCanBeRehearsedDownAndUp` proves the empty
  legacy-schema path and disposable down/up shape rehearsal.
- `PopulatedDatabaseKeepsExactKeysUntilFailedCleanupRecoversAndPreservesSharedObjects`
  proves the pending guard, failed-attempt retention, terminal outcomes, and
  preservation of shared non-banner references.

These fixtures prove the migration boundary and guard behavior. They do not
prove the full host deployment sequence or production object-store behavior.
The exact production migration baseline is still missing: aggregate row
counts do not identify the release receipt or applied migration history. A
production-shaped rehearsal must start from that exact baseline and exercise
`bingo-deploy` through `--migrate`, `--production-preflight`, and web
replacement. This batch makes no live provider call and does not claim that
rehearsal or the production deploy has passed.
