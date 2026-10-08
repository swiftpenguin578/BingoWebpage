# Remediation item 1 — F1 temporary end-update failures

Authority: [supplied brief](supplied-brief.md) item 1; [supplied review](supplied-review.md) F1 and source report24c F1. Baseline clean `4b9f15657978728160a00960bd45bf41cb04abb3` verified. This supersedes the earlier planner technical resolution that prohibited resending an unapplied end update; it does not relabel that resolution as user approval. Supplied product decisions, including D6, are preserved verbatim in [supplied-decisions.md](supplied-decisions.md).

An end-only Unknown write followed by an unchanged previous source window now becomes Retry. Immediate readback schedules the agreed delay; a due reconciliation has already waited and makes the proved-unapplied attempt eligible. A target-window read still completes without another write. Operation row locking and expected phase prevent duplicate reconciliation from overwriting a concurrent result. Publication stops, permanent rejections and non-end unknown behavior remain. HTTP408 is Unknown/temporary, like server/timeouts/network errors; 429 retains its later-provider-due handling.

Worker-reported execution (not independently rerun):

```
dotnet test tests/Bingo.IntegrationTests --configuration Release --no-restore --filter 'FullyQualifiedName~Au20Remediation|FullyQualifiedName~Au20UnknownEnd|FullyQualifiedName~Au20Concurrent|FullyQualifiedName~UnknownCreateRemains' --logger 'console;verbosity=minimal'
dotnet build Bingo.slnx --configuration Release --no-restore
git diff --check
```

PASS 7/7, zero skipped; Release zero warnings/errors; diff check clean. Real management HTTP client with controlled502→timeout/network/408→200 proves writes at +1 then +2minutes. Real PostgreSQL claim expiry becomes Unknown, reads unchanged source and resends. Existing unavailable-read→target-read, concurrent-worker and Unknown Create cases passed. No live WOM or user-owned DB. Initial serializer analyzer issue corrected; first runtime run exposed misplaced Update hook and crash fixture fingerprint, both corrected before the passing run. No schema change, no independent review; items2–6 remain.
