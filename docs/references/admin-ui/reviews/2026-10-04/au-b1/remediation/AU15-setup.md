# AU15 test setup remediation evidence

## Scope

This checkpoint implements item 3 of `review-notes/21-codex-brief-b1-b2-remediation.md`, which addresses AU15-1 in `review-notes/20a-b1-au15-au16-scope-review.md`. The change is confined to the PostgreSQL/HTTP test fixture.

## Change

`DevelopmentTest15DueControlIsIdempotentAndResetRemainsCachedOnly` now creates its `TestClock` at the same UTC half-hour boundary used by `DevelopmentScenarioSeeder`. The fake competition therefore uses the same `EventStartsAt` and `EventEndsAt` window as the seeded event, regardless of when the test starts. No WOM synchronization or production service code changed.

## Check

Executed:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~DevelopmentTest15DueControlIsIdempotentAndResetRemainsCachedOnly' --verbosity minimal -m:1 -nodeReuse:false
```

Result: **1 passed, 0 failed, 0 skipped**, using the real PostgreSQL test container and the HTTP `WebApplicationFactory` path. The completed test reached the subsequent non-Live Fetch refusal, rejected ClearCompetition, reset/reseed and cached-state assertions that were previously unreachable after the schedule assertion.

The Release web build and `git diff --check` are run before committing this checkpoint.

## Residual risk

The fixture intentionally shares the seeder's half-hour precision. It does not alter the schedule matching tolerance or any production refresh behavior.
