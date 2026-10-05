# Item 4 — AU19 proof gaps and contract clarifications

Authority: brief38 item4; review36c F3/F7/F8; `review-notes/08-decisions.md`,
“AU phase plan”, Decision1 AU19 (structured issues targeting affected tiles,
approved/published/private-correction comparison and authoritative readback).

- `CurrentRequirementsAsync` emits each `(TileId, objective-positions)` issue
  once, even when both template and working positions are ambiguous. The real
  PostgreSQL proof removes the unique template-position index only in its isolated
  disposable fixture, inserts both ambiguities, asserts one exact issue and both
  readable candidate objectives, and verifies no approval writes. No migration.
- The concurrency proof commits a catalogue row update from a separate PostgreSQL
  connection after approval starts its serializable snapshot and before its SHARE
  read. The database itself produces SQLSTATE40001; the test asserts that code,
  the single `approval-conflict` issue, known unchanged board state, and no partial
  approval/audit/price writes. It does not inject a synthetic conflict exception.
- The new-objective proof uses the actual tile editor to add a catalogue-drop
  objective, updates catalogue naming, then compares the full projected tile
  (every serialized field) with a real correction replacement publication. It
  asserts one differing tile, the new objective/drop/name/target, exact6.7143 EHB,
  and the count of actual changed published tiles. Existing retained-objective
  tests remain applicable; they were not modified or redundantly rerun here.
- During a correction, readback `Working` is the publication projection, also
  exposed as `PrivateCorrection`; it is not the raw editor cache. The new proof
  asserts their equality. Carried-over objectives use frozen inputs, new objectives
  use current catalogue inputs.
- Publish's catch-all introduced by brief35 item4 (31c M3) intentionally changes
  unexpected failure from the former HTTP500 to a **warning redirect** carrying
  `publication-failed`. Approve's catch-all remains Error. This item records that
  authorized behavior; it makes no additional handler/UI change. Existing prior
  publication-refusal proofs cover generic failure and unchanged persistence.

## Test-change rule / provenance correction

No previously committed test or expectation changed. The governing citation in
prior item4 evidence is corrected from brief35 to the recorded **AU19, AU phase
plan, Decision1** in `08-decisions.md`. Its prior exact assertion mapping remains:
`B5BoardApprovalIssuesCarryStableCodeAndTileOrBoardTarget(true)` Position null →1;
the fixture has position0 occupied and position1 empty; approval emits that exact
empty position, so the issue can jump to its target. All other assertions retained.

New-test development failure: initial run2 passed/1 failed/0 skipped; projection
compared serialized EHB `5` versus PostgreSQL `5.0000` (same numeric value). The
new test fixture was changed to a fractional current boss rate7 (retained rate10),
keeping every exact assertion and adding exact6.7143 EHB. No product fix or
expectation relaxation. The named projection rerun passed1/1, 0 failed/skipped.

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5Round2RealApprovalSerializationConflict|FullyQualifiedName~B5Round2ObjectivePositionIssue|FullyQualifiedName~B5Round2NewCatalogueObjective' --logger 'trx;LogFileName=item4.trx' --results-directory /tmp/au-b5-remediation-2 -v minimal
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5Round2NewCatalogueObjective' --logger 'trx;LogFileName=item4-projection-corrected.trx' --results-directory /tmp/au-b5-remediation-2 -v minimal
git diff --check
```

Final focused evidence: all3 new proofs pass (2 first-run passes reused +1 named
rerun), 0 skipped. Compilation without warnings/errors; diff check passed.
Independent Claude recheck/binding and final batch gate remain pending.
