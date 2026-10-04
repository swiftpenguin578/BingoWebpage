# Remediation item 3 — F3 external Conflict recovery

Authority: supplied-brief.md item3 / supplied-review.md F3 and24e M1. External management Conflict is terminal and no longer protects a link from local replacement/disconnection. The persisted queued/claimed/sending/retry/unknown operation guard remains before validation and inside the transaction. The protected-connection message now refers to a non-external connection rather than directing external links to nonexistent management controls. First-Live disconnect bound, exact window and local credential retirement remain.

Worker-reported execution (not independently rerun):
```
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20RemediationDeletedExternal|FullyQualifiedName~Au20ExternalReplacementWaits|FullyQualifiedName~Au20ExternalDelete' --logger 'console;verbosity=minimal'
dotnet build Bingo.slnx --configuration Release --no-restore
git diff --check
```
PASS7/7, zero skipped; Release zero warnings/errors; diff check clean. Real PostgreSQL plus controlled source-missing response produces Conflict/Failed, then Live replacement or pre-Live disconnect succeeds and clears the old code; zero DELETE calls. Existing unresolved-operation and delete/Live-disconnect refusals passed. No schema/new display. External recheck pending.
