# B4 follow-up 29 — D10, migration fixture, and import cleanup

Status: worker-implemented in the three follow-up remediation commits below;
external Claude direct recheck remains pending. This file records worker-run
evidence only. The source authorization is the supplied
[follow-up-29 review](/Users/christopher/Documents/BingoWebpage/review-notes/29-b4-remediation-recheck.md),
especially its final remediation brief, and D10 option (a) in
[08-decisions.md, B4 Catalogue review decisions](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-review-decisions).

The follow-up commits are:

1. `e955284aa01a0869242a499174c51bc5158ba29d` — R1 production preflight
   requires one active activity, item, and drop from PostgreSQL. It has no
   bundled-file comparison or path dependency and tells the operator to restore
   the production backup when a category is missing or inactive-only.
2. `a372a5c3090ee10b27bd9fa11747a5d034fff7d3` — R2 runbook/topology correction:
   the deploy script performs no restore; the new/interrupted branch only
   migrates and bootstraps an owner, leaves an empty catalogue, and is not a
   supported production rebuild. A rebuild restores first, then runs the normal
   retained deploy.
3. `7d89ddf1a9954433566e0843d894124fe37009fd` — R3 predecessor-schema test
   fixture uses a schema-matched test context and SQL-compatible mappings. The
   retained `20260916100000_PopulateRetainedCatalogue` migration is unchanged.
4. The current Low/docs checkpoint removes production `ExportAsync` and the
   dead `ValidateBaselineAsync` comparison, adapts import tests to the
   controlled test fixture writer, updates the active architecture wording, and
   records this follow-up. Its exact hash is in the parent completion handoff.

The R1 test uses database-only renamed activity/item names, and covers missing
and inactive-only activity/item/drop categories. No bundled snapshot is read by
the production preflight. R3 preserves the original migration assertions and
now exercises every retained-catalogue case against PostgreSQL.

Worker-executed checks (all in the assigned worktree; no production or
user-owned database/provider was used):

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter FullyQualifiedName~Slice6CatalogueAdministrationIntegrationTests
Passed 167, Failed 0, Skipped 0, Total 167.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter FullyQualifiedName~CataloguePopulationMigrationIntegrationTests
Passed 5, Failed 0, Skipped 0, Total 5.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter 'FullyQualifiedName~ProductionCataloguePreflightIntegrationTests|FullyQualifiedName~ProductionOperationsTests'
Passed 5, Failed 0, Skipped 0, Total 5.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter FullyQualifiedName~Snapshot
Passed 49, Failed 0, Skipped 0, Total 49.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter FullyQualifiedName~Au21
Passed 7, Failed 0, Skipped 0, Total 7.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter FullyQualifiedName~Au23
Passed 2, Failed 0, Skipped 0, Total 2.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter FullyQualifiedName~Cat1TeamSize
Passed 2, Failed 0, Skipped 0, Total 2.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter FullyQualifiedName~Cat1MigrationFailsClosed
Passed 1, Failed 0, Skipped 0, Total 1.

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --disable-build-servers --filter FullyQualifiedName~D8Snapshot
Passed 1, Failed 0, Skipped 0, Total 1.

dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --disable-build-servers
Build succeeded; 0 Warning(s); 0 Error(s).

git diff --check
PASS.

bash -n deploy/host/bingo-deploy
PASS; the script is unchanged by follow-up29 and was not executed.
```

The first two in-sandbox migration invocations were blocked before test
execution by VSTest `SocketException (13): Permission denied`; the successful
run used the approved external test path. This is an execution-environment
note, not a product failure. No production startup, deploy, restore, migration
or operator count query was run. The user's 4 October zero/zero per-drop
prechecks remain user-supplied evidence linked by the B4 brief and decisions;
they are not worker-run results.
