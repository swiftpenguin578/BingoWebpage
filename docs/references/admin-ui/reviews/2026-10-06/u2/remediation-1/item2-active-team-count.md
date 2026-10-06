# U2 brief70 remediation item2 — active team count (69a M1)

6 October 2026. Implementer evidence, awaiting independent review.
Predecessor item1:`50df2da759270c657f3ef1018d0fdd7f8b33fd7c`.

The U2-1 count now filters `Team.Active` before grouping by EventId. Bulk reads
and retained memberships/population/evidence rules are unchanged: filtering the
whole team read would unnecessarily change those other projections. No deletion,
history rewrite, provider request or compatibility loosening.

New real PostgreSQL case `RemovedInactiveTeamDoesNotCountInRecapHistoryOrChart`
persists six teams, removes one via `SetActive(false)`, independently verifies
six retained rows/five active, then asserts5 on recap, history and chart. It
would return6 before this edit. Empty active teams remain counted (69a observation
was not an assigned behavior change). Existing cases/expectations untouched.

## Checks / files

`dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~U2DashboardIntegrationTests|FullyQualifiedName~AdminDashboard' --logger 'trx;LogFileName=u2-remediation-item2.trx' -v:minimal`:
**24 passed,0 failed,0 skipped**,7s. Includes removed-Confirmed strict fingerprint,
mixed import headline, provisional/tie/latest/card date and all existing Dashboard
service tests. TRX:`tests/Bingo.IntegrationTests/TestResults/u2-remediation-item2.trx`
in the assigned checkout. `git diff --check`:exit0.

Changed:`src/Bingo.Infrastructure/Dashboard/AdminDashboardService.cs`,
`tests/Bingo.IntegrationTests/U2DashboardIntegrationTests.cs`, this evidence.
Commit item2 locally, then item3. Final whole .NET and UI batch gates pending;
no independent pass or visual acceptance claimed. No push.
