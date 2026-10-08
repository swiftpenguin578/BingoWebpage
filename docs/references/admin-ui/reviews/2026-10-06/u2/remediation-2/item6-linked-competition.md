# U2 brief72 item6 — stored link and missing bindings

Parent: bd0254a7bfaf8fff93b06d7df7e3478df997d6f3. Implementer evidence only.

- Added two real PostgreSQL theory cases, linked and unlinked. Fresh contexts
  read persisted synchronization CompetitionId and authenticated Dashboard history.
  A Confirmed Playing assignment is expected in each; a linked activity with an
  incompatible fingerprint deliberately remains unavailable, matched0/gainnull.
  HasLinkedCompetition reflects the stored link independently of strict EHB
  compatibility. No production behavior, assertions or schema changed.
- Added five DELIVERY_PLAN bindings for the already-kept linked-but-incompatible
  EHB, unknown platform approval, unknown actual interval hints, unavailable
  participant chart accessibility label and Live approved-submission hint ending.
  Item1 Events in-page binding already exists; inspected, not duplicated.
- Command: dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj
  --configuration Release --no-restore --filter FullyQualifiedName~U2DashboardIntegrationTests
  --logger 'trx;LogFileName=u2-rem2-item6.trx' -v:minimal.
  **10 passed / 0 failed / 0 skipped**, reported7s. No build warnings/errors.
  TRX: tests/Bingo.IntegrationTests/TestResults/u2-rem2-item6.trx.
- git diff --check exit0; the production/frozen CSS files are outside this diff.

Items7–8/final gates not run here. Dashboard/Events acceptance remains awaiting
Claude review, then user visual acceptance. Next: item7 importer/overlap seed.
No push/merge/deploy.
