# G2 paused-interval evidence

This record is scoped to G2 (BR-3). It contains only sanitized fixture and test
evidence; no production identifiers, participant data, provider calls, or user
database access are included.

## Baseline and implementation

- Source baseline for the remediation: `059faf5ba904b4a35c54eca4021fa306a2ea0586`.
- Activation checkpoint: `711d794`.
- G1 predecessor: `3c2a0a6`.
- This record covers the named G2 translation remediation; Claude's independent
  named recheck remains pending.
- Danish Review strings now cover the structured earlier-upload blocker, its
  upload-time label, the direct earlier-review link, the approval-block toast,
  and paused final-review guidance. Two stale Danish keys were removed only
  after repository-wide usage checks showed no runtime references.
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
