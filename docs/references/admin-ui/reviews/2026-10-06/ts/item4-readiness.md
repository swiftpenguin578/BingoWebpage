# TS item4 — credentialed bounded readiness everywhere affected

The common `PostgreSqlReadiness` helper already guards the shared template and
every clone. All **25** dedicated Integration container startups and the existing
Browser collection fixture now call the same `StartAsync(container)` helper.
Their original builders, schema setup, data, migrations, parallelism and teardown
are otherwise unchanged. AdminDesignParityFixture's existing ruling58 readiness
is preserved without edits.

Readiness opens a real nonpooled Npgsql connection with the actual configured
database/user/password and executes `SELECT 1`. One cancellation deadline bounds
the setup wait at 60 seconds; 250 ms polls occur only following a failed connection
or query. No test-body retry, unconditional startup sleep, tolerance change or
assertion/input/expected-value change is present. Permanent setup failure remains
a failure with the last connection error attached.

Candidate checks (not final-SHA suite runs):

- Clean: `dotnet clean Bingo.slnx --configuration Release --verbosity quiet`, exit 0.
- Build: `dotnet build Bingo.slnx --configuration Release --no-restore`, exit 0,
  **0 warnings / 0 errors**, MSBuild elapsed **29.72 seconds**.
- `git diff --check` passed. Normalizing only each new readiness call back to its
  original container `StartAsync()` makes all 25 dedicated Integration files and
  `BrowserTestApplicationFactory.cs` byte-identical to starting `8fd559c`.
- Standalone [provisioning probe](provisioning-probe.cs), same command as item2:
  exit 0. An intentionally incorrect synthetic password on its own ready server
  was rejected after **60.00 seconds** by the bounded real-connection helper.
  Concurrent cloning, 85 migrations, isolated writes, valid credentials, immutable
  template and cleanup still passed afterward. No timing tolerance or sleep was
  added to its checks. Owned container `7f02f9afe9c8` was disposed.

Whole Browser command:

```sh
/usr/bin/time -p dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --configuration Release --no-build --results-directory /private/tmp/bingo-ts-baseline.hQXteN/browser4 --logger 'trx;LogFileName=browser.trx'
```

**150 passed / 0 failed / 0 skipped**, exit 0; process wall-clock **16.01 seconds**.
TRX ID `33fe9df8-a604-4b4e-af2b-bb2deab580b3`, SHA-256
`121cc0716d3c1dc5744884ca00476d551895ba4190a162602ada0b43c8b495b7`.

Dedicated migration/fresh-database command:

```sh
/usr/bin/time -p dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Bingo.IntegrationTests.BoardEstimateFreshnessMigrationTests.|FullyQualifiedName~Bingo.IntegrationTests.Cat1TeamSizeMigrationIntegrationTests.|FullyQualifiedName~Bingo.IntegrationTests.PostgreSqlConnectivityTests.' --results-directory /private/tmp/bingo-ts-baseline.hQXteN/dedicated4 --logger 'trx;LogFileName=dedicated.trx'
```

**8 passed / 0 failed / 0 skipped**, exit 0; process wall-clock **19.68 seconds**.
TRX ID `b774b327-f8bc-4b5f-a2bf-66c56b4480d0`, SHA-256
`c7ef4213ef7ef1dad54620c3dcf6e0bfcb17bc770e541e9dab486639fb8072db`.

Scratch logs under `/private/tmp/bingo-ts-baseline.hQXteN/`:
`clean-build4.log` SHA-256 `6a182c2a3caa5362c91c2b29c008b28f9c852a17647fcb830cac79933e2c1b0c`;
`readiness-probe4.log` SHA-256 `af5310a239d2ff8c50e58db681a3d7287a6d98e2948c45513d4b5c57d6f1b500`.
Durable compact identities/counters are retained here instead of raw SQL output.
Only Testcontainers-owned databases were used. Independent review and the two
whole-suite final-SHA runs remain pending at this candidate checkpoint.
