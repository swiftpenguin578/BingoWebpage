# G6 current public role-label evidence

This record is scoped to G6 (TD-7). It contains sanitized controlled-fixture
and test evidence; no production identifiers, participant data, provider calls,
or user database access are included.

## Baseline and implementation

- G5 predecessor: `b9bb405`.
- `DraftModel.OnPostChangeRoleAsync` now locks the event and draft before
  deciding whether a role change is an ordinary mutation, a pre-Live
  publication correction, or a Live/Final Review publication correction.
- Live and Final Review role changes supersede the active roster publication
  and write a new snapshot using current membership roles and frozen public
  names. Earlier cycles remain retained, picks remain linked to their original
  frozen entries, and the role publication audit is written in the same
  transaction.
- A single serialization retry is used at both the role and draft-finalization
  boundaries. If the other operation wins the event lock, the retry re-reads
  the lifecycle state before applying or publishing the role. This closes the
  valid PostgreSQL serializable loser path without sleeps or time-based test
  assumptions.
- No membership add/remove path, WOM service call, schema change, or migration
  is included. A role transition remains one logical membership role change;
  the existing domain and persistence version hooks advance its concurrency
  value together.

## Controlled PostgreSQL checks

The following checks passed against independent Testcontainers PostgreSQL
fixtures:

- `HttpRoleChangeRepublishesCurrentPublicRoleDuringLiveAndFinalReview`:
  Live and Final Review role changes each made the public Teams page render the
  new Captain label, retained the prior Co-captain publication cycle, changed
  only the role on the existing membership, retained its team/participant,
  source, pick assignment and joined timestamp, and left all WOM management,
  synchronization, operation and update rows unchanged.
- `RoleChangeAndFinalizationSerializeAndPublishCurrentRoleInBothOrders`:
  independent event-lock barriers exercised role-first and finalization-first
  orderings. Both ended with a finalized draft, one active publication whose
  roster role matched the current membership, one finalized audit for the
  draft, and one role-transition audit.
- Existing pre-Live role correction, publication-failure rollback, and
  finalization checks passed 6/6 after the transaction and retry changes.

Build and focused checks:

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --configuration Release --disable-build-servers
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --disable-build-servers --filter 'FullyQualifiedName~C11FinalizedRosterIntegrationTests.HttpRoleChangeRepublishesCurrentPublicRoleDuringLiveAndFinalReview'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --disable-build-servers --filter 'FullyQualifiedName~DraftOperationsIntegrationTests.RoleChangeAndFinalizationSerializeAndPublishCurrentRoleInBothOrders'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --disable-build-servers --filter 'FullyQualifiedName~C11FinalizedRosterIntegrationTests.HttpCaptainRecoveryRepublishesRolesAndStartsWithActualPrimaryEligibility|FullyQualifiedName~C11FinalizedRosterIntegrationTests.HttpRolePublicationFailureRollsBackRoleHistoryNoticeAndCycleThenRetryWorks|FullyQualifiedName~DraftOperationsIntegrationTests.FinalizationUsesTheDerivedDraftedParticipantSetAndRejectsIncompleteDraftedRostersWithoutResidue|FullyQualifiedName~DraftOperationsIntegrationTests.ConcurrentFinalizationHasOneWinnerAndNoDuplicatePublicationResidue'
git diff --check
```

The build passed with 0 warnings and 0 errors. Focused tests passed 2/2,
1/1 and 6/6 respectively. No provider call, production access, user database
mutation, UI integration beyond the existing role form/public page, or other
Teams/Draft scope is included. Claude's independent review remains pending.
