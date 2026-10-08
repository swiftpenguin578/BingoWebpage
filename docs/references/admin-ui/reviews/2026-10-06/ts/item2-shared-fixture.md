# TS item2 — shared collection fixture and isolated test databases

Implemented on the assigned branch after item1 `a67aac9ab8943a4232c569ab7fdb7b1cec615017`.
This checkpoint awaits Claude independent review. All checks here are candidate /
precommit checks, not final-SHA whole-suite evidence.

`tests/PostgreSqlTestDatabase.cs` is the single shared helper, linked into the
Integration and Browser test projects. 41 Integration classes use
`IClassFixture<PostgreSqlTestFixture>`. xUnit 2.9.3's existing default collections
each contain one class; the native class-fixture lifetime permits one container
and one migrated template per existing collection, with deterministic teardown
and unchanged collection parallelism. No custom runner, global static lifetime,
new collection grouping, package or xUnit parallelism setting was introduced.

The first test supplies its original `postgres:17-alpine` builder, including its
original database bootstrap name, username, password and other settings. One
cached initialization task starts the container, validates the actual credentials,
migrates its template once and closes it to ordinary connections. Every test then
creates a unique `ts_<guid>` database using `CREATE DATABASE ... TEMPLATE ...`.
The test connection retains the original settings except its database name.
Readiness, template and admin connections disable pooling; test connections keep
their original pooling behavior. Teardown clears only that test's pool and drops
only its generated database with `DROP DATABASE ... WITH (FORCE)`. The fixture
disposes its owned container after the class collection finishes.

C20 now constructs its database, HTTP factory and evidence storage per test. Its
existing test/helper bodies are unchanged; every case still seeds its own rows.
Its former class-shared database no longer accumulates other cases' data.

The shared path includes bounded credentialed startup readiness as a prerequisite
to using the template or a clone. Item4 applies the same helper to dedicated
fixtures. The 25 dedicated Integration exceptions are preserved for item3's list.
No role/extension/server-setting mutation or database-level timezone setting was
found in the relevant migrations that would need special handling during cloning.

## Executed checks

- `dotnet build Bingo.slnx --configuration Release --no-restore`: exit 0,
  0 warnings / 0 errors; 26.69 seconds MSBuild elapsed. Clean Release build follows
  after the remaining setup edits.
- `git diff --check`: passed after correcting one generated trailing space.
- Byte comparison against starting `8fd559c`: all 40 ordinary classes' source
  suffixes from their first Fact/Theory (SchedulePrecision: its partial-case
  boundary) are unchanged. C20's entire source from its first Fact through the
  end of the test class is unchanged. Changes are class-fixture wiring and setup /
  teardown; assertion, input, expected-value and test-removal changes: none.
- Standalone real PostgreSQL probe in [provisioning-probe.cs](provisioning-probe.cs):
  exit 0. Verified two concurrent distinct clones on one endpoint, original
  credentials, identical **85** applied migrations, initially empty accounts,
  isolated writes, template connection protection, clean later clone, and drops
  while another clone remains usable. Owned container `b30d65896c7b` was disposed.
  Probe uses a deterministic microsecond-aligned UTC synthetic account timestamp.

Actual probe command (temporary net10.0 console project referencing the Integration
project, linking the durable probe source, `IsTestProject=false`):

```sh
dotnet run --project /private/tmp/bingo-ts-baseline.hQXteN/probe/ProvisioningProbe.csproj --configuration Release -p:BuildProjectReferences=false
```

Probe log: `/private/tmp/bingo-ts-baseline.hQXteN/provisioning-probe.log`, SHA-256
`23c32787976b1c69134da77c94d2d839b80252c262e31104f185779a61ee5976`.

Focused command:

```sh
/usr/bin/time -p dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Bingo.IntegrationTests.C20ObjectiveIdentityIntegrationTests.|FullyQualifiedName~Bingo.IntegrationTests.C11FinalizedRosterIntegrationTests.|FullyQualifiedName~Bingo.IntegrationTests.IdentityFieldConflictIntegrationTests.|FullyQualifiedName~Bingo.IntegrationTests.AdminStaleChangeIntegrationTests.|FullyQualifiedName~Bingo.IntegrationTests.SchedulePrecisionIntegrationTests.' --results-directory /private/tmp/bingo-ts-baseline.hQXteN/focused2 --logger 'trx;LogFileName=shared-fixture.trx'
```

**152 passed / 0 failed / 0 skipped**, exit 0; process wall-clock **58.90 seconds**.
TRX ID `eabf9e39-92c2-4aa5-ad91-043d6e7b6b0a`, SHA-256
`3736c41b0620c2d393edfcbf3b59bbe9300610b7be8e9f3a9dbef6df5096463d`.
No bodies were retried or weakened. No production code or user-owned database
was changed or used. Whole final-SHA suite gates remain required.
