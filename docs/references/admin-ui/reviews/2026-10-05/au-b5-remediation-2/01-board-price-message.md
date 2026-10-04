# Item 1 — board-wide missing-GP page message

Authority: brief38 item1 and review36 C1-a / 36c F1; AU19 in
`review-notes/08-decisions.md`, “AU phase plan”. Clean baseline verified at
`5676b1d5b3035838eb349608f6596a4de698db10` on the assigned branch.

Approval carries separate page arguments computed by the same board-wide missing
price query used before `c271ca0`; tile issue arguments/positions stay separate.
The new PostgreSQL test calls the actual page handler with two distinct unpriced
items on two tiles, checks the exact historical English message and both exact
issue targets/arguments, and compares persisted state before/after refusal.
Existing expectations are unchanged; no removed assertions, cases or skips.

Executed in the assigned worktree:

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5Round2ApprovalPageListsEveryUnpricedItemExactly|FullyQualifiedName~B5RemediationApprovalMissingPrices' --logger 'trx;LogFileName=item1.trx' --results-directory /tmp/au-b5-remediation-2 -v minimal
git diff --check
```

PASS: Integration 2 passed, 0 failed, 0 skipped; Release compilation emitted no
warnings/errors. Isolated Testcontainers PostgreSQL; no user database or WOM.
Diff check passed. No test development failures. An optional process-status read
(`ps`) was denied by the sandbox; it was not retried and did not affect execution.
Independent Claude recheck and binding remain pending; final batch gate follows
item6's commit. Claude's separate Integration run on `a4f8463` remains unreported.
