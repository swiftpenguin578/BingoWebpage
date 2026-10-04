# AU20 item 2 — local early end / Resume and durable pending update

Implementation checkpoint; external Claude review pending. Authority: [supplied brief](supplied-brief.md) item 2 and WA-2 in the [item 1 source record](item1-exact-window.md#authority-and-attribution).

## Change

- Manual early end preserves the precise click instant as actual end/transition/upload cutoff origin. Configured end is the ceiling minute; an exact-minute click is unchanged.
- Resume requires the explicit future replacement end, after the configured start, in the existing five-minute schedule increments. It uses that instant without rounding and retains singleton, overlap, authorization, transaction and published-history checks.
- Both actions atomically save a pending end target/request timestamp for an existing WOM link. Neither calls a provider or needs a credential. Ordinary scheduled/late end behavior is unchanged.
- The existing Resume input is now always required; its existing instruction is corrected to request a future replacement, with Danish translation. No new state display or page component was added.
- WOM readback exposes the pending end state, target, request time and error code. The existing management worker executes it in item 3; this checkpoint alone does not claim provider updates, retries or publication fallback.

## Persistence

Migration `20261004112050_AddCompetitionEndUpdateState` includes migration, designer and model snapshot. It adds end-update state/target/request/error columns on the existing synchronization row, with no new service or table.

Existing rows backfill only `NotRequired`; nullable fields stay null. No historical end, published result, snapshot, cache or operation is inferred or rewritten. Down drops these new fields and therefore cannot retain a pending request across a rollback; existing event/link history remains intact.

Persisted end-update status values are explicit: `NotRequired=0`, `Pending=1`, `Succeeded=2`, `Rejected=3`, `CouldNotUpdate=4`. These are lifecycle state, separate from the unchanged AU18 skip-reason enum; item 3/4 use the planned completion/rejection/publication states.

## Execution

- Focused Release PostgreSQL filter: `Au20`, `ResumeUsesExplicitFutureEndAndRedrivesCutoff`, `ResumeRequiresReplacementAfterConfiguredEndExpires`, `StatsPass2CatalogueEditsAndResumePreserveEveryFrozenField`: 11 passed, 0 failed/skipped. The initial filter additionally named a file instead of its partial class, so current-event race cases are recorded separately below.
- Includes 21:59:55 + 7 ticks and exact 21:59:00, exact assertions at PostgreSQL microsecond precision, missing/past/nonincrement/nonaligned replacement refusal, timezone-equivalent valid replacement, provider-mismatched ID-only local success, atomic pending state, immutable transition/cutoff, and populated migration Down/Up/default backfill.
- Initial generated migration failed the repository's file-scoped-namespace analyzer before tests ran; corrected generated migration style, then all 11 executed cases passed.
- `dotnet build Bingo.slnx --configuration Release --no-restore`: PASS, zero warnings/errors.
- Current-event race recheck `RestoreAndStartOrResumeShareCurrentLock`: 4 passed, 0 failed/skipped (both lock orderings, Start and Resume).
- `git diff --check`: PASS.

## Next boundary

Item 3 must execute these requests through the existing management operations with spaced backoff and publication guards. Planner classification is required by brief item 3 for other 4xx; reported before implementation. Recommendation: preserve existing 429 retry with maximum of provider RetryAt/backoff, 404 permanent missing source, 401/403 credential rejection permanent, and other Validation as stopped explicit rejection carrying its safe code; awaiting planner resolution.

The item 3 classification question was subsequently resolved by the planner; see [item 3](item3-end-update-retries.md#planner-technical-resolution-4-october-2026).
