# F6 event end boundary evidence

Date: 2026-10-03

Manual event ending now uses the configured `EventEndsAt` as the effective
actual end when the request arrives at or after that instant. The existing
early-end path still uses the current instant and requires a reason. In both
paths the upload cutoff is derived by `BingoEvent.EndEvent` from the effective
actual end. The transition audit records the request time separately from the
effective event time.

## Executed checks

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~LateScheduledEndUsesEffectiveInstantAndResumeThenSecondEndPreservesHistory|FullyQualifiedName~ManualEarlyEndAnchorsUploadCutoffToActualEnd'`
  — passed 2 tests against controlled PostgreSQL. The late path verifies the
  configured end, configured end plus 30 minutes, immediate cutoff closure,
  preserved resume history, and the effective transition instant. The early
  path verifies actual end at the request instant and a 30-minute cutoff.
- `dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --configuration Debug`
  — passed with 0 warnings and 0 errors.

No participant data, provider calls, user-owned database, or production access
was used. Independent source review remains assigned to Claude.
