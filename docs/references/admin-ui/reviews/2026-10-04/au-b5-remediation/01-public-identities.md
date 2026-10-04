# Item 1 — A1 public cross-team credit

Implemented for external Claude recheck; not independently reviewed or accepted.
Authority: supplied `review-notes/35-codex-brief-b5-remediation.md` item 1;
`review-notes/08-decisions.md`, B5 review decisions D13 (line 174), permits
extra rows in existing public displays while preserving roster/counts.

EventMastheadModel keys both activity and Drop-EHB by (team, participant), and
unions retained credit into its display rows. It does not copy activity between
teams or change the published roster. Consumer inspection covered
PublicBoardService, PublicStatsService, Board/Stats page models and Board/TeamBoard
markup for DropEhbTeams, Progress.Players, PlayerLeaderboard and RosterPlayers.
Their numeric projections already operate per team; no further correction was needed.

New real-PostgreSQL test retains one player's approved credit on Team A while
placing them on Team B's published roster and approving credit there too. Both
public pages return HTTP 200 and contain both team identities. Exact per-team EHB,
unchanged roster counts, finalization readiness, two official placements and both
pages after publication are asserted. No existing test or assertion changed.

Commands (assigned worktree; Release):

```
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationCrossTeamCreditRendersBoardStatsAndPublishedPlacements' --logger 'trx;LogFileName=item1-attempt7.trx' --results-directory /tmp/au-b5-remediation -v minimal
dotnet test tests/Bingo.BrowserTests/Bingo.BrowserTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~EventMastheadModelTests' --logger 'trx;LogFileName=item1-masthead.trx' --results-directory /tmp/au-b5-remediation -v minimal
git diff --check
```

Results: Integration 1 passed / 0 failed / 0 skipped; Browser 4 passed / 0 failed /
0 skipped. Diff check passed. Final whole-suite gate remains pending on item 8's
final commit.

Development failures are retained as failures: first compilation stopped on four
CA1861 new-test constant-array diagnostics (fixed with static readonly expected
arrays). Subsequent executions each failed one new-test assertion: Team A was
absent from the fixture's published roster; the zero-credit captain was initially
counted in the contributor-only leaderboard; the DOM selector included nested
team rows; a decimal string assertion assumed scale zero; Stats correctly returned
404 until the fixture set the event's BoardPublished flag. Corrections completed
the public fixture and made the new assertions target exact semantic values.
The final proof passes without production changes beyond EventMastheadModel.
