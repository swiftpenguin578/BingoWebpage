# U9 item 0a — Final Review server

Implemented on `codex/u9-final-wom`, base `a91b29ad627dac8b5bf859b1948ea4bbea014877`. Awaiting independent review and manual acceptance.

- B-Final-2 / BR-12: missing, zero and stale reopen version fail closed. Existing successful Stats and seeded-fixture reopen calls now supply the current version; original behavior assertions retained.
- U9-Q1: archived/legacy Finalized readiness includes the blocking current event and state-specific reason. Publish-time refusal wording retained.
- Structured publication result includes state/version/finalization identity and retained final-refresh status/reason. JSON publish/reopen outcomes and no-store Current read include retained history. Legacy redirect callers retained.
- Shared change: explicit `GET:Current` Read registration for Finalize in `AdminEventPagePolicies.cs`; Danish additions begin one contiguous U9 block at the resource end. No migration.

Executed final checks:

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj -c Release --no-build --filter 'FullyQualifiedName~C33FinalizationFreshnessTests|FullyQualifiedName~Au18|FullyQualifiedName~Au20PublicationFallback|FullyQualifiedName~Slice3FinalizationAtomicityIntegrationTests|FullyQualifiedName~Slice3DestructiveLifecycleIntegrationTests|FullyQualifiedName~BFinal2Br12|FullyQualifiedName~U9Q1|FullyQualifiedName~StatsPass4FiveByFiveObjectivesFinalizationArchiveAndUnfinalizationRetainOfficialHistory|FullyQualifiedName~StatsPass5CalculatedCompletionDtosRetainRawHistoryAcrossFinalizationArchiveAndReopening|FullyQualifiedName~DevelopmentSeededEmergencyCredentialRemainsHistoricalAtCutoff' --logger 'trx;LogFileName=u9-0a-final.trx' --results-directory /private/tmp/u9-test-results
dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj -c Release --filter 'FullyQualifiedName~AdminDesignLocalizationTests' --logger 'trx;LogFileName=u9-0a-localization.trx' --results-directory /private/tmp/u9-test-results
git diff --check
```

Results: PostgreSQL **73 passed / 0 failed / 0 skipped**, localization **4/0/0**, diff check passed. Release compilation completed as part of focused runs. Initial run 68/1 exposed publication wording; named corrections 0/2 exposed the expected Reopen wording assertion and missing Current route classification; corrected named rerun 2/0/0, then final 73/0/0. No known base failure, no whole .NET suite, no browser or visual acceptance claim. Owned test containers only; shared review ports/services untouched.

Next: item 0b WOM server, then 1a/1b and required checkpoint.
