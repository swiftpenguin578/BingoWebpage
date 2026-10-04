# B4 remediation item 3 — D8 snapshot/deployment handling

Status: worker-implemented in `c2b00b2`; external Claude direct recheck remains
pending. The user decision is [08-decisions.md, D8 option (a)](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b4-catalogue-review-decisions):
production rebuilds restore the reviewed database backup, the export command is
removed, and the snapshot loader remains only for CI, Development, and
manual-test data. The applied `20260916100000_PopulateRetainedCatalogue`
migration was not changed.

Snapshot export/import carries `TeamSize` with a default of 1 for older files.
Import validates every record before opening a transaction and refuses
conditional/Only-after or non-default per-drop participant context with a clear
product/data error and zero writes. Production rejects the snapshot command;
the deployment script no longer invokes it. `bash -n` validates the changed
deployment script without running it.

Worker-executed checks:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~D8SnapshotImportCarriesTeamSizeAndRejectsRetiredContextBeforeWrites
Passed 1, Failed 0, Skipped 0, Total 1

bash -n deploy/host/bingo-deploy
exit 0
```

The broader worker run of `FullyQualifiedName~Snapshot` produced 47 passes and
one unrelated existing `CataloguePopulationMigrationIntegrationTests` failure:
PostgreSQL reported missing `e.placement_rule` in its legacy fixture. No
production deployment/script or user-owned database was run.
