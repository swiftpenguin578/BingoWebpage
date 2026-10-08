# S8 — direct roster setup end boundary

Status: server part complete in brief40; RC04/DRF binding remains pending.

Authority: brief40 S8, `review-notes/08-decisions.md` S8 option (a), and
`DraftModel.CanDirectPreEventRosterMutation`. A missing configured end is now
allowed before actual start; a configured past end is refused. State, actual
start, draft, team-formation, and other roster conditions remain unchanged.
The five action messages used by this predicate now name both the event start
and configured end; the existing Danish resource is keyed for the corrected
messages.

Implementation commit: `610926e8f60a7fe521c3d6e7b3474ae39c179973`.

Evidence:

- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~DraftOperationsIntegrationTests.DirectRosterSetupAllowsNoEndAndRejectsPastEndOrActualStart' --logger 'trx;LogFileName=/tmp/bingo-brief40-s8.trx'` — **failed** because the initial actual-start fixture was still in Draft, where the domain correctly refuses `StartEvent`.
- The same command with `--logger 'trx;LogFileName=/tmp/bingo-brief40-s8-rerun.trx'` after using the seeded SignupClosed lifecycle for the actual-start case — **passed 1, failed 0, skipped 0** against Testcontainers PostgreSQL. It proves no-end team and member creation, past-end team refusal with the corrected message and no roster/audit residue, and actual-start member refusal with the corrected message and no residue.

Test-change mapping: this is a new focused proof; no existing expectation was
changed or weakened. The first failed run was a lifecycle-fixture correction.
