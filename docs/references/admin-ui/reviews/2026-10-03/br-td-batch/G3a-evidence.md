# G3a draft structure concurrency evidence

This record is scoped to G3a (TD-1). It contains only sanitized fixture and
test evidence; no production identifiers, participant data, provider calls, or
user database access are included.

## Baseline and implementation

- Source baseline: `059faf5ba904b4a35c54eca4021fa306a2ea0586`.
- Activation checkpoint: `711d794`.
- G1 predecessor: `3c2a0a6`.
- G2 predecessor: `a4a640c`.
- The remediation commit is recorded separately after the focused checks below;
  Claude's named independent recheck remains pending.
- `OnPostUpdateTeamAsync` now advances the locked draft version whenever team
  inclusion changes, and `OnPostRemoveDraftTeamAsync` advances it when an
  included team is removed. The shared draft row write gives a waiting
  serializable Start or Finalize a current-version conflict instead of allowing
  a stale included-team snapshot to pass validation.
- `OnPostStartAsync` maps serialization failures at either locked boundary to
  the existing no-write draft-conflict response. The event and draft locks still
  serialize the authority decision before team validation and publication.

## Controlled PostgreSQL checks

The focused checks used independent Testcontainers PostgreSQL connections. A
first operation held its real `draft_sessions ... FOR UPDATE` boundary open;
the second operation's event-lock interceptor captured its backend PID. The
test queried `pg_blocking_pids(waiting_pid)` before releasing the first
transaction, so it proves database blocking rather than task scheduling.

- `TeamInclusionChangeAndStartSerializeInBothOrders`: passed 2 orderings. A
  committed inclusion produces Setup with the team included and no
  `draft.started` audit when Start loses; a completed Start leaves the manual
  team excluded and exactly one `draft.started` audit when Start wins. The test
  requires exactly one operation to report success and rejects a stale Running
  draft that includes the newly added team.
- `TeamInclusionChangeAndDirectFinalizeSerializeInBothOrders`: passed 2
  orderings. A committed inclusion leaves Setup with no publication when
  Finalize loses; a completed direct publication leaves the team manual and one
  `DirectRoster` cycle when Finalize wins. The test rejects publication from a
  stale one-team snapshot.
- `RemovingDraftTeamAndStartSerializeInBothOrders`: passed 2 orderings after
  the shared draft-version write; existing removal and running/finalized
  refusal coverage also passed.

Build and focused PostgreSQL commands:

```text
dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --configuration Release --disable-build-servers
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~TeamInclusionChangeAndStartSerializeInBothOrders|FullyQualifiedName~TeamInclusionChangeAndDirectFinalizeSerializeInBothOrders'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --configuration Release --filter 'FullyQualifiedName~RemovingDraftTeamAndStartSerializeInBothOrders|FullyQualifiedName~TeamInclusionChangesAreRefusedWhileRunningAndFinalized'
git diff --check
```

The full `DraftOperationsIntegrationTests` class and final scoped build remain
required after the G3b follow-up. No unrelated TD-8 movement race, UI
integration, or other Teams/Draft item is included.
