# R-2 results-publication concurrency test checkpoint

Date: 2026-10-03

## Scope

Claude's read-only independent review of F1-F9 in
`/Users/christopher/Documents/BingoWebpage/review-notes/10-fix-batch-review.md`
identified R-2 in
`ResultsPublicationIntegrationTests.PublicationArchivesAtomicallyAndIsFailureSafeAndIdempotentAcrossConcurrentRetries`.
Claude did not build or run the checkout. This follow-up changes the test only;
there is no production behavior change.

The previous test required one concurrent call to throw a stale-session error
before reaching its publication assertions. PostgreSQL may instead serialize a
second call after the first commit; that call safely returns the service's
`AlreadyPublished` idempotent result. The test now accepts exactly one fresh
publication and exactly one of the expected stale refusal or verified
idempotent result. Only an `InvalidOperationException` containing the existing
stale-session message is classified as a refusal; unrelated exceptions still
fail the test. The finalization, placement, version, audit, rollback, and
explicit idempotent-retry assertions always run.

## Evidence

Focused real-PostgreSQL check:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore \
  --filter 'FullyQualifiedName~ResultsPublicationIntegrationTests.PublicationArchivesAtomicallyAndIsFailureSafeAndIdempotentAcrossConcurrentRetries' \
  --logger 'console;verbosity=minimal'
```

Result: 1 passed, 0 failed. The run built the affected project graph and used a
controlled PostgreSQL Testcontainers fixture. `git diff --check` also passed.

The full production-shaped deployment rehearsal (R-3) remains unexecuted and
deferred. No production database, provider API, participant data, deployment,
push, or merge was used.
