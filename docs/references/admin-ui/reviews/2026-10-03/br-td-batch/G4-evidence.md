# G4 cancelled-draft restart evidence

This record is scoped to G4 (TD-3). It contains only sanitized controlled
fixture and test evidence; no production identifiers, participant data,
provider calls, or user database access are included.

## Baseline and implementation

- G3b predecessor: `56020b8`.
- The new `requires_fresh_order` flag distinguishes a cancelled picked
  attempt from a current attempt while retaining `FirstPickRecordedAt` and
  every historical pick and membership row.
- An eligible cancellation marks the next attempt as requiring fresh order.
  Starting that attempt clears the included-team positions, scrambling is
  allowed again, and setup preassignment remains available. The first new
  pick clears the marker while preserving the original first-pick timestamp.
- The migration adds the non-null flag with a false default and backfills
  existing Setup rows that already have a first-pick timestamp. Finalized
  drafts and untouched Setup drafts keep the marker false.
- No existing pick, undo, cancellation, publication, or finalized-history row
  is deleted or rewritten.

## Controlled PostgreSQL checks

The following checks passed against independent Testcontainers PostgreSQL
fixtures, with the migration applied through `Database.MigrateAsync()`:

- `CancelPrivateDraftReturnsToEditableSetupAndRetainsPickHistory`: the
  existing cancellation contract remains intact, including two undone picks,
  retained first-pick timestamp, retained team positions and no active pick
  memberships.
- `CancelledDraftCanRestartWithNewIncludedTeamAndRetainsPickHistory`: after
  cancellation, a newly included team receives a captain and a fresh position;
  the attempt restarts, scrambles, picks, finalizes and publishes while the
  cancelled pick remains as undone history.
- `CancelledDraftCanReplaceCaptainAndRestart`: after cancellation, the
  original captain membership is left, an owned replacement captain is added,
  and the restarted attempt finalizes with the old membership and pick history
  retained alongside the new publication.

The focused PostgreSQL run passed 3/3. The domain rule
`CancelledPickedAttemptRequestsFreshOrderWithoutClearingHistory` passed 1/1.
The retained cancellation rollback and post-pick setup-assignment regression
checks passed 3/3.
The affected integration project build passed with 0 warnings and 0 errors.

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~CancelPrivateDraftReturnsToEditableSetupAndRetainsPickHistory|FullyQualifiedName~CancelledDraftCanRestartWithNewIncludedTeamAndRetainsPickHistory|FullyQualifiedName~CancelledDraftCanReplaceCaptainAndRestart'
dotnet test tests/Bingo.Domain.Tests/Bingo.Domain.Tests.csproj --no-build --filter 'FullyQualifiedName~CancelledPickedAttemptRequestsFreshOrderWithoutClearingHistory'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~ZeroActivePickCancellationAuditFailureRollsBackTheEntireDraftMutation|FullyQualifiedName~DraftedSetupAssignmentRejectsAfterFirstPickAndAfterEventStartWithoutResidue|FullyQualifiedName~CancelPrivateDraftReturnsToEditableSetupAndRetainsPickHistory'
git diff --check
```

No UI integration, production rehearsal, provider call, user database access,
or other Teams/Draft scope is included. Claude's independent review remains
pending.
