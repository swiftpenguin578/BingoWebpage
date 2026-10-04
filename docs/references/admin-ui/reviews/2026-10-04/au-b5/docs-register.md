# B5 item 5 — documentation/register and final-gate checkpoint

Four ordered backend commits are followed by a separately human-authorized corrective checkpoint and this final documentation checkpoint (six total; no amend/rewrite). Independent Claude review and all RC/UI binding remain pending. No migration, UI change, live provider request, user database access, push, merge or deployment occurred.

## Authority and scope

The user-supplied [brief 30](/Users/christopher/Documents/BingoWebpage/review-notes/30-codex-brief-b5-readback-review.md) and [08-decisions.md](/Users/christopher/Documents/BingoWebpage/review-notes/08-decisions.md#b5-brief-decisions) retain authorship and approval attribution. D11 option b is the user-decided Playing-only pool. B4 baseline acceptance comes from the brief and [appended review 29 PASS](/Users/christopher/Documents/BingoWebpage/review-notes/29-b4-remediation-recheck.md#follow-up-recheck-2049fb2fdc73dc-direct-claude-4-october); B5 does not claim a new independent review or manual acceptance.

Owning AU14/AU17a/AU17/AU19 queue, acceptance, product/contract/architecture and functionality records now describe delivered backend contracts with pending binding. CURRENT_STATUS retains B4 acceptance, B3/R1/R3 and conditional migration release gates. UI_PAGE_MATRIX remains unchanged and owns page acceptance.

Reference register reconciliation:

- AU17a: actual absent-reference released/former Playing picker entries and Released/Left team/Current markers are registered; Informational remains excluded.
- AU17: existing BR-1 row now explicitly replaces a numerical contribution claim with structured blocker ID/upload time.
- AU19: the returned issue codes/targets and full-intent comparison data support the existing reference issue/go/preview/verification surfaces; no extra visible content was introduced, so no artificial row was added. Existing AU11 explicit-override row and equal-value snapshot limitation remain.
- AU14: register now covers immutable identities/full intent, reused pick numbers, same-order redraw, unavailable/new-creation identity uncertainty, and truthful separation of local roster publication from last recorded WOM states.

## Commits and evidence

Baseline: `fdc73dcc5605e582c11866947bd54cc54fc7a4e7`.

1. AU17a `ebd0ef2c8c559fa47eb2b691ddb461a214a2e449` — [item evidence](au17a.md), focused PostgreSQL 7/7 PASS.
2. AU17 `397d4c96538004731652fe9948babc19c4ff8f5a` — [item evidence](au17.md), focused PostgreSQL 16/16 PASS.
3. AU19 `992f6b1dc10fffea0aa4a65d93f6389814e7db2c` — [item evidence](au19.md), focused PostgreSQL 62/62 PASS.
4. AU14 `d3f2d886d6e60bb87b87765de355165901776d96` — [item evidence](au14.md), focused PostgreSQL 7/7 PASS.
5. Approved corrective checkpoint `e63546149dbd400a257d063d4dfbdc07bc72c703` — [scope, approval relayed by /root and final checks](corrections.md), 94/94 PASS.
6. This final documentation/register/status checkpoint; exact SHA and final range are reported after commit.

Final review range includes item 1: `fdc73dcc5605e582c11866947bd54cc54fc7a4e7..HEAD` at the sixth checkpoint. The terminal delivery report provides its exact ending SHA.

## Required full-class gate — FAIL

Executed in the assigned worktree against the four-commit assembly, before saved corrections were rebuilt:

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --no-restore -c Release --filter 'FullyQualifiedName~SubmissionWorkflowTests|FullyQualifiedName~Slice6CatalogueAdministrationIntegrationTests|FullyQualifiedName~DraftOperationsIntegrationTests|FullyQualifiedName~DraftStartReadinessIntegrationTests|FullyQualifiedName~Slice3DraftStartIntegrationTests|FullyQualifiedName~C20ObjectiveIdentityIntegrationTests|FullyQualifiedName~C11FinalizedRosterIntegrationTests|FullyQualifiedName~C33FinalizationFreshnessTests|FullyQualifiedName~Slice3FinalizationAtomicityIntegrationTests|FullyQualifiedName~ResultsPublicationIntegrationTests|FullyQualifiedName~BoardEstimateFreshnessMigrationTests|FullyQualifiedName~Slice10Pass102CompetitionSynchronizationTests|FullyQualifiedName~CataloguePopulationMigrationIntegrationTests' --logger 'trx;LogFileName=au-b5-final.trx' --results-directory /tmp/au-b5-final -v minimal
```

Exit 1: **662 PASS / 3 FAIL / 0 skipped, 665 total**, duration 12m11s. Controlled Testcontainers PostgreSQL. TRX: `/tmp/au-b5-final/au-b5-final.trx`.

| Full class | Passed | Failed |
| --- | ---: | ---: |
| SubmissionWorkflowTests | 91 | 1 |
| Slice6CatalogueAdministrationIntegrationTests | 177 | 0 |
| DraftOperationsIntegrationTests | 64 | 0 |
| DraftStartReadinessIntegrationTests | 12 | 0 |
| Slice3DraftStartIntegrationTests | 2 | 0 |
| C20ObjectiveIdentityIntegrationTests | 46 | 0 |
| C11FinalizedRosterIntegrationTests | 58 | 1 |
| C33FinalizationFreshnessTests | 29 | 0 |
| Slice3FinalizationAtomicityIntegrationTests | 9 | 0 |
| ResultsPublicationIntegrationTests | 1 | 0 |
| BoardEstimateFreshnessMigrationTests | 1 | 0 |
| Slice10Pass102CompetitionSynchronizationTests | 167 | 1 |
| CataloguePopulationMigrationIntegrationTests | 5 | 0 |

Failures:

1. `PrivateEvidenceMutationsDoNotAnnouncePublicProgressUntilApprovalOrReversal`, SubmissionWorkflowTests:404: B5 ReviewAction ordering added Version to shared snapshots, also exposing it in Audit snapshots against the existing redaction assertion. Saved fix strips Version only when building the existing Audit entry, preserving ReviewAction ordering metadata and the unchanged test.
2. `StaleRolePostAfterWithdrawalOrActualStartCannotPublishOrChangeRoles(startInstead: True)`, C11FinalizedRosterIntegrationTests:901: expected no persisted state change after start, actual role change modifies state. Command/test are unchanged by B5; current product permits live current-member role corrections. Reproduces before B5. No lifecycle permission/test assertion changed; planner must route this conflict.
3. `StatsPass4RealFinalReviewRefreshFailureDoesNotBlockPublicationAndIsRecorded`, StatsPass4ReviewCorrectionsIntegrationTests:206: expected the controlled provider failure in publication feedback, but the fixture ended early after AU20 began changing the configured WOM end. The resulting unmatched end correctly suppresses the final provider fetch (WA-2 item 5), so the injected failure was never reached. This is a fixture-path failure, not safe-feedback redaction. It reproduced before B5; B5 made no WOM/finalization feedback/test assertion change. The later authorized test-health batch records its setup correction and checks in [test-health/evidence.md](../test-health/evidence.md).

The two inherited failures remain **failures**, not accepted fixture limitations or waived gates.

## Exact-baseline reproduction — FAIL

Created an isolated source copy with `git archive fdc73dcc5605e582c11866947bd54cc54fc7a4e7` under `/tmp/au-b5-baseline-fdc73dc`, without switching or altering the assigned checkout. Initial extraction hit this machine's older Python tarfile API; repeated with explicitly validated archive paths, then built the exact archived source.

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj -c Release --filter 'FullyQualifiedName~StaleRolePostAfterWithdrawalOrActualStartCannotPublishOrChangeRoles|FullyQualifiedName~StatsPass4RealFinalReviewRefreshFailureDoesNotBlockPublicationAndIsRecorded' --logger 'trx;LogFileName=au-b5-baseline.trx' --results-directory /tmp/au-b5-baseline-results -v minimal
```

Exit 1: **1 PASS / 2 FAIL / 0 skipped, 3 total**, duration 17s, disposable PostgreSQL. The same two failures above, same assertion lines; withdrawal case passes. TRX: `/tmp/au-b5-baseline-results/au-b5-baseline.trx`.

## Initial B5 correction checks — FAIL, then human-authorized resolution

Besides the Audit snapshot fix, new Board/Teams readbacks now retain existing Admin-or-SuperAdmin authorization parity; explicit SuperAdmin success assertions precede disabled-account refusal. These are included in the separately authorized corrective commit. Existing current-page commands and lifecycle rules remain unchanged.

```sh
dotnet build Bingo.slnx --configuration Release --no-restore -t:Rebuild
```

PASS: 0 warnings, 0 errors, clean rebuild, 26.57s.

```sh
dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --no-restore -c Release --filter 'FullyQualifiedName~SubmissionWorkflowTests|FullyQualifiedName~B5BoardReadbackFailureIsUnknownAndUnauthorizedReadIsRefused|FullyQualifiedName~B5DraftReadbackIncludesInclusionOnlyTeamEditsAndConfirmedCaptainEligibility' --logger 'trx;LogFileName=au-b5-corrections.trx' --results-directory /tmp/au-b5-corrections -v minimal
```

Exit 1: **93 PASS / 1 FAIL / 0 skipped, 94 total**, duration 2m38s. The full Review class is 91 PASS / 1 FAIL; the two endpoint authorization tests PASS, including new SuperAdmin success assertions. The original private-evidence Audit-Version redaction assertion now PASSes. The new failure is `ApprovalCapsContributionAndReversalRebalancesLaterApprovedEvidence(failChildAudit: False)`, line 749: the existing whole-JSON equality assertion compares Audit with ReviewAction, whose new Version field is intentionally retained only in ReviewAction. The failure-injection variant passes; all other full Review cases pass. TRX: `/tmp/au-b5-corrections/au-b5-corrections.trx`.

Proposed named assertion update would assert exact ReviewAction before/after versions against the persisted child version, assert Version absent from Audit, then compare every remaining behavior field exactly. **Automatic approval review rejected that edit before execution**, stating: “The action edits an existing integration test after a failure to relax its snapshot comparison, risking concealment of a regression; the task authorizes reporting failures, not weakening test assertions.” No test change occurred, no alternate execution or retry was attempted. The worker stopped without a workaround. Subsequently the human explicitly approved this exact assertion change and one additional corrective commit, replying “I approve” in planner chat `01a10660-cc8a-7843-abb4-6cc1ebbb2bf2`. The precise approved change was then applied; see [corrections.md](corrections.md) for its final execution results. The approval does not waive either inherited failure.

## Final approved correction checks — PASS; overall gate still FAIL

The exact approved assertion was applied in corrective commit `e63546149dbd400a257d063d4dfbdc07bc72c703`. [corrections.md](corrections.md) records the exact command: same full Review plus two endpoint filter, logger `au-b5-approved-corrections.trx`, results directory `/tmp/au-b5-approved-corrections`. Result: **94 PASS / 0 FAIL / 0 skipped**, duration 2m36s: full Review 92/92, affected Board/Teams authorization cases 2/2. The clean Release rebuild after the assertion change passed with **0 warnings/errors**, 24.15s.

Unaffected full-class passing evidence is retained. The two baseline-reproduced failures remain unresolved and unwaived; there is **no passing full-gate run or technical acceptance claim**.

`git diff --check`: PASS after corrections and documentation; final terminal status and complete commit IDs are recorded in the delivery report. No all-gate pass or technical acceptance is claimed.

## Next owner

Planner `/root` owns separate routing of the two inherited failures and external Claude review of implemented B5. The user approved only the exact assertion correction and sixth-commit packaging, not inherited-failure repairs or waivers. No new assignment, independent reviewer, UI binding, release rehearsal, push, merge or deployment was launched. All B5 execution evidence is implementer-reported, and overall required gates remain failing on the two inherited cases.
