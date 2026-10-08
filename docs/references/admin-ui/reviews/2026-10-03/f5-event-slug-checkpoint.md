# F5 event slug allocation evidence

Date: 2026-10-03

Event creation now keeps probing valid suffixed candidates until PostgreSQL
accepts a unique slug. Each candidate remains inside the existing request-key
advisory lock and aggregate transaction, so AU03 replay, lost-commit recovery,
audit, and permanent discarded slugs retain their prior boundaries. The former
ten-attempt failure and its misleading retry message are gone.

## Executed checks

- `dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --configuration Debug`
  — passed with 0 warnings and 0 errors.
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --filter FullyQualifiedName~SlugAllocationProbesPastTenCollisionsAndKeepsDiscardedUnicodeSlugs --logger "console;verbosity=minimal"`
  — passed against controlled PostgreSQL. It creates 11 all-Unicode events
  (`event` through `event-11`), discards the first tombstone, and proves the next
  allocation is `event-12`.
- Existing focused event-creation replay/concurrency/permanent-slug filter —
  passed 3 tests against controlled PostgreSQL.

No participant data, provider calls, user-owned database, or production access
was used. Independent source review remains assigned to Claude.
