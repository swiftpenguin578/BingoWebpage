# F2 / LK-1 checkpoint

The checkpoint writer now fences a changed incomplete batch only when the existing
compatible v2 payload represents a complete result (`Calculated` or
`NoEligibleActivity`). With no compatible complete snapshot, a newer incomplete
batch can replace an older incomplete one. A malformed retained payload is treated
as incomplete and is therefore eligible for replacement.

Focused PostgreSQL integration checks executed on 3 October 2026:

- `StatsPass4RepeatedIncompleteFetchReplacesPriorIncompleteSnapshot` — PASS.
  A permanently unsupported source accepts two fetches with different provider
  batches; the second checkpoint has a new `ActivityBatchId` and payload.
- `StatsPass4PartialPlayerCoverageAndUnexpectedUnrankedEndKeepHonestFullResults` —
  PASS. The existing partial → complete → partial path still retains the complete
  checkpoint and its original batch/calculated values.

The item commit is an implementation/test checkpoint awaiting Claude's independent
review; this file does not claim final technical completion or manual acceptance.
