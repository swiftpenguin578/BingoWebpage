# F3 Luck checkpoint conversion evidence

Date: 2026-10-03

The deployment path now runs `--convert-luck-checkpoints` after `--migrate` and
before production preflight. The operation enumerates retained version-1 rows in
event order and reports one outcome per event: `Converted`, `Already converted`,
or `Could not convert` with a bounded reason. Conversion locks the event at
`SERIALIZABLE` isolation and uses only the retained payload; it does not read
current provider evidence. A failed conversion leaves the original row unchanged
and causes a nonzero command result. A rerun reports the conversion marker as
`Already converted` without rewriting the row.

## Executed checks

- `dotnet build src/Bingo.Web/Bingo.Web.csproj --no-restore --disable-build-servers --configuration Debug`
  — passed with 0 warnings and 0 errors.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --filter FullyQualifiedName~LuckCheckpointConversionOperationReportsAllOutcomesAndPreservesFailedRows --logger "console;verbosity=minimal"`
  — passed against controlled PostgreSQL. It verifies successful conversion,
  idempotent retry, visible failure, and byte/metadata preservation for a failed
  retained row.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --filter FullyQualifiedName~BoundedLuckLegacyConversionIsExplicitAndReadsRetainConvertedSnapshot --logger "console;verbosity=minimal"`
  — passed against controlled PostgreSQL, preserving the existing bounded
  conversion/read behavior.
- `bash -n deploy/host/bingo-deploy` and `git diff --check` — passed.

The test fixtures are controlled and contain no participant data or provider
secrets. Production execution remains gated by the normal deployment operator
path; this checkpoint does not claim a production migration baseline.
