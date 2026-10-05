# Item 2 — preserve collected approval issues

Authority: brief38 item2; review36 C1-b / 36c F2; AU19 in
`review-notes/08-decisions.md`, “AU phase plan”.

Before refusing for evidence locks, reused objective identity, changed eligible
sources or stale catalogue, approval returns already-collected issues first.
The unavailable-prior-publication refusal has the same protection. Guards are
inside those refusal branches: when no such refusal applies, later per-tile
validation still collects every issue. This preserves the existing multi-tile
collection and first-message order without introducing writes.

New real PostgreSQL page and JSON cases combine an empty position with a
catalogue update after page load. Both return the exact `board-incomplete` issue
(position1, no tile ID); the page asserts the full historical message exactly.
Both compare persisted state and approval trees before/after. Existing tests and
assertions are unchanged. The existing four-issue aggregation proof is reused in
this focused run to detect accidental early termination of collection.

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5Round2EmptyPositionPrecedesStaleCatalogue|FullyQualifiedName~B5RemediationBoardCollectsDifferentTileIssues' --logger 'trx;LogFileName=item2.trx' --results-directory /tmp/au-b5-remediation-2 -v minimal
git diff --check
```

PASS: Integration3, 0 failed, 0 skipped; compilation without warnings/errors;
diff check passed. No test development failures. Controlled Testcontainers only.
Independent Claude recheck and binding pending; final batch gate not yet run.
