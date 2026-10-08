# G5 finalized-roster role-add evidence

This record is scoped to G5 (TD-4). It contains only sanitized controlled
fixture and test evidence; no production identifiers, participant data,
provider calls, or user database access are included.

## Baseline and implementation

- G4 predecessor: `1b5a139`.
- Finalized-roster Add now accepts Participant, Captain, and Co-captain and
  rejects every other enum value before opening its transaction.
- The selected role is stored on the new membership before the existing single
  finalized-roster publication and audit are written, so the new publication
  exposes the requested role atomically with the add.
- The finalized Add form now includes a role selector defaulting to Participant
  with Captain and Co-captain options. Existing account, Playing-account,
  EHB, team-version, pre-Live and no-cap rules remain unchanged.
- No schema or migration change is included.

## Controlled PostgreSQL checks

The following checks passed against an independent Testcontainers PostgreSQL
fixture:

- `FinalizedRosterAddPublishesCaptainRolesAndRejectsInvalidRoleWithoutWrites`:
  Captain and Co-captain additions each created one new publication with the
  selected membership/publication role; the public Teams page rendered the
  corresponding role labels; the finalized Add page rendered all three role
  options; an invalid role was refused before mutation and the state hash was
  unchanged.
- Existing finalized-roster Add and HTTP replacement checks passed 9/9,
  including team-version requirements, account/Playing-account selection,
  no-cap behavior, duplicate/ownership rejection, and pre-Live protections.

Build and focused checks:

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~FinalizedRosterAddPublishesCaptainRolesAndRejectsInvalidRoleWithoutWrites'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --filter 'FullyQualifiedName~FinalizedRosterAdd|FullyQualifiedName~HttpInternalReplacementValidatesReservationsAndCreatesNoPickOrPreStartActivation'
git diff --check
```

The build passed with 0 warnings and 0 errors. No provider call, production
access, user database mutation, UI integration beyond the named form control,
or other Teams/Draft scope is included. Claude's independent review remains
pending.
