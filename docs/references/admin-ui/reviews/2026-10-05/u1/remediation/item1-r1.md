# Item1 — R1 classification expectation

Authority: brief50 item1, D17, brief43 item7.8. Only test change:
`Identity|true|false|…` → `Identity|true|true|…`; all handler/gate assertions stay.
Production already allowed terminal Identity reads. This corrects the missed test
expectation; it does not change the production contract.

Executed: `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AdminEventHandlerClassificationTests --results-directory /private/tmp/bingo-u1-remediation-item1 --logger trx`: **18 passed / 0 failed / 0 skipped**.
`git diff --check` clean. No PostgreSQL effect in this test-only correction.

Baseline provenance correction: Claude's whole suite on ecbb947 completed with
**2021 passed / 1 failed / 0 skipped**, exit1: Application118, Domain265,
Browser149, Integration1489 passed/1 failed (this R1 test). It did not pass.
The final remediation whole-suite gate remains pending user/Claude execution;
CURRENT_STATUS is reconciled in item10 as assigned. Review49 is FAIL; no recheck
or visual acceptance is claimed.
