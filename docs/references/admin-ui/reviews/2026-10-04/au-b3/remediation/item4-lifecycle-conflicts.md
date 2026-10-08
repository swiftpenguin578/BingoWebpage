# Remediation item 4 — F4 lifecycle database conflict recovery

Authority: supplied-brief.md item4 / supplied-review.md F4 and24b M1. EndNow and Resume capture their original request instant, then attempt at most three Serializable transactions. PostgreSQL40001/40P01, including wrapped database exceptions, trigger internal retry after rollback/disposal and tracker clear. Exhaustion returns the normal try-again service result. Event row locks precede sync row locks, matching refresh order. Ordinary authorization/version/singleton/history/replacement validation remains.

Worker-reported execution (not independently rerun):
```
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20RemediationLifecycle|FullyQualifiedName~Au20EarlyEnd|FullyQualifiedName~Au20Resume' --logger 'console;verbosity=minimal'
dotnet build Bingo.slnx --configuration Release --no-restore
git diff --check
```
PASS8/8, zero skipped; Release zero warnings/errors; diff check clean. A separate PostgreSQL transaction commits a sync-row update after the lifecycle snapshot, forcing actual40001 at SELECT FOR UPDATE. End and Resume recover; the clock advances two minutes during the collision but the original actual/request instant remains exact at PostgreSQL microsecond precision (input includes extra100ns ticks). Repeated real collisions exhaust exactly three attempts, return try-again and leave event state/version unchanged. Existing rounding/Resume cases passed. No schema, provider call or new display. External recheck pending.
