# Review26 follow-up 1 — late remote apply

User-authorized two-commit follow-up; this is commit1/R1. Clean baseline `45a09ad0b2aa982c8603cd116a6e5d9bfbfa89c6` reverified on `codex/participants-functionality` in the assigned worktree. Source: supplied [review26 Fixes](26-b3-remediation-recheck.md), [26a R1](26a-b3-remediation-items-1-2-5.md), copied verbatim from `/Users/christopher/Documents/BingoWebpage/review-notes/`. Reviewer findings are source-only, without execution. 26a's separate invalid-read R2 is deferred, not this assignment's R2.

`CheckManagedSourceAsync` now checks the dispatched payload against a successful read before the old-state drift check. Competition identity must match; title/window and, when included, roster must match. A confirmed target completes through the existing retryable `CompleteUpdateAsync` receipt transaction without a PUT. Existing connection, operation-phase, current target/lifecycle and first-publication fences are retained. Genuine drift remains Conflict/Rejected. No provider classification or UI changes.

Executed by the implementer with isolated PostgreSQL17 Testcontainers, controlled HTTP handler through the real `WiseOldManCompetitionManagementClient`, and deterministic clock. No live WOM or user database. The new late-apply proof executes timeout → immediate old-end read → remote state changes → next due pre-write read, then asserts one PUT total and durable Succeeded/Active; the drift variant asserts one PUT and durable Failed/Rejected/Conflict. Two roster variants prove full roster required for read-based completion. One initial compile failed because the new fixture used a nonexistent participant property; corrected to the existing participant record before successful execution.

Commands (assigned worktree):

```sh
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20Followup|FullyQualifiedName~Au20UnknownEndUpdate|FullyQualifiedName~Au20RemediationRealHttpRoster' --logger 'console;verbosity=minimal'
# PASS 6/6, 19 s: four new cases, existing Unknown reconciliation, real-client roster receipt.
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20OfficialHistoryStopsADueEndRetry|FullyQualifiedName~Au20ConcurrentEndWorkersHaveOnlyOneProviderWriteInFlight' --logger 'console;verbosity=minimal'
# PASS 2/2, 9 s: publication stop and one concurrent write.
dotnet build Bingo.slnx --configuration Release --no-restore
# PASS, zero warnings/errors.
git diff --check
# PASS.
```

Unaffected prior whole-AU20/Stats63/63 and compatibility2/2 evidence is reused, not rerun or claimed as a new broad-suite result. R2 remains assigned next. Independent acceptance awaits Claude direct recheck; no self-review or new workers.
