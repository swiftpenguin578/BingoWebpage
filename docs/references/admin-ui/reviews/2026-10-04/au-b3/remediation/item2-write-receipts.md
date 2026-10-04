# Remediation item 2 — F2 actual write-receipt fields

Authority: supplied-brief.md item2 / supplied-review.md F2 and24c F2. Success receipts now compare identity plus title/configured window; they do not infer a roster snapshot from the adapter's empty participant list. Reconciliation reads retain the participant comparison when a roster was sent. No client parser or schema redesign.

Worker-reported execution (not independently rerun):
```
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20RemediationRealHttpRoster|FullyQualifiedName~ManualLinkWaitsForManagedProviderWrite' --logger 'console;verbosity=minimal'
dotnet build Bingo.slnx --configuration Release --no-restore
git diff --check
```
PASS2/2, zero skipped; Release zero warnings/errors; diff check clean. Real management client parses a controlled successful roster PUT directly into Succeeded, stores acknowledged roster and makes only the required source-check read. Existing mismatched-window receipt/reference-lock case still rejects the receipt as Unknown. Isolated PostgreSQL and fake HTTP only. External recheck pending.
