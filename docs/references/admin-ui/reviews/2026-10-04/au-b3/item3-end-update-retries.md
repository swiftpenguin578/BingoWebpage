# AU20 item 3 — existing management operations, end update and retries

Implementation checkpoint awaiting external Claude review. Authority: [brief](supplied-brief.md) item 3 and [item 1 attribution](item1-exact-window.md#authority-and-attribution).

## Planner technical resolution, 4 October 2026

Planner `/root`, chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`, explicitly resolved the reported other-4xx question before implementation: adopt retryable 429 with maximum of scheduled backoff and provider RetryAt; stop 401/403 invalid credentials, 404 missing source, and other mapped Validation responses as explicit safe-code provider rejection. This includes `COMPETITION_START_DATE_AFTER_END_DATE`. Unknown/ambiguous outcomes must retain reconciliation and must not become definite rejection or blind resend. This is a planner technical resolution under brief section 3, not a claim of a separate user product approval.

## Implementation

- Existing management worker discovers durable pending end requests; first attempt is due immediately. ID-only/missing/invalid credentials stop without a write.
- Existing Update operation owns attempt count, phase and due time: delays 1, 2, 4, 8, 16 minutes, then 30 repeatedly. Provider RetryAt can delay further. Repeated Queue/worker passes preserve an unchanged pending operation's due time; claim also enforces it.
- Existing claims/operation/reference locks and source/roster/history guards remain. One update can be in flight; uncertain writes are reconciled through reads, never blindly resent. Pending end reconciliation remains spaced beyond the old read-attempt cap.
- Rejections retain an unmatched end and sanitized safe code; code/message credentials are redacted. Ordinary non-end retry policy is preserved.
- A matching receipt updates metadata and marks the pending end succeeded, preserving the previous cache/generation until an accepted fetch. A stale/mismatched receipt cannot claim the latest local target as applied or restore a replaced connection.
- Existing official finalization history stops fresh end dispatch and reconciliation. Item 4 owns atomic publication fallback/readback.

## Checks

- Focused PostgreSQL end management run: 13 passed, 0 failed/skipped: seven-step persisted backoff, no immediate repeat, rate-limit max due, named/other validation and unauthorized/not-found terminal classes, absent/invalid credentials, unknown read-only reconciliation, concurrent workers, plus existing Unknown Create and edited pre-live coalescing.
- Follow-up focused run after making unknown read spacing exact: 4 passed, 0 failed/skipped; covers exact reconciliation due times, publication-history cancellation, named HTTP 400 through the real client with a controlled handler, and existing pre-live coalescing.
- Release solution build passed with zero warnings/errors. Initial build exposed a duplicate generated edit in the adjacent activity entity; removed it before any tests ran.
- `git diff --check`: PASS.
- No live WOM/user-owned database calls, new page display, independent review or manual acceptance.
