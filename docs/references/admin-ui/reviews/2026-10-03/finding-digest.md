# Findings digest — F1, F2 and F3

This compact evidence index preserves the actionable findings used for the first
implementation items. It is derived from the read-only source reports and does not
replace the full reports.

## F1 / X-1 — banner retirement deployment blocker

`20260926233834_RetireEventBanners` raises `55000` while any temporary retirement
ledger row is pending. LuckCheckpointV2, event-creation operations and signup-question
operations follow it, while `--migrate` runs all pending migrations and
`--production-preflight` rejects any remaining migration. The existing runbook says
the release may run with legacy tables present and asks operators to rerun the same
migration after cleanup, which is inconsistent with this release's preflight gate.
The required proof must begin from the actual production migration state and execute
the complete deploy sequence with the known one-asset/zero-cleanup shape.

## F2 / LK-1 — incomplete Luck snapshots

`PublishCheckpointAfterAcceptedFetchAsync` fences a changed incomplete batch whenever
any compatible snapshot exists, without requiring that the existing snapshot be
complete. The approved rule retains a prior compatible complete snapshot as a whole;
when no such snapshot exists, a newer incomplete batch can replace the older
incomplete batch. The existing partial → complete → partial retention behavior must
remain intact.

## F3 / LK-2 — version-1 conversion

The existing conversion helpers are only test-called, reads accept only v2, and
archived events do not fetch again. The approved remedy is an explicit deployment
operation over all v1 rows with per-event outcomes: Converted, Already converted, or
Could not convert with a specific reason. A conversion failure must be visible as a
non-success outcome and preserve the original row byte-for-byte.
