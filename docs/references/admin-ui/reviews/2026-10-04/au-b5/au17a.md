# AU17a — retained Playing attribution

Implemented backend only; independent Claude review and RC07 binding pending.
Authority: supplied brief 30 and [D11 option b](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b5-brief-decisions). Baseline `fdc73dcc5605e582c11866947bd54cc54fc7a4e7`.

Correction resolves one participant across retained event assignments, permits current/released Playing identities of current/former members of the submission’s own team, and excludes Informational/ambiguous/non-member/non-event identities. The service and authorized `CorrectionCharacters` GET expose released/left/current markers. Existing current-page markup is unchanged. Destination validation, event/submission serialization, reason, expected version, immutable evidence and atomic audit/history remain in place.

Public progress explicitly retains approved contributors absent from the published roster; roster entries and roster count remain unchanged. The PostgreSQL case carries the identity through per-player progress, leaderboard, drop-EHB output, Final Review readiness, official placements and published results.

Commands (assigned worktree):
- `dotnet build Bingo.slnx --no-restore -c Release -v quiet`: PASS, 0 warnings/errors (initial production implementation).
- `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5Correction|FullyQualifiedName~ReleasedPlayingAssignmentCanBeUsed' -v minimal`: first invocation FAILED to compile new test assertions (property name and xUnit analyzer); corrected. Subsequent 6/6 PASS, then final added stale-role proof 7/7 PASS, 0 skipped, isolated PostgreSQL 17 containers. Release compilation passed in this final test command.
- `git diff --check`: PASS.

The seven cases prove retained reassignment, released/former-member correction and result mapping, Informational/never-member/ambiguous/outside-event rejection with unchanged persisted evidence/history/audit/event state, and a role change after picker load. The valid case also refuses non-admin picker access. Fixture instants are deterministic microsecond-aligned UTC. Full affected classes and final Release build remain the batch-end gate.

Reference check: Review.dc.html correction picker (`acctOpts`, Playing accounts on this team) has no released/left markers. AU17a requires an actual register row in item 5. No reference edits or UI/manual acceptance claimed.

Batch-end result and saved correction status: [item-5 gate evidence](docs-register.md). The full gate failed; this focused result is not a final batch pass.
