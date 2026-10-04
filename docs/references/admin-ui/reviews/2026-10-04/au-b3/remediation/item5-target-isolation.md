# Remediation item 5 — F5 per-target outcomes and backoff

Authority: supplied-brief.md item5 / supplied-review.md F5 and24c F3/F4/F5. A permanent failure locks/reloads current end state before rejection. An outdated target retains its failed operation history but leaves management eligible for the resumed pending target. Credential invalidation is part of that guarded transaction, so an old401/403 cannot invalidate the newer request. Coalescing or dispatch-time refresh resets AttemptCount when a pending end's target changes; same-target retries retain count. HTTP408 temporary classification is implemented/proved in item1.

Worker-reported execution (not independently rerun):
```
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20RemediationLateOld|FullyQualifiedName~Au20RemediationResumeRestarts|FullyQualifiedName~Au20PermanentRejection|FullyQualifiedName~Au20RateLimit|FullyQualifiedName~Au20RemediationRealHttpTemporary' --logger 'console;verbosity=minimal'
dotnet build Bingo.slnx --configuration Release --no-restore
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-build --filter FullyQualifiedName~Au20RemediationLateOld --logger 'console;verbosity=minimal'
git diff --check
```
PASS12/12 then tightened late-response recheck2/2, zero skipped; Release zero warnings/errors; diff check clean. Controlled in-flight Validation/Unauthorized arrives after real local Resume, leaves the new target and writable credential intact, and the new target succeeds one minute later with count1. A target on30-minute retry is replaced through Resume; its first new failure schedules1minute with count1. Existing permanent rejection and rate-limit cases remain green. Real PostgreSQL and controlled doubles only. No schema/new display. External recheck pending.
