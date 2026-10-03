# G3b manual-team capacity evidence

This record is scoped to G3b (TD-2). It contains only sanitized controlled
fixture and test evidence; no production identifiers, participant data,
provider calls, or user database access are included.

## Baseline and implementation

- G3a predecessor: `a8a6c24`.
- Source baseline for the batch: `f2ea1cffb8f4d9c0b23dd68dcf3f6675d1bbb5d5`.
- Capacity admission, promotion, capacity-floor validation, and waiting
  position now count every event participant. The direct/finalized-roster
  eligibility checks retain their separate manual-team exclusion, so this
  change does not turn signup administration into finalized-roster editing.
- The Admin participant capacity summary counts all Confirmed and
  WaitingList rows, including active manual-team members. Draft derivation
  continues to exclude manual-team members from the draft pool and turn plan.
- Manual-team additions require a Confirmed event participant and are refused
  while the draft is Running. No migration, schema change, new capacity
  override, or automatic promotion path was added.
- `DATA_MODEL.md` now states that manual-team membership consumes capacity but
  no draft turn. Claude's independent review remains pending.

## Controlled PostgreSQL checks

The following checks passed against independent Testcontainers PostgreSQL
fixtures:

- `InterleavedPreformedMemberKeepsWaitingRanksAndOrdinaryAdmission`: an
  existing manual-team participant keeps the event at its cap; earlier
  waiters remain ordered and waiting after another confirmed participant
  withdraws. The Admin capacity summary reports both confirmed and waiting
  manual-team-inclusive counts.
- `CapacityDoesNotChangeWhenAIncludedTeamBecomesManualOrIsRestored`: switching
  an included team to manual and back leaves two confirmed participants and no
  waiter promotion in either state.
- `ManualRosterAdditionRequiresConfirmedParticipantAndStopsWhenDraftRuns`: a
  waiter cannot be added to a manual team, a confirmed manual member remains
  outside the draft distribution, and a later manual-team addition is refused
  after the draft enters Running.
- Existing affected manual-roster journeys passed:
  `AdminCreatedInternalParticipantsReachPoolPreassignmentPicksAndFinalRoster`,
  `FinalizedPreformedEditorPreservesSourceBasedRemovalControls`,
  `DraftedSetupAssignmentRejectsAfterFirstPickAndAfterEventStartWithoutResidue`,
  and `InvalidOrFailedSelectedRoleLeavesManualRosterAdditionWithoutResidue`.
- The full `ParticipantFlowIntegrationTests` class passed (10 tests), covering
  the public signup projection and existing lifecycle/capacity behavior.

Build and focused checks:

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~InterleavedPreformedMemberKeepsWaitingRanksAndOrdinaryAdmission|FullyQualifiedName~CapacityDoesNotChangeWhenAIncludedTeamBecomesManualOrIsRestored|FullyQualifiedName~ManualRosterAdditionRequiresConfirmedParticipantAndStopsWhenDraftRuns'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~ParticipantFlowIntegrationTests'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~AdminCreatedInternalParticipantsReachPoolPreassignmentPicksAndFinalRoster|FullyQualifiedName~FinalizedPreformedEditorPreservesSourceBasedRemovalControls|FullyQualifiedName~DraftedSetupAssignmentRejectsAfterFirstPickAndAfterEventStartWithoutResidue|FullyQualifiedName~InvalidOrFailedSelectedRoleLeavesManualRosterAdditionWithoutResidue'
git diff --check
```

The build passed with 0 warnings and 0 errors. The focused run passed 3/3,
the full participant-flow class passed 10/10, and the affected manual-roster
journeys passed 4/4. No other Teams/Draft scope is included.
