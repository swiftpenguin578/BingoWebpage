# Item 4 — C1 Board validation issues

Historical environment stop (subsequently resumed below). Brief35 section 3 requires stopping
when the sandbox cannot complete PostgreSQL checks. No retry or environment
workaround was attempted.

Current implementation aggregates empty positions and per-tile problems, supplies
structured approval/publication refusal issues, distinguishes already-started and
end-passed publication codes, and retains the first existing approval page message.
Publication GP issues are per tile. The latter publication-price and transaction
setup edits occurred after the test process compiled and before its environment
failure was observed; they have not been compiled or executed yet.

Existing expectation change: `B5BoardApprovalIssuesCarryStableCodeAndTileOrBoardTarget`
empty-position case `Position == null` → `Position == 1`. Recorded behavior
authority is `review-notes/08-decisions.md`, “AU phase plan”, Decision 1
AU19: “structured approval issues that jump to the affected tile”. Brief35
item4 applies that decision to each empty position. The fixture is a 1×2 board with only position 0
occupied. The new exact assertion passed, as did the new proof checking both empty
positions 2/3 and missing catalogue/manual estimates at positions 0/1. Every other
existing assertion is unchanged. No cases removed/skipped.

Commands:

```
dotnet build src/Bingo.Web/Bingo.Web.csproj --no-restore -c Release -v minimal
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationBoard|FullyQualifiedName~B5BoardApprovalIssues|FullyQualifiedName~BoardApprovalBatchReturnsLocalizedSafeValidationWithoutResidue' --logger 'trx;LogFileName=item4.trx' --results-directory /tmp/au-b5-remediation -v minimal
git diff --check
```

- Earlier Web build: PASS, 0 warnings/errors, before the final dirty publication
  price/transaction-setup changes described above.
- Focused test execution: **FAIL: 14 passed, 1 failed, 0 skipped**.
- Exact failing case:
  `Slice6CatalogueAdministrationIntegrationTests.BoardApprovalBatchReturnsLocalizedSafeValidationWithoutResidue(failure: "identity", ...)`.
- Failure occurs before the test body in `InitializeAsync` line 58, EF
  `NpgsqlMigrator.MigrateAsync`: `Npgsql.PostgresException: 28P01: password
  authentication failed for user "bingo"`.
- The controlled PostgreSQL fixture was used; no production/user DB or live WOM.
  No credentials/configuration changed. Environment cause is not a waived test
  failure. TRX retained at `/tmp/au-b5-remediation/item4.trx`.
- The run finished exit 1. A stop signal was sent after the failure was observed;
  the tool returned its completed 15-case result. No test process remains running.
- Diff check passes at the stopped checkpoint.

Remaining item 4 work: finish coverage of publication lifecycle/refusal paths,
verify every structured refusal/per-tile issue path, execute the latest changes,
record completed evidence and create commit 4. Items 5–8 and final gates have not
started. No independent review performed. The multi-issue list exists in Board.dc.html lines866–880/1186/1439.
Brief38 item6 corrects the original gap assessment: AU19 needs a register row for
empty-issue grouping, absent per-drop rate notes and first-only publish reasons.

## User-terminal continuation

The user ran the identical focused filter against the preserved working tree,
including the latest publication-price/transaction-setup edits, using
`--logger 'trx;LogFileName=item4-user.trx' --results-directory /tmp/au-b5-remediation-user`.
User-reported result: **15 passed, 0 failed, 0 skipped**, duration 41.5 seconds;
build succeeded in 54.2 seconds. This is user-executed evidence, not a sandbox pass.
The earlier 28P01 failure remains recorded; its precise cause is not established.
The planner authorized resumption from this checkpoint; the identical check was
not repeated. Additional publication refusal cases are being checked separately.

## Completed continuation

Item 4 backend remediation is implemented; Claude recheck and binding pending.
The user-run 15/15 proof is reused. The implementer subsequently executed 11 new
publication/per-tile-price cases: **11 passed, 0 failed, 0 skipped**. They cover
missing board, confirmation, conflict, unpublished roster, already-started event,
passed end, unavailable approval, tile-targeted missing GP, ordinary refusal and
generic failure. Every publication refusal compares Boards/Events/Audits/Approvals/
Prices from a fresh context exactly before/after. Two missing-price tiles each
return their own position and identity. No production edits followed the user-run
proof; subsequent changes were new tests and this evidence.

```
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore -c Release --filter 'FullyQualifiedName~B5RemediationPublicationRefusals|FullyQualifiedName~B5RemediationApprovalMissingPrices' --logger 'trx;LogFileName=item4-publication-corrected.trx' --results-directory /tmp/au-b5-remediation -v minimal
git diff --check
```

New-test development failures: compilation CS1061 for EventItemPrice.Id (corrected
to its composite EventId/ItemId ordering); first execution 9 passed/2 failed because
the price setup queried Single over the migrated catalogue instead of fixture item
ID. Only those setup lookups changed, assertions retained. Corrected group passes.
Diff check passes. Final whole-suite gate remains pending on item 8's final commit.
