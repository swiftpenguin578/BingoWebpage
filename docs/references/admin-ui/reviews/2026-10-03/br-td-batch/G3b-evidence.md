# G3b manual-team capacity evidence

This record is scoped to G3b (TD-2). It contains only sanitized controlled
fixture and test evidence; no production identifiers, participant data,
provider calls, or user database access are included.

## Baseline and implementation

- G3a predecessor: `93d75f7`.
- G1 predecessor: `3c2a0a6`; G2 predecessor: `a4a640c`.
- Source baseline for the batch: `059faf5ba904b4a35c54eca4021fa306a2ea0586`.
- Manual-team Remove and Move now use the same running-draft refusal message
  as manual Add, so a Running draft cannot change the manual roster pool.
- Schedule capacity-floor validation compares the requested cap with the
  persisted cap. A lower requested cap is rejected only when the cap changes;
  an unchanged legacy over-cap event can still save an unrelated schedule or
  Live-end change. No migration, capacity override, or automatic promotion
  path was added.
- Claude's named independent recheck remains pending.

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
- `ManualRosterRemoveAndMoveAreLockedWhileDraftRuns`: both manual membership
  mutations return the Add gate message while Running and leave the original
  membership, replacement history, and audit rows unchanged.
- `LiveEndChangeAllowsUnchangedLegacyCapacityBelowConfirmedCount`: a controlled
  Live event with two Confirmed participants and persisted cap 1 accepts an
  end-time change while posting the unchanged cap; the cap remains 1.
- Existing affected manual-roster journeys passed:
  `AdminCreatedInternalParticipantsReachPoolPreassignmentPicksAndFinalRoster`,
  `FinalizedPreformedEditorPreservesSourceBasedRemovalControls`,
  `DraftedSetupAssignmentRejectsAfterFirstPickAndAfterEventStartWithoutResidue`,
  and `InvalidOrFailedSelectedRoleLeavesManualRosterAdditionWithoutResidue`.
- The full `ParticipantFlowIntegrationTests` class passed (10 tests), covering
  the public signup projection and existing lifecycle/capacity behavior.

Build and focused PostgreSQL checks:

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --configuration Release --disable-build-servers
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~ManualRosterRemoveAndMoveAreLockedWhileDraftRuns|FullyQualifiedName~ManualRosterAdditionRequiresConfirmedParticipantAndStopsWhenDraftRuns|FullyQualifiedName~LiveEndChangeAllowsUnchangedLegacyCapacityBelowConfirmedCount'
git diff --check
```

The Release build passed with 0 warnings and 0 errors, and the focused run
passed 3/3. The required full `DraftOperationsIntegrationTests` and
`SubmissionWorkflowTests` runs remain pending after this commit. No other
Teams/Draft scope is included.
