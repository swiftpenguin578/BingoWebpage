# F8 AU13 manual team estimate evidence

Date: 2026-10-03

The Admin Board team-size control now remains available after a finalized draft
and active roster publication. The handler still enforces the 1–100 bound,
board editing claim/version and existing authorization/audit transaction path.
Board statistics and team workload projections use the manually maintained
`ExpectedTeamSize` in every lifecycle state. Frozen roster counts remain
available as actual roster context and do not replace that planning estimate.

## Executed checks

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~FinalizedBoardStatisticsUseFrozenRosterAfterMembershipChanges|FullyQualifiedName~FinalizedBoardKeepsManualEstimateForUnequalRostersAndAllowsCorrection|FullyQualifiedName~TeamSizeChangeSucceedsWithTheRenderedBoardVersion' --logger 'console;verbosity=minimal'`
  — passed 3 tests against controlled PostgreSQL. Coverage includes an
  unequal frozen roster (2 and 3 participants) while the planning estimate is
  4, followed by a correction to 6 after roster publication.
- `dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --no-restore --filter 'FullyQualifiedName~BoardEditingUiTests.BoardOpensInEditorWithoutExplicitReleaseAction' --logger 'console;verbosity=minimal'`
  — passed the scoped board markup contract.
- `git diff --check` — passed.

No competitive roster, draft, scoring or ranking behavior was changed. No
participant data, provider calls, user-owned database or production access was
used. Independent source review remains assigned to Claude.
