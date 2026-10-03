# G2 paused-interval evidence

This record is scoped to G2 (BR-3). It contains only sanitized fixture and test
evidence; no production identifiers, participant data, provider calls, or user
database access are included.

## Baseline and implementation

- Source baseline: `f2ea1cffb8f4d9c0b23dd68dcf3f6675d1bbb5d5`.
- Activation checkpoint: `14d88b0`.
- G1 predecessor: `24c30fc`.
- This implementation checkpoint is the local G2 commit; Claude's independent
  read-only review remains pending.
- The Review Details page now loads all completed `AwaitingFinalReview` to
  `Live` intervals for the event without comparing them with immutable upload
  time. It renders them as an informational note and asks the reviewer to
  verify the screenshot's in-game time against the listed interval(s).
- Upload-time cutoff, ordering, and the existing after-event-end context remain
  unchanged. An event's final unresumed review period remains represented by
  the existing effective-end warning rather than a completed pause interval.

## Controlled PostgreSQL checks

The following checks passed against Testcontainers PostgreSQL:

- `ReviewDetailsShowsPausedIntervalsAsScreenshotTimeGuidance`: inserts a
  resumed paused interval at deterministic UTC times, places the upload inside
  that interval, and verifies the real HTTP Review Details response lists the
  interval, gives screenshot-time verification guidance, and contains neither
  the old upload-time ineligibility wording nor its alert heading.
- `AdminReviewGraceContextUsesScheduledOrAuthoritativeEarlyEnd`: existing
  direct PageModel coverage passed for scheduled and authoritative early end
  times, including PostgreSQL microsecond-normalized timestamps.

Build and diff checks:

```text
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false --filter FullyQualifiedName~ReviewDetailsShowsPausedIntervalsAsScreenshotTimeGuidance --logger 'console;verbosity=minimal'
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore /p:UseSharedCompilation=false --filter FullyQualifiedName~AdminReviewGraceContextUsesScheduledOrAuthoritativeEarlyEnd --logger 'console;verbosity=minimal'
git diff --check
```

Both focused tests passed. No migration, schema, scoring, or persistence
behavior change is part of G2.
