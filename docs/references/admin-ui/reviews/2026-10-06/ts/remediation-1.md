# TS corrective checkpoint — original postgres template

The first whole-suite candidate at `1b237eda27556209743fbc86be268bd8b801de2b`
is **failed**, not final-SHA acceptance. Clean Release build had 0 warnings/errors
(26.62s MSBuild elapsed); run1 Domain 265/0/0, Application 118/0/0 and Browser
150/0/0 passed. Integration completed **1494 passed / 16 failed / 0 skipped**,
1510 total, wall-clock **1874.14s**. The runner stopped there; no run2 occurred.

All 16 failures are setup failures in AdminDashboardIntegrationTests (7) and
AdminDashboardRemediationIntegrationTests (9): their unchanged builders bootstrap
`postgres`, which becomes the migrated template. The helper incorrectly opened
its administrative connection in the same database before `ALTER DATABASE ...
ALLOW_CONNECTIONS false`; PostgreSQL rejected that with `22023`, “cannot disallow
connections for current database”. No test body executed in those failed cases.
This is a helper defect, not inter-test data or server-fact dependence.

The bounded corrective continuation changes only AdminConnectionString: use the
test-owned server's bootstrap `template1` when the original migrated database is
`postgres`, otherwise `postgres`. Original database builders, credentials,
template names, cloning, assertions, inputs, expected values and production code
are unchanged. Both administrative databases are created by this Testcontainers
server, not user databases. The 25 dedicated exceptions and CI remain unchanged.

## Evidence and commands

Failed candidate command is the [checked-in whole-suite runner](run-final-gates.sh).
Raw logs/TRX remain in ignored `artifacts/1b237eda27556209743fbc86be268bd8b801de2b/`.
Integration TRX ID `7b9e1bfd-0d57-4659-ae70-2e33258f1b80`; start
`2026-10-06T15:43:11.7676260+02:00`, finish
`2026-10-06T16:14:24.9051850+02:00`; TRX SHA256
`16b29a986fdf5006efaed633f724d1b891eccc9450f8aaa6a0154116d22bd0b1`,
log SHA256 `e973d95bfe4b8f39ac27965f31790180e10b3af2374e9861091841dffe1a7280`.

Corrected candidate build:
`dotnet build Bingo.slnx --configuration Release --no-restore`: exit0,
0 warnings/errors, 10.89s. Focused command:

```sh
/usr/bin/time -p dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Bingo.IntegrationTests.AdminDashboardIntegrationTests|FullyQualifiedName~Bingo.IntegrationTests.AdminDashboardRemediationIntegrationTests' --results-directory /private/tmp/bingo-ts-baseline.hQXteN/remediation-results --logger 'trx;LogFileName=dashboards.trx'
```

Exit0, **16 passed / 0 failed / 0 skipped**, wall **9.39s**; TRX ID
`1a001098-b46a-4be4-a1f0-56db38036f29`, SHA256
`d5b25238675bcdd7a6eb49d7662950e967219084da167557a882eca758a7c3d6`.
The standalone [provisioning probe](provisioning-probe.cs) was rerun via
`dotnet run --project /private/tmp/bingo-ts-baseline.hQXteN/probe/ProvisioningProbe.csproj --configuration Release -p:BuildProjectReferences=false`:
exit0, default-template branch, concurrent clone isolation, 85 migrations,
preserved credentials, closed template, later clean clone and database cleanup
all passed. Wrong credentials were rejected at 60.00s; owned container
`57ef5d7f518e` was disposed. Scratch log: `remediation-probe.log` in the same
baseline scratch directory; log SHA256
`5639e1d1bb84c75f6346a7baba776bf78eb0217d705b80c5626dbc1b78aa24c2`.
Scoped diff check passed.

These precommit checks are not final-SHA runs. After this corrective commit, run
both full suites from scratch on its SHA, using the existing runner. The
post-commit [result record](final-sha-results.json) must show both complete passes;
never reuse the failed candidate as acceptance. Independent review remains pending.
