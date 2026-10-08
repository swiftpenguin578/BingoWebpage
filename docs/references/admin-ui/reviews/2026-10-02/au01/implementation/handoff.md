# AU01 implementation evidence

Checkout: /Users/christopher/.codex/worktrees/participants-functionality/BingoWebpage
Branch: codex/participants-functionality
HEAD: 0ec8add9314a65ab66751bf44bbb4f5195ff854f
Role: implementer; independent review has not run.

## Implemented

Restore locks the existing current-event advisory boundary before locking/loading the event. A hidden current-state event is rejected with InvalidState when another visible current event exists. The shared helper retains the exact Start/Resume Live/AwaitingFinalReview/Finalized definition and development-fixture exclusion. Rejection occurs before mutation, audit or collaboration notification. Archived restoration remains available. Wrapped PostgreSQL serialization/deadlock errors return the existing stale-recovery result. Lifecycle Start/Resume and scheduled boundaries use the extracted helper without changing lock identity. Existing functional/product authority records the approved AU01 rejection rule.

## Stable owned files

- FUNCTIONAL_CONTRACTS.md
- PRODUCT_REQUIREMENTS.md
- src/Bingo.Infrastructure/Events/EventCurrentBoundary.cs
- src/Bingo.Infrastructure/Events/EventLifecycleService.cs
- src/Bingo.Infrastructure/Events/EventQuarantineService.cs
- tests/Bingo.IntegrationTests/EventQuarantineExclusivityIntegrationTests.cs
- tests/Bingo.IntegrationTests/EventQuarantineIntegrationTests.cs

Exact hashes: source.sha256. Scoped complete diff (including new files): au01.patch.

## Executed checks

1. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~EventQuarantineIntegrationTests.Restore|FullyQualifiedName~EventQuarantineIntegrationTests.ArchivedRestore' --logger 'trx;LogFileName=au01-focused.trx' --results-directory /private/tmp/au01-implementation-20261002`
   PASS: 9/9, 0 skipped. Logs: focused.log; au01-focused.trx.
   Three sequential tests hide A, successfully start B, and reject restore A for visible Live/AwaitingFinalReview/Finalized B. Snapshots assert every public event field unchanged on rejection, with no restored audit or notification and retained history.
   One archived-restoration test preserves lifecycle timestamps and restores while B remains current.
   Four race cases queue Restore and Start/Resume in both orders behind the same PostgreSQL advisory lock; pg_locks proves both reach that boundary before release. Exactly one succeeds and persisted state contains one visible current event, correct audit/transition counts, and unchanged hidden data on rejection.
   One authenticated HTTP Manage test uses real login/antiforgery, verifies failure redirect to hidden Manage, renders actionable conflict feedback and Restore control, retains all event data, then succeeds through the same transport after archiving the other event in the controlled fixture.
2. `dotnet test tests/Bingo.IntegrationTests/Bingo.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~EventQuarantineIntegrationTests.MissingQuarantineInputsHaveStableFieldErrorsWithoutMutation|FullyQualifiedName~EventQuarantineIntegrationTests.OnlyActiveSuperAdminCanQuarantineAndStaleRestoreIsRejectedWithoutChangingHistory|FullyQualifiedName~EventQuarantineIntegrationTests.FinalizedManageHideUsesExactHandlerBoundaryAndValidatesBeforeMutation' --logger 'trx;LogFileName=au01-regression.trx' --results-directory /private/tmp/au01-implementation-20261002`
   PASS: 3/3, 0 skipped. Logs: regression.log; au01-regression.trx. Retains active SuperAdmin checks, stale handling, nonconflicting restore/history and exact authenticated handler behavior.
3. `dotnet build Bingo.slnx --configuration Release --no-restore`
   PASS: 0 warnings, 0 errors. Log: release-build.log.
4. `git diff --check`
   PASS. Log: diff-check.log.

The PostgreSQL 17 Testcontainers fixture is disposable, uses deterministic UTC whole-second (microsecond-aligned) timestamps, and was disposed by the test lifecycle. No user database or live provider was used. The race polling waits for actual lock state, not timestamp tolerance. Sandbox Docker access initially failed; explicitly scoped escalation was approved and tests then ran successfully. No auto-review rejection occurred.

## Limits and next action

No full suite, browser/manual acceptance, new UI integration, packaging, staging, commit, push, or deployment. No domain change was needed: the cross-event invariant belongs in the transactional application service, before BingoEvent.Restore mutates metadata. Existing name/reason behavior is unchanged and outside AU01. Only the three lifecycle current states and existing development predicate were extracted; no global lifecycle audit.

Preserved unrelated/concurrent dirty files: AGENTS.md, CURRENT_STATUS.md, DELIVERY_PLAN.md, docs/references/admin-ui/FUNCTIONALITY_CHANGES.md. The reference file was dirty before this worker's first write; never edited by this worker.

Next permitted action: orchestrator dispatches a fresh independent reviewer against these seven stable source files and retained test evidence. Reviewer may reuse passing proof; implementer is available for named remediation. Status/plan updates belong to the orchestrator.
