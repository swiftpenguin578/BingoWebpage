# H6 focused evidence

Date: 3 October 2026

H6 applies the named cleanup and recheck fixes from the active handoff. It
does not change the retired-flow behavior covered by the renamed tests.

## Changes

- Renamed the two finalized-roster tests, the retired live replacement test,
  and the two Luck review-correction tests so their names describe the
  retained or retired behavior asserted by their bodies.
- Updated the two `PublicStatsService.Luck` comments so they describe retained
  checkpoints and whole-payload fetch handling rather than removed read-time
  rebuilding behavior.
- Added an early `OnPostUpdateTeamAsync` refusal when inclusion changes but no
  draft row exists. The controlled PostgreSQL regression changes the submitted
  team name as well as inclusion and verifies that the name, inclusion,
  version, draft-row absence and audit count remain unchanged.
- Changed the manual-team Remove and Move running-draft refusal to say that
  roster changes are locked. The Add path retains its additions-specific
  wording. Added the Danish translation for the changes message. The existing
  Remove/Move message assertions were updated because the required user-facing
  wording changed; no business assertion was weakened.

## Executed checks

All commands ran from the assigned checkout on controlled PostgreSQL fixtures.

1. `git diff --check` — passed.
2. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~DraftOperationsIntegrationTests.TeamInclusionChangeWithoutDraftRowIsRefusedWithoutWrites|FullyQualifiedName~DraftOperationsIntegrationTests.ManualRosterRemoveAndMoveAreLockedWhileDraftRuns' --logger 'console;verbosity=minimal'` — **2 passed, 0 failed**. This covers N-1's no-write guard and N-2's Remove/Move refusal.
3. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~C11FinalizedRosterIntegrationTests.FinalizedRosterWithdrawalPreservesPublicationPicksNotesReservationsAndNotificationAccess|FullyQualifiedName~C11FinalizedRosterIntegrationTests.ConcurrentFinalizedRosterAddsHaveOnlyOneWinner|FullyQualifiedName~Slice9Pass92LiveWithdrawalIntegrationTests.LiveWithdrawalAndReplacementAreRetiredAndPreserveHistory' --logger 'console;verbosity=minimal'` — **3 passed, 0 failed**.
4. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~Slice10Pass102CompetitionSynchronizationTests.StatsPass4ReviewF3AdditiveApprovalDuringOutageRetainsTheEntireOldLuckUntilAcceptedFetch|FullyQualifiedName~Slice10Pass102CompetitionSynchronizationTests.StatsPass4ReviewF3RetiredCompletionCorrectionDoesNotReplaceTheRetainedCheckpoint' --logger 'console;verbosity=minimal'` — **3 passed, 0 failed** (theory cases plus the completion-correction test).
5. `dotnet build src/Bingo.Web/Bingo.Web.csproj --no-restore --configuration Release --disable-build-servers` — **succeeded, 0 warnings, 0 errors**.
6. Name/source checks found each renamed declaration and no old retired-flow
   test declaration. The old additions wording remains only in the intended
   Add handler; Remove and Move use the changes wording. The historical
   handoff and accepted-review records retain their original finding text as
   provenance.

The focused checks do not constitute a full integration-suite run or an
independent review. Claude owns the independent H6 recheck after this commit.
