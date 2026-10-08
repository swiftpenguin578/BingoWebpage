# F9 finalization validation evidence

Date: 2026-10-03

BR-5 finalization and reopening blockers now identify the other current event
by name. A Live blocker says that ending it is the first step and that its
results must then be published; an Awaiting Final Review or legacy Finalized
blocker says to publish that event's official results before continuing. The
message applies to both the finalization and unfinalization paths.

BR-6 now validates the reopening reason at the page and service boundary using
the shared 2,000-character contract limit. An over-limit reason returns the
existing page validation path before the service runs. The service also fails
before opening a mutation transaction. At exactly 2,000 characters, the full
reason is retained in the finalization snapshot and audit entry. The separate
legacy lifecycle-transition reason column is capped at 1,000 characters, so it
stores an explicitly marked bounded copy while the authoritative history keeps
the complete reason.

## Executed checks

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~Slice3FinalizationAtomicityIntegrationTests' --logger 'console;verbosity=minimal'`
  — passed 9 tests against controlled PostgreSQL, including both BR-5 message
  states, page-side 2,001-character rejection with no service call or event
  mutation, service-side 2,001-character rejection, and exact 2,000-character
  persistence.
- `dotnet build tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --disable-build-servers --configuration Debug`
  — passed with 0 warnings and 0 errors.
- `git diff --check` — passed before the final source review.

The existing `ResultsPublicationIntegrationTests.PublicationArchivesAtomicallyAndIsFailureSafeAndIdempotentAcrossConcurrentRetries`
fixture was also run while isolating the changed query shape. It fails at its
pre-existing concurrency assertion (`expected one successful concurrent call,
observed two`) even with the original current-event projection, before the
new reopen validation path runs. It is recorded as an unrelated test-harness
limitation and is not claimed as passed.

No provider calls, user-owned database, production access or participant data
was used. Independent source review remains assigned to Claude.
