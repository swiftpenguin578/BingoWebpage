# Item 3 — outside-grid approval safety

Authority: brief38 item3; review36 C1-c / 36c F4; AU19 in
`review-notes/08-decisions.md`, “AU phase plan”.

Approval collects a `board-positions` issue for each tile with a row or column
outside the configured grid (including negative coordinates). It retains the
existing conflicting-position message and identifies the tile and linear position.
No normal create/move/resize behavior changes.

New PostgreSQL theory cases retain the full valid 1×1 grid and add an extra tile
at row1/column0 or row0/column1. Each asserts one exact code, tile identity,
position, name, resource key and empty arguments, plus unchanged persistent state
and no approval tree. No existing test or assertion changed.

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5Round2ApprovalRefusesTileOutsideFullGrid' --logger 'trx;LogFileName=item3.trx' --results-directory /tmp/au-b5-remediation-2 -v minimal
git diff --check
```

PASS: Integration2, 0 failed, 0 skipped; no compiler warnings/errors; diff check
passed. No test development failures. Isolated Testcontainers PostgreSQL only.
Independent Claude recheck/binding and the final batch gate remain pending.
