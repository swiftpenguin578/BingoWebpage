# F7 participant mutation version evidence

Date: 2026-10-03

The eight new Participants mutation boundaries now reject missing applicable
baselines before opening a mutation transaction: saved participant Add requires
the event version; primary and event-account operations require the participant
response version; Confirm and Move require both event and response versions;
Restore requires both as well. Matching versions are rechecked while the event
and participant rows are locked. The existing Participant Restore page now
posts its observed event and response versions. Retained `RestoreAsync`
overloads remain source-compatible by observing versions at invocation before
delegating to the guarded request boundary.

## Executed checks

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~Slice4ParticipantLifecycleIntegrationTests|FullyQualifiedName~DraftOperationsIntegrationTests.DraftStartInterleavesWithSelectedCapacityAndAccountMutationAtTheRealBoundary'`
  — passed 24 tests against controlled PostgreSQL. This includes missing
  baseline rejection for all eight operations with no audit or tracked writes,
  the stale Restore page post, existing participant lifecycle coverage, and
  updated callers carrying observed versions.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~DraftStartInterleavesWithSelectedCapacityAndAccountMutationAtTheRealBoundary'`
  — passed 1 test against controlled PostgreSQL for the draft boundary callers.
- `dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --configuration Debug`
  — passed with 0 warnings and 0 errors before the final focused test run; the
  focused test run also rebuilt the changed projects successfully.

Other nullable expected-version sites found in the same service remain outside
F7: live withdrawal membership (`LiveWithdrawalRequest.ExpectedMembershipVersion`)
and vacancy/replacement operations (`ExpectedVacancyVersion`). They are not
new Participants operations in this batch and were left unchanged.

No participant data, provider calls, user-owned database, or production access
was used. Independent source review remains assigned to Claude.
