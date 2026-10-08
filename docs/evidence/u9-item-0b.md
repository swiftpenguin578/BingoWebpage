# U9 item 0b — WOM server

Implemented on `codex/u9-final-wom`, after 0a `7a9d59c1`. Awaiting independent review/manual acceptance.

- Content-negotiated JSON results for all six WOM actions retain legacy redirects. Refusals carry error/reason; fetch returns exact skip/retry metadata; managed results expose operation identity and status. Submitted credentials are cleared and redacted from errors, including binding errors.
- Cached no-store Current read carries event/version, integration, management and activity. Both page and readback keep hidden events 404 for Admin/SuperAdmin; dead hidden inspection removed.
- U9-Q2 adds `WithinHour = 10`, preserving existing stored enum values. Link/Disconnect, Create/credential and deletion eligibility are projected by services. Queued/unknown operations disable relevant actions. Exact UTC window checks remain unchanged.
- End-update next attempt is the stored operation time only when an unresolved end-only Update payload matches the current target. No computed UI backoff. No migration or WOM request on GET.
- Shared: one WOM `GET:Current` read-policy entry; Danish additions extend the contiguous U9 resource block.

Executed checks (build/test output piped through tail, full temporary logs under `/private/tmp/u9-*`):

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj -c Release --no-build --filter 'FullyQualifiedName~EventCompetitionManagementIntegrationTests|FullyQualifiedName~Slice10Pass103ActivityProjectionTests|FullyQualifiedName~Slice10Pass102CompetitionSynchronizationTests|FullyQualifiedName~Au20' --logger 'trx;LogFileName=u9-0b-final.trx' --results-directory /private/tmp/u9-test-results
dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj -c Release --filter 'FullyQualifiedName~ManagedCompetitionUiTests|FullyQualifiedName~AdminDesignLocalizationTests' --logger 'trx;LogFileName=u9-0b-source.trx' --results-directory /private/tmp/u9-test-results
dotnet test tests/Bingo.Application.Tests/Bingo.Application.Tests.csproj -c Release --filter 'FullyQualifiedName~Au20StoredOutcomeContractTests' --logger 'trx;LogFileName=u9-0b-stored-outcome.trx' --results-directory /private/tmp/u9-test-results
git diff --check
```

Final results: PostgreSQL **264/0/0**, **11m51s**; Browser source/localization **8/0/0**; Application stored-outcome contracts **2/0/0**. Focused initial new tests 8/2: the hidden fixture lacked required metadata; fixed using domain Hide, named rerun 2/0/0 before the full final selection. No production quarantine rule changed. No known base failure. No whole .NET suite or browser/visual approval claim. Shared review environment untouched; all tests use owned fixtures.

Next: item 1a Final Review page, then 1b actions and required checkpoint; continue 2a without waiting after that checkpoint.
