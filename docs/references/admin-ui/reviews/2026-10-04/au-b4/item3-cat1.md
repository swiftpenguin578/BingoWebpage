# B4 item 3 — CAT-1 activity team size

Status: implemented by the B4 worker in `fd3ce10` (`feat(catalogue): move team size to activity`); external Claude review and new-UI/manual acceptance are pending.

Remediation status: the authorized CAT-1 binding and invalid HTTP-input proof
are recorded in [`remediation/item2-cat1-binding.md`](remediation/item2-cat1-binding.md)
and committed in `c2d2220`; the initial checkpoint above remains historical.

The agreed team size is one informational activity value, a whole number at least 1 with default 1, editable by every Admin beside efficient completions/hour. It is never a calculation input. Missing input from the current page preserves the stored value. The per-drop `assumed_participants` and `probability_scope` columns remain for history and snapshots, but are retired from new context input. The migration must stop with a clear error if any source drop has `assumed_participants <> 1` or `probability_scope <> 'Participant'`; it must not infer or coalesce a value. See [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md), “Drop-rate mechanics” final Option 1 and “Catalogue layout”, and the [authorized B4 brief](/Users/christopher/Documents/BingoWebpage/review-notes/27-codex-brief-b4-catalogue.md) lines 60–70.

The implementation adds `BossActivity.TeamSize`, the database default/check constraint, the complete Up/Down migration, designer and model snapshot, and explicit add/update handler binding. A team-size-only update leaves the existing EHB estimate unchanged; the existing rate-change path still refreshes dependent drafts through the established mechanism. The migration test uses an isolated populated PostgreSQL schema, proves fail-closed behavior, repairs the legacy context, proves default-1 backfill and then proves Down removes the column.

Worker-executed PostgreSQL checks:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Cat1ActivityTeamSize
Passed 2, Failed 0, Skipped 0, Total 2

dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-build --filter FullyQualifiedName~Cat1MigrationFailsClosed
Passed 1, Failed 0, Skipped 0, Total 1
```

The user's 4 October per-drop context count of zero is supplied evidence recorded in the source decisions and brief, not an agent-run production query. The runbook now requires both read-only counts again immediately before deployment. No production or user-owned database was accessed. Build and documentation checks are recorded in [item4-docs-register.md](item4-docs-register.md).
