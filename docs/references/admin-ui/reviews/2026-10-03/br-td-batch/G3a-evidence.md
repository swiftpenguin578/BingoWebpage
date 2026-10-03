# G3a draft structure concurrency evidence

This record is scoped to G3a (TD-1). It contains only sanitized fixture and
test evidence; no production identifiers, participant data, provider calls, or
user database access are included.

## Baseline and implementation

- Source baseline: `f2ea1cffb8f4d9c0b23dd68dcf3f6675d1bbb5d5`.
- Activation checkpoint: `14d88b0`.
- G2 predecessor: `2b7ddbc`.
- This implementation checkpoint is the local G3a commit; Claude's independent
  read-only review remains pending.
- `UpdateTeam` and `RemoveDraftTeam` now open their transaction before reading
  the event, draft, team, or affected memberships. They lock the event and
  draft rows with `FOR UPDATE`, then recheck the event lifecycle, draft state,
  team version, and inclusion boundary under those locks.
- `Start` and both direct/website `Finalize` branches use the same event and
  draft row locks. The validated included-team query also locks the rows it
  validates, so a serializable snapshot that became stale is returned through
  the existing no-write draft-conflict response rather than publishing or
  starting from an unvalidated team set.
- Start's audit continues to serialize the team set used for its validation.
  Inclusion changes and active-team removal are refused after the configured
  event start or outside draft Setup; no migration or schema change is part of
  G3a.

## Controlled PostgreSQL checks

The following checks passed against independent Testcontainers PostgreSQL
connections with an interceptor holding the actual `FOR UPDATE` boundary:

- `TeamInclusionChangeAndStartSerializeInBothOrders`: inclusion versus Start
  in both orders leaves either the change refused with Start Running, or the
  change committed with Start refusing/stopping in Setup; no Running draft has
  an unvalidated included-team set or a missing `draft.started` audit.
- `TeamInclusionChangeAndDirectFinalizeSerializeInBothOrders`: inclusion versus
  direct Finalize in both orders produces either one-team `DirectRoster` after
  the new set is accepted, or a clean Setup/no-publication serialization
  refusal. No `DirectRoster` publication has two or more active included
  teams.
- `RemovingDraftTeamAndStartSerializeInBothOrders`: removing an empty included
  team versus Start in both orders leaves the team inactive, the draft in
  Setup, and no `draft.started` audit.
- `TeamInclusionChangesAreRefusedWhileRunningAndFinalized`: inclusion changes
  leave both Running and Finalized draft structures unchanged.
- Existing `DraftAndTeamMutationsRollbackOnAuditInsertFailure` (11 theory
  cases) and the concurrent direct/website finalization checks passed after the
  shared lock boundary change.

Build and diff checks:

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false --filter 'FullyQualifiedName~TeamInclusionChangeAndStartSerializeInBothOrders|FullyQualifiedName~TeamInclusionChangeAndDirectFinalizeSerialize|FullyQualifiedName~RemovingDraftTeamAndStartSerialize|FullyQualifiedName~TeamInclusionChangesAreRefused'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false --filter 'FullyQualifiedName~DraftAndTeamMutationsRollbackOnAuditInsertFailure|FullyQualifiedName~ConcurrentDirectFinalizationHasOnePublicationWinner|FullyQualifiedName~ConcurrentFinalizationHasOneWinnerAndNoDuplicatePublicationResidue'
git diff --check
```

The focused build and all listed tests passed. No unrelated TD-8 movement race,
UI integration, or other Teams/Draft item is included.
