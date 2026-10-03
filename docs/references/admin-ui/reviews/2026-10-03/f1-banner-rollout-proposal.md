# F1 banner retirement rollout proposal

Date: 2026-10-03

Status: proposal sent to `/root`; implementation is awaiting explicit proposal
resolution and the missing production facts below. This file records the
recommendation durably; it does not approve or implement F1.

## Recommendation

Keep the guarded migration `20260926233834_RetireEventBanners`, but split
retirement into two explicit, idempotent operator steps in the supported
`bingo-deploy` path. The existing `event_banner_retirement_keys` table is the
one-off exact-key record; retain it through the migration instead of dropping
it, so its terminal outcomes remain the audit after the legacy banner tables
are gone. This is a bounded change to the existing migration, not a new
general-purpose cleanup framework.

Before `--migrate`, while the legacy tables still exist, the preflight
bootstraps an exact `(event_id, storage_key)` row for every legacy banner
asset and cleanup row, and checks each exact key against object storage and
non-banner database references. This preflight is verification-only: it does
not delete an object. It records the verified action needed for each key and
stops nonzero on provider failure, unknown references, or incomplete evidence.
The migration then drops the legacy banner tables and event foreign key but
keeps the exact-key ledger. After the migration succeeds, the deploy command
runs the ledger cleanup while `web` is still stopped. That step records only:

- `deleted`: the exact object was deleted after the verified reference check;
- `missing`: the exact object was already absent;
- `shared-retained`: another database reference still uses the exact key, so
  the object is retained and no shared data is deleted.

Any failed deletion remains retryable in the retained ledger and returns
nonzero, so the new web is not started until every key has a terminal outcome.
An empty legacy schema follows the same idempotent path. No broad prefix
delete, unconditional ledger rewrite, or drop-before-proof shortcut is
supported.

## Retention and tradeoffs

This preserves the data model rule that required cleanup is completed before
the retirement process is considered complete. Unique banner objects are
deleted only when their exact key is proven unreferenced by non-banner data.
Missing objects are retained as an explicit historical outcome. Shared objects
remain in storage; the legacy banner reference is retired while the shared
object stays available to its other owner. Retaining the existing ledger means
the exact outcome survives the migration; the current migration does not do
this because it currently drops that table, so the migration must change before
implementation can claim durable audit.

`bingo-deploy` stops `web` before the authoritative backup and keeps it stopped
through migration, cleanup, preflight, and replacement. The ordinary database
backup does not contain object bytes, and the runbook does not assume R2
versioning or deletion protection. With the two-phase sequence, a migration
failure happens before any object deletion, so restoring the database backup
does not need to restore object bytes. If the post-migration cleanup fails, the
ledger remains in the changed schema, `web` stays stopped, and the operator
can retry the exact keys. A recovery after uncertain migration history uses the
existing full database restore boundary; it cannot restore object bytes that a
separate, already-completed cleanup had deleted. The tradeoff is an extra
operator step and retained one-off ledger rows, in exchange for a clear
object-restoration boundary and no old release running against an uncertain
schema.

## Required proof before implementation

The implementation needs controlled PostgreSQL/object-store fixture tests and a
release rehearsal from the actual production migration state, not only a
database migrated to the revision immediately before banner retirement. The
exact production object key, object existence result, and non-banner reference
result are release-time operator facts; they are not needed to implement the
bounded command against synthetic fixtures, and this batch will not make live
provider calls. The proof must cover:

1. one legacy asset with zero cleanup rows is bootstrapped and, after the
   migration, reaches `deleted` (the exact ledger row survives);
2. an already absent object reaches `missing`;
3. a shared exact key reaches `shared-retained` and the object is not deleted;
4. provider/reference failure leaves the legacy row and pending ledger intact,
   returns nonzero, and succeeds on a later retry after the fault is removed;
5. a migration failure after verification but before cleanup deletes no object;
6. the full `bingo-deploy` sequence runs verification, `--migrate`, exact-key
   cleanup, production preflight, and web replacement successfully on the
   production-shaped database;
7. a rerun is idempotent and performs no repeat deletion.

The user-provided aggregate counts (one banner asset and zero cleanup rows)
show why the current migration blocks, but they do not establish the exact
production migration history. Before the production-shaped rehearsal, obtain
that migration baseline from the release receipt/host and run the operator's
exact-key object/reference checks at release time. Do not run a live provider
call or mutate the user-owned database as part of this batch. User approval is
still required for the ledger-retention and post-migration object-deletion
policy before F1 implementation.
