# S6 — WOM Fetch now during Final Review

Status: server part complete in brief40; WOM binding remains pending.

Authority: brief40 S6, `review-notes/08-decisions.md` S6 option (a), and WA-2.
Only the `FetchCompetition` route check and the existing WOM button condition
were opened to `AwaitingFinalReview`; `MakeDevelopmentCompetitionDue` remains
Live-only and the synchronization service guards are unchanged.

Implementation commits: route/page source in
`c174f03cd82edb2389f334e404ad6ecd0b1a90bf`; focused proof in
`73e414515c187314c47d5434b1a7b34814ac5c9a`.

Evidence:

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Slice10Pass102CompetitionSynchronizationTests.ManualRefreshRunsInFinalReviewButSkipsAnUnmatchedEndAndFinalizedEvent' --logger 'trx;LogFileName=/tmp/bingo-brief40-s6.trx'` — **passed 1, failed 0, skipped 0** against Testcontainers PostgreSQL and a controlled provider double.
- The proof records a successful Final Review refresh (`LastAttemptAt`/
  `LastSuccessfulAt` and the linked synchronization audit), skips an unmatched
  WOM end without changing those timestamps or provider-call count, and refuses
  a Finalized event without a provider call.

Test-change mapping: this is a new focused proof; no existing expectation was
changed, deleted, skipped, or loosened.
