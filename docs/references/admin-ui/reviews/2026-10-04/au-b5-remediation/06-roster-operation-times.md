# Item 6 — E1 roster/WOM readback times

Backend remediation implemented; external Claude recheck and RC04/DRF binding
pending. Authority: supplied brief35 item 6 / report31d M1. Existing register row
now requires comparing roster PublishedAt with operation CreatedAt/UpdatedAt.
These are current persisted facts, not a new synchronization claim or receipt.

DraftCurrentState exposes nullable RosterPublishedAt; its last existing operation
exposes CreatedAt alongside UpdatedAt. No provider calls, permission/lifecycle
changes, migration or UI display. The new PostgreSQL proof runs real draft
pick/finalize commands, then retains two local publication cycles and an older
successful operation. It asserts both exact times and identities, success older
than publication, no new queued status, no secret exposure and unchanged roster
history. Existing Pending/Failed proofs still pass. No existing test changed.

```
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationDraftReadbackSeparates|FullyQualifiedName~B5DraftReadbackExposesPendingAndFailedWom' --logger 'trx;LogFileName=item6.trx' --results-directory /tmp/au-b5-remediation -v minimal
git diff --check
```

Result: 3 passed, 0 failed, 0 skipped; diff check passed. Initial compile failed
CS1061 because the new fixture referenced DraftPickId instead of the frozen
roster's EffectivePickNumber; setup corrected, assertions unchanged. Final
whole-suite gate remains pending on the final commit.
